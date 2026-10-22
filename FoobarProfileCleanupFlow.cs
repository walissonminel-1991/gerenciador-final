using Microsoft.Win32;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal static class FoobarProfileCleanupFlow
{
    internal static async Task<FoobarProfilePlan> PreviewAsync(CancellationToken token)
    {
        var one = await Task.Run(AppServices.ScanAllInstalledApps, token);
        token.ThrowIfCancellationRequested();
        await Task.Delay(500, token);
        var two = await Task.Run(AppServices.ScanAllInstalledApps, token);
        token.ThrowIfCancellationRequested();
        bool running = Running();
        var a = FoobarProfileCleanup.Inspect(!one.Incomplete, one.Items.Select(x => x.Name), running);
        var b = FoobarProfileCleanup.Inspect(!two.Incomplete, two.Items.Select(x => x.Name), running);
        return a.Eligible && b.Eligible && FoobarProfileCleanup.Same(a, b)
            ? b : Block("Duas consultas completas não confirmaram os mesmos perfis.");
    }

    internal static async Task<FoobarProfileResult> DeleteConfirmedAsync(
        FoobarProfilePlan authorized, CancellationToken token)
    {
        if (!authorized.Eligible) return new(0, 0, 0, "Prévia não autorizada.");
        var current = await PreviewAsync(token);
        if (!current.Eligible || !FoobarProfileCleanup.Same(current, authorized))
            return new(0, 0, 0, "Perfil mudou desde a confirmação. Nenhum item foi excluído.");
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var allowed = new[] { roaming, local }.SelectMany(root =>
            FoobarProfileCleanup.Names.Select(name => Path.Combine(root, name))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!current.Folders.All(allowed.Contains) || Running())
            return new(0, 0, 0, "Escopo mudou ou o processo está em execução.");
        int deleted = 0, failed = 0;
        foreach (string folder in current.Folders)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (!FoobarProfileCleanup.SafeChain(folder)) { failed++; continue; }
                // Renomeacao atomica no proprio volume, sempre com nome novo.
                string staged = folder + ".gf-limpeza-" + Guid.NewGuid().ToString("N");
                Directory.Move(folder, staged);
                if (FoobarProfileCleanup.ContainsLink(staged))
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

    private static bool Running()
    {
        var processes = System.Diagnostics.Process.GetProcessesByName("foobar2000");
        try { return processes.Length > 0; }
        finally { foreach (var p in processes) p.Dispose(); }
    }

}
