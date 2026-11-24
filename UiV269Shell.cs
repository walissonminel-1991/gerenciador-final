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
    private readonly TextBox _description;
    private readonly ProgressBar _progress;
    private readonly Panel _operationContent;
    private readonly FlowLayoutPanel _choices;
    private TaskCompletionSource<bool>? _confirmation;

    internal event EventHandler? PresentationChanged;
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
            RowCount = 5,
            Margin = Padding.Empty,
            BackColor = SystemColors.Control
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));

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
        _description = new TextBox
        {
            Name = "V269PanelDescription", Dock = DockStyle.Fill,
            Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.Vertical,
            Margin = Padding.Empty,
            AccessibleName = "Descrição e estado da operação"
        };
        _layout.Controls.Add(_title, 0, 0);
        _layout.Controls.Add(_progress, 0, 1);
        _layout.Controls.Add(_description, 0, 2);
        _choices = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false, Visible = false };
        foreach (var (caption, accepted) in new[] { ("Cancelar", false), ("Confirmar", true) })
        {
            var button = new Button
            {
                Name = accepted ? "V269AcceptConfirmation" : "V269CancelConfirmation",
                Text = caption, AutoSize = true, MinimumSize = new Size(70, 26)
            };
            button.Click += (_, _) => ResolveConfirmation(accepted);
            _choices.Controls.Add(button);
        }
        _operationContent = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        _layout.Controls.Add(_choices, 0, 3);
        _layout.Controls.Add(_operationContent, 0, 4);
        Controls.Add(_layout);
        Present(null);
    }

    internal void SetOperationContent(Control content)
    {
        _operationContent.Controls.Add(content);
        _layout.RowStyles[4].SizeType = SizeType.Percent;
        _layout.RowStyles[4].Height = 55;
        _layout.RowStyles[2].Height = 45;
    }

    internal async Task<bool> ConfirmAsync(string title, string description, CancellationToken token)
    {
        if (_confirmation is not null || IsDisposed || token.IsCancellationRequested) return false;
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previous = Current;
        _confirmation = pending;
        try
        {
            _layout.RowStyles[3].Height = 38;
            _choices.Visible = true;
            PresentCore(new(V269PanelKind.Confirmation, title, description));
            _layout.PerformLayout();
            _choices.Controls[0].Focus();
            return await pending.Task.WaitAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
        finally
        {
            _confirmation = null;
            if (!IsDisposed)
            {
                _choices.Visible = false;
                _layout.RowStyles[3].Height = 0;
                PresentCore(previous);
            }
        }
    }

    internal void ResolveConfirmation(bool accepted) => _confirmation?.TrySetResult(accepted);

    internal void Present(V269PanelPresentation? presentation)
    {
        if (_confirmation is null) PresentCore(presentation);
    }

    private void PresentCore(V269PanelPresentation? presentation)
    {
        Current = presentation;
        _title.Text = presentation?.Title ?? "Detalhes";
        _description.Text = presentation?.Description ?? string.Empty;
        bool inProgress = presentation is { Kind: V269PanelKind.Progress, ProgressPercent: not null };
        _progress.Visible = inProgress;
        _layout.RowStyles[1].Height = inProgress ? 34 : 0;
        _progress.Value = Math.Clamp(presentation?.ProgressPercent ?? 0, 0, 100);
        _progress.AccessibleDescription = inProgress ? _progress.Value + "%" : string.Empty;
        AccessibleDescription = presentation?.Title ?? "";
        PresentationChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ApplyPalette(V269Palette palette)
    {
        BackColor = palette.RightPanel;
        _layout.BackColor = palette.RightPanel;
        ForeColor = palette.Text;
        _title.ForeColor = palette.Text;
        _description.BackColor = palette.RightPanel;
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
        (V269Page.Inicio, "Início", "Informações gerais do sistema e do hardware."),
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
    // WinForms pode manter uma instância Font anterior quando o novo
    // valor é equivalente. Cache finito pelas escalas normalizadas:
    // nunca destruir uma fonte enquanto algum controle pode referenciá-la.
    private readonly Dictionary<(float Size, FontStyle Style, int Scale), Font> _ownedFonts = new();
    private int _appliedTextScale;
    private readonly TableLayoutPanel _columns;
    private readonly TableLayoutPanel _navigationLayout;
    private readonly Panel _sidebar;
    private readonly Panel _bodyHost;
    private readonly Panel _content;
    private readonly Label _pageTitle;
    private readonly Label _pageSubtitle;
    private readonly Control _homeGlyph;
    private readonly TableLayoutPanel _centralLayout;
    private readonly TableLayoutPanel _headingLayout;
    private readonly V269RightPane _rightPane;
    private bool _homeGeometry;

    internal event EventHandler<V269NavigationEventArgs>? NavigationChanged;
    internal V269Theme Theme { get; private set; } = V269Theme.AzulGf;
    internal int InterfaceScalePercent { get; private set; } = 100;
    internal int TextScalePercent { get; private set; } = 100;
    internal V269Page SelectedPage { get; private set; } = V269Page.Inicio;
    internal V269RightPane RightPane => _rightPane;
    private Button? _paneToggle;
    private bool _compactPaneOpen;
    private bool? _dashboardPaneOpen;

    internal void SetOperationContent(Control content)
    {
        _rightPane.SetOperationContent(content);
        var toggle = new Button { Name = "V269PaneToggle", Text = "Painel",
            AccessibleName = "Mostrar ou ocultar painel de operações",
            Dock = DockStyle.Top, Height = 30, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 3, 0, 0) };
        _paneToggle = toggle;
        toggle.Click += (_, _) =>
        {
            bool open = !_rightPane.Visible;
            if (SelectedPage is V269Page.Inicio or V269Page.Configuracoes)
                _dashboardPaneOpen = open;
            else
                _compactPaneOpen = open;
            ApplyDimensions();
        };
        _navigationLayout.Controls.Add(toggle, 0, Pages.Length);
        ApplyDimensions();
    }

    internal void SetPageInteraction(bool enabled) => _bodyHost.Enabled = enabled;

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
            Name = "V269Navigation", AccessibleName = "Navegação principal",
            AccessibleRole = AccessibleRole.Grouping, Dock = DockStyle.Fill,
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
        _centralLayout = central;
        central.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        central.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        central.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new TableLayoutPanel
        {
            Name = "V269Heading", Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 2, Margin = Padding.Empty
        };
        _headingLayout = heading;
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
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
        _homeGlyph = new V269HomeHeadingIcon { Name = "V269HomeHeadingIcon", Dock = DockStyle.Fill,
            Margin = Padding.Empty, Visible = false, AccessibleRole = AccessibleRole.Graphic };
        heading.Controls.Add(_homeGlyph, 0, 0);
        heading.SetRowSpan(_homeGlyph, 2);
        heading.Controls.Add(_pageTitle, 1, 0);
        heading.Controls.Add(_pageSubtitle, 1, 1);
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

        _rightPane.PresentationChanged += (_, _) =>
        {
            if (_rightPane.Current?.Kind is V269PanelKind.Confirmation or V269PanelKind.Progress
                or V269PanelKind.Result or V269PanelKind.History)
            {
                _compactPaneOpen = true;
                _dashboardPaneOpen = null;
            }
            ApplyDimensions();
        };
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
        if (changed) _dashboardPaneOpen = null;
        SelectedPage = page;
        var descriptor = Pages[(int)page];
        _pageTitle.Text = descriptor.Title;
        _pageSubtitle.Text = descriptor.Subtitle;
        RefreshVisiblePage();
        ApplyNavigationSelection(V269VisualTokens.ForTheme(Theme));
        ApplyDimensions();
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
        _homeGlyph.ForeColor = Theme switch {
            V269Theme.Dark => ColorTranslator.FromHtml("#65b8ef"),
            V269Theme.Claro => ColorTranslator.FromHtml("#2b79a8"),
            _ => ColorTranslator.FromHtml("#69bfe0") };
        ApplyFonts();
        ApplyNavigationSelection(palette);
        ApplyDimensions();
        ResumeLayout(true);
    }

    private void ApplyFonts()
    {
        if (_appliedTextScale == TextScalePercent) return;
        _appliedTextScale = TextScalePercent;
        Font NewFont(float size, FontStyle style = FontStyle.Regular)
        {
            var key = (size, style, TextScalePercent);
            if (_ownedFonts.TryGetValue(key, out Font? existing)) return existing;
            var value = new Font("Segoe UI", size * TextScalePercent / 100f, style);
            _ownedFonts.Add(key, value);
            return value;
        }
        Font = NewFont(9.5f);
        _pageTitle.Font = NewFont(_homeGeometry ? 18f : 17f, FontStyle.Bold);
        _pageSubtitle.Font = NewFont(9f);
        _rightPane.SetFonts(NewFont(12f, FontStyle.Bold), NewFont(9.5f));
        foreach (var navButton in _navigation.Values)
            navButton.Font = NewFont(_homeGeometry ? 9f : 10f);
        // As fontes são liberadas somente após a destruição dos controles.
    }

    private void ApplyDimensions()
    {
        if (_columns is null || _navigationLayout is null) return;
        int width = Math.Max(1, ClientSize.Width);
        bool home = SelectedPage == V269Page.Inicio;
        int windowWidth = (int)Math.Round((FindForm()?.ClientSize.Width ?? width) * 96d / DeviceDpi);
        // Início usa os breakpoints do CSS congelado, medidos pela janela.
        int S(int n) => V269VisualTokens.Scale(n, InterfaceScalePercent);
        if (_homeGeometry != home)
        {
            _homeGeometry = home;
            _appliedTextScale = 0;
            ApplyFonts();
        }
        _homeGlyph.Visible = home;
        _headingLayout.ColumnStyles[0].Width = home ? S(57) : 0;
        _centralLayout.RowStyles[0].Height = home ? S(windowWidth <= 1040 ? 54 : 65) : 70;
        // As demais abas conservam as dimensões existentes nesta frente.
        int sidebar = V269VisualTokens.Scale(width < 1040 ? 166 : 210, InterfaceScalePercent);
        int right = V269VisualTokens.Scale(width < 1040 ? 240 : 310, InterfaceScalePercent);
        // O HTML aprovado reserva toda a área útil a Início e Configurações.
        // A exceção é um fluxo de operação: confirmação/progresso não podem
        // desaparecer, e o resultado/histórico continua acessível entre abas.
        bool dashboard = SelectedPage is V269Page.Inicio or V269Page.Configuracoes;
        bool mandatory = _rightPane.Current?.Kind is V269PanelKind.Confirmation or V269PanelKind.Progress;
        bool retained = _rightPane.Current?.Kind is V269PanelKind.Result or V269PanelKind.History;
        bool showRight = !dashboard || mandatory || retained;
        if (_paneToggle is { } toggle)
        {
            bool compact = width < 1060;
            // O painel é fechado por padrão no dashboard, mas o histórico
            // e a exportação local continuam acessíveis por comando explícito.
            toggle.Visible = compact || dashboard;
            var palette = V269VisualTokens.ForTheme(Theme);
            toggle.BackColor = palette.Sidebar;
            toggle.ForeColor = palette.Text;
            toggle.Enabled = !mandatory;
            showRight = mandatory || (dashboard
                ? _dashboardPaneOpen ?? (retained && (!compact || _compactPaneOpen))
                : !compact || _compactPaneOpen);
            toggle.Text = showRight ? "Fechar painel" : "Abrir painel";
            if (compact) sidebar = V269VisualTokens.Scale(136, InterfaceScalePercent);
        }
        if (!showRight) right = 0;
        if (home)
        {
            // As medidas CSS já incluem borda e padding da barra lateral.
            int sidebarCss = windowWidth <= 1040 ? 175 : windowWidth <= 1250 ? 195 : 236;
            sidebar = S(sidebarCss + 11);
            _columns.Padding = new Padding(S(11));
            _sidebar.Margin = new Padding(0, 0, S(11), 0);
            _content.Margin = new Padding(0, 0, showRight ? S(10) : 0, 0);
            _navigationLayout.Padding = new Padding(S(8), S(9), S(8), S(9));
        }
        else
        {
            _columns.Padding = new Padding(8);
            _sidebar.Margin = new Padding(0, 0, 7, 0);
            _content.Margin = new Padding(0, 0, 7, 0);
            _navigationLayout.Padding = new Padding(6);
        }
        _columns.ColumnStyles[0].Width = sidebar;
        _columns.ColumnStyles[2].Width = right;
        _rightPane.Visible = right > 0;
        int navHeight = _paneToggle is null ? 48 :
            Math.Clamp((ClientSize.Height - 64) / Pages.Length, 27, 48);
        if (home)
        {
            int desired = windowWidth <= 1040 ? 54 : 55;
            int available = Math.Max(1, ClientSize.Height - _columns.Padding.Vertical -
                _navigationLayout.Padding.Vertical - (_paneToggle?.Visible == true ? S(34) : 0));
            navHeight = Math.Min(desired, Math.Max(20, (int)Math.Floor(available * 100d /
                InterfaceScalePercent / Pages.Length)));
        }
        for (int i = 0; i < Pages.Length; i++)
            _navigationLayout.RowStyles[i].Height = V269VisualTokens.Scale(navHeight, InterfaceScalePercent);
        foreach (var button in _navigation.Values)
        {
            button.HomePresentation = home;
            button.CurrentTheme = Theme;
            button.Invalidate();
        }
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
            // Como no HTML V2.6.9: texto claro ao selecionar fundo azul
            // no tema Claro, e texto suave claro no Dark.
            button.ForeColor = selected
                ? Theme == V269Theme.Dark ? Color.FromArgb(242, 247, 251) : Color.White
                : palette.Text;
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
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (var font in _ownedFonts.Values) font.Dispose();
            _ownedFonts.Clear();
        }
    }

    private sealed class V269NavButton : Button
    {
        internal V269Page Page { get; }
        // Campos internos: não são propriedades serializáveis pelo Designer WinForms.
        internal Color IconColor;
        internal bool IsSelected;
        internal bool HomePresentation;
        internal V269Theme CurrentTheme;

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
            if (HomePresentation)
            {
                PaintHomeNavigation(e.Graphics);
                return;
            }
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

        // Cores semânticas efetivas da cascata CSS congelada. Em Dark,
        // a regra final de data-gf-icon prevalece também na seleção.
        internal static Color HomeIconColor(V269Theme theme, V269Page page, bool selected)
        {
            if (selected && theme != V269Theme.Dark) return Color.White;
            string[] colors = theme switch
            {
                V269Theme.Dark => ["#65b8ef", "#b998f2", "#ee9d68", "#55cbb5",
                    "#e4bb67", "#65b8ef", "#e4bb67", "#b998f2"],
                V269Theme.Claro => ["#2891db", "#7046c1", "#bc592b", "#087d82",
                    "#ad6f1d", "#167bbb", "#ad6f1d", "#7046c1"],
                _ => ["#77cbff", "#b7a5f5", "#e8a784", "#6bd6b2",
                    "#ecc27c", "#75c4ed", "#ecc27c", "#b7a5f5"]
            };
            return ColorTranslator.FromHtml(colors[(int)page]);
        }

        private void PaintHomeNavigation(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var theme = CurrentTheme;
            Color H(string h) => ColorTranslator.FromHtml(h);
            Color background = Parent?.BackColor ?? BackColor;
            g.Clear(background);
            var area = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using var shape = Rounded(area, 9);
            if (IsSelected)
            {
                var (first,last,border) = theme switch {
                    V269Theme.Dark => (H("#666d74"),H("#50575e"),H("#b6c0c8")),
                    V269Theme.Claro => (H("#6f97bc"),H("#5a7da1"),H("#7aa7d0")),
                    _ => (H("#246aae"),H("#1b4f91"),H("#6ea9d1")) };
                using var fill = new LinearGradientBrush(area, first, last, 0f);
                using var line = new Pen(border);
                g.FillPath(fill,shape); g.DrawPath(line,shape);
                using var rail = new SolidBrush(theme == V269Theme.Dark ? H("#c8d0d7") : Color.White);
                using var railShape = Rounded(new Rectangle(1,9,4,Math.Max(1,Height-18)),2);
                g.FillPath(rail,railShape);
            }
            else
            {
                using var border = new Pen(Color.FromArgb(22, theme == V269Theme.Claro ? Color.Black : Color.White));
                g.DrawPath(border,shape);
            }
            int iconSize = Math.Min(36, Math.Max(18, Height - 10));
            var icon = new Rectangle(9, (Height-iconSize)/2, iconSize, iconSize);
            using var iconShape = Rounded(icon, 11);
            using var tint = new SolidBrush(Color.FromArgb(IsSelected ? 42 : 15,
                theme == V269Theme.Claro && !IsSelected ? Color.Black : Color.White));
            using var iconBorder = new Pen(Color.FromArgb(IsSelected ? 85 : 65,
                theme == V269Theme.Claro ? H("#789bb7") : H("#a4c7e5")));
            g.FillPath(tint,iconShape); g.DrawPath(iconBorder,iconShape);
            var glyph = new Rectangle(icon.X+(icon.Width-18)/2,icon.Y+(icon.Height-18)/2,18,18);
            V269HomeHeadingIcon.DrawGlyph(g, glyph, Page, HomeIconColor(theme, Page, IsSelected));
            using var textFont = new Font(Font, IsSelected ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, Text, textFont,
                new Rectangle(icon.Right+8, 1, Math.Max(1,Width-icon.Right-12), Math.Max(1,Height-2)),
                theme == V269Theme.Dark ? H(IsSelected ? "#e3e6e9" : "#c6cdd3") : ForeColor,
                TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(area,-3,-3));
        }
    }

    internal static GraphicsPath Rounded(Rectangle r,int radius)
    {
        int d = Math.Max(1,Math.Min(radius*2,Math.Min(r.Width,r.Height)));
        var p = new GraphicsPath();
        p.AddArc(r.Left,r.Top,d,d,180,90);
        p.AddArc(r.Right-d,r.Top,d,d,270,90);
        p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);
        p.AddArc(r.Left,r.Bottom-d,d,d,90,90);
        p.CloseFigure(); return p;
    }
}

internal sealed class V269HomeHeadingIcon : Control
{
    internal V269HomeHeadingIcon()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        int n = Math.Min(34,Math.Min(Width,Height));
        if (n > 1) DrawGlyph(e.Graphics,new Rectangle(3,(Height-n)/2,n,n),V269Page.Inicio,ForeColor);
    }
    // Traçados 24 × 24 da família de ícones aprovada, escala uniforme.
    internal static void DrawGlyph(Graphics g, Rectangle r, V269Page page, Color color)
    {
        var state = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X,r.Y);
            g.ScaleTransform(r.Width/24f,r.Height/24f);
            using var p = new Pen(color,2.05f) { StartCap=LineCap.Round, EndCap=LineCap.Round, LineJoin=LineJoin.Round };
            void L(params PointF[] pts) => g.DrawLines(p,pts);
            PointF P(float x,float y) => new(x,y);
            switch (page)
            {
                case V269Page.Inicio:
                    L(P(3,10),P(12,3),P(21,10),P(21,21),P(14,21),P(14,14),P(10,14),P(10,21),P(3,21),P(3,10)); break;
                case V269Page.Aplicativos:
                    for(int y=3;y<=14;y+=11) for(int x=3;x<=14;x+=11)
                    { using var path=V269Shell.Rounded(new Rectangle(x,y,7,7),2); g.DrawPath(p,path); } break;
                case V269Page.Drivers:
                    g.DrawRectangle(p,6,6,12,12); g.DrawRectangle(p,9,9,6,6);
                    foreach(int t in new[]{8,12,16}) { L(P(t,2),P(t,6)); L(P(t,18),P(t,22)); L(P(2,t),P(6,t)); L(P(18,t),P(22,t)); } break;
                case V269Page.Limpeza:
                    L(P(12,2),P(12,12)); L(P(8,12),P(16,12),P(20,21),P(4,21),P(8,12));
                    L(P(8,17),P(8,21)); L(P(12,17),P(12,21)); L(P(16,17),P(16,21)); break;
                case V269Page.Reparo:
                    L(P(15,3),P(20,3),P(17,6),P(18,9),P(21,10),P(21,14),P(16,14),P(8,22),P(4,18),P(12,10),P(12,6),P(15,3)); break;
                case V269Page.Sistema:
                    using(var path=V269Shell.Rounded(new Rectangle(3,3,18,13),2)) g.DrawPath(p,path);
                    L(P(12,16),P(12,21)); L(P(7,21),P(17,21)); break;
                case V269Page.Inicializacao:
                    g.DrawArc(p,3,3,18,18,-45,270); L(P(12,2),P(12,12)); break;
                default:
                    var points = new List<PointF>();
                    for(int i=0;i<32;i++) { double angle=i*Math.PI/16; float radius=(i%4 is 0 or 3)?10:8; points.Add(P(12+(float)Math.Cos(angle)*radius,12+(float)Math.Sin(angle)*radius)); }
                    g.DrawPolygon(p,points.ToArray()); g.DrawEllipse(p,9,9,6,6); break;
            }
        }
        finally { g.Restore(state); }
    }
}
