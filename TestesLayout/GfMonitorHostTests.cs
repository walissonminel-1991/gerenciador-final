using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class GfMonitorHostTests
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Host Monitor GF: " + message);
    }

    private static HardwareSensorPortfolio Fixture(DateTimeOffset now, WddmGpuKey key)
    {
        var cpu = new LiveHardwareSample(now, MonitorReading<double>.Available(14, "fixture"),
            MonitorReading<double>.Unavailable("fixture"), new MemoryRamSample(now.UtcDateTime, 1000, 580),
            MonitorReading<TimeSpan>.Unavailable("fixture"), LiveHardwareSensors.Unavailable);
        var inventory = new HardwareDiscoverySnapshot(now, HardwareDiscoveryState.Complete, [], "fixture", null);
        var nvml = new NvidiaGpuSensorSnapshot(now, false, [], "fixture");
        var disk = WindowsStorageSensorProvider.Join([], []);
        var acpi = new AcpiThermalZoneSnapshot(now, HardwareDiscoveryState.Unavailable, [], "fixture");
        var dxgi = new DxgiGpuSnapshot(now, HardwareDiscoveryState.Complete,
            [new DxgiGpuIdentity(key.LuidHigh, key.LuidLow, "fixture", 0x8086, 1, 1, 1000, false)], null);
        var wddm = new WddmGpuSnapshot(now, HardwareDiscoveryState.Complete,
            [new WddmGpuAdapterSample(key, MonitorReading<double>.Available(80, "fixture"),
                MonitorReading<ulong>.Available(750, "fixture"), MonitorReading<ulong>.Unavailable("fixture"))], null);
        var absent = MonitorReading<double>.Unavailable("fixture");
        var thermal = new D3dKmtGpuSnapshot(now, HardwareDiscoveryState.Complete,
            [new D3dKmtGpuReading(key, MonitorReading<double>.Available(70, "fixture"), absent, absent, absent)], null);
        return new(now, cpu, inventory, nvml, disk, acpi, wddm, dxgi, null, thermal);
    }

    private static void VerifyPortfolio(DateTimeOffset now, WddmGpuKey key)
    {
        var portfolio = Fixture(now, key);
        var newer = portfolio with { WindowsCounters = portfolio.WindowsCounters with
            { CapturedAtUtc = now.AddSeconds(8), Memory = new MemoryRamSample(now.AddSeconds(8).UtcDateTime, 1000, 500) } };
        var s = GfResourceMonitorAdapter.FromPortfolio(newer, key, now.AddSeconds(8));
        Require(s.CpuPercent == 14 && s.RamPercent == 50 && s.GpuPercent == 80 &&
            s.VramPercent == 75 && s.GpuTemperature == 70, "portfólio com relógio antigo mantém fontes válidas");
        Require(s.MetricTimes?.GpuTemperature == now && s.MetricTimes?.Cpu == now.AddSeconds(8) &&
            s.MetricTimes?.GpuIdentity == now, "instantes reais por fonte e identidade");
        var retainedIdentity = portfolio with
        {
            WindowsGpuCounters = portfolio.WindowsGpuCounters! with { CapturedAtUtc = now.AddSeconds(8) },
            D3dKmtGpuSensors = portfolio.D3dKmtGpuSensors! with { CapturedAtUtc = now.AddSeconds(8) },
            WindowsCounters = newer.WindowsCounters
        };
        var retained = GfResourceMonitorAdapter.FromPortfolio(retainedIdentity, key, now.AddSeconds(8));
        Require(retained.GpuTemperature == 70 && retained.MetricTimes?.GpuTemperature == now.AddSeconds(8),
            "contadores novos com identidade ainda válida");
        var identityExpired = GfMonitorMetricTimes.FreshAt(retained, now.AddSeconds(11));
        Require(identityExpired.GpuIdentity is null && identityExpired.GpuPercent is null &&
            identityExpired.VramPercent is null && identityExpired.GpuTemperature is null &&
            identityExpired.CpuPercent == 14, "identidade vencida não é renovada por fonte de dados");
        s = GfResourceMonitorAdapter.FromPortfolio(newer, key, now.AddSeconds(11));
        Require(s.CpuPercent == 14 && s.RamPercent == 50 && s.GpuPercent is null && s.GpuTemperature is null,
            "CPU nova não renova identidade e GPU vencidas");
        s = GfResourceMonitorAdapter.FromPortfolio(portfolio with { WindowsGpuCounters = null }, key, now);
        Require(s.GpuPercent is null && s.GpuTemperature == 70, "falha WDDM não apaga sensor D3DKMT independente");
        Require(GfResourceMonitorAdapter.FromPortfolio(portfolio, new WddmGpuKey(9, 9, 0), now).GpuTemperature is null,
            "não cruzar identidade térmica");
    }

    // Percurso completo do host: produtor existente -> portfólio -> relay ->
    // janela nativa -> preferências. Somente fixtures; sem leitura de hardware.
    private static void VerifyWindowLifecycle(string path, WddmGpuKey key)
    {
        GfResourceMonitorForm Window() => Application.OpenForms.OfType<GfResourceMonitorForm>().Single();
        Label Metric(int index) => (Label)Window().Controls.Find("MonitorCompactMetric" + index, true).Single();
        var now = DateTimeOffset.UtcNow;
        var enabled = GfResourceMonitorPreferences.Default with { Enabled = true, OpacityPercent = 75 };
        using (var host = new GfMonitorHost(path))
        {
            var generation = host.BeginGeneration(key);
            var caps = GfThermalEvidence.FromD3dKmt(key, generation, key, generation, true, 700, 800, now);
            Require(host.Publish(generation, Fixture(now, key), now, caps), "publicar antes da janela");
            Require(host.Apply(enabled, now), "abrir monitor pelo host");
            Application.DoEvents();
            Require(Metric(0).Text == "CPU 14%" && Metric(1).Text == "GPU 80%" &&
                Metric(2).Text == "RAM 42%" && Metric(5).Text == "GPU 70°C",
                "replay apresenta dados de todas as fontes disponíveis");
            Require(Metric(5).ForeColor == GfMeterScale.Colors(V269Theme.AzulGf).Yellow,
                "evidência do produtor chega ao aviso térmico da janela");
            var newer = now.AddSeconds(1);
            var p = Fixture(newer, key);
            p = p with { WindowsCounters = p.WindowsCounters with
                { CpuUsagePercent = MonitorReading<double>.Available(25, "fixture") } };
            Require(host.Publish(generation, p, newer,
                GfThermalEvidence.FromD3dKmt(key, generation, key, generation, true, 700, 800, newer)) &&
                Metric(0).Text == "CPU 25%", "publicação chega à janela aberta");
            Window().Close();
            Require(!host.WantsCollection(false, true) && !host.Preferences().Enabled &&
                !GfResourceMonitorPreferencesStore.Load(path).Enabled, "fechar manualmente desabilita e persiste");
            Require(host.Apply(enabled, newer), "reabertura explícita");
            Require(Application.OpenForms.OfType<GfResourceMonitorForm>().Count() == 1,
                "não criar janelas duplicadas");
            // Descarte pelo shell deve preservar habilitação para restauração.
        }
        Require(!Application.OpenForms.OfType<GfResourceMonitorForm>().Any() &&
            GfResourceMonitorPreferencesStore.Load(path).Enabled, "encerramento libera janela e conserva opção");
        using (var restored = new GfMonitorHost(path))
        {
            Require(!Application.OpenForms.OfType<GfResourceMonitorForm>().Any() &&
                !restored.WantsCollection(false, true), "construtor não abre ou coleta implicitamente");
            restored.Restore(DateTimeOffset.UtcNow);
            Require(Window().Visible && restored.WantsCollection(false, true), "restauração explícita do shell");
            Require(restored.Apply(enabled with { Enabled = false }, DateTimeOffset.UtcNow),
                "limpar fixture de preferência");
        }
        Require(!Application.OpenForms.OfType<GfResourceMonitorForm>().Any(), "nenhuma janela residual");
    }

    internal static void Run()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var key = new WddmGpuKey(1, 2, 0);
        var old = now.AddSeconds(-11);
        var sample = new GfResourceMonitorSnapshot(now, 14, 90, 42, 80, 48, 75,
            GpuIdentity: key.ToString(), MetricTimes: new(now, old, now, old, now, old));
        var fresh = GfMonitorMetricTimes.FreshAt(sample, now);
        Require(fresh.CpuPercent == 14 && fresh.RamPercent == 42 && fresh.CpuTemperature == 48 &&
            fresh.GpuPercent is null && fresh.VramPercent is null && fresh.GpuTemperature is null,
            "entrega nova não renova GPU antiga");
        // Benchmark estrutural determinístico: sem relógio de desempenho
        // dependente do runner, não clonar em nenhuma passagem ociosa.
        var stable = fresh;
        for (int i = 0; i < 10_000; i++)
            if (!ReferenceEquals(stable, GfMonitorMetricTimes.FreshAt(stable, now)))
                throw new InvalidOperationException("Monitor GF: alocação de snapshot ocioso");
        Require(GfMonitorMetricTimes.FreshAt(sample with { MetricTimes = sample.MetricTimes! with { Cpu = null } }, now).CpuPercent is null,
            "timestamp ausente não herda entrega");
        var caps = GfThermalEvidence.FromD3dKmt(key, 1, key, 1, true, 700, 800, now.AddSeconds(-4));
        var thermal = sample with { Generation = 1, GpuTemperature = 70, GpuThermalEvidence = caps,
            MetricTimes = sample.MetricTimes! with { GpuTemperature = now.AddSeconds(-5) } };
        Require(new GfThermalPresentation().Evidence(thermal, now) is null, "capacidade posterior ao sensor não é comprovada");
        using (var relay = new GfMonitorSnapshotRelay())
        {
            relay.BeginGeneration(1);
            var cached = sample with { MetricTimes = new(now, now.AddSeconds(-9), now, now.AddSeconds(-9), now, now.AddSeconds(-9)) };
            Require(relay.Publish(1, cached, now), "publicação parcial");
            using var binding = relay.Attach(s => Require(s.CpuPercent == 14 && s.GpuTemperature is null && s.GpuPercent is null,
                "replay expira fontes independentemente"), now.AddSeconds(2), () => throw new Exception("replay global ainda fresco"));
        }
        VerifyPortfolio(now, key);
        var folder = Path.Combine(Path.GetTempPath(), "gf-monitor-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "monitor.json");
        try
        {
            using (var host = new GfMonitorHost(path))
            {
                Require(!host.WantsCollection(false, true) && host.WantsCollection(true, true) &&
                    !host.WantsCollection(true, false), "demanda não contorna gates");
                Require(host.BeginGeneration(key) == 1, "geração explícita");
                var portfolio = Fixture(now, key);
                Require(host.Publish(1, portfolio, now), "publicar dados já coletados");
                Require(!host.Publish(1, portfolio, now) && !host.Publish(1, portfolio, now.AddSeconds(1)),
                    "rejeitar cache repetido mesmo com nova hora de entrega");
                Require(!host.Publish(1, Fixture(now.AddSeconds(60), key), now.AddSeconds(1)),
                    "rejeitar fonte futura sem contaminar watermark");
                Require(host.Publish(1, Fixture(now.AddSeconds(2), key), now.AddSeconds(2)),
                    "fonte legítima após data futura inválida");
                Require(!host.Publish(1, Fixture(now.AddSeconds(-1), key), now.AddSeconds(1)), "não regredir fonte");
                Require(host.BeginGeneration(null) == 2 && !host.Publish(1, portfolio, now), "geração anterior rejeitada");
                var enabled = GfResourceMonitorPreferences.Default with { Enabled = true, OpacityPercent = 75 };
                Require(host.Apply(enabled, now), "habilitação explícita");
                Application.DoEvents();
                Require(host.WantsCollection(false, true) && !host.WantsCollection(false, false),
                    "monitor fora de Início demanda coleta existente, sem ultrapassar bloqueio");
                host.Configure(V269Theme.Dark, false);
                Require(host.Preferences().OpacityPercent == 75, "preferências aplicadas");
                Require(host.Apply(enabled with { Enabled = false }, now) && !host.WantsCollection(false, true),
                    "desativar libera consumidor e demanda");
            }
            Require(!GfResourceMonitorPreferencesStore.Load(path).Enabled, "desativação persistida");
            VerifyWindowLifecycle(path, key);
        }
        finally { Directory.Delete(folder, recursive: true); }

        foreach (V269Theme theme in Enum.GetValues<V269Theme>())
        {
            Require(GfUniversalMeter.Disk(90, 100, theme).Fraction == .9 &&
                GfUniversalMeter.Disk(101, 100, theme).Fraction is null &&
                GfUniversalMeter.Disk(0, 0, theme).Fraction is null, "ocupação real, sem denominador fictício");
            Require(GfUniversalMeter.Operation(null, GfMeterState.Running, false, theme).State == GfMeterState.Indeterminate &&
                GfUniversalMeter.Operation(100, GfMeterState.Running, true, theme).State == GfMeterState.Running &&
                GfUniversalMeter.Operation(null, GfMeterState.Success, true, theme).State == GfMeterState.Success,
                "progresso sem percentual inventado ou sucesso inferido");
        }
        Console.WriteLine("OK Monitor GF: host optativo, fontes independentes, replay e barras universais.");
    }
}
