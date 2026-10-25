using System.Text.Json;
using GerenciadorAplicativos;

// The same source is compiled against both immutable refs by measure_work14.py.
// No execution of payloads; eligibility stops before hash/signature validation.
internal static class Work14CoverageProbe
{
    internal static void Run()
    {
        string basis = Path.Combine(Path.GetTempPath(), "GF-W14-Coverage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(basis); AutomaticAppInstallation.SyntheticBaseRoot = basis;
        string fixture = Path.GetFullPath(Path.Combine("TestesResiduos", "SilentFixture", "bin", "Release", "net10.0-windows", "GF-SilentFixture.exe"));
        var rows = new List<object>();
        var cases = new[] { ("nsis", "nullsoft", false, false), ("nsis_arp", "nullsoft", true, false),
            ("msi", "msi", false, false), ("msi_icon_root", "msi", false, true),
            ("inno", "inno", false, false), ("inno_arp", "inno", true, false),
            ("wrong_arch", "nullsoft", false, false), ("wrong_product", "nullsoft", false, false),
            ("wrong_msi_code", "msi", false, false), ("server", "inno", false, false) };
        try
        {
            foreach (var (id, kind, decorated, missing) in cases)
            {
                string root = Path.Combine(basis, id); Directory.CreateDirectory(root);
                string binary = Path.Combine(root, "app.exe"), exe = Path.Combine(root, kind == "inno" ? "unins000.exe" : "uninstall.exe");
                byte[] data = File.ReadAllBytes(typeof(Work14CoverageProbe).Assembly.Location);
                int pe = BitConverter.ToInt32(data, 60);
                BitConverter.GetBytes((ushort)((BitConverter.ToUInt16(data, pe + 22) | 2) & ~0x2000)).CopyTo(data, pe + 22);
                File.WriteAllBytes(binary, data);
                if (kind == "inno")
                {
                    File.Copy(fixture, exe);
                    File.WriteAllBytes(Path.ChangeExtension(exe, ".dat"), System.Text.Encoding.ASCII.GetBytes("Inno Setup Uninstall Log (b)".PadRight(64, '\0')));
                }
                else
                {
                    using var stream = File.Create(exe); stream.Write(data); byte[] header = new byte[512];
                    byte[] marker = [0xef,0xbe,0xad,0xde,0x4e,0x75,0x6c,0x6c,0x73,0x6f,0x66,0x74,0x49,0x6e,0x73,0x74]; marker.CopyTo(header,4);
                    BitConverter.GetBytes(100u).CopyTo(header,20); BitConverter.GetBytes(200u).CopyTo(header,24); stream.Write(header);
                }
                const string code = "{00000000-0000-0000-0000-000000000059}";
                string name = "TestesAtualizador" + (decorated ? " 12.0.40664.0 (64-bit x64)" : "");
                if (id == "wrong_arch") name += " (x86)";
                if (id == "wrong_product") name = "Other Product";
                if (id == "server") name += " Server";
                var app = new ProgramEntry { Name = name, Publisher = "GF Tests", Version = "12.0.40664.0",
                    InventoryId = "LocalMachine|Registry64|" + id, InstallLocation = missing ? "" : root, DisplayIcon = binary,
                    UninstallString = kind == "msi" ? "msiexec.exe /X" + code : "\"" + exe + "\"",
                    MsiProductCode = kind == "msi" ? (id == "wrong_msi_code" ? "{00000000-0000-0000-0000-000000000060}" : code) : "",
                    IsUpdate = true, AvailableVersion = "13.0.0.0" };
                var local = AutomaticAppInstallation.Read(app);
                bool eligible = false;
                if (local is not null && local.Current([app]))
                {
                    var target = new AutomaticInstaller("Synthetic." + id, "TestesAtualizador", app.Publisher, app.AvailableVersion,
                        kind, "x64", "machine", new Uri("https://vendor.example/setup.exe"), new string('A',64),
                        kind == "msi" ? "{00000000-0000-0000-0000-000000000061}" : "", "", kind == "msi" ? "/qn /norestart" : kind == "inno" ? "/VERYSILENT /NORESTART" : "/S");
                    AutomaticUpdateDiscovery.Attach(AutomaticUpdateDiscovery.SimulateIssued(app,local,target));
                    var recipe = AutomaticUpdateDiscovery.Resolve(app);
                    eligible = recipe is not null && VerifiedUpdaters.SingleSafeSilentInstallation(recipe,app,[app]);
                }
                rows.Add(new { id, expected_eligible = !id.StartsWith("wrong_",StringComparison.Ordinal) && id != "server",
                    local_identity = local is not null, silent_eligible = eligible, payload_verified = false,
                    reason = eligible ? "payload_validation_required" : "installation_not_corroborated" });
            }
            Console.WriteLine("WORK14_UPDATE_MATRIX=" + JsonSerializer.Serialize(rows));
        }
        finally { AutomaticAppInstallation.SyntheticBaseRoot = null; Directory.Delete(basis,true); }
    }
}
