namespace GerenciadorAplicativos;

// Adaptador puro: somente dados já coletados, sem timer ou GPU implícita.
internal static class GfResourceMonitorAdapter
{
    internal static bool IsFresh(DateTimeOffset measured, DateTimeOffset now) =>
        measured <= now.AddSeconds(5) && measured >= now.AddSeconds(-10);

    private static double? Read(MonitorReading<double> reading) =>
        reading.Availability == MonitorAvailability.Available && reading.Value is double v && double.IsFinite(v) ? v : null;

    internal static GfResourceMonitorSnapshot FromCounters(LiveHardwareSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        DateTimeOffset? ramAt = sample.Memory is { } m && m.At.Kind == DateTimeKind.Utc ? new DateTimeOffset(m.At) : null;
        double? ram = sample.Memory is { TotalBytes: > 0 } memory && memory.AvailableBytes <= memory.TotalBytes &&
            ramAt is DateTimeOffset at && IsFresh(at, sample.CapturedAtUtc)
            ? (memory.TotalBytes - memory.AvailableBytes) * 100d / memory.TotalBytes : null;
        return new(sample.CapturedAtUtc, Read(sample.CpuUsagePercent), null, ram, null,
            Read(sample.Sensors.CpuTemperatureC), null, MetricTimes:
                new(sample.CapturedAtUtc, null, ramAt, null, sample.CapturedAtUtc, null));
    }

    internal static GfResourceMonitorSnapshot FromPortfolio(HardwareSensorPortfolio portfolio, WddmGpuKey? selected) =>
        FromPortfolio(portfolio, selected, portfolio.CapturedAtUtc);

    // A entrega possui relógio próprio. O portfólio pode conter inventário
    // estático e fontes atualizadas em cadências distintas (2 s / 8 s).
    internal static GfResourceMonitorSnapshot FromPortfolio(HardwareSensorPortfolio portfolio, WddmGpuKey? selected,
        DateTimeOffset deliveredAt)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        var result = FromCounters(portfolio.WindowsCounters) with { CapturedAtUtc = deliveredAt };
        result = GfMonitorMetricTimes.FreshAt(result, deliveredAt);
        if (selected is not WddmGpuKey key || portfolio.DxgiAdapters is not { } dxgi ||
            dxgi.State != HardwareDiscoveryState.Complete || !IsFresh(dxgi.CapturedAtUtc, deliveredAt)) return result;
        var identities = dxgi.Adapters.Where(x => x.LuidHigh == key.LuidHigh && x.LuidLow == key.LuidLow).ToArray();
        if (identities.Length != 1 || identities[0].IsSoftware) return result;
        // Temperatura não depende do sucesso do contador WDDM, mas depende
        // da identidade DXGI e da chave física D3DKMT inequívocas.
        var thermal = portfolio.D3dKmtGpuSensors;
        var temperatures = thermal?.Adapters.Where(x => x.Key == key).ToArray();
        bool thermalFresh = thermal is not null && IsFresh(thermal.CapturedAtUtc, deliveredAt);
        double? temperature = thermalFresh && temperatures is { Length: 1 } ? Read(temperatures[0].TemperatureC) : null;
        var wddm = portfolio.WindowsGpuCounters;
        var counters = wddm?.Adapters.Where(x => x.Key.LuidHigh == key.LuidHigh && x.Key.LuidLow == key.LuidLow).ToArray();
        var gpu = wddm is not null && IsFresh(wddm.CapturedAtUtc, deliveredAt) &&
            counters is { Length: 1 } && counters[0].Key == key ? counters[0] : null;
        var physical = thermal?.Adapters.Where(x => x.Key.LuidHigh == key.LuidHigh && x.Key.LuidLow == key.LuidLow).ToArray();
        double? vram = gpu is not null && key.Physical == 0 && physical is not { Length: > 1 } &&
            gpu.DedicatedUsedBytes.Availability == MonitorAvailability.Available &&
            gpu.DedicatedUsedBytes.Value is ulong used && identities[0].DedicatedVideoMemory > 0 &&
            used <= identities[0].DedicatedVideoMemory ? used * 100d / identities[0].DedicatedVideoMemory : null;
        return GfMonitorMetricTimes.FreshAt(result with
        {
            GpuPercent = gpu is null ? null : Read(gpu.BusiestEnginePercent),
            VramPercent = vram, GpuTemperature = temperature, GpuIdentity = key.ToString(),
            MetricTimes = result.MetricTimes! with
            {
                Gpu = wddm?.CapturedAtUtc, Vram = wddm?.CapturedAtUtc,
                GpuTemperature = thermal?.CapturedAtUtc, GpuIdentity = dxgi.CapturedAtUtc
            }
        }, deliveredAt);
    }
}
