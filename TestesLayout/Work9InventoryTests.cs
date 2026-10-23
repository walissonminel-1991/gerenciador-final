using Microsoft.Win32;
using GerenciadorAplicativos;

internal static class Work9InventoryTests
{
    internal static void Run(Action<bool, string> check)
    {
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
            }
            var visible = AppServices.ScanInstalledPrograms();
            check(!visible.Items.Any(p => p.InventoryId.EndsWith("|" + fixture, StringComparison.Ordinal)),
                "W9-I controle positivo instalacao oculta nao altera a lista visual");
            var safety = AppServices.GetInstalledPrograms();
            check(safety.Any(p => p.InventoryId.EndsWith("|" + fixture, StringComparison.Ordinal) && p.InventoryOnly) &&
                !SmallAppResidualCleanup.AbsenceConfirmed("Notepad++", safety),
                "W9-I registro SystemComponent oculto impede falsa ausencia para limpeza");
        }
        finally
        {
            using var key = hive.OpenSubKey(parent, writable: true);
            key!.DeleteSubKeyTree(fixture, throwOnMissingSubKey: false);
        }
    }
}
