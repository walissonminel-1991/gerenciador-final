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
// Falha real de fonte: conservar a última leitura completa só nesta sessão.
var sessionSnapshot = new LocalMsixSnapshot();
var completeSnapshot = sessionSnapshot.Read(() => new([terminal!, outlook!], 2, "ok"));
var failedSnapshot = sessionSnapshot.Read(() => new([], 0, "COMException(0x80040154)", Failures: 1));
Check(failedSnapshot.Items.Count == 2 && failedSnapshot.Items.All(x => x.Stale && !x.RecipeEligible) &&
    failedSnapshot.Failures == 1 && failedSnapshot.Status != "ok",
    "MSIX: falha de ativação preserva snapshot da sessão sem autorizar receitas");
var observedNew = terminal! with { Version = "1.25.0.0" };
var mixedSnapshot = sessionSnapshot.Read(() => new([observedNew], 1, "ok", Failures: 1));
Check(mixedSnapshot.Items.Count == 2 && mixedSnapshot.Items.Single(x => !x.Stale).Version == "1.25.0.0" &&
    mixedSnapshot.Items.All(x => !x.RecipeEligible),
    "MSIX: observação parcial recente prevalece sem habilitar atualização ou perder apps anteriores");
var failedAgain = sessionSnapshot.Read(() => new([], 0, "indisponível", Failures: 1));
Check(failedAgain.Items.Single(x => x.PackageName == terminal!.PackageName).Version == terminal!.Version,
    "MSIX: leitura parcial não substitui a última base completa");
var confirmedAfter = sessionSnapshot.Read(() => new([terminal!], 1, "ok"));
Check(confirmedAfter.Items.Count == 1 && !confirmedAfter.Items[0].Stale && confirmedAfter.Items[0].RecipeEligible,
    "MSIX: nova leitura completa confirma presença e deixa de reter item ausente");
Check(new LocalMsixSnapshot().Read(() => new([], 0, "indisponível", Failures: 1)).Items.Count == 0,
    "MSIX: processo sem leitura completa não inventa histórico de outra sessão");
Check(sessionSnapshot.Read(() => new([], 0, "ok")).Items.Count == 0 &&
    sessionSnapshot.Read(() => new([], 0, "falhou", Failures: 1)).Items.Count == 0,
    "MSIX: resultado vazio completo invalida a base anterior");
Check(LocalMsixInventory.ConfirmedMicrosoftVersion(completeSnapshot, terminal!.PackageName)?.ToString() == terminal.Version,
    "Confirmação MSIX: identidade verificada atual em fonte completa é utilizável");
Check(LocalMsixInventory.ConfirmedMicrosoftVersion(mixedSnapshot, terminal!.PackageName) is null,
    "Confirmação MSIX: status ok com falha parcial não confirma instalação nem versão final");
Check(LocalMsixInventory.ConfirmedMicrosoftVersion(new([terminal! with { Stale = true }], 1, "ok"), terminal!.PackageName) is null,
    "Confirmação MSIX: entrada retida nunca comprova versão atual mesmo com envelope indevido");
Check(LocalMsixInventory.ConfirmedMicrosoftVersion(new([terminal! with { RecipeEligible = false }], 1, "ok"), terminal!.PackageName) is null,
    "Confirmação MSIX: nome/editor de exibição sem publisherId auditado não autoriza atualização");
Check(LocalMsixInventory.ConfirmedMicrosoftVersion(failedSnapshot, terminal!.PackageName) is null &&
    LocalMsixInventory.ConfirmedMicrosoftVersion(completeSnapshot, "Contoso.Reader") is null,
    "Confirmação MSIX: falha e pacote fora do contrato não usam a base retida");
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
attempts = 0;
var retryRegistry = LocalMsixInventory.ReadWithRetry(() =>
{
    attempts++;
    return attempts == 1
        ? new LocalMsixResult([], 0, "COMException(0x800703FA)", Failures: 1)
        : new LocalMsixResult([generic!], 1, "ok");
});
Check(attempts == 2 && retryRegistry.Retried && retryRegistry.Failures == 0 &&
    retryRegistry.Items.Count == 1,
    "MSIX: 0x800703FA recuperado em apenas uma repeticao");
attempts = 0;
var repeatedRegistry = LocalMsixInventory.ReadWithRetry(() =>
{
    attempts++;
    return new LocalMsixResult([], 0, "COMException(0x800703FA)", Failures: 1);
});
Check(attempts == 2 && repeatedRegistry.Retried && repeatedRegistry.Failures > 0 &&
    repeatedRegistry.Items.Count == 0 && repeatedRegistry.Status != "ok",
    "MSIX: 0x800703FA persistente continua parcial, sem criar pacotes");
attempts = 0;
var partialRegistry = LocalMsixInventory.ReadWithRetry(() =>
{
    attempts++;
    return new LocalMsixResult([generic!], 1, "COMException(0x800703FA)",
        Failures: 1, FailureStage: "enumeration");
});
Check(attempts == 1 && !partialRegistry.Retried && partialRegistry.Items.Count == 1,
    "MSIX: enumeração com itens parciais jamais repetida e descartada");
Check(LocalMsixInventory.FailureStatus(new TypeInitializationException("WinRT.Factory",
    new System.Runtime.InteropServices.COMException("mensagem privada", unchecked((int)0x80040154))))
    == "COMException(0x80040154)",
    "MSIX: HRESULT de ativação preservado através do wrapper sem mensagem privada");
IEnumerable<int> Interrupted()
{
    yield return 1;
    throw new System.Runtime.InteropServices.COMException("mensagem privada", unchecked((int)0x800706BE));
}
var interrupted = LocalMsixInventory.ReadPackages(Interrupted(),
    _ => new LocalMsixObservation(generic!, Launchable: true));
Check(interrupted.Items.Count == 1 && interrupted.Seen == 1 && interrupted.Launchable == 1 &&
    interrupted.Failures == 1 && interrupted.Status == "COMException(0x800706BE)" &&
    interrupted.FailureStage == "enumeration" && interrupted.Apartment.Length > 0,
    "MSIX: interrupção de MoveNext preserva itens já lidos e informa cobertura parcial");
attempts = 0;
var interruptedActivation = LocalMsixInventory.ReadWithRetry(() =>
{
    attempts++;
    return attempts == 1 ? interrupted with { Status = "COMException(0x80040154)" }
        : new LocalMsixResult([], 0, "COMException(0x80040154)", Failures: 1);
});
Check(attempts == 1 && !interruptedActivation.Retried && interruptedActivation.Items.Count == 1 &&
    interruptedActivation.Failures > 0 && interruptedActivation.FailureStage == "enumeration",
    "MSIX: retry não substitui evidência parcial por outra leitura indisponível");
var packageFailure = LocalMsixInventory.ReadPackages(new[] { 1, 2, 3 }, value =>
{
    if (value == 2) throw new InvalidOperationException("pacote privado");
    return new LocalMsixObservation(value == 1 ? generic : generic! with { Version = "4.0.0.0" }, true);
});
Check(packageFailure.Items.Count == 1 && packageFailure.Items[0].Version == "4.0.0.0" &&
    packageFailure.Seen == 3 && packageFailure.Failures == 1 && packageFailure.Launchable == 2 &&
    packageFailure.FailureStage == "package",
    "MSIX: falha em um pacote não perde outros apps nem transforma leitura em completa");
var limited = LocalMsixInventory.ReadPackages(Enumerable.Range(1, 2402),
    _ => new LocalMsixObservation(null));
Check(limited.Seen == 2401 && limited.Filtered == 2400 && limited.Failures == 1 &&
    limited.FailureStage == "limit", "MSIX: limite de pacotes permanece explícito e não finge cobertura completa");
var complete = LocalMsixInventory.ReadPackages(new[] { 1 },
    _ => new LocalMsixObservation(generic!, true));
Check(complete.Items.Count == 1 && complete.Failures == 0 && complete.FailureStage == "" &&
    complete.Status == "ok", "MSIX: leitura completa não recebe falsa falha de diagnóstico");
var live = LocalMsixInventory.ReadCurrentUser();
Console.WriteLine($"MSIX_CI status={live.Status} fase={live.FailureStage} apartment={live.Apartment} enumerados={live.Seen} reconhecidos={live.Items.Count}");
Check(live.Status.Length > 0, "Inventário real tratado sem falhar aplicação");
Console.WriteLine($"{count}/{count} testes de inventário MSIX aprovados");
