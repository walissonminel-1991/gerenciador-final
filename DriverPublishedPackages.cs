using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal sealed record DriverPublishedPackage(DriverStoreEvidence Evidence, int ObservedDevices,
    bool DeviceInventoryPartial)
{
    internal bool CanMaintain => false;
    internal string AssociationLabel => ObservedDevices > 0 ? $"{ObservedDevices} dispositivos observados"
        : DeviceInventoryPartial ? "Associação desconhecida: inventário parcial; preservar"
        : "Sem dispositivo observado nesta consulta; preservar";
}
internal sealed record DriverPublishedAuditResult(IReadOnlyList<DriverPublishedPackage> Packages,
    bool Partial, bool EnumerationComplete)
{
    internal string Summary => $"INFs publicados: {Packages.Count}; " +
        $"{Packages.Count(x => x.ObservedDevices == 0)} sem dispositivo observado" +
        (Partial ? " (consulta parcial)." : ".") +
        " Não enumera diretórios órfãos do Driver Store; ausência não demonstra desuso.";
}

// Enumeração nativa do diretório INF do Windows. Não varre pastas por nome,
// não invoca pnputil/DISM e não confunde falta de associação WMI com lixo.
internal static class DriverPublishedPackages
{
    internal static string[] Enumerate()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (!SetupGetInfFileListW(null, 2, null, 0, out uint required) || required is < 2 or > 1_048_576)
            throw new IOException("Lista de INFs indisponível.");
        var buffer = new char[required];
        if (!SetupGetInfFileListW(null, 2, buffer, (uint)buffer.Length, out uint actual) || actual > required)
            throw new IOException("Lista de INFs mudou ou está indisponível.");
        return new string(buffer, 0, (int)actual).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    internal static DriverPublishedAuditResult Capture(DriverInventoryResult inventory, CancellationToken token,
        Func<string[]>? enumerate = null, Func<string, DriverStoreEvidence>? resolve = null,
        int maximumPackages = 2048, TimeSpan? timeout = null)
    {
        var clock = Stopwatch.StartNew();
        var result = new List<DriverPublishedPackage>();
        bool partial = inventory.Partial;
        string[] all;
        try { token.ThrowIfCancellationRequested(); all = (enumerate ?? Enumerate)(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or NotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        { return new([], true, false); }
        var valid = all.Where(DriverPackageLinks.IsValidInf).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        bool complete = all.Length <= 8192 && all.All(DriverPackageLinks.IsValidInf);
        partial |= !complete;
        var associated = inventory.Items.Where(d => DriverPackageLinks.IsValidInf(d.InfName))
            .GroupBy(d => d.InfName, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key,
                g => g.Select(d => d.DeviceId.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count(), StringComparer.OrdinalIgnoreCase);
        foreach (string inf in valid.Take(8192))
        {
            token.ThrowIfCancellationRequested();
            if (result.Count >= Math.Clamp(maximumPackages, 0, 4096) || clock.Elapsed >= (timeout ?? TimeSpan.FromSeconds(4)))
            { partial = true; complete = false; break; }
            DriverStoreEvidence evidence;
            try { evidence = (resolve ?? DriverStoreAudit.Resolve)(inf); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                System.Security.SecurityException or NotSupportedException or DllNotFoundException or EntryPointNotFoundException)
            { evidence = new(inf, DriverStoreState.Unavailable); }
            if (!evidence.InfName.Equals(inf, StringComparison.OrdinalIgnoreCase))
                evidence = new(inf, DriverStoreState.Inconsistent);
            partial |= evidence.State != DriverStoreState.Mapped;
            result.Add(new(evidence, associated.GetValueOrDefault(inf), inventory.Partial));
        }
        return new(result, partial, complete);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupGetInfFileListW(string? directory, uint style,
        [Out] char[]? buffer, uint capacity, out uint requiredSize);
}
