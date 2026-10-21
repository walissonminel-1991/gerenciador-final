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
    internal int Count => reports.Length;
    internal int Locations { get; }
    internal ResidualResultIndex(IReadOnlyList<ResidualAuditReport> source)
    {
        Coverage = (source as ResidualReportSet)?.Coverage ?? ResidualCoverage.Combine(source.Select(r => r.Coverage));
        reports = source.Select(r => r with
        {
            Findings = Array.AsReadOnly(r.Findings.ToArray()),
            RelatedRegisteredPrograms = Array.AsReadOnly(r.RelatedRegisteredPrograms.ToArray())
        }).OrderBy(r => r.Product, StringComparer.OrdinalIgnoreCase).ToArray();
        search = reports.Select(r => string.Join('\n', new[] { r.Product }
            .Concat(r.Findings.SelectMany(f => new[] { f.Location, f.Kind, f.Evidence }))
            .Concat(r.RelatedRegisteredPrograms))).ToArray();
        Locations = reports.Sum(r => r.Findings.Count);
    }
    internal ResidualResultPage Query(string? text = null, ResidualFilter filter = ResidualFilter.All,
        int page = 0, int pageSize = 50, CancellationToken cancellationToken = default, ResidualSource source = ResidualSource.None)
    {
        pageSize = Math.Clamp(pageSize, 10, 100);
        var matches = new List<ResidualAuditReport>();
        string query = (text ?? "").Trim();
        for (int i = 0; i < reports.Length; i++)
        {
            if ((i & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if ((query.Length == 0 || search[i].Contains(query, StringComparison.OrdinalIgnoreCase)) &&
                Matches(reports[i], filter) && (source == ResidualSource.None || reports[i].Findings.Any(f => (f.Sources & source) != 0))) matches.Add(reports[i]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        int pages = Math.Max(1, (matches.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 0, pages - 1);
        var items = matches.Skip(page * pageSize).Take(pageSize).ToArray();
        return new(items, page, pageSize, Count, matches.Count, Locations,
            items.Sum(r => r.Findings.Count), Coverage);
    }
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
