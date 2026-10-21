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
        // PC fraco v5.13.11.19: filtrar "Dados comuns" mantinha grupo
        // McAfee mas exibia também Registro e Roaming. Filtrar ambos os níveis.
        var threeSources = new ResidualAuditReport("Produto teste", false, new[]
        {
            new ResidualAuditFinding(@"C:\ProgramData\ProdutoTeste", "Dados comuns", 1,
                "comum", ResidualDisposition.PreserveData, ResidualSource.CommonData),
            new ResidualAuditFinding(@"C:\Users\Teste\AppData\Roaming\ProdutoTeste", "Preferências", 1,
                "privado", ResidualDisposition.PreserveData, ResidualSource.UserData),
            new ResidualAuditFinding(@"HKEY_LOCAL_MACHINE\SOFTWARE\ProdutoTeste", "Registro", 1,
                "registro", ResidualDisposition.PreserveShared, ResidualSource.Registry)
        }, []);
        var filtered = new ResidualResultIndex([threeSources]);
        var commonOnly = filtered.Query(source: ResidualSource.CommonData);
        check(commonOnly.Matching == 1 && commonOnly.LocationsAvailable == 1 &&
            commonOnly.Reports.Single().Findings.Single().Sources == ResidualSource.CommonData &&
            commonOnly.Reports.Single().Findings.Single().Location.Contains("ProgramData") &&
            !filtered.Query().Reports.Single().Findings.Any(f => f.Location == "MUTADO"),
            "Dados comuns mostra apenas locais ProgramData, sem Registry nem Roaming");
        var onlyRegistry = filtered.Query(source: ResidualSource.Registry, filter: ResidualFilter.Shared);
        check(onlyRegistry.Matching == 1 && onlyRegistry.LocationsAvailable == 1 &&
            onlyRegistry.Reports.Single().Findings.Single().Sources == ResidualSource.Registry,
            "Registro mais compartilhado mantém somente a interseção de locais");
        check(filtered.Query(source: ResidualSource.CommonData, filter: ResidualFilter.Shared).Matching == 0 &&
            filtered.Query(source: ResidualSource.UserData, filter: ResidualFilter.Data).Reports.Single().Findings.Count == 1,
            "Duas classificações não misturam origens de locais diferentes no mesmo grupo");
        var searchLocal = filtered.Query("privado", source: ResidualSource.UserData);
        check(searchLocal.Matching == 1 && searchLocal.LocationsAvailable == 1 &&
            searchLocal.Reports.Single().Findings.Single().Evidence == "privado" &&
            filtered.Query("privado", source: ResidualSource.CommonData).Matching == 0,
            "Busca e origem devem coincidir no mesmo local, sem falso positivo por outro campo");
        check(filtered.Query("Produto teste").LocationsAvailable == 3 &&
            filtered.Query().LocationsAvailable == 3 &&
            filtered.Query().Reports.Single().Findings.Count == 3,
            "Remover filtros recupera a fotografia completa sem perda de locais");
        var mutable = new[] { new ResidualAuditReport("Original", false, findings, []) };
        var snapshot = new ResidualResultIndex(mutable);
        mutable[0] = mutable[0] with { Product = "Alterado" }; findings[0] = findings[0] with { Location = "Outro" };
        check(snapshot.Query().Reports.Single().Product == "Original" && snapshot.Query().Reports.Single().Findings[0].Location == "Local0",
            "Índice preserva fotografia da consulta, independente das listas do chamador");
        // PC fraco v5.13.11.21: fontes indisponíveis e limites devem ter
        // motivo explícito, sem contaminar a fonte consultada anteriormente.
        var unavailableBudget = new ResidualScanBudget();
        unavailableBudget.Source("AppData");
        unavailableBudget.Unavailable("ProgramData");
        check(unavailableBudget.Coverage.Issues.HasFlag(ResidualScanIssue.SourceUnavailable) &&
            !unavailableBudget.Coverage.Issues.HasFlag(ResidualScanIssue.ReadFailure) &&
            unavailableBudget.Coverage.SourcesUnavailable!.SequenceEqual(["ProgramData"]),
            "Fonte indisponível não contamina origem anterior nem simula erro de leitura");
        var failedBudget = new ResidualScanBudget();
        failedBudget.Source("AppData");
        failedBudget.MarkIncomplete();
        check(failedBudget.Coverage.Issues.HasFlag(ResidualScanIssue.ReadFailure) &&
            failedBudget.Coverage.SourcesWithReadFailures!.SequenceEqual(["AppData"]) &&
            failedBudget.Coverage.SourcesUnavailable!.Count == 0,
            "Falha pontual preserva a fonte como parcialmente lida, não inteiramente indisponível");
        var combinedFaults = ResidualCoverage.Combine([unavailableBudget.Coverage, failedBudget.Coverage]);
        check(combinedFaults.SourcesWithReadFailures!.SequenceEqual(["AppData"]) &&
            combinedFaults.SourcesUnavailable!.SequenceEqual(["ProgramData"]) &&
            combinedFaults.Describe().Contains("FONTES COM FALHAS PONTUAIS", StringComparison.Ordinal) &&
            combinedFaults.Describe().Contains("FONTES NÃO CONSULTADAS OU INACESSÍVEIS", StringComparison.Ordinal),
            "Agregação preserva distinção entre leitura parcial e ausência de cobertura");
        var reasons = new ResidualCoverage(ResidualScanIssue.EntryLimit | ResidualScanIssue.SourceUnavailable);
        check(reasons.ReasonSummary.Contains("limite de entradas", StringComparison.Ordinal) &&
            reasons.ReasonSummary.Contains("fonte indisponível", StringComparison.Ordinal) &&
            reasons.Describe().Contains(reasons.ReasonSummary, StringComparison.Ordinal) &&
            new ResidualCoverage().ReasonSummary == "nenhum",
            "Resumo seguro distingue limite de quantidade de fonte não acessível");
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
        // Alguns produtores combinam a cobertura do conjunto sem propagar
        // a flag especifica de uma linha. Recuperar a flag sem duplicar contagens.
        var partialGroupCoverage = new ResidualCoverage(
            ResidualScanIssue.ReadFailure, EntriesExamined: 3,
            CandidatesNotEvaluated: null);
        var inconsistentSet = new ResidualReportSet(
            [new ResidualAuditReport("Leitura parcial", false, [], [],
                Coverage: partialGroupCoverage)],
            new ResidualCoverage(EntriesExamined: 8, CandidatesNotEvaluated: 0));
        var corrected = new ResidualResultIndex(inconsistentSet).Query().Coverage;
        check(corrected.Issues.HasFlag(ResidualScanIssue.ReadFailure) &&
            corrected.EntriesExamined == 8 && corrected.CandidatesNotEvaluated is null,
            "Flag de falha local propagada sem somar novamente contagens do agregado");
        var missingOptionalCoverage = new ResidualResultIndex(new[]
        {
            new ResidualAuditReport("Sem cobertura extra", false, [], [])
        }).Query().Coverage;
        check(!missingOptionalCoverage.Incomplete &&
            missingOptionalCoverage.CandidatesNotEvaluated == 0,
            "Relatorio sem cobertura individual nao implica parcialidade falsa");
        var mutableSources = new List<string> { "AppData" };
        var mutableUnavailable = new List<string> { "Registry" };
        var mutableReadFailures = new List<string> { "ProgramData" };
        var originalCoverage = new ResidualCoverage(
            ResidualScanIssue.SourceUnavailable,
            SourcesConsulted: mutableSources, SourcesUnavailable: mutableUnavailable,
            SourcesWithReadFailures: mutableReadFailures);
        var immutablePage = new ResidualResultIndex(
            new ResidualReportSet([], originalCoverage));
        mutableSources.Clear(); mutableUnavailable.Clear(); mutableReadFailures.Clear();
        check(immutablePage.Query().Coverage.SourcesConsulted!.SequenceEqual(new[] { "AppData" }) &&
            immutablePage.Query().Coverage.SourcesUnavailable!.SequenceEqual(new[] { "Registry" }) &&
            immutablePage.Query().Coverage.SourcesWithReadFailures!.SequenceEqual(new[] { "ProgramData" }),
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
