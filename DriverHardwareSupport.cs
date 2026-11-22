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
    private static readonly Uri Logitech = new("https://support.logi.com/hc/pt-br");
    private static readonly Uri Thrustmaster = new("https://support.thrustmaster.com/pt/");
    private static readonly Uri MicrosoftWindows = new("https://support.microsoft.com/pt-br/windows");
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

        uint? pci = DriverHardwareCatalog.ReportedPciProduct(item) >> 16;
        uint? usb = DriverHardwareCatalog.ReportedUsbProduct(item) >> 16;
        Uri? page = (category, pci, usb) switch
        {
            (DriverHardwareCategory.Graphics, 0x10de, _) => NvidiaDrivers,
            (DriverHardwareCategory.Graphics or DriverHardwareCategory.Platform, 0x1002 or 0x1022, _) => AmdDrivers,
            (DriverHardwareCategory.Graphics, 0x8086, _) => IntelGraphics,
            (DriverHardwareCategory.Platform, 0x8086, _) => IntelPlatform,
            (DriverHardwareCategory.Network, 0x8086, _) => IntelNetwork,
            (DriverHardwareCategory.GameInput or DriverHardwareCategory.Peripherals, _, 0x046d) => Logitech,
            (DriverHardwareCategory.GameInput or DriverHardwareCategory.Peripherals, _, 0x044f) => Thrustmaster,
            (DriverHardwareCategory.GameInput, _, 0x045e) => MicrosoftWindows,
            _ => null
        };
        return page is null
            ? new(category, null, "Fonte específica inconclusiva. Confira as ofertas do Windows Update para este dispositivo.")
            : new(category, page, category == DriverHardwareCategory.GameInput
                ? "Consulte o fabricante pelo modelo. Driver, aplicativo e firmware têm versões próprias."
                : "Consulta manual ao suporte oficial. Confirme o modelo, a variante OEM e a versão do Windows.");
    }
}
