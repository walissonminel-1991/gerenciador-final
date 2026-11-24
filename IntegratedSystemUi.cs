using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Globalization;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private readonly Label _systemPanelTitle = new();
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
    private TableLayoutPanel? _windowsUpdateActionsPanel;
    private Button? _searchWindowsUpdatesButton;
    private string _activeSystemView = "memory";
    private bool _fittingSystemCards;
    private long _systemViewGeneration;

    private TableLayoutPanel BuildIntegratedSystemPanel()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = AppServices.Background,
            ColumnCount = 1, RowCount = 2, Padding = new Padding(5, 5, 5, 4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 4,
            Margin = Padding.Empty, BackColor = AppServices.Background
        };
        for (int i = 0; i < 4; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddIntegratedAction(actions, 0, "Memória RAM", "Consumo e memória disponível. Histórico local, também na bandeja.", AppServices.Blue,
            async () => { await RefreshMemoryRamAsync(); });
        AddIntegratedAction(actions, 1, "Energia", "Consulte e revise os planos de energia do Windows.", AppServices.Gray,
            RefreshEnergyInfoAsync);
        AddIntegratedAction(actions, 2, "Windows Update", "Procure, selecione e instale atualizações oficiais.", AppServices.Green,
            () => { DrawWindowsUpdates(); return Task.CompletedTask; });
        AddIntegratedAction(actions, 3, "Este computador", "Detalhes de CPU, GPU, RAM, BIOS, placa-mãe e discos.", AppServices.Blue,
            async () =>
            {
                long request = _systemViewGeneration;
                await ExecuteAsync("Consultando hardware...", async () =>
                {
                    var snapshot = await StaWorker.Run(DetailedHardware.Capture);
                    if (!IsCurrentSystemView("hardware", request)) return;
                    ShowHardwareCompact(snapshot);
                    Status("Hardware consultado");
                    Log("Informações detalhadas de hardware atualizadas.");
                });
            });
        root.Controls.Add(actions, 0, 0);

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = PanelBackground,
            ColumnCount = 1, RowCount = 4, Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(2, 2, 2, 0)
        };
        _systemDetails = details;
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 186));
        _systemPanelTitle.Text = "Painel de informações";
        _systemPanelTitle.Dock = DockStyle.Fill;
        _systemPanelTitle.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
        _systemPanelTitle.ForeColor = Ink;
        _systemPanelTitle.TextAlign = ContentAlignment.MiddleLeft;
        header.Controls.Add(_systemPanelTitle, 0, 0);
        _exportHardwareButton = CreateSecondaryButton("Salvar ficha PNG");
        _exportHardwareButton.Dock = DockStyle.Fill;
        _exportHardwareButton.Margin = new Padding(4, 1, 1, 3);
        _exportHardwareButton.Visible = false;
        _exportHardwareButton.Click += (_, _) => ExportHardwareImage();
        _toolTips.SetToolTip(_exportHardwareButton, "Salva todas as informações de hardware em uma imagem PNG, sem depender da altura da janela.");
        _actionControls.Add(_exportHardwareButton);
        header.Controls.Add(_exportHardwareButton, 1, 0);
        details.Controls.Add(header, 0, 0);
        _systemPanelSummary.Text = "Escolha uma área acima. Alterações exigem confirmação explícita.";
        _systemPanelSummary.Dock = DockStyle.Fill;
        _systemPanelSummary.Font = new Font("Segoe UI", 9F);
        _systemPanelSummary.ForeColor = Muted;
        _systemPanelSummary.TextAlign = ContentAlignment.MiddleLeft;
        details.Controls.Add(_systemPanelSummary, 0, 1);
        _windowsUpdateActionsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 1,
            BackColor = PanelBackground, Margin = Padding.Empty,
            Padding = Padding.Empty, Visible = false
        };
        _windowsUpdateActionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _searchWindowsUpdatesButton = CreateButton("Buscar atualizações", AppServices.Blue, Color.White);
        _searchWindowsUpdatesButton.Name = "WindowsUpdateExplicitSearch";
        _searchWindowsUpdatesButton.AccessibleName = "Buscar atualizações do Windows";
        _searchWindowsUpdatesButton.AccessibleDescription =
            "Inicia consulta oficial apenas quando solicitado; abrir o cartão não inicia busca.";
        _searchWindowsUpdatesButton.Dock = DockStyle.Fill;
        _searchWindowsUpdatesButton.Margin = new Padding(3, 1, 3, 5);
        _searchWindowsUpdatesButton.Click += async (_, _) => await ScanWindowsUpdatesAsync();
        _actionControls.Add(_searchWindowsUpdatesButton);
        _windowsUpdateActionsPanel.Controls.Add(_searchWindowsUpdatesButton, 0, 0);
        details.Controls.Add(_windowsUpdateActionsPanel, 0, 2);
        _systemPanelCards = new DarkScrollableFlow { Dock = DockStyle.Fill, BackColor = PanelBackground };
        _systemPanelCards.Content.BackColor = PanelBackground;
        _systemPanelCards.Content.Padding = new Padding(3, 4, 3, 3);
        _systemPanelCards.Resize += (_, _) => ResizeIntegratedCards();
        // Quando a barra de rolagem aparece/desaparece, o viewport real muda
        // mesmo que o tamanho do controle externo nao se altere.
        _systemPanelCards.Content.SizeChanged += (_, _) => ResizeIntegratedCards();
        var contentHost = new Panel { Dock = DockStyle.Fill, BackColor = PanelBackground, Margin = Padding.Empty };
        contentHost.Controls.Add(_systemPanelCards);
        contentHost.Controls.Add(BuildMemoryRamDashboard());
        _hardwareGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3,
            BackColor = PanelBackground, Margin = Padding.Empty, Padding = Padding.Empty,
            Visible = false
        };
        _hardwareGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _hardwareGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 3; i++) _hardwareGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 3F));
        contentHost.Controls.Add(_hardwareGrid);
        _v269EnergyView = ScrollablePage(BuildEnergyPanel(),
            _ => (int)Math.Ceiling(405 * DeviceDpi / 96d));
        _v269EnergyView.Visible = false;
        contentHost.Controls.Add(_v269EnergyView);
        _systemPanelCards.BringToFront();
        details.Controls.Add(contentHost, 0, 3);
        SetSystemView("memory");
        root.Controls.Add(details, 0, 1);
        root.SizeChanged += (_, _) => FitSystemActionsHeight(root, actions);
        details.SizeChanged += (_, _) => FitSystemSummaryHeight();
        FitSystemActionsHeight(root, actions);
        FitSystemSummaryHeight();

        _memoryTimer.Tick += async (_, _) =>
        {
            if (_memorySampling || IsDisposed) return;
            UpdateProcessSamplingCadence();
            await RefreshMemoryRamAsync();
        };
        FormClosed += (_, _) => _memoryTimer.Dispose();
        return root;
    }

    private int SystemPageMinimumHeight(TableLayoutPanel root)
    {
        int Scale(int px) => (int)Math.Ceiling(px * Math.Max(1d, DeviceDpi / 96d));
        float minimumBody = _activeSystemView switch
        {
            "memory" when _memoryDashboardView is TableLayoutPanel dashboard =>
                dashboard.RowStyles.Cast<RowStyle>().Where(row => row.SizeType == SizeType.Absolute)
                    .Sum(row => row.Height) + dashboard.Padding.Vertical + Scale(50),
            "energy" => Scale(405),
            _ => Scale(180)
        };
        float fixedSystem = root.RowStyles[0].Height + root.Padding.Vertical;
        if (_systemDetails is { } details)
            fixedSystem += details.RowStyles.Cast<RowStyle>()
                .Where(row => row.SizeType == SizeType.Absolute).Sum(row => row.Height) +
                details.Padding.Vertical + details.Margin.Vertical;
        return Math.Max(Scale(365), (int)Math.Ceiling(fixedSystem + minimumBody));
    }

    private void SetSystemView(string view)
    {
        // Navegação imediata: não deixar cartões do modo anterior sob título
        // da nova aba enquanto operações assíncronas ainda consultam o Windows.
        ++_systemViewGeneration;
        bool changing = _activeSystemView != view;
        _activeSystemView = view; // Coleta em background permanece independente.
        if (changing && view is "startup" or "update" or "hardware")
            ReplaceSystemCards([]);
        if (_systemDetails is null || _windowsUpdateActionsPanel is null) return;
        bool showInstall = view == "update";
        bool showMemory = view == "memory";
        _systemDetails.RowStyles[2].Height = showInstall ? 38 : 0;
        FitSystemSummaryHeight();
        UpdateProcessSamplingCadence();
        _windowsUpdateActionsPanel.Visible = showInstall;
        if (_exportHardwareButton is not null) _exportHardwareButton.Visible = view == "hardware" && _hardwareGroups.Count > 0;
        if (_hardwareGrid is not null) _hardwareGrid.Visible = view == "hardware";
        if (_v269EnergyView is not null) _v269EnergyView.Visible = view == "energy";
        if (_memoryDashboardView is not null) _memoryDashboardView.Visible = showMemory;
        if (_systemPanelCards is not null)
            _systemPanelCards.Visible = view is not ("hardware" or "energy") && !showMemory;
        _systemDetails.PerformLayout();
        _relayoutSystem?.Invoke();
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

    private void FitSystemSummaryHeight()
    {
        if (_systemDetails is null || _systemDetails.IsDisposed ||
            _systemPanelSummary.IsDisposed) return;
        int width = Math.Max(180, _systemDetails.ClientSize.Width -
            _systemDetails.Padding.Horizontal - 10);
        int measured = TextRenderer.MeasureText(_systemPanelSummary.Text,
            _systemPanelSummary.Font, new Size(width, 200),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
        int height = Math.Clamp(measured + 4,
            (int)Math.Ceiling(26d * Math.Max(1d, DeviceDpi / 96d)),
            (int)Math.Ceiling(78d * Math.Max(1d, DeviceDpi / 96d)));
        if (Math.Abs(_systemDetails.RowStyles[1].Height - height) >= 1)
            _systemDetails.RowStyles[1].Height = height;
    }

    private void ReplaceSystemCards(IEnumerable<Control> cards, bool startup = false)
    {
        var host = startup ? _v269StartupCards : _systemPanelCards;
        if (host is null || IsDisposed) return;
        var flow = host.Content;
        flow.SuspendLayout();
        try
        {
            var old = flow.Controls.Cast<Control>().ToArray();
            flow.Controls.Clear();
            foreach (Control control in old) control.Dispose();
            foreach (Control card in cards) flow.Controls.Add(card);
        }
        finally { flow.ResumeLayout(true); }
        if (startup) ResizeV269StartupCards();
        else ResizeIntegratedCards();
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
        if (_fittingSystemCards || _systemPanelCards is null ||
            _systemPanelCards.IsDisposed) return;
        _fittingSystemCards = true;
        try
        {
            FlowLayoutPanel flow = _systemPanelCards.Content;
            // Usar viewport fisico JA reduzido pela scrollbar escura.
            // Evita reserva dupla e contempla 720p, ultrawide, 4K e DPI.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int space = flow.ClientSize.Width - flow.Padding.Horizontal;
                if (space <= 0) break;
                int columns = space >= 720 * Math.Max(1, flow.DeviceDpi / 96d) ? 2 : 1;
                int cardWidth = StartupCardLayout.CardWidth(space, columns, flow.DeviceDpi);
                int beforeWidth = flow.ClientSize.Width;
                flow.SuspendLayout();
                try
                {
                    foreach (Control control in flow.Controls)
                        if (control.Width != cardWidth) control.Width = cardWidth;
                }
                finally { flow.ResumeLayout(true); }
                _systemPanelCards.Reflow();
                if (flow.ClientSize.Width == beforeWidth) break;
            }
        }
        finally { _fittingSystemCards = false; }
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
            Status($"{entries.Count} entradas de inicialização consultadas");
            Log($"Inicialização: {entries.Count} entradas consultadas.");
        });
    }

    private Control BuildStartupTrayCard()
    {
        bool enabled;
        try { enabled = StartupTrayRegistration.IsEnabled(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or InvalidDataException)
        {
            enabled = false;
            DiagnosticTrace.Write("INICIALIZACAO.BANDEJA estado=consulta_indisponivel motivo=" + ex.GetType().Name);
        }
        var card = new TableLayoutPanel
        {
            Name = "GFStartupTrayCard",
            Width = 390, Height = 108, RowCount = 3, ColumnCount = 1,
            BackColor = Color.FromArgb(38, 50, 63), Margin = new Padding(2, 2, 7, 5),
            Padding = new Padding(8, 5, 8, 5)
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        card.Controls.Add(new Label
        {
            Text = "Gerenciador Final · iniciar com o Windows",
            Dock = DockStyle.Fill, ForeColor = Ink,
            Font = new Font("Segoe UI", 9.2F, FontStyle.Bold)
        }, 0, 0);
        card.Controls.Add(new Label
        {
            Text = "Opcional: abrir direto na bandeja após entrar na sua conta.",
            Dock = DockStyle.Fill, ForeColor = Muted,
            Font = new Font("Segoe UI", 8.1F), AutoEllipsis = true
        }, 0, 1);
        var toggle = CreateButton(enabled ? "Desativar início automático" : "Ativar início na bandeja",
            enabled ? AppServices.Orange : AppServices.Green, Color.White);
        toggle.Dock = DockStyle.Fill;
        toggle.Margin = Padding.Empty;
        toggle.Click += async (_, _) =>
        {
            if (_busy) return;
            if (!await ConfirmV269Async("Inicialização do Gerenciador Final",
                enabled ? "Desativar a inicialização do GF com o Windows?" :
                "Deseja iniciar o GF na bandeja quando entrar no Windows? Sem pedir administrador.")) return;
            try
            {
                StartupTrayRegistration.SetEnabled(!enabled);
                DiagnosticTrace.Write("INICIALIZACAO.BANDEJA estado=" + (!enabled ? "ativado" : "desativado"));
                Status(!enabled ? "GF iniciará na bandeja no próximo login." :
                    "Início automático do GF desativado.");
                _ = RefreshStartupEntriesAsync();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or InvalidDataException)
            {
                DiagnosticTrace.Write("INICIALIZACAO.BANDEJA estado=falha motivo=" + ex.GetType().Name);
                Status("Não foi possível alterar o início automático. Confira o diagnóstico.");
            }
        };
        card.Controls.Add(toggle, 0, 2);
        return card;
    }

    private Control BuildStartupCard(StartupItem item)
    {
        var card = new TableLayoutPanel
        {
            Name = "StartupEntryCard",
            Width = 390, Height = 108, RowCount = 3, ColumnCount = 1,
            BackColor = Color.FromArgb(38, 50, 63), Margin = new Padding(2, 2, 7, 5),
            Padding = new Padding(8, 5, 8, 5)
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        var name = new Label { Text = item.Name, Dock = DockStyle.Fill, ForeColor = Ink,
            Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
            AutoEllipsis = true, AccessibleDescription = item.Name };
        var desc = new Label { Text = item.Details, Dock = DockStyle.Fill, ForeColor = Muted,
            Font = new Font("Segoe UI", 8.1F),
            AutoEllipsis = true, AccessibleDescription = item.Details };
        _toolTips.SetToolTip(name, item.Name);
        _toolTips.SetToolTip(desc, item.Details);
        card.Controls.Add(name, 0, 0);
        card.Controls.Add(desc, 0, 1);
        var toggle = CreateButton(item.Editable ? (item.Enabled ? "Desativar na inicialização" : "Reativar na inicialização") : "Somente leitura",
            item.Editable ? (item.Enabled ? AppServices.Orange : AppServices.Green) : AppServices.Gray, Color.White);
        toggle.Dock = DockStyle.Fill;
        toggle.Margin = Padding.Empty;
        toggle.Enabled = item.Editable;
        toggle.Click += async (_, _) =>
        {
            if (_busy) return;
            string verb = item.Enabled ? "desativar" : "reativar";
            if (!await ConfirmV269Async("Confirmar inicialização",
                $"Deseja {verb} '{item.Name}' na inicialização do Windows?")) return;
            long request = _systemViewGeneration;
            await ExecuteAsync("Alterando inicialização...", async () =>
            {
                await Task.Run(() => StartupManager.Toggle(item));
                Log($"Inicialização: {item.Name} — " + (item.Enabled ? "desativado" : "reativado") + ".");
                var entries = await Task.Run(StartupManager.Read);
                if (!IsCurrentSystemView("startup", request)) return;
                ReplaceSystemCards(new[] { BuildStartupTrayCard() }
                .Concat(entries.Select(BuildStartupCard)).ToArray(), startup: true);
                Status("Inicialização alterada. Efeito no próximo login.");
            });
        };
        card.Controls.Add(toggle, 0, 2);
        return card;
    }

    private void UpdateProcessSamplingCadence()
    {
        if (IsDisposed) return;
        bool visible = Visible && !IsInTray && _nav.SelectedPage == V269Page.Sistema &&
                       _activeSystemView == "memory";
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

    private bool WindowsUpdateViewActive => !IsDisposed && !Disposing && _activeSystemView == "update";

    private async Task ScanWindowsUpdatesInBackgroundAsync(bool force = false)
    {
        if (IsDisposed || Disposing || _betaClose.IsClosing || _windowsUpdateInstalling) return;
        if (_windowsUpdateScanning || (!force && (_windowsUpdateChecked || _lastWindowsUpdateOperation is not null)))
        {
            if (WindowsUpdateViewActive) DrawWindowsUpdates();
            return;
        }

        _windowsUpdateScanning = true;
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

    private bool CanSelectWindowsUpdates => !_busy && !_batchUpdatesActive && !_v269Confirming &&
        !_betaClose.IsClosing && !_windowsUpdateInstalling &&
        !_windowsUpdateScanning && _windowsUpdateChecked && _windowsUpdateRebootRequired is not true;

    private void RefreshWindowsUpdateInstallButton()
    {
        if (_searchWindowsUpdatesButton is { IsDisposed: false } search)
        {
            search.Text = _windowsUpdateScanning ? "Buscando atualizações..." : "Buscar atualizações";
            search.Enabled = WindowsUpdateViewActive && !_busy && !_batchUpdatesActive &&
                !_betaClose.IsClosing && !_windowsUpdateInstalling && !_windowsUpdateScanning;
        }
        if (_systemPanelCards is null) return;
        foreach (var card in _systemPanelCards.Content.Controls.OfType<TableLayoutPanel>())
            foreach (var button in card.Controls.OfType<Button>())
                if (button.Name == "V269InstallWindowsUpdate" && button.Tag is WinUpdateChoice offer)
                    button.Enabled = WindowsUpdateViewActive && CanSelectWindowsUpdates && offer.EulaAccepted;
    }

    private void ShowWindowsUpdateStatus(string title, string summary)
    {
        _systemPanelTitle.Text = title;
        _systemPanelSummary.Text = summary;
        FitSystemSummaryHeight();
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
                ? " Selecione as desejadas e confirme a instalação." : "";
            ShowWindowsUpdateStatus("Windows Update", (_lastWindowsUpdateOperation?.Summary + " " +
                available + guidance + restart).Trim());
            ReplaceSystemCards(_updateCatalog.Select(BuildUpdateCard));
            return;
        }

        string summary = _windowsUpdateScanFailure is string failure
            ? $"Consulta inconclusiva {failure}. Clique em Buscar atualizações para tentar novamente."
            : _lastWindowsUpdateOperation?.Summary ??
                "Nenhuma busca nesta sessão. Clique em Buscar atualizações para consultar.";
        if (_lastWindowsUpdateOperation is { } result && _windowsUpdateScanFailure is null)
        {
            summary += WindowsUpdateOperation.ErrorText(result.Overall.Error);
            if (result.Outcome == WindowsUpdateOutcome.Installation && result.Overall.RebootRequired is null &&
                !result.RequiresRestart)
                summary += " O Windows não confirmou se é necessário reiniciar.";
            if (!result.RequiresRestart)
                summary += " Clique em Buscar atualizações para consultar novamente.";
        }
        ShowWindowsUpdateStatus(_windowsUpdateScanFailure is not null ? "Windows Update · consulta inconclusiva" :
            "Windows Update · resultado", summary + restart);
        ReplaceSystemCards(_lastWindowsUpdateSelection.Select(BuildWindowsUpdateResultCard));
    }

    private Control BuildUpdateCard(WinUpdateChoice update)
    {
        var card = new TableLayoutPanel
        {
            Width = 390, Height = 125, RowCount = 3, ColumnCount = 1,
            BackColor = Color.FromArgb(38, 50, 63), Margin = new Padding(2, 2, 7, 5),
            Padding = new Padding(8, 5, 8, 5)
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        card.Controls.Add(new Label { Text = update.Title, Dock = DockStyle.Fill,
            ForeColor = Ink, Font = new Font("Segoe UI", 9.3F, FontStyle.Bold), AutoEllipsis = false }, 0, 0);
        var details = new Label { Text = (update.Kb.Length > 0 ? update.Kb + " · " : "") +
            (update.Downloaded ? "Baixada" : "Download necessário") +
            (update.EulaAccepted ? " · Licença já aceita" : " · Licença pendente no Windows Update"),
            Dock = DockStyle.Fill, ForeColor = Muted, Font = new Font("Segoe UI", 8.7F) };
        card.Controls.Add(details, 0, 1);
        var toggle = CreateButton("Instalar", AppServices.Blue, Color.White);
        toggle.Name = "V269InstallWindowsUpdate";
        toggle.Tag = update;
        toggle.AccessibleName = "Instalar atualização: " + update.Title;
        toggle.Dock = DockStyle.Fill;
        toggle.Enabled = WindowsUpdateViewActive && CanSelectWindowsUpdates && update.EulaAccepted;
        toggle.Margin = Padding.Empty;
        toggle.Click += async (_, _) =>
        {
            if (!WindowsUpdateViewActive || _nav.SelectedPage != V269Page.Sistema ||
                !CanSelectWindowsUpdates || !update.EulaAccepted ||
                !_updateCatalog.Any(offer => EqualityComparer<WinUpdateChoice>.Default.Equals(offer, update))) return;
            // Cada clique da interface seleciona exatamente uma identidade.
            _updateSelection.Clear();
            _updateSelection.Add(update.Id);
            await InstallSelectedWindowsUpdatesAsync();
        };
        card.Controls.Add(toggle, 0, 2);
        return card;
    }

    private Control BuildWindowsUpdateResultCard(WinUpdateChoice update, int index)
    {
        var result = _lastWindowsUpdateOperation!;
        string details = DescribeWindowsUpdateItem(result, index);
        var card = new TableLayoutPanel
        {
            Width = 390, Height = 92, RowCount = 2, ColumnCount = 1,
            BackColor = Color.FromArgb(38, 50, 63), Margin = new Padding(2, 2, 7, 5),
            Padding = new Padding(8, 5, 8, 5), AccessibleName = update.Title
        };
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        card.Controls.Add(new Label
        {
            Text = update.Title, Dock = DockStyle.Fill, ForeColor = Ink,
            Font = new Font("Segoe UI", 9.3F, FontStyle.Bold), AutoEllipsis = true
        }, 0, 0);
        card.Controls.Add(new Label
        {
            Text = details, Dock = DockStyle.Fill, ForeColor = Muted,
            Font = new Font("Segoe UI", 8.7F)
        }, 0, 1);
        return card;
    }

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
                if (Visible && !IsInTray && _nav.SelectedPage == V269Page.Sistema &&
                    _activeSystemView == "memory")
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
