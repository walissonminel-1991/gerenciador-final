using GerenciadorAplicativos;

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
            check(File.ReadAllText(Path.Combine(folder, "playlist.m3u")) == "dados fictícios", "Playlist de fixture permanece intacta após inspeções");
        }
        finally { Directory.Delete(root, true); }
    }
}
