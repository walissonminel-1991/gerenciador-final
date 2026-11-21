using GerenciadorAplicativos;

// G1.8: somente fotografias PnP e ofertas WUA artificiais, sem sessão WUA.
internal static class G18PublicationTests
{
    internal static void Run(Action<bool, string> check)
    {
        var device = new DriverInventoryEntry(
            @"PCI\VEN_1234&DEV_5678\INSTANCE", "Placa fictícia",
            "Fornecedor A", "4.5.6.7", "oem42.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1234&DEV_5678&SUBSYS_00001234"],
            PackageEvidence = new("oem42.inf", DriverStoreState.Mapped),
            PnpProblemCode = 0
        };
        var inventory = new DriverInventoryResult([device], false, "fotografia G1.8");
        var evidence = DriverOfficialLookup.AuditEntries(
            [new(@"PCI\VEN_1234&DEV_5678&SUBSYS_00001234", "Fornecedor A",
                "Placa fictícia", "Display", "Fabricante A", new DateTime(2026, 11, 13), 1, 0)]);
        var offer = new DriverOfficialOffer(
            "5819343e-5d44-44c7-a8e3-e2c3c8abe622", 7, "Oferta de teste",
            @"PCI\VEN_1234&DEV_5678&SUBSYS_00001234",
            "Fornecedor A", "Placa fictícia", new DateTime(2026, 11, 13))
        {
            EntryAudit = evidence
        };
        var official = DriverOfficialLookup.Match(inventory, [offer], true);
        var guard = new DriverReportPublicationGuard(inventory, device, 31);
        check(official.Matches.Count == 1 && !official.Partial &&
            guard.CanPublish(inventory, device, 31, CancellationToken.None, official) &&
            !official.Matches.Single().CanInstall,
            "G1.8: oferta consultiva sintética só publica na geração PnP correta");

        // Inventário novo (mesmo ID e conteúdo) já é outra geração; não basta
        // comparar DeviceName, o índice na lista ou apenas HardwareID.
        var sameContentNewObject = new DriverInventoryResult([device], false, "nova leitura PnP");
        check(!guard.CanPublish(sameContentNewObject, device, 31, CancellationToken.None, official) &&
            !guard.CanPublish(inventory, device, 32, CancellationToken.None, official) &&
            !guard.CanPublish(inventory, device, 30, CancellationToken.None, official),
            "G1.8: respostas fora de ordem são rejeitadas mesmo com conteúdo visual igual");

        foreach (var changed in new DriverInventoryEntry[]
        {
            device with { DeviceId = @"PCI\VEN_1234&DEV_5678\OTHER" },
            device with { InfName = "oem43.inf" },
            device with { Version = "8.0.0.0" },
            device with { Provider = "Fornecedor B" },
            device with { Signed = false },
            device with { Present = false },
            device with { PnpProblemCode = 28 },
            device with { WithoutSignedDriverAssociation = true },
            device with { HardwareIds = [@"PCI\VEN_1234&DEV_9999"] },
            device with { PackageEvidence = new("oem43.inf", DriverStoreState.Mapped) }
        })
            check(!guard.CanPublish(inventory, changed, 31, CancellationToken.None, official),
                "G1.8: seleção PnP/INF/versão/assinatura/HardwareID divergente não aceita resposta WUA");

        check(!guard.CanPublish(inventory, null, 31, CancellationToken.None, official),
            "G1.8: troca da seleção para nenhum dispositivo invalida resultado anterior");
        var unselected = new DriverReportPublicationGuard(inventory, null, 31);
        check(unselected.IsCurrent(inventory, null, 31) &&
            !unselected.IsCurrent(inventory, device, 31),
            "G1.8: seleção vazia tem identidade distinta da seleção de dispositivo");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        check(!guard.CanPublish(inventory, device, 31, cancelled.Token, official),
            "G1.8: timeout/cancelamento tardio impede publicar resposta mesmo após Search terminar");

        var matched = official.Matches.Single();
        foreach (var bad in new DriverOfficialMatch[]
        {
            matched with { Offer = matched.Offer with { UpdateId = "invalid" } },
            matched with { Offer = matched.Offer with { Revision = 0 } },
            matched with { Offer = matched.Offer with { UpdateId = Guid.Empty.ToString("D") } },
            matched with { Offer = matched.Offer with { HardwareId = @"PCI\VEN_1111&DEV_2222" } },
            matched with { DeviceId = @"PCI\VEN_1234&DEV_5678\OUTRA" },
            matched with { Installed = matched.Installed! with { Version = "9.9.9.9" } },
            matched with { Installed = matched.Installed! with { InfName = "oem99.inf" } },
            matched with { Installed = null }
        })
            check(!guard.CanPublish(inventory, device, 31, CancellationToken.None,
                official with { Matches = [bad] }),
                "G1.8: identidade WUA/revisão/instância/INF inconsistentes impedem publicação");

        check(!guard.CanPublish(inventory, device, 31, CancellationToken.None,
            official with { Partial = true }) &&
            guard.CanPublish(inventory, device, 31, CancellationToken.None,
                official with { Partial = true, Matches = [] }),
            "G1.8: resposta parcial não expõe vínculos; status parcial vazio pode ser exibido");

        var newerRevision = offer with
        {
            UpdateId = "df8959b8-40cf-4d6d-ab42-a0aa2bdf9aa6",
            Revision = 8
        };
        var newerOfficial = DriverOfficialLookup.Match(sameContentNewObject, [newerRevision], true);
        var newGuard = new DriverReportPublicationGuard(sameContentNewObject, device, 32);
        check(newerOfficial.Matches.Count == 1 &&
            !guard.CanPublish(sameContentNewObject, device, 32, CancellationToken.None, official) &&
            newGuard.CanPublish(sameContentNewObject, device, 32, CancellationToken.None, newerOfficial) &&
            !newerOfficial.Matches.Single().IsNewerProven,
            "G1.8: geração nova aceita sua oferta ID/revisão novos sem classificar driver como mais novo");

        // Se o item saiu do inventário, uma seleção ainda em memória não basta.
        var removed = new DriverInventoryResult([], false, "PnP sem instância");
        var removedGuard = new DriverReportPublicationGuard(removed, device, 33);
        check(!removedGuard.IsCurrent(removed, device, 33) &&
            !removedGuard.CanPublish(removed, device, 33, CancellationToken.None, official),
            "G1.8: dispositivo não presente na fotografia corrente bloqueia publicação");
    }
}
