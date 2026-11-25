using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    // Mantem a rolagem somente na pagina. O painel de discos exige uma
    // segunda medicao depois que o Windows entrega as unidades locais.
    private Action? _relayoutCleaning;
    private Action? _relayoutRepair;
    private Action? _relayoutSystem;

    private Action? _fitV269CleanupDiskSection;
    private TableLayoutPanel? _v269CleanupDiskSection;
    private int DiskPanelHeight(int width)
    {
        _fitV269CleanupDiskSection?.Invoke();
        int S(int x) => V269AppTable.Scale(x,_v269Appearance.InterfacePercent,DeviceDpi);
        int reportHeight=CleanupReportHeight(width);
        var section=_v269CleanupDiskSection;
        if(section is not null)
            section.RowStyles[4].Height=_diskCards.Content.Controls.Count>0&&!_refreshingDisks&&
                !_diskSummary.Text.StartsWith("Não foi possível",StringComparison.Ordinal)?0:
                Math.Max(S(12),_diskSummary.Font.Height+2);
        int fixedRows=section is null?S(160):section.Padding.Vertical+
            section.RowStyles.Cast<RowStyle>().Where((row,index)=>index!=3&&index!=5).Sum(row=>(int)Math.Ceiling(row.Height));
        int area=Math.Max(1,width-(section?.Padding.Horizontal??S(26))-_diskCards.Content.Padding.Horizontal);
        int cards=_diskCards.Content.Controls.OfType<V269CleanupDiskCard>()
            .Sum(card=>card.HeightForWidth(area)+S(8));
        return fixedRows + reportHeight + Math.Max(S(6),cards+_diskCards.Content.Padding.Vertical);
    }

    // A pagina inteira rola quando necessario, mas nunca acrescenta barras
    // independentes a conjuntos de ferramentas e a unidades.
    private Control ScrollablePage(Control content, Func<int, int> minimumHeight,
        bool trackCleaning = false, bool trackRepair = false, bool trackSystem = false)
    {
        var host = new DarkScrollableFlow { Dock = DockStyle.Fill, Margin = Padding.Empty,
            IsPageViewport = true, BackColor = AppServices.Background };
        bool themedTools = trackCleaning || trackRepair || trackSystem;
        if (themedTools)
        {
            host.Name = trackCleaning ? "V269CleanupViewport" : trackRepair ? "V269RepairViewport" : "V269SystemViewport";
            host.ApplyCleanupSurface(_v269Appearance.Theme);
        }
        host.Content.Padding = Padding.Empty;
        host.Content.WrapContents = false;
        content.Dock = DockStyle.None;
        content.Margin = Padding.Empty;
        host.Content.Controls.Add(content);
        bool arranging = false;
        void Arrange(string origin)
        {
            if (arranging || host.IsDisposed || (trackSystem && !host.Visible)) return;
            arranging = true;
            try
            {
                // Evita barras que surgiam so porque o quadro interno tinha
                // 3 px de margem tecnica. A largura usa o espaco efetivo,
                // mas nao desativa a rolagem quando o conteudo ultrapassa.
                int scrollbar = (int)Math.Ceiling(14d * host.DeviceDpi / 96d);
                int width = Math.Max(1, host.ClientSize.Width - (themedTools ? 0 : scrollbar));
                int MeasureAt(int available, string pass)
                {
                    host.RecordPageMeasurement(origin, pass, available, content.Width);
                    if(content.Width!=available)content.Width=available;
                    content.PerformLayout();
                    return minimumHeight(available);
                }
                int required=MeasureAt(width,"candidate");
                if(themedTools && required>host.ClientSize.Height)
                {
                    // Só reservar a largura real da barra quando existe overflow.
                    width=Math.Max(1,host.ClientSize.Width-host.CleanupScrollbarWidth);
                    required=MeasureAt(width,"overflow");
                }
                void SetHeight(int minimum, int viewportHeight)
                {
                    int target = Math.Max(viewportHeight, minimum);
                    if (content.Height != target) content.Height = target;
                }
                SetHeight(required, host.ClientSize.Height);
                host.Reflow();
                if (themedTools)
                {
                    // Dock da barra pode atualizar o viewport depois da
                    // medição. Reconciliar com a área efetivamente atribuída
                    // pelo WinForms antes de concluir a transação visual.
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        host.PerformLayout();
                        int actual = host.CleanupViewportWidth;
                        if (actual <= 0 || content.Width == actual) break;
                        SetHeight(MeasureAt(actual,"reconcile"), host.CleanupViewportHeight);
                        host.Reflow();
                    }
                    if (host.Content.Width != host.CleanupViewportWidth) host.Reflow();
                }
            }
            finally { arranging = false; }
        }
        host.SizeChanged += (_, _) => Arrange("SizeChanged");
        if (themedTools)
            host.ToolViewportResized += (_, _) =>
            {
                if (host.Visible && content.Width != host.CleanupViewportWidth) Arrange("ToolViewportResized");
            };
        host.VisibleChanged += (_, _) =>
        {
            if (IsTrayTransitioning || !host.Visible) return;
            Arrange("VisibleChanged");
        };
        host.DpiChangedAfterParent += (_, _) => Arrange("DpiChanged");
        if (trackCleaning) _relayoutCleaning = () => Arrange("Content");
        if (trackRepair) _relayoutRepair = () => Arrange("Content");
        if (trackSystem) _relayoutSystem = () => Arrange("Content");
        Arrange("Initialize");
        return host;
    }

    private static int ToolsHeight(Control tools, int width)
    {
        var host = (DarkScrollableFlow)tools;
        return host.MeasureContentHeight(Math.Max(1, width - 16)) + host.Margin.Vertical;
    }
}
