using GerenciadorAplicativos;
int checkedCount = 0;
void Verify(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("Falha: " + label);
    checkedCount++;
    Console.WriteLine("OK " + label);
}
ProgramEntry Item(string name, string installed, string available, string publisher = "Microsoft Corporation", string source = "")
    => new() { Name = name, Version = installed, AvailableVersion = available, Publisher = publisher, Source = source, IsUpdate = true };
var x64 = VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501",
 "12.0.30501.0", "12.0.40664.0"));
var x86 = VerifiedUpdaters.Resolve(Item("Microsoft Visual C++ 2013 Redistributable (x86) - 12.0.30501",
 "12.0.30501.0", "12.0.40664.0"));
Verify(x64?.Identifier == "vc2013-x64" && x64.Download.Host == "aka.ms", "Visual C++ x64 autorizado");
Verify(x86?.Identifier == "vc2013-x86" && x86.Download != x64!.Download, "Visual C++ x86 autorizado");
Verify(x64!.Arguments.Contains("/quiet") && x64.Arguments.Contains("/norestart"), "Argumentos de instalacao controlados");
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
var terminal = Item("Windows Terminal", "1.24.12741.0", "1.25.2733.0", "Microsoft Corporation", "GitHub oficial");
Verify(VerifiedUpdaters.GetPrimaryAction(outlook) == "store" &&
       VerifiedUpdaters.GetMicrosoftStoreUri(outlook)?.AbsoluteUri.Contains("9NRX63209R7B") == true,
       "Outlook abre produto correto na Microsoft Store");
Verify(VerifiedUpdaters.GetPrimaryAction(gameInput) == "store" &&
       VerifiedUpdaters.GetMicrosoftStoreUri(gameInput)?.AbsoluteUri == "ms-windows-store://downloadsandupdates" &&
       VerifiedUpdaters.GetStoreWebFallback(gameInput) is null,
       "GameInput abre atualizacoes sem link de produto inventado");
Verify(VerifiedUpdaters.GetPrimaryAction(terminal) == "store" &&
       VerifiedUpdaters.GetMicrosoftStoreUri(terminal)?.AbsoluteUri.Contains("9N0DX20HK701") == true,
       "Windows Terminal prefere Store e atualizacoes continuas");
Verify(VerifiedUpdaters.GetMicrosoftStoreUri(Item("Windows Terminal", "1.0", "2.0", "Desconhecido")) is null,
       "Proibir spoof de fabricante Microsoft");
var npp = VerifiedUpdaters.Resolve(Item("Notepad++", "8.9.5", "8.9.8.1", "Don HO", "GitHub oficial"));
Verify(npp?.Identifier == "notepadpp-x64-8.9.8.1" && npp.Sha256?.Length == 64 &&
       npp.RequiredSigner == "Notepad++", "Notepad++: receita fixada com digest e editor");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++", "8.9.5", "8.9.8.1", "Desconhecido", "GitHub oficial")) is null,
       "Notepad++: rejeitar editor desconhecido");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++", "8.9.5", "8.9.8.2", "Don HO", "GitHub oficial")) is null,
       "Notepad++: rejeitar versao nao aprovada");
Verify(VerifiedUpdaters.Resolve(Item("Notepad++ (x86)", "8.9.5", "8.9.8.1", "Don HO", "GitHub oficial")) is null,
       "Notepad++: nao instalar pacote x64 no cadastro x86");
Console.WriteLine($"{checkedCount}/{checkedCount} testes do motor autorizador aprovados");
if (args.Contains("--verify-downloads", StringComparer.OrdinalIgnoreCase))
{
    var recipes = args.Contains("--verify-npp", StringComparer.OrdinalIgnoreCase)
        ? new[] { npp }
        : args.Contains("--verify-obs", StringComparer.OrdinalIgnoreCase)
        ? new[] { obs } : new[] { x86, x64 };
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


namespace GerenciadorAplicativos
{
    internal sealed class ProgramEntry
    {
        public string Name { get; init; } = "";
        public string Version { get; init; } = "";
        public string Publisher { get; init; } = "";
        public string AvailableVersion { get; init; } = "";
        public string Source { get; init; } = "";
        public bool IsUpdate { get; init; }
    }
}
