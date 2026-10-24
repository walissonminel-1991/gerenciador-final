using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Diagnóstico independente e somente leitura; NÃO remove pacotes do Driver Store,
// dispositivos, instaladores MSI, chaves do Registro ou documentos do usuário.
internal sealed record MaintenanceConcern(string Category, string Name, string Description);
internal sealed record DeviceAudit(int Disconnected, int WithProblems,
    int Queried, bool Supported, bool Partial, IReadOnlyList<string> Categories,
    IReadOnlySet<string>? PresentIds = null);
internal sealed record MaintenanceReport(int Installed, int Recognized,
    int UsableUninstallers, int MissingUninstallers, int UnverifiableUninstallers,
    int CandidatesForInvestigation, DeviceAudit Devices,
    IReadOnlyList<MaintenanceConcern> Concerns)
{
    // Separar inventário real do Windows de reconhecimento de receitas específicas.
    public int WithPublisher { get; init; }
    public int WithVersion { get; init; }
    public bool InventoryIncomplete { get; init; }
    public RegistryDeviceEvidence? HistoricalPnP { get; init; }
    public bool Incomplete => InventoryIncomplete || Devices.Partial ||
        (Devices.Supported && (HistoricalPnP is null || HistoricalPnP.Partial));
}

internal static class MaintenanceAudit
{
    private const int MaxExamples = 12;
    private const int MaxDevices = 3500;
    // Don't infer that an MSI entry or browser redirect is invalid based on a
    // missing user-facing installer filename; never launch anything to validate.
    internal static string CheckUninstaller(string? command, bool inventoryOnly)
    {
        if (inventoryOnly) return "protected";
        if (string.IsNullOrWhiteSpace(command)) return "absent";
        if (!UninstallCommand.TryParse(command, out var parsed)) return "unknown";
        string filename = parsed.FileName;
        if (filename.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase) ||
            filename.Equals("msiexec", StringComparison.OrdinalIgnoreCase)) return "available";
        try
        {
            if (!Path.IsPathFullyQualified(filename)) return "unknown";
            return File.Exists(Path.GetFullPath(filename)) ? "available" : "missing";
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or
            UnauthorizedAccessException or NotSupportedException) { return "unknown"; }
    }

    internal static (int Installed, int Recognized, int Available, int Missing,
        int Unverifiable, IReadOnlyList<MaintenanceConcern> Concerns)
        InspectPrograms(IReadOnlyCollection<ProgramEntry> apps)
    {
        int catalog = 0, good = 0, missing = 0, unknown = 0;
        List<MaintenanceConcern> concerns = new();
        foreach (var app in apps)
        {
            if (!string.IsNullOrWhiteSpace(app.CatalogIdentity)) catalog++;
            string state = CheckUninstallers(app);
            if (state == "available") good++;
            else if (state == "missing")
            {
                missing++;
                if (concerns.Count < MaxExamples)
                    concerns.Add(new("Desinstalação", app.Name,
                        "O arquivo de desinstalação registrado não foi encontrado. " +
                        "Confirme a unidade e o fabricante; não remova o Registro manualmente."));
            }
            else if (state is "unknown" or "absent")
                unknown++;
        }
        return (apps.Count, catalog, good, missing, unknown, concerns);
    }

    internal static string CheckUninstallers(ProgramEntry app)
    {
        string normal = CheckUninstaller(app.UninstallString, app.InventoryOnly);
        string quiet = CheckUninstaller(app.QuietUninstallString, app.InventoryOnly);
        if (normal == "protected" || quiet == "protected") return "protected";
        if (normal == "available" || quiet == "available") return "available";
        if (normal == "unknown" || quiet == "unknown") return "unknown";
        if (normal == "missing" || quiet == "missing") return "missing";
        return "absent";
    }

    internal static bool ReviewDisconnectedDevice(bool? present, string? deviceClass,
        string? hardwareId, string? name)
    {
        if (present != false || string.IsNullOrWhiteSpace(name)) return false;
        string category = deviceClass?.Trim() ?? "";
        string id = hardwareId?.Trim() ?? "";
        // Don't call ROOT/SW/virtual devices orphaned hardware merely because
        // Windows reports them as not present.
        if (id.StartsWith("ROOT\\", StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith("SWD\\", StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith("SW\\", StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith("HTREE\\", StringComparison.OrdinalIgnoreCase))
            return false;
        if (category.Equals("System", StringComparison.OrdinalIgnoreCase) ||
            category.Equals("SoftwareDevice", StringComparison.OrdinalIgnoreCase) ||
            category.Equals("Firmware", StringComparison.OrdinalIgnoreCase))
            return false;
        return id.Length is > 5 and <= 500;
    }

    private static DeviceAudit Devices()
    {
        var locatorType = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
        if (locatorType is null) return new(0, 0, 0, false, true, []);
        object? locator = null, services = null, result = null;
        int count = 0, disconnected = 0, errors = 0;
        bool partial = false;
        Dictionary<string, int> categories = new(StringComparer.OrdinalIgnoreCase);
        var activeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            locator = Activator.CreateInstance(locatorType);
            dynamic client = locator!;
            services = client.ConnectServer(".", @"root\cimv2");
            dynamic query = services;
            result = query.ExecQuery(
                "SELECT Name, PNPClass, PNPDeviceID, Present, ConfigManagerErrorCode " +
                "FROM Win32_PnPEntity");
            // COM/WMI only reads a snapshot. This is deliberately not PnPUtil /delete-driver.
            foreach (object item in (System.Collections.IEnumerable)result)
            {
                if (++count > MaxDevices) { partial = true; break; }
                try
                {
                    dynamic device = item;
                    object? Get(string prop)
                    {
                        try { return device.Properties_.Item(prop).Value; }
                        catch { partial = true; return null; }
                    }
                    string name = Convert.ToString(Get("Name"), CultureInfo.CurrentCulture) ?? "";
                    string category = Convert.ToString(Get("PNPClass"), CultureInfo.CurrentCulture) ?? "";
                    string id = Convert.ToString(Get("PNPDeviceID"), CultureInfo.CurrentCulture) ?? "";
                    object? rawPresent = Get("Present");
                    bool? present = rawPresent is bool b ? b : null;
                    if (present is null || string.IsNullOrWhiteSpace(id)) partial = true;
                    if (present == true && id.Length >= 6) activeIds.Add(id);
                    if (ReviewDisconnectedDevice(present, category, id, name))
                    {
                        disconnected++;
                        if (categories.Count < 32 || categories.ContainsKey(category))
                            categories[category] = categories.GetValueOrDefault(category) + 1;
                    }
                    int error = 0;
                    try { error = Convert.ToInt32(Get("ConfigManagerErrorCode") ?? 0); }
                    catch (Exception) { partial = true; }
                    if (present == true && error != 0 && error != 22)
                        errors++; // disabled is a choice, not proof of malfunction
                }
                finally
                {
                    if (Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item);
                }
            }
            return new(disconnected, errors, Math.Min(count, MaxDevices), true, partial,
                categories.OrderByDescending(kv => kv.Value)
                    .Take(8).Select(kv => $"{kv.Key}: {kv.Value}").ToArray(),
                activeIds);
        }
        catch (Exception)
        {
            // Access to WMI may be disabled by policy; not a failure to fix.
            return new(disconnected, errors, Math.Min(count, MaxDevices), false, true, []);
        }
        finally
        {
            if (result is not null && Marshal.IsComObject(result)) Marshal.FinalReleaseComObject(result);
            if (services is not null && Marshal.IsComObject(services)) Marshal.FinalReleaseComObject(services);
            if (locator is not null && Marshal.IsComObject(locator)) Marshal.FinalReleaseComObject(locator);
        }
    }

    internal static MaintenanceReport Inspect(IReadOnlyCollection<ProgramEntry> apps,
        bool inspectDevices = true, bool inventoryIncomplete = false)
    {
        var inventory = InspectPrograms(apps);
        DeviceAudit device = inspectDevices ? Devices() : new(0, 0, 0, false, false, []);
        // Comparar instâncias antigas só com snapshot WMI utilizável; se WMI
        // não estiver acessível, não rotular todas as instâncias como ausentes.
        RegistryDeviceEvidence? historical = inspectDevices && device.Supported &&
            !device.Partial && device.PresentIds is { Count: > 0 }
            ? HistoricalDeviceAudit.Inspect(device.PresentIds)
            : null;
        var findings = inventory.Concerns.ToList();
        if (inventoryIncomplete)
            findings.Add(new("Inventário", "Consulta parcial de aplicativos",
                "Uma ou mais fontes de aplicativos não puderam ser lidas por completo. " +
                "Ausência nesta consulta não comprova desinstalação."));
        if (device.Disconnected > 0)
            findings.Add(new("Hardware", "Dispositivos desconectados",
                $"{device.Disconnected} registros PnP desconectados (não significa driver inútil). " +
                "Não remover até confirmar a identidade e dependências."));
        if (device.WithProblems > 0)
            findings.Add(new("Hardware", "Dispositivos com problema",
                $"{device.WithProblems} dispositivos atualmente presentes têm códigos de problema no Windows."));
        if (historical is { NotPresent: > 0 })
            findings.Add(new("Hardware", "Instâncias PnP não confirmadas",
                $"{historical.NotPresent} instâncias PCI/USB/USBSTOR do Registro não foram " +
                "confirmadas como presentes no retrato atual do Windows. " +
                "Esse valor não é o total de dispositivos desconectados, " +
                "nem identifica pacotes de drivers obsoletos. Não remova nada."));
        if (inspectDevices && !device.Supported)
            findings.Add(new("Hardware", "Consulta parcial",
                "O Windows não disponibilizou a informação completa dos dispositivos PnP."));
        if (historical is { Partial: true })
            findings.Add(new("Hardware", "Registro parcialmente acessível",
                "Algumas instâncias históricas do Registro não puderam ser comparadas."));
        return new(inventory.Installed, inventory.Recognized, inventory.Available,
            inventory.Missing, inventory.Unverifiable,
            inventory.Missing + device.WithProblems, device, findings.Take(16).ToArray())
        {
            WithPublisher = apps.Count(a => !string.IsNullOrWhiteSpace(a.Publisher)),
            WithVersion = apps.Count(a => !string.IsNullOrWhiteSpace(a.Version)),
            InventoryIncomplete = inventoryIncomplete,
            HistoricalPnP = historical
        };
    }
}
