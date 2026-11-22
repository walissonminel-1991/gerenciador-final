using GerenciadorAplicativos;

internal static class DriverWuaPnpMatchingTests
{
    internal static void Run(Action<bool, string> check)
    {
        const string primary = @"USB\VID_1111&PID_0001";
        const string alternate = @"USB\VID_1111&PID_0002";
        const string compatible = @"USB\Class_07&SubClass_01";
        const string legacy = "*PNP0501";
        var date = new DateTime(2026, 11, 1);
        var context = new DriverOfficialSearchContext("Windows Update", true, "10.0", "X64", DateTimeOffset.UtcNow);

        DriverInventoryEntry Device(string id, string hardware, params string[] compatibility) =>
            new($@"USB\GF_FIXTURE\{id}", id, "Fornecedor de teste", "1.2.3.4", "oem7.inf", true, true)
            {
                HardwareIds = [hardware],
                CompatibleIds = compatibility
            };
        DriverOfficialEntryEvidence Entry(string id) =>
            new(id, "Fornecedor de teste", "Impressora de teste", "Printer", "Fabricante de teste", date, 0, 0);

        var main = Device("Principal", primary, compatible);
        var second = Device("Adicional", alternate);
        var third = Device("Compatível", @"USB\VID_2222&PID_0003", compatible.ToLowerInvariant(), legacy);
        var inventory = new DriverInventoryResult([main, second, third], false, "sintético");
        var offer = new DriverOfficialOffer("02e9d828-7fc6-4d7d-9290-568ae92fe1f5", 1, "Oferta sintética",
            primary, main.Provider, "Impressora de teste", date)
        {
            DriverClass = "Printer",
            Relations = new(DriverOfficialRelationsState.Complete, [], []),
            EntryAudit = DriverOfficialLookup.AuditEntries([Entry(compatible), Entry(alternate), Entry(primary)])
        };
        DriverOfficialResult Search(DriverInventoryResult source, params DriverOfficialOffer[] offers) =>
            DriverOfficialLookup.Match(source, offers, true) with { SearchContext = context };

        var result = Search(inventory, offer);
        var matches = result.Matches.ToDictionary(m => m.DeviceId);
        check(!result.Partial && result.Unmatched == 0 && matches.Count == 3 &&
              matches.GetValueOrDefault(main.DeviceId) is { Kind: DriverOfficialMatchKind.PrimaryHardwareId, MatchedId: primary } &&
              matches.GetValueOrDefault(second.DeviceId) is { Kind: DriverOfficialMatchKind.AdditionalHardwareId, MatchedId: alternate } &&
              matches.GetValueOrDefault(third.DeviceId) is { Kind: DriverOfficialMatchKind.CompatibleId, MatchedId: compatible },
            "WUA distingue HardwareID principal, entrada adicional e ID compatível sem duplicar vínculos");
        check(result.Matches.All(m => !m.CanInstall && !DriverUpdatePilot.TryPrepare(inventory, result, m, out _)),
            "WUA de múltiplas entradas permanece consultivo em todas as instâncias");

        var legacyOffer = offer with
        {
            HardwareId = legacy,
            EntryAudit = DriverOfficialLookup.AuditEntries([Entry(legacy)])
        };
        var legacyResult = Search(inventory, legacyOffer);
        var malformed = Search(inventory, legacyOffer with { HardwareId = "*PNP05*" });
        check(legacyResult.Matches is [{ Kind: DriverOfficialMatchKind.CompatibleId, MatchedId: legacy }] &&
              malformed.Partial && malformed.Matches.Count == 0,
            "WUA aceita ID PnP legado literal e rejeita padrões com curingas");

        var single = offer with { EntryAudit = DriverOfficialLookup.AuditEntries([Entry(primary)]) };
        var one = Search(inventory, single);
        check(one.Matches.Count == 1 && DriverUpdatePilot.TryPrepare(inventory, one, one.Matches[0], out _),
            "WUA preserva a revisão experimental de oferta principal com alvo exclusivo");
        var otherOccupant = second with { CompatibleIds = [primary], Present = null };
        var additionalTargets = inventory with { Items = [main, otherOccupant, third] };
        var withoutAssociation = inventory with { OtherPnpTargetIds = [primary.ToLowerInvariant()] };
        check(one.Matches.Count == 1 &&
              !DriverUpdatePilot.TryPrepare(additionalTargets, one, one.Matches[0], out _) &&
              !DriverUpdatePilot.TryPrepare(withoutAssociation, one, one.Matches[0], out _),
            "WUA bloqueia alvo adicional compatível com presença incerta ou sem associação WMI");

        var guard = new DriverReportPublicationGuard(inventory, third, 1);
        var compatibleLink = matches[third.DeviceId];
        check(guard.CanPublish(inventory, third, 1, CancellationToken.None, result) &&
              !guard.CanPublish(inventory, third, 1, CancellationToken.None, result with
              {
                  Matches = [compatibleLink with { Kind = DriverOfficialMatchKind.PrimaryHardwareId }]
              }),
            "WUA só publica a origem de identificação demonstrada na mesma fotografia PnP");
        ((string[])third.CompatibleIds)[0] = alternate;
        check(!guard.CanPublish(inventory, third, 1, CancellationToken.None, result),
            "WUA revoga resultado quando IDs compatíveis mudam durante a consulta");
    }
}
