using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    // Mantem a rolagem somente na pagina. O painel de discos exige uma
    // segunda medicao depois que o Windows entrega as unidades locais.
    private Action? _relayoutCleaning;

    private int DiskPanelHeight(int width)
    {
        int count = _diskCards.Content.Controls.Count;
        int usable = width - 45;
        int columns = usable >= 1050 ? 3 : usable >= 740 ? 2 : 1;
        int rows = Math.Max(1, (Math.Max(1, count) + columns - 1) / columns);
        float scale = Math.Max(1f, DeviceDpi / 96f);
        int fixedRows = 36 + 36 + 20 + 12 + 4 + 6;
        int cards = rows * (111 + 7);
        return (int)Math.Ceiling((fixedRows + cards) * scale);
    }

    // A pagina inteira rola quando necessario, mas nunca acrescenta barras
    // independentes a conjuntos de ferramentas e a unidades.
    private Control ScrollablePage(Control content, Func<int, int> minimumHeight,
        bool trackCleaning = false)
    {
        var host = new DarkScrollableFlow { Dock = DockStyle.Fill, Margin = Padding.Empty,
            IsPageViewport = true, BackColor = AppServices.Background };
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
                content.Size = new Size(width, Math.Max(host.ClientSize.Height, minimumHeight(width)));
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
        Arrange();
        return host;
    }

    private static int ToolsHeight(Control tools, int width)
    {
        var host = (DarkScrollableFlow)tools;
        return host.MeasureContentHeight(Math.Max(1, width - 16)) + host.Margin.Vertical;
    }
}
