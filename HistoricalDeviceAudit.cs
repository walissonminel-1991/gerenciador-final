using Microsoft.Win32;
using System.Diagnostics;

namespace GerenciadorAplicativos;

// Registros de instâncias PnP podem sobreviver à troca de placa-mãe/GPU.
// Compará-los com os dispositivos presentes é SOMENTE uma pista histórica.
// NÃO equivale a pacotes de driver órfãos, e não autoriza exclusões.
internal sealed record RegistryDeviceEvidence(int Registered, int NotPresent,
    int RootsChecked, int ReadErrors, bool Partial, IReadOnlyList<string> Buses);

internal static class HistoricalDeviceAudit
{
    private static readonly string[] Buses = ["PCI", "USB", "USBSTOR"];
    private const int MaxDevices = 4000;
    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(7);

    internal static RegistryDeviceEvidence Compare(IEnumerable<string> registeredIds,
        IReadOnlySet<string> currentIds, int inspectedRoots, int errors, bool partial)
    {
        var all = registeredIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxDevices).ToArray();
        var absent = all.Where(id => !currentIds.Contains(id)).ToArray();
        var byBus = absent.GroupBy(id => id.Split('\\')[0], StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Key.ToUpperInvariant() + ": " + x.Count()).ToArray();
        return new(all.Length, absent.Length, inspectedRoots, errors, partial, byBus);
    }

    internal static RegistryDeviceEvidence Inspect(IReadOnlySet<string> activePnP)
    {
        var ids = new List<string>(450);
        var clock = Stopwatch.StartNew();
        int scanned = 0, errors = 0, roots = 0;
        bool partial = false;
        try
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                RegistryView.Default);
            using var enumeration = hklm.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum",
                writable: false);
            if (enumeration is null) return new(0, 0, 0, 1, true, []);
            foreach (string bus in Buses)
            {
                if (clock.Elapsed > MaxDuration || scanned >= MaxDevices)
                { partial = true; break; }
                roots++;
                try
                {
                    using var root = enumeration.OpenSubKey(bus, writable: false);
                    if (root is null) continue;
                    foreach (string hardware in root.GetSubKeyNames())
                    {
                        if (clock.Elapsed > MaxDuration || scanned >= MaxDevices)
                        { partial = true; break; }
                        try
                        {
                            using var group = root.OpenSubKey(hardware, writable: false);
                            if (group is null) { errors++; partial = true; continue; }
                            foreach (string instance in group.GetSubKeyNames())
                            {
                                if (clock.Elapsed > MaxDuration || scanned >= MaxDevices)
                                { partial = true; break; }
                                scanned++;
                                if (instance.Length is < 1 or > 400) continue;
                                ids.Add(bus + "\\" + hardware + "\\" + instance);
                            }
                        }
                        catch (Exception ex) when (ex is UnauthorizedAccessException or
                            System.Security.SecurityException or IOException)
                        { errors++; partial = true; }
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or
                    System.Security.SecurityException or IOException)
                { errors++; partial = true; }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or
            System.Security.SecurityException or IOException)
        { errors++; partial = true; }
        return Compare(ids, activePnP, roots, errors, partial);
    }
}
