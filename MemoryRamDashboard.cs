using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Grafico GDI leve (sem WebView, servicos, bibliotecas externas ou consultas a PIDs).
internal sealed class MemoryRamChart : Control
{
    private IReadOnlyList<MemoryRamSample> _samples = Array.Empty<MemoryRamSample>();
    internal int SampleCount => _samples.Count;

    internal MemoryRamChart()
    {
        Name = "MemoryRamChart";
        BackColor = Color.FromArgb(25, 34, 44);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    internal void SetSamples(IReadOnlyList<MemoryRamSample> samples)
    {
        // O desenho nunca guarda mais de 90 pontos e nao copia perfis de usuarios.
        _samples = samples.TakeLast(MemoryRamHistory.Capacity).ToArray();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(9, 11, Math.Max(1, Width - 18), Math.Max(1, Height - 23));
        using var grid = new Pen(Color.FromArgb(48, 66, 83), 1);
        foreach (int percentage in new[] { 0, 25, 50, 75, 100 })
        {
            float y = area.Bottom - area.Height * percentage / 100f;
            g.DrawLine(grid, area.Left, y, area.Right, y);
        }
        if (_samples.Count == 0)
        {
            using var f = new Font("Segoe UI", 9F);
            g.DrawString("Aguardando leitura de RAM...", f,
                Brushes.LightGray, area.Left + 8, area.Top + 8);
            return;
        }

        var points = new PointF[_samples.Count];
        for (int i = 0; i < points.Length; i++)
        {
            float x = area.Left + (points.Length == 1 ? 0 :
                i * area.Width / (float)(points.Length - 1));
            float y = area.Bottom - (float)(_samples[i].UsedPercent / 100d) * area.Height;
            points[i] = new PointF(x, y);
        }
        using var line = new Pen(Color.FromArgb(96, 180, 254), 2.3F);
        if (points.Length > 1) g.DrawLines(line, points);
        else
        {
            using var dot = new SolidBrush(Color.FromArgb(96, 180, 254));
            g.FillEllipse(dot, points[0].X - 2.5F, points[0].Y - 2.5F, 5, 5);
        }
    }
}

internal sealed partial class MainForm
{
    private Control? _memoryDashboardView;
    private MemoryRamChart? _memoryChart;
    private readonly MemoryRamHistory _memoryHistory = new();
    private readonly Label _memoryUsageLabel = new();
    private readonly Label _memoryReadyLabel = new();
    private readonly Label _memoryCleanedLabel = new();
    private readonly Label _memoryCandidatesLabel = new();
    private readonly SafeMemoryOptimizationPolicy _memoryCleanupPolicy = new();
    private readonly AppCacheCleanupCounter _appCacheCounter = new();
    private readonly WindowsLowMemorySignal _memoryPressureSignal = new();
    private Button? _autoMemoryButton;

    private Control BuildMemoryRamDashboard()
    {
        var root = new TableLayoutPanel
        {
            Name = "MemoryRamDashboard", Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 5,
            BackColor = PanelBackground, Padding = new Padding(5, 4, 5, 3)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2, Margin = Padding.Empty
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
        top.Controls.Add(new Label
        {
            Text = "Uso da memória física • histórico desta sessão",
            ForeColor = Ink, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
        }, 0, 0);
        var taskManager = CreateSecondaryButton("Tarefas do Windows");
        taskManager.Dock = DockStyle.Fill;
        taskManager.Margin = new Padding(1, 2, 1, 2);
        taskManager.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(WindowsSystemTools.Resolve("taskmgr.exe")) { UseShellExecute = true }); }
            catch (Exception ex) { ReportError(ex); }
        };
        _actionControls.Add(taskManager);
        top.Controls.Add(taskManager, 1, 0);
        root.Controls.Add(top, 0, 0);

        _memoryChart = new MemoryRamChart { Dock = DockStyle.Fill, Margin = new Padding(2, 1, 2, 3) };
        root.Controls.Add(_memoryChart, 0, 1);

        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 3,
            Margin = Padding.Empty, BackColor = PanelBackground
        };
        for (int c = 0; c < 3; c++)
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
        var values = new[] { _memoryUsageLabel, _memoryReadyLabel, _memoryCleanedLabel };
        string[] defaults =
        {
            "Em uso: aguardando",
            "Disponível: aguardando",
            "Cache GF descartado: 0 KB"
        };
        for (int c = 0; c < values.Length; c++)
        {
            values[c].Name = c switch { 0 => "MemoryUsed", 1 => "MemoryAvailable", _ => "MemoryCleaned" };
            values[c].Text = defaults[c];
            values[c].Dock = DockStyle.Fill;
            values[c].TextAlign = ContentAlignment.MiddleLeft;
            values[c].AutoEllipsis = true;
            values[c].ForeColor = c == 1 ? Color.FromArgb(137, 213, 178) : Ink;
            values[c].Padding = new Padding(5, 3, 1, 0);
            stats.Controls.Add(values[c], c, 0);
        }
        root.Controls.Add(stats, 0, 2);

        var cleanupBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 3,
            BackColor = PanelBackground, Margin = Padding.Empty
        };
        cleanupBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 154));
        cleanupBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152));
        cleanupBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _autoMemoryButton = CreateSecondaryButton("Auto: desligada");
        _autoMemoryButton.Name = "MemoryAutoOptimization";
        _autoMemoryButton.Dock = DockStyle.Fill;
        _autoMemoryButton.Margin = new Padding(2, 2, 5, 2);
        _toolTips.SetToolTip(_autoMemoryButton,
            "Ative a limpeza conservadora de caches internos apenas sob pressão persistente, com 15 min entre ações.");
        _autoMemoryButton.Click += (_, _) =>
        {
            _memoryCleanupPolicy.AutomaticEnabled = !_memoryCleanupPolicy.AutomaticEnabled;
            _autoMemoryButton.Text = _memoryCleanupPolicy.AutomaticEnabled ? "Auto: ativada" : "Auto: desligada";
            UpdateMemoryCacheLabels();
        };
        _actionControls.Add(_autoMemoryButton);
        cleanupBar.Controls.Add(_autoMemoryButton, 0, 0);

        var manual = CreateSecondaryButton("Otimizar cache GF");
        manual.Name = "MemoryManualOptimization";
        manual.Dock = DockStyle.Fill;
        manual.Margin = new Padding(2, 2, 4, 2);
        _toolTips.SetToolTip(manual,
            "Descarta apenas mensagens antigas e amostras do próprio GF. Não reduz memória de outros aplicativos.");
        manual.Click += (_, _) => RunSafeMemoryCleanup(false);
        _actionControls.Add(manual);
        cleanupBar.Controls.Add(manual, 1, 0);

        _memoryCandidatesLabel.Name = "MemoryCandidates";
        _memoryCandidatesLabel.Text = "Cache interno elegível: 0 KB";
        _memoryCandidatesLabel.Dock = DockStyle.Fill;
        _memoryCandidatesLabel.ForeColor = Muted;
        _memoryCandidatesLabel.TextAlign = ContentAlignment.MiddleLeft;
        _memoryCandidatesLabel.AutoEllipsis = true;
        _memoryCandidatesLabel.Padding = new Padding(4, 1, 1, 0);
        cleanupBar.Controls.Add(_memoryCandidatesLabel, 2, 0);
        root.Controls.Add(cleanupBar, 0, 3);

        root.Controls.Add(new Label
        {
            Name = "MemorySafetyNote",
            Text = "Disponível inclui RAM em espera reutilizável. Otimização automática só com autorização e pressão persistente. " +
                   "Cache descartado mede buffers do GF, não memória física recuperada; sem alterar outros processos.",
            ForeColor = Muted, Dock = DockStyle.Fill, AutoEllipsis = false,
            Font = new Font("Segoe UI", 8.2F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 2, 4, 1)
        }, 0, 4);
        FormClosed += (_, _) => _memoryPressureSignal.Dispose();
        _memoryDashboardView = root;
        return root;
    }

    internal void PreviewMemoryRamForTests(MemoryRamSample sample) => ShowMemoryRamSample(sample);

    private void ShowMemoryRamSample(MemoryRamSample sample)
    {
        if (IsDisposed) return;
        _memoryHistory.Add(sample);
        _memoryChart?.SetSamples(_memoryHistory.Samples);
        _systemPanelTitle.Text = "Memória RAM · acompanhamento em tempo real";
        _systemPanelSummary.Text = "Histórico local em segundo plano. Sem ações em processos de terceiros.";
        static string GiB(ulong bytes) =>
            (bytes / 1073741824d).ToString("N2", CultureInfo.CurrentCulture) + " GB";
        _memoryUsageLabel.Text = $"Em uso: {GiB(sample.UsedBytes)} ({sample.UsedPercent:N0}%)";
        _memoryReadyLabel.Text = $"Disponível: {GiB(sample.AvailableBytes)}";
        // Nao inferir limpeza de oscilacoes normais de memoria do Windows.
        UpdateMemoryCacheLabels();
        FitSystemSummaryHeight();
    }

    private void UpdateMemoryCacheLabels()
    {
        _memoryCleanedLabel.Text = $"Cache GF descartado: {_appCacheCounter.DiscardedBufferBytes / 1024d:N0} KB";
        _memoryCandidatesLabel.Text = $"Cache interno elegível: {_log.ReclaimableOldLogBytes() / 1024d:N0} KB";
    }

    private void RunSafeMemoryCleanup(bool automatic)
    {
        if (IsDisposed) return;
        // O único texto descartado é do próprio painel de registro.
        // Eventos técnicos persistidos em arquivo não são alterados.
        long bytes = _log.CompactOldLog();
        _memoryHistory.TrimToRecent(30);
        _memoryChart?.SetSamples(_memoryHistory.Samples);
        _appCacheCounter.Register(bytes);
        _memoryCleanupPolicy.RecordRun(DateTime.UtcNow);
        UpdateMemoryCacheLabels();
        if (!automatic)
            Status(bytes > 0
                ? "Cache interno antigo descartado. RAM física liberada não é mensurada."
                : "Sem cache interno antigo elegível para descarte.");
        DiagnosticTrace.Write($"RAM.CACHE_INTERNO modo={(automatic ? "auto" : "manual")} bytes_descartados={bytes}");
    }

    private void EvaluateAutomaticMemoryCleanup(MemoryRamSample sample)
    {
        if (_memoryCleanupPolicy.ShouldOptimize(sample, _memoryPressureSignal.IsLow(), DateTime.UtcNow))
            RunSafeMemoryCleanup(true);
    }
}
