using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GerenciadorAplicativos;

// Fixtures locais: sem elevação, sem COM, sem PnPUtil, sem drivers reais.
internal static class DriverRecoveryVaultTests
{
    internal static void Run(Action<bool, string> check)
    {
        // Um backup manual, staging ou exclusão nunca é uma sessão WUA.
        check(!DriverG32RecoveryTicket.TryBeginOperation((DriverG32OperationKind)99),
            "Motor Drivers: espécie inválida não adquire trava");
        check(DriverG32RecoveryTicket.TryBeginOperation(DriverG32OperationKind.Export),
            "Motor Drivers: backup adquire lease exclusivo");
        try
        {
            check(DriverG32RecoveryTicket.HasActiveOperation &&
                !DriverG32RecoveryTicket.HasUpdateOperation &&
                !DriverG32RecoveryTicket.TryBeginOperation(DriverG32OperationKind.Update),
                "Motor Drivers: backup não se converte em permissão WUA e impede paralelismo");
        }
        finally { DriverG32RecoveryTicket.EndOperation(); }
        check(DriverG32RecoveryTicket.TryBeginOperation(),
            "Motor Drivers: WUA adquire nova sessão");
        try
        {
            check(DriverG32RecoveryTicket.HasUpdateOperation &&
                !DriverG32RecoveryTicket.TryBeginOperation(DriverG32OperationKind.Stage) &&
                !DriverG32RecoveryTicket.TryBeginOperation(DriverG32OperationKind.Uninstall),
                "Motor Drivers: sessão WUA exclui staging e desinstalação");
        }
        finally { DriverG32RecoveryTicket.EndOperation(); }
        check(!DriverG32RecoveryTicket.HasActiveOperation &&
            !DriverG32RecoveryTicket.HasUpdateOperation,
            "Motor Drivers: lease encerrado revoga autoridade de execução");
        const string id = @"PCI\VEN_1111&DEV_2222\INSTANCE";
        var store = new DriverStoreEvidence("oem17.inf", DriverStoreState.Mapped,
            @"C:\Windows\INF\oem17.inf",
            @"C:\Windows\System32\DriverStore\FileRepository\sample\sample.inf");
        var selected = new DriverInventoryEntry(id, "Synthetic", "Example", "1.0",
            "oem17.inf", true, true)
        {
            ReportedDriverClass = "Display",
            PackageEvidence = store,
            PnpProblemCode = 0
        };
        var complete = new DriverInventoryResult([selected], false, "fixture");
        check(DriverRecoveryVault.CanExport(complete, selected),
            "Backup: Display pode ser copiado sem autorizar instalacao");
        check(!DriverRecoveryVault.CanExport(complete with { Partial = true }, selected) &&
            !DriverRecoveryVault.CanExport(complete with { Items = [selected, selected] }, selected) &&
            !DriverRecoveryVault.CanExport(complete, selected with { Signed = false }) &&
            !DriverRecoveryVault.CanExport(complete, selected with { InfName = "display.inf" }),
            "Backup: origem parcial, duplicada, sem assinatura ou nao OEM bloqueada");
        string root = Path.Combine(Path.GetTempPath(), "gf-driver-vault-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "manual-fixture");
        Directory.CreateDirectory(folder);
        string inf = Path.Combine(folder, "sample.inf");
        string receiptPath = folder + ".backup.json";
        try
        {
            var infBytes = Encoding.UTF8.GetBytes("[Version]\r\nSignature=\"$Windows NT$\"\r\nClass=Printer\r\n");
            File.WriteAllBytes(inf, infBytes);
            string infHash = Convert.ToHexString(SHA256.HashData(infBytes));
            bool verified = DriverSilentUninstall.VerifyBackup(folder, "sample.inf", infHash,
                out string packageHash);
            check(verified && packageHash.Length == 64,
                "Backup: resumo inteiro calculado sem driver real");
            var receipt = new DriverBackupReceipt(1, "oem17.inf", "sample.inf",
                infHash, packageHash, "Printer", DateTimeOffset.UtcNow);
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt));
            var review = DriverRecoveryVault.ReviewForTest(root, folder);
            check(review.Verified && review.CanStage &&
                review.InfPath == inf && review.Receipt?.PackageSha256 == packageHash,
                "Restore: copia OEM, hash e recibo coerentes liberam apenas revisao");
            var availableCatalog = DriverRecoveryVault.BrowseBackupsForTest(root);
            check(availableCatalog.Items.Count == 1 &&
                availableCatalog.VerifiedCount == 1 &&
                availableCatalog.Items[0].State == DriverBackupCatalogState.Valid &&
                !availableCatalog.Items[0].CanExecute,
                "Cofre: lista read-only somente cópia íntegra, sem permissão de instalação");
            var evidence = new DriverStoreEvidence("oem900.inf", DriverStoreState.Mapped,
                inf, inf);
            var observedSet = new DriverPublishedAuditResult(
                [new DriverPublishedPackage(evidence, 0, false)], false, true);
            DriverInfVerificationResult Trusted(DriverStoreEvidence _) =>
                new(DriverInfTrust.VerifiedInf, infHash);
            var noRestore = DriverRecoveryVault.ReconcileWithEvidence(root, folder,
                observedSet, Trusted, _ => "Printer");
            check(noRestore.State == DriverRestoreReconcileState.NoRequest &&
                !noRestore.MayClearPendingAutomatically,
                "Restore: backup sem operação não é confundido com staging");
            File.WriteAllText(folder + ".restore-pending.json", JsonSerializer.Serialize(new
            {
                Schema = 1, Action = "StageOnly", AtUtc = DateTimeOffset.UtcNow,
                BackupHash = packageHash, State = "PENDING_RECONCILIATION"
            }));
            var pendingCatalog = DriverRecoveryVault.BrowseBackupsForTest(root);
            check(pendingCatalog.Items.Single().State ==
                DriverBackupCatalogState.PendingReconciliation &&
                !pendingCatalog.Items[0].CanExecute,
                "Cofre: marcador de staging preserva estado pendente sem repetir");
            var observed = DriverRecoveryVault.ReconcileWithEvidence(root, folder,
                observedSet, Trusted, _ => "Printer");
            check(observed.State == DriverRestoreReconcileState.MatchingInfObserved &&
                observed.PublishedInf == "oem900.inf" &&
                !observed.DeviceRestored && !observed.MayClearPendingAutomatically,
                "Restore: assinatura, classe e INF observados não provam dispositivo restaurado");
            int signatureChecks = 0;
            DriverInfVerificationResult CountSignature(DriverStoreEvidence source)
            {
                signatureChecks++;
                return Trusted(source);
            }
            var otherPackage = new DriverStoreEvidence("oem902.inf",
                DriverStoreState.Mapped, inf, Path.Combine(folder, "outro.inf"));
            var inventoryWithOther = observedSet with { Packages =
                [new DriverPublishedPackage(otherPackage, 0, false),
                 new DriverPublishedPackage(evidence, 0, false)] };
            var filtered = DriverRecoveryVault.ReconcileWithEvidence(root, folder,
                inventoryWithOther, CountSignature, _ => "Printer");
            check(filtered.State == DriverRestoreReconcileState.MatchingInfObserved &&
                signatureChecks == 1,
                "Restore: somente INF de mesmo nome original é submetido à assinatura");

            check(DriverRecoveryVault.ReconcileWithEvidence(root, folder,
                observedSet with { Partial = true }, Trusted, _ => "Printer").State ==
                    DriverRestoreReconcileState.Inconclusive &&
                DriverRecoveryVault.ReconcileWithEvidence(root, folder,
                    observedSet, Trusted, _ => "Display").State ==
                    DriverRestoreReconcileState.Inconclusive &&
                DriverRecoveryVault.ReconcileWithEvidence(root, folder,
                    observedSet with { Packages =
                        [new DriverPublishedPackage(evidence, 0, false),
                         new DriverPublishedPackage(evidence with { InfName = "oem901.inf" }, 0, false)] },
                    Trusted, _ => "Printer").State == DriverRestoreReconcileState.Inconclusive,
                "Restore: enumeração parcial, classe conflitante e duplicidade não comprovam recuperação");
            File.Delete(folder + ".restore-pending.json");
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt with { DriverClass = "Display" }));
            var displayCatalog = DriverRecoveryVault.BrowseBackupsForTest(root);
            check(displayCatalog.Items.Single().State == DriverBackupCatalogState.ProtectedClass &&
                !displayCatalog.Items[0].CanExecute,
                "Cofre: backup Display aparece mas nunca oferece execução");
            var display = DriverRecoveryVault.ReviewForTest(root, folder);
            check(display.Verified && !display.CanStage,
                "Restore: backup Display valido permanece protegido para staging");
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt));
            File.AppendAllText(inf, "; arquivo alterado");
            var damagedCatalog = DriverRecoveryVault.BrowseBackupsForTest(root);
            check(damagedCatalog.Partial &&
                damagedCatalog.Items.Single().State == DriverBackupCatalogState.Inconclusive,
                "Cofre: arquivo alterado não aparece como cópia confiável");
            check(!DriverRecoveryVault.ReviewForTest(root, folder).Verified,
                "Restore: modificacao de arquivo invalida backup");
            File.WriteAllBytes(inf, infBytes);
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt with { PackageSha256 = new string('A', 64) }));
            check(!DriverRecoveryVault.ReviewForTest(root, folder).Verified,
                "Restore: recibo com resumo falso falha fechado");
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt with { OriginalInf = "../sample.inf" }));
            check(!DriverRecoveryVault.ReviewForTest(root, folder).Verified,
                "Restore: caminho de INF com travessia bloqueado");
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt));
            Directory.CreateDirectory(Path.Combine(root, "elsewhere"));
            check(!DriverRecoveryVault.ReviewForTest(root, Path.Combine(root, "elsewhere", "child")).Verified &&
                !DriverRecoveryVault.ReviewForTest(root, root).Verified,
                "Restore: so aceita pasta filha direta do cofre local");
            Directory.CreateDirectory(Path.Combine(root, "manual-incomplete"));
            var bounded = DriverRecoveryVault.BrowseBackupsForTest(root, maximum: 1);
            check(bounded.Partial && bounded.Inspected == 1 &&
                bounded.Items.Count == 1 && bounded.Items.All(item => !item.CanExecute),
                "Cofre: enumeração limitada sinaliza parcial sem oferecer ações");
            var blocked = DriverRecoveryVault.StageAsync(folder, false, () => true,
                CancellationToken.None).GetAwaiter().GetResult();
            check(!blocked.Attempted && !blocked.RequiresReconciliation &&
                !File.Exists(folder + ".restore-pending.json"),
                "Restore: sem confirmacao jamais registra nem inicia staging");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
