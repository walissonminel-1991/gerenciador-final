using GerenciadorAplicativos;

// Nenhum acesso a dispositivos, COM, rede, arquivos de usuário ou atualização.
internal static class DriverCandidateTriageTests
{
    internal static void Run()
    {
        foreach (var value in new[] { "System", "sYsTeM", "FIRMWARE", "Net", "Display",
            "SCSIAdapter", "DiskDrive", "HDC", "USBDevice", "Keyboard", "Mouse" })
            if (!DriverCandidateTriage.ClassDisposition(value).Contains("EXCLUÍDO"))
                throw new Exception("UX4: classe crítica não excluída: " + value);
        foreach (string value in new[] { "MEDIA", "media", "HIDClass", "Camera", "AudioEndpoint" })
            if (!DriverCandidateTriage.ClassDisposition(value).Contains("SOMENTE CONSULTA"))
                throw new Exception("UX4: classe somente consultiva promovida: " + value);
        foreach (string value in new[] { "Printer", "Image", "Ports" })
            if (!DriverCandidateTriage.ClassDisposition(value).Contains("GRUPO DE ESTUDO"))
                throw new Exception("UX4: grupo restrito não descrito: " + value);
        if (!DriverCandidateTriage.ClassDisposition("").Contains("NÃO CLASSIFICADO") ||
            DriverInventory.NormalizeDriverClass("Pr\ninter") != "" ||
            DriverInventory.NormalizeDriverClass(" Image ") != "Image" ||
            DriverInventory.NormalizeDriverClass(new string('X', 65)) != "")
            throw new Exception("UX4: classe PnP inválida foi promovida a elegibilidade.");

        var classes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!DriverInventory.RecordDriverClass(classes, conflicts, "ID1", "Image") ||
            !DriverInventory.RecordDriverClass(classes, conflicts, "id1", "image") ||
            DriverInventory.RecordDriverClass(classes, conflicts, "ID1", "System") ||
            DriverInventory.RecordDriverClass(classes, conflicts, "ID1", "Image") ||
            classes["id1"] != "" || !conflicts.Contains("ID1"))
            throw new Exception("UX4: classe divergente de DeviceId duplicado não foi revogada.");

        const string id = @"USB\VID_1234&PID_0001\ABC";
        const string hw = @"USB\VID_1234&PID_0001";
        var device = new DriverInventoryEntry(id, "Impressora fictícia", "Fornecedor fictício",
            "1.2.3.4", "oem7.inf", true, true)
        {
            ReportedDriverClass = "Printer", HardwareIds = [hw], PnpProblemCode = 0,
            PackageEvidence = new("oem7.inf", DriverStoreState.Mapped)
        };
        var other = new DriverInventoryEntry(@"USB\VID_1234&PID_0001\XYZ",
            "Periférico fictício", "Fornecedor fictício", "1.0", "oem9.inf", true, true)
            { HardwareIds = [hw], ReportedDriverClass = "Ports" };
        var staged = new DriverPublishedPackage(new("oem100.inf", DriverStoreState.Mapped),
            ObservedDevices: 0, DeviceInventoryPartial: false);
        var inv = new DriverInventoryResult([device], false, "fixture")
        {
            PublishedPackages = new([staged], false, true)
        };
        string local = DriverCandidateTriage.Format(inv, device);
        if (!local.Contains("Classe do driver (WMI SignedDriver): Printer") ||
            !local.Contains("GRUPO DE ESTUDO") ||
            !local.Contains("WUA: consulta ainda não solicitada") ||
            !local.Contains("INFs publicados sem dispositivo observado na enumeração: 1") ||
            !local.Contains("não significa pacote órfão") ||
            !local.Contains("Assinatura relatada (WMI)") ||
            !local.Contains("NÃO substitui verificação criptográfica") ||
            !local.Contains("Ação real permanece BLOQUEADA"))
            throw new Exception("UX4: triagem consultiva local incompleta.");

        var unbound = device with { InfName = "", Version = "", Signed = null,
            WithoutSignedDriverAssociation = true, PackageEvidence = null,
            Present = null, PnpProblemCode = 28 };
        string without = DriverCandidateTriage.Format(inv with { Items = [unbound] }, unbound);
        if (!without.Contains("VÍNCULO WMI AUSENTE") ||
            !without.Contains("PRESENÇA NÃO CONFIRMADA") ||
            without.Contains("VERIFICADO SOMENTE NA FICÇÃO"))
            throw new Exception("UX4: sem vínculo foi tratado como prova de instalação.");

        var dup = inv with { Items = [device, device with { Provider = "Outra empresa" }] };
        if (!DriverCandidateTriage.Format(dup, device).Contains("IDENTIDADE BLOQUEADA"))
            throw new Exception("UX4: DeviceId duplicado foi considerado exclusivo.");
        var shared = inv with { Items = [device, other] };
        if (!DriverCandidateTriage.Format(shared, device).Contains("HARDWAREID COMPARTILHADO"))
            throw new Exception("UX4: HardwareID compartilhado não recebeu aviso.");
        if (!DriverCandidateTriage.Format(inv with { Partial = true }, device)
            .Contains("INVENTÁRIO PARCIAL"))
            throw new Exception("UX4: inventário parcial foi promovido a conclusão.");

        var empty = new DriverOfficialResult([], 0, 0, false, "consulta sem ofertas")
        { SearchContext = new("Windows Update", true, "10.0", "X64", DateTimeOffset.UtcNow) };
        if (!DriverCandidateTriage.Format(inv, device, empty).Contains("ZERO ofertas observadas") ||
            !DriverCandidateTriage.Format(inv, device, empty with { Partial = true })
                .Contains("PARCIAL") ||
            !DriverCandidateTriage.Format(inv, device, empty with { SearchContext = null })
                .Contains("NÃO COMPROVADA") ||
            !DriverOfferReview.Summary(inv, empty with { SearchContext = null })
                .Contains("origem Windows Update online não comprovada"))
            throw new Exception("UX4: estados WUA incompletos incorretos.");
        var unlinked = empty with { Offered = 1 };
        if (!DriverCandidateTriage.Format(inv, device, unlinked)
            .Contains("nenhum vínculo PnP comprovado"))
            throw new Exception("UX4: oferta sem vínculo PnP indevidamente aceita.");

        var entry = new DriverOfficialEntryEvidence(hw, "Fornecedor fictício", "Modelo",
            "Printer", "Fabricante", new DateTime(2026, 1, 1), 0, 0);
        var offer = new DriverOfficialOffer("ae4eae4e-1234-4567-a234-1234567890aa",
            1, "Oferta WUA fictícia", hw, "Fornecedor fictício", "Modelo",
            new DateTime(2026, 1, 1))
        {
            DriverClass = "Printer",
            EntryAudit = new(DriverOfficialEntryState.Complete, [entry]),
            Relations = new(DriverOfficialRelationsState.Complete, [], [])
        };
        var match = new DriverOfficialMatch(offer, device.DeviceId, device.DeviceName)
        {
            Installed = new("oem7.inf", device.Provider, device.Version,
                true, DriverStoreState.Mapped, 0, false),
            MatchingWuaEntry = entry
        };
        var official = new DriverOfficialResult([match], 1, 0, false, "fixture")
        { SearchContext = empty.SearchContext };
        string linked = DriverCandidateTriage.Format(inv, device, official);
        if (!linked.Contains("CANDIDATO APENAS") || !linked.Contains("DriverVer ofertado NÃO COMPROVADO") ||
            !linked.Contains("Ação real permanece BLOQUEADA"))
            throw new Exception("UX4: oferta exata não permaneceu consultiva.");
        var critical = device with { ReportedDriverClass = "system" };
        string forbidden = DriverCandidateTriage.Format(
            inv with { Items = [critical] }, critical, official);
        if (!forbidden.Contains("EXCLUÍDO DO LABORATÓRIO") ||
            !forbidden.Contains("BLOQUEADO: identidade PnP ambígua ou classe excluída") ||
            forbidden.Contains("Oferta WUA — CANDIDATO"))
            throw new Exception("UX4: classe crítica recebeu classificação de candidato.");

        // A propriedade original continua sem autorizar instalação, com ou sem UX4.
        if (match.CanInstall || match.IsNewerProven || match.CompletePackageVerified ||
            DriverUpdatePilot.AllowedClass("MEDIA") || DriverUpdatePilot.AllowedClass("HIDClass"))
            throw new Exception("UX4: diagnóstico alterou autoridade do piloto.");
        string report = DriverReadOnlyReport.Format(inv, device, official);
        if (!report.Contains("TRIAGEM DO PC FRACO") ||
            !report.Contains("Classe do driver (WMI SignedDriver)") ||
            !report.Contains("OFERTAS OFICIAIS"))
            throw new Exception("UX4: relatório real omitiu triagem local.");

        var result = DriverInventory.IncludeUnboundProblemDevices(
            [device], new Dictionary<string, int?> { [id] = 0, ["USB\\UNKNOWN"] = 28 },
            new Dictionary<string, string> { ["USB\\UNKNOWN"] = "Desconhecido" },
            new Dictionary<string, string[]>());
        if (result.Length != 2 ||
            result.Single(x => x.DeviceId == id).ReportedDriverClass != "Printer" ||
            result.Single(x => x.DeviceId == "USB\\UNKNOWN").ReportedDriverClass != "")
            throw new Exception("UX4: dispositivo sem driver ganhou classe sem evidência WMI.");
        Console.WriteLine("OK DRIVERS UX4: classe PnP, Store, WUA, identidades e laboratório fail-closed");
    }
}
