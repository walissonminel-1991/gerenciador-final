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
        bool gap = _last is DateTimeOffset last && capturedAt - last > TimeSpan.FromSeconds(5);
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
