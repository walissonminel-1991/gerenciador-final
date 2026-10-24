namespace GerenciadorAplicativos;

internal enum ResidualFilter { All, Review, Data, Shared, Installation, Unknown }
internal sealed record ResidualResultPage(IReadOnlyList<ResidualAuditReport> Reports,
    int PageIndex, int PageSize, int TotalAvailable, int Matching, int LocationsAvailable,
    int LocationsOnPage, ResidualCoverage Coverage)
{
    internal int PageCount => Math.Max(1, (Matching + PageSize - 1) / PageSize);
    internal int NotLoaded => Math.Max(0, Matching - Reports.Count);
    internal string Summary => $"{Matching} de {TotalAvailable} grupos disponíveis; " +
        $"página {PageIndex + 1}/{PageCount}, {Reports.Count} nesta página e {NotLoaded} nas demais. " +
        $"{LocationsAvailable} locais disponíveis no resultado. Nenhum grupo é descartado pela paginação.";
}

// Índice em memória da consulta atual: nunca acessa disco/rede nem concede capacidades.
// Filtros se aplicam à coleção inteira, antes de escolher a página.
internal sealed class ResidualResultIndex
{
    private readonly ResidualAuditReport[] reports;
    private readonly string[] search;
    internal ResidualCoverage Coverage { get; }
    private sealed record CachedQuery(string Text, ResidualFilter Filter, ResidualSource Source,
        int[] Indices, int MatchingLocations);
    private CachedQuery? cache;
    private int filterPasses;
    internal int FilterPasses => Volatile.Read(ref filterPasses);
    internal int Count => reports.Length;
    internal int Locations { get; }
    internal ResidualResultIndex(IReadOnlyList<ResidualAuditReport> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceCoverage = (source as ResidualReportSet)?.Coverage ??
            ResidualCoverage.Combine(source.Select(r => r.Coverage));
        // Alguns produtores legados marcam a linha como incompleta sem
        // preencher Issues na cobertura agregada. Nunca apresentar isso como
        // verificacao concluida, mesmo que nao existam locais a mostrar.
        // Um produtor tambem pode esquecer de incluir Issues individuais
        // na cobertura da colecao. Reconciliar flags sem somar contagens
        // novamente (as coberturas podem conter as mesmas observacoes).
        var reportIssues = source.Aggregate(ResidualScanIssue.None,
            (flags, report) => flags | (report.Coverage?.Issues ?? ResidualScanIssue.None));
        bool missingDetail = source.Any(r => r.Incomplete) &&
            (reportIssues | sourceCoverage.Issues) == ResidualScanIssue.None;
        bool unknownCount = source.Any(r => r.Coverage is { CandidatesNotEvaluated: null });
        sourceCoverage = sourceCoverage with
        {
            Issues = sourceCoverage.Issues | reportIssues |
                (missingDetail ? ResidualScanIssue.ReportIncomplete : ResidualScanIssue.None),
            CandidatesNotEvaluated = missingDetail || unknownCount
                ? null : sourceCoverage.CandidatesNotEvaluated
        };
        // A fotografia nao pode mudar quando o produtor reutiliza ou altera
        // as listas de fontes apos entregar os resultados a janela.
        Coverage = sourceCoverage with
        {
            SourcesConsulted = sourceCoverage.SourcesConsulted?.ToArray(),
            SourcesUnavailable = sourceCoverage.SourcesUnavailable?.ToArray(),
            SourcesWithReadFailures = sourceCoverage.SourcesWithReadFailures?.ToArray(),
            SourceDetails = sourceCoverage.SourceDetails?.ToArray()
        };
        reports = source.Select(r =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return r with
        {
            Findings = Array.AsReadOnly(r.Findings.ToArray()),
            RelatedRegisteredPrograms = Array.AsReadOnly(r.RelatedRegisteredPrograms.ToArray())
        }; }).OrderBy(r => r.Product, StringComparer.OrdinalIgnoreCase).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        search = reports.Select(r => { cancellationToken.ThrowIfCancellationRequested(); return string.Join('\n', new[] { r.Product }
            .Concat(r.Findings.SelectMany(f => new[] { f.Location, f.Kind, f.Evidence }))
            .Concat(r.RelatedRegisteredPrograms)); }).ToArray();
        Locations = reports.Sum(r => r.Findings.Count);
    }
    internal ResidualResultPage Query(string? text = null, ResidualFilter filter = ResidualFilter.All,
        int page = 0, int pageSize = 50, CancellationToken cancellationToken = default, ResidualSource source = ResidualSource.None)
    {
        pageSize = Math.Clamp(pageSize, 10, 100);
        cancellationToken.ThrowIfCancellationRequested();
        string query = (text ?? "").Trim();
        var current = Volatile.Read(ref cache);
        if (current is null || current.Text != query || current.Filter != filter || current.Source != source)
        {
            using var measurement = LocalPerformanceMetrics.Measure(PerformanceStage.ResidualFilter);
            var matches = new List<int>();
            int matchingLocations = 0;
            for (int i = 0; i < reports.Length; i++)
            {
                if ((i & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (!Matches(reports[i], filter) ||
                    (query.Length != 0 && !search[i].Contains(query, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var visible = VisibleFindings(reports[i], filter, source, query);
                // Um relatorio sem locais ainda pode ter uma advertencia relevante,
                // mas jamais corresponde a filtro de origem sem local dessa origem.
                if (visible.Length == 0 &&
                    (source != ResidualSource.None || reports[i].Findings.Count != 0))
                    continue;
                matches.Add(i);
                matchingLocations += visible.Length;
            }
            cancellationToken.ThrowIfCancellationRequested();
            current = new(query, filter, source, matches.ToArray(), matchingLocations);
            Volatile.Write(ref cache, current);
            Interlocked.Increment(ref filterPasses);
        }
        int pages = Math.Max(1, (current.Indices.Length + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 0, pages - 1);
        var items = current.Indices.Skip(page * pageSize).Take(pageSize)
            .Select(i =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var report = reports[i];
                var visible = VisibleFindings(report, filter, source, query);
                return visible.Length == report.Findings.Count ? report :
                    report with { Findings = Array.AsReadOnly(visible) };
            }).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new(items, page, pageSize, Count, current.Indices.Length,
            current.MatchingLocations, items.Sum(r => r.Findings.Count), Coverage);
    }

    // O filtro deve afetar tanto o grupo quanto os locais exibidos. Antes,
    // selecionar "Dados comuns" exibia tambem locais do Registro e AppData
    // de um produto com ao menos uma evidencia em ProgramData.
    private static ResidualAuditFinding[] VisibleFindings(ResidualAuditReport report,
        ResidualFilter filter, ResidualSource source, string query)
    {
        bool identityMatch = query.Length == 0 ||
            report.Product.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            report.RelatedRegisteredPrograms.Any(name =>
                name.Contains(query, StringComparison.OrdinalIgnoreCase));
        bool groupContext = (filter == ResidualFilter.Installation &&
            report.RelatedRegisteredPrograms.Count > 0) ||
            (filter == ResidualFilter.Unknown && report.Incomplete);
        return report.Findings.Where(f =>
            (source == ResidualSource.None || (f.Sources & source) != 0) &&
            (filter == ResidualFilter.All || groupContext || MatchesFinding(f, filter)) &&
            (identityMatch || f.Location.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                f.Kind.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                f.Evidence.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
    }

    private static bool MatchesFinding(ResidualAuditFinding f, ResidualFilter filter) => filter switch
    {
        ResidualFilter.Review => f.Disposition is ResidualDisposition.Review or ResidualDisposition.ValidatedCandidate,
        ResidualFilter.Data => f.Disposition == ResidualDisposition.PreserveData ||
            f.BasisDisposition == ResidualDisposition.PreserveData,
        ResidualFilter.Shared => f.Disposition == ResidualDisposition.PreserveShared ||
            f.BasisDisposition == ResidualDisposition.PreserveShared,
        ResidualFilter.Installation => f.Disposition == ResidualDisposition.InstallationCheck ||
            f.BasisDisposition == ResidualDisposition.InstallationCheck ||
            f.Kind.StartsWith("Instalação", StringComparison.OrdinalIgnoreCase),
        ResidualFilter.Unknown => f.Disposition == ResidualDisposition.Unknown,
        _ => true
    };

    internal static bool Matches(ResidualAuditReport report, ResidualFilter filter) => filter switch
    {
        ResidualFilter.All => true,
        ResidualFilter.Review => report.Findings.Any(f => f.Disposition is ResidualDisposition.Review or ResidualDisposition.ValidatedCandidate),
        ResidualFilter.Data => report.Findings.Any(f => f.Disposition == ResidualDisposition.PreserveData || f.BasisDisposition == ResidualDisposition.PreserveData),
        ResidualFilter.Shared => report.Findings.Any(f => f.Disposition == ResidualDisposition.PreserveShared || f.BasisDisposition == ResidualDisposition.PreserveShared),
        ResidualFilter.Installation => report.RelatedRegisteredPrograms.Count > 0 ||
            report.Findings.Any(f => f.Disposition == ResidualDisposition.InstallationCheck || f.BasisDisposition == ResidualDisposition.InstallationCheck || f.Kind.StartsWith("Instalação", StringComparison.OrdinalIgnoreCase)),
        _ => report.Incomplete || report.Findings.Any(f => f.Disposition == ResidualDisposition.Unknown)
    };
    internal static IReadOnlyList<ResidualAuditFinding> LocationPage(ResidualAuditReport report, int page, int pageSize = 30)
    {
        pageSize = Math.Clamp(pageSize, 10, 100);
        page = Math.Clamp(page, 0, Math.Max(0, (report.Findings.Count - 1) / pageSize));
        return report.Findings.Skip(page * pageSize).Take(pageSize).ToArray();
    }
}
