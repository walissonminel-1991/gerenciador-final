using GerenciadorAplicativos;
using Microsoft.Win32;
using System.Security.AccessControl;
using System.Security.Principal;

internal static class ResidualBlockingTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "GF-Blocked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = Path.Combine(root, "Editor.exe");
            File.WriteAllText(file, "fixture isolado");
            var readBudget = new ResidualScanBudget();
            check(ResidualReadSafety.HasExistingLocalFile(file, readBudget) &&
                !ResidualReadSafety.HasExistingLocalFile(file + "-ausente", readBudget) && !readBudget.Incomplete,
                "Executável registrado ausente é distinguido de arquivo existente sem presumir erro");
            check(!ResidualReadSafety.HasExistingLocalFile("relativo/Editor.exe", readBudget) && readBudget.Incomplete,
                "Pista de executável não verificável mantém análise parcial");
            var app = new ProgramEntry { Name = "Produto fictício" };
            var candidate = new ResidualCandidate(app.Name, root, [file], 0);
            var basic = UninstallResiduals.Clean(app, candidate);
            var direct = UninstallResiduals.CleanCandidateFiles(candidate, ["Editor.exe"]);
            var tree = AdvancedUninstallCleaner.DeleteApprovedTree(root, [root]);
            var advanced = AdvancedUninstallCleaner.Clean(new(app, [root], [], 1, 0), []);
            check(basic.RemovedFiles == 0 && direct.RemovedFiles == 0 && tree.Files == 0 &&
                tree.Directories == 0 && advanced.Files == 0 && advanced.RegistryKeys == 0 &&
                File.ReadAllText(file) == "fixture isolado", "Quatro pontos de entrada de arquivos não executam exclusão mesmo com allowlist exata");
            if (!OperatingSystem.IsWindows()) return;
            string key = @"Software\GF_Blocking_" + Guid.NewGuid().ToString("N");
            try
            {
                using (var created = Registry.CurrentUser.CreateSubKey(key)) created.SetValue("Fixture", "preservar");
                check(!AdvancedUninstallCleaner.DeleteApprovedRegistryKey(key, [key]),
                    "Ponto de entrada do Registro recusa allowlist explícita");
                using var existing = Registry.CurrentUser.OpenSubKey(key);
                check((string?)existing?.GetValue("Fixture") == "preservar", "Chave sintética permanece após tentativa de exclusão bloqueada");
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(key, false); }
            var info = new FileInfo(file);
            var original = info.GetAccessControl();
            try
            {
                var denied = info.GetAccessControl();
                denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData,
                    AccessControlType.Deny));
                info.SetAccessControl(denied);
                check(!(await ResidualPreview.CaptureAsync(candidate)).Complete,
                    "ACL nega leitura: prévia falha fechada sem excluir dados");
            }
            finally { info.SetAccessControl(original); }
            string link = Path.Combine(root, "Junction");
            string target = Path.Combine(root, "Destino");
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "playlist.m3u"), "fictício");
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
            foreach (string arg in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(arg);
            using (var process = System.Diagnostics.Process.Start(start)!)
            { process.WaitForExit(); check(process.ExitCode == 0, "Junction NTFS criada somente no fixture"); }
            try
            {
                check(!ResidualReadSafety.IsUnlinkedDirectory(link) &&
                    UninstallResiduals.InspectDirectory("Teste", link, ["playlist.m3u"]) is null,
                    "Junction NTFS e destino redirecionado recusados");
                AdvancedUninstallCleaner.DeleteApprovedTree(link, [link]);
                check(File.Exists(Path.Combine(target, "playlist.m3u")), "Bloqueio preserva destino da junction");
            }
            finally { Directory.Delete(link); }
        }
        finally { Directory.Delete(root, true); }
    }
}
