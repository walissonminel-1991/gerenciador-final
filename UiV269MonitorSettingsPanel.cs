using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Componente independente para Configurações > Geral, a ser posicionado pelo
// WORK. Não abre janela, grava arquivo ou solicita coleta por conta própria.
internal sealed class GfMonitorSettingsPanel : UserControl
{
    private readonly TableLayoutPanel _layout;
    private readonly CheckBox _enabled, _expanded, _top, _snap;
    private readonly TrackBar _opacity;
    private readonly Label _opacityText;
    private GfResourceMonitorPreferences _baseline = GfResourceMonitorPreferences.Default;
    private bool _loading;

    internal event EventHandler? PreferencesChanged;

    internal GfMonitorSettingsPanel()
    {
        Name = "GfMonitorSettingsPanel";
        AccessibleName = "Configurações do Monitor GF";
        Padding = new Padding(8);
        _layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Margin = Padding.Empty };
        _layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        _layout.ColumnStyles.Add(new(SizeType.Absolute, 54));
        foreach (int h in new[] { 26, 26, 26, 26, 26, 24, 36 })
            _layout.RowStyles.Add(new(SizeType.Absolute, h));
        Label heading = new() { Text = "Monitor GF", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Monitor GF", Margin = Padding.Empty };
        _layout.Controls.Add(heading, 0, 0); _layout.SetColumnSpan(heading, 2);
        CheckBox Choice(string name, string caption, int row)
        {
            var c = new CheckBox { Name = name, Text = caption, AccessibleName = caption, Dock = DockStyle.Fill,
                Margin = Padding.Empty, TabStop = true, UseVisualStyleBackColor = false };
            _layout.Controls.Add(c, 0, row); _layout.SetColumnSpan(c, 2);
            c.CheckedChanged += (_, _) => Changed();
            return c;
        }
        _enabled = Choice("GfMonitorEnabled", "Ativar Monitor GF", 1);
        _expanded = Choice("GfMonitorExpanded", "Restaurar no modo expandido", 2);
        _top = Choice("GfMonitorAlwaysOnTop", "Sempre no topo", 3);
        _snap = Choice("GfMonitorSnap", "Encaixar nas bordas", 4);
        _layout.Controls.Add(new Label { Text = "Opacidade", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Opacidade", Margin = Padding.Empty }, 0, 5);
        _opacityText = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Margin = Padding.Empty };
        _layout.Controls.Add(_opacityText, 1, 5);
        _opacity = new TrackBar { Name = "GfMonitorOpacity", Minimum = 40, Maximum = 100, Value = 100,
            TickFrequency = 10, SmallChange = 1, LargeChange = 5, Dock = DockStyle.Fill,
            TabStop = true, AccessibleName = "Opacidade do Monitor GF", Margin = Padding.Empty };
        _opacity.ValueChanged += (_, _) => Changed();
        _layout.Controls.Add(_opacity, 0, 6); _layout.SetColumnSpan(_opacity, 2);
        Controls.Add(_layout);
        Size = new Size(320, 206);
        LoadPreferences(_baseline);
        Configure(V269Theme.AzulGf);
    }

    internal void LoadPreferences(GfResourceMonitorPreferences value)
    {
        _baseline = value.Normalize();
        _loading = true;
        try
        {
            _enabled.Checked = _baseline.Enabled; _expanded.Checked = _baseline.Expanded;
            _top.Checked = _baseline.AlwaysOnTop; _snap.Checked = _baseline.SnapToEdges;
            _opacity.Value = _baseline.OpacityPercent;
            RefreshText();
        }
        finally { _loading = false; }
    }

    // A interface principal pode fornecer a posição atual da janela ao
    // confirmar mudanças, evitando sobrescrever posição movida pelo usuário.
    internal GfResourceMonitorPreferences ReadPreferences(GfResourceMonitorPreferences current) =>
        current.Normalize() with { Enabled = _enabled.Checked, Expanded = _expanded.Checked,
            AlwaysOnTop = _top.Checked, SnapToEdges = _snap.Checked, OpacityPercent = _opacity.Value };

    internal void Configure(V269Theme theme)
    {
        var p = V269VisualTokens.ForTheme(theme);
        BackColor = _layout.BackColor = p.Card; ForeColor = p.Text;
        foreach (Control control in _layout.Controls)
        {
            control.BackColor = p.Card;
            control.ForeColor = ReferenceEquals(control, _opacityText) ? p.SecondaryText : p.Text;
        }
    }

    private void RefreshText() => _opacityText.Text = _opacity.Value + "%";
    private void Changed()
    {
        if (_loading) return;
        RefreshText();
        PreferencesChanged?.Invoke(this, EventArgs.Empty);
    }
}
