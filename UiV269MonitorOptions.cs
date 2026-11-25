using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed class V269SettingsTab : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Selected { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color Accent { get; set; }

    internal V269SettingsTab(string name, string text)
    {
        Name = name;
        Text = AccessibleName = text;
        AccessibleRole = AccessibleRole.PageTab;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 3 || Height < 3) return;
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (Selected)
        {
            using var underline = new SolidBrush(Accent);
            e.Graphics.FillRectangle(underline, 1, Height - 2, Width - 2, 2);
        }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new TabAccessibility(this);

    private sealed class TabAccessibility(V269SettingsTab tab) : ControlAccessibleObject(tab)
    {
        public override AccessibleStates State => base.State | (tab.Selected ? AccessibleStates.Selected : AccessibleStates.Selectable);
        public override string DefaultAction => "Selecionar";
        public override void DoDefaultAction() => tab.PerformClick();
    }
}

internal sealed class V269MonitorOptions : V269SettingsCard
{
    private static readonly V269Theme[] Themes = [V269Theme.AzulGf, V269Theme.Claro, V269Theme.Dark];
    private readonly V269SettingsSwitch _enabled = new("V269MonitorEnabled", "Ativar Monitor GF");
    private readonly V269SettingsSwitch _fixed = new("V269MonitorFixed", "Fixar");
    private readonly V269SettingsSwitch _overlay = new("V269MonitorOverlay", "Sobreposição");
    private readonly V269SettingsSwitch _startup = new("V269MonitorStartup", "Iniciar com o Windows");
    private readonly V269SettingsRange _scale = new()
    {
        Name = "V269MonitorScale", AccessibleName = "Redimensionar",
        Minimum = GfResourceMonitorPreferences.MinScale, Maximum = GfResourceMonitorPreferences.MaxScale
    };
    private readonly V269SettingsRange _transparency = new()
    {
        Name = "V269MonitorTransparency", AccessibleName = "Transparência", Minimum = 0, Maximum = 60
    };
    private readonly V269SettingsCombo _theme = new() { Name = "V269MonitorTheme", AccessibleName = "Tema do Monitor" };
    private readonly Label[] _labels;
    private readonly Control[] _controls;
    private readonly int[] _dividers = new int[6];
    private bool _loading;

    internal event Action<Func<GfResourceMonitorPreferences, GfResourceMonitorPreferences>>? PreferenceChanged;
    internal event Action<int, int>? DisplaySizeChanged;
    internal event Action<bool>? StartupChanged;
    protected override bool PaintCard => false;

    internal V269MonitorOptions()
    {
        Name = "V269MonitorOptions";
        AccessibleName = "Preferências do Monitor GF";
        _controls = [_enabled, _fixed, _overlay, _startup, _scale, _transparency, _theme];
        _labels = _controls.Select((control, i) => TextLabel("V269MonitorLabel" + i, control.AccessibleName!)).ToArray();
        Controls.AddRange(_labels);
        Controls.AddRange(_controls);
        _theme.Items.AddRange(["Clássico", "Claro", "Dark"]);
        _enabled.Click += (_, _) => Change(p => p with { Enabled = !p.Enabled });
        _fixed.Click += (_, _) => Change(p => p with { Fixed = !p.Fixed });
        _overlay.Click += (_, _) => Change(p => p with { AlwaysOnTop = !p.AlwaysOnTop });
        _startup.Click += (_, _) => StartupChanged?.Invoke(!_startup.Checked);
        _scale.ValueChanged += (_, _) => UpdateSize();
        _transparency.ValueChanged += (_, _) => UpdateSize();
        _theme.SelectedIndexChanged += (_, _) =>
        {
            if (_theme.SelectedIndex < 0) return;
            var value = Themes[_theme.SelectedIndex];
            Change(p => p with { MonitorTheme = value });
        };
    }

    private void Change(Func<GfResourceMonitorPreferences, GfResourceMonitorPreferences> change)
    {
        if (!_loading) PreferenceChanged?.Invoke(change);
    }

    private void UpdateSize()
    {
        UpdateRangeLabels();
        if (!_loading) DisplaySizeChanged?.Invoke(_scale.Value, 100 - _transparency.Value);
    }

    private void UpdateRangeLabels()
    {
        _labels[4].Text = $"Redimensionar ({_scale.Value}%)";
        _labels[5].Text = $"Transparência ({_transparency.Value}%)";
    }

    internal void LoadObserved(GfResourceMonitorPreferences preferences, bool? startupRegistered)
    {
        _loading = true;
        try
        {
            _enabled.Checked = preferences.Enabled;
            _fixed.Checked = preferences.Fixed;
            _overlay.Checked = preferences.AlwaysOnTop;
            _startup.Checked = startupRegistered == true;
            _startup.Enabled = startupRegistered.HasValue;
            _labels[3].Text = startupRegistered == true ? "Iniciar com o Windows (registrado)" : "Iniciar com o Windows";
            _startup.AccessibleDescription = startupRegistered == true
                ? "Registro criado. A execução depende das configurações de Aplicativos de inicialização do Windows."
                : null;
            _scale.Value = preferences.ScalePercent;
            _transparency.Value = 100 - preferences.OpacityPercent;
            _theme.SelectedIndex = Array.IndexOf(Themes, preferences.MonitorTheme);
            UpdateRangeLabels();
        }
        finally { _loading = false; }
    }

    internal int ArrangeRows(int width)
    {
        int y = 0;
        for (int i = 0; i < _controls.Length; ++i)
        {
            var control = _controls[i];
            var label = _labels[i];
            StyleLabel(label, 12, true);
            control.Font = F(12);
            int controlWidth = control is V269SettingsSwitch ? S(47) : Math.Min(S(180), Math.Max(S(70), width / 3));
            int right = Math.Max(1, width - S(4));
            int copyWidth = Math.Max(1, right - controlWidth - S(24));
            int textHeight = TextRenderer.MeasureText(label.Text, label.Font, new Size(copyWidth, 1000),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
            // O TrackBar nativo usa altura automática por padrão. A barra
            // desenhada pelo GF tem altura explícita, como os demais comandos.
            if (control is V269SettingsRange slider) slider.AutoSize = false;
            control.Size = new Size(controlWidth, S(26));
            switch (control)
            {
                case V269SettingsSwitch sw: sw.Theme = Options.Theme; sw.Invalidate(); break;
                case V269SettingsRange range: range.ApplyOptions(Options); break;
                case V269SettingsCombo combo: combo.ApplyOptions(Options); break;
            }
            // ComboBox nativo pode impor altura própria após ajustar a fonte
            // e ItemHeight. Centralizar a altura efetiva, sem recortar a linha.
            int rowHeight = Math.Max(S(43), Math.Max(textHeight, control.Height) + S(14));
            label.Bounds = new Rectangle(S(4), y, copyWidth, rowHeight);
            control.Location = new Point(right - controlWidth, y + (rowHeight - control.Height) / 2);
            y += rowHeight;
            if (i < _dividers.Length) _dividers[i] = y - 1;
        }
        return y;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Rule);
        foreach (int y in _dividers)
            if (y > 0) e.Graphics.DrawLine(pen, S(4), y, Width - S(4), y);
    }
}
