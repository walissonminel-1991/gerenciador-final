using System.Net;
using System.Text.Json;
using GerenciadorAplicativos;

// Revisão pré-BETA: transporte inerte, sem rede ou instalação no modo comum.
internal static class ChromeVersionHistoryReviewTests
{
    private static readonly InstalledApp Chrome = new("Google Chrome", "155.0.8000.1", "Google LLC", Architecture: "x64");
    private static readonly InstalledApp Firefox = new("Mozilla Firefox", "139.0", "Mozilla Corporation");
    private static readonly InstalledApp Obs = new("OBS Studio", "31.0", "OBS Project");
    private const string Identity = "chrome/platforms/win64/channels/stable/versions/";
    private const string Version = "156.0.8078.48";

    internal static async Task Run(Action<bool, string> check)
    {
        string item = JsonSerializer.Serialize(new { name = Identity + Version, version = Version });
        foreach (var (label, json) in new[] {
            ("versions duplicado", "{\"versions\":[],\"versions\":[" + item + "]}"),
            ("version duplicado", "{\"versions\":[{\"version\":\"1.0.0.0\",\"version\":\"" + Version + "\",\"name\":\"" + Identity + Version + "\"}]}"),
            ("name duplicado", "{\"versions\":[{\"name\":\"outro-produto\",\"name\":\"" + Identity + Version + "\",\"version\":\"" + Version + "\"}]}"),
            ("chave escapada duplicada", "{\"versions\":[],\"ver\\u0073ions\":[" + item + "]}")
        })
            check(Rejected(json), "Chrome revisão: rejeita " + label);

        foreach (string version in new[] { Version + "\n", "١٥٦.0.8078.48", Version + " ", "156.0.8078" })
            check(Rejected(JsonSerializer.Serialize(new { versions = new[] { new { name = Identity + version, version } } })),
                "Chrome revisão: versão inteira ASCII de quatro componentes obrigatória");

        using (var doc = JsonDocument.Parse("{\"versions\":[" + item + "],\"nextPageToken\":\"pagina-seguinte\"}"))
            check(OfficialReleaseSources.ParseChromeVersionHistory(doc.RootElement, "win64") == Version,
                "Chrome revisão: resposta real paginada aceita sem seguir nextPageToken");

        const string endpoint = "https://versionhistory.googleapis.com/v1/chrome/platforms/win64/channels/stable/versions?order_by=version%20desc&pageSize=1";
        foreach (string url in new[] { endpoint + "#fragmento", endpoint + "&extra=1",
            endpoint.Replace("https://", "https://usuario@"), endpoint.Replace(".com/", ".com:444/") })
            check(!OfficialReleaseSources.IsAllowedUrl(new Uri(url)),
                "Chrome revisão: recusa fragmento, query extra, credencial ou porta alternativa");

        foreach (string failure in new[] { "429", "500", "302", "json", "large", "timeout", "duplicate" })
        {
            OfficialReleaseSources.ClearCacheForTests();
            var handler = new Fixture(failure);
            using var source = new OfficialReleaseSources(handler, responseTimeout: TimeSpan.FromMilliseconds(100));
            var scan = await source.QueryAsync([Chrome, Firefox, Obs], default);
            check(scan.Findings.Count == 2 && scan.Findings.Any(f => ReferenceEquals(f.Installed, Firefox)) &&
                  scan.Findings.Any(f => ReferenceEquals(f.Installed, Obs)) && scan.Diagnostics.Count == 3 &&
                  scan.Diagnostics.Single(d => d.Provider == "chrome-win64").Status != "ok" && handler.Requests == 3,
                  "Chrome revisão: " + failure + " isolado preserva Mozilla/GitHub sem repetir ou redirecionar");
        }

        OfficialReleaseSources.ClearCacheForTests();
        var concurrent = new Fixture("wait");
        using (var source = new OfficialReleaseSources(concurrent))
        {
            var first = source.QueryAsync([Chrome], default);
            await concurrent.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var second = source.QueryAsync([Chrome], default);
            concurrent.Release.TrySetResult();
            var scans = await Task.WhenAll(first, second);
            check(concurrent.Requests == 1 && scans.All(s => s.Findings.Count == 1) &&
                  scans.Sum(s => s.Diagnostics.Count(d => d.UsedMemoryCache)) == 1,
                  "Chrome revisão: consultas concorrentes compartilham uma requisição e o resultado validado");
        }

        OfficialReleaseSources.ClearCacheForTests();
        var cancelled = new Fixture("wait");
        using (var source = new OfficialReleaseSources(cancelled))
        using (var cancellation = new CancellationTokenSource())
        {
            var task = source.QueryAsync([Chrome, Firefox], cancellation.Token);
            await cancelled.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cancellation.Cancel();
            bool stopped = false;
            try { await task; }
            catch (OperationCanceledException) { stopped = true; }
            check(stopped, "Chrome revisão: cancelamento explícito não devolve scan parcial como concluído");
        }
        OfficialReleaseSources.ClearCacheForTests();
    }

    internal static async Task RunLive(Action<bool, string> check)
    {
        // Opt-in explícito --chrome-live: duas URLs públicas fixas; nenhum dado de PC.
        using var source = new OfficialReleaseSources();
        var scan = await source.QueryAsync([Chrome, Chrome with { Architecture = "x86" }], default, forceRefresh: true);
        check(scan.Findings.Count == 2 && scan.Diagnostics.Count == 2 && scan.Diagnostics.All(d => d.Status == "ok") &&
              scan.Findings.All(f => f.CanonicalId == "chrome-stable" && f.Channel == "stable"),
              "Chrome live: parser e transporte reais x86/x64, somente consulta");
        foreach (var f in scan.Findings) Console.WriteLine($"CHROME_LIVE fonte={f.SourceId} versao={f.LatestVersion} arquitetura={f.Architecture}");
    }

    private static bool Rejected(string json)
    {
        using var document = JsonDocument.Parse(json);
        try { OfficialReleaseSources.ParseChromeVersionHistory(document.RootElement, "win64"); return false; }
        catch (InvalidDataException) { return true; }
    }

    private sealed class Fixture(string mode) : HttpMessageHandler
    {
        private int requests;
        internal int Requests => Volatile.Read(ref requests);
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref requests);
            var url = request.RequestUri ?? throw new InvalidDataException();
            string json;
            if (url.Host == "versionhistory.googleapis.com")
            {
                Started.TrySetResult();
                if (mode == "wait") await Release.Task.WaitAsync(token);
                if (mode == "timeout") await Task.Delay(Timeout.InfiniteTimeSpan, token);
                if (int.TryParse(mode, out int status)) return new((HttpStatusCode)status);
                json = mode switch {
                    "json" => "{",
                    "large" => new string('x', 128 * 1024 + 1),
                    "duplicate" => "{\"versions\":[],\"versions\":[{\"name\":\"" + Identity + Version + "\",\"version\":\"" + Version + "\"}]}",
                    _ => JsonSerializer.Serialize(new { versions = new[] { new { name = Identity + Version, version = Version } } })
                };
            }
            else if (url.Host == "product-details.mozilla.org") json = "{\"LATEST_FIREFOX_VERSION\":\"140.0.1\"}";
            else if (url.Host == "api.github.com" && url.AbsolutePath == "/repos/obsproject/obs-studio/releases/latest") json = "{\"tag_name\":\"32.0.0\",\"prerelease\":false,\"draft\":false}";
            else throw new InvalidDataException("Destino não previsto na fixture.");
            return new(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }
}
