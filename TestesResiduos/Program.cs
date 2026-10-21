using GerenciadorAplicativos;

int passed = 0;
var largeDesktop = StartupWindowSizing.Fit(new System.Drawing.Rectangle(0, 0, 1920, 1400));
var regularDesktop = StartupWindowSizing.Fit(new System.Drawing.Rectangle(0, 0, 1920, 1040));
var shiftedDesktop = StartupWindowSizing.Fit(new System.Drawing.Rectangle(-1920, 40, 1920, 1080));
var smallDesktop = StartupWindowSizing.Fit(new System.Drawing.Rectangle(0, 0, 1024, 720));
void Check(bool result, string what)
{
    if (!result) throw new Exception("FALHA: " + what);
    Console.WriteLine("OK " + what);
    passed++;
}

ResidualCorrelationTests.Run(Check);
ResidualInspectionTests.Run(Check);
ResidualProgressiveTests.Run(Check);
ResidualPrecisionTests.Run(Check);
ResidualIntegrityTests.Run(Check);
await ResidualBlockingTests.Run(Check);
ResidualMissionTests.Run(Check);
if (args.Contains("--missao")) return;

bool partialRejected = false;
try { InventoryReconciler.RequireComplete(Array.Empty<ProgramEntry>(), 1); }
catch (InvalidOperationException) { partialRejected = true; }
Check(partialRejected, "Leitura parcial nao pode autorizar limpeza por falsa ausencia");
Check(ResidualAudit.IsDriverBoosterProduct(new ProgramEntry { Name = "Driver Booster 13", Publisher = "IObit" }),
    "Auditoria reconhece Driver Booster 13 como aplicativo de terceiros");
Check(ResidualAudit.IsDriverBoosterProduct(new ProgramEntry { Name = "Driver Booster 13.6" }),
    "Variantes numericas do Driver Booster sao reconhecidas");
Check(!ResidualAudit.IsDriverBoosterProduct(new ProgramEntry { Name = "Microsoft Driver Update" }) &&
      !ResidualAudit.IsDriverBoosterProduct(new ProgramEntry { Name = "Driver Booster Cleaner" }),
    "Auditoria especifica nao confunde drivers do Windows e produtos homonimos");
Check(InventoryReconciler.RequireComplete(Array.Empty<ProgramEntry>(), 0).Count == 0,
    "Inventario vazio completo nao e confundido com erro de acesso");

Check(largeDesktop.Size == new System.Drawing.Size(1344, 1148),
    "Monitor grande: janela cresce com a area util, mantendo fontes definidas pelo DPI");
Check(regularDesktop.Size == new System.Drawing.Size(1344, 852) &&
      regularDesktop.Y == 94 && regularDesktop.X == 288,
    "1920x1040: abertura centralizada e proporcional");
Check(shiftedDesktop.Left >= -1920 && shiftedDesktop.Right <= 0 &&
      shiftedDesktop.Top >= 40 && shiftedDesktop.Bottom <= 1120,
    "Centralizar em monitor deslocado");
Check(smallDesktop.Size == new System.Drawing.Size(716, 590) &&
      StartupWindowSizing.AllowedMinimum(smallDesktop).Width <= smallDesktop.Width,
    "1024x720: aplicar limite sem impedir abertura");
var compactArea = new System.Drawing.Rectangle(0, 0, 1366, 720);
var compactWindow = StartupWindowSizing.Fit(compactArea);
Check(compactWindow.Size == new System.Drawing.Size(956, 590) &&
      (long)compactWindow.Width * compactWindow.Height <=
          3L * compactArea.Width * compactArea.Height / 5L,
    "1366x768: janela proporcional dentro dos 60% da area util");
var tinyArea = new System.Drawing.Rectangle(0, 0, 800, 552);
Check(StartupWindowSizing.ApplyAreaLimit(tinyArea) &&
      StartupWindowSizing.Fit(tinyArea).Size == new System.Drawing.Size(560, 452),
    "A regra de 60% vale tambem para resolucoes menores que 1366x768");
var referenceDesktop = StartupWindowSizing.Fit(new System.Drawing.Rectangle(0, 0, 1440, 852));
Check(referenceDesktop.Size == new System.Drawing.Size(1008, 698) &&
      referenceDesktop.X == 216 && referenceDesktop.Y == 77,
    "1440x900: tamanho proporcional e centralizado");
var highResolutionArea = new System.Drawing.Rectangle(0, 0, 2560, 1392);
var highResolution = StartupWindowSizing.Fit(highResolutionArea);
Check(highResolution.Size == new System.Drawing.Size(1792, 1141) &&
      highResolution.Width > referenceDesktop.Width,
    "2560x1440: nao usar dimensoes fixas em monitores grandes");
Check((long)StartupWindowSizing.MaximumSizeFor(highResolutionArea).Width *
      StartupWindowSizing.MaximumSizeFor(highResolutionArea).Height <=
          3L * highResolutionArea.Width * highResolutionArea.Height / 5L,
    "Tamanho maximo nunca excede 60% da area em 2560x1440");
Check(StartupWindowSizing.Fit(new System.Drawing.Rectangle(0, 0, 0, 0)).Size ==
      StartupWindowSizing.PreferredSize,
    "Tamanho seguro quando a area util ainda e desconhecida");

// Jogos têm manifestos próprios; não usar ARP para inventar desinstaladores.
Check(GameLibraryAudit.ParseSteamName("\"AppState\"\n{\n\"appid\" \"123\"\n\"name\" \"Jogo de Teste\"\n}") == "Jogo de Teste",
    "Manifesto Steam possui nome detectável sem API online");
Check(GameLibraryAudit.ParseSteamName("invalid text") is null &&
      GameLibraryAudit.ParseSteamName(new string('x', 260000)) is null,
    "Manifestos Steam suspeitos/longos não são aceitos");
var libs = GameLibraryAudit.ParseSteamLibraryFolders(
    "\"path\" \"D:\\\\SteamLibrary\"\n\"path\" \"E:\\\\Jogos Steam\"\n\"path\" \"D:\\\\SteamLibrary\"");
Check(libs.Count == 2 && libs[0] == @"D:\SteamLibrary",
    "Steam pode possuir vários discos de biblioteca sem varredura global");
Check(GameLibraryAudit.TryReadEpicManifest(
    """{"DisplayName":"Jogo do Epic","AppName":"SomeAppId","InstallLocation":"D:\\Epic"}""",
    out string epicName, out string epicId) &&
    epicName == "Jogo do Epic" && epicId == "SomeAppId",
    "Epic Games é reconhecido por manifesto local validado");
Check(!GameLibraryAudit.TryReadEpicManifest("""{"DisplayName":"Incomplete"}""",
    out _, out _),
    "Manifesto Epic sem identificador não deve ser inventado");
Console.WriteLine("OK Bibliotecas de jogos somente leitura");
// Um inventario de jogos tem todas as entradas, ainda que so 12 sejam
// mostradas como exemplos no diagnostico. Registrar sem gerar desinstaladores.
var sourceGames = new GameLibraryReport(3, 1, 5, 0, false,
    new[] { new GameLibraryEntry("Steam", "1", "BioShock Infinite", @"D:\\Steam") })
{
    ActiveNames = new[] { "BioShock Infinite", "Outro Jogo", "Game com ARP" },
    AllGames = new[] {
        new GameLibraryEntry("Steam", "1", "BioShock Infinite", @"D:\\Steam"),
        new GameLibraryEntry("Steam", "1", "BioShock Infinite", @"D:\\Steam"),
        new GameLibraryEntry("Steam", "2", "Outro Jogo", @"E:\\Steam"),
        new GameLibraryEntry("Epic Games", "epic-1", "Outro Jogo", @"D:\\Epic"),
        new GameLibraryEntry("Steam", "3", "Game com ARP", @"D:\\Steam")
    }
};
Check(sourceGames.Examples.Count == 1 && sourceGames.AllGames.Count == 5,
    "Inventario guarda todos os manifestos e separa os exemplos da interface");
var mergedGames = GameLibraryAudit.UnregisteredGames(sourceGames,
    new[] { new ProgramEntry { Name = "Game com ARP", InventorySource = "Registro: Computador" } });
Check(mergedGames.Count == 3 &&
      mergedGames.Count(x => x.DisplayName == "Outro Jogo") == 2 &&
      mergedGames.Count(x => x.DisplayName == "BioShock Infinite") == 1,
    "Deduplicar mesma identidade por plataforma sem fundir instalacoes em lojas diferentes");
Check(GameLibraryAudit.UnregisteredGames(sourceGames, new[] {
    new ProgramEntry { Name = "BioShock   Infinite" },
    new ProgramEntry { Name = "Outro Jogo", InventorySource = "Aplicativo empacotado (MSIX)" },
    new ProgramEntry { Name = "Game com ARP" }
}).Count == 0,
    "Programas Win32/MSIX existentes impedem duplicacao de jogos pela biblioteca");
Console.WriteLine("OK Inventario de jogos sem acao de desinstalacao ficticia");
 
// A aba de aplicativos é uma lista de AÇÕES. O inventário técnico completo
// continua disponível aos diagnósticos e à comparação de atualizações.
var systemOnly = new ProgramEntry { Name = "Bloco de notas", InventoryOnly = true };
var gameOnly = new ProgramEntry { Name = "BioShock Infinite", InventoryOnly = true,
    InventorySource = "Biblioteca de jogos: Steam" };
var staleApp = new ProgramEntry { Name = "Jogo removido",
    UninstallString = "\"Z:\\GF-Teste-Desinstalador-Ausente\\uninstall.exe\"" };
var regular = new ProgramEntry { Name = "Programa MSI", UninstallString = "msiexec.exe /X{00000000-0000-0000-0000-000000000001}" };
var quiet = new ProgramEntry { Name = "Outra instalação MSI",
    UninstallString = "", QuietUninstallString = "msiexec.exe /X{00000000-0000-0000-0000-000000000002}" };
var protectedWin32 = new ProgramEntry { Name = "Microsoft GameInput",
    UninstallString = "msiexec.exe /X{00000000-0000-0000-0000-000000000003}" };
Check(!ActionableInventory.CanUninstall(systemOnly) &&
      !ActionableInventory.CanUninstall(gameOnly) &&
      !ActionableInventory.CanUninstall(staleApp) &&
      !ActionableInventory.CanUninstall(protectedWin32),
    "Nao exibir botao Desinstalar em MSIX, jogos, comandos ausentes ou componentes protegidos");
Check(ActionableInventory.CanUninstall(regular) && ActionableInventory.CanUninstall(quiet),
    "Reconhecer desinstalacao MSI normal e silenciosa como operacoes disponiveis");
regular.CanUninstall = ActionableInventory.CanUninstall(regular);
quiet.CanUninstall = ActionableInventory.CanUninstall(quiet);
Check(ActionableInventory.VisibleCount(new[] { systemOnly, gameOnly, staleApp, regular, quiet, protectedWin32 }) == 2,
    "Lista principal mostra apenas dois aplicativos realmente desinstalaveis");
systemOnly.IsUpdate = true;
Check(ActionableInventory.ShouldShow(systemOnly) && !systemOnly.CanUninstall &&
      ActionableInventory.VisibleCount(new[] { systemOnly, gameOnly, staleApp, regular, quiet, protectedWin32 }) == 3,
    "Atualizacao na Store permanece visivel mesmo quando nao ha desinstalador");
systemOnly.IsUpdate = false;
gameOnly.IsUpdate = true;
Check(ActionableInventory.ShouldShow(gameOnly) && !ActionableInventory.CanUninstall(gameOnly),
    "Um jogo somente leitura pode aparecer se uma atualizacao for identificada sem liberar desinstalacao");
gameOnly.IsUpdate = false;
regular.IsUpdate = true;
Check(ActionableInventory.VisibleCount(new[] { systemOnly, gameOnly, staleApp, regular, quiet, protectedWin32 }) == 2,
    "Uma atualizacao nao duplica a linha de programa que ja tem desinstalador");
Console.WriteLine("OK Filtro de programas acionaveis preserva inventario completo sem comandos artificiais");

var inventorySummary = ActionableInventory.Summarize(new[]
    { systemOnly, gameOnly, staleApp, regular, quiet, protectedWin32 });
Check(inventorySummary.Total == 6 && inventorySummary.Actionable == 2 &&
      inventorySummary.WithoutAction == 4 && inventorySummary.Uninstallable == 2 &&
      inventorySummary.Updates == 1,
    "Resumo distingue inventario completo e acoes sem duplicar programa atualizavel e desinstalavel");
systemOnly.IsUpdate = true;
var storeSummary = ActionableInventory.Summarize(new[] { systemOnly, regular });
Check(storeSummary.Total == 2 && storeSummary.Actionable == 2 &&
      storeSummary.Uninstallable == 1 && storeSummary.Updates == 2 && storeSummary.WithoutAction == 0,
    "Resumo inclui atualizacao sem desinstalador sem contar como desinstalavel");
systemOnly.IsUpdate = false;
Check(ActionableInventory.Summarize(Array.Empty<ProgramEntry>()) == new InventoryActionSummary(0, 0, 0, 0),
    "Inventario vazio possui contadores zerados sem consultas ao sistema");



// Dispositivos antigos que ainda constam no Registro, mas não no snapshot
// PnP presente, devem ser relatados, NUNCA apagados.
var activePnp = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    @"PCI\VEN_10DE&DEV_ACTIVE\6&1234",
    @"USB\VID_ACTIVE&PID_0001\ACTIVE"
};
var registryHistory = new[]
{
    @"PCI\VEN_10DE&DEV_ACTIVE\6&1234",
    @"PCI\VEN_10DE&DEV_OLDGPU\6&4321",
    @"PCI\VEN_8086&DEV_OLDNIC\3&1111",
    @"USB\VID_ACTIVE&PID_0001\ACTIVE",
    @"USB\VID_OLD&PID_3000\OLD",
    @"pci\ven_10de&dev_oldgpu\6&4321"
};
var historicalPnp = HistoricalDeviceAudit.Compare(registryHistory, activePnp, 3, 0, false);
Check(historicalPnp.Registered == 5 && historicalPnp.NotPresent == 3,
    "Comparação encontra hardware histórico não presente e remove duplicações de maiúsculas");
var wmiSnapshot = new DeviceAudit(0, 0, 162, true, false, [], activePnp);
Check(wmiSnapshot.Disconnected == 0 && historicalPnp.NotPresent == 3,
    "Dispositivos desconectados e instancias do Registro sem correspondencia sao medidas diferentes");
Check(historicalPnp.Buses.Contains("PCI: 2") &&
      historicalPnp.Buses.Contains("USB: 1"),
    "Registros por barramento agrupados sem expor IDs de hardware");
Check(HistoricalDeviceAudit.Compare(registryHistory,
    new HashSet<string>(registryHistory, StringComparer.OrdinalIgnoreCase),
    3, 0, false).NotPresent == 0,
    "Todos os dispositivos presentes não são falsamente considerados antigos");
Check(HistoricalDeviceAudit.Compare(Array.Empty<string>(), activePnp,
    0, 2, true).Partial,
    "Acesso parcial não declara que computador está livre de registros antigos");
Console.WriteLine("OK Históricos PnP somente leitura: diferenças sem exclusão");

// Inventário ARP não depende de receitas e não combina arquiteturas
// ou instalações apenas porque nomes e versões coincidem.
var installedSameName = new[]
{
    new ProgramEntry { Name = "Visual C++ Runtime", Version = "14.0",
        Publisher = "Microsoft", InventoryId = "LocalMachine|Registry64|{A0000000-0000-0000-0000-000000000001}" },
    new ProgramEntry { Name = "Visual C++ Runtime", Version = "14.0",
        Publisher = "Microsoft", InventoryId = "LocalMachine|Registry32|{A0000000-0000-0000-0000-000000000002}" },
    new ProgramEntry { Name = "Visual C++ Runtime", Version = "14.0",
        Publisher = "Outra Empresa", InventoryId = "CurrentUser|Registry64|Runtime" },
    new ProgramEntry { Name = "Visual C++ Runtime", Version = "14.0",
        Publisher = "Microsoft", InventoryId = "LocalMachine|Registry64|{A0000000-0000-0000-0000-000000000001}" }
};
var distinctVersions = InventoryReconciler.DistinctRegistryEntries(installedSameName);
Check(distinctVersions.Count == 3 &&
      distinctVersions.Count(x => x.Publisher == "Microsoft") == 2,
    "Inventário preserva x64/x86 e diferentes fabricantes; remove somente a mesma chave repetida");
var sharedUserView = new[]
{
    new ProgramEntry { Name = "App Por Usuário", Version = "2.0",
        Publisher = "Editora", InventoryId = "CurrentUser|Registry64|App_User",
        UninstallString = @"C:\Program Files\App\uninstall.exe" },
    new ProgramEntry { Name = "App Por Usuário", Version = "2.0",
        Publisher = "Editora", InventoryId = "CurrentUser|Registry32|App_User",
        UninstallString = @"C:\Program Files\App\uninstall.exe" }
};
Check(InventoryReconciler.DistinctRegistryEntries(sharedUserView).Count == 1,
    "HKCU compartilhado entre Registry32/64 não gera duas linhas");
Check(InventoryReconciler.DistinctRegistryEntries(new[]
{
    sharedUserView[0],
    new ProgramEntry { Name = "App Por Usuário", Version = "2.0",
        Publisher = "Editora", InventoryId = "CurrentUser|Registry32|App_User",
        UninstallString = @"D:\Versao32\uninstall.exe" }
}).Count == 2,
    "HKCU 32/64 com comandos diferentes representa instalações distintas");
Check(InventoryReconciler.IsSameRegisteredProgram(installedSameName[0], installedSameName[3]) &&
      !InventoryReconciler.IsSameRegisteredProgram(installedSameName[0], installedSameName[1]),
    "Identidade vem da chave real e não de nome/versão");
Check(InventoryReconciler.ProductCodeFromRegistryKey(
      "{A0000000-0000-0000-0000-000000000001}") ==
      "{A0000000-0000-0000-0000-000000000001}" &&
      InventoryReconciler.ProductCodeFromRegistryKey("Program.Random") is null,
    "MSI ProductCode validado sem adivinhar IDs de executáveis");
Check(InventoryReconciler.DistinctRegistryEntries(new[]
    { new ProgramEntry { Name = "Programa sem ID" },
      new ProgramEntry { Name = "Programa sem ID" } }).Count == 2,
    "Falta de identificação não autoriza unir instalações distintas");
Console.WriteLine("OK Identidade ARP independente de catálogos e receitas");

// Auditoria realista de uma máquina antiga: apps sem desinstalador válido,
// drivers PnP desconectados por trocas de hardware e falsos positivos críticos.
Check(MaintenanceAudit.CheckUninstaller("MsiExec.exe /X{ABC123}", false) == "available",
    "O serviço oficial MSI é reconhecido sem executar nada");
Check(MaintenanceAudit.CheckUninstaller("", false) == "absent",
    "Entrada sem desinstalador aparece na avaliação");
Check(MaintenanceAudit.CheckUninstaller("", true) == "protected",
    "Componentes protegidos não são tratados como desinstaladores ausentes");
var missingExecutable = Path.Combine(Path.GetTempPath(),
    "GF-CI-No-Uninstaller-" + Guid.NewGuid().ToString("N"), "uninstall.exe");
Check(MaintenanceAudit.CheckUninstaller('"' + missingExecutable + '"', false) == "missing",
    "Caminho absoluto removido pode ser identificado sem apagar entrada de Registro");
Check(MaintenanceAudit.CheckUninstaller("uninstall.exe /S", false) == "unknown",
    "Comandos relativos não são sinalizados indevidamente como arquivos ausentes");
Check(MaintenanceAudit.CheckUninstaller('"' + Path.Combine(Path.GetDirectoryName(missingExecutable)!, "msiexec.exe") + '"', false) == "missing",
    "Arquivo ausente chamado msiexec nao recebe confianca pelo nome");
var alternateUninstall = new ProgramEntry { Name = "Duas alternativas", UninstallString = '"' + missingExecutable + '"', QuietUninstallString = "msiexec.exe /X{ABC123}" };
Check(MaintenanceAudit.InspectPrograms(new[] { alternateUninstall }).Available == 1 &&
      MaintenanceAudit.InspectPrograms(new[] { alternateUninstall }).Missing == 0,
    "Diagnostico usa alternativa silenciosa valida sem falso desinstalador ausente");
var brokenInventory = new[]
{
    new ProgramEntry { Name = "Antigo Editor", UninstallString = '"' + missingExecutable + '"',
        CatalogIdentity = "antigo-editor" },
    new ProgramEntry { Name = "Microsoft Store Runtime", InventoryOnly = true },
    new ProgramEntry { Name = "Outra Ferramenta", UninstallString = "msiexec.exe /X {GUID}" }
};
var maintenance = MaintenanceAudit.Inspect(brokenInventory, inspectDevices: false);
Check(maintenance.Installed == 3 && maintenance.Recognized == 1 &&
      maintenance.MissingUninstallers == 1 && maintenance.UsableUninstallers == 1,
    "Problemas de uninstall não impedem inventário de itens instalados e protegidos");
Check(maintenance.Concerns.Count == 1 &&
      maintenance.Concerns[0].Name == "Antigo Editor" &&
      maintenance.Concerns[0].Description.Contains("não remova o Registro"),
    "Falhas de registro recebem explicação segura em vez de remoção agressiva");
Check(MaintenanceAudit.ReviewDisconnectedDevice(false, "Display",
      @"PCI\VEN_10DE&DEV_1234", "Placa de vídeo anterior"),
    "GPU PCI desconectada é candidata a revisão de hardware");
Check(MaintenanceAudit.ReviewDisconnectedDevice(false, "Net",
      @"PCI\VEN_8086&DEV_5678", "Adaptador anterior"),
    "Rede PCI desconectada é candidata a revisão de hardware");
Check(!MaintenanceAudit.ReviewDisconnectedDevice(true, "Display",
      @"PCI\VEN_10DE&DEV_1234", "GPU atual") &&
      !MaintenanceAudit.ReviewDisconnectedDevice(null, "Display",
      @"PCI\VEN_10DE&DEV_1234", "GPU incerta"),
    "Hardware presente ou status desconhecido não é classificado como antigo");
Check(!MaintenanceAudit.ReviewDisconnectedDevice(false, "System",
      @"ROOT\SYSTEM\0000", "Componente do Windows") &&
      !MaintenanceAudit.ReviewDisconnectedDevice(false, "SoftwareDevice",
      @"SWD\PRINTENUM\0000", "Impressora virtual"),
    "Dispositivos ROOT e software virtual nunca viram drivers antigos automaticamente");
Console.WriteLine("OK Auditoria de inventário e hardware somente leitura");

// Visão geral: independe de quando o programa foi instalado ou desinstalado.
Check(BroadResidualDiscovery.Roots().Count >= 7,
    "Varredura cobre AppData local/roaming/locallow, ProgramData, ProgramFiles e atalhos");
Check(BroadResidualDiscovery.Roots().All(r =>
    !r.Path.Contains("\\Documents\\", StringComparison.OrdinalIgnoreCase) &&
    !r.Path.Contains("\\Downloads\\", StringComparison.OrdinalIgnoreCase) &&
    !r.Path.Contains("\\Pictures\\", StringComparison.OrdinalIgnoreCase) &&
    !r.Path.Contains("\\Music\\", StringComparison.OrdinalIgnoreCase)),
    "Varredura geral nao acessa bibliotecas pessoais");
Check(!BroadResidualDiscovery.EligibleName("Microsoft") &&
      !BroadResidualDiscovery.EligibleName("Documents") &&
      !BroadResidualDiscovery.EligibleName("Downloads") &&
      !BroadResidualDiscovery.EligibleName("Common Files") &&
      !BroadResidualDiscovery.EligibleName("Package Cache") &&
      !BroadResidualDiscovery.EligibleName("Windows Defender") &&
      !BroadResidualDiscovery.EligibleName("Windows PowerShell") &&
      !BroadResidualDiscovery.EligibleName("Classes") &&
      !BroadResidualDiscovery.EligibleName("RegisteredApplications") &&
      !BroadResidualDiscovery.EligibleName("dotnet") &&
      !BroadResidualDiscovery.EligibleName("D3DSCache") &&
      !BroadResidualDiscovery.EligibleName("1D0EC6DE-4A80-4CC3-A335-E6E41C951198") &&
      !BroadResidualDiscovery.EligibleName("613238"),
    "Nomes do Windows e diretorios pessoais sao bloqueados");
Check(BroadResidualDiscovery.NormalizeName("SampleOldTool.lnk") == "SampleOldTool" &&
      BroadResidualDiscovery.NormalizeName("SampleOldTool (x64)") == "SampleOldTool",
    "Reconhecer atalhos e variantes de arquitetura sem confundir instalacoes");
Check(BroadResidualDiscovery.IsProtectedByInstalled("OBS Studio",
        new[] { new ProgramEntry { Name = "OBS Studio", Publisher = "OBS Project" } }),
    "Pastas de aplicativo ativo jamais sao listadas como residuos");
Check(BroadResidualDiscovery.IsProtectedByInstalled("Outlook for Windows",
        new[] { new ProgramEntry { Name = "Outlook for Windows", InventoryOnly = true,
            InventorySource = "Aplicativo empacotado (MSIX)" } }),
    "Aplicativos Store ativos devem bloquear falsos residuos apos unificar inventario");
Check(BroadResidualDiscovery.IsProtectedByInstalled("Origin",
        new[] { new ProgramEntry { Name = "EA app", Publisher = "Electronic Arts" } }),
    "Origin pode conter saves ainda importantes para EA app");
Check(!BroadResidualDiscovery.IsProtectedByInstalled("LegacyEditor", Array.Empty<ProgramEntry>()),
    "Aplicativo antigo sem registro pode continuar sendo investigado");
var oldEvidence = new[] {
    new BroadResidualEvidence("LegacyEditor", @"C:\FakeData\LegacyEditor", "Dados locais", "AppData"),
    new BroadResidualEvidence("LegacyEditor", @"HKCU\Software\LegacyEditor", "Chave", "Registry"),
    new BroadResidualEvidence("LegacyEditor", @"C:\FakeStart\LegacyEditor.lnk", "Atalho", "StartMenu"),
    new BroadResidualEvidence("OBS Studio", @"C:\FakeData\OBS Studio", "Dados locais", "AppData"),
    new BroadResidualEvidence("Microsoft", @"C:\FakeData\Microsoft", "Dados locais", "AppData"),
    new BroadResidualEvidence("SoloOrphan", @"C:\FakeData\SoloOrphan", "Dados locais", "AppData"),
    new BroadResidualEvidence("Origin", @"C:\FakeData\Origin", "Dados locais", "AppData")
};
var classified = BroadResidualDiscovery.Classify(oldEvidence, new[]
{
    new ProgramEntry { Name = "OBS Studio", Publisher = "OBS Project" },
    new ProgramEntry { Name = "EA app", Publisher = "Electronic Arts" }
});
Check(classified.Count == 1 &&
      classified.Any(x => x.Product == "LegacyEditor" && x.Findings.Count == 3),
    "Indicios antigos exigem duas fontes diferentes e nenhum historico previo");
Check(classified.All(x => x.Historical && !x.UninstallConfirmed),
    "Varredura geral nao se apresenta como desinstalacao comprovada");
var partialClassification = BroadResidualDiscovery.Classify(oldEvidence, Array.Empty<ProgramEntry>(), inventoryIncomplete: true);
Check(partialClassification.Count > 0 && partialClassification.All(r => !r.UninstallConfirmed &&
    r.Findings.All(f => f.Evidence.Contains("Inventário de proteção parcial"))),
    "Inventario parcial torna explicita a incerteza em todos os achados historicos");
Check(classified.All(x => x.Product != "OBS Studio" && x.Product != "Microsoft" &&
      x.Product != "Origin"),
    "Ignorar aplicativos ativos, pastas protegidas e substituto EA app");
Check(classified.All(x => x.Product != "SoloOrphan") &&
      classified.Single().Findings[0].Evidence.Contains("não comprovam desinstalação", StringComparison.Ordinal),
    "Uma pasta isolada e insuficiente; evidencia cruzada continua nao confirmada");
Check(!BroadResidualDiscovery.HasIndependentResidueEvidence(new[] {
    new BroadResidualEvidence("K-Lite Codec Pack", @"C:\Program Files\K-Lite Codec Pack", "Pasta", "ProgramFiles"),
    new BroadResidualEvidence("K-Lite Codec Pack", @"C:\Start\K-Lite Codec Pack", "Atalho", "StartMenu")
}) && !BroadResidualDiscovery.HasIndependentResidueEvidence(new[] {
    new BroadResidualEvidence("Ferramenta", @"HKLM\SOFTWARE\Ferramenta", "Chave", "Registry"),
    new BroadResidualEvidence("Ferramenta", @"HKLM32\SOFTWARE\Ferramenta", "Chave", "Registry")
}), "Pastas e atalhos de software ativo, e espelhos do Registro, nao bastam");
var auditedEvidence = new[] {
    new BroadResidualEvidence("Netease", @"C:\AppData\Netease", "Dados locais", "AppData"),
    new BroadResidualEvidence("Netease", @"C:\ProgramData\Netease", "Dados", "ProgramData"),
    new BroadResidualEvidence("Netease", @"HKCU\Software\Netease", "Chave", "Registry"),
    new BroadResidualEvidence("K-Lite Codec Pack", @"C:\Program Files\K-Lite Codec Pack", "Pasta", "ProgramFiles"),
    new BroadResidualEvidence("K-Lite Codec Pack", @"C:\Start\K-Lite Codec Pack", "Atalho", "StartMenu"),
    new BroadResidualEvidence("Classes", @"HKLM\SOFTWARE\Classes", "Chave", "Registry"),
    new BroadResidualEvidence("Classes", @"C:\AppData\Classes", "Pasta", "AppData")
};
var audited = BroadResidualDiscovery.Classify(auditedEvidence, Array.Empty<ProgramEntry>());
Check(audited.Count == 1 && audited[0].Product == "Netease" &&
      audited[0].Findings.Count == 3 && audited[0].Historical &&
      !audited[0].UninstallConfirmed,
    "Relatorio real: Netease investigavel, componentes do Windows e apps possivelmente ativos excluidos");
Console.WriteLine("OK Auditoria geral testa historico independente com provas de baixo risco");
// Casos reproduzidos a partir da auditoria de outubro: sem acesso ao computador real.
Check(BroadResidualDiscovery.IsSharedPublisherName("Opera Software") &&
      BroadResidualDiscovery.IsSharedPublisherName("BANDAI NAMCO Entertainment") &&
      BroadResidualDiscovery.IsSharedPublisherName("Crystal Dynamics") &&
      BroadResidualDiscovery.IsSharedPublisherName("Western Digital") &&
      !BroadResidualDiscovery.IsSharedPublisherName("MPC-HC"),
    "Distinguir empresas e perfis compartilhados de aplicativos comuns");
var contextualEvidence = new[]
{
    new BroadResidualEvidence("Netease", @"C:\\Program Files\\Netease", "Instalação", "ProgramFiles"),
    new BroadResidualEvidence("Netease", @"C:\\ProgramData\\Netease", "Dados", "ProgramData"),
    new BroadResidualEvidence("Netease", @"HKCU\\Software\\Netease", "Registro", "Registry"),
    new BroadResidualEvidence("Opera Software", @"C:\\AppData\\Local\\Opera Software", "Dados", "AppData"),
    new BroadResidualEvidence("Opera Software", @"HKCU\\Software\\Opera Software", "Registro", "Registry"),
    new BroadResidualEvidence("Crystal Dynamics", @"C:\\AppData\\Roaming\\Crystal Dynamics", "Dados", "AppData"),
    new BroadResidualEvidence("Crystal Dynamics", @"HKCU\\Software\\Crystal Dynamics", "Registro", "Registry"),
    new BroadResidualEvidence("MPC-HC", @"C:\\AppData\\Roaming\\MPC-HC", "Dados", "AppData"),
    new BroadResidualEvidence("MPC-HC", @"HKCU\\Software\\MPC-HC", "Registro", "Registry")
};
var contextual = BroadResidualDiscovery.Classify(contextualEvidence, Array.Empty<ProgramEntry>());
Check(contextual.Count == 4 &&
      contextual.Single(x => x.Product == "Netease").Findings.All(x =>
          x.Kind.StartsWith("Instalação não confirmada", StringComparison.Ordinal)),
    "Netease com Program Files nao pode ser tratado como residuo confirmado");
Check(contextual.Where(x => x.Product is "Opera Software" or "Crystal Dynamics")
    .All(x => x.Findings.All(f => f.Kind.StartsWith("Dados compartilhados de fabricante", StringComparison.Ordinal))),
    "Perfis e jogos de fabricantes recebem protecao contextual sem exclusao");
Check(contextual.Single(x => x.Product == "MPC-HC").Findings.All(x =>
      x.Kind.StartsWith("Vestígio não confirmado", StringComparison.Ordinal)),
    "MPC-HC permanece como hipotese somente leitura");
Check(contextual.SelectMany(x => x.Findings).All(x =>
      x.Evidence.Contains("Somente leitura.", StringComparison.Ordinal)),
    "Nenhuma categoria contextual autoriza apagar dados");
Check(UninstallLaunchDiagnostics.Explain(2).Contains("não encontrou", StringComparison.Ordinal) &&
      UninstallLaunchDiagnostics.Explain(1223).Contains("cancelada", StringComparison.Ordinal) &&
      UninstallLaunchDiagnostics.Explain(740).Contains("elevação", StringComparison.Ordinal),
    "Falhas reais de desinstalacao recebem orientacao em portugues sem executar comandos");
Check(UninstallLaunchDiagnostics.NativeCode(new System.ComponentModel.Win32Exception(2)) == 2,
    "Codigo Win32 do sistema registrado sem dados pessoais");
Console.WriteLine("OK Classificacao contextual de residuos e falhas Win32");
// Casos observados na varredura real: classificacao SOMENTE LEITURA,
// sem inferir que software, jogos ou dados pessoais foram desinstalados.
var protectedHistory = new[]
{
    new BroadResidualEvidence("GOG.com", @"C:\FakeLocal\GOG.com", "Dados", "AppData"),
    new BroadResidualEvidence("GOG.com", @"C:\FakeProgramData\GOG.com", "Dados", "ProgramData"),
    new BroadResidualEvidence("GOG.com", @"HKCU\Software\GOG.com", "Chave", "Registry"),
    new BroadResidualEvidence("McAfee", @"C:\FakeRoaming\McAfee", "Dados", "AppData"),
    new BroadResidualEvidence("McAfee", @"C:\FakeProgramData\McAfee", "Dados", "ProgramData"),
    new BroadResidualEvidence("McAfee", @"HKLM32\Software\McAfee", "Chave", "Registry"),
    new BroadResidualEvidence("Microvirt", @"C:\FakeLocal\Microvirt", "Dados", "AppData"),
    new BroadResidualEvidence("Microvirt", @"C:\Program Files\Microvirt", "Instalacao", "ProgramFiles"),
    new BroadResidualEvidence("MPC-HC", @"C:\FakeRoaming\MPC-HC", "Dados", "AppData"),
    new BroadResidualEvidence("MPC-HC", @"HKCU\Software\MPC-HC", "Chave", "Registry")
};
var protectedReview = BroadResidualDiscovery.Classify(protectedHistory, Array.Empty<ProgramEntry>());
Check(protectedReview.Count == 4 &&
      protectedReview.All(r => r.Historical && !r.UninstallConfirmed &&
          r.Findings.All(f => f.Evidence.Contains("Somente leitura.", StringComparison.Ordinal))),
    "GOG.com, McAfee, Microvirt e MPC-HC permanecem em revisao sem exclusao");
Check(protectedReview.Single(r => r.Product == "Microvirt").Findings.All(f =>
        f.Kind.StartsWith("Instalação não confirmada", StringComparison.Ordinal)) &&
      protectedReview.Where(r => r.Product is "GOG.com" or "McAfee" or "MPC-HC")
        .SelectMany(r => r.Findings).All(f =>
            f.Kind.StartsWith("Vestígio não confirmado", StringComparison.Ordinal)),
    "Microvirt pode estar instalado e dados historicos nao sao lixo confirmado");
Check(BroadResidualDiscovery.Classify(protectedHistory, new[]
    { new ProgramEntry { Name = "GOG Galaxy", Publisher = "GOG.com" } })
    .All(r => r.Product != "GOG.com"),
    "Perfil GOG nao aparece como residuo quando fabricante ainda esta registrado");
Check(BroadResidualDiscovery.IsProtectedByInstalled("MPC-HC",
          new[] { new ProgramEntry { Name = "K-Lite Codec Pack Full" } }) &&
      BroadResidualDiscovery.IsProtectedByInstalled("Microvirt",
          new[] { new ProgramEntry { Name = "MEmu Play" } }) &&
      BroadResidualDiscovery.IsProtectedByInstalled("GOG.com",
          new[] { new ProgramEntry { Name = "GOG GALAXY" } }),
    "Alias de software presente protege MPC-HC, Microvirt e GOG sem limpeza");
Check(!BroadResidualDiscovery.IsProtectedByInstalled("MPC-HC",
          new[] { new ProgramEntry { Name = "Windows Terminal" } }) &&
      !BroadResidualDiscovery.IsProtectedByInstalled("Microvirt",
          new[] { new ProgramEntry { Name = "WinRAR" } }),
    "Alias nao pode vincular fabricantes a outros aplicativos");
Check(BroadResidualDiscovery.IsRunningProduct("MPC-HC", new[] { "mpc-hc64" }) &&
      BroadResidualDiscovery.IsRunningProduct("MPC-HC", new[] { "MPC-HC" }) &&
      !BroadResidualDiscovery.IsRunningProduct("McAfee", new[] { "mpc-hc64" }),
    "Reconhecer MPC-HC ativo mesmo sem registro de desinstalacao e sem confundir fabricantes");
Check(BroadResidualDiscovery.Classify(protectedHistory,
        Array.Empty<ProgramEntry>(), null, new[] { "mpc-hc64" })
    .All(report => report.Product != "MPC-HC"),
    "MPC-HC em execucao nao deve ser mostrado como vestigio de remocao");
Check(BroadResidualDiscovery.Classify(protectedHistory, Array.Empty<ProgramEntry>())
    .Any(report => report.Product == "MPC-HC" &&
        !report.UninstallConfirmed),
    "Ausencia de processo nao prova desinstalacao; apenas manter revisao informativa");



// Um jogo Steam/Epic pode existir somente como manifesto, sem entrada ARP.
// Os testes abaixo usam evidencias inventadas e nao tocam no Windows.
var gameEvidence = new[]
{
    new BroadResidualEvidence("BioShock Infinite", @"C:\\FakeLocal\\BioShock Infinite", "Dados", "AppData"),
    new BroadResidualEvidence("BioShock Infinite", @"HKCU\\Software\\BioShock Infinite", "Chave", "Registry"),
    new BroadResidualEvidence("Editor Sem Registro", @"C:\\FakeLocal\\Editor Sem Registro", "Dados", "AppData"),
    new BroadResidualEvidence("Editor Sem Registro", @"HKCU\\Software\\Editor Sem Registro", "Chave", "Registry")
};
var withoutGameManifest = BroadResidualDiscovery.Classify(gameEvidence,
    Array.Empty<ProgramEntry>());
Check(withoutGameManifest.Count == 2,
    "Sem manifesto de jogo ativo as duas identidades seguem como hipoteses");
var withGameManifest = BroadResidualDiscovery.Classify(gameEvidence,
    Array.Empty<ProgramEntry>(), new[] { "BIOSHOCK  INFINITE" });
Check(withGameManifest.Count == 1 &&
      withGameManifest[0].Product == "Editor Sem Registro",
    "Jogo ativo via Steam/Epic nao e falso residuo; outro aplicativo continua investigavel");
Check(BroadResidualDiscovery.Classify(gameEvidence, Array.Empty<ProgramEntry>(),
    new[] { "BioShock Infinite", "Editor Sem Registro" }).Count == 0,
    "Proteger todos os jogos conhecidos, nao so os 12 exemplos da interface");
var gamesOnly = new GameLibraryReport(42, 0, 42, 0, false,
    Array.Empty<GameLibraryEntry>()) { ActiveNames = new[] { "BioShock Infinite" } };
Check(gamesOnly.Examples.Count == 0 && gamesOnly.ActiveNames.Count == 1,
    "Inventario completo de jogos e independente dos exemplos visuais");
Console.WriteLine("OK Jogos ativos nao sao confundidos com residuos historicos");



// Catálogo histórico: Origem, Vivaldi e uTorrent não podem ser limpos
// automaticamente, e não devem aparecer como resíduos com produto instalado.
Check(HistoricalResidualAudit.Rules.Count == 5,
    "Cinco famílias históricas para auditoria não destrutiva");
Check(HistoricalResidualAudit.Rules.Any(r => r.Id == "origin-legacy" &&
    r.Registry.Contains(@"HKLM32\SOFTWARE\Origin") &&
    r.Folders.Contains(@"Local\Origin")),
    "Origem: pasta de cache/saves e chave HKLM32 identificadas sem permissão de apagar");
Check(HistoricalResidualAudit.Rules.Any(r => r.Id == "vivaldi" &&
    r.Registry.Contains(@"HKCU\Software\Vivaldi\NativeMessagingHosts\me.proton.pass.nm")),
    "Proton Pass native messaging considerado integração potencialmente ativa");
var vivaldi = HistoricalResidualAudit.Rules.Single(r => r.Id == "vivaldi");
Check(vivaldi.ActiveDependents.Contains("Proton Pass") &&
      HistoricalResidualAudit.IsRuleInstalled(vivaldi,
          new[] { new ProgramEntry { Name = "Proton Pass", Publisher = "Proton AG" } }),
    "Host de mensagens do Vivaldi protegido enquanto Proton Pass permanecer instalado");
Check(!HistoricalResidualAudit.IsRuleInstalled(vivaldi,
          new[] { new ProgramEntry { Name = "Brave" } }),
    "Outro navegador sem dependente ativo nao mascara a revisao historica de Vivaldi");
Check(HistoricalResidualAudit.Rules.Any(r => r.Id == "utorrent-web" &&
    r.Folders.Contains(@"StartMenu\uTorrent Web")),
    "Atalhos uTorrent Web reconhecidos no menu Iniciar");
var originRule = HistoricalResidualAudit.Rules.Single(r => r.Id == "origin-legacy");
Check(HistoricalResidualAudit.IsRuleInstalled(originRule,
        new[] { new ProgramEntry { Name = "EA app" } }),
    "Evitar tratar cache/saves Origin como lixo se EA app ainda está instalado");
Check(!HistoricalResidualAudit.IsRuleInstalled(originRule,
        new[] { new ProgramEntry { Name = "Vivaldi" } }),
    "Programas diferentes não bloqueiam revisão histórica de Origin");
var installedHistorical = new[] {
    new ProgramEntry { Name = "Origin" },
    new ProgramEntry { Name = "uTorrent Web" },
    new ProgramEntry { Name = "Vivaldi" }
};
Check(HistoricalResidualAudit.Inspect(installedHistorical).Count == 0,
    "Não listar sobras de produtos que ainda constem instalados");
try
{
    HistoricalResidualAudit.Parse("""{"schema":1,"rules":[{"id":"insecure","name":"Global","installedNames":["Global"],"folders":["Local\\Microsoft"],"registry":["HKLM32\\SOFTWARE\\Microsoft"]}]}""");
    throw new Exception("FALHA: regra genérica de sistema aceita");
}
catch (InvalidDataException) { passed++; }
Console.WriteLine("OK Catálogo recusa chave de sistema e diretórios genéricos");

// WinMerge: registro do fabricante documentado, inspeção estritamente
// somente leitura, inclusive quando a remoção ocorreu antes desta sessão.
Check(ResidualAudit.WinMergeRegistrySubkey == @"SOFTWARE\Thingamahoochie\WinMerge",
    "Auditoria conhece a chave WinMerge sem confundir com Software\\WinMerge");
Check(ResidualAudit.IsWinMergeProduct(new ProgramEntry
    { Name = "WinMerge x64", Publisher = "Thingamahoochie Software" }) &&
    ResidualAudit.IsWinMergeProduct(new ProgramEntry
    { Name = "WinMerge", Publisher = "Thingamahoochie Software" }) &&
    !ResidualAudit.IsWinMergeProduct(new ProgramEntry
    { Name = "WinMerge x64", Publisher = "Fabricante Incorreto" }) &&
    !ResidualAudit.IsWinMergeProduct(new ProgramEntry
    { Name = "WinMerge Driver", Publisher = "Thingamahoochie Software" }),
    "Auditoria especifica WinMerge recusa produtos ambiguos e outros fabricantes");
var winMergeInstalled = ResidualAudit.InspectHistoricalWinMerge(new[]
    { new ProgramEntry { Name = "WinMerge x64", Publisher = "Thingamahoochie Software" } });
Check(winMergeInstalled.Findings.Count == 0 &&
    !winMergeInstalled.UninstallConfirmed && winMergeInstalled.Historical,
    "Uma instalacao WinMerge ativa nunca e considerada residuo historico");
var winMergeHistorical = ResidualAudit.InspectHistoricalWinMerge(Array.Empty<ProgramEntry>());
Check(!winMergeHistorical.UninstallConfirmed && winMergeHistorical.Historical &&
    winMergeHistorical.Findings.All(r =>
        r.Location.Contains("WinMerge", StringComparison.OrdinalIgnoreCase) &&
        r.Evidence.Contains("somente leitura", StringComparison.OrdinalIgnoreCase)),
    "Somente locais especificos sao consultados; ausencia no inventario nao confirma remocao");

// Auditoria ampliada: detectar dados remanescentes sem autorizar exclusão.
Check(ResidualAudit.CanonicalName(new ProgramEntry
    { Name = "DroidCam Client", Publisher = "Dev47Apps" }) == "DroidCam",
    "Normalizar nome específico DroidCam sem incluir componentes genéricos");
Check(ResidualAudit.CanonicalName(new ProgramEntry
    { Name = "Windows Driver", Publisher = "Microsoft Corporation" }) is null,
    "Recusar pastas com nome genérico de componente de sistema");
var droidcam = new ProgramEntry { Name = "DroidCam Client", Publisher = "Dev47Apps" };
var drivers = new ProgramEntry
    { Name = "DroidCam Audio & Video Drivers (X64)", Publisher = "Dev47Apps" };
Check(ResidualAudit.FindRelatedRegistrations(droidcam, new[] { drivers }).Length == 1,
    "Informar drivers DroidCam ainda registrados, sem classificá-los como lixo");
Check(ResidualAudit.FindRelatedRegistrations(droidcam, Array.Empty<ProgramEntry>()).Length == 0,
    "Não inventar drivers quando não constam no inventário");
var droidReport = ResidualAudit.Inspect(droidcam, new[] { drivers });
Check(droidReport.RelatedRegisteredPrograms.Count == 1 &&
      droidReport.Findings.All(f => f.Items >= 0),
    "Auditoria DroidCam não apaga componentes e mantém evidências limitadas");
string auditFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "GF-Audit-Test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(auditFolder);
try
{
    File.WriteAllText(Path.Combine(auditFolder, "leftover.txt"), "simulado");
    File.WriteAllText(Path.Combine(auditFolder, "settings.ini"), "simulado");
    var ghost = new ProgramEntry
    {
        Name = "GF-Audit-Test Client", Publisher = "Test Publisher",
        InstallLocation = auditFolder
    };
    Check(ResidualAudit.SafeAuditRoot(auditFolder), "Somente ler dentro de diretório subordinado autorizado");
    var report = ResidualAudit.Inspect(ghost, Array.Empty<ProgramEntry>());
    Check(report.UninstallConfirmed && report.Findings.Any(x =>
        x.Location == auditFolder && x.Items == 2), "Encontrar dois resíduos onde catálogo antigo não tinha receita");
    Check(File.Exists(Path.Combine(auditFolder, "leftover.txt")),
        "Auditoria adicional não exclui sequer arquivos claramente encontrados");
    Check(!ResidualAudit.SafeAuditRoot(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData)),
        "Não varrer raiz genérica AppData");
    Check(!ResidualAudit.SafeAuditRoot(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages")),
        "Proibir área compartilhada Packages");
}
finally
{
    Directory.Delete(auditFolder, true);
}
var sumatra = new ProgramEntry { Name = "SumatraPDF", Publisher = "Krzysztof Kowalczyk" };
var notepad = new ProgramEntry { Name = "Notepad++ (64-bit x64)", Publisher = "Notepad++ Team" };
Check(UninstallResiduals.IsSupported(sumatra), "Sumatra conhecido");
Check(UninstallResiduals.IsSupported(notepad), "Notepad conhecido");
Check(!UninstallResiduals.IsSupported(new ProgramEntry
    { Name = "SumatraPDF", Publisher = "Desconhecido" }), "Recusar fabricante desconhecido");
Check(!UninstallResiduals.IsSupported(new ProgramEntry
    { Name = "Windows Defender", Publisher = "Microsoft Corporation" }),
    "Recusar limpeza de software nao auditado");
Check(!UninstallResiduals.StillInstalled(sumatra, Array.Empty<ProgramEntry>()),
    "Confirmar desinstalacao pelo Registro ausente");
Check(ApplicationIdentityCatalog.IsStillInstalled("OBS Studio", "OBS Project",
    new[] { (Name: "OBS Studio", Publisher: "OBS Project, LLC") }),
    "Desinstalador confirma que uma variante com o mesmo ID continua instalada");
Check(!ApplicationIdentityCatalog.IsStillInstalled("OBS Studio", "OBS Project",
    new[] { (Name: "OBS Studio", Publisher: "Empresa Falsa") }),
    "Fabricante falso não pode simular versão instalada");
Check(UninstallResiduals.StillInstalled(notepad, new[]
    { new ProgramEntry { Name = "Notepad++ (x64)", Publisher = "Don HO" } }),
    "Resíduos preservados enquanto outra edição Notepad++ está instalada");
Check(ApplicationIdentityCatalog.Resolve("SumatraPDF", "Krzysztof Kowalczyk")?.CleanupPolicy == "sumatrapdf",
    "Identidade Sumatra conecta inventário/atualização/desinstalação");
Check(ApplicationIdentityCatalog.Resolve("SumatraPDF", "Editor Falso") is null,
    "Não existe permissão de limpeza por nome sem fabricante válido");
Check(!ApplicationIdentityCatalog.IsStillInstalled("SumatraPDF", "Krzysztof Kowalczyk",
    Array.Empty<(string Name, string Publisher)>()),
    "Após remoção confirmada, consulta não reporta instalação fantasma");

Check(UninstallResiduals.StillInstalled(sumatra, new[] { sumatra }),
    "Bloquear limpeza se Sumatra ainda instalado");
Check(UninstallResiduals.StillInstalled(notepad, new[]
    { new ProgramEntry { Name = "Notepad++ (32-bit x86)", Publisher = "Don HO" } }),
    "Bloquear limpeza se outra arquitetura ainda instalada");
Check(UninstallResiduals.FindPreviouslyUninstalled(new[] { sumatra, notepad }).Count == 0,
    "Nao confundir programas ainda instalados com residuos");

string temp = Path.Combine(Path.GetTempPath(), "GF-Residual-Tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    string root = Path.Combine(temp, "SumatraPDF");
    Directory.CreateDirectory(root);
    File.WriteAllText(Path.Combine(root, "SumatraPDF.exe"), "simulated");
    File.WriteAllText(Path.Combine(root, "settings.txt"), "preferencias importantes");
    File.WriteAllText(Path.Combine(root, "notas-pessoais.pdf"), "dados");
    string custom = Path.Combine(root, "plugins");
    Directory.CreateDirectory(custom);
    File.WriteAllText(Path.Combine(custom, "user.dll"), "plugin pessoal");
    var allowed = new[] { "SumatraPDF.exe", "uninstall.exe" };
    var scan = UninstallResiduals.InspectDirectory("SumatraPDF", root, allowed);
    Check(scan?.Files.Count == 1 && scan.Preserved == 3,
        "Inspecionar apenas exe autorizado e identificar dados preservados");
    var deny = UninstallResiduals.Clean(sumatra, scan!);
    Check(deny.Failed == 1 && File.Exists(Path.Combine(root, "SumatraPDF.exe")),
        "Impedir exclusao com raiz TEMP fora da politica de producao");
    var cleaned = UninstallResiduals.CleanCandidateFiles(scan!, allowed);
    Check(cleaned.RemovedFiles == 0 && cleaned.RemovedDirectories == 0 &&
          cleaned.Preserved == 4 && cleaned.Failed == 1,
          "Candidata bloqueia exclusão direta e preserva todos os arquivos");
    Check(File.Exists(Path.Combine(root, "settings.txt")) &&
          File.Exists(Path.Combine(root, "notas-pessoais.pdf")) &&
          File.Exists(Path.Combine(custom, "user.dll")),
          "Nao remover configuracoes documentos ou plugins");
    Check(UninstallResiduals.InspectDirectory("SumatraPDF", root, allowed)?.Files.Count == 1,
          "Nova varredura confirma que o executável também foi preservado");
    string root2 = Path.Combine(temp, "Notepad++");
    Directory.CreateDirectory(root2);
    File.WriteAllText(Path.Combine(root2, "notepad++.exe"), "simulated");
    var second = UninstallResiduals.InspectDirectory("Notepad++", root2,
        new[] { "notepad++.exe" })!;
    var result2 = UninstallResiduals.CleanCandidateFiles(second, new[] { "notepad++.exe" });
    Check(result2.RemovedFiles == 0 && result2.RemovedDirectories == 0 &&
          Directory.Exists(root2), "Pasta e executável permanecem em simulação");
    string root3 = Path.Combine(temp, "unknown");
    Directory.CreateDirectory(root3);
    File.WriteAllText(Path.Combine(root3, "qualquer.exe"), "other");
    Check(UninstallResiduals.InspectDirectory("SumatraPDF", root3, allowed) is null,
          "Nao reconhecer binarios de nome desconhecido");
    Check(File.Exists(Path.Combine(root3, "qualquer.exe")), "Arquivo desconhecido preservado");
}
finally
{
    try { Directory.Delete(temp, true); }
    catch (IOException) { Console.WriteLine("AVISO: limpeza somente da area de teste indisponivel"); }
}

Console.WriteLine("Testes avançados: exclusão direta irreversível somente em diretórios explicitamente autorizados.");
string testBase = Path.Combine(Path.GetTempPath(), "GF-DeepTest-" + Guid.NewGuid().ToString("N"));
string knownRoot = Path.Combine(testBase, "OwnedApp");
string sibling = Path.Combine(testBase, "SharedData");
Directory.CreateDirectory(knownRoot);
Directory.CreateDirectory(sibling);
try
{
    string owned = Path.Combine(knownRoot, "prefs.ini");
    string backup = Path.Combine(knownRoot, "sessions", "backup.dat");
    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
    File.WriteAllText(owned, "preferencias do aplicativo");
    File.WriteAllText(backup, "backup privado");
    string shared = Path.Combine(sibling, "other-user-data.txt");
    File.WriteAllText(shared, "preservar");
    var notAllowed = AdvancedUninstallCleaner.DeleteApprovedTree(knownRoot, new[] { sibling });
    Check(notAllowed.Files == 0 && notAllowed.Skipped > 0 && File.Exists(owned),
        "Não excluir dados fora da lista explícita de diretórios");
    var erased = AdvancedUninstallCleaner.DeleteApprovedTree(knownRoot, new[] { knownRoot });
    Check(erased.Files == 0 && erased.Directories == 0 &&
          File.Exists(owned) && File.Exists(backup), "Nem allowlist explícita contorna bloqueio de exclusão de dados");
    Check(File.Exists(shared), "Não tocar dados compartilhados ou diretórios vizinhos");
    var other = AdvancedUninstallCleaner.DeleteApprovedTree(testBase, new[] { knownRoot });
    Check(other.Files == 0 && Directory.Exists(testBase),
        "Não aceitar pasta ancestral para apagar todo o perfil");
}
finally
{
    try { Directory.Delete(testBase, recursive: true); }
    catch (IOException) { Console.WriteLine("AVISO: lixo restrito ao teste."); }
}
// Exercício real de HKCU em chave criada somente para os testes do runner.
string keyName = "GF_Test_" + Guid.NewGuid().ToString("N");
string testKey = @"Software\" + keyName;
try
{
    using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(testKey))
    {
        if (key is null) throw new Exception("HKCU temporário indisponível");
        key.SetValue("Confirmacao", "Apenas teste");
        using var nested = key.CreateSubKey("SubKey");
        nested?.SetValue("Preferencias", "ficticias");
    }
    Check(!AdvancedUninstallCleaner.DeleteApprovedRegistryKey(testKey,
          new[] { @"Software\SumatraPDF" }), "Não excluir chaves HKCU fora da allowlist");
    Check(!AdvancedUninstallCleaner.DeleteApprovedRegistryKey(@"Software",
          new[] { @"Software" }), "Nunca excluir raiz Software do usuário");
    using (var stillThere = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testKey))
        Check(stillThere is not null, "Registro desconhecido permanece intacto");
    Check(!AdvancedUninstallCleaner.DeleteApprovedRegistryKey(testKey, new[] { testKey }),
        "Chamada interna não contorna bloqueio de exclusão do Registro");
    using (var removedKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testKey))
        Check(removedKey is not null, "Confirmar chave e valores preservados no Registro");
}
finally
{
    try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testKey, false); }
    catch (ArgumentException) { }
}
Check(AdvancedUninstallCleaner.Inspect(new[] { sumatra, notepad }).Count == 0,
    "Modo profundo não apresenta programa ainda instalado");
// Revalidar o disco nao amplia o consentimento dado no dialogo anterior.
var originallyAccepted = new DeepCleanItem(sumatra,
    new[] { @"C:\Approved", @"C:\NoLongerPresent" },
    new[] { @"Software\Approved", @"Software\NoLongerPresent" }, 1, 0);
var afterDialog = new DeepCleanItem(sumatra,
    new[] { @"C:\Approved", @"C:\AppearedAfterConsent" },
    new[] { @"Software\Approved", @"Software\AppearedAfterConsent" }, 2, 0);
var restricted = AdvancedUninstallCleaner.RestrictToAccepted(originallyAccepted, afterDialog);
Check(restricted.Folders.SequenceEqual(new[] { @"C:\Approved" }) &&
    restricted.RegistryKeys.SequenceEqual(new[] { @"Software\Approved" }),
    "Novas pastas/chaves apos confirmacao ficam preservadas; alvos desaparecidos nao reaparecem");
var emptyApproval = originallyAccepted with { Folders = Array.Empty<string>(), RegistryKeys = Array.Empty<string>() };
var nothingApproved = AdvancedUninstallCleaner.RestrictToAccepted(emptyApproval, afterDialog);
Check(nothingApproved.Folders.Count == 0 && nothingApproved.RegistryKeys.Count == 0,
    "Confirmacao sem alvos nao autoriza limpar novos residuos");
Console.WriteLine("Testes do catálogo de desinstalação embarcado.");
var rules = UninstallRuleCatalog.Rules;
Check(rules.Count == 6, "Seis políticas específicas validadas e incorporadas ao executável");
Check(rules.Any(x => x.Id == "sumatrapdf" && x.RegistryKeys.Single() == @"Software\SumatraPDF"),
    "Política SumatraPDF tem HKCU exclusivo");
Check(rules.Any(x => x.Id == "obs" && x.Folders.Count == 1 && x.RegistryKeys.Count == 0),
    "OBS possui perfil próprio sem remover Registro não comprovado");
Check(rules.Any(x => x.Id == "vlc" && x.Folders.Count == 1),
    "Nova cobertura de resíduos VLC");
Check(AdvancedUninstallCleaner.StillInstalled(new ProgramEntry { Name = "7-Zip" },
    new[] { new ProgramEntry { Name = "7-Zip 26.04 (x64)" } }),
    "Nunca remover configuração 7-Zip enquanto variante instalada");
Check(AdvancedUninstallCleaner.StillInstalled(new ProgramEntry { Name = "OBS Studio" },
    new[] { new ProgramEntry { Name = "OBS Studio 32.2.2" } }),
    "Não apagar perfil OBS enquanto variante do aplicativo está presente");
Check(!AdvancedUninstallCleaner.StillInstalled(new ProgramEntry { Name = "SumatraPDF" },
    Array.Empty<ProgramEntry>()), "SumatraPDF desinstalado pode ser analisado");
Check(AdvancedUninstallCleaner.StillInstalled(new ProgramEntry { Name = "Program Unknown" },
    Array.Empty<ProgramEntry>()), "Aplicativo fora do catálogo bloqueado por padrão");
Check(!UninstallRuleCatalog.NameMatches("7-Zippy Browser", "7-Zip*"),
    "Correspondência por prefixo não captura aplicativos homônimos");
Check(UninstallRuleCatalog.NameMatches("Notepad++ (64-bit x64)", "Notepad++*"),
    "Cobrir variantes reais do Notepad++");

void InvalidCatalog(string path, string key = "")
{
    string json = System.Text.Json.JsonSerializer.Serialize(new
    {
        schema = 1,
        rules = new[] { new {
            id = "app", displayName = "App Test", installedNames = new[] { "App Test" },
            folders = new [] { path }, registryKeys = key.Length == 0 ? Array.Empty<string>() : new[] { key }
        } }
    });
    try
    {
        UninstallRuleCatalog.Parse(json);
        throw new Exception("FALHA: catálogo inseguro aceito: " + path);
    }
    catch (InvalidDataException) { passed++; }
}
InvalidCatalog("Roaming\\..");
InvalidCatalog("Local\\Microsoft");
InvalidCatalog("Roaming\\App\\Documents");
InvalidCatalog("Windows\\System32");
InvalidCatalog("Local\\App", @"Software\Microsoft");
InvalidCatalog("Local\\App", @"Software\Google");
InvalidCatalog("Local\\App", @"Software\Microsoft\Windows");
Console.WriteLine("7 políticas maliciosas de catálogo recusadas.");


Console.WriteLine($"{passed}/{passed} testes de varredura e limpeza pós-desinstalacao aprovados.");
namespace GerenciadorAplicativos
{
    internal sealed class ProgramEntry
    {
        public string Name { get; init; } = "";
        public string Publisher { get; init; } = "";
        public string Version { get; init; } = "";
        public string InstallLocation { get; init; } = "";
        public string CatalogDisplayName { get; init; } = "";
        public string CatalogIdentity { get; init; } = "";
        public string InventoryId { get; init; } = "";
        public string InventorySource { get; init; } = "";
        public string MsiProductCode { get; init; } = "";
        public string UninstallString { get; init; } = "";
        public string QuietUninstallString { get; init; } = "";
        public bool InventoryOnly { get; init; }
        public bool CanUninstall { get; set; }
        public bool IsUpdate { get; set; }
    }
}
