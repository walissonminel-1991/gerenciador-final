using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal sealed record DriverInventoryEntry(string DeviceId, string DeviceName, string Provider,
    string Version, string InfName, bool? Signed, bool? Present)
{
    internal string SignatureLabel => Signed switch { true => "Sim (Windows)", false => "Não (Windows)", _ => "Indeterminado" };
    internal string PresenceLabel => Present switch { true => "Presente", false => "Não presente", _ => "Indeterminado" };
}
internal sealed record DriverInventoryResult(IReadOnlyList<DriverInventoryEntry> Items, bool Partial, string Status);

// Somente SELECT. Nao enumera todo o Driver Store nem autoriza exclusoes.
// IsSigned e um relato WMI, nao uma nova verificacao criptografica do pacote.
internal static class DriverInventory
{
    internal static IReadOnlyList<DriverInventoryEntry> Correlate(IEnumerable<DriverInventoryEntry> drivers,
        IReadOnlyDictionary<string, bool?> presence)
    {
        var indexed = new Dictionary<string, bool?>(presence, StringComparer.OrdinalIgnoreCase);
        return drivers.Select(d => d with { Present = indexed.GetValueOrDefault(d.DeviceId) })
            .DistinctBy(d => (d.DeviceId.ToUpperInvariant(), d.InfName.ToUpperInvariant(), d.Version, d.Provider.ToUpperInvariant(), d.Signed))
            .OrderBy(d => d.DeviceName, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal static DriverInventoryResult Capture(CancellationToken token)
    {
        var rows = new List<DriverInventoryEntry>();
        var presence = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
        bool partial = false;
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
                    token.ThrowIfCancellationRequested();
                    result = ((dynamic)services!).ExecQuery(sql, "WQL", 48);
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
            Query("SELECT PNPDeviceID, Present FROM Win32_PnPEntity", get =>
            {
                string id = Convert.ToString(get("PNPDeviceID")) ?? "";
                if (id.Length > 0) presence[id] = get("Present") is bool b ? b : null;
            });
            Query("SELECT DeviceID, DeviceName, DriverProviderName, DriverVersion, InfName, IsSigned FROM Win32_PnPSignedDriver", get =>
            {
                string Text(string key) => Convert.ToString(get(key)) ?? "";
                string id = Text("DeviceID");
                if (id.Length == 0) { partial = true; return; }
                rows.Add(new(id, Text("DeviceName"), Text("DriverProviderName"), Text("DriverVersion"),
                    Text("InfName"), get("IsSigned") is bool b ? b : null, null));
            });
            return new(Correlate(rows, presence), partial, partial ? "Consulta parcial" : "Consulta concluída");
        }
        catch (Exception ex)
        {
            return new(Correlate(rows, presence), true, "Consulta parcial ou indisponível: " + ex.GetType().Name);
        }
        finally { Release(services); Release(locator); }
    }
}
