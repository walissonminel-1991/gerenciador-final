using GerenciadorAplicativos;

// Fixtures descartáveis; nenhum Registro real, instalador ou PC pessoal é tocado.
internal static class Work14ReliabilityTests
{
    internal static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "GF-Lote1-" + Guid.NewGuid().ToString("N"));
        string secret = @"C:\Users\PRIVATE\dados\meu-programa.exe";
        Directory.CreateDirectory(root);
        ProgramEntry App(string id, string version = "1.0") => new()
        {
            InventoryId = "Synthetic|" + id, Name = "Privado " + secret,
            Publisher = "Editor confidencial", Version = version, CanUninstall = false
        };
        var a = App("A");
        var b = App("B");
        var c = App("C");
        try
        {
            using (var history = new LocalReliabilityHistory(root))
            {
                check(history.StorageAvailable && File.Exists(history.FilePath),
                    "L1 histórico utiliza apenas pasta sintética com arquivo exclusivo");
                var first = history.Observe([a, b], true);
                check(!first.Comparable && first.Total == 2,
                    "L1 primeira fotografia não inventa alterações");
                var partial = history.Observe([a], false);
                check(!partial.Comparable && partial.Explanation.Contains("NÃO podem"),
                    "L1 parcial não transforma ausência em desinstalação");
                var after = history.Observe([a, b, c], true);
                check(after.Comparable && after.Added == 1 && after.NotObserved == 0,
                    "L1 parcial preserva base completa anterior");
                var same = history.Observe([a, b, c], true);
                check(same.Comparable && same.Added == 0 && same.NotObserved == 0 && same.Changed == 0,
                    "L1 identidades inalteradas não criam diferenças");
                var metadata = history.Observe([a, App("B", "2.0"), c], true);
                check(metadata.Changed == 1 && metadata.NotObserved == 0,
                    "L1 versão modificada não se confunde com instalação removida");
                var duplicate = history.Observe([a, a], true);
                check(!duplicate.Comparable && duplicate.Explanation.Contains("suspensa"),
                    "L1 identificadores duplicados não substituem base segura");
                check(!history.Observe([new ProgramEntry { Name = "Sem identidade" }], true).Comparable,
                    "L1 identificador vazio não autoriza comparar");
                check(!history.Observe([], true).Comparable,
                    "L1 inventário vazio não atesta remoções em massa");
                var missing = history.Observe([a, c], true);
                check(missing.Comparable && missing.NotObserved == 1 &&
                    missing.Explanation.Contains("NÃO confirma desinstalação"),
                    "L1 ausência em fotografia completa é apenas consultiva");

                var blocked = new UpdateOperationSnapshot(Guid.NewGuid(), secret, secret,
                    new(UpdateStage.Blocked), 5, false, secret, 100, false);
                history.RecordUpdate(blocked);
                history.RecordUninstall(new(UninstallStage.Failed, secret, secret));
                string report = history.Report([a], incomplete: true);
                string file = File.ReadAllText(history.FilePath);
                check(report.Contains("PARCIAL") && report.Contains("Bloqueios") &&
                    report.Contains("ATUALIZACAO_BLOCKED") && report.Contains("DESINSTALACAO_FAILED"),
                    "L1 relatório agrega restrições e resultados locais");
                check(!report.Contains(secret, StringComparison.OrdinalIgnoreCase) &&
                    !report.Contains("Editor confidencial") &&
                    !file.Contains(secret, StringComparison.OrdinalIgnoreCase) &&
                    !file.Contains("Editor confidencial") &&
                    !file.Contains("Synthetic|") && !file.Contains("Privado"),
                    "L1 evento ou relatório nunca grava nomes, caminhos, comandos ou identificadores");
                check(LocalReliabilityHistory.BlockReason(new ProgramEntry { InventoryOnly = true })
                        .Contains("somente leitura") &&
                    LocalReliabilityHistory.BlockReason(new ProgramEntry { UpdateRequiresReview = true })
                        .Contains("revisão") &&
                    LocalReliabilityHistory.BlockReason(new ProgramEntry { IsUpdate = false, CanUninstall = false })
                        .Contains("não validado"),
                    "L1 motivos conservadores e legíveis distinguem restrições");
                for (int i = 0; i < 150; i++)
                    history.RecordUninstall(new(UninstallStage.Cancelled, secret, secret));
                check(history.Report([], false).Split("DESINSTALACAO_CANCELLED").Length <= 129,
                    "L1 prévia mantém no máximo 128 eventos por sessão");
            }
            string blockedFolder = Path.Combine(root, "nao-pasta");
            File.WriteAllText(blockedFolder, "não sobrescrever");
            using (var unavailable = new LocalReliabilityHistory(blockedFolder))
            {
                check(!unavailable.StorageAvailable && !unavailable.Observe([a], true).Comparable &&
                    unavailable.Report([a], false).Contains("apenas em memória") &&
                    File.ReadAllText(blockedFolder) == "não sobrescrever",
                    "L1 falha ao abrir histórico nunca sobrescreve arquivo nem libera ações");
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
