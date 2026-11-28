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

    internal static WddmGpuKey? SelectGpu(HardwareSensorPortfolio snapshot, WddmGpuKey? current, DateTimeOffset now)
    {
        if (snapshot.DxgiAdapters is not { State: HardwareDiscoveryState.Complete } dxgi ||
            !GfResourceMonitorAdapter.IsFresh(dxgi.CapturedAtUtc, now)) return null;

        IEnumerable<WddmGpuKey> wddmKeys = snapshot.WindowsGpuCounters is { } wddm &&
            GfResourceMonitorAdapter.IsFresh(wddm.CapturedAtUtc, now) ? wddm.Adapters.Select(x => x.Key) : [];
        IEnumerable<WddmGpuKey> thermalKeys = snapshot.D3dKmtGpuSensors is { } thermal &&
            GfResourceMonitorAdapter.IsFresh(thermal.CapturedAtUtc, now) ? thermal.Adapters.Select(x => x.Key) : [];
        var keys = wddmKeys.Concat(thermalKeys).Distinct();
        // Um LUID com mais de um índice físico exige identificação adicional
        // para que todos os indicadores possam referir-se ao mesmo dispositivo.
        return keys.GroupBy(key => (key.LuidHigh, key.LuidLow))
            .Where(group => group.Count() == 1)
            .Select(group => group.Single())
            .Select(key => (Key: key, Readings: GfResourceMonitorAdapter.FromPortfolio(snapshot, key, now)))
            .Where(candidate => candidate.Readings.GpuPercent.HasValue || candidate.Readings.VramPercent.HasValue ||
                candidate.Readings.GpuTemperature.HasValue)
            .OrderByDescending(candidate => candidate.Readings.GpuPercent.GetValueOrDefault())
            .ThenByDescending(candidate => candidate.Key == current)
            .ThenByDescending(candidate => candidate.Readings.GpuPercent.HasValue)
            .ThenBy(candidate => candidate.Key.LuidHigh)
            .ThenBy(candidate => candidate.Key.LuidLow)
            .Select(candidate => (WddmGpuKey?)candidate.Key)
            .FirstOrDefault();
    }
}
