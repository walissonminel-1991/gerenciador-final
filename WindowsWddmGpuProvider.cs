using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Contadores WDDM padronizados pelo Windows: AMD, Intel, NVIDIA e adaptadores
// virtuais que publiquem GPU Engine / GPU Adapter Memory. A chave é o LUID da
// sessão e o índice físico: não associar ao nome PnP, à ordem ou à GPU 0.
internal readonly record struct WddmGpuKey(uint LuidHigh, uint LuidLow, uint Physical)
{
    public override string ToString() =>
        FormattableString.Invariant($"luid_0x{LuidHigh:X8}_0x{LuidLow:X8}_phys_{Physical}");
}

internal sealed record WddmEngineCounterRow(string Name, object? UtilizationPercentage);
internal sealed record WddmMemoryCounterRow(string Name, object? DedicatedUsage, object? SharedUsage);
// Atividade dos motores físicos: 3D, COPY, COMPUTE e vídeo são
// domínios diferentes. Não somar seus percentuais, nem chamar um deles
// de "uso total" da GPU.
internal sealed record WddmEngineSample(
    string EngineIndex, string EngineType,
    MonitorReading<double> UsagePercent);

internal sealed record WddmGpuAdapterSample(
    WddmGpuKey Key,
    MonitorReading<double> BusiestEnginePercent,
    MonitorReading<ulong> DedicatedUsedBytes,
    MonitorReading<ulong> SharedUsedBytes)
{
    internal IReadOnlyList<WddmEngineSample> Engines { get; init; } = [];

    internal MonitorReading<double> BusiestOfType(string engineType)
    {
        string source = "Windows WDDM engine " + engineType;
        if (string.IsNullOrWhiteSpace(engineType))
            return MonitorReading<double>.Unavailable(source);
        var observed = Engines
            .Where(x => (string.Equals(x.EngineType, engineType,
                StringComparison.OrdinalIgnoreCase) ||
                x.EngineType.StartsWith(engineType + "_", StringComparison.OrdinalIgnoreCase)) &&
                x.UsagePercent.Availability == MonitorAvailability.Available &&
                x.UsagePercent.Value.HasValue)
            .Select(x => x.UsagePercent.Value!.Value).ToArray();
        return observed.Length > 0
            ? MonitorReading<double>.Available(observed.Max(),
                source + " (motor mais ocupado, não uso total)")
            : MonitorReading<double>.Unavailable(source);
    }
}
internal sealed record WddmGpuSnapshot(
    DateTimeOffset CapturedAtUtc, HardwareDiscoveryState State,
    IReadOnlyList<WddmGpuAdapterSample> Adapters, string? Limitation);

internal static class WindowsWddmGpuProvider
{
    private const string Source = "Windows WDDM GPU Performance Counters";
    private const int MaxRows = 4096;
    private static readonly Regex EnginePattern = new(
        @"^pid_\d+_luid_0x([0-9A-Fa-f]{1,8})_0x([0-9A-Fa-f]{1,8})_phys_(\d+)_eng_(\d+)_engtype_([A-Za-z0-9_]+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static readonly Regex MemoryPattern = new(
        @"^luid_0x([0-9A-Fa-f]{1,8})_0x([0-9A-Fa-f]{1,8})_phys_(\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private static bool TryKey(Match match, out WddmGpuKey key)
    {
        key = default;
        if (!match.Success ||
            !uint.TryParse(match.Groups[1].Value, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out uint high) ||
            !uint.TryParse(match.Groups[2].Value, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out uint low) ||
            !uint.TryParse(match.Groups[3].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out uint phys) || phys > 63)
            return false;
        key = new(high, low, phys);
        return true;
    }

    internal static bool TryEngineKey(string? name, out WddmGpuKey key,
        out string engine)
    {
        key = default;
        engine = "";
        if (string.IsNullOrWhiteSpace(name)) return false;
        var match = EnginePattern.Match(name);
        if (!TryKey(match, out key)) return false;
        // Engine identity includes engine number AND type. Different processes
        // using the same engine contribute to the same engine counter.
        engine = match.Groups[4].Value + ":" + match.Groups[5].Value.ToUpperInvariant();
        return true;
    }

    internal static bool TryMemoryKey(string? name, out WddmGpuKey key) =>
        TryKey(MemoryPattern.Match(name ?? ""), out key);

    internal static ulong? Counter(object? raw)
    {
        if (raw is null or DBNull or float or double or decimal) return null;
        if (raw is string text)
            return ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture,
                out ulong value) ? value : null;
        if (raw is not (byte or ushort or uint or ulong or short or int or long))
            return null;
        try { return Convert.ToUInt64(raw, CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
        { return null; }
    }

    internal static WddmGpuSnapshot Join(
        IEnumerable<WddmEngineCounterRow> engines,
        IEnumerable<WddmMemoryCounterRow> memory,
        bool sourcePartial = false)
    {
        var engineRows = engines.Take(MaxRows + 1).ToArray();
        var memoryRows = memory.Take(MaxRows + 1).ToArray();
        bool partial = sourcePartial || engineRows.Length > MaxRows ||
            memoryRows.Length > MaxRows;

        var byKey = new Dictionary<WddmGpuKey,
            Dictionary<string, List<ulong>>>();
        foreach (var row in engineRows.Take(MaxRows))
        {
            if (!TryEngineKey(row.Name, out var key, out string engine))
            { partial = true; continue; }
            if (!byKey.TryGetValue(key, out var groups))
                byKey[key] = groups = new(StringComparer.Ordinal);
            if (!groups.TryGetValue(engine, out var values))
                groups[engine] = values = [];
            ulong? value = Counter(row.UtilizationPercentage);
            if (value is not { } measured || measured > 100)
            { partial = true; values.Add(ulong.MaxValue); }
            else values.Add(measured);
        }

        var memoryByKey = new Dictionary<WddmGpuKey, List<WddmMemoryCounterRow>>();
        foreach (var row in memoryRows.Take(MaxRows))
        {
            if (!TryMemoryKey(row.Name, out var key))
            { partial = true; continue; }
            if (!memoryByKey.TryGetValue(key, out var rows))
                memoryByKey[key] = rows = [];
            rows.Add(row);
        }

        var keys = byKey.Keys.Union(memoryByKey.Keys)
            .OrderBy(x => x.LuidHigh).ThenBy(x => x.LuidLow)
            .ThenBy(x => x.Physical).ToArray();
        var adapters = new List<WddmGpuAdapterSample>(keys.Length);
        foreach (var key in keys)
        {
            var busy = MonitorReading<double>.Unavailable(Source + " busiest-engine");
            var engineDetails = new List<WddmEngineSample>();
            if (byKey.TryGetValue(key, out var groups))
            {
                // Somar somente processos do mesmo engine. Outra engine de
                // mesma classe NÃO é somada: selecionar o maior motor.
                foreach (var group in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    int split = group.Key.IndexOf(':');
                    string engineIndex = group.Key[..split];
                    string engineType = group.Key[(split + 1)..];
                    var values = group.Value;
                    if (values.Contains(ulong.MaxValue) ||
                        values.Sum(v => (double)v) > 100)
                    {
                        partial = true;
                        engineDetails.Add(new(engineIndex, engineType,
                            MonitorReading<double>.Unavailable(Source + " engine " + engineType)));
                        continue;
                    }
                    engineDetails.Add(new(engineIndex, engineType,
                        MonitorReading<double>.Available(
                            values.Sum(v => (double)v),
                            Source + " engine " + engineType)));
                }
                var valid = engineDetails
                    .Where(e => e.UsagePercent.Availability == MonitorAvailability.Available &&
                        e.UsagePercent.Value.HasValue)
                    .Select(e => e.UsagePercent.Value!.Value).ToArray();
                if (valid.Length > 0)
                    busy = MonitorReading<double>.Available(valid.Max(),
                        Source + " busiest-engine (not total GPU)");
            }

            var dedicated = MonitorReading<ulong>.Unavailable(Source + " dedicated-usage");
            var shared = MonitorReading<ulong>.Unavailable(Source + " shared-usage");
            if (memoryByKey.TryGetValue(key, out var rows) && rows.Count == 1)
            {
                ulong? bytes = Counter(rows[0].DedicatedUsage);
                ulong? sharedBytes = Counter(rows[0].SharedUsage);
                if (bytes is { } valid)
                    dedicated = MonitorReading<ulong>.Available(valid, Source + " dedicated-usage");
                else partial = true;
                if (sharedBytes is { } validShared)
                    shared = MonitorReading<ulong>.Available(validShared, Source + " shared-usage");
                else partial = true;
            }
            else if (rows is { Count: > 1 }) partial = true;
            adapters.Add(new(key, busy, dedicated, shared)
            {
                Engines = engineDetails.AsReadOnly()
            });
        }
        return new(DateTimeOffset.UtcNow,
            keys.Length == 0 ? HardwareDiscoveryState.Unavailable :
                partial ? HardwareDiscoveryState.Partial : HardwareDiscoveryState.Complete,
            adapters.AsReadOnly(),
            keys.Length == 0 ? "WDDM não publicou contadores válidos" :
                partial ? "Alguns contadores WDDM foram parciais ou inconsistentes" : null);
    }

    // WMI COM de leitura, usando o mesmo padrão COM já empregado pelo GF.
    // Sob demanda em worker STA; NUNCA chamar esta captura no timer de 2 s.
    internal static WddmGpuSnapshot Capture(CancellationToken token)
    {
        if (!OperatingSystem.IsWindows())
            return Unavailable("Contadores WDDM exclusivos do Windows");
        object? locator = null, services = null;
        var engines = new List<WddmEngineCounterRow>();
        var memory = new List<WddmMemoryCounterRow>();
        bool partial = false;
        try
        {
            token.ThrowIfCancellationRequested();
            var progId = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (progId is null) return Unavailable("COM WMI indisponível");
            locator = Activator.CreateInstance(progId);
            services = ((dynamic)locator!).ConnectServer(".", @"root\cimv2");
            var clock = Stopwatch.StartNew();

            void Query(string sql, Action<Func<string, object?>> use)
            {
                object? result = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    result = ((dynamic)services!).ExecQuery(sql, "WQL", 16);
                    int scanned = 0;
                    foreach (object row in (IEnumerable)result)
                    {
                        try
                        {
                            if (++scanned > MaxRows || clock.Elapsed > TimeSpan.FromSeconds(12))
                            { partial = true; break; }
                            token.ThrowIfCancellationRequested();
                            object? Get(string name)
                            {
                                object? properties = null, property = null;
                                try
                                {
                                    properties = ((dynamic)row).Properties_;
                                    property = ((dynamic)properties).Item(name);
                                    return ((dynamic)property).Value;
                                }
                                catch (COMException) { partial = true; return null; }
                                finally { Release(property); Release(properties); }
                            }
                            use(Get);
                        }
                        finally { Release(row); }
                    }
                }
                catch (COMException) { partial = true; }
                finally { Release(result); }
            }

            Query("SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine",
                get => engines.Add(new(Convert.ToString(get("Name"), CultureInfo.InvariantCulture) ?? "",
                    get("UtilizationPercentage"))));
            Query("SELECT Name, DedicatedUsage, SharedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory",
                get => memory.Add(new(Convert.ToString(get("Name"), CultureInfo.InvariantCulture) ?? "",
                    get("DedicatedUsage"), get("SharedUsage"))));

            return Join(engines, memory, partial);
        }
        catch (OperationCanceledException) { return Unavailable("Consulta WDDM cancelada"); }
        catch (Exception ex) when (ex is COMException or InvalidCastException or
            UnauthorizedAccessException or InvalidOperationException or
            ArgumentException or System.Security.SecurityException)
        { return Unavailable("WMI WDDM indisponível ou acesso negado"); }
        finally { Release(services); Release(locator); }
    }

    private static void Release(object? obj)
    {
        if (obj is not null && Marshal.IsComObject(obj))
            Marshal.FinalReleaseComObject(obj);
    }

    private static WddmGpuSnapshot Unavailable(string reason) =>
        new(DateTimeOffset.UtcNow, HardwareDiscoveryState.Unavailable, [], reason);
}
