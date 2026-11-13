using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal sealed record DriverInventoryEntry(string DeviceId, string DeviceName, string Provider,
    string Version, string InfName, bool? Signed, bool? Present)
{
    internal DriverStoreEvidence? PackageEvidence { get; init; }
    internal IReadOnlyList<string> HardwareIds { get; init; } = [];
    internal IReadOnlyList<DriverWindowsEvent> WindowsEvents { get; init; } = [];
    // Codigo informado pelo PnP do Windows, não diagnóstico independente de hardware.
    internal int? PnpProblemCode { get; init; }
    internal bool WithoutSignedDriverAssociation { get; init; }
    internal bool HasPnpProblem => PnpProblemCode is > 0;
    internal string PnpStatusLabel => PnpProblemCode switch
    {
        0 => "Sem código de problema PnP informado (não garante saúde)",
        22 => "Dispositivo desativado (PnP 22)",
        28 => "Driver não instalado (PnP 28)",
        > 0 => $"Problema relatado pelo Windows (PnP {PnpProblemCode})",
        _ => "Estado PnP não informado"
    };
    internal string SignatureLabel => Signed switch { true => "Sim (Windows)", false => "Não (Windows)", _ => "Indeterminado" };
    internal string PresenceLabel => Present switch { true => "Presente", false => "Não presente", _ => "Indeterminado" };
}
internal sealed record DriverInventoryResult(IReadOnlyList<DriverInventoryEntry> Items, bool Partial, string Status)
{
    internal DriverStoreAuditResult? Store { get; init; }
    internal DriverPublishedAuditResult? PublishedPackages { get; init; }
    internal DriverWindowsHistoryResult? WindowsHistory { get; init; }
    internal int PnpEntities { get; init; }
    internal int SignedRows { get; init; }
}

// Somente SELECT. Nao enumera todo o Driver Store nem autoriza exclusoes.
// IsSigned e um relato WMI, nao uma nova verificacao criptografica do pacote.
internal static class DriverInventory
{
    internal static bool ValidHardwareId(string? id) => id is { Length: > 2 and <= 4000 } &&
        id == id.Trim() && id.Contains('\\') && !id.Any(char.IsControl) &&
        !id.Contains('*') && !id.Contains('?');
    internal static IReadOnlyList<DriverInventoryEntry> Correlate(IEnumerable<DriverInventoryEntry> drivers,
        IReadOnlyDictionary<string, bool?> presence,
        IReadOnlyDictionary<string, string>? deviceNames = null)
    {
        var indexed = presence.GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Value).Distinct().Count() == 1
                ? g.First().Value : null, StringComparer.OrdinalIgnoreCase);
        var names = deviceNames is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(deviceNames, StringComparer.OrdinalIgnoreCase);
        return drivers.Select(d => d with
            {
                DeviceId = d.DeviceId.Trim(),
                Present = indexed.GetValueOrDefault(d.DeviceId.Trim()),
                // DeviceName pode ser vazio na associação do driver. O nome
                // PnP só é aproveitado quando pertence ao MESMO ID de dispositivo.
                DeviceName = string.IsNullOrWhiteSpace(d.DeviceName)
                    ? names.GetValueOrDefault(d.DeviceId.Trim(), "") : d.DeviceName,
                HardwareIds = d.HardwareIds.Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase).ToArray()
            })
            // Não descartar HardwareIDs divergentes antes de avaliar a ambiguidade.
            .DistinctBy(d => (d.DeviceId.ToUpperInvariant(), d.InfName.ToUpperInvariant(), d.Version,
                d.Provider.ToUpperInvariant(), d.Signed, d.Present, string.Join('\n', d.HardwareIds).ToUpperInvariant()))
            .OrderBy(d => d.DeviceName, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    // Inclui também dispositivos com problema PnP que não tenham linha em
    // Win32_PnPSignedDriver. Um dispositivo sem linha não é driver obsoleto
    // nem candidato a instalação: é apenas evidência que merece investigação.
    internal static DriverInventoryEntry[] IncludeUnboundProblemDevices(
        IEnumerable<DriverInventoryEntry> signedDrivers,
        IReadOnlyDictionary<string, int?> problemCodes,
        IReadOnlyDictionary<string, string> deviceNames,
        IReadOnlyDictionary<string, string[]> hardwareIds)
    {
        var rows = signedDrivers.Select(d => d with
        {
            PnpProblemCode = problemCodes.GetValueOrDefault(d.DeviceId.Trim())
        }).ToList();
        var withDriver = rows.Select(d => d.DeviceId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in problemCodes.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            string id = pair.Key.Trim();
            if (id.Length == 0 || pair.Value is not > 0 || withDriver.Contains(id))
                continue;
            rows.Add(new DriverInventoryEntry(id, deviceNames.GetValueOrDefault(id, ""),
                "", "", "", null, null)
            {
                HardwareIds = hardwareIds.GetValueOrDefault(id) ?? [],
                PnpProblemCode = pair.Value,
                WithoutSignedDriverAssociation = true
            });
            withDriver.Add(id);
        }
        return rows.ToArray();
    }

    // Usar Description apenas quando Name vier vazio e somente no MESMO ID PnP.
    // Nao inferir hardware, compatibilidade, fabricante ou versao.
    internal static string ReportedDeviceName(string? name, string? description) =>
        !string.IsNullOrWhiteSpace(name) ? name.Trim() :
        !string.IsNullOrWhiteSpace(description) ? description.Trim() : "";

    internal static DriverInventoryResult Capture(CancellationToken token)
    {
        var rows = new List<DriverInventoryEntry>();
        var presence = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
        var deviceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hardwareIds = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var problemCodes = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        var conflictedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool partial = false;
        string stage = "conectar";
        object? locator = null, services = null;
        var clock = Stopwatch.StartNew();
        static void Release(object? value)
        {
            if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }
        try
        {
            var type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (type is null) return new([], true, "WMI indisponível");
            locator = Activator.CreateInstance(type);
            services = ((dynamic)locator!).ConnectServer(".", @"root\cimv2");
            void Query(string sql, Action<Func<string, object?>> consume)
            {
                object? result = null;
                int count = 0;
                try
                {
                    stage = "executar consulta";
                    token.ThrowIfCancellationRequested();
                    // O adaptador COM IEnumerable do .NET requer enumeracao rebobinavel.
                    // Manter retorno semissincrono (16), sem ForwardOnly (32).
                    result = ((dynamic)services!).ExecQuery(sql, "WQL", 16);
                    stage = "enumerar consulta";
                    foreach (object row in (System.Collections.IEnumerable)result)
                    {
                        try
                        {
                            if (++count > 4000 || clock.Elapsed > TimeSpan.FromSeconds(18)) { partial = true; break; }
                            token.ThrowIfCancellationRequested();
                            object? Get(string name)
                            {
                                object? properties = null, property = null;
                                try
                                {
                                    properties = ((dynamic)row).Properties_;
                                    property = ((dynamic)properties).Item(name);
                                    return ((dynamic)property).Value;
                                }
                                catch { partial = true; return null; }
                                finally { Release(property); Release(properties); }
                            }
                            consume(Get);
                        }
                        finally { Release(row); }
                    }
                }
                finally { Release(result); }
            }
            Query("SELECT PNPDeviceID, Name, Description, Present, HardwareID, ConfigManagerErrorCode FROM Win32_PnPEntity", get =>
            {
                string id = (Convert.ToString(get("PNPDeviceID")) ?? "").Trim();
                if (id.Length > 0)
                {
                    bool? reported = get("Present") is bool b ? b : null;
                    if (presence.TryGetValue(id, out var previous) && previous != reported)
                    { presence[id] = null; partial = true; }
                    else presence[id] = reported;
                    string name = ReportedDeviceName(
                        Convert.ToString(get("Name")), Convert.ToString(get("Description")));
                    if (name.Length > 0) deviceNames[id] = name;
                    string[] ids = get("HardwareID") is Array list ? list.Cast<object>()
                        .OfType<string>().Where(ValidHardwareId).Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(64).ToArray() : [];
                    if (hardwareIds.TryGetValue(id, out var oldIds) && !oldIds.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(ids))
                    { hardwareIds[id] = []; partial = true; }
                    else hardwareIds[id] = ids;
                    object? rawCode = get("ConfigManagerErrorCode");
                    int? code = rawCode switch
                    {
                        int value when value is >= 0 and <= 9999 => value,
                        uint value when value <= 9999 => (int)value,
                        _ => null
                    };
                    if (rawCode is not null && code is null) partial = true;
                    // Duplicatas conflitantes nunca sao resolvidas pela ultima linha.
                    if (!conflictedCodes.Contains(id))
                    {
                        if (problemCodes.TryGetValue(id, out var previousCode) && previousCode != code)
                        {
                            problemCodes[id] = null;
                            conflictedCodes.Add(id);
                            partial = true;
                        }
                        else problemCodes[id] = code;
                    }
                }
            });
            Query("SELECT DeviceID, DeviceName, DriverProviderName, DriverVersion, InfName, IsSigned FROM Win32_PnPSignedDriver", get =>
            {
                string Text(string key) => Convert.ToString(get(key)) ?? "";
                string id = Text("DeviceID").Trim();
                if (id.Length == 0) { partial = true; return; }
                rows.Add(new(id, Text("DeviceName"), Text("DriverProviderName"), Text("DriverVersion"),
                    Text("InfName"), get("IsSigned") is bool b ? b : null, null)
                    { HardwareIds = hardwareIds.GetValueOrDefault(id) ?? [] });
            });
            return new(Correlate(
                IncludeUnboundProblemDevices(rows, problemCodes, deviceNames, hardwareIds),
                presence, deviceNames), partial, partial ? "Consulta parcial" : "Consulta concluída")
                { PnpEntities = presence.Count, SignedRows = rows.Count };
        }
        catch (Exception ex)
        {
            return new(Correlate(
                IncludeUnboundProblemDevices(rows, problemCodes, deviceNames, hardwareIds),
                presence, deviceNames), true, $"Consulta parcial ou indisponível: {stage}; {ex.GetType().Name}; 0x{ex.HResult:X8}")
                { PnpEntities = presence.Count, SignedRows = rows.Count };
        }
        finally { Release(services); Release(locator); }
    }
}
