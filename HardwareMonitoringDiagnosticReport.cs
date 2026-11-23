namespace GerenciadorAplicativos;

// Relatório consultivo de homologação: não mostra DeviceId, PNPDeviceID,
// UUID, nome de usuário, serial de disco, nome de placa nem caminho local.
// As contagens são de métricas efetivamente reportadas, não de sensores
// disponíveis em tese. Não faz escrita, coleta remota ou configuração.
internal static class HardwareMonitoringDiagnosticReport
{
    private static string State(MonitorAvailability state) => state switch
    {
        MonitorAvailability.Available => "disponível",
        MonitorAvailability.WarmingUp => "medindo",
        _ => "indisponível"
    };

    private static string State(HardwareDiscoveryState state) => state switch
    {
        HardwareDiscoveryState.Complete => "completo",
        HardwareDiscoveryState.Partial => "parcial",
        _ => "indisponível"
    };

    private static int WithValue<T>(IEnumerable<MonitorReading<T>> readings) where T : struct =>
        readings.Count(x => x.Availability == MonitorAvailability.Available && x.Value.HasValue);

    internal static string Format(HardwareSensorPortfolio portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        var dxgi = portfolio.DxgiAdapters;
        var wddm = portfolio.WindowsGpuCounters;

        // Atribuição de fabricante somente pelo identificador DXGI, nunca pelo
        // nome da primeira GPU, pelo barramento ou pela posição da amostra.
        var physical = dxgi?.Adapters.Where(d => !d.IsSoftware).ToArray() ?? [];
        int amd = physical.Count(d => d.VendorId == 0x1002);
        int intel = physical.Count(d => d.VendorId == 0x8086);
        int nvidia = physical.Count(d => d.VendorId == 0x10DE);
        int other = physical.Length - amd - intel - nvidia;

        var gpu = portfolio.NvidiaGpus.Devices;
        var storage = portfolio.Storage.Disks;
        var acpi = portfolio.AcpiPlatform.Zones;
        var matched = portfolio.MatchedGpuDevices;

        var lines = new List<string>
        {
            "GERENCIADOR FINAL — MONITORAMENTO — DIAGNÓSTICO LOCAL SEM IDENTIFICADORES",
            "Somente leitura; nenhum driver instalado, alterado ou removido.",
            "Métricas disponíveis significam resposta da fonte, não homologação universal.",
            "CPU uso: " + State(portfolio.WindowsCounters.CpuUsagePercent.Availability) +
                "; frequência reportada: " + State(portfolio.WindowsCounters.CpuFrequencyMhz.Availability) +
                "; uptime: " + State(portfolio.WindowsCounters.SystemUptime.Availability),
            "RAM física: " + (portfolio.WindowsCounters.Memory is { TotalBytes: > 0 }
                ? "disponível" : "indisponível"),
            "Inventário PnP: " + State(portfolio.DeviceInventory.State) +
                "; dispositivos observados: " + portfolio.DeviceInventory.DeviceCount,
            "DXGI: " + State(dxgi?.State ?? HardwareDiscoveryState.Unavailable) +
                "; adaptadores físicos: " + physical.Length +
                " (AMD " + amd + ", Intel " + intel + ", NVIDIA " + nvidia +
                ", outros " + other + ")",
            "GPU WDDM: " + State(wddm?.State ?? HardwareDiscoveryState.Unavailable) +
                "; adaptadores: " + (wddm?.Adapters.Count ?? 0) +
                "; com engine observado: " +
                    WithValue(wddm?.Adapters.Select(x => x.BusiestEnginePercent) ?? []) +
                "; memória dedicada: " +
                    WithValue(wddm?.Adapters.Select(x => x.DedicatedUsedBytes) ?? []) +
                "; motores 3D: " +
                    WithValue(wddm?.Adapters.Select(x => x.BusiestOfType("3D")) ?? []) +
                "; motores de cópia: " +
                    WithValue(wddm?.Adapters.Select(x => x.BusiestOfType("COPY")) ?? []) +
                "; motores compute: " +
                    WithValue(wddm?.Adapters.Select(x => x.BusiestOfType("COMPUTE")) ?? []) +
                "; motores decode de vídeo: " +
                    WithValue(wddm?.Adapters.Select(x => x.BusiestOfType("VIDEODECODE")) ?? []) +
                "; motores encode de vídeo: " +
                    WithValue(wddm?.Adapters.Select(x => x.BusiestOfType("VIDEOENCODE")) ?? []) +
                "; DXGI inequívocos: " + matched.Count(x => x.DxgiDescription is not null) +
                "; PnP inequívocos: " + matched.Count(x => x.PnpInstanceId is not null),
            "NVIDIA NVML: " + (portfolio.NvidiaGpus.ProviderAvailable ? "disponível" : "indisponível") +
                "; GPUs: " + gpu.Count +
                "; uso: " + WithValue(gpu.Select(x => x.UsagePercent)) +
                "; temperatura: " + WithValue(gpu.Select(x => x.TemperatureC)) +
                "; potência: " + WithValue(gpu.Select(x => x.PowerWatts)) +
                "; VRAM: " + WithValue(gpu.Select(x => x.VramUsedBytes)) +
                "; ventoinha em %: " + WithValue(gpu.Select(x => x.FanPercent)) +
                " (não é RPM)",
            "Armazenamento WMI: " + State(portfolio.Storage.State) +
                "; discos: " + storage.Count +
                "; temperatura: " + WithValue(storage.Select(x => x.TemperatureC)) +
                "; desgaste: " + WithValue(storage.Select(x => x.WearPercent)) +
                "; horas: " + WithValue(storage.Select(x => x.PowerOnHours)),
            "Zonas térmicas ACPI: " + State(portfolio.AcpiPlatform.State) +
                "; zonas: " + acpi.Count +
                "; temperaturas válidas: " + WithValue(acpi.Select(x => x.TemperatureC)),
            "Temperaturas de CPU/chipset/VRM: indisponíveis sem sensor associado e comprovado.",
            "AMD/Intel: temperatura de GPU, potência e RPM indisponíveis sem provedor dedicado.",
            "Não correlacionar UUID da NVML com LUID WDDM/DXGI sem evidência adicional."
        };
        return string.Join(Environment.NewLine, lines);
    }
}
