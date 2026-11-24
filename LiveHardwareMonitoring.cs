using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// Motor somente leitura para os cartões de Início/Sistema. Não cria serviço,
// processo auxiliar, tarefa agendada, driver, arquivo ou tráfego de rede.
// A interface controla a cadência usando ProcessSamplingPolicy já existente.
internal enum MonitorAvailability { Available, WarmingUp, Unavailable }

internal sealed record MonitorReading<T>(T? Value, MonitorAvailability Availability, string Source)
    where T : struct
{
    internal static MonitorReading<T> Available(T value, string source) =>
        new(value, MonitorAvailability.Available, source);

    internal static MonitorReading<T> Unavailable(string source) =>
        new(null, MonitorAvailability.Unavailable, source);

    internal static MonitorReading<T> WarmingUp(string source) =>
        new(null, MonitorAvailability.WarmingUp, source);
}

// Contrato dos sensores para o novo layout. Sem provedor de hardware validado,
// nenhum valor será exibido como se viesse de um sensor real.
internal sealed record LiveHardwareSensors(
    MonitorReading<double> CpuTemperatureC,
    MonitorReading<double> CpuPowerWatts,
    MonitorReading<double> CpuFanRpm,
    MonitorReading<double> GpuUsagePercent,
    MonitorReading<double> GpuTemperatureC,
    MonitorReading<double> GpuPowerWatts,
    MonitorReading<double> GpuFanRpm,
    MonitorReading<ulong> GpuVramUsedBytes,
    MonitorReading<double> ChipsetTemperatureC,
    MonitorReading<double> VrmTemperatureC)
{
    private const string NoSensor = "Sensor indisponível: provedor de hardware não integrado";

    internal static LiveHardwareSensors Unavailable { get; } = new(
        MonitorReading<double>.Unavailable(NoSensor),
        MonitorReading<double>.Unavailable(NoSensor),
        MonitorReading<double>.Unavailable(NoSensor),
        MonitorReading<double>.Unavailable(NoSensor),
        MonitorReading<double>.Unavailable(NoSensor),
        MonitorReading<double>.Unavailable(NoSensor),
        MonitorReading<double>.Unavailable(NoSensor),
        MonitorReading<ulong>.Unavailable(NoSensor),
        MonitorReading<double>.Unavailable(NoSensor),
        MonitorReading<double>.Unavailable(NoSensor));
}

internal sealed record LiveHardwareSample(
    DateTimeOffset CapturedAtUtc,
    MonitorReading<double> CpuUsagePercent,
    MonitorReading<double> CpuFrequencyMhz,
    MemoryRamSample? Memory,
    MonitorReading<TimeSpan> SystemUptime,
    LiveHardwareSensors Sensors);

// GetSystemTimes soma os tempos de todas as CPUs do grupo do processo.
// O tempo de kernel inclui o tempo ocioso; nunca o somar duas vezes.
internal readonly record struct CpuSystemTimes(ulong Idle, ulong Kernel, ulong User);

internal static class CpuUsageMath
{
    internal static double? Between(CpuSystemTimes? previous, CpuSystemTimes current)
    {
        if (previous is not { } before ||
            current.Idle < before.Idle || current.Kernel < before.Kernel ||
            current.User < before.User)
            return null;

        ulong idle = current.Idle - before.Idle;
        ulong kernel = current.Kernel - before.Kernel;
        ulong user = current.User - before.User;
        if (kernel > ulong.MaxValue - user) return null;
        ulong total = kernel + user;
        if (total == 0 || idle > total) return null;
        return Math.Clamp((total - idle) * 100d / total, 0d, 100d);
    }
}

internal static class NativeHardwareCounters
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FileTime
    {
        internal readonly uint Low;
        internal readonly uint High;
        internal ulong Ticks => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPowerInfo
    {
        public uint Number, MaxMhz, CurrentMhz, MhzLimit, MaxIdleState, CurrentIdleState;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetSystemTimes", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("kernel32.dll", EntryPoint = "GetTickCount64")]
    private static extern ulong GetTickCount64();

    [DllImport("powrprof.dll", EntryPoint = "CallNtPowerInformation")]
    private static extern uint CallNtPowerInformation(
        uint informationLevel, IntPtr input, uint inputLength,
        [Out] ProcessorPowerInfo[] output, uint outputLength);

    internal static CpuSystemTimes? ReadCpuTimes()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;
            return new(idle.Ticks, kernel.Ticks, user.Ticks);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        { return null; }
    }

    // Frequência CURRENT_MHZ reportada pela API de energia do Windows.
    // NÃO equivale à medição do clock físico instantâneo de cada núcleo.
    internal static double? ReadReportedCpuMhz()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            int logical = Math.Clamp(Environment.ProcessorCount, 1, 256);
            var data = new ProcessorPowerInfo[logical];
            uint size = checked((uint)(logical * Marshal.SizeOf<ProcessorPowerInfo>()));
            if (CallNtPowerInformation(11, IntPtr.Zero, 0, data, size) != 0) return null;
            var valid = data.Where(x => x.CurrentMhz is >= 100 and <= 20_000).ToArray();
            return valid.Length == 0 ? null : valid.Average(x => (double)x.CurrentMhz);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or
            OverflowException or ArgumentException)
        { return null; }
    }

    internal static TimeSpan? ReadUptime()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            // Contador monotônico de boot do Windows, não relógio de parede.
            ulong milliseconds = GetTickCount64();
            return milliseconds > (ulong)TimeSpan.MaxValue.TotalMilliseconds
                ? null : TimeSpan.FromMilliseconds(milliseconds);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or
            OverflowException)
        { return null; }
    }
}

// Instância por janela/sessão. A primeira leitura de CPU aquece o comparador;
// leituras fracassadas invalidam a base para não produzir porcentagens falsas.
internal sealed class LiveHardwareMonitor
{
    internal const int HistoryCapacity = 90;
    private readonly object _gate = new();
    private readonly List<LiveHardwareSample> _history = new(HistoryCapacity);
    private CpuSystemTimes? _previous;

    internal IReadOnlyList<LiveHardwareSample> History
    {
        get { lock (_gate) return _history.ToArray(); }
    }

    internal int RecommendedIntervalMs(bool panelVisible, bool inTray) =>
        ProcessSamplingPolicy.IntervalMs(panelVisible, inTray);

    internal LiveHardwareSample Capture() => CaptureCore(null, useSharedMemory: false);

    // A aba Sistema já possui a amostra de RAM. Reutilizá-la impede
    // duas chamadas ao GlobalMemoryStatusEx em uma única atualização visual.
    internal LiveHardwareSample Capture(MemoryRamSample? sharedMemory) =>
        CaptureCore(sharedMemory, useSharedMemory: true);

    private LiveHardwareSample CaptureCore(MemoryRamSample? sharedMemory, bool useSharedMemory)
    {
        lock (_gate)
        {
            CpuSystemTimes? current = NativeHardwareCounters.ReadCpuTimes();
            double? usage = current is { } now ? CpuUsageMath.Between(_previous, now) : null;
            var cpu = usage is { } value
                ? MonitorReading<double>.Available(value, "Windows GetSystemTimes")
                : current is null
                    ? MonitorReading<double>.Unavailable("Windows GetSystemTimes")
                    : MonitorReading<double>.WarmingUp("Windows GetSystemTimes");
            _previous = current;

            double? frequency = NativeHardwareCounters.ReadReportedCpuMhz();
            TimeSpan? uptime = NativeHardwareCounters.ReadUptime();
            // Reutiliza o mesmo leitor de RAM já empregado pela aba Sistema.
            MemoryRamSample? memory = useSharedMemory ? sharedMemory :
                OperatingSystem.IsWindows() ? MemoryRamReader.Capture() : null;

            var sample = new LiveHardwareSample(
                DateTimeOffset.UtcNow,
                cpu,
                frequency is { } mhz
                    ? MonitorReading<double>.Available(mhz, "Windows CallNtPowerInformation (Mhz reportados)")
                    : MonitorReading<double>.Unavailable("Windows CallNtPowerInformation"),
                memory,
                uptime is { } elapsed
                    ? MonitorReading<TimeSpan>.Available(elapsed, "Windows GetTickCount64")
                    : MonitorReading<TimeSpan>.Unavailable("Windows GetTickCount64"),
                LiveHardwareSensors.Unavailable);

            _history.Add(sample);
            if (_history.Count > HistoryCapacity) _history.RemoveAt(0);
            return sample;
        }
    }
}
