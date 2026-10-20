using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Motores SOMENTE LEITURA. Fontes oficiais fixas; nenhum script, instalador,
// comando de terminal ou dado do computador é enviado nas consultas.
internal sealed record OfficialRelease(InstalledApp Installed, string LatestVersion,
    string Provider, string OfficialPage);
internal sealed record ProviderDiagnostic(string Provider, string Status, long Milliseconds);
internal sealed record ProviderScan(IReadOnlyList<OfficialRelease> Findings,
    IReadOnlyList<ProviderDiagnostic> Diagnostics);

internal sealed class OfficialReleaseSources : IDisposable
{
    private sealed record Recipe(string Id, string Provider, string Name, string Endpoint,
        string Page, Func<InstalledApp, bool> Matches, string JsonProperty);
    private sealed record CachedVersion(string Version, DateTimeOffset Expires);
    private static readonly ConcurrentDictionary<string, CachedVersion> Cache = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim GithubConcurrency = new(2, 2);
    private static readonly HttpClient SharedClient = MakeClient();
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly TimeSpan _responseTimeout;

    // Nomes e fabricantes são verificados ANTES de qualquer requisição.
    // Não tentamos inferir URLs a partir da lista de programas do usuário.
    private static readonly Recipe[] Known =
    [
        new("gameinput-winget", "Catálogo WinGet", "Microsoft GameInput",
            "https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/m/Microsoft/GameInput",
            "https://github.com/microsoft/winget-pkgs/tree/master/manifests/m/Microsoft/GameInput",
            GameInput, "__WINGET_VERSIONS__"),
        new("outlook-winget", "Catálogo WinGet", "Outlook for Windows",
            "https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/m/Microsoft/Outlook",
            "https://github.com/microsoft/winget-pkgs/tree/master/manifests/m/Microsoft/Outlook",
            Outlook, "__WINGET_VERSIONS__"),
        new("edge-stable", "Microsoft", "Microsoft Edge",
            "https://edgeupdates.microsoft.com/api/products?view=enterprise",
            "https://www.microsoft.com/edge", EdgeStable, "__EDGE_STABLE__"),
        new("proton-pass-desktop", "Proton", "Proton Pass",
            "https://raw.githubusercontent.com/ProtonMail/WebClients/main/applications/pass-desktop/CHANGELOG.md",
            "https://proton.me/pass/download", ProtonPass, "__PROTON_CHANGELOG__"),
        new("windows-terminal", "GitHub oficial", "Windows Terminal",
            "https://api.github.com/repos/microsoft/terminal/releases/latest",
            "https://github.com/microsoft/terminal/releases",
            WindowsTerminal, "tag_name"),
        new("firefox-stable", "Mozilla", "Firefox",
            "https://product-details.mozilla.org/1.0/firefox_versions.json",
            "https://www.firefox.com/", Firefox, "LATEST_FIREFOX_VERSION"),
        new("thunderbird-stable", "Mozilla", "Thunderbird",
            "https://product-details.mozilla.org/1.0/thunderbird_versions.json",
            "https://www.thunderbird.net/", Thunderbird, "LATEST_THUNDERBIRD_VERSION"),
        new("obs", "GitHub oficial", "OBS Studio",
            "https://api.github.com/repos/obsproject/obs-studio/releases/latest",
            "https://obsproject.com/download", Obs, "tag_name"),
        new("notepadpp", "GitHub oficial", "Notepad++",
            "https://api.github.com/repos/notepad-plus-plus/notepad-plus-plus/releases/latest",
            "https://notepad-plus-plus.org/downloads/", NotepadPlus, "tag_name"),
        new("powertoys", "GitHub oficial", "PowerToys",
            "https://api.github.com/repos/microsoft/PowerToys/releases/latest",
            "https://learn.microsoft.com/windows/powertoys/", PowerToys, "tag_name"),
        new("powershell", "GitHub oficial", "PowerShell 7",
            "https://api.github.com/repos/PowerShell/PowerShell/releases/latest",
            "https://github.com/PowerShell/PowerShell/releases", PowerShell7, "tag_name"),
        new("sevenzip", "GitHub oficial", "7-Zip",
            "https://api.github.com/repos/ip7z/7zip/releases/latest",
            "https://www.7-zip.org/download.html", SevenZip, "tag_name"),
        new("github-desktop", "GitHub oficial", "GitHub Desktop",
            "https://api.github.com/repos/desktop/desktop/releases/latest",
            "https://desktop.github.com/", GitHubDesktop, "tag_name"),
        new("sumatrapdf", "GitHub oficial", "SumatraPDF",
            "https://api.github.com/repos/sumatrapdfreader/sumatrapdf/releases/latest",
            "https://www.sumatrapdfreader.org/download-free-pdf-viewer",
            SumatraPdf, "tag_name")
    ];

    private static bool Publisher(InstalledApp app, params string[] allowed)
    {
        var maker = Matching.NormalizePublisher(app.Publisher);
        return allowed.Any(x => maker == x);
    }
    private static bool GameInput(InstalledApp a) =>
        a.Name.Equals("Microsoft GameInput", StringComparison.OrdinalIgnoreCase) &&
        Publisher(a, "microsoft");
    private static bool Outlook(InstalledApp a) =>
        (a.Name.Equals("Outlook for Windows", StringComparison.OrdinalIgnoreCase) ||
         a.Name.Equals("Outlook para Windows", StringComparison.OrdinalIgnoreCase)) &&
        Publisher(a, "microsoft");
    private static bool EdgeStable(InstalledApp a) =>
        Publisher(a, "microsoft") &&
        a.Name.Equals("Microsoft Edge", StringComparison.OrdinalIgnoreCase);
    private static bool ProtonPass(InstalledApp a) =>
        Publisher(a, "proton ag", "proton", "proton technologies ag") &&
        a.Name.Equals("Proton Pass", StringComparison.OrdinalIgnoreCase);
    private static bool WindowsTerminal(InstalledApp a) =>
        Publisher(a, "microsoft") &&
        (a.Name.Equals("Windows Terminal", StringComparison.OrdinalIgnoreCase) ||
         a.Name.Equals("Terminal do Windows", StringComparison.OrdinalIgnoreCase));
    private static bool Firefox(InstalledApp a) =>
        Publisher(a, "mozilla", "mozilla corporation", "mozilla foundation") &&
        Regex.IsMatch(a.Name, @"^(Mozilla )?Firefox(\s*\(x64[^)]*\))?$", RegexOptions.IgnoreCase) &&
        !a.Name.Contains("ESR", StringComparison.OrdinalIgnoreCase);
    private static bool Thunderbird(InstalledApp a) =>
        Publisher(a, "mozilla") &&
        // Sem indicação de canal não diferenciar ESR de Stable.
        Regex.IsMatch(a.Name, @"^(Mozilla )?Thunderbird\s*\((?:Release|Stable)\)$", RegexOptions.IgnoreCase);
    private static bool SevenZip(InstalledApp a) =>
        Publisher(a, "igor pavlov") &&
        Regex.IsMatch(a.Name, @"^7-Zip\s+\d{2}\.\d{1,2}(?:\s+\((?:x64|x86)\))?$",
            RegexOptions.IgnoreCase);
    private static bool GitHubDesktop(InstalledApp a) =>
        Publisher(a, "github") &&
        a.Name.Equals("GitHub Desktop", StringComparison.OrdinalIgnoreCase);
    private static bool SumatraPdf(InstalledApp a) =>
        Publisher(a, "krzysztof kowalczyk", "sumatrapdf", "sumatrapdf reader") &&
        Regex.IsMatch(a.Name, @"^SumatraPDF(?:\s*\((?:x64|x86|64-bit|32-bit)\))?$",
            RegexOptions.IgnoreCase);
    private static bool Obs(InstalledApp a) =>
        a.Name.Equals("OBS Studio", StringComparison.OrdinalIgnoreCase) &&
        Publisher(a, "obs project", "obs project llc", "obs");
    private static bool NotepadPlus(InstalledApp a) =>
        Regex.IsMatch(a.Name, @"^Notepad\+\+(?:\s*\((?:64-bit x64|32-bit x86|x64|x86)\))?$", RegexOptions.IgnoreCase) &&
        Publisher(a, "notepad team", "notepad", "don ho");
    private static bool PowerToys(InstalledApp a) =>
        (a.Name.Equals("PowerToys", StringComparison.OrdinalIgnoreCase) ||
         a.Name.Equals("Microsoft PowerToys", StringComparison.OrdinalIgnoreCase)) &&
        Publisher(a, "microsoft");
    private static bool PowerShell7(InstalledApp a) =>
        Regex.IsMatch(a.Name, @"^PowerShell 7(?:-x64|-x86| \(x64\)| \(x86\))?$", RegexOptions.IgnoreCase) &&
        Publisher(a, "microsoft");

    private static HttpClient MakeClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(14)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GerenciadorFinal", "5.12.21"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
    internal OfficialReleaseSources(HttpMessageHandler? handler = null, TimeSpan? responseTimeout = null)
    {
        _responseTimeout = responseTimeout ?? TimeSpan.FromSeconds(14);
        _ownsHttp = handler is not null;
        _http = handler is null ? SharedClient : new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(14)
        };
        if (_ownsHttp)
        {
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GerenciadorFinal", "5.12.21"));
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    internal async Task<ProviderScan> QueryAsync(IEnumerable<InstalledApp> installed,
        CancellationToken token, bool forceRefresh = false)
    {
        token.ThrowIfCancellationRequested();
        var apps = installed.ToArray();
        var applicable = Known.Select(recipe => (
            Recipe: recipe,
            Apps: apps.Where(app => app.EligibleForUpdates && recipe.Matches(app) &&
                ApplicationIdentityCatalog.MatchesOfficialSource(
                    app.Name, app.Publisher, recipe.Id)).ToArray()))
            .Where(item => item.Apps.Length > 0).ToArray();
        if (applicable.Length == 0) return new([], []);
        var tasks = applicable.Select(async target =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                string version = await GetVersionAsync(target.Recipe, token, forceRefresh);
                var findings = target.Apps.Select(app =>
                    new OfficialRelease(app, version, target.Recipe.Provider,
                        target.Recipe.Page)).ToArray();
                return (Findings: findings,
                    Diagnostic: new ProviderDiagnostic(target.Recipe.Id, "ok", clock.ElapsedMilliseconds));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException
                or OperationCanceledException or IOException)
            {
                // Falha de uma fonte não invalida candidatos de outras fontes.
                return (Findings: Array.Empty<OfficialRelease>(),
                    Diagnostic: new ProviderDiagnostic(target.Recipe.Id, ex.GetType().Name, clock.ElapsedMilliseconds));
            }
        });
        var results = await Task.WhenAll(tasks);
        return new ProviderScan(results.SelectMany(x => x.Findings).ToArray(),
            results.Select(x => x.Diagnostic).ToArray());
    }

    private async Task<string> GetVersionAsync(Recipe recipe, CancellationToken token, bool force)
    {
        token.ThrowIfCancellationRequested();
        if (!force && Cache.TryGetValue(recipe.Id, out var cached) &&
            cached.Expires > DateTimeOffset.UtcNow) return cached.Version;
        var gate = Gates.GetOrAdd(recipe.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try
        {
            if (!force && Cache.TryGetValue(recipe.Id, out cached) &&
                cached.Expires > DateTimeOffset.UtcNow) return cached.Version;
            bool github = recipe.Endpoint.StartsWith("https://api.github.com/", StringComparison.Ordinal);
            if (github) await GithubConcurrency.WaitAsync(token);
            try
            {
                var value = await FetchVersionAsync(recipe, token);
                Cache[recipe.Id] = new CachedVersion(value, DateTimeOffset.UtcNow.Add(Ttl));
                return value;
            }
            finally { if (github) GithubConcurrency.Release(); }
        }
        finally { gate.Release(); }
    }

    private async Task<string> FetchVersionAsync(Recipe recipe, CancellationToken token)
    {
        Uri url = new(recipe.Endpoint);
        if (!IsAllowedUrl(url)) throw new InvalidDataException("Endereço não autorizado.");
        // Proton pode ficar bloqueado por 14s; limite local de 5s sem atrasar demais outras fontes.
        // ResponseHeadersRead encerra o timeout do HttpClient nos cabecalhos.
        // Este token limita tambem o corpo, inclusive servidores que param de enviar.
        using var quickTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        quickTimeout.CancelAfter(recipe.Id == "proton-pass-desktop" && _responseTimeout > TimeSpan.FromSeconds(5)
            ? TimeSpan.FromSeconds(5) : _responseTimeout);
        CancellationToken requestToken = quickTimeout.Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken);
        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
            throw new HttpRequestException("Limite de solicitações ou acesso negado.");
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Consulta indisponível.");
        long maximumBytes = recipe.Id == "edge-stable" ? 4 * 1024 * 1024 :
            recipe.Id == "proton-pass-desktop" ? 128 * 1024 : 128 * 1024;
        if (response.Content.Headers.ContentLength > maximumBytes)
            throw new InvalidDataException("Resposta excessivamente grande.");
        using var body = await response.Content.ReadAsStreamAsync(requestToken);
        using var bounded = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await body.ReadAsync(buffer, requestToken)) != 0)
        {
            if (bounded.Length + read > maximumBytes) throw new InvalidDataException("Resposta acima do limite.");
            bounded.Write(buffer, 0, read);
        }
        bounded.Position = 0;
        string tag;
        if (recipe.JsonProperty == "__PROTON_CHANGELOG__")
        {
            using var reader = new StreamReader(bounded);
            string changelog = await reader.ReadToEndAsync(requestToken);
            // Não interpretar arquivos executáveis ou nomes de branches como versões.
            // Apenas o primeiro cabeçalho de versão estável do desktop é relevante.
            var match = Regex.Match(changelog, @"(?m)^### Version (\d{1,4}(?:\.\d{1,6}){1,3})\s*$");
            if (!match.Success) throw new InvalidDataException("Versão desktop não identificada.");
            tag = match.Groups[1].Value;
        }
        else
        {
            using var doc = await JsonDocument.ParseAsync(bounded, cancellationToken: requestToken);
            if (recipe.JsonProperty == "__EDGE_STABLE__")
                tag = ParseEdgeStableVersion(doc.RootElement);
            else if (recipe.JsonProperty == "__WINGET_VERSIONS__")
                tag = ParseWinGetVersionDirectories(doc.RootElement);
            else
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty(recipe.JsonProperty, out var property) ||
                    property.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("Versão não localizada na resposta.");
                if (recipe.JsonProperty == "tag_name" &&
                    ((doc.RootElement.TryGetProperty("prerelease", out var pre) && pre.ValueKind != JsonValueKind.False) ||
                     (doc.RootElement.TryGetProperty("draft", out var draft) && draft.ValueKind != JsonValueKind.False)))
                    throw new InvalidDataException("Publicacao nao estavel.");
                tag = NormalizeOfficialVersionTag(recipe.Id, property.GetString() ?? "");
            }
        }
        if (!Regex.IsMatch(tag, @"^\d{1,4}(?:\.\d{1,6}){1,3}$"))
            throw new InvalidDataException("Versão não estável ou não comparável.");
        return tag;
    }

    // Apenas diretórios de versão do repositório de manifestos comunitários Microsoft.
    // Nenhum YAML ou URL de instalador é baixado/executado. Catálogo não é fonte de assinatura.
    internal static string ParseWinGetVersionDirectories(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Índice WinGet não retornou uma lista.");
        string? newest = null;
        int seen = 0;
        foreach (var item in root.EnumerateArray())
        {
            if (++seen > 1000) throw new InvalidDataException("Índice WinGet excessivamente grande.");
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("type", out var kind) ||
                kind.ValueKind != JsonValueKind.String || kind.GetString() != "dir" ||
                !item.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                continue;
            string version = name.GetString() ?? "";
            if (!Regex.IsMatch(version, @"^\d{1,4}(?:\.\d{1,6}){1,3}$"))
                continue;
            if (newest is null || Matching.CompareVersions(newest, version) is < 0)
                newest = version;
        }
        return newest ?? throw new InvalidDataException("Nenhuma versão estável no índice WinGet.");
    }

    // Seleção estrita: Microsoft Edge Stable, Windows, arquitetura correspondente.
    // Beta, Dev, Canary, EdgeUpdate e Extended Stable não se misturam com Stable.
    internal static string ParseEdgeStableVersion(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Catálogo Edge inválido.");
        string arch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        string? latest = null;
        foreach (var product in root.EnumerateArray())
        {
            if (product.ValueKind != JsonValueKind.Object || !product.TryGetProperty("Product", out var name) ||
                name.ValueKind != JsonValueKind.String ||
                !string.Equals(name.GetString(), "Stable", StringComparison.OrdinalIgnoreCase) ||
                !product.TryGetProperty("Releases", out var releases) ||
                releases.ValueKind != JsonValueKind.Array) continue;
            foreach (var release in releases.EnumerateArray())
            {
                if (release.ValueKind != JsonValueKind.Object || !release.TryGetProperty("Platform", out var platform) ||
                    platform.ValueKind != JsonValueKind.String ||
                    platform.GetString() != "Windows" ||
                    !release.TryGetProperty("Architecture", out var architecture) ||
                    architecture.ValueKind != JsonValueKind.String ||
                    !string.Equals(architecture.GetString(), arch, StringComparison.OrdinalIgnoreCase) ||
                    !release.TryGetProperty("ProductVersion", out var versionElement) ||
                    versionElement.ValueKind != JsonValueKind.String) continue;
                string version = versionElement.GetString() ?? "";
                if (!Regex.IsMatch(version, @"^\d{1,4}(?:\.\d{1,6}){3}$")) continue;
                if (latest is null || Matching.CompareVersions(latest, version) is < 0)
                    latest = version;
            }
        }
        return latest ?? throw new InvalidDataException("Versão Edge Stable não encontrada.");
    }

    // Alguns fabricantes publicam tags próprias. Apenas os padrões
    // conhecidos abaixo são normalizados; prereleases e rótulos novos são rejeitados.
    internal static string NormalizeOfficialVersionTag(string sourceId, string value)
    {
        string tag = value.Trim();
        if (sourceId == "github-desktop" &&
            tag.StartsWith("release-", StringComparison.OrdinalIgnoreCase))
            tag = tag["release-".Length..];
        if (sourceId == "sumatrapdf" &&
            tag.EndsWith("rel", StringComparison.OrdinalIgnoreCase))
            tag = tag[..^3];
        return tag.TrimStart('v', 'V');
    }

    internal static bool IsAllowedUrl(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort &&
        string.IsNullOrEmpty(url.UserInfo) && url.AbsolutePath.StartsWith("/", StringComparison.Ordinal) &&
        (url.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase) ||
         url.Host.Equals("product-details.mozilla.org", StringComparison.OrdinalIgnoreCase) ||
         url.Host.Equals("edgeupdates.microsoft.com", StringComparison.OrdinalIgnoreCase) ||
         url.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    internal static void ClearCacheForTests() => Cache.Clear();
    public void Dispose() { if (_ownsHttp) _http.Dispose(); }
}

internal static class MultiSourceMatcher
{
    // Uma fonte oficial só sobrepõe a comparação geral se nome, editor e
    // versão forem comparáveis. Uma única linha instalada gera no máximo um candidato.
    internal static IReadOnlyList<UpdateMatch> Merge(
        IReadOnlyList<InstalledApp> installed,
        IReadOnlyList<UpdateMatch>? ruckzuck,
        IReadOnlyList<OfficialRelease> official)
    {
        if (ruckzuck is not null && ruckzuck.Count != installed.Count)
            throw new InvalidDataException("Quantidade divergente entre instalada e resultado do catálogo.");
        var byApp = new Dictionary<InstalledApp, OfficialRelease>(ReferenceEqualityComparer.Instance);
        foreach (var release in official)
        {
            if (!installed.Any(x => ReferenceEquals(x, release.Installed))) continue;
            if (Matching.CompareVersions("0.0", release.LatestVersion) is null) continue;
            if (byApp.TryGetValue(release.Installed, out var current))
            {
                if (Matching.CompareVersions(current.LatestVersion, release.LatestVersion) is < 0)
                    byApp[release.Installed] = release;
            }
            else byApp[release.Installed] = release;
        }
        var result = new UpdateMatch[installed.Count];
        for (int i = 0; i < installed.Count; i++)
        {
            var app = installed[i];
            var original = ruckzuck is null ?
                new UpdateMatch(app, null, MatchState.Unmatched) : ruckzuck[i];
            if (!byApp.TryGetValue(app, out var release))
            {
                result[i] = original;
                continue;
            }
            int? delta = Matching.CompareVersions(app.Version, release.LatestVersion);
            if (delta is null) { result[i] = original; continue; }
            // Registros antigos do Visual C++ têm precedência sobre candidatos,
            // mesmo que outras fontes venham a incluí-los.
            if (original.State == MatchState.SupersededRegistration)
            {
                result[i] = original;
                continue;
            }
            if (delta >= 0)
            {
                result[i] = new UpdateMatch(app, null, MatchState.EqualOrNewer, release.Provider);
                continue;
            }
            var catalog = new CatalogEntry(app.Name, app.Name, release.LatestVersion, app.Publisher);
            result[i] = new UpdateMatch(app, catalog, MatchState.Candidate, release.Provider);
        }
        return result;
    }
}
