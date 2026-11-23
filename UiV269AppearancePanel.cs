using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

/// <summary>
/// Preferências visuais, sem persistência, telemetria ou alterações no Windows.
/// O controlador real de Configurações será integrado somente pelo WORK.
/// </summary>
internal readonly record struct V269AppearanceOptions(
    V269Theme Theme, int InterfacePercent, int TextPercent, bool AnimationsEnabled)
{
    internal static V269AppearanceOptions Default => new(V269Theme.AzulGf, 100, 100, true);

    internal V269AppearanceOptions Normalize() => new(
        Enum.IsDefined(Theme) ? Theme : V269Theme.AzulGf,
        V269VisualTokens.InterfacePercent(InterfacePercent),
        V269VisualTokens.TextPercent(TextPercent), AnimationsEnabled);

    internal bool CanAnimate(bool reducedMotion) => AnimationsEnabled && !reducedMotion;
}

/// <summary>
/// Controles acessíveis de aparência V2.6.9; nenhum botão executa manutenção.
/// Retorna opções para o adaptador futuro sem gravá-las ou aplicá-las na MainForm.
/// </summary>
internal sealed class V269AppearancePanel : UserControl
{
    private readonly TableLayoutPanel _layout;
    private readonly ComboBox _theme;
    private readonly TrackBar _uiScale;
    private readonly TrackBar _textScale;
    private readonly Label _uiValue;
    private readonly Label _textValue;
    private readonly CheckBox _animations;
    private bool _updating;

    internal event EventHandler? AppearanceChanged;

    internal V269AppearancePanel()
    {
        Name = "V269AppearancePanel";
        AccessibleName = "Aparência da interface";
        Padding = new Padding(12);
        _layout = new TableLayoutPanel
        {
            Name = "V269AppearanceLayout", Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 7, Margin = Padding.Empty
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        foreach (int height in new[] { 28, 38, 28, 46, 28, 46, 39 })
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));

        Label Heading(string text, string name) => new()
        {
            Name = name, Text = text, TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill, Margin = Padding.Empty,
            AccessibleName = text
        };

        _theme = new ComboBox
        {
            Name = "V269ThemeChoice", Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList, TabStop = true,
            AccessibleName = "Tema da interface",
            IntegralHeight = false, Margin = new Padding(0, 0, 0, 5)
        };
        _theme.Items.AddRange(["Azul GF", "Dark", "Claro"]);

        _uiValue = Heading("100%", "V269UiPercent");
        _uiValue.TextAlign = ContentAlignment.MiddleRight;
        _textValue = Heading("100%", "V269TextPercent");
        _textValue.TextAlign = ContentAlignment.MiddleRight;
        _uiScale = new TrackBar
        {
            Name = "V269UiScale", Dock = DockStyle.Fill,
            Minimum = 90, Maximum = 110, Value = 100,
            SmallChange = 1, LargeChange = 5, TickFrequency = 5,
            AccessibleName = "Tamanho da interface", TabStop = true,
            Margin = Padding.Empty
        };
        _textScale = new TrackBar
        {
            Name = "V269TextScale", Dock = DockStyle.Fill,
            Minimum = 90, Maximum = 115, Value = 100,
            SmallChange = 1, LargeChange = 5, TickFrequency = 5,
            AccessibleName = "Tamanho dos textos", TabStop = true,
            Margin = Padding.Empty
        };
        _animations = new CheckBox
        {
            Name = "V269Animations", Text = "Animações da interface",
            Dock = DockStyle.Fill, Checked = true, TabStop = true,
            AccessibleName = "Animações da interface", Margin = Padding.Empty
        };

        _layout.Controls.Add(Heading("Tema", "V269ThemeLabel"), 0, 0);
        _layout.SetColumnSpan(_layout.Controls[_layout.Controls.Count - 1], 2);
        _layout.Controls.Add(_theme, 0, 1);
        _layout.SetColumnSpan(_theme, 2);
        _layout.Controls.Add(Heading("Tamanho da interface", "V269UiScaleLabel"), 0, 2);
        _layout.Controls.Add(_uiValue, 1, 2);
        _layout.Controls.Add(_uiScale, 0, 3);
        _layout.SetColumnSpan(_uiScale, 2);
        _layout.Controls.Add(Heading("Tamanho do texto", "V269TextScaleLabel"), 0, 4);
        _layout.Controls.Add(_textValue, 1, 4);
        _layout.Controls.Add(_textScale, 0, 5);
        _layout.SetColumnSpan(_textScale, 2);
        _layout.Controls.Add(_animations, 0, 6);
        _layout.SetColumnSpan(_animations, 2);
        Controls.Add(_layout);

        SetOptions(V269AppearanceOptions.Default);
        _theme.SelectedIndexChanged += (_, _) => Changed();
        _uiScale.ValueChanged += (_, _) => Changed();
        _textScale.ValueChanged += (_, _) => Changed();
        _animations.CheckedChanged += (_, _) => Changed();
    }

    internal V269AppearanceOptions GetOptions() => new(
        (V269Theme)Math.Max(0, _theme.SelectedIndex),
        _uiScale.Value, _textScale.Value, _animations.Checked);

    internal void SetOptions(V269AppearanceOptions options)
    {
        options = options.Normalize();
        _updating = true;
        try
        {
            _theme.SelectedIndex = (int)options.Theme;
            _uiScale.Value = options.InterfacePercent;
            _textScale.Value = options.TextPercent;
            _animations.Checked = options.AnimationsEnabled;
            RefreshLabels();
            ApplyPalette(V269VisualTokens.ForTheme(options.Theme));
        }
        finally { _updating = false; }
    }

    internal void ApplyPalette(V269Palette palette)
    {
        BackColor = palette.Card;
        ForeColor = palette.Text;
        _layout.BackColor = palette.Card;
        foreach (Control child in _layout.Controls)
            child.ForeColor = child == _uiValue || child == _textValue
                ? palette.SecondaryText : palette.Text;
        _theme.BackColor = palette.Content;
        _theme.ForeColor = palette.Text;
        _uiScale.BackColor = palette.Card;
        _textScale.BackColor = palette.Card;
    }

    private void RefreshLabels()
    {
        _uiValue.Text = _uiScale.Value + "%";
        _textValue.Text = _textScale.Value + "%";
    }

    private void Changed()
    {
        if (_updating) return;
        RefreshLabels();
        ApplyPalette(V269VisualTokens.ForTheme(GetOptions().Theme));
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }
}
