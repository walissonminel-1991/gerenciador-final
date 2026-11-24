using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Uma identidade DXGI corresponde ao LUID do ADAPTADOR da sessão Windows.
// O índice phys do WDDM é mantido para não misturar GPUs vinculadas (LDA).
internal sealed record DxgiGpuIdentity(
    uint LuidHigh, uint LuidLow, string Name, uint VendorId, uint DeviceId,
    uint SubSystemId, ulong DedicatedVideoMemory, bool IsSoftware)
{
    // Capacidade reportada por D3D12 em dispositivo discreto com nó único;
    // não representa diretamente o estado de Resizable BAR do fabricante.
    internal bool? DiscreteGpuUploadHeapSupported { get; init; }
}

internal sealed record DxgiGpuSnapshot(
    DateTimeOffset CapturedAtUtc, HardwareDiscoveryState State,
    IReadOnlyList<DxgiGpuIdentity> Adapters, string? Limitation);

internal sealed record ResolvedWddmGpu(
    WddmGpuAdapterSample Counters,
    string? DxgiDescription,
    string? PnpInstanceId,
    string IdentitySource);

internal static class DxgiGpuIdentityProvider
{
    private static readonly Guid Factory1Id =
        new("770aae78-f26f-4dba-a829-253c83d1b387");
    private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
    private const uint SoftwareFlag = 2;
    private const int MaxAdapters = 32;

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Name;
        public uint VendorId, DeviceId, SubSysId, Revision;
        public UIntPtr DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public Luid AdapterLuid;
        public uint Flags;
    }

    [DllImport("dxgi.dll", EntryPoint = "CreateDXGIFactory1", ExactSpelling = true)]
    private static extern int CreateFactory(ref Guid factoryId, out IntPtr factory);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters(IntPtr factory, uint index, out IntPtr adapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReadDescription(IntPtr adapter, out AdapterDescription result);

    private static T Method<T>(IntPtr com, int vtableSlot) where T : Delegate
    {
        IntPtr table = Marshal.ReadIntPtr(com);
        IntPtr address = Marshal.ReadIntPtr(table, checked(vtableSlot * IntPtr.Size));
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    // Slots DXGI 1.1 de COM IUnknown + IDXGIObject + IDXGIFactory +
    // IDXGIFactory1: EnumAdapters1 slot 12; IDXGIAdapter1::GetDesc1 slot 10.
    // Não usar interfaces após Release. Nunca abrir driver/kernel.
    internal static DxgiGpuSnapshot Capture() => Capture(false);

    internal static DxgiGpuSnapshot Capture(bool probeCpuVisibleVram)
    {
        if (!OperatingSystem.IsWindows())
            return Unavailable("DXGI disponível somente no Windows");

        IntPtr factory = IntPtr.Zero;
        try
        {
            Guid iid = Factory1Id;
            if (CreateFactory(ref iid, out factory) < 0 || factory == IntPtr.Zero)
                return Unavailable("CreateDXGIFactory1 indisponível");
            var enumerate = Method<EnumAdapters>(factory, 12);
            var devices = new List<DxgiGpuIdentity>();
            bool partial = false;
            for (uint i = 0; i <= MaxAdapters; i++)
            {
                IntPtr adapter = IntPtr.Zero;
                try
                {
                    int status = enumerate(factory, i, out adapter);
                    if (status == DxgiErrorNotFound) break;
                    if (status < 0 || adapter == IntPtr.Zero)
                    { partial = true; break; }
                    if (i == MaxAdapters) { partial = true; break; }
                    var desc = Method<ReadDescription>(adapter, 10);
                    if (desc(adapter, out var value) < 0)
                    { partial = true; continue; }
                    string name = (value.Name ?? "").Trim();
                    if (name.Length == 0 || name.Any(char.IsControl))
                        name = "Adaptador gráfico DXGI sem nome";
                    devices.Add(new(unchecked((uint)value.AdapterLuid.HighPart),
                        value.AdapterLuid.LowPart, name,
                        value.VendorId, value.DeviceId, value.SubSysId,
                        value.DedicatedVideoMemory.ToUInt64(),
                        (value.Flags & SoftwareFlag) != 0)
                    {
                        DiscreteGpuUploadHeapSupported = probeCpuVisibleVram &&
                            (value.Flags & SoftwareFlag) == 0 && value.DedicatedVideoMemory.ToUInt64() > 0
                            ? ProbeDiscreteUploadHeap(adapter) : null
                    });
                }
                finally
                {
                    if (adapter != IntPtr.Zero) Marshal.Release(adapter);
                }
            }
            return new(DateTimeOffset.UtcNow,
                partial ? HardwareDiscoveryState.Partial : HardwareDiscoveryState.Complete,
                devices.AsReadOnly(), partial ? "DXGI parcialmente enumerado" : null);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
            or COMException or BadImageFormatException or SEHException or ArgumentException)
        { return Unavailable("Consulta DXGI indisponível"); }
        finally { if (factory != IntPtr.Zero) Marshal.Release(factory); }
    }

    // Windows d3d12.h: ID3D12Device (7=GetNodeCount, 13=CheckFeatureSupport),
    // ARCHITECTURE1=16 e OPTIONS16=44. Não criar fila, heap, shader ou workload.
    [DllImport("d3d12.dll", EntryPoint = "D3D12CreateDevice", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int CreateD3d12Device(IntPtr adapter, uint featureLevel,
        ref Guid iid, out IntPtr device);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReadNodeCount(IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CheckArchitecture(IntPtr device, uint feature, ref Architecture1 data, uint bytes);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CheckOptions16(IntPtr device, uint feature, ref Options16 data, uint bytes);

    [StructLayout(LayoutKind.Sequential)]
    private struct Architecture1 { public uint Node; public int TileBased, Uma, CoherentUma, IsolatedMmu; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Options16 { public int DynamicDepthBias, GpuUploadHeap; }

    internal static bool? InterpretDiscreteUploadHeap(uint nodes, int architectureStatus, int uma,
        int optionsStatus, int upload) =>
        nodes == 1 && architectureStatus == 0 && uma == 0 && optionsStatus == 0 && upload is 0 or 1
            ? upload == 1 : null;

    private static bool? ProbeDiscreteUploadHeap(IntPtr adapter)
    {
        if (adapter == IntPtr.Zero) return null;
        IntPtr device = IntPtr.Zero;
        try
        {
            Guid iid = new("189819f1-1db6-4b57-be54-1821339b85f7");
            if (CreateD3d12Device(adapter, 0xb000, ref iid, out device) < 0 || device == IntPtr.Zero)
                return null;
            uint nodes = Method<ReadNodeCount>(device, 7)(device);
            if (nodes != 1) return null;
            var architecture = new Architecture1 { Node = 0 };
            int archStatus = Method<CheckArchitecture>(device, 13)(device, 16, ref architecture,
                (uint)Marshal.SizeOf<Architecture1>());
            if (archStatus != 0 || architecture.Uma != 0) return null;
            var options = new Options16();
            int optionsStatus = Method<CheckOptions16>(device, 13)(device, 44, ref options,
                (uint)Marshal.SizeOf<Options16>());
            return InterpretDiscreteUploadHeap(nodes, archStatus, architecture.Uma, optionsStatus, options.GpuUploadHeap);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or
            COMException or BadImageFormatException or SEHException or ArgumentException)
        { return null; }
        finally { if (device != IntPtr.Zero) Marshal.Release(device); }
    }

    // O timer não cria dispositivos D3D12. Conservar somente capacidade
    // consultada anteriormente da mesma identidade DXGI inequívoca.
    internal static DxgiGpuSnapshot RetainUploadHeapCapability(DxgiGpuSnapshot current, DxgiGpuSnapshot? previous)
    {
        if (current.State != HardwareDiscoveryState.Complete ||
            previous?.State != HardwareDiscoveryState.Complete) return current;
        var adapters = current.Adapters.Select(adapter =>
        {
            if (adapter.IsSoftware || current.Adapters.Count(a =>
                a.LuidHigh == adapter.LuidHigh && a.LuidLow == adapter.LuidLow) != 1) return adapter;
            var matches = previous.Adapters.Where(old =>
                !old.IsSoftware && old.LuidHigh == adapter.LuidHigh && old.LuidLow == adapter.LuidLow &&
                old.VendorId == adapter.VendorId && old.DeviceId == adapter.DeviceId &&
                old.SubSystemId == adapter.SubSystemId && old.DedicatedVideoMemory == adapter.DedicatedVideoMemory)
                .Take(2).ToArray();
            return matches.Length == 1 ? adapter with {
                DiscreteGpuUploadHeapSupported = matches[0].DiscreteGpuUploadHeapSupported
            } : adapter;
        }).ToArray();
        return current with { Adapters = adapters };
    }

    private static readonly Regex PciPnpId = new(
        @"^PCI\\VEN_([A-Fa-f0-9]{4})&DEV_([A-Fa-f0-9]{4})&SUBSYS_([A-Fa-f0-9]{8})(?:&|\\|$)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    internal static string? MatchUniquePnp(
        DxgiGpuIdentity adapter, IReadOnlyList<HardwareDeviceDescriptor> inventory)
    {
        if (adapter.IsSoftware || adapter.VendorId is 0 or > 0xFFFF ||
            adapter.DeviceId is 0 or > 0xFFFF || adapter.SubSystemId == 0)
            return null;
        var matches = inventory.Where(d => d.Kind == HardwareDeviceKind.Graphics &&
            PciPnpId.Match(d.InstanceId).Success &&
            MatchesId(d.InstanceId, adapter)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0].InstanceId : null;
    }

    private static bool MatchesId(string instance, DxgiGpuIdentity adapter)
    {
        var match = PciPnpId.Match(instance);
        return match.Success &&
            uint.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var ven) &&
            uint.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var dev) &&
            uint.TryParse(match.Groups[3].Value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var subsys) &&
            ven == adapter.VendorId && dev == adapter.DeviceId &&
            subsys == adapter.SubSystemId;
    }

    internal static IReadOnlyList<ResolvedWddmGpu> Correlate(
        WddmGpuSnapshot counters, DxgiGpuSnapshot dxgi,
        HardwareDiscoverySnapshot inventory)
    {
        var result = new List<ResolvedWddmGpu>();
        // Um LUID pode ter vários índices "phys" (adaptadores vinculados).
        // Nesse caso um único GetDesc1 não prova a identidade de CADA
        // instância física do contador; preservar ambas sem associação.
        var ambiguousLuids = counters.Adapters
            .GroupBy(x => (x.Key.LuidHigh, x.Key.LuidLow))
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .ToHashSet();
        foreach (var item in counters.Adapters)
        {
            if (ambiguousLuids.Contains((item.Key.LuidHigh, item.Key.LuidLow)))
            {
                result.Add(new(item, null, null,
                    "LUID WDDM compartilhado por múltiplos índices físicos"));
                continue;
            }
            var matches = dxgi.Adapters.Where(x =>
                x.LuidHigh == item.Key.LuidHigh &&
                x.LuidLow == item.Key.LuidLow).Take(2).ToArray();
            if (matches.Length == 1)
            {
                var adapter = matches[0];
                result.Add(new(item, adapter.Name,
                    inventory.State == HardwareDiscoveryState.Complete
                        ? MatchUniquePnp(adapter, inventory.Devices) : null,
                    "Windows WDDM LUID + DXGI GetDesc1"));
            }
            else result.Add(new(item, null, null, "LUID sem correspondência DXGI inequívoca"));
        }
        return result.AsReadOnly();
    }

    private static DxgiGpuSnapshot Unavailable(string reason) =>
        new(DateTimeOffset.UtcNow, HardwareDiscoveryState.Unavailable, [], reason);
}
