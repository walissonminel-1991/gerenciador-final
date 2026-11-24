using System.Runtime.InteropServices;
using System.Text;

namespace GerenciadorAplicativos;

// Inventário plug-and-play independente de fabricante, sob demanda e somente leitura.
// Não instala drivers nem consulta sensores/registradores privilegiados.
// ID de instância é mantido APENAS em memória para eventual associação exata.
internal enum HardwareDeviceKind
{
    Processor, Graphics, Storage, Network, Audio, Bluetooth, Usb,
    Input, Monitor, Battery, Sensor, Camera, Printer, Firmware,
    Security, System, Other
}

internal enum HardwareDiscoveryState { Complete, Partial, Unavailable }

internal sealed record HardwareDeviceDescriptor(
    string InstanceId,
    string Name,
    string Manufacturer,
    string SetupClass,
    string Bus,
    HardwareDeviceKind Kind)
{
    internal string? InstalledDriverVersion { get; init; }
}

internal sealed record HardwareDiscoverySnapshot(
    DateTimeOffset CapturedAtUtc,
    HardwareDiscoveryState State,
    IReadOnlyList<HardwareDeviceDescriptor> Devices,
    string Source,
    string? Limitation)
{
    internal int DeviceCount => Devices.Count;
}

// A classe PnP identifica a FUNÇÃO no Windows, não a disponibilidade de sensores.
// Não supõe marca, frequência, temperatura, potência nem integridade do driver.
internal static class HardwareDeviceClassifier
{
    internal static HardwareDeviceKind Classify(string? setupClass)
    {
        return setupClass?.Trim().ToUpperInvariant() switch
        {
            "PROCESSOR" => HardwareDeviceKind.Processor,
            "DISPLAY" => HardwareDeviceKind.Graphics,
            "DISKDRIVE" or "HDC" or "SCSIADAPTER" or "STORAGEVOLUMES" or
                "VOLUME" or "VOLUMESNAPSHOT" => HardwareDeviceKind.Storage,
            "NET" or "NETCLIENT" or "NETSERVICE" => HardwareDeviceKind.Network,
            "MEDIA" or "AUDIOENDPOINT" or "SOUND" => HardwareDeviceKind.Audio,
            "BLUETOOTH" => HardwareDeviceKind.Bluetooth,
            "USB" or "USBDEVICE" or "USBFUNCTION" => HardwareDeviceKind.Usb,
            "HIDCLASS" or "KEYBOARD" or "MOUSE" or "GAMECONTROLLER" => HardwareDeviceKind.Input,
            "MONITOR" => HardwareDeviceKind.Monitor,
            "BATTERY" => HardwareDeviceKind.Battery,
            "SENSOR" or "SENSORS" => HardwareDeviceKind.Sensor,
            "IMAGE" or "CAMERA" => HardwareDeviceKind.Camera,
            "PRINTER" or "PRINTQUEUE" => HardwareDeviceKind.Printer,
            "FIRMWARE" => HardwareDeviceKind.Firmware,
            "SECURITYDEVICES" or "SMARTCARDREADER" or "BIOMETRIC" => HardwareDeviceKind.Security,
            "SYSTEM" or "COMPUTER" => HardwareDeviceKind.System,
            _ => HardwareDeviceKind.Other
        };
    }

    internal static string BusOf(string? deviceInstanceId)
    {
        if (string.IsNullOrWhiteSpace(deviceInstanceId)) return "Não informado";
        int separator = deviceInstanceId.IndexOf('\\');
        if (separator < 1) return "Não informado";
        // Não transforma o barramento em modelo ou classe: ex. USB pode ser áudio.
        string enumerator = deviceInstanceId[..separator].ToUpperInvariant();
        return enumerator.Length > 24 ? "Outro" : enumerator;
    }
}

internal static class NativeHardwareDiscovery
{
    private const int ErrorNoMoreItems = 259;
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfAllClasses = 0x00000004;
    private const uint SpdrpDevicedesc = 0x00000000;
    private const uint SpdrpClass = 0x00000007;
    private const uint SpdrpMfg = 0x0000000B;
    private const uint SpdrpFriendlyName = 0x0000000C;
    private const int MaxPropertyBytes = 32_768;

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfoData
    {
        public uint Size;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetClassDevsW",
        CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetDeviceSet(
        IntPtr classGuid, string? enumerator, IntPtr parent, uint flags);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiEnumDeviceInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumerateDevice(
        IntPtr set, uint index, ref DeviceInfoData data);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInstanceIdW",
        CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetInstanceId(
        IntPtr set, ref DeviceInfoData data, StringBuilder value,
        uint capacity, out uint required);

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceRegistryPropertyW",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadDeviceProperty(
        IntPtr set, ref DeviceInfoData data, uint property,
        out uint registryType, [Out] byte[] buffer, uint bufferSize, out uint required);

    [StructLayout(LayoutKind.Sequential)]
    private struct DevicePropertyKey { public Guid Format; public uint Id; }

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDevicePropertyW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadDevicePropertyKey(IntPtr set, ref DeviceInfoData data,
        ref DevicePropertyKey key, out uint propertyType, [Out] byte[] buffer,
        uint bufferSize, out uint required, uint flags);

    // DEVPKEY_Device_DriverVersion, devpkey.h, DEVPROP_TYPE_STRING.
    // É a versão do pacote instalado nesta instância, não a versão global do fabricante.
    private static string? ReadInstalledDriver(IntPtr set, ref DeviceInfoData data)
    {
        var key = new DevicePropertyKey { Format = new Guid("a8b865dd-2e3d-4094-ad97-e593a70c75d6"), Id = 3 };
        byte[] buffer = new byte[256];
        if (!ReadDevicePropertyKey(set, ref data, ref key, out uint type, buffer,
            (uint)buffer.Length, out uint needed, 0) ||
            type != 0x12 || needed < 2 || needed > buffer.Length || (needed & 1) != 0)
            return null;
        return ValidateInstalledDriver(Encoding.Unicode.GetString(buffer, 0, (int)needed).TrimEnd('\0'));
    }

    internal static string? ValidateInstalledDriver(string? version)
    {
        if (string.IsNullOrEmpty(version) || version.Length > 64) return null;
        var parts = version.Split('.');
        return parts.Length == 4 && parts.All(x => x.Length > 0 && x.All(char.IsAsciiDigit) &&
            ushort.TryParse(x, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out _)) ? version : null;
    }

    [DllImport("setupapi.dll", EntryPoint = "SetupDiDestroyDeviceInfoList", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyDeviceSet(IntPtr set);

    // Não usar no timer de 2 segundos: descoberta de dispositivos é separada
    // das amostras de CPU/RAM e deve ser acionada apenas quando necessário.
    internal static HardwareDiscoverySnapshot Capture(int maxDevices = 4096)
    {
        if (!OperatingSystem.IsWindows())
            return new(DateTimeOffset.UtcNow, HardwareDiscoveryState.Unavailable,
                Array.Empty<HardwareDeviceDescriptor>(), "Windows SetupAPI",
                "Inventário PnP disponível somente no Windows");

        int limit = Math.Clamp(maxDevices, 1, 4096);
        var result = new List<HardwareDeviceDescriptor>();
        IntPtr set = IntPtr.Zero;
        try
        {
            set = GetDeviceSet(IntPtr.Zero, null, IntPtr.Zero, DigcfPresent | DigcfAllClasses);
            if (set == IntPtr.Zero || set == new IntPtr(-1))
                return Failure("SetupDiGetClassDevs falhou");

            bool partial = false;
            for (uint index = 0; index <= limit; ++index)
            {
                var data = new DeviceInfoData { Size = (uint)Marshal.SizeOf<DeviceInfoData>() };
                if (!EnumerateDevice(set, index, ref data))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != ErrorNoMoreItems) partial = true;
                    break;
                }
                if (index == limit)
                {
                    partial = true; // não confundir o teto com inventário completo
                    break;
                }

                string instance = ReadInstanceId(set, ref data);
                if (instance.Length == 0) partial = true; // identidade sem evidência
                string name = ReadProperty(set, ref data, SpdrpFriendlyName);
                if (name.Length == 0) name = ReadProperty(set, ref data, SpdrpDevicedesc);
                if (name.Length == 0) name = "Dispositivo sem nome informado";
                string manufacturer = ReadProperty(set, ref data, SpdrpMfg);
                string setupClass = ReadProperty(set, ref data, SpdrpClass);
                var kind = HardwareDeviceClassifier.Classify(setupClass);
                result.Add(new(instance, name, manufacturer, setupClass,
                    HardwareDeviceClassifier.BusOf(instance), kind)
                {
                    InstalledDriverVersion = kind == HardwareDeviceKind.Graphics && instance.Length > 0
                        ? ReadInstalledDriver(set, ref data) : null
                });
            }

            return new(DateTimeOffset.UtcNow,
                partial ? HardwareDiscoveryState.Partial : HardwareDiscoveryState.Complete,
                result.AsReadOnly(), "Windows SetupAPI (dispositivos presentes)",
                partial ? "Inventário PnP parcial: limite ou falha de enumeração" : null);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or
            System.Security.SecurityException or ArgumentException)
        {
            return new(DateTimeOffset.UtcNow, HardwareDiscoveryState.Unavailable,
                result.AsReadOnly(), "Windows SetupAPI", "Consulta nativa indisponível");
        }
        finally
        {
            if (set != IntPtr.Zero && set != new IntPtr(-1)) DestroyDeviceSet(set);
        }
    }

    private static HardwareDiscoverySnapshot Failure(string reason) =>
        new(DateTimeOffset.UtcNow, HardwareDiscoveryState.Unavailable,
            Array.Empty<HardwareDeviceDescriptor>(), "Windows SetupAPI", reason);

    private static string ReadInstanceId(IntPtr set, ref DeviceInfoData data)
    {
        var builder = new StringBuilder(512);
        if (GetInstanceId(set, ref data, builder, (uint)builder.Capacity, out uint needed))
            return builder.ToString();
        if (needed <= (uint)builder.Capacity || needed > MaxPropertyBytes / 2)
            return string.Empty;
        builder = new StringBuilder((int)needed);
        return GetInstanceId(set, ref data, builder, (uint)builder.Capacity, out _)
            ? builder.ToString() : string.Empty;
    }

    private static string ReadProperty(IntPtr set, ref DeviceInfoData data, uint property)
    {
        var buffer = new byte[512];
        if (!ReadDeviceProperty(set, ref data, property, out uint type,
            buffer, (uint)buffer.Length, out uint needed))
        {
            if (needed <= buffer.Length || needed > MaxPropertyBytes) return string.Empty;
            buffer = new byte[(int)needed];
            if (!ReadDeviceProperty(set, ref data, property, out type,
                buffer, (uint)buffer.Length, out needed))
                return string.Empty;
        }
        // REG_SZ=1, REG_EXPAND_SZ=2, REG_MULTI_SZ=7; outros tipos não são nomes.
        if (type is not (1 or 2 or 7) || needed < 2 || needed > buffer.Length)
            return string.Empty;
        return Encoding.Unicode.GetString(buffer, 0, (int)needed)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
    }
}
