namespace GerenciadorAplicativos;

// Adaptação de apresentação: sem acesso a hardware ou a capacidades por nome.
// O relay carimba a geração do host; a evidência deve ter a mesma chave física.
internal sealed class GfThermalPresentation
{
    private readonly GfThermalAlertLatch _latch = new();
    private string? _identity;
    private long _generation;
    private GfThermalAlert _alert;

    internal void Reset()
    {
        _latch.Reset(); _identity = null; _generation = 0; _alert = GfThermalAlert.Unknown;
    }

    internal GfThermalEvidence? Evidence(GfResourceMonitorSnapshot snapshot, DateTimeOffset now)
    {
        var caps = snapshot.GpuThermalEvidence;
        if (caps is null || snapshot.Generation <= 0 ||
            GfMonitorMetricTimes.At(snapshot, 5) is not DateTimeOffset measured ||
            !GfResourceMonitorAdapter.IsFresh(measured, now) ||
            !string.Equals(snapshot.GpuIdentity, caps.Key.ToString(), StringComparison.Ordinal) ||
            !caps.Matches(caps.Key, snapshot.Generation, measured, now)) return null;
        return caps;
    }

    internal GfThermalAlert Update(GfResourceMonitorSnapshot snapshot, DateTimeOffset now)
    {
        if (_generation != snapshot.Generation || !string.Equals(_identity, snapshot.GpuIdentity, StringComparison.Ordinal))
        {
            Reset(); _identity = snapshot.GpuIdentity; _generation = snapshot.Generation;
        }
        var caps = Evidence(snapshot, now);
        if (caps is null)
        {
            _latch.Reset(); return _alert = GfThermalAlert.Unknown;
        }
        return _alert = _latch.Update(snapshot.GpuTemperature, GfMonitorMetricTimes.At(snapshot, 5) ?? snapshot.CapturedAtUtc, now, caps.Key, snapshot.Generation, caps);
    }

    internal bool Expire(DateTimeOffset now)
    {
        var previous = _alert;
        _alert = _latch.Expire(now);
        return _alert != previous;
    }

    internal GfMeterFrame Frame(GfResourceMonitorSnapshot snapshot, double? smoothed,
        V269Theme theme, DateTimeOffset now)
    {
        if (!GfMeterScale.Valid(snapshot.GpuTemperature, GfMeterKind.Temperature))
            return GfMeterScale.Frame(GfMeterKind.Temperature, null, theme);
        var caps = Evidence(snapshot, now);
        // Sem par comprovado, manter somente referência; não reutilizar o
        // máximo de uma evidência vencida ou ligada a outro componente.
        if (caps is null || _alert == GfThermalAlert.Unknown ||
            !GfMeterScale.Valid(snapshot.GpuTemperature, GfMeterKind.Temperature))
            return GfMeterScale.Frame(GfMeterKind.Temperature, smoothed, theme,
                snapshot.GpuThermalEvidence is null ? snapshot.GpuThermalLimit : null);
        double raw = snapshot.GpuTemperature!.Value;
        double value = GfMeterScale.Valid(smoothed, GfMeterKind.Temperature) ? smoothed!.Value : raw;
        bool immediate = raw >= caps.WarningC;
        if (immediate) value = raw;
        double floor = _alert == GfThermalAlert.MaximumReached ? 100 : _alert == GfThermalAlert.Warning ? 75 : 0;
        double index = Math.Max(floor, caps.Index(value));
        return new(Math.Clamp(value / caps.MaximumC, 0, 1),
            GfMeterScale.ScaleColor(GfMeterKind.Temperature, index, theme),
            GfMeterState.Running, immediate, GfMeterKind.Temperature, index);
    }

    internal string Description(GfResourceMonitorSnapshot snapshot, DateTimeOffset now)
    {
        if (Evidence(snapshot, now) is not { } caps || _alert == GfThermalAlert.Unknown)
            return " (sem par térmico comprovado; escala de referência)";
        string state = _alert switch
        {
            GfThermalAlert.MaximumReached => "limite máximo atingido ou em recuperação",
            GfThermalAlert.Warning => "faixa de aviso ou em recuperação",
            _ => "abaixo do aviso reportado"
        };
        return $" ({state}; aviso {caps.WarningC:0.#}°C; máximo {caps.MaximumC:0.#}°C)";
    }
}
