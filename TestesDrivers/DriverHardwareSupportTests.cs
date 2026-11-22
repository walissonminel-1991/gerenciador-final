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
    }

    private static DriverInventoryEntry Device(string id, string pnpClass) =>
        new(id + @"\INSTANCIA", "", "", "", "", null, true)
        { HardwareIds = [id], ReportedPnpClass = pnpClass, ReportedDriverClass = pnpClass };

    private static DriverHardwareSupportInfo Find(DriverInventoryEntry item) =>
        DriverHardwareSupport.Get(new([item], false, ""), item);
}
