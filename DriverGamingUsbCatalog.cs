namespace GerenciadorAplicativos;

internal sealed record DriverGamingUsbSupport(Uri? Page, string? Model, string Status);

internal static class DriverGamingUsbCatalog
{
    private readonly record struct UsbProduct(Uri Page, string? Model, bool IsAudio,
        string? Status = null, uint? RequiredInterface = null);

    private static readonly Uri Extreme3DPro = LogitechArticle("360023331594");
    private static readonly Uri X52 = LogitechArticle("360023462973");
    private static readonly Uri X52Pro = LogitechArticle("360023462793");
    private static readonly Uri X56 = LogitechArticle("360023302854");
    private static readonly Uri LegacyLogitechFlight = new("https://support.logi.com/hc/en-us/articles/360023375193");
    private static readonly Uri FlightYoke = LogitechArticle("360023462873");
    private static readonly Uri FlightThrottle = LogitechArticle("360023462893");
    private static readonly Uri G430 = LogitechArticle("360023466633");
    private static readonly Uri G533 = LogitechArticle("360023303074");
    private static readonly Uri G930 = LogitechArticle("360023466133");
    private static readonly Uri G933 = LogitechArticle("360023464013");
    private static readonly Uri G733 = LogitechArticle("17207077246871");
    private static readonly Uri ProXWireless = LogitechArticle("360052715513");
    private static readonly Uri G635 = LogitechArticle("360023303694");
    private static readonly Uri VkbSupport = new("https://vkbsimcontrollers.com/pages/downloads");
    private static readonly Uri EposCommand260 = new("https://www.eposaudio.com/en-br/products/audio/series/epos-command-260-usb-ptt/");
    private static readonly Uri VoxiconAudioRecord = new("https://catalog.update.microsoft.com/ScopedViewInline.aspx?updateid=08f12451-f2bc-475a-bc4b-795e75af3860");

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
            DriverHardwareCatalog.ReportedUsbIdentity(item) is not { } identity ||
            (device.RequiredInterface is uint required && identity.Interface != required))
            return new(null, null,
                "Modelo USB inconclusivo nesta leitura. Confirme a classe PnP e a identidade da interface.");

        return new(device.Page, device.Model, device.Status ??
            $"{device.Model} identificado pelo VID/PID e pela função PnP. Consulte a documentação oficial para o Windows.");
    }

    private static UsbProduct? Product(uint? product) => product switch
    {
        0x046dc215 => new(Extreme3DPro, "Joystick Logitech Extreme 3D Pro", IsAudio: false),
        0x06a30255 => new(X52, "Controle de voo Logitech X52 H.O.T.A.S.", IsAudio: false),
        0x06a30762 => new(X52Pro, "Controle de voo Logitech X52 Pro H.O.T.A.S.", IsAudio: false),
        0x07382221 => new(X56, "Joystick Logitech X56 H.O.T.A.S.", IsAudio: false),
        0x0738a221 => new(X56, "Acelerador Logitech X56 H.O.T.A.S.", IsAudio: false),
        0x046dc207 => new(LegacyLogitechFlight, "Joystick Logitech Extreme Digital 3D", IsAudio: false),
        0x046dc214 => new(LegacyLogitechFlight, "Joystick Logitech Attack 3", IsAudio: false),
        0x046dc283 => new(LegacyLogitechFlight, "Joystick Logitech Force 3D", IsAudio: false),
        0x046dc287 => new(LegacyLogitechFlight, null, IsAudio: false,
            Status: "O VID/PID consta na documentação do Logitech Flight System G940. Os módulos físicos conectados são inconclusivos."),
        0x06a30bac => new(FlightYoke, "Manche USB Logitech Flight Yoke System", IsAudio: false,
            Status: "A ficha Logitech associa este VID/PID ao manche. A presença do acelerador ligado ao manche é inconclusiva."),
        0x06a30c2d => new(FlightThrottle, "Acelerador USB Logitech Flight Throttle Quadrant", IsAudio: false),
        0x046d0a4d => new(G430, "Adaptador USB Logitech G430", IsAudio: true),
        0x046d0a66 => new(G533, "Adaptador USB Logitech G533", IsAudio: true,
            Status: "A ficha do Logitech G533 atribui este VID/PID ao adaptador. A presença e o modelo do headset conectado são inconclusivos."),
        0x046d0a1f => new(G930, "Receptor USB Logitech G930", IsAudio: true),
        0x046d0a5b => new(G933, "Receptor USB Logitech G933", IsAudio: true),
        0x046d0ab6 => new(G733, "Receptor USB Logitech G733", IsAudio: true),
        0x046d0aba => new(ProXWireless, "Receptor USB Logitech PRO X Wireless", IsAudio: true),
        0x046d0aaa => new(DriverHardwareSupport.Logitech, "DAC USB Logitech", IsAudio: true,
            Status: "As fichas do PRO, PRO X e PRO X SE com fio documentam este VID/PID. O kit e o fone analógico conectado são inconclusivos."),
        0x046d0a89 => new(G635, null, IsAudio: true,
            Status: "O VID/PID da função USB de áudio consta na ficha do Logitech G635. O modelo físico do headset é inconclusivo."),
        0x046d0a87 => new(DriverHardwareSupport.Logitech, null, IsAudio: true,
            Status: "Receptor USB Logitech com VID/PID compartilhado por G935 e G933s. O modelo do headset é inconclusivo."),
        // O manual VKB mostra perfis do controlador. O VID/PID sozinho não determina a montagem física.
        0x231d011d => new(VkbSupport, "Controlador USB VKB TinyBox", IsAudio: false,
            Status: "O manual VKB T-Link documenta o controlador USB TinyBox para pedais T-Rudder. A presença e a versão dos pedais são inconclusivas."),
        0x231d0117 => new(VkbSupport, null, IsAudio: false,
            Status: "O manual VKB Njoy32 documenta este VID/PID no perfil Fat Mamba. O modelo físico é inconclusivo."),
        0x231d0125 => new(VkbSupport, null, IsAudio: false,
            Status: "O manual VKB Njoy32 documenta este VID/PID no perfil Gunfighter Modern Combat PRO. O modelo físico é inconclusivo."),
        0x13950400 => new(EposCommand260, "Família EPOS Command 260 USB PTT", IsAudio: true,
            Status: "O VID/PID consta na ficha da família EPOS Command 260 USB PTT. A variante física é inconclusiva."),
        // O registro oficial identifica uma interface e uma descrição genérica, sem SKU comercial.
        0x260d2007 => new(VoxiconAudioRecord, null, IsAudio: true, RequiredInterface: 0,
            Status: "O Catálogo Microsoft registra áudio VOXICON neste VID/PID e MI_00. Modelo comercial, oferta e compatibilidade inconclusivos."),
        _ => null
    };
}
