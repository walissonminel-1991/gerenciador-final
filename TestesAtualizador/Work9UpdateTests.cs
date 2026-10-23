using GerenciadorAplicativos;

internal static class Work9UpdateTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        using var cancel = new CancellationTokenSource();
        int launches = 0;
        var result = await new UpdateLaunchGate().RunAsync(() => Task.FromResult(true),
            () => { launches++; return Task.FromResult(0); },
            () => { cancel.Cancel(); return Task.FromResult(true); }, cancel.Token);
        check(result.State == UpdateAttemptState.Indeterminate && launches == 1,
            "W9-U1 cancelamento durante pos-conferencia nunca confirma sucesso");
        ProgramEntry Obs(string msi) => new() { Name = "OBS Studio", Publisher = "OBS Project",
            Version = "31.1.2", InventoryId = "LocalMachine|Registry64|OBS", MsiProductCode = msi };
        check(!VerifiedUpdaters.SameInstallationForUpdate(Obs(""), [Obs("{00000000-0000-0000-0000-000000000091}")]),
            "W9-U2 mudanca EXE para MSI invalida a identidade antes do lancamento");
        check(VerifiedUpdaters.SameInstallationForUpdate(Obs(""), [Obs("")]),
            "W9-U2 controle positivo preserva a mesma instalacao EXE");

        ProgramEntry Npp(string location, string version, string id = "LocalMachine|Registry64|NPP") => new()
        { Name = "Notepad++ (64-bit x64)", Publisher = "Don HO", Version = version,
            AvailableVersion = "8.9.8.1", Source = "GitHub oficial", IsUpdate = true,
            InventoryId = id, InstallLocation = location };
        var original = Npp(@"C:\GF-Synthetic\Original", "8.9.5");
        var recipe = VerifiedUpdaters.Resolve(original)!;
        check(!VerifiedUpdaters.ConfirmInstallation(recipe, original,
            [Npp(@"C:\GF-Synthetic\Another", "8.9.8.1")]),
            "W9-U3 versao nova em outra pasta nao confirma o Notepad++ escolhido");
        check(!VerifiedUpdaters.ConfirmInstallation(recipe, original,
            [Npp(original.InstallLocation, "8.9.8.1"), Npp(@"C:\GF-Synthetic\MSI", "8.9.8.1", "Other")]),
            "W9-U3 coexistencia posterior nao confirma atualizacao silenciosa unica");

        if (!OperatingSystem.IsWindows())
        { Console.WriteLine("SKIP W9-U4/U5: PE, escopo e redirecionamento exigem Windows"); return; }
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SumatraPDF");
        if (Directory.Exists(folder)) throw new IOException("Fixture recusada: SumatraPDF ja existe");
        Directory.CreateDirectory(folder);
        try
        {
            string exe = Path.Combine(folder, "SumatraPDF.exe");
            WritePe(exe, x64: true);
            ProgramEntry Sumatra(string command) => new() { Name = "SumatraPDF", Publisher = "Krzysztof Kowalczyk",
                Version = "3.5.2", AvailableVersion = "3.6.1", IsUpdate = true, Source = "GitHub oficial",
                InventoryId = "CurrentUser|Registry64|SumatraPDF", InstallLocation = folder, UninstallString = command };
            var app = Sumatra("\"" + exe + "\" -uninstall");
            var sumRecipe = VerifiedUpdaters.Resolve(app)!;
            check(VerifiedUpdaters.SingleSafeSilentInstallation(sumRecipe, app, [app]),
                "W9-U4 controle positivo Sumatra x64 local coerente com a receita");
            var after = new ProgramEntry { Name = app.Name, Publisher = app.Publisher, Version = "3.6.1",
                InventoryId = app.InventoryId, InstallLocation = folder, UninstallString = app.UninstallString };
            check(!VerifiedUpdaters.ConfirmInstallation(sumRecipe, app, [after]),
                "W9-U6 Registro novo sem versao PE comprovada nao confirma Sumatra");
            string ownFile = typeof(Work9UpdateTests).Assembly.Location;
            string ownVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(ownFile).FileVersion!;
            check(VerifiedUpdaters.BinaryVersionAtLeast(ownFile, ownVersion) &&
                !VerifiedUpdaters.BinaryVersionAtLeast(ownFile, "9999.0"),
                "W9-U6 controle positivo le versao PE real sem executar arquivo");
            WritePe(exe, x64: false);
            check(!VerifiedUpdaters.SingleSafeSilentInstallation(sumRecipe, app, [app]),
                "W9-U4 PE alterado para x86 durante download bloqueia pacote x64");
            WritePe(exe, x64: true);
            var msi = Sumatra("msiexec.exe /X{00000000-0000-0000-0000-000000000092}");
            check(!VerifiedUpdaters.SingleSafeSilentInstallation(sumRecipe, msi, [msi]),
                "W9-U5 comando MSI sem campo ProductCode nao autoriza substituir pelo EXE");
            string moved = folder + ".GF-W9-" + Guid.NewGuid().ToString("N");
            Directory.Move(folder, moved);
            try
            {
                Directory.CreateSymbolicLink(folder, moved);
                check(!VerifiedUpdaters.SingleSafeSilentInstallation(sumRecipe, app, [app]),
                    "W9-U4 pasta Sumatra redirecionada bloqueia atualizacao");
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder); Directory.Move(moved, folder); }
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static void WritePe(string path, bool x64)
    {
        byte[] data = new byte[512];
        data[0] = 0x4d; data[1] = 0x5a; data[0x3c] = 0x80;
        data[0x80] = 0x50; data[0x81] = 0x45;
        data[0x84] = x64 ? (byte)0x64 : (byte)0x4c;
        data[0x85] = x64 ? (byte)0x86 : (byte)0x01;
        File.WriteAllBytes(path, data); // nunca executado
    }
}
