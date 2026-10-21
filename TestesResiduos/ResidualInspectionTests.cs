using GerenciadorAplicativos;
using Microsoft.Win32;

internal static class ResidualInspectionTests
{
    internal static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "GF-Inspect-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "Editor Antigo");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "playlist.m3u"), "dados fictícios");
            Directory.CreateDirectory(Path.Combine(folder, "Componentes", "Perfil", "NivelExtra"));
            var report = new ResidualAuditReport("Editor Antigo", false, [new(folder, "Perfil", 1, "preservar")], []);
            var session = new ResidualInspectionSession(report, [root]);
            var result = session.Inspect(folder);
            check(result.Entries.Count == 2 && !result.Coverage.Incomplete && !result.CanExecute &&
                result.Entries.All(e => e.Disposition == ResidualDisposition.PreserveData), "Inspeção sob demanda enumera somente metadados locais e preserva dados");
            check(session.Inspect(root).Coverage.Incomplete && session.Inspect(folder + "Outro").Coverage.Incomplete,
                "Inspeção recusa locais digitados, raiz e prefixos fora da fotografia");
            var second = session.Inspect(result.Entries.Single(e => e.Container).Location);
            check(second.Entries.Count == 1 && session.Inspect(second.Entries.Single().Location).Coverage.Issues.HasFlag(ResidualScanIssue.DepthLimit),
                "Navegação descoberta tem limite de profundidade global por sessão");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            check(session.Inspect(folder, cancelled.Token).Coverage.Issues.HasFlag(ResidualScanIssue.Cancelled), "Inspeção cancelada não vira leitura concluída");
            check(session.Inspect(folder, timeout: TimeSpan.Zero).Coverage.Issues.HasFlag(ResidualScanIssue.Timeout), "Inspeção com tempo esgotado preserva parcialidade");
            check(session.Inspect(folder, maximumEntries: 1).Entries.Count == 1 && session.Inspect(folder, maximumEntries: 1).Coverage.Incomplete,
                "Limite sob demanda não é confundido com inspeção completa");
            check(session.Inspect(Path.Combine(folder, "playlist.m3u")).Coverage.Incomplete,
                "Arquivos encontrados não viram alvos executáveis de novas operações");
            string linked = Path.Combine(folder, "Vinculo");
            Directory.CreateSymbolicLink(linked, Path.Combine(folder, "Componentes"));
            var linkedResult = session.Inspect(folder);
            check(linkedResult.Coverage.Issues.HasFlag(ResidualScanIssue.LinkSkipped) && linkedResult.Entries.All(e => e.Name != "Vinculo"),
                "Inspeção não segue links nem os inclui como contêineres navegáveis");
            Directory.Delete(linked);
            var protectedTarget = new ResidualAuditReport("Fictício", false,
                [new(Path.GetPathRoot(root)!, "Raiz protegida", 1, "fora do escopo")], []);
            check(new ResidualInspectionSession(protectedTarget, [root]).Inspect(Path.GetPathRoot(root)!).Coverage.Incomplete,
                "Inspeção recusa raiz protegida mesmo quando recebida em relatório sintético");
            check(File.ReadAllText(Path.Combine(folder, "playlist.m3u")) == "dados fictícios", "Playlist de fixture permanece intacta após inspeções");
        }
        finally { Directory.Delete(root, true); }
        if (!OperatingSystem.IsWindows()) return;
        string key = @"Software\GF_Inspection_" + Guid.NewGuid().ToString("N");
        try
        {
            using (var created = Registry.CurrentUser.CreateSubKey(key))
            using (var child = created.CreateSubKey("Perfil")) child.SetValue("Privado", "CONTEUDO_FICTICIO_NAO_LER");
            var report = new ResidualAuditReport("Fictício", false,
                [new(@"HKEY_CURRENT_USER\" + key, "Preferências", 1, "preservar")], []);
            var registrySession = new ResidualInspectionSession(report);
            var result = registrySession.Inspect(report.Findings.Single().Location);
            check(result.Entries.Count == 1 && result.Entries[0].Name == "Perfil" && !result.Coverage.Incomplete &&
                !result.Explanation.Contains("CONTEUDO_FICTICIO"), "Inspeção nativa do Registro enumera somente nomes de subchaves fictícias");
            using var preserved = Registry.CurrentUser.OpenSubKey(key + @"\Perfil");
            check((string?)preserved?.GetValue("Privado") == "CONTEUDO_FICTICIO_NAO_LER", "Valor de fixture do Registro permanece intacto após inspeção");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(key, false); }
    }
}
