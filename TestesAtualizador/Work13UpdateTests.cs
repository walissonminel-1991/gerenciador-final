using GerenciadorAplicativos;
using System.IO.Compression;

internal static class Work13UpdateTests
{
    internal static ProgramEntry Obs(string version = "31.1.2", string location = @"C:\GF-W13\Original") => new()
    {
        Name = "OBS Studio", Publisher = "OBS Project", Version = version, AvailableVersion = "32.2.2",
        IsUpdate = true, Source = "GitHub oficial", InventoryId = "LocalMachine|Registry64|OBS Studio",
        InstallLocation = location, UninstallString = "\"" + location + "\\uninstall.exe\""
    };

    internal static async Task RunAsync(Action<bool, string> check)
    {
        var obs = VerifiedUpdaters.Resolve(Obs())!;
        check(!obs.Interactive && obs.Arguments == "/S", "W13 OBS comprovado oferece modo NSIS silencioso");
        var vc = VerifiedUpdaters.Resolve(new ProgramEntry { Name = "Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501",
            Publisher = "Microsoft Corporation", Version = "12.0.30501.0", AvailableVersion = "12.0.40664.0", IsUpdate = true })!;
        check(VerifiedUpdaters.IsApprovedRecipe(vc), "W13 VC2013 oficial com hash fixado pode ser autorizado");
        check(!VerifiedUpdaters.ConfirmInstallation(obs, Obs(), [Obs("32.2.2", @"C:\GF-W13\Another")]),
            "W13 OBS registro novo em outra pasta nao confirma a instalacao escolhida");
        check(!VerifiedUpdaters.ConfirmInstallation(obs, Obs(), [Obs("32.2.2")]),
            "W13 OBS registro com versao falsa sem binario nao comprova sucesso");

        using var token = new CancellationTokenSource(); int launches = 0;
        var result = await new UpdateLaunchGate().RunAsync(() => Task.FromResult(true),
            () => { launches++; return Task.FromResult(0); }, () => Task.FromResult(true), token.Token,
            p => { if (p.Stage == UpdateStage.Installing) token.Cancel(); });
        check(launches == 0 && result.State == UpdateAttemptState.Cancelled,
            "W13 cancelamento no ultimo callback antes de executar impede lancamento");

        string root = Path.Combine(Path.GetTempPath(), "GF-W13-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "synthetic.msixbundle");
            WriteBundle(path, "CN=Microsoft Corporation Evil, O=Unrelated");
            bool refused = false;
            try { WindowsTerminalUpdater.ValidateBundleIdentity(path); } catch (InvalidDataException) { refused = true; }
            check(refused, "W13 MSIX publisher com prefixo Microsoft e sufixo hostil e recusado");
        }
        finally { Directory.Delete(root, true); }
    }

    internal static void WriteBundle(string path, string publisher)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var text = new StreamWriter(archive.CreateEntry("AppxMetadata/AppxBundleManifest.xml").Open()))
            text.Write("<Bundle xmlns=\"http://schemas.microsoft.com/appx/2013/bundle\"><Identity Name=\"Microsoft.WindowsTerminal\" Version=\"3001.25.2733.0\" Publisher=\"" +
                publisher + "\"/><Packages><Package Type=\"application\" Architecture=\"x64\" FileName=\"synthetic.msix\" Version=\"1.25.2733.0\"/></Packages></Bundle>");
        using var signature = new StreamWriter(archive.CreateEntry("AppxSignature.p7x").Open()); signature.Write("SYNTHETIC IDENTITY TEST ONLY");
    }
}
