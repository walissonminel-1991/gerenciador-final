using GerenciadorAplicativos;

internal static class Work14SilentTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        string basis = Path.Combine(Path.GetTempPath(), "GF-W14-Silent-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(basis, "Application"), nested = Path.Combine(root, "uninstaller");
        Directory.CreateDirectory(nested);
        string fixture = Path.Combine(AppContext.BaseDirectory, "SyntheticSilent", "GF-SilentFixture.exe");
        string binary = Path.Combine(root, "application.exe"); File.Copy(fixture, binary);
        InstallerEngineEvidence.SyntheticUninstallBase = basis;
        try
        {
            foreach (string kind in new[] { "inno", "nullsoft" })
            {
                string exe = Path.Combine(nested, kind == "inno" ? "unins000.exe" : "uninstall.exe");
                File.Copy(fixture, exe, true);
                if (kind == "inno") File.WriteAllBytes(Path.ChangeExtension(exe, ".dat"),
                    System.Text.Encoding.ASCII.GetBytes("Inno Setup Uninstall Log (b)".PadRight(64, '\0')));
                else
                {
                    using var stream = new FileStream(exe, FileMode.Append);
                    byte[] header = new byte[512];
                    byte[] marker = [0xef,0xbe,0xad,0xde,0x4e,0x75,0x6c,0x6c,0x73,0x6f,0x66,0x74,0x49,0x6e,0x73,0x74]; marker.CopyTo(header, 4);
                    BitConverter.GetBytes(100u).CopyTo(header, 20); BitConverter.GetBytes(200u).CopyTo(header, 24); stream.Write(header);
                }
                ProgramEntry App(string name = "SumatraPDF 3.6.1 (64-bit x64)", string publisher = "GF Synthetic Tests") => new()
                { Name = name, Publisher = publisher, Version = "3.6.1", InstallLocation = root,
                    InventoryId = "LocalMachine|Registry64|GF-W14-Silent", UninstallString = "\"" + exe + "\"" };
                var app = App(); UninstallCommand.TryParse(app.UninstallString, out var command);
                InstallerEngineEvidence.SyntheticSigner = null;
                check(!InstallerEngineEvidence.GenericDesktopUninstaller(app, command, kind),
                    "W14 " + kind + " fixture sem assinatura real nao autoriza descoberta generica");
                // Explicitly simulated signature evidence. No cert is installed and
                // this hook is absent from the product. Real Authenticode is a
                // separate mandatory CI job; this proves the selection decision.
                InstallerEngineEvidence.SyntheticSigner = _ => (true, "GF Synthetic Tests");
                check(InstallerEngineEvidence.GenericDesktopUninstaller(app, command, kind),
                    "W14 " + kind + " produto e versao comprovados com desinstalador em subpasta; assinante simulado");
                var request = UninstallBatch.Select([app]).Single();
                check(SilentUninstallPolicy.UsesSilentMode(request) && request.Command.Arguments ==
                    (kind == "inno" ? "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" : "/S"),
                    "W14 " + kind + " flags locais documentadas sem comandos remotos");
                foreach (var wrong in new[] { App("SumatraPDF Preview"), App("SumatraPDF (32-bit x86)"),
                    App(publisher: "Different Vendor"), App("SumatraPDF Server") })
                    check(!InstallerEngineEvidence.GenericDesktopUninstaller(wrong, command, kind) &&
                        !SilentUninstallPolicy.TrySelect(wrong, command, out _),
                        "W14 " + kind + " recusa produto canal arquitetura fornecedor ou servidor divergente");
                InstallerEngineEvidence.SyntheticSigner = file => (true, file == exe ? "GF Synthetic Tests" : "Other Signer");
                check(!InstallerEngineEvidence.GenericDesktopUninstaller(app, command, kind),
                    "W14 " + kind + " assinaturas divergentes nao autorizam");
                InstallerEngineEvidence.SyntheticSigner = _ => (true, "GF Synthetic Tests");
                string second = Path.Combine(root, "duplicate.exe"); File.Copy(binary, second);
                check(!InstallerEngineEvidence.GenericDesktopUninstaller(app, command, kind),
                    "W14 " + kind + " dois executaveis da familia nao autorizam escolha arbitraria");
                File.Delete(second);
                var shared = new ProgramEntry { Name = "Other App", Publisher = "Other", Version = "1.0",
                    InventoryId = "LocalMachine|Registry64|Other", DisplayIcon = Path.Combine(root, "other.exe"),
                    UninstallString = "\"" + Path.Combine(root, "other-uninstall.exe") + "\"" };
                int starts = 0;
                var results = await new UninstallBatch().RunAsync([request], true,
                    () => Task.FromResult(new UninstallSnapshot([app, shared], true)),
                    (_, _) => { starts++; return Task.FromResult<int?>(0); }, _ => Task.CompletedTask);
                check(starts == 0 && results.Single().State == UninstallState.Blocked,
                    "W14 " + kind + " pasta compartilhada bloqueia inferencia silenciosa antes da execucao");
                File.Delete(exe); if (kind == "inno") File.Delete(Path.ChangeExtension(exe, ".dat"));
            }
        }
        finally
        {
            InstallerEngineEvidence.SyntheticSigner = null;
            InstallerEngineEvidence.SyntheticUninstallBase = null;
            Directory.Delete(basis, true);
        }
    }
}
