using GerenciadorAplicativos;

internal static class ResidualProgressiveTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (int count in new[] { 500, 1000, 5000, 10000 })
        {
            var source = Enumerable.Range(0, count).Select(i => new ResidualAuditReport("Produto" + i.ToString("D5"), false,
                [new("Local" + i, "Perfil", 1, "Fabricante" + (i % 5), i % 2 == 0 ? ResidualDisposition.PreserveData : ResidualDisposition.PreserveShared)], [])).ToArray();
            var index = new ResidualResultIndex(source);
            var visited = new HashSet<string>();
            for (int page = 0; page < index.Query().PageCount; page++)
                foreach (var report in index.Query(page: page).Reports)
                    if (!visited.Add(report.Product)) throw new Exception("Identidade repetida entre páginas");
            check(visited.Count == count && index.Query().NotLoaded == count - 50,
                $"Navegação {count}: todos disponíveis, sem corte nem duplicação entre páginas");
            var search = index.Query("Fabricante3");
            check(search.Matching == count / 5 && search.Reports.All(r => r.Findings.Single().Evidence.Contains("Fabricante3")),
                $"Busca {count}: fabricante pesquisado na coleção inteira");
            check(index.Query(filter: ResidualFilter.Shared).Matching == count / 2,
                $"Filtro {count}: classificação considera grupos além da primeira página");
            check(index.Query(page: int.MaxValue).PageIndex == index.Query().PageCount - 1,
                $"Página {count}: índices inválidos limitados sem perder estado");
        }
        var cached = new ResidualResultIndex(Enumerable.Range(0, 500).Select(i => new ResidualAuditReport("App" + i, false, [], [])).ToArray());
        cached.Query(); cached.Query(page: 1); cached.Query(page: 2);
        check(cached.FilterPasses == 1, "Trocar página reutiliza filtro em memória sem reexaminar candidatos ou disco");
        cached.Query("App1");
        check(cached.FilterPasses == 2 && cached.Query("App1", page: 1).Matching == 111, "Cache é invalidado por critério e mantém resultado global");
        PerformanceSample? sample = null;
        using (LocalPerformanceMetrics.Measure(PerformanceStage.ResidualFilter, s => sample = s))
            _ = new byte[5000];
        check(sample is not null && sample.DurationMs >= 0 && sample.AllocatedBytes >= 0 &&
            !sample.Format().Contains("App1") && !sample.Format().Contains(Path.GetTempPath()), "Métrica agregada mede processo sem nomes ou caminhos");
        var findings = Enumerable.Range(0, 1000).Select(i => new ResidualAuditFinding("Local" + i, "Dados", 1, "preservar")).ToArray();
        var reportMany = new ResidualAuditReport("Editor Antigo", false, findings, []);
        check(Enumerable.Range(0, 34).SelectMany(p => ResidualResultIndex.LocationPage(reportMany, p)).Count() == 1000,
            "Mil locais do mesmo produto podem ser apresentados progressivamente");
        var mutable = new[] { new ResidualAuditReport("Original", false, findings, []) };
        var snapshot = new ResidualResultIndex(mutable);
        mutable[0] = mutable[0] with { Product = "Alterado" }; findings[0] = findings[0] with { Location = "Outro" };
        check(snapshot.Query().Reports.Single().Product == "Original" && snapshot.Query().Reports.Single().Findings[0].Location == "Local0",
            "Índice preserva fotografia da consulta, independente das listas do chamador");
        var coverage = new ResidualCoverage(ResidualScanIssue.ReadFailure, SourcesUnavailable: ["Registry"], CandidatesNotEvaluated: null);
        var empty = new ResidualResultIndex(new ResidualReportSet([], coverage));
        check(empty.Query().Coverage.Incomplete && empty.Query().Matching == 0 && empty.Query().Coverage.SourcesUnavailable!.Count == 1,
            "Paginação vazia preserva fonte inacessível e parcialidade da descoberta");
        // Integradores legados podem entregar Incomplete=true com Coverage
        // sem flags; a UI nao deve esconder a parcialidade em uma página vazia.
        var incompleteLegacy = new ResidualResultIndex(new[]
        {
            new ResidualAuditReport("Programa de teste", false, [], [],
                Incomplete: true)
        });
        check(incompleteLegacy.Query().Coverage.Incomplete &&
            incompleteLegacy.Query().Coverage.Issues.HasFlag(ResidualScanIssue.ReportIncomplete) &&
            incompleteLegacy.Query().Coverage.Describe().Contains("relatório de origem incompleto", StringComparison.Ordinal) &&
            incompleteLegacy.Query().Coverage.CandidatesNotEvaluated is null,
            "Relatorio parcialmente classificado nunca aparece como busca concluida");
        var mutableSources = new List<string> { "AppData" };
        var mutableUnavailable = new List<string> { "Registry" };
        var originalCoverage = new ResidualCoverage(
            ResidualScanIssue.SourceUnavailable,
            SourcesConsulted: mutableSources, SourcesUnavailable: mutableUnavailable);
        var immutablePage = new ResidualResultIndex(
            new ResidualReportSet([], originalCoverage));
        mutableSources.Clear(); mutableUnavailable.Clear();
        check(immutablePage.Query().Coverage.SourcesConsulted!.SequenceEqual(new[] { "AppData" }) &&
            immutablePage.Query().Coverage.SourcesUnavailable!.SequenceEqual(new[] { "Registry" }),
            "Fotografia de cobertura nao muda apos mutacao das listas da origem");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        bool stopped = false;
        try { snapshot.Query(cancellationToken: cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
        bool creationStopped = false;
        try { _ = new ResidualResultIndex(mutable, cancelled.Token); } catch (OperationCanceledException) { creationStopped = true; }
        check(creationStopped, "Fechamento cancela construção do índice antes de copiar resultados");
        check(stopped && snapshot.Query().Matching == 1, "Cancelar filtro não destrói a fotografia nem resultados anteriores");
        var one = new ResidualAuditReport("Editor", false, [new("MesmoLocal", "Perfil", 1, "A")], []);
        var merged = ResidualReportMerger.Merge([one, one]);
        check(merged.Count == 1 && merged.Coverage.ReportsReconciled == 1 && merged.Coverage.LocationsReconciled == 1,
            "Duplicatas reconciliadas são contabilizadas separadamente de locais não examinados");
    }
}
