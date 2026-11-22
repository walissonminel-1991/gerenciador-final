using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GerenciadorAplicativos;

// Informações somente de apresentação. O ID do pai nunca é incorporado ao
// HardwareIds do filho nem enviado como identidade a WUA ou ao Driver Store.
internal sealed record DriverDeviceMetadata(string BusDescription, string UsbParentHardwareId)
{
    private const int StringPropertyType = 0x12;
    private static readonly PropertyKey BusDescriptionKey =
        new(new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2"), 4);
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
            string parent = IsUsbPrinter(deviceId) ? ReadString(node, ParentKey) : "";
            string usbParent = parent.StartsWith(@"USB\VID_", StringComparison.OrdinalIgnoreCase)
                ? HardwarePart(parent) : "";
            return description.Length == 0 && usbParent.Length == 0 ? null : new(description, usbParent);
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

    internal static DriverBindingEvidence? ReadBinding(string deviceId)
    {
        if (!OperatingSystem.IsWindows() || deviceId.Length > 200 ||
            !DriverInventory.ValidHardwareId(deviceId))
            return null;
        var driverPropertySet = new Guid("a8b865dd-2e3d-4094-ad97-e593a70c75d6");
        DriverBindingEvidence? Sample(uint node) => DriverBindingEvidence.FromProperties(
            ReadString(node, new(driverPropertySet, 5)), // DEVPKEY_Device_DriverInfPath
            ReadString(node, new(driverPropertySet, 3)), // DEVPKEY_Device_DriverVersion
            ReadString(node, new(driverPropertySet, 8))); // DEVPKEY_Device_MatchingDeviceId
        try
        {
            if (CM_Locate_DevNodeW(out uint node, deviceId, 0) != 0) return null;
            var snapshot = Sample(node);
            return snapshot is not null && snapshot == Sample(node) ? snapshot : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
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

internal enum DriverBindingStatus { Unavailable, Incomplete, Confirmed, Conflicting }

// Associação local do devnode. Não representa assinatura, arquivos carregados
// pelo kernel nem versão de uma oferta remota.
internal sealed record DriverBindingEvidence
{
    private DriverBindingEvidence(string infName, string version, string matchingId) =>
        (InfName, Version, MatchingId) = (infName, version, matchingId);

    internal string InfName { get; }
    internal string Version { get; }
    internal string MatchingId { get; }

    internal static DriverBindingEvidence? FromProperties(string inf, string version, string matchingId)
    {
        string validInf = DriverPackageLinks.IsValidInf(inf) ? inf : "";
        string validVersion = DriverOfficialLookup.NormalizeInstalledDriverVersion(version) ?? "";
        string validId = DriverInventory.ValidCompatibleId(matchingId) ? matchingId : "";
        return validInf.Length == 0 && validVersion.Length == 0
            ? null : new(validInf, validVersion, validId);
    }

    internal DriverBindingStatus Compare(string wmiInf, string wmiVersion)
    {
        string? normalized = DriverOfficialLookup.NormalizeInstalledDriverVersion(wmiVersion);
        bool comparableInf = InfName.Length > 0 && DriverPackageLinks.IsValidInf(wmiInf);
        bool comparableVersion = Version.Length > 0 && normalized is not null;
        if ((comparableInf && !InfName.Equals(wmiInf, StringComparison.OrdinalIgnoreCase)) ||
            (comparableVersion && Version != normalized))
            return DriverBindingStatus.Conflicting;
        return comparableInf && comparableVersion
            ? DriverBindingStatus.Confirmed : DriverBindingStatus.Incomplete;
    }

    internal static DriverInventoryResult Enrich(DriverInventoryResult inventory, CancellationToken token,
        Func<string, DriverBindingEvidence?>? read = null)
    {
        if (read is null && !OperatingSystem.IsWindows()) return inventory;
        read ??= DriverDeviceMetadata.ReadBinding;
        var duplicateIds = inventory.Items.GroupBy(item => item.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Skip(1).Any()).Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clock = Stopwatch.StartNew();
        return inventory with
        {
            Items = inventory.Items.Select(item => item.Present == true &&
                !token.IsCancellationRequested && clock.Elapsed < TimeSpan.FromSeconds(2) &&
                !duplicateIds.Contains(item.DeviceId)
                    ? item with { DeviceBinding = read(item.DeviceId) }
                    : item).ToArray()
        };
    }
}
