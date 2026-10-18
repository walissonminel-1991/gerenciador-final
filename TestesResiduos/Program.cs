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

Check(largeDesktop.Size == new System.Drawing.Size(1250, 1160),
    "Abrir com o dimensionamento vertical aprovado na captura");
Check(regularDesktop.Size == new System.Drawing.Size(1250, 1040) &&
      regularDesktop.Y == 0 && regularDesktop.X == 335,
    "Respeitar altura da area de trabalho com barra de tarefas");
Check(shiftedDesktop.Left >= -1920 && shiftedDesktop.Right <= 0 &&
      shiftedDesktop.Top >= 40 && shiftedDesktop.Bottom <= 1120,
    "Centralizar em monitor com origem e posicao deslocadas");
Check(smallDesktop.Width == 1024 && smallDesktop.Height == 720 &&
      StartupWindowSizing.AllowedMinimum(smallDesktop).Width <= smallDesktop.Width,
    "Nao iniciar fora de monitores menores");
Check(StartupWindowSizing.Fit(new System.Drawing.Rectangle(0, 0, 0, 0)).Size ==
      StartupWindowSizing.PreferredSize,
    "Ter tamanho inicial seguro quando o monitor ainda nao puder ser consultado");

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
      !BroadResidualDiscovery.EligibleName("Package Cache"),
    "Nomes do Windows e diretorios pessoais sao bloqueados");
Check(BroadResidualDiscovery.NormalizeName("SampleOldTool.lnk") == "SampleOldTool" &&
      BroadResidualDiscovery.NormalizeName("SampleOldTool (x64)") == "SampleOldTool",
    "Reconhecer atalhos e variantes de arquitetura sem confundir instalacoes");
Check(BroadResidualDiscovery.IsProtectedByInstalled("OBS Studio",
        new[] { new ProgramEntry { Name = "OBS Studio", Publisher = "OBS Project" } }),
    "Pastas de aplicativo ativo jamais sao listadas como residuos");
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
Check(classified.Count == 2 &&
      classified.Any(x => x.Product == "LegacyEditor" && x.Findings.Count == 3) &&
      classified.Any(x => x.Product == "SoloOrphan" && x.Findings.Count == 1),
    "Identificar indícios antigos mesmo sem qualquer histórico do gerenciador");
Check(classified.All(x => x.Historical && !x.UninstallConfirmed),
    "Varredura geral nao se apresenta como desinstalacao comprovada");
Check(classified.All(x => x.Product != "OBS Studio" && x.Product != "Microsoft" &&
      x.Product != "Origin"),
    "Ignorar aplicativos ativos, pastas protegidas e substituto EA app");
Check(classified.Single(x => x.Product == "SoloOrphan").Findings[0].Evidence
      .Contains("Apenas um local", StringComparison.Ordinal),
    "Sinais de baixa confianca sao claramente diferenciados");
Console.WriteLine("OK Auditoria geral testa historico independente com provas de baixo risco");

// Catálogo histórico: Origem, Vivaldi e uTorrent não podem ser limpos
// automaticamente, e não devem aparecer como resíduos com produto instalado.
Check(HistoricalResidualAudit.Rules.Count == 3,
    "Três famílias históricas para auditoria não destrutiva");
Check(HistoricalResidualAudit.Rules.Any(r => r.Id == "origin-legacy" &&
    r.Registry.Contains(@"HKLM32\SOFTWARE\Origin") &&
    r.Folders.Contains(@"Local\Origin")),
    "Origem: pasta de cache/saves e chave HKLM32 identificadas sem permissão de apagar");
Check(HistoricalResidualAudit.Rules.Any(r => r.Id == "vivaldi" &&
    r.Registry.Contains(@"HKCU\Software\Vivaldi\NativeMessagingHosts\me.proton.pass.nm")),
    "Proton Pass native messaging considerado integração potencialmente ativa");
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
    Check(cleaned.RemovedFiles == 1 && cleaned.RemovedDirectories == 0 &&
          cleaned.Preserved == 3 && cleaned.Failed == 0,
          "Excluir apenas arquivo exato, mantendo dados e subpasta");
    Check(File.Exists(Path.Combine(root, "settings.txt")) &&
          File.Exists(Path.Combine(root, "notas-pessoais.pdf")) &&
          File.Exists(Path.Combine(custom, "user.dll")),
          "Nao remover configuracoes documentos ou plugins");
    Check(UninstallResiduals.InspectDirectory("SumatraPDF", root, allowed) is null,
          "Nova varredura nao sugere apagar dados pessoais");
    string root2 = Path.Combine(temp, "Notepad++");
    Directory.CreateDirectory(root2);
    File.WriteAllText(Path.Combine(root2, "notepad++.exe"), "simulated");
    var second = UninstallResiduals.InspectDirectory("Notepad++", root2,
        new[] { "notepad++.exe" })!;
    var result2 = UninstallResiduals.CleanCandidateFiles(second, new[] { "notepad++.exe" });
    Check(result2.RemovedFiles == 1 && result2.RemovedDirectories == 1 &&
          !Directory.Exists(root2), "Remover pasta SOMENTE quando ficar vazia");
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
    Check(erased.Files == 2 && erased.Directories == 2 &&
          !Directory.Exists(knownRoot), "Apagar pasta própria, preferências e backups com autorização");
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
    Check(AdvancedUninstallCleaner.DeleteApprovedRegistryKey(testKey, new[] { testKey }),
        "Remover subchave HKCU exclusiva após permissão explícita");
    using (var removedKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testKey))
        Check(removedKey is null, "Confirmar chave e valores removidos do Registro");
}
finally
{
    try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testKey, false); }
    catch (ArgumentException) { }
}
Check(AdvancedUninstallCleaner.Inspect(new[] { sumatra, notepad }).Count == 0,
    "Modo profundo não apresenta programa ainda instalado");
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
        public string InstallLocation { get; init; } = "";
        public string CatalogDisplayName { get; init; } = "";
        public string CatalogIdentity { get; init; } = "";
        public string UninstallString { get; init; } = "";
        public string QuietUninstallString { get; init; } = "";
        public bool InventoryOnly { get; init; }
    }
}