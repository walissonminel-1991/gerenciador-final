using Microsoft.Win32;

namespace GerenciadorAplicativos;

// Executor ESPECIFICO para os perfis pessoais de foobar2000 no PC de teste.
// Nao remove instalacoes, bibliotecas de musica, drivers, dados compartilhados
// ou chaves Microsoft/Windows. O usuario autoriza os locais explicitamente.
internal sealed record FoobarProfilePlan(IReadOnlyList<string> Folders, int Files, long Bytes, string Blocker)
{
    internal bool Eligible => Blocker.Length == 0 && Folders.Count > 0;
}
internal sealed record FoobarProfileResult(int FoldersDeleted, int RegistryKeysDeleted,
    int Failed, string Message);

internal static class FoobarProfileCleanup
{
    private static readonly string[] Names = ["foobar2000", "foobar2000-v2"];
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
        bool inventoryComplete, IEnumerable<string> installedNames, bool processRunning)
    {
        if (!inventoryComplete) return Block("Inventário parcial: preservar.");
        if (ResidualAudit.TrackedApplicationStillPresent("foobar2000", installedNames))
            return Block("foobar2000 continua registrado; não excluir.");
        if (processRunning) return Block("foobar2000.exe ainda está em execução.");
        var folders = new List<string>(); int files = 0; long bytes = 0;
        try
        {
            foreach (string root in new[] { roaming, local })
            {
                if (!ResidualReadSafety.IsLocalAbsolute(root) || !SafeChain(root))
                    return Block("Pasta de perfil ausente, redirecionada ou não validada.");
                foreach (string name in Names)
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

    internal static async Task<FoobarProfilePlan> PreviewAsync(CancellationToken token)
    {
        var one = await Task.Run(AppServices.ScanAllInstalledApps, token);
        token.ThrowIfCancellationRequested();
        await Task.Delay(500, token);
        var two = await Task.Run(AppServices.ScanAllInstalledApps, token);
        token.ThrowIfCancellationRequested();
        bool running = Running();
        var a = Inspect(!one.Incomplete, one.Items.Select(x => x.Name), running);
        var b = Inspect(!two.Incomplete, two.Items.Select(x => x.Name), running);
        return a.Eligible && b.Eligible && Same(a, b)
            ? b : Block("Duas consultas completas não confirmaram os mesmos perfis.");
    }

    internal static async Task<FoobarProfileResult> DeleteConfirmedAsync(
        FoobarProfilePlan authorized, CancellationToken token)
    {
        if (!authorized.Eligible) return new(0, 0, 0, "Prévia não autorizada.");
        var current = await PreviewAsync(token);
        if (!current.Eligible || !Same(current, authorized))
            return new(0, 0, 0, "Perfil mudou desde a confirmação. Nenhum item foi excluído.");
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var allowed = new[] { roaming, local }.SelectMany(root =>
            Names.Select(name => Path.Combine(root, name))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!current.Folders.All(allowed.Contains) || Running())
            return new(0, 0, 0, "Escopo mudou ou o processo está em execução.");
        int deleted = 0, failed = 0;
        foreach (string folder in current.Folders)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (!SafeChain(folder)) { failed++; continue; }
                // Renomeacao atomica no proprio volume, sempre com nome novo.
                string staged = folder + ".gf-limpeza-" + Guid.NewGuid().ToString("N");
                Directory.Move(folder, staged);
                if (ContainsLink(staged))
                {
                    Directory.Move(staged, folder);
                    failed++; continue;
                }
                Directory.Delete(staged, recursive: true);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or System.Security.SecurityException)
            { failed++; }
        }

        int reg = 0;
        // Somente HKCU\Software\foobar2000, jamais chaves Microsoft,
        // associação de arquivos, UFH, serviços ou outras contas.
        if (failed == 0 && !token.IsCancellationRequested && !Running())
        {
            var last = await Task.Run(AppServices.ScanAllInstalledApps, token);
            if (!last.Incomplete && !ResidualAudit.TrackedApplicationStillPresent(
                "foobar2000", last.Items.Select(x => x.Name)))
                try
                {
                    using var software = Registry.CurrentUser.OpenSubKey("SOFTWARE", writable: true);
                    using (var existing = software?.OpenSubKey("foobar2000", writable: false))
                    {
                        if (existing is not null) reg = 1;
                    }
                    if (reg == 1) software!.DeleteSubKeyTree("foobar2000", throwOnMissingSubKey: false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                    System.Security.SecurityException or ArgumentException) { failed++; reg = 0; }
        }
        return new(deleted, reg, failed,
            $"Perfis apagados: {deleted}; chave própria HKCU removida: {reg}; falhas: {failed}. " +
            "Entradas compartilhadas do Windows não foram removidas.");
    }

    internal static async Task<string> ConfirmAndCleanAsync(Form owner, CancellationToken token)
    {
        var plan = await PreviewAsync(token);
        if (!plan.Eligible) return "Limpeza não iniciada: " + plan.Blocker;
        string targetText = string.Join("\n", plan.Folders.Select(path => "• " + path));
        if (DarkConfirmDialog.Confirm(owner,
            "Excluir permanentemente os perfis do foobar2000 listados abaixo?\n\n" +
            targetText + "\n\n" + plan.Files + " arquivo(s), até " +
            (plan.Bytes / 1024.0 / 1024.0).ToString("0.0") + " MiB. " +
            "Também removerá exclusivamente HKCU\\Software\\foobar2000, caso exista. " +
            "Playlists e preferências nesses perfis serão perdidas; não há desfazer. " +
            "Chaves compartilhadas do Windows serão preservadas.",
            "Confirmar limpeza dos perfis foobar2000") != DialogResult.Yes)
            return "Limpeza cancelada pelo usuário; nenhum perfil apagado.";
        var result = await DeleteConfirmedAsync(plan, token);
        return result.Message;
    }

    private static bool Same(FoobarProfilePlan a, FoobarProfilePlan b) =>
        a.Files == b.Files && a.Bytes == b.Bytes &&
        a.Folders.SequenceEqual(b.Folders, StringComparer.OrdinalIgnoreCase);
    private static bool Running()
    {
        var processes = System.Diagnostics.Process.GetProcessesByName("foobar2000");
        try { return processes.Length > 0; }
        finally { foreach (var p in processes) p.Dispose(); }
    }
    private static bool SafeChain(string folder)
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
    private static bool ContainsLink(string root)
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
    private static FoobarProfilePlan Block(string reason) => new([], 0, 0, reason);
}
