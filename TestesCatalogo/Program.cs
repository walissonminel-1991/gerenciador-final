using System.Diagnostics;
using System.Net;
using System.Text.Json;
using GerenciadorAplicativos;

var passed = 0;
void Test(bool ok, string name)
{
    if (!ok) throw new Exception("FALHA " + name);
    Console.WriteLine("OK " + name);
    passed++;
}

const string lookup = "\"https://ruckzuck.tools\"";
var catalog = JsonSerializer.Serialize(new[]
{
    new { ShortName = "OBS Studio", ProductName = "OBS Studio", ProductVersion = "32.2.2", Manufacturer = "OBS Project" },
    new { ShortName = "Programa X", ProductName = "Programa X", ProductVersion = "1.2.0", Manufacturer = "Empresa" }
});
var fake = new FakeHandler(lookup, catalog);
RuckZuckCatalog.ClearCacheForTests();
using (var client = new RuckZuckCatalog(fake))
{
    var first = await client.GetCatalogAsync(CancellationToken.None);
    Test(first.Count == 2 && !client.UsedMemoryCache, "Primeira consulta HTTPS simulada");
    var second = await client.GetCatalogAsync(CancellationToken.None);
    Test(second.Count == 2 && client.UsedMemoryCache && fake.CatalogRequests == 1,
        "Cache em memoria sem requisicao duplicada");
    var forced = await client.GetCatalogAsync(CancellationToken.None, forceRefresh: true);
    Test(forced.Count == 2 && !client.UsedMemoryCache && fake.CatalogRequests == 2,
        "Botao manual força atualizacao da rede");
}

RuckZuckCatalog.ClearCacheForTests();
var concurrentFake = new FakeHandler(lookup, catalog, delayMs: 35);
var clients = Enumerable.Range(0, 16).Select(_ => new RuckZuckCatalog(concurrentFake)).ToArray();
try
{
    var sw = Stopwatch.StartNew();
    var results = await Task.WhenAll(clients.Select(c => c.GetCatalogAsync(CancellationToken.None)));
    sw.Stop();
    Test(results.All(r => r.Count == 2) && concurrentFake.CatalogRequests == 1,
        "16 consultas simultaneas compartilham um download");
    Console.WriteLine($"PERFORMANCE.CONCORRENCIA_MS={sw.ElapsedMilliseconds}");
}
finally { foreach (var c in clients) c.Dispose(); }

RuckZuckCatalog.ClearCacheForTests();
using (var wrapped = new RuckZuckCatalog(new FakeHandler(lookup, JsonSerializer.Serialize(catalog))))
{
    var items = await wrapped.GetCatalogAsync(CancellationToken.None);
    Test(items.Count == 2, "JSON encapsulado continua aceito");
}
RuckZuckCatalog.ClearCacheForTests();
using (var evil = new RuckZuckCatalog(new FakeHandler("\"https://example.org\"", catalog)))
{
    bool rejected = false;
    try { await evil.GetCatalogAsync(CancellationToken.None); }
    catch (InvalidDataException) { rejected = true; }
    Test(rejected, "Bloqueio de origem externa nao autorizada");
}
RuckZuckCatalog.ClearCacheForTests();
using (var broken = new RuckZuckCatalog(new FakeHandler(lookup, "{malformed")))
{
    bool rejected = false;
    try { await broken.GetCatalogAsync(CancellationToken.None); }
    catch (JsonException) { rejected = true; }
    Test(rejected, "JSON invalido nao e armazenado em cache");
}
RuckZuckCatalog.ClearCacheForTests();
using (var throttled = new RuckZuckCatalog(new FakeHandler(lookup, catalog, throttle: true)))
{
    bool rejected = false;
    try { await throttled.GetCatalogAsync(CancellationToken.None); }
    catch (HttpRequestException) { rejected = true; }
    Test(rejected, "HTTP 429 nao e tratado como resultado valido");
}
RuckZuckCatalog.ClearCacheForTests();
using (var cancelled = new RuckZuckCatalog(new FakeHandler(lookup, catalog)))
using (var cts = new CancellationTokenSource())
{
    cts.Cancel();
    bool rejected = false;
    try { await cancelled.GetCatalogAsync(cts.Token); }
    catch (OperationCanceledException) { rejected = true; }
    Test(rejected, "Cancelamento impede download");
}
RuckZuckCatalog.ClearCacheForTests();

Test(Matching.CompareVersions("1.2", "1.2.0") == 0 &&
     Matching.CompareVersions("1.2.0", "1.2.0.0") == 0,
     "Versoes equivalentes nao geram falso positivo");
Test(Matching.CompareVersions("1.2", "1.2.1") < 0 &&
     Matching.CompareVersions("1.2.1", "1.2") > 0,
     "Comparacao de revisoes crescente/decrescente");
Test(Matching.CompareVersions("1.2-beta", "1.2") is null,
     "Versao nao numerica permanece incerta");

var installed = Enumerable.Range(0, 142)
    .Select(i => new InstalledApp("Programa " + i, "1.0", "Fabricante " + i)).ToArray();
var synthetic = Enumerable.Range(0, 631)
    .Select(i => new CatalogEntry("Programa " + i, "Programa " + i, "1.1", "Fabricante " + i)).ToArray();
var warm = Matching.Evaluate(installed, synthetic);
Test(warm.Count == 142, "Comparacao de 142 instalados com 631 pacotes");
var elapsed = new long[20];
for (int i = 0; i < elapsed.Length; i++)
{
    var timer = Stopwatch.StartNew();
    var matches = Matching.Evaluate(installed, synthetic);
    timer.Stop();
    if (matches.Count(x => x.State == MatchState.Candidate) != 142)
        throw new Exception("FALHA coincidencias dos 142 aplicativos");
    elapsed[i] = timer.ElapsedMilliseconds;
}
Array.Sort(elapsed);
Console.WriteLine($"PERFORMANCE.COMPARACAO_142x631_MEDIANA_MS={elapsed[10]} P95_MS={elapsed[18]}");
Test(elapsed[18] < 5000, "Comparacao abaixo de limite generoso de 5 segundos");
await CatalogLifetimeTests.Run(Test);
Console.WriteLine($"{passed}/{passed} testes de catalogo/cache/precisao aprovados");

internal sealed class FakeHandler(string lookup, string json, int delayMs = 0, bool throttle = false) : HttpMessageHandler
{
    private int _catalogRequests;
    internal int CatalogRequests => Volatile.Read(ref _catalogRequests);
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get ||
            request.RequestUri is null || request.RequestUri.Scheme != "https" ||
            request.RequestUri.Host != "ruckzuck.tools")
            throw new InvalidOperationException("Requisicao inesperada: " + request.RequestUri);
        if (delayMs > 0) await Task.Delay(delayMs, cancellationToken);
        if (request.RequestUri.AbsolutePath.EndsWith("/geturl", StringComparison.Ordinal))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(lookup) };
        if (request.RequestUri.AbsolutePath.EndsWith("/getcatalog", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _catalogRequests);
            if (throttle) return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
        throw new InvalidOperationException("Caminho inesperado");
    }
}
