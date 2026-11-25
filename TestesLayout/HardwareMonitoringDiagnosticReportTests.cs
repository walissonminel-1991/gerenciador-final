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
              summary.Contains("CPU sockets WMI: indisponível", StringComparison.Ordinal) &&
              summary.Contains("CPU lógica WMI: indisponível", StringComparison.Ordinal) &&
              summary.Contains("Módulos RAM SMBIOS: indisponível", StringComparison.Ordinal) &&
              summary.Contains("Placa-mãe WMI: indisponível", StringComparison.Ordinal) &&
              summary.Contains("DXGI: indisponível", StringComparison.Ordinal) &&
              summary.Contains("GPU D3DKMT (LUID + índice físico): indisponível", StringComparison.Ordinal) &&
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
              confirmed.Contains("adaptadores não software: 1", StringComparison.Ordinal) &&
              !confirmed.Contains("adaptadores físicos:", StringComparison.Ordinal) &&
              confirmed.Contains("D3DKMT sem métricas válidas é inconclusivo", StringComparison.Ordinal) &&
              !confirmed.Contains("indisponíveis sem provedor dedicado", StringComparison.Ordinal) &&
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
        var modules = WindowsPlatformHardwareProvider.Join(
            [new WmiCpuSocketRaw("CPU0", "CPU pessoal secreto", 8U, 16U, 3950U)],
            [new WmiRamModuleRaw("BANK0", "DIMM-SECRETO-1",
                8_589_934_592UL, 3600U, 26U, 1234U, 1000U, 1500U)],
            [new WmiMotherboardRaw("Base Board", "Fabricante confidencial",
                "Modelo placa pessoal 123", "Rev 1")],
            [new WmiLogicalCpuRaw("0", 17UL), new WmiLogicalCpuRaw("_Total", 40UL)]);
        var extended = HardwareMonitoringDiagnosticReport.Format(measured with
        {
            PlatformHardware = modules
        });
        Check(extended.Contains("CPU sockets WMI: completo; sockets identificados: 1; frequências reportadas: 1",
                  StringComparison.Ordinal) &&
              extended.Contains("CPU lógica WMI: completo; instâncias observadas: 1; com uso reportado: 1",
                  StringComparison.Ordinal) &&
              extended.Contains("Módulos RAM SMBIOS: completo; módulos identificados: 1; capacidades informadas: 1; clocks configurados: 1; tensões SMBIOS configuradas em mV: 1",
                  StringComparison.Ordinal) &&
              extended.Contains("faixas operacionais SMBIOS completas: 1", StringComparison.Ordinal) &&
              !extended.Contains("1234", StringComparison.Ordinal) &&
              !extended.Contains("1000", StringComparison.Ordinal) &&
              !extended.Contains("1500", StringComparison.Ordinal) &&
              extended.Contains("Placa-mãe WMI: completo; placas identificadas: 1; fabricante/modelo informados: 1",
                  StringComparison.Ordinal) &&
              !extended.Contains("CPU pessoal secreto", StringComparison.Ordinal) &&
              !extended.Contains("DIMM-SECRETO-1", StringComparison.Ordinal) &&
              !extended.Contains("Fabricante confidencial", StringComparison.Ordinal) &&
              !extended.Contains("Modelo placa pessoal 123", StringComparison.Ordinal),
            "relatório SMBIOS deve expor somente contagens, não identidades físicas");

        var sensors = D3dKmtGpuSensorProvider.Join([
            new D3dKmtAdapterRaw(new WddmGpuKey(1, 1234, 0),
                1_750_000_000UL, 1500, 825, 680, 2900, 1060, true, true)
        ]);
        string joined = HardwareMonitoringDiagnosticReport.Format(measured with
        {
            D3dKmtGpuSensors = sensors
        });
        Check(joined.Contains("GPU D3DKMT (LUID + índice físico): completo; adaptadores consultados: 1; temperaturas reais: 1; ventoinhas em RPM: 1; potência relativa em %: 1 (não são watts); frequências memória MHz: 1", StringComparison.Ordinal) &&
              !joined.Contains("1750", StringComparison.Ordinal) &&
              !joined.Contains("1500", StringComparison.Ordinal) &&
              !joined.Contains("825", StringComparison.Ordinal) &&
              !joined.Contains("680", StringComparison.Ordinal) &&
              !joined.Contains("luid_0x", StringComparison.OrdinalIgnoreCase),
            "relatório D3DKMT deve revelar somente contagens, sem LUID nem valores individuais");

        var noResponse = D3dKmtGpuSensorProvider.Join([
            new D3dKmtAdapterRaw(new WddmGpuKey(1, 1234, 0),
                0, 0, 0, 0, 0, 0, false, false, false)
        ]);
        string diagnostic = HardwareMonitoringDiagnosticReport.Format(measured with
        {
            D3dKmtGpuSensors = noResponse
        });
        Check(diagnostic.Contains("consulta desempenho ausente: 1; capacidades ausentes: 1; GPUs desligadas confirmadas: 0; adaptadores sem métrica válida: 1", StringComparison.Ordinal) &&
              !diagnostic.Contains("luid_0x", StringComparison.OrdinalIgnoreCase) &&
              !diagnostic.Contains("GPU-UUID-test", StringComparison.Ordinal),
            "sem resposta D3DKMT não pode ser classificada como GPU desligada nem revelar identificador");

        Check(confirmed.Contains("não é RPM", StringComparison.Ordinal) &&
              confirmed.Contains("latências máximas leitura: 0", StringComparison.Ordinal) &&
              confirmed.Contains("CPU/chipset/VRM: indisponíveis", StringComparison.Ordinal),
            "saída atribuiu RPM ou sensor ACPI de CPU sem comprovação");
        var typedHardware=measured with
        {
            PlatformHardware=WindowsPlatformHardwareProvider.Join(
                [new WmiCpuSocketRaw("CPU-ID-PRIVADO","AMD Ryzen 7 5700X",8U,16U,3950U)],
                [new WmiRamModuleRaw("BANK-PRIVADO","SLOT-PRIVADO",8_589_934_592UL,3600U,26U,
                    Manufacturer:"Kingston",PartNumber:"KF436C16BB")],
                [new WmiMotherboardRaw("TAG-PRIVADO","ASUS","TUF GAMING B550-PLUS","Rev 1")],[]),
            Storage=WindowsStorageSensorProvider.Join(
                [new StorageIdentity("SERIAL-PRIVADO","FRIENDLY-PRIVADO","Samsung","SSD 980 PRO")],[])
        };
        string configuration=LocalDiagnosticReport.FormatHardware(typedHardware);
        Check(configuration.Contains("AMD Ryzen 7 5700X")&&configuration.Contains("Kingston")&&
            configuration.Contains("KF436C16BB")&&configuration.Contains("TUF GAMING B550-PLUS")&&
            configuration.Contains("SSD 980 PRO")&&configuration.Contains("GPU Intel de exemplo")&&
            !configuration.Contains("PRIVADO")&&!configuration.Contains(secretDevice)&&
            !configuration.Contains("GPU-UUID-test"),"relatório manual expõe apenas campos técnicos de modelos e configuração");
        var session=new LocalDiagnosticReport();
        session.SampleHardware(typedHardware);
        string readings=session.Format();
        Check(readings.Contains("MONITOR fonte=NVML")&&readings.Contains("temperatura_c=65")&&
            readings.Contains("potencia_w=Unavailable")&&!readings.Contains("GPU-UUID-test"),
            "amostras de hardware preservam indisponibilidade e não exportam identificadores");
        Console.WriteLine("OK monitoramento: resumo consultivo sem identificadores e sensores não presumidos.");
    }
}
