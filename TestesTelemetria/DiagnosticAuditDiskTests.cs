using GerenciadorAplicativos;

// Apenas arquivos descartáveis. Não altera permissões do sistema, não chama
// manutenção e não depende de encher disco, antivírus ou administrador.
internal static class DiagnosticAuditDiskTests
{
    internal static void Run(string root, Action<bool, string> verify)
    {
        string folder = Path.Combine(root, "gf03-audit-disk");
        Directory.CreateDirectory(folder);
        string occupied = Path.Combine(folder, "file-not-directory");
        File.WriteAllText(occupied, "fixture-sentinela");
        string logFolder = Path.Combine(folder, "session");
        int effects = 0;
        try
        {
            var unavailable = new DiagnosticAuditAdmission(DiagnosticTrace.Write);
            verify(!DiagnosticTrace.StartInFolders([occupied]) &&
                File.ReadAllText(occupied) == "fixture-sentinela" &&
                !unavailable.CompleteStartup(false) && !unavailable.TryAuthorizeMutation(),
                "GF-03 disco: destino inválido não admite manutenção nem altera sentinela");

            verify(DiagnosticTrace.StartInFolders([logFolder]), "GF-03 disco: log de teste aberto");
            string path = DiagnosticTrace.LogFilePath;
            // Outro leitor legítimo permite a identidade já mantida, mas
            // nega novas aberturas para escrita; simula indisponibilidade real.
            // Exercitar a admissão estática real antes de qualquer escrita
            // negativa e manter o estado degradado até o fim deste processo.
            verify(DiagnosticTrace.CompleteStartupAudit(true) && DiagnosticTrace.MutationAuditReady &&
                DiagnosticTrace.TryAuthorizeMutation(), "GF-03 disco: fachada estática admite após escrita durável");
            var healthy = new DiagnosticAuditAdmission(DiagnosticTrace.Write);
            verify(healthy.CompleteStartup(true) && healthy.TryAuthorizeMutation() &&
                File.ReadAllText(path).Contains(DiagnosticAuditAdmission.MutationMarker, StringComparison.Ordinal),
                "GF-03 disco: primeira gravação e marcador efetivamente persistidos");
            var first = new DiagnosticAuditAdmission(DiagnosticTrace.Write);
            using (var denyWrite = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (DiagnosticTrace.TryAuthorizeMutation()) effects++;
                verify(DiagnosticTrace.MutationAuditDegraded && !DiagnosticTrace.MutationAuditReady && effects == 0,
                    "GF-03 disco: fachada estática fecha admissão ao perder escrita");
                verify(!first.CompleteStartup(true) && first.IsDegraded && !first.TryAuthorizeMutation(),
                    "GF-03 disco: primeira gravação realmente recusada após abrir o log");
                if (healthy.TryAuthorizeMutation()) effects++;
                verify(healthy.IsDegraded && effects == 0,
                    "GF-03 disco: perda real de gravação recusa operação sem efeito");
            }
            verify(DiagnosticTrace.Write("TESTE.GF03.GRAVACAO_RESTAURADA") &&
                File.ReadAllText(path).Contains("TESTE.GF03.GRAVACAO_RESTAURADA", StringComparison.Ordinal) &&
                !healthy.TryAuthorizeMutation() && !first.TryAuthorizeMutation() && effects == 0 &&
                DiagnosticTrace.MutationAuditDegraded && !DiagnosticTrace.TryAuthorizeMutation(),
                "GF-03 disco: restauração de escrita não rearma admissão local nem estática");
        }
        finally { DiagnosticTrace.Stop(); }
    }
}
