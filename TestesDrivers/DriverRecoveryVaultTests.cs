using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GerenciadorAplicativos;

// Fixtures locais: sem elevação, sem COM, sem PnPUtil, sem drivers reais.
internal static class DriverRecoveryVaultTests
{
    internal static void Run(Action<bool, string> check)
    {
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
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt with { DriverClass = "Display" }));
            var display = DriverRecoveryVault.ReviewForTest(root, folder);
            check(display.Verified && !display.CanStage,
                "Restore: backup Display valido permanece protegido para staging");
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt));
            File.AppendAllText(inf, "; arquivo alterado");
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
            var blocked = DriverRecoveryVault.StageAsync(folder, false, () => true,
                CancellationToken.None).GetAwaiter().GetResult();
            check(!blocked.Attempted && !blocked.RequiresReconciliation &&
                !File.Exists(folder + ".restore-pending.json"),
                "Restore: sem confirmacao jamais registra nem inicia staging");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
