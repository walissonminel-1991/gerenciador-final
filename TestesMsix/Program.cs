using GerenciadorAplicativos;
int count = 0;
void Check(bool ok, string title)
{
    if (!ok) throw new Exception("FALHA " + title);
    count++;
    Console.WriteLine("OK " + title);
}
var terminal = LocalMsixInventory.Recognize("Microsoft.WindowsTerminal",
    "8wekyb3d8bbwe", 1, 24, 12741, 0);
Check(terminal is { Name: "Windows Terminal", Version: "1.24.12741.0" },
    "Terminal Store reconhecido por identidade exata");
var outlook = LocalMsixInventory.Recognize("Microsoft.OutlookForWindows",
    "8wekyb3d8bbwe", 1, 2025, 416, 400);
Check(outlook is { Name: "Outlook for Windows", Version: "1.2025.416.400" },
    "Outlook Store reconhecido por identidade exata");
Check(LocalMsixInventory.Recognize("Microsoft.WindowsTerminalPreview",
    "8wekyb3d8bbwe", 1, 25, 100, 0) is null,
    "Terminal Preview não é confundido com Stable");
Check(LocalMsixInventory.Recognize("Microsoft.WindowsTerminal",
    "fakepublisher", 1, 24, 12741, 0) is null,
    "Identidade de assinatura desconhecida é rejeitada");
Check(LocalMsixInventory.Recognize("Microsoft.OutlookForWindows",
    "fakepublisher", 1, 2026, 812, 100) is null,
    "Outlook falso é rejeitado");
var additions = LocalMsixInventory.MergeWithRegistry(
    [terminal!, outlook!],
    [new InstalledApp("Terminal do Windows", "1.24.12741.0", "Microsoft Corporation")]);
Check(additions.Count == 1 && additions[0].PackageName == "Microsoft.OutlookForWindows",
    "Não duplica Terminal já no registro tradicional");
Check(LocalMsixInventory.MergeWithRegistry([outlook!],
    [new InstalledApp("Outlook para Windows", "1.2025.416.400", "Microsoft Corporation")]).Count == 0,
    "Não duplica Outlook localizado no Registro");
Check(ProtectedApplications.IsProtectedName("Microsoft GameInput") &&
      ProtectedApplications.IsProtectedName(" MICROSOFT GAMEINPUT "),
    "GameInput protegido mesmo sem SystemComponent no Registro");
Check(ProtectedApplications.IsInventoryOnly("Microsoft GameInput", false) &&
      ProtectedApplications.IsInventoryOnly("Microsoft GameInput", true) &&
      ProtectedApplications.IsInventoryOnly("Outro componente", true),
    "Proteção independente da marca SystemComponent");
Check(!ProtectedApplications.IsInventoryOnly("OBS Studio", false) &&
      !ProtectedApplications.IsInventoryOnly("Microsoft GameInput Preview", false),
    "Programas convencionais não são bloqueados indevidamente");
var live = LocalMsixInventory.ReadCurrentUser();
Console.WriteLine($"MSIX_CI status={live.Status} enumerados={live.Seen} reconhecidos={live.Items.Count}");
Check(live.Status.Length > 0, "Inventário real tratado sem falhar aplicação");
Console.WriteLine($"{count}/{count} testes de inventário MSIX aprovados");
