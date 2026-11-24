namespace GerenciadorAplicativos;

// Instantes de medição distintos da entrega. Null explícito não vira
// timestamp global. Snapshots legados sem este contrato conservam sua semântica.
internal sealed record GfMonitorMetricTimes(DateTimeOffset? Cpu, DateTimeOffset? Gpu,
    DateTimeOffset? Ram, DateTimeOffset? Vram, DateTimeOffset? CpuTemperature, DateTimeOffset? GpuTemperature)
{
    internal static DateTimeOffset? At(GfResourceMonitorSnapshot s, int index)
    {
        if (s.MetricTimes is not { } t) return s.CapturedAtUtc;
        return index switch { 0 => t.Cpu, 1 => t.Gpu, 2 => t.Ram, 3 => t.Vram,
            4 => t.CpuTemperature, 5 => t.GpuTemperature, _ => null };
    }

    internal static GfResourceMonitorSnapshot FreshAt(GfResourceMonitorSnapshot s, DateTimeOffset now)
    {
        bool Fresh(int i) => At(s, i) is DateTimeOffset at &&
            GfResourceMonitorAdapter.IsFresh(at, now) && at <= s.CapturedAtUtc.AddSeconds(5);
        return s with
        {
            CpuPercent = Fresh(0) ? s.CpuPercent : null,
            GpuPercent = Fresh(1) ? s.GpuPercent : null,
            RamPercent = Fresh(2) ? s.RamPercent : null,
            VramPercent = Fresh(3) ? s.VramPercent : null,
            CpuTemperature = Fresh(4) ? s.CpuTemperature : null,
            GpuTemperature = Fresh(5) ? s.GpuTemperature : null,
            CpuThermalLimit = Fresh(4) ? s.CpuThermalLimit : null,
            GpuThermalLimit = Fresh(5) ? s.GpuThermalLimit : null,
            GpuThermalEvidence = Fresh(5) ? s.GpuThermalEvidence : null
        };
    }
}
