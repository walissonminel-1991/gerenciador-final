using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal enum V269RepairGlyph
{
    Shield, Wrench, Wifi, Refresh, Audio, Printer, Usb, Chip, Monitor, Restore, Disk, Clock
}

internal static class V269RepairVisuals
{
    internal static Color WrenchInk(V269Theme theme) => V269AppTable.H(theme switch
    {
        V269Theme.Claro => "#ad690d", V269Theme.Dark => "#e7bd5f", _ => "#f2c477"
    });

    internal static void Draw(Graphics g, V269RepairGlyph glyph, Rectangle bounds, V269Theme theme)
    {
        V269DriverGlyph? driverGlyph = glyph switch
        {
            V269RepairGlyph.Shield => V269DriverGlyph.Shield,
            V269RepairGlyph.Wifi => V269DriverGlyph.WiFi,
            V269RepairGlyph.Audio => V269DriverGlyph.Audio,
            V269RepairGlyph.Usb => V269DriverGlyph.Usb,
            V269RepairGlyph.Chip => V269DriverGlyph.Chip,
            V269RepairGlyph.Monitor => V269DriverGlyph.Monitor,
            V269RepairGlyph.Disk => V269DriverGlyph.Storage,
            _ => null
        };
        if (driverGlyph is { } driver)
        {
            Color ink = glyph == V269RepairGlyph.Shield && theme == V269Theme.Dark
                ? V269AppTable.H("#82c874") : V269DriverVisuals.GlyphInk(driver, theme);
            V269DriverVisuals.DrawGlyph(g, bounds, driver, ink);
            return;
        }
        if (glyph == V269RepairGlyph.Wrench)
        {
            V269HomeHeadingIcon.DrawGlyph(g, bounds, V269Page.Reparo, WrenchInk(theme));
            return;
        }
        Color color = V269AppTable.H(theme switch
        {
            V269Theme.Claro => "#167bbb",
            V269Theme.Dark when glyph is V269RepairGlyph.Printer or V269RepairGlyph.Restore => "#c6cdd3",
            V269Theme.Dark => "#65b8ef", _ => "#82c6df"
        });
        var state = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(bounds.X, bounds.Y);
            g.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
            using var pen = new Pen(color, 2.05f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            switch (glyph)
            {
                case V269RepairGlyph.Refresh:
                    g.DrawArc(pen, 3, 3, 18, 18, 195, 145);
                    g.DrawArc(pen, 3, 3, 18, 18, 15, 145);
                    g.DrawLines(pen, new Point[] { new Point(3, 3), new(3, 9), new(9, 9) });
                    g.DrawLines(pen, new Point[] { new Point(21, 21), new(21, 15), new(15, 15) });
                    break;
                case V269RepairGlyph.Printer:
                    g.DrawRectangle(pen, 6, 2, 12, 6);
                    using (var body = V269Shell.Rounded(new Rectangle(2, 8, 20, 10), 2)) g.DrawPath(pen, body);
                    g.DrawRectangle(pen, 6, 14, 12, 8);
                    g.DrawLine(pen, 16, 11, 18, 11);
                    break;
                case V269RepairGlyph.Restore:
                    g.DrawArc(pen, 3, 3, 18, 18, 170, 335);
                    g.DrawLines(pen, new Point[] { new Point(3, 22), new(3, 15), new(10, 15) });
                    break;
                case V269RepairGlyph.Clock:
                    g.DrawEllipse(pen, 3, 3, 18, 18);
                    g.DrawLines(pen, new Point[] { new Point(12, 6), new(12, 12), new(16, 14) });
                    break;
            }
        }
        finally { g.Restore(state); }
    }
}

internal sealed partial class MainForm
{
    private sealed record V269RepairTool(string Title, string Description, string Category,
        string Command, V269RepairGlyph Glyph, Func<Task> Execute);
    private sealed record V269RepairItem(V269RepairTool Tool, V269RepairCard Card);
    private sealed record V269RepairOutcome(string Description, bool Complete);
    private readonly List<V269RepairItem> _v269RepairItems = new();
    private readonly List<V269AppPreviousResult> _v269RepairPreviousResults = new();
    private readonly Dictionary<V269RepairTool, V269RepairOutcome> _v269RepairEvidence = new();
    private V269RepairItem? _v269RepairSelected;
    private V269RepairTool? _v269RepairCommand;
    private V269RepairOutcome? _v269RepairOutcome;
    private V269PanelPresentation? _v269RepairRetained;
    private bool _v269RepairExecutionActive;
    private int? _v269RepairPercent;
    private CancellationTokenSource? _v269RepairWait;

    private Control CreateV269RepairPage()
    {
        // Os comandos permanecem ligados aos motores reais e às autorizações originais.
        V269RepairTool[] tools =
        [
            new("Verificar Windows", "Consulta a integridade da imagem e dos arquivos protegidos do Windows sem repará-los. Pode demorar e exige autorização administrativa.",
                "Análise", "Verificar", V269RepairGlyph.Shield,
                () => ExecuteAsync("Verificando integridade do Windows...", VerifyWindowsIntegrityAsync)),
            new("Reparar Windows", "Reparo condicionado ao resultado da verificação. A integridade será conferida novamente após a execução.",
                "Reparo orientado", "Reparar", V269RepairGlyph.Wrench, RepairWindowsAfterVerificationAsync),
            new("Analisar rede", "Consulta o adaptador, o gateway, a resolução DNS e conexões TCP na porta 443.",
                "Análise", "Analisar", V269RepairGlyph.Wifi, RunNetworkDiagnosticAsync),
            new("Reparar rede", "Oferece uma correção orientada pelas evidências do diagnóstico de rede, mediante confirmação.",
                "Reparo orientado", "Reparar", V269RepairGlyph.Refresh, RepairNetworkAfterDiagnosisAsync),
            new("Diagnosticar áudio", "Consulta as saídas de áudio ativas e a saída padrão para multimídia.",
                "Diagnóstico consultivo", "Analisar", V269RepairGlyph.Audio, RunAudioDiagnosticAsync),
            new("Diagnosticar impressora", "Consulta o estado da fila da impressora padrão do Windows.",
                "Diagnóstico consultivo", "Analisar", V269RepairGlyph.Printer, RunPrinterDiagnosticAsync),
            new("Diagnosticar USB", "Consulta presença e códigos PnP dos dispositivos USB.",
                "Diagnóstico consultivo", "Analisar", V269RepairGlyph.Usb, RunUsbDiagnosticAsync),
            new("Diagnosticar chipset", "Consulta o estado PnP dos controladores SMBus e da ponte PCI–ISA.",
                "Diagnóstico consultivo", "Analisar", V269RepairGlyph.Chip, RunChipsetDiagnosticAsync),
            new("Diagnosticar tela azul", "Consulta eventos de desligamento inesperado e códigos de parada registrados pelo Windows.",
                "Diagnóstico consultivo", "Analisar", V269RepairGlyph.Monitor, RunBlueScreenDiagnosticAsync),
            new("Restaurar configurações da rede", "Redefinição avançada das configurações da rede. A conexão pode cair e pode ser necessário reiniciar o Windows.",
                "Manutenção sensível", "Restaurar", V269RepairGlyph.Restore,
                () => ConfirmThenRun("network-reset", "Restaurar as configurações da conexão? A internet pode cair. Talvez seja necessário reiniciar o Windows.")),
            new("Criar ponto de restauração", "Solicita a criação de um ponto de restauração. A Proteção do Sistema deve estar habilitada.",
                "Manutenção", "Criar", V269RepairGlyph.Disk,
                () => ConfirmThenRun("create-restore-point", "Solicitar um ponto de restauração ao Windows? Requer autorização administrativa e Proteção do Sistema habilitada. Não substitui backup e o Windows pode reutilizar um ponto recente.")),
            new("Escolher ponto de restauração", "Abre o assistente oficial de restauração para escolher um ponto anterior.",
                "Ferramenta do Windows", "Abrir", V269RepairGlyph.Clock, ChooseRestorePointAsync)
        ];
        var host = new DarkScrollableFlow
        {
            Name = "RepairActions", Dock = DockStyle.Fill, Margin = Padding.Empty,
            EmbeddedInPage = true, BackColor = AppServices.Background
        };
        host.ApplyCleanupSurface(_v269Appearance.Theme);
        var flow = host.Content;
        flow.Padding = Padding.Empty;
        foreach (var tool in tools)
        {
            var card = new V269RepairCard(tool.Title, tool.Description, _v269RepairItems.Count, tool.Command, tool.Glyph);
            var item = new V269RepairItem(tool, card);
            _v269RepairItems.Add(item);
            _actionControls.Add(card.Command);
            if (tool.Glyph == V269RepairGlyph.Wrench) _repairWindowsButton = card.Command;
            if (tool.Glyph == V269RepairGlyph.Refresh) _repairNetworkButton = card.Command;
            card.Enter += (_, _) => SelectV269RepairItem(item, false);
            card.Click += (_, _) => SelectV269RepairItem(item, true);
            card.Command.Enter += (_, _) => SelectV269RepairItem(item, false);
            card.Command.Click += async (_, _) => await RunV269RepairToolAsync(item);
            flow.Controls.Add(card);
        }
        _v269RepairSelected = _v269RepairItems[0];
        _v269RepairSelected.Card.SetSelected(true);
        RefreshV269RepairCommands();
        bool fitting = false;
        (int Width, int Gap, int Columns, int Dpi, V269AppearanceOptions Options, bool Compact)? fitted = null;
        void Fit()
        {
            if (fitting || host.IsDisposed || host.ClientSize.Width <= 0) return;
            fitting = true;
            try
            {
                int S(int n) => V269AppTable.Scale(n, _v269Appearance.InterfacePercent, host.DeviceDpi);
                int gap = S(_nav.LayoutWindowWidth <= 1040 ? 6 : 9);
                int width = Math.Max(1, host.ClientSize.Width - Math.Max(4, S(4)));
                int columns = _nav.LayoutWindowWidth <= 850 || width < S(540) ? 2 : 3;
                if (width < S(330)) columns = 1;
                var key = (width, gap, columns, host.DeviceDpi, _v269Appearance.Normalize(), _nav.LayoutWindowWidth <= 1250);
                if (fitted == key) return; // alteração apenas de altura não invalida os cartões.
                int cardWidth = Math.Max(1, (width - gap * (columns - 1)) / columns);
                flow.SuspendLayout();
                try
                {
                    for (int row = 0; row < _v269RepairItems.Count; row += columns)
                    {
                        var cards = _v269RepairItems.Skip(row).Take(columns).Select(item => item.Card).ToArray();
                        foreach (var card in cards)
                        {
                            card.ApplyOptions(key.Item5);
                            card.ApplyToolLayout(key.Item6);
                        }
                        int height = cards.Max(card => card.HeightForWidth(cardWidth));
                        for (int column = 0; column < cards.Length; column++)
                        {
                            var card = cards[column];
                            var margin = new Padding(0, 0, column == columns - 1 ? 0 : gap, gap);
                            var size = new Size(cardWidth, height);
                            if (card.Margin != margin) card.Margin = margin;
                            if (card.Size != size) card.Size = size;
                        }
                    }
                }
                finally { flow.ResumeLayout(false); }
                host.Reflow();
                fitted = key;
            }
            finally { fitting = false; }
        }
        host.Tag = (Action)Fit;
        host.SizeChanged += (_, _) => Fit();
        host.DpiChangedAfterParent += (_, _) => Fit();
        Fit();
        return ScrollablePage(host, width => host.MeasureContentHeight(width), trackRepair: true);
    }

    private void RefreshV269RepairCommands()
    {
        bool idle = !_busy && !_v269Confirming && !_betaClose.IsClosing;
        foreach (var item in _v269RepairItems)
        {
            bool eligible = item.Tool.Glyph switch
            {
                V269RepairGlyph.Wrench => _windowsRepairable && DateTimeOffset.UtcNow - _windowsScannedAt < TimeSpan.FromMinutes(30),
                V269RepairGlyph.Refresh => GetSuggestedNetworkRepair(_lastNetworkScan) is not null,
                _ => true
            };
            item.Card.Command.Enabled = idle && eligible;
        }
    }

    private void SelectV269RepairItem(V269RepairItem item, bool explicitRequest)
    {
        if (_v269RepairSelected != item)
        {
            _v269RepairSelected?.Card.SetSelected(false);
            _v269RepairSelected = item;
            item.Card.SetSelected(true);
        }
        if (explicitRequest && _nav.SelectedPage == V269Page.Reparo && !_busy && !_v269Confirming)
        {
            _v269RepairRetained = null;
            if (_nav.RightPane.Current?.Kind is V269PanelKind.Result or V269PanelKind.History)
                _nav.PresentPanel(null);
            _nav.EnsureV269RepairPane();
        }
        PresentV269RepairSelection();
    }

    private void PresentV269RepairSelection()
    {
        if (_busy || _v269Confirming || _nav.SelectedPage != V269Page.Reparo ||
            _v269RepairSelected is not { } selected ||
            _nav.RightPane.Current?.Kind is V269PanelKind.Progress or V269PanelKind.Confirmation or V269PanelKind.Result or V269PanelKind.History)
            return;
        var tool = selected.Tool;
        _v269RepairEvidence.TryGetValue(tool, out var evidence);
        string state = evidence is null ? "Aguardando execução" : evidence.Complete ? "Concluído" : "Requer atenção";
        if (tool.Glyph is V269RepairGlyph.Wrench or V269RepairGlyph.Refresh && !selected.Card.Command.Enabled)
            state = "Verificação necessária";
        // Enter do cartão/comando e NavigationChanged podem solicitar o mesmo
        // detalhe consecutivamente. Não reconstruir a árvore do painel sem mudança.
        if (_nav.RightPane.Current is { Kind: V269PanelKind.Details, Repair: { } current } &&
            current.Name == tool.Title && current.RepairGlyph == tool.Glyph &&
            current.Fields.SequenceEqual(new[] { ("Estado", state) }) &&
            current.RepairReport == evidence?.Description &&
            (current.PreviousResults ?? []).SequenceEqual(_v269RepairPreviousResults))
            return;
        var model = new V269AppDetailModel(tool.Title, tool.Category, [("Estado", state)],
            "Reparos orientados exigem uma verificação anterior. Operações sensíveis pedem confirmação e podem alterar o Windows.",
            Description: tool.Description, PreviousResults: _v269RepairPreviousResults.ToArray(),
            DismissResult: DismissV269RepairResult, RepairGlyph: tool.Glyph, RepairReport: evidence?.Description);
        _nav.PresentPanel(new(V269PanelKind.Details, "Detalhes da ferramenta", "", Repair: model));
    }

    private void PresentV269RepairRetained()
    {
        RefreshV269RepairCommands();
        if (!_busy && !_v269Confirming && _v269RepairRetained is { } result &&
            _nav.RightPane.Current?.Kind is not (V269PanelKind.Progress or V269PanelKind.Confirmation or V269PanelKind.Result or V269PanelKind.History))
            _nav.PresentPanel(result);
        else PresentV269RepairSelection();
    }

    private async Task RunV269RepairToolAsync(V269RepairItem item)
    {
        if (_busy || _v269Confirming || _betaClose.IsClosing ||
            _v269RepairCommand is not null || !item.Card.Command.Enabled) return;
        SelectV269RepairItem(item, true);
        _v269RepairCommand = item.Tool;
        try { await item.Tool.Execute(); }
        catch (Exception ex) { if (!IsDisposed && !Disposing && !_betaClose.IsShutdown) ReportError(ex); }
        finally
        {
            _v269RepairCommand = null;
            if (!IsDisposed && !Disposing)
            {
                RefreshV269RepairCommands();
                if (_nav.SelectedPage != V269Page.Reparo && _nav.RightPane.Current?.Repair is not null)
                    _nav.PresentPanel(null);
            }
        }
    }

    private void SetV269RepairOutcome(string description, bool complete)
    {
        if (_v269RepairCommand is not { } tool || IsDisposed || Disposing) return;
        if (_v269RepairExecutionActive) _v269RepairOutcome = new(description, complete);
        else RecordV269RepairResult(tool, description, complete);
    }

    private void RecordV269RepairResult(V269RepairTool tool, string description, bool complete)
    {
        if (IsDisposed || Disposing) return;
        LocalDiagnosticReport.Session.RecordOperation(V269Page.Reparo,"resultado",complete);
        _v269RepairEvidence[tool] = new(description, complete);
        var entry = new V269AppPreviousResult(Guid.NewGuid(), tool.Title, description, complete);
        _v269RepairPreviousResults.Insert(0, entry);
        if (_v269RepairPreviousResults.Count > 4) _v269RepairPreviousResults.RemoveAt(4);
        var presentation = new V269PanelPresentation(V269PanelKind.Result, tool.Title, description,
            FlowResult: new(V269Page.Reparo, complete, () => DismissV269RepairResult(entry.Id), entry.Id));
        _v269RepairRetained = presentation;
        if (_nav.SelectedPage == V269Page.Reparo) _nav.PresentPanel(presentation);
        _nav.RightPane.RefreshAppOperationHistory();
    }

    private void DismissV269RepairResult(Guid id)
    {
        if (IsDisposed || Disposing || _nav.SelectedPage != V269Page.Reparo ||
            _v269RepairPreviousResults.RemoveAll(entry => entry.Id == id) == 0) return;
        if (_v269RepairRetained?.FlowResult?.HistoryId == id) _v269RepairRetained = null;
        if (_nav.RightPane.Current?.FlowResult is { Page: V269Page.Reparo, HistoryId: var current } && current == id)
            _nav.PresentPanel(null);
        _nav.RightPane.RefreshAppOperationHistory();
        PresentV269RepairSelection();
    }

    private void PresentV269RepairProgress(string message)
    {
        if (_v269RepairCommand is not { } tool || IsDisposed || Disposing) return;
        var wait = _v269RepairWait;
        Action? cancel = wait is null ? null : () =>
        {
            if (ReferenceEquals(_v269RepairWait, wait) && !wait.IsCancellationRequested) wait.Cancel();
        };
        _nav.PresentPanel(new(V269PanelKind.Progress, tool.Title, message, _v269RepairPercent,
            CancelFlow: cancel, FlowPage: V269Page.Reparo, CancelLabel: "Cancelar espera"));
    }

    private async Task ExecuteV269RepairQueryAsync(string description, Func<CancellationToken, Task> query)
    {
        if (_busy) return;
        using var wait = new CancellationTokenSource();
        _v269RepairWait = wait;
        try
        {
            await ExecuteAsync(description, async () =>
            {
                try { await query(wait.Token); }
                catch (OperationCanceledException) when (wait.IsCancellationRequested)
                {
                    const string cancelled = "Espera cancelada. Uma consulta já enviada ao Windows pode terminar em segundo plano.";
                    Status(cancelled);
                    SetV269RepairOutcome(cancelled, false);
                    Log(cancelled);
                }
            });
        }
        finally { _v269RepairWait = null; }
    }
}
