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
Assert(new UpdateMatch(new InstalledApp("Inventario", "1.0", "Editor", false), null, MatchState.Unmatched).DiagnosticReason.Contains("SOMENTE INVENTÁRIO"),
    "Cobertura distingue entrada inelegivel de ausencia no catalogo");
Assert(new UpdateMatch(App("Nome privado", "1.0", "Editor privado"), null, MatchState.Ambiguous).DiagnosticReason == "CORRESPONDÊNCIA AMBÍGUA",
    "Motivo agregado nao inclui nomes nem dados do aplicativo");
var obs = App("OBS Studio", "31.1.2", "OBS Project");
var firefox = App("Mozilla Firefox", "139.0.1", "Mozilla Corporation");
var notepad = App("Notepad++ (64-bit x64)", "8.7.5", "Notepad++ Team");
var powertoys = App("Microsoft PowerToys", "0.80.0", "Microsoft Corporation");
var shell = App("PowerShell 7-x64", "7.4.2", "Microsoft Corporation");
var sevenzip = App("7-Zip 25.00 (x64)", "25.00", "Igor Pavlov");
var desktop = App("GitHub Desktop", "3.5.0", "GitHub, Inc.");
var sumatra = App("SumatraPDF", "3.5", "Krzysztof Kowalczyk");
var thunderbird = App("Mozilla Thunderbird (Release)", "135.0", "Mozilla Corporation");
var fakeObs = App("OBS Studio", "31.1.2", "Editor Suspeito");
var fakeSeven = App("7-Zip 25.00 (x64)", "25.00", "Editor Suspeito");
var esr = App("Mozilla Firefox ESR", "115.0.0", "Mozilla Corporation");
var thunderbirdEsr = App("Thunderbird ESR", "128.0", "Mozilla Corporation");
var unknownThunderbird = App("Mozilla Thunderbird", "140.0", "Mozilla Corporation");
Assert(ApplicationIdentityCatalog.RuleCount == 22,
    "Catálogo unificado reconhece 22 famílias verificadas");
Assert(ApplicationIdentityCatalog.Rules.Count(r => r.OfficialSource is not null) == 18,
    "Dezoito fontes oficiais referenciam identidade estável");
Assert(ApplicationIdentityCatalog.Resolve("OBS Studio", "OBS Project")?.Id == "obs" &&
    ApplicationIdentityCatalog.Resolve("OBS Studio", "OBS Project, LLC")?.Id == "obs",
    "Mesmo ID para versões de fabricante oficial do OBS");
Assert(ApplicationIdentityCatalog.Resolve("OBS Studio", "Editor Suspeito") is null,
    "Nome idêntico de fabricante falso nunca concede identidade");
Assert(ApplicationIdentityCatalog.Resolve("Windows Terminal Preview", "Microsoft Corporation") is null,
    "Terminal Preview não é confundido com canal estável");
Assert(ApplicationIdentityCatalog.Resolve("Mozilla Firefox ESR", "Mozilla Corporation") is null,
    "Firefox ESR não pode compartilhar ID do Firefox estável");
Assert(ApplicationIdentityCatalog.Resolve("7-Zippy Browser", "Igor Pavlov") is null,
    "Prefixos parecidos não autorizam fonte do 7-Zip");
Assert(ApplicationIdentityCatalog.Resolve("Notepad++ (64-bit x64)", "Notepad++ Team")?.CleanupPolicy == "notepadpp",
    "Mesma identidade conecta atualizações e limpeza profunda");
Assert(ApplicationIdentityCatalog.MatchesOfficialSource(obs.Name, obs.Publisher, "obs") &&
    !ApplicationIdentityCatalog.MatchesOfficialSource(obs.Name, obs.Publisher, "sumatrapdf"),
    "Fonte oficial não pode atualizar produto de outra família");

var list = new[] {obs, firefox, notepad, powertoys, shell, sevenzip, desktop,
    sumatra, thunderbird, fakeObs, fakeSeven, esr, thunderbirdEsr, unknownThunderbird};

OfficialReleaseSources.ClearCacheForTests();
var fixture = new Fixture();
using (var source = new OfficialReleaseSources(fixture))
{
    var timer = Stopwatch.StartNew();
    var result = await source.QueryAsync(list, CancellationToken.None);
    timer.Stop();
    Assert(result.Findings.Count == 9, "9 aplicativos conhecidos; falsos fabricantes e ESR rejeitados");
    Assert(result.Diagnostics.Count == 9 && result.Diagnostics.All(x => x.Status == "ok"),
        "Nove fontes oficiais simuladas responderam");
    Assert(result.Findings.First(x => ReferenceEquals(x.Installed, obs)).LatestVersion == "32.2.2",
        "Tag oficial do OBS sem prefixo v");
    Assert(result.Findings.First(x => ReferenceEquals(x.Installed, firefox)).LatestVersion == "140.0.2",
        "Versão estável Firefox pelo JSON Mozilla");
    Assert(result.Findings.First(x => ReferenceEquals(x.Installed, sevenzip)).LatestVersion == "26.03",
        "7-Zip oficial reconhece sua versão numérica");
    Assert(result.Findings.First(x => ReferenceEquals(x.Installed, desktop)).LatestVersion == "3.6.6",
        "GitHub Desktop remove apenas o prefixo release-");
    Assert(result.Findings.First(x => ReferenceEquals(x.Installed, sumatra)).LatestVersion == "3.6.1",
        "SumatraPDF remove apenas o sufixo rel");
    Assert(result.Findings.First(x => ReferenceEquals(x.Installed, thunderbird)).LatestVersion == "146.0.1",
        "Thunderbird estável identificado sem misturar ESR");
    Assert(fixture.Requests == 9, "Uma chamada apenas por projeto reconhecido");
    var second = await source.QueryAsync(list, CancellationToken.None);
    Assert(second.Findings.Count == 9 && fixture.Requests == 9, "Cache elimina chamadas repetidas");
    var forced = await source.QueryAsync(list, CancellationToken.None, true);
    Assert(forced.Findings.Count == 9 && fixture.Requests == 18, "Consulta manual atualiza todas as fontes");
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
    Assert(merged[9].State == MatchState.Unmatched && merged[10].State == MatchState.Unmatched &&
        merged[11].State == MatchState.Unmatched && merged[12].State == MatchState.Unmatched &&
        merged[13].State == MatchState.Unmatched,
        "Pacotes não reconhecidos ou ESR não são falsos positivos");
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

var sharex = App("ShareX", "20.2.0", "ShareX Team");
var handbrake = App("HandBrake", "1.10.0", "HandBrake Team");
var rufus = App("Rufus", "4.13", "Akeo Consulting");
var qb = App("qBittorrent", "5.2.0", "The qBittorrent Project");
var spoofShare = App("ShareX", "20.2.0", "Falso Editor");
var previewShare = App("ShareX Preview", "20.2.0", "ShareX Team");
var spoofRufus = App("Rufus", "4.13", "Fornecedor Desconhecido");
var spoofQb = App("qBittorrent Pro", "5.2.0", "The qBittorrent Project");
Assert(ApplicationIdentityCatalog.Resolve(sharex.Name, sharex.Publisher)?.Id == "sharex" &&
       ApplicationIdentityCatalog.Resolve(handbrake.Name, handbrake.Publisher)?.Id == "handbrake" &&
       ApplicationIdentityCatalog.Resolve(rufus.Name, rufus.Publisher)?.Id == "rufus" &&
       ApplicationIdentityCatalog.Resolve(qb.Name, qb.Publisher)?.Id == "qbittorrent",
       "Quatro identidades novas exigem nome exato e editor permitido");
Assert(ApplicationIdentityCatalog.Resolve(spoofShare.Name, spoofShare.Publisher) is null &&
       ApplicationIdentityCatalog.Resolve(previewShare.Name, previewShare.Publisher) is null &&
       ApplicationIdentityCatalog.Resolve(spoofRufus.Name, spoofRufus.Publisher) is null &&
       ApplicationIdentityCatalog.Resolve(spoofQb.Name, spoofQb.Publisher) is null,
       "Nomes parecidos e editores falsos não participam de atualizações");

OfficialReleaseSources.ClearCacheForTests();
var sourcesFixture = new Fixture();
using (var officialSources = new OfficialReleaseSources(sourcesFixture))
{
    var apps = new[] { sharex, handbrake, rufus, qb, spoofShare, previewShare, spoofRufus, spoofQb };
    var result = await officialSources.QueryAsync(apps, CancellationToken.None);
    Assert(result.Findings.Count == 4 && result.Diagnostics.Count == 4 &&
        result.Diagnostics.All(x => x.Status == "ok") && sourcesFixture.Requests == 4,
        "Quatro endpoints oficiais independentes e nenhuma requisição por falso editor");
    Assert(result.Findings.Any(x => ReferenceEquals(x.Installed, sharex) && x.LatestVersion == "21.0.0") &&
        result.Findings.Any(x => ReferenceEquals(x.Installed, handbrake) && x.LatestVersion == "1.11.2") &&
        result.Findings.Any(x => ReferenceEquals(x.Installed, rufus) && x.LatestVersion == "4.15") &&
        result.Findings.Any(x => ReferenceEquals(x.Installed, qb) && x.LatestVersion == "5.2.3"),
        "Tags oficiais estáveis normalizadas sem versões inventadas");
    var merged = MultiSourceMatcher.Merge(apps, null, result.Findings);
    Assert(merged.Count(x => x.State == MatchState.Candidate) == 4 &&
        merged.Skip(4).All(x => x.State == MatchState.Unmatched),
        "Novas fontes geram só quatro candidatos informativos");
    await officialSources.QueryAsync([sharex, handbrake, rufus, qb], CancellationToken.None);
    Assert(sourcesFixture.Requests == 4, "Cache conjunto evita repetição de consultas");
}
Assert(OfficialReleaseSources.NormalizeOfficialVersionTag("qbittorrent", "release-5.2.3") == "5.2.3" &&
       OfficialReleaseSources.NormalizeOfficialVersionTag("sharex", "v21.0.0-beta1") == "21.0.0-beta1",
       "Tag release- e pre-lançamento separados sem autorizar beta");
OfficialReleaseSources.ClearCacheForTests();
using (var partial = new OfficialReleaseSources(new Fixture(failSharex:true)))
{
    var scan = await partial.QueryAsync([sharex, handbrake, rufus, qb], CancellationToken.None);
    Assert(scan.Findings.Count == 3 && scan.Diagnostics.Count == 4 &&
        scan.Diagnostics.Any(d => d.Provider == "sharex" && d.Status != "ok"),
        "Falha de ShareX não cancela HandBrake, Rufus ou qBittorrent");
}
OfficialReleaseSources.ClearCacheForTests();
using (var unstable = new OfficialReleaseSources(new Fixture(badNewTags:true)))
{
    var scan = await unstable.QueryAsync([sharex, handbrake, rufus, qb], CancellationToken.None);
    Assert(scan.Findings.Count == 3 && scan.Diagnostics.Any(d => d.Provider == "qbittorrent" && d.Status != "ok"),
        "Pré-lançamento qBittorrent rejeitado sem prejudicar outras fontes");
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
Assert(OfficialReleaseSources.NormalizeOfficialVersionTag("sumatrapdf", "3.6.1rel") == "3.6.1" &&
    OfficialReleaseSources.NormalizeOfficialVersionTag("github-desktop", "release-3.6.6") == "3.6.6" &&
    OfficialReleaseSources.NormalizeOfficialVersionTag("obs", "v32.2.2-beta1") == "32.2.2-beta1",
    "Etiquetas de versões não autorizam prereleases");
Assert(Matching.CompareVersions("1.2", "1.2.0.0") == 0,
    "Normalização de versões preservada");
var edge = App("Microsoft Edge", "155.0.4283.60", "Microsoft Corporation");
var terminal = App("Windows Terminal", "1.24.12741.0", "Microsoft Corporation");
var proton = App("Proton Pass", "1.37.0", "Proton AG");
var gameInput = App("Microsoft GameInput", "3.5.270.0", "Microsoft Corporation");
var outlook = App("Outlook for Windows", "1.2025.416.400", "Microsoft Corporation");
var wrongGame = App("Microsoft GameInput Preview", "3.5.270.0", "Microsoft Corporation");
var wrongOutlook = App("Outlook for Windows", "1.2025.416.400", "Outro Fornecedor");
var wrongEdge = App("Microsoft Edge Beta", "155.0.4283.60", "Microsoft Corporation");
var wrongProton = App("Proton Pass", "1.37.0", "Fornecedor Desconhecido");
var wrongTerminal = App("Windows Terminal Preview", "1.24.12741.0", "Microsoft Corporation");
OfficialReleaseSources.ClearCacheForTests();
var expandedFixture = new Fixture();
using (var extraSource = new OfficialReleaseSources(expandedFixture))
{
    var expanded = await extraSource.QueryAsync(
        [edge, terminal, proton, wrongEdge, wrongProton, wrongTerminal], CancellationToken.None);
    Assert(expanded.Findings.Count == 3 && expanded.Diagnostics.Count == 3 &&
        expanded.Diagnostics.All(x => x.Status == "ok"),
        "Edge, Terminal e Proton reconhecidos com exclusão dos canais errados");
    Assert(expanded.Findings.First(x => ReferenceEquals(x.Installed, edge)).LatestVersion == "155.0.4283.62",
        "Edge recebe Stable Windows da arquitetura correta; não Beta ou Mac");
    Assert(expanded.Findings.First(x => ReferenceEquals(x.Installed, terminal)).LatestVersion == "1.25.2733.0",
        "Windows Terminal Stable preserva todos os componentes da versão");
    Assert(expanded.Findings.First(x => ReferenceEquals(x.Installed, proton)).LatestVersion == "1.42.0",
        "Proton Pass lê changelog oficial desktop e não aplicativo de navegador");
    Assert(expandedFixture.Requests == 3, "Três fontes extras consultadas somente uma vez");
    var cachedExtra = await extraSource.QueryAsync([edge, terminal, proton], CancellationToken.None);
    Assert(cachedExtra.Findings.Count == 3 && expandedFixture.Requests == 3,
        "Edge/Terminal/Proton compartilham cache em memória");
    var mergedExtras = MultiSourceMatcher.Merge([edge, terminal, proton], null, expanded.Findings);
    Assert(mergedExtras.Count(x => x.State == MatchState.Candidate) == 3,
        "As três fontes adicionam candidatos sem simular WinGet");
}
OfficialReleaseSources.ClearCacheForTests();
using (var extraSource = new OfficialReleaseSources(new Fixture(failEdge:true)))
{
    var found = await extraSource.QueryAsync([edge, terminal, proton], CancellationToken.None);
    Assert(found.Findings.Count == 2 &&
        found.Diagnostics.Any(x => x.Provider == "edge-stable" && x.Status != "ok"),
        "Falha do serviço Edge não apaga outras fontes");
}
OfficialReleaseSources.ClearCacheForTests();
using (var extraSource = new OfficialReleaseSources(new Fixture(failProton:true)))
{
    var found = await extraSource.QueryAsync([edge, terminal, proton], CancellationToken.None);
    Assert(found.Findings.Count == 2 && found.Diagnostics.Any(x => x.Provider == "proton-pass-desktop" && x.Status != "ok"),
        "Falha no changelog Proton não apaga Microsoft/GitHub");
}
Assert(OfficialReleaseSources.IsAllowedUrl(new Uri("https://edgeupdates.microsoft.com/api/products?view=enterprise")) &&
    OfficialReleaseSources.IsAllowedUrl(new Uri("https://raw.githubusercontent.com/ProtonMail/WebClients/main/applications/pass-desktop/CHANGELOG.md")) &&
    !OfficialReleaseSources.IsAllowedUrl(new Uri("https://edgeupdates.microsoft.com.evil.org/")) &&
    !OfficialReleaseSources.IsAllowedUrl(new Uri("http://raw.githubusercontent.com/")),
    "Novos domínios somente HTTPS com origem exata");
OfficialReleaseSources.ClearCacheForTests();
using (var wingetSource = new OfficialReleaseSources(new Fixture()))
{
    var dirResults = await wingetSource.QueryAsync([gameInput, outlook, wrongGame, wrongOutlook], CancellationToken.None);
    Assert(dirResults.Findings.Count == 2 && dirResults.Diagnostics.Count == 2,
        "Somente GameInput e Outlook Microsoft reconhecidos");
    Assert(dirResults.Findings.First(x => ReferenceEquals(x.Installed, gameInput)).LatestVersion == "3.5.283",
        "GameInput lê versão mais recente do índice Microsoft winget-pkgs");
    Assert(dirResults.Findings.First(x => ReferenceEquals(x.Installed, outlook)).LatestVersion == "1.2026.812.100",
        "Outlook lê versão mais recente do índice Microsoft winget-pkgs");
    var union = MultiSourceMatcher.Merge([gameInput, outlook], null, dirResults.Findings);
    Assert(union.Count(x => x.State == MatchState.Candidate) == 2,
        "GameInput e Outlook entram sem executar WinGet");
}
using (var doc = System.Text.Json.JsonDocument.Parse("[{\"name\":\".validation\",\"type\":\"dir\"},{\"name\":\"3.5.283\",\"type\":\"dir\"},{\"name\":\"9999.9999\",\"type\":\"file\"}]"))
{
    Assert(OfficialReleaseSources.ParseWinGetVersionDirectories(doc.RootElement) == "3.5.283",
        "Somente diretórios numéricos participam da ordenação");
}
// Formatos defeituosos nao podem derrubar as outras fontes.
using (var malformed = System.Text.Json.JsonDocument.Parse("[null,42,{\"type\":true},{\"type\":\"dir\",\"name\":\"1.2.3\"}]"))
    Assert(OfficialReleaseSources.ParseWinGetVersionDirectories(malformed.RootElement) == "1.2.3",
        "Indice ignora elementos de tipos inesperados sem excecao global");
using (var malformed = System.Text.Json.JsonDocument.Parse("[null,{\"Product\":42},{\"Product\":\"Stable\",\"Releases\":[null,{\"Platform\":false}]}]"))
{
    try { OfficialReleaseSources.ParseEdgeStableVersion(malformed.RootElement); throw new Exception("Edge invalido aceito"); }
    catch (InvalidDataException) { Assert(true, "Edge malformado resulta em fonte indisponivel controlada"); }
}
OfficialReleaseSources.ClearCacheForTests();
using (var source = new OfficialReleaseSources(new AdversarialFixture()))
{
    var scan = await source.QueryAsync([obs, firefox], CancellationToken.None);
    Assert(scan.Findings.Count == 1 && scan.Findings[0].Provider == "Mozilla",
        "Tag numerica marcada prerelease nao vence fonte estavel");
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    try { await source.QueryAsync([firefox], cancelled.Token); throw new Exception("Cache ignorou cancelamento"); }
    catch (OperationCanceledException) { Assert(true, "Cancelamento respeitado mesmo com cache preenchido"); }
}
OfficialReleaseSources.ClearCacheForTests();
using (var source = new OfficialReleaseSources(new AdversarialFixture(stallBody: true), TimeSpan.FromMilliseconds(150)))
{
    var clock = Stopwatch.StartNew();
    var scan = await source.QueryAsync([obs, firefox], CancellationToken.None);
    Assert(scan.Findings.Count == 1 && scan.Diagnostics.Any(d => d.Status.Contains("Canceled")) && clock.Elapsed < TimeSpan.FromSeconds(5),
        "Corpo parado respeita prazo local e preserva resultado da outra fonte");
}

Console.WriteLine($"{tests}/{tests} testes de motores múltiplos aprovados");
if (args.Contains("--live", StringComparer.OrdinalIgnoreCase))
{
    // Usa SOMENTE GETs HTTPS de dados públicos; nenhum programa instalado é consultado no CI.
    OfficialReleaseSources.ClearCacheForTests();
    using var source = new OfficialReleaseSources();
    var live = await source.QueryAsync([obs, firefox, notepad, powertoys, shell, sevenzip,
        desktop, sumatra, thunderbird, edge, terminal, proton, gameInput, outlook,
        sharex, handbrake, rufus, qb],
        new CancellationTokenSource(TimeSpan.FromSeconds(25)).Token, true);
    foreach (var item in live.Diagnostics)
        Console.WriteLine($"FONTE_AO_VIVO {item.Provider} status={item.Status} tempo_ms={item.Milliseconds}");
    Console.WriteLine($"FONTE_AO_VIVO_TOTAL resultados={live.Findings.Count}");
    if (live.Findings.Count < 1)
        throw new Exception("Nenhum provedor oficial disponível no teste ao vivo.");
}

internal sealed class Fixture(bool failMozilla = false, bool badTags = false, int delay = 0,
    bool failEdge = false, bool failProton = false, bool failSharex = false, bool badNewTags = false) : HttpMessageHandler
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
            uri.Host != "product-details.mozilla.org" &&
            uri.Host != "edgeupdates.microsoft.com" &&
            uri.Host != "raw.githubusercontent.com")) throw new Exception("Host não aprovado");
        if (uri.Host == "api.github.com" && uri.AbsolutePath.Contains("/microsoft/winget-pkgs/contents/"))
        {
            string versions = uri.AbsolutePath.EndsWith("GameInput", StringComparison.OrdinalIgnoreCase)
                ? "[{\"name\":\"3.5.270\",\"type\":\"dir\"},{\"name\":\"3.5.283\",\"type\":\"dir\"}]"
                : "[{\"name\":\"1.2026.720.100\",\"type\":\"dir\"},{\"name\":\"1.2026.812.100\",\"type\":\"dir\"}]";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(versions) };
        }
        if (uri.Host == "edgeupdates.microsoft.com")
        {
            if (failEdge) return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            string correctArch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            string edgeJson = "[{\"Product\":\"Beta\",\"Releases\":[{\"Platform\":\"Windows\",\"Architecture\":\"" +
                correctArch + "\",\"ProductVersion\":\"999.0.0.0\"}]},{\"Product\":\"Stable\",\"Releases\":[" +
                "{\"Platform\":\"MacOS\",\"Architecture\":\"universal\",\"ProductVersion\":\"999.0.0.0\"}," +
                "{\"Platform\":\"Windows\",\"Architecture\":\"" + correctArch +
                "\",\"ProductVersion\":\"155.0.4283.62\"}]}]";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(edgeJson) };
        }
        if (uri.Host == "raw.githubusercontent.com")
        {
            if (failProton) return new HttpResponseMessage(HttpStatusCode.NotFound);
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("# Proton Pass desktop\n\n### Version 1.42.0\n- official desktop\n")
            };
        }
        if (failSharex && uri.AbsolutePath.Contains("/ShareX/ShareX/", StringComparison.OrdinalIgnoreCase))
            return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        if (uri.Host == "api.github.com")
        {
            string? tag = uri.AbsolutePath switch
            {
                "/repos/ShareX/ShareX/releases/latest" => "v21.0.0",
                "/repos/HandBrake/HandBrake/releases/latest" => "1.11.2",
                "/repos/pbatard/rufus/releases/latest" => "v4.15",
                "/repos/qbittorrent/qBittorrent/releases/latest" =>
                    badNewTags ? "release-5.3.0beta1" : "release-5.2.3",
                _ => null
            };
            if (tag is not null)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\\"tag_name\\":\\"" + tag + "\\"}")
                };
        }
        if (failMozilla && uri.Host == "product-details.mozilla.org")
            return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        string body = uri.Host == "product-details.mozilla.org"
            ? uri.AbsolutePath.Contains("thunderbird_versions")
              ? "{\"LATEST_THUNDERBIRD_VERSION\":\"146.0.1\"}"
              : "{\"LATEST_FIREFOX_VERSION\":\"140.0.2\"}"
            : uri.AbsolutePath.Contains("obs-studio")
            ? "{\"tag_name\":\"" + (badTags ? "v33.0.0-beta1" : "v32.2.2") + "\"}"
            : uri.AbsolutePath.Contains("notepad-plus-plus")
            ? "{\"tag_name\":\"v8.8.4\"}"
            : uri.AbsolutePath.Contains("PowerToys")
            ? "{\"tag_name\":\"v0.93.0\"}"
            : uri.AbsolutePath.Contains("ip7z/7zip")
            ? "{\"tag_name\":\"26.03\"}"
            : uri.AbsolutePath.Contains("desktop/desktop")
            ? "{\"tag_name\":\"release-3.6.6\"}"
            : uri.AbsolutePath.Contains("sumatrapdfreader/sumatrapdf")
            ? "{\"tag_name\":\"3.6.1rel\"}"
            : uri.AbsolutePath.Contains("microsoft/terminal")
            ? "{\"tag_name\":\"v1.25.2733.0\"}"
            : "{\"tag_name\":\"v7.5.2\"}";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    }
}

internal sealed class AdversarialFixture(bool stallBody = false) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        bool mozilla = request.RequestUri!.Host == "product-details.mozilla.org";
        HttpContent content = mozilla ? new StringContent("{\"LATEST_FIREFOX_VERSION\":\"140.0.2\"}") :
            stallBody ? new StreamContent(new StalledStream()) :
            new StringContent("{\"tag_name\":\"v999.0.0\",\"prerelease\":true}");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
internal sealed class StalledStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    { await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
