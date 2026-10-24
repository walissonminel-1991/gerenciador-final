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
    string Provider, string OfficialPage, string SourceId = "",
    string CanonicalId = "", DateTimeOffset ObservedAt = default,
    string Architecture = "unknown", string Channel = "unknown", string Scope = "unknown",
    string InstallType = "unknown", string EvidenceKind = "published-version");
internal sealed record ProviderDiagnostic(string Provider, string Status, long Milliseconds, bool UsedMemoryCache = false);
internal sealed record ProviderScan(IReadOnlyList<OfficialRelease> Findings,
    IReadOnlyList<ProviderDiagnostic> Diagnostics);

internal sealed class OfficialReleaseSources : IDisposable
{
    private sealed record Recipe(string Id, string Provider, string Name, string Endpoint,
        string Page, Func<InstalledApp, bool> Matches, string JsonProperty, string? IdentitySource = null);
    private sealed record CachedVersion(string Version, TimeProvider Clock, long Started, DateTimeOffset ObservedAt);
    private static readonly ConcurrentDictionary<(object Transport, string Id), CachedVersion> Cache = new();
    private static readonly ConcurrentDictionary<(object Transport, string Id), SemaphoreSlim> Gates = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim GithubConcurrency = new(2, 2);
    private static readonly HttpClient SharedClient = MakeClient();
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly TimeSpan _responseTimeout;
    private readonly TimeProvider _clock;
    private readonly object _cacheTransport;

    // Nomes e fabricantes são verificados ANTES de qualquer requisição.
    // Não tentamos inferir URLs a partir da lista de programas do usuário.
    private static readonly Recipe[] Known =
    [
        new("ea-desktop", "Catálogo WinGet HTTPS", "EA app",
            "https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/e/ElectronicArts/EADesktop",
            "https://www.ea.com/ea-app", a => Exact(a, "EA app", "electronic arts"), "__WINGET_VERSIONS__"),
        new("fancontrol", "GitHub oficial", "FanControl",
            "https://api.github.com/repos/Rem0o/FanControl.Releases/releases/latest",
            "https://getfancontrol.com/", a => Exact(a, "FanControl", "remi mercier software", "rem0o"), "tag_name"),
        new("jellyfin-server", "GitHub oficial", "Jellyfin Server",
            "https://api.github.com/repos/jellyfin/jellyfin/releases/latest",
            "https://jellyfin.org/downloads/windows/", JellyfinServer, "tag_name"),
        new("klite-mega", "Catálogo WinGet HTTPS", "K-Lite Mega Codec Pack",
            "https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/c/CodecGuide/K-LiteCodecPack/Mega",
            "https://codecguide.com/download_k-lite_codec_pack_mega.htm", KLiteMega, "__WINGET_VERSIONS__"),
        new("plex", "Catálogo WinGet HTTPS", "Plex Media Server",
            "https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/p/Plex/PlexMediaServer",
            "https://www.plex.tv/media-server-downloads/", PlexMediaServer, "__WINGET_VERSIONS__"),
        new("proton-pass-windows", "Proton Windows", "Proton Pass",
            "https://proton.me/download/PassDesktop/win32/x64/version.json",
            "https://proton.me/pass/download/windows", ProtonPass, "__PROTON_WINDOWS__", "proton-pass-desktop"),
        new("proton-pass-winget", "Catálogo WinGet HTTPS", "Proton Pass",
            "https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/p/Proton/ProtonPass",
            "https://proton.me/pass/download", ProtonPass, "__WINGET_VERSIONS__", "proton-pass-desktop"),
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
            SumatraPdf, "tag_name"),
        new("sharex", "GitHub oficial", "ShareX",
            "https://api.github.com/repos/ShareX/ShareX/releases/latest",
            "https://getsharex.com/", ShareX, "tag_name"),
        new("handbrake", "GitHub oficial", "HandBrake",
            "https://api.github.com/repos/HandBrake/HandBrake/releases/latest",
            "https://handbrake.fr/", HandBrake, "tag_name"),
        new("rufus", "GitHub oficial", "Rufus",
            "https://api.github.com/repos/pbatard/rufus/releases/latest",
            "https://rufus.ie/", Rufus, "tag_name"),
        new("qbittorrent", "GitHub oficial", "qBittorrent",
            "https://api.github.com/repos/qbittorrent/qBittorrent/releases/latest",
            "https://www.qbittorrent.org/", Qbittorrent, "tag_name")
    ];

    private static bool Publisher(InstalledApp app, params string[] allowed)
    {
        var maker = Matching.NormalizePublisher(app.Publisher);
        return allowed.Any(x => maker == x);
    }
    private static bool Exact(InstalledApp a, string name, params string[] publishers) =>
        a.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && Publisher(a, publishers);
    // Sufixo opcional de versão precisa corresponder por inteiro; não confundir com player, preview ou outras edições.
    private static bool JellyfinServer(InstalledApp a) =>
        Publisher(a, "the jellyfin project") &&
        Regex.IsMatch(a.Name, @"^Jellyfin Server(?: \d{1,4}(?:\.\d{1,6}){1,3})?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static bool PlexMediaServer(InstalledApp a) =>
        Publisher(a, "plex") &&
        Regex.IsMatch(a.Name,
            @"^Plex Media Server(?: \d{1,4}(?:\.\d{1,6}){1,3}(?: \((?:x64|x86)\))?)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static bool KLiteMega(InstalledApp a) => Publisher(a, "klcp", "codec guide") &&
        Regex.IsMatch(a.Name, @"^K-Lite Mega Codec Pack(?: \d{1,4}(?:\.\d{1,6}){1,3})?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
        Regex.IsMatch(a.Name, @"^7-Zip\s+\d{2}\.\d{1,2}(?:\s+\((?:x64|x86)(?:\s+edition)?\))?$",
            RegexOptions.IgnoreCase);
    private static bool GitHubDesktop(InstalledApp a) =>
        Publisher(a, "github") &&
        a.Name.Equals("GitHub Desktop", StringComparison.OrdinalIgnoreCase);
    private static bool SumatraPdf(InstalledApp a) =>
        Publisher(a, "krzysztof kowalczyk", "sumatrapdf", "sumatrapdf reader") &&
        Regex.IsMatch(a.Name, @"^SumatraPDF(?:\s*\((?:x64|x86|64-bit|32-bit)\))?$",
            RegexOptions.IgnoreCase);
    // Correspondencia restrita a produto e editor. Nunca habilita limpeza ou
    // instalacao; esta lista e somente informativa.
    private static bool ShareX(InstalledApp a) =>
        a.Name.Equals("ShareX", StringComparison.OrdinalIgnoreCase) &&
        Publisher(a, "sharex team", "sharex", "jaex");
    private static bool HandBrake(InstalledApp a) =>
        a.Name.Equals("HandBrake", StringComparison.OrdinalIgnoreCase) &&
        Publisher(a, "handbrake team", "handbrake", "handbrake project");
    private static bool Rufus(InstalledApp a) =>
        a.Name.Equals("Rufus", StringComparison.OrdinalIgnoreCase) &&
        Publisher(a, "akeo consulting", "pete batard", "akeo");
    private static bool Qbittorrent(InstalledApp a) =>
        (a.Name.Equals("qBittorrent", StringComparison.OrdinalIgnoreCase) ||
         a.Name.Equals("qBittorrent (64-bit)", StringComparison.OrdinalIgnoreCase)) &&
        Publisher(a, "qbittorrent project", "the qbittorrent project", "qbittorrent");
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

    internal static int ApplicableSourceCount(IEnumerable<InstalledApp> installed)
    {
        var apps = installed.ToArray();
        return Known.Count(recipe => apps.Any(app => app.EligibleForUpdates && (app.Channel is "unknown" or "stable") && recipe.Matches(app) &&
            ApplicationIdentityCatalog.MatchesOfficialSource(app.Name, app.Publisher, recipe.IdentitySource ?? recipe.Id)));
    }

    internal static int CoverageStage(InstalledApp app, string sourceId, ProviderScan scan, UpdateMatch match)
    {
        var recipe = Known.SingleOrDefault(r => r.Id == sourceId);
        if (recipe is null) return 1; // no fixed source
        if (!app.EligibleForUpdates) return 2;
        if (app.Channel is not ("unknown" or "stable") || !recipe.Matches(app) || !ApplicationIdentityCatalog.MatchesOfficialSource(
            app.Name, app.Publisher, recipe.IdentitySource ?? recipe.Id)) return 3;
        var diagnostic = scan.Diagnostics.SingleOrDefault(d => d.Provider == sourceId);
        if (diagnostic is null) return 4; // not queried
        if (diagnostic.Status != "ok") return 5;
        if (!scan.Findings.Any(f => ReferenceEquals(f.Installed, app) && f.SourceId == sourceId)) return 6;
        if (match.HasSourceConflict) return 7;
        if (match.RequiresReview) return 8;
        return match.State switch { MatchState.Candidate => 9, MatchState.EqualOrNewer => 10,
            MatchState.NotComparable => 11, _ => 12 };
    }

    private static HttpClient MakeClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(14)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GerenciadorFinal", "5.13.11"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
    internal OfficialReleaseSources(HttpMessageHandler? handler = null, TimeSpan? responseTimeout = null, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _cacheTransport = (object?)handler ?? SharedClient;
        _responseTimeout = responseTimeout ?? TimeSpan.FromSeconds(14);
        _ownsHttp = handler is not null;
        _http = handler is null ? SharedClient : new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(14)
        };
        if (_ownsHttp)
        {
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GerenciadorFinal", "5.13.11"));
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    internal async Task<ProviderScan> QueryAsync(IEnumerable<InstalledApp> installed,
        CancellationToken token, bool forceRefresh = false, IProgress<ProviderDiagnostic>? progress = null)
    {
        token.ThrowIfCancellationRequested();
        var apps = installed.ToArray();
        var applicable = Known.Select(recipe => (
            Recipe: recipe,
            Apps: apps.Where(app => app.EligibleForUpdates && (app.Channel is "unknown" or "stable") && recipe.Matches(app) &&
                ApplicationIdentityCatalog.MatchesOfficialSource(
                    app.Name, app.Publisher, recipe.IdentitySource ?? recipe.Id)).ToArray()))
            .Where(item => item.Apps.Length > 0).ToArray();
        if (applicable.Length == 0) return new([], []);
        var tasks = applicable.Select(async target =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var (version, cached, observedAt) = await GetVersionAsync(target.Recipe, token, forceRefresh);
                var findings = target.Apps.Select(app =>
                    new OfficialRelease(app, version, target.Recipe.Provider,
                        target.Recipe.Page, target.Recipe.Id, target.Recipe.IdentitySource ?? target.Recipe.Id,
                        observedAt, Architecture: target.Recipe.Id == "proton-pass-windows" ? "x64" : "unknown",
                        Channel: target.Recipe.JsonProperty is "tag_name" or "__PROTON_WINDOWS__" ? "stable" : "unknown",
                        InstallType: target.Recipe.Id == "proton-pass-windows" ? "msix" : "unknown",
                        EvidenceKind: target.Recipe.JsonProperty == "__PROTON_WINDOWS__" ? "windows-stable-feed" : target.Recipe.JsonProperty == "__WINGET_VERSIONS__" ? "community-index" :
                            target.Recipe.JsonProperty == "__PROTON_CHANGELOG__" ? "desktop-changelog" : "vendor-release")).ToArray();
                return (Findings: findings,
                    Diagnostic: new ProviderDiagnostic(target.Recipe.Id, "ok", clock.ElapsedMilliseconds, cached));
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
        var results = await Task.WhenAll(tasks.Select(async task =>
        {
            var result = await task;
            progress?.Report(result.Diagnostic);
            return result;
        }));
        return new ProviderScan(results.SelectMany(x => x.Findings).ToArray(),
            results.Select(x => x.Diagnostic).ToArray());
    }

    private async Task<(string Version, bool Cached, DateTimeOffset ObservedAt)> GetVersionAsync(Recipe recipe, CancellationToken token, bool force)
    {
        token.ThrowIfCancellationRequested();
        var key = (_cacheTransport, recipe.Id);
        if (!force && Cache.TryGetValue(key, out var cached) &&
            ReferenceEquals(cached.Clock, _clock) && _clock.GetElapsedTime(cached.Started) < Ttl) return (cached.Version, true, cached.ObservedAt);
        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try
        {
            if (!force && Cache.TryGetValue(key, out cached) &&
                ReferenceEquals(cached.Clock, _clock) && _clock.GetElapsedTime(cached.Started) < Ttl) return (cached.Version, true, cached.ObservedAt);
            bool github = recipe.Endpoint.StartsWith("https://api.github.com/", StringComparison.Ordinal);
            if (github) await GithubConcurrency.WaitAsync(token);
            try
            {
                var value = await FetchVersionAsync(recipe, token);
                token.ThrowIfCancellationRequested();
                Cache[key] = new CachedVersion(value, _clock, _clock.GetTimestamp(), _clock.GetUtcNow());
                return (value, false, Cache[key].ObservedAt);
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
        quickTimeout.CancelAfter((recipe.Id is "proton-pass-desktop" or "proton-pass-windows") && _responseTimeout > TimeSpan.FromSeconds(5)
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
            if (recipe.JsonProperty == "__PROTON_WINDOWS__")
                tag = ParseProtonWindowsVersion(doc.RootElement);
            else if (recipe.JsonProperty == "__EDGE_STABLE__")
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
        if (!Regex.IsMatch(tag, recipe.Id == "fancontrol" ? @"^\d{1,4}$" : @"^\d{1,4}(?:\.\d{1,6}){1,3}$"))
            throw new InvalidDataException("Versão não estável ou não comparável.");
        return tag;
    }

    // Feed do fornecedor específico Windows x64. Apenas Stable com rollout completo.
    // URL/hash são validados como metadados; nunca baixados, executados ou convertidos em receita.
    internal static string ParseProtonWindowsVersion(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Releases", out var releases) ||
            releases.ValueKind != JsonValueKind.Array || releases.GetArrayLength() > 500)
            throw new InvalidDataException("Feed Windows inválido.");
        string? latest = null;
        foreach (var release in releases.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object ||
                !release.TryGetProperty("CategoryName", out var category) || category.ValueKind != JsonValueKind.String ||
                category.GetString() != "Stable" || !release.TryGetProperty("RolloutPercentage", out var rollout) ||
                rollout.ValueKind != JsonValueKind.Number || !rollout.TryGetDouble(out double fraction) || fraction != 1 ||
                !release.TryGetProperty("Version", out var versionElement) || versionElement.ValueKind != JsonValueKind.String ||
                !release.TryGetProperty("File", out var files) || files.ValueKind != JsonValueKind.Array ||
                files.GetArrayLength() != 1) continue;
            string version = versionElement.GetString() ?? "";
            if (!Regex.IsMatch(version, @"^\d{1,4}(?:\.\d{1,6}){1,3}$")) continue;
            var file = files[0];
            if (file.ValueKind != JsonValueKind.Object ||
                !file.TryGetProperty("Url", out var url) || url.ValueKind != JsonValueKind.String ||
                url.GetString() != "https://proton.me/download/pass/windows/ProtonPass_" + version + ".msix" ||
                !file.TryGetProperty("Sha512CheckSum", out var digest) || digest.ValueKind != JsonValueKind.String ||
                digest.GetString() is not { Length: 128 } checksum || !checksum.All(Uri.IsHexDigit)) continue;
            if (latest is null || Matching.CompareVersions(latest, version) is < 0) latest = version;
        }
        return latest ?? throw new InvalidDataException("Sem versão Windows estável com rollout completo.");
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
        if ((sourceId == "github-desktop" || sourceId == "qbittorrent") &&
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
         (url.Host.Equals("proton.me", StringComparison.OrdinalIgnoreCase) &&
          url.AbsolutePath == "/download/PassDesktop/win32/x64/version.json" && url.Query.Length == 0 && url.Fragment.Length == 0) ||
         url.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    internal static void ClearCacheForTests() => Cache.Clear();
    public void Dispose() { if (_ownsHttp) _http.Dispose(); }
}

internal static class MultiSourceMatcher
{
    private static bool Conflicting(string a, string b) =>
        a != "unknown" && b != "unknown" && !a.Equals(b, StringComparison.OrdinalIgnoreCase);

    private static bool Compatible(InstalledApp app, OfficialRelease release)
    {
        string? canonical = ApplicationIdentityCatalog.Resolve(app.Name, app.Publisher)?.Id;
        return !Matching.HasConflictingArchitecture(app) &&
            (release.CanonicalId.Length == 0 || release.CanonicalId == canonical) &&
            !Conflicting(app.Architecture, release.Architecture) &&
            !Conflicting(app.Channel, release.Channel) &&
            !Conflicting(app.Scope, release.Scope) &&
            !Conflicting(app.InstallType, release.InstallType);
    }

    private static int Rank(OfficialRelease release) => release.EvidenceKind switch
    { "community-index" => 1, "desktop-changelog" => 2, "windows-stable-feed" => 4, _ => 3 };

    internal static IReadOnlyList<UpdateMatch> Merge(
        IReadOnlyList<InstalledApp> installed,
        IReadOnlyList<UpdateMatch>? ruckzuck,
        IReadOnlyList<OfficialRelease> official)
    {
        if (ruckzuck is not null && ruckzuck.Count != installed.Count)
            throw new InvalidDataException("Quantidade divergente entre instalada e resultado do catálogo.");
        // Referência da sessão preserva instalações iguais, por usuário/arquitetura/edição.
        var snapshots = new HashSet<InstalledApp>(installed, ReferenceEqualityComparer.Instance);
        var byApp = new Dictionary<InstalledApp, List<OfficialRelease>>(ReferenceEqualityComparer.Instance);
        foreach (var release in official)
        {
            if (!release.Installed.EligibleForUpdates || !snapshots.Contains(release.Installed)) continue;
            if (Matching.CompareVersions("0.0", release.LatestVersion) is null) continue;
            if (!byApp.TryGetValue(release.Installed, out var rows))
                byApp[release.Installed] = rows = [];
            rows.Add(release);
        }
        var result = new UpdateMatch[installed.Count];
        for (int i = 0; i < installed.Count; i++)
        {
            var app = installed[i];
            if (ruckzuck is not null && !ReferenceEquals(ruckzuck[i].Installed, app))
                throw new InvalidDataException("Resultado do catálogo pertence a outra fotografia de inventário.");
            if (!app.EligibleForUpdates)
            {
                result[i] = new UpdateMatch(app, null, MatchState.Unmatched);
                continue;
            }
            var original = ruckzuck is null ? new UpdateMatch(app, null, MatchState.Unmatched) : ruckzuck[i];
            var all = byApp.GetValueOrDefault(app) ?? [];
            var compatible = all.Where(r => Compatible(app, r))
                .OrderByDescending(Rank).ThenBy(r => r.SourceId, StringComparer.Ordinal)
                .ThenBy(r => r.Provider, StringComparer.Ordinal)
                .ThenBy(r => r.OfficialPage, StringComparer.Ordinal).ToArray();
            OfficialRelease? chosen = null;
            foreach (var release in compatible)
                if (chosen is null || (Rank(release) == Rank(chosen) &&
                    Matching.CompareVersions(chosen.LatestVersion, release.LatestVersion) is < 0))
                    chosen = release;
            string canonical = ApplicationIdentityCatalog.Resolve(app.Name, app.Publisher)?.Id ?? "";
            var evidence = all.Select(r => new UpdateObservation(r.CanonicalId, r.SourceId,
                r.Provider, r.LatestVersion, r.OfficialPage, r.ObservedAt,
                r.Architecture, r.Channel, r.Scope, r.InstallType,
                Compatible(app, r) ? r.EvidenceKind : "incompatible-identity-or-context")).ToList();
            if (original.Catalog is { } rz)
                evidence.Add(new(canonical, rz.ShortName, original.Provider, rz.ProductVersion,
                    "", default, EvidenceKind: "catalog-version"));
            evidence = evidence.Distinct().OrderBy(e => e.SourceId, StringComparer.Ordinal)
                .ThenBy(e => e.Provider, StringComparer.Ordinal).ThenBy(e => e.Version, StringComparer.Ordinal).ToList();
            var usableVersions = compatible.Select(r => r.LatestVersion).ToList();
            if (original.Catalog is { } catalog) usableVersions.Add(catalog.ProductVersion);
            bool conflict = usableVersions.Count > 1 && usableVersions.Skip(1)
                .Any(v => Matching.CompareVersions(usableVersions[0], v) is not 0);
            bool review = original.RequiresReview || conflict || all.Count != compatible.Length || chosen?.EvidenceKind == "desktop-changelog";
            var merged = original;
            if (chosen is not null)
            {
                int? delta = Matching.CompareVersions(app.Version, chosen.LatestVersion);
                if (delta is not null)
                {
                    // Migração de servidor exige decisão própria; changelog nunca prova instalador Windows.
                    bool majorChange = canonical == "jellyfin-server" &&
                        app.Version.Split('.')[0] != chosen.LatestVersion.Split('.')[0];
                    var state = delta >= 0 ? MatchState.EqualOrNewer :
                        majorChange ? MatchState.DifferentMajor :
                        Matching.IsSupersededRegistration(app, installed, chosen.LatestVersion)
                            ? MatchState.SupersededRegistration : MatchState.Candidate;
                    review |= majorChange;
                    merged = new UpdateMatch(app, delta >= 0 ? null :
                        new CatalogEntry(app.Name, app.Name, chosen.LatestVersion, app.Publisher),
                        state, chosen.Provider);
                }
            }
            result[i] = merged with { Observations = evidence.ToArray(), HasSourceConflict = conflict,
                RequiresReview = review };
        }
        return result;
    }
}
