using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Globalization;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private readonly Label _systemPanelTitle = new V269ModuleHeading("Windows Update","refresh");
    private readonly Label _systemPanelSummary = new();
    private DarkScrollableFlow? _systemPanelCards;
    private Control? _v269EnergyView;
    private TableLayoutPanel? _systemDetails;
    private TableLayoutPanel? _hardwareGrid;
    private Button? _exportHardwareButton;
    private IReadOnlyList<HardwareGroup> _hardwareGroups = Array.Empty<HardwareGroup>();
    private sealed record HardwareGroup(string Title, string Content);
    private readonly HashSet<string> _updateSelection = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<WinUpdateChoice> _updateCatalog = Array.Empty<WinUpdateChoice>();
    private readonly System.Windows.Forms.Timer _memoryTimer = new() { Interval = ProcessSamplingPolicy.BackgroundIntervalMs };
    private bool _memorySampling;
    private bool _windowsUpdateScanning;
    private bool _windowsUpdateChecked;
    private string? _windowsUpdateScanFailure;
    private bool _windowsUpdateInstalling;
    private bool? _windowsUpdateRebootRequired;
    private WindowsUpdateOperation? _lastWindowsUpdateOperation;
    private WinUpdateChoice[] _lastWindowsUpdateSelection = [];
    private Button? _searchWindowsUpdatesButton;
    private Button? _windowsUpdateHistoryButton;
    private bool _windowsUpdateHistoryReading;
    private string _activeSystemView = "update";
    private readonly Dictionary<V269Page,List<V269AppPreviousResult>> _v269ModuleHistory=new();
    private readonly Dictionary<V269Page,V269PanelPresentation> _v269ModuleRetained=new();
    private bool _fittingSystemCards;
    private long _systemViewGeneration;

    private TableLayoutPanel BuildIntegratedSystemPanel()
    {
        var root = new TableLayoutPanel
        {
            Name = "V269SystemModules", Dock = DockStyle.Fill, BackColor = Color.Transparent,
            ColumnCount = 1, RowCount = 4, Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var updates = new V269CleanupSection
        {
            Name = "V269WindowsUpdateSection", Dock = DockStyle.Fill, BackColor = Color.Transparent,
            ColumnCount = 1, RowCount = 3, Padding = new Padding(12), Margin = new Padding(0,0,0,7)
        };
        _systemDetails = updates;
        updates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        updates.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        updates.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
        updates.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, BackColor = Color.Transparent };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,174));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,98));
        _systemPanelTitle.Text = "Windows Update";
        _systemPanelTitle.Dock = DockStyle.Fill;
        _systemPanelTitle.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
        _systemPanelTitle.ForeColor = Ink;
        _systemPanelTitle.TextAlign = ContentAlignment.MiddleLeft;
        _systemPanelTitle.Click += (_,_) => SetSystemView("update");
        header.Controls.Add(_systemPanelTitle,0,0);
        _searchWindowsUpdatesButton = new V269CleanupButton("Verificar atualizações", "refresh", true)
        { Name = "WindowsUpdateExplicitSearch", Dock = DockStyle.Fill, Margin = new Padding(3,1,3,1),
            AccessibleName = "Verificar atualizações do Windows", AccessibleDescription = "Consulta oficial somente sob comando." };
        _searchWindowsUpdatesButton.Click += async (_,_) => { SetSystemView("update"); await ScanWindowsUpdatesAsync(); };
        _actionControls.Add(_searchWindowsUpdatesButton);
        header.Controls.Add(_searchWindowsUpdatesButton,1,0);
        _windowsUpdateHistoryButton = new V269CleanupButton("Histórico","clock")
        { Name = "V269WindowsUpdateHistory", Dock = DockStyle.Fill, Margin = new Padding(3,1,0,1),
            AccessibleName = "Histórico do Windows Update" };
        _windowsUpdateHistoryButton.Click += async (_,_) => { SetSystemView("update"); await ShowWindowsUpdateHistoryAsync(); };
        _actionControls.Add(_windowsUpdateHistoryButton);
        header.Controls.Add(_windowsUpdateHistoryButton,2,0);
        updates.Controls.Add(header,0,0);
        int lastColumns=3;
        void FitHeader()
        {
            int S(int n)=>V269AppTable.Scale(n,_v269Appearance.InterfacePercent,updates.DeviceDpi);
            int width=Math.Max(1,updates.Width-updates.Padding.Horizontal);
            int columns=width>=S(475)?3:width>=S(270)?2:1;
            if(lastColumns!=columns)
            {
                lastColumns=columns;header.SuspendLayout();
                try
                {
                    header.SetColumnSpan(_systemPanelTitle,1);
                    header.ColumnCount=columns;header.RowCount=columns==3?1:columns==2?2:3;
                    header.ColumnStyles.Clear();header.RowStyles.Clear();
                    if(columns==3)
                    {
                        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,S(174)));
                        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,S(98)));
                        header.SetCellPosition(_searchWindowsUpdatesButton,new TableLayoutPanelCellPosition(1,0));
                        header.SetCellPosition(_windowsUpdateHistoryButton,new TableLayoutPanelCellPosition(2,0));
                    }
                    else
                    {
                        for(int i=0;i<columns;i++)header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/columns));
                        header.SetColumnSpan(_systemPanelTitle,columns);
                        header.SetCellPosition(_searchWindowsUpdatesButton,new TableLayoutPanelCellPosition(0,1));
                        header.SetCellPosition(_windowsUpdateHistoryButton,new TableLayoutPanelCellPosition(columns==2?1:0,columns==2?1:2));
                    }
                    for(int i=0;i<header.RowCount;i++)header.RowStyles.Add(new RowStyle(SizeType.Percent,100f/header.RowCount));
                }
                finally{header.ResumeLayout(true);}
            }
            updates.RowStyles[0].Height=header.RowCount*Math.Max(S(34),_systemPanelTitle.Font.Height+S(10));
        }
        updates.Tag=(Action)FitHeader;updates.SizeChanged+=(_,_)=>FitHeader();
        _systemPanelSummary.Text = "Consulta sob comando do usuário · aguardando verificação.";
        _systemPanelSummary.Dock = DockStyle.Fill;
        _systemPanelSummary.ForeColor = Muted;
        _systemPanelSummary.TextAlign = ContentAlignment.MiddleLeft;
        updates.Controls.Add(_systemPanelSummary,0,1);
        _systemPanelCards = new DarkScrollableFlow
        {
            Name = "V269SystemUpdateList", Dock = DockStyle.Fill, BackColor = Color.Transparent,
            Margin = Padding.Empty, EmbeddedInPage = true
        };
        _systemPanelCards.ApplyCleanupSurface(_v269Appearance.Theme);
        _systemPanelCards.Content.Padding = Padding.Empty;
        _systemPanelCards.Content.WrapContents = false;
        _systemPanelCards.Content.FlowDirection=FlowDirection.TopDown;
        _systemPanelCards.Content.SizeChanged += (_,_) => ResizeIntegratedCards();
        updates.Controls.Add(_systemPanelCards,0,2);
        root.Controls.Add(updates,0,0);
        _v269EnergyView = BuildEnergyPanel();
        _v269EnergyView.Name = "V269EnergySection";
        root.Controls.Add(_v269EnergyView,0,1);
        root.Controls.Add(BuildMemoryRamDashboard(),0,2);
        _memoryTimer.Tick += async (_,_) =>
        {
            if(IsDisposed)return;
            LocalDiagnosticReport.Session.ObserveUiCadence(_memoryTimer.Interval);
            if (_memorySampling) return;
            UpdateProcessSamplingCadence();
            LocalDiagnosticReport.Session.SampleProcess(IsInTray || !Visible,
                _busy || _v269Confirming || _batchUpdatesActive || _windowsUpdateInstalling ||
                _windowsUpdateScanning || _v269LiveSampling || _consultaCatalogoCts is not null);
            await RefreshMemoryRamAsync();
        };
        FormClosed += (_,_) => _memoryTimer.Dispose();
        return root;
    }

    private int SystemPageMinimumHeight(TableLayoutPanel root)
    {
        int S(int n) => V269AppTable.Scale(n,_v269Appearance.InterfacePercent,DeviceDpi);
        int width = Math.Max(1, root.ClientSize.Width);
        if (_systemDetails is { } updates)
        {
            int body = Math.Max(1,width-updates.Padding.Horizontal);
            if (_systemPanelCards is { } cards)
            {
                if(cards.Width!=body) cards.Width=body;
                ResizeIntegratedCards();
            }
            int summary = TextRenderer.MeasureText(_systemPanelSummary.Text,_systemPanelSummary.Font,
                new Size(body,600),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+S(8);
            if(updates.Tag is Action fitHeader)fitHeader();
            updates.RowStyles[1].Height = Math.Max(S(35),summary);
            int rows = _systemPanelCards?.MeasureContentHeight(body) ?? 0;
            root.RowStyles[0].Height = updates.RowStyles[0].Height+updates.RowStyles[1].Height+
                Math.Max(S(12),rows)+updates.Padding.Vertical+updates.Margin.Vertical;
        }
        if (_v269EnergyView is TableLayoutPanel energy)
        {
            if(energy.Tag is Action fitEnergy)fitEnergy();
            energy.PerformLayout();
            root.RowStyles[1].Height = Math.Max(S(190),
                energy.RowStyles.Cast<RowStyle>().Where(x=>x.SizeType==SizeType.Absolute).Sum(x=>x.Height)+energy.Padding.Vertical+energy.Margin.Vertical);
        }
        if (_memoryDashboardView is TableLayoutPanel ram)
        {
            if(ram.Tag is Action fitRam)fitRam();
            ram.PerformLayout();
            root.RowStyles[2].Height = Math.Max(S(224),
                ram.RowStyles.Cast<RowStyle>().Where(x=>x.SizeType==SizeType.Absolute).Sum(x=>x.Height)+ram.Padding.Vertical+ram.Margin.Vertical);
        }
        return (int)Math.Ceiling(root.RowStyles.Cast<RowStyle>().Where(x=>x.SizeType==SizeType.Absolute).Sum(x=>x.Height));
    }

    private void SetSystemView(string view)
    {
        if (_activeSystemView != view)
        {
            ++_systemViewGeneration;
            if(_nav.SelectedPage==V269Page.Sistema&&!_busy&&!_v269Confirming&&_nav.RightPane.Current?.Kind is V269PanelKind.Result or V269PanelKind.History)
                _nav.PresentPanel(null);
        }
        _activeSystemView = view;
        UpdateProcessSamplingCadence();
        if(_nav.SelectedPage==V269Page.Sistema) PresentV269SystemDetails();
    }

    private void PresentV269SystemDetails()
    {
        if (_nav.SelectedPage!=V269Page.Sistema || _busy || _v269Confirming ||
            _nav.RightPane.Current?.Kind is V269PanelKind.Confirmation or V269PanelKind.Progress or V269PanelKind.Result or V269PanelKind.History)
            return;
        string title, sub, description, state, glyph;
        switch(_activeSystemView)
        {
            case "energy":
                title="Planos de Energia"; sub="Perfis do Windows"; glyph="bolt";
                description="Permite alternar perfis de energia, consultar a hibernação e restaurar o plano anterior da sessão.";
                state=_currentPowerLabel.Text; break;
            case "memory":
                title="Memória RAM"; sub="Gerenciamento de memória"; glyph="memory";
                description="Acompanha o uso da memória física. Aliviar RAM exige autorização para o aplicativo selecionado; não fecha programas.";
                state=_memoryUsageLabel.Text; break;
            default:
                title="Windows Update"; sub="Atualizações do Windows"; glyph="refresh";
                description="Consulta ofertas oficiais de software sob comando e permite instalação individual mediante confirmação.";
                state=_windowsUpdateScanning?"Consultando":_windowsUpdateScanFailure is not null?"Consulta inconclusiva":_windowsUpdateChecked?"Consultado":"Aguardando consulta";
                break;
        }
        var model=new V269AppDetailModel(title,sub,[("Estado",state)],
            "Operações sensíveis exigem confirmação. O Windows não é reiniciado automaticamente.",
            Description:description,CleanupGlyph:glyph,CleanupDescriptionHeading:"O que faz",
            PreviousResults:V269ModuleHistory(V269Page.Sistema),DismissResult:DismissV269ModuleResult);
        if(_nav.RightPane.Current?.Module is {} old&&old.Name==model.Name&&old.Fields.SequenceEqual(model.Fields)&&
            (old.PreviousResults??[]).SequenceEqual(model.PreviousResults??[]))return;
        _nav.PresentPanel(new(V269PanelKind.Details,"Detalhes do módulo","",FlowPage:V269Page.Sistema,Module:model));
    }

    private bool IsCurrentSystemView(string view, long request) =>
        !IsDisposed && !Disposing && _systemViewGeneration == request &&
        (view == "startup" ? _nav.SelectedPage == V269Page.Inicializacao :
            _nav.SelectedPage == V269Page.Sistema && _activeSystemView == view);

    private void AddIntegratedAction(TableLayoutPanel parent, int column, string caption, string description,
        Color color, Func<Task> action)
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = PanelBackground,
            ColumnCount = 1, RowCount = 2, Margin = new Padding(2, 1, 3, 2),
            Padding = new Padding(4, 2, 4, 2)
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var button = CreateButton(caption, color, Color.White);
        button.AccessibleDescription = description;
        button.Dock = DockStyle.Fill;
        button.Margin = Padding.Empty;
        _actionControls.Add(button);
        _toolTips.SetToolTip(button, description);
        button.Click += async (_, _) =>
        {
            if (_busy) return;
            SetSystemView(caption switch
            {
                "Memória RAM" => "memory", "Energia" => "energy", "Windows Update" => "update", _ => "hardware"
            });
            try { await action(); } catch (Exception ex) { ReportError(ex); }
        };
        card.Controls.Add(button, 0, 0);
        card.Controls.Add(new Label
        {
            Name = "SystemActionDescription" + column,
            Text = description,
            Dock = DockStyle.Fill, ForeColor = Muted,
            AutoEllipsis = false,
            Font = new Font("Segoe UI", 9F),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(1, 2, 1, 0),
            AccessibleDescription = description
        }, 0, 1);
        parent.Controls.Add(card, column, 0);
    }

    private void FitSystemActionsHeight(TableLayoutPanel root, TableLayoutPanel actions)
    {
        if (root.IsDisposed || actions.IsDisposed || root.ClientSize.Width < 1) return;
        double dpi = Math.Max(1d, DeviceDpi / 96d);
        int available = root.ClientSize.Width - root.Padding.Horizontal;
        int columns = available < Math.Ceiling(480 * dpi) ? 2 : 4;
        var cards = actions.Controls.OfType<TableLayoutPanel>().ToArray();
        int rows = (cards.Length + columns - 1) / columns;
        if (actions.ColumnCount != columns)
        {
            actions.SuspendLayout();
            try
            {
                actions.ColumnCount = columns;
                actions.RowCount = rows;
                actions.ColumnStyles.Clear();
                actions.RowStyles.Clear();
                for (int column = 0; column < columns; column++)
                    actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
                for (int row = 0; row < rows; row++)
                    actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
                for (int i = 0; i < cards.Length; i++)
                    actions.SetCellPosition(cards[i], new TableLayoutPanelCellPosition(i % columns, i / columns));
            }
            finally { actions.ResumeLayout(true); }
        }
        int width = Math.Max(24, available / columns - 23);
        int minHeight = (int)Math.Ceiling(82d * dpi);
        int height = minHeight;
        foreach (var card in cards)
        {
            var label = card.Controls.OfType<Label>().FirstOrDefault();
            if (label is null) continue;
            int measured = TextRenderer.MeasureText(label.Text, label.Font,
                new Size(width, 800),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
            int required = (int)card.RowStyles[0].Height + measured +
                card.Margin.Vertical + card.Padding.Vertical + label.Padding.Vertical + 3;
            height = Math.Max(height, required);
        }
        int totalHeight = height * rows;
        if (Math.Abs(root.RowStyles[0].Height - totalHeight) >= 1)
            root.RowStyles[0].Height = totalHeight;
    }

    private void FitSystemSummaryHeight() => _relayoutSystem?.Invoke();

    private void ReplaceSystemCards(IEnumerable<Control> cards, bool startup = false)
    {
        var host = startup ? _v269StartupCards : _systemPanelCards;
        if (host is null || IsDisposed) return;
        var flow = host.Content;
        var oldSelection=startup?_v269StartupSelected:null;
        var oldItem=oldSelection?.Item;
        flow.SuspendLayout();
        try
        {
            var old = flow.Controls.Cast<Control>().ToArray();
            flow.Controls.Clear();
            foreach (Control control in old) control.Dispose();
            foreach (Control card in cards) flow.Controls.Add(card);
        }
        finally { flow.ResumeLayout(true); }
        if (startup)
        {
            _v269StartupTitle.Visible=flow.Controls.Count==0;
            _v269StartupSelected=flow.Controls.OfType<V269StartupCard>().FirstOrDefault(x=>
                oldSelection is not null && (oldItem is null?x.Item is null:Equals(x.Item,oldItem)))??
                flow.Controls.OfType<V269StartupCard>().FirstOrDefault();
            _v269StartupSelected?.SetSelected(true);
            ResizeV269StartupCards(); PresentV269StartupDetails();
        }
        else { ResizeIntegratedCards(); _relayoutSystem?.Invoke(); }
    }

    private static IReadOnlyList<HardwareGroup> GroupHardware(SystemInfoSnapshot snapshot)
    {
        var items = snapshot.Entries;
        string Get(string key) => items.FirstOrDefault(e => e.Title.Equals(key, StringComparison.OrdinalIgnoreCase))?.Description ?? "Não informado";
        IEnumerable<SystemInfoEntry> Starting(string prefix) => items.Where(e => e.Title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        string Join(IEnumerable<string> values) => string.Join("\n", values.Where(x => !string.IsNullOrWhiteSpace(x)));
        var cpu = Starting("CPU ·").ToArray();
        var gpu = Starting("GPU ·").ToArray();
        var memory = Starting("RAM ·").ToArray();
        var disks = Starting("Disco ·").ToArray();
        return new[]
        {
            new HardwareGroup("SISTEMA", Join(new[]
            {
                Get("Windows"), "Modo: " + Get("Modo de inicialização atual"),
                "Ligado há: " + Get("Tempo ligado desde a última inicialização"),
                "Computador: " + Get("Nome do computador")
            })),
            new HardwareGroup("PROCESSADOR", cpu.Length > 0
                ? Join(cpu.Select(e => e.Title[6..] + "\n" + e.Description))
                : Join(new[] { Get("Processador"), Get("Arquitetura") })),
            new HardwareGroup("PLACA-MÃE E BIOS", Join(new[]
            {
                Get("Placa-mãe"), "BIOS: " + Get("BIOS / UEFI")
            })),
            new HardwareGroup("PLACA DE VÍDEO", gpu.Length > 0
                ? Join(gpu.Select(e => e.Title[6..] + "\n" + e.Description))
                : "GPU não informada pelo Windows"),
            new HardwareGroup("MEMÓRIA RAM", Join(new[]
            {
                "Total: " + Get("Memória física instalada (aprox.)")
            }.Concat(memory.Select(e => e.Title[6..] + ": " + e.Description)))),
            new HardwareGroup("ARMAZENAMENTO", disks.Length > 0
                ? Join(disks.Select(e => e.Title[8..] + "\n" + e.Description))
                : "Unidades físicas não informadas pelo Windows")
        };
    }

    private static string CompactDetails(HardwareGroup group)
    {
        // A tela prioriza legibilidade; o PNG contém o texto integral.
        string value = group.Content.Replace("\r", "").Replace(" | ", "  ·  ");
        if (group.Title == "MEMÓRIA RAM" || group.Title == "ARMAZENAMENTO")
        {
            var lines = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 3) value = string.Join("\n", lines.Take(3)) + $"\n+{lines.Length - 3} informações na ficha PNG";
        }
        return value;
    }

    private Control HardwareTile(HardwareGroup group)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = new Padding(3, 3, 8, 7), Padding = new Padding(11, 6, 11, 6),
            BackColor = Color.FromArgb(38, 50, 63)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var name = new Label
        {
            Text = group.Title, Dock = DockStyle.Fill, ForeColor = Accent,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var value = new Label
        {
            Text = CompactDetails(group), Dock = DockStyle.Fill, ForeColor = Ink,
            Font = new Font("Segoe UI", 8.2F), AutoEllipsis = true,
            TextAlign = ContentAlignment.TopLeft
        };
        _toolTips.SetToolTip(name, group.Title);
        _toolTips.SetToolTip(value, group.Content);
        root.Controls.Add(name, 0, 0);
        root.Controls.Add(value, 0, 1);
        return root;
    }

    private void ShowHardwareCompact(SystemInfoSnapshot snapshot)
    {
        _hardwareGroups = GroupHardware(snapshot);
        _systemPanelTitle.Text = "Este computador · resumo da configuração";
        _systemPanelSummary.Text = "Seis grupos sem dados duplicados. Salve a ficha PNG para consultar os detalhes completos.";
        if (_hardwareGrid is null) return;
        _hardwareGrid.SuspendLayout();
        try
        {
            foreach (Control card in _hardwareGrid.Controls.Cast<Control>().ToArray()) card.Dispose();
            _hardwareGrid.Controls.Clear();
            for (int i = 0; i < _hardwareGroups.Count; i++)
                _hardwareGrid.Controls.Add(HardwareTile(_hardwareGroups[i]), i % 2, i / 2);
        }
        finally { _hardwareGrid.ResumeLayout(true); }
        if (_exportHardwareButton is not null) _exportHardwareButton.Visible = true;
        _hardwareGrid.Visible = true;
        _hardwareGrid.BringToFront();
        if (_systemPanelCards is not null) _systemPanelCards.Visible = false;
    }

    private void ExportHardwareImage()
    {
        if (_hardwareGroups.Count == 0) return;
        using var picker = new SaveFileDialog
        {
            Title = "Salvar ficha do computador como imagem",
            Filter = "Imagem PNG (*.png)|*.png",
            DefaultExt = "png", AddExtension = true,
            FileName = "Configuracao-PC-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".png"
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            const int width = 1500;
            const int outer = 44;
            const int gap = 18;
            const int cardWidth = (width - outer * 2 - gap) / 2;
            using var titleFont = new Font("Segoe UI", 26F, FontStyle.Bold);
            using var headingFont = new Font("Segoe UI", 14F, FontStyle.Bold);
            using var bodyFont = new Font("Segoe UI", 11F);
            using var smallFont = new Font("Segoe UI", 10F);
            using var measuring = new Bitmap(1, 1);
            using var measureGraphics = Graphics.FromImage(measuring);
            var groupHeights = new int[_hardwareGroups.Count];
            for (int i = 0; i < _hardwareGroups.Count; i++)
            {
                float h = measureGraphics.MeasureString(_hardwareGroups[i].Content, bodyFont, cardWidth - 42).Height;
                groupHeights[i] = Math.Max(132, (int)Math.Ceiling(h) + 82);
            }
            int[] rows = new int[3];
            for (int row = 0; row < 3; row++) rows[row] = Math.Max(groupHeights[row * 2], groupHeights[row * 2 + 1]);
            int height = outer + 104 + rows.Sum() + 2 * gap + 94;
            using var image = new Bitmap(width, height);
            using var g = Graphics.FromImage(image);
            g.Clear(Color.FromArgb(21, 27, 35));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using var white = new SolidBrush(Color.FromArgb(237, 243, 250));
            using var accent = new SolidBrush(Color.FromArgb(111, 184, 255));
            using var muted = new SolidBrush(Color.FromArgb(167, 181, 196));
            using var cardBrush = new SolidBrush(Color.FromArgb(38, 50, 63));
            g.DrawString("CONFIGURAÇÃO DO COMPUTADOR", titleFont, white, outer, outer - 8);
            g.DrawString("Relatório local · " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
                smallFont, muted, outer, outer + 43);
            int y = outer + 104;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    int index = row * 2 + col;
                    HardwareGroup group = _hardwareGroups[index];
                    int x = outer + col * (cardWidth + gap);
                    var card = new Rectangle(x, y, cardWidth, rows[row]);
                    using var path = ModernButton.RoundRect(card, 13);
                    g.FillPath(cardBrush, path);
                    g.DrawString(group.Title, headingFont, accent, x + 20, y + 14);
                    g.DrawString(group.Content, bodyFont, white,
                        new RectangleF(x + 20, y + 52, cardWidth - 40, rows[row] - 60));
                }
                y += rows[row] + gap;
            }
            g.DrawString("Fonte: Windows/WMI. Valores estimados podem variar; sensores e VRAM podem estar indisponíveis.",
                smallFont, muted, outer, height - 60);
            image.Save(picker.FileName, ImageFormat.Png);
            Status("Ficha de hardware salva em PNG");
            Log("Ficha de hardware exportada para: " + Path.GetFileName(picker.FileName));
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void ShowIntegratedSnapshot(SystemInfoSnapshot snapshot)
    {
        if (_systemPanelCards is null || IsDisposed) return;
        _systemPanelTitle.Text = snapshot.Heading;
        _systemPanelSummary.Text = snapshot.Summary;
        ReplaceSystemCards(snapshot.Entries.Select(BuildInfoCard).ToArray());
    }

    private Control BuildInfoCard(SystemInfoEntry entry)
    {
        var panel = new TableLayoutPanel
        {
            Width = 390, Height = 104, RowCount = 2, ColumnCount = 1,
            BackColor = Color.FromArgb(38, 50, 63), Margin = new Padding(2, 2, 7, 5),
            Padding = new Padding(9, 5, 9, 4)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var title = new Label
        {
            Text = entry.Title, Dock = DockStyle.Fill,
            ForeColor = Ink, Font = new Font("Segoe UI", 9.6F, FontStyle.Bold),
            AutoEllipsis = false, TextAlign = ContentAlignment.MiddleLeft
        };
        var detail = new Label
        {
            Text = entry.Description, Dock = DockStyle.Fill,
            ForeColor = Muted, Font = new Font("Segoe UI", 8.7F),
            AutoEllipsis = false, TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 4, 0, 0)
        };
        _toolTips.SetToolTip(title, entry.Title);
        _toolTips.SetToolTip(detail, entry.Description);
        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(detail, 0, 1);
        return panel;
    }

    private void ResizeIntegratedCards()
    {
        if (_fittingSystemCards || _systemPanelCards is null || _systemPanelCards.IsDisposed) return;
        _fittingSystemCards = true;
        try
        {
            var flow=_systemPanelCards.Content;
            int width=Math.Max(1,_systemPanelCards.ClientSize.Width-flow.Padding.Horizontal);
            flow.SuspendLayout();
            try
            {
                foreach(Control row in flow.Controls)
                {
                    int target=Math.Max(1,width-row.Margin.Horizontal);
                    if(row.Width!=target) row.Width=target;
                    if(row is V269SystemUpdateRow update) update.Fit();
                }
            }
            finally { flow.ResumeLayout(true); }
            _systemPanelCards.Reflow();
        }
        finally { _fittingSystemCards=false; }
    }

    private async Task RefreshStartupEntriesAsync(Func<Task<IReadOnlyList<StartupItem>>>? read = null)
    {
        long request = _systemViewGeneration;
        if (IsCurrentSystemView("startup", request))
        {
            _v269StartupTitle.Text = "Consultando entradas do seu usuário e de todos os usuários.";
        }
        await ExecuteAsync("Carregando itens de inicialização...", async () =>
        {
            var entries = await (read?.Invoke() ?? Task.Run(StartupManager.Read));
            if (!IsCurrentSystemView("startup", request)) return;
            _v269StartupTitle.Text = "Inicialização · ativar e desativar";

            ReplaceSystemCards(new[] { BuildStartupTrayCard() }
                .Concat(entries.Select(BuildStartupCard)).ToArray(), startup: true);
            // Consulta silenciosa não pode ocupar o painel operacional.
            _operationResult=$"{entries.Count} entradas de inicialização consultadas";
            _status.Text=_operationResult;
            Log($"Inicialização: {entries.Count} entradas consultadas.");
        }, quietStartup:true);
        PresentV269StartupDetails();
    }

    private V269StartupCard? _v269StartupSelected;
    private bool _fittingV269Startup;

    private static string StartupScope(StartupItem item) =>
        item.Details.StartsWith("Meu usuário",StringComparison.Ordinal) ? "Meu usuário" :
        item.Details.StartsWith("Todos os usuários",StringComparison.Ordinal) ? "Todos os usuários" : "Escopo não informado";

    private void SelectV269StartupCard(V269StartupCard card,bool explicitSelection)
    {
        if(_v269StartupSelected!=card)
        {
            _v269StartupSelected?.SetSelected(false);
            _v269StartupSelected=card;card.SetSelected(true);
        }
        if(explicitSelection&&!_busy&&!_v269Confirming&&_nav.SelectedPage==V269Page.Inicializacao)
        {
            if(_nav.RightPane.Current?.Kind is V269PanelKind.Result or V269PanelKind.History)_nav.PresentPanel(null);
        }
        PresentV269StartupDetails();
    }

    private V269StartupCard WireV269StartupCard(V269StartupCard card)
    {
        card.ApplyOptions(_v269Appearance);
        card.Click+=(_,_)=>SelectV269StartupCard(card,true);
        card.Enter+=(_,_)=>SelectV269StartupCard(card,false);
        card.Command.Enter+=(_,_)=>SelectV269StartupCard(card,false);
        return card;
    }

    private void PresentV269StartupDetails()
    {
        if(_nav.SelectedPage!=V269Page.Inicializacao||_busy||_v269Confirming||
            _nav.RightPane.Current?.Kind is V269PanelKind.Progress or V269PanelKind.Confirmation or V269PanelKind.Result or V269PanelKind.History)return;
        if(_v269StartupSelected is null||_v269StartupSelected.IsDisposed)
        {
            _v269StartupSelected=_v269StartupCards.Content.Controls.OfType<V269StartupCard>().FirstOrDefault();
            _v269StartupSelected?.SetSelected(true);
        }
        if(_v269StartupSelected is not {} selected)
        {
            _nav.PresentPanel(new(V269PanelKind.Details,"Detalhes da entrada","Aguardando consulta das entradas de inicialização.",FlowPage:V269Page.Inicializacao));
            return;
        }
        var item=selected.Item;
        string[] parts=item?.Details.Split(" · ",StringSplitOptions.None)??[];
        string type=item is null?"Registro do GF":parts.Length>1?parts[1]:item.Kind;
        string path=item is null?"Início na bandeja da sessão do usuário":item.Kind=="file"?item.Locator:
            parts.Length>2&&!parts[^1].Equals("somente leitura",StringComparison.OrdinalIgnoreCase)?parts[^1]:item?.Locator??"Não informado";
        bool? enabled=item?.Enabled??selected.GfEnabled;
        var model=new V269AppDetailModel(selected.AccessibleName??"",item is null?"Gerenciador Final":"Origem local",
            [("Origem",item is null?"Gerenciador Final":type),("Escopo",item is null?"Meu usuário":StartupScope(item)),("Tipo",type),("Situação",enabled is true?"Habilitado":enabled is false?"Desabilitado":"Indisponível")],
            "Apenas entradas do usuário que o motor permite podem ser alteradas. O efeito ocorre no próximo login.",
            Description:path,CleanupGlyph:"power",CleanupDescriptionHeading:"Caminho",
            PreviousResults:V269ModuleHistory(V269Page.Inicializacao),DismissResult:DismissV269ModuleResult);
        if(_nav.RightPane.Current?.Module is {} previous&&previous.Name==model.Name&&previous.Fields.SequenceEqual(model.Fields)&&previous.Description==model.Description&&(previous.PreviousResults??[]).SequenceEqual(model.PreviousResults??[]))return;
        _nav.PresentPanel(new(V269PanelKind.Details,"Detalhes da entrada","",FlowPage:V269Page.Inicializacao,Module:model));
    }

    private Control BuildStartupTrayCard()
    {
        bool? enabled;
        try { enabled = StartupTrayRegistration.IsEnabled(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or InvalidDataException)
        {
            enabled = null;
            DiagnosticTrace.Write("INICIALIZACAO.BANDEJA estado=consulta_indisponivel motivo=" + ex.GetType().Name);
        }
        var card=WireV269StartupCard(new V269StartupCard("Gerenciador Final","Meu usuário","apps",
            enabled is true?"Desativar na inicialização":"Ativar na inicialização",enabled.HasValue,null,enabled));
        card.Command.Click+=async (_,_) =>
        {
            if(_busy||_v269Confirming||enabled is not bool old)return;
            SelectV269StartupCard(card,true);
            if(!await ConfirmV269Async("Inicialização do Gerenciador Final",
                old?"Desativar a inicialização do GF com o Windows?":"Iniciar o GF na bandeja no próximo login?"))return;
            await ExecuteAsync("Alterando inicialização do GF...",async ()=>
            {
                if(StartupTrayRegistration.IsEnabled()!=old)throw new InvalidOperationException("A configuração mudou; consulte novamente.");
                StartupTrayRegistration.SetEnabled(!old);
                bool verified=StartupTrayRegistration.IsEnabled()==!old;
                string message=verified?"Configuração confirmada; efeito no próximo login.":"Não foi possível confirmar a configuração.";
                Status(message);
                PresentV269ModuleResult(V269Page.Inicializacao,"Inicialização do Gerenciador Final",message,verified);
                if(_nav.SelectedPage==V269Page.Inicializacao)
                {
                    var entries=await Task.Run(StartupManager.Read);
                    ReplaceSystemCards(new[]{BuildStartupTrayCard()}.Concat(entries.Select(BuildStartupCard)),startup:true);
                }
            });
        };
        return card;
    }

    private Control BuildStartupCard(StartupItem item)
    {
        var card=WireV269StartupCard(new V269StartupCard(item.Name,StartupScope(item),
            item.Kind=="file"?"folder":"document",
            !item.Editable?"Somente leitura":item.Enabled?"Desativar na inicialização":"Ativar na inicialização",
            item.Editable,item,null));
        card.Command.Click+=async (_,_) =>
        {
            if(_busy||_v269Confirming||!item.Editable)return;
            SelectV269StartupCard(card,true);
            string verb=item.Enabled?"desativar":"ativar";
            if(!await ConfirmV269Async("Confirmar inicialização",$"Deseja {verb} '{item.Name}' na inicialização do Windows?"))return;
            long request=_systemViewGeneration;
            await ExecuteAsync("Alterando inicialização...",async ()=>
            {
                var before=await Task.Run(StartupManager.Read);
                if(!before.Contains(item))throw new InvalidOperationException("A entrada mudou; consulte novamente.");
                await Task.Run(()=>StartupManager.Toggle(item));
                var entries=await Task.Run(StartupManager.Read);
                string target=item.Kind=="file"?(item.Enabled?item.Locator+".disabled":item.Locator[..^9]):item.Locator;
                bool verified=entries.Any(x=>x.Kind==item.Kind&&x.Locator.Equals(target,StringComparison.OrdinalIgnoreCase)&&x.Enabled!=item.Enabled);
                string message=verified?"Inicialização alterada e confirmada. Efeito no próximo login.":"Alteração não confirmada na nova consulta.";
                Log("Inicialização: "+item.Name+" — "+message);
                Status(message);
                PresentV269ModuleResult(V269Page.Inicializacao,item.Name,message,verified);
                if(!IsCurrentSystemView("startup",request))return;
                ReplaceSystemCards(new[]{BuildStartupTrayCard()}.Concat(entries.Select(BuildStartupCard)),startup:true);
            });
        };
        return card;
    }

    private IReadOnlyList<V269AppPreviousResult> V269ModuleHistory(V269Page page) =>
        _v269ModuleHistory.TryGetValue(page,out var results)?results.ToArray():[];

    private void DismissV269ModuleResult(Guid id)
    {
        foreach(var list in _v269ModuleHistory.Values)list.RemoveAll(x=>x.Id==id);
        foreach(var pair in _v269ModuleRetained.Where(x=>x.Value.FlowResult?.HistoryId==id).ToArray())
            _v269ModuleRetained.Remove(pair.Key);
        if(_nav.RightPane.Current?.FlowResult?.HistoryId==id)_nav.PresentPanel(null);
        _nav.RightPane.RefreshAppOperationHistory();
        if(_nav.SelectedPage==V269Page.Sistema)PresentV269SystemDetails();
        if(_nav.SelectedPage==V269Page.Inicializacao)PresentV269StartupDetails();
    }

    private void PresentV269ModuleRetained(V269Page page)
    {
        if(!_busy&&!_v269Confirming&&_nav.RightPane.Current?.Kind is not (V269PanelKind.Confirmation or V269PanelKind.Progress or V269PanelKind.Result or V269PanelKind.History)
            &&_v269ModuleRetained.TryGetValue(page,out var result))
            _nav.PresentPanel(result);
        else if(page==V269Page.Sistema)PresentV269SystemDetails();else PresentV269StartupDetails();
    }

    private void PresentV269ModuleResult(V269Page page,string title,string message,bool complete)
    {
        if(IsDisposed||Disposing)return;
        LocalDiagnosticReport.Session.RecordOperation(page,"resultado",complete);
        var id=Guid.NewGuid();
        if(!_v269ModuleHistory.TryGetValue(page,out var history))_v269ModuleHistory[page]=history=new();
        history.Insert(0,new V269AppPreviousResult(id,title,message,complete));
        if(history.Count>20)history.RemoveAt(history.Count-1);
        var result=new V269PanelPresentation(V269PanelKind.Result,title,message,
            FlowResult:new V269FlowResult(page,complete,()=>DismissV269ModuleResult(id),id),FlowPage:page);
        _v269ModuleRetained[page]=result;
        if(_nav.SelectedPage==page&&_nav.RightPane.Current?.Kind!=V269PanelKind.Confirmation &&
            (_nav.RightPane.Current?.Kind!=V269PanelKind.Progress || _nav.RightPane.Current.FlowPage is null || _nav.RightPane.Current.FlowPage==page))
            _nav.PresentPanel(result);
        else if(_nav.RightPane.Current is {Kind:V269PanelKind.Progress,FlowPage:var owner}&&owner==page)
            _nav.PresentPanel(null);
    }

    private void UpdateProcessSamplingCadence()
    {
        if (IsDisposed) return;
        bool visible = Visible && !IsInTray && _nav.SelectedPage == V269Page.Sistema;
        int interval = ProcessSamplingPolicy.IntervalMs(visible, IsInTray || !Visible);
        if (_memoryTimer.Interval != interval) _memoryTimer.Interval = interval;
    }

    // Coleta global por GlobalMemoryStatusEx, sem enumeracao de processos.
    private void BeginBackgroundMemoryMonitoring()
    {
        if (IsDisposed) return;
        UpdateProcessSamplingCadence();
        _memoryTimer.Start();
        Log("RAM: acompanhamento local em segundo plano (2 s na aba RAM, 12 s em outras abas, 20 s na bandeja).");
    }

    private Task ScanWindowsUpdatesAsync() => ScanWindowsUpdatesInBackgroundAsync(force: true);

    private bool WindowsUpdateViewActive => !IsDisposed && !Disposing && _nav.SelectedPage==V269Page.Sistema;

    private async Task ScanWindowsUpdatesInBackgroundAsync(bool force = false)
    {
        if (IsDisposed || Disposing || _betaClose.IsClosing || _windowsUpdateInstalling || _busy || _v269Confirming) return;
        if (_windowsUpdateScanning || (!force && (_windowsUpdateChecked || _lastWindowsUpdateOperation is not null)))
        {
            if (WindowsUpdateViewActive) DrawWindowsUpdates();
            return;
        }

        _windowsUpdateScanning = true;
        if(WindowsUpdateViewActive) _nav.PresentPanel(new(V269PanelKind.Progress,"Verificar atualizações","Consultando ofertas oficiais de software…",FlowPage:V269Page.Sistema));
        _updateSelection.Clear();
        RenderTitle();
        if (WindowsUpdateViewActive) DrawWindowsUpdates();
        Log("Windows Update: consulta oficial de atualizações de software iniciada.");
        try
        {
            var found = await WindowsUpdateClient.ScanAsync();
            if (IsDisposed || Disposing) return;
            _updateCatalog = found.Offers;
            _windowsUpdateRebootRequired = found.RebootRequired ?? _windowsUpdateRebootRequired;
            _windowsUpdateChecked = true;
            _windowsUpdateScanFailure = null;
            _lastWindowsUpdateOperation = null;
            _lastWindowsUpdateSelection = [];
            Log($"Windows Update: {_updateCatalog.Count} atualização(ões) de software encontrada(s).");
            DiagnosticTrace.Write($"WINDOWS_UPDATE.CONSULTA_OK quantidade={_updateCatalog.Count}");
            if (WindowsUpdateViewActive && _nav.SelectedPage == V269Page.Sistema)
                Status($"Windows Update: {_updateCatalog.Count} atualização(ões) disponível(is)");
            if(force)PresentV269ModuleResult(V269Page.Sistema,"Verificar atualizações",$"{_updateCatalog.Count} oferta(s) de software consultada(s).",true);
        }
        catch (Exception ex)
        {
            if (IsDisposed || Disposing) return;
            _windowsUpdateScanFailure = WindowsUpdateOperation.ErrorText(ex.HResult).TrimStart();
            _windowsUpdateChecked = false;
            _updateCatalog = [];
            DiagnosticTrace.Failure("WINDOWS_UPDATE.CONSULTA_FALHA", ex);
            Log("Windows Update: consulta inconclusiva " + _windowsUpdateScanFailure + ".");
            if (WindowsUpdateViewActive && _nav.SelectedPage == V269Page.Sistema)
                Status("Windows Update: consulta inconclusiva");
            if(force)PresentV269ModuleResult(V269Page.Sistema,"Verificar atualizações","Consulta inconclusiva "+_windowsUpdateScanFailure+".",false);
        }
        finally
        {
            _windowsUpdateScanning = false;
            if (!IsDisposed && !Disposing)
            {
                RenderTitle();
                if (WindowsUpdateViewActive) DrawWindowsUpdates();
            }
        }
    }

    private async Task ShowWindowsUpdateHistoryAsync()
    {
        if (!WindowsUpdateViewActive || _nav.SelectedPage != V269Page.Sistema ||
            _busy || _batchUpdatesActive || _v269Confirming || _betaClose.IsClosing ||
            _windowsUpdateScanning || _windowsUpdateInstalling || _windowsUpdateHistoryReading) return;
        long generation = _systemViewGeneration;
        var loading = new V269PanelPresentation(V269PanelKind.Details,
            "Histórico do Windows Update", "Consultando o registro local do Windows Update…");
        _windowsUpdateHistoryReading = true;
        RefreshWindowsUpdateInstallButton();
        _nav.PresentPanel(loading);
        string history;
        try { history = await WindowsUpdateClient.ReadHistoryAsync(); }
        catch (Exception)
        {
            history = "Não foi possível consultar o histórico do Windows Update. Nenhuma alteração foi feita.";
        }
        finally { _windowsUpdateHistoryReading = false; }
        if (IsDisposed || Disposing || _betaClose.IsClosing) return;
        RefreshWindowsUpdateInstallButton();
        if (IsCurrentSystemView("update", generation) && !_busy && !_v269Confirming &&
            ReferenceEquals(_nav.RightPane.Current, loading))
            _nav.PresentPanel(new(V269PanelKind.History, "Histórico do Windows Update", history));
    }

    private bool CanSelectWindowsUpdates => !_busy && !_batchUpdatesActive && !_v269Confirming &&
        !_betaClose.IsClosing && !_windowsUpdateInstalling &&
        !_windowsUpdateScanning && _windowsUpdateChecked && _windowsUpdateRebootRequired is not true;

    private void RefreshWindowsUpdateInstallButton()
    {
        if (_searchWindowsUpdatesButton is { IsDisposed: false } search)
        {
            search.Text = _windowsUpdateScanning ? "Verificando..." : "Verificar atualizações";
            search.Enabled = WindowsUpdateViewActive && !_busy && !_batchUpdatesActive &&
                !_betaClose.IsClosing && !_windowsUpdateInstalling && !_windowsUpdateScanning;
        }
        if (_windowsUpdateHistoryButton is { IsDisposed: false } history)
            history.Enabled = WindowsUpdateViewActive && !_busy && !_batchUpdatesActive && !_v269Confirming &&
                !_betaClose.IsClosing && !_windowsUpdateScanning && !_windowsUpdateInstalling && !_windowsUpdateHistoryReading;
        if (_systemPanelCards is null) return;
        foreach (var card in _systemPanelCards.Content.Controls.OfType<V269SystemUpdateRow>())
            foreach (var button in card.Controls.OfType<Button>())
                if (button.Name == "V269InstallWindowsUpdate" && button.Tag is WinUpdateChoice offer)
                    button.Enabled = WindowsUpdateViewActive && CanSelectWindowsUpdates && offer.EulaAccepted;
    }

    private void ShowWindowsUpdateStatus(string title, string summary)
    {
        _systemPanelTitle.Text = "Windows Update";
        _systemPanelSummary.Text = summary;
        FitSystemSummaryHeight();
        if(WindowsUpdateViewActive&&_activeSystemView=="update")PresentV269SystemDetails();
    }

    private void DrawWindowsUpdates()
    {
        RefreshWindowsUpdateInstallButton();
        if (_windowsUpdateScanning)
        {
            ShowWindowsUpdateStatus("Windows Update · consultando",
                "Busca oficial em andamento. A consulta pode demorar alguns minutos.");
            ReplaceSystemCards([]);
            return;
        }

        string restart = _windowsUpdateRebootRequired is true
            ? " Reinicialização necessária. Salve o trabalho e reinicie o Windows manualmente." : "";
        if (_windowsUpdateChecked)
        {
            string available = _updateCatalog.Count switch
            {
                0 => "Nenhuma atualização de software disponível nesta consulta.",
                1 => "1 atualização de software disponível.",
                _ => $"{_updateCatalog.Count} atualizações de software disponíveis."
            };
            string guidance = _updateCatalog.Count > 0 && _windowsUpdateRebootRequired is not true
                ? " Clique em Instalar na atualização desejada e confirme a operação." : "";
            ShowWindowsUpdateStatus("Windows Update", (_lastWindowsUpdateOperation?.Summary + " " +
                available + guidance + restart).Trim());
            ReplaceSystemCards(_updateCatalog.Select(BuildUpdateCard));
            return;
        }

        string summary = _windowsUpdateScanFailure is string failure
            ? $"Consulta inconclusiva {failure}. Clique em Verificar atualizações para tentar novamente."
            : _lastWindowsUpdateOperation?.Summary ??
                "Nenhuma busca nesta sessão. Clique em Verificar atualizações para consultar.";
        if (_lastWindowsUpdateOperation is { } result && _windowsUpdateScanFailure is null)
        {
            summary += WindowsUpdateOperation.ErrorText(result.Overall.Error);
            if (result.Outcome == WindowsUpdateOutcome.Installation && result.Overall.RebootRequired is null &&
                !result.RequiresRestart)
                summary += " O Windows não confirmou se é necessário reiniciar.";
            if (!result.RequiresRestart)
                summary += " Clique em Verificar atualizações para consultar novamente.";
        }
        ShowWindowsUpdateStatus(_windowsUpdateScanFailure is not null ? "Windows Update · consulta inconclusiva" :
            "Windows Update · resultado", summary + restart);
        ReplaceSystemCards(_lastWindowsUpdateSelection.Select(BuildWindowsUpdateResultCard));
    }

    private Control BuildUpdateCard(WinUpdateChoice update)
    {
        string description=(update.Kb.Length>0?update.Kb+" · ":"")+
            (update.Downloaded?"Baixada":"Download necessário")+
            (update.EulaAccepted?" · Licença já aceita":" · Licença pendente no Windows Update");
        var row = new V269SystemUpdateRow(update.Title,description,"Instalar",_v269Appearance);
        var toggle=row.Command!;
        toggle.Name="V269InstallWindowsUpdate"; toggle.Tag=update;
        toggle.AccessibleName="Instalar atualização: "+update.Title;
        toggle.Enabled=WindowsUpdateViewActive && CanSelectWindowsUpdates && update.EulaAccepted;
        toggle.Click += async (_,_) =>
        {
            if (!WindowsUpdateViewActive || _nav.SelectedPage != V269Page.Sistema ||
                !CanSelectWindowsUpdates || !update.EulaAccepted ||
                !_updateCatalog.Any(offer => EqualityComparer<WinUpdateChoice>.Default.Equals(offer, update))) return;
            SetSystemView("update");
            _updateSelection.Clear(); _updateSelection.Add(update.Id);
            await InstallSelectedWindowsUpdatesAsync();
        };
        row.Click += (_,_)=>SetSystemView("update");
        return row;
    }

    private Control BuildWindowsUpdateResultCard(WinUpdateChoice update, int index) =>
        new V269SystemUpdateRow(update.Title,DescribeWindowsUpdateItem(_lastWindowsUpdateOperation!,index),null,_v269Appearance);

    private static string DescribeWindowsUpdateItem(WindowsUpdateOperation result, int index)
    {
        if (result.Items.Count == 0)
            return result.Outcome is WindowsUpdateOutcome.Installation or WindowsUpdateOutcome.NoResult
                ? "Resultado inconclusivo" : "Instalação não iniciada";
        var item = result.Items[index];
        string stage = result.Outcome == WindowsUpdateOutcome.Download ? "Download" : "Instalação";
        string restart = item.RebootRequired is true ? " · Reinicialização necessária" : "";
        return $"{stage} · {item.Description}{WindowsUpdateOperation.ErrorText(item.Error)}{restart}";
    }

    private async Task InstallSelectedWindowsUpdatesAsync()
    {
        if (!WindowsUpdateViewActive || !CanSelectWindowsUpdates || _updateSelection.Count == 0) return;
        if (_updateSelection.Count > WindowsUpdateOperation.MaxSelection)
        {
            Status($"Selecione no máximo {WindowsUpdateOperation.MaxSelection} atualizações por operação.");
            return;
        }
        var selected = _updateCatalog.Where(update => update.EulaAccepted && _updateSelection.Contains(update.Id))
            .OrderBy(update => update.Id, StringComparer.Ordinal).ToArray();
        if (selected.Length != _updateSelection.Count || selected.Any(update =>
            WindowsUpdateClient.CanonicalSelectedIdentity(update.Id) != update.Id))
        {
            Status("A seleção do Windows Update mudou. Consulte novamente.");
            return;
        }
        string titles = string.Join(Environment.NewLine, selected.Select(update => "• " + update.Title));
        if (!await ConfirmV269Async("Confirmar Windows Update",
            $"Baixar e instalar {selected.Length} atualização(ões) do Windows? " +
            "Algumas podem exigir reinicialização." + Environment.NewLine + Environment.NewLine + titles)) return;
        if (!WindowsUpdateViewActive || _nav.SelectedPage != V269Page.Sistema || !CanSelectWindowsUpdates ||
            !_updateSelection.SetEquals(selected.Select(update => update.Id)) ||
            selected.Any(update => !_updateCatalog.Any(offer => EqualityComparer<WinUpdateChoice>.Default.Equals(offer, update)))) return;

        await ExecuteAsync("Aguardando autorização administrativa…", async () =>
        {
            _windowsUpdateInstalling = true;
            var exchange = new WindowsUpdateExchange(selected.Length, phase =>
            {
                if (IsDisposed || Disposing) return;
                string description = WindowsUpdateOperation.PhaseText(phase);
                Status(description);
                Log("Windows Update: " + description);
                if (WindowsUpdateViewActive)
                    ShowWindowsUpdateStatus("Windows Update · em andamento", description);
            });
            try
            {
                RefreshWindowsUpdateInstallButton();
                ShowWindowsUpdateStatus("Windows Update · em andamento", "Aguardando autorização administrativa…");
                ReplaceSystemCards([]);
                await RunElevatedAsync("wu-install:" + string.Join(",", selected.Select(update => update.Id)),
                    windowsUpdateMessage: exchange.Receive);
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Failure("WINDOWS_UPDATE.OPERACAO_SEM_RETORNO", ex);
            }
            finally { _windowsUpdateInstalling = false; }

            if (IsDisposed || Disposing || _betaClose.IsShutdown) return;
            var result = exchange.Result;
            _lastWindowsUpdateOperation = result;
            if (result.Outcome != WindowsUpdateOutcome.AuthorizationDeclined)
            {
                _lastWindowsUpdateSelection = selected;
                _updateSelection.Clear();
                _updateCatalog = [];
                _windowsUpdateChecked = false;
                _windowsUpdateScanFailure = null;
                if (result.RequiresRestart) _windowsUpdateRebootRequired = true;
            }
            RenderTitle();
            Log("Windows Update: " + result.Summary + WindowsUpdateOperation.ErrorText(result.Overall.Error));
            for (int i = 0; i < selected.Length && i < result.Items.Count; i++)
                Log(selected[i].Title + " · " + DescribeWindowsUpdateItem(result, i));
            if (result.RequiresRestart)
                Log("Windows Update: reinicialização necessária. Salve o trabalho e reinicie manualmente.");
            Status(result.RequiresRestart ? "Windows Update · reinicialização necessária" : result.Summary);
            PresentV269ModuleResult(V269Page.Sistema,"Windows Update",result.Summary,
                result.Outcome==WindowsUpdateOutcome.Installation && result.Items.Count==selected.Length &&
                result.FullySucceeded);
            if (WindowsUpdateViewActive) DrawWindowsUpdates();
        });
    }

    private Task RefreshMemoryRamAsync()
    {
        if (_memorySampling || IsDisposed) return Task.CompletedTask;
        _memorySampling = true;
        try
        {
            // GlobalMemoryStatusEx é consulta instantânea, não requer
            // Task.Run nem cria threads para cada amostra.
            var sample = MemoryRamReader.Capture();
            if (!IsDisposed && sample is not null)
            {
                if (Visible && !IsInTray && _nav.SelectedPage == V269Page.Sistema)
                    ShowMemoryRamSample(sample);
                else
                    _memoryHistory.Add(sample);
                EvaluateAutomaticMemoryCleanup(sample);
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed) Log("RAM: consulta indisponível (" + ex.GetType().Name + ").");
        }
        finally { _memorySampling = false; }
        return Task.CompletedTask;
    }
}
