using System.Net;
using System.Text.Json;
using GerenciadorAplicativos;

// Fixtures isoladas: não acessam serviços reais, não executam instaladores.
internal static class ChromeVersionHistoryTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        var x64 = new InstalledApp("Google Chrome", "156.0.7999.30", "Google LLC", Architecture: "x64");
        var x86 = new InstalledApp("Google Chrome", "156.0.7999.30", "Google Inc.", Architecture: "x86");
        var unknown = x64 with { Architecture = "unknown" };
        var fake = x64 with { Publisher = "Editora Desconhecida" };
        var beta = x64 with { Name = "Google Chrome Beta" };
        var preview = x64 with { Channel = "preview" };
        var arm = x64 with { Architecture = "arm64" };
        check(ApplicationIdentityCatalog.Resolve(x64.Name, x64.Publisher)?.Id == "chrome-stable" &&
              ApplicationIdentityCatalog.Resolve(x86.Name, x86.Publisher)?.Id == "chrome-stable" &&
              ApplicationIdentityCatalog.Resolve(fake.Name, fake.Publisher) is null &&
              ApplicationIdentityCatalog.Resolve(beta.Name, beta.Publisher) is null,
              "Chrome: identidade exige nome e editor; rejeita imitação e Beta");

        OfficialReleaseSources.ClearCacheForTests();
        var transport = new ChromeFixture();
        using (var official = new OfficialReleaseSources(transport))
        {
            var apps = new[] { x64, x86, unknown, fake, beta, preview, arm };
            var scan = await official.QueryAsync(apps, CancellationToken.None);
            check(scan.Findings.Count == 2 && scan.Diagnostics.Count == 2 &&
                  scan.Diagnostics.All(d => d.Status == "ok") && transport.Requests == 2,
                  "Chrome: apenas x86/x64 conhecidos consultados, sem rede para outros alvos");
            check(scan.Findings.Any(f => ReferenceEquals(f.Installed, x64) && f.LatestVersion == "156.0.8078.48" &&
                    f.Architecture == "x64" && f.Channel == "stable" && f.CanonicalId == "chrome-stable") &&
                  scan.Findings.Any(f => ReferenceEquals(f.Installed, x86) && f.LatestVersion == "156.0.8078.44" &&
                    f.Architecture == "x86" && f.Channel == "stable" && f.CanonicalId == "chrome-stable"),
                  "Chrome: versões oficiais por plataforma sem misturar x86 e x64");
            var joined = MultiSourceMatcher.Merge(apps, null, scan.Findings);
            check(joined.Count(x => x.State == MatchState.Candidate) == 2 &&
                  joined.Skip(2).All(x => x.State == MatchState.Unmatched),
                  "Chrome: somente candidatos consultivos da instalação correspondente");
            var cached = await official.QueryAsync([x64, x86], CancellationToken.None);
            check(cached.Findings.Count == 2 && transport.Requests == 2 &&
                  cached.Diagnostics.All(x => x.UsedMemoryCache),
                  "Chrome: respostas oficiais em cache sem repetir consultas");
            var refreshed = await official.QueryAsync([x64, x86], CancellationToken.None, forceRefresh: true);
            check(refreshed.Findings.Count == 2 && transport.Requests == 4,
                  "Chrome: atualização manual refaz somente duas consultas oficiais");
        }

        foreach (var (json, platform) in new (string, string)[] {
            ("{\"versions\":[]}", "win64"),
            ("{\"versions\":[{\"version\":\"156.0.8078.48\",\"name\":\"chrome/platforms/win/channels/stable/versions/156.0.8078.48\"}]}", "win64"),
            ("{\"versions\":[{\"version\":\"156.0.8078.48\",\"name\":\"chrome/platforms/win64/channels/beta/versions/156.0.8078.48\"}]}", "win64"),
            ("{\"versions\":[{\"version\":\"156.0.8078.48-beta\",\"name\":\"chrome/platforms/win64/channels/stable/versions/156.0.8078.48-beta\"}]}", "win64"),
            ("{\"versions\":[{\"version\":\"156.0.8078.48\",\"name\":\"chrome/platforms/win64/channels/stable/versions/156.0.8078.48\"},{\"version\":\"156.0.8078.49\",\"name\":\"chrome/platforms/win64/channels/stable/versions/156.0.8078.49\"}]}", "win64"),
            ("{\"versions\":[{\"version\":\"156.0.8078.48\"}]}", "win64"),
            ("{\"versions\":[{\"version\":\"156.0.8078.48\",\"name\":\"chrome/platforms/win64/channels/stable/versions/156.0.8078.48\"}]}", "linux")
        })
        {
            using var document = JsonDocument.Parse(json);
            bool refused = false;
            try { _ = OfficialReleaseSources.ParseChromeVersionHistory(document.RootElement, platform); }
            catch (InvalidDataException) { refused = true; }
            check(refused, "Chrome: recusa resposta ambígua, outro canal/plataforma, prerelease ou sem identidade");
        }

        check(OfficialReleaseSources.IsAllowedUrl(new Uri(
                  "https://versionhistory.googleapis.com/v1/chrome/platforms/win64/channels/stable/versions?order_by=version%20desc&pageSize=1")) &&
              !OfficialReleaseSources.IsAllowedUrl(new Uri(
                  "http://versionhistory.googleapis.com/v1/chrome/platforms/win64/channels/stable/versions?order_by=version%20desc&pageSize=1")) &&
              !OfficialReleaseSources.IsAllowedUrl(new Uri(
                  "https://versionhistory.googleapis.com.attacker.invalid/v1/chrome/platforms/win64/channels/stable/versions?order_by=version%20desc&pageSize=1")) &&
              !OfficialReleaseSources.IsAllowedUrl(new Uri(
                  "https://versionhistory.googleapis.com/v1/chrome/platforms/win64/channels/beta/versions?order_by=version%20desc&pageSize=1")) &&
              !OfficialReleaseSources.IsAllowedUrl(new Uri(
                  "https://versionhistory.googleapis.com/v1/chrome/platforms/win64/channels/stable/versions?order_by=version%20asc&pageSize=1")),
              "Chrome: HTTPS fixo, rejeita host impostor, beta e ordem alterada");

        OfficialReleaseSources.ClearCacheForTests();
        using var broken = new OfficialReleaseSources(new ChromeFixture(failWin64: true));
        var independent = await broken.QueryAsync([x64, x86], CancellationToken.None);
        check(independent.Findings.Count == 1 && ReferenceEquals(independent.Findings[0].Installed, x86) &&
              independent.Diagnostics.Count == 2 && independent.Diagnostics.Single(d => d.Provider == "chrome-win64").Status != "ok",
              "Chrome: erro HTTP isolado não interrompe a outra fonte");
    }

    private sealed class ChromeFixture(bool failWin64 = false) : HttpMessageHandler
    {
        private int _requests;
        internal int Requests => Volatile.Read(ref _requests);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            var url = request.RequestUri ?? throw new InvalidDataException("URI ausente");
            if (!OfficialReleaseSources.IsAllowedUrl(url) ||
                url.Host != "versionhistory.googleapis.com")
                throw new InvalidDataException("Consulta fora da origem aprovada");
            bool win64 = url.AbsolutePath.Contains("/win64/", StringComparison.Ordinal);
            if (win64 && failWin64)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            string platform = win64 ? "win64" : "win";
            string version = win64 ? "156.0.8078.48" : "156.0.8078.44";
            string json = "{\"versions\":[{\"name\":\"chrome/platforms/" + platform +
                "/channels/stable/versions/" + version + "\",\"version\":\"" + version + "\"}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(json)
            });
        }
    }
}
