using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GfReleaseMetadataLab;

internal static class Program
{
    private static int count;

    private static void Assert(bool condition, string key)
    {
        if (!condition)
            throw new InvalidOperationException("FAIL_CLOSED_TEST_" + key);
        count++;
    }

    private static string Payload(string tag = "v5.13.11.66",
        string draft = "false", string prerelease = "false")
        => $$"""{"tag_name":"{{tag}}","draft":{{draft}},"prerelease":{{prerelease}},"assets":[{"browser_download_url":"https://evil.invalid/setup.exe"}]}""";

    private static DiscoveryState Parse(string json, string installed = "5.13.11.65")
    {
        if (!GitHubReleaseDiscovery.ParseVersion(installed, out var v))
            throw new InvalidOperationException("INVALID_INSTALLED_FIXTURE");
        return GitHubReleaseDiscovery.EvaluateJson(Encoding.UTF8.GetBytes(json), v).State;
    }

    private sealed class FakeHandler(HttpStatusCode code, string json = "",
        string mime = "application/json") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,
            CancellationToken cancellationToken)
        {
            var result = new HttpResponseMessage(code)
            {
                RequestMessage = req,
                Content = new StringContent(json, Encoding.UTF8),
            };
            result.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
            return Task.FromResult(result);
        }
    }

    private static async Task<DiscoveryResult> Probe(HttpStatusCode status, string json = "",
        string mime = "application/json")
    {
        using var client = new HttpClient(new FakeHandler(status, json, mime));
        return await GitHubReleaseDiscovery.CheckAsync(client, new Version(5, 13, 11, 65));
    }

    public static async Task<int> Main()
    {
        Assert(GitHubReleaseDiscovery.Repository == "walissonminel-1991/gerenciador-final", "fixed_repo");
        Assert(GitHubReleaseDiscovery.Endpoint ==
            "https://api.github.com/repos/walissonminel-1991/gerenciador-final/releases/latest", "fixed_endpoint");
        Assert(Parse(Payload()) == DiscoveryState.NewVersionMetadataOnly, "new");
        Assert(Parse(Payload("5.13.11.65")) == DiscoveryState.NoNewVersion, "same");
        Assert(Parse(Payload("v5.13.11.64")) == DiscoveryState.NoNewVersion, "downgrade");
        Assert(Parse(Payload("v5.13.11.66", "true")) == DiscoveryState.UntrustedMetadataBlocked, "draft");
        Assert(Parse(Payload("v5.13.11.66", prerelease:"true")) == DiscoveryState.UntrustedMetadataBlocked, "prerelease");
        Assert(Parse(Payload("v5.13.11.66", draft:"null")) == DiscoveryState.UntrustedMetadataBlocked, "null_draft");
        Assert(Parse("""{"tag_name":"v5.13.11.66","draft":false}""") == DiscoveryState.UntrustedMetadataBlocked, "missing_pre");
        Assert(Parse(Payload("v5.13.11.66-beta")) == DiscoveryState.UntrustedMetadataBlocked, "suffix");
        Assert(Parse(Payload("v5.13.11.066")) == DiscoveryState.UntrustedMetadataBlocked, "leading_zero");
        Assert(Parse(Payload("v5.13.11.99999")) == DiscoveryState.UntrustedMetadataBlocked, "out_of_range");
        Assert(Parse(Payload("v5.13.11.66.5")) == DiscoveryState.UntrustedMetadataBlocked, "five_components");
        Assert(Parse(Payload("v5.13.11")) == DiscoveryState.NoNewVersion, "three_components");
        Assert(Parse("{") == DiscoveryState.UntrustedMetadataBlocked, "invalid_json");
        Assert(Parse("[]") == DiscoveryState.UntrustedMetadataBlocked, "array_json");
        Assert(Parse("""{"tag_name":"v5.13.11.66","draft":false,"prerelease":false,"tag_name":"v99.0.0"}""") ==
            DiscoveryState.UntrustedMetadataBlocked, "duplicate_tag"); // enforced after addition
        Assert(Parse(Payload("v5.13.11.66", "false", "\"false\"")) == DiscoveryState.UntrustedMetadataBlocked, "string_bool");
        Assert((await Probe(HttpStatusCode.NotFound)).State == DiscoveryState.NoPublishedRelease, "empty_repo");
        Assert((await Probe(HttpStatusCode.Redirect, Payload())).State == DiscoveryState.RemoteUnavailable, "redirect");
        Assert((await Probe(HttpStatusCode.Forbidden)).State == DiscoveryState.RemoteUnavailable, "rate_limited");
        Assert((await Probe(HttpStatusCode.OK, Payload(), "text/html")).State ==
            DiscoveryState.UntrustedMetadataBlocked, "html_injection");
        var found = await Probe(HttpStatusCode.OK, Payload());
        Assert(found.State == DiscoveryState.NewVersionMetadataOnly, "200_only_metadata");
        Assert(!found.CanInstall && !found.Ready && !found.AuthorizeRecovery, "never_install");
        Assert(GitHubReleaseDiscovery.CreateStrictClient().DefaultRequestVersion == HttpVersion.Version11,
            "client_constructed");
        Console.WriteLine("GF_GITHUB_RELEASE_METADATA_LAB " +
            JsonSerializer.Serialize(new {
                schema = "gf-github-metadata-lab/1",
                tests_passed = count,
                status = "passed",
                scope = "OFFLINE_MOCK_GITHUB_READ_ONLY",
                no_download = true,
                no_install = true,
                no_product_changes = true,
                operational_ready = false,
                automatic_recovery_authorized = false,
            }));
        return 0;
    }
}
