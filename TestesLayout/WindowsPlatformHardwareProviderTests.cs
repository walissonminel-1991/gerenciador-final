using GerenciadorAplicativos;

internal static class WindowsPlatformHardwareProviderTests
{
    internal static void Run(bool probeNative = false)
    {
        static void Check(bool ok, string reason)
        {
            if (!ok) throw new InvalidOperationException("Plataforma hardware: " + reason);
        }

        Check(WindowsPlatformHardwareProvider.UInt("8589934592") == 8_589_934_592UL &&
              WindowsPlatformHardwareProvider.UInt(0UL) == 0UL &&
              WindowsPlatformHardwareProvider.UInt(null) is null &&
              WindowsPlatformHardwareProvider.UInt(-1) is null &&
              WindowsPlatformHardwareProvider.UInt(double.NaN) is null &&
              WindowsPlatformHardwareProvider.UInt(ulong.MaxValue) is null,
            "SMBIOS UInt64 deve rejeitar ausência, negativos e sentinelas");
        Check(WindowsPlatformHardwareProvider.Identity("  CPU0  ") == "CPU0" &&
              WindowsPlatformHardwareProvider.Identity("Unknown") is null &&
              WindowsPlatformHardwareProvider.Identity("To Be Filled By O.E.M.") is null &&
              WindowsPlatformHardwareProvider.Identity("DIMM\nB2") is null &&
              WindowsPlatformHardwareProvider.Identity("") is null,
            "identidade genérica ou contendo controles não pode ser confiável");

        var sockets = new[] {
            new WmiCpuSocketRaw("CPU0", "Ryzen teste", 8U, 16U, 4100U),
            new WmiCpuSocketRaw("CPU1", "Intel teste", 4U, 8U, 2600U)
        };
        var memory = new[] {
            new WmiRamModuleRaw("BANK0", "DIMM_A2", "8589934592", 3200U, 26U, 1_200U, 1_100U, 1_350U),
            new WmiRamModuleRaw("BANK1", "DIMM_B2", 8_589_934_592UL, 3200U, 26U, 0U, 0U, 0U)
        };
        var motherboards = new[] {
            new WmiMotherboardRaw("Base Board", "Fabricante exemplo", "Modelo A", "Rev 2")
        };
        var loads = new[] {
            new WmiLogicalCpuRaw("0", 33UL),
            new WmiLogicalCpuRaw("1", 0UL),
            new WmiLogicalCpuRaw("_Total", 50UL)
        };

        var complete = WindowsPlatformHardwareProvider.Join(
            sockets, memory, motherboards, loads);
        Check(complete.CpuSocketsState == HardwareDiscoveryState.Complete &&
              complete.RamModulesState == HardwareDiscoveryState.Complete &&
              complete.MotherboardsState == HardwareDiscoveryState.Complete &&
              complete.LogicalCpuState == HardwareDiscoveryState.Complete &&
              complete.CpuSockets.Count == 2 &&
              complete.CpuSockets[0].DeviceId == "CPU0" &&
              complete.CpuSockets[0].PhysicalCores == 8 &&
              complete.CpuSockets[0].LogicalProcessors == 16 &&
              complete.CpuSockets[0].ReportedCurrentMhz.Value == 4100 &&
              complete.RamModules.Count == 2 &&
              complete.RamModules[0].CapacityBytes.Value == 8_589_934_592 &&
              complete.RamModules[0].ConfiguredMhz.Value == 3200 &&
              complete.RamModules[0].SmbiosMemoryType == 26 &&
              complete.RamModules[0].ConfiguredMilliVolts.Value == 1200 &&
              complete.RamModules[0].ConfiguredMilliVolts.Source.Contains("SMBIOS", StringComparison.Ordinal) &&
              complete.RamModules[0].MinMilliVolts.Value == 1100 &&
              complete.RamModules[0].MaxMilliVolts.Value == 1350 &&
              complete.RamModules[1].MinMilliVolts.Value is null &&
              complete.RamModules[1].MaxMilliVolts.Value is null &&
              complete.RamModules[1].ConfiguredMilliVolts.Value is null &&
              complete.Motherboards.Count == 1 &&
              complete.Motherboards[0].Tag == "Base Board" &&
              complete.MeasuredLogicalCpuInstances == 2 &&
              complete.LogicalCpuInstances.Single(x => x.CounterInstance == "1")
                  .UsagePercent.Value == 0,
            "inventário CPU socket, DIMM, board e uso por CPU lógica devem preservar identidade");
        Check(complete.LogicalCpuInstances.All(x => x.CounterInstance != "_Total"),
            "contador _Total não equivale a processador lógico");

        var noCpu = WindowsPlatformHardwareProvider.Join([], memory, motherboards, loads,
            socketsOk: false);
        Check(noCpu.CpuSocketsState == HardwareDiscoveryState.Unavailable &&
              noCpu.RamModulesState == HardwareDiscoveryState.Complete,
            "falha no provedor de CPU não pode apagar RAM válida");

        var duplicateSocket = WindowsPlatformHardwareProvider.Join(
            sockets.Append(sockets[0]), memory, motherboards, loads);
        Check(duplicateSocket.CpuSocketsState == HardwareDiscoveryState.Partial &&
              duplicateSocket.CpuSockets.Single().DeviceId == "CPU1",
            "CPU WMI duplicada não pode ser atribuída arbitrariamente");

        var invalidVoltage = WindowsPlatformHardwareProvider.Join(sockets,
            [
                memory[0] with { ConfiguredVoltage = "25000" },
                memory[1] with { ConfiguredVoltage = -1L }
            ], motherboards, loads);
        Check(invalidVoltage.RamModulesState == HardwareDiscoveryState.Partial &&
              invalidVoltage.RamModules.All(x => x.ConfiguredMilliVolts.Value is null) &&
              invalidVoltage.RamModules.All(x => x.CapacityBytes.Value.HasValue),
            "tensão SMBIOS impossível ou negativa não pode apagar capacidade válida");
        var noVoltage = WindowsPlatformHardwareProvider.Join(sockets,
            [memory[0] with { ConfiguredVoltage = null },
             memory[1] with { ConfiguredVoltage = 0U }], motherboards, loads);
        Check(noVoltage.RamModulesState == HardwareDiscoveryState.Complete &&
              noVoltage.RamModules.All(x => x.ConfiguredMilliVolts.Value is null),
            "0 e null em ConfiguredVoltage indicam desconhecido, não 0 mV medidos");

        var noBounds = WindowsPlatformHardwareProvider.Join(sockets,
            [memory[0] with { MinVoltage = null, MaxVoltage = 0U }, memory[1]],
            motherboards, loads);
        Check(noBounds.RamModulesState == HardwareDiscoveryState.Complete &&
              noBounds.RamModules[0].MinMilliVolts.Value is null &&
              noBounds.RamModules[0].MaxMilliVolts.Value is null &&
              noBounds.RamModules[0].ConfiguredMilliVolts.Value == 1200,
            "limites SMBIOS ausentes não invalidam tensão configurada conhecida");

        var reversedBounds = WindowsPlatformHardwareProvider.Join(sockets,
            [memory[0] with { MinVoltage = 1_500U, MaxVoltage = 1_100U }, memory[1]],
            motherboards, loads);
        Check(reversedBounds.RamModulesState == HardwareDiscoveryState.Partial &&
              reversedBounds.RamModules[0].MinMilliVolts.Value is null &&
              reversedBounds.RamModules[0].MaxMilliVolts.Value is null &&
              reversedBounds.RamModules[0].ConfiguredMilliVolts.Value == 1200 &&
              reversedBounds.RamModules[0].CapacityBytes.Value.HasValue,
            "intervalo SMBIOS contraditório não pode criar leitura nem apagar dados independentes");

        var impossibleBounds = WindowsPlatformHardwareProvider.Join(sockets,
            [memory[0] with { MinVoltage = -1L, MaxVoltage = 9_000U }, memory[1]],
            motherboards, loads);
        Check(impossibleBounds.RamModulesState == HardwareDiscoveryState.Partial &&
              impossibleBounds.RamModules[0].MinMilliVolts.Value is null &&
              impossibleBounds.RamModules[0].MaxMilliVolts.Value is null &&
              impossibleBounds.RamModules[0].CapacityBytes.Value.HasValue,
            "limites de tensão impossíveis não podem derrubar capacidade RAM válida");

        var duplicateRam = WindowsPlatformHardwareProvider.Join(sockets,
            memory.Append(memory[0] with { Capacity = 16_000_000_000UL }),
            motherboards, loads);
        Check(duplicateRam.RamModulesState == HardwareDiscoveryState.Partial &&
              duplicateRam.RamModules.Single().DeviceLocator == "DIMM_B2",
            "slots repetidos devem ficar inconclusivos sem misturar tamanhos");

        var duplicateBoard = WindowsPlatformHardwareProvider.Join(sockets,
            memory, motherboards.Append(motherboards[0] with { Product = "Modelo B" }), loads);
        Check(duplicateBoard.MotherboardsState == HardwareDiscoveryState.Unavailable &&
              duplicateBoard.Motherboards.Count == 0,
            "placa-mãe com chave SMBIOS duplicada não pode receber modelo arbitrário");

        var badValues = WindowsPlatformHardwareProvider.Join(
            [new WmiCpuSocketRaw("CPU0", "CPU", 0U, 9_000UL, 50U)],
            [new WmiRamModuleRaw("BANK1", "DIMM_B1", 0UL, 0U, 0U)],
            [new WmiMotherboardRaw("Base Board", "Default string", "Unknown", null)],
            [new WmiLogicalCpuRaw("0", 101UL),
             new WmiLogicalCpuRaw("1", 0UL),
             new WmiLogicalCpuRaw("0,0", 42UL),
             new WmiLogicalCpuRaw("_Total", 50UL)]);
        Check(badValues.CpuSocketsState == HardwareDiscoveryState.Partial &&
              badValues.CpuSockets[0].ReportedCurrentMhz.Value is null &&
              badValues.CpuSockets[0].PhysicalCores is null &&
              badValues.RamModulesState == HardwareDiscoveryState.Partial &&
              badValues.RamModules[0].CapacityBytes.Value is null &&
              badValues.RamModules[0].ConfiguredMhz.Value is null &&
              badValues.MotherboardsState == HardwareDiscoveryState.Partial &&
              badValues.Motherboards[0].Manufacturer is null &&
              badValues.LogicalCpuState == HardwareDiscoveryState.Partial &&
              badValues.LogicalCpuInstances.Count == 2 &&
              badValues.LogicalCpuInstances[0].UsagePercent.Value is null &&
              badValues.LogicalCpuInstances[1].UsagePercent.Value == 0,
            "SMBIOS nulo/fora do limite deve ficar indisponível sem descartar as outras fontes");

        var duplicateLoad = WindowsPlatformHardwareProvider.Join(sockets, memory,
            motherboards, loads.Append(new WmiLogicalCpuRaw("1", 95UL)));
        Check(duplicateLoad.LogicalCpuState == HardwareDiscoveryState.Partial &&
              duplicateLoad.LogicalCpuInstances.Count == 1 &&
              duplicateLoad.LogicalCpuInstances[0].CounterInstance == "0",
            "mesma instância de CPU lógica duplicada impede atribuição de percentual");

        if (probeNative && OperatingSystem.IsWindows())
        {
            var native = WindowsPlatformHardwareProvider.Capture(CancellationToken.None);
            Check(native.CapturedAtUtc.Offset == TimeSpan.Zero &&
                  native.CpuSockets.Count <= 64 &&
                  native.RamModules.Count <= 128 &&
                  native.Motherboards.Count <= 16 &&
                  native.LogicalCpuInstances.Count <= 512 &&
                  native.LogicalCpuInstances.All(x => x.UsagePercent.Value is null ||
                      x.UsagePercent.Value is >= 0 and <= 100),
                "sonda Windows WMI produziu contagens ou valores impossíveis");
            Console.WriteLine("PLATAFORMA consulta cpu_sockets=" + native.CpuSockets.Count +
                " dimms=" + native.RamModules.Count +
                " dimms_voltagem_configurada=" +
                    native.RamModules.Count(x => x.ConfiguredMilliVolts.Value.HasValue) +
                " dimms_faixa_tensao=" + native.RamModules.Count(x =>
                    x.MinMilliVolts.Value.HasValue && x.MaxMilliVolts.Value.HasValue) +
                " boards=" + native.Motherboards.Count +
                " cpu_instances=" + native.MeasuredLogicalCpuInstances);
        }
        Console.WriteLine("OK monitoramento plataforma: CPU sockets, DIMMs, placa-mãe e CPU lógica sem inventar temperaturas.");
    }
}
