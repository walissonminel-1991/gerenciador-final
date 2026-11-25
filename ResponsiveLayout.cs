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
        if(trackCleaning)
        {
            host.Name = "V269CleanupViewport";
            host.ApplyCleanupSurface(_v269Appearance.Theme);
        }
        host.Content.Padding = Padding.Empty;
        host.Content.WrapContents = false;
        content.Dock = DockStyle.None;
        content.Margin = Padding.Empty;
        host.Content.Controls.Add(content);
        bool arranging = false;
        void Arrange()
        {
            if (arranging || host.IsDisposed) return;
            arranging = true;
            try
            {
                // Evita barras que surgiam so porque o quadro interno tinha
                // 3 px de margem tecnica. A largura usa o espaco efetivo,
                // mas nao desativa a rolagem quando o conteudo ultrapassa.
                int scrollbar = (int)Math.Ceiling(14d * host.DeviceDpi / 96d);
                int width = Math.Max(1, host.ClientSize.Width - scrollbar);
                // O conteúdo redimensiona os cartões e recalcula a quebra
                // das descrições ao receber a nova largura. Medir depois.
                content.Width = width;
                content.PerformLayout();
                content.Height = Math.Max(host.ClientSize.Height, minimumHeight(width));
                host.Reflow();
            }
            finally { arranging = false; }
        }
        host.SizeChanged += (_, _) => Arrange();
        host.VisibleChanged += (_, _) =>
        {
            if (IsTrayTransitioning) return;
            Arrange();
        };
        host.DpiChangedAfterParent += (_, _) => Arrange();
        if (trackCleaning) _relayoutCleaning = Arrange;
        if (trackRepair) _relayoutRepair = Arrange;
        if (trackSystem) _relayoutSystem = Arrange;
        Arrange();
        return host;
    }

    private static int ToolsHeight(Control tools, int width)
    {
        var host = (DarkScrollableFlow)tools;
        return host.MeasureContentHeight(Math.Max(1, width - 16)) + host.Margin.Vertical;
    }
}
