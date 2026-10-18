using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Diagnóstico independente e somente leitura; NÃO remove pacotes do Driver Store,
// dispositivos, instaladores MSI, chaves do Registro ou documentos do usuário.
internal sealed record MaintenanceConcern(string Category, string Name, string Description);
internal sealed record DeviceAudit(int Disconnected, int WithProblems,
    int Queried, bool Supported, bool Partial, IReadOnlyList<string> Categories);
internal sealed record MaintenanceReport(int Installed, int Recognized,
    int UsableUninstallers, int MissingUninstallers, int UnverifiableUninstallers,
    int CandidatesForInvestigation, DeviceAudit Devices,
    IReadOnlyList<MaintenanceConcern> Concerns);

internal static class MaintenanceAudit
{
    private const int MaxExamples = 12;
    private const int MaxDevices = 3500;
    private static readonly HashSet<string> SystemUninstallers = new(StringComparer.OrdinalIgnoreCase)
    { "msiexec.exe", "rundll32.exe", "cmd.exe", "msiexec", "rundll32" };

    private static string Expand(string value) =>
        Environment.ExpandEnvironmentVariables(value).Trim('"', ' ');

    // Don't infer that an MSI entry or browser redirect is invalid based on a
    // missing user-facing installer filename; never launch anything to validate.
    internal static string CheckUninstaller(string? command, bool inventoryOnly)
    {
        if (inventoryOnly) return "protected";
        if (string.IsNullOrWhiteSpace(command)) return "absent";
        string raw = command.Trim();
        // AppServices.TryParseUninstaller is separate from this audit; never
        // reinterpret an arbitrary command or test executable by invoking it.
        string filename;
        if (raw.StartsWith('"'))
        {
            int end = raw.IndexOf('"', 1);
            if (end <= 1) return "unknown";
            filename = raw[1..end];
        }
        else
        {
            var match = Regex.Match(raw, @"^.+?\.exe(?=\s|$)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) return "unknown";
            filename = match.Value;
        }
        filename = Expand(filename);
        if (SystemUninstallers.Contains(filename) ||
            SystemUninstallers.Contains(Path.GetFileName(filename))) return "available";
        if (!Path.IsPathFullyQualified(filename)) return "unknown";
        try
        {
            string complete = Path.GetFullPath(filename);
            if (File.Exists(complete)) return "available";
            // The user may have unmounted a volume or lost permissions. Mark as
            // suspect, but do not remove its uninstall entry automatically.
            return "missing";
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
            string state = CheckUninstaller(
                string.IsNullOrWhiteSpace(app.UninstallString) ? app.QuietUninstallString : app.UninstallString,
                app.InventoryOnly);
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
                        catch { return null; }
                    }
                    string name = Convert.ToString(Get("Name"), CultureInfo.CurrentCulture) ?? "";
                    string category = Convert.ToString(Get("PNPClass"), CultureInfo.CurrentCulture) ?? "";
                    string id = Convert.ToString(Get("PNPDeviceID"), CultureInfo.CurrentCulture) ?? "";
                    object? rawPresent = Get("Present");
                    bool? present = rawPresent is bool b ? b : null;
                    if (ReviewDisconnectedDevice(present, category, id, name))
                    {
                        disconnected++;
                        if (categories.Count < 32 || categories.ContainsKey(category))
                            categories[category] = categories.GetValueOrDefault(category) + 1;
                    }
                    int error = 0;
                    try { error = Convert.ToInt32(Get("ConfigManagerErrorCode") ?? 0); }
                    catch (Exception) { }
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
                    .Take(8).Select(kv => $"{kv.Key}: {kv.Value}").ToArray());
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
        bool inspectDevices = true)
    {
        var inventory = InspectPrograms(apps);
        DeviceAudit device = inspectDevices ? Devices() : new(0, 0, 0, false, false, []);
        var findings = inventory.Concerns.ToList();
        if (device.Disconnected > 0)
            findings.Add(new("Hardware", "Dispositivos desconectados",
                $"{device.Disconnected} registros PnP desconectados (não significa driver inútil). " +
                "Não remover até confirmar a identidade e dependências."));
        if (device.WithProblems > 0)
            findings.Add(new("Hardware", "Dispositivos com problema",
                $"{device.WithProblems} dispositivos atualmente presentes têm códigos de problema no Windows."));
        if (inspectDevices && !device.Supported)
            findings.Add(new("Hardware", "Consulta parcial",
                "O Windows não disponibilizou a informação completa dos dispositivos PnP."));
        return new(inventory.Installed, inventory.Recognized, inventory.Available,
            inventory.Missing, inventory.Unverifiable,
            inventory.Missing + device.WithProblems, device, findings.Take(16).ToArray());
    }
}
