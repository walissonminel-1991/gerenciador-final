using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Grade nativa com buffer local: usada pelos drivers, sem enumerar processos.
internal sealed class FlickerFreeProcessGrid : DataGridView
{
    public FlickerFreeProcessGrid()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint, true);
    }
}
