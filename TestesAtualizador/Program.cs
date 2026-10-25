using GerenciadorAplicativos;
if (args.Contains("--work11-installer-fixture")) return;
if (args.Contains("--work13-slow-fixture")) { await Task.Delay(1000); return; }
if (args.Contains("--verify-catalog"))
{
    using var token = new CancellationTokenSource(TimeSpan.FromMinutes(4));
    using var catalog = await PackageCatalogIndex.DownloadAsync(token.Token);
    foreach (var item in new[] { ("OBS Studio", "OBS Project"), ("VLC media player", "VideoLAN"), ("ShareX", "ShareX Team"), ("SumatraPDF", "Krzysztof Kowalczyk") })
    {
        var rows = catalog.Find(item.Item1, item.Item2, "", token.Token);
        if (rows.Count != 1) throw new Exception("Catalogo nao identifica fixture publica " + item.Item1);
        var bytes = await PackageCatalogIndex.ReadManifestAsync(rows[0], token.Token);
        if(item.Item1!="SumatraPDF")
        {
            string kind=item.Item1=="ShareX"?"inno":"nullsoft";
            var parsed=AutomaticPackageManifest.Select(bytes,rows[0],"x64","machine",kind);
            if(parsed is null)throw new Exception("Manifesto publico sem contrato esperado "+rows[0].Id);
            Console.WriteLine("MANIFESTO.AUTOMATICO "+parsed.PackageId+" "+parsed.Version+" "+parsed.Type+" "+parsed.Arguments);
            if(args.Contains("--verify-engine-downloads") && item.Item1 is "VLC media player" or "ShareX")
            {
                string temp=Path.Combine(Path.GetTempPath(),"GF-Engine-Proof-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
                try
                {
                    using var client=new HttpClient(AutomaticUpdateDiscovery.DownloadHandler());Uri uri=parsed.Download;bool validated=false;
                    for(int redirects=0;redirects<8;redirects++)
                    {
                        if(!AutomaticPackageManifest.IsPublicHttps(uri))throw new Exception("Redirect nao autorizado");
                        using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token.Token);
                        if((int)response.StatusCode is >=300 and <400){uri=new Uri(uri,response.Headers.Location??throw new Exception("Redirect vazio"));continue;}
                        string file=Path.Combine(temp,"payload.exe");
                        await using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                            await UpdateDownloadTransfer.CopyAsync(response,output,50000,260L*1024*1024,token.Token);
                        using(var held=InstallerFileGuard.OpenRead(file,allowDirectoryWrites:true))await InstallerFileGuard.RequireDigestAsync(held,parsed.Sha256,token.Token);
                        string actualEngine=InstallerEngineEvidence.Detect(file);
                        if(actualEngine!=parsed.Type)throw new Exception("Motor real "+actualEngine+" diverge de "+parsed.Type);
                        if(TrustedFileSignature.TryGetSigner(file,out var signer,out var trustIssue))
                            Console.WriteLine("ENGINE.REAL_VALIDADO "+parsed.PackageId+" "+parsed.Type+" signer="+signer);
                        else
                        {
                            // ShareX 21.0.0 e um controle negativo real: hash oficial,
                            // Inno valido, mas sem certificado no PE. O produto exige assinatura.
                            using var input=File.OpenRead(file);
                            using var pe=new System.Reflection.PortableExecutable.PEReader(input);
                            var sample=new AutomaticAppInstallation("",parsed.Name,parsed.Publisher,parsed.Version,"machine","x64",temp,file,"","inno","");
                            if(item.Item1!="ShareX" || pe.PEHeaders.PEHeader!.CertificateTableDirectory.Size!=0 || sample.SameSigner(file))
                                throw new Exception("Assinatura real nao confirmada: "+trustIssue);
                            Console.WriteLine("ENGINE.REAL_VALIDADO "+parsed.PackageId+" "+parsed.Type+" assinatura=ausente EXECUCAO_BLOQUEADA");
                        }
                        validated=true;break;
                    }
                    if(!validated)throw new Exception("Limite de redirects atingido sem validar o instalador");
                }
                finally{Directory.Delete(temp,true);}
            }
        }
        Console.WriteLine("CATALOGO.AUTENTICADO " + rows[0].Id + " bytes=" + bytes.Length);
    }
    return;
}
if(args.Contains("--automatic")){Work14CapabilityTests.Run((ok,label)=>{Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(label);});AutomaticDiscoveryTests.Run((ok,label)=>{Console.WriteLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(label);});return;}
int checkedCount = 0;
void Verify(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("Falha: " + label);
    checkedCount++;
    Console.WriteLine("OK " + label);
}
if (args.Contains("--work13"))
{
    int failures = 0;
    await Work13UpdateTests.RunAsync((ok, label) => { Console.WriteLine((ok ? "PASS " : "FAIL ") + label); if (!ok) failures++; });
    if (failures > 0) throw new Exception($"WORK13: {failures} falhas reproduzidas");
    return;
}
if (args.Contains("--work11")) { await Work11ProgressTests.RunAsync(Verify); return; }
if (args.Contains("--work9"))
{
    int failures = 0;
    await Work9UpdateTests.RunAsync((ok, label) => { Console.WriteLine((ok ? "PASS " : "FAIL ") + label); if (!ok) failures++; });
    if (failures != 0) throw new Exception($"WORK9 updates: {failures} falhas reproduzidas");
    return;
}
if (args.Contains("--work6-flow"))
{ await Work6FlowTests.RunAsync(Verify); return; }
if (args.Contains("--work6-authorization"))
{
    var failures = new List<string>();
    Work6AuthorizationTests.Run((ok, label) => { Console.WriteLine((ok ? "PASS " : "FAIL ") + label); if (!ok) failures.Add(label); });
    if (failures.Count > 0) throw new InvalidOperationException($"WORK6: {failures.Count} falhas reproduzidas");
    return;
}
ProgramEntry Item(string name, string installed, string available, string publisher = "Microsoft Corporation", string source = "", bool inventoryOnly = false)
    => new() { Name = name, Version = installed, AvailableVersion = available, Publisher = publisher, Source = source, IsUpdate = true, InventoryOnly = inventoryOnly };
var x64 = VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501",
 "12.0.30501.0", "12.0.40664.0"));
var x86 = VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x86) - 12.0.30501",
 "12.0.30501.0", "12.0.40664.0"));
Verify(x64?.Identifier == "vc2013-x64" && x64.Download.Host == "aka.ms" &&
       VerifiedUpdaters.IsApprovedRecipe(x64) && !VerifiedUpdaters.IsApprovedRecipe(x64 with { Sha256 = null }),
       "Visual C++ x64 hash oficial autorizado; receita sem SHA continua bloqueada");
Verify(x86?.Identifier == "vc2013-x86" && x86.Download != x64!.Download &&
       VerifiedUpdaters.IsApprovedRecipe(x86),
       "Visual C++ x86 com hash oficial independente");
Verify(x64!.Arguments.Contains("/quiet") && x64.Arguments.Contains("/norestart") &&
       VerifiedUpdaters.GetPrimaryAction(Item("Microsoft Visual C++ 2013 Redistributable (x64)",
            "12.0.30501", "12.0.40664")) == "update",
       "Visual C++ oferece receita silenciosa com hash fixado e /norestart");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x64)",
 "12.0.30501.0", "12.0.40664.0", "Fabricante desconhecido")) is null, "Recusar editor nao Microsoft");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x64)",
 "12.0.30501.0", "12.0.40665.0")) is null, "Recusar versao sem receita");
Verify(VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x64)",
 "12.0.40664.0", "12.0.40664.0")) is null, "Recusar pacote atualizado");
var reviewObs = new ProgramEntry { Name = "OBS Studio", Version = "31.1.2", AvailableVersion = "32.2.2",
    Publisher = "OBS Project", Source = "GitHub oficial", IsUpdate = true, UpdateRequiresReview = true };
Verify(VerifiedUpdaters.Resolve(reviewObs) is null && VerifiedUpdaters.GetPrimaryAction(reviewObs) == "site",
    "Divergência preserva site oficial e bloqueia receita EXE mesmo com versão autorizada");
var reviewTerminal = new ProgramEntry { Name = "Windows Terminal", Version = "1.24.0.0", AvailableVersion = "1.25.2733.0",
    Publisher = "Microsoft Corporation", Source = "GitHub oficial", IsUpdate = true, InventoryOnly = true, UpdateRequiresReview = true };
Verify(WindowsTerminalUpdater.Resolve(reviewTerminal) is null && VerifiedUpdaters.GetPrimaryAction(reviewTerminal) == "store",
    "Divergência bloqueia instalador MSIX direto e conserva rota oficial Store");
// PC FORTE: EXE antigo + MSI novo. A coexistencia pode deixar um registro
// MSI sem binarios apos desinstalar o EXE. Testar somente com metadados sintéticos.
ProgramEntry ZipItem(string name = "7-Zip 26.04 (x64)", string version = "26.04",
    string publisher = "Igor Pavlov", string inventoryId = "LocalMachine|Registry64|7-Zip",
    string? location = null, string? uninstall = null)
{
    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip");
    return new ProgramEntry
    {
        Name = name, Version = version, AvailableVersion = "26.05",
        Publisher = publisher, Source = "GitHub oficial", IsUpdate = true,
        InventoryId = inventoryId, InstallLocation = location ?? folder + "\\",
        UninstallString = uninstall ?? "\"" + Path.Combine(folder, "Uninstall.exe") + "\""
    };
}
var zip = ZipItem();
var zipRecipe = VerifiedUpdaters.CreateSevenZipRecipe();
Verify(VerifiedUpdaters.IsApprovedRecipe(zipRecipe) &&
       zipRecipe.Identifier == "sevenzip-exe-x64-26.05" &&
       zipRecipe.Arguments == "/S" && !zipRecipe.Interactive &&
       zipRecipe.Download.Host == "github.com" &&
       zipRecipe.Download.AbsolutePath == "/ip7z/7zip/releases/download/26.05/7z2605-x64.exe" &&
       zipRecipe.Sha256 == "FD99E86F57D8F429EF5ED045BF98315AF962E2309B0F1BA87A147A44C71647D0",
       "7-Zip: receita EXE silenciosa fixa, URL oficial e SHA-256 de versao unica");
Verify(VerifiedUpdaters.IsSevenZipRegistryCandidate(zip) &&
       VerifiedUpdaters.IsInstalledVersionAtLeast(zipRecipe.Identifier, "26.04", [zip]),
       "7-Zip: nome/registro/desinstalador EXE x64 e editor consistentes");
Verify(!VerifiedUpdaters.IsApprovedRecipe(zipRecipe with { Sha256 = new string('0', 64) }) &&
       !VerifiedUpdaters.IsApprovedRecipe(zipRecipe with { Arguments = "/S /D=C:\\Outro" }) &&
       !VerifiedUpdaters.IsApprovedRecipe(zipRecipe with { Download = new Uri("https://example.com/7z2605-x64.exe") }) &&
       !VerifiedUpdaters.IsApprovedRecipe(zipRecipe with { RequiredSigner = "Microsoft" }),
       "7-Zip: hash, argumentos, origem e politica de assinatura nao alteraveis");
var zipMsi = ZipItem(name: "7-Zip 26.05 (x64 edition)", version: "26.05",
    inventoryId: "LocalMachine|Registry64|{23170F69-40C1-2702-2605-000001000000}",
    location: "", uninstall: "MsiExec.exe /I{23170F69-40C1-2702-2605-000001000000}");
Verify(!VerifiedUpdaters.IsSevenZipRegistryCandidate(zipMsi) &&
       !VerifiedUpdaters.SingleSevenZipRegistration(zip, [zip, zipMsi]) &&
       !VerifiedUpdaters.IsSevenZipRegistryCandidate(ZipItem(inventoryId: zipMsi.InventoryId)) &&
       !VerifiedUpdaters.IsSevenZipRegistryCandidate(ZipItem(uninstall: zipMsi.UninstallString)),
       "7-Zip: MSI coexistente e identidade equivocada bloqueiam atualizacao");
Verify(!VerifiedUpdaters.IsSevenZipRegistryCandidate(ZipItem(name: "7-Zip 26.04 (x86)")) &&
       !VerifiedUpdaters.IsSevenZipRegistryCandidate(ZipItem(publisher: "Editor desconhecido")) &&
       !VerifiedUpdaters.IsSevenZipRegistryCandidate(ZipItem(location: @"D:\Apps\7-Zip")) &&
       !VerifiedUpdaters.IsSevenZipRegistryCandidate(ZipItem(version: "26.03")) &&
       !VerifiedUpdaters.IsInstalledVersionAtLeast(zipRecipe.Identifier, "26.05", [zip]),
       "7-Zip: arquitetura, editor, pasta e versao divergentes rejeitados");
var zipNew = ZipItem(name: "7-Zip 26.05 (x64)", version: "26.05");
Verify(VerifiedUpdaters.IsSevenZipRegistryCandidate(zipNew) &&
       VerifiedUpdaters.IsInstalledVersionAtLeast(zipRecipe.Identifier, "26.05", [zipNew]) &&
       !VerifiedUpdaters.IsInstalledVersionAtLeast(zipRecipe.Identifier, "26.05", [zipMsi]),
       "7-Zip: novo EXE valido distinguido de registro MSI com versao recente");

var obs = VerifiedUpdaters.Resolve(Item("OBS Studio", "31.1.2", "32.2.2", "OBS Project"));
Verify(obs?.Identifier == "obs-x64-32.2.2" && obs.Sha256?.Length == 64 && !obs.Interactive && obs.Arguments == "/S",
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
Verify(gameRecipe is null && VerifiedUpdaters.GetPrimaryAction(gameInput) != "update",
       "GameInput protegido não autoriza pacote nem execução mesmo com fabricante/fonte corretos");
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
Verify(npp!.Arguments == "/S" && !npp.Interactive &&
       VerifiedUpdaters.IsApprovedRecipe(npp) &&
       !VerifiedUpdaters.IsApprovedRecipe(npp with { Arguments = "/s" }) &&
       !VerifiedUpdaters.IsApprovedRecipe(npp with { Arguments = "/S /closeRunningNpp" }),
       "Notepad++ NSIS: /S maiusculo silencioso, sem fechamento forçado ou flags adulteradas");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++", "8.9.5", "8.9.8.1", "Desconhecido", "GitHub oficial")) is null,
       "Notepad++: rejeitar editor desconhecido");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++", "8.9.5", "8.9.8.2", "Don HO", "GitHub oficial")) is null,
       "Notepad++: rejeitar versao nao aprovada");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++ (x86)", "8.9.5", "8.9.8.1", "Don HO", "GitHub oficial")) is { Identifier: "notepadpp-x86-8.9.8.1" } x86Npp &&
       x86Npp.Sha256 != npp.Sha256 && x86Npp.Download != npp.Download,
       "Notepad++: cadastro x86 recebe somente pacote e hash x86 proprios");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++", "8.9.5", "8.9.8.1", "Don HO", "GitHub oficial")) is null,
       "Notepad++: bloquear arquitetura nao identificada");
Verify(VerifiedUpdaters.GetPrimaryAction(Item("Notepad++ (64-bit x64)", "8.8.8", "8.9.8.1",
       "Notepad++ Team", "GitHub oficial")) == "update",
       "Notepad++ Team x64 deve apresentar botao Atualizar");
var unsafeMsiNpp = new ProgramEntry
{
    Name = "Notepad++ (64-bit x64)", Publisher = "Don HO",
    InventoryId = "LocalMachine|Registry64|SyntheticMSI",
    MsiProductCode = "{00000000-0000-0000-0000-000000000045}",
    UninstallString = "msiexec.exe /X{00000000-0000-0000-0000-000000000045}"
};
Verify(!VerifiedUpdaters.SingleSafeSilentInstallation(npp!, unsafeMsiNpp, [unsafeMsiNpp]),
    "Instalacao MSI Notepad++ nao pode ser substituida pelo EXE silencioso");
Verify(!VerifiedUpdaters.SingleSafeSilentInstallation(npp!, unsafeMsiNpp,
    [unsafeMsiNpp, new ProgramEntry { Name = "Notepad++ (x86)",
      InventoryId = "LocalMachine|Registry32|NPP32" }]),
    "Variantes MSI/EXE coexistentes impedem atualizar sem revisar a identidade");
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
       sumX64.Arguments == "-install -silent" && !sumX64.Interactive &&
       VerifiedUpdaters.IsApprovedRecipe(sumX64) &&
       !VerifiedUpdaters.IsApprovedRecipe(sumX64 with { Arguments = "-install" }),
       "SumatraPDF x64: instalador oficial silencioso por usuario com SHA e assinatura");
Verify(sumX86.Identifier == "sumatrapdf-x86-3.6.1" &&
       sumX86.Arguments == "-install -silent -all-users" &&
       !sumX86.Interactive && VerifiedUpdaters.IsApprovedRecipe(sumX86) &&
       sumX86.Download != sumX64.Download,
       "SumatraPDF x86: execução silenciosa preserva arquitetura e escopo global");
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
            Verify(!VerifiedUpdaters.ConfirmInstallation(actual64!, installed, new[] { updatedSumatra }), "Sumatra: cabecalho PE sem versao de arquivo nao comprova atualizacao");
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
Verify(!VerifiedUpdaters.ConfirmInstallation(x64!,
    new ProgramEntry { Name="Microsoft Visual C++ 2013 Redistributable (x64)", Publisher="Microsoft Corporation", Version="12.0.30501", InventoryId="LocalMachine|Registry64|VC2013" },
    [new ProgramEntry { Name="Microsoft Visual C++ 2013 Redistributable (x64)", Publisher="Microsoft Corporation", Version="12.0.40664", InventoryId="LocalMachine|Registry64|VC2013" }]),
    "VC registro sem DLL real nao pode confirmar instalacao");
var originalVc = Item("OBS Studio", "31.1.2", "32.2.2", "OBS Project");
originalVc.InventoryId = "LocalMachine|Registry64|OBS";
var updatedVc = Item(originalVc.Name, "32.2.2", "", "OBS Project");
updatedVc.InventoryId = originalVc.InventoryId;
Verify(!VerifiedUpdaters.ConfirmInstallation(obs!, originalVc, new[] { updatedVc }), "Registro sozinho sem binario nao comprova OBS; controle positivo real em WORK13");
updatedVc.InventoryId = "CurrentUser|Registry64|OBS";
Verify(!VerifiedUpdaters.ConfirmInstallation(obs!, originalVc, new[] { updatedVc }), "Outra instalacao por usuario nao confirma a selecionada");
updatedVc.InventoryId = "LocalMachine|Registry32|OBS";
Verify(!VerifiedUpdaters.ConfirmInstallation(obs!, originalVc, new[] { updatedVc }), "Outra vista do Registro nao confirma a selecionada");
originalVc.InventoryId = "";
Verify(!VerifiedUpdaters.ConfirmInstallation(obs!, originalVc, new[] { updatedVc }), "Identidade ausente permanece indeterminada");
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
    var recipes = args.Contains("--verify-work13", StringComparer.OrdinalIgnoreCase)
        ? new[] { x64, x86, VerifiedUpdaters.Resolve(Item("Notepad++ (32-bit x86)", "8.9.5", "8.9.8.1", "Don HO", "GitHub oficial")), obs, npp }
        : args.Contains("--verify-sevenzip", StringComparer.OrdinalIgnoreCase)
        ? new[] { zipRecipe }
        : args.Contains("--verify-sumatra", StringComparer.OrdinalIgnoreCase)
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
foreach (var approved in new[] { obs, npp, sumX64, sumX86 })
    Verify(approved is not null && VerifiedUpdaters.IsApprovedRecipe(approved),
        "Receita local completa reconhecida: " + approved?.Identifier);
var forgedRecipes = new[]
{
    obs! with { Arguments = "/arbitrary-command" },
    obs! with { RequiredSigner = "Outro Editor" },
    obs! with { Download = new Uri("https://github.com/outro/projeto/releases/download/x/installer.exe") },
    obs! with { Sha256 = new string('0', 64) },
    obs! with { Identifier = "../outside" },
    obs! with { Interactive = true },
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

Work14CapabilityTests.Run(Verify);
AutomaticDiscoveryTests.Run(Verify);
await Work13UpdateTests.RunAsync(Verify);
await Work9UpdateTests.RunAsync(Verify);
await Work11ProgressTests.RunAsync(Verify);
Work6AuthorizationTests.Run(Verify);
await Work6FlowTests.RunAsync(Verify);
SimulatedUpdateScenarioTests.Run(Verify);
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
        public bool UpdateRequiresReview { get; init; }
        public bool InventoryOnly { get; init; }
        public string InstallLocation { get; set; } = "";
        public string DisplayIcon { get; init; } = "";
        public string UninstallString { get; init; } = "";
        public string MsiProductCode { get; init; } = "";
    }
}


namespace GerenciadorAplicativos { internal sealed record OfficialRelease(InstalledApp Installed,string LatestVersion,string Provider,string OfficialPage,string SourceId="",string CanonicalId="",DateTimeOffset ObservedAt=default,string Architecture="unknown",string Channel="unknown",string Scope="unknown",string InstallType="unknown",string EvidenceKind="published-version"); internal sealed record ProviderDiagnostic(string Provider,string Status,long Milliseconds,bool UsedMemoryCache=false); }
