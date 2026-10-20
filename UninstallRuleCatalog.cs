using System.Text.Json;

namespace GerenciadorAplicativos;

// Banco de regras EMBUTIDO e somente leitura. Novas entradas exigem teste/commit;
// jamais executar listas, scripts ou expressões vindas da Internet.
internal sealed record UninstallCatalogRule(
    string Id, string DisplayName, IReadOnlyList<string> InstalledNames,
    IReadOnlyList<string> Folders, IReadOnlyList<string> RegistryKeys);

internal static class UninstallRuleCatalog
{
    private sealed class CatalogData
    {
        public int Schema { get; set; }
        public List<RawRule>? Rules { get; set; }
    }
    private sealed class RawRule
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public List<string>? InstalledNames { get; set; }
        public List<string>? Folders { get; set; }
        public List<string>? RegistryKeys { get; set; }
    }

    private static readonly Lazy<IReadOnlyList<UninstallCatalogRule>> Saved =
        new(LoadInternal, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static IReadOnlyList<UninstallCatalogRule> Rules => Saved.Value;
    internal static int Count => Rules.Count;

    private static IReadOnlyList<UninstallCatalogRule> LoadInternal()
    {
        try
        {
            using Stream? embedded = typeof(UninstallRuleCatalog).Assembly
                .GetManifestResourceStream("GerenciadorFinal.UninstallCatalog");
            if (embedded is null || embedded.Length is <= 0 or > 65536) return [];
            using var reader = new StreamReader(embedded);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException
            or UnauthorizedAccessException) { return []; }
    }

    internal static IReadOnlyList<UninstallCatalogRule> Parse(string json)
    {
        var root = JsonSerializer.Deserialize<CatalogData>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (root?.Schema != 1 || root.Rules is null || root.Rules.Count > 100)
            throw new InvalidDataException("Catalogo de desinstalacao com formato inesperado.");
        var policies = new List<UninstallCatalogRule>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in root.Rules)
        {
            if (string.IsNullOrWhiteSpace(item.Id) ||
                string.IsNullOrWhiteSpace(item.DisplayName) ||
                !ids.Add(item.Id) || item.InstalledNames is null || item.InstalledNames.Count == 0 ||
                item.Folders is null || item.RegistryKeys is null ||
                item.Folders.Count + item.RegistryKeys.Count == 0)
                throw new InvalidDataException("Regra de aplicativo incompleta ou duplicada.");
            foreach (string alias in item.InstalledNames)
            {
                if (string.IsNullOrWhiteSpace(alias) || alias.Length > 100 ||
                    alias.Count(c => c == '*') > 1 ||
                    (alias.Contains('*') && !alias.EndsWith('*')))
                    throw new InvalidDataException("Alias inseguro na lista de desinstalacao.");
            }
            var folders = item.Folders.Select(ResolveFolder).ToArray();
            var keys = item.RegistryKeys.Select(ValidateKey).ToArray();
            if (folders.Distinct(StringComparer.OrdinalIgnoreCase).Count() != folders.Length ||
                keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length)
                throw new InvalidDataException("Pastas ou chaves repetidas.");
            policies.Add(new(item.Id, item.DisplayName, item.InstalledNames, folders, keys));
        }
        // Uma pasta/chave exclusiva nunca poderá pertencer a duas receitas.
        if (policies.SelectMany(x => x.Folders).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            policies.Sum(x => x.Folders.Count))
            throw new InvalidDataException("Pasta compartilhada entre receitas.");
        if (policies.SelectMany(x => x.RegistryKeys).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            policies.Sum(x => x.RegistryKeys.Count))
            throw new InvalidDataException("Chave compartilhada entre receitas.");
        return policies;
    }

    private static string ResolveFolder(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 125) throw new InvalidDataException("Pasta invalida.");
        string[] segments = value.Split('\\');
        // Por enquanto, somente UMA subpasta exclusiva em AppData Local/Roaming.
        // Nunca AppData inteiro, Program Files, Windows, ProgramData nem raiz do usuario.
        if (segments.Length != 2 || segments[1] is "." or ".." ||
            new[] { "Microsoft", "Windows", "Google", "Mozilla", "Programs", "Packages",
                    "Common", "Temp", "Cache", "Start Menu", "System", "Shared" }
                .Contains(segments[1], StringComparer.OrdinalIgnoreCase) ||
            segments[1].Length < 2 ||
            segments[1].Any(c => !(char.IsAsciiLetterOrDigit(c) || c is ' ' or '_' or '-' or '+' or '.')))
            throw new InvalidDataException("Diretorio fora dos locais seguros.");
        var knownFolder = segments[0] switch
        {
            "Roaming" => Environment.SpecialFolder.ApplicationData,
            "Local" => Environment.SpecialFolder.LocalApplicationData,
            _ => throw new InvalidDataException("Pasta base nao autorizada.")
        };
        return Path.Combine(Environment.GetFolderPath(knownFolder), segments[1]);
    }

    private static string ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidDataException("Chave vazia.");
        var parts = key.Split('\\');
        // HKCU\Software\Produto diretamente. Nao subchaves compartilhadas ou hive completo.
        if (parts.Length != 2 || parts[0] != "Software" ||
            new[] { "Microsoft", "Windows", "Policies", "Classes", "Mozilla", "Google",
                    "WOW6432Node", "Common", "Shared" }
                .Contains(parts[1], StringComparer.OrdinalIgnoreCase) ||
            parts[1].Length < 2 || parts[1] is "." or ".." ||
            parts[1].Any(c => !(char.IsAsciiLetterOrDigit(c) || c is ' ' or '_' or '-' or '+' or '.')))
            throw new InvalidDataException("Registro fora da allowlist do usuario.");
        return key;
    }

    internal static bool IsInstalled(UninstallCatalogRule rule, IReadOnlyCollection<ProgramEntry> inventory)
    {
        var identity = ApplicationIdentityCatalog.Rules.FirstOrDefault(i =>
            rule.Id.Equals(i.CleanupPolicy, StringComparison.OrdinalIgnoreCase));
        // A identificação normalizada é a mesma usada na verificação de atualizações.
        // Falha no catálogo não autoriza exclusão: nomes conhecidos permanecem verificados.
        return inventory.Any(program =>
            rule.InstalledNames.Any(name => NameMatches(program.Name, name)) ||
            (identity is not null && ApplicationIdentityCatalog.Resolve(
                program.Name, program.Publisher)?.Id == identity.Id));
    }

    internal static bool NameMatches(string installedName, string alias)
    {
        if (string.IsNullOrWhiteSpace(installedName) || string.IsNullOrWhiteSpace(alias)) return false;
        if (!alias.EndsWith('*'))
            return installedName.Equals(alias, StringComparison.OrdinalIgnoreCase);
        string prefix = alias[..^1];
        if (prefix.Length < 3 || !installedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        return installedName.Length == prefix.Length ||
            installedName[prefix.Length] is ' ' or '(' or '-';
    }
}
