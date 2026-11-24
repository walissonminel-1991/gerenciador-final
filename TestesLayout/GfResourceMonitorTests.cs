using System.Drawing;
using System.Runtime.InteropServices;
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

    // DrawToBitmap registra uma superfície retangular, não prova o recorte
    // do HWND. Consultar somente a região dos nossos próprios controles.
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PtInRegion(IntPtr region, int x, int y);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    private static void VerifyNativeRoundedRegion(Control control, string name)
    {
        Require(control.Visible && control.IsHandleCreated && control.Width > 4 && control.Height > 4,
            "geometria nativa inválida: " + name);
        var region = control.Region;
        Require(region is not null, "região gerenciada ausente: " + name);
        Point[] corners = [new(0, 0), new(control.Width - 1, 0),
            new(0, control.Height - 1), new(control.Width - 1, control.Height - 1)];
        Point center = new(control.Width / 2, control.Height / 2);
        Require(corners.All(p => !region!.IsVisible(p)) && region!.IsVisible(center),
            "recorte gerenciado não arredondado: " + name);
        IntPtr native = CreateRectRgn(0, 0, 0, 0);
        Require(native != IntPtr.Zero, "alocação de região de teste");
        try
        {
            int kind = GetWindowRgn(control.Handle, native);
            Require(kind is 2 or 3, "HWND não possui região válida: " + name);
            Require(corners.All(p => !PtInRegion(native, p.X, p.Y)) &&
                PtInRegion(native, center.X, center.Y), "recorte Win32 divergente: " + name);
        }
        finally { DeleteObject(native); }
    }

    private static void VerifyRoundedRegions(GfResourceMonitorForm form)
    {
        VerifyNativeRoundedRegion(form, "janela");
        var logos = All(form).OfType<GfMonitorLogoLabel>().Where(x => x.Visible).ToArray();
        Require(logos.Length == 1, "modo ativo possui exatamente um logo");
        VerifyNativeRoundedRegion(logos[0], "logo GF");
    }

    private static Rectangle CaptureBounds(Control control, Form form, Bitmap bitmap) =>
        Rectangle.Intersect(new Rectangle(form.PointToClient(control.PointToScreen(Point.Empty)), control.Size),
            new Rectangle(Point.Empty, bitmap.Size));

    private static int MatchingPixels(Bitmap bitmap, Rectangle area, Color foreground)
    {
        int count = 0;
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (Math.Abs(p.R - foreground.R) + Math.Abs(p.G - foreground.G) + Math.Abs(p.B - foreground.B) < 65)
                    count++;
            }
        return count;
    }

    private static void VerifyExpandedCapture(GfResourceMonitorForm form, Bitmap bitmap)
    {
        var all = All(form).Where(x => x.Visible).ToArray();
        var bars = all.OfType<GfDynamicProgressBar>().ToArray();
        Require(bars.Length == 6, "expandido exige seis barras");
        (string Name, string Value)[] expected = [
            ("Utilização CPU", "14%"), ("Utilização RAM", "42%"),
            ("Utilização GPU", "12%"), ("Utilização VRAM", "15%"),
            ("Temperatura CPU", "48°C"), ("Temperatura GPU", "46°C")
        ];
        foreach (var metric in expected)
        {
            var labels = all.OfType<Label>().Where(x => x.AccessibleName == metric.Name).ToArray();
            var matchingBars = bars.Where(x => x.AccessibleName == metric.Name).ToArray();
            Require(labels.Length == 1 && labels[0].Text == metric.Value && matchingBars.Length == 1,
                "métrica expandida ausente ou duplicada: " + metric.Name);
            var label = labels[0]; var bar = matchingBars[0];
            var labelArea = CaptureBounds(label, form, bitmap);
            var barArea = CaptureBounds(bar, form, bitmap);
            Require(labelArea.Width > 0 && labelArea.Height > 0 && barArea.Width > 0 && barArea.Height > 0,
                "métrica expandida fora da captura: " + metric.Name);
            Require(MatchingPixels(bitmap, labelArea, label.ForeColor) >= 5,
                "valor expandido não renderizado: " + metric.Name);
            Require(MatchingPixels(bitmap, barArea, label.ForeColor) >= 5,
                "preenchimento de barra ausente: " + metric.Name);
        }
    }

    // Gate de pixels: um PNG com os painéis presentes mas sem texto não passa.
    private static void VerifyCompactCapture(GfResourceMonitorForm form, Bitmap bitmap)
    {
        var labels = All(form).OfType<Label>().Where(x => x.Name.StartsWith("MonitorCompactMetric", StringComparison.Ordinal)).ToArray();
        Require(labels.Length == 5 && labels.All(x => x.Visible && x.Width > 0 && x.Height > 0), "compacto exige cinco métricas visíveis");
        Require(All(form).OfType<GfMonitorGlyph>().Count(x => x.Visible) == 5, "compacto exige cinco pictogramas");
        Require(All(form).OfType<Button>().Count(x => x.Visible) == 2, "compacto exige expandir e fechar");
        string[] expected = ["CPU 14%", "GPU 12%", "RAM 42%", "CPU 48°C", "GPU 46°C"];
        Require(expected.All(x => labels.Any(l => l.Text == x)), "compacto deve apresentar os cinco valores da fixture");
        foreach (var label in labels)
        {
            Require(label is GfMonitorCompactLabel compact && compact.Fits(label.Text) &&
                compact.Fits(label.Text.StartsWith("RAM") ? "RAM 100%" :
                    label.Text.EndsWith("°C") ? label.Text[..3] + " 150°C" : label.Text[..3] + " 100%"),
                "compacto deve acomodar valor de três dígitos sem truncar");
            var box = Rectangle.Intersect(new Rectangle(form.PointToClient(label.PointToScreen(Point.Empty)), label.Size),
                new Rectangle(Point.Empty, bitmap.Size));
            Require(box.Width > 0 && box.Height > 0, "métrica fora da janela");
            var foreground = label.ForeColor;
            int pixels = 0;
            for (int y = box.Top; y < box.Bottom; y++)
                for (int x = box.Left; x < box.Right; x++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (Math.Abs(p.R - foreground.R) + Math.Abs(p.G - foreground.G) + Math.Abs(p.B - foreground.B) < 65) pixels++;
                }
            Require(pixels >= 5, "captura sem texto renderizado: " + label.Name);
        }
    }

    private static double Contrast(Color foreground, Color background)
    {
        static double Linear(byte value)
        {
            double x = value / 255d;
            return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color c) => 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        double a = Luminance(foreground), b = Luminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static void VerifyContrast(V269Theme theme)
    {
        Color background = V269VisualTokens.ForTheme(theme).Card;
        var (blue, green, yellow, red) = GfMeterScale.Colors(theme);
        if (theme == V269Theme.Dark)
            Require(red == Color.FromArgb(242, 116, 126), "vermelho Dark corrigido");
        foreach (var kind in new[] { GfMeterKind.Usage, GfMeterKind.InvertedProgress, GfMeterKind.Temperature })
            for (int i = 0; i <= 10_000; i++)
                Require(Contrast(GfMeterScale.ScaleColor(kind, i / 100d, theme), background) >= 4.5,
                    $"contraste {theme}/{kind}/{i / 100d:F2}%");
        foreach (var (a, b) in new[] { (blue, green), (green, yellow), (yellow, red), (yellow, blue) })
        {
            var cuts = new SortedSet<double> { 0, 1 };
            foreach (var (x, y) in new[] { (a.R, b.R), (a.G, b.G), (a.B, b.B) })
                if (x != y)
                    for (int k = Math.Min(x, y); k < Math.Max(x, y); k++)
                        cuts.Add((k + 0.5 - x) / (y - x));
            double? previous = null;
            foreach (double t in cuts)
            {
                Require(Contrast(GfMeterScale.Mix(a, b, t), background) >= 4.5, "contraste RGB no limite de arredondamento");
                if (previous is double start)
                    Require(Contrast(GfMeterScale.Mix(a, b, (start + t) / 2), background) >= 4.5, "contraste RGB intermediário");
                previous = t;
            }
        }
        // Uma transição ampla percorre a escala, não o segmento azul/vermelho.
        Require(GfMeterScale.ScaleColor(GfMeterKind.Usage, 60, theme) == green &&
            GfMeterScale.ScaleColor(GfMeterKind.Usage, 75, theme) == yellow &&
            GfMeterScale.Frame(GfMeterKind.Temperature, 70, theme, 95).ScalePosition == 60 &&
            GfMeterScale.Frame(GfMeterKind.InvertedProgress, 35, theme).Color == yellow,
            "trajetória de cor e posição térmica");
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
            VerifyContrast(theme);
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
                VerifyRoundedRegions(preview);
                if (expanded) VerifyExpandedCapture(preview, bitmap);
                else VerifyCompactCapture(preview, bitmap);
                bitmap.Save(Path.Combine("LayoutCaptures", $"MonitorGF-{theme}-{(expanded ? "Expandido" : "Compacto")}.png"),
                    System.Drawing.Imaging.ImageFormat.Png);
            }
            preview.Close();
        }
        form.Close();
        Console.WriteLine("OK Monitor GF: regiões gerenciada e Win32, logo e pixels dos seis valores/barras nos três temas.");
        Console.WriteLine("OK Monitor GF: seis métricas, três temas, interpolação, limites, ausência e progresso.");
    }
}
