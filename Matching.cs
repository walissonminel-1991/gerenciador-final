using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Conservador: um nome parecido nunca é suficiente para declarar uma atualização.
internal static class Matching
{
    private static readonly Regex Architecture = new(@"\s*(?:\((?:x64|x86|64.bit|32.bit)\)|\b(?:x64|x86|64.bit|32.bit)\b)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AnyArchitecture = new(@"(?<![a-z0-9])(?:x64|x86|64[\s-]?bit|32[\s-]?bit)(?![a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TrailingVersion = new(@"\s+v?\d+(?:\.\d+){1,3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VersionPattern = new(@"^v?\d+(?:\.\d+){0,3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NonLetters = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly string[] LegalSuffixes = [" inc", " incorporated", " ltd", " limited", " llc", " corp", " corporation", " gmbh", " company", " co"];

    internal static string NormalizeName(string? value)
    {
        var s = (value ?? "").Trim();
        s = Architecture.Replace(s, "");
        s = TrailingVersion.Replace(s, "");
        s = AnyArchitecture.Replace(s, "");
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

    private static string GetArchitecture(string? name)
    {
        var m = AnyArchitecture.Match(name ?? "");
        if (!m.Success) return "";
        string value = m.Value.ToLowerInvariant();
        return value is "x64" or "64bit" or "64-bit" or "64 bit" ? "x64" : "x86";
    }

    private static bool IsSideBySideRuntime(string name)
    {
        var n = NormalizeName(name);
        return n.Contains("net runtime", StringComparison.Ordinal) ||
               n.Contains("asp net core", StringComparison.Ordinal) ||
               n.Contains("net sdk", StringComparison.Ordinal) ||
               n.Contains("desktop runtime", StringComparison.Ordinal) ||
               n.Contains("net hosting", StringComparison.Ordinal) ||
               n.Contains("net framework", StringComparison.Ordinal);
    }

    internal static bool IsUncertain(MatchState state) =>
        state is MatchState.Ambiguous or MatchState.NotComparable or MatchState.DifferentMajor;

    internal static int? CompareVersions(string? installed, string? available)
    {
        var a = (installed ?? "").Trim();
        var b = (available ?? "").Trim();
        if (!VersionPattern.IsMatch(a) || !VersionPattern.IsMatch(b)) return null;
        if (!Version.TryParse(a.TrimStart('v', 'V'), out var x) ||
            !Version.TryParse(b.TrimStart('v', 'V'), out var y)) return null;
        return x.CompareTo(y);
    }

    private static bool SameVisualCpp2013Family(InstalledApp installed, InstalledApp other)
    {
        const string prefix = "Microsoft Visual C++ 2013 Redistributable";
        return installed.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               other.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               NormalizePublisher(installed.Publisher) == "microsoft" &&
               NormalizePublisher(other.Publisher) == "microsoft" &&
               GetArchitecture(installed.Name) is "x86" or "x64" &&
               GetArchitecture(installed.Name) == GetArchitecture(other.Name);
    }

    internal static IReadOnlyList<UpdateMatch> Evaluate(IEnumerable<InstalledApp> installed, IEnumerable<CatalogEntry> catalog)
    {
        // O registro do Windows pode preservar entradas antigas do Visual C++ após
        // instalar o redistribuível atualizado. Não oferecer reinstalação em ciclo.
        var installedRows = installed.ToArray();
        var byName = catalog
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductVersion))
            .SelectMany(x => new[] { NormalizeName(x.ShortName), NormalizeName(x.ProductName) }
                .Where(k => k.Length > 0).Distinct().Select(k => (key: k, item: x)))
            .GroupBy(x => x.key)
            .ToDictionary(x => x.Key, x => x.Select(v => v.item).Distinct().ToArray());

        var output = new List<UpdateMatch>();
        foreach (var app in installedRows)
        {
            var name = NormalizeName(app.Name);
            var publisher = NormalizePublisher(app.Publisher);
            if (name.Length == 0 || publisher.Length == 0 || !byName.TryGetValue(name, out var nameMatches))
            {
                output.Add(new(app, null, MatchState.Unmatched));
                continue;
            }

            var samePublisher = nameMatches.Where(x => NormalizePublisher(x.Manufacturer) == publisher).ToArray();
            if (samePublisher.Length == 0)
            {
                output.Add(new(app, null, MatchState.Unmatched));
                continue;
            }
            string installedArch = GetArchitecture(app.Name);
            bool isRuntime = IsSideBySideRuntime(app.Name);
            bool requiresExactArch = isRuntime ||
                (name.Contains("visual c", StringComparison.Ordinal) && name.Contains("redistributable", StringComparison.Ordinal));
            var matches = samePublisher.Where(x =>
            {
                string shortArch = GetArchitecture(x.ShortName);
                string productArch = GetArchitecture(x.ProductName);
                if (shortArch.Length > 0 && productArch.Length > 0 && shortArch != productArch) return false;
                string availableArch = productArch.Length > 0 ? productArch : shortArch;
                if (requiresExactArch && (installedArch.Length == 0 || availableArch.Length == 0))
                    return false;
                return installedArch.Length == 0 || availableArch.Length == 0 || installedArch == availableArch;
            }).ToArray();
            if (matches.Length == 0)
            {
                output.Add(new(app, null, MatchState.Ambiguous));
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
                < 0 when isRuntime &&
                    Version.TryParse(app.Version.TrimStart('v', 'V'), out var installedVersion) &&
                    Version.TryParse(newest.ProductVersion.TrimStart('v', 'V'), out var availableVersion) &&
                    installedVersion.Major != availableVersion.Major => MatchState.DifferentMajor,
                < 0 => MatchState.Candidate,
                _ => MatchState.EqualOrNewer
            };
            if (state == MatchState.Candidate &&
                installedRows.Any(other =>
                    SameVisualCpp2013Family(app, other) &&
                    other != app &&
                    CompareVersions(other.Version, newest.ProductVersion) is >= 0))
            {
                // Mantemos o registro antigo visível em "Todos", mas não afirmamos
                // que exista uma atualização pendente para a arquitetura atendida.
                state = MatchState.SupersededRegistration;
            }
            output.Add(new(app, newest, state));
        }
        return output;
    }
}
