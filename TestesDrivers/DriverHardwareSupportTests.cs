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

    private static DriverInventoryEntry Device(string id, string pnpClass) =>
        new(id + @"\INSTANCIA", "", "", "", "", null, true)
        { HardwareIds = [id], ReportedPnpClass = pnpClass, ReportedDriverClass = pnpClass };

    private static DriverHardwareSupportInfo Find(DriverInventoryEntry item) =>
        DriverHardwareSupport.Get(new([item], false, ""), item);
}
