using GerenciadorAplicativos;

internal static class Work7UninstallTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        check(InventoryReconciler.UpdateInstallLocation(new ProgramEntry {
            UninstallString = "\"C:\\Program Files\\7-Zip\\Uninstall.exe\"" }) == @"C:\Program Files\7-Zip",
            "W7-ID pasta omitida no ARP usa somente EXE absoluto registrado, para correlação consultiva");
        foreach (string command in new[] { "msiexec.exe /X{00000000-0000-0000-0000-000000000781}",
            @"..\7-Zip\uninstall.exe", @"\\server\share\uninstall.exe", "cmd.exe /c fixture" })
            check(InventoryReconciler.UpdateInstallLocation(new ProgramEntry { UninstallString = command }) == "",
                "W7-ID comando sem pasta exclusiva verificável não vira diretório de instalação");
        ProgramEntry App(string id) => new() { Name = "Fixture " + id, Publisher = "Fixture",
            InventoryId = "CurrentUser|Registry64|" + id,
            MsiProductCode = id == "A" ? "{00000000-0000-0000-0000-000000000781}" : "{00000000-0000-0000-0000-000000000782}",
            UninstallString = "msiexec.exe /X" + (id == "A" ? "{00000000-0000-0000-0000-000000000781}" : "{00000000-0000-0000-0000-000000000782}") };
        var app = App("A");
        var selected = UninstallBatch.Select([app]);
        var present = new UninstallSnapshot([app], true);
        var absent = new UninstallSnapshot([], true);
        Task Wait(CancellationToken _) => Task.CompletedTask;
        int scans = 0, launches = 0;
        async Task<UninstallOutcome> Scenario(int code, params UninstallSnapshot[] snapshots)
        {
            scans = launches = 0;
            return (await new UninstallBatch().RunAsync(selected, true,
                () => Task.FromResult(snapshots[Math.Min(scans++, snapshots.Length - 1)]),
                (_, _) => { launches++; return Task.FromResult<int?>(code); }, Wait)).Single();
        }
        var result = await Scenario(2, present, absent);
        check(result.State == UninstallState.Pending && result.Code == 2 && scans >= 3 && launches == 1,
            "W7-UN erro com ausência posterior fica pendente; não declara fracasso nem autoriza limpeza");
        result = await Scenario(2, present);
        check(result.State == UninstallState.ProcessFailed && scans > 1 && launches == 1,
            "W7-UN código não zero com presença persistente confirma falha após consultar estado");
        result = await Scenario(2, present, new UninstallSnapshot([], false));
        check(result.State == UninstallState.Pending && launches == 1,
            "W7-UN código não zero e inventário parcial não prova falha definitiva");
        result = await Scenario(3010, present, absent);
        check(result.State == UninstallState.Pending && result.Code == 3010,
            "W7-UN reinicialização necessária não autoriza limpeza antecipada");
        result = await Scenario(0, present, absent, absent, present);
        check(result.State == UninstallState.Pending,
            "W7-UN duas ausências transitórias seguidas de retorno não confirmam remoção");
        result = await Scenario(0, present, present, absent);
        check(result.State == UninstallState.RegistrationAbsent && launches == 1,
            "W7-UN ausência estável após sucesso confirma somente registro ausente");
        scans = 0;
        result = (await new UninstallBatch().RunAsync(selected, true,
            () => ++scans == 1 ? Task.FromResult(present) : throw new IOException("fixture"),
            (_, _) => Task.FromResult<int?>(0), Wait)).Single();
        check(result.State == UninstallState.Pending,
            "W7-UN falha de leitura após início do fornecedor conserva operação pendente");
        var second = App("B");
        var both = new UninstallSnapshot([app, second], true);
        launches = 0;
        var batch = await new UninstallBatch().RunAsync(UninstallBatch.Select([app, second]), true,
            () => Task.FromResult(both), (_, _) => { launches++; return Task.FromResult<int?>(0); }, Wait);
        check(batch.Count == 1 && batch[0].State == UninstallState.Pending && launches == 1,
            "W7-UN lote não inicia outro fornecedor enquanto o primeiro está pendente");
        check(!UninstallerSignaturePolicy.IsUntrustedButNotProvenTampered("WinVerifyTrust=0x800B0111"),
            "W7-SIGN desconfiança explícita do Windows bloqueia execução");
        check(!UninstallerSignaturePolicy.IsUntrustedButNotProvenTampered("WinVerifyTrust=0x80004005"),
            "W7-SIGN erro não classificado não recebe permissão por omissão");
        foreach (var (code, expected) in new[] { (1602, UninstallState.Cancelled),
            (1618, UninstallState.Pending), (1641, UninstallState.Pending) })
        {
            launches = 0;
            var special = await new UninstallBatch().RunAsync(UninstallBatch.Select([app, second]), true,
                () => Task.FromResult(both), (_, _) => { launches++; return Task.FromResult<int?>(code); }, Wait);
            check(special.Count == 1 && special[0].State == expected && special[0].Code == code && launches == 1,
                "W7-MSI cancelamento/ocupado/reinício não inicia o próximo item, código " + code);
        }
        var exe = new ProgramEntry { Name = "Fixture EXE", Publisher = "Fixture", InventoryId = "CurrentUser|Registry64|EXE",
            UninstallString = "\"C:\\GF-Fixture\\uninstall.exe\"" };
        if (!UninstallCommand.TryParse(exe.UninstallString, out var parsedExe)) throw new Exception("Fixture EXE inválida");
        var delayed = await new UninstallBatch().RunAsync([new(exe, parsedExe)], true,
            () => Task.FromResult(new UninstallSnapshot([exe], true)), (_, _) => Task.FromResult<int?>(2), Wait);
        check(delayed.Single().State == UninstallState.Pending && delayed.Single().Code == 2,
            "W7-EXE código desconhecido com registro persistente não comprova fracasso do processo filho");
        var alias = new ProgramEntry { Name = "Fixture outro registro", Publisher = exe.Publisher,
            InventoryId = "CurrentUser|Registry64|EXE-ALIAS", UninstallString = exe.UninstallString };
        launches = 0;
        var shared = await new UninstallBatch().RunAsync([new(exe, parsedExe)], true,
            () => Task.FromResult(new UninstallSnapshot([exe, alias], true)),
            (_, _) => { launches++; return Task.FromResult<int?>(0); }, Wait);
        check(shared.Single().State == UninstallState.Blocked && launches == 0,
            "W7-EXE dois registros com o mesmo comando não autorizam remover a instalação errada");
        var separateProduct = new ProgramEntry { Name = "Fixture outro produto", Publisher = exe.Publisher,
            InventoryId = "CurrentUser|Registry64|EXE-OTHER-PRODUCT", UninstallString = exe.UninstallString + " /product:other" };
        bool removed = false; launches = 0;
        var independent = await new UninstallBatch().RunAsync([new(exe, parsedExe)], true,
            () => Task.FromResult(new UninstallSnapshot(removed ? [separateProduct] : [exe, separateProduct], true)),
            (_, _) => { removed = true; launches++; return Task.FromResult<int?>(0); }, Wait);
        check(independent.Single().State == UninstallState.RegistrationAbsent && launches == 1,
            "W7-EXE comando de outro produto no mesmo executável não bloqueia remoção legítima");
        // Reproduz falha real vista no PC FORTE: UninstallString tem GUI
        // e QuietUninstallString do NSIS oferece /S. Nenhum EXE e executado.
        string quietRoot = Path.Combine(Path.GetTempPath(), "GF-W7-Quiet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(quietRoot);
        try
        {
            string executable = Path.Combine(quietRoot, "uninstall.exe");
            string otherExecutable = Path.Combine(quietRoot, "uninstall-other.exe");
            File.Copy(Environment.ProcessPath ?? throw new IOException("Fixture Windows sem apphost"), executable);
            File.Copy(executable, otherExecutable);
            string normalCommand = "\"" + executable + "\"";
            var supportsQuiet = new ProgramEntry
            {
                Name = "Fixture NSIS", Publisher = "Fixture",
                InventoryId = "CurrentUser|Registry64|FixtureQuiet",
                UninstallString = normalCommand,
                QuietUninstallString = normalCommand + " /S"
            };
            var quietRequest = UninstallBatch.Select([supportsQuiet]).Single();
            check(quietRequest.Command.Arguments == "/S" &&
                quietRequest.Command.FileName.Equals(executable, StringComparison.OrdinalIgnoreCase) &&
                SilentUninstallPolicy.MatchesAuthorized(supportsQuiet, quietRequest.Command),
                "W7-QUIET comando quiet registrado prevalece sobre desinstalador interativo");

            var wrongQuiet = new ProgramEntry
            {
                Name = supportsQuiet.Name, Publisher = supportsQuiet.Publisher,
                InventoryId = supportsQuiet.InventoryId,
                UninstallString = normalCommand,
                QuietUninstallString = "\"" + otherExecutable + "\" /S"
            };
            var normalRequest = UninstallBatch.Select([wrongQuiet]).Single();
            check(normalRequest.Command.Arguments == "" &&
                normalRequest.Command.FileName.Equals(executable, StringComparison.OrdinalIgnoreCase),
                "W7-QUIET campo quiet nao pode trocar o EXE do programa");

            var noQuiet = new ProgramEntry
            {
                Name = "Unlocker 1.9.2", Publisher = "Unknown",
                InventoryId = "CurrentUser|Registry64|UnlockerSynthetic",
                UninstallString = normalCommand
            };
            check(UninstallBatch.Select([noQuiet]).Single().Command.Arguments == "",
                "W7-QUIET sem contrato do fabricante nao inventa flags para Unlocker");
        }
        finally { Directory.Delete(quietRoot, recursive: true); }
        check(UninstallCommand.TryParse("msiexec.exe /X" + app.MsiProductCode + " /qn", out var noRestart) &&
            noRestart.Arguments.Contains("/norestart", StringComparison.OrdinalIgnoreCase),
            "W7-MSI comando silencioso registrado não autoriza reinicialização automática");
    }
}
