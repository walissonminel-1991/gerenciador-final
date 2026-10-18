using Microsoft.Win32;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Uma biblioteca de jogos não é entrada ARP/MSI. Reconhecer, mas nunca tentar
// desinstalar jogos via comandos reconstruídos ou apagar pastas de instalação.
internal sealed record GameLibraryEntry(string Platform, string GameId,
    string DisplayName, string Library);
internal sealed record GameLibraryReport(int Steam, int Epic,
    int ManifestsChecked, int Unreadable, bool Partial,
    IReadOnlyList<GameLibraryEntry> Examples)
{
    // Nomes verificados em manifestos locais: protege jogos ativos na auditoria
    // de residuos. Os exemplos visuais permanecem limitados a 12 entradas.
    internal IReadOnlyCollection<string> ActiveNames { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<GameLibraryEntry> AllGames { get; init; } = Array.Empty<GameLibraryEntry>();
}

internal static class GameLibraryAudit
{
    private const int ManifestLimit = 1400;
    private const int MaxManifestBytes = 256 * 1024;
    private static readonly StringComparer Comparison = StringComparer.OrdinalIgnoreCase;

    internal static string? ParseSteamName(string text)
    {
        if (text.Length > MaxManifestBytes) return null;
        var match = Regex.Match(text, "^\\s*\"name\"\\s+\"(?<name>[^\"]{2,140})\"",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["name"].Value.Trim() : null;
    }

    internal static IReadOnlyList<string> ParseSteamLibraryFolders(string text)
    {
        if (text.Length > MaxManifestBytes) return [];
        return Regex.Matches(text, "^\\s*\"path\"\\s+\"(?<path>[^\"]{3,500})\"",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(m => m.Groups["path"].Value.Replace(@"\\", @"\").Trim())
            .Where(p => p.Length is >= 3 and <= 500 && Path.IsPathFullyQualified(p))
            .Distinct(Comparison).Take(24).ToArray();
    }

    internal static string? ReadSteamRoot()
    {
        try
        {
            using RegistryKey? steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", false);
            string? location = Convert.ToString(steam?.GetValue("SteamPath"));
            if (!string.IsNullOrWhiteSpace(location) && Path.IsPathFullyQualified(location))
                return location;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException) { }
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string fallback = Path.Combine(pf, "Steam");
        return Directory.Exists(fallback) ? fallback : null;
    }

    private static bool SafeFile(string path)
    {
        try
        {
            var item = new FileInfo(path);
            if (!item.Exists || item.Length > MaxManifestBytes ||
                (item.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            var dir = item.Directory;
            // Do not follow a symlink in the Steam library's manifest folder.
            return dir is not null && (dir.Attributes & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            NotSupportedException or System.Security.SecurityException or ArgumentException) { return false; }
    }

    internal static bool TryReadEpicManifest(string json,
        out string name, out string appId)
    {
        name = ""; appId = "";
        if (json.Length > MaxManifestBytes) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json,
                new JsonDocumentOptions { MaxDepth = 16 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!root.TryGetProperty("DisplayName", out var display) ||
                display.ValueKind != JsonValueKind.String) return false;
            if (!root.TryGetProperty("AppName", out var id) ||
                id.ValueKind != JsonValueKind.String) return false;
            name = display.GetString()?.Trim() ?? "";
            appId = id.GetString()?.Trim() ?? "";
            return name.Length is >= 2 and <= 150 && appId.Length is >= 2 and <= 150;
        }
        catch (JsonException) { return false; }
    }

    // A mesma instalacao pode aparecer no Registro/MSIX e no manifesto da loja.
    // Mesclar apenas nomes canonicos iguais, sem inventar versoes, comandos
    // de desinstalacao ou presumir que jogos de lojas diferentes sao iguais.
    internal static IReadOnlyList<GameLibraryEntry> UnregisteredGames(
        GameLibraryReport library, IReadOnlyCollection<ProgramEntry> alreadyRegistered)
    {
        static string Key(string name) => Regex.Replace(name.Trim().ToUpperInvariant(),
            @"[^\p{L}\p{Nd}]+", " ").Trim();
        var registeredNames = alreadyRegistered.Select(x => Key(x.Name))
            .Where(x => x.Length >= 2).ToHashSet(Comparison);
        return library.AllGames
            .Where(game => game.DisplayName.Length is >= 2 and <= 150 &&
                !registeredNames.Contains(Key(game.DisplayName)))
            .DistinctBy(game => game.Platform + "|" + game.GameId, Comparison)
            .ToArray();
    }

    internal static GameLibraryReport Inspect()
    {
        int steam = 0, epic = 0, seen = 0, unreadable = 0;
        bool partial = false;
        var examples = new List<GameLibraryEntry>();
        var unique = new HashSet<string>(Comparison);
        var activeNames = new HashSet<string>(Comparison);
        var allGames = new List<GameLibraryEntry>();
        string? main = ReadSteamRoot();
        if (!string.IsNullOrWhiteSpace(main))
        {
            var libraryPaths = new List<string> { main };
            string libraryConfig = Path.Combine(main, "steamapps", "libraryfolders.vdf");
            try
            {
                if (SafeFile(libraryConfig))
                    libraryPaths.AddRange(ParseSteamLibraryFolders(File.ReadAllText(libraryConfig)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { unreadable++; partial = true; }
            foreach (string library in libraryPaths.Distinct(Comparison).Take(25))
            {
                if (seen >= ManifestLimit) { partial = true; break; }
                string path = Path.Combine(library, "steamapps");
                try
                {
                    if (!Directory.Exists(path) ||
                        (new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;
                    foreach (string filename in Directory.EnumerateFiles(path,
                        "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
                    {
                        if (++seen > ManifestLimit) { partial = true; break; }
                        if (!SafeFile(filename)) { unreadable++; partial = true; continue; }
                        string? name = ParseSteamName(File.ReadAllText(filename));
                        if (name is null) { unreadable++; partial = true; continue; }
                        string id = Path.GetFileNameWithoutExtension(filename)["appmanifest_".Length..];
                        if (!Regex.IsMatch(id, @"^\d{1,12}$") || !unique.Add("steam:" + id)) continue;
                        steam++;
                        activeNames.Add(name);
                        var game = new GameLibraryEntry("Steam", id, name, library);
                        allGames.Add(game);
                        if (examples.Count < 12) examples.Add(game);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                { unreadable++; partial = true; }
            }
        }

        string epicRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        try
        {
            if (Directory.Exists(epicRoot) &&
                (new DirectoryInfo(epicRoot).Attributes & FileAttributes.ReparsePoint) == 0)
            {
                foreach (string filename in Directory.EnumerateFiles(epicRoot,
                    "*.item", SearchOption.TopDirectoryOnly))
                {
                    if (++seen > ManifestLimit) { partial = true; break; }
                    if (!SafeFile(filename)) { unreadable++; partial = true; continue; }
                    if (!TryReadEpicManifest(File.ReadAllText(filename), out string name, out string id))
                    { unreadable++; partial = true; continue; }
                    if (!unique.Add("epic:" + id)) continue;
                    epic++;
                    activeNames.Add(name);
                    var game = new GameLibraryEntry("Epic Games", id, name, epicRoot);
                    allGames.Add(game);
                    if (examples.Count < 12) examples.Add(game);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { unreadable++; partial = true; }
        return new(steam, epic, Math.Min(seen, ManifestLimit),
            unreadable, partial, examples)
        {
            ActiveNames = activeNames.ToArray(),
            AllGames = allGames.ToArray()
        };
    }
}
