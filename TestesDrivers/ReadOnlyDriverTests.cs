using GerenciadorAplicativos;
using System.Text;

internal static class ReadOnlyDriverTests
{
    internal static void Run(Action<bool, string> check)
    {
        const string id = @"PCI\VEN_1234&DEV_5678\INSTANCE1";
        const string hw = @"PCI\VEN_1234&DEV_5678&SUBSYS_00112233";
        const string windows = @"C:\Windows";
        string Section(string device = id, string inf = "oem17.inf", string status = "SUCCESS") =>
            $"""
            >>>  [Device Install (Hardware initiated) - {device}]
            >>>  Section start 2026/10/25 12:34:56.789
                 inf:      Published Inf Path: C:\Windows\INF\{inf}
            <<<  Section end 2026/10/25 12:35:00.000
            <<<  [Exit status: {status}]
            """;
        DriverWindowsHistoryResult Parse(string text) => DriverWindowsHistory.Parse(text, windows);
        var log = Parse(Section());
        check(!log.Partial && log.Events.Count == 1 && log.Events[0].Outcome == DriverInstallOutcome.Succeeded &&
            log.Events[0].Started.Kind == DateTimeKind.Unspecified,
            "D2: seção explícita preserva data local, dispositivo, INF e sucesso sem inventar fuso");
        var device = new DriverInventoryEntry(id, "Vídeo", "Vendor", "1.0", "OEM17.INF", true, true)
            { HardwareIds = [hw] };
        check(DriverWindowsHistory.Correlate(device, log).Length == 1 &&
            DriverWindowsHistory.Correlate(device with { DeviceId = id + "2" }, log).Length == 0 &&
            DriverWindowsHistory.Correlate(device with { InfName = "oem18.inf" }, log).Length == 0,
            "D2/R6: correlação exige o mesmo dispositivo E INF, sem semelhança ou fabricante");
        check(Parse(Section().Replace("inf:      Published Inf Path:", "ndv:      Driver INF Path:")).Events.Count == 1 &&
            Parse(Section().Replace("inf:      Published Inf Path:", "ndv:      Infpath=")).Events.Count == 1,
            "D2: campos explícitos ndv do INF instalado também são reconhecidos");
        check(Parse(Section().Replace("inf:      Published Inf Path:", "ndv:      INF path:")).Events.Count == 0,
            "D2: caminho de entrada de um instalador não é promovido a INF instalado");
        check(Parse(Section(status: "FAILURE(0x00000005)")).Events.Single().Outcome == DriverInstallOutcome.Failed &&
            Parse(Section().Replace("[Exit status: SUCCESS]", "[Exit]")).Events.Single().Outcome == DriverInstallOutcome.Unknown,
            "D2: falha e resultado desconhecido não são sucesso");
        string classic = Section().Replace("Section start 2026/10/25 12:34:56.789", "2026/10/25 12:34:56.789: Section start")
            .Replace("[Exit status: SUCCESS]", "[Exit Status(0x00000000)]");
        check(Parse(classic).Events.Single().Outcome == DriverInstallOutcome.Succeeded,
            "D2: formato documentado antigo e formato moderno do log reconhecidos");
        foreach (string malformed in new[]
        {
            Section().Replace("2026/10/25 12:34:56.789", "data inválida"),
            Section().Replace("<<<  Section end", "sem terminador"),
            Section().Replace("<<<  [Exit status: SUCCESS]", ""),
            Section().Replace("C:\\Windows\\INF\\oem17.inf", "C:\\Windows\\INF\\..\\oem17.inf"),
            Section().Replace("C:\\Windows\\INF\\oem17.inf", "C:\\OutraPasta\\oem17.inf"),
            Section().Replace("<<<  Section end", "inf: Published Inf Path: C:\\Windows\\INF\\oem18.inf\n<<<  Section end"),
            Section().Replace("<<<  Section end", ">>>  [Other operation - outro]\n<<<  Section end")
        })
            check(Parse(malformed).Partial && Parse(malformed).Events.Count == 0,
                "D2: fragmento, data, caminho, múltiplos INFs ou seção cruzada não cria vínculo");
        check(DriverWindowsHistory.Parse(Section(), windows, tailOnly: true).Partial &&
            DriverWindowsHistory.Parse(Section(), windows, maximumEvents: 1).Partial,
            "D2: recorte e teto de eventos permanecem parciais");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        bool stopped = false;
        try { DriverWindowsHistory.Parse(Section(), windows, cancelled.Token); }
        catch (OperationCanceledException) { stopped = true; }
        check(stopped, "D2: parser interrompe com cancelamento, sem publicar resultado tardio");
        string folder = Path.Combine(Path.GetTempPath(), "GF-drivers-readonly-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "setupapi.dev.log");
            foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(true), Encoding.Unicode, Encoding.BigEndianUnicode })
            {
                File.WriteAllText(path, Section(), encoding);
                check(DriverWindowsHistory.Read(path, windows, default).Events.Count == 1,
                    "D2: arquivo descartável UTF-8/UTF-16 lido sem alterar conteúdo");
            }
            File.WriteAllText(path, new string('x', 4_194_304) + "\n" + Section(), new UTF8Encoding(false));
            var tail = DriverWindowsHistory.Read(path, windows, default);
            check(tail.TailOnly && tail.Partial && tail.Events.Count == 1,
                "D2: log grande lê só cauda limitada e conserva restrição de cobertura");
            check(DriverWindowsHistory.Read(path + "-ausente", windows, default).Unavailable,
                "D2: log ausente não significa ausência de histórico");
            File.WriteAllBytes(path, [0xff, 0xff, 0xff]);
            check(DriverWindowsHistory.Read(path, windows, default).Unavailable,
                "D2: codificação inválida não inventa evidência");
        }
        finally { Directory.Delete(folder, true); }

        var inventory = new DriverInventoryResult([device], false, "fixture");
        DriverStoreEvidence Resolve(string inf) => new(inf, DriverStoreState.Mapped);
        var published = DriverPublishedPackages.Capture(inventory, default,
            () => ["oem17.inf", "OEM17.INF", "oem18.inf"], Resolve);
        check(published.EnumerationComplete && !published.Partial && published.Packages.Count == 2 &&
            published.Packages.Single(p => p.Evidence.InfName == "oem18.inf").ObservedDevices == 0 &&
            published.Packages.All(p => !p.CanMaintain && !p.Evidence.CanMaintain),
            "D2/R6: pacote sem dispositivo é exibido e preservado, duplicata exata reconciliada");
        var partial = DriverPublishedPackages.Capture(inventory with { Partial = true }, default,
            () => ["oem18.inf"], Resolve);
        check(partial.Partial && partial.Packages.Single().AssociationLabel.Contains("desconhecida"),
            "D2/R6: inventário incompleto não declara pacote sem associação");
        check(DriverPublishedPackages.Capture(inventory, default, () => ["../oem17.inf"], Resolve).Partial &&
            DriverPublishedPackages.Capture(inventory, default, () => throw new UnauthorizedAccessException(), Resolve).Partial &&
            !DriverPublishedPackages.Capture(inventory, default, () => ["oem17.inf", "oem18.inf"], Resolve, maximumPackages: 1).EnumerationComplete &&
            DriverPublishedPackages.Capture(inventory, default, () => ["oem17.inf"], _ => new("oem99.inf", DriverStoreState.Mapped)).Partial,
            "D2: lista insegura, acesso negado, teto e retorno de outro INF não aprovam consulta");

        var offer = new DriverOfficialOffer("1d938bf4-6cef-45bd-92e2-7a034d280c6e", 1,
            "Vendor - Display - 2.0", hw, "Vendor", "Display", new DateTime(2026, 10, 1));
        var official = DriverOfficialLookup.Match(inventory, [offer], true);
        check(!official.Partial && official.Matches.Count == 1 && !official.Matches.Single().CanInstall,
            "G1: oferta oficial com hardware exato é consultiva e nunca autoriza instalação");
        foreach (var wrong in new[]
        {
            offer with { HardwareId = @"PCI\VEN_1234&DEV_5678" },
            offer with { HardwareId = hw + "2" }, offer with { HardwareId = hw + "*" },
            offer with { HardwareId = hw + " " }, offer with { UpdateId = "não-guid" },
            offer with { UpdateId = Guid.Empty.ToString() }, offer with { Revision = 0 },
            offer with { Provider = "" }, offer with { Model = "" }
        })
            check(DriverOfficialLookup.Match(inventory, [wrong], true).Matches.Count == 0,
                "G1: mesmo modelo/título/fabricante não substitui ID exato e metadados válidos");
        check(DriverOfficialLookup.Match(inventory, [offer, offer with { Model = "Divergente" }], true).Partial &&
            DriverOfficialLookup.Match(inventory, [offer, offer with { Model = "Divergente" }], true).Matches.Count == 0,
            "G1: ID/revisão com metadados conflitantes não é selecionado");
        check(DriverOfficialLookup.Match(inventory, [offer, offer], true).Matches.Count == 1 &&
            DriverOfficialLookup.Match(inventory, [offer with { HardwareId = hw.ToLowerInvariant() }], true).Matches.Count == 1,
            "G1: duplicata idêntica e caixa do ID não alteram a correspondência");
        check(DriverOfficialLookup.Match(inventory with { Partial = true }, [offer], true).Matches.Count == 0 &&
            DriverOfficialLookup.Match(inventory, [offer], false).Matches.Count == 0 &&
            DriverOfficialLookup.Match(inventory with { Items = [device with { Present = false }] }, [offer], true).Matches.Count == 0 &&
            DriverOfficialLookup.Match(inventory with { Items = [device with { HardwareIds = [] }] }, [offer], true).Matches.Count == 0,
            "G1: WUA parcial, dispositivo ausente e hardware desconhecido não concedem vínculo");
        check(DriverOfficialLookup.Match(inventory with { Items = [device with { DeviceId = "" }] }, [offer], true).Matches.Count == 0,
            "G1: HardwareID sem identidade de dispositivo não cria associação");
        check(DriverOfficialLookup.Match(inventory, [], true) is { Partial: false, Offered: 0 } &&
            DriverOfficialLookup.Match(inventory, [], false).Partial,
            "G1: consulta vazia bem-sucedida é diferente de falha da fonte");
        check(DriverInfVerification.Interpret(true, 0, true, true) == DriverInfTrust.VerifiedInf &&
            DriverInfVerification.Interpret(false, unchecked((int)0xE0000241), true, true) == DriverInfTrust.VerifiedInf &&
            DriverInfVerification.Interpret(false, unchecked((int)0xE0000242), true, true) == DriverInfTrust.PublisherNotEstablished &&
            DriverInfVerification.Interpret(false, 5, true, true) == DriverInfTrust.Rejected &&
            DriverInfVerification.Interpret(true, 0, false, true) == DriverInfTrust.Rejected,
            "G2: confiança criptográfica distingue WHQL/AuthentiCode, editor não estabelecido e erro");
        check(!new DriverInfVerificationResult(DriverInfTrust.VerifiedInf).CanInstall &&
            !new DriverInfVerificationResult(DriverInfTrust.VerifiedInf).CanMaintain,
            "G2/R6: até INF verificado permanece sem autorização de escrita ou limpeza");
    }
}
