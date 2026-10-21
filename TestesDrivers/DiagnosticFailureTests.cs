using GerenciadorAplicativos;

internal static class DiagnosticFailureTests
{
    internal static void Run(Action<bool, string> check)
    {
        var session = new TestDiagnosticSession();
        session.SetProfile(1, _ => throw new IOException());
        check(!session.Enabled, "Falha do log ao ativar diagnóstico mantém coleta desligada");
        int calls = 0;
        session.SetProfile(1, _ => ++calls == 1);
        check(session.Enabled && !session.Record("AMBIENTE", ("largura", 1280), ("altura", 720),
            ("dpi", 96), ("logicos", 4)) && !session.Enabled,
            "Gravação recusada desativa observações adicionais sem interromper a consulta");
        int writes = 0;
        var churn = new TestDiagnosticSession();
        for (int i = 0; i < 500; i++)
        {
            churn.SetProfile(1 + i % 2, _ => { writes++; return true; });
            churn.SetProfile(0, _ => { writes++; return true; });
        }
        check(writes == 160 && !churn.Enabled,
            "Liga/desliga também respeita o teto acumulado de 160 tentativas por processo");
        int failedWrites = 0;
        for (int i = 0; i < 500; i++)
            churn.SetProfile(1, _ => { failedWrites++; throw new InvalidOperationException(); });
        check(failedWrites == 0, "Teto esgotado não é reiniciado por falha ou troca de perfil");
        var failures = new TestDiagnosticSession();
        for (int i = 0; i < 500; i++) failures.SetProfile(1, _ => { failedWrites++; return false; });
        check(failedWrites == 160 && !failures.Enabled, "Tentativas recusadas não permitem logging ilimitado");
        foreach (var domain in new[] { "MSIX", "PACOTES", "ORIGEM" })
            check(!TestDiagnosticDetail.TryFormat(1, domain, [("caminho", 1)], out _),
                "Novas métricas recusam campos fora do contrato: " + domain);
    }
}
