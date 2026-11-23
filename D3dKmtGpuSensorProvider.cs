using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// GPU térmica/mecânica universal via interface oficial D3DKMT do Windows.
// Somente consultas: nenhuma escrita, abertura de dispositivo físico, driver
// novo, serviço ou acesso administrativo. Identidade = LUID + índice físico.
internal sealed record D3dKmtAdapterRaw(
    WddmGpuKey Key,
    ulong MemoryFrequencyHz, uint FanRpm, uint PowerTenthsPercent,
    uint TemperatureTenthsC, uint MaxFanRpm, uint TemperatureMaxTenthsC,
    bool HasPerformanceData, bool HasCapabilities,
    bool IsPoweredOn = true);

internal sealed record D3dKmtGpuReading(
    WddmGpuKey Key,
    MonitorReading<double> TemperatureC,
    MonitorReading<double> FanRpm,
    MonitorReading<double> RelativePowerPercent,
    MonitorReading<double> MemoryFrequencyMhz);

internal sealed record D3dKmtGpuSnapshot(
    DateTimeOffset CapturedAtUtc, HardwareDiscoveryState State,
    IReadOnlyList<D3dKmtGpuReading> Adapters, string? Limitation)
{
    // Contagens consultivas por adaptador físico, sem expor LUID, modelo,
    // valores individuais ou inferir defeito do driver. Podem se sobrepor.
    internal int WithoutPerformanceQuery { get; init; }
    internal int WithoutCapabilitiesQuery { get; init; }
    internal int ConfirmedPoweredOff { get; init; }
    internal int WithoutAnyValidMetric { get; init; }
}

internal static class D3dKmtGpuSensorProvider
{
    private const string Source = "Windows D3DKMT_ADAPTER_PERFDATA";
    // KMTQUERYADAPTERINFOTYPE, d3dkmthk.h (WDDM 2.4+).
    private const uint PhysicalAdapterCountType = 30;
    private const uint AdapterPerfDataType = 62;
    private const uint AdapterPerfCapsType = 63;
    private const int MaxLogicalAdapters = 32;
    private const int MaxPhysicalAdapters = 16;

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenAdapter
    {
        public Luid AdapterLuid;
        public uint Handle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CloseAdapter { public uint Handle; }

    [StructLayout(LayoutKind.Sequential)]
    private struct QueryAdapter
    {
        public uint Handle;
        public uint Type;
        public IntPtr Data;
        public uint DataLength;
    }

    // D3DKMT_ALIGN64 ULONGLONG tem alinhamento de 8 bytes; conferir a ABI.
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct AdapterPerf
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency;
        public ulong MaxMemoryFrequency;
        public ulong MaxMemoryFrequencyOc;
        public ulong MemoryBandwidth;
        public ulong PcieBandwidth;
        public uint FanRpm;
        public uint Power;
        public uint Temperature;
        public byte PowerStateOverride;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct AdapterCaps
    {
        public uint PhysicalAdapterIndex;
        public ulong MaxMemoryBandwidth;
        public ulong MaxPcieBandwidth;
        public uint MaxFanRpm;
        public uint TemperatureMax;
        public uint TemperatureWarning;
    }

    [DllImport("gdi32.dll", EntryPoint = "D3DKMTOpenAdapterFromLuid", ExactSpelling = true)]
    private static extern int OpenAdapterFromLuid(ref OpenAdapter input);

    [DllImport("gdi32.dll", EntryPoint = "D3DKMTQueryAdapterInfo", ExactSpelling = true)]
    private static extern int QueryAdapterInfo(ref QueryAdapter input);

    [DllImport("gdi32.dll", EntryPoint = "D3DKMTCloseAdapter", ExactSpelling = true)]
    private static extern int CloseAdapterHandle(ref CloseAdapter input);

    private static bool Query<T>(uint handle, uint queryType, ref T payload) where T : struct
    {
        IntPtr pointer = IntPtr.Zero;
        try
        {
            int length = Marshal.SizeOf<T>();
            pointer = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(payload, pointer, false);
            var args = new QueryAdapter
            {
                Handle = handle, Type = queryType, Data = pointer,
                DataLength = checked((uint)length)
            };
            // NTSTATUS == 0: sucesso. 0xC0... indica ausência de suporte,
            // remoção de GPU, parâmetros inválidos etc. Sem repetição cega.
            if (QueryAdapterInfo(ref args) != 0) return false;
            payload = Marshal.PtrToStructure<T>(pointer);
            return true;
        }
        finally
        {
            if (pointer != IntPtr.Zero) Marshal.FreeHGlobal(pointer);
        }
    }

    internal static D3dKmtGpuSnapshot Join(
        IEnumerable<D3dKmtAdapterRaw> raw, bool sourcePartial = false)
    {
        var items = raw.Take(MaxLogicalAdapters * MaxPhysicalAdapters + 1).ToArray();
        bool partial = sourcePartial || items.Length > MaxLogicalAdapters * MaxPhysicalAdapters;
        var duplicateIds = items.GroupBy(x => x.Key)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        if (duplicateIds.Count > 0) partial = true;
        var result = new List<D3dKmtGpuReading>();
        int performanceMissing = 0, capabilitiesMissing = 0;
        int powerOff = 0, noValidMetric = 0;
        foreach (var row in items.Take(MaxLogicalAdapters * MaxPhysicalAdapters))
        {
            if (row.Key.Physical >= MaxPhysicalAdapters || duplicateIds.Contains(row.Key))
            { partial = true; continue; }
            // PowerStateOverride do driver indica 1 para GPU ligada.
            // Conservar a identidade do adaptador, mas não aceitar telemetria
            // de uma GPU que o próprio driver informa estar desligada.
            bool active = row.HasPerformanceData && row.IsPoweredOn;
            if (!row.HasPerformanceData) performanceMissing++;
            if (!row.HasCapabilities) capabilitiesMissing++;
            if (row.HasPerformanceData && !row.IsPoweredOn) powerOff++;
            if (!active) partial = true;

            var unavailableTemp = MonitorReading<double>.Unavailable(Source + ".Temperature");
            var unavailableFan = MonitorReading<double>.Unavailable(Source + ".FanRPM");
            var unavailablePower = MonitorReading<double>.Unavailable(Source + ".Power (percentagem relativa)");
            var unavailableClock = MonitorReading<double>.Unavailable(Source + ".MemoryFrequency");
            // Sem caps coerentes, temperatura/RPM podem ser zeros default de
            // driver/VM; a medição não é homologável.
            bool capTemperature = row.HasCapabilities &&
                row.TemperatureMaxTenthsC is >= 200 and <= 2_000;
            bool capFan = row.HasCapabilities && row.MaxFanRpm is >= 300 and <= 40_000;
            bool temperatureOk = active && capTemperature &&
                row.TemperatureTenthsC is >= 100 and <= 1_500 &&
                row.TemperatureTenthsC <= row.TemperatureMaxTenthsC;
            bool fanOk = active && capFan &&
                row.FanRpm <= row.MaxFanRpm;
            // Power não informa watts: é percentagem relativa (décimos).
            bool powerOk = active &&
                row.PowerTenthsPercent is >= 1 and <= 2_000;
            bool clockOk = active &&
                row.MemoryFrequencyHz is >= 10_000_000 and <= 50_000_000_000;
            if (!temperatureOk && !fanOk && !powerOk && !clockOk)
                noValidMetric++;
            result.Add(new(row.Key,
                temperatureOk
                    ? MonitorReading<double>.Available(row.TemperatureTenthsC / 10d,
                        Source + ".Temperature (0,1°C)")
                    : unavailableTemp,
                fanOk
                    ? MonitorReading<double>.Available(row.FanRpm,
                        Source + ".FanRPM (RPM, suporta parada em zero)")
                    : unavailableFan,
                powerOk
                    ? MonitorReading<double>.Available(row.PowerTenthsPercent / 10d,
                        Source + ".Power (percentagem relativa, não W)")
                    : unavailablePower,
                clockOk
                    ? MonitorReading<double>.Available(row.MemoryFrequencyHz / 1_000_000d,
                        Source + ".MemoryFrequency (MHz)")
                    : unavailableClock));
        }
        return new(DateTimeOffset.UtcNow,
            result.Count == 0 ? HardwareDiscoveryState.Unavailable :
                partial ? HardwareDiscoveryState.Partial : HardwareDiscoveryState.Complete,
            result.AsReadOnly(),
            partial ? "GPU WDDM/driver sem sensores em alguns adaptadores" :
                result.Count == 0 ? "Sem GPU física consultável" : null)
        {
            WithoutPerformanceQuery = performanceMissing,
            WithoutCapabilitiesQuery = capabilitiesMissing,
            ConfirmedPoweredOff = powerOff,
            WithoutAnyValidMetric = noValidMetric
        };
    }

    internal static D3dKmtGpuSnapshot Capture(DxgiGpuSnapshot dxgi, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows())
            return Join([], sourcePartial: true);
        if (dxgi.State != HardwareDiscoveryState.Complete || dxgi.Adapters.Count > MaxLogicalAdapters)
            return Join([], sourcePartial: true);

        var physical = dxgi.Adapters.Where(a => !a.IsSoftware).ToArray();
        // DXGI repetiu o mesmo LUID: preservar inconclusivo, nunca escolher.
        var duplicates = physical.GroupBy(a => (a.LuidHigh, a.LuidLow))
            .Where(g => g.Count() != 1).Select(g => g.Key).ToHashSet();
        bool partial = duplicates.Count > 0;
        var result = new List<D3dKmtAdapterRaw>();
        foreach (var gpu in physical)
        {
            token.ThrowIfCancellationRequested();
            if (duplicates.Contains((gpu.LuidHigh, gpu.LuidLow))) continue;
            var opened = new OpenAdapter
            {
                AdapterLuid = new Luid
                {
                    LowPart = gpu.LuidLow,
                    HighPart = unchecked((int)gpu.LuidHigh)
                }
            };
            try
            {
                if (OpenAdapterFromLuid(ref opened) != 0 || opened.Handle == 0)
                { partial = true; continue; }
                uint count = 0;
                if (!Query(opened.Handle, PhysicalAdapterCountType, ref count) ||
                    count is 0 or > MaxPhysicalAdapters)
                { partial = true; continue; }
                for (uint i = 0; i < count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var caps = new AdapterCaps { PhysicalAdapterIndex = i };
                    var perf = new AdapterPerf { PhysicalAdapterIndex = i };
                    bool capOk = Query(opened.Handle, AdapterPerfCapsType, ref caps) &&
                        caps.PhysicalAdapterIndex == i;
                    bool perfOk = Query(opened.Handle, AdapterPerfDataType, ref perf) &&
                        perf.PhysicalAdapterIndex == i;
                    if (!capOk || !perfOk) partial = true;
                    result.Add(new(new WddmGpuKey(gpu.LuidHigh, gpu.LuidLow, i),
                        perf.MemoryFrequency, perf.FanRpm, perf.Power, perf.Temperature,
                        caps.MaxFanRpm, caps.TemperatureMax,
                        perfOk, capOk, perfOk && perf.PowerStateOverride == 1));
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or
                COMException or SEHException or BadImageFormatException or
                ArgumentException or OverflowException)
            { partial = true; }
            finally
            {
                if (opened.Handle != 0)
                {
                    var close = new CloseAdapter { Handle = opened.Handle };
                    try { CloseAdapterHandle(ref close); }
                    catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
                }
            }
        }
        return Join(result, partial);
    }

    // Comprovação de tamanho e offsets da ABI pública, sem carregar GDI.
    internal static bool VerifyWindowsAbi() =>
        Marshal.SizeOf<OpenAdapter>() == 12 &&
        Marshal.SizeOf<AdapterPerf>() == 64 &&
        Marshal.OffsetOf<AdapterPerf>(nameof(AdapterPerf.Temperature)).ToInt32() == 56 &&
        Marshal.SizeOf<AdapterCaps>() == 40 &&
        Marshal.SizeOf<QueryAdapter>() == (IntPtr.Size == 8 ? 24 : 16);
}
