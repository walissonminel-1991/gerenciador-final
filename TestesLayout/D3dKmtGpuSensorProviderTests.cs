using GerenciadorAplicativos;

internal static class D3dKmtGpuSensorProviderTests
{
    internal static void Run(bool probeNative = false)
    {
        static void Check(bool ok, string label)
        { if (!ok) throw new InvalidOperationException("GPU D3DKMT: " + label); }

        Check(D3dKmtGpuSensorProvider.VerifyWindowsAbi(),
            "structs Win32 D3DKMT com alinhamento incorreto");

        var amd = new WddmGpuKey(1, 0xA123, 0);
        var intel = new WddmGpuKey(2, 0xB123, 0);
        var nvidia = new WddmGpuKey(3, 0xC123, 0);
        var rows = new[]
        {
            new D3dKmtAdapterRaw(amd, 1_750_000_000UL, 1250, 845, 665,
                3400, 1050, true, true),
            new D3dKmtAdapterRaw(intel, 800_000_000UL, 0, 0, 0,
                0, 0, true, false),
            new D3dKmtAdapterRaw(nvidia, 18_000_000_000UL, 0, 1200, 530,
                2800, 950, true, true)
        };
        var all = D3dKmtGpuSensorProvider.Join([rows[2], rows[0], rows[1]]);
        Check(all.State == HardwareDiscoveryState.Complete && all.Adapters.Count == 3 &&
              all.WithoutPerformanceQuery == 0 &&
              all.WithoutCapabilitiesQuery == 1 &&
              all.ConfirmedPoweredOff == 0 &&
              all.WithoutAnyValidMetric == 0,
            "chaves por LUID/phys e fontes consultadas não podem misturar GPUs");
        var a = all.Adapters.Single(x => x.Key == amd);
        Check(a.TemperatureC.Value == 66.5 &&
              a.FanRpm.Value == 1250 &&
              a.RelativePowerPercent.Value == 84.5 &&
              a.MemoryFrequencyMhz.Value == 1750,
            "temperatura deci-C, RPM, potência relativa e Hz precisam de conversão correta");
        var i = all.Adapters.Single(x => x.Key == intel);
        Check(i.TemperatureC.Value is null && i.FanRpm.Value is null &&
              i.RelativePowerPercent.Value is null && i.MemoryFrequencyMhz.Value == 800,
            "sem caps e medição não pode inventar temperatura, ventoinha nem watts");
        var n = all.Adapters.Single(x => x.Key == nvidia);
        Check(n.FanRpm.Value == 0 && n.RelativePowerPercent.Value == 120 &&
              n.TemperatureC.Value == 53,
            "0 RPM com capacidades publicadas é fan-stop real, não sensor ausente");
        Check(!a.RelativePowerPercent.Source.Contains("watts", StringComparison.OrdinalIgnoreCase),
            "potência normalizada D3DKMT não representa W");

        var reject = D3dKmtGpuSensorProvider.Join([
            rows[0] with { FanRpm = 35_000, TemperatureTenthsC = 1800,
                PowerTenthsPercent = 9999, MemoryFrequencyHz = 0 },
            rows[1] with { HasPerformanceData = false }
        ]);
        Check(reject.Adapters.Count == 2 &&
              reject.Adapters.Single(x => x.Key == amd).TemperatureC.Value is null &&
              reject.Adapters.Single(x => x.Key == amd).FanRpm.Value is null &&
              reject.Adapters.Single(x => x.Key == amd).RelativePowerPercent.Value is null &&
              reject.Adapters.Single(x => x.Key == amd).MemoryFrequencyMhz.Value is null &&
              reject.Adapters.Single(x => x.Key == intel).MemoryFrequencyMhz.Value is null &&
              reject.State == HardwareDiscoveryState.Partial &&
              reject.WithoutPerformanceQuery == 1 &&
              reject.WithoutCapabilitiesQuery == 1 &&
              reject.ConfirmedPoweredOff == 0 &&
              reject.WithoutAnyValidMetric == 2,
            "valores impossíveis e falha de desempenho têm contagens independentes");
        var poweredOff = D3dKmtGpuSensorProvider.Join([
            rows[0] with { IsPoweredOn = false }
        ]);
        Check(poweredOff.State == HardwareDiscoveryState.Partial &&
              poweredOff.Adapters.Count == 1 &&
              poweredOff.Adapters[0].Key == amd &&
              poweredOff.Adapters[0].TemperatureC.Value is null &&
              poweredOff.Adapters[0].FanRpm.Value is null &&
              poweredOff.Adapters[0].RelativePowerPercent.Value is null &&
              poweredOff.Adapters[0].MemoryFrequencyMhz.Value is null &&
              poweredOff.ConfirmedPoweredOff == 1 &&
              poweredOff.WithoutPerformanceQuery == 0 &&
              poweredOff.WithoutAnyValidMetric == 1,
            "GPU desligada confirmada não equivale a consulta de desempenho ausente");
        var poweredMixed = D3dKmtGpuSensorProvider.Join([
            rows[0] with { IsPoweredOn = false }, rows[2]
        ]);
        Check(poweredMixed.State == HardwareDiscoveryState.Partial &&
              poweredMixed.Adapters.Single(x => x.Key == nvidia).TemperatureC.Value == 53 &&
              poweredMixed.Adapters.Single(x => x.Key == amd).TemperatureC.Value is null &&
              poweredMixed.ConfirmedPoweredOff == 1 &&
              poweredMixed.WithoutAnyValidMetric == 1,
            "GPU desligada não pode invalidar sensores de outro LUID");

        var unanswered = D3dKmtGpuSensorProvider.Join([
            rows[1] with { HasPerformanceData = false, IsPoweredOn = false }
        ]);
        Check(unanswered.WithoutPerformanceQuery == 1 &&
              unanswered.ConfirmedPoweredOff == 0 &&
              unanswered.WithoutAnyValidMetric == 1 &&
              unanswered.Adapters.Single().TemperatureC.Value is null,
            "GPU sem resposta D3DKMT não pode ser classificada como desligada");

        var duplicate = D3dKmtGpuSensorProvider.Join([rows[0], rows[0]]);
        Check(duplicate.State == HardwareDiscoveryState.Unavailable &&
              duplicate.Adapters.Count == 0,
            "mesmo LUID/phys duplicado deve ser recusado, não escolhido");
        var linked = D3dKmtGpuSensorProvider.Join([
            rows[0],
            rows[0] with { Key = amd with { Physical = 1 }, TemperatureTenthsC = 710 }
        ]);
        Check(linked.State == HardwareDiscoveryState.Complete &&
              linked.Adapters.Count == 2 &&
              linked.Adapters[0].TemperatureC.Value == 66.5 &&
              linked.Adapters[1].TemperatureC.Value == 71,
            "índices físicos de um LUID não podem compartilhar temperatura arbitrariamente");
        var sentinel = D3dKmtGpuSensorProvider.Join([
            rows[0] with { FanRpm = uint.MaxValue, TemperatureTenthsC = 0,
                PowerTenthsPercent = uint.MaxValue, MemoryFrequencyHz = ulong.MaxValue }
        ]);
        Check(sentinel.Adapters.Single().TemperatureC.Value is null &&
              sentinel.Adapters.Single().FanRpm.Value is null &&
              sentinel.Adapters.Single().RelativePowerPercent.Value is null &&
              sentinel.Adapters.Single().MemoryFrequencyMhz.Value is null,
            "sentinelas WDDM devem ficar indisponíveis");

        if (probeNative && OperatingSystem.IsWindows())
        {
            var dxgi = DxgiGpuIdentityProvider.Capture();
            var native = D3dKmtGpuSensorProvider.Capture(dxgi, CancellationToken.None);
            // Confirmar que nem mesmo o relatório agregado aceita GPU
            // inventada: cada LUID da sonda deve existir no DXGI físico desta
            // mesma consulta. Índices LDA são internos a cada LUID.
            var dxgiPhysical = dxgi.Adapters.Where(x => !x.IsSoftware).ToArray();
            Check(native.Adapters.GroupBy(x => x.Key).All(g => g.Count() == 1) &&
                  native.Adapters.All(x =>
                      dxgiPhysical.Count(d => d.LuidHigh == x.Key.LuidHigh &&
                          d.LuidLow == x.Key.LuidLow) == 1 && x.Key.Physical < 16),
                "D3DKMT nativo sem LUID DXGI inequívoco ou com índice físico inválido");
            Check(native.WithoutPerformanceQuery <= native.Adapters.Count &&
                  native.WithoutCapabilitiesQuery <= native.Adapters.Count &&
                  native.ConfirmedPoweredOff <= native.Adapters.Count &&
                  native.WithoutAnyValidMetric == native.Adapters.Count(x =>
                      !x.TemperatureC.Value.HasValue &&
                      !x.FanRpm.Value.HasValue &&
                      !x.RelativePowerPercent.Value.HasValue &&
                      !x.MemoryFrequencyMhz.Value.HasValue),
                "contagens D3DKMT de ausência inconsistentes com os sensores reportados");
            Check(native.CapturedAtUtc.Offset == TimeSpan.Zero &&
                  native.Adapters.Count <= 512 &&
                  native.Adapters.All(x =>
                      x.TemperatureC.Value is null or (>= 10 and <= 150) &&
                      x.FanRpm.Value is null or (>= 0 and <= 40_000) &&
                      x.RelativePowerPercent.Value is null or (>= 0 and <= 200)),
                "GPU D3DKMT nativo fora dos limites ou identidade impossível");
            Console.WriteLine("D3DKMT_GPU estado=" + native.State +
                "; dxgi_nao_software=" + dxgiPhysical.Length +
                "; dispositivos_consultados=" + native.Adapters.Count +
                "; temperaturas=" + native.Adapters.Count(x => x.TemperatureC.Value.HasValue) +
                "; ventoinhasRPM=" + native.Adapters.Count(x => x.FanRpm.Value.HasValue) +
                "; potenciasRelativas=" + native.Adapters.Count(x => x.RelativePowerPercent.Value.HasValue) +
                "; frequenciasMemoria=" + native.Adapters.Count(x => x.MemoryFrequencyMhz.Value.HasValue) +
                "; sem_temperatura=" + native.Adapters.Count(x => !x.TemperatureC.Value.HasValue) +
                "; sem_RPM=" + native.Adapters.Count(x => !x.FanRpm.Value.HasValue) +
                "; consulta_perf_ausente=" + native.WithoutPerformanceQuery +
                "; caps_ausentes=" + native.WithoutCapabilitiesQuery +
                "; desligadas_confirmadas=" + native.ConfirmedPoweredOff +
                "; sem_metrica_valida=" + native.WithoutAnyValidMetric +
                "; limitacao=" + (native.Limitation ?? "nenhuma"));
        }
        Console.WriteLine("OK monitoramento GPU D3DKMT: temperatura, RPM, potência relativa e clock por LUID/phys.");
    }
}
