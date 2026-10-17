using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GerenciadorFinal.Laboratorio;

// Apenas GET: não envia a lista de aplicativos instalados, não executa scripts nem instaladores.
internal sealed class RuckZuckCatalog : IDisposable
{
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(55)
    };
    private const int MaximumCatalogBytes = 25 * 1024 * 1024;

    internal RuckZuckCatalog()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GerenciadorFinal-Laboratorio", "0.1"));
    }

    internal async Task<IReadOnlyList<CatalogEntry>> GetCatalogAsync(CancellationToken token)
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
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.String)
        {
            // Alguns servidores intermediários retornam uma lista JSON serializada como string JSON.
            // Aceitar somente se o conteúdo decodificado for realmente um array JSON.
            var inner = document.RootElement.GetString() ?? "";
            var kind = inner.TrimStart().StartsWith("[", StringComparison.Ordinal) ? "lista" : "outro";
            if (inner.Length > MaximumCatalogBytes || kind != "lista")
                throw new InvalidDataException($"Resposta textual inesperada do catálogo (conteudo={kind}; comprimento={inner.Length}).");
            using var nested = JsonDocument.Parse(inner);
            if (nested.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Resposta interna do catálogo não é uma lista (tipo={nested.RootElement.ValueKind}).");
            json = inner;
        }
        else if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"O catálogo não retornou uma lista de produtos (tipo={document.RootElement.ValueKind}).");
        }
        var entries = JsonSerializer.Deserialize<List<CatalogJson>>(json, options) ?? [];
        return entries
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductVersion) && !string.IsNullOrWhiteSpace(x.Manufacturer))
            .Select(x => new CatalogEntry(x.ShortName ?? "", x.ProductName ?? "", x.ProductVersion ?? "", x.Manufacturer ?? ""))
            .ToArray();
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

    public void Dispose() => _http.Dispose();

    internal sealed class CatalogJson
    {
        public CatalogJson() { }
        public string? ShortName { get; set; }
        public string? ProductName { get; set; }
        public string? ProductVersion { get; set; }
        public string? Manufacturer { get; set; }
    }
}
