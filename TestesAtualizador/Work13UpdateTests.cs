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

        var progressOperation = new UpdateOperationStore().Begin(Obs(), obs.Version)!;
        foreach (var stage in new[] { UpdateStage.VerifyingIntegrity, UpdateStage.ValidatingSignature, UpdateStage.ValidatingIdentity })
            progressOperation.Report(new(stage));
        check(progressOperation.Snapshot.Progress.Stage == UpdateStage.ValidatingIdentity,
            "W13 identidade depois da assinatura aparece no progresso sem regressao de etapa");
        int reads = 0;
        check(await UpdateInstallationContract.ConfirmAfterAsync(() => Task.FromResult(++reads == 3), interval: TimeSpan.Zero) && reads == 3,
            "W13 bootstrapper demora: confirmacao aguarda evidencia posterior");
        reads = 0;
        check(!await UpdateInstallationContract.ConfirmAfterAsync(() => { reads++; return Task.FromResult(false); }, interval: TimeSpan.Zero) && reads == 4,
            "W13 exit zero com versao inalterada permanece inconclusivo apos limite");
        using (var stopped = new CancellationTokenSource())
        {
            stopped.Cancel(); bool cancelled = false;
            try { await UpdateInstallationContract.ConfirmAfterAsync(() => Task.FromResult(true), stopped.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            check(cancelled, "W13 cancelamento da consulta posterior nao confirma sucesso");
        }
        var npp32 = VerifiedUpdaters.Resolve(new ProgramEntry { Name = "Notepad++ (32-bit x86)", Publisher = "Don HO", Version = "8.9.5",
            AvailableVersion = "8.9.8.1", Source = "GitHub oficial", IsUpdate = true });
        check(npp32 is { Identifier: "notepadpp-x86-8.9.8.1", Arguments: "/S", Interactive: false } && VerifiedUpdaters.IsApprovedRecipe(npp32),
            "W13 Notepad++ x86 possui pacote e hash proprios");
        foreach (string name in new[] { "Adobe Acrobat (64-bit)", "Proton Pass", "Plex Media Server", "Jellyfin Server", "Microsoft GameInput", "Microsoft Visual C++ 2015-2022 Redistributable (x64)" })
            check(VerifiedUpdaters.Resolve(new ProgramEntry { Name = name, Publisher = "Microsoft Corporation", Version = "1.0", AvailableVersion = "99.0", IsUpdate = true }) is null,
                "W13 nao improvisa instalador para " + name);
        check(VerifiedUpdaters.GetOfficialPage(new ProgramEntry { Name = "Plex Media Server", IsUpdate = true }) == "https://www.plex.tv/media-server-downloads/" &&
            VerifiedUpdaters.GetOfficialPage(new ProgramEntry { Name = "Jellyfin Server 12.1.0", IsUpdate = true }) == "https://jellyfin.org/downloads/windows/",
            "W13 servidores encaminhados ao produto correto");

        foreach (bool cancelWait in new[] { false, true })
        {
            using var running = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Environment.ProcessPath!, UseShellExecute = false, CreateNoWindow = true,
                ArgumentList = { typeof(Work13UpdateTests).Assembly.Location, "--work13-slow-fixture" }
            })!;
            using var stop = new CancellationTokenSource();
            if (cancelWait) stop.CancelAfter(30);
            bool interrupted = false;
            try { await VerifiedUpdaters.WaitForInstallerExitAsync(running, cancelWait ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(30), stop.Token); }
            catch (OperationCanceledException) when (cancelWait) { interrupted = true; }
            catch (TimeoutException) when (!cancelWait) { interrupted = true; }
            check(interrupted && !running.HasExited, "W13 espera real interrompida sem matar processo sintetico: " + (cancelWait ? "cancelamento" : "prazo"));
            await running.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            check(running.ExitCode == 0, "W13 processo sintetico conclui depois da espera interrompida sem repeticao: " + cancelWait);
        }

        string root = Path.Combine(Path.GetTempPath(), "GF-W13-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "synthetic.msixbundle");
            const string publisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";
            WriteBundle(path, publisher);
            WindowsTerminalUpdater.ValidateBundleIdentity(path);
            check(true, "W13 MSIX identidade estrutural Microsoft exata aceita; assinatura testada separadamente");
            File.Delete(path);
            WriteBundle(path, "CN=Microsoft Corporation Evil, O=Unrelated");
            bool refused = false;
            try { WindowsTerminalUpdater.ValidateBundleIdentity(path); } catch (InvalidDataException) { refused = true; }
            check(refused, "W13 MSIX publisher com prefixo Microsoft e sufixo hostil e recusado");
            File.Delete(path); WriteBundle(path, publisher);
            using (var duplicate = ZipFile.Open(path, ZipArchiveMode.Update)) duplicate.CreateEntry("AppxSignature.p7x");
            refused = false; try { WindowsTerminalUpdater.ValidateBundleIdentity(path); } catch (InvalidDataException) { refused = true; }
            check(refused, "W13 MSIX entradas duplicadas recusadas");
            if (OperatingSystem.IsWindows()) RunBinaryContracts(root, obs, vc, check);
            else Console.WriteLine("SKIP W13 contratos PE e escopo canonico: executados na CI Windows");
        }
        finally { Directory.Delete(root, true); }
    }

    internal static void WriteBundle(string path, string publisher)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var text = new StreamWriter(archive.CreateEntry("AppxMetadata/AppxBundleManifest.xml").Open()))
            text.Write("<Bundle xmlns=\"http://schemas.microsoft.com/appx/2013/bundle\"><Identity Name=\"Microsoft.WindowsTerminal\" Version=\"3001.25.2733.0\" Publisher=\"" +
                publisher + "\"/><Packages><Package Type=\"application\" Architecture=\"x64\" FileName=\"synthetic.msix\" Version=\"1.25.2733.0\"/></Packages></Bundle>");
        using (var signature = new StreamWriter(archive.CreateEntry("AppxSignature.p7x").Open())) signature.Write("SYNTHETIC IDENTITY TEST ONLY");
        archive.CreateEntry("synthetic.msix");
    }
    private static ProgramEntry With(ProgramEntry entry, string? publisher = null, string? msi = null, bool? inventoryOnly = null, string? version = null) => new()
    {
        Name = entry.Name, Publisher = publisher ?? entry.Publisher, Version = version ?? entry.Version,
        IsUpdate = entry.IsUpdate, AvailableVersion = entry.AvailableVersion, Source = entry.Source,
        InventoryId = entry.InventoryId, InstallLocation = entry.InstallLocation, UninstallString = entry.UninstallString,
        MsiProductCode = msi ?? entry.MsiProductCode, InventoryOnly = inventoryOnly ?? entry.InventoryOnly
    };

    private static void RunBinaryContracts(string root, VerifiedUpdateRecipe obs, VerifiedUpdateRecipe vc, Action<bool,string> check)
    {
        UpdateInstallationContract.SyntheticRoot = root;
        try
        {
            var layout = UpdateInstallationContract.For(obs.Identifier)!;
            Directory.CreateDirectory(Path.GetDirectoryName(layout.Binary)!);
            WriteVersioned(layout.Binary, "31.1.00002.0");
            WriteVersioned(layout.Uninstaller, "31.1.00002.0");
            var original = Obs(location: layout.Root);
            var after = Obs("32.2.2", layout.Root);
            check(VerifiedUpdaters.SingleSafeSilentInstallation(obs, original, [original]), "W13 OBS antigo com binario real e pasta canonica autoriza preflight");
            check(!VerifiedUpdaters.ConfirmInstallation(obs, original, [after]), "W13 Registro atualizado com binario antigo nao confirma");
            WriteVersioned(layout.Binary, "32.2.00002.0");
            check(VerifiedUpdaters.ConfirmInstallation(obs, original, [after]), "W13 OBS confirma apenas Registro e binario coerentes");
            check(!VerifiedUpdaters.ConfirmInstallation(obs, original, [after, Obs("32.2.2", root)]), "W13 duas instalacoes posteriores nao confirmam uma copia arbitraria");
            WriteVersioned(layout.Binary, "32.2.00002.0", x64: false);
            check(!VerifiedUpdaters.ConfirmInstallation(obs, original, [after]), "W13 OBS x86 nao confirma atualizacao x64");
            WriteVersioned(layout.Binary, "32.2.00002.0");
            after = With(after, publisher: "Outro");
            check(!VerifiedUpdaters.ConfirmInstallation(obs, original, [after]), "W13 fabricante alterado apos atualizar e inconclusivo");
            after = With(after, publisher: original.Publisher);
            original = With(original, msi: "{00000000-0000-0000-0000-000000000013}");
            check(!VerifiedUpdaters.SingleSafeSilentInstallation(obs, original, [original]), "W13 instalacao MSI nao e substituida pelo NSIS");
            original = With(original, msi: "", inventoryOnly: true);
            check(!VerifiedUpdaters.SingleSafeSilentInstallation(obs, original, [original]), "W13 aplicativo protegido nao recebe EXE");

            var oldVc = new ProgramEntry { Name = "Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501", Publisher = "Microsoft Corporation",
                Version = "12.0.30501.0", AvailableVersion = vc.Version, IsUpdate = true, InventoryId = "LocalMachine|Registry32|OldVc" };
            var newVc = new ProgramEntry { Name = "Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.40664", Publisher = oldVc.Publisher,
                Version = vc.Version, InventoryId = "LocalMachine|Registry32|NewVc" };
            string dll = UpdateInstallationContract.VcBinary(true); Directory.CreateDirectory(Path.GetDirectoryName(dll)!);
            WriteVersioned(dll, "12.0.30501.0");
            check(VerifiedUpdaters.SingleSafeSilentInstallation(vc, oldVc, [oldVc]), "W13 VC2013 antigo exige DLL da arquitetura e versao registradas");
            check(!VerifiedUpdaters.ConfirmInstallation(vc, oldVc, [newVc]), "W13 GUID novo com DLL antiga nao confirma VC2013");
            WriteVersioned(dll, "12.0.40664.0");
            check(VerifiedUpdaters.ConfirmInstallation(vc, oldVc, [newVc]), "W13 migracao de GUID VC2013 confirmada por familia unica escopo e DLL");
            var waitingStore = new UpdateOperationStore(); var waitingOperation = waitingStore.Begin(oldVc, vc.Version)!;
            check(waitingStore.Begin(newVc, vc.Version) is null, "W13 GUID novo nao duplica runtime ainda ativo");
            waitingOperation.Complete(new(UpdateAttemptState.Indeterminate));
            check(waitingStore.Begin(newVc, vc.Version) is null && waitingStore.Find(newVc)?.Progress.Stage == UpdateStage.Pending,
                "W13 GUID novo preserva pendencia sem receber falso sucesso ou nova execucao");
            var store = new UpdateOperationStore(); var operation = store.Begin(oldVc, vc.Version)!;
            check(!store.BindConfirmedInstallation(vc, oldVc, [newVc], operation), "W13 resultado ativo nao pode ser reassociado como sucesso");
            operation.Complete(new(UpdateAttemptState.Confirmed));
            check(store.BindConfirmedInstallation(vc, oldVc, [newVc], operation) && store.Find(newVc)?.Id == operation.Snapshot.Id && store.Begin(newVc, vc.Version) is null,
                "W13 resultado segue GUID comprovado sem duplicar atualizacao");
            newVc.InventoryId = "CurrentUser|Registry32|NewVc";
            check(!VerifiedUpdaters.ConfirmInstallation(vc, oldVc, [newVc]), "W13 copia em outro usuario nao confirma VC2013 global");
            newVc.InventoryId = "LocalMachine|Registry32|NewVc"; newVc = With(newVc, version: "99.0.0.0");
            check(!VerifiedUpdaters.ConfirmInstallation(vc, oldVc, [newVc]), "W13 versao falsa de outra familia nao confirma runtime compartilhado");
            newVc = With(newVc, version: vc.Version);
            WriteVersioned(dll, "99.0.40664.0");
            check(!VerifiedUpdaters.ConfirmInstallation(vc, oldVc, [newVc]), "W13 DLL de outra familia nao comprova VC2013");
            foreach (bool x64 in new[] { false, true })
            {
                string id = x64 ? "notepadpp-x64-8.9.8.1" : "notepadpp-x86-8.9.8.1";
                var nppLayout = UpdateInstallationContract.For(id)!; Directory.CreateDirectory(nppLayout.Root);
                var npp = new ProgramEntry { Name = x64 ? "Notepad++ (64-bit x64)" : "Notepad++ (32-bit x86)", Publisher = "Don HO", Version = "8.9.5",
                    AvailableVersion = "8.9.8.1", IsUpdate = true, Source = "GitHub oficial", InventoryId = "LocalMachine|Registry32|Notepad++",
                    InstallLocation = nppLayout.Root, UninstallString = "\"" + nppLayout.Uninstaller + "\"" };
                var nppRecipe = VerifiedUpdaters.Resolve(npp)!;
                WriteVersioned(nppLayout.Binary, "08.9.00005.0", x64);
                check(VerifiedUpdaters.SingleSafeSilentInstallation(nppRecipe, npp, [npp]), "W13 Notepad++ preflight real arquitetura " + (x64 ? "x64" : "x86"));
                var nppAfter = With(npp, version: "8.9.8.1");
                check(!VerifiedUpdaters.ConfirmInstallation(nppRecipe, npp, [nppAfter]), "W13 Notepad++ registro novo com binario antigo " + id);
                WriteVersioned(nppLayout.Binary, "08.9.00008.1", x64);
                check(VerifiedUpdaters.ConfirmInstallation(nppRecipe, npp, [nppAfter]), "W13 Notepad++ confirmacao real com pacote correto " + id);
                WriteVersioned(nppLayout.Binary, "08.9.00008.1", !x64);
                check(!VerifiedUpdaters.ConfirmInstallation(nppRecipe, npp, [nppAfter]), "W13 Notepad++ arquitetura trocada nao confirma " + id);
            }
            bool refused = false;
            try { VerifiedUpdaters.ValidatePayloadIdentity(obs, layout.Binary); } catch (InvalidDataException) { refused = true; }
            check(refused, "W13 PE sintetico sem identidade do instalador e recusado mesmo com versao compatível");
        }
        finally { UpdateInstallationContract.SyntheticRoot = null; }
    }

    // Copia inerte do assembly de testes, modifica somente recurso VERSIONINFO e arquitetura.
    // Nunca e executada; todos os caminhos ficam na pasta temporaria sintetica.
    private static void WriteVersioned(string path, string version, bool x64 = true)
    {
        byte[] data = File.ReadAllBytes(typeof(Work13UpdateTests).Assembly.Location);
        byte[] oldText = System.Text.Encoding.Unicode.GetBytes("12.0.40664.0"), newText = System.Text.Encoding.Unicode.GetBytes(version);
        if (newText.Length != oldText.Length) throw new Exception("Comprimento inesperado da versao sintetica");
        for (int i = 0; i <= data.Length - oldText.Length; i++)
            if (data.AsSpan(i, oldText.Length).SequenceEqual(oldText)) newText.CopyTo(data, i);
        var v = Version.Parse(version);
        for (int i = 0; i < data.Length - 52; i++)
            if (BitConverter.ToUInt32(data, i) == 0xFEEF04BD)
            {
                BitConverter.GetBytes((uint)(v.Major << 16 | v.Minor)).CopyTo(data, i + 8);
                BitConverter.GetBytes((uint)(v.Build << 16 | v.Revision)).CopyTo(data, i + 12);
            }
        int pe = BitConverter.ToInt32(data, 60);
        BitConverter.GetBytes((ushort)(x64 ? 0x8664 : 0x14c)).CopyTo(data, pe + 4);
        File.WriteAllBytes(path, data);
    }

}
