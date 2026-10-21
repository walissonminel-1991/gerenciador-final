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
// Ampliação: reconhecer aplicativos genuinamente listados pelo Windows,
 // sem permitir receitas baseadas apenas no título exibido pelo pacote.
var generic = LocalMsixInventory.Describe("Contoso.Reader", "otherpublisher",
    "Leitor de documentos", "Contoso Ltda.", false, false, true, 3, 2, 1, 0);
Check(generic is { Name: "Leitor de documentos", Version: "3.2.1.0",
    RecipeEligible: false }, "MSIX genérico identificado sem necessidade de constar no catálogo");
Check(LocalMsixInventory.Describe("Contoso.Runtime", "otherpublisher",
    "Runtime Contoso", "Contoso", true, false, true, 1, 0, 0, 0) is null,
    "Dependência framework não deve poluir lista de aplicativos");
Check(LocalMsixInventory.Describe("Contoso.Lang", "otherpublisher",
    "Pacote de idioma", "Contoso", false, true, true, 1, 0, 0, 0) is null,
    "Pacote de recursos MSIX não é um aplicativo autônomo");
Check(LocalMsixInventory.Describe("Contoso.Service", "otherpublisher",
    "Serviço auxiliar", "Contoso", false, false, false, 1, 0, 0, 0) is null,
    "Pacote sem aplicativo visível não é contado como programa");
Check(LocalMsixInventory.Describe("Contoso.App", "otherpublisher",
    "ms-resource:AppName", "Contoso", false, false, true, 1, 0, 0, 0) is null,
    "Identificador de recurso não resolvido não vira aplicativo fictício");
Check(LocalMsixInventory.Describe("Microsoft.WindowsTerminal", "fakepublisher",
    "Windows Terminal", "Microsoft Corporation", false, false, true, 2, 0, 0, 0)
    is { RecipeEligible: false },
    "Nome de pacote conhecido sem editor verificado NÃO recebe permissão de receita");
Check(LocalMsixInventory.MergeWithRegistry([generic!],
    [new InstalledApp("Leitor de documentos", "3.2.1.0", "Outra Empresa")]).Count == 1,
    "Apps com nome igual e fabricantes diferentes são preservados");
Check(LocalMsixInventory.MergeWithRegistry([generic!],
    [new InstalledApp("Leitor de documentos", "3.2.1.0", "Contoso Ltda.")]).Count == 0,
    "Não exibir duplicata Win32/MSIX de mesmo programa e fabricante");
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
Check(LocalMsixInventory.FailureStatus(
    new System.Runtime.InteropServices.COMException("teste", unchecked((int)0x80070005)))
    == "COMException(0x80070005)",
    "MSIX: excecao COM relatada por codigo HRESULT");
Check(LocalMsixInventory.FailureStatus(new InvalidOperationException("teste"))
    == "InvalidOperationException",
    "MSIX: excecao generica relatada por tipo");
// Regressão real do PC fraco: REGDB_E_CLASSNOTREG eliminou 19 entradas
// MSIX da leitura. Retry é limitado, não substitui apps por lista inventada.
int attempts = 0;
var recovered = LocalMsixInventory.ReadWithRetry(() =>
{
    attempts++;
    return attempts == 1
        ? new LocalMsixResult([], 0, "COMException(0x80040154)", Failures: 1)
        : new LocalMsixResult([generic!], 1, "ok");
});
Check(attempts == 2 && recovered.Retried && recovered.Status == "ok" &&
    recovered.Items.Count == 1 && recovered.Failures == 0,
    "MSIX: erro de ativação recuperado com uma única segunda tentativa");
attempts = 0;
var persistent = LocalMsixInventory.ReadWithRetry(() =>
{
    attempts++;
    return new LocalMsixResult([], 0, "COMException(0x80040154)", Failures: 1);
});
Check(attempts == 2 && persistent.Retried && persistent.Failures > 0 &&
    persistent.Status != "ok" && persistent.Items.Count == 0,
    "MSIX: falha persistente permanece parcial sem simular lista vazia completa");
attempts = 0;
var denied = LocalMsixInventory.ReadWithRetry(() =>
{
    attempts++;
    return new LocalMsixResult([], 0, "COMException(0x80070005)", Failures: 1);
});
Check(attempts == 1 && !denied.Retried &&
    denied.Status == "COMException(0x80070005)",
    "MSIX: erro de permissão não é repetido indiscriminadamente");
Check(MainForm.InventoryCoverageTitle(true) == "Inventário PARCIAL" &&
      MainForm.InventoryCoverageTitle(false) == "Inventário completo",
    "UI: falha em fonte do Windows não permite legenda de inventário completo");
var incomplete = new UnifiedInventoryScan([], new RegistryInventoryScan([], 0, 0, 0, 0, 0),
    persistent, 0, new GameLibraryReport([], 0, 0, false), 0);
Check(incomplete.Incomplete,
    "Fontes MSIX indisponíveis propagam inventário incompleto para investigação de resíduos");
var live = LocalMsixInventory.ReadCurrentUser();
Console.WriteLine($"MSIX_CI status={live.Status} enumerados={live.Seen} reconhecidos={live.Items.Count}");
Check(live.Status.Length > 0, "Inventário real tratado sem falhar aplicação");
Console.WriteLine($"{count}/{count} testes de inventário MSIX aprovados");
