using GerenciadorAplicativos;

internal static class DriverHardwareCatalogTests
{
    internal static void Run()
    {
        void Check(bool valid, string name)
        {
            if (!valid) throw new InvalidOperationException("Catálogo de drivers: " + name);
            Console.WriteLine("OK catálogo de drivers: " + name);
        }

        var gpu = new DriverInventoryEntry(@"PCI\VEN_10DE&DEV_2D04&REV_A1\3&01",
            "Adaptador de Vídeo Básico da Microsoft", "Microsoft", "10.0.26100.1", "display.inf", true, true);
        Check(DriverHardwareCatalog.Identify(gpu).Vendor.Contains("NVIDIA", StringComparison.Ordinal) &&
            DriverHardwareCatalog.DisplayName(gpu).Contains("GeForce RTX 5060 Ti", StringComparison.Ordinal),
            "o ID de instância PCI identifica a RTX 5060 Ti mesmo sem HardwareID");
        // VF02 fase 1: GPU/versão Windows por mesmo DeviceId, sem criar
        // oferta Game Ready, canal presumido ou elegibilidade Display.
        var rtx = gpu with { ReportedDriverClass = "Display" };
        var found = GraphicsDriverInventory.Find(new([rtx], false, "consulta completa"));
        Check(found is [{ Manufacturer: "NVIDIA", InstalledWindowsVersion: "10.0.26100.1",
                         InstalledChannel: GraphicsDriverInventory.UnknownChannel }] &&
            found[0].Model.Contains("RTX 5060 Ti", StringComparison.Ordinal) &&
            found[0].ModelSource.Contains("catálogo PCI", StringComparison.Ordinal) &&
            found[0].VersionSource.Contains("Win32_PnPSignedDriver.DriverVersion", StringComparison.Ordinal) &&
            !DriverUpdatePilot.AllowedClass("Display"),
            "VF02 lê RTX 5060 Ti e versão Windows por PCI/WMI; canal desconhecido e Display bloqueado");
        Check(GraphicsDriverInventory.CompactSummary(new([rtx], false, ""))
                .Contains("Canal: não identificado", StringComparison.Ordinal) &&
            DriverReadOnlyReport.Format(new([rtx], false, ""), null)
                .Contains("DRIVERS DE JOGOS — IDENTIFICAÇÃO LOCAL", StringComparison.Ordinal) &&
            DriverReadOnlyReport.Format(new([rtx], false, ""), rtx)
                .Contains("Versão do driver Windows: 10.0.26100.1", StringComparison.Ordinal),
            "VF02 mostra evidência gráfica na aba e no relatório geral/selecionado");

        var amdDisplay = new DriverInventoryEntry(@"PCI\VEN_1002&DEV_A123\LOCAL",
            "Radeon de fixture", "AMD", "32.0.1.1234", "oem3.inf", true, true)
            { ReportedDriverClass = "Display" };
        var intelDisplay = new DriverInventoryEntry(@"PCI\VEN_8086&DEV_B123\LOCAL",
            "Intel Arc de fixture", "Intel", "31.0.1.1234", "oem4.inf", true, true)
            { ReportedDriverClass = "Display" };
        var graphics = GraphicsDriverInventory.Find(new([rtx, amdDisplay, intelDisplay], false, ""));
        Check(graphics.Length == 3 &&
            graphics.Any(x => x.Manufacturer == "AMD" && x.Model == "Radeon de fixture") &&
            graphics.Any(x => x.Manufacturer == "Intel" && x.Model == "Intel Arc de fixture") &&
            graphics.All(x => x.InstalledChannel == GraphicsDriverInventory.UnknownChannel),
            "VF02 estrutura AMD/Intel por PCI + classe Display, sem afirmar canais instalados");

        var intelNonGpu = intelDisplay with { ReportedDriverClass = "Net", DeviceName = "Rede Intel" };
        var conflictingRtx = rtx with { HardwareIds = [@"PCI\VEN_1002&DEV_A123"] };
        var malformedId = rtx with { DeviceId = @"PCI\VEN_10DE&DEV_2D04X\LOCAL" };
        Check(GraphicsDriverInventory.Find(new([intelNonGpu, conflictingRtx, malformedId], false, "")).Length == 0,
            "VF02 recusa PCI de rede, IDs de GPU conflitantes e identificador malformado");
        var duplicates = GraphicsDriverInventory.Find(new([rtx, rtx], false, ""));
        var partial = GraphicsDriverInventory.Find(new([rtx], true, "parcial"));
        var unknownClass = GraphicsDriverInventory.Find(new([gpu], false, "classe indisponível"));
        Check(duplicates is [{ InstalledWindowsVersion: "não confirmada" }] &&
            partial is [{ InstalledWindowsVersion: "não confirmada" }] &&
            unknownClass is [{ Manufacturer: "NVIDIA", InstalledWindowsVersion: "não confirmada" }] &&
            duplicates[0].Observation.Contains("múltiplas", StringComparison.Ordinal) &&
            !DriverUpdatePilot.AllowedClass("Display"),
            "VF02 duplicidade, inventário parcial e classe ausente não confirmam versão nem autorizam instalação");

        // VF02 fase 3: equivalência de numeração NVIDIA DOCUMENTADA, somente
        // para fornecedor NVIDIA e versão Windows íntegra do mesmo Display.
        var nvidiaSigned = rtx with { Provider = "NVIDIA", Version = "32.0.16.1742", InfName = "oem617.inf" };
        var nvidiaInventory = new DriverInventoryResult([nvidiaSigned], false, "consulta completa");
        var nvidiaSnapshot = GraphicsDriverInventory.Find(nvidiaInventory).Single();
        Check(GraphicsDriverInventory.NvidiaPublicVersion(nvidiaSnapshot) == "617.42" &&
            DriverReadOnlyReport.Format(nvidiaInventory, nvidiaSigned)
                .Contains("Versão NVIDIA equivalente (derivada da WMI): 617.42", StringComparison.Ordinal) &&
            nvidiaSnapshot.InstalledWindowsVersion == "32.0.16.1742" &&
            nvidiaSnapshot.InstalledChannel == GraphicsDriverInventory.UnknownChannel &&
            !DriverUpdatePilot.AllowedClass("Display"),
            "VF02 apresenta conversão oficial WMI->NVIDIA sem inferir canal nem liberar Display");
        var nvidiaOlder = GraphicsDriverInventory.Find(new(
            [nvidiaSigned with { Version = "31.0.15.6094" }], false, "")).Single();
        Check(GraphicsDriverInventory.NvidiaPublicVersion(nvidiaOlder) == "560.94" &&
            GraphicsDriverInventory.NvidiaPublicVersion(found[0]) is null &&
            GraphicsDriverInventory.NvidiaPublicVersion(graphics.Single(x => x.Manufacturer == "AMD")) is null &&
            GraphicsDriverInventory.NvidiaPublicVersion(graphics.Single(x => x.Manufacturer == "Intel")) is null &&
            GraphicsDriverInventory.NvidiaPublicVersion(GraphicsDriverInventory.Find(new(
                [nvidiaSigned with { Provider = "Microsoft" }], false, "")).Single()) is null &&
            GraphicsDriverInventory.NvidiaPublicVersion(GraphicsDriverInventory.Find(new(
                [nvidiaSigned with { Version = "32.0.16.174" }], false, "")).Single()) is null &&
            GraphicsDriverInventory.NvidiaPublicVersion(GraphicsDriverInventory.Find(new(
                [nvidiaSigned with { Version = "32.1.16.1742" }], false, "")).Single()) is null &&
            GraphicsDriverInventory.NvidiaPublicVersion(GraphicsDriverInventory.Find(new(
                [nvidiaSigned, nvidiaSigned], false, "")).Single()) is null &&
            GraphicsDriverInventory.NvidiaPublicVersion(GraphicsDriverInventory.Find(new(
                [nvidiaSigned], true, "parcial")).Single()) is null &&
            GraphicsDriverInventory.NvidiaPublicVersion(GraphicsDriverInventory.Find(new(
                [nvidiaSigned with { ReportedDriverClass = "" }], false, "")).Single()) is null,
            "VF02 recusa conversão NVIDIA para outros fornecedores, provedor errado, formato inválido, duplicidade e lacunas");

        // VF02 fase 2: nenhum endpoint de scraping nem versão remota presumida.
        var nvidiaSite = GraphicsDriverOfficialSources.For(found[0]);
        var amdSite = GraphicsDriverOfficialSources.For(graphics.Single(x => x.Manufacturer == "AMD"));
        var intelSite = GraphicsDriverOfficialSources.For(graphics.Single(x => x.Manufacturer == "Intel"));
        Check(nvidiaSite is { Manufacturer: "NVIDIA",
            PageUrl: "https://www.nvidia.com/pt-br/geforce/drivers/" } &&
            amdSite is { Manufacturer: "AMD",
            PageUrl: "https://www.amd.com/en/support/download/drivers.html" } &&
            intelSite is { Manufacturer: "Intel",
            PageUrl: "https://www.intel.com/content/www/us/en/products/docs/discrete-gpus/arc/software/drivers.html" } &&
            new[] { nvidiaSite, amdSite, intelSite }.All(s =>
                s!.ReviewState == GraphicsDriverOfficialSources.ManualReview &&
                s.OfferedChannel == GraphicsDriverOfficialSources.UnknownOffer &&
                s.OfferedVersion == GraphicsDriverOfficialSources.UnknownOffer &&
                GraphicsDriverOfficialSources.IsApprovedOfficialPage(s.PageUrl)),
            "VF02 fase 2 fontes oficiais por fabricante com oferta/canal desconhecidos");
        Check(!GraphicsDriverOfficialSources.IsApprovedOfficialPage("http://www.nvidia.com/pt-br/geforce/drivers/") &&
            !GraphicsDriverOfficialSources.IsApprovedOfficialPage("https://www.nvidia.com.evil.test/pt-br/geforce/drivers/") &&
            !GraphicsDriverOfficialSources.IsApprovedOfficialPage("https://www.nvidia.com/pt-br/geforce/drivers/?model=secret") &&
            GraphicsDriverOfficialSources.For(found[0] with { Manufacturer = "desconhecido" }) is null &&
            DriverReadOnlyReport.Format(new([rtx], false, ""), rtx)
                .Contains("Consulta de versão oficial: inconclusiva", StringComparison.Ordinal) &&
            DriverReadOnlyReport.Format(new([rtx], false, ""), rtx)
                .Contains("nenhuma versão remota foi consultada ou comparada pelo GF", StringComparison.Ordinal),
            "VF02 rejeita URLs não aprovadas, não infere ofertas e exibe estado inconclusivo");

        // VF02 fase 4: registro oficial Intel #785597 publica versão, lista de
        // produtos e OS; o link é somente consulta MANUAL (sem oferta inferida).
        var intelB580 = intelDisplay with { DeviceName = "Intel(R) Arc(TM) B580 Graphics" };
        var intelA770 = intelDisplay with { DeviceName = "Intel® Arc™ A770 Graphics" };
        var intelA350M = intelDisplay with { DeviceName = "Intel Arc A350M Graphics" };
        var intelPro = intelDisplay with { DeviceName = "Intel Arc Pro B60 Graphics" };
        var intelUnknown = intelDisplay with { DeviceName = "Intel Arc A999 Graphics" };
        var intelFalseSku = intelDisplay with { DeviceName = "Intel Arc B5800 Graphics" };
        var officialIntelRecord =
            "https://www.intel.com/content/www/us/en/download/785597/intel-arc-graphics-windows.html";
        GraphicsDriverOfficialSource? IntelSite(DriverInventoryEntry entry) =>
            GraphicsDriverOfficialSources.For(
                GraphicsDriverInventory.Find(new([entry], false, "consulta completa")).Single());
        Check(IntelSite(intelB580) is { PageUrl: var b580Url, OfferedVersion: "não verificada",
                OfferedChannel: "não verificada" } && b580Url == officialIntelRecord &&
            IntelSite(intelA770)?.PageUrl == officialIntelRecord &&
            IntelSite(intelA350M)?.PageUrl == officialIntelRecord &&
            GraphicsDriverOfficialSources.IsApprovedOfficialPage(officialIntelRecord) &&
            !GraphicsDriverOfficialSources.IsApprovedOfficialPage(officialIntelRecord + "?driver=999") &&
            !GraphicsDriverOfficialSources.IsApprovedOfficialPage(
                "https://www.intel.com.evil.test/en/download/785597/intel-arc-graphics-windows.html"),
            "VF02 fase 4 encaminha SKUs Intel Arc reconhecidos ao registro oficial sem oferta automática");
        Check(IntelSite(intelPro)?.PageUrl != officialIntelRecord &&
            IntelSite(intelUnknown)?.PageUrl != officialIntelRecord &&
            IntelSite(intelFalseSku)?.PageUrl != officialIntelRecord &&
            IntelSite(intelDisplay)?.ReviewState == GraphicsDriverOfficialSources.ManualReview &&
            !DriverUpdatePilot.AllowedClass("Display"),
            "VF02 fase 4 recusa associação do registro específico a Pro, SKU desconhecido ou parecido");

        var named = gpu with { DeviceName = "Nome específico informado pelo fabricante" };
        Check(DriverHardwareCatalog.DisplayName(named) == named.DeviceName,
            "preserva o nome específico informado pelo Windows");
        Check(DriverListFilters.Apply([gpu], "Presentes", "5060 nvidia display.inf").Single() == gpu,
            "busca aceita fabricante do hardware, modelo e INF em qualquer ordem");

        var ethernet = new DriverInventoryEntry(@"PCI\VEN_8086&DEV_15F3\4&01",
            "Controlador de rede", "", "", "", null, true)
        {
            HardwareIds = [@"pci\ven_8086&dev_15f3&subsys_20314c52&rev_03", @"PCI\VEN_8086&DEV_15F3"],
            PnpProblemCode = 28,
            WithoutSignedDriverAssociation = true
        };
        Check(DriverHardwareCatalog.Identify(ethernet) is
            { Model: "Ethernet Controller I225-V", Subsystem: "LRES2031PT Single-port 2.5Gb Ethernet Network Adapter" },
            "resolve variante PCI pelo SUBSYS, com subdispositivo antes de subfornecedor");
        Check(DriverListFilters.Apply([gpu, ethernet], "Sem driver",
            "i225-v SUBSYS_20314C52").Single() == ethernet,
            "busca um HardwareID que não aparece no ID de instância e respeita o filtro");
        var sameChip = ethernet with
        {
            HardwareIds = [.. ethernet.HardwareIds, @"PCI\VEN_8086&DEV_15F3&SUBSYS_00038086"]
        };
        Check(DriverHardwareCatalog.Identify(sameChip) is { Model: "Ethernet Controller I225-V", Subsystem: "" },
            "IDs de variantes divergentes conservam somente o modelo base");
        var conflict = ethernet with { HardwareIds = [@"PCI\VEN_10DE&DEV_2D04"] };
        Check(DriverHardwareCatalog.Identify(conflict) == DriverHardwareIdentity.Unknown &&
            DriverHardwareCatalog.DisplayName(conflict) == conflict.DeviceName,
            "IDs de produto conflitantes não atribuem nome do catálogo");

        var camera = new DriverInventoryEntry(@"USB\VID_046D&PID_0825&MI_00\6&01",
            "Dispositivo USB", "Microsoft", "", "usbvideo.inf", true, true)
        { HardwareIds = [@"USB\VID_046D&PID_0825&REV_0012&MI_00"] };
        Check(DriverHardwareCatalog.Identify(camera) is { Model: "Webcam C270", UsbInterface: true } &&
            DriverHardwareCatalog.DisplayName(camera) == "Dispositivo USB · Webcam C270" &&
            DriverListFilters.Apply([camera], "Todos", "c270 dispositivo logitech mi_00").Single() == camera,
            "reconhece produto USB composto e mantém o nome da função");
        var namedCamera = camera with { DeviceName = "Câmera USB" };
        Check(DriverListFilters.Apply([namedCamera], "Todos", "camera USB").Single() == namedCamera,
            "busca com e sem acentos");
        var cameraA = camera with { DeviceName = "Câmera A" };
        var cameraB = camera with { DeviceName = "Câmera B" };
        Check(DriverListFilters.Apply([cameraA, cameraB], "Todos", "camera a").Single() == cameraA,
            "um termo de um caractere diferencia modelos com nomes parecidos");
        Check(DriverListFilters.Apply([camera], "Problemas", "c270").Length == 0 &&
            DriverListFilters.Apply([camera], "Todos", "c270 inexistente").Length == 0,
            "todos os termos devem aparecer e o filtro de estado continua valendo");

        var unknown = camera with
        {
            DeviceId = "",
            DeviceName = "",
            HardwareIds = [@"ACPI\VEN_046D&DEV_0825", @"USB\VID_046D&PID_0825X"]
        };
        Check(DriverHardwareCatalog.Identify(unknown) == DriverHardwareIdentity.Unknown &&
            DriverHardwareCatalog.DisplayName(unknown) == DriverHardwareCatalog.UnknownName,
            "IDs incompletos, malformados ou de barramento diferente não recebem identidade");
        string report = DriverReadOnlyReport.Format(new([camera], false, ""), camera);
        Check(report.Contains("Nome no Windows: Dispositivo USB", StringComparison.Ordinal) &&
            report.Contains("Fabricante do hardware (catálogo): Logitech", StringComparison.Ordinal) &&
            report.Contains("Produto USB da interface (catálogo): Webcam C270", StringComparison.Ordinal) &&
            report.Contains(camera.HardwareIds[0], StringComparison.Ordinal),
            "Detalhes mantém a origem dos nomes e os IDs de hardware");

        var audio = new DriverInventoryEntry(@"HDAUDIO\FUNC_01&VEN_10EC&DEV_0892&SUBSYS_10438436\5&01",
            "Dispositivo de áudio de alta definição", "Microsoft", "", "hdaudio.inf", true, true)
        { HardwareIds = [@"HDAUDIO\FUNC_01&VEN_10EC&DEV_0892&REV_1003"] };
        Check(DriverHardwareCatalog.Identify(audio) is { Vendor: "Realtek", Model: "Realtek ALC892", Subsystem: "" } &&
            DriverHardwareCatalog.DisplayName(audio) == "Realtek ALC892" &&
            DriverListFilters.Apply([audio], "Todos", "alc892 realtek").Single() == audio,
            "reconhece um codec HDA pelo ID exato em um catálogo próprio");
        var otherFunction = audio with
        {
            DeviceId = @"HDAUDIO\FUNC_02&VEN_10EC&DEV_0892\5&01",
            HardwareIds = []
        };
        Check(DriverHardwareCatalog.Identify(otherFunction) == DriverHardwareIdentity.Unknown,
            "o ID de outra função HDA não é identificado como codec de áudio");

        var printer = new DriverInventoryEntry(@"USBPRINT\HPHP_LASERJET_P1102ABCD\1&01",
            "Suporte de impressão USB", "Microsoft", "", "usbprint.inf", true, true)
        { HardwareIds = [@"USBPRINT\HPHP_LASERJET_P1102ABCD"] };
        var forgotten = audio with { Present = false };
        var duplicate = camera with { DeviceId = camera.DeviceId.ToUpperInvariant() };
        var generic = new DriverInventoryEntry(@"ACPI\TEST0001\0",
            "Dispositivo desconhecido", "", "", "", null, true);
        var seen = new List<string>();
        var enriched = DriverDeviceMetadata.Enrich(new([printer, forgotten, camera, duplicate, generic], false, ""),
            CancellationToken.None, id =>
            {
                seen.Add(id);
                return id == printer.DeviceId
                    ? new("", @"USB\VID_03F0&PID_002A")
                    : new("Nome informado pelo dispositivo", "");
            });
        var linkedPrinter = enriched.Items[0];
        var namedByBus = enriched.Items[4];
        Check(seen.SequenceEqual([printer.DeviceId, generic.DeviceId]) &&
            linkedPrinter.DeviceId == printer.DeviceId && linkedPrinter.HardwareIds.SequenceEqual(printer.HardwareIds),
            "lê apenas instâncias presentes e únicas sem substituir a evidência de compatibilidade");
        Check(DriverHardwareCatalog.Identify(printer) == DriverHardwareIdentity.Unknown &&
            DriverHardwareCatalog.Identify(linkedPrinter) is { Model: "LaserJet P1102", FromUsbParent: true } &&
            DriverHardwareCatalog.DisplayName(linkedPrinter) == "LaserJet P1102" &&
            DriverListFilters.Apply([linkedPrinter], "Todos", "laserjet 002a").Single() == linkedPrinter &&
            DriverReadOnlyReport.Format(enriched, linkedPrinter)
                .Contains("Modelo do dispositivo USB vinculado (catálogo): LaserJet P1102", StringComparison.Ordinal),
            "reconhece USBPRINT pelo pai confirmado e informa a origem do modelo");
        Check(DriverHardwareCatalog.DisplayName(namedByBus) == "Nome informado pelo dispositivo" &&
            DriverHardwareCatalog.DisplayName(namedByBus with { DeviceName = "Nome específico do Windows" }) ==
                "Nome específico do Windows" &&
            DriverReadOnlyReport.Format(enriched, namedByBus)
                .Contains("Descrição informada pelo dispositivo: Nome informado pelo dispositivo", StringComparison.Ordinal),
            "o nome do barramento recupera rótulos genéricos e aparece em Detalhes");

        var fromFriendly = generic with
        {
            DeviceMetadata = new("Texto do barramento", "", "Placa de captura virtual", "Descrição local")
        };
        Check(DriverHardwareCatalog.DisplayName(fromFriendly) == "Placa de captura virtual" &&
            DriverListFilters.Apply([fromFriendly], "Todos", "captura virtual").Single() == fromFriendly &&
            DriverReadOnlyReport.Format(new([fromFriendly], false, ""), fromFriendly)
                .Contains("Nome amigável da instância (Windows PnP): Placa de captura virtual"),
            "o nome amigável do MESMO devnode pode identificar uma entrada WMI genérica");
        var fromBus = generic with
        {
            DeviceMetadata = new("Nome do barramento", "", "Dispositivo desconhecido", "Outro detalhe específico")
        };
        Check(DriverHardwareCatalog.DisplayName(fromBus) == "Nome do barramento" &&
            DriverListFilters.Apply([fromBus], "Todos", "específico").Single() == fromBus,
            "bus description e device description podem ser pesquisados independentemente");
        var noNativeEvidence = generic with
        {
            DeviceMetadata = new("", "", "%INVALID%", "@unresolved")
        };
        Check(DriverHardwareCatalog.DisplayName(noNativeEvidence) == generic.DeviceName &&
            DriverInventory.ReportedDeviceName("Dispositivo desconhecido", "Controlador Realtek HD") ==
                "Controlador Realtek HD" &&
            DriverInventory.ReportedDeviceName("Nome verdadeiro", "Outro nome") == "Nome verdadeiro",
            "não atribui nome sem evidência e privilegia descrição específica da mesma instância");

        var broad = named with { Provider = "Realtek ALC892" };
        var contains = named with { DeviceName = "Dispositivo de teste Realtek ALC892" };
        Check(DriverListFilters.Apply([broad, contains, audio], "Todos", "Realtek ALC892")
                .SequenceEqual([audio, contains, broad]) &&
            DriverListFilters.Apply([broad, contains, audio], "Todos", "")
                .SequenceEqual([broad, contains, audio]),
            "ordena por modelo exato, nome parcial e fabricante, preservando a ordem sem busca");
        var containsId = named with { DeviceName = audio.HardwareIds[0] };
        Check(DriverListFilters.Apply([containsId, audio], "Presentes", audio.HardwareIds[0])
                .SequenceEqual([audio, containsId]),
            "ID de hardware exato vem antes do mesmo texto em outros campos");
        var eligible = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { gpu.DeviceId };
        Check(DriverListFilters.Apply([camera, gpu], "Todos", "", eligible)
                .SequenceEqual([gpu, camera]) &&
            DriverListFilters.Apply([camera, gpu], "Todos", "")
                .SequenceEqual([camera, gpu]),
            "integração #92/#93: elegível WUA no topo e ordem original sem oferta");
        Check(DriverListFilters.Apply([camera, gpu], "Todos", "c270", eligible)
                .SequenceEqual([camera]) &&
            DriverListFilters.Apply([camera, gpu], "Todos", "5060", eligible)
                .SequenceEqual([gpu]),
            "integração #92/#93: busca por catálogo respeita o filtro e prioridade");

    }
}
