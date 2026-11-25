namespace GerenciadorAplicativos;

// Rascunho isolado: somente capacidades e amostras do sensor principal
// D3DKMT no MESMO adaptador físico e na MESMA geração da topologia.
// Não coleta hardware, não escolhe GPU e não estima limites ausentes.
internal sealed record GfThermalEvidence
{
    internal WddmGpuKey Key { get; }
    internal long Generation { get; }
    internal double WarningC { get; }
    internal double MaximumC { get; }
    internal DateTimeOffset ObservedAtUtc { get; }

    private GfThermalEvidence(WddmGpuKey key, long generation, double warning, double maximum, DateTimeOffset observedAtUtc)
    {
        Key = key; Generation = generation; WarningC = warning; MaximumC = maximum;
        ObservedAtUtc = observedAtUtc;
    }

    internal static GfThermalEvidence? FromD3dKmt(
        WddmGpuKey sampleKey, long sampleGeneration,
        WddmGpuKey capsKey, long capsGeneration,
        bool capabilitiesSucceeded, uint warningTenthsC, uint maximumTenthsC, DateTimeOffset observedAtUtc)
    {
        // Limites conservadores de aceitação do GF, NÃO especificações do hardware.
        // Valores não aceitos mantêm a classificação térmica como desconhecida.
        if (!capabilitiesSucceeded || sampleKey != capsKey || sampleGeneration <= 0 ||
            sampleGeneration != capsGeneration || warningTenthsC is < 300 or > 1500 ||
            maximumTenthsC is < 500 or > 1500 || warningTenthsC >= maximumTenthsC)
            return null;
        return new(sampleKey, sampleGeneration, warningTenthsC / 10d, maximumTenthsC / 10d, observedAtUtc);
    }

    // As capacidades e a temperatura são provenientes da mesma consulta
    // D3DKMT; o snapshot conserva o instante da fonte, não o de entrega.
    internal static GfThermalEvidence? FromPortfolio(HardwareSensorPortfolio portfolio,
        WddmGpuKey selected, long generation, DateTimeOffset now)
    {
        if (portfolio.DxgiAdapters is not { State: HardwareDiscoveryState.Complete } dxgi ||
            !GfResourceMonitorAdapter.IsFresh(dxgi.CapturedAtUtc, now) ||
            dxgi.Adapters.Count(x => x.LuidHigh == selected.LuidHigh && x.LuidLow == selected.LuidLow) != 1 ||
            !dxgi.Adapters.Any(x => x.LuidHigh == selected.LuidHigh && x.LuidLow == selected.LuidLow && !x.IsSoftware) ||
            portfolio.D3dKmtGpuSensors is not { } thermal ||
            !GfResourceMonitorAdapter.IsFresh(thermal.CapturedAtUtc, now)) return null;
        var candidates = thermal.Adapters.Where(x => x.Key == selected).ToArray();
        if (candidates.Length != 1 || candidates[0].TemperatureC.Availability != MonitorAvailability.Available ||
            !GfMeterScale.Valid(candidates[0].TemperatureC.Value, GfMeterKind.Temperature) ||
            candidates[0].TemperatureWarningTenthsC is not uint warning ||
            candidates[0].TemperatureMaxTenthsC is not uint maximum) return null;
        return FromD3dKmt(selected, generation, candidates[0].Key, generation, true,
            warning, maximum, thermal.CapturedAtUtc);
    }

    internal bool SameProfile(GfThermalEvidence other) => Key == other.Key &&
        Generation == other.Generation && WarningC == other.WarningC && MaximumC == other.MaximumC;

    // Capacidades e leitura precisam estar temporalmente associadas. Não aceitar
    // um perfil vencido ou obtido depois da leitura a classificar.
    internal bool Matches(WddmGpuKey key, long generation, DateTimeOffset measured, DateTimeOffset now) =>
        Key == key && Generation == generation &&
        ObservedAtUtc <= measured && measured - ObservedAtUtc <= TimeSpan.FromSeconds(10) &&
        GfResourceMonitorAdapter.IsFresh(ObservedAtUtc, now);

    // Azul e verde são escolhas de apresentação, não limites certificados.
    // Amarelo corresponde ao aviso de throttling reportado pelo driver;
    // vermelho pleno corresponde ao máximo reportado.
    internal double Index(double celsius)
    {
        if (!double.IsFinite(celsius)) return double.NaN;
        double blue = Math.Min(40, WarningC - 20);
        double green = WarningC - 10;
        return celsius <= blue ? 40 : celsius <= green
            ? 40 + 20 * (celsius - blue) / (green - blue)
            : celsius <= WarningC ? 60 + 15 * (celsius - green) / (WarningC - green)
            : Math.Min(100, 75 + 25 * (celsius - WarningC) / (MaximumC - WarningC));
    }
}

internal enum GfThermalAlert { Unknown, BelowWarning, Warning, MaximumReached }

// Estado independente da cor contínua e da EMA. Subida sem atraso;
// recuperação exige margem de 3°C e 3 segundos de amostras contínuas.
// Sem perfil comprovado, nunca classificar temperatura como segura/crítica.
internal sealed class GfThermalAlertLatch
{
    private GfThermalEvidence? _evidence;
    private WddmGpuKey? _selected;
    private long _generation;
    private DateTimeOffset? _last;
    private DateTimeOffset? _recoverySince;
    private GfThermalAlert _state = GfThermalAlert.Unknown;

    internal void Reset()
    {
        _selected = null; _generation = 0; _evidence = null; _last = null; _recoverySince = null;
        _state = GfThermalAlert.Unknown;
    }

    // O host chama no seu ciclo de frescor existente; nenhum timer adicional.
    internal GfThermalAlert Expire(DateTimeOffset now)
    {
        if ((_last is DateTimeOffset last && !GfResourceMonitorAdapter.IsFresh(last, now)) ||
            (_evidence is { } caps && !GfResourceMonitorAdapter.IsFresh(caps.ObservedAtUtc, now)))
        {
            _evidence = null; _recoverySince = null; _state = GfThermalAlert.Unknown;
        }
        return _state;
    }

    internal GfThermalAlert Update(double? rawC, DateTimeOffset capturedAt, DateTimeOffset now,
        WddmGpuKey selected, long generation, GfThermalEvidence? evidence)
    {
        // Uma entrega atrasada não apaga um alerta mais recente, inclusive
        // quando sua métrica está ausente. Mudança de seleção é responsabilidade
        // da thread proprietária; gerações anteriores nunca reabrem a antiga.
        Expire(now);
        if (generation <= 0 || generation < _generation ||
            !GfResourceMonitorAdapter.IsFresh(capturedAt, now)) return _state;
        // Alterar a GPU selecionada exige nova geração do host, não apenas um
        // pacote com outra chave. Isso evita alternância entre GPUs por replay.
        if (_generation == generation && _selected is WddmGpuKey key && key != selected) return _state;
        if (_selected != selected || _generation != generation)
        {
            Reset(); _selected = selected; _generation = generation;
        }
        if (_last is DateTimeOffset old && capturedAt <= old) return _state;
        // A coleta compartilhada da GPU pode ocorrer a cada 8 s. Uma lacuna
        // dentro do mesmo orçamento de frescor não reinicia a histerese.
        bool gap = _last is DateTimeOffset last && capturedAt - last > TimeSpan.FromSeconds(10);
        _last = capturedAt;
        if (evidence is null || !evidence.Matches(selected, generation, capturedAt, now) ||
            !GfMeterScale.Valid(rawC, GfMeterKind.Temperature))
        {
            _evidence = null; _recoverySince = null;
            return _state = GfThermalAlert.Unknown;
        }
        if (gap || _evidence is null || !_evidence.SameProfile(evidence))
        {
            _recoverySince = null; _state = GfThermalAlert.Unknown;
        }
        _evidence = evidence;
        double v = rawC!.Value;
        var immediate = v >= evidence.MaximumC ? GfThermalAlert.MaximumReached :
            v >= evidence.WarningC ? GfThermalAlert.Warning : GfThermalAlert.BelowWarning;
        if (_state == GfThermalAlert.Unknown || (int)immediate >= (int)_state)
        {
            _recoverySince = null; return _state = immediate;
        }
        double threshold = _state == GfThermalAlert.MaximumReached ? evidence.MaximumC : evidence.WarningC;
        if (v > threshold - 3)
        {
            _recoverySince = null; return _state;
        }
        _recoverySince ??= capturedAt;
        if (capturedAt - _recoverySince.Value < TimeSpan.FromSeconds(3)) return _state;
        _recoverySince = null;
        return _state = _state == GfThermalAlert.MaximumReached
            ? GfThermalAlert.Warning : GfThermalAlert.BelowWarning;
    }
}
