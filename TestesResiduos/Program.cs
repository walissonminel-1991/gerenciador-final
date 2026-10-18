using GerenciadorAplicativos;

int passed = 0;
void Check(bool result, string what)
{
    if (!result) throw new Exception("FALHA: " + what);
    Console.WriteLine("OK " + what);
    passed++;
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

Console.WriteLine($"{passed}/{passed} testes de varredura e limpeza pós-desinstalacao aprovados.");
namespace GerenciadorAplicativos
{
    internal sealed class ProgramEntry
    {
        public string Name { get; init; } = "";
        public string Publisher { get; init; } = "";
    }
}