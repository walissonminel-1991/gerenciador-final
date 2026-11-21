using GerenciadorAplicativos;

// Testes de política: nenhum processo, INF, registro ou dispositivo real é tocado.
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
        Console.WriteLine("OK G3: política genérica de desinstalação e bloqueios críticos; sem alterar Windows");
    }
}
