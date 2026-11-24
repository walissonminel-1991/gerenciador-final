using System.Runtime.InteropServices;
using System.Text;

namespace GerenciadorAplicativos;

// NVML é entregue pelo driver NVIDIA já instalado; não baixar, instalar ou
// elevar. Apenas caminhos Windows administrados, sem busca no PATH/current dir.
// O snapshot cobre cada GPU NVIDIA separadamente: nunca tomar GPU 0 por "a GPU".
internal sealed record NvidiaGpuSensorSample(
    uint Index, string Name, string? Uuid,
    MonitorReading<double> UsagePercent,
    MonitorReading<double> TemperatureC,
    MonitorReading<double> PowerWatts,
    MonitorReading<double> FanPercent,
    MonitorReading<ulong> VramUsedBytes)
{
    internal string? CurrentPcieLink { get; init; }
    internal string? ReportedArchitecture { get; init; }
}

internal sealed record NvidiaGpuSensorSnapshot(
    DateTimeOffset CapturedAtUtc, bool ProviderAvailable,
    IReadOnlyList<NvidiaGpuSensorSample> Devices, string? Limitation)
{
    internal string? NvidiaDriverVersion { get; init; }
}

internal static class NvidiaNvmlSensorProvider
{
    private const string Source = "NVIDIA NVML (driver local)";
    private const int MaxDevices = 16;
    private const int Success = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Utilization { public uint Gpu; public uint Memory; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo { public ulong Total; public ulong Free; public ulong Used; }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Init();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Shutdown();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetCount(out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetHandle(uint index, out IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetName(IntPtr device, [Out] byte[] name, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetUuid(IntPtr device, [Out] byte[] uuid, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetUtilization(IntPtr device, out Utilization utilization);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetTemperature(IntPtr device, uint sensor, out uint temp);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetPower(IntPtr device, out uint milliwatts);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetFan(IntPtr device, out uint percent);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetMemory(IntPtr device, out MemoryInfo memory);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetDeviceUInt(IntPtr device, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetDriverVersion([Out] byte[] version, uint length);

    // Enum oficial nvmlDeviceArchitecture_t. Não inferir pelo nome,
    // compute capability, memória, índice DXGI ou geração PCIe.
    internal static string? FormatArchitecture(uint? raw) => raw switch
    {
        2 => "Kepler", 3 => "Maxwell", 4 => "Pascal", 5 => "Volta",
        6 => "Turing", 7 => "Ampere", 8 => "Ada", 9 => "Hopper",
        10 => "Blackwell", _ => null
    };

    internal static string? FormatPcie(uint? generation, uint? width) =>
        generation is >= 1 and <= 7 && width is 1 or 2 or 4 or 8 or 12 or 16 or 32
            ? $"PCIe {generation}.0 x{width} (atual)" : null;

    internal static string? ValidateDriverVersion(string? value)
    {
        if (value is null || value.Length > 32) return null;
        var parts = value.Split('.');
        return parts.Length is >= 2 and <= 4 && parts.All(p =>
            p.Length is >= 1 and <= 6 && p.All(char.IsAsciiDigit)) ? value : null;
    }

    internal static MonitorReading<double> Percent(uint? raw, string metric) =>
        raw is <= 100 ? MonitorReading<double>.Available(raw.Value, Source + " " + metric)
        : MonitorReading<double>.Unavailable(Source + " " + metric);

    internal static MonitorReading<double> Temperature(uint? raw) =>
        raw is > 0 and <= 125
            ? MonitorReading<double>.Available(raw.Value, Source + " temperatura GPU")
            : MonitorReading<double>.Unavailable(Source + " temperatura GPU");

    internal static MonitorReading<double> Watts(uint? milliwatts) =>
        milliwatts is > 0 and <= 1_000_000
            ? MonitorReading<double>.Available(milliwatts.Value / 1000d, Source + " potência GPU")
            : MonitorReading<double>.Unavailable(Source + " potência GPU");

    // A memória de vídeo reportada pelo WDDM não representa alocação por jogo.
    // A margem de 32 MiB aceita variação do contador entre campos do driver.
    internal static bool IsPlausibleMemory(ulong total, ulong free, ulong used) =>
        total > 0 && used <= total && free <= total &&
        (used <= total - free || used - (total - free) <= 32UL * 1024 * 1024);

    private static T? Export<T>(IntPtr library, string name) where T : Delegate =>
        NativeLibrary.TryGetExport(library, name, out var address)
            ? Marshal.GetDelegateForFunctionPointer<T>(address) : null;

    // Não investigar caminhos personalizados do driver sem revisão.
    internal static string[] TrustedCandidatePaths(string windowsSystem, string programFiles)
    {
        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(windowsSystem))
            paths.Add(Path.Combine(windowsSystem, "nvml.dll"));
        if (!string.IsNullOrWhiteSpace(programFiles))
            paths.Add(Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvml.dll"));
        return paths.ToArray();
    }

    private static IntPtr LoadTrusted()
    {
        if (!OperatingSystem.IsWindows()) return IntPtr.Zero;
        foreach (string path in TrustedCandidatePaths(Environment.SystemDirectory,
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)))
        {
            try
            {
                if (Path.IsPathFullyQualified(path) &&
                    NativeLibrary.TryLoad(path, out IntPtr library))
                    return library;
            }
            catch (Exception ex) when (ex is BadImageFormatException or DllNotFoundException
                or FileLoadException or UnauthorizedAccessException or IOException)
            { /* Sem injetar DLL da pasta atual ou da variável PATH. */ }
        }
        return IntPtr.Zero;
    }

    private static string? Decode(byte[] buffer)
    {
        int end = Array.IndexOf(buffer, (byte)0);
        if (end < 0) end = buffer.Length;
        if (end == 0) return null;
        string value = Encoding.UTF8.GetString(buffer, 0, end).Trim();
        return value.Length is > 0 and < 192 && !value.Any(char.IsControl) ? value : null;
    }

    internal static NvidiaGpuSensorSnapshot Capture()
    {
        var now = DateTimeOffset.UtcNow;
        IntPtr library = LoadTrusted();
        if (library == IntPtr.Zero)
            return new(now, false, [], "NVML não disponível nos caminhos confiáveis");

        bool started = false;
        Shutdown? shutdown = null;
        try
        {
            var init = Export<Init>(library, "nvmlInit_v2");
            shutdown = Export<Shutdown>(library, "nvmlShutdown");
            var count = Export<GetCount>(library, "nvmlDeviceGetCount_v2");
            var handle = Export<GetHandle>(library, "nvmlDeviceGetHandleByIndex_v2");
            if (init is null || shutdown is null || count is null || handle is null)
                return new(now, false, [], "ABI NVML incompleta");
            if (init() != Success)
                return new(now, false, [], "Driver NVML recusou inicialização");
            started = true;
            if (count(out uint total) != Success)
                return new(now, false, [], "NVML recusou enumeração de GPUs");

            var name = Export<GetName>(library, "nvmlDeviceGetName");
            var uuid = Export<GetUuid>(library, "nvmlDeviceGetUUID");
            var usage = Export<GetUtilization>(library, "nvmlDeviceGetUtilizationRates");
            var temperature = Export<GetTemperature>(library, "nvmlDeviceGetTemperature");
            var power = Export<GetPower>(library, "nvmlDeviceGetPowerUsage");
            var fan = Export<GetFan>(library, "nvmlDeviceGetFanSpeed");
            var memory = Export<GetMemory>(library, "nvmlDeviceGetMemoryInfo");
            var pcieGeneration = Export<GetDeviceUInt>(library, "nvmlDeviceGetCurrPcieLinkGeneration");
            var pcieWidth = Export<GetDeviceUInt>(library, "nvmlDeviceGetCurrPcieLinkWidth");
            var architecture = Export<GetDeviceUInt>(library, "nvmlDeviceGetArchitecture");
            var driverVersion = Export<GetDriverVersion>(library, "nvmlSystemGetDriverVersion");
            var versionBuffer = new byte[96];
            string? version = driverVersion is not null &&
                driverVersion(versionBuffer, (uint)versionBuffer.Length) == Success
                ? ValidateDriverVersion(Decode(versionBuffer)) : null;
            var devices = new List<NvidiaGpuSensorSample>();
            bool partial = total > MaxDevices;
            for (uint index = 0; index < Math.Min(total, (uint)MaxDevices); ++index)
            {
                if (handle(index, out var device) != Success || device == IntPtr.Zero)
                {
                    partial = true; continue;
                }
                var nameBytes = new byte[96];
                var uuidBytes = new byte[96];
                string deviceName = name is not null &&
                    name(device, nameBytes, (uint)nameBytes.Length) == Success
                    ? Decode(nameBytes) ?? $"NVIDIA GPU índice {index}"
                    : $"NVIDIA GPU índice {index}";
                string? identity = uuid is not null &&
                    uuid(device, uuidBytes, (uint)uuidBytes.Length) == Success
                    ? Decode(uuidBytes) : null;
                uint? gpuPercent = usage is not null &&
                    usage(device, out var utilization) == Success ? utilization.Gpu : null;
                uint? gpuTemp = temperature is not null &&
                    temperature(device, 0, out var degrees) == Success ? degrees : null;
                uint? gpuPower = power is not null &&
                    power(device, out var milli) == Success ? milli : null;
                uint? fanPct = fan is not null &&
                    fan(device, out var reported) == Success ? reported : null;
                MemoryInfo? vram = memory is not null &&
                    memory(device, out var info) == Success ? info : null;
                uint? gen = pcieGeneration is not null && pcieGeneration(device, out uint reportedGen) == Success
                    ? reportedGen : null;
                uint? lanes = pcieWidth is not null && pcieWidth(device, out uint reportedLanes) == Success
                    ? reportedLanes : null;
                devices.Add(new(index, deviceName, identity,
                    Percent(gpuPercent, "utilização"),
                    Temperature(gpuTemp), Watts(gpuPower),
                    Percent(fanPct, "ventoinha (%; não RPM)"),
                    vram is { } m && IsPlausibleMemory(m.Total, m.Free, m.Used)
                        ? MonitorReading<ulong>.Available(m.Used, Source + " VRAM (WDDM)")
                        : MonitorReading<ulong>.Unavailable(Source + " VRAM (WDDM)"))
                {
                    CurrentPcieLink = FormatPcie(gen, lanes),
                    ReportedArchitecture = architecture is not null &&
                        architecture(device, out uint reportedArchitecture) == Success
                        ? FormatArchitecture(reportedArchitecture) : null
                });
            }
            return new(now, true, devices.AsReadOnly(),
                partial ? "Enumeração NVIDIA parcial" : null)
            { NvidiaDriverVersion = devices.Count > 0 ? version : null };
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or BadImageFormatException
            or InvalidOperationException or SEHException or ArgumentException)
        {
            return new(now, false, [], "Interface NVML indisponível");
        }
        finally
        {
            if (started) { try { shutdown?.Invoke(); } catch { } }
            NativeLibrary.Free(library);
        }
    }
}
