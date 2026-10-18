using Microsoft.Win32;

namespace GerenciadorAplicativos;

// Opt-in AVANÇADO: elimina dados privados e configurações exclusivas de
// aplicativos identificados. Não altera HKLM, serviços, histórico do Windows,
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
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return new(new ProgramEntry { Name = "SumatraPDF", Publisher = "Krzysztof Kowalczyk" },
            [Path.Combine(roaming, "SumatraPDF"), Path.Combine(local, "SumatraPDF")],
            [@"Software\SumatraPDF"]);
        yield return new(new ProgramEntry { Name = "Notepad++ (64-bit x64)", Publisher = "Notepad++ Team" },
            [Path.Combine(roaming, "Notepad++"), Path.Combine(local, "Notepad++")],
            [@"Software\Notepad++"]);
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
            if (UninstallResiduals.StillInstalled(policy.App, installed)) continue;
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

    // Nunca seguir junctions/reparse ou ultrapassar a raiz. Escopo é o diretório
    // exclusivo aprovado; as versões de teste usam apenas uma raiz em TEMP.
    internal static DeepCleanResult DeleteApprovedTree(string root, IReadOnlyCollection<string> allowedRoots)
    {
        if (!AllowedPath(root, allowedRoots) || !Directory.Exists(root))
            return new(0, 0, 0, 1, 0);
        var inventory = CountTree(root);
        if (!inventory.complete)
            return new(0, 0, 0, 1 + inventory.skipped, 0);
        var directories = new List<string>();
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        try
        {
            while (pending.Count > 0)
            {
                string dir = pending.Pop();
                if (!AllowedPath(root, allowedRoots) || !SafeDescendant(dir, root))
                    return new(0, 0, 0, 1, 0);
                foreach (FileSystemInfo item in new DirectoryInfo(dir).EnumerateFileSystemInfos())
                {
                    if ((item.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((item.Attributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push(item.FullName);
                        directories.Add(item.FullName);
                    }
                    else files.Add(item.FullName);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new(0, 0, 0, 1, 0); }
        int deleted = 0, removedDirs = 0, failed = 0;
        foreach (string file in files)
        {
            try
            {
                if (!AllowedPath(root, allowedRoots) || !SafeDescendant(file, root))
                { failed++; continue; }
                var fi = new FileInfo(file);
                if ((fi.Attributes & FileAttributes.ReparsePoint) != 0) { failed++; continue; }
                fi.Delete(); deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed++; }
        }
        foreach (string dir in directories.OrderByDescending(x => x.Length).Append(root))
        {
            try
            {
                if (!AllowedPath(root, allowedRoots) || !SafeDescendant(dir, root))
                { failed++; continue; }
                var di = new DirectoryInfo(dir);
                if (di.Exists && (di.Attributes & FileAttributes.ReparsePoint) == 0 &&
                    !di.EnumerateFileSystemInfos().Any())
                { di.Delete(); removedDirs++; }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed++; }
        }
        return new(deleted, removedDirs, 0, inventory.skipped, failed);
    }

    // Somente HKCU e caminho EXATO previamente autorizado. Não remove a raiz
    // Software nem resultados de buscas por substring no Regedit.
    internal static bool DeleteApprovedRegistryKey(string key, IReadOnlyCollection<string> allowlist)
    {
        if (!OwnRegistryPath(key, allowlist)) return false;
        try
        {
            using var software = Registry.CurrentUser.OpenSubKey("Software", writable: true);
            if (software is null) return false;
            string leaf = key[@"Software\".Length..];
            if (leaf.Length == 0 || leaf.Contains('\\') || leaf.Contains('/')) return false;
            using (var existing = software.OpenSubKey(leaf))
                if (existing is null) return false;
            software.DeleteSubKeyTree(leaf, throwOnMissingSubKey: false);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException
            or IOException or ArgumentException) { return false; }
    }

    internal static DeepCleanResult Clean(DeepCleanItem accepted,
        IReadOnlyCollection<ProgramEntry> currentInstalled)
    {
        var policy = KnownPolicies().FirstOrDefault(x =>
            x.App.Name.Equals(accepted.App.Name, StringComparison.OrdinalIgnoreCase));
        if (policy is null || UninstallResiduals.StillInstalled(policy.App, currentInstalled))
            return new(0, 0, 0, 1, 0);
        // Recalcular os alvos da política, nunca confiar cegamente na lista antiga.
        var current = Inspect(currentInstalled).FirstOrDefault(x =>
            x.App.Name.Equals(accepted.App.Name, StringComparison.OrdinalIgnoreCase));
        if (current is null) return new(0, 0, 0, 0, 0);
        int files = 0, dirs = 0, reg = 0, skipped = current.InaccessibleOrLinked, failed = 0;
        foreach (string folder in current.Folders)
        {
            var result = DeleteApprovedTree(folder, policy.Folders);
            files += result.Files; dirs += result.Directories;
            skipped += result.Skipped; failed += result.Failed;
        }
        foreach (string key in current.RegistryKeys)
        {
            if (DeleteApprovedRegistryKey(key, policy.RegistryKeys)) reg++;
            else failed++;
        }
        return new(files, dirs, reg, skipped, failed);
    }
}
