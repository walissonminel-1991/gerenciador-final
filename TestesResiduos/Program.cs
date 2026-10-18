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
    }
}