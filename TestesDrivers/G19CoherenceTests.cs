using GerenciadorAplicativos;

// G1.9 — fotos sintéticas de PnP, Driver Store e INF. Nenhum arquivo real,
// nenhuma chamada WUA ou execução de manutenção.
internal static class G19CoherenceTests
{
    internal static void Run(Action<bool, string> check)
    {
        const string hardware = @"PCI\VEN_1234&DEV_5678&SUBSYS_00001234";
        const string instance = @"PCI\VEN_1234&DEV_5678\INSTANCE";
        var package = new DriverStoreEvidence("oem42.inf", DriverStoreState.Mapped,
            @"C:\Windows\INF\oem42.inf",
            @"C:\Windows\System32\DriverStore\FileRepository\ficticio\oem42.inf");
        DriverInventoryEntry Device(DriverStoreEvidence? evidence = null) =>
            new(instance, "Hardware sintético", "Fornecedor A", "4.5.6.7",
                "oem42.inf", true, true)
            {
                HardwareIds = [hardware],
                PackageEvidence = evidence ?? package,
                PnpProblemCode = 0
            };
        DriverInventoryResult Snapshot(DriverInventoryEntry item,
            IReadOnlyList<DriverStoreEvidence>? stores = null,
            IReadOnlyList<DriverPublishedPackage>? published = null,
            bool publishedPartial = false,
            bool enumerationComplete = true) =>
            new([item], false, "snapshot sintético G1.9")
            {
                Store = new(stores ?? [package], false, 0),
                PublishedPackages = new(published ??
                    [new DriverPublishedPackage(package, 1, false)],
                    publishedPartial, enumerationComplete)
            };
        var item = Device();
        var snapshot = Snapshot(item);
        var guard = new DriverReportPublicationGuard(snapshot, item, 10);
        var verified = new DriverInfVerificationResult(DriverInfTrust.VerifiedInf,
            new string('A', 64), @"C:\Fake\catalogo.cat", "Fornecedor A");
        var rejected = new DriverInfVerificationResult(DriverInfTrust.Rejected);
        bool AcceptInf(DriverReportPublicationGuard g, DriverInventoryResult inv,
            DriverInventoryEntry selection, DriverInfVerificationResult result) =>
            g.CanPublishInf(inv, selection, 10, CancellationToken.None, result);

        check(guard.IsCurrent(snapshot, item, 10) &&
            AcceptInf(guard, snapshot, item, verified) &&
            AcceptInf(guard, snapshot, item, rejected) &&
            !verified.CanInstall && !verified.CanMaintain,
            "G1.9: INF sintético só pode ser exibido no snapshot de pacote consistente; resultado rejeitado é diagnóstico");

        foreach (var forged in new[]
        {
            verified with { Sha256 = "AA" },
            verified with { Sha256 = new string('G', 64) },
            verified with { Catalog = "" },
            verified with { Signer = "" }
        })
            check(!AcceptInf(guard, snapshot, item, forged),
                "G1.9: confiança INF aparente sem digest/catálogo/signatário não vira prova de assinatura");

        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            check(!guard.CanPublishInf(snapshot, item, 10, cancelled.Token, verified) &&
                !guard.CanPublishInf(snapshot, item, 11, CancellationToken.None, verified) &&
                !guard.CanPublishInf(Snapshot(item), item, 10, CancellationToken.None, verified),
                "G1.9: retorno INF atrasado, geração nova e token cancelado são rejeitados");
        }

        var offer = new DriverOfficialOffer(
            "5819343e-5d44-44c7-a8e3-e2c3c8abe622", 7, "Oferta falsa",
            hardware, "Fornecedor A", "Hardware sintético", new DateTime(2026, 11, 13))
        {
            EntryAudit = DriverOfficialLookup.AuditEntries(
                [new(hardware, "Fornecedor A", "Hardware sintético", "Display",
                    "Fabricante A", new DateTime(2026, 11, 13), 1, 0)])
        };
        var result = DriverOfficialLookup.Match(snapshot, [offer], true);
        check(result.Matches.Count == 1 && !result.Partial &&
            guard.CanPublish(snapshot, item, 10, CancellationToken.None, result),
            "G1.9: um único DeviceId, pacote coerente e oferta WUA fictícia preservam consulta legítima");

        var duplicates = new DriverInventoryResult([item, item with { DeviceName = "Outro nome" }],
            false, "mesma instância duplicada")
        {
            Store = snapshot.Store, PublishedPackages = snapshot.PublishedPackages
        };
        var dupSelection = new DriverReportPublicationGuard(duplicates, item, 10);
        var dupUnselected = new DriverReportPublicationGuard(duplicates, null, 10);
        check(!dupSelection.IsCurrent(duplicates, item, 10) &&
            !dupUnselected.CanPublish(duplicates, null, 10, CancellationToken.None, result),
            "G1.9: DeviceId duplicado não usa a primeira linha como associação INF inequívoca");

        var identicalDuplicates = duplicates with { Items = [item, item] };
        var identicalGuard = new DriverReportPublicationGuard(identicalDuplicates, item, 10);
        check(!identicalGuard.IsCurrent(identicalDuplicates, item, 10) &&
            !identicalGuard.CanPublishInf(identicalDuplicates, item, 10,
                CancellationToken.None, verified),
            "G1.9: nem duplicatas idênticas provam exclusividade da instância selecionada");

        var malformedList = new List<string> { hardware };
        var mutableItem = Device() with { HardwareIds = malformedList };
        var mutableSnapshot = Snapshot(mutableItem);
        var frozen = new DriverReportPublicationGuard(mutableSnapshot, mutableItem, 10);
        check(frozen.IsCurrent(mutableSnapshot, mutableItem, 10),
            "G1.9: evidência mutável original começa consistente");
        malformedList[0] = @"PCI\VEN_DEAD&DEV_BEEF";
        check(!frozen.IsCurrent(mutableSnapshot, mutableItem, 10) &&
            !frozen.CanPublishInf(mutableSnapshot, mutableItem, 10,
                CancellationToken.None, verified),
            "G1.9: mutação in-place de HardwareIds não altera o original congelado");

        var storeList = new List<DriverStoreEvidence> { package };
        var storeItem = Device();
        var mutableStore = Snapshot(storeItem, storeList);
        var frozenStore = new DriverReportPublicationGuard(mutableStore, storeItem, 10);
        storeList[0] = package with { State = DriverStoreState.Inconsistent };
        check(!frozenStore.IsCurrent(mutableStore, storeItem, 10),
            "G1.9: alteração in-place do resultado Driver Store invalida o relatório");

        var publishedList = new List<DriverPublishedPackage>
        {
            new(package, 1, false)
        };
        var publishedItem = Device();
        var mutablePublished = Snapshot(publishedItem, published: publishedList);
        var frozenPublished = new DriverReportPublicationGuard(
            mutablePublished, publishedItem, 10);
        publishedList[0] = new(package with { StorePath = @"C:\Fake\OUTRO.inf" }, 1, false);
        check(!frozenPublished.IsCurrent(mutablePublished, publishedItem, 10),
            "G1.9: mudança in-place de catálogo publicado não preserva publicação antiga");

        foreach (var conflicting in new DriverStoreEvidence[]
        {
            package with { StorePath = @"C:\Fake\outra-pasta\oem42.inf" },
            package with { PublishedPath = @"C:\Fake\INF\oem42.inf" },
            package with { FailureStage = DriverStoreStage.PublishedMismatch },
            package with { NativeError = 5 },
            package with { State = DriverStoreState.Inconsistent }
        })
        {
            var device = Device();
            var inconsistent = Snapshot(device, [conflicting]);
            var mismatch = new DriverReportPublicationGuard(inconsistent, device, 10);
            check(!mismatch.IsCurrent(inconsistent, device, 10) &&
                !mismatch.CanPublishInf(inconsistent, device, 10,
                    CancellationToken.None, verified),
                "G1.9: estado, caminho e erro Driver Store divergentes não publicam INF");
        }

        var repeatedStore = Snapshot(Device(), [package, package]);
        var repeatGuard = new DriverReportPublicationGuard(repeatedStore, repeatedStore.Items[0], 10);
        check(!repeatGuard.IsCurrent(repeatedStore, repeatedStore.Items[0], 10),
            "G1.9: catálogo Store com INF duplicado é inconclusivo");

        var publishedConflict = Snapshot(Device(), published:
            [new(package with { PublishedPath = @"C:\Fake\INF\outro.inf" }, 1, false)]);
        var pubGuard = new DriverReportPublicationGuard(
            publishedConflict, publishedConflict.Items[0], 10);
        check(!pubGuard.IsCurrent(publishedConflict, publishedConflict.Items[0], 10),
            "G1.9: mapeamento publicado contraditório bloqueia evidência de pacote");

        var publishedDuplicated = Snapshot(Device(), published:
            [new(package, 1, false), new(package, 1, false)]);
        var dupPubGuard = new DriverReportPublicationGuard(
            publishedDuplicated, publishedDuplicated.Items[0], 10);
        check(!dupPubGuard.IsCurrent(publishedDuplicated, publishedDuplicated.Items[0], 10),
            "G1.9: mesmo INF repetido no catálogo nativo não é escolhido arbitrariamente");

        var completeAbsent = Snapshot(Device(), published: [], publishedPartial: false);
        var completeGuard = new DriverReportPublicationGuard(
            completeAbsent, completeAbsent.Items[0], 10);
        check(!completeGuard.IsCurrent(completeAbsent, completeAbsent.Items[0], 10),
            "G1.9: catálogo publicado completo sem INF selecionado é contraditório");

        var partialAbsent = Snapshot(Device(), published: [], publishedPartial: true,
            enumerationComplete: false);
        var partialGuard = new DriverReportPublicationGuard(
            partialAbsent, partialAbsent.Items[0], 10);
        check(partialGuard.IsCurrent(partialAbsent, partialAbsent.Items[0], 10) &&
            partialGuard.CanPublishInf(partialAbsent, partialAbsent.Items[0], 10,
                CancellationToken.None, rejected),
            "G1.9: ausência em enumeração parcial não inventa desinstalação nem impede diagnóstico negativo");

        var missingAssociation = Snapshot(Device() with { PackageEvidence = null });
        var missingGuard = new DriverReportPublicationGuard(
            missingAssociation, missingAssociation.Items[0], 10);
        check(!missingGuard.CanPublishInf(missingAssociation, missingAssociation.Items[0],
                10, CancellationToken.None, verified),
            "G1.9: sem associação INF mapeada não apresentar assinatura como confirmada");
    }
}
