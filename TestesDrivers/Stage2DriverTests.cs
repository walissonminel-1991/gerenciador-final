using GerenciadorAplicativos;

// IDs e logs inteiramente sintéticos. Nenhum pacote é baixado ou executado.
internal static class Stage2DriverTests
{
    internal static void Run(Action<bool, string> check)
    {
        const string id = @"PCI\VEN_1234&DEV_5678\INSTANCE1";
        const string hw = @"PCI\VEN_1234&DEV_5678&SUBSYS_00112233";
        const string otherHw = @"PCI\VEN_1234&DEV_5679&SUBSYS_00112233";
        var device = new DriverInventoryEntry(id, "Dispositivo sintético", "Fornecedor", "1.0", "oem17.inf", true, true)
            { HardwareIds = [hw] };
        var inventory = new DriverInventoryResult([device], false, "fixture Etapa 2");
        var offer = new DriverOfficialOffer("1d938bf4-6cef-45bd-92e2-7a034d280c6e", 1,
            "Título genérico, sem versão confiável", hw, "Fornecedor", "Modelo", new DateTime(2026, 10, 1));
        DriverOfficialResult Match(params DriverInventoryEntry[] items) =>
            DriverOfficialLookup.Match(inventory with { Items = items }, [offer], true);
        foreach (var conflict in new (string Field, DriverInventoryEntry Row)[]
        {
            ("INF", device with { InfName = "oem18.inf" }),
            ("versão", device with { Version = "2.0" }),
            ("fornecedor", device with { Provider = "Outro fornecedor" }),
            ("assinatura WMI", device with { Signed = false }),
            ("presença", device with { Present = false }),
            ("presença desconhecida", device with { Present = null }),
            ("hardware", device with { HardwareIds = [otherHw] }),
            ("hardware desconhecido", device with { HardwareIds = [] }),
            ("ID canônico", device with { DeviceId = " " + id.ToLowerInvariant() + " ", InfName = "oem18.inf" })
        })
        {
            var result = Match(device, conflict.Row);
            var reverse = Match(conflict.Row, device);
            check(result.Partial && result.Matches.Count == 0 && reverse.Partial && reverse.Matches.Count == 0,
                "ETAPA2: conflito de " + conflict.Field + " do mesmo dispositivo é inconclusivo nas duas ordens");
        }
        var repeated = Match(device, device, device with
        {
            DeviceId = id.ToLowerInvariant(), DeviceName = "Nome traduzido",
            InfName = "OEM17.INF", Provider = "FORNECEDOR", HardwareIds = [hw.ToLowerInvariant(), hw]
        });
        check(!repeated.Partial && repeated.Matches.Count == 1,
            "ETAPA2: repetição equivalente, nome traduzido e caixa não criam ambiguidade");
        var shared = Match(device, device with { DeviceId = id + "2" });
        check(!shared.Partial && shared.Matches.Count == 2 && shared.Matches.All(m => !m.CanInstall),
            "ETAPA2: duas instâncias distintas do mesmo hardware conservam dois vínculos consultivos");
        var twoIds = device with { HardwareIds = [hw, otherHw] };
        var reordered = Match(twoIds, twoIds with { HardwareIds = [otherHw.ToLowerInvariant(), hw] });
        check(!reordered.Partial && reordered.Matches.Count == 1,
            "ETAPA2: ordem de IDs de hardware não transforma evidência equivalente em conflito");
        var similar = Match(device with { HardwareIds = [otherHw, @"PCI\VEN_1234&DEV_5678"] });
        check(!similar.Partial && similar.Matches.Count == 0 && similar.Offered == 1 && similar.Unmatched == 1,
            "ETAPA2: oferta encontrada continua sem vínculo para hardware parecido ou ID genérico");
        foreach (var incomplete in new[] { offer with { VersionDate = null }, offer with { VersionDate = default(DateTime) } })
        {
            var result = DriverOfficialLookup.Match(inventory, [incomplete], true);
            check(result.Partial && result.Matches.Count == 0,
                "ETAPA2: data oficial ausente ou sentinela não é metadado completo");
        }
        DriverOfficialResult? malformed = null;
        try { malformed = DriverOfficialLookup.Match(inventory, [offer with { UpdateId = null! }], true); }
        catch (NullReferenceException) { }
        check(malformed is { Partial: true, Matches.Count: 0 },
            "ETAPA2: identidade oficial nula retorna inconclusivo sem exceção");
        var mixed = DriverOfficialLookup.Match(inventory,
            [offer, offer with { UpdateId = "2d938bf4-6cef-45bd-92e2-7a034d280c6e", VersionDate = null }], true);
        check(mixed.Partial && mixed.Matches.Count == 0,
            "ETAPA2: lote oficial incompleto não publica subconjunto como consulta aprovada");

        var correlated = DriverInventory.Correlate([device, device with { HardwareIds = [otherHw] }],
            new Dictionary<string, bool?> { [id] = true });
        check(correlated.Count == 2 &&
            DriverOfficialLookup.Match(inventory with { Items = correlated }, [offer], true) is { Partial: true, Matches.Count: 0 },
            "ETAPA2: correlação WMI conserva HardwareIDs divergentes para impedir falso vínculo posterior");
        var named = DriverInventory.Correlate([device with { DeviceId = " " + id + " ", DeviceName = "" }],
            new Dictionary<string, bool?> { [id] = true }, new Dictionary<string, string> { [id] = "Nome PnP exato" });
        check(named.Single().DeviceName == "Nome PnP exato" && named.Single().Present == true,
            "ETAPA2: nome e presença usam a mesma identidade normalizada sem perder o fallback PnP");

        string Section(string header) => $"""
            {header}
            >>> Section start 2026/10/25 12:34:56.789
                inf: Published Inf Path: C:\Windows\INF\oem17.inf
            <<< Section end 2026/10/25 12:35:00.000
            <<< [Exit status: SUCCESS]
            """;
        string valid = Section($">>> [Device Install (Hardware initiated) - {id}]");
        foreach (string header in new[]
        {
            $">>> [Device Install (Hardware initiated) - {id}",
            $">>> [Device Install (Hardware initiated) : {id}]",
            $">>> [Device Install (Hardware initiated) - {id}] texto adicional"
        })
        {
            var result = DriverWindowsHistory.Parse(Section(header), @"C:\Windows");
            check(result.Partial && result.RejectedSections == 1 && result.Events.Count == 0,
                "ETAPA2: seção Device Install não reconhecida é rejeitada e torna a cobertura parcial");
        }
        var mixedHistory = DriverWindowsHistory.Parse(valid + "\n" + Section(">>> [Device Install sem identidade]"), @"C:\Windows");
        check(mixedHistory.Partial && mixedHistory.Events.Count == 1 && mixedHistory.RejectedSections == 1,
            "ETAPA2: seção válida é conservada sem esconder outra seção de instalação malformada");
        var unrelated = DriverWindowsHistory.Parse(valid + "\n" + Section(">>> [Other operation - exemplo]"), @"C:\Windows");
        check(!unrelated.Partial && unrelated.Events.Count == 1 && unrelated.RejectedSections == 0,
            "ETAPA2: seção de outra operação não é confundida com instalação de dispositivo");

        DriverStoreEvidence Resolve(string inf) => new(inf, DriverStoreState.Mapped);
        var unknown = DriverPublishedPackages.Capture(inventory with { Items = [device with { DeviceId = " " }] },
            default, () => ["oem17.inf"], Resolve);
        check(unknown.Partial && unknown.EnumerationComplete && unknown.Packages.Single() is
            { ObservedDevices: 0, DeviceInventoryPartial: true } &&
            unknown.Packages.Single().AssociationLabel.Contains("desconhecida"),
            "ETAPA2: INF com identidade vazia não inventa dispositivo nem afirma ausência de associação");
        var known = DriverPublishedPackages.Capture(inventory with { Items = [device, device with { DeviceId = id.ToLowerInvariant() }] },
            default, () => ["oem17.inf", "oem18.inf"], Resolve);
        check(!known.Partial && known.Packages.Single(p => p.Evidence.InfName == "oem17.inf").ObservedDevices == 1 &&
            known.Packages.Single(p => p.Evidence.InfName == "oem18.inf").ObservedDevices == 0 &&
            known.Packages.All(p => !p.CanMaintain),
            "ETAPA2: enumeração completa distingue associação exata e pacote preservado sem dispositivo observado");
    }
}
