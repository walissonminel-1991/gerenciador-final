namespace GerenciadorAplicativos;

internal static class GfMonitorSampling
{
    internal static LiveHardwareSample UnavailableCounters(DateTimeOffset at) => new(at,
        MonitorReading<double>.Unavailable("consulta local inconclusiva"),
        MonitorReading<double>.Unavailable("consulta local inconclusiva"), null,
        MonitorReading<TimeSpan>.Unavailable("consulta local inconclusiva"), LiveHardwareSensors.Unavailable);

    internal static HardwareSensorPortfolio WithoutGpuReadings(HardwareSensorPortfolio snapshot, DateTimeOffset at) =>
        snapshot with { NvidiaGpus = new NvidiaGpuSensorSnapshot(at, false, [], "consulta GPU inconclusiva"),
            WindowsGpuCounters = null, D3dKmtGpuSensors = null };

    internal static HardwareSensorPortfolio CountersOnly(LiveHardwareSample sample)
    {
        var at = sample.CapturedAtUtc;
        const HardwareDiscoveryState unavailable = HardwareDiscoveryState.Unavailable;
        return new(at, sample, new(at, unavailable, [], "Consulta leve", null),
            new(at, false, [], null), new(at, unavailable, [], null), new(at, unavailable, [], null));
    }

    internal static HardwareSensorPortfolio RefreshGpu(HardwareSensorPortfolio baseline, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var nvml = NvidiaNvmlSensorProvider.Capture();
        token.ThrowIfCancellationRequested();
        var dxgi = DxgiGpuIdentityProvider.RetainUploadHeapCapability(
            DxgiGpuIdentityProvider.Capture(), baseline.DxgiAdapters);
        token.ThrowIfCancellationRequested();
        var d3dkmt = D3dKmtGpuSensorProvider.Capture(dxgi, token);
        token.ThrowIfCancellationRequested();
        var wddm = WindowsWddmGpuProvider.Capture(token);
        token.ThrowIfCancellationRequested();
        return baseline with { NvidiaGpus = nvml, DxgiAdapters = dxgi,
            D3dKmtGpuSensors = d3dkmt, WindowsGpuCounters = wddm };
    }

    internal static WddmGpuKey? SelectGpu(HardwareSensorPortfolio snapshot)
    {
        if (snapshot.DxgiAdapters is not { State: HardwareDiscoveryState.Complete } dxgi) return null;
        var hardware = dxgi.Adapters.Where(x => !x.IsSoftware).ToArray();
        if (hardware.Length != 1) return null;
        var selected = hardware[0];
        var candidates = (snapshot.WindowsGpuCounters?.Adapters.Select(x => x.Key) ?? [])
            .Concat(snapshot.D3dKmtGpuSensors?.Adapters.Select(x => x.Key) ?? [])
            .Where(x => x.LuidHigh == selected.LuidHigh && x.LuidLow == selected.LuidLow)
            .Distinct().Take(2).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
}
