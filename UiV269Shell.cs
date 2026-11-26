using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Medidas finais da referência HTML: largura da janela e media query
// da altura disponível no monitor. Aplicadas exclusivamente a Início.
internal static class V269HomeMetrics
{
    internal static int LogicalScreenHeight(Control control) =>
        Math.Max(1, (int)Math.Round(Screen.FromControl(control).WorkingArea.Height * 96d / control.DeviceDpi));
    internal static int HeaderHeight(int width, int screenHeight) =>
        screenHeight <= 810 ? 69 : width <= 850 ? 70 : 80;
    internal static int LogoSize(int width, int screenHeight) =>
        screenHeight <= 810 || width <= 850 ? 45 : 55;
    internal static int TitlePixels(int width, int screenHeight) =>
        screenHeight <= 810 ? 22 : width <= 850 ? 20 : width <= 1250 ? 24 : 28;
}

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
    History,
    DriverReview
}

/// <summary>
/// Estado exclusivamente visual do painel. O serviço de domínio conserva
/// autorização, revalidação, cancelamento e evidências de execução.
/// </summary>
internal sealed record V269FlowResult(V269Page Page, bool Complete, Action Dismiss, Guid? HistoryId = null);

internal sealed record V269PanelPresentation(
    V269PanelKind Kind, string Title, string Description, int? ProgressPercent = null,
    V269AppDetailModel? Application = null, V269AppDetailModel? Driver = null,
    V269FlowResult? FlowResult = null, Form? DriverReview = null, Action? CancelFlow = null, V269AppDetailModel? Cleanup = null, V269Page? FlowPage = null, string? CancelLabel = null, V269AppDetailModel? Repair = null, V269AppDetailModel? Module = null);

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
    private readonly Panel _flowViewport=new(){Name="V269AppFlowViewport",Dock=DockStyle.Fill,Margin=Padding.Empty,TabStop=false};
    private readonly DarkVScrollBar _cleanupFlowScroll=new(){Name="V269CleanupFlowScroll",Dock=DockStyle.Right,Visible=false,AccessibleName="Rolagem do acompanhamento da operação"};
    private readonly WheelDeltaAccumulator _cleanupFlowWheel=new();
    private bool _cleanupFlowScrollActive;
    private readonly Label _title;
    private readonly Panel _titleHost=new(){Dock=DockStyle.Fill,Margin=Padding.Empty};
    private readonly Label _appFlowHeading=new(){Name="V269AppFlowHeading",Dock=DockStyle.Top,
        TextAlign=ContentAlignment.TopLeft,Visible=false,Margin=Padding.Empty};
    private readonly Panel _flowStateGap = new() { Dock = DockStyle.Top, Visible = false, Margin = Padding.Empty };
    private readonly Panel _flowTitleGap = new() { Dock = DockStyle.Top, Visible = false, Margin = Padding.Empty };
    private readonly V269AppFlowState _appFlowState=new();
    private readonly TextBox _description;
    private readonly GfDynamicProgressBar _progress;
    private readonly V269AppFlowMeter _flowMeter = new();
    private readonly V269AppFlowNote _flowNote = new();
    private readonly V269AppFlowButton _cancelFlow = new(false)
    {
        Name = "V269CancelOwnedFlow", Text = "Cancelar espera", AutoSize = true, Visible = false
    };
    private readonly Panel _driverReviewHost = new()
    {
        Name = "V269DriverReviewHost", Dock = DockStyle.Fill, Visible = false
    };
    private Form? _driverReview;
    private readonly V269AppFlowButton _dismissFlowResult = new(false, true)
    {
        Name = "V269DismissFlowResult", Text = "Dispensar resultado", AutoSize = true, Visible = false
    };
    private readonly Panel _operationContent;
    private readonly V269AppDetailPanel _applicationDetails=new();
    private readonly Panel _descriptionHost=new(){Dock=DockStyle.Fill,Margin=Padding.Empty};
    private readonly V269TextScrollHost _descriptionScroll;
    private bool _hasOperationContent;
    private bool _structuredApp;
    private bool _appHistoryPage;
    private bool _driversPage;
    private bool _cleanupPage;
    private bool _repairPage;
    private V269Page? _modulePage;
    private Func<V269Page,IReadOnlyList<V269AppPreviousResult>>? _moduleHistory;
    private Action<Guid>? _moduleDismiss;
    private Func<IReadOnlyList<V269AppPreviousResult>>? _repairHistoryProvider;
    private Action<Guid>? _repairHistoryDismiss;
    private Func<IReadOnlyList<V269AppPreviousResult>>? _cleanupHistoryProvider;
    private Action<Guid>? _cleanupHistoryDismiss;
    private bool _appFlow;
    private bool _fittingOperationHistory;
    private int _presentationDepth;
    private Func<IReadOnlyList<V269AppPreviousResult>>? _appHistoryProvider;
    private Action<Guid>? _appHistoryDismiss;
    private Func<IReadOnlyList<V269AppPreviousResult>>? _driverHistoryProvider;
    private Action<Guid>? _driverHistoryDismiss;
    private V269AppOperationHistory? _operationHistory;
    private V269AppPreviousResult[] _operationHistorySnapshot=[];
    private V269Palette _basePanePalette=V269VisualTokens.ForTheme(V269Theme.AzulGf);
    private V269Theme _appPaneTheme=V269Theme.AzulGf;
    private int _appInterfacePercent=100;
    private Font? _normalTitleFont,_appTitleFont;
    private readonly FlowLayoutPanel _choices;
    private TaskCompletionSource<bool>? _confirmation;
    private TableLayoutPanel? _readOnlyView;
    private Control? _readOnlyOrigin;
    private bool _readOnlyPreviousScrollVisible;
    internal bool ReadOnlyViewActive => _readOnlyView is not null;

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
            RowCount = 7,
            Margin = Padding.Empty,
            BackColor = SystemColors.Control
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));

        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        _appFlowHeading.Paint+=PaintAppFlowHeading;
        _title = new Label
        {
            Name = "V269PanelTitle", AccessibleName = "Título do painel", Dock = DockStyle.Fill,
            AutoEllipsis = true, Text = "Detalhes",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = Padding.Empty
        };
        _progress = new GfDynamicProgressBar
        {
            Name = "V269PanelProgress", AccessibleName = "Progresso da operação", Dock = DockStyle.Fill,
            Visible = false, Margin = new Padding(0, 3, 0, 10)
        };
        _description = new TextBox
        {
            Name = "V269PanelDescription", Dock = DockStyle.Fill,
            Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.None,
            Margin = Padding.Empty,
            AccessibleName = "Descrição e estado da operação"
        };
        _title.Paint+=(_,e)=>{
            if(!_structuredApp||_title.Width<2)return;
            using var line=new Pen(V269AppDetailStyle.Divider(_appPaneTheme));
            e.Graphics.DrawLine(line,0,_title.Height-1,_title.Width-1,_title.Height-1);
        };
        _titleHost.Controls.Add(_title);
        _titleHost.Controls.Add(_flowTitleGap);
        _titleHost.Controls.Add(_appFlowState);
        _titleHost.Controls.Add(_flowStateGap);
        _titleHost.Controls.Add(_appFlowHeading);
        _appFlowHeading.SendToBack();
        _layout.Controls.Add(_titleHost, 0, 0);
        _layout.Controls.Add(_progress, 0, 1);
        _descriptionScroll = new V269TextScrollHost(_description);
        _descriptionHost.Controls.Add(_descriptionScroll);
        _descriptionHost.Controls.Add(_flowMeter);
        _descriptionHost.Controls.Add(_flowNote);
        _descriptionHost.Controls.Add(_applicationDetails);
        _descriptionHost.Controls.Add(_driverReviewHost);
        _descriptionHost.SizeChanged+=(_,_)=>FitOperationHistory();
        _layout.Controls.Add(_descriptionHost,0,2);
        _choices = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, WrapContents = false, Visible = false };
        foreach (var (caption, accepted) in new[] { ("Cancelar", false), ("Confirmar", true) })
        {
            var button = new V269AppFlowButton(accepted)
            {
                Name = accepted ? "V269AcceptConfirmation" : "V269CancelConfirmation",
                Text = caption, AutoSize = true, MinimumSize = new Size(70, 26)
            };
            button.Click += (_, _) => ResolveConfirmation(accepted);
            _choices.Controls.Add(button);
        }
        _dismissFlowResult.Click += (_, _) => Current?.FlowResult?.Dismiss();
        _choices.Controls.Add(_dismissFlowResult);
        _cancelFlow.Click += (_, _) => Current?.CancelFlow?.Invoke();
        _choices.Controls.Add(_cancelFlow);
        _operationContent = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        _layout.Controls.Add(_choices, 0, 3);
        _layout.Controls.Add(_operationContent, 0, 4);
        _layout.Controls.Add(new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,TabStop=false},0,6);
        _flowViewport.Controls.Add(_layout);Controls.Add(_flowViewport);
        Controls.Add(_cleanupFlowScroll);_cleanupFlowScroll.BringToFront();
        _cleanupFlowScroll.ValueChanged+=(_,_)=>{if(_cleanupFlowScrollActive)_layout.Top=-_cleanupFlowScroll.Value;};
        void TrackCleanupFlow(Control control)
        {
            control.MouseWheel+=(_,e)=>
            {
                if(!_cleanupFlowScrollActive||e is HandledMouseEventArgs {Handled:true})return;
                int notches=_cleanupFlowWheel.Consume(e.Delta);
                if(notches!=0)_cleanupFlowScroll.Value=(int)Math.Clamp(
                    (long)_cleanupFlowScroll.Value-85L*notches,0,_cleanupFlowScroll.MaximumValue);
                if(e is HandledMouseEventArgs handled)handled.Handled=true;
            };
            control.Enter+=(_,_)=>
            {
                if(!_cleanupFlowScrollActive||!control.Visible||!control.IsHandleCreated||
                    control is not (Button or TextBox))return;
                var bounds=_layout.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                if(bounds.Top<_cleanupFlowScroll.Value)_cleanupFlowScroll.Value=bounds.Top;
                else if(bounds.Bottom>_cleanupFlowScroll.Value+_flowViewport.ClientSize.Height)
                    _cleanupFlowScroll.Value=bounds.Bottom-_flowViewport.ClientSize.Height;
            };
            control.ControlAdded+=(_,e)=>{if(e.Control is {} child)TrackCleanupFlow(child);};
            foreach(Control child in control.Controls)TrackCleanupFlow(child);
        }
        TrackCleanupFlow(_flowViewport);
        _flowViewport.SizeChanged+=(_,_)=>FitOperationHistory();
        Resize+=(_,_)=>{if(_structuredApp||_appFlow)Invalidate();FitOperationHistory();};
        Present(null);
    }

    // Consulta transitória no próprio painel. Current nunca é substituído:
    // não guardar/restaurar callbacks, resultados ou confirmações antigas.
    private bool CanShowReadOnly => !IsDisposed && !Disposing && _confirmation is null &&
        Current?.Kind is (V269PanelKind.Result or V269PanelKind.History);

    internal bool TryShowReadOnly(string title, string description, string editorName, Control origin)
    {
        if (!CanShowReadOnly) return false;
        var editor = new TextBox { Name = editorName, AccessibleName = title, Text = description,
            Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.None,
            BorderStyle = BorderStyle.None, Dock = DockStyle.Fill };
        ShowReadOnlyContent(title, new V269TextScrollHost(editor), origin, null);
        return true;
    }

    internal bool TryShowReliabilityReport(string report, Control origin, Action save)
    {
        if (!CanShowReadOnly) return false;
        var body = new DarkLogView { Name = "LocalReliabilityReportText", Dock = DockStyle.Fill,
            AccessibleName = "Histórico e relatório local somente leitura" };
        body.SetText(report);
        ShowReadOnlyContent("Histórico local", body, origin, save);
        return true;
    }

    private void ShowReadOnlyContent(string title, Control body, Control origin, Action? save)
    {
        CloseReadOnly(false);
        var view = new TableLayoutPanel
        {
            Name = "V269ReadOnlyView", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Margin = Padding.Empty, TabStop = false, AccessibleName = title
        };
        view.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        view.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        view.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        view.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var heading = new Label { Name = "V269ReadOnlyTitle", Text = title, Dock = DockStyle.Fill,
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        var close = new Button { Name = "V269ReadOnlyClose", Text = "Voltar", Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat, AccessibleName = "Voltar ao resultado ou histórico anterior" };
        close.Click += (_, _) => CloseReadOnly(true);
        view.Controls.Add(heading, 0, 0);
        view.Controls.Add(body, 0, 1);
        if (save is null) view.Controls.Add(close, 0, 2);
        else
        {
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                Margin = Padding.Empty, TabStop = false };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var export = new Button { Name = "SaveLocalReliabilityReport", Text = "Salvar TXT",
                AccessibleName = "Salvar relatório local em arquivo TXT", FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill };
            export.Click += (_, _) => save();
            footer.Controls.Add(export, 0, 0);
            footer.Controls.Add(close, 1, 0);
            view.Controls.Add(footer, 0, 2);
        }
        _readOnlyOrigin = origin;
        _readOnlyPreviousScrollVisible = _cleanupFlowScroll.Visible;
        _readOnlyView = view;
        SuspendLayout();
        try
        {
            _flowViewport.Visible = false;
            _cleanupFlowScroll.Visible = false;
            Controls.Add(view);
            view.BringToFront();
            ApplyReadOnlyAppearance();
        }
        finally { ResumeLayout(true); }
    }

    private static IEnumerable<Control> ReadOnlyDescendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in ReadOnlyDescendants(child)) yield return descendant;
        }
    }

    internal void FocusReadOnly()
    {
        if (_readOnlyView?.GetControlFromPosition(0, 1) is Control body)
            ReadOnlyDescendants(body).OfType<TextBox>().Single().Focus();
    }

    internal bool CloseReadOnly(bool restoreFocus)
    {
        if (_readOnlyView is not { } view) return false;
        var origin = _readOnlyOrigin;
        _readOnlyView = null;
        _readOnlyOrigin = null;
        SuspendLayout();
        try
        {
            Controls.Remove(view);
            view.Dispose();
            _flowViewport.Visible = true;
            _cleanupFlowScroll.Visible = _readOnlyPreviousScrollVisible;
            _cleanupFlowScroll.BringToFront();
        }
        finally { ResumeLayout(true); }
        FitOperationHistory();
        if (restoreFocus && origin is { IsDisposed: false, CanSelect: true }) origin.Focus();
        return true;
    }

    private void ApplyReadOnlyAppearance()
    {
        if (_readOnlyView is not { } view) return;
        var palette = _basePanePalette;
        view.BackColor = palette.RightPanel;
        var heading = (Label)view.GetControlFromPosition(0, 0)!;
        var body = view.GetControlFromPosition(0, 1)!;
        var editor = ReadOnlyDescendants(body).OfType<TextBox>().Single();
        var buttons = ReadOnlyDescendants(view).OfType<Button>().ToArray();
        heading.Font = _normalTitleFont ?? _title.Font;
        heading.ForeColor = palette.Text;
        body.BackColor = editor.BackColor = palette.RightPanel;
        editor.ForeColor = palette.Text;
        editor.Font = _description.Font;
        foreach (var button in buttons)
        {
            button.Font = _description.Font;
            button.BackColor = palette.Card; button.ForeColor = palette.Text;
            button.FlatAppearance.BorderColor = palette.Border;
        }
        view.RowStyles[0].Height = Math.Max(heading.Font.Height * 2 + 8,
            V269AppTable.Scale(50, _appInterfacePercent, DeviceDpi));
        view.RowStyles[2].Height = Math.Max(_description.Font.Height + 12,
            V269AppTable.Scale(38, _appInterfacePercent, DeviceDpi));
        if (body is V269TextScrollHost host)
        {
            host.Configure(_appPaneTheme);
            host.Reflow();
        }
        else if (body is DarkLogView log)
        {
            foreach (var panel in ReadOnlyDescendants(log).OfType<Panel>())
                panel.BackColor = palette.RightPanel;
            log.ApplyScrollTheme(_appPaneTheme);
            log.UseReadableFont(_description.Font);
        }
    }

    internal void SetOperationContent(Control content)
    {
        _operationContent.Controls.Add(content);
        _hasOperationContent=true;
        UpdateOperationContentLayout();FitOperationHistory();
    }

    internal async Task<bool> ConfirmAsync(string title, string description, CancellationToken token, V269Page? flowPage = null)
    {
        if (_confirmation is not null || IsDisposed || token.IsCancellationRequested) return false;
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previous = Current;
        _confirmation = pending;
        try
        {
            _layout.RowStyles[3].Height = 38;
            _choices.Visible = true;
            PresentCore(new(V269PanelKind.Confirmation, title, description, FlowPage: flowPage));
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
                // Se o usuário mudou de aba enquanto autorizava, não
                // restaurar os detalhes ou resultado contextual de outra aba.
                if (previous is not null &&
                    (previous.Application is not null && !_appHistoryPage ||
                     previous.Driver is not null && !_driversPage ||
                     previous.Cleanup is not null && !_cleanupPage ||
                     previous.Repair is not null && !_repairPage ||
                     previous.Module is not null && previous.FlowPage != _modulePage ||
                     previous.FlowResult is { } result && !IsFlowPageSelected(result.Page)))
                    previous = null;
                PresentCore(previous);
            }
        }
    }

    private bool IsFlowPageSelected(V269Page page) => page switch
    {
        V269Page.Aplicativos => _appHistoryPage,
        V269Page.Drivers => _driversPage,
        V269Page.Limpeza => _cleanupPage,
        V269Page.Reparo => _repairPage,
        V269Page.Sistema or V269Page.Inicializacao => _modulePage==page,
        _ => true
    };

    internal void ResolveConfirmation(bool accepted) => _confirmation?.TrySetResult(accepted);

    internal void Present(V269PanelPresentation? presentation)
    {
        if (_confirmation is null) PresentCore(presentation);
    }

    internal void ConfigureAppHistory(Func<IReadOnlyList<V269AppPreviousResult>> provider,Action<Guid> dismiss)
    {
        _appHistoryProvider=provider;_appHistoryDismiss=dismiss;RefreshAppOperationHistory();
    }
    internal void ConfigureDriverHistory(Func<IReadOnlyList<V269AppPreviousResult>> provider, Action<Guid> dismiss)
    {
        _driverHistoryProvider = provider;
        _driverHistoryDismiss = dismiss;
        RefreshAppOperationHistory();
    }
    internal void ConfigureCleanupHistory(Func<IReadOnlyList<V269AppPreviousResult>> provider, Action<Guid> dismiss)
    {
        _cleanupHistoryProvider=provider;_cleanupHistoryDismiss=dismiss;RefreshAppOperationHistory();
    }
    internal void ConfigureRepairHistory(Func<IReadOnlyList<V269AppPreviousResult>> provider, Action<Guid> dismiss)
    {
        _repairHistoryProvider = provider;
        _repairHistoryDismiss = dismiss;
        RefreshAppOperationHistory();
    }
    internal void SetAppHistoryPage(bool active, bool drivers = false, bool cleanup = false, bool repair = false, V269Page? module = null)
    {
        _appHistoryPage=active;
        _driversPage=drivers;
        _cleanupPage=cleanup;
        _repairPage=repair;
        _modulePage=module;
        UpdateAppFlowScope();RefreshAppOperationHistory();FitOperationHistory();
    }
    internal void ConfigureModuleHistory(Func<V269Page,IReadOnlyList<V269AppPreviousResult>> provider, Action<Guid> dismiss)
    { _moduleHistory=provider;_moduleDismiss=dismiss; }
    private bool CleanupConfirmationChrome => _cleanupPage &&
        Current is { Kind: V269PanelKind.Confirmation, FlowPage: null or V269Page.Limpeza };

    private void UpdateAppFlowScope()
    {
        bool result = Current is { Kind: V269PanelKind.Result, FlowResult: { } flow } &&
            flow.Page is (V269Page.Aplicativos or V269Page.Drivers or V269Page.Limpeza or V269Page.Reparo or V269Page.Sistema or V269Page.Inicializacao) &&
            IsFlowPageSelected(flow.Page);
        bool active = result || Current is { Kind: V269PanelKind.Progress, CancelFlow: not null } ||
            Current is { Kind: V269PanelKind.Progress or V269PanelKind.Confirmation, FlowPage: V269Page.Reparo } ||
            (_appHistoryPage || _driversPage || _cleanupPage || _repairPage || _modulePage is not null) &&
                Current?.Kind is (V269PanelKind.Confirmation or V269PanelKind.Progress);
        bool changed=active!=_appFlow;_appFlow=active;
        _appFlowHeading.Text = Current?.Kind switch
        {
            V269PanelKind.Confirmation => "Confirmação da ação",
            V269PanelKind.Result => Current.FlowResult?.Complete == true ? "Operação concluída" : "Resultado da operação",
            _ => "Operação em andamento"
        };
        _appFlowHeading.Visible=active;_appFlowState.Visible=active;
        _flowStateGap.Visible=active;_flowTitleGap.Visible=active;
        bool pending = _confirmation is not null && Current?.Kind == V269PanelKind.Confirmation;
        bool operationCancel = active && Current is { Kind: V269PanelKind.Progress, CancelFlow: not null };
        _cancelFlow.Text = Current?.CancelLabel ?? "Cancelar espera";
        foreach (Control choice in _choices.Controls)
            choice.Visible = ReferenceEquals(choice, _dismissFlowResult) ? result :
                ReferenceEquals(choice, _cancelFlow) ? operationCancel : pending;
        _choices.Visible = pending || result || operationCancel;
        int choiceHeight = V269AppTable.Scale(active ? 44 : 38, _appInterfacePercent, DeviceDpi);
        _layout.RowStyles[3].Height = pending || result || operationCancel ? choiceHeight : 0;
        _flowNote.SetContent(active, Current, _appPaneTheme, _appInterfacePercent, _applicationDetails.Font, CleanupConfirmationChrome);
        _flowMeter.SetProgress(active && Current?.Kind == V269PanelKind.Progress, Current?.ProgressPercent,
            _appPaneTheme, _appInterfacePercent, _applicationDetails.Font);
        bool driverReview = _driversPage && Current is { Kind: V269PanelKind.DriverReview };
        _driverReviewHost.Visible = driverReview;
        if (driverReview) _driverReviewHost.BringToFront();
        _descriptionScroll.Visible = !_structuredApp && !result && !driverReview;
        bool legacyProgress = !active && Current is { Kind: V269PanelKind.Progress };
        _progress.Visible = legacyProgress;
        UpdateLegacyMeter();
        _layout.RowStyles[1].Height = legacyProgress ? 34 : 0;
        // A operação pode já fornecer o mesmo título do estado.
        _title.Visible=!active||!string.Equals(Current?.Title,_appFlowHeading.Text,StringComparison.Ordinal);
        if(changed)ApplyPaneSurface();
        else if(active)ApplyAppTitleMetrics();
        UpdateOperationContentLayout();
    }
    internal void RefreshAppOperationHistory()
    {
        if(IsDisposed||Disposing)return;
        var flowPage = Current?.FlowPage ?? (Current?.CancelFlow is not null ? V269Page.Drivers : (V269Page?)null);
        var provider = _appHistoryPage && flowPage is (null or V269Page.Aplicativos) ? _appHistoryProvider :
            _driversPage && flowPage is (null or V269Page.Drivers) ? _driverHistoryProvider :
            _cleanupPage && flowPage is (null or V269Page.Limpeza) ? _cleanupHistoryProvider :
            _repairPage && flowPage is (null or V269Page.Reparo) ? _repairHistoryProvider : null;
        var dismiss = _appHistoryPage ? _appHistoryDismiss : _driversPage ? _driverHistoryDismiss :
            _cleanupPage ? _cleanupHistoryDismiss : _repairHistoryDismiss;
        if(_modulePage is {} module && (flowPage is null || flowPage==module))
        { provider=_moduleHistory is null?null:()=>_moduleHistory(module);dismiss=_moduleDismiss; }
        var currentId = Current?.FlowResult?.HistoryId;
        V269AppPreviousResult[] results = _appFlow
            ? provider?.Invoke().Where(entry => entry.Id != currentId).ToArray() ?? [] : [];
        if(results.SequenceEqual(_operationHistorySnapshot))return;
        _operationHistorySnapshot=results;
        _operationHistory?.Dispose();_operationHistory=null;
        _layout.RowStyles[5].Height=0;
        if(results.Length==0||dismiss is null){FitOperationHistory();return;}
        _operationHistory=new V269AppOperationHistory(results,dismiss);
        // Assim como no HTML, resultados anteriores vêm depois dos
        // controles de autorização/progresso. O registro real continua acessível.
        _layout.Controls.Add(_operationHistory,0,5);
        _operationHistory.ApplyAppearance(_appPaneTheme,_appInterfacePercent,_applicationDetails.Font);
        FitOperationHistory();
    }
    private void FitOperationHistory()
    {
        if(_readOnlyView is not null || _presentationDepth != 0 || _fittingOperationHistory||IsDisposed||Disposing)return;
        _fittingOperationHistory=true;
        try
        {
            // Aplicativos usa a mesma política de overflow do aside HTML:
            // medir o conjunto inteiro, sem subdividir em áreas de rolagem.
            if(!_appFlow&&_cleanupFlowScrollActive)
            {
                _cleanupFlowScrollActive=false;
                _cleanupFlowScroll.SetMetrics(0,1);
                _cleanupFlowScroll.Value=0;
                _layout.Location=Point.Empty;
            }
            if(!_appFlow)
            {
                if(_flowViewport.AutoScroll)
                {
                    _flowViewport.AutoScrollPosition=Point.Empty;
                    _flowViewport.AutoScrollMinSize=Size.Empty;
                    _flowViewport.AutoScroll=false;
                }
                _layout.Dock=DockStyle.Fill;
                _descriptionScroll.Configure(_appPaneTheme, true);
                _descriptionScroll.Reflow();
                return;
            }
            int S(int n)=>V269AppTable.Scale(n,_appInterfacePercent,DeviceDpi);
            _descriptionScroll.Configure(_appPaneTheme, false);
            int height=Math.Max(1,_flowViewport.ClientSize.Height);
            bool executing=AppProgressChromeActive&&_hasOperationContent;
            int operation=executing?Math.Max(S(52),_description.Font.Height+S(32)):0;
            int MeasureFlow(int width)
            {
                int description = 0;
                if (Current?.Kind != V269PanelKind.Result)
                {
                    int measured=TextRenderer.MeasureText(_description.Text,_description.Font,
                        new Size(Math.Max(1,width-S(10)),32767),
                        TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+S(12);
                    description=Math.Max(Math.Max(_description.Font.Height+S(8),S(26)),measured);
                }
                _flowNote.Height = _flowNote.MeasureHeight(width);
                description += _flowNote.Height + _flowMeter.Height;
                int history=_operationHistory is {} previous?previous.PreferredHeightForWidth(width)+S(3):0;
                _layout.RowStyles[2].Height=description;
                _layout.RowStyles[4].Height=operation;
                _layout.RowStyles[5].Height=history;
                return (int)Math.Ceiling(_layout.RowStyles[0].Height+_layout.RowStyles[1].Height+
                    description+_layout.RowStyles[3].Height+operation+history);
            }
            _flowViewport.SuspendLayout();_layout.SuspendLayout();
            try
            {
                if (_flowViewport.AutoScroll)
                {
                    _flowViewport.AutoScrollPosition = Point.Empty;
                    _flowViewport.AutoScrollMinSize = Size.Empty;
                    _flowViewport.AutoScroll = false;
                }
                _cleanupFlowScrollActive = true;
                _cleanupFlowScroll.ApplyAppScrollTheme(_appPaneTheme);
                int width = Math.Max(1, ClientSize.Width - Padding.Horizontal);
                int content = MeasureFlow(width);
                if (content > height)
                {
                    width = Math.Max(1, width - _cleanupFlowScroll.Width);
                    content = MeasureFlow(width);
                }
                _cleanupFlowScroll.SetMetrics(content, height);
                _layout.Dock = DockStyle.None;
                _layout.Bounds = new Rectangle(0, -_cleanupFlowScroll.Value, width, Math.Max(height, content));
            }
            finally{_layout.ResumeLayout(true);_flowViewport.ResumeLayout(true);}
        }
        finally{_fittingOperationHistory=false;}
    }
    private void PresentCore(V269PanelPresentation? presentation)
    {
        // Uma publicação nova ou confirmação tem prioridade sobre uma consulta.
        CloseReadOnly(false);
        // Um estado do painel é uma transação: as mudanças de linhas,
        // visibilidade e conteúdo não precisam rearranjar o viewport
        // separadamente. A medição final usa a geometria já aplicada.
        _presentationDepth++;
        _flowViewport.SuspendLayout();
        _layout.SuspendLayout();
        try { PresentCoreLayout(presentation); }
        finally
        {
            try
            {
                _flowViewport.ResumeLayout(true);
                _layout.ResumeLayout(true);
            }
            finally { _presentationDepth--; }
        }
        FitOperationHistory();
        PresentationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PresentCoreLayout(V269PanelPresentation? presentation)
    {
        // A confirmação restaura a fotografia anterior. Se um resultado
        // foi dispensado durante a espera, não ressuscitar essa fotografia.
        if(_appHistoryPage&&presentation?.Application is {} app&&_appHistoryProvider is not null)
        {
            var latest=_appHistoryProvider();
            if(!(app.PreviousResults??Array.Empty<V269AppPreviousResult>()).SequenceEqual(latest))
                presentation=presentation with {Application=app with {PreviousResults=latest}};
        }
        if (_driversPage && presentation?.Driver is { } driver && _driverHistoryProvider is not null)
        {
            var latest = _driverHistoryProvider();
            if (!(driver.PreviousResults ?? []).SequenceEqual(latest))
                presentation = presentation with { Driver = driver with { PreviousResults = latest } };
        }
        if (_cleanupPage && presentation?.Cleanup is { } cleanup && _cleanupHistoryProvider is not null)
        {
            var latest = _cleanupHistoryProvider();
            if (!(cleanup.PreviousResults ?? []).SequenceEqual(latest))
                presentation = presentation with { Cleanup = cleanup with { PreviousResults = latest } };
        }
        if (_repairPage && presentation?.Repair is { } repair && _repairHistoryProvider is not null)
        {
            var latest = _repairHistoryProvider();
            if (!(repair.PreviousResults ?? []).SequenceEqual(latest))
                presentation = presentation with { Repair = repair with { PreviousResults = latest } };
        }
        if (Current?.Kind != presentation?.Kind || Current?.Title != presentation?.Title ||
            Current?.FlowPage != presentation?.FlowPage)
            _cleanupFlowScroll.Value = 0;
        Current = presentation;
        if (presentation?.DriverReview is { IsDisposed: false } driverReview &&
            !ReferenceEquals(driverReview, _driverReview))
        {
            _driverReview = driverReview;
            _driverReviewHost.Controls.Add(driverReview);
            driverReview.Disposed += (_, _) =>
            {
                if (ReferenceEquals(_driverReview, driverReview)) _driverReview = null;
            };
            driverReview.Show();
        }
        _title.Text = presentation?.Title ?? "Detalhes";
        _description.Text = presentation?.Description ?? string.Empty;
        var detail = presentation?.Kind == V269PanelKind.Details ? presentation.Application ?? presentation.Driver ?? presentation.Cleanup ?? presentation.Repair ?? presentation.Module : null;
        bool structured = detail is not null;
        _structuredApp=structured;UpdateAppFlowScope();ApplyPaneSurface();
        _applicationDetails.Present(detail);
        _applicationDetails.Visible=structured;
        if(structured)_applicationDetails.BringToFront();
        UpdateOperationContentLayout();
        bool inProgress = presentation is { Kind: V269PanelKind.Progress };
        _progress.Visible = inProgress && !_appFlow;
        _layout.RowStyles[1].Height = inProgress && !_appFlow ? 34 : 0;
        UpdateLegacyMeter();
        AccessibleDescription = presentation?.Title ?? "";
        RefreshAppOperationHistory();
    }

    private void UpdateLegacyMeter()
    {
        var value=Current?.ProgressPercent;
        var frame=GfUniversalMeter.Operation(value,Current?.Kind==V269PanelKind.Progress ?
            GfMeterState.Running : GfMeterState.Unavailable,true,_appPaneTheme);
        _progress.BackColor=BackColor;
        _progress.Configure(_appPaneTheme, FindForm() is MainForm main && main.V269CanAnimateButtons);
        _progress.Present(frame,frame.Fraction is double fraction ? $"{fraction*100:0}%" : "Progresso não informado");
    }

    private void UpdateOperationContentLayout()
    {
        // A autorização de Aplicativos não exibe o status/log de uma execução anterior.
        // Os controles reais permanecem intactos e voltam durante o progresso.
        bool show=!_driversPage&&!_repairPage&&!_structuredApp&&_modulePage is null&&Current?.CancelFlow is null &&
            Current?.FlowPage != V269Page.Reparo &&
            Current?.Kind != V269PanelKind.DriverReview && (!_appFlow||Current?.Kind==V269PanelKind.Progress);
        _operationContent.Visible=show;
        bool reserve=show&&_hasOperationContent;
        _layout.RowStyles[2].SizeType=_appFlow?SizeType.Absolute:SizeType.Percent;
        _layout.RowStyles[4].SizeType=reserve&&!_appFlow?SizeType.Percent:SizeType.Absolute;
        _layout.RowStyles[6].SizeType=_appFlow?SizeType.Percent:SizeType.Absolute;
        _layout.RowStyles[6].Height=_appFlow?100:0;
        if(!_appFlow)
        {
            _layout.RowStyles[4].Height=reserve?55:0;
            _layout.RowStyles[2].Height=reserve?45:100;
        }
        else FitOperationHistory();
    }

    internal bool AppProgressChromeActive=>(_appHistoryPage||_cleanupPage)&&_appFlow&&
        Current is {Kind:V269PanelKind.Progress, CancelFlow:null};

    private void PaintAppFlowHeading(object? sender,PaintEventArgs e)
    {
        if(!_appFlow||_appFlowHeading.Width<2)return;
        int S(int n)=>V269AppTable.Scale(n,_appInterfacePercent,DeviceDpi);
        int n=S(20),x=S(2),y=S(1);
        bool confirmation=Current?.Kind==V269PanelKind.Confirmation;
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        string color=confirmation || Current?.FlowResult?.Complete==true
            ? (_appPaneTheme==V269Theme.Claro?"#087660":"#70c6a3")
            : Current?.FlowResult is { Complete: false }
                ? (_appPaneTheme==V269Theme.Claro?"#966117":"#e4bb79")
                : (_appPaneTheme==V269Theme.Claro?"#14649e":"#54b8e2");
        using var pen=new Pen(V269AppTable.H(color),Math.Max(1f,S(2)))
            {StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        if(confirmation)
        {
            g.DrawPolygon(pen,new Point[]{new(x+n/2,y),new(x+n-2,y+n/5),new(x+n-3,y+3*n/5),
                new(x+n/2,y+n-1),new(x+3,y+3*n/5),new(x+2,y+n/5)});
            g.DrawLines(pen,new Point[]{new(x+n/3,y+n/2),new(x+n/2,y+2*n/3),new(x+3*n/4,y+n/3)});
        }
        else if (Current?.Kind == V269PanelKind.Result)
        {
            if (Current.FlowResult?.Complete == true)
                g.DrawLines(pen, new Point[] { new(x+2,y+n/2), new(x+n/3,y+n-3), new(x+n-2,y+3) });
            else
            {
                g.DrawEllipse(pen, x+1, y+1, n-2, n-2);
                g.DrawLine(pen, x+n/2, y+5, x+n/2, y+n-7);
                g.DrawLine(pen, x+n/2, y+n-4, x+n/2+1, y+n-4);
            }
        }
        else
        {
            g.DrawArc(pen,new Rectangle(x+2,y+2,Math.Max(2,n-4),Math.Max(2,n-4)),30,140);
            g.DrawArc(pen,new Rectangle(x+2,y+2,Math.Max(2,n-4),Math.Max(2,n-4)),210,140);
            g.DrawLines(pen,new Point[]{new(x+2,y+2),new(x+2,y+n/2),new(x+n/2,y+n/2)});
            g.DrawLines(pen,new Point[]{new(x+n-2,y+n-2),new(x+n-2,y+n/2),new(x+n/2,y+n/2)});
        }
        var palette = V269AppDetailStyle.Palette(_appPaneTheme);
        TextRenderer.DrawText(g, "ACOMPANHAMENTO LOCAL", _applicationDetails.Font,
            new Rectangle(S(29), _appFlowHeading.Height-S(23), Math.Max(1,_appFlowHeading.Width-S(29)), S(18)),
            palette.SecondaryText, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        using var line=new Pen(V269AppDetailStyle.Divider(_appPaneTheme));
        g.DrawLine(line,0,_appFlowHeading.Height-S(3),_appFlowHeading.Width-1,_appFlowHeading.Height-S(3));
    }

    internal void ApplyPalette(V269Palette palette)
    {
        _basePanePalette=palette;
        _appPaneTheme=palette==V269VisualTokens.ForTheme(V269Theme.Dark)?V269Theme.Dark:
            palette==V269VisualTokens.ForTheme(V269Theme.Claro)?V269Theme.Claro:V269Theme.AzulGf;
        ApplyPaneSurface();
    }
    private void ApplyPaneSurface()
    {
        bool styled = _structuredApp || _appFlow || Current?.Kind == V269PanelKind.DriverReview;
        var palette=styled?V269AppDetailStyle.Palette(_appPaneTheme):_basePanePalette;
        BackColor = palette.RightPanel;
        _titleHost.BackColor=palette.RightPanel;_appFlowHeading.ForeColor=palette.Text;
        foreach(var button in _choices.Controls.OfType<V269AppFlowButton>())
        {
            button.ApplyAppStyle(_appFlow,_appPaneTheme,_appInterfacePercent);
            button.MinimumSize=_appFlow?button.GetPreferredSize(Size.Empty):new Size(70,26);
        }
        _choices.PerformLayout();
        _descriptionHost.PerformLayout();
        _layout.BackColor = palette.RightPanel;
        _flowViewport.BackColor=palette.RightPanel;
        ForeColor = palette.Text;
        _title.ForeColor = palette.Text;
        _descriptionScroll.BackColor = palette.RightPanel;
        _descriptionScroll.Configure(_appPaneTheme, !_appFlow);
        _description.BackColor = palette.RightPanel;
        _descriptionHost.BackColor=palette.RightPanel;
        _description.ForeColor = palette.SecondaryText;
        _applicationDetails.ApplyPalette(V269AppDetailStyle.Palette(_appPaneTheme),_appPaneTheme);
        if (_driverReview is { IsDisposed: false } review)
        {
            var reviewPalette = V269AppDetailStyle.Palette(_appPaneTheme);
            void ColorReview(Control control)
            {
                control.BackColor = control is TextBoxBase or ListBox or Button ? reviewPalette.Card : reviewPalette.RightPanel;
                control.ForeColor = reviewPalette.Text;
                if (control is V269ReviewScrollHost viewport) viewport.Configure(_appPaneTheme);
                if (control is V269TextScrollHost textViewport) textViewport.Configure(_appPaneTheme);
                if (control is DarkVScrollBar scrollbar) scrollbar.ApplyAppScrollTheme(_appPaneTheme);
                foreach (Control child in control.Controls) ColorReview(child);
            }
            ColorReview(review);
        }
        _operationHistory?.ApplyAppearance(_appPaneTheme,_appInterfacePercent,_applicationDetails.Font);
        Padding=styled?new Padding(V269AppTable.Scale(13,_appInterfacePercent,DeviceDpi),
            V269AppTable.Scale(12,_appInterfacePercent,DeviceDpi),
            V269AppTable.Scale(13,_appInterfacePercent,DeviceDpi),
            V269AppTable.Scale(12,_appInterfacePercent,DeviceDpi)):new Padding(13);
        _title.Font=(styled?_appTitleFont:_normalTitleFont)??_title.Font;
        ApplyAppTitleMetrics();
        UpdateLegacyMeter();
        ApplyReadOnlyAppearance();
        _title.Invalidate();Invalidate();
    }
    private void ApplyAppTitleMetrics()
    {
        // HTML: título de 18px, linha de 33px incluindo divisor,
        // seguida de margem inferior de 12px. Somente detalhes de Aplicativos.
        _titleHost.Padding=Padding.Empty;
        if(_appFlow)
        {
            int F(int n)=>V269AppTable.Scale(n,_appInterfacePercent,DeviceDpi);
            int height=Math.Max(F(49),_appFlowHeading.Font.Height+F(25));
            _appFlowHeading.Padding=new Padding(F(29),0,0,F(24));
            _appFlowHeading.Height=height;_appFlowHeading.Invalidate();
            _appFlowState.Font=_applicationDetails.Font;
            _appFlowState.ApplyState(Current?.Kind, Current?.ProgressPercent, Current?.FlowResult?.Complete,
                _appPaneTheme,_appInterfacePercent);
            _flowNote.SetContent(true, Current, _appPaneTheme, _appInterfacePercent, _applicationDetails.Font, CleanupConfirmationChrome);
            _flowMeter.SetProgress(Current?.Kind == V269PanelKind.Progress, Current?.ProgressPercent,
                _appPaneTheme, _appInterfacePercent, _applicationDetails.Font);
            int stateHeight=Math.Max(V269AppTable.Scale(36,_appInterfacePercent,DeviceDpi),_appFlowState.Font.Height*2+4);
            _appFlowState.Height=stateHeight;
            // Na confirmação de Limpeza, aproximar o estado do divisor e
            // separar o título da ação conforme o ritmo vertical do HTML.
            _flowStateGap.Height=F(CleanupConfirmationChrome ? 14 : 19);
            _flowTitleGap.Height=F(CleanupConfirmationChrome ? 16 : 12);
            _title.TextAlign=ContentAlignment.MiddleLeft;_title.Margin=Padding.Empty;
            bool distinct=!string.Equals(Current?.Title,_appFlowHeading.Text,StringComparison.Ordinal);
            _layout.RowStyles[0].Height=height+_flowStateGap.Height+stateHeight+_flowTitleGap.Height+
                (distinct?Math.Max(26,_title.Font.Height+6):0);
            return;
        }
        if(!_structuredApp)
        {
            _title.TextAlign=ContentAlignment.MiddleLeft;
            _title.Margin=Padding.Empty;
            _layout.RowStyles[0].Height=50;
            return;
        }
        int S(int n)=>V269AppTable.Scale(n,_appInterfacePercent,DeviceDpi);
        _title.TextAlign=ContentAlignment.TopLeft;
        _title.Margin=new Padding(0,0,0,S(12));
        // O host de título conserva o espaço que antes era a margem
        // da célula da tabela, inclusive nos detalhes já homologados.
        _titleHost.Padding=new Padding(0,0,0,S(12));
        _layout.RowStyles[0].Height=Math.Max(S(33),_title.Font.Height+S(12))+S(12);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if((!_structuredApp&&!_appFlow&&Current?.Kind!=V269PanelKind.DriverReview)||Width<3||Height<3)return;
        var state=e.Graphics.Save();
        try
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using var path=ModernButton.RoundRect(new Rectangle(0,0,Width-1,Height-1),
                V269AppTable.Scale(11,_appInterfacePercent,DeviceDpi));
            using var outline=new Pen(V269AppDetailStyle.Palette(_appPaneTheme).Border);
            e.Graphics.DrawPath(outline,path);
        }
        finally{e.Graphics.Restore(state);}
    }
    internal void ApplyAppInterfaceScale(int percent)
    {
        _appInterfacePercent=percent;_applicationDetails.ApplyInterfaceScale(percent);
        _operationHistory?.ApplyAppearance(_appPaneTheme,percent,_applicationDetails.Font);
        if(_structuredApp||_appFlow)ApplyPaneSurface();
        else ApplyReadOnlyAppearance();
        FitOperationHistory();
    }

    internal void SetFonts(Font title, Font body,Font appTitle,Font appBody)
    {
        _normalTitleFont=title;_appTitleFont=appTitle;
        _title.Font=(_structuredApp||_appFlow)?appTitle:title;
        _appFlowHeading.Font=appTitle;
        _description.Font=body;
        _applicationDetails.Font=appBody;
        _operationHistory?.ApplyAppearance(_appPaneTheme,_appInterfacePercent,appBody);
        ApplyReadOnlyAppearance();
        ApplyAppTitleMetrics();FitOperationHistory();
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
        (V269Page.Aplicativos, "Aplicativos", "Programas instalados, atualizações e remoção controlada."),
        (V269Page.Drivers, "Drivers", "Consulte dispositivos, verifique atualizações e gerencie drivers elegíveis."),
        (V269Page.Limpeza, "Limpeza e Desempenho", "Ferramentas de limpeza e visão do espaço em disco."),
        (V269Page.Reparo, "Reparo e Rede", "Verificações e reparos orientados do Windows e da rede."),
        (V269Page.Sistema, "Sistema", "Windows Update, planos de energia e gerenciamento de memória RAM."),
        (V269Page.Inicializacao, "Inicialização", "Gerencie entradas do seu usuário e consulte itens protegidos."),
        (V269Page.Configuracoes, "Configurações", "Ajuste o GERENCIADOR FINAL com temas variados e preferências de uso.")
    ];

    private readonly Dictionary<V269Page, V269NavButton> _navigation = new();
    private readonly Dictionary<V269Page, Control> _pages = new();
    // WinForms pode manter uma instância Font anterior quando o novo
    // valor é equivalente. Cache finito pelas escalas normalizadas:
    // nunca destruir uma fonte enquanto algum controle pode referenciá-la.
    private readonly Dictionary<(float Size, FontStyle Style, int Scale), Font> _ownedFonts = new();
    private int _appliedTextScale;
    private int _homeFontWidthClass;
    private readonly TableLayoutPanel _columns;
    private readonly TableLayoutPanel _navigationLayout;
    private readonly Panel _sidebar;
    private readonly Label _homeCopyright;
    private readonly Panel _homeNavRule;
    private readonly Panel _bodyHost;
    private readonly Panel _content;
    private readonly Label _pageTitle;
    private readonly Label _pageSubtitle;
    private readonly V269HomeHeadingIcon _homeGlyph;
    private readonly TableLayoutPanel _centralLayout;
    private readonly TableLayoutPanel _headingLayout;
    private readonly V269RightPane _rightPane;
    private bool _homeGeometry;
    private (V269Page Page, bool Mandatory, bool Retained, bool CompactOpen,
        bool? DashboardOpen, bool? AppOpen)? _lastPaneLayout;

    private (V269Page Page, bool Mandatory, bool Retained, bool CompactOpen,
        bool? DashboardOpen, bool? AppOpen) PaneLayoutState() =>
        (SelectedPage, _rightPane.Current?.Kind is V269PanelKind.Confirmation or V269PanelKind.Progress,
            _rightPane.Current?.Kind is V269PanelKind.Result or V269PanelKind.History or V269PanelKind.DriverReview,
            _compactPaneOpen, _dashboardPaneOpen, _appPaneOpen);

    internal event EventHandler<V269NavigationEventArgs>? NavigationChanged;
    internal V269Theme Theme { get; private set; } = V269Theme.AzulGf;
    internal int InterfaceScalePercent { get; private set; } = 100;
    internal int TextScalePercent { get; private set; } = 100;
    internal V269Page SelectedPage { get; private set; } = V269Page.Inicio;
    internal V269RightPane RightPane => _rightPane;
    private Button? _paneToggle;
    private bool _compactPaneOpen;
    private bool? _appPaneOpen;
    private int _appPanePlacement;
    private int _appPaneWidth=305;
    private V269AppSplitter? _appSplitter;
    private bool? _dashboardPaneOpen;
    private bool _movingHomeToggle;
    // Uma troca de aba pode fechar detalhes e publicar eventos do painel.
    // Consolidar esses pedidos antes de calcular a geometria da nova aba.
    private bool _selectingPage;
    // A aba só deve medir seu conteúdo após o shell negociar a área final.
    // Não bloqueia ApplyDimensions nem altera a publicação de NavigationChanged.
    internal bool IsPageLayoutTransition { get; private set; }
    internal event EventHandler? PageLayoutCommitted;

    internal void SetOperationContent(Control content)
    {
        _rightPane.SetOperationContent(content);
        var toggle = new Button { Name = "V269PaneToggle", Text = "Painel",
            AccessibleName = "Mostrar ou ocultar painel de operações",
            Dock = DockStyle.Top, Height = 30, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 3, 0, 0) };
        _paneToggle = toggle;
        toggle.Click += (_, _) =>
        {
            if (_rightPane.Visible && _rightPane.CloseReadOnly(true)) return;
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

        _sidebar = new V269HomeSurfacePanel(true) { Name = "V269Sidebar", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 7, 0) };
        _navigationLayout = new TableLayoutPanel
        {
            Name = "V269Navigation", AccessibleName = "Navegação principal",
            AccessibleRole = AccessibleRole.Grouping, Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = Pages.Length + 4,
            Margin = Padding.Empty, Padding = new Padding(6)
        };
        _navigationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < Pages.Length; i++)
            _navigationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        _navigationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++)
            _navigationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));

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
        _homeNavRule = new Panel { Name = "V269HomeNavRule", Dock = DockStyle.Top,
            Height = 1, Margin = Padding.Empty, Visible = false };
        _navigationLayout.Controls.Add(_homeNavRule, 0, Pages.Length + 1);
        _homeCopyright = new Label
        {
            Name = "V269HomeCopyright", Text = "GF · V2.6.9\n© 2026 Walisson Minel",
            Dock = DockStyle.Fill, Margin = new Padding(11, 3, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Visible = false
        };
        _navigationLayout.Controls.Add(_homeCopyright, 0, Pages.Length + 3);
        _sidebar.Controls.Add(_navigationLayout);

        _content = new V269HomeSurfacePanel(false) { Name = "V269Main", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 7, 0) };
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
                or V269PanelKind.Result or V269PanelKind.History or V269PanelKind.DriverReview)
            {
                _compactPaneOpen = true;
                _dashboardPaneOpen = null;
                _appPaneOpen=null;
            }
            // Alterar texto, percentual ou histórico não muda a geometria
            // da janela quando a necessidade de espaço do painel é a mesma.
            if (_lastPaneLayout != PaneLayoutState()) ApplyDimensions();
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
        if (control.Parent != _bodyHost)
        {
            // Nunca expor uma view inativa durante a montagem do shell.
            control.Visible = page == SelectedPage;
            _bodyHost.Controls.Add(control);
        }
        RefreshVisiblePage();
    }

    internal Control? PageContent(V269Page page) =>
        _pages.TryGetValue(page, out var content) ? content : null;

    internal void SelectPage(V269Page page)
    {
        if (!Enum.IsDefined(page)) throw new ArgumentOutOfRangeException(nameof(page));
        bool changed = SelectedPage != page;
        // Um segundo clique na aba já ativa não altera seleção nem geometria.
        // SizeChanged e ApplyAppearance mantêm seus próprios percursos.
        if (!changed && _pages.TryGetValue(page, out var active) && active.Visible &&
            _pageTitle.Text == Pages[(int)page].Title && _navigation[page].IsSelected)
            return;
        IsPageLayoutTransition = true;
        try
        {
            _selectingPage = true;
            try
            {
                if (changed)
                {
                    _rightPane.CloseReadOnly(false);
                    _dashboardPaneOpen = null;
                }
                if (changed && SelectedPage == V269Page.Drivers &&
                    _rightPane.Current is { Kind: V269PanelKind.DriverReview, DriverReview: { IsDisposed: false } review })
                {
                    review.Close();
                    if (review.IsDisposed && ReferenceEquals(_rightPane.Current?.DriverReview, review))
                        _rightPane.Present(null);
                }
                if (changed && (_rightPane.Current?.FlowResult is not null ||
                    (_rightPane.Current?.Module is not null && _rightPane.Current.FlowPage==SelectedPage) ||
                    (SelectedPage == V269Page.Aplicativos && _rightPane.Current?.Application is not null) ||
                    (SelectedPage == V269Page.Drivers && _rightPane.Current?.Driver is not null) ||
                    (SelectedPage == V269Page.Limpeza && (_rightPane.Current?.Cleanup is not null || _rightPane.Current?.FlowResult?.Page == V269Page.Limpeza)) ||
                    (SelectedPage == V269Page.Reparo && (_rightPane.Current?.Repair is not null || _rightPane.Current?.FlowResult?.Page == V269Page.Reparo))))
                    _rightPane.Present(null);
                SelectedPage = page;
                _rightPane.SetAppHistoryPage(page==V269Page.Aplicativos, page==V269Page.Drivers, page==V269Page.Limpeza, page==V269Page.Reparo,
                    page is V269Page.Sistema or V269Page.Inicializacao ? page : null);
                var descriptor = Pages[(int)page];
                _pageTitle.Text = descriptor.Title;
                _pageSubtitle.Text = descriptor.Subtitle;
                RefreshVisiblePage();
                ApplyNavigationSelection(V269VisualTokens.ForTheme(Theme));
            }
            finally { _selectingPage = false; }
            ApplyDimensions();
        }
        finally { IsPageLayoutTransition = false; }
        // Despachar na mesma pilha de clique, antes do evento público.
        // Nenhum timer, BeginInvoke ou medida de outra aba.
        PageLayoutCommitted?.Invoke(this, EventArgs.Empty);
        if (changed) NavigationChanged?.Invoke(this, new V269NavigationEventArgs(page));
    }

    internal void ShowV269AppPane()
    {
        if(SelectedPage is not (V269Page.Aplicativos or V269Page.Drivers))return;
        if(_rightPane.Current?.Kind is V269PanelKind.Confirmation or V269PanelKind.Progress)return;
        _appPaneOpen=!_rightPane.Visible;ApplyDimensions();
    }

    internal void EnsureV269AppPane()
    {
        if(SelectedPage is not (V269Page.Aplicativos or V269Page.Drivers))return;
        _appPaneOpen=true;ApplyDimensions();
    }

    internal void EnsureV269RepairPane()
    {
        if (SelectedPage != V269Page.Reparo) return;
        if (_compactPaneOpen && _rightPane.Visible) return;
        _compactPaneOpen = true;
        ApplyDimensions();
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
        _rightPane.ApplyAppInterfaceScale(InterfaceScalePercent);
        _pageTitle.ForeColor = palette.Text;
        _pageSubtitle.ForeColor = palette.SecondaryText;
        // A regra final do SVG no HTML prevalece sobre o cinza do contêiner.
        _homeGlyph.ForeColor = Theme switch {
            V269Theme.Dark => ColorTranslator.FromHtml("#65b8ef"),
            V269Theme.Claro => ColorTranslator.FromHtml("#2577a8"),
            _ => ColorTranslator.FromHtml("#69bfe0") };
        ApplyFonts();
        ApplyNavigationSelection(palette);
        ApplyDimensions();
        ResumeLayout(true);
    }

    private void ApplyFonts()
    {
        int widthClass = !_homeGeometry ? 0 : LayoutWindowWidth <= 850 ? 1 :
            LayoutWindowWidth <= 1250 ? 2 : 3;
        int screenHeight = _homeGeometry ? V269HomeMetrics.LogicalScreenHeight(this) : int.MaxValue;
        int fontClass = widthClass + (screenHeight <= 810 ? 4 : 0);
        if (_appliedTextScale == TextScalePercent && _homeFontWidthClass == fontClass) return;
        _appliedTextScale = TextScalePercent;
        _homeFontWidthClass = fontClass;
        Font NewFont(float size, FontStyle style = FontStyle.Regular)
        {
            var key = (size, style, TextScalePercent);
            if (_ownedFonts.TryGetValue(key, out Font? existing)) return existing;
            var value = new Font("Segoe UI", size * TextScalePercent / 100f, style);
            _ownedFonts.Add(key, value);
            return value;
        }
        Font = NewFont(9.5f);
        // Tamanho efetivo CSS: título 20/24/28 px, subtítulo 10/12 px,
        // navegação 11/12/14 px. 1 CSS px = 0,75 pt em 96 DPI.
        _pageTitle.Font = NewFont(_homeGeometry
            ? V269HomeMetrics.TitlePixels(LayoutWindowWidth, screenHeight) * .75f : 17f, FontStyle.Bold);
        _pageSubtitle.Font = NewFont(widthClass == 1 ? 7.5f : 9f);
        _homeCopyright.Font = NewFont(widthClass == 1 ? 6.75f : 7.5f);
        _rightPane.SetFonts(NewFont(12f, FontStyle.Bold), NewFont(9.5f),
            NewFont(13.5f,FontStyle.Bold),NewFont(9f));
        foreach (var navButton in _navigation.Values)
            navButton.Font = NewFont(widthClass switch { 1 => 8.25f, 2 => 9f, 3 => 10.5f, _ => 10f });
        // As fontes são liberadas somente após a destruição dos controles.
    }

    // A referência usa a largura da superfície da janela (gfwindow).
    // O Form pode ter outra largura nos testes de resolução simulada.
    internal int LayoutWindowWidth => Math.Max(1, (int)Math.Round(
        (Parent?.ClientSize.Width ?? ClientSize.Width) * 96d / DeviceDpi));

    private void ApplyDimensions()
    {
        if (_selectingPage || _columns is null || _navigationLayout is null) return;
        // Aplicar as mesmas propriedades em uma transação visual.
        _columns.SuspendLayout();
        _centralLayout.SuspendLayout();
        _headingLayout.SuspendLayout();
        _navigationLayout.SuspendLayout();
        try
        {
            ApplyDimensionsCore();
            _lastPaneLayout = PaneLayoutState();
        }
        finally
        {
            _columns.ResumeLayout(true);
            _centralLayout.ResumeLayout(true);
            _headingLayout.ResumeLayout(true);
            _navigationLayout.ResumeLayout(true);
        }
    }

    private void ApplyDimensionsCore()
    {
        int width = Math.Max(1, ClientSize.Width);
        bool apps=SelectedPage is V269Page.Aplicativos or V269Page.Drivers;
        bool repair = SelectedPage == V269Page.Reparo;
        bool cleanup = SelectedPage == V269Page.Limpeza;
        bool modules = SelectedPage is V269Page.Sistema or V269Page.Inicializacao;
        bool split = apps || repair || cleanup || modules;
        bool home = true;
        int windowWidth = LayoutWindowWidth;
        // Início usa os breakpoints do CSS congelado, medidos pela janela.
        int S(int n) => V269VisualTokens.Scale(n, InterfaceScalePercent);
        if (_homeGeometry != home)
        {
            _homeGeometry = home;
            _appliedTextScale = 0;
        }
        ApplyFonts();
        ApplyHomeSurfaces(home);
        _homeGlyph.Visible = home;
        _homeGlyph.Page = SelectedPage;
        _homeGlyph.ForeColor = SelectedPage == V269Page.Drivers ? V269DriverVisuals.GlyphInk(V269DriverGlyph.Chip, Theme) :
            repair ? V269RepairVisuals.WrenchInk(Theme) : cleanup ? ColorTranslator.FromHtml(Theme switch { V269Theme.Claro=>"#087d82",V269Theme.Dark=>"#55cbb5",_=>"#6bd6b2" }) : Theme switch {
            V269Theme.Dark => ColorTranslator.FromHtml(SelectedPage is V269Page.Configuracoes or V269Page.Aplicativos ? "#b998f2" : "#65b8ef"),
            V269Theme.Claro => ColorTranslator.FromHtml(SelectedPage is V269Page.Configuracoes or V269Page.Aplicativos ? "#7046c1" : "#2577a8"),
            _ => ColorTranslator.FromHtml(SelectedPage is V269Page.Configuracoes or V269Page.Aplicativos ? "#b7a5f5" : "#69bfe0") };
        if(SelectedPage==V269Page.Inicializacao) _homeGlyph.ForeColor=Theme==V269Theme.Claro?ColorTranslator.FromHtml("#ad690d"):ColorTranslator.FromHtml("#f2c477");
        _homeGlyph.Invalidate();
        _headingLayout.ColumnStyles[0].Width = home ? S(57) : 0;
        _centralLayout.RowStyles[0].Height = home ? S(windowWidth <= 1040 ? 57 : 68) : 70;
        _headingLayout.Margin = home ? new Padding(0, 0, 0, S(3)) : Padding.Empty;
        _centralLayout.Padding = home ? new Padding(S(13), S(13), S(13), S(12)) : new Padding(13, 8, 13, 10);
        // As demais abas conservam as dimensões existentes nesta frente.
        int sidebar = V269VisualTokens.Scale(width < 1040 ? 166 : 210, InterfaceScalePercent);
        int right = V269VisualTokens.Scale(width < 1040 ? 240 : 310, InterfaceScalePercent);
        // O HTML aprovado reserva toda a área útil a Início e Configurações.
        // A exceção é um fluxo de operação: confirmação/progresso não podem
        // desaparecer, e o resultado/histórico continua acessível entre abas.
        bool dashboard = SelectedPage is V269Page.Inicio or V269Page.Configuracoes;
        bool mandatory = _rightPane.Current?.Kind is V269PanelKind.Confirmation or V269PanelKind.Progress;
        bool retained = _rightPane.Current?.Kind is V269PanelKind.Result or V269PanelKind.History or V269PanelKind.DriverReview;
        bool showRight = !dashboard || mandatory || retained;
        if (_paneToggle is { } toggle)
        {
            if (!_movingHomeToggle)
            {
                _movingHomeToggle = true;
                try
                {
                    if (toggle.Parent?.Parent is V269HomeDashboard oldHome && SelectedPage != V269Page.Inicio)
                        oldHome.DetachPaneToggle(toggle);
                    if (toggle.Parent is V269SettingsPage oldSettings && SelectedPage != V269Page.Configuracoes)
                        oldSettings.DetachPaneToggle(toggle);
                    if (SelectedPage == V269Page.Inicio && PageContent(V269Page.Inicio) is V269HomeDashboard dashboardView)
                        dashboardView.AttachPaneToggle(toggle);
                    else if (SelectedPage == V269Page.Configuracoes && PageContent(V269Page.Configuracoes) is V269SettingsPage settingsView)
                        settingsView.AttachPaneToggle(toggle);
                    else if (toggle.Parent != _navigationLayout)
                    {
                        toggle.AutoSize = false;
                        toggle.MinimumSize = Size.Empty;
                        toggle.Dock = DockStyle.Top;
                        toggle.Height = 30;
                        toggle.Margin = new Padding(0, 3, 0, 0);
                        _navigationLayout.Controls.Add(toggle, 0, Pages.Length);
                    }
                }
                finally { _movingHomeToggle = false; }
            }
            bool compact = split ? windowWidth<=1040 : width < 1060;
            // O painel é fechado por padrão no dashboard, mas o histórico
            // e a exportação local continuam acessíveis por comando explícito.
            toggle.Visible = !apps && (compact || dashboard);
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
        if(apps)
        {
            bool compact=windowWidth<=1040;
            showRight=mandatory||(!compact||(_appPaneOpen??retained));
            right=S(_appPaneWidth);
        }
        if (repair || modules) showRight = mandatory || windowWidth > 1040 || _compactPaneOpen || (modules && retained);
        if (cleanup) showRight = mandatory || windowWidth > 1040 || _compactPaneOpen || retained;
        bool below=split&&windowWidth<=1040&&showRight;
        if(!showRight||split)right=0;
        PlaceV269AppPane(split&&showRight?below?2:1:0);
        if (home)
        {
            // As medidas CSS já incluem borda e padding da barra lateral.
            int sidebarCss = windowWidth <= 1040 ? 175 : windowWidth <= 1250 ? 195 : 236;
            sidebar = S(sidebarCss + 11);
            _columns.Padding = new Padding(S(9));
            _sidebar.Margin = new Padding(0, 0, S(11), 0);
            _content.Margin = new Padding(0,0,showRight&&!split?S(10):0,0);
            _navigationLayout.Padding = new Padding(S(8), S(8), S(8), S(9));
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
        _rightPane.Visible = right > 0 || split&&showRight;
        int navHeight = _paneToggle is null ? 48 :
            Math.Clamp((ClientSize.Height - 64) / Pages.Length, 27, 48);
        // No HTML, Configurações pertence a .nav-end, não a .nav-list.
        // Preservar o rodapé também em janelas pequenas, sem perder a ação
        // explícita de histórico/operações, que já existia no produto.
        int spacerRow = Pages.Length - 1, toggleRow = Pages.Length,
            ruleRow = Pages.Length + 1, settingsRow = Pages.Length + 2,
            copyrightRow = Pages.Length + 3;
        int available = Math.Max(1, ClientSize.Height - _columns.Padding.Vertical -
            _navigationLayout.Padding.Vertical);
        bool compactHome = home && windowWidth <= 850;
        int copyrightHeight = compactHome ? S(31) : S(39);
        bool footerFits = home && available >= S(compactHome ? 455 : 500);
        int toggleHeight = _paneToggle?.Visible == true && _paneToggle.Parent == _navigationLayout ? S(34) : 0;
        if (home)
        {
            int desired = windowWidth <= 1250 ? 58 : 60;
            int fixedHeight = toggleHeight + S(8) + (footerFits ? copyrightHeight : 0);
            navHeight = Math.Min(desired, Math.Max(20, (int)Math.Floor(
                Math.Max(1, available - fixedHeight) * 100d / InterfaceScalePercent / Pages.Length)));
        }
        var settings = _navigation[V269Page.Configuracoes];
        _navigationLayout.SuspendLayout();
        try
        {
            for (int i = 0; i < spacerRow; i++)
            {
                _navigationLayout.RowStyles[i].SizeType = SizeType.Absolute;
                _navigationLayout.RowStyles[i].Height = S(navHeight);
            }
            _navigationLayout.RowStyles[spacerRow].SizeType = home ? SizeType.Percent : SizeType.Absolute;
            _navigationLayout.RowStyles[spacerRow].Height = home ? 100 : S(navHeight);
            _navigationLayout.RowStyles[toggleRow].SizeType = home ? SizeType.Absolute : SizeType.Percent;
            _navigationLayout.RowStyles[toggleRow].Height = home ? toggleHeight : 100;
            _navigationLayout.RowStyles[ruleRow].Height = home ? S(8) : 0;
            _navigationLayout.RowStyles[settingsRow].Height = home ? S(navHeight) : 0;
            _navigationLayout.RowStyles[copyrightRow].Height = footerFits ? copyrightHeight : 0;
            int targetRow = home ? settingsRow : spacerRow;
            if (_navigationLayout.GetRow(settings) != targetRow)
                _navigationLayout.SetRow(settings, targetRow);
            // A borda selecionada do HTML inicia em y=91 (capturas de
            // 760 e 992); a margem legada de 2px a deslocava para y=93.
            // Conservar o rodapé alcançável no viewport compacto.
            foreach (var button in _navigation.Values)
                button.Margin = home ? new Padding(0, 0, 0, 2) : new Padding(0, 2, 0, 2);
            _homeNavRule.Visible = home;
            _homeCopyright.Visible = footerFits;
            _homeCopyright.Margin = compactHome ? new Padding(S(4), S(3), 0, 0) : new Padding(S(11), S(3), 0, 0);
            _homeNavRule.BackColor = Theme switch {
                V269Theme.Dark => ColorTranslator.FromHtml("#4d535a"),
                V269Theme.Claro => ColorTranslator.FromHtml("#8db1ca"),
                _ => ColorTranslator.FromHtml("#24465f") };
            _homeCopyright.ForeColor = Theme switch {
                V269Theme.Dark => ColorTranslator.FromHtml("#a7b0b8"),
                V269Theme.Claro => ColorTranslator.FromHtml("#3f627a"),
                _ => ColorTranslator.FromHtml("#748fa5") };
        }
        finally { _navigationLayout.ResumeLayout(true); }
        foreach (var button in _navigation.Values)
        {
            button.HomePresentation = home;
            button.HomeScale = InterfaceScalePercent;
            button.CurrentTheme = Theme;
            // O botão desenha a superfície do menu explicitamente, sem pedir
            // que o WinForms repinte uma cadeia de pais transparentes.
            button.BackColor = V269VisualTokens.ForTheme(Theme).Sidebar;
            if (!home && button.IsSelected) button.BackColor = V269VisualTokens.ForTheme(Theme).Selection;
            button.Invalidate();
        }
    }

    // Gradientes e bordas da última cascata do HTML congelado, somente
    // na aba Início. Os demais painéis conservam sua paleta original.
    // A superfície de Aplicativos contém toolbar, tabela, divisor e painel,
    // sem alterar as posições de Início, Configurações ou outras views.
    private void PlaceV269AppPane(int placement)
    {
        int S(int value)=>V269VisualTokens.Scale(value,InterfaceScalePercent);
        if(_appPanePlacement!=placement)
        {
            _columns.SuspendLayout();_centralLayout.SuspendLayout();
            try
            {
                _rightPane.Parent?.Controls.Remove(_rightPane);
                if(_appSplitter?.Parent is not null)_appSplitter.Parent.Controls.Remove(_appSplitter);
                _centralLayout.SetColumnSpan(_headingLayout,1);
                _centralLayout.ColumnCount=1;_centralLayout.RowCount=2;
                while(_centralLayout.ColumnStyles.Count>1)_centralLayout.ColumnStyles.RemoveAt(1);
                while(_centralLayout.RowStyles.Count>2)_centralLayout.RowStyles.RemoveAt(2);
                _centralLayout.RowStyles[1].Height=100;
                if(placement==1)
                {
                    _centralLayout.ColumnCount=3;
                    _centralLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,S(25)));
                    _centralLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,S(_appPaneWidth)));
                    _centralLayout.SetColumnSpan(_headingLayout,3);
                    if(_appSplitter is null)
                    {
                        _appSplitter=new V269AppSplitter();
                        _appSplitter.MoveBy+=delta=>{
                            int logical=(int)Math.Round(delta*100d/InterfaceScalePercent);
                            _appPaneWidth=Math.Clamp(_appPaneWidth-logical,210,480);
                            ApplyDimensions();
                        };
                    }
                    _centralLayout.Controls.Add(_appSplitter,1,1);
                    _centralLayout.Controls.Add(_rightPane,2,1);
                }
                else if(placement==2)
                {
                    _centralLayout.RowCount=3;_centralLayout.RowStyles[1].Height=58;
                    _centralLayout.RowStyles.Add(new RowStyle(SizeType.Percent,42));
                    _centralLayout.Controls.Add(_rightPane,0,2);
                }
                else _columns.Controls.Add(_rightPane,2,0);
                _appPanePlacement=placement;
            }
            finally{_centralLayout.ResumeLayout(true);_columns.ResumeLayout(true);}
        }
        if(placement==1)
        {
            int sidebarCss=LayoutWindowWidth<=1250?195:236;
            int available=Math.Max(1,ClientSize.Width-S(sidebarCss+11+18+26));
            int width=Math.Min(S(_appPaneWidth),Math.Max(S(170),(int)(available*.45)));
            _centralLayout.ColumnStyles[1].Width=S(25);
            _centralLayout.ColumnStyles[2].Width=width;
            if(_appSplitter is not null)_appSplitter.ForeColor=V269VisualTokens.ForTheme(Theme).Border;
        }
        _rightPane.Margin=placement==2?new Padding(0,S(10),0,0):Padding.Empty;
    }

    private void ApplyHomeSurfaces(bool home)
    {
        var palette = V269VisualTokens.ForTheme(Theme);
        var side = (V269HomeSurfacePanel)_sidebar;
        var main = (V269HomeSurfacePanel)_content;
        side.Configure(home, Theme);
        main.Configure(home, Theme);
        if (home)
        {
            _columns.BackColor = V269VisualTokens.WorkspaceBase(Theme);
            _navigationLayout.BackColor = Color.Transparent;
            _centralLayout.BackColor = _headingLayout.BackColor = _bodyHost.BackColor = Color.Transparent;
            _pageTitle.ForeColor = ColorTranslator.FromHtml(Theme switch {
                V269Theme.Dark => "#dce0e4", V269Theme.Claro => "#17374c", _ => "#e4edf6" });
            _pageSubtitle.ForeColor = ColorTranslator.FromHtml(Theme switch {
                V269Theme.Dark => "#adb6be", V269Theme.Claro => "#3f627a", _ => "#adbfce" });
        }
        else
        {
            _columns.BackColor = palette.Background;
            _navigationLayout.BackColor = palette.Sidebar;
            _centralLayout.BackColor = _headingLayout.BackColor = _bodyHost.BackColor = palette.Content;
            _pageTitle.ForeColor = palette.Text;
            _pageSubtitle.ForeColor = palette.SecondaryText;
        }
    }

    private void RefreshVisiblePage()
    {
        // Não reconstruir ou descartar views: seleção, rolagem e operações
        // devem continuar sob controle da aba/motor que as criou.
        _bodyHost.SuspendLayout();
        try
        {
            // Primeiro ocultar todas as views anteriores; depois revelar a ativa.
            // A ordem de inserção não pode produzir duas views simultaneamente.
            foreach (var (page, content) in _pages)
                if (page != SelectedPage) content.Visible = false;
            if (_pages.TryGetValue(SelectedPage, out Control? active))
            {
                active.BringToFront();
                active.Visible = true;
            }
        }
        finally { _bodyHost.ResumeLayout(true); }
    }

    private void ApplyNavigationSelection(V269Palette palette)
    {
        foreach (var (page, button) in _navigation)
        {
            bool selected = page == SelectedPage;
            button.BackColor = !_homeGeometry && selected ? palette.Selection : palette.Sidebar;
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
        internal int HomeScale = 100;
        internal V269Theme CurrentTheme;

        internal V269NavButton(V269Page page)
        {
            Page = page;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
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
        internal static Color HomeIconColor(V269Theme theme, V269Page page, bool selected) =>
            V269VisualTokens.NavigationGlyph(theme,page,selected);

        private void PaintHomeNavigation(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var theme = CurrentTheme;
            Color H(string h) => ColorTranslator.FromHtml(h);
            // Toda pintura inicializa o fundo, inclusive a área não selecionada.
            // A origem do gradiente é a barra inteira, não cada botão.
            var origin = Location;
            Control? ancestor = Parent;
            while (ancestor is not null && ancestor is not V269HomeSurfacePanel)
            {
                origin.Offset(ancestor.Location);
                ancestor = ancestor.Parent;
            }
            using (var background = new SolidBrush(BackColor))
                g.FillRectangle(background, ClientRectangle);
            if (ancestor is V269HomeSurfacePanel surface)
                surface.PaintChildSurface(g, origin, ClientRectangle);
            var area = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using var shape = Rounded(area, 9);
            if (IsSelected)
            {
                var (first,last,border) = V269VisualTokens.NavigationSelection(theme);
                using var fill = new LinearGradientBrush(area, first, last, theme == V269Theme.Dark ? 15f : 0f);
                using var line = new Pen(border);
                g.FillPath(fill,shape); g.DrawPath(line,shape);
                using var rail = new SolidBrush(V269VisualTokens.NavigationSelectionRail(theme));
                using var railShape = Rounded(new Rectangle(1,9,4,Math.Max(1,Height-18)),2);
                g.FillPath(rail,railShape);
            }
            else
            {
                using var border = new Pen(Color.FromArgb(5, theme == V269Theme.Claro ? Color.Black : Color.White));
                g.DrawPath(border,shape);
            }
            int S(int px) => Math.Max(1, (int)Math.Round(px * HomeScale * DeviceDpi / 9600d));
            int iconSize = Math.Min(S(36), Math.Max(S(18), Height - S(10)));
            var icon = new Rectangle(S(9), (Height-iconSize)/2, iconSize, iconSize);
            using var iconShape = Rounded(icon, 11);
            // Cores efetivas de .nav-item .ic e .nav-item.active .ic,
            // verificadas nos três temas da referência congelada.
            var (iconTop, iconBottom, iconOutline) = V269VisualTokens.NavigationIconSurface(theme,IsSelected);
            using var tint = new LinearGradientBrush(icon, iconTop, iconBottom, 90f);
            using var iconBorder = new Pen(iconOutline);
            g.FillPath(tint,iconShape); g.DrawPath(iconBorder,iconShape);
            int glyphSize = Math.Min(iconSize, S(18));
            var glyph = new Rectangle(icon.X+(icon.Width-glyphSize)/2,icon.Y+(icon.Height-glyphSize)/2,glyphSize,glyphSize);
            V269HomeHeadingIcon.DrawGlyph(g, glyph, Page, HomeIconColor(theme, Page, IsSelected));
            using var textFont = new Font(Font, IsSelected ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, Text, textFont,
                new Rectangle(icon.Right+S(8), 1, Math.Max(1,Width-icon.Right-S(12)), Math.Max(1,Height-2)),
                V269VisualTokens.NavigationPaintInk(theme,IsSelected),
                TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
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

internal sealed class V269HomeSurfacePanel : Panel
{
    private readonly bool _sidebar;
    private bool _home;
    private V269Theme _theme;

    internal V269HomeSurfacePanel(bool sidebar)
    {
        _sidebar = sidebar;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
    }

    internal void Configure(bool home, V269Theme theme)
    {
        _home = home; _theme = theme;
        Padding = home ? new Padding(1) : Padding.Empty;
        var palette = V269VisualTokens.ForTheme(theme);
        BackColor = home ? V269VisualTokens.WorkspaceBase(theme)
            : _sidebar ? palette.Sidebar : palette.Content;
        Invalidate(true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!_home || Width < 3 || Height < 3) { base.OnPaintBackground(e); return; }
        PaintSurface(e.Graphics);
    }

    internal void PaintChildSurface(Graphics graphics, Point offset, Rectangle childBounds)
    {
        if (!_home || Width < 3 || Height < 3) return;
        var state = graphics.Save();
        try
        {
            graphics.SetClip(childBounds, CombineMode.Intersect);
            graphics.TranslateTransform(-offset.X, -offset.Y);
            PaintSurface(graphics);
        }
        finally { graphics.Restore(state); }
    }

    private void PaintSurface(Graphics graphics)
    {
        // Fundo opaco antes de desenhar cantos arredondados/gradiente.
        using (var background = new SolidBrush(BackColor))
            graphics.FillRectangle(background, ClientRectangle);
        var (first,last,border,angle) = V269VisualTokens.WorkspaceGradient(_theme,_sidebar);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = V269Shell.Rounded(bounds, Math.Max(1, 12 * DeviceDpi / 96));
        using var fill = new LinearGradientBrush(bounds, first, last, angle);
        using var outline = new Pen(border);
        graphics.FillPath(fill, path);
        graphics.DrawPath(outline, path);
    }
}

internal sealed class V269AppSplitter:Control
{
    private int _lastX;private bool _drag;
    internal event Action<int>? MoveBy;
    internal V269AppSplitter()
    {
        Name="V269AppSplitter";Dock=DockStyle.Fill;Margin=Padding.Empty;TabStop=true;
        AccessibleName="Redimensionar painel de detalhes";AccessibleRole=AccessibleRole.Separator;
        Cursor=Cursors.VSplit;
        SetStyle(ControlStyles.UserPaint|ControlStyles.SupportsTransparentBackColor|
            ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
        BackColor=Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        using var pen=new Pen(ForeColor,Focused?3:2);
        e.Graphics.DrawLine(pen,Width/2,Height*42/100,Width/2,Height*58/100);
        if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;
        Focus();_drag=true;_lastX=PointToScreen(e.Location).X;Capture=true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);if(!_drag||!Capture)return;
        int now=PointToScreen(e.Location).X,delta=now-_lastX;_lastX=now;
        if(delta!=0)MoveBy?.Invoke(delta);
    }
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);_drag=false;Capture=false;}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)_drag=false;}
    protected override bool IsInputKey(Keys keyData)=>(keyData&Keys.KeyCode) is Keys.Left or Keys.Right||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(e.KeyCode is Keys.Left or Keys.Right){MoveBy?.Invoke(e.KeyCode==Keys.Left?-12:12);e.Handled=true;return;}
        base.OnKeyDown(e);
    }
}

internal sealed class V269HomeHeadingIcon : Control
{
    internal V269Page Page = V269Page.Inicio;
    internal V269HomeHeadingIcon()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        int n = Math.Min((int)Math.Round(34 * DeviceDpi / 96d), Math.Min(Width, Height));
        int iconBox = Math.Min(Width, (int)Math.Round(43 * DeviceDpi / 96d));
        if (n > 1) DrawGlyph(e.Graphics, new Rectangle(Math.Max(0, (iconBox - n) / 2),
            (Height - n) / 2, n, n), Page, ForeColor);
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
            using var p = new Pen(color, 2.05f) { StartCap=LineCap.Round, EndCap=LineCap.Round, LineJoin=LineJoin.Round };
            // Geometria derivada dos iconPaths originais da PR #109.
            // Arcos SVG convertidos em segmentos Bézier cúbicos.
            switch (page)
            {
                case V269Page.Inicio:
                    // home: <path d="m3 10 9-7 9 7v10a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1Z"/>
                    {
                        using var path = new GraphicsPath();
                        path.StartFigure();
                        path.AddLine(3f, 10f, 12f, 3f);
                        path.AddLine(12f, 3f, 21f, 10f);
                        path.AddLine(21f, 10f, 21f, 20f);
                        path.AddBezier(21f, 20f, 21f, 20.552285f, 20.552285f, 21f, 20f, 21f);
                        path.AddLine(20f, 21f, 15f, 21f);
                        path.AddLine(15f, 21f, 15f, 14f);
                        path.AddLine(15f, 14f, 9f, 14f);
                        path.AddLine(9f, 14f, 9f, 21f);
                        path.AddLine(9f, 21f, 4f, 21f);
                        path.AddBezier(4f, 21f, 3.447715f, 21f, 3f, 20.552285f, 3f, 20f);
                        path.CloseFigure();
                        g.DrawPath(p, path);
                    }
                    break;
                case V269Page.Aplicativos:
                    // apps: <rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>
                    using (var rect = V269Shell.Rounded(new Rectangle(3, 3, 7, 7), 1)) g.DrawPath(p, rect);
                    using (var rect = V269Shell.Rounded(new Rectangle(14, 3, 7, 7), 1)) g.DrawPath(p, rect);
                    using (var rect = V269Shell.Rounded(new Rectangle(3, 14, 7, 7), 1)) g.DrawPath(p, rect);
                    using (var rect = V269Shell.Rounded(new Rectangle(14, 14, 7, 7), 1)) g.DrawPath(p, rect);
                    break;
                case V269Page.Drivers:
                    // cpu: <rect x="6" y="6" width="12" height="12" rx="1"/><rect x="9" y="9" width="6" height="6"/><path d="M9 2v4m6-4v4M9 18v4m6-4v4M2 9h4m-4 6h4m12-6h4m-4 6h4"/>
                    using (var rect = V269Shell.Rounded(new Rectangle(6, 6, 12, 12), 1)) g.DrawPath(p, rect);
                    g.DrawRectangle(p, 9f, 9f, 6f, 6f);
                    {
                        using var path = new GraphicsPath();
                        path.StartFigure();
                        path.AddLine(9f, 2f, 9f, 6f);
                        path.StartFigure();
                        path.AddLine(15f, 2f, 15f, 6f);
                        path.StartFigure();
                        path.AddLine(9f, 18f, 9f, 22f);
                        path.StartFigure();
                        path.AddLine(15f, 18f, 15f, 22f);
                        path.StartFigure();
                        path.AddLine(2f, 9f, 6f, 9f);
                        path.StartFigure();
                        path.AddLine(2f, 15f, 6f, 15f);
                        path.StartFigure();
                        path.AddLine(18f, 9f, 22f, 9f);
                        path.StartFigure();
                        path.AddLine(18f, 15f, 22f, 15f);
                        g.DrawPath(p, path);
                    }
                    break;
                case V269Page.Limpeza:
                    // brush: <path d="M12 2v12" stroke-width="2.6"/><path d="M7 14h10l3 8H4l3-8Z" fill="currentColor" fill-opacity=".25"/><path d="M7 14h10M8 18v4m4-4v4m4-4v4M5 19h14"/>
                    {
                        using var path = new GraphicsPath();
                        path.StartFigure();
                        path.AddLine(12f, 2f, 12f, 14f);
                        using var stroke = new Pen(color, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                        g.DrawPath(stroke, path);
                    }
                    {
                        using var path = new GraphicsPath();
                        path.StartFigure();
                        path.AddLine(7f, 14f, 17f, 14f);
                        path.AddLine(17f, 14f, 20f, 22f);
                        path.AddLine(20f, 22f, 4f, 22f);
                        path.AddLine(4f, 22f, 7f, 14f);
                        path.CloseFigure();
                        using var fill = new SolidBrush(Color.FromArgb(64, color));
                        g.FillPath(fill, path);
                        g.DrawPath(p, path);
                    }
                    {
                        using var path = new GraphicsPath();
                        path.StartFigure();
                        path.AddLine(7f, 14f, 17f, 14f);
                        path.StartFigure();
                        path.AddLine(8f, 18f, 8f, 22f);
                        path.StartFigure();
                        path.AddLine(12f, 18f, 12f, 22f);
                        path.StartFigure();
                        path.AddLine(16f, 18f, 16f, 22f);
                        path.StartFigure();
                        path.AddLine(5f, 19f, 19f, 19f);
                        g.DrawPath(p, path);
                    }
                    break;
                case V269Page.Reparo:
                    // wrench: <path d="M14.5 6a5 5 0 0 0-6 6L3 17.5 6.5 21l5.5-5.5a5 5 0 0 0 6-6l-4 2-3-3z"/>
                    {
                        using var path = new GraphicsPath();
                        path.StartFigure();
                        path.AddBezier(14.5f, 6f, 12.818854f, 5.610271f, 11.05557f, 6.11502f, 9.835295f, 7.335295f);
                        path.AddBezier(9.835295f, 7.335295f, 8.61502f, 8.55557f, 8.110271f, 10.318854f, 8.5f, 12f);
                        path.AddLine(8.5f, 12f, 3f, 17.5f);
                        path.AddLine(3f, 17.5f, 6.5f, 21f);
                        path.AddLine(6.5f, 21f, 12f, 15.5f);
                        path.AddBezier(12f, 15.5f, 13.681146f, 15.889729f, 15.44443f, 15.38498f, 16.664705f, 14.164705f);
                        path.AddBezier(16.664705f, 14.164705f, 17.88498f, 12.94443f, 18.389729f, 11.181146f, 18f, 9.5f);
                        path.AddLine(18f, 9.5f, 14f, 11.5f);
                        path.AddLine(14f, 11.5f, 11f, 8.5f);
                        path.CloseFigure();
                        g.DrawPath(p, path);
                    }
                    break;
                case V269Page.Sistema:
                    // monitor: <rect x="3" y="4" width="18" height="14" rx="2"/><path d="M8 22h8M12 18v4"/>
                    using (var rect = V269Shell.Rounded(new Rectangle(3, 4, 18, 14), 2)) g.DrawPath(p, rect);
                    {
                        using var path = new GraphicsPath();
                        path.StartFigure();
                        path.AddLine(8f, 22f, 16f, 22f);
                        path.StartFigure();
                        path.AddLine(12f, 18f, 12f, 22f);
                        g.DrawPath(p, path);
                    }
                    break;
                case V269Page.Inicializacao:
                    // power: <path d="M12 2v10M7.1 5.2a9 9 0 1 0 9.8 0"/>
                    {
                        using var path = new GraphicsPath();
                        path.StartFigure();
                        path.AddLine(12f, 2f, 12f, 12f);
                        path.StartFigure();
                        path.AddBezier(7.1f, 5.2f, 3.756186f, 7.370395f, 2.238559f, 11.4819f, 3.370326f, 15.304309f);
                        path.AddBezier(3.370326f, 15.304309f, 4.502093f, 19.126718f, 8.013559f, 21.749172f, 12f, 21.749172f);
                        path.AddBezier(12f, 21.749172f, 15.986441f, 21.749172f, 19.497907f, 19.126718f, 20.629674f, 15.304309f);
                        path.AddBezier(20.629674f, 15.304309f, 21.761441f, 11.4819f, 20.243814f, 7.370395f, 16.9f, 5.2f);
                        g.DrawPath(p, path);
                    }
                    break;
                case V269Page.Configuracoes:
                    // settings: <path d="M10.4 2.3h3.2l.6 2.4a7.8 7.8 0 0 1 1.8.8l2.2-1.2 2.2 2.2-1.2 2.2c.35.6.62 1.2.8 1.8l2.4.6v3.2l-2.4.6a7.8 7.8 0 0 1-.8 1.8l1.2 2.2-2.2 2.2-2.2-1.2a7.8 7.8 0 0 1-1.8.8l-.6 2.4h-3.2l-.6-2.4a7.8 7.8 0 0 1-1.8-.8l-2.2 1.2-2.2-2.2 1.2-2.2a7.8 7.8 0 0 1-.8-1.8l-2.4-.6v-3.2l2.4-.6a7.8 7.8 0 0 1 .8-1.8L4.6 6.5l2.2-2.2L9 5.5a7.8 7.8 0 0 1 1.8-.8Z"/><circle cx="12" cy="12" r="3.1"/>
                    {
                        using var path = new GraphicsPath();
                        path.StartFigure();
                        path.AddLine(10.4f, 2.3f, 13.6f, 2.3f);
                        path.AddLine(13.6f, 2.3f, 14.2f, 4.7f);
                        path.AddBezier(14.2f, 4.7f, 14.831396f, 4.88953f, 15.436217f, 5.15834f, 16f, 5.5f);
                        path.AddLine(16f, 5.5f, 18.2f, 4.3f);
                        path.AddLine(18.2f, 4.3f, 20.4f, 6.5f);
                        path.AddLine(20.4f, 6.5f, 19.2f, 8.7f);
                        path.AddBezier(19.2f, 8.7f, 19.55f, 9.3f, 19.82f, 9.9f, 20f, 10.5f);
                        path.AddLine(20f, 10.5f, 22.4f, 11.1f);
                        path.AddLine(22.4f, 11.1f, 22.4f, 14.3f);
                        path.AddLine(22.4f, 14.3f, 20f, 14.9f);
                        path.AddBezier(20f, 14.9f, 19.81047f, 15.531396f, 19.54166f, 16.136217f, 19.2f, 16.7f);
                        path.AddLine(19.2f, 16.7f, 20.4f, 18.9f);
                        path.AddLine(20.4f, 18.9f, 18.2f, 21.1f);
                        path.AddLine(18.2f, 21.1f, 16f, 19.9f);
                        path.AddBezier(16f, 19.9f, 15.436217f, 20.24166f, 14.831396f, 20.51047f, 14.2f, 20.7f);
                        path.AddLine(14.2f, 20.7f, 13.6f, 23.1f);
                        path.AddLine(13.6f, 23.1f, 10.4f, 23.1f);
                        path.AddLine(10.4f, 23.1f, 9.8f, 20.7f);
                        path.AddBezier(9.8f, 20.7f, 9.168604f, 20.51047f, 8.563783f, 20.24166f, 8f, 19.9f);
                        path.AddLine(8f, 19.9f, 5.8f, 21.1f);
                        path.AddLine(5.8f, 21.1f, 3.6f, 18.9f);
                        path.AddLine(3.6f, 18.9f, 4.8f, 16.7f);
                        path.AddBezier(4.8f, 16.7f, 4.45834f, 16.136217f, 4.18953f, 15.531396f, 4f, 14.9f);
                        path.AddLine(4f, 14.9f, 1.6f, 14.3f);
                        path.AddLine(1.6f, 14.3f, 1.6f, 11.1f);
                        path.AddLine(1.6f, 11.1f, 4f, 10.5f);
                        path.AddBezier(4f, 10.5f, 4.18953f, 9.868604f, 4.45834f, 9.263783f, 4.8f, 8.7f);
                        path.AddLine(4.8f, 8.7f, 4.6f, 6.5f);
                        path.AddLine(4.6f, 6.5f, 6.8f, 4.3f);
                        path.AddLine(6.8f, 4.3f, 9f, 5.5f);
                        path.AddBezier(9f, 5.5f, 9.563783f, 5.15834f, 10.168604f, 4.88953f, 10.8f, 4.7f);
                        path.CloseFigure();
                        g.DrawPath(p, path);
                    }
                    g.DrawEllipse(p, 8.9f, 8.9f, 6.2f, 6.2f);
                    break;
            }
        }
        finally { g.Restore(state); }
    }
}
