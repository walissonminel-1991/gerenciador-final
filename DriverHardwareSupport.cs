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
    // Referências históricas não atestam compatibilidade com o Windows atual.
    private static readonly Uri NvidiaFermi = new("https://www.nvidia.com/pt-br/drivers/details/132985/");
    private static readonly Uri IntelOldOs = new("https://www.intel.com/content/www/us/en/support/articles/000005526/graphics.html");
    private static readonly Uri Dell = new("https://www.dell.com/support/home/pt-br?app=drivers");
    private static readonly Uri Hp = new("https://support.hp.com/br-pt/drivers");
    private static readonly Uri Lenovo = new("https://pcsupport.lenovo.com/br/pt");
    private static readonly Uri Acer = new("https://www.acer.com/br-pt/support/drivers-and-manuals");
    private static readonly Uri RealtekNetwork = new("https://www.realtek.com/Download/Index?cate_id=194&menu_id=368");
    private static readonly Uri RealtekEthernet = new("https://www.realtek.com/Download/List?cate_id=584");
    private static readonly Uri AsixEthernet = new("https://www.asix.com.tw/en/support/download/step2/11/2/3");
    // A Qualcomm orienta consultar o OEM; MediaTek não mantém catálogo público Windows nesta URL.
    private static readonly Uri QualcommQca9377 = new("https://www.qualcomm.com/wi-fi/products/qca9377/support");
    private static readonly Uri QualcommDrivers = new("https://www.qualcomm.com/drivers");
    private static readonly Uri MediaTekNetwork = new("https://www.mediatek.com/products/networking-and-connectivity");
    private static readonly Uri Broadcom5719 = new("https://www.broadcom.com/products/ethernet-connectivity/network-adapters/bcm5719-4p");
    private static readonly Uri Broadcom5720 = new("https://www.broadcom.com/products/ethernet-connectivity/network-adapters/bcm5720-2p");
    private static readonly Uri MarvellAqtion = new("https://www.marvell.com/support/ethernet-adapters-and-controllers.html");
    private static readonly Uri MarvellYukon = new("https://www.catalog.update.microsoft.com/Search.aspx?q=Marvell%20Yukon%2088E8056");
    // Páginas de metadados do Catálogo Microsoft, sem selecionar um pacote ou versão.
    private static readonly Uri QualcommBluetooth = new("https://www.catalog.update.microsoft.com/Search.aspx?q=USB%5CVID_0CF3%26PID_E300");
    private static readonly Uri MediaTekRz608 = new("https://www.catalog.update.microsoft.com/Search.aspx?q=USB%5CVID_0E8D%26PID_0608%26MI_00");
    private static readonly Uri MediaTekRz616 = new("https://www.catalog.update.microsoft.com/Search.aspx?q=USB%5CVID_0E8D%26PID_0616%26MI_00");
    internal static readonly Uri Logitech = new("https://support.logi.com/hc/pt-br");
    private static readonly Uri LogitechWheels = new("https://support.logi.com/hc/pt-br/articles/4404187621911-Troubleshoot-G923-G29-G920-for-connection-Issues");
    private static readonly Uri LogitechF310 = new("https://support.logi.com/hc/pt-br/articles/360023467133-Logitech-Gamepad-F310-Technical-Specifications");
    private static readonly Uri LogitechF710 = new("https://support.logi.com/hc/pt-br/articles/360023465553-Logitech-Wireless-Gamepad-F710-Technical-Specifications");
    private static readonly Uri Thrustmaster = new("https://support.thrustmaster.com/pt/");
    private static readonly Uri MicrosoftWindows = new("https://support.microsoft.com/pt-br/windows");
    private static readonly Uri SonyDualSense = new("https://www.playstation.com/pt-br/support/hardware/pair-dualsense-controller-bluetooth/");
    private static readonly Uri SonyDualSenseEdge = new("https://www.playstation.com/pt-br/support/hardware/dualsense-edge-other-devices/");
    private static readonly Uri SonyDualShock4 = new("https://www.playstation.com/pt-br/support/hardware/ps4-pair-dualshock-4-wireless-with-pc-or-mac/");
    private static readonly Uri SteamController2015 = new("https://help.steampowered.com/pt-br/faqs/view/1796-5FC3-88B3-C85F");
    private static readonly Uri Xbox360Catalog = new("https://www.catalog.update.microsoft.com/Search.aspx?q=USB%5CVID_045E%26PID_028E");
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

        bool microsoftBinding = item.BindingStatus != DriverBindingStatus.Conflicting && !item.WithoutSignedDriverAssociation &&
            (item.Provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ||
             item.Provider.Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase));
        bool windowsUsbAudio = microsoftBinding && category == DriverHardwareCategory.Audio &&
            item.InfName.Equals("usbaudio2.inf", StringComparison.OrdinalIgnoreCase);
        const string windowsUsbAudioStatus = "O inventário informa o driver USB Audio 2.0 fornecido pelo Windows.";
        if (DriverGamingUsbCatalog.Find(item) is { } gamingUsb)
        {
            var devicePage = gamingUsb.Page ?? (windowsUsbAudio ? UsbAudio : null);
            var status = windowsUsbAudio ? gamingUsb.Status + " " + windowsUsbAudioStatus : gamingUsb.Status;
            return new(category, devicePage, status);
        }
        if (windowsUsbAudio)
            return new(category, UsbAudio, windowsUsbAudioStatus);
        if (microsoftBinding && category == DriverHardwareCategory.Storage &&
            item.InfName.Equals("stornvme.inf", StringComparison.OrdinalIgnoreCase))
            return new(category, Nvme, "O inventário informa o driver StorNVMe fornecido pelo Windows.");

        var pci = DriverHardwareCatalog.ReportedPciIdentity(item);
        if (category is DriverHardwareCategory.Graphics or DriverHardwareCategory.Network &&
            pci is { Subsystem: uint subsystem } identifiedPci && OemFor(identifiedPci) is { } oem)
            return new(category, oem.Page, $"O código PCI SUBSYS_{subsystem:X8} indica uma variante {oem.Name}. " +
                "Confirme o modelo do equipamento e o Windows no suporte OEM.");

        if (category == DriverHardwareCategory.Graphics && LegacyPage(pci?.Product) is { } legacy)
            return new(category, legacy, legacy == IntelOldOs
                ? "Suporte legado: matriz oficial de sistemas operacionais, não um pacote confirmado para este Windows."
                : "Suporte legado do fabricante. Referência histórica, não oferta atual nem prova de compatibilidade com este Windows.");

        uint? usbProduct = DriverHardwareCatalog.ReportedUsbProduct(item);
        if (category == DriverHardwareCategory.Network && EthernetSource(pci?.Product) is { } ethernet)
            return new(category, ethernet.Page, ethernet.Page == MarvellYukon
                ? $"{ethernet.Model} reconhecido pelo ID PCI. Catálogo histórico da Microsoft; compatibilidade inconclusiva."
                : $"{ethernet.Model} reconhecido pelo ID PCI. Confirme a variante e o Windows no suporte oficial.");

        if (category == DriverHardwareCategory.Network && BluetoothSource(item) is { } bluetooth)
            return new(category, bluetooth.Page, bluetooth.Label +
                " Consulta manual ao Catálogo Microsoft. Oferta e compatibilidade continuam inconclusivas.");

        if (category == DriverHardwareCategory.Network && NetworkPage(pci, usbProduct) is { } network)
        {
            string status = network == QualcommQca9377
                ? "ID PCI Qualcomm QCA9377 reconhecido. A Qualcomm orienta procurar o OEM para drivers; sem oferta ou compatibilidade confirmada."
                : network == QualcommDrivers
                    ? "Fabricante Qualcomm/Atheros identificado; modelo e pacote específicos inconclusivos. Consulte o OEM."
                    : network == MediaTekNetwork
                        ? (pci?.Product switch
                        {
                            0x14c37961 => "ID PCI MediaTek MT7921 reconhecido. ",
                            0x14c30616 => "ID PCI MediaTek MT7922/RZ616 reconhecido. ",
                            _ => "Fabricante MediaTek identificado; modelo específico inconclusivo. "
                        }) + "Página institucional, não catálogo de drivers Windows. Confirme o modelo no OEM."
                        : "Consulta manual ao suporte de rede. Confirme o modelo, a variante OEM e o Windows.";
            return new(category, network, status);
        }

        bool input = category is DriverHardwareCategory.GameInput or DriverHardwareCategory.Peripherals;
        if (input && LogitechGameSource(usbProduct) is { } logitech)
            return new(category, logitech.Page, logitech.Model is null
                ? "VID/PID Logitech compartilhado em DirectInput. O modelo específico é inconclusivo. Confira a identificação física."
                : $"{logitech.Model} reconhecido pelo VID/PID. Consulte a documentação oficial para o Windows.");

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
            : new(category, page, page == Xbox360Catalog
                ? "Catálogo histórico da Microsoft para este VID/PID; não comprova oferta atual nem compatibilidade do pacote."
                : page == SteamController2015
                    ? "Suporte oficial do Steam Controller de 2015 (descontinuado). Firmware, aplicativo e driver são distintos."
                    : category == DriverHardwareCategory.GameInput
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
        0x10de0dc4 or 0x10de0e22 => NvidiaFermi,
        0x10026770 or 0x10026779 or 0x100268f9 => AmdLegacy,
        0x80860166 => IntelLegacy,
        0x80860126 => IntelOldOs,
        0x102b0520 or 0x102b0521 => MatroxLegacy,
        _ => null
    };

    private static (Uri Page, string Model)? EthernetSource(uint? product) => product switch
    {
        0x14e41657 => (Broadcom5719, "Broadcom NetXtreme BCM5719"),
        0x14e4165f => (Broadcom5720, "Broadcom NetXtreme BCM5720"),
        0x1d6a07b1 => (MarvellAqtion, "Marvell AQtion AQC107"),
        0x1d6a04c0 => (MarvellAqtion, "Marvell AQtion AQC113"),
        0x11ab4364 => (MarvellYukon, "Marvell Yukon 88E8056"),
        _ => null
    };

    private static (Uri Page, string Label)? BluetoothSource(DriverInventoryEntry item)
    {
        if (!item.DeviceId.StartsWith(@"USB\VID_", StringComparison.OrdinalIgnoreCase) ||
            (!item.ReportedPnpClass.Equals("Bluetooth", StringComparison.OrdinalIgnoreCase) &&
             !item.ReportedDriverClass.Equals("Bluetooth", StringComparison.OrdinalIgnoreCase)) ||
            item.ReportedPnpClass.Equals("Net", StringComparison.OrdinalIgnoreCase) ||
            item.ReportedDriverClass.Equals("Net", StringComparison.OrdinalIgnoreCase))
            return null;

        return DriverHardwareCatalog.ReportedUsbIdentity(item) switch
        {
            { Product: 0x0cf3e300, Interface: null } =>
                (QualcommBluetooth, "Rádio Qualcomm/Atheros reconhecido com modelo específico inconclusivo."),
            { Product: 0x0e8d0608, Interface: 0 } =>
                (MediaTekRz608, "Rádio MediaTek RZ608 reconhecido pelo ID USB."),
            { Product: 0x0e8d0616, Interface: 0 } =>
                (MediaTekRz616, "Rádio MediaTek RZ616 reconhecido pelo ID USB."),
            _ => null
        };
    }

    private static Uri? NetworkPage(DriverPciIdentity? pci, uint? usb) => (pci?.Vendor, pci?.Product, usb) switch
    {
        (0x8086, _, _) => IntelNetwork,
        (0x168c, 0x168c0042, _) => QualcommQca9377,
        (0x168c or 0x17cb, _, _) => QualcommDrivers,
        (0x14c3, _, _) => MediaTekNetwork,
        (0x10ec, 0x10ec8125 or 0x10ec8168, _) => RealtekEthernet,
        (0x10ec, _, _) => RealtekNetwork,
        (_, _, 0x0b951790) => AsixEthernet,
        _ when (usb >> 16) == 0x0bda => RealtekNetwork,
        _ => null
    };

    private static (Uri Page, string? Model)? LogitechGameSource(uint? product) => product switch
    {
        0x046dc24f => (LogitechWheels, "Volante Logitech G29"),
        0x046dc262 => (LogitechWheels, "Volante Logitech G920"),
        0x046dc266 => (LogitechWheels, "Volante Logitech G923 para PlayStation"),
        0x046dc26e => (LogitechWheels, "Volante Logitech G923 para Xbox"),
        0x046dc21d => (LogitechF310, "Controle Logitech F310 em modo XInput"),
        0x046dc21f => (LogitechF710, "Controle Logitech F710 em modo XInput"),
        0x046dc216 or 0x046dc219 => (Logitech, null),
        _ => null
    };

    private static Uri? InputPage(DriverHardwareCategory category, uint? product) => product switch
    {
        0x054c0ce6 => SonyDualSense,
        0x054c0df2 => SonyDualSenseEdge,
        0x054c05c4 or 0x054c09cc => SonyDualShock4,
        0x28de1102 => SteamController2015,
        0x045e028e when category == DriverHardwareCategory.GameInput => Xbox360Catalog,
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
