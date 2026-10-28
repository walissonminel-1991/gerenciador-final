using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GerenciadorAplicativos;

// Apenas GET: não envia a lista de aplicativos instalados, não executa scripts nem instaladores.
internal sealed class RuckZuckCatalog : IDisposable
{
    private static readonly HttpClient SharedHttp = CreateSharedClient();
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly TimeProvider _clock;
    private const int MaximumCatalogBytes = 25 * 1024 * 1024;
    // Cache somente em memoria: reduz duas solicitações HTTPS e não deixa arquivos no computador.
    // Instalações e decisões de segurança continuam consultando o Registro atual.
    private static readonly SemaphoreSlim CacheGate = new(1, 1);
    private sealed record CacheSnapshot(IReadOnlyList<CatalogEntry> Entries, TimeProvider Clock, long Started);
    private static volatile CacheSnapshot? _memoryCache;
    private static readonly TimeSpan MemoryTtl = TimeSpan.FromMinutes(10);
    internal bool UsedMemoryCache { get; private set; }

    private static HttpClient CreateSharedClient()
    {
        // Pool HTTPS reutilizado entre consultas, evita novo handshake TLS a cada clique.
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(55)
        };
        client.DefaultRequestHeaders.UserAgent.Add(ProductHttpIdentity.UserAgent);
        return client;
    }

    internal RuckZuckCatalog(HttpMessageHandler? handler = null, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _ownsClient = handler is not null;
        _http = handler is null ? SharedHttp : new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(55)
        };
        if (_ownsClient)
            _http.DefaultRequestHeaders.UserAgent.Add(ProductHttpIdentity.UserAgent);
    }

    internal async Task<IReadOnlyList<CatalogEntry>> GetCatalogAsync(
        CancellationToken token, bool forceRefresh = false)
    {
        token.ThrowIfCancellationRequested();
        UsedMemoryCache = false;
        if (!forceRefresh && _memoryCache is { } cached && ReferenceEquals(cached.Clock, _clock) && _clock.GetElapsedTime(cached.Started) < MemoryTtl)
        {
            UsedMemoryCache = true;
            return cached.Entries;
        }
        await CacheGate.WaitAsync(token);
        try
        {
            // Outra consulta pode ter preenchido o cache enquanto aguardávamos a trava.
            if (!forceRefresh && _memoryCache is { } existing && ReferenceEquals(existing.Clock, _clock) && _clock.GetElapsedTime(existing.Started) < MemoryTtl)
            {
                UsedMemoryCache = true;
                return existing.Entries;
            }
            var catalog = await DownloadCatalogAsync(token);
            token.ThrowIfCancellationRequested();
            _memoryCache = new CacheSnapshot(catalog, _clock, _clock.GetTimestamp());
            return catalog;
        }
        finally { CacheGate.Release(); }
    }

    // Exercita cache/requisição nos testes sem acessar a rede.
    internal static void ClearCacheForTests()
    {
        _memoryCache = null;
    }

    private async Task<IReadOnlyList<CatalogEntry>> DownloadCatalogAsync(CancellationToken token)
    {
        var location = await DownloadStringAsync(new Uri("https://ruckzuck.tools/rest/v2/geturl"), token, 4096);
        location = location.Trim().Trim('"');
        if (!location.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (location.Contains("/", StringComparison.Ordinal) || location.Contains(':', StringComparison.Ordinal))
                throw new InvalidDataException("Resposta de endereço do catálogo inválida.");
            location = "https://" + location;
        }
        if (!Uri.TryCreate(location, UriKind.Absolute, out var baseUri) || !IsAllowedUri(baseUri))
            throw new InvalidDataException("O provedor retornou um endereço fora do domínio autorizado.");

        // O endpoint é sempre reconstruído localmente, sem aceitar caminhos ou parâmetros externos.
        var catalogUri = new Uri($"https://{baseUri.Host}/rest/v2/getcatalog");
        var json = await DownloadStringAsync(catalogUri, token, MaximumCatalogBytes);
        using var document = JsonDocument.Parse(json);
        JsonDocument? innerDocument = null;
        try
        {
            JsonElement rows = document.RootElement;
            if (rows.ValueKind == JsonValueKind.String)
            {
                // Alguns espelhos retornam JSON como string; uma segunda leitura só nesse caso.
                var inner = rows.GetString() ?? "";
                if (inner.Length > MaximumCatalogBytes ||
                    !inner.TrimStart().StartsWith("[", StringComparison.Ordinal))
                    throw new InvalidDataException("Resposta textual do catálogo não contém lista JSON válida.");
                innerDocument = JsonDocument.Parse(inner);
                rows = innerDocument.RootElement;
            }
            if (rows.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"O catálogo não retornou uma lista de produtos (tipo={rows.ValueKind}).");
            var entries = new List<CatalogEntry>();
            foreach (var item in rows.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                if (TryCreateEntry(item, out var result)) entries.Add(result);
            }
            else if (item.ValueKind == JsonValueKind.String)
            {
                var nestedText = item.GetString();
                if (string.IsNullOrWhiteSpace(nestedText) || nestedText.Length > 8192 ||
                    !nestedText.TrimStart().StartsWith("{", StringComparison.Ordinal)) continue;
                try
                {
                    using var nestedRow = JsonDocument.Parse(nestedText);
                    if (nestedRow.RootElement.ValueKind == JsonValueKind.Object &&
                        TryCreateEntry(nestedRow.RootElement, out var result))
                        entries.Add(result);
                }
                catch (JsonException) { /* Ignora somente registro inválido. */ }
            }
        }
            if (entries.Count == 0)
                throw new InvalidDataException($"O catálogo não possui entradas reconhecíveis (registros={rows.GetArrayLength()}).");
            return entries.ToArray();
        }
        finally { innerDocument?.Dispose(); }
    }

    private static bool TryCreateEntry(JsonElement item, out CatalogEntry result)
    {
        result = null!;
        var shortName = ReadProperty(item, "ShortName") ?? "";
        var productName = ReadProperty(item, "ProductName") ?? "";
        var version = ReadProperty(item, "ProductVersion") ?? "";
        var maker = ReadProperty(item, "Manufacturer") ?? "";
        if (version.Length == 0 || maker.Length == 0 || (shortName.Length == 0 && productName.Length == 0))
            return false;
        result = new CatalogEntry(shortName, productName, version, maker);
        return true;
    }

    private static string? ReadProperty(JsonElement obj, string name)
    {
        foreach (var field in obj.EnumerateObject())
        {
            if (!field.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            string? value = field.Value.ValueKind switch
            {
                JsonValueKind.String => field.Value.GetString(),
                JsonValueKind.Number => field.Value.GetRawText(),
                _ => null
            };
            value = value?.Trim();
            return value is { Length: > 0 and <= 512 } ? value : null;
        }
        return null;
    }

    private static bool IsAllowedUri(Uri url)
    {
        return url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort &&
               string.IsNullOrEmpty(url.UserInfo) &&
               (url.Host.Equals("ruckzuck.tools", StringComparison.OrdinalIgnoreCase) ||
                url.Host.EndsWith(".ruckzuck.tools", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> DownloadStringAsync(Uri url, CancellationToken token, int maxBytes)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("O serviço limitou as consultas (HTTP 429). Tente novamente mais tarde.");
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            throw new HttpRequestException("O serviço exige autorização para esta operação.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long n && n > maxBytes)
            throw new InvalidDataException("O catálogo excedeu o tamanho máximo permitido.");
        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0)
        {
            if (memory.Length + count > maxBytes) throw new InvalidDataException("Resposta maior que o limite de segurança.");
            memory.Write(buffer, 0, count);
        }
        return System.Text.Encoding.UTF8.GetString(memory.ToArray());
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }

}
