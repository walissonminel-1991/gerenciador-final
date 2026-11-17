using GerenciadorAplicativos;

// Somente dados sintéticos; nenhum driver, rede ou API Windows é acessado.
internal static class DriverUxGuidanceTests
{
    internal static void Run()
    {
        foreach (var (code, fragment) in new (int?, string)[]
        {
            (null, "não informado"), (0, "não garante"), (10, "não consegue iniciar"),
            (22, "desativado"), (28, "não instalados"), (31, "não consegue carregar"),
            (43, "não distingue"), (52, "Não desative"), (987, "sem explicação"),
            (-1, "não válido")
        })
        {
            if (!DriverPnpGuidance.Describe(code).Contains(fragment,
                StringComparison.OrdinalIgnoreCase))
                throw new Exception("UX2: orientação PnP incorreta para " + code);
        }

        const string hw = @"PCI\VEN_1111&DEV_4444";
        const string id = @"PCI\UX\DEVICE";
        var device = new DriverInventoryEntry(id, "Dispositivo fictício",
            "Fornecedor fictício", "1.0.0.0", "oem7.inf", true, true)
        { HardwareIds = [hw], PnpProblemCode = 28 };
        var inventory = new DriverInventoryResult([device], false, "fixture");
        var entry = new DriverOfficialEntryEvidence(hw, "Fornecedor fictício", "Camera",
            "Image", "Fabricante", new DateTime(2026, 1, 1), 0, 0);
        var offer = new DriverOfficialOffer("ae4eae4e-1234-4567-a234-1234567890aa", 1,
            "Oferta fictícia", hw, "Fornecedor fictício", "Camera", new DateTime(2026, 1, 1))
        {
            DriverClass = "Image",
            EntryAudit = new(DriverOfficialEntryState.Complete, [entry]),
            Relations = new(DriverOfficialRelationsState.Complete, [], [])
        };
        var match = new DriverOfficialMatch(offer, id, device.DeviceName)
        {
            Installed = new("oem7.inf", "Fornecedor fictício", "1.0.0.0", true, null, 28, false),
            MatchingWuaEntry = entry
        };
        var official = new DriverOfficialResult([match], 1, 0, false, "consulta fictícia")
        { SearchContext = new("Windows Update", true, "10.0", "X64", DateTimeOffset.UtcNow) };
        if (!DriverUpdatePilot.TryPrepare(inventory, official, match, out var choice) ||
            choice is null)
            throw new Exception("UX2: fixture não aprovada pelos gates pré-existentes.");
        if (!DriverOfferReview.Explain(inventory, official, match).Contains("CANDIDATO APENAS") ||
            !DriverOfferReview.Summary(inventory, official).Contains("1 candidatos"))
            throw new Exception("UX2: candidato conservador não descrito.");
        var disallowed = match with { Offer = offer with { DriverClass = "Display" } };
        if (!DriverOfferReview.Explain(inventory, official, disallowed).Contains("classe fora") ||
            !DriverOfferReview.Explain(inventory with { Partial = true }, official, match).Contains("parcial") ||
            !DriverOfferReview.Explain(inventory with { Items = [device, device] }, official, match).Contains("identidade PnP"))
            throw new Exception("UX2: motivos de bloqueio WUA incorretos.");
        var report = DriverReadOnlyReport.Format(inventory, device, official);
        if (!report.Contains("Orientação consultiva PnP") ||
            !report.Contains("Triagem pré-download") ||
            !report.Contains("CANDIDATO APENAS") ||
            match.CanInstall || match.IsNewerProven || match.CompletePackageVerified)
            throw new Exception("UX2: relatório ou política de driver alterados.");
        Console.WriteLine("OK DRIVERS UX2: códigos PnP e motivos WUA, sem relaxar autoridade.");
    }
}
