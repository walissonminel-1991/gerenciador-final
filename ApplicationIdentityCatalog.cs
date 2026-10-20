using System.Text.Json;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

internal sealed record UnifiedAppRule(
    string Id, string Name, IReadOnlyList<string> Names, IReadOnlyList<string> Publishers,
    string? OfficialSource, string? CleanupPolicy);

// Source-of-truth identity; no URLs, no executable commands, no deletion targets.
// The JSON resource is embedded at build time and cannot be modified by web catalogs.
internal static class ApplicationIdentityCatalog
{
    private sealed class Document
    {
        public int Schema { get; set; }
        public List<Raw>? Rules { get; set; }
    }
    private sealed class Raw
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<string>? Names { get; set; }
        public List<string>? Publishers { get; set; }
        public string? OfficialSource { get; set; }
        public string? CleanupPolicy { get; set; }
    }

    private static readonly Lazy<IReadOnlyList<UnifiedAppRule>> Saved =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    internal static IReadOnlyList<UnifiedAppRule> Rules => Saved.Value;
    internal static int RuleCount => Rules.Count;

    private static IReadOnlyList<UnifiedAppRule> Load()
    {
        try
        {
            using var stream = typeof(ApplicationIdentityCatalog).Assembly
                .GetManifestResourceStream("GerenciadorFinal.ApplicationIdentityCatalog");
            if (stream is null || stream.Length is <= 0 or > 65536) return [];
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        { return []; } // fail-closed: only disables integrated identity, never Windows inventory
    }

    internal static IReadOnlyList<UnifiedAppRule> Parse(string json)
    {
        var doc = JsonSerializer.Deserialize<Document>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (doc?.Schema != 1 || doc.Rules is null || doc.Rules.Count is < 1 or > 100)
            throw new InvalidDataException("Unexpected catalog schema.");
        var rules = new List<UnifiedAppRule>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in doc.Rules)
        {
            if (string.IsNullOrWhiteSpace(r.Id) || r.Id.Length > 45 ||
                !Regex.IsMatch(r.Id, "^[a-z0-9-]+$", RegexOptions.CultureInvariant) ||
                !ids.Add(r.Id) || string.IsNullOrWhiteSpace(r.Name) ||
                r.Names is null || r.Names.Count is < 1 or > 12 ||
                r.Publishers is null || r.Publishers.Count is < 1 or > 10 ||
                r.Names.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 120 ||
                    n.Count(c => c == '*') > 1 ||
                    (n.Contains('*') && (!n.EndsWith('*') || n.Length < 5))) ||
                r.Publishers.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 100 ||
                    p.Contains('*')))
                throw new InvalidDataException("Unsafe, duplicate or incomplete identity.");

            if (r.OfficialSource is not null &&
                (!r.OfficialSource.Equals(r.Id, StringComparison.OrdinalIgnoreCase) ||
                 !providers.Add(r.OfficialSource)))
                throw new InvalidDataException("Conflicting official update provider identity.");
            if (r.CleanupPolicy is not null &&
                !Regex.IsMatch(r.CleanupPolicy, "^[a-z0-9-]+$", RegexOptions.CultureInvariant))
                throw new InvalidDataException("Invalid cleanup policy reference.");
            rules.Add(new(r.Id, r.Name, r.Names, r.Publishers,
                r.OfficialSource, r.CleanupPolicy));
        }
        // Ambiguous aliases may never silently grant update or deletion rights.
        for (int a = 0; a < rules.Count; a++)
            for (int b = a + 1; b < rules.Count; b++)
                if (rules[a].Names.Any(x => rules[b].Names.Contains(x, StringComparer.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Alias shared between different products.");
        return rules;
    }

    private static string NormalizePublisher(string? input)
    {
        string text = Regex.Replace((input ?? "").Trim().ToLowerInvariant(), @"[^\p{L}\p{Nd}]+", " ").Trim();
        bool changed;
        do
        {
            changed = false;
            foreach (string suffix in new[] { " corporation", " incorporated", " inc", " ltd",
                " limited", " llc", " gmbh", " company", " co" })
            {
                if (text.Length > suffix.Length && text.EndsWith(suffix, StringComparison.Ordinal))
                {
                    text = text[..^suffix.Length].Trim();
                    changed = true; break;
                }
            }
        } while (changed);
        return text;
    }

    internal static bool NameMatches(string value, string alias)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(alias)) return false;
        if (!alias.EndsWith('*')) return value.Equals(alias, StringComparison.OrdinalIgnoreCase);
        string prefix = alias[..^1];
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        string rest = value[prefix.Length..];
        if (rest.Length == 0) return false;
        if (prefix.EndsWith(" (", StringComparison.Ordinal))
            return rest.StartsWith("x64", StringComparison.OrdinalIgnoreCase) ||
                   rest.StartsWith("x86", StringComparison.OrdinalIgnoreCase) ||
                   rest.StartsWith("64-bit", StringComparison.OrdinalIgnoreCase) ||
                   rest.StartsWith("32-bit", StringComparison.OrdinalIgnoreCase);
        if (prefix.EndsWith("(x64", StringComparison.OrdinalIgnoreCase))
            return rest[0] is ')' or ' ';
        if (prefix.EndsWith(' ')) return char.IsAsciiDigit(rest[0]) ||
            (prefix.StartsWith("Plex Media Server", StringComparison.OrdinalIgnoreCase)
                && char.IsAsciiLetter(rest[0]));
        return rest[0] is ' ' or '(' or '-';
    }

    internal static UnifiedAppRule? Resolve(string? name, string? publisher)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(publisher)) return null;
        string maker = NormalizePublisher(publisher);
        if (maker.Length == 0) return null;
        UnifiedAppRule? found = null;
        foreach (var rule in Rules)
        {
            if (!rule.Names.Any(n => NameMatches(name, n))) continue;
            if (!rule.Publishers.Any(p => NormalizePublisher(p) == maker)) continue;
            if (found is not null) return null;
            found = rule;
        }
        return found;
    }

    internal static bool MatchesOfficialSource(string name, string publisher, string sourceId)
        => Resolve(name, publisher) is { OfficialSource: not null } rule &&
           rule.OfficialSource.Equals(sourceId, StringComparison.OrdinalIgnoreCase);

    internal static bool IsStillInstalled(string name, string publisher,
        IEnumerable<(string Name, string Publisher)> installed)
    {
        var rule = Resolve(name, publisher);
        if (rule is null)
            return installed.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return installed.Any(p =>
            Resolve(p.Name, p.Publisher)?.Id.Equals(rule.Id, StringComparison.OrdinalIgnoreCase) == true ||
            (rule.Names.Any(alias => NameMatches(p.Name, alias)) &&
             string.IsNullOrWhiteSpace(p.Publisher)));
    }
}
