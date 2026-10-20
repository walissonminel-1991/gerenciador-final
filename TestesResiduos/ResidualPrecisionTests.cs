using GerenciadorAplicativos;
using System.Diagnostics;

internal static class ResidualPrecisionTests
{
    private static BroadResidualEvidence[] Pair(string product, string data = "AppData") =>
        [new(product, "Perfil" + product, "Dados", data), new(product, "Registro" + product, "Preferências", "Registry")];
    internal static void Run(Action<bool, string> check)
    {
        foreach (string name in new[] { "Aquiris", "BANDAI NAMCO Entertainment", "Colossal Order", "Crystal Dynamics",
            "Eidos Montreal", "Guerrilla Games", "nWay Inc", "Opera Software", "Red Dot Games", "Sucker Punch Productions", "Western Digital" })
            check(BroadResidualDiscovery.Classify(Pair(name), []).Single().Findings.All(f =>
                f.Disposition == ResidualDisposition.PreserveShared && !f.CanRemove), "Fabricante preservado: " + name);
        foreach (string name in new[] { "foobar2000", "LegacyEditor", "VLC Player" })
            check(BroadResidualDiscovery.Classify(Pair(name, "ProgramData"), []).Single().Findings.All(f =>
                f.Disposition == ResidualDisposition.PreserveData), "Perfis desconhecidos, configurações e dados compartilháveis são preservados: " + name);
        var installed = new[] { new ProgramEntry { Name = "Editor Pro 2", Publisher = "Empresa A" } };
        check(BroadResidualDiscovery.Classify(Pair("Editor"), installed).Count == 0 &&
            BroadResidualDiscovery.Classify(Pair("EditorPro"), installed).Count == 1,
            "Índice preserva prefixo por palavra sem confundir produto homônimo");
        check(BroadResidualDiscovery.Classify(Pair("Netease"), []).Single().Findings.All(f => f.Disposition == ResidualDisposition.Unknown),
            "Netease exige conferência de instalação sem assumir desinstalação");
        check(BroadResidualDiscovery.Classify(Pair("EditorPortatil").Append(new("EditorPortatil", "Instalacao", "Pasta", "ProgramFiles")), [])
            .Single().Findings.All(f => f.Disposition == ResidualDisposition.Unknown), "Instalação portátil ainda possível impede conclusão de resíduo");
        var conflict = ResidualReportMerger.Merge(new[]
        {
            new ResidualAuditReport("App A", false, [new(@"C:\Vendor", "Fabricante", 1, "pai")], []),
            new ResidualAuditReport("App B", false, [new(@"C:\Vendor\Produto", "Produto", 1, "filho")], []),
            new ResidualAuditReport("App C", false, [new(@"C:\VendorOutro", "Produto", 1, "prefixo")], [])
        });
        check(conflict.Where(r => r.Product != "App C").SelectMany(r => r.Findings).All(f => f.Disposition == ResidualDisposition.PreserveShared) &&
            conflict.Single(r => r.Product == "App C").Findings.Single().Disposition == ResidualDisposition.Review,
            "Sobreposição de pai/filho preserva ambos sem confundir prefixo textual");
        var registry = ResidualReportMerger.Merge(new[]
        {
            new ResidualAuditReport("App", false, [new(@"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Editor", "Histórico", 1, "fonte A", ResidualDisposition.PreserveData)], []),
            new ResidualAuditReport("App", false, [new(@"HKEY_LOCAL_MACHINE (32 bits)\SOFTWARE\Editor", "Geral", 1, "fonte B")], []),
            new ResidualAuditReport("App", false, [new(@"HKEY_LOCAL_MACHINE (64 bits)\SOFTWARE\Editor", "Outra arquitetura", 1, "fonte C")], [])
        });
        check(registry.Single().Findings.Count == 2 && registry.Single().Findings.Any(f =>
            f.Evidence.Contains("fonte A") && f.Evidence.Contains("fonte B") && f.Kind.Contains("Histórico") && f.Kind.Contains("Geral")),
            "Aliases da mesma visão do Registro reconciliados; visões 32/64 continuam separadas");
        var contradictory = BroadResidualDiscovery.Classify(new[]
        {
            new BroadResidualEvidence("Editor Antigo", "LocalUnico", "Perfil", "AppData"),
            new BroadResidualEvidence("Editor Antigo", "LocalUnico", "Serviço", "Registry")
        }, []);
        check(contradictory.Incomplete && contradictory.Coverage.Issues.HasFlag(ResidualScanIssue.ContradictoryEvidence) &&
            contradictory.Single().Findings.Single().Disposition == ResidualDisposition.Unknown,
            "Local único com fontes incompatíveis informa contradição em vez de confiança artificial");
        var budget = new ResidualScanBudget(maxEntries: 10);
        budget.MarkIncomplete(ResidualScanIssue.EntryLimit);
        check(budget.Visit() && budget.Incomplete, "Limite local não cancela a leitura das outras fontes");
        var evidence = Enumerable.Range(0, 5000).SelectMany(i => Pair("Produto" + i)).ToArray();
        var processes = Enumerable.Range(0, 1000).Select(i => "Processo" + i + "64").ToArray();
        var inventory = Enumerable.Range(0, 5000).Select(i => new ProgramEntry { Name = "Instalado" + i, Publisher = "Fabricante" + i }).ToArray();
        var clock = Stopwatch.StartNew();
        var large = BroadResidualDiscovery.Classify(evidence, inventory, runningProcessNames: processes);
        check(large.Coverage.CandidatesFound == 5000 && large.Coverage.ReportsOmitted == 4900 &&
            !large.Coverage.Issues.HasFlag(ResidualScanIssue.Timeout) && clock.Elapsed < TimeSpan.FromSeconds(20),
            "Dez mil evidências, cinco mil instalações e mil processos: comparação indexada sem omissão silenciosa");
        Console.WriteLine("Métrica sintética de classificação: " + clock.ElapsedMilliseconds + " ms (não representa PC real).");
    }
}
