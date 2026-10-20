using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

internal static class ResidualReportMerger
{
    private static string ProductKey(string product) => Regex.Replace(product.Trim(),
        @"\s+(?:\((?:x64|x86|64-bit|32-bit)\)|x64|x86)$", "",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).ToUpperInvariant();

    // União de locais: um catálogo mais específico não deve ocultar outra fonte.
    // Visões de Registro distintas continuam distintas, inclusive 32/64 bits.
    internal static IReadOnlyList<ResidualAuditReport> Merge(IEnumerable<ResidualAuditReport> reports,
        bool incomplete = false)
    {
        var merged = reports.Take(500).GroupBy(r => ProductKey(r.Product)).Select(group =>
        {
            var all = group.ToArray();
            bool partial = incomplete || all.Any(r => r.Incomplete);
            var findings = all.SelectMany(r => r.Findings).GroupBy(f => f.Location.TrimEnd('\\', '/'),
                StringComparer.OrdinalIgnoreCase).Select(locations =>
            {
                var first = locations.First();
                var disposition = locations.Select(f => f.Disposition).OrderByDescending(Rank).First();
                return first with
                {
                    Items = locations.Max(f => f.Items),
                    Evidence = string.Join(" ", locations.Select(f => f.Evidence).Distinct(StringComparer.Ordinal)) +
                        (partial ? " Análise ou inventário parcial; preservar." : ""),
                    Disposition = partial ? ResidualDisposition.Unknown : disposition
                };
            }).ToArray();
            return new ResidualAuditReport(all[0].Product, all.All(r => r.UninstallConfirmed) && !partial,
                findings, all.SelectMany(r => r.RelatedRegisteredPrograms)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                all.Any(r => r.Historical), partial);
        }).ToArray();
        var shared = merged.SelectMany(r => r.Findings.Select(f => (r.Product, f.Location)))
            .GroupBy(x => x.Location.TrimEnd('\\', '/'), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.Product).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return merged.Select(r => r with
        {
            Findings = r.Findings.Select(f => shared.Contains(f.Location.TrimEnd('\\', '/'))
                ? f with { Disposition = r.Incomplete ? ResidualDisposition.Unknown : ResidualDisposition.PreserveShared,
                    Evidence = f.Evidence + " Mesmo local associado a mais de um aplicativo; preservar." } : f).ToArray()
        }).ToArray();
    }
    private static int Rank(ResidualDisposition disposition) => disposition switch
    {
        ResidualDisposition.Unknown => 5,
        ResidualDisposition.PreserveShared => 4,
        ResidualDisposition.PreserveData => 3,
        ResidualDisposition.Review => 2,
        _ => 1
    };
}
