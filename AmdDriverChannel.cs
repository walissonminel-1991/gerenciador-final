using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// VF02 fase 6: leitura somente consultiva da AMD Device Library eXtra (ADLX).
// ABI C oficial: https://github.com/GPUOpen-LibrariesAndSDKs/ADLX
// ISystem.h, ISystem2.h e ADLX.h: cada índice de vtable abaixo corresponde
// a uma função documentada. Não inclui DLL de terceiros nem inicia atualização.
internal static class AmdDriverChannel
{
    private static readonly Regex Pci = new(
        @"\bVEN_1002&DEV_([0-9A-F]{4})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex Subsystem = new(
        @"\bSUBSYS_([0-9A-F]{8})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int QueryFullVersion(out ulong version);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Initialize(ulong version, out IntPtr system);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Terminate();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetGpus(IntPtr system, out IntPtr list);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ListSize(IntPtr list);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ListAtGpu(IntPtr list, uint index, out IntPtr gpu);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterface(IntPtr gpu,
        [MarshalAs(UnmanagedType.LPWStr)] string interfaceId, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetString(IntPtr gpu, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Release(IntPtr instance);

    // O ADLX retorna a EDIÇÃO instalada; ela não comprova um pacote remoto.
    internal static string? Classify(string? edition, string? adlxWindowsVersion,
        string? wmiWindowsVersion)
    {
        if (string.IsNullOrWhiteSpace(edition) || string.IsNullOrWhiteSpace(adlxWindowsVersion) ||
            !string.Equals(adlxWindowsVersion, wmiWindowsVersion, StringComparison.Ordinal))
            return null;
        return edition.Trim() switch
        {
            "AMD Software: Adrenalin Edition" or "Adrenalin Edition" or
                "Radeon Software: Adrenalin Edition" => "AMD Software: Adrenalin Edition",
            "AMD Software: PRO Edition" or "PRO Edition" => "AMD Software: PRO Edition",
            _ => null
        };
    }

    // Associação por identidade OEM completa. Somente VEN/DEV sem SUBSYS não
    // permite atribuir um estado ADLX à mesma instância WMI.
    internal static bool SamePnp(string? adlx, string? wmi)
    {
        if (string.IsNullOrWhiteSpace(adlx) || string.IsNullOrWhiteSpace(wmi)) return false;
        var a = Pci.Match(adlx);
        var b = Pci.Match(wmi);
        var sa = Subsystem.Match(adlx);
        var sb = Subsystem.Match(wmi);
        return a.Success && b.Success && sa.Success && sb.Success &&
            a.Groups[1].Value.Equals(b.Groups[1].Value, StringComparison.OrdinalIgnoreCase) &&
            sa.Groups[1].Value.Equals(sb.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool CanRead(DriverInventoryResult inventory, GraphicsDriverSnapshot? selected)
    {
        if (selected is not { Manufacturer: "AMD" } || inventory.Partial ||
            selected.InstalledWindowsVersion == "não confirmada" ||
            !Version.TryParse(selected.InstalledWindowsVersion, out _) ||
            (selected.DriverProvider is not "AMD" and not "Advanced Micro Devices, Inc." and
                not "Advanced Micro Devices")) return false;

        if (inventory.Items.Any(row => string.IsNullOrWhiteSpace(row.DeviceId)) ||
            inventory.Items.Select(row => row.DeviceId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != inventory.Items.Count)
            return false;
        var amd = GraphicsDriverInventory.Find(inventory)
            .Where(gpu => gpu.Manufacturer == "AMD").ToArray();
        return amd.Length == 1 && amd[0].DeviceId == selected.DeviceId &&
            amd[0].InstalledWindowsVersion == selected.InstalledWindowsVersion &&
            amd[0].DriverProvider == selected.DriverProvider;
    }

    private static T Method<T>(IntPtr instance, int slot) where T : Delegate
    {
        if (instance == IntPtr.Zero) throw new InvalidOperationException("ADLX sem interface");
        IntPtr vtable = Marshal.ReadIntPtr(instance);
        IntPtr pointer = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("ADLX sem método");
        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }

    private static string? ReadString(IntPtr instance, int slot)
    {
        if (Method<GetString>(instance, slot)(instance, out var buffer) != 0 ||
            buffer == IntPtr.Zero) return null;
        string? value = Marshal.PtrToStringUTF8(buffer);
        return value is { Length: > 0 and <= 100 } ? value : null;
    }

    internal static string? Read(DriverInventoryResult inventory, GraphicsDriverSnapshot? selected)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess ||
            !CanRead(inventory, selected)) return null;
        IntPtr dll = IntPtr.Zero, list = IntPtr.Zero, gpu = IntPtr.Zero, gpu2 = IntPtr.Zero;
        Terminate? terminate = null;
        bool initialized = false;
        try
        {
            // Apenas DLL do driver já instalado, carregada por caminho absoluto.
            if (!NativeLibrary.TryLoad(Path.Combine(Environment.SystemDirectory,
                "amdadlx64.dll"), out dll)) return null;
            if (!NativeLibrary.TryGetExport(dll, "ADLXQueryFullVersion", out var queryPtr) ||
                !NativeLibrary.TryGetExport(dll, "ADLXInitialize", out var initPtr) ||
                !NativeLibrary.TryGetExport(dll, "ADLXTerminate", out var termPtr))
                return null;
            var query = Marshal.GetDelegateForFunctionPointer<QueryFullVersion>(queryPtr);
            var initialize = Marshal.GetDelegateForFunctionPointer<Initialize>(initPtr);
            terminate = Marshal.GetDelegateForFunctionPointer<Terminate>(termPtr);
            if (query(out ulong runtimeVersion) != 0 ||
                ((runtimeVersion >> 48) & 0xffff) is not 1 and not 2 ||
                (((runtimeVersion >> 48) & 0xffff) == 1 &&
                    ((runtimeVersion >> 32) & 0xffff) < 4))
                return null; // GPU2 edição disponível desde ADLX 1.4
            if (initialize(runtimeVersion, out var system) != 0) return null;
            initialized = true;
            if (system == IntPtr.Zero ||
                Method<GetGpus>(system, 1)(system, out list) != 0 ||
                list == IntPtr.Zero) return null;

            // Escopo restrito nesta fase: uma única GPU AMD também no ADLX.
            if (Method<ListSize>(list, 3)(list) != 1 ||
                Method<ListAtGpu>(list, 11)(list, 0, out gpu) != 0 ||
                gpu == IntPtr.Zero) return null;
            string? pnp = ReadString(gpu, 9);
            if (!SamePnp(pnp, selected!.DeviceId)) return null;
            if (Method<QueryInterface>(gpu, 2)(gpu, "IADLXGPU2", out gpu2) != 0 ||
                gpu2 == IntPtr.Zero) return null;
            string? windowsVersion = ReadString(gpu2, 33);
            string? edition = ReadString(gpu2, 30);
            return Classify(edition, windowsVersion, selected.InstalledWindowsVersion);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
            or BadImageFormatException or ArgumentException or InvalidOperationException
            or SEHException or MarshalDirectiveException or OverflowException)
        {
            return null;
        }
        finally
        {
            if (gpu2 != IntPtr.Zero) _ = Method<Release>(gpu2, 1)(gpu2);
            if (gpu != IntPtr.Zero) _ = Method<Release>(gpu, 1)(gpu);
            if (list != IntPtr.Zero) _ = Method<Release>(list, 1)(list);
            if (initialized) _ = terminate?.Invoke();
            if (dll != IntPtr.Zero) NativeLibrary.Free(dll);
        }
    }
}
