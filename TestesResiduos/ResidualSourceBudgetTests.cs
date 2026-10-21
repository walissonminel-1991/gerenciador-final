using GerenciadorAplicativos;

internal static class ResidualSourceBudgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        var budget = new ResidualScanBudget(maxEntries: 6);
        budget.Source("AppData"); budget.Visit(); budget.Visit();
        budget.MarkIncomplete(ResidualScanIssue.EntryLimit | ResidualScanIssue.DepthLimit);
        budget.Source("ProgramData"); budget.Visit(); budget.MarkIncomplete();
        var coverage = budget.Coverage;
        check(coverage.SourceDetails!.Single(x => x.Source == "AppData") is { Entries: 2 } app &&
            app.Issues.HasFlag(ResidualScanIssue.DepthLimit) &&
            coverage.SourceDetails!.Single(x => x.Source == "ProgramData") is { Entries: 1, Issues: ResidualScanIssue.ReadFailure },
            "Limites locais e ACL são atribuídos à origem correta sem interromper outra fonte");
        check(coverage.SourceDetails!.Sum(x => x.Entries) == coverage.EntriesExamined &&
            coverage.Describe().Contains("128 filhos") && coverage.Describe().Contains("LIMITES POR ORIGEM"),
            "Resumo explica por que rapidez não implica cobertura completa");
        var combined = ResidualCoverage.Combine([coverage, ResidualCoverage.Combine([coverage])]);
        check(combined.EntriesExamined == 3 && combined.SourceDetails!.Sum(x => x.Entries) == 3,
            "Combinar resumo e origem não duplica contadores por fonte");
        budget.Source("AppData"); budget.Visit();
        check(coverage.SourceDetails!.Single(x => x.Source == "AppData").Entries == 2,
            "Fotografia de métricas não muda com continuação do produtor");
        budget.Unavailable("StartMenu");
        check(budget.Coverage.SourceDetails!.Single(x => x.Source == "StartMenu").Issues.HasFlag(ResidualScanIssue.SourceUnavailable) &&
            !budget.Coverage.SourceDetails!.Single(x => x.Source == "AppData").Issues.HasFlag(ResidualScanIssue.SourceUnavailable),
            "Fonte indisponível distinta não contamina a última raiz consultada");
        string root = Path.Combine(Path.GetTempPath(), "GF-Budget-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string vendor = Path.Combine(root, "Fabricante Ficticio");
            for (int i = 0; i < 150; i++) Directory.CreateDirectory(Path.Combine(vendor, "Produto" + i));
            var nativeBudget = new ResidualScanBudget();
            var found = new List<BroadResidualEvidence>();
            int entries = 0, restricted = 0; bool partial = false;
            BroadResidualDiscovery.InspectDirectory(root, "Dados locais", "AppData", found,
                ref entries, ref restricted, ref partial, nativeBudget);
            check(partial && entries == 129 && nativeBudget.Entries == entries &&
                nativeBudget.Coverage.SourceDetails!.Single().Entries == entries &&
                nativeBudget.Issues.HasFlag(ResidualScanIssue.EntryLimit),
                "150 filhos: examina raiz + 128, sem contabilizar o item que excedeu o limite");
            var portable = BroadResidualDiscovery.Classify([
                new("Editor Portatil", Path.Combine(root, "Editor Portatil"), "Instalação", "ProgramFiles"),
                new("Editor Portatil", Path.Combine(root, "Dados"), "Dados", "AppData")], []);
            check(portable.Count == 1 && portable.All(r => r.Findings.All(f => !f.CanRemove)) &&
                portable.Single().Findings.All(x => x.Disposition == ResidualDisposition.InstallationCheck),
                "Software portátil sem ARP continua instalação a conferir");
            var homonyms = BroadResidualDiscovery.Classify([
                new("Editor Homonimo", Path.Combine(root, "A"), "Dados", "AppData", "Fabricante A"),
                new("Editor Homonimo", "HKEY_CURRENT_USER\\Software\\Editor Homonimo", "Registro", "Registry", "Fabricante B")], []);
            check(homonyms.Count == 1 && homonyms.Single().Findings.All(f => !f.CanRemove) &&
                homonyms.Single().Findings.All(x => x.Disposition == ResidualDisposition.Unknown),
                "Homônimos sob fabricantes diferentes permanecem ambíguos e preservados");
        }
        finally { Directory.Delete(root, true); }
    }
}
