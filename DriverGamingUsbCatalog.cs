namespace GerenciadorAplicativos;

internal sealed record DriverGamingUsbSupport(Uri? Page, string? Model, string Status);

internal static class DriverGamingUsbCatalog
{
    private static readonly Uri Extreme3DPro = LogitechArticle("360023331594");
    private static readonly Uri X52 = LogitechArticle("360023462973");
    private static readonly Uri X52Pro = LogitechArticle("360023462793");
    private static readonly Uri X56 = LogitechArticle("360023302854");
    private static readonly Uri G430 = LogitechArticle("360023466633");
    private static readonly Uri G930 = LogitechArticle("360023466133");
    private static readonly Uri G933 = LogitechArticle("360023464013");
    private static readonly Uri G733 = LogitechArticle("17207077246871");
    private static readonly Uri ProXWireless = LogitechArticle("360052715513");

    private static Uri LogitechArticle(string article) =>
        new($"https://support.logi.com/hc/pt-br/articles/{article}");

    internal static DriverGamingUsbSupport? Find(DriverInventoryEntry item)
    {
        var product = DriverHardwareCatalog.InstanceUsbProduct(item);
        if (Product(product) is not { } device) return null;

        string expectedClass = device.IsAudio ? "Media" : "HIDClass";
        var expectedCategory = device.IsAudio ? DriverHardwareCategory.Audio : DriverHardwareCategory.GameInput;
        if (item.Present != true ||
            !item.ReportedPnpClass.Equals(expectedClass, StringComparison.OrdinalIgnoreCase) ||
            DriverHardwareSupport.CategoryOf(item) != expectedCategory ||
            (device.IsAudio && !item.DeviceId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase)) ||
            DriverHardwareCatalog.ReportedUsbIdentity(item) is null)
            return new(null, null,
                "Modelo USB inconclusivo nesta leitura. Confirme a classe PnP e a identidade da interface.");

        return device.Model is null
            ? new(device.Page, null,
                "Receptor USB Logitech com VID/PID compartilhado por G935 e G933s. O modelo do headset é inconclusivo.")
            : new(device.Page, device.Model,
                $"{device.Model} identificado pelo VID/PID e pela função PnP. Consulte a documentação oficial para o Windows.");
    }

    // A Logitech documenta os VID/PID de joystick e acelerador separadamente.
    // Nos headsets sem fio, o ID USB atribui o receptor. No G430, atribui o adaptador.
    private static (Uri Page, string? Model, bool IsAudio)? Product(uint? product) => product switch
    {
        0x046dc215 => (Extreme3DPro, "Joystick Logitech Extreme 3D Pro", false),
        0x06a30255 => (X52, "Controle de voo Logitech X52 H.O.T.A.S.", false),
        0x06a30762 => (X52Pro, "Controle de voo Logitech X52 Pro H.O.T.A.S.", false),
        0x07382221 => (X56, "Joystick Logitech X56 H.O.T.A.S.", false),
        0x0738a221 => (X56, "Acelerador Logitech X56 H.O.T.A.S.", false),
        0x046d0a4d => (G430, "Adaptador USB Logitech G430", true),
        0x046d0a1f => (G930, "Receptor USB Logitech G930", true),
        0x046d0a5b => (G933, "Receptor USB Logitech G933", true),
        0x046d0ab6 => (G733, "Receptor USB Logitech G733", true),
        0x046d0aba => (ProXWireless, "Receptor USB Logitech PRO X Wireless", true),
        0x046d0a87 => (DriverHardwareSupport.Logitech, null, true),
        _ => null
    };
}
