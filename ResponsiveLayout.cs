using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    // A pagina inteira rola: nunca destinar toda a altura aos cartoes e
    // deixar acoes posteriores em uma linha de altura zero.
    private Control ScrollablePage(Control content, Func<int, int> minimumHeight)
    {
        var host = new DarkScrollableFlow { Dock = DockStyle.Fill, Margin = Padding.Empty,
            BackColor = AppServices.Background };
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
                int width = Math.Max(1, host.ClientSize.Width - (int)Math.Ceiling(16 * host.DeviceDpi / 96d));
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
        Arrange();
        return host;
    }

    private static int ToolsHeight(Control tools, int width)
    {
        var host = (DarkScrollableFlow)tools;
        return host.MeasureContentHeight(Math.Max(1, width - 16)) + host.Margin.Vertical;
    }
}
