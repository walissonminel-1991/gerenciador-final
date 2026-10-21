using GerenciadorAplicativos;
using System.Diagnostics;

internal static class ResidualIntegrityTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (int count in new[] { 500, 501, 1000, 5000, 10000 })
        {
            var reports = Enumerable.Range(0, count).Select(i => new ResidualAuditReport(
                "Produto" + i.ToString("D5"), false,
                [new("Local" + i, "Perfil", 1, "fictício", ResidualDisposition.PreserveData)], [])).ToArray();
            var watch = Stopwatch.StartNew();
            var merged = ResidualReportMerger.Merge(reports);
            check(merged.Count == count && merged.Sum(r => r.Findings.Count) == count && !merged.Incomplete,
                $"Saturação {count}: união não perde relatórios nem locais");
            check(merged.Select(r => r.Product).SequenceEqual(ResidualReportMerger.Merge(reports.Reverse()).Select(r => r.Product)),
                $"Saturação {count}: ordenação estável da união");
            var evidence = Enumerable.Range(0, count).SelectMany(i => new[]
            {
                new BroadResidualEvidence("Produto" + i.ToString("D5"), "Perfil" + i, "Dados", "AppData"),
                new BroadResidualEvidence("Produto" + i.ToString("D5"), "Registro" + i, "Chave", "Registry")
            }).ToArray();
            var classified = BroadResidualDiscovery.Classify(evidence, []);
            check(classified.Count == count && !classified.Incomplete && classified.Coverage.CandidatesFound == count &&
                classified.Coverage.ReportsOmitted == 0 && classified.Coverage.LocationsOmitted == 0 &&
                classified.Sum(r => r.Findings.Count) == count * 2 &&
                classified.All(r => r.Findings.All(f => f.Disposition == ResidualDisposition.PreserveData)),
                $"Saturação {count}: todos os grupos e locais disponíveis, sem perda antes da paginação");
            check(classified.Select(r => r.Product).SequenceEqual(BroadResidualDiscovery.Classify(evidence.Reverse(), []).Select(r => r.Product)) &&
                watch.Elapsed < TimeSpan.FromSeconds(30), $"Saturação {count}: seleção reproduzível com custo limitado");
        }
        var boundary = Enumerable.Range(0, 100).SelectMany(i => new[]
        {
            new BroadResidualEvidence("Produto" + i, "Local" + i, "Perfil", "AppData"),
            new BroadResidualEvidence("Produto" + i, "Chave" + i, "Chave", "Registry")
        }).ToArray();
        check(!BroadResidualDiscovery.Classify(boundary, []).Incomplete,
            "Exatos cem grupos não são confundidos com truncamento");
        var locations = Enumerable.Range(0, 8).Select(i => new BroadResidualEvidence("Editor Antigo", "Local" + i,
            "Dados", i == 0 ? "Registry" : "AppData")).ToArray();
        var cut = BroadResidualDiscovery.Classify(locations, []);
        check(!cut.Incomplete && cut.Coverage.LocationsOmitted == 0 && cut.Single().Findings.Count == 8,
            "Oitava localização permanece disponível para consulta progressiva");
        var tooMany = Enumerable.Range(0, 30001).Select(i => new BroadResidualEvidence("Editor Antigo", "Local" + i, "Dados", "AppData")).ToArray();
        var capped = BroadResidualDiscovery.Classify(tooMany, []);
        check(capped.Count == 0 && capped.Incomplete && capped.Coverage.CandidatesNotEvaluated == 1 &&
            capped.Coverage.Issues.HasFlag(ResidualScanIssue.EntryLimit),
            "Fonte com 30.001 evidências informa entrada não avaliada mesmo sem achados");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var cancelled = BroadResidualDiscovery.Classify(boundary, [], cancellationToken: cancellation.Token);
        check(cancelled.Count == 0 && cancelled.Incomplete && cancelled.Coverage.CandidatesNotEvaluated == boundary.Length &&
            cancelled.Coverage.Issues.HasFlag(ResidualScanIssue.Cancelled), "Cancelamento preserva estado parcial vazio");
        var unavailable = new ResidualCoverage(ResidualScanIssue.ReadFailure, SourcesUnavailable: ["Registry"], CandidatesNotEvaluated: null);
        var empty = ResidualReportMerger.Merge([], coverage: unavailable);
        check(empty.Count == 0 && empty.Incomplete && empty.Coverage.Describe().Contains("ANÁLISE PARCIAL") &&
            empty.Coverage.Describe().Contains("quantidade desconhecida"), "Erro de fonte sem achados não equivale a análise concluída");
        var sourceA = new ResidualCoverage(EntriesExamined: 21, CandidatesFound: 3, CandidatesExamined: 5);
        var sourceB = new ResidualCoverage(EntriesExamined: 12, CandidatesFound: 2, CandidatesExamined: 4);
        var composite = ResidualCoverage.Combine([sourceA, sourceB]);
        var nested = ResidualCoverage.Combine([composite, sourceA, sourceB, composite]);
        var repeated = ResidualCoverage.Combine([nested, composite]);
        check(repeated.EntriesExamined == 33 && repeated.CandidatesFound == 5 && repeated.CandidatesExamined == 9,
            "Cobertura de fontes e resumos aninhados contabiliza cada observação uma única vez");
        var noDuplicates = ResidualReportMerger.Merge(new[]
        {
            new ResidualAuditReport("App", false, [new("MesmoLocal", "Perfil", 1, "fonte A")], [], Coverage: unavailable),
            new ResidualAuditReport("App", false, [new("MesmoLocal", "Perfil", 1, "fonte B")], [], Coverage: unavailable)
        });
        check(noDuplicates.Count == 1 && noDuplicates.Single().Findings.Count == 1 && noDuplicates.Incomplete &&
            noDuplicates.Single().Findings.Single().Evidence.Contains("fonte A") && noDuplicates.Single().Findings.Single().Evidence.Contains("fonte B"),
            "Duplicações não apagam evidências nem o estado parcial");
    }
}
