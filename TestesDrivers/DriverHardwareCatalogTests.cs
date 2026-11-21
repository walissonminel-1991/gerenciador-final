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
    }
}
