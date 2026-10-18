using System.Diagnostics;
using System.Net;
using GerenciadorAplicativos;

int tests = 0;
void Assert(bool ok, string label)
{
    if (!ok) throw new Exception("FALHA: " + label);
    tests++;
    Console.WriteLine("OK " + label);
}
static InstalledApp App(string n, string ver, string maker) => new(n, ver, maker);
var obs = App("OBS Studio", "31.1.2", "OBS Project");
var firefox = App("Mozilla Firefox", "139.0.1", "Mozilla Corporation");
var notepad = App("Notepad++ (64-bit x64)", "8.7.5", "Notepad++ Team");
var powertoys = App("Microsoft PowerToys", "0.80.0", "Microsoft Corporation");
var shell = App("PowerShell 7-x64", "7.4.2", "Microsoft Corporation");
var fakeObs = App("OBS Studio", "31.1.2", "Editor Suspeito");
var esr = App("Mozilla Firefox ESR", "115.0.0", "Mozilla Corporation");
var list = new[] {obs, firefox, notepad, powertoys, shell, fakeObs, esr};

OfficialReleaseSources.ClearCacheForTests();
var fixture = new Fixture();
using (var source = new OfficialReleaseSources(fixture))
{
    var timer = Stopwatch.StartNew();
    var result = await source.QueryAsync(list, CancellationToken.None);
    timer.Stop();
    Assert(result.Findings.Count == 5, "5 aplicativos conhecidos, nenhum editor desconhecido ou ESR");
    Assert(result.Diagnostics.Count == 5 && result.Diagnostics.All(x => x.Status == "ok"),
        "Três tipos de repositório GitHub e Mozilla responderam");
    Assert(result.Findings.First(x => ReferenceEquals(x.Installed, obs)).LatestVersion == "32.2.2",
        "Tag oficial do OBS sem prefixo v");
    Assert(result.Findings.First(x => ReferenceEquals(x.Installed, firefox)).LatestVersion == "140.0.2",
        "Versão estável Firefox pelo JSON Mozilla");
    Assert(fixture.Requests == 5, "Uma chamada apenas por projeto reconhecido");
    var second = await source.QueryAsync(list, CancellationToken.None);
    Assert(second.Findings.Count == 5 && fixture.Requests == 5, "Cache elimina chamadas repetidas");
    var forced = await source.QueryAsync(list, CancellationToken.None, true);
    Assert(forced.Findings.Count == 5 && fixture.Requests == 10, "Consulta manual atualiza todas as fontes");
    Console.WriteLine($"PERFORMANCE.PRIMEIRA_MULTIFONTES_MS={timer.ElapsedMilliseconds}");

    var ruckMatches = Matching.Evaluate(list, new CatalogEntry[]
    {
        new("OBS Studio", "OBS Studio", "33.0.0", "OBS Project"),
        new("Mozilla Firefox", "Mozilla Firefox", "139.0.0", "Mozilla Corporation")
    });
    var merged = MultiSourceMatcher.Merge(list, ruckMatches, result.Findings);
    Assert(merged.Count == list.Length && merged[0].State == MatchState.Candidate &&
        merged[0].Catalog?.ProductVersion == "32.2.2" && merged[0].Provider == "GitHub oficial",
        "Fonte oficial corrige candidato RuckZuck conflitante");
    Assert(merged[1].State == MatchState.Candidate && merged[1].Provider == "Mozilla",
        "Firefox usa a fonte original da Mozilla");
    Assert(merged[5].State == MatchState.Unmatched && merged[6].State == MatchState.Unmatched,
        "Pacotes não reconhecidos não são falsos positivos");
    var duplicate = MultiSourceMatcher.Merge([obs], null,
        [new OfficialRelease(obs, "31.0.0", "Fonte A", ""),
         new OfficialRelease(obs, "32.2.2", "GitHub oficial", "")]);
    Assert(duplicate.Count == 1 && duplicate[0].Catalog?.ProductVersion == "32.2.2",
        "Resultados duplicados consolidados na maior versão oficial");
    var upToDate = MultiSourceMatcher.Merge([App("OBS Studio", "32.2.2", "OBS Project")],
        [new UpdateMatch(obs, new("OBS", "OBS", "33.0.0", "OBS Project"), MatchState.Candidate)],
        [new OfficialRelease(obs, "32.2.2", "GitHub oficial", "")]);
    // Referência diferente não deve fundir um resultado de outro aplicativo.
    Assert(upToDate[0].State == MatchState.Candidate, "IDs das linhas não são confundidos");
    var currentApp = App("OBS Studio", "32.2.2", "OBS Project");
    var suppressed = MultiSourceMatcher.Merge([currentApp],
        [new UpdateMatch(currentApp, new("OBS", "OBS", "33.0", "OBS Project"), MatchState.Candidate)],
        [new OfficialRelease(currentApp, "32.2.2", "GitHub oficial", "")]);
    Assert(suppressed[0].State == MatchState.EqualOrNewer, "Versão oficial atual anula falso positivo");
}

OfficialReleaseSources.ClearCacheForTests();
var failing = new Fixture(failMozilla:true);
using (var source = new OfficialReleaseSources(failing))
{
    var result = await source.QueryAsync([obs, firefox], CancellationToken.None);
    Assert(result.Findings.Count == 1 && result.Findings[0].Provider == "GitHub oficial" &&
        result.Diagnostics.Any(x => x.Status == "HttpRequestException"),
        "Falha Mozilla não afeta GitHub");
}
OfficialReleaseSources.ClearCacheForTests();
var invalid = new Fixture(badTags:true);
using (var source = new OfficialReleaseSources(invalid))
{
    var result = await source.QueryAsync([obs, firefox], CancellationToken.None);
    Assert(result.Findings.Count == 1 && result.Findings[0].Provider == "Mozilla",
        "Rejeita tag beta sem corromper demais fontes");
}
OfficialReleaseSources.ClearCacheForTests();
var gateFixture = new Fixture(delay:30);
var sources = Enumerable.Range(0, 12).Select(_ => new OfficialReleaseSources(gateFixture)).ToArray();
try
{
    var all = await Task.WhenAll(sources.Select(x => x.QueryAsync([obs, firefox], CancellationToken.None)));
    Assert(all.All(x => x.Findings.Count == 2) && gateFixture.Requests == 2,
        "Doze consultas simultâneas compartilham somente dois downloads");
}
finally { foreach(var s in sources) s.Dispose(); }
OfficialReleaseSources.ClearCacheForTests();
using (var client = new OfficialReleaseSources(new Fixture()))
using (var cts = new CancellationTokenSource())
{
    cts.Cancel();
    try
    {
        await client.QueryAsync([obs], cts.Token);
        throw new Exception("FALHA: cancelamento não respeitado");
    }
    catch(OperationCanceledException) { Assert(true, "Cancelamento imediato sem novos downloads"); }
}
Assert(!OfficialReleaseSources.IsAllowedUrl(new Uri("http://api.github.com/repos/obsproject/obs-studio/releases/latest")) &&
    !OfficialReleaseSources.IsAllowedUrl(new Uri("https://api.github.com.evil.example/x")) &&
    OfficialReleaseSources.IsAllowedUrl(new Uri("https://api.github.com/repos/obsproject/obs-studio/releases/latest")),
    "Allowlist HTTPS proíbe HTTP e subdomínios falsos");
Assert(Matching.CompareVersions("1.2", "1.2.0.0") == 0,
    "Normalização de versões preservada");
Console.WriteLine($"{tests}/{tests} testes de motores múltiplos aprovados");
if (args.Contains("--live", StringComparer.OrdinalIgnoreCase))
{
    // Usa SOMENTE GETs HTTPS de dados públicos; nenhum programa instalado é consultado no CI.
    OfficialReleaseSources.ClearCacheForTests();
    using var source = new OfficialReleaseSources();
    var live = await source.QueryAsync([obs, firefox, notepad, powertoys, shell],
        new CancellationTokenSource(TimeSpan.FromSeconds(25)).Token, true);
    foreach (var item in live.Diagnostics)
        Console.WriteLine($"FONTE_AO_VIVO {item.Provider} status={item.Status} tempo_ms={item.Milliseconds}");
    Console.WriteLine($"FONTE_AO_VIVO_TOTAL resultados={live.Findings.Count}");
    if (live.Findings.Count < 1)
        throw new Exception("Nenhum provedor oficial disponível no teste ao vivo.");
}

internal sealed class Fixture(bool failMozilla = false, bool badTags = false, int delay = 0) : HttpMessageHandler
{
    private int _requests;
    public int Requests => Volatile.Read(ref _requests);
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken token)
    {
        Interlocked.Increment(ref _requests);
        if (delay > 0) await Task.Delay(delay, token);
        var uri = request.RequestUri ?? throw new Exception("URI vazia");
        if (uri.Scheme != "https" || (uri.Host != "api.github.com" &&
            uri.Host != "product-details.mozilla.org")) throw new Exception("Host não aprovado");
        if (failMozilla && uri.Host == "product-details.mozilla.org")
            return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        string body = uri.Host == "product-details.mozilla.org"
            ? "{\"LATEST_FIREFOX_VERSION\":\"140.0.2\"}"
            : uri.AbsolutePath.Contains("obs-studio")
            ? "{\"tag_name\":\"" + (badTags ? "v33.0.0-beta1" : "v32.2.2") + "\"}"
            : uri.AbsolutePath.Contains("notepad-plus-plus")
            ? "{\"tag_name\":\"v8.8.4\"}"
            : uri.AbsolutePath.Contains("PowerToys")
            ? "{\"tag_name\":\"v0.93.0\"}"
            : "{\"tag_name\":\"v7.5.2\"}";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    }
}
