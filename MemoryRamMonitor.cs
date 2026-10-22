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

    internal void TrimToRecent(int count)
    {
        int retained = Math.Clamp(count, 2, Capacity);
        if (_samples.Count > retained) _samples.RemoveRange(0, _samples.Count - retained);
    }
}

// Dados resumidos de todo o Windows, independentes do cache interno do GF.
// SystemCache inclui standby E conjunto de trabalho de cache do sistema:
// nao e uma promessa de bytes descartaveis nem de RAM recuperada.
internal sealed record WindowsMemoryDetails(ulong CacheBytes, ulong CommittedBytes, ulong CommitLimitBytes)
{
    internal static ulong Bytes(ulong pages, ulong pageSize) =>
        pageSize == 0 || pages > ulong.MaxValue / pageSize ? 0 : pages * pageSize;
}

internal static class WindowsMemoryDetailsReader
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInfo
    {
        public uint Size;
        public UIntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable,
            SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(ref PerformanceInfo data, uint size);

    internal static WindowsMemoryDetails? Capture()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var info = new PerformanceInfo { Size = (uint)Marshal.SizeOf<PerformanceInfo>() };
        try
        {
            if (!GetPerformanceInfo(ref info, info.Size)) return null;
            ulong page = info.PageSize.ToUInt64();
            if (page < 1024 || page > 1048576) return null;
            return new(
                WindowsMemoryDetails.Bytes(info.SystemCache.ToUInt64(), page),
                WindowsMemoryDetails.Bytes(info.CommitTotal.ToUInt64(), page),
                WindowsMemoryDetails.Bytes(info.CommitLimit.ToUInt64(), page));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        { return null; }
    }
}
