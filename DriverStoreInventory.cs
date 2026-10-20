using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal sealed record DriverStoreEntry(string PublishedName, string Provider, string Version,
    string ClassName, bool InBox, bool BootCritical, int Signature);
internal sealed record DriverStoreResult(IReadOnlyList<DriverStoreEntry> Items, bool Partial, string Status);
internal sealed record DriverStoreLink(DriverStoreEntry Package, int? ObservedDevices, bool EvidencePartial, int VersionConflicts = 0)
{
    internal string LinkLabel => ObservedDevices is null ? "Não consultado" : ObservedDevices == 0
        ? "Sem vínculo observado" : $"{ObservedDevices} dispositivo(s)" + (VersionConflicts > 0 ? $"; {VersionConflicts} versão divergente" : "");
}

internal static class DriverStoreInventory
{
    private static readonly object Gate = new();
    internal static IReadOnlyList<DriverStoreLink> Correlate(DriverStoreResult packages, DriverInventoryResult? devices)
    {
        var links = devices?.Items.Where(d => !string.IsNullOrWhiteSpace(d.InfName))
            .GroupBy(d => d.InfName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        bool DifferentVersion(string a, string b)
        {
            if (!Version.TryParse(a, out var x) || !Version.TryParse(b, out var y)) return false;
            return x.Major != y.Major || x.Minor != y.Minor || Math.Max(0, x.Build) != Math.Max(0, y.Build) ||
                Math.Max(0, x.Revision) != Math.Max(0, y.Revision);
        }
        return packages.Items.Select(p =>
        {
            var observed = links?.GetValueOrDefault(p.PublishedName) ?? [];
            return new DriverStoreLink(p, links is null ? null : observed.Select(d => d.DeviceId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                packages.Partial || devices is null || devices.Partial,
                observed.Where(d => DifferentVersion(d.Version, p.Version)).Select(d => d.DeviceId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }).ToArray();
    }

    // ABI documentada em DismAPI.h. BOOL e enum sao inteiros de 32 bits.
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePackage
    {
        public IntPtr PublishedName, OriginalFileName;
        public int InBox;
        public IntPtr CatalogFile, ClassName, ClassGuid, ClassDescription;
        public int BootCritical, DriverSignature;
        public IntPtr ProviderName;
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
        public uint Major, Minor, Build, Revision;
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    private delegate int Initialize(int level, string? log, string? scratch);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    private delegate int Open(string image, string? windows, string? drive, out uint session);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetDrivers(uint session, int all, out IntPtr packages, out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int Close(uint session);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int Delete(IntPtr data);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int Shutdown();

    internal static DriverStoreResult Capture(CancellationToken token)
    {
        lock (Gate)
        {
            var entries = new List<DriverStoreEntry>();
            IntPtr library = IntPtr.Zero, data = IntPtr.Zero;
            uint session = 0;
            bool initialized = false, opened = false;
            Close? close = null; Delete? delete = null; Shutdown? shutdown = null;
            try
            {
                token.ThrowIfCancellationRequested();
                // Apenas biblioteca do Windows por caminho absoluto; nunca procurar no diretorio do aplicativo.
                string path = Path.Combine(Environment.SystemDirectory, "Dism", "DismApi.dll");
                if (!File.Exists(path)) path = Path.Combine(Environment.SystemDirectory, "DismApi.dll");
                library = NativeLibrary.Load(path, typeof(DriverStoreInventory).Assembly,
                    DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.System32);
                T Function<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
                var initialize = Function<Initialize>("DismInitialize");
                var open = Function<Open>("DismOpenSession");
                var get = Function<GetDrivers>("DismGetDrivers");
                close = Function<Close>("DismCloseSession"); delete = Function<Delete>("DismDelete"); shutdown = Function<Shutdown>("DismShutdown");
                void Check(int hr) { if (hr != 0) Marshal.ThrowExceptionForHR(hr < 0 ? hr : unchecked((int)0x80004005)); }
                Check(initialize(0, null, null)); initialized = true;
                token.ThrowIfCancellationRequested();
                Check(open("DISM_{53BFAE52-B167-4E2F-A258-0A37B57FF845}", null, null, out session)); opened = true;
                Check(get(session, 1, out data, out uint count));
                if (count > 12000 || (count > 0 && data == IntPtr.Zero)) throw new InvalidDataException();
                int size = Marshal.SizeOf<NativePackage>();
                for (int i = 0; i < count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var p = Marshal.PtrToStructure<NativePackage>(IntPtr.Add(data, checked(i * size)));
                    string Text(IntPtr value) => Marshal.PtrToStringUni(value) ?? "";
                    entries.Add(new(Text(p.PublishedName), Text(p.ProviderName), $"{p.Major}.{p.Minor}.{p.Build}.{p.Revision}",
                        Text(p.ClassName), p.InBox != 0, p.BootCritical != 0, p.DriverSignature));
                }
                return new(entries.OrderBy(x => x.PublishedName, StringComparer.OrdinalIgnoreCase).ToArray(), false, "Pacotes consultados pelo Windows");
            }
            catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException
                or BadImageFormatException or InvalidDataException or OperationCanceledException or IOException)
            {
                string status = ex.HResult is unchecked((int)0x800702E4) or unchecked((int)0x80070005)
                    ? "Consulta de pacotes requer execução como administrador; nenhuma elevação automática."
                    : $"Consulta de pacotes parcial ou indisponível: {ex.GetType().Name}; 0x{ex.HResult:X8}";
                return new(entries, true, status);
            }
            finally
            {
                if (data != IntPtr.Zero) delete?.Invoke(data);
                if (opened) close?.Invoke(session);
                if (initialized) shutdown?.Invoke();
                if (library != IntPtr.Zero) NativeLibrary.Free(library);
            }
        }
    }
}
