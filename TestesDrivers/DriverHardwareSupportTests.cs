using GerenciadorAplicativos;

internal static class DriverHardwareSupportTests
{
    internal static void Run(Action<bool, string> check)
    {
        var gpu = Device(@"PCI\VEN_10DE&DEV_1C03", "Display") with
        {
            DeviceName = "Microsoft Basic Display Adapter",
            WithoutSignedDriverAssociation = true,
            ReportedDriverClass = ""
        };
        var gpuInventory = new DriverInventoryResult([gpu], false, "fixture");
        var report = DriverReadOnlyReport.Format(gpuInventory, gpu);
        check(DriverGamingCatalog.Find(gpuInventory, gpu, true) is null &&
            DriverHardwareCatalog.DisplayName(gpu).Contains("GeForce GTX 1060") &&
            DriverHardwareSupport.Get(gpuInventory, gpu) is { Category: DriverHardwareCategory.Graphics, Page.Host: "www.nvidia.com" } &&
            report.Contains("GeForce GTX 1060") && report.Contains("Consulta manual ao suporte oficial"),
            "GPU fora das referências fixas é identificada e recebe o suporte oficial do fabricante");

        var unfamiliar = Device(@"PCI\VEN_FFFE&DEV_0101", "Display") with { DeviceName = "Adaptador de Vídeo Básico da Microsoft" };
        check(Find(unfamiliar) is { Category: DriverHardwareCategory.Graphics, Page: null } &&
              DriverReadOnlyReport.Format(new([unfamiliar], false, ""), unfamiliar)
                  .Contains("Fonte específica inconclusiva"),
            "GPU de fabricante desconhecido mantém categoria, evidência PCI e fonte inconclusiva");

        const string gameId = @"HID\VID_046D&PID_C262&MI_00&COL01";
        const string systemGame = "HID_DEVICE_SYSTEM_GAME";
        const string gamePad = "HID_DEVICE_UP:0001_U:0005";
        var wheel = Device(gameId, "HIDClass") with
        {
            DeviceName = "HID-compliant device",
            HasGameInputUsage = DriverInventory.ReadGameInputUsage(new[] { systemGame })
        };
        var mouse = Device(@"HID\VID_046D&PID_C077", "Mouse") with { DeviceName = "Mouse" };
        check(Find(wheel) is { Category: DriverHardwareCategory.GameInput, Page.Host: "support.logi.com" } &&
            DriverHardwareCatalog.DisplayName(wheel).Contains("G920") &&
            Find(mouse with { HasGameInputUsage = true, ReportedDriverClass = "HIDClass" })
                .Category == DriverHardwareCategory.Peripherals,
            "Volante Logitech G920 usa evidência HID e fonte oficial. Mouse permanece em periféricos");
        check(DriverInventory.ReadGameInputUsage(new[] { gamePad.ToLowerInvariant() }) &&
            !DriverInventory.ReadGameInputUsage(new[] { gamePad + "0", "HID_DEVICE_UP:0001_U:0002" }) &&
            DriverInventory.ReadCompatibleIds(new[] { systemGame, gamePad }) is { Length: 0 },
            "Descritores HID classificam controles sem entrar nos identificadores de oferta WUA");

        var platform = Device(@"PCI\VEN_1022&DEV_790B", "System");
        var network = Device(@"PCI\VEN_8086&DEV_2723", "Net");
        var pcmUsb = Device(@"USB\VID_1234&PID_0001", "MEDIA") with { Provider = "Microsoft", InfName = "usbaudio2.inf" };
        var ssd = Device(@"PCI\VEN_144D&DEV_A804", "SCSIAdapter") with { Provider = "Microsoft", InfName = "stornvme.inf" };
        check(Find(platform) is { Category: DriverHardwareCategory.Platform, Page.Host: "www.amd.com" } &&
              Find(network) is { Category: DriverHardwareCategory.Network, Page.Host: "www.intel.com" } &&
              Find(pcmUsb) is { Category: DriverHardwareCategory.Audio, Page.Host: "learn.microsoft.com" } &&
              Find(ssd) is { Category: DriverHardwareCategory.Storage, Page.Host: "learn.microsoft.com" } &&
              Find(ssd).Status.Contains("StorNVMe"),
            "Chipset, rede, USB Audio e StorNVMe apresentam a fonte correspondente e preservam a origem Windows");

        var wrongProduct = wheel with { HardwareIds = [gameId, @"HID\VID_044F&PID_B677"] };
        var conflict = gpu with { ReportedDriverClass = "Media" };
        check(Find(wrongProduct).Page is null && Find(conflict).Category == DriverHardwareCategory.Unknown &&
              DriverHardwareSupport.Get(new([wheel, wheel], false, ""), wheel).Page is null &&
              DriverHardwareSupport.Get(new([wheel], true, ""), wheel).Page is null &&
              Find(wheel with { Present = null }).Page is null &&
              Find(pcmUsb with { Provider = "Outro" }).Page is null,
            "Leitura parcial, identidade ambígua, conflito de classe e INF sem origem Microsoft não publicam a fonte");

        var items = new[] { gpu, wheel, mouse, platform, network, pcmUsb, ssd };
        check(DriverListFilters.Apply(items, "Todos", "controles de jogo").SequenceEqual([wheel]) &&
            DriverListFilters.Apply(items, "Todos", "Armazenamento").SequenceEqual([ssd]) &&
            !DriverUpdatePilot.AllowedClass("Display") && !DriverUpdatePilot.AllowedClass("Net") &&
            !DriverUpdatePilot.AllowedClass("HIDClass"),
            "Busca por categoria preserva o bloqueio do piloto para vídeo, rede e HID");

        CheckSources(check);
        CheckExtendedSources(check);
        CheckEthernetSources(check);
        CheckBluetoothSources(check);
        CheckLogitechGameSources(check);
        CheckFlightControls(check);
        CheckUsbHeadsets(check);
        CheckOtherUsbManufacturers(check);
        CheckAdditionalLogitechFlightAndAudio(check);
        CheckPedalControllerAndHeadsetAdapter(check);
        CheckDedicatedPedalsAndAdditionalUsbHeadsets(check);
    }

    private static void CheckSources(Action<bool, string> check)
    {
        var legacy = new[]
        {
            (Device(@"PCI\VEN_10DE&DEV_0FC6", "Display"), "nvidia.custhelp.com"),
            (Device(@"PCI\VEN_1002&DEV_6779", "Display"), "www.amd.com"),
            (Device(@"PCI\VEN_8086&DEV_0166", "Display"), "www.intel.com"),
            (Device(@"PCI\VEN_102B&DEV_0520", "Display"), "video.matrox.com")
        };
        check(legacy.All(pair =>
        {
            var source = Find(pair.Item1);
            return source.Page?.Host == pair.Item2 && source.Status.Contains("Suporte legado");
        }), "GTX 650, Radeon Caicos, Intel HD 4000 e Matrox G200 usam fontes legadas oficiais");

        const string caicos = @"PCI\VEN_1002&DEV_6779";
        const string dell = caicos + "&SUBSYS_21201028";
        var oemGraphics = Device(dell + "&REV_00", "Display") with { HardwareIds = [dell.ToLowerInvariant(), caicos] };
        var oemLookup = Find(oemGraphics);
        check(DriverHardwareCatalog.ReportedPciIdentity(oemGraphics) is { Product: 0x10026779, Subsystem: 0x21201028, SubsystemVendor: 0x1028 } &&
              oemLookup.Page?.Host == "www.dell.com" && oemLookup.Status.Contains("SUBSYS_21201028") &&
              Find(Device(caicos + "&SUBSYS_10282120", "Display")).Page?.Host == "www.amd.com",
            "SUBSYS usa o fabricante na metade baixa e prioriza Dell sobre o catálogo legado do chip");

        const string geforce = @"PCI\VEN_10DE&DEV_2D04";
        var oemNvidia = Device(geforce + "&SUBSYS_0001103C", "Display");
        var oemIntel = Device(@"PCI\VEN_8086&DEV_2723&SUBSYS_002017AA", "Net");
        var oemAcer = Device(geforce + "&SUBSYS_00011025", "Display");
        check(Find(oemNvidia).Page?.Host == "support.hp.com" &&
              DriverGamingCatalog.Find(new([oemNvidia], false, ""), oemNvidia, true) is null &&
              Find(oemIntel).Page?.Host == "pcsupport.lenovo.com" &&
              Find(oemAcer).Page?.Host == "www.acer.com",
            "A rota OEM atende GPU e rede e precede o catálogo de jogos na variante HP");

        var conflicting = oemGraphics with { HardwareIds = [dell, caicos + "&SUBSYS_2128103C"] };
        var malformed = oemGraphics with { HardwareIds = [dell, caicos + "&SUBSYS_2120ZZZZ"] };
        var zero = Device(caicos + "&SUBSYS_00000000", "Display");
        var compatibleOnly = Device(caicos, "Display") with { CompatibleIds = [dell] };
        check(Find(conflicting) is { Page: null, Category: DriverHardwareCategory.Graphics } &&
              Find(malformed).Page is null &&
              DriverHardwareCatalog.ReportedPciIdentity(zero) is { Subsystem: null } &&
              DriverHardwareCatalog.ReportedPciIdentity(compatibleOnly) is { Subsystem: null } &&
              Find(compatibleOnly).Page?.Host == "www.amd.com",
            "SUBSYS contraditório ou malformado fica inconclusivo e IDs compatíveis não concedem rota OEM");

        var realtekEthernet = Device(@"PCI\VEN_10EC&DEV_8125", "Net");
        var realtekWireless = Device(@"PCI\VEN_10EC&DEV_C822", "Net");
        var asix = Device(@"USB\VID_0B95&PID_1790", "Net");
        check(Find(realtekEthernet).Page?.AbsolutePath == "/Download/List" &&
              Find(realtekWireless).Page?.AbsolutePath == "/Download/Index" &&
              Find(asix).Page?.Host == "www.asix.com.tw" &&
              Find(asix with { ReportedPnpClass = "Media", ReportedDriverClass = "Media" }).Page is null,
            "Realtek Ethernet, Realtek de rede e ASIX USB recebem rotas distintas e restritas à função de rede");

        var dualSense = Device(@"HID\VID_054C&PID_0CE6&COL01", "HIDClass") with { HasGameInputUsage = true };
        var dualSenseEdge = Device(@"USB\VID_054C&PID_0DF2", "HIDClass") with { HasGameInputUsage = true };
        var corsair = Device(@"HID\VID_1B1C&PID_1B3D", "Keyboard");
        var steelSeries = Device(@"HID\VID_1038&PID_1720", "Mouse");
        check(Find(dualSense) is { Category: DriverHardwareCategory.GameInput, Page.Host: "www.playstation.com" } &&
              Find(dualSenseEdge).Page?.AbsolutePath.Contains("dualsense-edge", StringComparison.Ordinal) == true &&
              Find(Device(@"USB\VID_054C&PID_0001", "HIDClass")).Page is null &&
              Find(corsair).Page?.Host == "www.corsair.com" &&
              Find(steelSeries).Page?.Host == "support.steelseries.com",
            "DualSense e Edge usam o produto exato e periféricos Corsair e SteelSeries recebem suporte oficial");
    }


    // O SUBSYS sintético valida a seleção de portal, sem atribuir um SKU real.
    private static void CheckExtendedSources(Action<bool, string> check)
    {
        var qca = Device(@"PCI\VEN_168C&DEV_0042", "Net");
        var qcaOther = Device(@"PCI\VEN_168C&DEV_DEAD", "Net");
        var qNew = Device(@"PCI\VEN_17CB&DEV_DEAD", "Net");
        check(Find(qca).Page?.AbsolutePath.EndsWith("/qca9377/support", StringComparison.Ordinal) == true &&
              Find(qca).Status.Contains("OEM", StringComparison.Ordinal) &&
              Find(qcaOther).Page?.AbsolutePath == "/drivers" &&
              Find(qcaOther).Status.Contains("inconclusivos", StringComparison.Ordinal) &&
              Find(qNew).Page?.Host == "www.qualcomm.com",
            "Qualcomm QCA9377 específico e fabricante sem identificação de modelo");

        var mt1 = Device(@"PCI\VEN_14C3&DEV_7961", "Net");
        var mt2 = Device(@"PCI\VEN_14C3&DEV_0616", "Net");
        var mtUnknown = Device(@"PCI\VEN_14C3&DEV_DEAD", "Net");
        check(Find(mt1).Page?.Host == "www.mediatek.com" &&
              Find(mt1).Status.Contains("MT7921", StringComparison.Ordinal) &&
              Find(mt1).Status.Contains("não catálogo de drivers Windows", StringComparison.Ordinal) &&
              Find(mt2).Status.Contains("MT7922", StringComparison.Ordinal) &&
              Find(mtUnknown).Status.Contains("modelo específico inconclusivo", StringComparison.Ordinal),
            "MediaTek MT7921 e MT7922 com fabricante sem pacote Windows presumido");

        var qOem = Device(@"PCI\VEN_168C&DEV_0042&SUBSYS_090117AA", "Net");
        var mOem = Device(@"PCI\VEN_14C3&DEV_7961&SUBSYS_12341028", "Net");
        check(Find(qOem).Page?.Host == "pcsupport.lenovo.com" &&
              Find(mOem).Page?.Host == "www.dell.com" &&
              Find(qOem).Status.Contains("SUBSYS_090117AA", StringComparison.Ordinal),
            "SUBSYS coerente prioriza o portal OEM em Qualcomm e MediaTek");

        var conflict = qOem with { HardwareIds = [
            @"PCI\VEN_168C&DEV_0042&SUBSYS_090117AA",
            @"PCI\VEN_168C&DEV_0042&SUBSYS_0001103C"] };
        var malformed = mOem with { HardwareIds = [
            @"PCI\VEN_14C3&DEV_7961&SUBSYS_12341028",
            @"PCI\VEN_14C3&DEV_7961&SUBSYS_1234ZZZZ"] };
        var compatible = mt1 with { CompatibleIds = [@"PCI\VEN_14C3&DEV_7961&SUBSYS_12341028"] };
        check(Find(conflict).Page is null && Find(malformed).Page is null &&
              Find(compatible).Page?.Host == "www.mediatek.com" &&
              DriverHardwareCatalog.ReportedPciIdentity(compatible) is { Subsystem: null } &&
              Find(mt1 with { HardwareIds = [] }).Page is null &&
              Find(mt1 with { HardwareIds = [@"PCI\VEN_168C&DEV_0042"] }).Page is null,
            "SUBSYS conflitante/malformado, sem evidência e IDs divergentes não geram falsa fonte");

        var fermi = Device(@"PCI\VEN_10DE&DEV_0DC4", "Display");
        var gtx460 = Device(@"PCI\VEN_10DE&DEV_0E22", "Display");
        var cedar = Device(@"PCI\VEN_1002&DEV_68F9", "Display");
        var hd3000 = Device(@"PCI\VEN_8086&DEV_0126", "Display");
        check(new[] { fermi, gtx460 }.All(d => Find(d).Page?.Host == "www.nvidia.com" &&
                    Find(d).Status.Contains("histórica", StringComparison.Ordinal)) &&
              Find(cedar).Page?.Host == "www.amd.com" &&
              Find(hd3000).Page?.AbsolutePath.EndsWith("/000005526/graphics.html", StringComparison.Ordinal) == true &&
              Find(hd3000).Status.Contains("não um pacote confirmado", StringComparison.Ordinal) &&
              Find(fermi with { ReportedDriverClass = "Net" }).Page is null,
            "GTS 450, GTX 460, Radeon HD 5450 e HD 3000 com avisos de legado");

        var ds4 = Device(@"HID\VID_054C&PID_05C4&COL01", "HIDClass") with { HasGameInputUsage = true };
        var ds4v2 = Device(@"USB\VID_054C&PID_09CC", "HIDClass") with { HasGameInputUsage = true };
        var steam = Device(@"HID\VID_28DE&PID_1102&MI_00", "HIDClass") with { HasGameInputUsage = true };
        var xbox = Device(@"USB\VID_045E&PID_028E", "HIDClass") with { HasGameInputUsage = true };
        check(new[] { ds4, ds4v2 }.All(d => Find(d).Page?.AbsolutePath.Contains("dualshock-4", StringComparison.Ordinal) == true) &&
              Find(steam).Page?.Host == "help.steampowered.com" &&
              Find(steam).Status.Contains("2015", StringComparison.Ordinal) &&
              Find(xbox).Page?.Host == "www.catalog.update.microsoft.com" &&
              Find(xbox).Status.Contains("histórico", StringComparison.Ordinal),
            "DUALSHOCK 4, Steam Controller 2015 e Xbox 360 possuem fontes próprias");

        check(Find(Device(@"HID\VID_28DE&PID_1142", "HIDClass") with { HasGameInputUsage = true }).Page is null &&
              Find(Device(@"HID\VID_054C&PID_0001", "HIDClass") with { HasGameInputUsage = true }).Page is null &&
              Find(xbox with { HasGameInputUsage = false }).Page is null &&
              Find(steam with { ReportedDriverClass = "Net" }).Page is null &&
              Find(ds4 with { Present = false }).Page is null &&
              !DriverUpdatePilot.AllowedClass("Display") && !DriverUpdatePilot.AllowedClass("Net") &&
              !DriverUpdatePilot.AllowedClass("HIDClass"),
            "Periféricos não comprovados e leitura ausente são inconclusivos; instalar segue bloqueado");
    }

    private static void CheckEthernetSources(Action<bool, string> check)
    {
        var bcm5719 = Device(@"PCI\VEN_14E4&DEV_1657", "Net");
        const string bcm5720Id = @"PCI\VEN_14E4&DEV_165F";
        var bcm5720 = Device(bcm5720Id, "Net");
        check(Find(bcm5719).Page?.Host == "www.broadcom.com" &&
              Find(bcm5719).Status.Contains("BCM5719", StringComparison.Ordinal) &&
              Find(bcm5720).Page?.Host == "www.broadcom.com" &&
              Find(bcm5720).Status.Contains("BCM5720", StringComparison.Ordinal) &&
              Find(Device(@"PCI\VEN_14E4&DEV_DEAD", "Net")).Page is null &&
              Find(Device(bcm5720Id, "Media")).Page is null,
            "Broadcom BCM5719 e BCM5720 recebem fontes oficiais somente com ID e categoria coerentes");

        var aqc107 = Device(@"PCI\VEN_1D6A&DEV_07B1", "Net");
        var aqc113 = Device(@"PCI\VEN_1D6A&DEV_04C0", "Net");
        const string yukonId = @"PCI\VEN_11AB&DEV_4364";
        var yukon = Device(yukonId, "Net");
        check(Find(aqc107).Page?.Host == "www.marvell.com" &&
              Find(aqc107).Status.Contains("AQC107", StringComparison.Ordinal) &&
              Find(aqc113).Page?.Host == "www.marvell.com" &&
              Find(aqc113).Status.Contains("AQC113", StringComparison.Ordinal) &&
              Find(yukon).Page?.Host == "www.catalog.update.microsoft.com" &&
              Find(yukon).Status.Contains("histórico", StringComparison.Ordinal) &&
              Find(Device(@"PCI\VEN_1D6A&DEV_00B1", "Net")).Page is null,
            "Marvell AQC107, AQC113 e Yukon têm fontes próprias e mantêm variantes não mapeadas inconclusivas");

        string dellId = bcm5720Id + "&SUBSYS_00011028";
        var dell = Device(dellId, "Net");
        var lenovoYukon = Device(yukonId + "&SUBSYS_305617AA", "Net");
        check(Find(dell).Page?.Host == "www.dell.com" &&
              Find(lenovoYukon).Page?.Host == "pcsupport.lenovo.com" &&
              Find(dell with { HardwareIds = [dellId, bcm5720Id + "&SUBSYS_000117AA"] }).Page is null &&
              Find(bcm5720 with { CompatibleIds = [dellId] }).Page?.Host == "www.broadcom.com",
            "SUBSYS OEM prevalece em Broadcom e Marvell; conflito e ID apenas compatível não concedem fonte OEM");
    }

    private static void CheckBluetoothSources(Action<bool, string> check)
    {
        const string qualcommId = @"USB\VID_0CF3&PID_E300";
        const string rz608Id = @"USB\VID_0E8D&PID_0608&MI_00";
        const string rz616Id = @"USB\VID_0E8D&PID_0616&MI_00";
        var qualcomm = Device(qualcommId, "Bluetooth");
        var rz608 = Device(rz608Id, "Bluetooth") with { ReportedDriverClass = "OtherHardware" };
        var rz616 = Device(rz616Id, "Bluetooth") with
        {
            HardwareIds = [@"usb\vid_0e8d&pid_0616&rev_0100&mi_00", rz616Id]
        };
        var known = new[] { (qualcomm, qualcommId), (rz608, rz608Id), (rz616, rz616Id) };
        check(known.All(entry =>
        {
            var source = Find(entry.Item1);
            return source.Page is { Host: "www.catalog.update.microsoft.com" } page &&
                   Uri.UnescapeDataString(page.Query) == "?q=" + entry.Item2 &&
                   source.Status.Contains("compatibilidade continuam inconclusivas", StringComparison.Ordinal);
        }) && Find(qualcomm).Status.Contains("modelo específico inconclusivo", StringComparison.Ordinal),
            "Bluetooth Qualcomm E300 e MediaTek RZ608/RZ616 consultam o Catálogo Microsoft por IDs exatos");

        var unknown = Device(@"USB\VID_0CF3&PID_DEAD", "Bluetooth");
        var parent = Device(@"USB\VID_0E8D&PID_0616", "Bluetooth");
        var otherInterface = Device(@"USB\VID_0E8D&PID_0616&MI_01", "Bluetooth");
        check(Find(unknown).Page is null &&
              Find(parent with { CompatibleIds = [rz616Id] }).Page is null &&
              Find(otherInterface).Page is null &&
              Find(rz616 with { HardwareIds = [rz616Id, @"USB\VID_0E8D&PID_0616&MI_01"] }).Page is null &&
              Find(rz616 with { HardwareIds = [rz616Id + "&MI_00"] }).Page is null &&
              Find(rz616 with { HardwareIds = [] }).Page is null &&
              Find(rz616 with { ReportedPnpClass = "Net" }).Page is null &&
              Find(Device(@"HID\VID_0CF3&PID_E300", "Bluetooth")).Page is null,
            "Bluetooth sem interface USB corroborada ou com classe incompatível fica inconclusivo");
    }

    private static void CheckLogitechGameSources(Action<bool, string> check)
    {
        var wheels = new[]
        {
            ("C24F", "G29"), ("C262", "G920"),
            ("C266", "G923 para PlayStation"), ("C26E", "G923 para Xbox")
        };
        check(wheels.All(wheel =>
        {
            var device = Device($"HID\\VID_046D&PID_{wheel.Item1}&COL01", "HIDClass") with { HasGameInputUsage = true };
            var source = Find(device);
            return source.Category == DriverHardwareCategory.GameInput &&
                   source.Page is { Host: "support.logi.com" } page &&
                   page.AbsolutePath.Contains("4404187621911", StringComparison.Ordinal) &&
                   source.Status.Contains(wheel.Item2, StringComparison.Ordinal);
        }), "Volantes Logitech G29, G920 e as duas variantes G923 usam suporte específico");

        var f310 = Device(@"USB\VID_046D&PID_C21D", "USBDevice");
        var f710 = Device(@"HID\VID_046D&PID_C21F", "HIDClass") with { HasGameInputUsage = true };
        var sharedF310 = Device(@"HID\VID_046D&PID_C216", "HIDClass");
        var sharedF710 = Device(@"HID\VID_046D&PID_C219", "HIDClass");
        check(Find(f310).Page?.AbsolutePath.Contains("360023467133", StringComparison.Ordinal) == true &&
              Find(f710).Page?.AbsolutePath.Contains("360023465553", StringComparison.Ordinal) == true &&
              Find(f310).Status.Contains("F310 em modo XInput", StringComparison.Ordinal) &&
              Find(f710).Status.Contains("F710 em modo XInput", StringComparison.Ordinal) &&
              new[] { sharedF310, sharedF710 }.All(device =>
                  Find(device).Page?.AbsolutePath == "/hc/pt-br" &&
                  Find(device).Status.Contains("modelo específico é inconclusivo", StringComparison.Ordinal)) &&
              Find(f710 with { ReportedPnpClass = "Net" }).Page is null,
            "Gamepads Logitech F310/F710 têm páginas em XInput e modelo inconclusivo em DirectInput compartilhado");
    }

    private static void CheckFlightControls(Action<bool, string> check)
    {
        var products = new[]
        {
            (@"HID\VID_046D&PID_C215", "360023331594", "Joystick Logitech Extreme 3D Pro"),
            (@"HID\VID_06A3&PID_0255", "360023462973", "Controle de voo Logitech X52 H.O.T.A.S."),
            (@"HID\VID_06A3&PID_0762", "360023462793", "Controle de voo Logitech X52 Pro H.O.T.A.S."),
            (@"HID\VID_0738&PID_2221", "360023302854", "Joystick Logitech X56 H.O.T.A.S."),
            (@"HID\VID_0738&PID_A221", "360023302854", "Acelerador Logitech X56 H.O.T.A.S.")
        };
        check(products.All(product =>
        {
            var item = Device(product.Item1 + "&COL01", "HIDClass") with { HasGameInputUsage = true };
            var source = Find(item);
            return source.Category == DriverHardwareCategory.GameInput &&
                source.Page is { Host: "support.logi.com" } page &&
                page.AbsolutePath.EndsWith("/" + product.Item2, StringComparison.Ordinal) &&
                source.Status.Contains(product.Item3, StringComparison.Ordinal) &&
                DriverHardwareCatalog.Identify(item).Model == product.Item3;
        }), "Extreme 3D Pro, X52/X52 Pro e os dois módulos X56 usam IDs e fontes oficiais distintos");

        const string x56 = @"HID\VID_0738&PID_2221&MI_03&COL01";
        var joystick = Device(x56, "HIDClass") with { HasGameInputUsage = true };
        var invalid = new[]
        {
            joystick with { HasGameInputUsage = false },
            joystick with { ReportedPnpClass = "" },
            joystick with { ReportedPnpClass = "Mouse" },
            joystick with { ReportedDriverClass = "Media" },
            joystick with { HardwareIds = [x56.Replace("MI_03", "MI_02")] },
            joystick with { HardwareIds = [], CompatibleIds = [x56] }
        };
        check(Find(joystick).Page?.Host == "support.logi.com" &&
              invalid.All(item => Find(item).Page is null && Find(item).Status.Contains("Modelo USB inconclusivo") &&
                  DriverHardwareCatalog.Identify(item).Model.Length == 0),
            "Joysticks sem função HID corroborada ou com interface conflitante ficam inconclusivos");
    }

    private static void CheckUsbHeadsets(Action<bool, string> check)
    {
        var products = new[]
        {
            ("0A4D", "360023466633", "Adaptador USB Logitech G430"),
            ("0A1F", "360023466133", "Receptor USB Logitech G930"),
            ("0A5B", "360023464013", "Receptor USB Logitech G933"),
            ("0AB6", "17207077246871", "Receptor USB Logitech G733"),
            ("0ABA", "360052715513", "Receptor USB Logitech PRO X Wireless")
        };
        check(products.All(product =>
        {
            var item = Device($@"USB\VID_046D&PID_{product.Item1}", "Media");
            var source = Find(item);
            return source.Category == DriverHardwareCategory.Audio &&
                source.Page is { Host: "support.logi.com" } page &&
                page.AbsolutePath.EndsWith("/" + product.Item2, StringComparison.Ordinal) &&
                source.Status.Contains(product.Item3, StringComparison.Ordinal) &&
                DriverHardwareCatalog.Identify(item).Model == product.Item3;
        }), "G430, G930, G933, G733 e PRO X Wireless identificam o adaptador ou receptor USB");

        var shared = Device(@"USB\VID_046D&PID_0A87&MI_00", "Media") with { DeviceName = "USB Audio Device" };
        var report = DriverReadOnlyReport.Format(new([shared], false, ""), shared);
        check(Find(shared).Page?.AbsolutePath == "/hc/pt-br" &&
              report.Contains("compartilhado por G935 e G933s") &&
              report.Contains("modelo do headset é inconclusivo") &&
              DriverHardwareCatalog.Identify(shared).Model.Length == 0 &&
              DriverHardwareCatalog.DisplayName(shared) == shared.DeviceName,
            "G935 e G933s compartilham o VID/PID sem atribuição de modelo específico no relatório ou na lista");

        const string headset = @"USB\VID_046D&PID_0ABA&MI_00";
        var audio = Device(headset, "Media") with
        {
            HardwareIds = [headset, @"usb\vid_046d&pid_0aba&rev_0100&mi_00"]
        };
        var invalid = new[]
        {
            audio with { ReportedPnpClass = "USB" },
            audio with { ReportedDriverClass = "HIDClass" },
            audio with { HardwareIds = [headset, @"USB\VID_046D&PID_0ABA&MI_02"] },
            audio with { HardwareIds = [headset.Replace("MI_00", "MI_ZZ")] },
            audio with { HardwareIds = [headset + "&MI_00"] },
            audio with { HardwareIds = [@"USB\VID_046D&PID_0ABA"] },
            Device(@"HID\VID_046D&PID_0ABA&MI_00", "HIDClass"),
            Device(@"HID\VID_046D&PID_0ABA&MI_00", "Media"),
            Device(@"SWD\MMDEVAPI\VID_046D&PID_0ABA", "AudioEndpoint")
        };
        check(Find(audio).Page?.Host == "support.logi.com" &&
              DriverHardwareCatalog.ReportedUsbIdentity(audio) is { Product: 0x046d0aba, Interface: 0 } &&
              invalid.All(item => Find(item).Page is null && DriverHardwareCatalog.Identify(item).Model.Length == 0),
            "Headset requer identidade USB e interface coerentes na classe Media sem inferência por HID ou endpoint");

        var windowsAudio = audio with { Provider = "Microsoft", InfName = "usbaudio2.inf" };
        var unknownInterface = windowsAudio with { HardwareIds = [] };
        check(Find(windowsAudio).Page?.Host == "support.logi.com" &&
              Find(windowsAudio).Status.Contains("USB Audio 2.0 fornecido pelo Windows") &&
              Find(unknownInterface).Page?.Host == "learn.microsoft.com" &&
              Find(unknownInterface).Status.Contains("Modelo USB inconclusivo") &&
              DriverHardwareSupport.Get(new([windowsAudio], true, ""), windowsAudio).Page is null &&
              DriverHardwareSupport.Get(new([windowsAudio, windowsAudio], false, ""), windowsAudio).Page is null,
            "Orientação do fabricante preserva o relato USB Audio do Windows e os bloqueios de identidade do inventário");
    }

    private static void CheckOtherUsbManufacturers(Action<bool, string> check)
    {
        const string mambaId = @"HID\VID_231D&PID_0117&MI_02&COL01";
        var mamba = Device(mambaId, "HIDClass") with { HasGameInputUsage = true };
        var gunfighter = Device(@"HID\VID_231D&PID_0125", "HIDClass") with { HasGameInputUsage = true };
        check(Find(mamba) is { Category: DriverHardwareCategory.GameInput, Page.Host: "vkbsimcontrollers.com" } &&
              Find(mamba).Status.Contains("perfil Fat Mamba", StringComparison.Ordinal) &&
              Find(gunfighter).Page == Find(mamba).Page &&
              Find(gunfighter).Status.Contains("perfil Gunfighter Modern Combat PRO", StringComparison.Ordinal) &&
              new[] { mamba, gunfighter }.All(item => DriverHardwareCatalog.Identify(item).Model.Length == 0 &&
                  Find(item).Status.Contains("modelo físico é inconclusivo", StringComparison.Ordinal)),
            "VKB Fat Mamba e Gunfighter abrem a documentação oficial sem atribuir uma montagem física");

        var invalidVkb = new[]
        {
            mamba with { HasGameInputUsage = false },
            mamba with { ReportedPnpClass = "Media" },
            mamba with { HardwareIds = [mambaId.Replace("MI_02", "MI_03")] },
            mamba with { HardwareIds = [], CompatibleIds = [mambaId] },
            Device(@"HID\VID_231D&PID_DEAD", "HIDClass") with { HasGameInputUsage = true }
        };
        check(invalidVkb.All(item => Find(item).Page is null && DriverHardwareCatalog.Identify(item).Model.Length == 0),
            "Perfil VKB sem classe e identidade corroboradas ou com PID desconhecido fica inconclusivo");

        const string eposId = @"USB\VID_1395&PID_0400&MI_01";
        var epos = Device(eposId, "Media") with { HardwareIds = [eposId, @"usb\vid_1395&pid_0400&rev_0100&mi_01"] };
        var eposReport = DriverReadOnlyReport.Format(new([epos], false, ""), epos);
        check(Find(epos) is { Category: DriverHardwareCategory.Audio, Page.Host: "www.eposaudio.com" } &&
              DriverHardwareCatalog.Identify(epos).Model == "Família EPOS Command 260 USB PTT" &&
              eposReport.Contains("A variante física é inconclusiva", StringComparison.Ordinal) &&
              Find(epos with { HardwareIds = [eposId.Replace("MI_01", "MI_02")] }).Page is null &&
              Find(Device(@"HID\VID_1395&PID_0400", "HIDClass")).Page is null,
            "EPOS Command 260 consulta a ficha da família somente na função de áudio USB corroborada");

        const string voxiconId = @"USB\VID_260D&PID_2007&MI_00";
        var voxicon = Device(voxiconId, "Media") with { ReportedDriverClass = "Sound" };
        var voxiconReport = DriverReadOnlyReport.Format(new([voxicon], false, ""), voxicon);
        check(Find(voxicon).Page is { Host: "catalog.update.microsoft.com", AbsolutePath: "/ScopedViewInline.aspx" } &&
              Find(voxicon).Page?.Query == "?updateid=08f12451-f2bc-475a-bc4b-795e75af3860" &&
              DriverHardwareCatalog.Identify(voxicon).Model.Length == 0 &&
              voxiconReport.Contains("Modelo comercial, oferta e compatibilidade inconclusivos", StringComparison.Ordinal),
            "VOXICON consulta o registro de áudio no Catálogo Microsoft sem inferir SKU ou oferta");

        var invalidVoxicon = new[]
        {
            Device(@"USB\VID_260D&PID_2007", "Media"),
            Device(@"USB\VID_260D&PID_2007&MI_01", "Media"),
            voxicon with { HardwareIds = [@"USB\VID_260D&PID_2007"] },
            voxicon with { HardwareIds = [voxiconId + "&MI_00"] },
            voxicon with { ReportedPnpClass = "AudioEndpoint" },
            voxicon with { Present = null }
        };
        check(invalidVoxicon.All(item => Find(item).Page is null && DriverHardwareCatalog.Identify(item).Model.Length == 0),
            "VOXICON exige MI_00 consistente e classe Media na instância presente");
    }

    private static void CheckAdditionalLogitechFlightAndAudio(Action<bool, string> check)
    {
        var legacy = new[]
        {
            ("C207", "Joystick Logitech Extreme Digital 3D"),
            ("C214", "Joystick Logitech Attack 3"),
            ("C283", "Joystick Logitech Force 3D")
        };
        check(legacy.All(product =>
        {
            var item = Device($@"HID\VID_046D&PID_{product.Item1}", "HIDClass") with { HasGameInputUsage = true };
            return Find(item) is { Category: DriverHardwareCategory.GameInput, Page: { Host: "support.logi.com" } page } &&
                   page.AbsolutePath.Contains("360023375193", StringComparison.Ordinal) &&
                   DriverHardwareCatalog.Identify(item).Model == product.Item2;
        }), "Extreme Digital 3D, Attack 3 e Force 3D têm fontes oficiais por VID/PID");

        var g940 = Device(@"HID\VID_046D&PID_C287&COL01", "HIDClass") with { HasGameInputUsage = true };
        var g940Report = DriverReadOnlyReport.Format(new([g940], false, ""), g940);
        check(Find(g940).Page?.AbsolutePath.Contains("360023375193", StringComparison.Ordinal) == true &&
              g940Report.Contains("Logitech Flight System G940", StringComparison.Ordinal) &&
              g940Report.Contains("módulos físicos conectados são inconclusivos", StringComparison.Ordinal) &&
              DriverHardwareCatalog.Identify(g940).Model.Length == 0,
            "Flight System G940 aponta a referência do conjunto e mantém os módulos inconclusivos");

        const string yokeId = @"HID\VID_06A3&PID_0BAC&MI_00";
        var yoke = Device(yokeId, "HIDClass") with
        {
            HasGameInputUsage = true,
            HardwareIds = [yokeId, @"hid\vid_06a3&pid_0bac&rev_0100&mi_00"]
        };
        var throttle = Device(@"HID\VID_06A3&PID_0C2D", "HIDClass") with { HasGameInputUsage = true };
        check(Find(yoke).Page?.AbsolutePath.Contains("360023462873", StringComparison.Ordinal) == true &&
              DriverHardwareCatalog.Identify(yoke).Model == "Manche USB Logitech Flight Yoke System" &&
              Find(yoke).Status.Contains("presença do acelerador ligado ao manche é inconclusiva", StringComparison.Ordinal) &&
              Find(throttle).Page?.AbsolutePath.Contains("360023462893", StringComparison.Ordinal) == true &&
              DriverHardwareCatalog.Identify(throttle).Model == "Acelerador USB Logitech Flight Throttle Quadrant",
            "Flight Yoke e Flight Throttle Quadrant USB têm identidades distintas");

        const string dacId = @"USB\VID_046D&PID_0AAA&MI_00";
        var wired = Device(dacId, "Media");
        var wireless = Device(@"USB\VID_046D&PID_0ABA", "Media");
        var g635 = Device(@"USB\VID_046D&PID_0A89", "Media");
        var wiredSource = Find(wired);
        check(wiredSource.Category == DriverHardwareCategory.Audio &&
              wiredSource.Page == DriverHardwareSupport.Logitech &&
              DriverHardwareCatalog.Identify(wired).Model == "DAC USB Logitech" &&
              wiredSource.Status.Contains("PRO, PRO X e PRO X SE com fio", StringComparison.Ordinal) &&
              wiredSource.Status.Contains("kit e o fone analógico conectado são inconclusivos", StringComparison.Ordinal) &&
              Find(wireless).Page?.AbsolutePath.Contains("360052715513", StringComparison.Ordinal) == true &&
              Find(g635).Page?.AbsolutePath.Contains("360023303694", StringComparison.Ordinal) == true &&
              Find(g635).Status.Contains("modelo físico do headset é inconclusivo", StringComparison.Ordinal) &&
              DriverHardwareCatalog.Identify(g635).Model.Length == 0,
            "DAC Logitech com ID compartilhado e referência G635 mantêm o headset inconclusivo");

        var invalid = new[]
        {
            g940 with { HasGameInputUsage = false },
            yoke with { HardwareIds = [yokeId.Replace("MI_00", "MI_01")] },
            yoke with { HardwareIds = [yokeId.Replace("PID_0BAC", "PID_0C2D")] },
            wired with { HardwareIds = [], CompatibleIds = [dacId] },
            wired with { ReportedPnpClass = "AudioEndpoint" },
            Device(@"HID\VID_046D&PID_0AAA&MI_00", "HIDClass"),
            g635 with { Present = null }
        };
        check(invalid.All(item => Find(item).Page is null && DriverHardwareCatalog.Identify(item).Model.Length == 0),
            "Periféricos Logitech com classe USB ou interface divergente ficam inconclusivos");
    }

    private static void CheckPedalControllerAndHeadsetAdapter(Action<bool, string> check)
    {
        const string tinyBoxId = @"HID\VID_231D&PID_011D&MI_02&COL01";
        var tinyBox = Device(tinyBoxId, "HIDClass") with
        {
            HasGameInputUsage = true,
            HardwareIds = [tinyBoxId, @"hid\vid_231d&pid_011d&rev_0100&mi_02&col01"]
        };
        var tinyBoxReport = DriverReadOnlyReport.Format(new([tinyBox], false, ""), tinyBox);
        check(Find(tinyBox) is { Category: DriverHardwareCategory.GameInput, Page.Host: "vkbsimcontrollers.com" } &&
              DriverHardwareCatalog.Identify(tinyBox).Model == "Controlador USB VKB TinyBox" &&
              tinyBoxReport.Contains("TinyBox para pedais T-Rudder", StringComparison.Ordinal) &&
              tinyBoxReport.Contains("presença e a versão dos pedais são inconclusivas", StringComparison.Ordinal),
            "VKB TinyBox associa a fonte T-Link ao controlador e mantém os pedais inconclusivos");

        const string g533Id = @"USB\VID_046D&PID_0A66&MI_00";
        var g533 = Device(g533Id, "Media") with
        {
            HardwareIds = [g533Id, @"usb\vid_046d&pid_0a66&rev_0100&mi_00"]
        };
        var g533Report = DriverReadOnlyReport.Format(new([g533], false, ""), g533);
        check(Find(g533) is { Category: DriverHardwareCategory.Audio, Page: { Host: "support.logi.com" } g533Page } &&
              g533Page.AbsolutePath.Contains("360023303074", StringComparison.Ordinal) &&
              DriverHardwareCatalog.Identify(g533).Model == "Adaptador USB Logitech G533" &&
              g533Report.Contains("presença e o modelo do headset conectado são inconclusivos", StringComparison.Ordinal),
            "G533 consulta a ficha do adaptador USB sem determinar qual headset está conectado");

        var invalid = new[]
        {
            tinyBox with { HasGameInputUsage = false },
            tinyBox with { ReportedPnpClass = "Media" },
            tinyBox with { HardwareIds = [tinyBoxId.Replace("MI_02", "MI_03")] },
            tinyBox with { Present = null },
            g533 with { ReportedPnpClass = "AudioEndpoint" },
            g533 with { HardwareIds = [], CompatibleIds = [g533Id] },
            g533 with { HardwareIds = [g533Id.Replace("MI_00", "MI_01")] },
            Device(@"HID\VID_046D&PID_0A66&MI_00", "Media")
        };
        check(invalid.All(item => Find(item).Page is null && DriverHardwareCatalog.Identify(item).Model.Length == 0),
            "TinyBox e G533 exigem classe, função e identidade USB coerentes");
    }

    private static void CheckDedicatedPedalsAndAdditionalUsbHeadsets(Action<bool, string> check)
    {
        const string saitekId = @"HID\VID_06A3&PID_0763&MI_00";
        var saitek = Device(saitekId, "HIDClass") with { HasGameInputUsage = true };
        var saitekVariant = Device(@"HID\VID_06A3&PID_0764", "HIDClass") with { HasGameInputUsage = true };
        check(new[] { saitek, saitekVariant }.All(item =>
            Find(item) is { Category: DriverHardwareCategory.GameInput, Page: { Host: "support.logi.com" } page } &&
            page.AbsolutePath.Contains("360024851754", StringComparison.Ordinal) &&
            DriverHardwareCatalog.Identify(item).Model == "Família Saitek Pro Flight Rudder Pedals" &&
            Find(item).Status.Contains("A variante física é inconclusiva", StringComparison.Ordinal)),
            "Saitek 0763 e 0764 reconhecem a família de pedais, preservando a variante inconclusiva");

        const string tfrpId = @"HID\VID_044F&PID_B679&MI_01&COL01";
        var tfrp = Device(tfrpId, "HIDClass") with
        {
            HasGameInputUsage = true,
            HardwareIds = [tfrpId, @"hid\vid_044f&pid_b679&rev_0100&mi_01&col01"]
        };
        var tfrpReport = DriverReadOnlyReport.Format(new([tfrp], false, ""), tfrp);
        check(Find(tfrp) is { Category: DriverHardwareCategory.GameInput, Page.Host: "support.thrustmaster.com" } &&
              DriverHardwareCatalog.Identify(tfrp).Model == "Interface USB Thrustmaster TFRP Rudder" &&
              tfrpReport.Contains("Steam Input", StringComparison.Ordinal) &&
              tfrpReport.Contains("pedais ligados via RJ12 são inconclusivos", StringComparison.Ordinal),
            "TFRP distingue a interface USB da montagem física de pedais RJ12");

        const string arctisId = @"USB\VID_1038&PID_1294&MI_00";
        const string hecateId = @"USB\VID_2D99&PID_0026&MI_01";
        var arctis = Device(arctisId, "Media");
        var hecate = Device(hecateId, "Media");
        check(Find(arctis) is { Category: DriverHardwareCategory.Audio, Page.Host: "support.steelseries.com" } &&
              Find(arctis).Status.Contains("Arctis Pro Wireless", StringComparison.Ordinal) &&
              Find(arctis).Status.Contains("modelo do fone conectado são inconclusivos", StringComparison.Ordinal) &&
              Find(hecate) is { Category: DriverHardwareCategory.Audio, Page.Host: "hecategaming.com" } &&
              Find(hecate).Status.Contains("HECATE G2 Gaming Headset", StringComparison.Ordinal) &&
              Find(hecate).Status.Contains("A variante física é inconclusiva", StringComparison.Ordinal) &&
              new[] { arctis, hecate }.All(item => DriverHardwareCatalog.Identify(item).Model.Length == 0),
            "Arctis Pro Wireless e HECATE G2 consultam fontes por VID/PID com o headset físico inconclusivo");

        var invalid = new[]
        {
            saitek with { HardwareIds = [saitekId.Replace("PID_0763", "PID_0764")] },
            saitekVariant with { HasGameInputUsage = false },
            tfrp with { HardwareIds = [tfrpId.Replace("MI_01", "MI_00")] },
            tfrp with { Present = null },
            arctis with { ReportedPnpClass = "AudioEndpoint" },
            Device(@"HID\VID_1038&PID_1294&MI_00", "Media"),
            hecate with { HardwareIds = [hecateId.Replace("MI_01", "MI_00")] },
            hecate with { HardwareIds = [], CompatibleIds = [hecateId] }
        };
        check(invalid.All(item => Find(item).Page is null && DriverHardwareCatalog.Identify(item).Model.Length == 0),
            "Pedais e headsets com classe, função ou identidade USB divergentes permanecem inconclusivos");
    }

    private static DriverInventoryEntry Device(string id, string pnpClass) =>
        new(id + @"\INSTANCIA", "", "", "", "", null, true)
        { HardwareIds = [id], ReportedPnpClass = pnpClass, ReportedDriverClass = pnpClass };

    private static DriverHardwareSupportInfo Find(DriverInventoryEntry item) =>
        DriverHardwareSupport.Get(new([item], false, ""), item);
}
