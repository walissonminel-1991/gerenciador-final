namespace GerenciadorAplicativos;

internal enum DriverHardwareCategory
{
    Unknown, Graphics, Platform, Network, Audio, GameInput, Peripherals, Storage, Firmware
}

internal sealed record DriverHardwareSupportInfo(DriverHardwareCategory Category, Uri? Page, string Status);

// As classes e o catálogo identificam o hardware para consulta. Nenhuma
// destas rotas indica a compatibilidade de um pacote específico.
internal static class DriverHardwareSupport
{
    internal static readonly Uri NvidiaDrivers = new("https://www.nvidia.com/pt-br/drivers/");
    internal static readonly Uri AmdDrivers = new("https://www.amd.com/en/support/download/drivers.html");
    private static readonly Uri IntelGraphics = new("https://www.intel.com/content/www/us/en/support/articles/000090440/processors.html");
    private static readonly Uri IntelPlatform = new("https://www.intel.com/content/www/us/en/support/articles/000005533/software/chipset-software.html");
    private static readonly Uri IntelNetwork = new("https://www.intel.com/content/www/us/en/download-center/home.html");
    private static readonly Uri NvidiaKepler = new("https://nvidia.custhelp.com/app/answers/detail/a_id/5202");
    private static readonly Uri AmdLegacy = new("https://www.amd.com/en/resources/support-articles/faqs/GPU-630.html");
    private static readonly Uri IntelLegacy = new("https://www.intel.com/content/www/us/en/support/articles/000008632/graphics.html");
    private static readonly Uri MatroxLegacy = new("https://video.matrox.com/en/support/legacy/");
    private static readonly Uri Dell = new("https://www.dell.com/support/home/pt-br?app=drivers");
    private static readonly Uri Hp = new("https://support.hp.com/br-pt/drivers");
    private static readonly Uri Lenovo = new("https://pcsupport.lenovo.com/br/pt");
    private static readonly Uri Acer = new("https://www.acer.com/br-pt/support/drivers-and-manuals");
    private static readonly Uri RealtekNetwork = new("https://www.realtek.com/Download/Index?cate_id=194&menu_id=368");
    private static readonly Uri RealtekEthernet = new("https://www.realtek.com/Download/List?cate_id=584");
    private static readonly Uri AsixEthernet = new("https://www.asix.com.tw/en/support/download/step2/11/2/3");
    private static readonly Uri Logitech = new("https://support.logi.com/hc/pt-br");
    private static readonly Uri Thrustmaster = new("https://support.thrustmaster.com/pt/");
    private static readonly Uri MicrosoftWindows = new("https://support.microsoft.com/pt-br/windows");
    private static readonly Uri SonyDualSense = new("https://www.playstation.com/pt-br/support/hardware/pair-dualsense-controller-bluetooth/");
    private static readonly Uri SonyDualSenseEdge = new("https://www.playstation.com/pt-br/support/hardware/dualsense-edge-other-devices/");
    private static readonly Uri Corsair = new("https://www.corsair.com/br/pt/s/downloads");
    private static readonly Uri SteelSeries = new("https://support.steelseries.com/hc/en-us/");
    private static readonly Uri UsbAudio = new("https://learn.microsoft.com/pt-br/windows-hardware/drivers/audio/usb-2-0-audio-drivers");
    private static readonly Uri Nvme = new("https://learn.microsoft.com/pt-br/windows-hardware/drivers/storage/nvme-features-supported-by-stornvme");

    internal static DriverHardwareCategory CategoryOf(DriverInventoryEntry item)
    {
        var pnp = FromClass(item.ReportedPnpClass);
        var wmi = FromClass(item.ReportedDriverClass);
        if (pnp != DriverHardwareCategory.Unknown && wmi != DriverHardwareCategory.Unknown && pnp != wmi)
            return DriverHardwareCategory.Unknown;
        var category = pnp != DriverHardwareCategory.Unknown ? pnp : wmi;
        if (category == DriverHardwareCategory.Peripherals && item.HasGameInputUsage &&
            IsHidClass(pnp != DriverHardwareCategory.Unknown ? item.ReportedPnpClass : item.ReportedDriverClass))
            return DriverHardwareCategory.GameInput;
        return category;
    }

    private static bool IsHidClass(string value) =>
        value.Equals("HIDClass", StringComparison.OrdinalIgnoreCase);

    private static DriverHardwareCategory FromClass(string value) => value.ToUpperInvariant() switch
    {
        "DISPLAY" => DriverHardwareCategory.Graphics,
        "SYSTEM" or "PROCESSOR" => DriverHardwareCategory.Platform,
        "NET" or "BLUETOOTH" => DriverHardwareCategory.Network,
        "MEDIA" or "AUDIOENDPOINT" => DriverHardwareCategory.Audio,
        "HIDCLASS" or "KEYBOARD" or "MOUSE" or "USBDEVICE" => DriverHardwareCategory.Peripherals,
        "DISKDRIVE" or "SCSIADAPTER" or "HDC" or "STORAGEVOLUME" => DriverHardwareCategory.Storage,
        "FIRMWARE" => DriverHardwareCategory.Firmware,
        _ => DriverHardwareCategory.Unknown
    };

    internal static string Label(DriverHardwareCategory category) => category switch
    {
        DriverHardwareCategory.Graphics => "Vídeo / GPU",
        DriverHardwareCategory.Platform => "Plataforma e chipset",
        DriverHardwareCategory.Network => "Rede e Bluetooth",
        DriverHardwareCategory.Audio => "Áudio",
        DriverHardwareCategory.GameInput => "Controles de jogo",
        DriverHardwareCategory.Peripherals => "Periféricos",
        DriverHardwareCategory.Storage => "Armazenamento",
        DriverHardwareCategory.Firmware => "Firmware",
        _ => "Categoria inconclusiva"
    };

    internal static DriverHardwareSupportInfo Get(DriverInventoryResult inventory, DriverInventoryEntry item)
    {
        var category = CategoryOf(item);
        if (inventory.Partial || item.Present != true || !inventory.Items.Contains(item) ||
            inventory.Items.Count(row => row.DeviceId.Equals(item.DeviceId, StringComparison.OrdinalIgnoreCase)) != 1)
            return new(category, null, "Fonte inconclusiva nesta leitura. Confirme a presença e a identidade do dispositivo.");

        if (item.BindingStatus != DriverBindingStatus.Conflicting && !item.WithoutSignedDriverAssociation &&
            (item.Provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
             item.Provider.Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase)))
        {
            if (category == DriverHardwareCategory.Audio && item.InfName.Equals("usbaudio2.inf", StringComparison.OrdinalIgnoreCase))
                return new(category, UsbAudio, "O inventário informa o driver USB Audio 2.0 fornecido pelo Windows.");
            if (category == DriverHardwareCategory.Storage && item.InfName.Equals("stornvme.inf", StringComparison.OrdinalIgnoreCase))
                return new(category, Nvme, "O inventário informa o driver StorNVMe fornecido pelo Windows.");
        }

        var pci = DriverHardwareCatalog.ReportedPciIdentity(item);
        if (category is DriverHardwareCategory.Graphics or DriverHardwareCategory.Network &&
            pci is { Subsystem: uint subsystem } identifiedPci && OemFor(identifiedPci) is { } oem)
            return new(category, oem.Page, $"O código PCI SUBSYS_{subsystem:X8} indica uma variante {oem.Name}. " +
                "Confirme o modelo do equipamento e o Windows no suporte OEM.");

        if (category == DriverHardwareCategory.Graphics && LegacyPage(pci?.Product) is { } legacy)
            return new(category, legacy, "Suporte legado do fabricante. Confira o sistema operacional e o pacote indicado para o modelo.");

        uint? usbProduct = DriverHardwareCatalog.ReportedUsbProduct(item);
        if (category == DriverHardwareCategory.Network && NetworkPage(pci, usbProduct) is { } network)
            return new(category, network, "Consulta manual ao suporte de rede. Confirme o modelo, a variante OEM e o Windows.");

        bool input = category is DriverHardwareCategory.GameInput or DriverHardwareCategory.Peripherals;
        Uri? page = input ? InputPage(category, usbProduct) : (category, pci?.Vendor) switch
        {
            (DriverHardwareCategory.Graphics, 0x10de) => NvidiaDrivers,
            (DriverHardwareCategory.Graphics or DriverHardwareCategory.Platform, 0x1002 or 0x1022) => AmdDrivers,
            (DriverHardwareCategory.Graphics, 0x8086) => IntelGraphics,
            (DriverHardwareCategory.Platform, 0x8086) => IntelPlatform,
            _ => null
        };
        return page is null
            ? new(category, null, "Fonte específica inconclusiva. Confira as ofertas do Windows Update para este dispositivo.")
            : new(category, page, category == DriverHardwareCategory.GameInput
                ? "Consulte o fabricante pelo modelo. Driver, aplicativo e firmware têm versões próprias."
                : "Consulta manual ao suporte oficial. Confirme o modelo, a variante OEM e a versão do Windows.");
    }

    internal static (string Name, Uri Page)? OemFor(DriverPciIdentity pci) => pci.SubsystemVendor switch
    {
        0x1028 => ("Dell", Dell),
        0x103c => ("HP", Hp),
        0x17aa => ("Lenovo", Lenovo),
        0x1025 => ("Acer", Acer),
        _ => null
    };

    private static Uri? LegacyPage(uint? product) => product switch
    {
        0x10de0fc6 or 0x10de0fc8 => NvidiaKepler,
        0x10026770 or 0x10026779 => AmdLegacy,
        0x80860166 => IntelLegacy,
        0x102b0520 or 0x102b0521 => MatroxLegacy,
        _ => null
    };

    private static Uri? NetworkPage(DriverPciIdentity? pci, uint? usb) => (pci?.Vendor, pci?.Product, usb) switch
    {
        (0x8086, _, _) => IntelNetwork,
        (0x10ec, 0x10ec8125 or 0x10ec8168, _) => RealtekEthernet,
        (0x10ec, _, _) => RealtekNetwork,
        (_, _, 0x0b951790) => AsixEthernet,
        _ when (usb >> 16) == 0x0bda => RealtekNetwork,
        _ => null
    };

    private static Uri? InputPage(DriverHardwareCategory category, uint? product) => product switch
    {
        0x054c0ce6 => SonyDualSense,
        0x054c0df2 => SonyDualSenseEdge,
        _ => (product >> 16) switch
        {
            0x046d => Logitech,
            0x044f => Thrustmaster,
            0x045e when category == DriverHardwareCategory.GameInput => MicrosoftWindows,
            0x1b1c => Corsair,
            0x1038 => SteelSeries,
            _ => null
        }
    };
}
