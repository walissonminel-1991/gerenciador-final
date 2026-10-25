using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

internal enum IdentityConfidence { Unmatched, Suggestion, Corroborated, Strong, Ambiguous, Blocked }

// Facts, not permissions. A provider may only report what it actually observed.
// InstalledVersion belongs to the local registration, never the available release.
internal sealed record PackageIdentity(string Provider, string PackageId, string Name, string Publisher,
    string InstalledVersion = "", string Architecture = "unknown", string Scope = "unknown",
    string Channel = "unknown", string Location = "", string ProductCode = "", string UpgradeCode = "",
    string PackageFamilyName = "", string Signer = "", bool Managed = false);
internal sealed record IdentityMatch(IdentityConfidence Confidence, string Reason, IReadOnlyList<string> Evidence)
{
    internal bool Discovered => Confidence is IdentityConfidence.Corroborated or IdentityConfidence.Strong;
    // Execution always requires the separate operation-specific validation gate.
}

internal static class PackageIdentityMatcher
{
    private static string Key(string value) => string.Concat(value.Normalize(System.Text.NormalizationForm.FormKC)
        .ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '+'));
    private static bool Known(string value) => !string.IsNullOrWhiteSpace(value) && value != "unknown";
    private static bool Different(string a, string b) => Known(a) && Known(b) && !a.Equals(b, StringComparison.OrdinalIgnoreCase);
    private static bool SameCode(string a, string b) => Guid.TryParse(a, out var x) && x != Guid.Empty && Guid.TryParse(b, out var y) && x == y;
    internal static bool ValidId(string id) => id.Length is > 0 and <= 256 &&
        Regex.IsMatch(id, @"^[A-Za-z0-9][A-Za-z0-9._+\-]*$", RegexOptions.CultureInvariant);
    internal static string LocalPath(string path)
    {
        string p = path.Trim().Trim('"').Replace('/', '\\').TrimEnd('\\');
        if (!Regex.IsMatch(p, @"^[A-Za-z]:\\") || p.IndexOf(':', 2) >= 0 ||
            p.Split('\\').Any(s => s is "." or "..")) return "";
        return p;
    }
    internal static IdentityMatch Match(PackageIdentity local, PackageIdentity candidate)
    {
        IdentityMatch Result(IdentityConfidence c, string reason, params string[] evidence) => new(c, reason, evidence);
        if (Different(local.Architecture, candidate.Architecture) || Different(local.Scope, candidate.Scope) ||
            Different(local.Channel, candidate.Channel)) return Result(IdentityConfidence.Blocked, "context_conflict");
        if (Different(local.PackageFamilyName, candidate.PackageFamilyName) ||
            Known(local.ProductCode) && Known(candidate.ProductCode) && !SameCode(local.ProductCode, candidate.ProductCode) ||
            Known(local.UpgradeCode) && Known(candidate.UpgradeCode) && !SameCode(local.UpgradeCode, candidate.UpgradeCode) ||
            Different(local.Signer, candidate.Signer)) return Result(IdentityConfidence.Blocked, "hard_identity_conflict");
        if (local.Managed && candidate.Managed && local.Provider == candidate.Provider &&
            Different(local.PackageId, candidate.PackageId)) return Result(IdentityConfidence.Blocked, "package_id_conflict");
        bool publisher = Key(local.Publisher).Length > 0 && Key(local.Publisher) == Key(candidate.Publisher);
        if (Known(local.Publisher) && Known(candidate.Publisher) && !publisher)
            return Result(IdentityConfidence.Blocked, "publisher_conflict");
        bool name = Key(local.Name).Length > 0 && Key(local.Name) == Key(candidate.Name);
        bool version = Known(local.InstalledVersion) && Known(candidate.InstalledVersion) &&
            Matching.CompareVersions(local.InstalledVersion, candidate.InstalledVersion) is 0;
        if (Known(local.InstalledVersion) && Known(candidate.InstalledVersion) && !version)
            return Result(IdentityConfidence.Blocked, "stale_registration");
        string root = LocalPath(local.Location), otherRoot = LocalPath(candidate.Location);
        if (root.Length > 0 && otherRoot.Length > 0 && !root.Equals(otherRoot, StringComparison.OrdinalIgnoreCase))
            return Result(IdentityConfidence.Blocked, "installation_conflict");
        bool path = root.Length > 0 && root.Equals(otherRoot, StringComparison.OrdinalIgnoreCase);
        bool product = SameCode(local.ProductCode, candidate.ProductCode);
        bool family = Known(local.PackageFamilyName) && local.PackageFamilyName.Equals(candidate.PackageFamilyName, StringComparison.OrdinalIgnoreCase);
        bool package = local.Managed && candidate.Managed && local.Provider == candidate.Provider &&
            ValidId(local.PackageId) && local.PackageId.Equals(candidate.PackageId, StringComparison.OrdinalIgnoreCase);
        bool signer = Known(local.Signer) && local.Signer.Equals(candidate.Signer, StringComparison.OrdinalIgnoreCase);
        var evidence = new List<string>();
        if (product) evidence.Add("product_code"); if (family) evidence.Add("package_family");
        if (package) evidence.Add("managed_package_id"); if (publisher) evidence.Add("publisher");
        if (version) evidence.Add("installed_version"); if (path) evidence.Add("installation_path");
        if (signer) evidence.Add("verified_signer"); if (name) evidence.Add("exact_name");
        bool upgrade = SameCode(local.UpgradeCode, candidate.UpgradeCode) && signer && name && path;
        if (upgrade) evidence.Add("upgrade_code_with_binary_context");
        if ((product || family || package || upgrade) && publisher && (version || path || signer))
            return new(IdentityConfidence.Strong, "independent_identity_evidence", evidence);
        if (name && publisher && version && path) return new(IdentityConfidence.Corroborated, "local_context", evidence);
        if (name || product || family || package) return new(IdentityConfidence.Suggestion, "insufficient_independent_evidence", evidence);
        return Result(IdentityConfidence.Unmatched, "no_identity_evidence");
    }
    internal static (PackageIdentity? Identity, IdentityMatch Match) Resolve(PackageIdentity local, IEnumerable<PackageIdentity> candidates)
    {
        var matches = candidates.Select(c => (Identity: c, Match: Match(local, c))).Where(c => c.Match.Discovered).ToArray();
        // Same package ID with multiple local registrations is still ambiguous.
        if (matches.Length > 1) return (null, new(IdentityConfidence.Ambiguous, "multiple_installations", []));
        return matches.Length == 1 ? matches[0] : (null, new(IdentityConfidence.Unmatched, "no_unique_identity", []));
    }
}
