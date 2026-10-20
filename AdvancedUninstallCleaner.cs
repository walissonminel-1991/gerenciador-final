using Microsoft.Win32;

namespace GerenciadorAplicativos;

// Revisão AVANÇADA somente leitura. Exclusão de dados e configurações
// permanece bloqueada, inclusive nas chamadas internas. Não altera HKLM, serviços, histórico do Windows,
// caches globais, associações compartilhadas ou outros perfis de usuário.
// Excluir no NTFS não significa sobrescrita física/forense em SSD.
internal sealed record DeepCleanItem(ProgramEntry App, IReadOnlyList<string> Folders,
    IReadOnlyList<string> RegistryKeys, long FileCount, int InaccessibleOrLinked);
internal sealed record DeepCleanResult(int Files, int Directories, int RegistryKeys,
    int Skipped, int Failed);

internal static class AdvancedUninstallCleaner
{
    private sealed record ProductPolicy(ProgramEntry App, string[] Folders, string[] RegistryKeys);
    private const int MaxEntries = 30000;
    private const int MaxDepth = 16;
    private static readonly StringComparer Compare = StringComparer.OrdinalIgnoreCase;

    private static IEnumerable<ProductPolicy> KnownPolicies()
    {
        // Cada regra vem do banco de dados embutido e validado em tempo de compilação.
        // A ausência do catálogo bloqueia qualquer limpeza profunda.
        foreach (var rule in UninstallRuleCatalog.Rules)
            yield return new(
                new ProgramEntry { Name = rule.DisplayName, Publisher = "Catálogo verificado" },
                rule.Folders.ToArray(), rule.RegistryKeys.ToArray());
    }

    internal static bool StillInstalled(ProgramEntry candidate, IReadOnlyCollection<ProgramEntry> installed)
    {
        var rule = UninstallRuleCatalog.Rules.FirstOrDefault(x =>
            x.DisplayName.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase));
        // Fail closed: nem mesmo uma limpeza manual pode contornar a falta da regra.
        return rule is null || UninstallRuleCatalog.IsInstalled(rule, installed);
    }

    private static bool AllowedPath(string path, IEnumerable<string> policyFolders)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return false;
        try
        {
            var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (!policyFolders.Any(p => Compare.Equals(Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar), normalized)))
                return false;
            // Não tocar uma pasta que seja junção ou cujo ancestral foi redirecionado.
            for (DirectoryInfo? dir = new DirectoryInfo(normalized); dir is not null; dir = dir.Parent)
            {
                if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return false; }
    }

    private static bool SafeDescendant(string path, string root)
    {
        try
        {
            string basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            string full = Path.GetFullPath(path);
            if (!full.Equals(basePath, StringComparison.OrdinalIgnoreCase) &&
                !full.StartsWith(basePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return false;
            DirectoryInfo? parent = Directory.Exists(full)
                ? new DirectoryInfo(full)
                : new FileInfo(full).Directory;
            while (parent is not null && parent.FullName.Length >= basePath.Length)
            {
                if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) return false;
                if (parent.FullName.Equals(basePath, StringComparison.OrdinalIgnoreCase)) return true;
                parent = parent.Parent;
            }
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException) { return false; }
    }

    private static bool OwnRegistryPath(string key, IEnumerable<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(@"Software\", StringComparison.OrdinalIgnoreCase))
            return false;
        return allowed.Any(x => Compare.Equals(x, key));
    }

    private static bool RegistryExists(string key)
    {
        try { using var handle = Registry.CurrentUser.OpenSubKey(key, writable: false); return handle is not null; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { return false; }
    }

    private static (long files, int skipped, bool complete) CountTree(string folder)
    {
        long files = 0;
        int skipped = 0, visited = 0;
        var pending = new Stack<(string path, int depth)>();
        pending.Push((folder, 0));
        try
        {
            while (pending.Count > 0)
            {
                var (dir, depth) = pending.Pop();
                if (depth > MaxDepth) return (files, skipped, false);
                var info = new DirectoryInfo(dir);
                if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                    return (files, skipped, false);
                foreach (FileSystemInfo child in info.EnumerateFileSystemInfos())
                {
                    if (++visited > MaxEntries) return (files, skipped, false);
                    if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        skipped++;
                        continue;
                    }
                    if ((child.Attributes & FileAttributes.Directory) != 0)
                        pending.Push((child.FullName, depth + 1));
                    else files++;
                }
            }
            return (files, skipped, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return (files, skipped, false); }
    }

    internal static IReadOnlyList<DeepCleanItem> Inspect(IReadOnlyCollection<ProgramEntry> installed)
    {
        var found = new List<DeepCleanItem>();
        foreach (var policy in KnownPolicies())
        {
            if (StillInstalled(policy.App, installed)) continue;
            var folders = new List<string>();
            var keys = new List<string>();
            long fileCount = 0;
            int skipped = 0;
            foreach (string root in policy.Folders.Distinct(Compare))
            {
                if (!Directory.Exists(root) || !AllowedPath(root, policy.Folders)) continue;
                var inventory = CountTree(root);
                if (!inventory.complete)
                {
                    skipped++;
                    continue; // diretório com acesso incerto não é sequer oferecido
                }
                fileCount += inventory.files;
                skipped += inventory.skipped;
                folders.Add(root);
            }
            foreach (string key in policy.RegistryKeys)
                if (OwnRegistryPath(key, policy.RegistryKeys) && RegistryExists(key)) keys.Add(key);
            if (folders.Count > 0 || keys.Count > 0)
                found.Add(new DeepCleanItem(policy.App, folders, keys, fileCount, skipped));
        }
        return found;
    }

    // Exclusão suspensa nesta candidata, também para chamadas diretas.
    // Antes de reativar: snapshot por identidade/conteúdo, handles estáveis,
    // exclusividade comprovada e bateria supervisionada em Windows.
    internal static DeepCleanResult DeleteApprovedTree(string root, IReadOnlyCollection<string> allowedRoots) =>
        new(0, 0, 0, 1, 0);

    internal static bool DeleteApprovedRegistryKey(string key, IReadOnlyCollection<string> allowlist) => false;

    // Uma nova pasta/chave surgida depois do dialogo precisa de nova autorizacao.
    // A lista atual limita a politica; a lista aceita limita o consentimento.
    internal static DeepCleanItem RestrictToAccepted(DeepCleanItem accepted, DeepCleanItem current) =>
        current with
        {
            Folders = current.Folders.Where(p => accepted.Folders.Contains(p, Compare)).ToArray(),
            RegistryKeys = current.RegistryKeys.Where(k => accepted.RegistryKeys.Contains(k, Compare)).ToArray()
        };

    internal static DeepCleanResult Clean(DeepCleanItem accepted,
        IReadOnlyCollection<ProgramEntry> currentInstalled) => new(0, 0, 0, 1, 0);
}
