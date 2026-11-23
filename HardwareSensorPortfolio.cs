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
    DxgiGpuSnapshot? DxgiAdapters = null)
{
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
        HardwareDiscoverySnapshot? existingInventory = null)
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
        var dxgi = DxgiGpuIdentityProvider.Capture();
        token.ThrowIfCancellationRequested();
        var storage = WindowsStorageSensorProvider.Capture(token);
        token.ThrowIfCancellationRequested();
        var acpi = AcpiThermalZoneProvider.Capture(token);
        token.ThrowIfCancellationRequested();
        return new(DateTimeOffset.UtcNow, baseSample, inventory, gpu, storage, acpi, wddm, dxgi);
    }
}
