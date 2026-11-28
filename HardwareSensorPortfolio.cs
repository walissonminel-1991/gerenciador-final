namespace GerenciadorAplicativos;

// Uma fotografia consultiva reúne as fontes sem associar a GPU NVML à
// placa PnP errada e sem anunciar dados ACPI como sensores CPU/chipset/VRM.
// O código da interface pode reutilizar um inventário PnP já capturado.
internal sealed record HardwareSensorPortfolio(
    DateTimeOffset CapturedAtUtc,
    LiveHardwareSample WindowsCounters,
    HardwareDiscoverySnapshot DeviceInventory,
    NvidiaGpuSensorSnapshot NvidiaGpus,
    StorageSensorsSnapshot Storage,
    AcpiThermalZoneSnapshot AcpiPlatform,
    WddmGpuSnapshot? WindowsGpuCounters = null,
    DxgiGpuSnapshot? DxgiAdapters = null,
    WindowsPlatformHardwareSnapshot? PlatformHardware = null,
    D3dKmtGpuSnapshot? D3dKmtGpuSensors = null)
{
    internal string? TpmDescription { get; init; }
    internal bool HasMeasuredGpu => NvidiaGpus.Devices.Any(x =>
        x.UsagePercent.Availability == MonitorAvailability.Available ||
        x.TemperatureC.Availability == MonitorAvailability.Available ||
        x.VramUsedBytes.Availability == MonitorAvailability.Available);
    internal bool HasMeasuredStorage => Storage.Disks.Any(x =>
        x.TemperatureC.Availability == MonitorAvailability.Available);
    internal bool HasMeasuredWddmGpu => WindowsGpuCounters?.Adapters.Any(x =>
        x.BusiestEnginePercent.Availability == MonitorAvailability.Available ||
        x.DedicatedUsedBytes.Availability == MonitorAvailability.Available) == true;
    internal IReadOnlyList<ResolvedWddmGpu> MatchedGpuDevices =>
        WindowsGpuCounters is not null && DxgiAdapters is not null
            ? DxgiGpuIdentityProvider.Correlate(WindowsGpuCounters, DxgiAdapters, DeviceInventory)
            : [];
}

// Somente consulta manual/explicitamente solicitada. A chamada tem WMI,
// portanto a UI deve usar StaWorker e não invocá-la no timer de RAM de 2 s.
// Não altera políticas nem dispara manutenção de hardware.
internal static class HardwareSensorPortfolioReader
{
    internal static HardwareSensorPortfolio CaptureOnDemand(
        LiveHardwareMonitor monitor, CancellationToken token,
        HardwareDiscoverySnapshot? existingInventory = null) =>
        CaptureOnDemand(monitor, token, existingInventory, false);

    internal static HardwareSensorPortfolio CaptureOnDemand(
        LiveHardwareMonitor monitor, CancellationToken token,
        HardwareDiscoverySnapshot? existingInventory, bool probeCpuVisibleVram)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        token.ThrowIfCancellationRequested();

        // A GUI pode compartilhar o inventário existente, evitando
        // segunda consulta ao Windows nesta mesma sessão.
        var inventory = existingInventory ?? NativeHardwareDiscovery.Capture();
        token.ThrowIfCancellationRequested();
        var baseSample = monitor.Capture();
        token.ThrowIfCancellationRequested();
        var gpu = NvidiaNvmlSensorProvider.Capture();
        token.ThrowIfCancellationRequested();
        var wddm = WindowsWddmGpuProvider.Capture(token);
        token.ThrowIfCancellationRequested();
        var dxgi = DxgiGpuIdentityProvider.Capture(probeCpuVisibleVram);
        token.ThrowIfCancellationRequested();
        // D3DKMT query reporta sensor pela chave {LUID, phys}; não
        // pressupor identidade via índice DXGI ou nome do adaptador.
        var d3dkmt = D3dKmtGpuSensorProvider.Capture(dxgi, token);
        token.ThrowIfCancellationRequested();
        var storage = WindowsStorageSensorProvider.Capture(token);
        token.ThrowIfCancellationRequested();
        var acpi = AcpiThermalZoneProvider.Capture(token);
        token.ThrowIfCancellationRequested();
        // Dados SMBIOS e contadores por CPU lógica. O diagnóstico não
        // associa esses dados a temperatura de pacote, VRM ou DIMM.
        var platform = WindowsPlatformHardwareProvider.Capture(token);
        token.ThrowIfCancellationRequested();
        string? tpm = V269TpmReader.Capture();
        token.ThrowIfCancellationRequested();
        return new(DateTimeOffset.UtcNow, baseSample, inventory, gpu, storage, acpi, wddm, dxgi, platform, d3dkmt)
        { TpmDescription = tpm };
    }
}


// TBS: leitura da versão por API oficial, sem abrir contexto de comandos,
// sem provisionar, habilitar, desabilitar ou limpar o TPM.
internal static class V269TpmReader
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct TpmDeviceInfo
    {
        public uint StructVersion, TpmVersion, InterfaceType, ImplementationRevision;
    }

    [System.Runtime.InteropServices.DllImport("tbs.dll", ExactSpelling = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    private static extern uint Tbsi_GetDeviceInfo(uint size, out TpmDeviceInfo info);

    internal static string? Describe(uint result, uint structVersion, uint tpmVersion)
    {
        if (result == 0x8028400F) return "Não detectado (TBS)";
        if (result != 0 || structVersion != 1) return null;
        return tpmVersion switch { 1 => "Presente (1.2)", 2 => "Presente (2.0)", _ => null };
    }

    internal static string? Capture()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            uint result = Tbsi_GetDeviceInfo(16, out var info);
            return Describe(result, info.StructVersion, info.TpmVersion);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { return null; }
    }
}
