using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class GfMonitorHostTests
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Host Monitor GF: " + message);
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
        var folder = Path.Combine(Path.GetTempPath(), "gf-monitor-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "monitor.json");
        try
        {
            using (var host = new GfMonitorHost(path))
            {
                Require(!host.WantsCollection(false, true) && host.WantsCollection(true, true) &&
                    !host.WantsCollection(true, false), "demanda não contorna gates");
                Require(host.BeginGeneration(key) == 1 && host.BeginGeneration(null) == 2, "gerações explícitas");
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
