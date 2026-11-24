using GerenciadorAplicativos;

// Rascunho de testes sintéticos. Registrar Run na suíte existente somente
// depois da aprovação visual e da revisão do contrato real de capacidades.
internal static class GfThermalEvidenceTests
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Monitor GF térmico: " + message);
    }

    internal static void Run()
    {
        var a = new WddmGpuKey(1, 2, 0);
        var b = new WddmGpuKey(1, 2, 1);
        const long generation = 4;
        DateTimeOffset t = DateTimeOffset.UtcNow;
        var first = GfThermalEvidence.FromD3dKmt(a, generation, a, generation, true, 700, 800, t);
        var second = GfThermalEvidence.FromD3dKmt(b, generation, b, generation, true, 850, 1000, t);
        Require(first is not null && second is not null, "pares nativos válidos");
        var p = first!; var q = second!;
        Require(p.WarningC == 70 && p.MaximumC == 80, "conversão décimos de Celsius");
        Require(p.Index(70) == 75 && p.Index(80) == 100 && q.Index(85) == 75 && q.Index(100) == 100,
            "âncoras de aviso/máximo específicas do componente");
        Require(p.Index(75) > q.Index(75), "mesma temperatura, limites diferentes");
        Require(GfThermalEvidence.FromD3dKmt(a, generation, b, generation, true, 700, 800, t) is null,
            "não cruzar adaptadores físicos");
        Require(GfThermalEvidence.FromD3dKmt(a, generation, a, generation - 1, true, 700, 800, t) is null,
            "não reutilizar capacidades da topologia antiga");
        Require(GfThermalEvidence.FromD3dKmt(a, generation, a, generation, false, 700, 800, t) is null &&
            GfThermalEvidence.FromD3dKmt(a, generation, a, generation, true, 0, 800, t) is null &&
            GfThermalEvidence.FromD3dKmt(a, generation, a, generation, true, 800, 800, t) is null,
            "consulta falha, zero e limites incoerentes não provam perfil");

        var latch = new GfThermalAlertLatch();
        GfThermalAlert Read(double? v, int seconds, GfThermalEvidence? caps = null) =>
            latch.Update(v, t.AddSeconds(seconds), t.AddSeconds(seconds), a, generation,
                caps ?? GfThermalEvidence.FromD3dKmt(a, generation, a, generation, true, 700, 800, t.AddSeconds(seconds)));
        Require(Read(65, 0) == GfThermalAlert.BelowWarning, "primeira amostra");
        Require(Read(70, 1) == GfThermalAlert.Warning, "aviso sem atraso");
        Require(Read(69, 2) == GfThermalAlert.Warning && Read(67, 3) == GfThermalAlert.Warning &&
            Read(66, 4) == GfThermalAlert.Warning && Read(67, 5) == GfThermalAlert.Warning &&
            Read(66, 6) == GfThermalAlert.BelowWarning, "recuperação com margem e duração");
        Require(Read(80, 7) == GfThermalAlert.MaximumReached, "máximo imediato, sem EMA");
        Require(Read(79, 8) == GfThermalAlert.MaximumReached, "não piscar no limite");
        Require(Read(60, 7) == GfThermalAlert.MaximumReached, "amostra fora de ordem não rebaixa alerta");
        Require(Read(null, 7) == GfThermalAlert.MaximumReached, "ausência atrasada não apaga estado recente");
        Require(Read(60, 9) == GfThermalAlert.MaximumReached && Read(60, 10) == GfThermalAlert.MaximumReached &&
            Read(60, 12) == GfThermalAlert.Warning, "recuperação do máximo não pula aviso");
        Require(Read(60, 13) == GfThermalAlert.Warning && Read(60, 16) == GfThermalAlert.BelowWarning,
            "recuperação da faixa de aviso independente");
        Require(Read(null, 17) == GfThermalAlert.Unknown, "perda do sensor elimina classificação");
        Require(Read(75, 18, q) == GfThermalAlert.Unknown, "não aplicar limite de outra GPU");
        Require(latch.Update(80, t.AddSeconds(30), t.AddSeconds(30), a, generation, p) == GfThermalAlert.Unknown,
            "não classificar leitura com capacidades vencidas");
        Require(latch.Update(80, t.AddSeconds(31), t.AddSeconds(31), a, generation, null) == GfThermalAlert.Unknown,
            "sem evidência, sem classificação absoluta");
        Require(Read(81, 32) == GfThermalAlert.MaximumReached, "leitura acima do máximo não deve desaparecer");
        var other = GfThermalEvidence.FromD3dKmt(b, generation, b, generation, true, 850, 1000, t.AddSeconds(33));
        Require(latch.Update(50, t.AddSeconds(33), t.AddSeconds(33), b, generation, other) == GfThermalAlert.MaximumReached,
            "troca de seleção exige nova geração");
        Require(latch.Expire(t.AddSeconds(43)) == GfThermalAlert.Unknown, "expirar sem novas leituras");
        var next = GfThermalEvidence.FromD3dKmt(b, generation + 1, b, generation + 1, true, 850, 1000, t.AddSeconds(44));
        Require(latch.Update(85, t.AddSeconds(44), t.AddSeconds(44), b, generation + 1, next) == GfThermalAlert.Warning,
            "nova geração aceita a GPU selecionada");
        Require(Read(65, 45) == GfThermalAlert.Warning, "geração anterior não reabre seleção antiga");

        latch.Reset();
        Require(Read(81, 0) == GfThermalAlert.MaximumReached, "máximo na coleta compartilhada");
        Require(Read(60, 8) == GfThermalAlert.MaximumReached, "o primeiro intervalo de 8 s não limpa o máximo");
        Require(Read(60, 16) == GfThermalAlert.Warning, "recuperação em duas etapas na cadência real");
        Require(Read(60, 24) == GfThermalAlert.Warning &&
            Read(60, 32) == GfThermalAlert.BelowWarning, "recuperação confirmada por amostras distintas");
        Require(Read(81, 40) == GfThermalAlert.MaximumReached &&
            Read(60, 51) == GfThermalAlert.BelowWarning, "lacuna maior que frescor inicia classificação nova");
    }
}
