namespace GerenciadorAplicativos;

// Adaptador puro: não consulta hardware, não cria timer e não escolhe GPU 0.
// O host fornece snapshots já coletados por uma única instância do motor.
internal static class GfResourceMonitorAdapter
{
    // Cada fonte mantém seu próprio relógio. Rejeitar atraso e data futura
    // fora de pequena tolerância, sem reaproveitar valor de cache como atual.
    internal static bool IsFresh(DateTimeOffset measured, DateTimeOffset now) =>
        measured <= now.AddSeconds(5) && measured >= now.AddSeconds(-10);

    private static double? Read(MonitorReading<double> reading) =>
        reading.Availability == MonitorAvailability.Available && reading.Value is double v && double.IsFinite(v) ? v : null;

    internal static GfResourceMonitorSnapshot FromCounters(LiveHardwareSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        double? ram = sample.Memory is { TotalBytes: > 0 } m && m.AvailableBytes <= m.TotalBytes &&
            m.At.Kind == DateTimeKind.Utc && IsFresh(new DateTimeOffset(m.At), sample.CapturedAtUtc)
            ? (m.TotalBytes - m.AvailableBytes) * 100d / m.TotalBytes : null;
        return new(sample.CapturedAtUtc, Read(sample.CpuUsagePercent), null, ram, null,
            Read(sample.Sensors.CpuTemperatureC), null);
    }

    // WDDM não afirma utilização total: o campo é o motor mais ocupado,
    // como no modelo de utilização de GPU do Windows. Não soma engines.
    internal static GfResourceMonitorSnapshot FromPortfolio(HardwareSensorPortfolio portfolio, WddmGpuKey? selected)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        var result = FromCounters(portfolio.WindowsCounters);
        var at = portfolio.CapturedAtUtc;
        if (!IsFresh(result.CapturedAtUtc, at))
            result = result with { CpuPercent = null, RamPercent = null, CpuTemperature = null };
        result = result with { CapturedAtUtc = at };
        if (selected is not WddmGpuKey key || portfolio.DxgiAdapters is null || portfolio.WindowsGpuCounters is null ||
            !IsFresh(portfolio.DxgiAdapters.CapturedAtUtc, at) || !IsFresh(portfolio.WindowsGpuCounters.CapturedAtUtc, at))
            return result;
        var identities = portfolio.DxgiAdapters.Adapters.Where(x => x.LuidHigh == key.LuidHigh && x.LuidLow == key.LuidLow).ToArray();
        var counters = portfolio.WindowsGpuCounters.Adapters.Where(x => x.Key.LuidHigh == key.LuidHigh && x.Key.LuidLow == key.LuidLow).ToArray();
        // Sem inventar capacidade física por LUID em um adaptador vinculado.
        if (identities.Length != 1 || identities[0].IsSoftware || counters.Length != 1 || counters[0].Key != key) return result;
        var gpu = counters[0];
        var physical = portfolio.D3dKmtGpuSensors?.Adapters.Where(x => x.Key.LuidHigh == key.LuidHigh && x.Key.LuidLow == key.LuidLow).ToArray();
        double? vram = key.Physical == 0 && physical is not { Length: > 1 } && gpu.DedicatedUsedBytes.Availability == MonitorAvailability.Available &&
            gpu.DedicatedUsedBytes.Value is ulong used && identities[0].DedicatedVideoMemory > 0 &&
            used <= identities[0].DedicatedVideoMemory
            ? used * 100d / identities[0].DedicatedVideoMemory : null;
        var temperatures = portfolio.D3dKmtGpuSensors?.Adapters.Where(x => x.Key == key).ToArray();
        double? temperature = portfolio.D3dKmtGpuSensors is { } thermal && IsFresh(thermal.CapturedAtUtc, at) &&
            temperatures is { Length: 1 } ? Read(temperatures[0].TemperatureC) : null;
        // D3DKMT ainda não publica o limite no contrato de saída; não adivinhar.
        return result with { GpuPercent = Read(gpu.BusiestEnginePercent), VramPercent = vram,
            GpuTemperature = temperature, GpuIdentity = key.ToString() };
    }
}
