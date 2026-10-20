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

    private Control BuildMemoryRamDashboard()
    {
        var root = new TableLayoutPanel
        {
            Name = "MemoryRamDashboard", Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 4,
            BackColor = PanelBackground, Padding = new Padding(5, 4, 5, 3)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

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
            try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); }
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
            "Liberada pelo GF: 0 MB"
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
        root.Controls.Add(new Label
        {
            Name = "MemorySafetyNote",
            Text = "Disponível inclui cache reutilizável pelo Windows. Limpeza forçada de outros aplicativos não é realizada. " +
                   "Contador de limpeza: somente ganhos confirmados; por enquanto, nenhuma limpeza automática ativa.",
            ForeColor = Muted, Dock = DockStyle.Fill, AutoEllipsis = false,
            Font = new Font("Segoe UI", 8.2F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 2, 4, 1)
        }, 0, 3);
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
        _memoryCleanedLabel.Text = $"Liberada pelo GF: {_memoryHistory.ConfirmedReclaimedBytes / 1048576d:N0} MB";
        FitSystemSummaryHeight();
    }
}
