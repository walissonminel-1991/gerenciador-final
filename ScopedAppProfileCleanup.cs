using Microsoft.Win32;
using System.Diagnostics;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Limpador expressamente autorizado por produto, nunca por correspondencia de palavras.
// So perfis pessoais AppData e uma chave propria conhecida. Nao toca HKLM,
// Windows, Program Files, AppCompat, UFH, drivers ou chaves de outro aplicativo.
internal static class ScopedAppProfileCleanup
{
    internal sealed record Scope(string Product, string[] Profiles, string[] Processes, string RegistrySubKey);
    internal static Scope? Recognize(string? product)
    {
        if (string.IsNullOrWhiteSpace(product)) return null;
        if (ResidualAudit.TrackedApplicationStillPresent("foobar2000", [product]))
            return new("foobar2000", ["foobar2000", "foobar2000-v2"], ["foobar2000"], "foobar2000");
        if (ResidualAudit.TrackedApplicationStillPresent("OBS Studio", [product]))
            return new("OBS Studio", ["obs-studio"], ["obs64", "obs32"], "OBS Studio");
        if (ResidualAudit.TrackedApplicationStillPresent("VLC media player", [product]))
            return new("VLC media player", ["vlc"], ["vlc"], @"VideoLAN\VLC");
        return null;
    }

    private static bool IsRunning(Scope scope)
    {
        foreach (string processName in scope.Processes)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            try { if (processes.Length > 0) return true; }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        return false;
    }

    private static FoobarProfilePlan Read(Scope scope, UnifiedInventoryScan snapshot)
    {
        return FoobarProfileCleanup.InspectRoots(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            !snapshot.Incomplete, snapshot.Items.Select(x => x.Name), IsRunning(scope),
            scope.Product, scope.Profiles);
    }
    private static async Task<FoobarProfilePlan> PreviewAsync(Scope scope, CancellationToken token)
    {
        var one = await Task.Run(AppServices.ScanAllInstalledApps, token);
        token.ThrowIfCancellationRequested();
        await Task.Delay(500, token);
        var two = await Task.Run(AppServices.ScanAllInstalledApps, token);
        token.ThrowIfCancellationRequested();
        var a = Read(scope, one);
        var b = Read(scope, two);
        return a.Eligible && b.Eligible && FoobarProfileCleanup.Same(a, b)
            ? b : FoobarProfileCleanup.Block("Inventário ou perfis não confirmados; nada foi apagado.");
    }

    internal static async Task<string> ConfirmAndCleanAsync(Form owner, string product, CancellationToken token)
    {
        Scope? scope = Recognize(product);
        if (scope is null) return "Não há uma política segura para limpar este aplicativo.";
        if (scope.Product == "foobar2000")
            return await FoobarProfileCleanupFlow.ConfirmAndCleanAsync(owner, token);

        FoobarProfilePlan plan = await PreviewAsync(scope, token);
        if (!plan.Eligible) return "Limpeza não iniciada: " + plan.Blocker;
        string[] authorized = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        }.SelectMany(root => scope.Profiles.Select(name => Path.Combine(root, name))).ToArray();
        if (!plan.Folders.All(path => authorized.Contains(path, StringComparer.OrdinalIgnoreCase)))
            return "Pasta não autorizada; nada foi apagado.";

        string targets = string.Join("\n", plan.Folders.Select(path => "• " + path));
        if (DarkConfirmDialog.Confirm(owner,
            "Deseja apagar os dados próprios de " + scope.Product + " nestas pastas?\n\n" +
            targets + "\n\n" + plan.Files + " arquivo(s), " +
            (plan.Bytes / 1048576d).ToString("N1") + " MiB. Também poderá remover apenas a chave " +
            @"HKCU\Software\" + scope.RegistrySubKey + ", se existir. " +
            "Preferências e configurações nesses perfis serão perdidas definitivamente. " +
            "Arquivos de instalação, documentos externos e referências compartilhadas serão preservados.",
            "Confirmar limpeza de dados do aplicativo") != DialogResult.Yes)
            return "Limpeza cancelada; nenhum arquivo apagado.";

        var fresh = await PreviewAsync(scope, token);
        if (!fresh.Eligible || !FoobarProfileCleanup.Same(plan, fresh) || IsRunning(scope))
            return "O estado mudou depois da autorização. Nenhum item foi apagado.";

        int deleted = 0, failed = 0, keys = 0;
        foreach (string folder in fresh.Folders)
        {
            token.ThrowIfCancellationRequested();
            if (!authorized.Contains(folder, StringComparer.OrdinalIgnoreCase) ||
                !FoobarProfileCleanup.DeleteCheckedFolder(folder)) failed++;
            else deleted++;
        }

        // Nunca remove chave de Registro se limpeza de pastas encontrou falha.
        if (failed == 0 && !token.IsCancellationRequested && !IsRunning(scope))
        {
            var final = await Task.Run(AppServices.ScanAllInstalledApps, token);
            if (!final.Incomplete &&
                !ResidualAudit.TrackedApplicationStillPresent(scope.Product, final.Items.Select(x => x.Name)))
            {
                try
                {
                    using var software = Registry.CurrentUser.OpenSubKey("SOFTWARE", writable:true);
                    using (var existing = software?.OpenSubKey(scope.RegistrySubKey, writable:false))
                        if (existing is not null) keys = 1;
                    if (keys == 1)
                        software!.DeleteSubKeyTree(scope.RegistrySubKey, throwOnMissingSubKey:false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                    System.Security.SecurityException or ArgumentException) { failed++; keys = 0; }
            }
        }
        return $"Perfis próprios apagados: {deleted}; chaves próprias removidas: {keys}; falhas: {failed}. " +
            "Nenhuma referência compartilhada do Windows foi apagada. Refaça a análise para conferir.";
    }
}
