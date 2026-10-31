using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GfReleaseMetadataLab;

// Metadata discovery only. This does not authenticate the downloaded executable,
// give an update permission, write to disk or trigger a product update.
internal enum DiscoveryState
{
    NoPublishedRelease,
    NoNewVersion,
    NewVersionMetadataOnly,
    UntrustedMetadataBlocked,
    RemoteUnavailable,
}

internal sealed record DiscoveryResult(DiscoveryState State, string? Version = null)
{
    internal bool CanInstall => false;
    internal bool Ready => false;
    internal bool AuthorizeRecovery => false;
}

internal static partial class GitHubReleaseDiscovery
{
    internal const string Repository = "walissonminel-1991/gerenciador-final";
    internal const string Endpoint =
        "https://api.github.com/repos/walissonminel-1991/gerenciador-final/releases/latest";
    private const int MaxResponseBytes = 512 * 1024;

    // GitHub release tags used for this project are explicit 3- or 4-part
    // numeric versions. Never order untrusted SemVer suffixes lexically.
    [GeneratedRegex(@"\Av?(?<a>0|[1-9][0-9]{0,4})\.(?<b>0|[1-9][0-9]{0,4})\.(?<c>0|[1-9][0-9]{0,4})(?:\.(?<d>0|[1-9][0-9]{0,4}))?\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex StrictTag();

    internal static bool ParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (text is null || text.Length is < 5 or > 24)
            return false;
        var m = StrictTag().Match(text);
        if (!m.Success)
            return false;
        int[] components = ["a", "b", "c", "d"].Where(n => m.Groups[n].Success)
            .Select(n => int.Parse(m.Groups[n].Value, CultureInfo.InvariantCulture)).ToArray();
        if (components.Any(v => v > 65535))
            return false;
        version = components.Length == 3
            ? new Version(components[0], components[1], components[2])
            : new Version(components[0], components[1], components[2], components[3]);
        return true;
    }

    internal static DiscoveryResult EvaluateJson(ReadOnlySpan<byte> body, Version installed)
    {
        if (body.Length is 0 or > MaxResponseBytes || installed.Major < 0 ||
            installed.Minor < 0 || installed.Build < 0)
            return new(DiscoveryState.UntrustedMetadataBlocked);
        try
        {
            using var doc = JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new(DiscoveryState.UntrustedMetadataBlocked);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!keys.Add(property.Name))
                    return new(DiscoveryState.UntrustedMetadataBlocked);
            if (
                !root.TryGetProperty("tag_name", out var tag) ||
                tag.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("draft", out var draft) ||
                draft.ValueKind != JsonValueKind.False ||
                !root.TryGetProperty("prerelease", out var pre) ||
                pre.ValueKind != JsonValueKind.False)
                return new(DiscoveryState.UntrustedMetadataBlocked);
            var tagName = tag.GetString();
            if (!ParseVersion(tagName, out var candidate))
                return new(DiscoveryState.UntrustedMetadataBlocked);
            // GitHub's API indicates a published release; its tag does NOT
            // authenticate assets. Never accept update commands or URLs from JSON.
            return candidate > installed
                ? new(DiscoveryState.NewVersionMetadataOnly, tagName)
                : new(DiscoveryState.NoNewVersion);
        }
        catch (JsonException)
        {
            return new(DiscoveryState.UntrustedMetadataBlocked);
        }
    }

    internal static HttpClient CreateStrictClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseDefaultCredentials = false,
        };
        return new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(12) };
    }

    internal static async Task<DiscoveryResult> CheckAsync(
        HttpClient http, Version installed, CancellationToken cancellationToken = default)
    {
        if (installed.Major < 0 || installed.Minor < 0 || installed.Build < 0)
            return new(DiscoveryState.UntrustedMetadataBlocked);
        using var req = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("GerenciadorFinal", "0.1"));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        try
        {
            using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            // Fail closed on redirects, or mismatched final URL/host.
            if (res.RequestMessage?.RequestUri?.AbsoluteUri != Endpoint)
                return new(DiscoveryState.UntrustedMetadataBlocked);
            if (res.StatusCode == HttpStatusCode.NotFound)
                return new(DiscoveryState.NoPublishedRelease);
            if (res.StatusCode != HttpStatusCode.OK)
                return new(DiscoveryState.RemoteUnavailable);
            var type = res.Content.Headers.ContentType?.MediaType;
            if (type is not ("application/json" or "application/vnd.github+json"))
                return new(DiscoveryState.UntrustedMetadataBlocked);
            if (res.Content.Headers.ContentLength > MaxResponseBytes)
                return new(DiscoveryState.UntrustedMetadataBlocked);
            await using var input = await res.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var size = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (size == 0)
                    break;
                if (output.Length + size > MaxResponseBytes)
                    return new(DiscoveryState.UntrustedMetadataBlocked);
                output.Write(buffer, 0, size);
            }
            return EvaluateJson(output.ToArray(), installed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(DiscoveryState.RemoteUnavailable);
        }
        catch (HttpRequestException)
        {
            return new(DiscoveryState.RemoteUnavailable);
        }
        catch (IOException)
        {
            return new(DiscoveryState.RemoteUnavailable);
        }
    }
}
