using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// VF02 fase 5. Leitura somente local do NVAPI instalado no Windows.
// Fonte: https://docs.nvidia.com/nvapi/group__gpu.html
// Header oficial NVIDIA/nvapi/nvapi_interface.h. Nenhum DLL/SDK distribuído pelo GF.
// A API é global ao driver, não vinculada a um PCI DeviceId; por isso exige
// exatamente UMA GPU NVIDIA válida e equivalência da versão NVAPI com a WMI.
internal static class NvidiaDriverChannel
{
    private const uint InitializeId = 0x0150E828;
    private const uint UnloadId = 0xD22BDD7E;
    private const uint GetDisplayDriverInfoId = 0x721FACEB;
    private const uint KnownTypeMask = 0x1Eu; // bits 1..4: Studio, Game Ready, RTX Production, RTX New
    private const uint AllowedFlags = 0x1Fu; // bit 0: DCH, jamais um tipo de canal

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr QueryInterface(uint interfaceId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvInitialize();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvUnload();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvGetDriverInfo(ref NativeDriverInfo data);

    // NV_DISPLAY_DRIVER_INFO v1: two NvU32 + NvAPI_ShortString[64] +
    // packed NvU32 bitfields. Tamanho 76 (0x4C), versão 0x0001004C.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct NativeDriverInfo
    {
        public uint Version;
        public uint DriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string BuildBranch;
        public uint Flags;
    }

    internal static int NativeStructureSize => Marshal.SizeOf<NativeDriverInfo>();

    // Função pura para testes sintéticos; sem acesso ao computador.
    internal static string? Classify(uint driverVersion, uint flags, string? expectedNvidiaVersion)
    {
        if (string.IsNullOrEmpty(expectedNvidiaVersion) || driverVersion == 0 ||
            flags == 0 || (flags & ~AllowedFlags) != 0) return null;
        var formattedVersion = $"{driverVersion / 100}.{driverVersion % 100:D2}";
        if (!string.Equals(formattedVersion, expectedNvidiaVersion, StringComparison.Ordinal))
            return null;
        uint type = flags & KnownTypeMask;
        return type switch
        {
            1u << 1 => "Studio",
            1u << 2 => "Game Ready",
            1u << 3 => "RTX Production Branch",
            1u << 4 => "RTX New Feature Branch",
            _ => null // múltiplos flags ou nenhum = inconclusivo
        };
    }

    internal static bool CanRead(DriverInventoryResult inventory, GraphicsDriverSnapshot? selected)
    {
        if (selected is null || inventory.Partial ||
            GraphicsDriverInventory.NvidiaPublicVersion(selected) is null ||
            inventory.Items.Any(x => string.IsNullOrWhiteSpace(x.DeviceId)) ||
            inventory.Items.Select(x => x.DeviceId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != inventory.Items.Count)
            return false;
        var graphics = GraphicsDriverInventory.Find(inventory);
        var nvidia = graphics.Where(x => x.Manufacturer == "NVIDIA").ToArray();
        return nvidia.Length == 1 &&
            string.Equals(nvidia[0].DeviceId, selected.DeviceId, StringComparison.OrdinalIgnoreCase) &&
            nvidia[0].InstalledWindowsVersion == selected.InstalledWindowsVersion &&
            nvidia[0].DriverProvider == selected.DriverProvider;
    }

    internal static string? Read(DriverInventoryResult inventory, GraphicsDriverSnapshot? selected)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess ||
            !CanRead(inventory, selected)) return null;
        string expected = GraphicsDriverInventory.NvidiaPublicVersion(selected)!;
        string dll = Path.Combine(Environment.SystemDirectory, "nvapi64.dll");
        IntPtr library = IntPtr.Zero;
        try
        {
            // Caminho ABSOLUTO no System32 evita pesquisa em CWD/PATH e DLL de terceiros.
            if (!NativeLibrary.TryLoad(dll, out library)) return null;
            if (!NativeLibrary.TryGetExport(library, "nvapi_QueryInterface", out var entry))
                return null;
            var query = Marshal.GetDelegateForFunctionPointer<QueryInterface>(entry);
            IntPtr initializeEntry = query(InitializeId);
            IntPtr unloadEntry = query(UnloadId);
            IntPtr readEntry = query(GetDisplayDriverInfoId);
            if (initializeEntry == IntPtr.Zero || unloadEntry == IntPtr.Zero ||
                readEntry == IntPtr.Zero || NativeStructureSize != 76) return null;
            var initialize = Marshal.GetDelegateForFunctionPointer<NvInitialize>(initializeEntry);
            var unload = Marshal.GetDelegateForFunctionPointer<NvUnload>(unloadEntry);
            var read = Marshal.GetDelegateForFunctionPointer<NvGetDriverInfo>(readEntry);
            if (initialize() != 0) return null;
            try
            {
                var data = new NativeDriverInfo
                {
                    Version = ((uint)1 << 16) | (uint)NativeStructureSize,
                    BuildBranch = string.Empty
                };
                if (read(ref data) != 0) return null;
                return Classify(data.DriverVersion, data.Flags, expected);
            }
            finally { _ = unload(); }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
            or BadImageFormatException or SEHException or ArgumentException
            or InvalidOperationException or MarshalDirectiveException)
        {
            return null;
        }
        finally
        {
            if (library != IntPtr.Zero) NativeLibrary.Free(library);
        }
    }
}
