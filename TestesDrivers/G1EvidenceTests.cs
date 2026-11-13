using GerenciadorAplicativos;

// G1: só fixtures sintéticas. Nunca baixar, instalar, remover nem executar drivers.
internal static class G1EvidenceTests
{
    internal static void Run(Action<bool, string> check)
    {
        const string instance = @"PCI\VEN_1234&DEV_5678\INSTANCE";
        const string hardware = @"PCI\VEN_1234&DEV_5678&SUBSYS_00001234";
        var device = new DriverInventoryEntry(instance, "Vídeo sintético",
            "Fornecedor A", "4.5.6.7", "oem42.inf", true, true)
        {
            PnpProblemCode = 0,
            HardwareIds = [hardware],
            PackageEvidence = new("oem42.inf", DriverStoreState.Mapped)
        };
        var offer = new DriverOfficialOffer(
            "5819343e-5d44-44c7-a8e3-e2c3c8abe622", 7,
            "Fornecedor A - Video - 999.99", hardware,
            "Fornecedor A", "Vídeo", new DateTime(2026, 11, 11))
        {
            DriverClass = "Display",
            Manufacturer = "Fabricante A"
        };
        DriverOfficialResult Match(DriverInventoryEntry observed, DriverOfficialOffer proposed) =>
            DriverOfficialLookup.Match(new([observed], false, "fixture G1"), [proposed], true);

        var result = Match(device, offer);
        var link = result.Matches.Single();
        check(!result.Partial && result.Offered == 1 && link.DeviceId == instance &&
            link.Offer.UpdateId == offer.UpdateId && link.Offer.Revision == offer.Revision &&
            link.Offer.HardwareId == hardware && link.Offer.DriverClass == "Display" &&
            link.Offer.Manufacturer == "Fabricante A",
            "G1: instância, hardware exato e identidade WUA (ID/revisão/classe/fabricante) vinculados");
        check(link.Installed is
            { InfName: "oem42.inf", Provider: "Fornecedor A", Version: "4.5.6.7",
              SignedReportedByWindows: true, StoreState: DriverStoreState.Mapped,
              PnpProblemCode: 0, WithoutSignedDriverAssociation: false } &&
            link.ProviderMatchesInstalled,
            "G1: snapshot da versão/INF/fornecedor/assinatura/PnP/Store instalado é preservado");
        check(!link.OfferedNumericVersionProven && !link.IsNewerProven &&
            !link.TargetOsArchitectureProven && !link.CompletePackageVerified &&
            !link.CanInstall && link.Label.Contains("não classificar como mais novo", StringComparison.Ordinal),
            "G1: título 999.99 e data recente não comprovam upgrade, compatibilidade ou payload");

        // Troca de fornecedor pode ser legítima; apenas informar a divergência.
        var providerChanged = Match(device, offer with { Provider = "Fornecedor B" }).Matches.Single();
        check(!providerChanged.ProviderMatchesInstalled && !providerChanged.CanInstall,
            "G1: fornecedor da oferta divergente não altera fabricante instalado nem autoriza operação");

        // Ausência/invalidade da versão instalada é diferente da versão oferecida.
        foreach (var invalid in new[]
        {
            device with { InfName = "" },
            device with { InfName = "../oem42.inf" },
            device with { Version = "" },
            device with { Version = "versão do título 42" },
            device with { Provider = "" },
            device with { PackageEvidence = new("oem43.inf", DriverStoreState.Mapped) },
            device with { PackageEvidence = new("oem42.inf", DriverStoreState.Inconsistent) },
            device with { PackageEvidence = new("oem42.inf", DriverStoreState.Unavailable) }
        })
        {
            var blocked = Match(invalid, offer);
            check(blocked.Partial && blocked.Matches.Count == 0,
                "G1: INF, versão, fornecedor e vínculo Store incertos falham fechado");
        }

        var noDriver = device with
        {
            InfName = "", Provider = "", Version = "", Signed = null,
            PackageEvidence = null, PnpProblemCode = 28, WithoutSignedDriverAssociation = true
        };
        var diagnostic = Match(noDriver, offer).Matches.Single();
        check(diagnostic.Installed is
            { InfName: "", Version: "", PnpProblemCode: 28,
              WithoutSignedDriverAssociation: true } &&
            !diagnostic.IsNewerProven && !diagnostic.CanInstall,
            "G1: dispositivo PnP 28 recebe oferta consultiva sem inventar driver existente");
        foreach (var invalid in new[]
        {
            noDriver with { PnpProblemCode = 22 },
            noDriver with { PnpProblemCode = null },
            noDriver with { Signed = false },
            noDriver with { InfName = "oem42.inf" },
            noDriver with { Version = "1.0" }
        })
        {
            var blocked = Match(invalid, offer);
            check(blocked.Partial && blocked.Matches.Count == 0,
                "G1: dispositivo sem associação só permite correspondência conservadora para PnP 28");
        }
        var incompatibleHardware = Match(device with { HardwareIds = [@"PCI\VEN_1234&DEV_5679"] }, offer);
        check(!incompatibleHardware.Partial && incompatibleHardware.Matches.Count == 0,
            "G1: nome, título, data e fabricante não substituem igualdade do HardwareID");
        var conflicted = DriverOfficialLookup.Match(new([device, device with { PnpProblemCode = 28 }],
            false, "fixture G1"), [offer], true);
        check(conflicted.Partial && conflicted.Matches.Count == 0,
            "G1: dois relatos PnP conflitantes não podem escolher a primeira instância");
        var changedClass = DriverOfficialLookup.Match(new([device], false, "fixture G1"),
            [offer, offer with { DriverClass = "Outro" }], true);
        check(changedClass.Partial && changedClass.Matches.Count == 0,
            "G1: mesmo UpdateID/revisão com classe divergente não publica vínculo");
        var alternatives = DriverOfficialLookup.Match(new([device], false, "fixture G1"),
            [offer, offer with { UpdateId = "0ab6b8a0-637a-46b1-9096-e68dfa1d5d6a",
                 VersionDate = new DateTime(2020, 1, 1) }], true);
        check(!alternatives.Partial && alternatives.Matches.Count == 2 &&
            alternatives.Matches.All(x => !x.IsNewerProven && !x.CanInstall),
            "G1: múltiplas ofertas são alternativas consultivas, não ranking automático");
    }
}
