using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal enum V269Page
{
    Inicio,
    Aplicativos,
    Drivers,
    Limpeza,
    Reparo,
    Sistema,
    Inicializacao,
    Configuracoes
}

internal enum V269PanelKind
{
    Details,
    Confirmation,
    Progress,
    Result,
    History
}

/// <summary>
/// Estado exclusivamente visual do painel. O serviço de domínio conserva
/// autorização, revalidação, cancelamento e evidências de execução.
/// </summary>
internal sealed record V269PanelPresentation(
    V269PanelKind Kind, string Title, string Description, int? ProgressPercent = null);

internal sealed class V269NavigationEventArgs(V269Page page) : EventArgs
{
    internal V269Page Page { get; } = page;
}

/// <summary>
/// Pane nativo; nenhuma ação administrativa nasce aqui.
/// Os estados vêm de um controlador funcional integrado em etapa posterior.
/// </summary>
internal sealed class V269RightPane : UserControl
{
    private readonly TableLayoutPanel _layout;
    private readonly Label _title;
    private readonly Label _description;
    private readonly ProgressBar _progress;

    internal V269PanelPresentation? Current { get; private set; }

    internal V269RightPane()
    {
        Name = "V269RightPane";
        AccessibleName = "Painel direito de detalhes e operações";
        Padding = new Padding(13);
        _layout = new TableLayoutPanel
        {
            Name = "V269RightContent",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            BackColor = SystemColors.Control
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _title = new Label
        {
            Name = "V269PanelTitle", AccessibleName = "Título do painel", Dock = DockStyle.Fill,
            AutoEllipsis = true, Text = "Detalhes",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };
        _progress = new ProgressBar
        {
            Name = "V269PanelProgress", AccessibleName = "Progresso da operação", Dock = DockStyle.Fill,
            Minimum = 0, Maximum = 100, Visible = false,
            Margin = new Padding(0, 3, 0, 10),
            Style = ProgressBarStyle.Continuous
        };
        _description = new Label
        {
            Name = "V269PanelDescription", Dock = DockStyle.Fill,
            AutoSize = false, TextAlign = ContentAlignment.TopLeft,
            Margin = Padding.Empty,
            AccessibleName = "Descrição e estado da operação"
        };
        _layout.Controls.Add(_title, 0, 0);
        _layout.Controls.Add(_progress, 0, 1);
        _layout.Controls.Add(_description, 0, 2);
        Controls.Add(_layout);
    }

    internal void Present(V269PanelPresentation? presentation)
    {
        Current = presentation;
        _title.Text = presentation?.Title ?? "Detalhes";
        _description.Text = presentation?.Description ?? string.Empty;
        bool inProgress = presentation?.Kind == V269PanelKind.Progress;
        _progress.Visible = inProgress;
        _layout.RowStyles[1].Height = inProgress ? 34 : 0;
        _progress.Value = Math.Clamp(presentation?.ProgressPercent ?? 0, 0, 100);
        _progress.AccessibleDescription = inProgress ? _progress.Value + "%" : string.Empty;
        AccessibleDescription = _description.Text;
    }

    internal void ApplyPalette(V269Palette palette)
    {
        BackColor = palette.RightPanel;
        _layout.BackColor = palette.RightPanel;
        ForeColor = palette.Text;
        _title.ForeColor = palette.Text;
        _description.ForeColor = palette.SecondaryText;
    }

    internal void SetFonts(Font title, Font body)
    {
        _title.Font = title;
        _description.Font = body;
    }
}

/// <summary>
/// Shell nativo V2.6.9 (etapa A). Não substitui a MainForm nem contém
/// inventário sintético. As views reais são anexadas por SetPageContent
/// quando a integração de cada aba estiver autorizada.
/// </summary>
internal sealed class V269Shell : UserControl
{
    private static readonly (V269Page Page, string Title, string Subtitle)[] Pages =
    [
        (V269Page.Inicio, "Início", "Informações deste computador"),
        (V269Page.Aplicativos, "Aplicativos", "Programas instalados e ações elegíveis"),
        (V269Page.Drivers, "Drivers", "Dispositivos e drivers"),
        (V269Page.Limpeza, "Limpeza e Desempenho", "Prévia e manutenção controlada"),
        (V269Page.Reparo, "Reparo e Rede", "Diagnósticos e reparos supervisionados"),
        (V269Page.Sistema, "Sistema", "Windows Update, energia e memória"),
        (V269Page.Inicializacao, "Inicialização", "Itens de inicialização"),
        (V269Page.Configuracoes, "Configurações", "Preferências e consentimento")
    ];

    private readonly Dictionary<V269Page, V269NavButton> _navigation = new();
    private readonly Dictionary<V269Page, Control> _pages = new();
    private readonly List<Font> _ownedFonts = [];
    private readonly TableLayoutPanel _columns;
    private readonly TableLayoutPanel _navigationLayout;
    private readonly Panel _sidebar;
    private readonly Panel _bodyHost;
    private readonly Panel _content;
    private readonly Label _pageTitle;
    private readonly Label _pageSubtitle;
    private readonly V269RightPane _rightPane;

    internal event EventHandler<V269NavigationEventArgs>? NavigationChanged;
    internal V269Theme Theme { get; private set; } = V269Theme.AzulGf;
    internal int InterfaceScalePercent { get; private set; } = 100;
    internal int TextScalePercent { get; private set; } = 100;
    internal V269Page SelectedPage { get; private set; } = V269Page.Inicio;
    internal V269RightPane RightPane => _rightPane;

    internal V269Shell()
    {
        Name = "V269Shell";
        AccessibleName = "Gerenciador Final - interface V2.6.9";
        Dock = DockStyle.Fill;
        _columns = new TableLayoutPanel
        {
            Name = "V269Columns", Dock = DockStyle.Fill,
            ColumnCount = 3, RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(8)
        };
        _columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        _columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        _columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _sidebar = new Panel { Name = "V269Sidebar", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 7, 0) };
        _navigationLayout = new TableLayoutPanel
        {
            Name = "V269Navigation", Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = Pages.Length + 1,
            Margin = Padding.Empty, Padding = new Padding(6)
        };
        _navigationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < Pages.Length; i++)
            _navigationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        _navigationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        foreach (var (page, title, _) in Pages)
        {
            var button = new V269NavButton(page)
            {
                Name = "V269Nav" + page, Text = title,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2),
                TabStop = true,
                AccessibleName = "Abrir " + title
            };
            button.Click += (_, _) => SelectPage(page);
            button.PreviewKeyDown += (_, e) =>
            {
                if (e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End)
                    e.IsInputKey = true;
            };
            button.KeyDown += (_, e) => HandleNavigationKeys(page, e);
            _navigation[page] = button;
            _navigationLayout.Controls.Add(button, 0, (int)page);
        }
        _sidebar.Controls.Add(_navigationLayout);

        _content = new Panel { Name = "V269Main", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 7, 0) };
        var central = new TableLayoutPanel
        {
            Name = "V269MainLayout", Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 2,
            Margin = Padding.Empty, Padding = new Padding(13, 8, 13, 10)
        };
        central.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        central.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        central.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new TableLayoutPanel
        {
            Name = "V269Heading", Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 2, Margin = Padding.Empty
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        _pageTitle = new Label
        {
            Name = "V269PageTitle", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
            Margin = Padding.Empty
        };
        _pageSubtitle = new Label
        {
            Name = "V269PageSubtitle", Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true,
            Margin = Padding.Empty
        };
        heading.Controls.Add(_pageTitle, 0, 0);
        heading.Controls.Add(_pageSubtitle, 0, 1);
        _bodyHost = new Panel
        {
            Name = "V269BodyHost", Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        central.Controls.Add(heading, 0, 0);
        central.Controls.Add(_bodyHost, 0, 1);
        _content.Controls.Add(central);

        _rightPane = new V269RightPane
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty
        };
        _columns.Controls.Add(_sidebar, 0, 0);
        _columns.Controls.Add(_content, 1, 0);
        _columns.Controls.Add(_rightPane, 2, 0);
        Controls.Add(_columns);

        SizeChanged += (_, _) => ApplyDimensions();
        ApplyAppearance(V269Theme.AzulGf, 100, 100);
        SelectPage(V269Page.Inicio);
    }

    internal void SetPageContent(V269Page page, Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!Enum.IsDefined(page)) throw new ArgumentOutOfRangeException(nameof(page));
        if (_pages.Any(item => item.Key != page && ReferenceEquals(item.Value, control)))
            throw new InvalidOperationException("Uma view não pode pertencer a duas abas.");
        if (control.Parent is not null && control.Parent != _bodyHost)
            throw new InvalidOperationException("A view ainda pertence a outro contêiner.");
        if (_pages.TryGetValue(page, out Control? old) && !ReferenceEquals(old, control))
            _bodyHost.Controls.Remove(old);
        _pages[page] = control;
        control.Dock = DockStyle.Fill;
        control.Margin = Padding.Empty;
        if (control.Parent != _bodyHost) _bodyHost.Controls.Add(control);
        RefreshVisiblePage();
    }

    internal Control? PageContent(V269Page page) =>
        _pages.TryGetValue(page, out var content) ? content : null;

    internal void SelectPage(V269Page page)
    {
        if (!Enum.IsDefined(page)) throw new ArgumentOutOfRangeException(nameof(page));
        bool changed = SelectedPage != page;
        SelectedPage = page;
        var descriptor = Pages[(int)page];
        _pageTitle.Text = descriptor.Title;
        _pageSubtitle.Text = descriptor.Subtitle;
        RefreshVisiblePage();
        ApplyNavigationSelection(V269VisualTokens.ForTheme(Theme));
        if (changed) NavigationChanged?.Invoke(this, new V269NavigationEventArgs(page));
    }

    internal void PresentPanel(V269PanelPresentation? presentation) =>
        _rightPane.Present(presentation);

    internal void ApplyAppearance(V269Theme theme, int interfacePercent, int textPercent)
    {
        Theme = Enum.IsDefined(theme) ? theme : V269Theme.AzulGf;
        InterfaceScalePercent = V269VisualTokens.InterfacePercent(interfacePercent);
        TextScalePercent = V269VisualTokens.TextPercent(textPercent);
        V269Palette palette = V269VisualTokens.ForTheme(Theme);

        SuspendLayout();
        BackColor = palette.Background;
        ForeColor = palette.Text;
        _columns.BackColor = palette.Background;
        _sidebar.BackColor = palette.Sidebar;
        _navigationLayout.BackColor = palette.Sidebar;
        _content.BackColor = palette.Content;
        _bodyHost.BackColor = palette.Content;
        foreach (var name in new[] { "V269MainLayout", "V269Heading" })
            foreach (Control layout in _content.Controls.Find(name, true))
                layout.BackColor = palette.Content;
        _rightPane.ApplyPalette(palette);
        _pageTitle.ForeColor = palette.Text;
        _pageSubtitle.ForeColor = palette.SecondaryText;
        ApplyFonts();
        ApplyNavigationSelection(palette);
        ApplyDimensions();
        ResumeLayout(true);
    }

    private void ApplyFonts()
    {
        var previous = _ownedFonts.ToArray();
        _ownedFonts.Clear();
        Font NewFont(float size, FontStyle style = FontStyle.Regular)
        {
            var value = new Font("Segoe UI", size * TextScalePercent / 100f, style);
            _ownedFonts.Add(value);
            return value;
        }
        Font = NewFont(9.5f);
        _pageTitle.Font = NewFont(17f, FontStyle.Bold);
        _pageSubtitle.Font = NewFont(9f);
        _rightPane.SetFonts(NewFont(12f, FontStyle.Bold), NewFont(9.5f));
        foreach (var navButton in _navigation.Values)
            navButton.Font = NewFont(10f);
        foreach (var old in previous) old.Dispose();
    }

    private void ApplyDimensions()
    {
        if (_columns is null || _navigationLayout is null) return;
        int width = Math.Max(1, ClientSize.Width);
        // São dimensões preliminares: a homologação completa de DPI virá ao final.
        int sidebar = V269VisualTokens.Scale(width < 1040 ? 166 : 210, InterfaceScalePercent);
        int right = V269VisualTokens.Scale(width < 1040 ? 240 : 310, InterfaceScalePercent);
        _columns.ColumnStyles[0].Width = sidebar;
        _columns.ColumnStyles[2].Width = right;
        for (int i = 0; i < Pages.Length; i++)
            _navigationLayout.RowStyles[i].Height = V269VisualTokens.Scale(48, InterfaceScalePercent);
    }

    private void RefreshVisiblePage()
    {
        // Não reconstruir ou descartar views: seleção, rolagem e operações
        // devem continuar sob controle da aba/motor que as criou.
        foreach (var (page, content) in _pages)
            content.Visible = page == SelectedPage;
        if (_pages.TryGetValue(SelectedPage, out Control? active))
            active.BringToFront();
    }

    private void ApplyNavigationSelection(V269Palette palette)
    {
        foreach (var (page, button) in _navigation)
        {
            bool selected = page == SelectedPage;
            button.BackColor = selected ? palette.Selection : palette.Sidebar;
            button.ForeColor = palette.Text;
            button.IconColor = page switch
            {
                V269Page.Inicio => Color.FromArgb(77, 179, 236),
                V269Page.Aplicativos => Color.FromArgb(172, 127, 235),
                V269Page.Drivers => Color.FromArgb(239, 158, 96),
                V269Page.Limpeza => Color.FromArgb(81, 196, 155),
                V269Page.Reparo => Color.FromArgb(231, 189, 95),
                V269Page.Sistema => Color.FromArgb(110, 173, 227),
                V269Page.Inicializacao => Color.FromArgb(218, 175, 100),
                _ => Color.FromArgb(175, 133, 229)
            };
            button.IsSelected = selected;
            button.AccessibleDescription = selected ? "Aba atual" : "Abrir aba";
            button.Invalidate();
        }
    }

    private void HandleNavigationKeys(V269Page page, KeyEventArgs e)
    {
        int next = e.KeyCode switch
        {
            Keys.Down => ((int)page + 1) % Pages.Length,
            Keys.Up => ((int)page + Pages.Length - 1) % Pages.Length,
            Keys.Home => 0,
            Keys.End => Pages.Length - 1,
            _ => -1
        };
        if (next < 0) return;
        e.Handled = true;
        e.SuppressKeyPress = true;
        _navigation[(V269Page)next].Focus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var font in _ownedFonts) font.Dispose();
            _ownedFonts.Clear();
        }
        base.Dispose(disposing: disposing);
    }

    private sealed class V269NavButton : Button
    {
        internal V269Page Page { get; }
        // Campos internos: não são propriedades serializáveis pelo Designer WinForms.
        internal Color IconColor;
        internal bool IsSelected;

        internal V269NavButton(V269Page page)
        {
            Page = page;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(32, 0, 2, 0);
            UseVisualStyleBackColor = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(IconColor, 2f);
            using var brush = new SolidBrush(IconColor);
            int y = (Height - 16) / 2;
            if (Page is V269Page.Aplicativos or V269Page.Configuracoes)
            {
                for (int row = 0; row < 2; row++)
                    for (int col = 0; col < 2; col++)
                        e.Graphics.DrawRectangle(pen, 10 + col * 8, y + row * 8, 5, 5);
            }
            else if (Page is V269Page.Drivers or V269Page.Sistema)
            {
                e.Graphics.DrawRectangle(pen, 11, y + 2, 14, 12);
                e.Graphics.DrawLine(pen, 18, y, 18, y + 2);
                e.Graphics.DrawLine(pen, 18, y + 14, 18, y + 16);
            }
            else if (Page is V269Page.Limpeza or V269Page.Reparo)
            {
                e.Graphics.DrawEllipse(pen, 11, y + 2, 13, 13);
                e.Graphics.FillEllipse(brush, 16, y + 7, 4, 4);
            }
            else
            {
                e.Graphics.DrawLine(pen, 10, y + 12, 14, y + 6);
                e.Graphics.DrawLine(pen, 14, y + 6, 18, y + 10);
                e.Graphics.DrawLine(pen, 18, y + 10, 25, y + 2);
            }
        }
    }
}
