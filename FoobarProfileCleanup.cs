using Microsoft.Win32;

namespace GerenciadorAplicativos;

// Executor ESPECIFICO para os perfis pessoais de foobar2000 no PC de teste.
// Nao remove instalacoes, bibliotecas de musica, drivers, dados compartilhados
// ou chaves Microsoft/Windows. O usuario autoriza os locais explicitamente.
internal sealed record FoobarProfilePlan(IReadOnlyList<string> Folders, int Files, long Bytes, string Blocker)
{
    internal bool CanExecute => false; // Resumo nao concede autorizacao de exclusao.
    internal bool Eligible => Blocker.Length == 0 && Folders.Count > 0;
}
internal sealed record FoobarProfileResult(int FoldersDeleted, int RegistryKeysDeleted,
    int Failed, string Message);

internal static class FoobarProfileCleanup
{
    internal static readonly string[] Names = ["foobar2000", "foobar2000-v2"];
    private const int MaxFiles = 5000;
    private const long MaxBytes = 512L * 1024 * 1024;
    private const int MaxDepth = 16;
    internal static FoobarProfilePlan Inspect(bool inventoryComplete,
        IEnumerable<string> installedNames, bool processRunning)
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return InspectRoots(roaming, local, inventoryComplete, installedNames, processRunning);
    }

    // Fixture roots apenas nos testes. O fluxo real usa KnownFolders do Windows.
    internal static FoobarProfilePlan InspectRoots(string roaming, string local,
        bool inventoryComplete, IEnumerable<string> installedNames, bool processRunning,
        string product = "foobar2000", IReadOnlyList<string>? profiles = null)
    {
        profiles ??= Names;
        if (profiles.Count is < 1 or > 2 ||
            profiles.Any(p => p is not ("foobar2000" or "foobar2000-v2" or "obs-studio")))
            return Block("Perfil desconhecido: preservar.");
        if (!inventoryComplete) return Block("Inventário parcial: preservar.");
        if (ResidualAudit.TrackedApplicationStillPresent(product, installedNames))
            return Block("foobar2000 continua registrado; não excluir.");
        if (processRunning) return Block("foobar2000.exe ainda está em execução.");
        var folders = new List<string>(); int files = 0; long bytes = 0;
        try
        {
            foreach (string root in new[] { roaming, local })
            {
                if (!ResidualReadSafety.IsLocalAbsolute(root) || !SafeChain(root))
                    return Block("Pasta de perfil ausente, redirecionada ou não validada.");
                foreach (string name in profiles)
                {
                    string folder = Path.Combine(root, name);
                    if (!Directory.Exists(folder))
                    {
                        if (File.Exists(folder)) return Block("Perfil inesperado: arquivo no lugar da pasta.");
                        continue;
                    }
                    if (!SafeChain(folder)) return Block("Perfil redirecionado.");
                    var pending = new Stack<(string Path, int Depth)>();
                    pending.Push((folder, 0));
                    while (pending.Count > 0)
                    {
                        var (dir, depth) = pending.Pop();
                        if (depth > MaxDepth || !SafeChain(dir)) return Block("Pasta profunda ou redirecionada.");
                        foreach (string entry in Directory.EnumerateFileSystemEntries(dir))
                        {
                            var attr = File.GetAttributes(entry);
                            if ((attr & FileAttributes.ReparsePoint) != 0)
                                return Block("Link/junção encontrado. Preservar o conjunto.");
                            if ((attr & FileAttributes.Directory) != 0) pending.Push((entry, depth + 1));
                            else
                            {
                                if (++files > MaxFiles) return Block("Limite de 5.000 arquivos.");
                                bytes += new FileInfo(entry).Length;
                                if (bytes > MaxBytes) return Block("Limite de 512 MiB.");
                            }
                        }
                    }
                    folders.Add(folder);
                }
            }
            return new(folders.AsReadOnly(), files, bytes, "");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        { return Block("Falha de leitura do perfil; preservar."); }
    }

#if GF_PROGRESSIVE
    // Ausente do assembly de producao; aceita somente a arvore sintética do CI.
    internal static bool DeleteSyntheticFolderForTests(string folder, string fixtureRoot)
    {
        string root = Path.GetFullPath(fixtureRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (!Path.GetFileName(root).StartsWith("GF-Foobar-Fixture-", StringComparison.Ordinal) ||
            !root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFullPath(folder).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!SafeChain(folder) || ContainsLink(folder)) return false;
        string staged = folder + ".gf-limpeza-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.Move(folder, staged);
            if (ContainsLink(staged))
            {
                Directory.Move(staged, folder);
                return false;
            }
            Directory.Delete(staged, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            try
            {
                if (Directory.Exists(staged) && !Directory.Exists(folder) &&
                    !ContainsLink(staged)) Directory.Move(staged, folder);
            }
            catch (Exception) { /* A falha sera exibida e nao afeta outras pastas. */ }
            return false;
        }
    }
#endif
    internal static bool Same(FoobarProfilePlan a, FoobarProfilePlan b) =>
        a.Files == b.Files && a.Bytes == b.Bytes &&
        a.Folders.SequenceEqual(b.Folders, StringComparer.OrdinalIgnoreCase);
    internal static bool SafeChain(string folder)
    {
        if (!Directory.Exists(folder)) return false;
        try
        {
            for (DirectoryInfo? node = new(folder); node is not null; node = node.Parent)
                if ((node.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { return false; }
    }
    internal static bool ContainsLink(string root)
    {
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            string dir = pending.Pop();
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) return true;
            foreach (string path in Directory.EnumerateFileSystemEntries(dir))
            {
                var attr = File.GetAttributes(path);
                if ((attr & FileAttributes.ReparsePoint) != 0) return true;
                if ((attr & FileAttributes.Directory) != 0) pending.Push(path);
            }
        }
        return false;
    }
    internal static FoobarProfilePlan Block(string reason) => new([], 0, 0, reason);
}
