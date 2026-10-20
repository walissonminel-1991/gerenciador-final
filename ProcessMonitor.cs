using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// Fotografia de leitura local; nao administra ou encerra processos.
internal sealed record ProcessMonitorEntry(
    int Pid, string Name, double? CpuPercent, long MemoryBytes,
    double? IoBytesPerSecond);

internal sealed record ProcessMonitorSnapshot(
    DateTime At, int ProcessCount, double? TotalCpuPercent,
    uint? UsedMemoryPercent, double? NetworkBytesPerSecond,
    IReadOnlyList<ProcessMonitorEntry> Processes);

// Processo TotalProcessorTime e GetProcessIoCounters sao acumuladores.
// Diferencas sucessivas sao medidas por intervalo, nunca como "uso atual"
// antes da primeira leitura. E/S nao equivale necessariamente a disco fisico.
internal sealed class ProcessMonitorSampler
{
    private sealed record Previous(string Name, TimeSpan CpuTime, ulong? IoBytes, DateTime At);
    private readonly Dictionary<int, Previous> _previous = new();
    private ulong? _previousIdle;
    private ulong? _previousKernel;
    private ulong? _previousUser;
    private long? _previousNetwork;
    private DateTime? _networkAt;

    internal static double CpuPercent(TimeSpan cpuDelta, double elapsedSeconds, int logicalProcessors)
    {
        if (elapsedSeconds <= 0 || logicalProcessors <= 0) return 0;
        return Math.Clamp(cpuDelta.TotalSeconds / (elapsedSeconds * logicalProcessors) * 100d, 0, 100);
    }

    internal static double? Throughput(long current, long previous, double seconds)
    {
        if (seconds <= 0 || current < previous) return null;
        return (current - previous) / seconds;
    }

    // A leitura em segundo plano não enumera processos nem abre seus handles.
    // Mantem somente os contadores globais, sem criar histórico fictício de
    // consumo individual durante o período oculto.
    public ProcessMonitorSnapshot TakeResourceSample()
    {
        _previous.Clear(); // taxas por processo nao podem usar um baseline antigo.
        var now = DateTime.UtcNow;
        return new ProcessMonitorSnapshot(
            now, 0, ReadCpuUsage(), ReadMemoryLoad(), ReadNetworkUsage(now),
            Array.Empty<ProcessMonitorEntry>());
    }

    public ProcessMonitorSnapshot TakeSample()
    {
        var now = DateTime.UtcNow;
        var seen = new HashSet<int>();
        var rows = new List<ProcessMonitorEntry>(192);
        Process[] processes = Process.GetProcesses();
        foreach (Process process in processes)
        {
            using (process)
            {
                int pid;
                string name;
                try { pid = process.Id; name = process.ProcessName; }
                catch (InvalidOperationException) { continue; }
                catch (Win32Exception) { continue; }

                seen.Add(pid);
                long memory = 0;
                TimeSpan? cpu = null;
                ulong? io = null;
                try { memory = Math.Max(0, process.WorkingSet64); }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
                try { cpu = process.TotalProcessorTime; }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
                try
                {
                    if (GetProcessIoCounters(process.Handle, out var counts))
                        io = counts.ReadTransferCount + counts.WriteTransferCount;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }

                double? cpuRate = null;
                double? ioRate = null;
                if (cpu.HasValue)
                {
                    if (_previous.TryGetValue(pid, out var prev) &&
                        prev.Name == name && cpu.Value >= prev.CpuTime)
                    {
                        double seconds = (now - prev.At).TotalSeconds;
                        if (seconds >= 0.25)
                        {
                            cpuRate = CpuPercent(cpu.Value - prev.CpuTime, seconds,
                                Environment.ProcessorCount);
                            if (io.HasValue && prev.IoBytes.HasValue &&
                                io.Value >= prev.IoBytes.Value)
                                ioRate = (io.Value - prev.IoBytes.Value) / seconds;
                        }
                    }
                    _previous[pid] = new Previous(name, cpu.Value, io, now);
                }
                else _previous.Remove(pid);

                rows.Add(new ProcessMonitorEntry(pid, name, cpuRate, memory, ioRate));
            }
        }
        foreach (int pid in _previous.Keys.Where(pid => !seen.Contains(pid)).ToArray())
            _previous.Remove(pid);

        double? totalCpu = ReadCpuUsage();
        uint? memoryLoad = ReadMemoryLoad();
        double? networkRate = ReadNetworkUsage(now);

        return new ProcessMonitorSnapshot(
            now, rows.Count, totalCpu, memoryLoad, networkRate,
            rows.OrderByDescending(row => row.CpuPercent ?? -1)
                .ThenByDescending(row => row.MemoryBytes)
                .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray());
    }

    private double? ReadCpuUsage()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return null;
        ulong i = idle.Value, k = kernel.Value, u = user.Value;
        double? result = null;
        if (_previousIdle.HasValue && _previousKernel.HasValue && _previousUser.HasValue &&
            i >= _previousIdle.Value && k >= _previousKernel.Value && u >= _previousUser.Value)
        {
            ulong idleDelta = i - _previousIdle.Value;
            ulong totalDelta = k - _previousKernel.Value + u - _previousUser.Value;
            if (totalDelta > 0)
                result = Math.Clamp((1d - (double)idleDelta / totalDelta) * 100d, 0, 100);
        }
        _previousIdle = i;
        _previousKernel = k;
        _previousUser = u;
        return result;
    }

    private static uint? ReadMemoryLoad()
    {
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref memory) ? memory.MemoryLoad : null;
    }

    private double? ReadNetworkUsage(DateTime now)
    {
        try
        {
            long total = 0;
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback
                        or NetworkInterfaceType.Tunnel) continue;
                var statistics = adapter.GetIPStatistics();
                total += statistics.BytesReceived + statistics.BytesSent;
            }
            double? rate = _previousNetwork.HasValue && _networkAt.HasValue
                ? Throughput(total, _previousNetwork.Value,
                    (now - _networkAt.Value).TotalSeconds) : null;
            _previousNetwork = total;
            _networkAt = now;
            return rate;
        }
        catch (Exception ex) when (ex is NetworkInformationException or Win32Exception
                                     or InvalidOperationException)
        {
            _previousNetwork = null;
            _networkAt = null;
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low, High;
        public ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr process, out IoCounters counts);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel,
        out FileTime user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
}
