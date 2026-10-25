using Microsoft.Win32;
using GerenciadorAplicativos;

internal static class Work9InventoryTests
{
    internal static void Run(Action<bool, string> check)
    {
        var modern = new ProgramEntry { Name = "GF Test Modern App", InventoryOnly = true,
            VerifiedMsixIdentity = true };
        var protectedMsix = new ProgramEntry { Name = "Microsoft GameInput", InventoryOnly = true,
            VerifiedMsixIdentity = true };
        check(ActionableInventory.CanManageWithWindows(modern) && !ActionableInventory.CanUninstall(modern) &&
            !ActionableInventory.CanManageWithWindows(protectedMsix) &&
            !ActionableInventory.CanManageWithWindows(new ProgramEntry { Name = "GF Test Modern App", InventoryOnly = true }),
            "W15 gerenciamento MSIX abre somente Configuracoes; nao cria desinstalador e nao libera sistema");
        string fixture = "GF-W9-Hidden-" + Guid.NewGuid().ToString("N");
        const string parent = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        try
        {
            using (var key = hive.CreateSubKey(parent + "\\" + fixture))
            {
                key.SetValue("DisplayName", "Notepad++ (64-bit x64)");
                key.SetValue("Publisher", "Don HO"); key.SetValue("DisplayVersion", "8.9.5");
                key.SetValue("SystemComponent", 1, RegistryValueKind.DWord);
                key.SetValue("InstallLocation", @"C:\GF-W9-Synthetic-Hidden");
                key.SetValue("UninstallString", "\"C:\\GF-W9-Synthetic-Hidden\\uninstall.exe\"");
            }
            var visible = AppServices.ScanInstalledPrograms();
            check(!visible.Items.Any(p => p.InventoryId.EndsWith("|" + fixture, StringComparison.Ordinal)),
                "W9-I controle positivo instalacao oculta nao altera a lista visual");
            var safety = AppServices.GetInstalledPrograms();
            var previous = new ProgramEntry { Name = "Notepad++", Publisher = "Don HO" };
            check(safety.Any(p => p.InventoryId.EndsWith("|" + fixture, StringComparison.Ordinal) && p.InventoryOnly) &&
                UninstallResiduals.StillInstalled(previous, safety),
                "W9-I registro SystemComponent oculto impede falsa ausencia para limpeza");
            var fullSafety = AppServices.ScanAllInstalledAppsForSafety();
            check(fullSafety.Items.Any(p => p.InventoryId.EndsWith("|" + fixture, StringComparison.Ordinal) &&
                    p.InventoryOnly && !ActionableInventory.CanUninstall(p) &&
                    p.UninstallString.Length > 0 && p.InstallLocation == @"C:\GF-W9-Synthetic-Hidden") &&
                UninstallResiduals.StillInstalled(previous, fullSafety.Items),
                "W9-I consulta completa de protecao conserva presenca e caminho sem conceder acao");
            using (var key = hive.OpenSubKey(parent + "\\" + fixture, true))
            { key!.SetValue("SystemComponent", 0, RegistryValueKind.DWord); key.SetValue("ReleaseType", "Update"); }
            safety = AppServices.GetInstalledPrograms();
            check(safety.Any(p => p.InventoryId.EndsWith("|" + fixture, StringComparison.Ordinal) && p.InventoryOnly),
                "W9-I cadastro marcado Update tambem protege contra falsa ausencia");
        }
        finally
        {
            using var key = hive.OpenSubKey(parent, writable: true);
            key!.DeleteSubKeyTree(fixture, throwOnMissingSubKey: false);
        }
    }
}
