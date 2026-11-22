using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GerenciadorAplicativos;

// Informações somente de apresentação. O ID do pai nunca é incorporado ao
// HardwareIds do filho nem enviado como identidade a WUA ou ao Driver Store.
internal sealed record DriverDeviceMetadata(string BusDescription, string UsbParentHardwareId,
    string FriendlyName = "", string DeviceDescription = "")
{
    private const int StringPropertyType = 0x12;
    private static readonly PropertyKey BusDescriptionKey =
        new(new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2"), 4);
    // Propriedades oficiais do MESMO devnode (leitura de exibição apenas).
    // DEVPKEY_Device_FriendlyName PID 14 e DeviceDesc PID 2.
    private static readonly PropertyKey FriendlyNameKey =
        new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    private static readonly PropertyKey DeviceDescriptionKey =
        new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);
    private static readonly PropertyKey ParentKey =
        new(new Guid("4340a6c5-93fa-4706-972c-7b648008a5a7"), 8);

    internal static DriverInventoryResult Enrich(DriverInventoryResult inventory, CancellationToken token,
        Func<string, DriverDeviceMetadata?>? read = null)
    {
        if (read is null && !OperatingSystem.IsWindows()) return inventory;
        read ??= Read;
        var clock = Stopwatch.StartNew();
        var uniqueDevices = inventory.Items.GroupBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1).Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return inventory with
        {
            Items = inventory.Items.Select(item => item.Present == true &&
                (DriverHardwareCatalog.IsGenericName(item.DeviceName) || IsUsbPrinter(item.DeviceId)) &&
                !token.IsCancellationRequested && clock.Elapsed < TimeSpan.FromSeconds(2) &&
                uniqueDevices.Contains(item.DeviceId)
                    ? item with { DeviceMetadata = read(item.DeviceId) }
                    : item).ToArray()
        };
    }

    internal static bool IsUsbPrinter(string deviceId) =>
        deviceId.StartsWith(@"USBPRINT\", StringComparison.OrdinalIgnoreCase);

    internal static string HardwarePart(string deviceId)
    {
        int prefix = deviceId.IndexOf('\\');
        int separator = prefix < 0 ? -1 : deviceId.IndexOf('\\', prefix + 1);
        return separator < 0 ? deviceId : deviceId[..separator];
    }

    private static DriverDeviceMetadata? Read(string deviceId)
    {
        if (deviceId.Length > 200 || !DriverInventory.ValidHardwareId(deviceId)) return null;
        try
        {
            // CM_LOCATE_DEVNODE_NORMAL restringe a leitura a uma instância presente.
            if (CM_Locate_DevNodeW(out uint node, deviceId, 0) != 0) return null;
            string description = ReadString(node, BusDescriptionKey);
            string friendly = ReadString(node, FriendlyNameKey);
            string deviceDescription = ReadString(node, DeviceDescriptionKey);
            string parent = IsUsbPrinter(deviceId) ? ReadString(node, ParentKey) : "";
            string usbParent = parent.StartsWith(@"USB\VID_", StringComparison.OrdinalIgnoreCase)
                ? HardwarePart(parent) : "";
            return description.Length == 0 && usbParent.Length == 0 &&
                friendly.Length == 0 && deviceDescription.Length == 0 ? null :
                new(description, usbParent, friendly, deviceDescription);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    private static string ReadString(uint node, PropertyKey key)
    {
        var buffer = new byte[1024];
        uint bytes = (uint)buffer.Length;
        if (CM_Get_DevNode_PropertyW(node, in key, out uint type, buffer, ref bytes, 0) != 0 ||
            type != StringPropertyType || bytes < 2 || bytes > buffer.Length || (bytes & 1) != 0 ||
            buffer[bytes - 2] != 0 || buffer[bytes - 1] != 0)
            return "";
        string value = Encoding.Unicode.GetString(buffer, 0, (int)bytes - 2).Trim();
        return value.Any(char.IsControl) ? "" : value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PropertyKey(Guid format, uint id)
    {
        private readonly Guid _format = format;
        private readonly uint _id = id;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint CM_Locate_DevNodeW(out uint node, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint CM_Get_DevNode_PropertyW(uint node, in PropertyKey key, out uint type,
        [Out] byte[] buffer, ref uint bytes, uint flags);
}
