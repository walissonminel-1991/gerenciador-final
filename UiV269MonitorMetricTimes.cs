namespace GerenciadorAplicativos;

// Instantes de medição distintos da entrega. Null explícito não vira
// timestamp global. Snapshots legados sem este contrato conservam sua semântica.
internal sealed record GfMonitorMetricTimes(DateTimeOffset? Cpu, DateTimeOffset? Gpu,
    DateTimeOffset? Ram, DateTimeOffset? Vram, DateTimeOffset? CpuTemperature, DateTimeOffset? GpuTemperature,
    DateTimeOffset? GpuIdentity = null)
{
    internal static DateTimeOffset? At(GfResourceMonitorSnapshot s, int index)
    {
        if (s.MetricTimes is not { } t) return s.CapturedAtUtc;
        return index switch { 0 => t.Cpu, 1 => t.Gpu, 2 => t.Ram, 3 => t.Vram,
            4 => t.CpuTemperature, 5 => t.GpuTemperature, _ => null };
    }

    internal static GfResourceMonitorSnapshot FreshAt(GfResourceMonitorSnapshot s, DateTimeOffset now)
    {
        bool FreshTime(DateTimeOffset at) =>
            GfResourceMonitorAdapter.IsFresh(at, now) && at <= s.CapturedAtUtc.AddSeconds(5);
        bool Fresh(int i) => At(s, i) is DateTimeOffset at && FreshTime(at);
        // Quando o adaptador fornece o instante DXGI, sua expiração também
        // invalida métricas dependentes, sem falsificar os tempos WDDM/D3DKMT.
        bool identity = s.MetricTimes?.GpuIdentity is not DateTimeOffset identityAt || FreshTime(identityAt);
        bool cpu = Fresh(0), gpu = identity && Fresh(1), ram = Fresh(2),
            vram = identity && Fresh(3), cpuTemp = Fresh(4), gpuTemp = identity && Fresh(5);
        // Caminho ocioso: devolver a mesma instância sem alocar um record a
        // cada Tick, inclusive quando todas as métricas já estão indisponíveis.
        if ((cpu || s.CpuPercent is null) && (gpu || s.GpuPercent is null) &&
            (ram || s.RamPercent is null) && (vram || s.VramPercent is null) &&
            (cpuTemp || (s.CpuTemperature is null && s.CpuThermalLimit is null)) &&
            (gpuTemp || (s.GpuTemperature is null && s.GpuThermalLimit is null && s.GpuThermalEvidence is null)) &&
            (identity || s.GpuIdentity is null)) return s;
        return s with
        {
            CpuPercent = cpu ? s.CpuPercent : null,
            GpuPercent = gpu ? s.GpuPercent : null,
            RamPercent = ram ? s.RamPercent : null,
            VramPercent = vram ? s.VramPercent : null,
            CpuTemperature = cpuTemp ? s.CpuTemperature : null,
            GpuTemperature = gpuTemp ? s.GpuTemperature : null,
            CpuThermalLimit = cpuTemp ? s.CpuThermalLimit : null,
            GpuThermalLimit = gpuTemp ? s.GpuThermalLimit : null,
            GpuThermalEvidence = gpuTemp ? s.GpuThermalEvidence : null,
            GpuIdentity = identity ? s.GpuIdentity : null,
            GpuDisplayName = identity ? s.GpuDisplayName : null
        };
    }
}
