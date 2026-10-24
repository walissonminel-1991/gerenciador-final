using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

internal static class ResidualReportMerger
{
    private static string ProductKey(string product) => Regex.Replace(product.Trim(),
        @"\s+(?:\((?:x64|x86|64-bit|32-bit)\)|x64|x86)$", "",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).ToUpperInvariant();

    // Canonicaliza apenas identidade de exibição. Nunca usado para abrir/apagar caminhos.
    internal static string LocationKey(string location)
    {
        string key = location.TrimEnd('\\', '/');
        if (key.Length > 2 && key[1] == ':') key = key.Replace('/', '\\');
        if (key.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase)) key = "HKEY_CURRENT_USER" + key[4..];
        if (key.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase)) key = "HKEY_LOCAL_MACHINE" + key[4..];
        foreach (string hive in new[] { "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE" })
        {
            string wow = hive + @"\Software\WOW6432Node\";
            if (key.StartsWith(wow, StringComparison.OrdinalIgnoreCase))
                return hive + @" (32 bits)\SOFTWARE\" + key[wow.Length..];
            if (key.StartsWith(hive + @"\", StringComparison.OrdinalIgnoreCase))
                return hive + (Environment.Is64BitProcess ? " (64 bits)" : " (32 bits)") + key[hive.Length..];
        }
        return key;
    }

    // União de locais: um catálogo mais específico não deve ocultar outra fonte.
    // Visões de Registro distintas continuam distintas, inclusive 32/64 bits.
    internal static ResidualReportSet Merge(IEnumerable<ResidualAuditReport> reports,
        bool incomplete = false, ResidualCoverage? coverage = null)
    {
        var input = reports.ToArray();
        coverage = ResidualCoverage.Combine(input.Select(r => r.Coverage)
            .Append((reports as ResidualReportSet)?.Coverage).Append(coverage));
        if (incomplete && !coverage.Incomplete)
            coverage = coverage with { Issues = coverage.Issues | ResidualScanIssue.InventoryPartial };
        incomplete |= coverage.Incomplete || input.Any(r => r.Incomplete);
        if (incomplete && !coverage.Incomplete)
            coverage = coverage with { Issues = ResidualScanIssue.ReadFailure };
        var merged = input.GroupBy(r => ProductKey(r.Product)).Select(group =>
        {
            var all = group.ToArray();
            bool partial = incomplete || all.Any(r => r.Incomplete);
            var findings = all.SelectMany(r => r.Findings).GroupBy(f => LocationKey(f.Location),
                StringComparer.OrdinalIgnoreCase).Select(locations =>
            {
                var first = locations.OrderBy(f => f.Location, StringComparer.OrdinalIgnoreCase).First();
                var disposition = locations.Select(f => f.Disposition).OrderByDescending(Rank).First();
                return first with
                {
                    Sources = locations.Aggregate(ResidualSource.None, (a, f) => a | f.Sources),
                    BasisDisposition = locations.Select(f => f.BasisDisposition ?? f.Disposition).OrderByDescending(Rank).First(),
                    Kind = string.Join(" / ", locations.Select(f => f.Kind).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                    Items = locations.Max(f => f.Items),
                    Evidence = string.Join(" ", locations.Select(f => f.Evidence).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) +
                        (locations.Select(f => f.Disposition).Distinct().Count() > 1 ? " Fontes com classificações diferentes; decisão conservadora." : "") +
                        (partial ? " Análise ou inventário parcial; preservar." : ""),
                    Disposition = partial ? ResidualDisposition.Unknown : disposition
                };
            }).ToArray();
            return new ResidualAuditReport(all.Select(r => r.Product).OrderBy(n => n.Length).ThenBy(n => n, StringComparer.Ordinal).First(), all.All(r => r.UninstallConfirmed) && !partial,
                findings, all.SelectMany(r => r.RelatedRegisteredPrograms)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                all.Any(r => r.Historical), partial, coverage);
        }).ToArray();
        var owners = merged.SelectMany(r => r.Findings.Select(f => (r.Product, Location: LocationKey(f.Location))))
            .GroupBy(x => x.Location, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Product).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        var shared = owners.Where(p => p.Value.Count > 1).Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (location, products) in owners)
        {
            for (int end = location.LastIndexOfAny(['\\', '/']); end > 0;
                end = location.LastIndexOfAny(['\\', '/'], end - 1))
            {
                string parent = location[..end];
                if (owners.TryGetValue(parent, out var parentProducts) &&
                    products.Any(p => parentProducts.Any(q => !StringComparer.OrdinalIgnoreCase.Equals(p, q))))
                { shared.Add(location); shared.Add(parent); }
            }
        }
        var result = merged.Select(r => r with
        {
            Findings = r.Findings.Select(f => shared.Contains(LocationKey(f.Location))
                ? f with { BasisDisposition = ResidualDisposition.PreserveShared, Disposition = r.Incomplete ? ResidualDisposition.Unknown : ResidualDisposition.PreserveShared,
                    Evidence = f.Evidence + " Local igual ou ancestral associado a mais de um aplicativo; preservar." } : f).ToArray()
        }).OrderBy(r => r.Product, StringComparer.OrdinalIgnoreCase).ToArray();
        var reconciled = new ResidualCoverage(ReportsReconciled: input.Length - result.Length,
            LocationsReconciled: input.Sum(r => r.Findings.Count) - result.Sum(r => r.Findings.Count));
        coverage = ResidualCoverage.Combine([coverage, reconciled]);
        return new(result.Select(r => r with { Coverage = coverage }).ToArray(), coverage);
    }
    private static int Rank(ResidualDisposition disposition) => disposition switch
    {
        ResidualDisposition.Unknown => 6,
        ResidualDisposition.InstallationCheck => 5,
        ResidualDisposition.PreserveShared => 4,
        ResidualDisposition.PreserveData => 3,
        ResidualDisposition.Review => 2,
        _ => 1
    };
}
