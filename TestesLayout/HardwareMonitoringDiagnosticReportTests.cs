using GerenciadorAplicativos;

internal static class HardwareMonitoringDiagnosticReportTests
{
    internal static void Run()
    {
        static void Check(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException("Relatório monitoramento: " + reason); }

        var missingCpu = MonitorReading<double>.Unavailable("CPU indisponível");
        var missingUptime = MonitorReading<TimeSpan>.Unavailable("Uptime indisponível");
        var cpu = new LiveHardwareSample(DateTimeOffset.UtcNow, missingCpu, missingCpu,
            null, missingUptime, LiveHardwareSensors.Unavailable);
        const string secretDevice = @"PCI\VEN_8086&DEV_1234&SUBSYS_12345678\local-test-ID";
        var hardware = new HardwareDeviceDescriptor(secretDevice, "Modelo pessoal XYZ",
            "Fabricante interno", "Display", "PCI", HardwareDeviceKind.Graphics);
        var pnp = new HardwareDiscoverySnapshot(DateTimeOffset.UtcNow,
            HardwareDiscoveryState.Complete, [hardware], "PnP", null);
        var gpu = new NvidiaGpuSensorSnapshot(DateTimeOffset.UtcNow,
            false, [], "Sem driver NVML");
        var disk = WindowsStorageSensorProvider.Join(
            [new StorageIdentity("serial-interno-do-disco", "SSD XYZ")], []);
        var acpi = new AcpiThermalZoneSnapshot(DateTimeOffset.UtcNow,
            HardwareDiscoveryState.Unavailable, [], "Não exposto");

        var unavailable = new HardwareSensorPortfolio(DateTimeOffset.UtcNow,
            cpu, pnp, gpu, disk, acpi);
        string summary = HardwareMonitoringDiagnosticReport.Format(unavailable);
        Check(summary.Contains("CPU uso: indisponível", StringComparison.Ordinal) &&
              summary.Contains("RAM física: indisponível", StringComparison.Ordinal) &&
              summary.Contains("DXGI: indisponível", StringComparison.Ordinal) &&
              summary.Contains("GPU WDDM: indisponível", StringComparison.Ordinal) &&
              summary.Contains("NVIDIA NVML: indisponível", StringComparison.Ordinal) &&
              summary.Contains("Armazenamento WMI: completo", StringComparison.Ordinal) &&
              summary.Contains("Confiabilidade Storage (contadores reportados, não saúde geral): erros leitura totais: 0", StringComparison.Ordinal) &&
              summary.Contains("Zonas térmicas ACPI: indisponível", StringComparison.Ordinal),
            "fontes indisponíveis não foram distinguidas");
        Check(!summary.Contains(secretDevice, StringComparison.Ordinal) &&
              !summary.Contains("Modelo pessoal XYZ", StringComparison.Ordinal) &&
              !summary.Contains("serial-interno-do-disco", StringComparison.Ordinal) &&
              !summary.Contains("SSD XYZ", StringComparison.Ordinal),
            "saída sem identificadores expôs dados pessoais do inventário");

        var gpuId = new DxgiGpuIdentity(1, 1234, "GPU Intel de exemplo",
            0x8086, 0x1234, 0x12345678, 2048, false);
        var wddm = WindowsWddmGpuProvider.Join(
            [new WddmEngineCounterRow(
                "pid_123_luid_0x00000001_0x000004D2_phys_0_eng_0_engtype_3D", 27UL)],
            [new WddmMemoryCounterRow("luid_0x00000001_0x000004D2_phys_0", 1_000UL, 300UL)]);
        var measured = unavailable with
        {
            DxgiAdapters = new DxgiGpuSnapshot(DateTimeOffset.UtcNow,
                HardwareDiscoveryState.Complete, [gpuId], null),
            WindowsGpuCounters = wddm,
            NvidiaGpus = new NvidiaGpuSensorSnapshot(DateTimeOffset.UtcNow,
                true, [new NvidiaGpuSensorSample(0, "NVIDIA de exemplo", "GPU-UUID-test",
                    MonitorReading<double>.Available(70, "NVML"),
                    MonitorReading<double>.Available(65, "NVML"),
                    MonitorReading<double>.Unavailable("watts"),
                    MonitorReading<double>.Unavailable("fan"),
                    MonitorReading<ulong>.Available(100, "NVML"))], null)
        };
        string confirmed = HardwareMonitoringDiagnosticReport.Format(measured);
        Check(confirmed.Contains("Intel 1", StringComparison.Ordinal) &&
              confirmed.Contains("com engine observado: 1", StringComparison.Ordinal) &&
              confirmed.Contains("motores 3D: 1", StringComparison.Ordinal) &&
              confirmed.Contains("motores de cópia: 0", StringComparison.Ordinal) &&
              confirmed.Contains("motores decode de vídeo: 0", StringComparison.Ordinal) &&
              confirmed.Contains("DXGI inequívocos: 1", StringComparison.Ordinal) &&
              confirmed.Contains("PnP inequívocos: 1", StringComparison.Ordinal) &&
              confirmed.Contains("temperatura: 1; potência: 0; VRAM: 1", StringComparison.Ordinal),
            "métricas por GPU e vínculo não foram agregados corretamente");
        Check(!confirmed.Contains("GPU-UUID-test", StringComparison.Ordinal) &&
              !confirmed.Contains("GPU Intel de exemplo", StringComparison.Ordinal) &&
              !confirmed.Contains("NVIDIA de exemplo", StringComparison.Ordinal),
            "saída de diagnóstico vazou nomes ou UUID de GPUs");
        Check(confirmed.Contains("não é RPM", StringComparison.Ordinal) &&
              confirmed.Contains("latências máximas leitura: 0", StringComparison.Ordinal) &&
              confirmed.Contains("CPU/chipset/VRM: indisponíveis", StringComparison.Ordinal),
            "saída atribuiu RPM ou sensor ACPI de CPU sem comprovação");
        Console.WriteLine("OK monitoramento: resumo consultivo sem identificadores e sensores não presumidos.");
    }
}
