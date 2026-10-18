using Microsoft.Win32;
using System.Text.Json;

namespace GerenciadorAplicativos;

// Auditoria de aplicativos já removidos, orientada por fingerprints revisados.
// Totalmente SOMENTE LEITURA: não remove arquivos, chaves, caches ou atalhos.
internal static class HistoricalResidualAudit
{
    private sealed class Document
    {
        public int Schema { get; set; }
        public List<Raw>? Rules { get; set; }
    }
    private sealed class Raw
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<string>? InstalledNames { get; set; }
        public List<string>? Folders { get; set; }
        public List<string>? Registry { get; set; }
        public string? Warn { get; set; }
    }
    internal sealed record Rule(string Id, string Name, string[] InstalledNames,
        string[] Folders, string[] Registry, string Warn);

    private static readonly Lazy<IReadOnlyList<Rule>> RulesHolder =
        new(ReadCatalog, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static IReadOnlyList<Rule> Rules => RulesHolder.Value;

    private static IReadOnlyList<Rule> ReadCatalog()
    {
        try
        {
            using Stream? stream = typeof(HistoricalResidualAudit).Assembly
                .GetManifestResourceStream("GerenciadorFinal.HistoricalOrphanCatalog");
            if (stream is null || stream.Length is <= 0 or > 65536) return [];
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        { return []; }
    }

    internal static IReadOnlyList<Rule> Parse(string json)
    {
        var doc = JsonSerializer.Deserialize<Document>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (doc?.Schema != 1 || doc.Rules is null || doc.Rules.Count is < 1 or > 30)
            throw new InvalidDataException("Catalogo historico invalido.");
        var rules = new List<Rule>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in doc.Rules)
        {
            if (string.IsNullOrWhiteSpace(raw.Id) ||
                string.IsNullOrWhiteSpace(raw.Name) ||
                raw.InstalledNames is not { Count: > 0 and <= 8 } ||
                raw.Folders is null || raw.Registry is null ||
                !seen.Add(raw.Id) ||
                raw.InstalledNames.Any(n => string.IsNullOrWhiteSpace(n) ||
                    n.Length > 70 || n.Contains('*')) ||
                raw.Folders.Count + raw.Registry.Count == 0 ||
                raw.Folders.Count > 8 || raw.Registry.Count > 8 ||
                raw.Folders.Any(f => !AllowedFolderIdentifier(f)) ||
                raw.Registry.Any(k => !AllowedRegistryIdentifier(k)))
                throw new InvalidDataException("Regra historica insegura.");
            rules.Add(new(raw.Id, raw.Name, raw.InstalledNames.ToArray(),
                raw.Folders.ToArray(), raw.Registry.ToArray(), raw.Warn ?? ""));
        }
        return rules;
    }

    private static bool AllowedFolderIdentifier(string id)
    {
        var parts = id.Split('\\');
        return parts.Length == 2 &&
            parts[0] is "Local" or "Roaming" or "StartMenu" &&
            parts[1].Length is >= 4 and <= 60 &&
            parts[1].All(c => char.IsAsciiLetterOrDigit(c) ||
                c is ' ' or '+' or '-' or '_' or '.');
    }

    private static bool AllowedRegistryIdentifier(string id)
    {
        string[] parts = id.Split('\\');
        if (parts.Length is < 3 or > 6) return false;
        if (parts[0] is not ("HKCU" or "HKLM32") ||
            parts[1] != "Software" && parts[1] != "SOFTWARE") return false;
        // Regra nunca aponta para hives, pais genéricos ou classes compartilhadas.
        if (parts[2].Length < 4 || new[] { "Microsoft", "Windows",
             "Policies", "Classes", "WOW6432Node", "Shared", "Common" }
                .Contains(parts[2], StringComparer.OrdinalIgnoreCase)) return false;
        return parts.Skip(2).All(x => x.Length is >= 2 and <= 75 &&
            x.All(c => char.IsAsciiLetterOrDigit(c) ||
                c is ' ' or '+' or '-' or '_' or '.'));
    }

    private static string? ResolveFolder(string identifier)
    {
        if (!AllowedFolderIdentifier(identifier)) return null;
        string[] parts = identifier.Split('\\');
        var folder = parts[0] switch
        {
            "Local" => Environment.SpecialFolder.LocalApplicationData,
            "Roaming" => Environment.SpecialFolder.ApplicationData,
            "StartMenu" => Environment.SpecialFolder.Programs,
            _ => Environment.SpecialFolder.UserProfile
        };
        string basePath = Environment.GetFolderPath(folder);
        if (string.IsNullOrWhiteSpace(basePath)) return null;
        string path = Path.Combine(basePath, parts[1]);
        if (parts[0] != "StartMenu") return ResidualAudit.SafeAuditRoot(path) ? path : null;
        // Windows Start Menu is in a shared-looking Microsoft subtree; for this
        // fixed exact child, inspect only without proposing any unlink/delete.
        try
        {
            if (!Directory.Exists(path)) return null;
            for (DirectoryInfo? parent = new DirectoryInfo(path);
                parent is not null; parent = parent.Parent)
                if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                    return null;
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { return null; }
    }

    private static ResidualAuditFinding? InspectFolder(string id, string warning)
    {
        string? folder = ResolveFolder(id);
        if (folder is null) return null;
        try
        {
            int items = 0;
            foreach (FileSystemInfo file in new DirectoryInfo(folder).EnumerateFileSystemInfos())
            {
                if (++items > 1500) return null;
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0) return null;
            }
            return items == 0 ? null :
                new(folder, "Diretório histórico (somente leitura)", items,
                    "Pode conter dados ainda úteis. " + warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return null; }
    }

    private static ResidualAuditFinding? InspectRegistry(string id, string warning)
    {
        if (!AllowedRegistryIdentifier(id)) return null;
        string[] parts = id.Split('\\');
        RegistryHive hive = parts[0] == "HKLM32" ?
            RegistryHive.LocalMachine : RegistryHive.CurrentUser;
        RegistryView view = parts[0] == "HKLM32" ?
            RegistryView.Registry32 : RegistryView.Default;
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(string.Join("\\", parts.Skip(1)), writable: false);
            if (key is null) return null;
            string path = (parts[0] == "HKLM32" ? @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\" :
                @"HKEY_CURRENT_USER\Software\") + string.Join("\\", parts.Skip(2));
            return new(path, "Chave de Registro (somente leitura)",
                Math.Max(1, key.ValueCount + key.SubKeyCount),
                "Existência não significa que possa ser excluída. " + warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException) { return null; }
    }

    internal static bool IsRuleInstalled(Rule rule, IReadOnlyCollection<ProgramEntry> inventory)
    {
        return inventory.Any(app => rule.InstalledNames.Any(alias =>
            app.Name.Equals(alias, StringComparison.OrdinalIgnoreCase) ||
            app.Name.StartsWith(alias + " (", StringComparison.OrdinalIgnoreCase)));
    }

    internal static IReadOnlyList<ResidualAuditReport> Inspect(
        IReadOnlyCollection<ProgramEntry> inventory)
    {
        var result = new List<ResidualAuditReport>();
        foreach (var rule in Rules)
        {
            if (IsRuleInstalled(rule, inventory)) continue;
            var findings = rule.Folders.Select(f => InspectFolder(f, rule.Warn))
                .Concat(rule.Registry.Select(k => InspectRegistry(k, rule.Warn)))
                .Where(x => x is not null).Select(x => x!).Take(15).ToArray();
            if (findings.Length > 0)
                result.Add(new ResidualAuditReport(rule.Name, false, findings,
                    Array.Empty<string>(), Historical: true));
        }
        return result;
    }
}
