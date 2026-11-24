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
    string? GpuIdentity = null);

internal sealed class GfResourceMonitorForm : Form
{
    private static readonly string[] Names = ["CPU", "GPU", "RAM", "VRAM", "CPU", "GPU"];
    private readonly GfMeterSmoother[] _filters = Enumerable.Range(0, 6).Select(_ => new GfMeterSmoother()).ToArray();
    private readonly Label[] _values = new Label[6];
    private readonly Label?[] _compactValues = new Label?[6];
    private readonly GfDynamicProgressBar[] _bars = new GfDynamicProgressBar[6];
    private readonly Panel _compact = new() { Dock = DockStyle.Fill };
    private readonly Panel _expanded = new() { Dock = DockStyle.Fill };
    private readonly List<Control> _surfaces = [];
    private readonly List<Control> _text = [];
    private readonly List<Button> _buttons = [];
    private readonly List<GfMonitorGlyph> _glyphs = [];
    private readonly Label _compactLogo, _expandedLogo;
    private readonly Label _legend;
    private readonly System.Windows.Forms.Timer _freshness;
    private V269Theme _theme = V269Theme.AzulGf;
    private bool _animate = true, _isExpanded;
    private GfResourceMonitorSnapshot? _last;
    private string? _gpuIdentity;
    private Point? _drag;
    private DateTimeOffset? _arrivedAt;
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
        Padding = new(1);
        _surfaces.AddRange([_compact, _expanded]);

        var strip = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 9,
            Margin = Padding.Empty, Padding = new(3) };
        _surfaces.Add(strip);
        foreach (int w in new[] { 30, 74, 74, 74, 65, 65, 25, 25 })
            strip.ColumnStyles.Add(new(SizeType.Absolute, w));
        strip.ColumnStyles.Add(new(SizeType.Percent, 100));
        _compactLogo = Logo();
        strip.Controls.Add(_compactLogo, 0, 0);
        int col = 1;
        foreach (int i in new[] { 0, 1, 2, 4, 5 })
        {
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2,
                Margin = new(2, 0, 2, 0), Padding = Padding.Empty };
            cell.ColumnStyles.Add(new(SizeType.Absolute, 19));
            cell.ColumnStyles.Add(new(SizeType.Percent, 100));
            _surfaces.Add(cell);
            cell.Controls.Add(Glyph(i), 0, 0);
            var label = MakeLabel("—", i >= 4 ? "Temperatura " + Names[i] : "Utilização " + Names[i]);
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
        layout.RowStyles.Add(new(SizeType.Absolute, 28));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Absolute, 23));
        _surfaces.Add(layout);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        header.ColumnStyles.Add(new(SizeType.Absolute, 29));
        header.ColumnStyles.Add(new(SizeType.Percent, 100));
        header.ColumnStyles.Add(new(SizeType.Absolute, 26));
        header.ColumnStyles.Add(new(SizeType.Absolute, 26));
        _surfaces.Add(header);
        _expandedLogo = Logo();
        var title = MakeLabel("Monitor GF", "Monitor GF");
        header.Controls.Add(_expandedLogo, 0, 0); header.Controls.Add(title, 1, 0);
        header.Controls.Add(Chrome("⌄", "Recolher monitor", () => SetExpanded(false)), 2, 0);
        header.Controls.Add(Chrome("×", "Fechar monitor", Close), 3, 0);
        AttachDrag(header); AttachDrag(title); AttachDrag(_expandedLogo);
        layout.Controls.Add(header, 0, 0);

        var panels = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2, Margin = new(0, 3, 0, 3) };
        panels.ColumnStyles.Add(new(SizeType.Percent, 57));
        panels.ColumnStyles.Add(new(SizeType.Percent, 43));
        _surfaces.Add(panels);
        panels.Controls.Add(PanelFor("Uso dos recursos", [0, 2, 1, 3]), 0, 0);
        panels.Controls.Add(PanelFor("Temperaturas", [4, 5]), 1, 0);
        layout.Controls.Add(panels, 0, 1);
        _legend = MakeLabel("Baixo · Normal · Alto · Muito alto", "Escala cromática: azul, verde, amarelo, vermelho");
        _legend.TextAlign = ContentAlignment.MiddleCenter;
        layout.Controls.Add(_legend, 0, 2);
        _expanded.Controls.Add(layout);
        Controls.Add(_expanded); Controls.Add(_compact);

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
            if (_arrivedAt is DateTimeOffset at && DateTimeOffset.UtcNow - at > TimeSpan.FromSeconds(10))
                ClearReadings();
        };
        VisibleChanged += (_, _) => { if (Visible) _freshness.Start(); else _freshness.Stop(); };
        Configure(V269Theme.AzulGf, true);
        SetExpanded(false);
        ClearReadings();
    }

    private Label Logo() => new() { Text = "GF", AccessibleName = "Gerenciador Final", Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter, Margin = new(2), ForeColor = Color.White };
    private Label MakeLabel(string text, string accessible)
    {
        var l = new Label { Text = text, AccessibleName = accessible, Dock = DockStyle.Fill, AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
        _text.Add(l); return l;
    }
    private GfMonitorGlyph Glyph(int i)
    {
        var g = new GfMonitorGlyph(i >= 4 ? 4 : i) { Dock = DockStyle.Fill, Margin = new(1) };
        _glyphs.Add(g); return g;
    }
    private Button Chrome(string text, string name, Action action)
    {
        var b = new Button { Text = text, AccessibleName = name, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
            Margin = new(1), Padding = Padding.Empty };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => action();
        _buttons.Add(b); return b;
    }

    private Control PanelFor(string caption, int[] indices)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = indices.Length + 1,
            Padding = new(4), Margin = new(2, 0, 2, 0), CellBorderStyle = TableLayoutPanelCellBorderStyle.None };
        panel.ColumnStyles.Add(new(SizeType.Percent, 100)); panel.RowStyles.Add(new(SizeType.Absolute, 20));
        panel.Controls.Add(MakeLabel(caption, caption), 0, 0);
        _surfaces.Add(panel);
        for (int n = 0; n < indices.Length; n++)
        {
            int i = indices[n];
            panel.RowStyles.Add(new(SizeType.Percent, 100f / indices.Length));
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2,
                Margin = new(0, 1, 0, 1), Padding = Padding.Empty };
            row.ColumnStyles.Add(new(SizeType.Absolute, 20));
            row.ColumnStyles.Add(new(SizeType.Percent, 100));
            row.ColumnStyles.Add(new(SizeType.Absolute, 49));
            row.RowStyles.Add(new(SizeType.Percent, 100));
            row.RowStyles.Add(new(SizeType.Absolute, 6));
            _surfaces.Add(row);
            row.Controls.Add(Glyph(i >= 4 ? i - 4 : i), 0, 0);
            row.Controls.Add(MakeLabel(Names[i], Names[i]), 1, 0);
            var value = MakeLabel("—", (i >= 4 ? "Temperatura " : "Utilização ") + Names[i]);
            value.TextAlign = ContentAlignment.MiddleRight; _values[i] = value;
            row.Controls.Add(value, 2, 0);
            var bar = new GfDynamicProgressBar { Dock = DockStyle.Fill, Margin = new(1, 0, 0, 0), AccessibleName = value.AccessibleName };
            _bars[i] = bar; row.Controls.Add(bar, 1, 1); row.SetColumnSpan(bar, 2);
            panel.Controls.Add(row, 0, n + 1);
        }
        return panel;
    }

    internal void Configure(V269Theme theme, bool animationsEnabled)
    {
        _theme = theme; _animate = animationsEnabled && SystemInformation.IsMenuAnimationEnabled;
        var p = V269VisualTokens.ForTheme(theme);
        BackColor = p.Border;
        foreach (var c in _surfaces) c.BackColor = p.Card;
        foreach (var c in _text) c.ForeColor = p.Text;
        foreach (var b in _buttons) { b.BackColor = p.Card; b.ForeColor = p.Text; }
        foreach (var g in _glyphs) { g.ForeColor = p.SecondaryText; g.Invalidate(); }
        _compactLogo.BackColor = _expandedLogo.BackColor = p.Selection;
        _legend.ForeColor = p.SecondaryText;
        foreach (var bar in _bars) bar.Configure(theme, _animate);
        if (_last is not null) Render(_last, updateFilters: false); else ClearReadings();
    }

    internal void SetExpanded(bool value)
    {
        bool changed = _isExpanded != value; _isExpanded = value;
        SuspendLayout();
        try
        {
            _compact.Visible = !value; _expanded.Visible = value;
            (value ? _expanded : _compact).BringToFront();
            ClientSize = new(SizeFor(value ? 412 : 444), SizeFor(value ? 194 : 36));
        }
        finally { ResumeLayout(true); }
        if (changed) ModeChanged?.Invoke(this, EventArgs.Empty);
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
        c.MouseUp += (_, _) => { _drag = null; c.Capture = false; };
        c.MouseCaptureChanged += (_, _) => { if (!c.Capture) _drag = null; };
    }

    // Chamado exclusivamente na thread de UI pelo futuro host compartilhado.
    internal void Present(GfResourceMonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (InvokeRequired) throw new InvalidOperationException("Apresente na thread da interface.");
        if (_last is not null && snapshot.CapturedAtUtc <= _last.CapturedAtUtc) return;
        if (!string.Equals(_gpuIdentity, snapshot.GpuIdentity, StringComparison.Ordinal))
        {
            foreach (int i in new[] { 1, 3, 5 }) _filters[i].Reset();
            _gpuIdentity = snapshot.GpuIdentity;
        }
        // Não exibir métricas de GPU que não tenham uma identidade selecionada.
        if (string.IsNullOrWhiteSpace(snapshot.GpuIdentity))
            snapshot = snapshot with { GpuPercent = null, VramPercent = null, GpuTemperature = null, GpuThermalLimit = null };
        _last = snapshot; _arrivedAt = DateTimeOffset.UtcNow;
        Render(snapshot, updateFilters: true);
    }

    private readonly double?[] _smoothed = new double?[6];
    private void Render(GfResourceMonitorSnapshot s, bool updateFilters)
    {
        double?[] data = [s.CpuPercent, s.GpuPercent, s.RamPercent, s.VramPercent, s.CpuTemperature, s.GpuTemperature];
        for (int i = 0; i < data.Length; i++)
        {
            var kind = i >= 4 ? GfMeterKind.Temperature : GfMeterKind.Usage;
            double? limit = i == 4 ? s.CpuThermalLimit : i == 5 ? s.GpuThermalLimit : null;
            if (updateFilters) _smoothed[i] = _filters[i].Update(data[i], s.CapturedAtUtc, kind, i is 2 or 3 ? 2 : 3, limit);
            var frame = GfMeterScale.Frame(kind, _smoothed[i], _theme, limit);
            string number = GfMeterScale.Valid(data[i], kind) ? data[i]!.Value.ToString("0") + (i >= 4 ? "°C" : "%") : "—";
            string description = _values[i].AccessibleName + ": " + number +
                (i >= 4 && !GfMeterScale.ValidThermalLimit(limit) ? " (escala térmica de referência)" : "");
            _values[i].Text = number; _values[i].ForeColor = frame.Color; _values[i].AccessibleDescription = description;
            if (_compactValues[i] is Label compact)
            {
                compact.Text = (i >= 4 ? Names[i] + " " : "") + number;
                compact.ForeColor = frame.Color; compact.AccessibleDescription = description;
            }
            _bars[i].Present(frame, description);
        }
    }

    private void ClearReadings()
    {
        _arrivedAt = null; _last = null;
        for (int i = 0; i < 6; i++)
        {
            _filters[i].Reset(); _smoothed[i] = null;
            _values[i].Text = "—"; _values[i].ForeColor = V269VisualTokens.ForTheme(_theme).SecondaryText;
            _values[i].AccessibleDescription = "Indisponível";
            if (_compactValues[i] is Label l) { l.Text = (i >= 4 ? Names[i] + " " : "") + "—"; l.ForeColor = _values[i].ForeColor; l.AccessibleDescription = "Indisponível"; }
            _bars[i].Present(GfMeterScale.Frame(i >= 4 ? GfMeterKind.Temperature : GfMeterKind.Usage, null, _theme), "Indisponível");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _freshness.Dispose();
        base.Dispose(disposing);
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
            case 2: case 3:
                g.DrawRectangle(p, 2, 6, 16, 8);
                for (int x = 5; x <= 14; x += 4) { g.DrawRectangle(p, x, 8, 2, 3); g.DrawLine(p, x, 14, x, 16); }
                break;
            default:
                g.DrawEllipse(p, 7, 12, 6, 6); g.DrawLine(p, 8, 13, 8, 4); g.DrawLine(p, 12, 13, 12, 4); g.DrawArc(p, 8, 2, 4, 4, 180, 180);
                break;
        }
    }
}
