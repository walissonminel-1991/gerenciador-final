using System.Security.Cryptography;
using GerenciadorAplicativos;

// Usa arquivos temporários sintéticos. Nenhum processo ou dispositivo do Windows é alterado.
internal static class DriverSilentUninstallTests
{
    private static void Ensure(bool expected, string description)
    {
        if (!expected) throw new InvalidOperationException("G3 UNINSTALL: " + description);
    }

    private static DriverInventoryEntry RootCamera(string id, string inf, string kind = "MEDIA") =>
        new(id, "Dispositivo genérico", "Fornecedor OEM", "1.0.0.0", inf, true, true)
        {
            ReportedDriverClass = kind,
            PnpProblemCode = 0,
            PackageEvidence = new(inf, DriverStoreState.Mapped,
                @"C:\Windows\INF\" + inf, @"C:\Windows\System32\DriverStore\FileRepository\test\driver.inf")
        };

    private static DriverInventoryResult Snapshot(params DriverInventoryEntry[] entries) =>
        new(entries, false, "Teste G3")
        {
            Store = new(entries.Select(d => d.PackageEvidence!)
                .DistinctBy(x => x.InfName, StringComparer.OrdinalIgnoreCase).ToArray(), false, 0)
        };

    internal static void Run()
    {
        var root = RootCamera(@"ROOT\MEDIA\0000", "oem39.inf");
        Ensure(DriverSilentUninstall.Evaluate(Snapshot(root), root).Allowed,
            "DroidCam genérico ROOT MEDIA e OEM exclusivo não deve ser bloqueado por fabricante");
        var printer = RootCamera(@"ROOT\PRINTER\0000", "oem5.inf", "Printer");
        Ensure(DriverSilentUninstall.Evaluate(Snapshot(printer), printer).Allowed,
            "impressora de laboratório é elegível sem vincular a marca");
        var webcam = RootCamera(@"USB\VID_1234&PID_0001\A", "oem10.inf", "Camera");
        Ensure(DriverSilentUninstall.Evaluate(Snapshot(webcam), webcam).Allowed,
            "webcam OEM física não crítica permanece representada");
        var sound = RootCamera(@"HDAUDIO\FUNC_01\1", "oem15.inf");
        Ensure(!DriverSilentUninstall.Evaluate(Snapshot(sound), sound).Allowed,
            "MEDIA físico bloqueado para não remover áudio integrado");
        var network = root with { ReportedDriverClass = "Net" };
        Ensure(!DriverSilentUninstall.Evaluate(Snapshot(network), network).Allowed,
            "rede crítica não pode ser removida");
        var sys = root with { InfName = "machine.inf" };
        Ensure(!DriverSilentUninstall.Evaluate(Snapshot(sys), sys).Allowed,
            "INF do Windows não pode ser removido");
        var ms = root with { Provider = "Microsoft" };
        Ensure(!DriverSilentUninstall.Evaluate(Snapshot(ms), ms).Allowed,
            "driver publicado pela Microsoft bloqueado");
        var ambiguous = root with { DeviceId = @"ROOT\MEDIA\0001" };
        Ensure(!DriverSilentUninstall.Evaluate(Snapshot(root, ambiguous), root).Allowed,
            "pacote com múltiplos dispositivos não pode ser removido");
        Ensure(!DriverSilentUninstall.Evaluate(Snapshot(root, root), root).Allowed,
            "DeviceId duplicado não pode ser escolhido");
        var unknown = root with { PnpProblemCode = null };
        Ensure(!DriverSilentUninstall.Evaluate(Snapshot(unknown), unknown).Allowed,
            "estado PnP desconhecido não autoriza remoção");
        Ensure(!DriverSilentUninstall.Evaluate(Snapshot(root) with { Partial = true }, root).Allowed,
            "inventário parcial não autoriza remoção");
        Ensure(!DriverSilentUninstall.Evaluate(Snapshot(root), root with { Version = "0.0" }).Allowed,
            "seleção que mudou desde a fotografia não autoriza remoção");
        var refused = DriverSilentUninstall.Execute(Snapshot(root), root, false,
            () => true).GetAwaiter().GetResult();
        Ensure(refused.State == DriverSilentUninstallState.Blocked,
            "sem consentimento nao pode chamar pnputil, mesmo com driver elegivel");
        Ensure(!DriverResidualReviewPolicy.MayInspect(DriverSilentUninstallState.VerificationUncertain),
            "nao inspecionar residuos de driver com remocao incerta");
        Ensure(DriverResidualReviewPolicy.MayInspect(DriverSilentUninstallState.Verified),
            "permite apenas revisao consultiva apos remocao comprovada");
        Ensure(!DriverResidualReviewPolicy.MayDeleteFromWindows(true),
            "nunca apagar DriverStore ou Registro por limpeza generica");
        VerifyBackupAndRemoval(root);
        Ensure(new DriverSilentUninstallOutcome(DriverSilentUninstallState.InstallationUncertain, "").RequiresInventoryRefresh &&
            new DriverSilentUninstallOutcome(DriverSilentUninstallState.VerificationUncertain, "").RequiresInventoryRefresh &&
            new DriverSilentUninstallOutcome(DriverSilentUninstallState.Verified, "").RequiresInventoryRefresh &&
            !new DriverSilentUninstallOutcome(DriverSilentUninstallState.Blocked, "").RequiresInventoryRefresh,
            "resultado confirmado ou incerto exige revogar a fotografia");
        Console.WriteLine("OK G3: política genérica de desinstalação e bloqueios críticos; sem alterar Windows");
    }

    private static void VerifyBackupAndRemoval(DriverInventoryEntry selected)
    {
        var temp = Directory.CreateTempSubdirectory("gf-driver-uninstall-");
        try
        {
            string backup = Directory.CreateDirectory(Path.Combine(temp.FullName, "backup")).FullName;
            byte[] data = "INF sintético para teste"u8.ToArray();
            string infDigest = Convert.ToHexString(SHA256.HashData(data));
            File.WriteAllBytes(Path.Combine(backup, "outro.inf"), data);
            Ensure(!DriverSilentUninstall.VerifyBackup(backup, "original.inf", infDigest, out _),
                "arquivo INF de outro nome não comprova o backup");
            string original = Path.Combine(backup, "original.inf");
            File.WriteAllText(original, "conteúdo divergente");
            Ensure(!DriverSilentUninstall.VerifyBackup(backup, "original.inf", infDigest, out _),
                "INF exportado deve possuir o SHA-256 verificado");
            File.WriteAllBytes(original, data);
            Ensure(DriverSilentUninstall.VerifyBackup(backup, "original.inf", infDigest, out string digest) &&
                digest.Length == 64, "INF exato confirma identidade do backup e produz digest agregado");
            string duplicate = Directory.CreateDirectory(Path.Combine(backup, "duplicate")).FullName;
            File.WriteAllBytes(Path.Combine(duplicate, "original.inf"), data);
            Ensure(!DriverSilentUninstall.VerifyBackup(backup, "original.inf", infDigest, out _),
                "cópia INF ambígua bloqueia remoção");

            var package = selected.PackageEvidence! with
            {
                PublishedPath = Path.Combine(temp.FullName, selected.InfName),
                StorePath = Path.Combine(temp.FullName, "store", "original.inf")
            };
            var trust = new DriverInfVerificationResult(DriverInfTrust.VerifiedInf, infDigest, "catalog.cat", "assinante");
            Ensure(DriverSilentUninstall.MatchesBackupSource(package, trust, package, trust) &&
                !DriverSilentUninstall.MatchesBackupSource(package, trust, package, trust with { Sha256 = "alterado" }) &&
                !DriverSilentUninstall.MatchesBackupSource(package, trust, package, trust with { Catalog = "outro.cat" }),
                "revalidação rejeita pacote ou catálogo trocado após exportação");

            var other = RootCamera(@"ROOT\PRINTER\0001", "oem5.inf", "Printer");
            var after = Snapshot(other);
            Ensure(DriverSilentUninstall.RemovalConfirmed(0, after, package), "ausência independente confirma a remoção");
            Ensure(!DriverSilentUninstall.RemovalConfirmed(3010, after, package) &&
                !DriverSilentUninstall.RemovalConfirmed(0, after with { Partial = true }, package) &&
                !DriverSilentUninstall.RemovalConfirmed(0, after with { Items = [] }, package) &&
                !DriverSilentUninstall.RemovalConfirmed(0, Snapshot(other with { InfName = selected.InfName }), package),
                "reinício, leitura inconclusiva ou INF associado a outro dispositivo mantêm verificação pendente");
            File.WriteAllBytes(package.PublishedPath, data);
            Ensure(!DriverSilentUninstall.RemovalConfirmed(0, after, package), "INF publicado ainda existe");
            File.Delete(package.PublishedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(package.StorePath)!);
            File.WriteAllBytes(package.StorePath, data);
            Ensure(!DriverSilentUninstall.RemovalConfirmed(0, after, package), "INF no Driver Store ainda existe");
            Console.WriteLine("OK G3: backup corresponde ao INF verificado e ausência do pacote é confirmada independentemente.");
        }
        finally { temp.Delete(recursive: true); }
    }
}
