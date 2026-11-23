using GerenciadorAplicativos;

internal static class HardwareSensorProviderTests
{
    internal static void Run(bool probeNative = false)
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Sensores: " + message);
        }

        // Exigências de interoperabilidade: resposta ausente nunca vira 0%.
        Check(NvidiaNvmlSensorProvider.Percent(null, "uso").Value is null &&
              NvidiaNvmlSensorProvider.Percent(101, "uso").Value is null &&
              NvidiaNvmlSensorProvider.Percent(0, "uso").Value == 0 &&
              NvidiaNvmlSensorProvider.Percent(100, "uso").Value == 100,
              "uso da GPU fora dos limites/nulo foi aceito");
        Check(NvidiaNvmlSensorProvider.Temperature(null).Value is null &&
              NvidiaNvmlSensorProvider.Temperature(0).Value is null &&
              NvidiaNvmlSensorProvider.Temperature(126).Value is null &&
              NvidiaNvmlSensorProvider.Temperature(55).Value == 55,
              "temperatura NVML precisa de leitura comprovada");
        Check(NvidiaNvmlSensorProvider.Watts(null).Value is null &&
              NvidiaNvmlSensorProvider.Watts(0).Value is null &&
              NvidiaNvmlSensorProvider.Watts(1_000_001).Value is null &&
              NvidiaNvmlSensorProvider.Watts(47_500).Value == 47.5,
              "conversão mW/W errada");
        Check(NvidiaNvmlSensorProvider.IsPlausibleMemory(8_000, 6_000, 2_000) &&
              !NvidiaNvmlSensorProvider.IsPlausibleMemory(0, 0, 0) &&
              !NvidiaNvmlSensorProvider.IsPlausibleMemory(100, 105, 0) &&
              !NvidiaNvmlSensorProvider.IsPlausibleMemory(100, 10, 200),
              "VRAM não preservou limites de tamanho/uso");
        var paths = NvidiaNvmlSensorProvider.TrustedCandidatePaths(
            @"C:\Windows\System32", @"C:\Program Files");
        Check(paths.Length == 2 && paths.All(x => x.EndsWith("nvml.dll", StringComparison.OrdinalIgnoreCase)) &&
              !paths.Any(x => x.Contains("PATH", StringComparison.OrdinalIgnoreCase)) &&
              !paths.Any(x => x.StartsWith(".", StringComparison.Ordinal)),
              "carregamento NVML não deve percorrer diretórios arbitrários");

        Check(WindowsStorageSensorProvider.Temperature(null).Value is null &&
              WindowsStorageSensorProvider.Temperature(0).Value is null &&
              WindowsStorageSensorProvider.Temperature((byte)45).Value == 45 &&
              WindowsStorageSensorProvider.Temperature(126).Value is null,
              "temperatura de disco precisa de fonte explícita");
        Check(WindowsStorageSensorProvider.Wear((byte)0).Value is null &&
              WindowsStorageSensorProvider.Wear((byte)12).Value == 12 &&
              WindowsStorageSensorProvider.Wear(110).Value is null,
              "desgaste sem prova não pode ser estimado");
        Check(WindowsStorageSensorProvider.Hours(0).Value is null &&
              WindowsStorageSensorProvider.Hours((ushort)4321).Value == 4321 &&
              WindowsStorageSensorProvider.Hours(65536).Value is null,
              "horas fora de faixa não podem virar medição");
        var physical = new[]
        {
            new StorageIdentity("disk-A", "SSD A"),
            new StorageIdentity("disk-B", "SSD B")
        };
        var values = new[]
        {
            new StorageReliabilityRaw("disk-B", (byte)37, (byte)14, (ushort)50),
            new StorageReliabilityRaw("disk-A", (byte)42, (byte)21, (ushort)90)
        };
        var joined = WindowsStorageSensorProvider.Join(physical, values);
        Check(joined.State == HardwareDiscoveryState.Complete && joined.Disks.Count == 2 &&
              joined.Disks[0].TemperatureC.Value == 42 && joined.Disks[1].TemperatureC.Value == 37,
              "telemetria de discos foi associada por ordem em vez de DeviceId");
        var duplicated = WindowsStorageSensorProvider.Join(physical,
            values.Append(values[1]));
        Check(duplicated.State == HardwareDiscoveryState.Partial &&
              duplicated.Disks[0].TemperatureC.Value is null &&
              duplicated.Disks[1].TemperatureC.Value == 37,
              "DeviceId duplicado não pode ser escolhido arbitrariamente");
        var duplicatePhysical = WindowsStorageSensorProvider.Join(
            physical.Append(new StorageIdentity("disk-A", "Outro SSD")),
            values);
        Check(duplicatePhysical.State == HardwareDiscoveryState.Partial &&
              duplicatePhysical.Disks.All(d => d.DeviceId != "disk-A") &&
              duplicatePhysical.Disks.Any(d => d.DeviceId == "disk-B"),
              "DeviceId físico duplicado não pode atribuir sensores a um dos discos");
        var orphan = WindowsStorageSensorProvider.Join(physical,
            values.Append(new StorageReliabilityRaw("outro", 99, 1, 1)));
        Check(orphan.State == HardwareDiscoveryState.Partial,
            "telemetria sem dispositivo correspondente não é inventário completo");

        // Falha reproduzida em Windows Server 2025 na sonda real: WMI
        // retornou objeto de propriedade nulo e o binder lançou exceção.
        Check(AcpiThermalZoneProvider.ReadWmiValueOrNull(null, "InstanceName",
                out bool absentRow) is null && absentRow,
            "linha ACPI nula não pode derrubar o monitoramento");
        dynamic absentProperties = new System.Dynamic.ExpandoObject();
        absentProperties.Properties_ = null;
        Check(AcpiThermalZoneProvider.ReadWmiValueOrNull(
                (object)absentProperties, "CurrentTemperature",
                out bool absentCollection) is null && absentCollection,
            "coleção de propriedades WMI nula não pode derrubar o monitoramento");
        dynamic emptyPropertyCollection = new System.Dynamic.ExpandoObject();
        emptyPropertyCollection.Properties_ = new System.Dynamic.ExpandoObject();
        Check(AcpiThermalZoneProvider.ReadWmiValueOrNull(
                (object)emptyPropertyCollection, "CurrentTemperature",
                out bool unreadableProperty) is null && unreadableProperty,
            "propriedade ACPI inexistente deve ser inconclusiva");

        Check(AcpiThermalZoneProvider.ConvertTenthsKelvin(null).Value is null &&
              AcpiThermalZoneProvider.ConvertTenthsKelvin(0).Value is null &&
              AcpiThermalZoneProvider.ConvertTenthsKelvin(3001u).Value is { } degrees &&
              Math.Abs(degrees - 26.95) < 0.01 &&
              AcpiThermalZoneProvider.ConvertTenthsKelvin(5000u).Value is null,
              "temperatura ACPI deve converter décimos Kelvin sem atribuir a CPU");

        var nil = MonitorReading<double>.Unavailable("Nenhum sensor");
        var nilTime = MonitorReading<TimeSpan>.Unavailable("Nenhum contador");
        var gpuIdentity = new NvidiaGpuSensorSample(0, "GPU de teste", null,
            nil, nil, nil, nil, MonitorReading<ulong>.Unavailable("VRAM ausente"));
        var gpuValue = gpuIdentity with
        {
            TemperatureC = MonitorReading<double>.Available(48, "GPU teste")
        };
        var sample = new LiveHardwareSample(DateTimeOffset.UtcNow,
            nil, nil, null, nilTime, LiveHardwareSensors.Unavailable);
        var inventory = new HardwareDiscoverySnapshot(DateTimeOffset.UtcNow,
            HardwareDiscoveryState.Complete, [], "PnP teste", null);
        var emptyGpu = new NvidiaGpuSensorSnapshot(DateTimeOffset.UtcNow, false, [], "Sem NVML");
        var emptyStorage = WindowsStorageSensorProvider.Join(
            [new StorageIdentity("disk-Q", "SSD teste")], []);
        var emptyAcpi = new AcpiThermalZoneSnapshot(DateTimeOffset.UtcNow,
            HardwareDiscoveryState.Unavailable, [], "Sem zonas");
        var portfolio = new HardwareSensorPortfolio(DateTimeOffset.UtcNow, sample,
            inventory, emptyGpu, emptyStorage, emptyAcpi);
        Check(!portfolio.HasMeasuredGpu && !portfolio.HasMeasuredStorage,
            "sensores ausentes não podem gerar monitor disponível");
        var measured = portfolio with
        {
            NvidiaGpus = new NvidiaGpuSensorSnapshot(DateTimeOffset.UtcNow, true, [gpuValue], null),
            Storage = WindowsStorageSensorProvider.Join(
                [new StorageIdentity("disk-Q", "SSD teste")],
                [new StorageReliabilityRaw("disk-Q", (byte)39, (byte)8, (ushort)100)])
        };
        Check(measured.HasMeasuredGpu && measured.HasMeasuredStorage,
            "medidas legítimas devem aparecer individualizadas");

        if (probeNative && OperatingSystem.IsWindows())
        {
            var gpu = NvidiaNvmlSensorProvider.Capture();
            Check(gpu.Devices.Count <= 16 && gpu.CapturedAtUtc.Offset == TimeSpan.Zero,
                "snapshot NVML excedeu teto ou data UTC");
            Check(gpu.Devices.All(d => d.UsagePercent.Value is null ||
                (d.UsagePercent.Value >= 0 && d.UsagePercent.Value <= 100)),
                "GPU reportou porcentagem inválida");
            // Sem driver NVIDIA, GPU genérica permanece indisponível, não falha.
            var acpi = AcpiThermalZoneProvider.Capture(CancellationToken.None);
            Check(acpi.CapturedAtUtc.Offset == TimeSpan.Zero,
                "ACPI não preservou timestamp UTC");
            var disks = WindowsStorageSensorProvider.Capture(CancellationToken.None);
            Check(disks.CapturedAtUtc.Offset == TimeSpan.Zero &&
                  disks.Disks.All(x => !string.IsNullOrWhiteSpace(x.Name)),
                "Storage WMI produziu dados sem identificação");
            Console.WriteLine("SENSORES_PROBE gpu=" + gpu.Devices.Count +
                " storage=" + disks.Disks.Count + " estado=" + disks.State);
        }
        Console.WriteLine("OK sensores: NVIDIA NVML opcional e Storage WMI vinculados por identidade.");
    }
}
