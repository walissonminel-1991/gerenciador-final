using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Componente isolado da aba interna Monitor. O WORK aplica os mesmos
// controles visuais do shell; startup vem do Windows, não de JSON.
internal sealed class GfMonitorSettingsPanel : UserControl
{
    private readonly TableLayoutPanel _layout;
    private readonly CheckBox _enabled, _fixed, _overlay, _startup;
    private readonly TrackBar _scale, _opacity;
    private readonly Label _scaleText, _opacityText;
    private readonly ComboBox _theme;
    private bool _loading;
    internal event EventHandler? PreferencesChanged;
    internal event EventHandler? StartupChangeRequested;

    internal GfMonitorSettingsPanel()
    {
        Name = "GfMonitorSettingsPanel";
        AccessibleName = "Configurações do Monitor GF";
        Padding = new Padding(8);
        _layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, Margin = Padding.Empty };
        _layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        _layout.ColumnStyles.Add(new(SizeType.Absolute, 110));
        foreach (int h in new[] { 30,30,30,30,24,34,24,34,34 })
            _layout.RowStyles.Add(new(SizeType.Absolute, h));
        Label LabelFor(string text) => new() { Text = text, AccessibleName = text, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
        CheckBox Choice(string name, string caption, int row, bool auto = true)
        {
            var c = new CheckBox { Name = name, Text = "", AccessibleName = caption, Dock = DockStyle.Right,
                Width = 48, Margin = Padding.Empty, TabStop = true, AutoCheck = auto, UseVisualStyleBackColor = false,
                CheckAlign = ContentAlignment.MiddleRight };
            _layout.Controls.Add(LabelFor(caption),0,row);
            _layout.Controls.Add(c,1,row);
            if (auto) c.CheckedChanged += (_,_) => Changed();
            return c;
        }
        _enabled = Choice("GfMonitorEnabled", "Ativar Monitor GF", 0);
        _fixed = Choice("GfMonitorFixed", "Fixar", 1);
        _overlay = Choice("GfMonitorOverlay", "Sobreposição", 2);
        _startup = Choice("GfMonitorStartup", "Iniciar com o Windows", 3, false);
        _startup.Click += (_,_) => { if (!_loading && _startup.Enabled) StartupChangeRequested?.Invoke(this,EventArgs.Empty); };
        TrackBar Range(string name, string caption, int row, int min, int max, out Label value)
        {
            _layout.Controls.Add(LabelFor(caption),0,row);
            value = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Margin = Padding.Empty };
            _layout.Controls.Add(value,1,row);
            var range = new TrackBar { Name = name, Minimum = min, Maximum = max, Value = min,
                TickStyle = TickStyle.None, SmallChange = 1, LargeChange = 5, Dock = DockStyle.Fill,
                TabStop = true, AccessibleName = caption, Margin = Padding.Empty };
            _layout.Controls.Add(range,0,row+1); _layout.SetColumnSpan(range,2);
            range.ValueChanged += (_,_) => Changed();
            return range;
        }
        _scale = Range("GfMonitorScale", "Redimensionar", 4, GfResourceMonitorPreferences.MinScale, GfResourceMonitorPreferences.MaxScale, out _scaleText);
        _opacity = Range("GfMonitorTransparency", "Transparência", 6, 0, 60, out _opacityText);
        _theme = new ComboBox { Name = "GfMonitorTheme", AccessibleName = "Tema do Monitor", DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill, TabStop = true, Margin = Padding.Empty };
        _theme.Items.AddRange(["Clássico","Claro","Dark"]);
        _theme.SelectedIndexChanged += (_,_) => Changed();
        _layout.Controls.Add(LabelFor("Tema do Monitor"),0,8); _layout.Controls.Add(_theme,1,8);
        Controls.Add(_layout);
        Size = new Size(340,290);
        LoadPreferences(GfResourceMonitorPreferences.Default);
        LoadStartupState(null,false);
        Configure(V269Theme.AzulGf);
    }

    internal void LoadStartupState(bool? registered, bool canChange)
    {
        _startup.Checked = registered == true;
        _startup.Enabled = canChange && registered.HasValue;
        _startup.AccessibleDescription = registered.HasValue ? "Inicialização independente do GF." : "Estado da inicialização não confirmado.";
    }
    internal bool RequestedStartupState() => !_startup.Checked;

    internal void LoadPreferences(GfResourceMonitorPreferences value)
    {
        var p = value.Normalize();
        _loading = true;
        try
        {
            _enabled.Checked = p.Enabled; _fixed.Checked = p.Fixed; _overlay.Checked = p.AlwaysOnTop;
            _scale.Value = p.ScalePercent; _opacity.Value = 100-p.OpacityPercent;
            _theme.SelectedIndex = p.MonitorTheme switch { V269Theme.Claro => 1, V269Theme.Dark => 2, _ => 0 };
            RefreshText();
        }
        finally { _loading = false; }
    }

    internal GfResourceMonitorPreferences ReadPreferences(GfResourceMonitorPreferences current) =>
        current.Normalize() with { Enabled = _enabled.Checked, Fixed = _fixed.Checked, AlwaysOnTop = _overlay.Checked,
            ScalePercent = _scale.Value, OpacityPercent = 100-_opacity.Value,
            MonitorTheme = _theme.SelectedIndex switch { 1 => V269Theme.Claro, 2 => V269Theme.Dark, _ => V269Theme.AzulGf } };

    internal void Configure(V269Theme theme)
    {
        var p = V269VisualTokens.ForTheme(theme);
        BackColor = _layout.BackColor = p.Card; ForeColor = p.Text;
        foreach (Control control in _layout.Controls)
        {
            control.BackColor = p.Card;
            control.ForeColor = ReferenceEquals(control,_opacityText) || ReferenceEquals(control,_scaleText) ? p.SecondaryText : p.Text;
        }
    }

    private void RefreshText() { _opacityText.Text = _opacity.Value + "%"; _scaleText.Text = _scale.Value + "%"; }
    private void Changed()
    {
        if (_loading) return;
        RefreshText();
        PreferencesChanged?.Invoke(this,EventArgs.Empty);
    }
}
