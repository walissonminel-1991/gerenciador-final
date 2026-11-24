using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// Inventário consultivo por componente SMBIOS/WMI e uso por instância lógica
// de CPU. Um ID WMI não é um sensor térmico de CPU, DIMM, chipset ou VRM.
internal sealed record WmiCpuSocketRaw(
    string? DeviceId, string? Name, object? NumberOfCores,
    object? NumberOfLogicalProcessors, object? CurrentClockSpeed,
    object? Architecture = null, object? L3CacheSize = null,
    object? VirtualizationFirmwareEnabled = null, string? SocketDesignation = null);
internal sealed record WmiRamModuleRaw(
    string? BankLabel, string? DeviceLocator, object? Capacity,
    object? ConfiguredClockSpeed, object? SMBIOSMemoryType,
    object? ConfiguredVoltage = null,
    object? MinVoltage = null, object? MaxVoltage = null);
internal sealed record WmiMotherboardRaw(
    string? Tag, string? Manufacturer, string? Product, string? Version);
internal sealed record WmiLogicalCpuRaw(string? Name, object? PercentProcessorTime);

internal sealed record WindowsCpuSocket(
    string DeviceId, string? ReportedName,
    uint? PhysicalCores, uint? LogicalProcessors,
    MonitorReading<double> ReportedCurrentMhz)
{
    internal string? ReportedArchitecture { get; init; }
    internal uint? ReportedL3KiB { get; init; }
    internal bool? FirmwareVirtualizationEnabled { get; init; }
    internal string? ReportedSocketDesignation { get; init; }
}
internal sealed record WindowsRamModule(
    string BankLabel, string DeviceLocator,
    MonitorReading<ulong> CapacityBytes,
    MonitorReading<double> ConfiguredMhz, uint? SmbiosMemoryType)
{
    // SMBIOS configurado, NÃO leitura elétrica instantânea nem sensor VRM.
    internal MonitorReading<double> ConfiguredMilliVolts { get; init; } =
        MonitorReading<double>.Unavailable("Windows WMI Win32_PhysicalMemory.ConfiguredVoltage");
    internal MonitorReading<double> MinMilliVolts { get; init; } =
        MonitorReading<double>.Unavailable("Windows WMI Win32_PhysicalMemory.MinVoltage");
    internal MonitorReading<double> MaxMilliVolts { get; init; } =
        MonitorReading<double>.Unavailable("Windows WMI Win32_PhysicalMemory.MaxVoltage");
}
internal sealed record WindowsMotherboard(
    string Tag, string? Manufacturer, string? Product, string? Version);
internal sealed record WindowsLogicalCpuLoad(
    string CounterInstance, MonitorReading<double> UsagePercent);

internal sealed record WindowsPlatformHardwareSnapshot(
    DateTimeOffset CapturedAtUtc,
    HardwareDiscoveryState CpuSocketsState,
    IReadOnlyList<WindowsCpuSocket> CpuSockets,
    HardwareDiscoveryState RamModulesState,
    IReadOnlyList<WindowsRamModule> RamModules,
    HardwareDiscoveryState MotherboardsState,
    IReadOnlyList<WindowsMotherboard> Motherboards,
    HardwareDiscoveryState LogicalCpuState,
    IReadOnlyList<WindowsLogicalCpuLoad> LogicalCpuInstances)
{
    internal string? ReportedBiosVersion { get; init; }
    internal int? IntelNominalBaseMhz { get; init; }
    internal int? IntelReportedMaximumMhz { get; init; }
    internal int? ReportedPopulatedMemoryChannels { get; init; }
    internal int MeasuredLogicalCpuInstances => LogicalCpuInstances.Count(x =>
        x.UsagePercent.Availability == MonitorAvailability.Available &&
        x.UsagePercent.Value.HasValue);
}

internal static class WindowsPlatformHardwareProvider
{
    private const string Source = "Windows WMI root\\cimv2";
    private const int MaxRowsPerClass = 512;
    private const int MaxRamModules = 128;
    private const int MaxCpuSockets = 64;
    private const int MaxMotherboards = 16;

    internal static ulong? UInt(object? raw)
    {
        if (raw is null or DBNull or bool or float or double or decimal) return null;
        if (raw is string s)
            return ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture,
                out ulong n) && n != ulong.MaxValue ? n : null;
        if (raw is not (byte or ushort or uint or ulong or short or int or long))
            return null;
        try
        {
            ulong n = Convert.ToUInt64(raw, CultureInfo.InvariantCulture);
            return n == ulong.MaxValue ? null : n;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        { return null; }
    }

    internal static string? Identity(string? value)
    {
        string? clean = value?.Trim();
        if (string.IsNullOrEmpty(clean) || clean.Length > 256 ||
            clean.Any(char.IsControl) ||
            clean.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("not specified", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("to be filled by o.e.m.", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("default string", StringComparison.OrdinalIgnoreCase))
            return null;
        return clean;
    }

    private static HardwareDiscoveryState Section(bool querySucceeded, bool partial, int count)
    {
        if (!querySucceeded || count == 0) return HardwareDiscoveryState.Unavailable;
        return partial ? HardwareDiscoveryState.Partial : HardwareDiscoveryState.Complete;
    }

    internal static WindowsPlatformHardwareSnapshot Join(
        IEnumerable<WmiCpuSocketRaw> sockets,
        IEnumerable<WmiRamModuleRaw> memory,
        IEnumerable<WmiMotherboardRaw> boards,
        IEnumerable<WmiLogicalCpuRaw> logical,
        bool socketsOk = true, bool memoryOk = true, bool boardsOk = true,
        bool logicalOk = true, bool sourcePartial = false,
        string? reportedBiosVersion = null)
    {
        var cpus = sockets.Take(MaxRowsPerClass + 1).ToArray();
        var rams = memory.Take(MaxRowsPerClass + 1).ToArray();
        var baseboards = boards.Take(MaxRowsPerClass + 1).ToArray();
        var loads = logical.Take(MaxRowsPerClass + 1).ToArray();

        bool cpuPartial = sourcePartial || cpus.Length > MaxCpuSockets;
        bool memoryPartial = sourcePartial || rams.Length > MaxRamModules;
        bool boardPartial = sourcePartial || baseboards.Length > MaxMotherboards;
        bool loadPartial = sourcePartial || loads.Length > MaxRowsPerClass;

        var cpuIdentities = cpus.Take(MaxCpuSockets)
            .Select(x => (Row: x, Id: Identity(x.DeviceId)))
            .ToArray();
        var cpuDuplicates = cpuIdentities.Where(x => x.Id is not null)
            .GroupBy(x => x.Id!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (cpuDuplicates.Count != 0) cpuPartial = true;
        var cpuResult = new List<WindowsCpuSocket>();
        foreach (var (raw, id) in cpuIdentities)
        {
            if (id is null || cpuDuplicates.Contains(id))
            { cpuPartial = true; continue; }
            ulong? cores = UInt(raw.NumberOfCores);
            ulong? logicalCount = UInt(raw.NumberOfLogicalProcessors);
            ulong? mhz = UInt(raw.CurrentClockSpeed);
            if (cores is null or 0 or > 4096 ||
                logicalCount is null or 0 or > 8192 ||
                mhz is null or 0 or > 20_000)
                cpuPartial = true;
            cpuResult.Add(new(id, Identity(raw.Name),
                cores is > 0 and <= 4096 ? (uint)cores.Value : null,
                logicalCount is > 0 and <= 8192 ? (uint)logicalCount.Value : null,
                mhz is >= 100 and <= 20_000
                    ? MonitorReading<double>.Available(mhz.Value, Source + " Win32_Processor.CurrentClockSpeed (reportado)")
                    : MonitorReading<double>.Unavailable(Source + " Win32_Processor.CurrentClockSpeed"))
            {
                // Win32_Processor.Architecture identifica ISA, não microarquitetura.
                ReportedArchitecture = UInt(raw.Architecture) switch
                { 0 => "x86", 1 => "MIPS", 2 => "Alpha", 3 => "PowerPC",
                  5 => "ARM", 6 => "IA64", 9 => "x64", 12 => "ARM64", _ => null },
                ReportedL3KiB = UInt(raw.L3CacheSize) is > 0 and <= 16_777_216
                    ? (uint)UInt(raw.L3CacheSize)!.Value : null,
                FirmwareVirtualizationEnabled = raw.VirtualizationFirmwareEnabled is bool enabled
                    ? enabled : null,
                ReportedSocketDesignation = Identity(raw.SocketDesignation)
            });
        }

        var ramIdentities = rams.Take(MaxRamModules)
            .Select(x => (Row: x, Bank: Identity(x.BankLabel),
                Slot: Identity(x.DeviceLocator))).ToArray();
        var duplicateSlots = ramIdentities
            .Where(x => x.Bank is not null || x.Slot is not null)
            .GroupBy(x => (x.Bank ?? "") + "\u001f" + (x.Slot ?? ""),
                StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (duplicateSlots.Count != 0) memoryPartial = true;
        var ramResult = new List<WindowsRamModule>();
        foreach (var (raw, bank, slot) in ramIdentities)
        {
            if ((bank is null && slot is null) ||
                duplicateSlots.Contains((bank ?? "") + "\u001f" + (slot ?? "")))
            { memoryPartial = true; continue; }
            ulong? capacity = UInt(raw.Capacity);
            ulong? mhz = UInt(raw.ConfiguredClockSpeed);
            ulong? memoryType = UInt(raw.SMBIOSMemoryType);
            ulong? millivolts = UInt(raw.ConfiguredVoltage);
            ulong? minimumMillivolts = UInt(raw.MinVoltage);
            ulong? maximumMillivolts = UInt(raw.MaxVoltage);
            // ConfiguredVoltage = 0 ou null significa "não informado"
            // segundo Win32_PhysicalMemory; não tornar o módulo inválido.
            // Valor presente mas impossível só afeta a confiabilidade da fonte.
            bool voltageProvided = raw.ConfiguredVoltage is not null &&
                raw.ConfiguredVoltage is not DBNull;
            bool voltageValid = millivolts is >= 500 and <= 5_000;
            bool minimumProvided = raw.MinVoltage is not null and not DBNull;
            bool maximumProvided = raw.MaxVoltage is not null and not DBNull;
            bool minimumValid = minimumMillivolts is >= 500 and <= 5_000;
            bool maximumValid = maximumMillivolts is >= 500 and <= 5_000;
            // Um intervalo mínimo > máximo é contraditório. Não se presume
            // que a tensão configurada precise ficar dentro dele (ex.: XMP).
            bool contradictoryBounds = minimumValid && maximumValid &&
                minimumMillivolts!.Value > maximumMillivolts!.Value;
            if (capacity is null or < 16_777_216 or > 4_398_046_511_104 ||
                mhz is null or < 100 or > 20_000 ||
                (voltageProvided && millivolts != 0 && !voltageValid) ||
                (minimumProvided && minimumMillivolts != 0 && !minimumValid) ||
                (maximumProvided && maximumMillivolts != 0 && !maximumValid) ||
                contradictoryBounds)
                memoryPartial = true;
            ramResult.Add(new(bank ?? "", slot ?? "",
                capacity is >= 16_777_216 and <= 4_398_046_511_104
                    ? MonitorReading<ulong>.Available(capacity.Value, Source + " Win32_PhysicalMemory.Capacity")
                    : MonitorReading<ulong>.Unavailable(Source + " Win32_PhysicalMemory.Capacity"),
                mhz is >= 100 and <= 20_000
                    ? MonitorReading<double>.Available(mhz.Value, Source + " Win32_PhysicalMemory.ConfiguredClockSpeed")
                    : MonitorReading<double>.Unavailable(Source + " Win32_PhysicalMemory.ConfiguredClockSpeed"),
                memoryType is > 0 and <= ushort.MaxValue
                    ? (uint)memoryType.Value : null)
            {
                ConfiguredMilliVolts = voltageValid
                    ? MonitorReading<double>.Available(millivolts!.Value,
                        Source + " Win32_PhysicalMemory.ConfiguredVoltage (mV SMBIOS reportados, não medidos)")
                    : MonitorReading<double>.Unavailable(
                        Source + " Win32_PhysicalMemory.ConfiguredVoltage (0/nulo/fora da faixa)"),
                MinMilliVolts = minimumValid && !contradictoryBounds
                    ? MonitorReading<double>.Available(minimumMillivolts!.Value,
                        Source + " Win32_PhysicalMemory.MinVoltage (mV SMBIOS, não medidos)")
                    : MonitorReading<double>.Unavailable(
                        Source + " Win32_PhysicalMemory.MinVoltage (0/nulo/inválido)"),
                MaxMilliVolts = maximumValid && !contradictoryBounds
                    ? MonitorReading<double>.Available(maximumMillivolts!.Value,
                        Source + " Win32_PhysicalMemory.MaxVoltage (mV SMBIOS, não medidos)")
                    : MonitorReading<double>.Unavailable(
                        Source + " Win32_PhysicalMemory.MaxVoltage (0/nulo/inválido)")
            });
        }

        var boardIdentities = baseboards.Take(MaxMotherboards)
            .Select(x => (Row: x, Tag: Identity(x.Tag))).ToArray();
        var duplicateTags = boardIdentities.Where(x => x.Tag is not null)
            .GroupBy(x => x.Tag!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (duplicateTags.Count != 0) boardPartial = true;
        var boardsResult = new List<WindowsMotherboard>();
        foreach (var (raw, tag) in boardIdentities)
        {
            if (tag is null || duplicateTags.Contains(tag))
            { boardPartial = true; continue; }
            var maker = Identity(raw.Manufacturer);
            var model = Identity(raw.Product);
            if (maker is null || model is null) boardPartial = true;
            boardsResult.Add(new(tag, maker, model, Identity(raw.Version)));
        }

        var validLoads = loads.Take(MaxRowsPerClass).Select(x =>
            (Raw: x, Name: Identity(x.Name))).ToArray();
        var duplicateInstances = validLoads
            .Where(x => x.Name is not null && IsLogicalIndex(x.Name))
            .GroupBy(x => x.Name!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (duplicateInstances.Count != 0) loadPartial = true;
        var loadResult = new List<WindowsLogicalCpuLoad>();
        foreach (var (raw, name) in validLoads)
        {
            if (name == "_Total") continue;
            if (name is null || !IsLogicalIndex(name) || duplicateInstances.Contains(name))
            { loadPartial = true; continue; }
            ulong? usage = UInt(raw.PercentProcessorTime);
            if (usage is > 100 or null) loadPartial = true;
            loadResult.Add(new(name,
                usage is <= 100
                    ? MonitorReading<double>.Available(usage.Value, Source + " Win32_PerfFormattedData_PerfOS_Processor.PercentProcessorTime (instância lógica)")
                    : MonitorReading<double>.Unavailable(Source + " Win32_PerfFormattedData_PerfOS_Processor.PercentProcessorTime")));
        }

        return new(DateTimeOffset.UtcNow,
            Section(socketsOk, cpuPartial, cpuResult.Count), cpuResult.AsReadOnly(),
            Section(memoryOk, memoryPartial, ramResult.Count), ramResult.AsReadOnly(),
            Section(boardsOk, boardPartial, boardsResult.Count), boardsResult.AsReadOnly(),
            Section(logicalOk, loadPartial, loadResult.Count), loadResult.AsReadOnly())
        {
            ReportedBiosVersion = Identity(reportedBiosVersion)
        };
    }

    private static bool IsLogicalIndex(string name) =>
        name.Length is >= 1 and <= 4 && name.All(char.IsAsciiDigit) &&
        ushort.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out ushort n) &&
        n <= 4095;

    internal static WindowsPlatformHardwareSnapshot Capture(CancellationToken token)
    {
        if (!OperatingSystem.IsWindows())
            return Join([], [], [], [], false, false, false, false);
        object? locator = null, service = null;
        var cpus = new List<WmiCpuSocketRaw>();
        var ram = new List<WmiRamModuleRaw>();
        var boards = new List<WmiMotherboardRaw>();
        var loads = new List<WmiLogicalCpuRaw>();
        try
        {
            token.ThrowIfCancellationRequested();
            var type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (type is null)
                return Join([], [], [], [], false, false, false, false);
            locator = Activator.CreateInstance(type);
            service = ((dynamic)locator!).ConnectServer(".", @"root\cimv2");
            var clock = Stopwatch.StartNew();
            bool partial = false;

            bool Query(string sql, Action<Func<string, object?>> use)
            {
                object? result = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    result = ((dynamic)service!).ExecQuery(sql, "WQL", 16);
                    if (result is null) return false;
                    int count = 0;
                    foreach (object row in (IEnumerable)result)
                    {
                        try
                        {
                            if (++count > MaxRowsPerClass ||
                                clock.Elapsed > TimeSpan.FromSeconds(12))
                            { partial = true; break; }
                            token.ThrowIfCancellationRequested();
                            object? Get(string key)
                            {
                                object? props = null, prop = null;
                                try
                                {
                                    props = ((dynamic)row).Properties_;
                                    if (props is null) { partial = true; return null; }
                                    prop = ((dynamic)props).Item(key);
                                    if (prop is null) { partial = true; return null; }
                                    return ((dynamic)prop).Value;
                                }
                                catch (Exception ex) when (ex is COMException or
                                    Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or NullReferenceException)
                                { partial = true; return null; }
                                finally { Release(prop); Release(props); }
                            }
                            use(Get);
                        }
                        finally { Release(row); }
                    }
                    return true;
                }
                catch (Exception ex) when (ex is COMException or
                    Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or
                    UnauthorizedAccessException or InvalidCastException)
                { partial = true; return false; }
                finally { Release(result); }
            }

            bool cpuOk = Query("SELECT DeviceID, Name, NumberOfCores, " +
                "NumberOfLogicalProcessors, CurrentClockSpeed, Architecture, L3CacheSize, " +
                "VirtualizationFirmwareEnabled, SocketDesignation FROM Win32_Processor",
                get => cpus.Add(new(
                    Convert.ToString(get("DeviceID"), CultureInfo.InvariantCulture),
                    Convert.ToString(get("Name"), CultureInfo.InvariantCulture),
                    get("NumberOfCores"), get("NumberOfLogicalProcessors"),
                    get("CurrentClockSpeed"), get("Architecture"), get("L3CacheSize"),
                    get("VirtualizationFirmwareEnabled"),
                    Convert.ToString(get("SocketDesignation"), CultureInfo.InvariantCulture))));
            bool ramOk = Query("SELECT BankLabel, DeviceLocator, Capacity, " +
                "ConfiguredClockSpeed, SMBIOSMemoryType, ConfiguredVoltage, " +
                "MinVoltage, MaxVoltage FROM Win32_PhysicalMemory",
                get => ram.Add(new(
                    Convert.ToString(get("BankLabel"), CultureInfo.InvariantCulture),
                    Convert.ToString(get("DeviceLocator"), CultureInfo.InvariantCulture),
                    get("Capacity"), get("ConfiguredClockSpeed"), get("SMBIOSMemoryType"),
                    get("ConfiguredVoltage"), get("MinVoltage"), get("MaxVoltage"))));
            bool boardOk = Query("SELECT Tag, Manufacturer, Product, Version FROM Win32_BaseBoard",
                get => boards.Add(new(
                    Convert.ToString(get("Tag"), CultureInfo.InvariantCulture),
                    Convert.ToString(get("Manufacturer"), CultureInfo.InvariantCulture),
                    Convert.ToString(get("Product"), CultureInfo.InvariantCulture),
                    Convert.ToString(get("Version"), CultureInfo.InvariantCulture))));
            bool loadOk = Query("SELECT Name, PercentProcessorTime " +
                "FROM Win32_PerfFormattedData_PerfOS_Processor",
                get => loads.Add(new(
                    Convert.ToString(get("Name"), CultureInfo.InvariantCulture),
                    get("PercentProcessorTime"))));
            // Consulta adicional isolada: Win32_BIOS.SMBIOSBIOSVersion é a
            // versão reportada pelo firmware, não versão da placa-base.
            // Falha opcional não deve contaminar a cobertura já obtida.
            bool priorPartial = partial;
            var bios = new List<string?>();
            bool biosOk = Query("SELECT SMBIOSBIOSVersion FROM Win32_BIOS",
                get => bios.Add(Identity(Convert.ToString(get("SMBIOSBIOSVersion"), CultureInfo.InvariantCulture))));
            string? biosVersion = biosOk && !partial && bios.Count == 1 ? bios[0] : null;
            var snapshot = Join(cpus, ram, boards, loads,
                cpuOk, ramOk, boardOk, loadOk, priorPartial, biosVersion);
            if (snapshot.CpuSocketsState == HardwareDiscoveryState.Complete &&
                snapshot.CpuSockets.Count == 1 && snapshot.CpuSockets[0].ReportedArchitecture == "x64" &&
                V269IntelCpuidClocks.Capture() is { } clocks)
                snapshot = snapshot with { IntelNominalBaseMhz = clocks.BaseMhz,
                    IntelReportedMaximumMhz = clocks.MaximumMhz };
            token.ThrowIfCancellationRequested();
            if (snapshot.RamModulesState == HardwareDiscoveryState.Complete)
                snapshot = snapshot with { ReportedPopulatedMemoryChannels =
                    V269SmbiosChannels.Capture(snapshot.RamModules.Count) };
            return snapshot;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is COMException or
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or
            UnauthorizedAccessException or InvalidCastException or
            ArgumentException or System.Security.SecurityException)
        { return Join([], [], [], [], false, false, false, false); }
        finally { Release(service); Release(locator); }
    }

    private static void Release(object? obj)
    {
        if (obj is not null && Marshal.IsComObject(obj))
            Marshal.FinalReleaseComObject(obj);
    }
}


// Intel SDM, CPUID leaf 16H. Base e máximo reportados em MHz.
// Somente um socket homogêneo, sem hipervisor anunciado. O máximo
// arquitetural não é automaticamente a frequência Turbo Boost.
internal sealed record V269IntelCpuidClocks(int BaseMhz, int? MaximumMhz)
{
    internal static V269IntelCpuidClocks? Parse(int eax, int ebx)
    {
        int basis = eax & 0xffff, maximum = ebx & 0xffff;
        if (basis is < 100 or > 20_000) return null;
        return new(basis, maximum >= basis && maximum <= 20_000 ? maximum : null);
    }

    internal static V269IntelCpuidClocks? Capture()
    {
        if (!System.Runtime.Intrinsics.X86.X86Base.IsSupported) return null;
        var (max, b, c, d) = System.Runtime.Intrinsics.X86.X86Base.CpuId(0, 0);
        if (max < 0x16 || b != 0x756e6547 || d != 0x49656e69 || c != 0x6c65746e)
            return null; // GenuineIntel, e folha implementada.
        var features = System.Runtime.Intrinsics.X86.X86Base.CpuId(1, 0);
        if ((unchecked((uint)features.Ecx) & 0x80000000u) != 0) return null;
        var extended = System.Runtime.Intrinsics.X86.X86Base.CpuId(7, 0);
        if ((extended.Edx & (1 << 15)) != 0) return null; // CPUs híbridas: sem inferência.
        var frequency = System.Runtime.Intrinsics.X86.X86Base.CpuId(0x16, 0);
        return Parse(frequency.Eax, frequency.Ebx);
    }
}


// SMBIOS DSP0134 §§7.17, 7.18, 7.38. Topologia descrita pelo
// firmware; não prova interleaving ativo, largura efetiva nem latência CAS.
// Não ler strings, números de série, UUIDs ou outros identificadores;
// o buffer bruto é temporário e nunca entra em relatório/persistência.
internal static class V269SmbiosChannels
{
    private const uint Rsmb = 0x52534D42;
    private const int MaximumBytes = 4 * 1024 * 1024;

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetSystemFirmwareTable(uint provider, uint table,
        [Out] byte[]? buffer, uint size);

    internal static int? Capture(int expectedModules)
    {
        if (!OperatingSystem.IsWindows() || expectedModules is < 1 or > 128) return null;
        byte[]? buffer = null;
        try
        {
            uint bytes = GetSystemFirmwareTable(Rsmb, 0, null, 0);
            if (bytes < 14 || bytes > MaximumBytes) return null;
            buffer = new byte[(int)bytes];
            if (GetSystemFirmwareTable(Rsmb, 0, buffer, bytes) != bytes) return null;
            return Parse(buffer, expectedModules);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or
            BadImageFormatException or UnauthorizedAccessException or System.Security.SecurityException)
        { return null; }
        finally { if (buffer is not null) Array.Clear(buffer); }
    }

    internal static int? Parse(ReadOnlySpan<byte> raw, int expectedModules)
    {
        if (raw.Length < 14 || raw.Length > MaximumBytes ||
            expectedModules is < 1 or > 128) return null;
        uint declared = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(4, 4));
        if (declared != raw.Length - 8 || declared < 6) return null;
        var table = raw[8..];
        var seen = new HashSet<ushort>();
        var arrays = new Dictionary<ushort, bool>();
        var modules = new Dictionary<ushort, (ushort Array, bool Populated)>();
        var channels = new List<ushort[]>();
        bool endMarker = false;
        static ushort U16(ReadOnlySpan<byte> value, int offset) =>
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(value.Slice(offset, 2));

        int offset = 0, records = 0;
        while (offset + 4 <= table.Length && ++records <= 8192)
        {
            byte type = table[offset], length = table[offset + 1];
            if (length < 4 || offset + length + 2 > table.Length) return null;
            var record = table.Slice(offset, length);
            ushort handle = U16(record, 2);
            if (!seen.Add(handle)) return null;
            int end = offset + length;
            // Strings somente delimitam registros; seu conteúdo não é usado.
            while (end + 1 < table.Length && (table[end] != 0 || table[end + 1] != 0)) end++;
            if (end + 1 >= table.Length) return null;
            offset = end + 2;
            if (type == 127)
            {
                if (length != 4) return null;
                endMarker = true; break;
            }
            if (type == 16)
            {
                if (length < 15) return null;
                arrays.Add(handle, record[5] == 3); // System Memory.
            }
            else if (type == 17)
            {
                if (length < 21) return null;
                ushort size = U16(record, 12);
                // 0 = slot vazio; FFFF = tamanho desconhecido.
                if (size is 0xffff or 0x8000) return null;
                if (size == 0x7fff)
                {
                    if (length < 32) return null;
                    uint extended = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(28, 4));
                    if (extended == 0 || (extended & 0x80000000u) != 0) return null;
                }
                modules.Add(handle, (U16(record, 4), size != 0));
            }
            else if (type == 37)
            {
                if (length < 7) return null;
                int count = record[6];
                if (count > 82 || length < 7 + 3 * count) return null;
                var members = new ushort[count];
                int load = 0;
                for (int i = 0; i < count; i++)
                {
                    load += record[7 + 3 * i];
                    members[i] = U16(record, 8 + 3 * i);
                }
                if (load > record[5]) return null;
                channels.Add(members);
            }
        }
        if (!endMarker || channels.Count == 0 ||
            modules.Values.Any(m => !arrays.ContainsKey(m.Array))) return null;
        var populated = modules.Where(m => m.Value.Populated && arrays[m.Value.Array])
            .Select(m => m.Key).ToHashSet();
        if (populated.Count != expectedModules) return null;
        var mapped = new HashSet<ushort>();
        var allChannelMembers = new HashSet<ushort>();
        int usedChannels = 0;
        foreach (var channel in channels)
        {
            bool used = false;
            foreach (ushort handle in channel)
            {
                if (!modules.ContainsKey(handle) || !allChannelMembers.Add(handle)) return null;
                if (!populated.Contains(handle)) continue;
                mapped.Add(handle); used = true;
            }
            if (used) usedChannels++;
        }
        return mapped.SetEquals(populated) && usedChannels > 0 ? usedChannels : null;
    }
}
