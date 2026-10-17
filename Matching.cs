using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Conservador: um nome parecido nunca é suficiente para declarar uma atualização.
internal static class Matching
{
    private static readonly Regex Architecture = new(@"\s*(?:\((?:x64|x86|64.bit|32.bit)\)|\b(?:x64|x86|64.bit|32.bit)\b)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TrailingVersion = new(@"\s+v?\d+(?:\.\d+){1,3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VersionPattern = new(@"^v?\d+(?:\.\d+){0,3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NonLetters = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly string[] LegalSuffixes = [" inc", " incorporated", " ltd", " limited", " llc", " corp", " corporation", " gmbh", " company", " co"];

    internal static string NormalizeName(string? value)
    {
        var s = (value ?? "").Trim();
        s = Architecture.Replace(s, "");
        s = TrailingVersion.Replace(s, "");
        return Normalize(s);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return NonLetters.Replace(sb.ToString(), " ").Trim();
    }

    internal static string NormalizePublisher(string? publisher)
    {
        var s = Normalize(publisher ?? "");
        bool altered;
        do
        {
            altered = false;
            foreach (var suffix in LegalSuffixes)
            {
                if (s.EndsWith(suffix, StringComparison.Ordinal) && s.Length > suffix.Length)
                {
                    s = s[..^suffix.Length];
                    altered = true;
                    break;
                }
            }
        } while (altered);
        return s.Trim();
    }

    internal static int? CompareVersions(string? installed, string? available)
    {
        var a = (installed ?? "").Trim();
        var b = (available ?? "").Trim();
        if (!VersionPattern.IsMatch(a) || !VersionPattern.IsMatch(b)) return null;
        if (!Version.TryParse(a.TrimStart('v', 'V'), out var x) ||
            !Version.TryParse(b.TrimStart('v', 'V'), out var y)) return null;
        return x.CompareTo(y);
    }

    internal static IReadOnlyList<UpdateMatch> Evaluate(IEnumerable<InstalledApp> installed, IEnumerable<CatalogEntry> catalog)
    {
        var byName = catalog
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductVersion))
            .SelectMany(x => new[] { NormalizeName(x.ShortName), NormalizeName(x.ProductName) }
                .Where(k => k.Length > 0).Distinct().Select(k => (key: k, item: x)))
            .GroupBy(x => x.key)
            .ToDictionary(x => x.Key, x => x.Select(v => v.item).Distinct().ToArray());

        var output = new List<UpdateMatch>();
        foreach (var app in installed)
        {
            var name = NormalizeName(app.Name);
            var publisher = NormalizePublisher(app.Publisher);
            if (name.Length == 0 || publisher.Length == 0 || !byName.TryGetValue(name, out var nameMatches))
            {
                output.Add(new(app, null, MatchState.Unmatched));
                continue;
            }

            var matches = nameMatches.Where(x => NormalizePublisher(x.Manufacturer) == publisher).ToArray();
            if (matches.Length == 0)
            {
                output.Add(new(app, null, MatchState.Unmatched));
                continue;
            }
            // Produtos com identificações distintas ou revisões incomparáveis são ambíguos.
            if (matches.Select(x => NormalizeName(x.ShortName)).Distinct().Count() > 1)
            {
                output.Add(new(app, null, MatchState.Ambiguous));
                continue;
            }
            CatalogEntry? newest = null;
            foreach (var candidate in matches)
            {
                if (newest is null) { newest = candidate; continue; }
                int? compare = CompareVersions(newest.ProductVersion, candidate.ProductVersion);
                if (compare is null) { newest = null; break; }
                if (compare < 0) newest = candidate;
            }
            if (newest is null)
            {
                output.Add(new(app, null, MatchState.Ambiguous));
                continue;
            }
            int? versionComparison = CompareVersions(app.Version, newest.ProductVersion);
            var state = versionComparison switch
            {
                null => MatchState.NotComparable,
                < 0 => MatchState.Candidate,
                _ => MatchState.EqualOrNewer
            };
            output.Add(new(app, newest, state));
        }
        return output;
    }
}
