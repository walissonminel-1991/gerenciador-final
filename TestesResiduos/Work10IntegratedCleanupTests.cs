using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using GerenciadorAplicativos;
using Microsoft.Win32;

internal static class Work10IntegratedCleanupTests
{
    private static readonly Dictionary<string, ResidualScope> Scopes = new(StringComparer.Ordinal);
    private sealed class Fixture : IDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "GF-W10-" + Guid.NewGuid().ToString("N"));
        internal readonly string KeyPath = @"Software\GF-W10-" + Guid.NewGuid().ToString("N") + @"\Notepad++";
        internal readonly ProgramEntry App;
        internal readonly List<(RegistryHive Hive, RegistryView View)> Views = [];
        internal Fixture(params (RegistryHive Hive, RegistryView View)[] views)
        {
            Directory.CreateDirectory(Root);
            App = new() { Name = "PRIVATE_W10_MARKER_" + Guid.NewGuid().ToString("N"), Publisher = "Synthetic fixture",
                InventoryId = Guid.NewGuid().ToString("N"), InstallLocation = Root,
                UninstallString = "\"" + Path.Combine(Root, "UninstallFixture.exe") + "\"" };
            foreach (var view in views)
            {
                Views.Add(view);
                using var hive = RegistryKey.OpenBaseKey(view.Hive, view.View);
                using var key = hive.CreateSubKey(KeyPath); key.SetValue("Marker", "SYNTHETIC");
                using var nested = key.CreateSubKey("Nested"); nested.SetValue("Value", new[] { "a", "b" });
            }
            Scopes.Add(App.InventoryId, new("synthetic", [Root],
                views.Select(v => new ResidualKeyLocation(v.Hive, v.View, KeyPath)).ToArray(), []));
            IntegratedResidualPolicy.SyntheticScope = a => Scopes.TryGetValue(a.InventoryId, out var scope) ? scope : new("unsupported", [], [], []);
        }
        internal string File(string name, string value = "SYNTHETIC")
        { string path = Path.Combine(Root, name); System.IO.File.WriteAllText(path, value); return path; }
        internal ResidualUninstallContext Prepare() => IntegratedResidualCleanup.Prepare(App, new([App], true));
        internal IntegratedResidualPlan Plan() => IntegratedResidualCleanup.Capture(Prepare(), new([], true));
        internal bool KeyExists(RegistryHive hive, RegistryView view)
        { using var root = RegistryKey.OpenBaseKey(hive, view); using var key = root.OpenSubKey(KeyPath); return key is not null; }
        public void Dispose()
        {
            IntegratedResidualCleanup.BeforeTarget = null; ResidualRegistryGuard.BeforeCommit = null;
            Scopes.Remove(App.InventoryId);
            foreach (var view in Views)
            {
                using var root = RegistryKey.OpenBaseKey(view.Hive, view.View);
                root.DeleteSubKeyTree(KeyPath[..KeyPath.LastIndexOf('\\')], false);
            }
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
    private static Task<UninstallSnapshot> Empty() => Task.FromResult(new UninstallSnapshot([], true));
    private static UninstallOutcome Removed(ProgramEntry app) => new(new(app,
        new ParsedUninstallCommand(app.UninstallString.Trim('"'), "", null)), UninstallState.RegistrationAbsent, 0);
    private static Dictionary<string, ResidualUninstallContext> Contexts(params Fixture[] fixtures) =>
        fixtures.ToDictionary(f => f.App.InventoryId, f => f.Prepare());
    private static Task<ResidualElevationResponse?> NoElevation(IReadOnlyList<ResidualElevationRequest> _, CancellationToken __) =>
        Task.FromResult<ResidualElevationResponse?>(new("uac_declined", 1223, []));

    internal static async Task RunAsync(Action<bool, string> check, bool coreOnly = false)
    {
        if (!OperatingSystem.IsWindows()) { Console.WriteLine("SKIP W10 Windows required"); return; }
        try
        {
            RebootReviewAndTray(check);
            await Decisions(check);
            await RegistryViews(check);
            await MutationsAndCancellation(check);
            await SharingAndPrivacy(check);
            await LinkBoundaries(check);
            await Permissions(check);
            await WireAndPerformance(check);
            if (coreOnly) return;
            IntegratedResidualPolicy.SyntheticScope = null;
            await ActualUninstaller(check);
            await AuthenticatedWorker(check);
        }
        finally
        {
            IntegratedResidualPolicy.SyntheticScope = null;
            IntegratedResidualCleanup.BeforeTarget = null;
            ResidualRegistryGuard.BeforeCommit = null;
            Scopes.Clear();
        }
    }
    private static void RebootReviewAndTray(Action<bool, string> check)
    {
        string executable = @"C:\\Program Files\\Gerenciador Final\\GerenciadorFinal.exe";
        check(StartupTrayRegistration.Command(executable) ==
            "\"" + executable + "\" --tray" &&
            StartupTrayRegistration.Command(executable, recovery: true).EndsWith("--tray --post-reboot"),
            "W13 inicializacao na bandeja usa comando literal com aspas, sem shell");
        bool rejected = false;
        try { StartupTrayRegistration.Command(@"C:\GF.exe" + (char)34 + " --bad"); }
        catch (InvalidDataException) { rejected = true; }
        check(rejected, "W13 inicializacao recusa argumentos arbitrarios");

        long boot = RebootResidualReview.BootUtcTicks(DateTimeOffset.Parse("2026-10-23T15:00:00Z"), 3600_000);
        check(!RebootResidualReview.IsLaterBoot(boot,
                RebootResidualReview.BootUtcTicks(DateTimeOffset.Parse("2026-10-23T15:05:00Z"), 3900_000)) &&
            RebootResidualReview.IsLaterBoot(boot,
                RebootResidualReview.BootUtcTicks(DateTimeOffset.Parse("2026-10-23T16:00:00Z"), 120_000)),
            "W13 segunda conferencia exige boot diferente, nao reabertura da janela");

        check(RebootResidualReview.IsRetryable(new("f1", "file", ResidualItemState.Failed,
                "sharing_violation", 32)) &&
            !RebootResidualReview.IsRetryable(new("f2", "file", ResidualItemState.AccessDenied,
                "permission_required", 5)),
            "W13 somente bloqueios 32/33 sugerem reinicio, UAC erro 5 nao");

        var app = new ProgramEntry { InventoryId = "GF-TEST-PENDING", Name = "Synthetic Test" };
        var outcome = new UninstallOutcome(
            new UninstallRequest(app, new ParsedUninstallCommand("msiexec.exe", "/X{00000000-0000-0000-0000-000000000001}", "{00000000-0000-0000-0000-000000000001}")),
            UninstallState.Pending, 3010);
        var pending = RebootResidualReview.Select([outcome], null);
        check(RebootResidualReview.ProviderRequiresRestart(outcome) && pending.Length == 1 &&
            pending[0].ProviderPending && pending[0].Targets.Length == 0,
            "W13 codigo 3010 exige revisao sem alegar residuos eliminados");

        var wrong = outcome with { Code = 5 };
        check(!RebootResidualReview.ProviderRequiresRestart(wrong) &&
            RebootResidualReview.Select([wrong], null).Length == 0,
            "W13 acesso negado comum nao sugere reinicio infundado");

        byte[] original = System.Text.Encoding.UTF8.GetBytes("GF SYNTHETIC REBOOT CHECK");
        check(original.SequenceEqual(RebootResidualReview.TestRoundTrip(original)),
            "W13 DPAPI protege e restaura dados apenas no mesmo usuario Windows");
    }

    private static async Task Decisions(Action<bool, string> check)
    {
        using (var f = new Fixture())
        {
            string file = f.File("settings.cfg"); var context = f.Prepare(); int prompts = 0;
            var result = await IntegratedResidualBatch.RunAsync([Removed(f.App)], new Dictionary<string, ResidualUninstallContext> { [f.App.InventoryId] = context }, Empty,
                (plans, _) => { prompts++; check(System.IO.File.Exists(file), "W10 no deletion before YES"); return Task.FromResult(true); }, NoElevation, _ => { });
            check(prompts == 1 && result.Results.Single().Verified && result.Results.Single().Files == 1 &&
                !System.IO.File.Exists(file) && !Directory.Exists(f.Root), "W10 YES permanently removes real file and empty directory");
        }
        using (var f = new Fixture((RegistryHive.CurrentUser, RegistryView.Registry64)))
        {
            string file = f.File("settings.cfg"); string before = System.IO.File.ReadAllText(file); int prompts = 0, elevations = 0;
            var contexts = Contexts(f);
            var result = await IntegratedResidualBatch.RunAsync([Removed(f.App)], contexts, Empty,
                (_, _) => { prompts++; return Task.FromResult(false); }, (_, _) => { elevations++; return NoElevation([], default); }, _ => { });
            check(prompts == 1 && elevations == 0 && result.Declined && System.IO.File.Exists(file) && System.IO.File.ReadAllText(file) == before &&
                f.KeyExists(RegistryHive.CurrentUser, RegistryView.Registry64), "W10 NO preserves file, directory and registry contents");
        }
        using (var f = new Fixture())
        {
            string personal = f.File("personal.txt"); int prompts = 0;
            var result = await IntegratedResidualBatch.RunAsync([Removed(f.App)], Contexts(f), Empty,
                (_, _) => { prompts++; return Task.FromResult(true); }, NoElevation, _ => { });
            check(prompts == 0 && result.Offered == 0 && System.IO.File.Exists(personal), "W10 no eligible residue completes without prompt");
        }
        using (var f = new Fixture())
        {
            string file = f.File("settings.cfg"); int prompts = 0;
            using var cancellation = new CancellationTokenSource();
            var result = await IntegratedResidualBatch.RunAsync([Removed(f.App)], Contexts(f), Empty,
                (_, _) => { prompts++; return Task.FromResult(true); }, NoElevation,
                _ => cancellation.Cancel(), cancellation.Token);
            check(result.Cancelled && prompts == 0 && System.IO.File.Exists(file),
                "W10 unavailable audit sink cancels before consent and preserves every target");
        }
        using (var one = new Fixture())
        using (var two = new Fixture())
        using (var pending = new Fixture())
        {
            string a = one.File("one.cfg"), b = two.File("two.cfg"), p = pending.File("pending.cfg"); int prompts = 0;
            var result = await IntegratedResidualBatch.RunAsync([Removed(one.App), Removed(two.App), Removed(pending.App) with { State = UninstallState.Pending }],
                Contexts(one, two, pending), Empty,
                (plans, _) => { prompts++; check(plans.Count == 2, "W10 batch excludes pending removal from consent"); return Task.FromResult(true); }, NoElevation, _ => { });
            check(prompts == 1 && result.Results.Count == 2 && result.Results.All(r => r.Verified) &&
                !System.IO.File.Exists(a) && !System.IO.File.Exists(b) && System.IO.File.Exists(p), "W10 batch has one consent and separate effective results");
        }
    }
    private static async Task RegistryViews(Action<bool, string> check)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            using var f = new Fixture((hive, view)); var accepted = f.Plan();
            var trace = new List<string>();
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty, audit: trace.Add);
            string location = (hive == RegistryHive.LocalMachine ? "hklm" : "hkcu") + (view == RegistryView.Registry32 ? "32" : "64");
            check(accepted.Keys.Count == 1 && result.Keys == 1 && result.Verified && !f.KeyExists(hive, view) &&
                trace.Any(line => line.Contains("etapa=" + location + " ", StringComparison.Ordinal)),
                "W10 real transactional deletion " + hive + "/" + view);
        }
        using (var f = new Fixture((RegistryHive.LocalMachine, RegistryView.Registry32), (RegistryHive.LocalMachine, RegistryView.Registry64)))
        {
            var accepted = f.Plan(); var key32 = accepted.Keys.Single(k => k.View == RegistryView.Registry32);
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty, onlyIds: [key32.Id]);
            check(result.Keys == 1 && !f.KeyExists(RegistryHive.LocalMachine, RegistryView.Registry32) &&
                f.KeyExists(RegistryHive.LocalMachine, RegistryView.Registry64), "W10 Notepad++ equivalent HKLM32 deletion preserves identical HKLM64 path");
        }
        using (var f = new Fixture((RegistryHive.CurrentUser, RegistryView.Registry64)))
        {
            var accepted = f.Plan();
            using (var key = Registry.CurrentUser.OpenSubKey(f.KeyPath, true)) key!.SetValue("Marker", "NEW CONTENT");
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
            check(result.Keys == 0 && !result.Verified && f.KeyExists(RegistryHive.CurrentUser, RegistryView.Registry64), "W10 key changed after consent remains intact");
        }
        foreach (string concurrentSubkey in new[] { "", @"\Nested" })
        using (var f = new Fixture((RegistryHive.CurrentUser, RegistryView.Registry64)))
        {
            var accepted = f.Plan(); bool externalWriteBlocked = false;
            ResidualRegistryGuard.BeforeCommit = _ =>
            {
                try { using var key = Registry.CurrentUser.OpenSubKey(f.KeyPath + concurrentSubkey, true); key!.SetValue("Marker", "CONCURRENT CONTENT"); }
                catch (IOException) { externalWriteBlocked = true; throw; }
            };
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
            check(result.Keys == 0 && f.KeyExists(RegistryHive.CurrentUser, RegistryView.Registry64) && !result.Verified,
                "W10 concurrent registry write conflicts/rolls back instead of deleting changed tree " + concurrentSubkey + " (blocked=" + externalWriteBlocked + ")");
            ResidualRegistryGuard.BeforeCommit = null;
        }
    }
    private static async Task MutationsAndCancellation(Action<bool, string> check)
    {
        // Regressao real para erro 5: FileDispositionInfo legado nao remove
        // FILE_ATTRIBUTE_READONLY; a exclusao Ex nao deve mudar ACL/atributos.
        using (var f = new Fixture())
        {
            string file = f.File("settings.cfg", "READONLY FIXTURE");
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
            try
            {
                var plan = f.Plan();
                check(plan.Files.Count == 1, "W12 arquivo readonly permanece elegivel para conferencia por handle");
                var output = await IntegratedResidualCleanup.DeleteAsync(plan, true, Empty);
                check(output.Verified && output.Files == 1 && output.Directories == 1 &&
                    !File.Exists(file) && !Directory.Exists(f.Root),
                    "W12 readonly real e pasta vazia removidos sem alterar ACL");
            }
            finally
            {
                if (File.Exists(file)) File.SetAttributes(file, FileAttributes.Normal);
            }
        }
        using (var f = new Fixture())
        {
            string file = f.File("settings.cfg", "SAME CONTENT");
            var plan = f.Plan();
            // Atributo muda apos a captura, mas identidade e hash sao preservados.
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
            try
            {
                var output = await IntegratedResidualCleanup.DeleteAsync(plan, true, Empty);
                check(output.Files == 1 && output.Verified && !File.Exists(file),
                    "W12 readonly aplicado apos captura nao impede exclusao de conteudo identico");
            }
            finally
            {
                if (File.Exists(file)) File.SetAttributes(file, FileAttributes.Normal);
            }
        }
        using (var f = new Fixture())
        {
            string file = f.File("settings.cfg"); var accepted = f.Plan(); System.IO.File.WriteAllText(file, "NEW CONTENT");
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
            check(System.IO.File.ReadAllText(file) == "NEW CONTENT" && result.Files == 0 && !result.Verified,
                "W10 changed file is preserved after YES");
        }
        using (var f = new Fixture())
        {
            string file = f.File("settings.cfg", "ORIGINAL!"); var accepted = f.Plan();
            var timestamp = System.IO.File.GetLastWriteTimeUtc(file);
            System.IO.File.WriteAllText(file, "MODIFIED!"); System.IO.File.SetLastWriteTimeUtc(file, timestamp);
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
            check(System.IO.File.Exists(file) && System.IO.File.ReadAllText(file) == "MODIFIED!" && result.Files == 0,
                "W10 same metadata with changed content is protected by SHA256");
        }
        using (var f = new Fixture())
        {
            string file = f.File("settings.cfg"); var accepted = f.Plan();
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, () => Task.FromResult(new UninstallSnapshot([f.App], true)));
            check(System.IO.File.Exists(file) && result.Files == 0 && !result.Verified, "W10 reinstall before execution blocks every deletion");
        }
        using (var f = new Fixture())
        {
            string a = f.File("one.cfg"), b = f.File("two.cfg"); var accepted = f.Plan();
            string registration = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GF-W10-Reinstall-" + Guid.NewGuid().ToString("N");
            bool reinstalled = false;
            try
            {
                IntegratedResidualCleanup.BeforeTarget = id =>
                {
                    if (id != accepted.Files[1].Id) return;
                    using var key = Registry.CurrentUser.CreateSubKey(registration); key.SetValue("DisplayName", f.App.Name);
                    reinstalled = true; Thread.Sleep(20);
                };
                var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true,
                    () => Task.FromResult(new UninstallSnapshot(reinstalled ? [f.App] : [], true)));
                check(result.Files == 1 && System.IO.File.Exists(accepted.Files[1].Path) && !result.Verified,
                    "W10 OS registration notification stops deletion when reinstall occurs mid-operation");
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registration, false); IntegratedResidualCleanup.BeforeTarget = null; }
        }
        using (var f = new Fixture())
        {
            f.File("one.cfg"); f.File("two.cfg"); var accepted = f.Plan(); using var cancel = new CancellationTokenSource();
            IntegratedResidualCleanup.BeforeTarget = id => { if (id == accepted.Files[1].Id) cancel.Cancel(); };
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty, cancel.Token);
            check(result.Cancelled && result.Files == 1 && System.IO.File.Exists(accepted.Files[1].Path) && !result.Verified,
                "W10 cancellation reports partial deletion and preserves remaining targets");
        }
        using (var f = new Fixture())
        {
            f.File("one.cfg"); f.File("two.cfg"); var accepted = f.Plan();
            using var locked = new FileStream(accepted.Files[1].Path, FileMode.Open, FileAccess.Read, FileShare.None);
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
            check(result.Files == 1 && result.Failed > 0 && !result.Verified && System.IO.File.Exists(accepted.Files[1].Path),
                "W10 real sharing violation is a partial failure, not success");
        }
        using (var f = new Fixture())
        {
            f.File("settings.cfg"); var accepted = f.Plan(); string added = f.File("new-personal.txt");
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
            check(System.IO.File.Exists(added) && Directory.Exists(f.Root) && result.Directories == 0 && !result.Verified,
                "W10 new contents prevent recursive deletion of a directory");
        }
        using (var f = new Fixture())
        {
            f.File("settings.cfg"); var accepted = f.Plan(); string previous = f.Root + ".old";
            Directory.Move(f.Root, previous); Directory.CreateDirectory(f.Root); string replacement = f.File("settings.cfg", "NEW ROOT");
            try
            {
                var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
                check(System.IO.File.ReadAllText(replacement) == "NEW ROOT" && result.Files == 0 && !result.Verified,
                    "W10 replaced directory identity is preserved");
            }
            finally { Directory.Delete(previous, true); }
        }
        using (var f = new Fixture())
        {
            string original = f.File("settings.cfg"); var accepted = f.Plan(); string previous = f.Root + ".old";
            Directory.Move(f.Root, previous); Directory.CreateDirectory(f.Root);
            System.IO.File.Move(Path.Combine(previous, "settings.cfg"), original);
            try
            {
                var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
                check(System.IO.File.Exists(original) && result.Files == 0,
                    "W10 moved identical file in replaced directory requires original parent identity");
            }
            finally { Directory.Delete(previous, true); }
        }
    }
    private static async Task SharingAndPrivacy(Action<bool, string> check)
    {
        using (var f = new Fixture())
        {
            string config = f.File("settings.cfg"), personal = f.File("personal.pdf"), unknown = f.File("document.xml");
            Directory.CreateDirectory(Path.Combine(f.Root, "Backups")); string backup = f.File("Backups\\document.cfg");
            var plan = f.Plan(); var log = new List<string>();
            var result = await IntegratedResidualCleanup.DeleteAsync(plan, true, Empty, audit: log.Add);
            check(!System.IO.File.Exists(config) && System.IO.File.Exists(personal) && System.IO.File.Exists(unknown) &&
                System.IO.File.Exists(backup) && plan.Preserved == 3, "W10 personal files, unknown XML and backups survive effective cleanup");
            check(log.Count > 0 && log.All(line => !line.Contains(f.Root) && !line.Contains(f.App.Name) &&
                !line.Contains("SYNTHETIC") && !line.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase)),
                "W10 diagnostic contains technical identity and counts without private paths or file contents");
        }
        using (var f = new Fixture())
        {
            string file = f.File("settings.cfg");
            var other = new ProgramEntry { Name = "Other product", InventoryId = "other", InstallLocation = f.Root };
            var context = IntegratedResidualCleanup.Prepare(f.App, new([f.App, other], true));
            var plan = IntegratedResidualCleanup.Capture(context, new([other], true));
            check(plan.Eligible == 0 && System.IO.File.Exists(file), "W10 shared installation root is not eligible");
        }
        using (var f = new Fixture())
        {
            Directory.CreateDirectory(Path.Combine(f.Root, "steamapps"));
            string game = f.File("steamapps\\game.cfg"); f.File("settings.cfg");
            var result = await IntegratedResidualCleanup.DeleteAsync(f.Plan(), true, Empty);
            check(result.Files == 1 && System.IO.File.Exists(game), "W10 game-library directory remains preserved inside an application root");
        }
        using (var f = new Fixture())
        {
            string outside = Path.Combine(Path.GetTempPath(), "GF-W10-Hardlink-" + Guid.NewGuid().ToString("N") + ".cfg");
            System.IO.File.WriteAllText(outside, "SHARED");
            try
            {
                check(CreateHardLinkW(Path.Combine(f.Root, "linked.cfg"), outside, IntPtr.Zero), "W10 hardlink fixture created");
                var accepted = f.Plan(); var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
                check(accepted.Files.Count == 0 && System.IO.File.ReadAllText(outside) == "SHARED" && result.Files == 0,
                    "W10 shared hardlink is preserved");
            }
            finally { System.IO.File.Delete(outside); }
        }
        using (var f = new Fixture())
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments, Environment.SpecialFolderOption.DoNotVerify);
            string root = Path.Combine(documents, "GF-W10-Personal-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string personal = Path.Combine(root, "settings.cfg"); System.IO.File.WriteAllText(personal, "PERSONAL");
            try
            {
                Scopes[f.App.InventoryId] = new("synthetic", [root], [], []);
                var context = f.Prepare(); var plan = IntegratedResidualCleanup.Capture(context, new([], true));
                check(plan.Eligible == 0 && System.IO.File.Exists(personal), "W10 redirected personal directory never becomes cleanup authority");
            }
            finally { Directory.Delete(root, true); }
        }
    }
    private static async Task Permissions(Action<bool, string> check)
    {
        using var f = new Fixture((RegistryHive.CurrentUser, RegistryView.Registry64));
        var accepted = f.Plan(); using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        RegistrySecurity original;
        using (var key = root.OpenSubKey(f.KeyPath, true))
        {
            original = key!.GetAccessControl(); var blocked = key.GetAccessControl();
            blocked.AddAccessRule(new RegistryAccessRule(WindowsIdentity.GetCurrent().User!,
                RegistryRights.SetValue, AccessControlType.Deny)); key.SetAccessControl(blocked);
        }
        void RestoreRights(RegistryKey key)
        {
            var restored = new RegistrySecurity();
            restored.SetSecurityDescriptorBinaryForm(original.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
            key.SetAccessControl(restored);
        }
        try
        {
            var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
            check(result.ElevationIds.Length == 1 && result.Keys == 0 && f.KeyExists(RegistryHive.CurrentUser, RegistryView.Registry64),
                "W10 actual permission denial requests elevation and preserves key");
            int attempts = 0; var logs = new List<string>();
            var batch = await IntegratedResidualBatch.RunAsync([Removed(f.App)], Contexts(f), Empty,
                (_, _) => Task.FromResult(true), (_, _) => { attempts++; return NoElevation([], default); }, logs.Add);
            check(attempts == 1 && !batch.Results.Single().Verified && f.KeyExists(RegistryHive.CurrentUser, RegistryView.Registry64) &&
                batch.Results.Single().Problems.Any(p => p.Error == 1223), "W10 UAC refusal preserves denied target and records 1223");
            attempts = 0;
            batch = await IntegratedResidualBatch.RunAsync([Removed(f.App)], Contexts(f), Empty,
                (_, _) => Task.FromResult(true), async (requests, token) =>
                {
                    attempts++;
                    using (var key = root.OpenSubKey(f.KeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions)) RestoreRights(key!);
                    var results = new List<IntegratedCleanupResult>();
                    foreach (var request in requests)
                        results.Add(await IntegratedResidualCleanup.DeleteAsync(request.Plan, true, Empty, token, request.TargetIds));
                    return new ResidualElevationResponse("uac_authorized", 0, results);
                }, logs.Add);
            check(attempts == 1 && batch.Results.Single().Keys == 1 && batch.Results.Single().Verified &&
                !f.KeyExists(RegistryHive.CurrentUser, RegistryView.Registry64),
                "W10 authorized elevation retries only the denied key and merges actual deletion (ACL restored only in disposable fixture)");
        }
        finally
        {
            using var key = root.OpenSubKey(f.KeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions);
            if (key is not null) RestoreRights(key);
        }
    }
    private static async Task LinkBoundaries(Action<bool, string> check)
    {
        using (var f = new Fixture())
        {
            string target = Path.Combine(Path.GetTempPath(), "GF-W10-Link-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(target); string personal = Path.Combine(target, "settings.cfg");
            System.IO.File.WriteAllText(personal, "LINK TARGET KEEP");
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(f.Root, "LinkedFolder"), target);
                f.File("normal.cfg"); var accepted = f.Plan();
                var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
                check(result.Files == 1 && System.IO.File.ReadAllText(personal) == "LINK TARGET KEEP" &&
                    accepted.Preserved > 0 && Directory.Exists(f.Root), "W10 directory symlink is not traversed or deleted");
            }
            finally { Directory.Delete(Path.Combine(f.Root, "LinkedFolder")); Directory.Delete(target, true); }
        }
        using (var f = new Fixture((RegistryHive.CurrentUser, RegistryView.Registry64)))
        {
            string ownParent = f.KeyPath[..f.KeyPath.LastIndexOf('\\')];
            string destination = @"Software\GF-W10-LinkTarget-" + Guid.NewGuid().ToString("N");
            using (var key = Registry.CurrentUser.CreateSubKey(destination + @"\Notepad++")) key.SetValue("Marker", "REGISTRY LINK TARGET KEEP");
            Registry.CurrentUser.DeleteSubKeyTree(ownParent, false);
            IntPtr handle = IntPtr.Zero;
            try
            {
                int created = RegCreateKeyExW(Registry.CurrentUser.Handle.DangerousGetHandle(), ownParent, 0, null,
                    2, 0x2001F, IntPtr.Zero, out handle, out _);
                check(created == 0, "W10 registry symbolic-link fixture created");
                byte[] bytes = Encoding.Unicode.GetBytes(@"\Registry\User\" + WindowsIdentity.GetCurrent().User!.Value + "\\" + destination);
                check(RegSetValueExW(handle, "SymbolicLinkValue", 0, 6, bytes, bytes.Length) == 0,
                    "W10 registry symbolic-link target set");
                RegCloseKey(handle); handle = IntPtr.Zero;
                var context = f.Prepare(); var accepted = IntegratedResidualCleanup.Capture(context, new([], true));
                var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, Empty);
                using var kept = Registry.CurrentUser.OpenSubKey(destination + @"\Notepad++");
                check(accepted.Keys.Count == 0 && result.Keys == 0 && (string?)kept?.GetValue("Marker") == "REGISTRY LINK TARGET KEEP",
                    "W10 registry link in an intermediate path never authorizes target deletion");
            }
            finally
            {
                if (handle != IntPtr.Zero) RegCloseKey(handle);
                // RegDeleteKey supprime o link, sem percorrer seu destino.
                RegDeleteKeyW(Registry.CurrentUser.Handle.DangerousGetHandle(), ownParent);
                Registry.CurrentUser.DeleteSubKeyTree(destination, false);
            }
        }
    }
    private static async Task WireAndPerformance(Action<bool, string> check)
    {
        using var f = new Fixture();
        for (int i = 0; i < 120; i++) f.File("config" + i + ".cfg", "");
        var accepted = f.Plan();
        var request = new ResidualElevationRequest(accepted, [accepted.Files[0].Id]);
        check(ResidualElevatedProtocol.ValidRequests([request]), "W10 bounded worker accepts the authorized scope");
        var foreign = accepted with { Files = [accepted.Files[0] with { Path = @"C:\Windows\System32\kernel32.dll" }] };
        check(!ResidualElevatedProtocol.ValidRequests([request with { Plan = foreign }]), "W10 worker rejects arbitrary deletion path");
        using var stream = new MemoryStream(); byte[] header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, int.MaxValue); stream.Write(header); stream.Position = 0;
        bool blocked = false; try { await ResidualElevatedProtocol.ReadFrameAsync<ResidualElevationRequest[]>(stream, default); } catch (InvalidDataException) { blocked = true; }
        check(blocked, "W10 oversized IPC frame is rejected before allocation");
        int scans = 0; var clock = Stopwatch.StartNew();
        var result = await IntegratedResidualCleanup.DeleteAsync(accepted, true, () => { scans++; return Empty(); });
        check(result.Files == 120 && result.Verified && scans is >= 2 and <= 6 && clock.Elapsed < TimeSpan.FromSeconds(60),
            $"W10 performance: 120 real files, {scans} inventories, {clock.ElapsedMilliseconds} ms");
    }

    // Fixture executavel real em Local\\Programs, com identidade/ARP descartaveis.
    private static (ProgramEntry App, string Leaf, string Root) CreateActualFixture()
    {
        string id = Guid.NewGuid().ToString("N"), leaf = "GF-W10-Fixture-" + id, name = "GF W10 Fixture " + id;
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", name);
        Directory.CreateDirectory(folder);
        string self = Environment.ProcessPath ?? throw new IOException("Apphost missing");
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) throw new IOException("Run tests through their apphost");
        System.IO.File.Copy(self, Path.Combine(folder, "UninstallFixture.exe"));
        foreach (string extension in new[] { ".dll", ".runtimeconfig.json", ".deps.json" })
            System.IO.File.Copy(Path.Combine(AppContext.BaseDirectory, "TestesResiduos" + extension), Path.Combine(folder, "TestesResiduos" + extension));
        var app = new ProgramEntry { Name = name, Publisher = "GF synthetic fixture", InventoryId = "CurrentUser|Registry64|" + leaf,
            InstallLocation = folder, UninstallString = "\"" + Path.Combine(folder, "UninstallFixture.exe") + "\" --synthetic-uninstall " + leaf };
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + leaf);
        key.SetValue("DisplayName", app.Name); key.SetValue("Publisher", app.Publisher); key.SetValue("InstallLocation", folder);
        key.SetValue("UninstallString", app.UninstallString);
        System.IO.File.WriteAllText(Path.Combine(folder, "settings.cfg"), "RESIDUE");
        return (app, leaf, folder);
    }
    private static void ClearActual((ProgramEntry App, string Leaf, string Root) fixture)
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + fixture.Leaf, false);
        if (Directory.Exists(fixture.Root)) Directory.Delete(fixture.Root, true);
    }
    internal static int UninstallFixture(string leaf)
    {
        if (!Regex.IsMatch(leaf, "^GF-W10-Fixture-[a-f0-9]{32}$")) return 9;
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + leaf, false);
        return 0;
    }
    private static async Task ActualUninstaller(Action<bool, string> check)
    {
        var f = CreateActualFixture();
        try
        {
            Task<UninstallSnapshot> Inventory()
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + f.Leaf);
                return Task.FromResult(new UninstallSnapshot(key is null ? [] : [f.App], true));
            }
            var before = await Inventory(); var context = IntegratedResidualCleanup.Prepare(f.App, before);
            var requests = UninstallBatch.Select([f.App]);
            using var prepared = RegisteredUninstaller.Prepare(requests.Single(), (_, _) => false, (_, _) => "WinVerifyTrust=0x800B0100");
            var outcomes = await new UninstallBatch().RunAsync(requests, true, Inventory,
                (_, token) => prepared.LaunchAsync(token), _ => Task.CompletedTask);
            check(outcomes.Single().State == UninstallState.RegistrationAbsent, "W10 real PE uninstaller exits and removes its synthetic ARP registration");
            int prompts = 0;
            var result = await IntegratedResidualBatch.RunAsync(outcomes, new Dictionary<string, ResidualUninstallContext> { [f.App.InventoryId] = context }, Inventory,
                (_, _) => { prompts++; return Task.FromResult(true); }, NoElevation, Console.WriteLine);
            check(prompts == 1 && result.Results.Single().Verified && !Directory.Exists(f.Root),
                "W10 end-to-end: actual child process -> confirmed removal -> YES -> all synthetic files disappear");
        }
        finally { ClearActual(f); }
    }
    internal static async Task<int> WorkerFixture(string pipeName, string token)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25)); await pipe.ConnectAsync(deadline.Token);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
        await writer.WriteLineAsync("HELLO|" + token);
        return await ResidualElevatedProtocol.ServeAsync(pipe, Empty);
    }
    private static async Task AuthenticatedWorker(Action<bool, string> check)
    {
        var f = CreateActualFixture();
        try
        {
            var context = IntegratedResidualCleanup.Prepare(f.App, new([f.App], true)); UninstallFixture(f.Leaf);
            var plan = IntegratedResidualCleanup.Capture(context, new([], true));
            var requests = new[] { new ResidualElevationRequest(plan, plan.Files.Select(x => x.Id).Concat(plan.Directories.Select(x => x.Id)).ToArray()) };
            string name = "GA5122_" + Guid.NewGuid().ToString("N"), token = Guid.NewGuid().ToString("N");
            using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            start.ArgumentList.Add("--residual-worker-test"); start.ArgumentList.Add(name); start.ArgumentList.Add(token);
            using var child = Process.Start(start)!; using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await pipe.WaitForConnectionAsync(deadline.Token);
            check(PipePeerVerifier.IsExpectedClient(pipe, child.Id), "W10 worker client PID authenticated by kernel");
            using var reader = new StreamReader(pipe, Encoding.UTF8, true, 4096, true);
            check(await reader.ReadLineAsync(deadline.Token) == "HELLO|" + token, "W10 worker session token matches");
            var response = await ResidualElevatedProtocol.ParentAsync(pipe, requests, default);
            await child.WaitForExitAsync(deadline.Token);
            check(child.ExitCode == 0 && response.State == "uac_authorized" && response.Results.Single().Verified && !Directory.Exists(f.Root),
                "W10 authenticated separate administrative process deletes real synthetic files (runner already elevated; no interactive UAC claim)");
        }
        finally { ClearActual(f); }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string path, string existing, IntPtr security);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegCreateKeyExW(IntPtr key, string subkey, int reserved, string? keyClass,
        int options, int access, IntPtr security, out IntPtr result, out int disposition);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegSetValueExW(IntPtr key, string name, int reserved, int type, byte[] data, int size);
    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern int RegCloseKey(IntPtr key);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegDeleteKeyW(IntPtr key, string name);
}
