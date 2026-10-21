using GerenciadorAplicativos;
int checkedCount = 0;
void Verify(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("Falha: " + label);
    checkedCount++;
    Console.WriteLine("OK " + label);
}
ProgramEntry Item(string name, string installed, string available, string publisher = "Microsoft Corporation", string source = "", bool inventoryOnly = false)
    => new() { Name = name, Version = installed, AvailableVersion = available, Publisher = publisher, Source = source, IsUpdate = true, InventoryOnly = inventoryOnly };
var x64 = VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501",
 "12.0.30501.0", "12.0.40664.0"));
var x86 = VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x86) - 12.0.30501",
 "12.0.30501.0", "12.0.40664.0"));
Verify(x64?.Identifier == "vc2013-x64" && x64.Download.Host == "aka.ms" &&
       !VerifiedUpdaters.IsApprovedRecipe(x64),
       "Visual C++ x64 sem hash fixado: nao permite instalacao automatica");
Verify(x86?.Identifier == "vc2013-x86" && x86.Download != x64!.Download &&
       !VerifiedUpdaters.IsApprovedRecipe(x86),
       "Visual C++ x86 sem hash fixado: somente orientacao oficial");
Verify(x64!.Arguments.Contains("/quiet") && x64.Arguments.Contains("/norestart") &&
       VerifiedUpdaters.GetPrimaryAction(Item("Microsoft Visual C++ 2013 Redistributable (x64)",
            "12.0.30501", "12.0.40664")) == "site",
       "Visual C++ apresenta site oficial em vez de executar pacote sem SHA-256");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x64)",
 "12.0.30501.0", "12.0.40664.0", "Fabricante desconhecido")) is null, "Recusar editor nao Microsoft");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x64)",
 "12.0.30501.0", "12.0.40665.0")) is null, "Recusar versao sem receita");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x64)",
 "12.0.40664.0", "12.0.40664.0")) is null, "Recusar pacote atualizado");
var obs = VerifiedUpdaters.Resolve(Item("OBS Studio", "31.1.2", "32.2.2", "OBS Project"));
Verify(obs?.Identifier == "obs-x64-32.2.2" && obs.Sha256?.Length == 64 && obs.Interactive,
 "OBS instalador oficial e SHA-256 fixo");
Verify(VerifiedUpdaters.Resolve(Item("OBS Studio", "31.1.2", "33.0.0", "OBS Project")) is null,
 "Recusar versao OBS nao validada");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft .NET Runtime - 9.0.21 (x64)", "9.0.21", "10.0.13")) is null,
 "Nao confundir runtimes paralelos");
Verify(VerifiedUpdaters.GetOfficialPage(Item("Adobe Acrobat (64-bit)", "26.002.21931", "26.002.22031", "Adobe")) ==
 "https://helpx.adobe.com/br/acrobat/desktop/get-set-up/access-the-app/update-manual.html",
 "Adobe Acrobat: pagina oficial correta de atualizacao em portugues");
Verify(VerifiedUpdaters.GetOfficialPage(Item("Plex", "1.112.0", "1.115.0", "Plex")) is not null,
 "Site oficial Plex como alternativa");
Verify(VerifiedUpdaters.TryVersion("12.0.40664", out var ver) && ver == new Version(12, 0, 40664, 0),
 "Normalizacao de versoes");
Verify(VerifiedUpdaters.GetPrimaryAction(Item("OBS Studio", "31.1.2", "32.2.2", "OBS Project")) == "update",
 "Acao Atualizar exige receita verificada");
Verify(VerifiedUpdaters.GetPrimaryAction(Item("Adobe Acrobat (64-bit)", "26.002.21931", "26.002.22031", "Adobe")) == "site",
 "Acao Site oficial sem receita");
Verify(VerifiedUpdaters.GetPrimaryAction(Item("Aplicativo sem fabricante confirmado", "1.0", "2.0", "Desconhecido")) == "guide",
 "Candidato sem pagina nao executa download");
Verify(VerifiedUpdaters.GetPrimaryAction(new ProgramEntry { Name = "OBS Studio", IsUpdate = false }) is null,
 "Programa atualizado nao possui acao");
var outlook = Item("Outlook for Windows", "1.2025.416.400", "1.2026.812.100");
var gameInput = Item("Microsoft GameInput", "3.5.270.0", "3.5.283");
var terminal = Item("Windows Terminal", "1.24.12741.0", "1.25.2733.0", "Microsoft Corporation", "GitHub oficial", true);
Verify(VerifiedUpdaters.GetPrimaryAction(outlook) == "store" &&
       VerifiedUpdaters.GetMicrosoftStoreUri(outlook)?.AbsoluteUri.Contains("9NRX63209R7B") == true,
       "Outlook abre produto correto na Microsoft Store");
gameInput.Source = "Catálogo WinGet";
var gameRecipe = VerifiedUpdaters.Resolve(gameInput);
Verify(VerifiedUpdaters.GetMicrosoftStoreUri(gameInput) is null &&
       VerifiedUpdaters.GetStoreWebFallback(gameInput) is null,
       "GameInput não existe como produto da Microsoft Store");
Verify(VerifiedUpdaters.GetPrimaryAction(gameInput) == "update" &&
       gameRecipe?.Identifier == "gameinput-x64-3.5.283" &&
       gameRecipe.Sha256 == "B5988CB8FF9D7009B6DDF6AD4E3FF87E91B00CC17FFCD5D628DABC21C208F100",
       "GameInput autoriza somente pacote Microsoft NuGet fixado em SHA-256");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft GameInput", "3.5.270.0", "3.5.283",
       "Fornecedor de Jogos", "Catálogo WinGet")) is null,
       "GameInput rejeita fabricante nao Microsoft");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft GameInput", "3.5.270.0", "3.5.283",
       "Microsoft Corporation", "RuckZuck")) is null,
       "GameInput exige correspondencia de fonte Microsoft oficial");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft GameInput", "3.5.270.0", "4.0.300",
       "Microsoft Corporation", "Catálogo WinGet")) is null,
       "GameInput nao instala versao nao auditada");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft GameInput", "3.5.283.0", "3.5.283",
       "Microsoft Corporation", "Catálogo WinGet")) is null,
       "GameInput nao reinstala mesma versao");
var terminalRecipe = WindowsTerminalUpdater.Resolve(terminal);
Verify(VerifiedUpdaters.GetPrimaryAction(terminal) == "terminal" &&
       terminalRecipe?.Sha256 == "CCE6E9CC3C8457FA11CED1D2A7354888C7D6DB83C5ACE5D5891A52C95E3B21DA" &&
       terminalRecipe.Download.Host == "github.com",
       "Windows Terminal: atualizar MSIX GitHub com hash fixo");
Verify(WindowsTerminalUpdater.Resolve(Item("Windows Terminal", "1.24.12741.0", "1.25.2733.0",
       "Fornecedor desconhecido", "GitHub oficial", true)) is null,
       "Terminal: recusar fornecedor nao Microsoft");
Verify(WindowsTerminalUpdater.Resolve(Item("Windows Terminal Preview", "1.24.12741.0", "1.25.2733.0",
       "Microsoft Corporation", "GitHub oficial", true)) is null,
       "Terminal: nao atualizar canal Preview");
Verify(WindowsTerminalUpdater.Resolve(Item("Windows Terminal", "1.24.12741.0", "1.25.2733.0",
       "Microsoft Corporation", "RuckZuck", true)) is null,
       "Terminal: exigir GitHub oficial");
Verify(WindowsTerminalUpdater.Resolve(Item("Windows Terminal", "1.24.12741.0", "1.26.0.0",
       "Microsoft Corporation", "GitHub oficial", true)) is null,
       "Terminal: bloquear nova versao nao auditada");
Verify(WindowsTerminalUpdater.Resolve(Item("Windows Terminal", "1.25.2733.0", "1.25.2733.0",
       "Microsoft Corporation", "GitHub oficial", true)) is null,
       "Terminal: nao reinstalar versao ja atualizada");
Verify(WindowsTerminalUpdater.Resolve(Item("Windows Terminal", "1.24.12741.0", "1.25.2733.0",
       "Microsoft Corporation", "GitHub oficial", false)) is null,
       "Terminal: somente pacote MSIX identificado no inventario");
Verify(VerifiedUpdaters.GetMicrosoftStoreUri(terminal)?.AbsoluteUri.Contains("9N0DX20HK701") == true,
       "Windows Terminal: preservar Store como alternativa");
Verify(VerifiedUpdaters.GetMicrosoftStoreUri(Item("Windows Terminal", "1.0", "2.0", "Desconhecido")) is null,
       "Proibir spoof de fabricante Microsoft");
var npp = VerifiedUpdaters.Resolve(Item("Notepad++ (64-bit x64)", "8.9.5", "8.9.8.1", "Don HO", "GitHub oficial"));
Verify(npp?.Identifier == "notepadpp-x64-8.9.8.1" && npp.Sha256?.Length == 64 &&
       npp.RequiredSigner == "Notepad++", "Notepad++: receita fixada com digest e editor");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++", "8.9.5", "8.9.8.1", "Desconhecido", "GitHub oficial")) is null,
       "Notepad++: rejeitar editor desconhecido");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++", "8.9.5", "8.9.8.2", "Don HO", "GitHub oficial")) is null,
       "Notepad++: rejeitar versao nao aprovada");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++ (x86)", "8.9.5", "8.9.8.1", "Don HO", "GitHub oficial")) is null,
       "Notepad++: nao instalar pacote x64 no cadastro x86");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++", "8.9.5", "8.9.8.1", "Don HO", "GitHub oficial")) is null,
       "Notepad++: bloquear arquitetura nao identificada");
Verify(VerifiedUpdaters.GetPrimaryAction(Item("Notepad++ (64-bit x64)", "8.8.8", "8.9.8.1",
       "Notepad++ Team", "GitHub oficial")) == "update",
       "Notepad++ Team x64 deve apresentar botao Atualizar");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++ (64-bit x64)", "8.8.8", "8.9.8.1",
       "Editor desconhecido", "GitHub oficial")) is null,
       "Notepad++: publisher desconhecido nao ativa botao");
var sumX64 = VerifiedUpdaters.CreateSumatraRecipe(Item("SumatraPDF", "3.5", "3.6.1",
    "Krzysztof Kowalczyk", "GitHub oficial"), x64: true, allUsers: false);
var sumX86 = VerifiedUpdaters.CreateSumatraRecipe(Item("SumatraPDF", "3.5", "3.6.1",
    "Krzysztof Kowalczyk", "GitHub oficial"), x64: false, allUsers: true);
Verify(sumX64.Identifier == "sumatrapdf-x64-3.6.1" &&
       sumX64.Sha256?.Length == 64 && sumX64.RequiredSigner == "Krzysztof Kowalczyk" &&
       sumX64.Download.Host == "www.sumatrapdfreader.org" &&
       sumX64.Arguments == "-install",
       "SumatraPDF x64: instalador oficial por usuario com SHA e assinatura");
Verify(sumX86.Identifier == "sumatrapdf-x86-3.6.1" &&
       sumX86.Arguments.Contains("-all-users") && sumX86.Download != sumX64.Download,
       "SumatraPDF x86: preservar arquitetura e escopo global");
Verify(VerifiedUpdaters.Resolve(Item("SumatraPDF", "3.5", "3.6.1",
       "Empresa desconhecida", "GitHub oficial")) is null,
       "SumatraPDF: impedir fabricante falso");
Verify(VerifiedUpdaters.Resolve(Item("SumatraPDF", "3.5", "3.6.2",
       "Krzysztof Kowalczyk", "GitHub oficial")) is null,
       "SumatraPDF: nao aceitar versao de destino nao auditada");
Verify(VerifiedUpdaters.Resolve(Item("SumatraPDF", "3.5", "3.6.1",
       "Krzysztof Kowalczyk", "RuckZuck")) is null,
       "SumatraPDF: so permitir candidatos de fonte oficial");
Verify(VerifiedUpdaters.Resolve(Item("SumatraPDF", "3.6.1", "3.6.1",
       "Krzysztof Kowalczyk", "GitHub oficial")) is null,
       "SumatraPDF: evitar reinstalar versao ja atualizada");
{
    // Exercitar a rotina completa a partir de um executável PE simulado no
    // diretório padrão do runner, sem executar código ou instalar programas.
    string standardRoot = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "SumatraPDF");
    if (Directory.Exists(standardRoot))
        Console.WriteLine("AVISO: teste de arquitetura dispensado porque já existe pasta SumatraPDF.");
    else
    {
        Directory.CreateDirectory(standardRoot);
        string exe = Path.Combine(standardRoot, "SumatraPDF.exe");
        try
        {
            var stub = new byte[512];
            stub[0] = 0x4D; stub[1] = 0x5A;
            stub[0x3C] = 0x80; // PE offset
            stub[0x80] = 0x50; stub[0x81] = 0x45;
            stub[0x84] = 0x64; stub[0x85] = 0x86; // AMD64
            File.WriteAllBytes(exe, stub);
            var installed = Item("SumatraPDF", "3.5", "3.6.1",
                "Krzysztof Kowalczyk", "GitHub oficial");
            installed.InstallLocation = standardRoot;
            var actual64 = VerifiedUpdaters.Resolve(installed);
            Verify(actual64?.Identifier == "sumatrapdf-x64-3.6.1" &&
                   VerifiedUpdaters.GetPrimaryAction(installed) == "update",
                   "SumatraPDF instalado x64: botão Atualizar habilitado com arquitetura PE");
            installed.InventoryId = "CurrentUser|Registry64|SumatraPDF";
            var updatedSumatra = Item("SumatraPDF", "3.6.1", "", "Krzysztof Kowalczyk");
            updatedSumatra.InventoryId = installed.InventoryId;
            updatedSumatra.InstallLocation = standardRoot;
            Verify(VerifiedUpdaters.ConfirmInstallation(actual64!, installed, new[] { updatedSumatra }), "Sumatra confirma arquitetura e escopo originais");
            var wrongScope = VerifiedUpdaters.CreateSumatraRecipe(installed, true, true);
            Verify(!VerifiedUpdaters.ConfirmInstallation(wrongScope, installed, new[] { updatedSumatra }), "Sumatra local nao comprova pacote para todos os usuarios");
            stub[0x84] = 0x4C; stub[0x85] = 0x01; // I386
            File.WriteAllBytes(exe, stub);
            Verify(VerifiedUpdaters.Resolve(installed)?.Identifier == "sumatrapdf-x86-3.6.1",
                   "SumatraPDF instalado x86: arquitetura original preservada");
            Verify(!VerifiedUpdaters.ConfirmInstallation(actual64!, installed, new[] { updatedSumatra }), "Sumatra x86 nao confirma pacote x64");
            stub[0x84] = 0; stub[0x85] = 0;
            File.WriteAllBytes(exe, stub);
            Verify(VerifiedUpdaters.Resolve(installed) is null,
                   "Executável sem arquitetura comprovada não permite instalação");
        }
        finally
        {
            try { Directory.Delete(standardRoot, recursive: true); }
            catch (IOException) { Console.WriteLine("AVISO: não foi possível remover simulação."); }
        }
    }
}

Console.WriteLine($"{checkedCount}/{checkedCount} testes do motor autorizador aprovados");
Verify(VerifiedUpdaters.IsInstalledVersionAtLeast("vc2013-x64", "12.0.40664",
    new[] { Item("Microsoft Visual C++ 2013 Redistributable (x64)", "12.0.40664.0", "") }),
    "Pos-instalacao confirma produto, editor, arquitetura e versao equivalente");
Verify(!VerifiedUpdaters.IsInstalledVersionAtLeast("vc2013-x64", "12.0.40664",
    new[] { Item("Microsoft Visual C++ 2013 Redistributable (x86)", "12.0.40664.0", "") }),
    "Pos-instalacao x86 nao confirma atualizacao x64");
foreach (var publisher in new[] { "", "Outra Empresa", "Microsoft Tools Unofficial" })
    Verify(!VerifiedUpdaters.IsInstalledVersionAtLeast("vc2013-x64", "12.0.40664",
        new[] { Item("Microsoft Visual C++ 2013 Redistributable (x64)", "12.0.40664.0", "", publisher) }),
        "Pos-instalacao recusa editor ausente ou divergente: " + publisher);
Verify(!VerifiedUpdaters.IsInstalledVersionAtLeast("obs-x64-32.2.2", "32.2.2",
    new[] { Item("OBS Studio", "32.2.2", "", "OBS Project", inventoryOnly: true) }),
    "Entrada somente leitura nao comprova instalacao executada");
Verify(!VerifiedUpdaters.IsInstalledVersionAtLeast("obs-x64-32.2.2", "32.2.2",
    new[] { Item("OBS Studio", "31.1.2", "", "OBS Project") }),
    "Versao antiga apos instalador nao significa sucesso");
Verify(VerifiedUpdaters.IsInstalledVersionAtLeast("obs-x64-32.2.2", "32.2.2",
    new[] { Item("OBS Studio", "32.2.3", "", "OBS Project, LLC") }),
    "Versao posterior do mesmo produto e editor e reconhecida");
Verify(!VerifiedUpdaters.IsInstalledVersionAtLeast("receita-desconhecida", "1.0",
    new[] { Item("OBS Studio", "32.2.2", "", "OBS Project") }),
    "Receita desconhecida nao confirma instalacao");
var originalVc = Item("Microsoft Visual C++ 2013 Redistributable (x64)", "12.0.30501", "12.0.40664");
originalVc.InventoryId = "LocalMachine|Registry64|VC2013";
var updatedVc = Item(originalVc.Name, "12.0.40664", "");
updatedVc.InventoryId = originalVc.InventoryId;
Verify(VerifiedUpdaters.ConfirmInstallation(x64!, originalVc, new[] { updatedVc }), "Confirmar a identidade selecionada");
updatedVc.InventoryId = "CurrentUser|Registry64|VC2013";
Verify(!VerifiedUpdaters.ConfirmInstallation(x64!, originalVc, new[] { updatedVc }), "Outra instalacao por usuario nao confirma a selecionada");
updatedVc.InventoryId = "LocalMachine|Registry32|VC2013";
Verify(!VerifiedUpdaters.ConfirmInstallation(x64!, originalVc, new[] { updatedVc }), "Outra vista do Registro nao confirma a selecionada");
originalVc.InventoryId = "";
Verify(!VerifiedUpdaters.ConfirmInstallation(x64!, originalVc, new[] { updatedVc }), "Identidade ausente permanece indeterminada");
Console.WriteLine($"{checkedCount}/{checkedCount} testes incluindo confirmacao posterior aprovados");
if (args.Contains("--verify-terminal", StringComparer.OrdinalIgnoreCase))
{
    if (terminalRecipe is null) throw new InvalidOperationException("Terminal sem receita.");
    string folder = Path.Combine(Path.GetTempPath(), "GF-Terminal-Test-" + Guid.NewGuid().ToString("N"));
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
    try
    {
        Console.WriteLine("TERMINAL.MSIX.TESTE_INICIO");
        string bundle = await WindowsTerminalUpdater.DownloadAndVerifyAsync(terminalRecipe,
            folder, Console.WriteLine, cts.Token);
        Verify(new FileInfo(bundle).Length > 20_000_000, "Terminal pacote oficial MSIX verificado");
        Console.WriteLine("TERMINAL.MSIX.VERIFICADO_SEM_INSTALACAO");
    }
    finally
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        catch (IOException) { Console.WriteLine("AVISO: limpeza temporaria indisponivel."); }
    }
}
if (args.Contains("--verify-downloads", StringComparer.OrdinalIgnoreCase))
{
    var recipes = args.Contains("--verify-sumatra", StringComparer.OrdinalIgnoreCase)
        ? new[] { sumX64, sumX86 }
        : args.Contains("--verify-gameinput", StringComparer.OrdinalIgnoreCase)
        ? new[] { gameRecipe }
        : args.Contains("--verify-npp", StringComparer.OrdinalIgnoreCase)
        ? new[] { npp }
        : args.Contains("--verify-obs", StringComparer.OrdinalIgnoreCase)
        ? new[] { obs } : new[] { obs, npp };
    foreach (var recipe in recipes)
    {
        if (recipe is null) throw new InvalidOperationException("Receita não encontrada.");
        string directory = Path.Combine(Path.GetTempPath(), "GF-Trust-Test-" + Guid.NewGuid().ToString("N"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            Console.WriteLine("ASSINATURA.TESTE_INICIO " + recipe.Identifier);
            string file = await VerifiedUpdaters.DownloadAndVerifyAsync(recipe, directory,
                msg => Console.WriteLine("ASSINATURA.ETAPA " + msg), timeout.Token);
            Console.WriteLine("ASSINATURA.VALIDADA " + recipe.Identifier + " bytes=" + new FileInfo(file).Length);
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch { Console.WriteLine("AVISO: Limpeza temporária indisponível."); }
        }
    }
}


Console.WriteLine("Seguranca: receitas adulteradas e instaladores sem comprovante.");
foreach (var approved in new[] { obs, npp, sumX64, sumX86, gameRecipe })
    Verify(approved is not null && VerifiedUpdaters.IsApprovedRecipe(approved),
        "Receita local completa reconhecida: " + approved?.Identifier);
var forgedRecipes = new[]
{
    obs! with { Arguments = "/arbitrary-command" },
    obs! with { RequiredSigner = "Outro Editor" },
    obs! with { Download = new Uri("https://github.com/outro/projeto/releases/download/x/installer.exe") },
    obs! with { Sha256 = new string('0', 64) },
    obs! with { Identifier = "../outside" },
    obs! with { Interactive = false },
    sumX64 with { Arguments = "-install -arbitrary" }
};
string blockedFolder = Path.Combine(Path.GetTempPath(), "GF-Blocked-Recipe-" + Guid.NewGuid().ToString("N"));
foreach (var forged in forgedRecipes)
{
    Verify(!VerifiedUpdaters.IsApprovedRecipe(forged), "Receita adulterada recusada: " + forged.Identifier);
    bool refused = false;
    try { await VerifiedUpdaters.DownloadAndVerifyAsync(forged, blockedFolder, _ => { }, CancellationToken.None); }
    catch (InvalidDataException) { refused = true; }
    Verify(refused && !Directory.Exists(blockedFolder),
        "Receita adulterada bloqueada antes de rede/escrita");
}
bool noReceipt = false;
try
{
    using var unexpected = await VerifiedUpdaters.OpenVerifiedForLaunchAsync(
        obs!, Path.Combine(Path.GetTempPath(), "never-downloaded.exe"));
}
catch (InvalidDataException) { noReceipt = true; }
Verify(noReceipt, "Caminho sem comprovante da sessao nao chega ao instalador/UAC");


string isolated = Path.Combine(Path.GetTempPath(), "GF-Siblings-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(isolated);
try
{
    string installer = Path.Combine(isolated, obs!.Identifier + ".exe");
    File.WriteAllText(installer, "fixture nao executavel");
    VerifiedUpdaters.ValidateInstallerDirectory(obs, installer);
    string sibling = Path.Combine(isolated, "version.dll");
    File.WriteAllText(sibling, "fixture nao executavel");
    bool denied = false;
    try { VerifiedUpdaters.ValidateInstallerDirectory(obs, installer); }
    catch (InvalidDataException) { denied = true; }
    Verify(denied, "DLL inesperada ao lado do instalador bloqueia lancamento");
    File.Delete(sibling);
    Directory.CreateDirectory(sibling);
    denied = false;
    try { VerifiedUpdaters.ValidateInstallerDirectory(obs, installer); }
    catch (InvalidDataException) { denied = true; }
    Verify(denied, "pasta inesperada ao lado do instalador bloqueia lancamento");
}
finally { Directory.Delete(isolated, true); }

if (OperatingSystem.IsWindows()) AuthenticodeTests.Run(Verify);
else Console.WriteLine("SKIP: provedor Authenticode e contexto nativo exigem Windows; nao equivalem a aprovacao.");
await AuthenticodeTests.RunArchiveAsync(Verify);
Console.WriteLine($"{checkedCount}/{checkedCount} testes executados de autorizacao, identidade e integridade aprovados");

namespace GerenciadorAplicativos
{
    internal sealed class ProgramEntry
    {
        public string Name { get; init; } = "";
        public string InventoryId { get; set; } = "";
        public string Version { get; init; } = "";
        public string Publisher { get; init; } = "";
        public string AvailableVersion { get; init; } = "";
        public string Source { get; set; } = "";
        public bool IsUpdate { get; init; }
        public bool InventoryOnly { get; init; }
        public string InstallLocation { get; set; } = "";
        public string UninstallString { get; init; } = "";
    }
}
