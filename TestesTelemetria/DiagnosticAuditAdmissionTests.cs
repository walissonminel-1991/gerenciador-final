using GerenciadorAplicativos;

internal static class DiagnosticAuditAdmissionTests
{
    internal static void Run(Action<bool, string> verify)
    {
        var writes = new List<string>();
        bool available = true;
        var gate = new DiagnosticAuditAdmission(marker => { writes.Add(marker); return available; });
        verify(!gate.TryAuthorizeMutation() && writes.Count == 0, "GF-03: sem startup confirmado, nenhum marcador nem admissão");
        verify(gate.CompleteStartup(true) && gate.IsReady &&
            writes.SequenceEqual(new[] { "APP.INICIALIZACAO_INICIO" }), "GF-03: primeira gravação confirma sessão");
        verify(gate.TryAuthorizeMutation() && writes[^1] == DiagnosticAuditAdmission.MutationMarker,
            "GF-03: admissão exige gravação imediatamente anterior");
        available = false;
        int effects = 0;
        if (gate.TryAuthorizeMutation()) effects++;
        verify(gate.IsDegraded && effects == 0, "GF-03: perda de gravação durante sessão impede efeito");
        available = true;
        int count = writes.Count;
        if (gate.TryAuthorizeMutation()) effects++;
        verify(!gate.CompleteStartup(true) && !gate.IsReady && effects == 0 && writes.Count == count,
            "GF-03: recuperação do destino não rearma sessão degradada");

        int attempted = 0;
        var missing = new DiagnosticAuditAdmission(_ => { attempted++; return true; });
        verify(!missing.CompleteStartup(false) && missing.IsDegraded && !missing.TryAuthorizeMutation() && attempted == 0,
            "GF-03: nenhum destino gravável falha fechado");
        var firstWrite = new DiagnosticAuditAdmission(_ => false);
        verify(!firstWrite.CompleteStartup(true) && firstWrite.IsDegraded && !firstWrite.TryAuthorizeMutation(),
            "GF-03: falha na primeira linha não admite operação");

        DiagnosticAuditAdmission? race = null;
        race = new DiagnosticAuditAdmission(_ => { race!.ObserveFailure(); return true; });
        verify(!race.CompleteStartup(true) && race.IsDegraded, "GF-03: falha concorrente não é sobrescrita por Ready");

        var external = new DiagnosticAuditAdmission(_ => true);
        verify(external.CompleteStartup(true), "GF-03: sessão íntegra inicial");
        external.ObserveFailure();
        verify(!external.TryAuthorizeMutation() && external.IsDegraded,
            "GF-03: falha observada em escrita diagnóstica também fecha admissão");

        bool throwNow = false;
        var exception = new DiagnosticAuditAdmission(_ => throwNow ? throw new IOException("fixture") : true);
        verify(exception.CompleteStartup(true), "GF-03: sessão íntegra antes de exceção");
        throwNow = true;
        try { exception.TryAuthorizeMutation(); }
        catch (IOException) { }
        verify(exception.IsDegraded && !exception.TryAuthorizeMutation(), "GF-03: exceção não deixa sessão rearmável");
    }
}
