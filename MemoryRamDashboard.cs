using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Grafico GDI leve (sem WebView, servicos, bibliotecas externas ou consultas a PIDs).
internal sealed class MemoryRamChart : Control
{
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    internal void ApplyOptions(V269AppearanceOptions options){_options=options.Normalize();BackColor=V269AppTable.EmptySurface(_options.Theme);Invalidate();}
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
        var palette=V269VisualTokens.ForTheme(_options.Theme);
        using var grid = new Pen(palette.Border, 1);
        foreach (int percentage in new[] { 0, 25, 50, 75, 100 })
        {
            float y = area.Bottom - area.Height * percentage / 100f;
            g.DrawLine(grid, area.Left, y, area.Right, y);
        }
        if (_samples.Count == 0)
        {
            using var f = new Font("Segoe UI", 9F);
            using var text=new SolidBrush(palette.SecondaryText);
            g.DrawString("Aguardando leitura de RAM...", f,text,area.Left+8,area.Top+8);
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
        using var line = new Pen(palette.Accent, 2.3F);
        if (points.Length > 1) g.DrawLines(line, points);
        else
        {
            using var dot = new SolidBrush(palette.Accent);
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
    private MemoryTrimTarget? _memoryAuthorizedTarget;
    private int _memoryTrimOperations;
    private WindowsMemoryDetails? _windowsMemoryDetails;

    private double? _v269RamUsagePercent;
    private GfDynamicProgressBar? _v269RamMeter;

    private void RefreshV269RamMeter(bool appearanceChanged = false)
    {
        if (_v269RamMeter is not { IsDisposed: false } meter) return;
        if (appearanceChanged)
        {
            meter.BackColor = V269VisualTokens.ForTheme(_v269Appearance.Theme).Card;
            meter.Configure(_v269Appearance.Theme, CanAnimateV269);
        }
        meter.Present(GfMeterScale.Frame(GfMeterKind.Usage, _v269RamUsagePercent, _v269Appearance.Theme),
            _v269RamUsagePercent is double usage ? $"{usage:0.#}% de memória física em uso" : "Uso da memória indisponível");
    }
    private Control BuildMemoryRamDashboard()
    {
        var root=new V269CleanupSection
        {
            Name="MemoryRamDashboard",Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,
            Margin=new Padding(0,0,0,7),Padding=new Padding(12),BackColor=Color.Transparent
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(int height in new[]{30,27,10,49,29,38})root.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        var header=new V269ModuleHeading("Memória RAM","memory"){Dock=DockStyle.Fill};
        header.Click+=(_,_)=>SetSystemView("memory");root.Controls.Add(header,0,0);
        var top=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,BackColor=Color.Transparent};
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,53));top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,47));
        var text=new Label{Text="Uso da memória física nesta sessão",ForeColor=Muted,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};
        top.Controls.Add(text,0,0);
        _memoryUsageLabel.Name="MemoryUsed";_memoryUsageLabel.Text="Aguardando leitura";_memoryUsageLabel.ForeColor=Ink;_memoryUsageLabel.Dock=DockStyle.Fill;_memoryUsageLabel.TextAlign=ContentAlignment.MiddleRight;
        top.Controls.Add(_memoryUsageLabel,1,0);root.Controls.Add(top,0,1);
        _v269RamMeter = new GfDynamicProgressBar
        {
            Name = "V269RamUsageMeter", AccessibleName = "Uso da memória física",
            Dock = DockStyle.Fill, Margin = new Padding(0,1,0,1)
        };
        RefreshV269RamMeter(appearanceChanged: true);
        root.Controls.Add(_v269RamMeter,0,2);
        _memoryChart=new MemoryRamChart{Dock=DockStyle.Fill,Margin=new Padding(0,3,0,3)};
        root.Controls.Add(_memoryChart,0,3);
        var stats=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty,BackColor=Color.Transparent};
        stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));
        _memoryReadyLabel.Name="MemoryAvailable";_memoryReadyLabel.Text="Disponível: aguardando";_memoryReadyLabel.Dock=DockStyle.Fill;_memoryReadyLabel.ForeColor=Muted;
        _memoryCandidatesLabel.Name="MemoryCandidates";_memoryCandidatesLabel.Text="Cache do Windows: aguardando";_memoryCandidatesLabel.Dock=DockStyle.Fill;_memoryCandidatesLabel.ForeColor=Muted;
        stats.Controls.Add(_memoryReadyLabel,0,0);stats.Controls.Add(_memoryCandidatesLabel,1,0);root.Controls.Add(stats,0,4);
        var commands=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty,BackColor=Color.Transparent};
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));commands.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,126));
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,78));commands.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,47));
        _memoryCleanedLabel.Name="MemoryCleaned";_memoryCleanedLabel.Text="Cache GF: 0 KB";_memoryCleanedLabel.ForeColor=Muted;_memoryCleanedLabel.Dock=DockStyle.Fill;
        commands.Controls.Add(_memoryCleanedLabel,0,0);
        var manual=new V269CleanupButton("Aliviar RAM","brush",true){Name="MemoryManualOptimization",Dock=DockStyle.Fill,Margin=new Padding(2,2,2,2)};
        manual.Click+=async (_,_)=>{SetSystemView("memory");await RunUserMemoryOptimization();};
        _actionControls.Add(manual);commands.Controls.Add(manual,1,0);
        commands.Controls.Add(new Label{Text="Automático",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=Muted},2,0);
        _autoMemoryButton=new V269SystemSwitch("MemoryAutoOptimization","Controle automático da RAM"){Dock=DockStyle.None,Anchor=AnchorStyles.Left,Size=new Size(43,24)};
        ((V269SystemSwitch)_autoMemoryButton).SetState(_memoryCleanupPolicy.AutomaticEnabled);
        _autoMemoryButton.Click+=(_,_)=>{SetSystemView("memory");_memoryCleanupPolicy.AutomaticEnabled=!_memoryCleanupPolicy.AutomaticEnabled;
            ((V269SystemSwitch)_autoMemoryButton).SetState(_memoryCleanupPolicy.AutomaticEnabled);UpdateMemoryCacheLabels();};
        _actionControls.Add(_autoMemoryButton);commands.Controls.Add(_autoMemoryButton,3,0);root.Controls.Add(commands,0,5);
        // A ferramenta nativa e as informações completas permanecem acessíveis pelo menu de contexto.
        var menu=new ContextMenuStrip();
        var taskManager=menu.Items.Add("Abrir Gerenciador de Tarefas");
        taskManager.Click+=(_,_)=>{try{Process.Start(new ProcessStartInfo(WindowsSystemTools.Resolve("taskmgr.exe")){UseShellExecute=true});}catch(Exception ex){ReportError(ex);}};
        root.ContextMenuStrip=menu;_memoryChart.ContextMenuStrip=menu;
        root.Disposed+=(_,_)=>menu.Dispose();
        bool? compact=null;
        void Fit()
        {
            if(root.Width<=0)return;
            int S(int n)=>V269AppTable.Scale(n,_v269Appearance.InterfacePercent,root.DeviceDpi);
            bool small=root.ClientSize.Width-root.Padding.Horizontal<S(400);
            if(compact!=small)
            {
                compact=small;
                commands.SuspendLayout();
                try
                {
                    commands.SetCellPosition(_memoryCleanedLabel,new TableLayoutPanelCellPosition(0,0));
                    var button=manual;
                    if(small)
                    {
                        commands.SetColumnSpan(_memoryCleanedLabel,4);
                        commands.SetCellPosition(button,new TableLayoutPanelCellPosition(0,1));
                        commands.SetColumnSpan(button,2);
                        commands.SetCellPosition(commands.Controls.OfType<Label>().Single(x=>x.Text=="Automático"),new TableLayoutPanelCellPosition(2,1));
                        commands.SetCellPosition(_autoMemoryButton,new TableLayoutPanelCellPosition(3,1));
                    }
                    else
                    {
                        commands.SetColumnSpan(_memoryCleanedLabel,1);commands.SetColumnSpan(button,1);
                        commands.SetCellPosition(button,new TableLayoutPanelCellPosition(1,0));
                        commands.SetCellPosition(commands.Controls.OfType<Label>().Single(x=>x.Text=="Automático"),new TableLayoutPanelCellPosition(2,0));
                        commands.SetCellPosition(_autoMemoryButton,new TableLayoutPanelCellPosition(3,0));
                    }
                    commands.RowCount=small?2:1;commands.RowStyles.Clear();
                    for(int i=0;i<commands.RowCount;i++)commands.RowStyles.Add(new RowStyle(SizeType.Percent,100f/commands.RowCount));
                }
                finally{commands.ResumeLayout(true);}
            }
            root.RowStyles[0].Height=S(30);root.RowStyles[1].Height=S(small?42:27);root.RowStyles[2].Height=S(10);
            root.RowStyles[3].Height=S(49);root.RowStyles[4].Height=S(small?50:29);root.RowStyles[5].Height=S(small?68:38);
        }
        root.SizeChanged+=(_,_)=>Fit();root.Tag=(Action)Fit;Fit();
        FormClosed+=(_,_)=>_memoryPressureSignal.Dispose();_memoryDashboardView=root;return root;
    }

    internal void PreviewMemoryRamForTests(MemoryRamSample sample) => ShowMemoryRamSample(sample);

    private void ShowMemoryRamSample(MemoryRamSample sample)
    {
        if (IsDisposed) return;
        _memoryHistory.Add(sample);
        _windowsMemoryDetails = WindowsMemoryDetailsReader.Capture();
        _memoryChart?.SetSamples(_memoryHistory.Samples);
        _v269RamUsagePercent = sample.TotalBytes > 0 && GfMeterScale.Valid(sample.UsedPercent, GfMeterKind.Usage)
            ? sample.UsedPercent : null;
        RefreshV269RamMeter();
        static string GiB(ulong bytes) =>
            (bytes / 1073741824d).ToString("N2", CultureInfo.CurrentCulture) + " GB";
        _memoryUsageLabel.Text = $"{GiB(sample.UsedBytes)} / {GiB(sample.TotalBytes)} ({sample.UsedPercent:N0}%)";
        _memoryReadyLabel.Text = $"Disponível: {GiB(sample.AvailableBytes)}";
        // Nao inferir limpeza de oscilacoes normais de memoria do Windows.
        UpdateMemoryCacheLabels();
    }

    private void UpdateMemoryCacheLabels()
    {
        _memoryCleanedLabel.Text = $"Ajustes de RAM: {_memoryTrimOperations} · Cache GF: {_appCacheCounter.DiscardedBufferBytes / 1024d:N0} KB";
        if (_windowsMemoryDetails is { } info)
        {
            string cache = (info.CacheBytes / 1073741824d).ToString("N1", CultureInfo.CurrentCulture);
            string committed = (info.CommittedBytes / 1073741824d).ToString("N1", CultureInfo.CurrentCulture);
            _memoryCandidatesLabel.Text = $"Cache: {cache} GB";
            _memoryCandidatesLabel.AccessibleDescription=$"Memória comprometida: {committed} GB. Cache inclui dados em espera, reutilizáveis pelo Windows. Memória comprometida não é igual à memória física ocupada.";
        }
        else
        {
            _memoryCandidatesLabel.Text = "Detalhes do cache do Windows não disponíveis.";
            _memoryCandidatesLabel.AccessibleDescription = _memoryCandidatesLabel.Text;
        }
        if (_autoMemoryButton is not null)
            _autoMemoryButton.AccessibleDescription = _memoryAuthorizedTarget is null
                ? "Escolha um aplicativo em Aliviar RAM para autorizar ajustes automáticos nesta sessão."
                : "Automático autorizado somente para: " + _memoryAuthorizedTarget.Name;
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

    private async Task RunUserMemoryOptimization()
    {
        if (IsDisposed || _busy) return;
        var candidate = SelectiveMemoryTrim.ChooseCandidate();
        if (candidate is null)
        {
            Status("Nenhum aplicativo em segundo plano adequado para aliviar RAM agora.");
            PresentV269ModuleResult(V269Page.Sistema,"Aliviar RAM","Nenhum aplicativo adequado para aliviar RAM agora.",false);
            return;
        }
        if (!await ConfirmV269Async("Aliviar RAM de aplicativo em segundo plano",
            "Permitir que o Windows reduza temporariamente a RAM de " + candidate.Name +
            " (PID " + candidate.Pid + ")?\n\n" + candidate.Executable +
            "\n\nO aplicativo não será fechado e arquivos não serão apagados. " +
            "Ele poderá ficar mais lento ao reutilizar dados. Se o automático estiver ligado, " +
            "somente este mesmo processo poderá ser ajustado sob pressão de memória nesta sessão."))
            return;
        _memoryAuthorizedTarget = candidate;
        bool changed = SelectiveMemoryTrim.TryTrimAuthorized(candidate, out string message);
        if (changed) _memoryTrimOperations++;
        _memoryCleanupPolicy.RecordRun(DateTime.UtcNow);
        Status(message);
        PresentV269ModuleResult(V269Page.Sistema,"Aliviar RAM",message,changed);
        DiagnosticTrace.Write("RAM.PROCESSO_ESCOLHIDO resultado=" + (changed ? "ok" : "indisponivel"));
        UpdateMemoryCacheLabels();
    }

    private void EvaluateAutomaticMemoryCleanup(MemoryRamSample sample)
    {
        if (!_memoryCleanupPolicy.ShouldOptimize(sample, _memoryPressureSignal.IsLow(), DateTime.UtcNow)) return;
        // Nunca descobre novos processos nem atinge programas nao autorizados automaticamente.
        if (_memoryAuthorizedTarget is not null)
        {
            bool ok = SelectiveMemoryTrim.TryTrimAuthorized(_memoryAuthorizedTarget, out _);
            if (ok) _memoryTrimOperations++;
            DiagnosticTrace.Write("RAM.PROCESSO_AUTORIZADO resultado=" + (ok ? "ok" : "indisponivel"));
            if (!ok) _memoryAuthorizedTarget = null;
        }
        RunSafeMemoryCleanup(true);
    }
}
