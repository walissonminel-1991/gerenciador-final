using GerenciadorAplicativos;
using Microsoft.Win32;

internal static class Work9ResidualTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        if (!OperatingSystem.IsWindows())
        { Console.WriteLine("SKIP W9-R: Registro, locks e desinstalacao exigem Windows"); return; }
        await LateUninstallCancellation(check);
        string fixture = Path.Combine(Path.GetTempPath(), "GF-W9-Residual-" + Guid.NewGuid().ToString("N"));
        try
        {
            SmallAppResidualCleanup.SyntheticFolders = [fixture];
            SmallAppResidualCleanup.SyntheticNoRegistry = true;
            Directory.CreateDirectory(fixture);
            string file = Path.Combine(fixture, "empty.conf");
            File.WriteAllBytes(file, []);
            var empty = SmallAppResidualCleanup.Capture("Notepad++", true, []);
            check(empty.Complete && empty.Files.Count == 1 && empty.Files[0].Bytes == 0,
                "W9-R1 arquivo de configuracao vazio e valido na previa residual");
            File.Delete(file);
            file = Path.Combine(fixture, "settings.conf");
            File.WriteAllText(file, "accepted");
            var accepted = SmallAppResidualCleanup.Capture("Notepad++", true, []);
            bool replacementBlocked = false;
            SmallAppResidualCleanup.SyntheticBeforeFileDelete = path =>
            {
                try { File.Move(path, path + ".old"); File.WriteAllText(path, "NEW DATA"); }
                catch (IOException) { replacementBlocked = true; }
            };
            await SmallAppResidualCleanup.DeleteAsync(accepted, Empty);
            check(replacementBlocked || (File.Exists(file) && File.ReadAllText(file) == "NEW DATA"),
                "W9-R2 troca do arquivo entre hash e exclusao nao apaga conteudo novo");
            SmallAppResidualCleanup.SyntheticBeforeFileDelete = null;
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
            Directory.CreateDirectory(fixture);
            File.WriteAllText(file, "accepted");
            accepted = SmallAppResidualCleanup.Capture("Notepad++", true, []);
            using var cancel = new CancellationTokenSource();
            int scans = 0;
            bool cancelled = false;
            try
            {
                await SmallAppResidualCleanup.DeleteAsync(accepted, () =>
                { if (++scans == 2) cancel.Cancel(); return Empty(); }, cancel.Token);
            }
            catch (OperationCanceledException) { cancelled = true; }
            check(cancelled && File.Exists(file),
                "W9-R3 cancelamento durante inventario anterior ao arquivo preserva o alvo");
        }
        finally
        {
            SmallAppResidualCleanup.SyntheticFolders = null;
            SmallAppResidualCleanup.SyntheticNoRegistry = false;
            SmallAppResidualCleanup.SyntheticBeforeFileDelete = null;
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            await RegistryCases(hive, view, check);
    }

    private static Task<(bool Complete, IReadOnlyCollection<ProgramEntry> Apps)> Empty() =>
        Task.FromResult((true, (IReadOnlyCollection<ProgramEntry>)Array.Empty<ProgramEntry>()));

    private static async Task LateUninstallCancellation(Action<bool, string> check)
    {
        var app = new ProgramEntry { Name = "GF W9 synthetic app", InventoryId = "W9-SYNTHETIC",
            UninstallString = "\"C:\\GF-W9-Synthetic\\uninstall.exe\"" };
        if (!UninstallCommand.TryParse(app.UninstallString, out var command)) throw new Exception("Fixture invalida");
        using var cancel = new CancellationTokenSource();
        int scans = 0, launches = 0;
        var results = await new UninstallBatch().RunAsync([new(app, command)], true,
            () =>
            {
                scans++;
                if (scans == 8) cancel.Cancel(); // ultima pos-conferencia, apos sete ausencias
                return Task.FromResult(new UninstallSnapshot(scans == 1 ? [app] : [], true));
            }, (_, _) => { launches++; return Task.FromResult<int?>(0); },
            _ => Task.CompletedTask, cancel.Token);
        check(results.Single().State == UninstallState.Pending && launches == 1,
            "W9-UN cancelamento na ultima pos-conferencia deixa resultado pendente");
    }

    private static async Task RegistryCases(RegistryHive hive, RegistryView view, Action<bool, string> check)
    {
        string parent = @"Software\GF-W9-Synthetic-" + Guid.NewGuid().ToString("N");
        string subkey = parent + @"\Notepad++";
        string label = hive + "/" + view;
        using var root = RegistryKey.OpenBaseKey(hive, view);
        try
        {
            using (var own = root.CreateSubKey(subkey))
            { own.CreateSubKey("A").Dispose(); own.CreateSubKey("B").Dispose(); }
            using (var sibling = root.CreateSubKey(parent + @"\Preserve")) sibling.SetValue("Marker", "KEEP");
            SmallAppResidualCleanup.SyntheticFolders = [Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))];
            SmallAppResidualCleanup.SyntheticNoRegistry = false;
            SmallAppResidualCleanup.SyntheticRegistryKeys = [(hive, view, subkey)];
            var accepted = SmallAppResidualCleanup.Capture("Notepad++", true, []);
            using (var own = root.OpenSubKey(subkey, true))
            { own!.DeleteSubKeyTree("B"); own.CreateSubKey(@"A\B").Dispose(); }
            var changed = SmallAppResidualCleanup.Capture("Notepad++", true, []);
            check(accepted.Complete && changed.Complete && accepted.Keys.Single().Digest != changed.Keys.Single().Digest,
                "W9-R4 hierarquia do Registro altera fingerprint " + label);
            var outcome = await SmallAppResidualCleanup.DeleteAsync(accepted, Empty);
            using (var surviving = root.OpenSubKey(subkey))
                check(outcome.Keys == 0 && outcome.Failed > 0 && surviving is not null,
                    "W9-R4 arvore reorganizada apos previa deve ser preservada " + label);
            using (var own = root.CreateSubKey(subkey))
                own.SetValue("Multi", new[] { "a\u001fb", "c" }, RegistryValueKind.MultiString);
            accepted = SmallAppResidualCleanup.Capture("Notepad++", true, []);
            using (var own = root.OpenSubKey(subkey, true))
                own!.SetValue("Multi", new[] { "a", "b\u001fc" }, RegistryValueKind.MultiString);
            changed = SmallAppResidualCleanup.Capture("Notepad++", true, []);
            check(accepted.Keys.Single().Digest != changed.Keys.Single().Digest,
                "W9-R4 fronteiras de REG_MULTI_SZ alteram fingerprint " + label);
            outcome = await SmallAppResidualCleanup.DeleteAsync(changed, Empty);
            using var removed = root.OpenSubKey(subkey);
            using var kept = root.OpenSubKey(parent + @"\Preserve");
            check(outcome.Keys == 1 && outcome.Failed == 0 && removed is null &&
                (string?)kept?.GetValue("Marker") == "KEEP",
                "W9-R5 excluir somente folha da visao autorizada e preservar irma " + label);
        }
        finally
        {
            SmallAppResidualCleanup.SyntheticFolders = null;
            SmallAppResidualCleanup.SyntheticRegistryKeys = null;
            SmallAppResidualCleanup.SyntheticNoRegistry = false;
            using var software = root.OpenSubKey("Software", true);
            software!.DeleteSubKeyTree(parent[(parent.LastIndexOf('\\') + 1)..], false);
        }
    }
}
