using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Snapshot de apresentação somente leitura. O host compartilhará o motor de
// Início e fornecerá identidade da GPU e limites do MESMO sensor. Null = ausente.
internal sealed record GfResourceMonitorSnapshot(DateTimeOffset CapturedAtUtc,
    double? CpuPercent, double? GpuPercent, double? RamPercent, double? VramPercent,
    double? CpuTemperature, double? GpuTemperature,
    double? CpuThermalLimit = null, double? GpuThermalLimit = null,
    string? GpuIdentity = null, GfThermalEvidence? GpuThermalEvidence = null, long Generation = 0);

internal sealed class GfResourceMonitorForm : Form
{
    private static readonly string[] Names = ["CPU", "GPU", "RAM", "VRAM", "CPU", "GPU"];
    private readonly GfMeterSmoother[] _filters = Enumerable.Range(0, 6).Select(_ => new GfMeterSmoother()).ToArray();
    private readonly GfThermalPresentation _gpuThermal = new();
    private readonly Label[] _values = new Label[6];
    private readonly Label?[] _compactValues = new Label?[6];
    private readonly GfDynamicProgressBar[] _bars = new GfDynamicProgressBar[6];
    private readonly Panel _compact = new() { Dock = DockStyle.Fill };
    private readonly Panel _expanded = new() { Dock = DockStyle.Fill };
    private readonly List<Control> _surfaces = [];
    private readonly List<Control> _text = [];
    private readonly List<Button> _buttons = [];
    private readonly List<Font> _ownedFonts = [];
    private readonly List<GfMonitorGlyph> _glyphs = [];
    private readonly Label _compactLogo, _expandedLogo;
    private readonly GfMonitorLegend _legend;
    private readonly List<TableLayoutPanel> _outlined = [];
    private readonly List<TableLayoutPanel> _separated = [];
    private readonly System.Windows.Forms.Timer _freshness;
    private V269Theme _theme = V269Theme.AzulGf;
    private bool _animate = true, _isExpanded;
    private GfResourceMonitorSnapshot? _last;
    private string? _gpuIdentity;
    private Point? _drag;
    private long? _arrivedTick;
    private DateTimeOffset? _lastAcceptedAt;
    private bool _snapToEdges = true;
    internal bool Expanded => _isExpanded;
    internal event EventHandler? ModeChanged;

    internal GfResourceMonitorForm()
    {
        Name = "GfResourceMonitor";
        Text = "Monitor GF";
        AccessibleName = "Monitor GF — somente leitura";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new(2);
        _surfaces.AddRange([_compact, _expanded]);

        var strip = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 8,
            Margin = Padding.Empty, Padding = new(3) };
        _surfaces.Add(strip);
        strip.RowStyles.Add(new(SizeType.Percent, 100));
        strip.ColumnStyles.Add(new(SizeType.Absolute, 32));
        foreach (int weight in new[] { 19, 19, 19, 21, 22 })
            strip.ColumnStyles.Add(new(SizeType.Percent, weight));
        strip.ColumnStyles.Add(new(SizeType.Absolute, 27));
        strip.ColumnStyles.Add(new(SizeType.Absolute, 27));
        _compactLogo = Logo();
        strip.Controls.Add(_compactLogo, 0, 0);
        int col = 1;
        foreach (int i in new[] { 0, 1, 2, 4, 5 })
        {
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2,
                Margin = new(2, 0, 2, 0), Padding = Padding.Empty };
            cell.RowStyles.Add(new(SizeType.Percent, 100));
            cell.ColumnStyles.Add(new(SizeType.Absolute, 19));
            cell.ColumnStyles.Add(new(SizeType.Percent, 100));
            _surfaces.Add(cell);
            AddSeparator(cell);
            cell.Controls.Add(Glyph(i >= 4 ? 4 : i), 0, 0);
            var label = new GfMonitorCompactLabel { Text = Names[i] + " —", Dock = DockStyle.Fill,
                Margin = Padding.Empty, AccessibleName = i >= 4 ? "Temperatura " + Names[i] : "Utilização " + Names[i] };
            _text.Add(label);
            Emphasize(label, 8.5f);
            label.Name = "MonitorCompactMetric" + i;
            label.TextAlign = ContentAlignment.MiddleCenter;
            _compactValues[i] = label;
            cell.Controls.Add(label, 1, 0);
            strip.Controls.Add(cell, col++, 0);
            AttachDrag(cell); AttachDrag(label);
        }
        strip.Controls.Add(Chrome("↗", "Expandir monitor", () => SetExpanded(true)), 6, 0);
        strip.Controls.Add(Chrome("×", "Fechar monitor", Close), 7, 0);
        AttachDrag(strip); AttachDrag(_compactLogo);
        _compact.Controls.Add(strip);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Margin = Padding.Empty, Padding = new(5) };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 30));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 46));
        _surfaces.Add(layout);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        header.RowStyles.Add(new(SizeType.Percent, 100));
        header.ColumnStyles.Add(new(SizeType.Absolute, 29));
        header.ColumnStyles.Add(new(SizeType.Percent, 100));
        header.ColumnStyles.Add(new(SizeType.Absolute, 26));
        header.ColumnStyles.Add(new(SizeType.Absolute, 26));
        _surfaces.Add(header);
        _expandedLogo = Logo();
        var title = MakeLabel("Monitor GF", "Monitor GF");
        Emphasize(title, 11f);
        header.Controls.Add(_expandedLogo, 0, 0); header.Controls.Add(title, 1, 0);
        header.Controls.Add(Chrome("⌄", "Recolher monitor", () => SetExpanded(false)), 2, 0);
        header.Controls.Add(Chrome("×", "Fechar monitor", Close), 3, 0);
        AttachDrag(header); AttachDrag(title); AttachDrag(_expandedLogo);
        layout.Controls.Add(header, 0, 0);

        var panels = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2, Margin = new(0, 3, 0, 3) };
        panels.RowStyles.Add(new(SizeType.Percent, 100));
        panels.ColumnStyles.Add(new(SizeType.Percent, 57));
        panels.ColumnStyles.Add(new(SizeType.Percent, 43));
        _surfaces.Add(panels);
        panels.Controls.Add(PanelFor("Uso dos recursos", [0, 2, 1, 3]), 0, 0);
        panels.Controls.Add(PanelFor("Temperaturas", [4, 5]), 1, 0);
        layout.Controls.Add(panels, 0, 1);
        _legend = new GfMonitorLegend { Dock = DockStyle.Fill, Margin = new(2, 0, 2, 0),
            AccessibleName = "Escalas cromáticas de utilização e temperatura" };
        layout.Controls.Add(_legend, 0, 2);
        _expanded.Controls.Add(layout);
        // Somente a superfície ativa fica na árvore visual: evita sobreposição
        // de painéis Dock.Fill e captura de filhos do modo oculto.
        _expanded.Visible = false;
        Controls.Add(_compact);

        var menu = new ContextMenuStrip();
        var top = new ToolStripMenuItem("Sempre no topo") { CheckOnClick = true };
        top.CheckedChanged += (_, _) => TopMost = top.Checked;
        menu.Opening += (_, _) => top.Checked = TopMost;
        menu.Items.Add(top);
        menu.Items.Add("Alternar visualização", null, (_, _) => SetExpanded(!Expanded));
        menu.Items.Add("Fechar", null, (_, _) => Close());
        ContextMenuStrip = menu;
        Disposed += (_, _) => menu.Dispose();
        // Sem leitura de hardware: invalida uma amostra que deixou de chegar.
        _freshness = new() { Interval = 1000 };
        _freshness.Tick += (_, _) =>
        {
            if (_arrivedTick is long tick && Environment.TickCount64 - tick > 10_000)
                ClearReadings();
            else if (_last is { } snapshot && _gpuThermal.Expire(DateTimeOffset.UtcNow))
                Render(snapshot, updateFilters: false);
        };
        VisibleChanged += (_, _) =>
        {
            if (IsDisposed || Disposing) return;
            if (Visible)
            {
                if (_arrivedTick is long tick && Environment.TickCount64 - tick > 10_000) ClearReadings();
                _freshness.Start();
            }
            else _freshness.Stop();
        };
        Configure(V269Theme.AzulGf, true);
        SetExpanded(false);
        ClearReadings();
    }

    private void Emphasize(Control control, float size)
    {
        var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Point);
        _ownedFonts.Add(font);
        control.Font = font;
    }
    private Label Logo()
    {
        var label = new GfMonitorLogoLabel { Text = "GF", AccessibleName = "Gerenciador Final", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter, Margin = new(2), ForeColor = Color.White };
        Emphasize(label, 10f);
        return label;
    }
    private Label MakeLabel(string text, string accessible)
    {
        var l = new Label { Text = text, AccessibleName = accessible, Dock = DockStyle.Fill, AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
        _text.Add(l); return l;
    }
    private GfMonitorGlyph Glyph(int i)
    {
        var g = new GfMonitorGlyph(i) { Dock = DockStyle.Fill, Margin = new(1) };
        _glyphs.Add(g); return g;
    }
    private Button Chrome(string text, string name, Action action)
    {
        var b = new Button { Text = text, AccessibleName = name, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
            Margin = new(1), Padding = Padding.Empty };
        b.FlatAppearance.BorderSize = 0;
        b.UseVisualStyleBackColor = false;
        Emphasize(b, 13f);
        b.Click += (_, _) => action();
        _buttons.Add(b); return b;
    }

    private Control PanelFor(string caption, int[] indices)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = indices.Length + 1,
            Padding = new(4), Margin = new(2, 0, 2, 0), CellBorderStyle = TableLayoutPanelCellBorderStyle.None };
        panel.ColumnStyles.Add(new(SizeType.Percent, 100)); panel.RowStyles.Add(new(SizeType.Absolute, 20));
        var heading = MakeLabel(caption, caption);
        Emphasize(heading, 9f);
        var band = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty };
        band.ColumnStyles.Add(new(SizeType.Absolute, 22));
        band.ColumnStyles.Add(new(SizeType.Percent, 100));
        band.RowStyles.Add(new(SizeType.Percent, 100));
        band.Controls.Add(Glyph(indices[0] >= 4 ? 4 : 5), 0, 0);
        band.Controls.Add(heading, 1, 0);
        _surfaces.Add(band);
        band.Paint += (_, e) =>
        {
            using var pen = new Pen(V269VisualTokens.ForTheme(_theme).Border);
            e.Graphics.DrawLine(pen, 0, band.Height - 1, band.Width, band.Height - 1);
        };
        panel.Controls.Add(band, 0, 0);
        _surfaces.Add(panel);
        _outlined.Add(panel);
        panel.Paint += (_, e) => GfMonitorShape.DrawOutline(e.Graphics, panel.ClientRectangle,
            V269VisualTokens.ForTheme(_theme).Border, 6f * panel.DeviceDpi / 96f);
        for (int n = 0; n < indices.Length; n++)
        {
            int i = indices[n];
            panel.RowStyles.Add(new(SizeType.Percent, 100f / indices.Length));
            bool thermal = i >= 4;
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = thermal ? 3 : 4, RowCount = thermal ? 2 : 1,
                Margin = new(0, 1, 0, 1), Padding = new(2, 1, 2, 1) };
            row.ColumnStyles.Add(new(SizeType.Absolute, 22));
            if (!thermal) row.ColumnStyles.Add(new(SizeType.Absolute, 43));
            row.ColumnStyles.Add(new(SizeType.Percent, 100));
            row.ColumnStyles.Add(new(SizeType.Absolute, thermal ? 58 : 43));
            row.RowStyles.Add(new(SizeType.Percent, 100));
            if (thermal) row.RowStyles.Add(new(SizeType.Absolute, 15));
            _surfaces.Add(row);
            row.Controls.Add(Glyph(thermal ? i - 4 : i), 0, 0);
            var metricName = MakeLabel(Names[i], Names[i]);
            Emphasize(metricName, 8.5f);
            row.Controls.Add(metricName, 1, 0);
            var value = MakeLabel("—", (thermal ? "Temperatura " : "Utilização ") + Names[i]);
            value.TextAlign = ContentAlignment.MiddleRight; _values[i] = value;
            Emphasize(value, 9f);
            row.Controls.Add(value, thermal ? 2 : 3, 0);
            var bar = new GfDynamicProgressBar { Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 9, Margin = new(1, 0, 0, 0), AccessibleName = value.AccessibleName };
            _bars[i] = bar;
            row.Controls.Add(bar, thermal ? 1 : 2, thermal ? 1 : 0);
            // A barra térmica termina antes da coluna do valor, como na referência.
            if (n < indices.Length - 1)
                row.Paint += (_, e) => { using var pen = new Pen(V269VisualTokens.ForTheme(_theme).Border); e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1); };
            panel.Controls.Add(row, 0, n + 1);
        }
        return panel;
    }

    internal void Configure(V269Theme theme, bool animationsEnabled)
    {
        _theme = theme; _animate = animationsEnabled && SystemInformation.IsMenuAnimationEnabled;
        var p = V269VisualTokens.ForTheme(theme);
        BackColor = p.Accent;
        foreach (var c in _surfaces) c.BackColor = p.Card;
        foreach (var c in _text) c.ForeColor = p.Text;
        foreach (var b in _buttons) { b.BackColor = p.Card; b.ForeColor = p.Text; }
        foreach (var g in _glyphs) { g.ForeColor = p.Text; g.Invalidate(); }
        foreach (var l in _compactValues.OfType<GfMonitorCompactLabel>()) l.SetCaptionColor(p.Text);
        _compactLogo.BackColor = _expandedLogo.BackColor = p.Selection;
        _legend.Configure(theme);
        foreach (var surface in _outlined.Concat(_separated)) surface.Invalidate();
        foreach (var bar in _bars) bar.Configure(theme, _animate);
        if (_last is not null) Render(_last, updateFilters: false); else ClearReadings();
    }

    internal void SetExpanded(bool value)
    {
        bool changed = _isExpanded != value; _isExpanded = value;
        SuspendLayout();
        try
        {
            Panel active = value ? _expanded : _compact;
            Panel inactive = value ? _compact : _expanded;
            inactive.Visible = false;
            if (inactive.Parent == this) Controls.Remove(inactive);
            ClientSize = new(SizeFor(552), SizeFor(value ? 232 : 38));
            if (active.Parent != this) Controls.Add(active);
            active.Visible = true;
            active.BringToFront();
        }
        finally { ResumeLayout(true); }
        GfMonitorShape.ApplyRegion(this, SizeFor(9));
        (value ? _expanded : _compact).PerformLayout();
        Invalidate(true);
        if (Visible) ClampLocation(snap: false);
        if (changed) ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddSeparator(TableLayoutPanel cell)
    {
        _separated.Add(cell);
        cell.Paint += (_, e) =>
        {
            using var pen = new Pen(V269VisualTokens.ForTheme(_theme).Border);
            e.Graphics.DrawLine(pen, 0, 3, 0, Math.Max(3, cell.Height - 4));
        };
    }

    // O host decide habilitação e gravação. Não abre monitor nem altera
    // a inicialização do Windows ao ler preferências.
    internal void ApplyPreferences(GfResourceMonitorPreferences preferences)
    {
        var p = preferences.Normalize();
        TopMost = p.AlwaysOnTop;
        Opacity = p.OpacityPercent / 100d;
        _snapToEdges = p.SnapToEdges;
        SetExpanded(p.Expanded);
        if (p.X is int x && p.Y is int y) Location = new(x, y);
        ClampLocation(snap: false);
    }

    internal GfResourceMonitorPreferences CapturePreferences(bool enabled) =>
        new GfResourceMonitorPreferences(1, enabled, Expanded, TopMost,
            (int)Math.Round(Opacity * 100), _snapToEdges, Location.X, Location.Y).Normalize();

    private void ClampLocation(bool snap)
    {
        var area = Screen.FromRectangle(Bounds).WorkingArea;
        Location = GfResourceMonitorPreferences.Fit(Location, Size, area, snap ? SizeFor(10) : 0);
    }

    private int SizeFor(int logical) => Math.Max(1, (int)Math.Round(logical * DeviceDpi / 96d));
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e); SetExpanded(_isExpanded);
    }

    private void AttachDrag(Control c)
    {
        c.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _drag = Cursor.Position - new Size(Location); c.Capture = true; } };
        c.MouseMove += (_, _) => { if (_drag is Point d && c.Capture) Location = Cursor.Position - new Size(d); };
        c.MouseUp += (_, _) => { _drag = null; c.Capture = false; ClampLocation(_snapToEdges); };
        c.MouseCaptureChanged += (_, _) => { if (!c.Capture) _drag = null; };
    }

    // Chamado exclusivamente na thread de UI pelo futuro host compartilhado.
    internal void Present(GfResourceMonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (InvokeRequired) throw new InvalidOperationException("Apresente na thread da interface.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!GfResourceMonitorAdapter.IsFresh(snapshot.CapturedAtUtc, now) ||
            (_lastAcceptedAt is DateTimeOffset accepted && snapshot.CapturedAtUtc <= accepted)) return;
        if (!string.Equals(_gpuIdentity, snapshot.GpuIdentity, StringComparison.Ordinal))
        {
            foreach (int i in new[] { 1, 3, 5 }) _filters[i].Reset();
            _gpuIdentity = snapshot.GpuIdentity;
        }
        // Não exibir métricas de GPU que não tenham uma identidade selecionada.
        if (string.IsNullOrWhiteSpace(snapshot.GpuIdentity))
            snapshot = snapshot with { GpuPercent = null, VramPercent = null, GpuTemperature = null, GpuThermalLimit = null, GpuThermalEvidence = null };
        _last = snapshot; _lastAcceptedAt = snapshot.CapturedAtUtc;
        // Descontar idade anterior à entrega, usando relógio monotônico depois dela.
        _arrivedTick = Environment.TickCount64 - (long)Math.Max(0, (now - snapshot.CapturedAtUtc).TotalMilliseconds);
        Render(snapshot, updateFilters: true);
    }

    private readonly double?[] _smoothed = new double?[6];
    private void Render(GfResourceMonitorSnapshot s, bool updateFilters)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (updateFilters) _gpuThermal.Update(s, now);
        else _gpuThermal.Expire(now);
        var evidence = _gpuThermal.Evidence(s, now);
        double?[] data = [s.CpuPercent, s.GpuPercent, s.RamPercent, s.VramPercent, s.CpuTemperature, s.GpuTemperature];
        for (int i = 0; i < data.Length; i++)
        {
            var kind = i >= 4 ? GfMeterKind.Temperature : GfMeterKind.Usage;
            double? limit = i == 4 ? s.CpuThermalLimit : i == 5
                ? evidence?.MaximumC ?? (s.GpuThermalEvidence is null ? s.GpuThermalLimit : null) : null;
            bool atWarning = i == 5 && evidence is not null && data[i] is double raw && raw >= evidence.WarningC;
            if (updateFilters) _smoothed[i] = _filters[i].Update(data[i], s.CapturedAtUtc, kind,
                atWarning ? 0 : i is 2 or 3 ? 2 : 3, limit);
            var frame = i == 5 ? _gpuThermal.Frame(s, _smoothed[i], _theme, now) :
                GfMeterScale.Frame(kind, _smoothed[i], _theme, limit);
            string number = GfMeterScale.Valid(data[i], kind) ? data[i]!.Value.ToString("0") + (i >= 4 ? "°C" : "%") : "—";
            string description = _values[i].AccessibleName + ": " + number +
                (i == 5 && s.GpuThermalEvidence is not null ? _gpuThermal.Description(s, now) :
                    i >= 4 && !GfMeterScale.ValidThermalLimit(limit) ? " (escala térmica de referência)" : "");
            _values[i].Text = number; _values[i].ForeColor = frame.Color; _values[i].AccessibleDescription = description;
            if (_compactValues[i] is Label compact)
            {
                compact.Text = Names[i] + " " + number;
                compact.ForeColor = frame.Color; compact.AccessibleDescription = description;
            }
            _bars[i].Present(frame, description);
        }
    }

    // Invalidação explícita de geração, distinta da expiração normal que
    // preserva o watermark e rejeita replays antigos.
    internal void ResetForSnapshotGeneration()
    {
        if (IsDisposed || Disposing || InvokeRequired)
            throw new InvalidOperationException("Reset de geração exige janela viva na thread da UI.");
        _lastAcceptedAt = null;
        _gpuIdentity = null;
        ClearReadings();
    }

    private void ClearReadings()
    {
        _gpuThermal.Reset();
        _arrivedTick = null; _last = null;
        for (int i = 0; i < 6; i++)
        {
            _filters[i].Reset(); _smoothed[i] = null;
            _values[i].Text = "—"; _values[i].ForeColor = V269VisualTokens.ForTheme(_theme).SecondaryText;
            _values[i].AccessibleDescription = "Indisponível";
            if (_compactValues[i] is Label l) { l.Text = Names[i] + " —"; l.ForeColor = _values[i].ForeColor; l.AccessibleDescription = "Indisponível"; }
            _bars[i].Present(GfMeterScale.Frame(i >= 4 ? GfMeterKind.Temperature : GfMeterKind.Usage, null, _theme), "Indisponível");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _freshness.Dispose();
            if (_compact.Parent != this) _compact.Dispose();
            if (_expanded.Parent != this) _expanded.Dispose();
        }
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (Font font in _ownedFonts) font.Dispose();
            _ownedFonts.Clear();
        }
    }
}

// Pictogramas pequenos, independentes de fontes de emoji.
internal sealed class GfMonitorGlyph(int kind) : Control
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 4 || Height < 4) return;
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float d = Math.Min(Width, Height) - 2;
        g.TranslateTransform((Width - d) / 2, (Height - d) / 2); g.ScaleTransform(d / 20f, d / 20f);
        using var p = new Pen(ForeColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        switch (kind)
        {
            case 0:
                g.DrawRectangle(p, 5, 5, 10, 10); g.DrawRectangle(p, 8, 8, 4, 4);
                for (int x = 7; x <= 13; x += 3) { g.DrawLine(p, x, 2, x, 5); g.DrawLine(p, x, 15, x, 18); g.DrawLine(p, 2, x, 5, x); g.DrawLine(p, 15, x, 18, x); }
                break;
            case 1:
                g.DrawRectangle(p, 2, 5, 16, 10); g.DrawEllipse(p, 4, 7, 6, 6); g.DrawEllipse(p, 11, 7, 5, 6); g.DrawLine(p, 1, 3, 1, 17);
                break;
            case 2:
                g.DrawRectangle(p, 2, 6, 16, 8);
                for (int x = 5; x <= 14; x += 4) { g.DrawRectangle(p, x, 8, 2, 3); g.DrawLine(p, x, 14, x, 16); }
                break;
            case 3:
                g.DrawRectangle(p, 4, 5, 12, 10);
                g.DrawRectangle(p, 7, 8, 6, 4);
                for (int x = 6; x <= 14; x += 4) { g.DrawLine(p, x, 2, x, 5); g.DrawLine(p, x, 15, x, 18); }
                break;
            case 5:
                using (var brush = new SolidBrush(ForeColor))
                {
                    g.FillRectangle(brush, 3, 12, 3, 6); g.FillRectangle(brush, 8, 8, 3, 10); g.FillRectangle(brush, 13, 3, 3, 15);
                }
                break;
            default:
                g.DrawEllipse(p, 7, 12, 6, 6); g.DrawLine(p, 8, 13, 8, 4); g.DrawLine(p, 12, 13, 12, 4); g.DrawArc(p, 8, 2, 4, 4, 180, 180);
                break;
        }
    }
}


// Legenda enxuta, com âncoras de uso e indicação explícita da referência
// térmica. Não transforma carga alta em diagnóstico de defeito.
internal sealed class GfMonitorLegend : Control
{
    private V269Theme _theme;
    internal GfMonitorLegend()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }

    internal void Configure(V269Theme theme)
    {
        _theme = theme;
        var p = V269VisualTokens.ForTheme(theme);
        BackColor = p.Card; ForeColor = p.Text;
        AccessibleDescription = "Uso: azul até 40%, verde em 60%, amarelo em 75%, vermelho em 100%. " +
            "Temperatura adaptada ao limite comprovado; quando ausente, referência genérica.";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 20 || Height < 20) return;
        var p = V269VisualTokens.ForTheme(_theme);
        var (blue, green, yellow, red) = GfMeterScale.Colors(_theme);
        Color[] colors = [blue, green, yellow, red];
        using var border = new Pen(p.Border);
        GfMonitorShape.DrawOutline(e.Graphics, ClientRectangle, p.Border, 6f * DeviceDpi / 96f);
        e.Graphics.DrawLine(border, Width / 2, 4, Width / 2, Height - 5);
        void Group(int x, int width, string title, string[] labels)
        {
            int header = Height * 2 / 5;
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(e.Graphics, title, Font, new Rectangle(x + 3, 0, width - 6, header), p.Text, flags);
            for (int i = 0; i < 4; i++)
            {
                int cell = width / 4, left = x + i * cell;
                using var brush = new SolidBrush(colors[i]);
                using var path = GfMonitorShape.Rounded(new RectangleF(left + cell / 2f - 8, header, 16, 5), 2.5f);
                e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, labels[i], Font, new Rectangle(left + 1, header + 6, Math.Max(1, cell - 2), Height - header - 7), p.Text, flags);
            }
        }
        Group(0, Width / 2, "Escala de uso · 40 / 60 / 75 / 100%", ["Baixo", "Normal", "Alto", "Muito alto"]);
        Group(Width / 2, Width - Width / 2, "Temperatura · limite ou referência", ["Fria", "Normal", "Alta", "Limite"]);
    }
}


// Desenho comum: bordas arredondadas e ícones sem fontes externas.
internal static class GfMonitorShape
{
    internal static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(Math.Min(rect.Width, rect.Height), Math.Max(0, radius * 2));
        if (rect.Width <= 0 || rect.Height <= 0) return path;
        if (d <= 1) { path.AddRectangle(rect); return path; }
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void DrawOutline(Graphics graphics, Rectangle rect, Color color, float radius)
    {
        if (rect.Width < 2 || rect.Height < 2) return;
        using var path = Rounded(new RectangleF(rect.X + .5f, rect.Y + .5f, rect.Width - 1, rect.Height - 1), radius);
        using var pen = new Pen(color);
        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawPath(pen, path);
        graphics.SmoothingMode = previous;
    }

    internal static void ApplyRegion(Control control, int radius)
    {
        if (control.Width < 2 || control.Height < 2) return;
        using var path = Rounded(new RectangleF(0, 0, control.Width, control.Height), radius);
        control.Region = new Region(path);
    }
}

internal sealed class GfMonitorLogoLabel : Label
{
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        GfMonitorShape.ApplyRegion(this, Math.Max(2, 5 * DeviceDpi / 96));
    }
}

// Mantém o texto acessível completo, mas separa a identificação neutra
// do número cromático. Se necessário, acomoda ambos em duas linhas dentro
// da mesma faixa horizontal, sem cortar a temperatura ou o percentual.
internal sealed class GfMonitorCompactLabel : Label
{
    private Color _captionColor = SystemColors.ControlText;

    // Aparência exclusivamente em tempo de execução, sem propriedade serializável
    // pelo designer WinForms. Preserva o analisador WFO1000 habilitado.
    internal void SetCaptionColor(Color color)
    {
        if (_captionColor == color) return;
        _captionColor = color;
        Invalidate();
    }
    private const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
        TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;

    internal GfMonitorCompactLabel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    private (string Caption, string Value) Parts()
    {
        int split = Text.IndexOf(' ');
        return split < 0 ? (string.Empty, Text) : (Text[..split], Text[(split + 1)..]);
    }

    internal bool Fits(string candidate)
    {
        int split = candidate.IndexOf(' ');
        string caption = split < 0 ? string.Empty : candidate[..split];
        string value = split < 0 ? candidate : candidate[(split + 1)..];
        var a = TextRenderer.MeasureText(caption, Font, Size.Empty, Flags);
        var b = TextRenderer.MeasureText(value, Font, Size.Empty, Flags);
        return (a.Width + b.Width + 3 <= Width && Math.Max(a.Height, b.Height) <= Height) ||
            (Math.Max(a.Width, b.Width) <= Width && a.Height + b.Height <= Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var (caption, value) = Parts();
        var a = TextRenderer.MeasureText(e.Graphics, caption, Font, Size.Empty, Flags);
        var b = TextRenderer.MeasureText(e.Graphics, value, Font, Size.Empty, Flags);
        int total = a.Width + b.Width + 3;
        if (total <= Width)
        {
            int x = (Width - total) / 2;
            TextRenderer.DrawText(e.Graphics, caption, Font, new Rectangle(x, 0, a.Width, Height), _captionColor, Flags);
            TextRenderer.DrawText(e.Graphics, value, Font, new Rectangle(x + a.Width + 3, 0, b.Width, Height), ForeColor, Flags);
        }
        else
        {
            int top = Height / 2;
            TextRenderer.DrawText(e.Graphics, caption, Font, new Rectangle(0, 0, Width, top), _captionColor, Flags);
            TextRenderer.DrawText(e.Graphics, value, Font, new Rectangle(0, top, Width, Height - top), ForeColor, Flags);
        }
    }
}
