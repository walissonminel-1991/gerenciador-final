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
    private TableLayoutPanel? _systemDetails;
    private TableLayoutPanel? _hardwareGrid;
    private Button? _exportHardwareButton;
    private IReadOnlyList<HardwareGroup> _hardwareGroups = Array.Empty<HardwareGroup>();
    private sealed record HardwareGroup(string Title, string Content);
    private Button? _installSelectedUpdates;
    private readonly HashSet<string> _updateSelection = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<WinUpdateChoice> _updateCatalog = Array.Empty<WinUpdateChoice>();
    private readonly ProcessHistorySampler _processSampler = new();
    private readonly System.Windows.Forms.Timer _processTimer = new() { Interval = 5000 };
    private bool _processSampling;
    private SystemInfoSnapshot? _latestProcessSnapshot;
    private bool _windowsUpdateScanning;
    private bool _windowsUpdateChecked;
    private string? _windowsUpdateScanFailure;
    private string _activeSystemView = "processes";

    private Control BuildIntegratedSystemPanel()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = AppServices.Background,
            ColumnCount = 1, RowCount = 2, Padding = new Padding(8, 8, 8, 6)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 4,
            Margin = Padding.Empty, BackColor = AppServices.Background
        };
        for (int i = 0; i < 4; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddIntegratedAction(actions, 0, "Processos", "Histórico recente de CPU e RAM, atualizado a cada 5 s.", AppServices.Blue,
            async () => { _activeSystemView = "processes"; ShowCachedProcessesIfAvailable(); await RefreshProcessHistoryAsync(); });
        AddIntegratedAction(actions, 1, "Inicialização", "Ative ou desative entradas do seu usuário de modo reversível.", AppServices.Gray,
            async () => { _activeSystemView = "startup"; await RefreshStartupEntriesAsync(); });
        AddIntegratedAction(actions, 2, "Windows Update", "Procure, selecione e instale atualizações oficiais.", AppServices.Green,
            async () => { _activeSystemView = "update"; await ScanWindowsUpdatesAsync(); });
        AddIntegratedAction(actions, 3, "Este computador", "Detalhes de CPU, GPU, RAM, BIOS, placa-mãe e discos.", AppServices.Blue,
            async () =>
            {
                _activeSystemView = "hardware";
                await ExecuteAsync("Consultando hardware...", async () =>
                {
                    var snapshot = await StaWorker.Run(DetailedHardware.Capture);
                    ShowHardwareCompact(snapshot);
                    Status("Hardware consultado");
                    Log("Informações detalhadas de hardware atualizadas.");
                });
            });
        root.Controls.Add(actions, 0, 0);

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = PanelBackground,
            ColumnCount = 1, RowCount = 4, Padding = new Padding(12, 9, 12, 10),
            Margin = new Padding(4, 5, 4, 0)
        };
        _systemDetails = details;
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
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
        _systemPanelSummary.TextAlign = ContentAlignment.TopLeft;
        details.Controls.Add(_systemPanelSummary, 0, 1);
        _installSelectedUpdates = CreateButton("Baixar e instalar selecionadas (0)", AppServices.Green, Color.White);
        _installSelectedUpdates.Dock = DockStyle.Fill;
        _installSelectedUpdates.Margin = new Padding(3, 1, 3, 5);
        _installSelectedUpdates.Visible = false;
        _installSelectedUpdates.Click += async (_, _) => await InstallSelectedWindowsUpdatesAsync();
        _actionControls.Add(_installSelectedUpdates);
        details.Controls.Add(_installSelectedUpdates, 0, 2);
        _systemPanelCards = new DarkScrollableFlow { Dock = DockStyle.Fill, BackColor = PanelBackground };
        _systemPanelCards.Content.BackColor = PanelBackground;
        _systemPanelCards.Content.Padding = new Padding(3, 4, 3, 3);
        _systemPanelCards.Resize += (_, _) => ResizeIntegratedCards();
        var contentHost = new Panel { Dock = DockStyle.Fill, BackColor = PanelBackground, Margin = Padding.Empty };
        contentHost.Controls.Add(_systemPanelCards);
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
        _systemPanelCards.BringToFront();
        details.Controls.Add(contentHost, 0, 3);
        root.Controls.Add(details, 0, 1);

        _processTimer.Tick += async (_, _) =>
        {
            if (_processSampling || IsDisposed) return;
            await RefreshProcessHistoryAsync();
        };
        FormClosed += (_, _) => _processTimer.Dispose();
        return root;
    }

    private void SetSystemView(string view)
    {
        _activeSystemView = view; // Amostragem segue em memória para ter histórico ao abrir a aba.
        if (_systemDetails is null || _installSelectedUpdates is null) return;
        bool showInstall = view == "update";
        _systemDetails.RowStyles[2].Height = showInstall ? 43 : 0;
        _installSelectedUpdates.Visible = showInstall;
        if (_exportHardwareButton is not null) _exportHardwareButton.Visible = view == "hardware" && _hardwareGroups.Count > 0;
        if (_hardwareGrid is not null) _hardwareGrid.Visible = view == "hardware";
        if (_systemPanelCards is not null) _systemPanelCards.Visible = view != "hardware";
        _systemDetails.PerformLayout();
    }

    private void AddIntegratedAction(TableLayoutPanel parent, int column, string caption, string description,
        Color color, Func<Task> action)
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = PanelBackground,
            ColumnCount = 1, RowCount = 2, Margin = new Padding(4, 4, 8, 9),
            Padding = new Padding(7, 5, 7, 4)
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var button = CreateButton(caption, color, Color.White);
        button.Dock = DockStyle.Fill;
        button.Margin = Padding.Empty;
        _actionControls.Add(button);
        _toolTips.SetToolTip(button, description);
        button.Click += async (_, _) =>
        {
            if (_busy) return;
            SetSystemView(caption switch
            {
                "Processos" => "processes", "Inicialização" => "startup", "Windows Update" => "update", _ => "hardware"
            });
            try { await action(); } catch (Exception ex) { ReportError(ex); }
        };
        card.Controls.Add(button, 0, 0);
        card.Controls.Add(new Label
        {
            Text = description, Dock = DockStyle.Fill, ForeColor = Muted,
            Font = new Font("Segoe UI", 8.2F), TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 3, 2, 0)
        }, 0, 1);
        parent.Controls.Add(card, column, 0);
    }

    private void ReplaceSystemCards(IEnumerable<Control> cards)
    {
        if (_systemPanelCards is null || IsDisposed) return;
        var flow = _systemPanelCards.Content;
        flow.SuspendLayout();
        try
        {
            var old = flow.Controls.Cast<Control>().ToArray();
            flow.Controls.Clear();
            foreach (Control control in old) control.Dispose();
            foreach (Control card in cards) flow.Controls.Add(card);
        }
        finally { flow.ResumeLayout(true); }
        ResizeIntegratedCards();
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
        _systemPanelSummary.Text = "Seis grupos sem dados duplicados. Passe o mouse para detalhes ou salve a ficha completa em PNG.";
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
            Width = 390, Height = 133, RowCount = 2, ColumnCount = 1,
            BackColor = Color.FromArgb(38, 50, 63), Margin = new Padding(2, 3, 13, 11),
            Padding = new Padding(12, 8, 12, 7)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var title = new Label
        {
            Text = entry.Title, Dock = DockStyle.Fill,
            ForeColor = Ink, Font = new Font("Segoe UI", 9.6F, FontStyle.Bold),
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft
        };
        var detail = new Label
        {
            Text = entry.Description, Dock = DockStyle.Fill,
            ForeColor = Muted, Font = new Font("Segoe UI", 8.7F),
            AutoEllipsis = true, TextAlign = ContentAlignment.TopLeft,
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
        if (_systemPanelCards is null || _systemPanelCards.IsDisposed) return;
        FlowLayoutPanel flow = _systemPanelCards.Content;
        int space = _systemPanelCards.ClientSize.Width - 42;
        if (space < 280) return;
        int columns = space >= 720 ? 2 : 1;
        int cardWidth = Math.Max(250, space / columns - 17);
        flow.SuspendLayout();
        try
        {
            foreach (Control control in flow.Controls)
                if (control.Width != cardWidth) control.Width = cardWidth;
        }
        finally { flow.ResumeLayout(true); }
        _systemPanelCards.Reflow();
    }

    private async Task RefreshStartupEntriesAsync()
    {
        await ExecuteAsync("Carregando itens de inicialização...", async () =>
        {
            var entries = await Task.Run(StartupManager.Read);
            _systemPanelTitle.Text = "Inicialização · ativar e desativar";
            _systemPanelSummary.Text = "Entradas do seu usuário podem ser alternadas e restauradas. Entradas de todos os usuários são apenas leitura. Serviços e tarefas agendadas não estão incluídos.";
            ReplaceSystemCards(entries.Select(BuildStartupCard).ToArray());
            Status($"{entries.Count} entradas de inicialização consultadas");
            Log($"Inicialização: {entries.Count} entradas consultadas.");
        });
    }

    private Control BuildStartupCard(StartupItem item)
    {
        var card = new TableLayoutPanel
        {
            Width = 390, Height = 144, RowCount = 3, ColumnCount = 1,
            BackColor = Color.FromArgb(38, 50, 63), Margin = new Padding(2, 3, 13, 11),
            Padding = new Padding(10, 8, 10, 8)
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        var name = new Label { Text = item.Name, Dock = DockStyle.Fill, ForeColor = Ink,
            Font = new Font("Segoe UI", 9.6F, FontStyle.Bold), AutoEllipsis = true };
        var desc = new Label { Text = item.Details, Dock = DockStyle.Fill, ForeColor = Muted,
            Font = new Font("Segoe UI", 8.3F), AutoEllipsis = true };
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
            if (DarkConfirmDialog.Confirm(this,
                $"Deseja {verb} '{item.Name}' na inicialização do Windows?",
                "Confirmar inicialização") != DialogResult.Yes) return;
            await ExecuteAsync("Alterando inicialização...", async () =>
            {
                await Task.Run(() => StartupManager.Toggle(item));
                Log($"Inicialização: {item.Name} — " + (item.Enabled ? "desativado" : "reativado") + ".");
                var entries = await Task.Run(StartupManager.Read);
                ReplaceSystemCards(entries.Select(BuildStartupCard).ToArray());
                Status("Inicialização alterada. Efeito no próximo login.");
            });
        };
        card.Controls.Add(toggle, 0, 2);
        return card;
    }

    // Inicializa as amostras de CPU/RAM independentemente da aba visível.
    private void BeginBackgroundProcessSampling()
    {
        if (IsDisposed) return;
        _processTimer.Start();
        _ = RefreshProcessHistoryAsync();
        Log("Monitor de processos iniciado automaticamente (somente memória, a cada 5 segundos).");
    }

    private void ShowCachedProcessesIfAvailable()
    {
        if (_latestProcessSnapshot is not null && !IsDisposed)
            ShowIntegratedSnapshot(_latestProcessSnapshot);
    }

    private async Task ScanWindowsUpdatesAsync()
    {
        await ScanWindowsUpdatesInBackgroundAsync();
    }

    private async Task ScanWindowsUpdatesInBackgroundAsync(bool force = false)
    {
        if (IsDisposed) return;
        if (_windowsUpdateScanning)
        {
            if (_activeSystemView == "update" && _nav.SelectedIndex == 4)
            {
                _systemPanelTitle.Text = "Windows Update · consultando...";
                _systemPanelSummary.Text = "Busca automática em andamento. Nenhuma atualização será instalada sem confirmação.";
            }
            return;
        }
        if (_windowsUpdateChecked && !force)
        {
            if (_activeSystemView == "update" && _nav.SelectedIndex == 4)
                DrawWindowsUpdates();
            return;
        }
        _windowsUpdateScanning = true;
        RenderTitle();
        if (_activeSystemView == "update" && _nav.SelectedIndex == 4)
        {
            _systemPanelTitle.Text = "Windows Update · consultando...";
            _systemPanelSummary.Text = "Consulta oficial do Windows em segundo plano; pode demorar alguns minutos.";
        }
        Log("Windows Update: consulta iniciada automaticamente, sem instalar atualizações.");
        try
        {
            // A API Windows Update Agent roda numa thread STA própria e não bloqueia a janela.
            var found = await WindowsUpdateClient.ScanAsync();
            if (IsDisposed) return;
            _updateCatalog = found;
            _updateSelection.Clear();
            _windowsUpdateChecked = true;
            _windowsUpdateScanFailure = null;
            RenderTitle();
            Log($"Windows Update: {_updateCatalog.Count} atualização(ões) de software encontrada(s).");
            DiagnosticTrace.Write($"WINDOWS_UPDATE.CONSULTA_OK quantidade={found.Count}");
            if (_activeSystemView == "update" && _nav.SelectedIndex == 4)
            {
                DrawWindowsUpdates();
                Status($"Windows Update: {found.Count} atualizações disponíveis");
            }
        }
        catch (Exception ex)
        {
            if (IsDisposed) return;
            _windowsUpdateScanFailure = ex.GetType().Name;
            RenderTitle();
            DiagnosticTrace.Failure("WINDOWS_UPDATE.CONSULTA_FALHA", ex);
            Log("Windows Update: consulta indisponível (" + _windowsUpdateScanFailure + ").");
            if (_activeSystemView == "update" && _nav.SelectedIndex == 4)
            {
                _systemPanelTitle.Text = "Windows Update · consulta indisponível";
                _systemPanelSummary.Text = "O Windows Update não pôde ser consultado. Nenhuma instalação foi iniciada.";
            }
        }
        finally
        {
            _windowsUpdateScanning = false;
            if (!IsDisposed) RenderTitle();
        }
    }

    private void DrawWindowsUpdates()
    {
        _systemPanelTitle.Text = "Windows Update · atualizações disponíveis";
        _systemPanelSummary.Text = _updateCatalog.Count == 0
            ? "Nenhuma atualização de software disponível foi encontrada. Drivers e pacotes opcionais podem não aparecer nesta pesquisa."
            : $"{_updateCatalog.Count} atualização(ões) encontradas. Selecione as desejadas e confirme para baixar e instalar. Pacotes com licença pendente não são instalados automaticamente.";
        if (_installSelectedUpdates is not null)
        {
            _installSelectedUpdates.Text = $"Baixar e instalar selecionadas ({_updateSelection.Count})";
            _installSelectedUpdates.Enabled = _updateSelection.Count > 0;
        }
        ReplaceSystemCards(_updateCatalog.Select(BuildUpdateCard).ToArray());
    }

    private Control BuildUpdateCard(WinUpdateChoice update)
    {
        var card = new TableLayoutPanel
        {
            Width = 390, Height = 144, RowCount = 3, ColumnCount = 1,
            BackColor = Color.FromArgb(38, 50, 63), Margin = new Padding(2, 3, 13, 11),
            Padding = new Padding(11, 7, 11, 7)
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        card.Controls.Add(new Label { Text = update.Title, Dock = DockStyle.Fill,
            ForeColor = Ink, Font = new Font("Segoe UI", 9.3F, FontStyle.Bold), AutoEllipsis = true }, 0, 0);
        var details = new Label { Text = (update.Kb.Length > 0 ? update.Kb + " · " : "") +
            (update.Downloaded ? "Baixada" : "Download necessário") +
            (update.EulaAccepted ? " · Licença já aceita" : " · Licença pendente"),
            Dock = DockStyle.Fill, ForeColor = Muted, Font = new Font("Segoe UI", 8.7F) };
        card.Controls.Add(details, 0, 1);
        var toggle = CreateButton(_updateSelection.Contains(update.Id) ? "✓ Selecionada" : "Selecionar atualização",
            _updateSelection.Contains(update.Id) ? AppServices.Green : AppServices.Blue, Color.White);
        toggle.Dock = DockStyle.Fill;
        toggle.Enabled = update.EulaAccepted;
        toggle.Margin = Padding.Empty;
        toggle.Click += (_, _) =>
        {
            if (!_updateSelection.Add(update.Id)) _updateSelection.Remove(update.Id);
            toggle.Text = _updateSelection.Contains(update.Id) ? "✓ Selecionada" : "Selecionar atualização";
            toggle.BackColor = _updateSelection.Contains(update.Id) ? AppServices.Green : AppServices.Blue;
            toggle.Invalidate();
            if (_installSelectedUpdates is not null)
            {
                _installSelectedUpdates.Text = $"Baixar e instalar selecionadas ({_updateSelection.Count})";
                _installSelectedUpdates.Enabled = _updateSelection.Count > 0;
            }
        };
        card.Controls.Add(toggle, 0, 2);
        return card;
    }

    private async Task InstallSelectedWindowsUpdatesAsync()
    {
        if (_busy || _updateSelection.Count == 0) return;
        if (_updateSelection.Count > 30)
        {
            MessageBox.Show(this, "Selecione no máximo 30 atualizações por operação para facilitar o acompanhamento.",
                "Windows Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        string[] selected;
        try { selected = _updateSelection.Select(s => Guid.Parse(s).ToString("D")).ToArray(); }
        catch (FormatException)
        {
            MessageBox.Show(this, "O Windows retornou um identificador inválido. Procure as atualizações novamente.",
                "Windows Update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (DarkConfirmDialog.Confirm(this,
            $"Deseja instalar {selected.Length} atualização(ões) do Windows? Algumas podem exigir reinicialização.",
            "Confirmar Windows Update") != DialogResult.Yes) return;
        await ExecuteAsync("Instalando atualizações do Windows...", async () =>
        {
            // O trabalhador já existente solicita UAC e mostra as mensagens na janela principal.
            await RunElevatedAsync("wu-install:" + string.Join(",", selected));
            Log("Consulte os resultados acima. Reinicie manualmente se o Windows solicitar.");
            _updateSelection.Clear();
            // Não atualizamos a lista automaticamente para não reiniciar uma consulta custosa após instalar.
            if (_installSelectedUpdates is not null)
            {
                _installSelectedUpdates.Text = "Procurar novamente na aba Windows Update";
                _installSelectedUpdates.Enabled = false;
            }
        });
    }

    private async Task RefreshProcessHistoryAsync()
    {
        if (_processSampling || IsDisposed) return;
        _processSampling = true;
        try
        {
            var snapshot = await Task.Run(_processSampler.TakeSample);
            if (IsDisposed) return;
            _latestProcessSnapshot = snapshot;
            if (_activeSystemView == "processes" && _nav.SelectedIndex == 4)
            {
                ShowIntegratedSnapshot(snapshot);
                Status("Histórico de processos atualizado");
            }
        }
        catch (Exception ex) { if (!IsDisposed) Log("Histórico de processos: " + ex.GetType().Name); }
        finally { _processSampling = false; }
    }
}
