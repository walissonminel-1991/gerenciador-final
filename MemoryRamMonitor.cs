using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// Amostras somente leitura do sistema. Memoria disponivel inclui standby/cache
// imediatamente reutilizavel pelo Windows, sem qualquer limpeza artificial.
internal sealed record MemoryRamSample(DateTime At, ulong TotalBytes, ulong AvailableBytes)
{
    internal ulong UsedBytes => TotalBytes >= AvailableBytes ? TotalBytes - AvailableBytes : 0;
    internal double UsedPercent => TotalBytes == 0 ? 0 :
        Math.Clamp(UsedBytes * 100.0 / TotalBytes, 0, 100);
}

internal static class MemoryRamReader
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus data);

    internal static MemoryRamSample? Capture()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0 ||
            status.AvailablePhysical > status.TotalPhysical)
            return null;
        return new MemoryRamSample(DateTime.UtcNow, status.TotalPhysical, status.AvailablePhysical);
    }
}

// Historico limitado em memoria, sem escrita em disco nem dados por processo.
internal sealed class MemoryRamHistory
{
    internal const int Capacity = 90;
    private readonly List<MemoryRamSample> _samples = new(Capacity);
    internal IReadOnlyList<MemoryRamSample> Samples => _samples;
    internal long ConfirmedReclaimedBytes { get; private set; } // sem limpeza ativa: 0.

    internal void Add(MemoryRamSample sample)
    {
        if (sample.TotalBytes == 0 || sample.AvailableBytes > sample.TotalBytes) return;
        _samples.Add(sample);
        if (_samples.Count > Capacity) _samples.RemoveAt(0);
    }
}
