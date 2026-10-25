using System.Text.Json;
using GerenciadorAplicativos;

// Shared baseline/candidate probe. Signature evidence is supplied identically in
// the disposable copies by the comparison script; it is not Authenticode proof.
internal static class Work14SilentCoverageProbe
{
    internal static void Run()
    {
        string basis = Path.Combine(Path.GetTempPath(), "GF-W14-Mode-Coverage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(basis);
        Environment.SetEnvironmentVariable("GF_WORK14_FIXTURE_SIGNATURE_ROOT", basis);
        typeof(InstallerEngineEvidence).GetField("SyntheticUninstallBase", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null,basis);
        string fixture = Path.GetFullPath(Path.Combine("TestesResiduos", "SilentFixture", "bin", "Release", "net10.0-windows", "GF-SilentFixture.exe"));
        var rows = new List<object>();
        try
        {
            foreach (string id in new[] { "nsis", "nsis_nested", "inno", "inno_nested", "inno_no_product", "nsis_wrong_arch", "inno_wrong_publisher", "inno_server" })
            {
                bool nsis = id.StartsWith("nsis",StringComparison.Ordinal), nested = id.EndsWith("nested",StringComparison.Ordinal);
                string root = Path.Combine(basis,id), location = nested ? Path.Combine(root,"uninstaller") : root;
                Directory.CreateDirectory(location);
                string exe = Path.Combine(location, nsis ? "uninstall.exe" : "unins000.exe"); File.Copy(fixture,exe);
                if (id != "inno_no_product") File.Copy(fixture,Path.Combine(root,"app.exe"));
                if (!nsis) File.WriteAllBytes(Path.ChangeExtension(exe,".dat"), System.Text.Encoding.ASCII.GetBytes("Inno Setup Uninstall Log (b)".PadRight(64,'\0')));
                else
                {
                    using var stream = new FileStream(exe,FileMode.Append); byte[] header = new byte[512];
                    byte[] marker = [0xef,0xbe,0xad,0xde,0x4e,0x75,0x6c,0x6c,0x73,0x6f,0x66,0x74,0x49,0x6e,0x73,0x74]; marker.CopyTo(header,4);
                    BitConverter.GetBytes(100u).CopyTo(header,20); BitConverter.GetBytes(200u).CopyTo(header,24); stream.Write(header);
                }
                var app = new ProgramEntry { Name = "SumatraPDF" + (id == "nsis_wrong_arch" ? " (x86)" : id == "inno_server" ? " Server" : ""),
                    Publisher = id == "inno_wrong_publisher" ? "Other Vendor" : "GF Synthetic Tests", Version = "3.6.1",
                    InventoryId = "LocalMachine|Registry64|" + id, InstallLocation = root, UninstallString = "\"" + exe + "\"" };
                var request = UninstallBatch.Select([app]).Single();
                rows.Add(new { id, expected_eligible = id is "nsis" or "nsis_nested" or "inno" or "inno_nested",
                    silent_eligible = SilentUninstallPolicy.UsesSilentMode(request), signature_evidence = "simulated-identically-in-both-refs" });
            }
            int policies = ApplicationIdentityCatalog.Rules.Count(r => r.CleanupPolicy is { } id && UninstallRuleCatalog.Rules.Any(p => p.Id == id));
            Console.WriteLine("WORK14_UNINSTALL_MATRIX=" + JsonSerializer.Serialize(rows));
            Console.WriteLine("WORK14_RESIDUAL_POLICIES=" + JsonSerializer.Serialize(new { identities = ApplicationIdentityCatalog.RuleCount, policies, deletion_authorized = 0 }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GF_WORK14_FIXTURE_SIGNATURE_ROOT",null);
            typeof(InstallerEngineEvidence).GetField("SyntheticUninstallBase",System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null,null);
            Directory.Delete(basis,true);
        }
    }
}
