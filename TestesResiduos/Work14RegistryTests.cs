using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using GerenciadorAplicativos;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

// Only GUID fixtures are mutated. The HKLM test is strictly read-only and
// removes privileges from this runner token; no accounts or ACLs are changed.
internal static class Work14RegistryTests
{
    internal static void Run(Action<bool, string> check)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var identity = WindowsIdentity.GetCurrent();
        if (!CreateRestrictedToken(identity.AccessToken, 5, 0, IntPtr.Zero, 0, IntPtr.Zero,
            0, IntPtr.Zero, out var restricted)) throw new Win32Exception(Marshal.GetLastWin32Error());
        using (restricted)
        {
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                WindowsIdentity.RunImpersonated(restricted, () =>
                {
                    using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    bool incidentalWriteDenied = false;
                    try { using var oldHandle = root.Handle; }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
                    { incidentalWriteDenied = true; }
                    check(incidentalWriteDenied, "W14 reproduz erro 5 na reabertura .NET da raiz HKLM com escrita, token restrito " + view);
                    using var read = ResidualRegistryGuard.Open(root, @"SOFTWARE\Microsoft", view);
                    check(read is not null, "W14 consulta HKLM funciona com direitos somente leitura, token restrito " + view);
                });
        }
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var read = ResidualRegistryGuard.Open(root, @"SOFTWARE\Microsoft", view);
            check(read is not null, "W14 consulta no token original do runner " + view);
        }
        foreach (var where in new[] { "parent", "leaf", "child" }) TestDenied(where, check);
    }

    private static void TestDenied(string where, Action<bool, string> check)
    {
        string path = @"Software\GF-W14-" + Guid.NewGuid().ToString("N");
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using (var parent = root.CreateSubKey(path))
        using (var leaf = parent.CreateSubKey("Leaf"))
        using (var child = leaf.CreateSubKey("Child")) child.SetValue("Marker", "PRIVATE_FIXTURE");
        string target = path + (where == "parent" ? "" : where == "leaf" ? @"\Leaf" : @"\Leaf\Child");
        using var acl = root.OpenSubKey(target, RegistryKeyPermissionCheck.ReadWriteSubTree,
            RegistryRights.ChangePermissions | RegistryRights.ReadPermissions)!;
        var original = acl.GetAccessControl();
        var denied = acl.GetAccessControl();
        denied.AddAccessRule(new RegistryAccessRule(WindowsIdentity.GetCurrent().User!,
            RegistryRights.QueryValues, AccessControlType.Deny));
        var location = new ResidualKeyLocation(RegistryHive.CurrentUser, RegistryView.Registry64, path + @"\Leaf");
        var app = new ProgramEntry { Name = "PRIVATE_FIXTURE", Publisher = "GF fixture", InventoryId = Guid.NewGuid().ToString("N") };
        IntegratedResidualPolicy.SyntheticScope = _ => new("synthetic", [], [location], []);
        try
        {
            var context = IntegratedResidualCleanup.Prepare(app, new([app], true));
            check(context.Keys.Count == 1, "W14 captura inicial integral da fixture " + where);
            acl.SetAccessControl(denied);
            var plan = IntegratedResidualCleanup.Capture(context, new([], true));
            var problem = plan.Problems.Single(p => p.Reason == "registry_unreadable");
            string expected = where == "parent" ? "open_parent" : where == "leaf" ? "open_leaf" : "open_child";
            check(!plan.Conclusive && plan.Keys.Count == 0 && problem.Error == 5 && problem.Detail == expected,
                "W14 erro 5 localiza etapa e nao autoriza exclusao: " + where);
            check(plan.RegistryChecks is [{ State: "unreadable", Error: 5 }] &&
                ResidualCoverageSummary.Describe(plan).Contains("1 inacessível(is)"),
                "W14 inacessibilidade aparece na interface sem virar ausencia " + where);
            var beforeDenied = IntegratedResidualCleanup.Prepare(app, new([app], true));
            check(beforeDenied.Keys.Count == 0 && beforeDenied.Problems.Any(p => p.Error == 5 && p.Detail == expected),
                "W14 preflight negado nao incorpora autoridade " + where);
            var next = IntegratedResidualCleanup.Capture(beforeDenied, new([], true));
            check(!next.Conclusive && next.Eligible == 0, "W14 segundo alvo sem prova continua inconclusivo " + where);
            string audit = IntegratedResidualCleanup.Audit(ResidualAppIdentity.From(app), problem.Stage, "preserved",
                error: problem.Error, reason: problem.Reason, detail: problem.Detail, depth: problem.Depth);
            check(!audit.Contains("PRIVATE_FIXTURE") && !audit.Contains(path) && audit.Contains("leitura=" + expected),
                "W14 diagnostico registra etapa sem valores ou caminhos " + where);
        }
        finally
        {
            acl.SetAccessControl(original);
            IntegratedResidualPolicy.SyntheticScope = null;
            root.DeleteSubKeyTree(path, false);
        }
    }
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateRestrictedToken(SafeAccessTokenHandle token, uint flags,
        uint disableCount, IntPtr disable, uint deleteCount, IntPtr delete, uint restrictCount,
        IntPtr restrict, out SafeAccessTokenHandle result);
}
