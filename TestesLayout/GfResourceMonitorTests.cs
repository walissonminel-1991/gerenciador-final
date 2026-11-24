using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class GfResourceMonitorTests
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Monitor GF: " + message);
    }
    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in All(child)) yield return descendant;
        }
    }

    private static void VerifyPreferences()
    {
        var defaults = GfResourceMonitorPreferences.Default;
        Require(!defaults.Enabled && defaults.OpacityPercent == 100, "monitor opcional por padrão");
        var invalid = defaults with { OpacityPercent = 2, X = int.MaxValue, Y = 10 };
        Require(invalid.Normalize().OpacityPercent == 40 && invalid.Normalize().X is null,
            "limitar transparência e rejeitar coordenadas absurdas");
        var work = new Rectangle(-1440, 0, 1440, 900);
        Require(GfResourceMonitorPreferences.Fit(new Point(-1440, 880), new Size(552, 232), work, 10) ==
            new Point(-1440, 668), "reencaixar após mudança de modo ou tela");
        var folder = Path.Combine(Path.GetTempPath(), "gf-monitor-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "monitor.json");
        try
        {
            var selected = defaults with { Enabled = true, Expanded = true, AlwaysOnTop = true, OpacityPercent = 75, X = -600, Y = 100 };
            Require(GfResourceMonitorPreferencesStore.TrySave(path, selected), "persistir apenas preferências locais");
            Require(GfResourceMonitorPreferencesStore.Load(path) == selected, "restaurar preferências");
            File.WriteAllText(path, "{invalid");
            Require(GfResourceMonitorPreferencesStore.Load(path) == defaults, "configuração inválida deve falhar fechada");
            File.WriteAllText(path, new string('x', 4097));
            Require(GfResourceMonitorPreferencesStore.Load(path) == defaults, "limite de tamanho");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    internal static void Run()
    {
        foreach (V269Theme theme in Enum.GetValues<V269Theme>())
        {
            var (blue, green, yellow, red) = GfMeterScale.Colors(theme);
            Require(GfMeterScale.UsageColor(0, theme) == blue && GfMeterScale.UsageColor(40, theme) == blue &&
                GfMeterScale.UsageColor(60, theme) == green && GfMeterScale.UsageColor(75, theme) == yellow &&
                GfMeterScale.UsageColor(100, theme) == red, "âncoras cromáticas da utilização");
            Require(GfMeterScale.UsageColor(50, theme) == GfMeterScale.Mix(blue, green, .5), "interpolação contínua");
            Require(GfMeterScale.ThermalIndex(75, 80) > GfMeterScale.ThermalIndex(75, 100),
                "mesma temperatura deve respeitar limite de cada componente");
            Require(GfMeterScale.ThermalIndex(80, 80) == 100 && GfMeterScale.ThermalIndex(100, 100) == 100,
                "limite real deve chegar ao vermelho");
            Require(GfMeterScale.Frame(GfMeterKind.Temperature, 80, theme, 80).Immediate &&
                !GfMeterScale.Frame(GfMeterKind.Temperature, 80, theme).Immediate,
                "não inferir limite crítico quando desconhecido");
            Require(GfMeterScale.Frame(GfMeterKind.Usage, double.NaN, theme).Fraction is null &&
                GfMeterScale.Frame(GfMeterKind.Usage, 105, theme).Fraction is null &&
                GfMeterScale.Frame(GfMeterKind.Usage, 0, theme).Fraction == 0,
                "distinguir zero medido de ausente/inválido");
            Require(GfMeterScale.Frame(GfMeterKind.InvertedProgress, 100, theme).Color != green &&
                GfMeterScale.Frame(GfMeterKind.InvertedProgress, null, theme, state: GfMeterState.Success).Color == green &&
                GfMeterScale.Frame(GfMeterKind.Progress, 100, theme).State == GfMeterState.Running,
                "sucesso deve vir do motor, não do percentual");
            Require(GfMeterScale.Frame(GfMeterKind.Progress, null, theme, state: GfMeterState.Indeterminate).Fraction is null &&
                GfMeterScale.Frame(GfMeterKind.Progress, null, theme, state: GfMeterState.Failed).Color == red &&
                GfMeterScale.Frame(GfMeterKind.Progress, 50, theme, state: GfMeterState.Cancelled).Fraction is null,
                "estados de operação independentes");
        }

        var now = DateTimeOffset.UtcNow;
        var filter = new GfMeterSmoother();
        Require(filter.Update(30, now, GfMeterKind.Usage, 3) == 30, "primeira leitura real");
        double? smooth = filter.Update(90, now.AddSeconds(1), GfMeterKind.Usage, 3);
        Require(smooth is > 30 and < 90, "suavização temporal EMA");
        Require(filter.Update(1, now, GfMeterKind.Usage, 3) == smooth, "ignorar amostra fora de ordem");
        Require(filter.Update(null, now.AddSeconds(2), GfMeterKind.Usage, 3) is null &&
            filter.Update(20, now.AddSeconds(3), GfMeterKind.Usage, 3) == 20, "perda de sensor reinicia filtro");
        filter.Reset();
        filter.Update(60, now, GfMeterKind.Temperature, 3, 80);
        Require(filter.Update(80, now.AddSeconds(1), GfMeterKind.Temperature, 3, 80) == 80,
            "limite térmico confirmado tem prioridade imediata");

        var sample = new LiveHardwareSample(now, MonitorReading<double>.Available(24, "fixture"),
            MonitorReading<double>.Unavailable("fixture"), new MemoryRamSample(now.UtcDateTime, 1000, 580),
            MonitorReading<TimeSpan>.Unavailable("fixture"), LiveHardwareSensors.Unavailable);
        var snapshot = GfResourceMonitorAdapter.FromCounters(sample);
        Require(snapshot.CpuPercent == 24 && snapshot.RamPercent == 42 && snapshot.GpuPercent is null &&
            snapshot.CpuTemperature is null && snapshot.VramPercent is null, "adaptador reutiliza CPU/RAM sem inventar GPU/sensores");

        Require(GfResourceMonitorAdapter.IsFresh(now.AddSeconds(-9), now) &&
            !GfResourceMonitorAdapter.IsFresh(now.AddSeconds(-11), now) &&
            !GfResourceMonitorAdapter.IsFresh(now.AddSeconds(6), now), "validar tempo de cada fonte");
        var oldMemory = sample with { Memory = new MemoryRamSample(now.AddSeconds(-30).UtcDateTime, 1000, 580) };
        Require(GfResourceMonitorAdapter.FromCounters(oldMemory).RamPercent is null, "não renovar RAM obsoleta");
        VerifyPreferences();

        using var form = new GfResourceMonitorForm();
        form.Show();
        Application.DoEvents();
        foreach (V269Theme theme in Enum.GetValues<V269Theme>())
        {
            form.Configure(theme, false);
            form.SetExpanded(false);
            Application.DoEvents();
            Require(All(form).OfType<GfDynamicProgressBar>().Count(x => x.Visible) == 0,
                "compacto não deve ter barras");
            int compactHeight = form.ClientSize.Height;
            form.SetExpanded(true);
            Application.DoEvents();
            Require(form.ClientSize.Height > compactHeight &&
                All(form).OfType<GfDynamicProgressBar>().Count(x => x.Visible) == 6,
                "expandido deve ter quatro barras de uso e duas térmicas");
            Require(All(form).OfType<GfDynamicProgressBar>().All(x => x.Width > 0 && x.Height > 0),
                "barras precisam ter geometria válida nos três temas");
            Require(!All(form).OfType<Label>().Any(x => x.Text.Contains("Disco") || x.Text.Contains("GHz") ||
                x.Text.Contains("GB") || x.Text.Contains("atividade", StringComparison.OrdinalIgnoreCase)),
                "métricas excluídas reapareceram");
        }
        form.Present(snapshot with { CapturedAtUtc = now.AddMinutes(-1), CpuPercent = 99 });
        Require(!All(form).OfType<Label>().Any(x => x.Text == "99%"), "rejeitar amostra obsoleta");
        form.Present(snapshot);
        Require(All(form).OfType<Label>().Any(x => x.Text == "24%") &&
            All(form).OfType<Label>().Any(x => x.Text == "42%"), "apresentação de valores reais adaptados");
        form.Present(snapshot with { CapturedAtUtc = now.AddSeconds(1), GpuPercent = 80, VramPercent = 90, GpuTemperature = 70 });
        Require(!All(form).OfType<Label>().Any(x => x.Text == "80%" || x.Text == "90%" || x.Text == "70°C"),
            "não aceitar GPU sem identidade");
        form.Present(snapshot with { CapturedAtUtc = now.AddSeconds(2), GpuPercent = 80, VramPercent = 90,
            GpuTemperature = 70, GpuIdentity = "fixture selecionada" });
        Require(All(form).OfType<Label>().Any(x => x.Text == "80%") &&
            All(form).OfType<Label>().Any(x => x.Text == "90%") &&
            All(form).OfType<Label>().Any(x => x.Text == "70°C"), "fixture explícita de GPU identificada");
        // Capturas sintéticas de UI, sem consultar ou identificar hardware real.
        // LayoutCaptures já é publicado pela CI existente; nenhuma alteração de workflow.
        Directory.CreateDirectory("LayoutCaptures");
        foreach (V269Theme theme in Enum.GetValues<V269Theme>())
        {
            using var preview = new GfResourceMonitorForm();
            preview.Configure(theme, false);
            preview.Show();
            preview.Present(new GfResourceMonitorSnapshot(DateTimeOffset.UtcNow, 14, 12, 42, 15,
                48, 46, CpuThermalLimit: 95, GpuThermalLimit: 85, GpuIdentity: "fixture visual"));
            foreach (bool expanded in new[] { false, true })
            {
                preview.SetExpanded(expanded);
                Application.DoEvents();
                Require(preview.ClientSize.Width > 0 && preview.ClientSize.Height > 0, "captura com área válida");
                using var bitmap = new Bitmap(preview.ClientSize.Width, preview.ClientSize.Height);
                preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine("LayoutCaptures", $"MonitorGF-{theme}-{(expanded ? "Expandido" : "Compacto")}.png"),
                    System.Drawing.Imaging.ImageFormat.Png);
            }
            preview.Close();
        }
        form.Close();
        Console.WriteLine("OK Monitor GF: seis métricas, três temas, interpolação, limites, ausência e progresso.");
    }
}
