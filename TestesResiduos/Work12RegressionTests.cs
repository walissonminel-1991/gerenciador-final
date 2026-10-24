using System.Security.AccessControl;
using System.Security.Principal;
using GerenciadorAplicativos;
using Microsoft.Win32;

// Somente alvos com GUID criados pelo proprio teste; nunca aplicativos reais.
internal static class Work12RegressionTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("WORK12 requires Windows");
        string id = "GF-W12-" + Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), id);
        string keyPath = @"Software\" + id;
        Directory.CreateDirectory(root);
        var app = new ProgramEntry { Name = id, Publisher = "GF Synthetic", InventoryId = id,
            InstallLocation = root, UninstallString = "\"" + Path.Combine(root, "uninstall.exe") + "\"" };
        var address = new ResidualKeyLocation(RegistryHive.CurrentUser, RegistryView.Registry64, keyPath);
        IntegratedResidualPolicy.SyntheticScope = _ => new("synthetic", [root], [address], []);
        Task<UninstallSnapshot> Empty() => Task.FromResult(new UninstallSnapshot([], true));
        var removed = new UninstallOutcome(new(app, new(Path.Combine(root, "uninstall.exe"), "", null)), UninstallState.RegistrationAbsent, 0);
        try
        {
            // A chave nao existia antes, mas o desinstalador deixou configuracao nela.
            var context = IntegratedResidualCleanup.Prepare(app, new([app], true));
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath)) key.SetValue("InstallPath", root);
            var plan = IntegratedResidualCleanup.Capture(context, new([], true));
            check(plan.Keys.Count == 1, "W12 chave da politica criada pelo desinstalador precisa ser encontrada");
            Registry.CurrentUser.DeleteSubKeyTree(keyPath);

            // Reproduzir .56: erro 5 no preflight nunca equivale a ausencia.
            context = context with { Preserved = 1, Problems = [new("prepare", "registry_unreadable", 5)] };
            var audit = new List<string>();
            var batch = await IntegratedResidualBatch.RunAsync([removed],
                new Dictionary<string, ResidualUninstallContext> { [id] = context }, Empty,
                (_, _) => Task.FromResult(false), (_, _) => Task.FromResult<ResidualElevationResponse?>(null), audit.Add);
            check(batch.Unavailable > 0 && !audit.Any(x => x.Contains("no_eligible_residue")),
                "W12 erro 5 sem alvo elegivel deve terminar inconclusivo");

            // Consultar valores nao exige notificacoes nem permissoes de escrita.
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath)) key.SetValue("Marker", "SYNTHETIC");
            using (var acl = Registry.CurrentUser.OpenSubKey(keyPath, RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.ChangePermissions | RegistryRights.ReadPermissions))
            {
                var original = acl!.GetAccessControl();
                var denied = acl.GetAccessControl();
                denied.AddAccessRule(new RegistryAccessRule(WindowsIdentity.GetCurrent().User!, RegistryRights.Notify, AccessControlType.Deny));
                acl.SetAccessControl(denied);
                bool readable = false;
                try
                {
                    using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                    using var key = ResidualRegistryGuard.Open(hive, keyPath, RegistryView.Registry64);
                    readable = key is not null && ResidualRegistryGuard.Fingerprint(key, RegistryView.Registry64, out _, out _);
                }
                catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex)) { }
                finally { acl.SetAccessControl(original); }
                check(readable, "W12 inspecao nao deve exigir KEY_NOTIFY negado");
            }
            Registry.CurrentUser.DeleteSubKeyTree(keyPath);

            // Sem receita e sem InstallLocation: comando registrado corrobora a pasta.
            IntegratedResidualPolicy.SyntheticScope = null;
            string dedicated = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), id);
            var unknown = new ProgramEntry { Name = id, Publisher = app.Publisher, InventoryId = id,
                UninstallString = "\"" + Path.Combine(dedicated, "uninstall.exe") + "\"" };
            check(IntegratedResidualPolicy.Build(ResidualAppIdentity.From(unknown)).Roots.Contains(dedicated, StringComparer.OrdinalIgnoreCase),
                "W12 pasta dedicada corroborada pelo comando nao depende de InstallLocation preenchido");
        }
        finally
        {
            IntegratedResidualPolicy.SyntheticScope = null;
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
