using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// A confirmação precisa mostrar TODOS os caminhos autorizados.
// Diálogo próprio com rolamento: não truncar nomes nem ocultar itens no alerta.
internal sealed class SevenZipCleanupReviewDialog : Form
{
    internal SevenZipCleanupReviewDialog(SevenZipCleanupPlan plan)
        : this("7-Zip", SevenZipCompleteCleanup.Describe(plan)) { }

    private SevenZipCleanupReviewDialog(string product, string report)
    {
        Text = product + " — revisar limpeza residual";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = AppServices.Background;
        ForeColor = Color.FromArgb(230, 240, 248);
        ClientSize = new Size(765, 525);
        Font = new Font("Segoe UI", 9.5F);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            Padding = new Padding(15, 13, 15, 12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.Controls.Add(new Label
        {
            Text = "Limpeza completa de resíduos exclusivos de " + product + ". Confira os caminhos abaixo. " +
                   "Somente esses itens serão considerados; qualquer mudança bloqueia a operação. " +
                   "A exclusão é permanente e não será executada sem sua autorização.",
            ForeColor = ForeColor, Dock = DockStyle.Fill, Padding = new Padding(4),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        var details = new DarkLogView { Dock = DockStyle.Fill };
        details.SetText(report);
        layout.Controls.Add(details, 0, 1);
        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Padding = new Padding(0, 8, 0, 0)
        };
        var agree = new ModernButton
        {
            Name = "SevenZipCleanupConfirm",
            Text = "Excluir itens exibidos", Width = 190, Height = 34,
            BackColor = AppServices.Red, ForeColor = Color.White,
            DialogResult = DialogResult.Yes
        };
        var cancel = new ModernButton
        {
            Name = "SevenZipCleanupCancel",
            Text = "Preservar", Width = 118, Height = 34,
            BackColor = AppServices.Gray, ForeColor = Color.White,
            DialogResult = DialogResult.Cancel
        };
        controls.Controls.Add(agree);
        controls.Controls.Add(cancel);
        layout.Controls.Add(controls, 0, 2);
        Controls.Add(layout);
        AcceptButton = agree;
        CancelButton = cancel;
    }

    internal static bool Confirm(Form owner, SmallAppSnapshot plan)
    {
        if (!plan.Complete) return false;
        using var dialog = new SevenZipCleanupReviewDialog(
            plan.Product, SmallAppResidualCleanup.Describe(plan));
        return dialog.ShowDialog(owner) == DialogResult.Yes;
    }

    internal static bool Confirm(Form owner, SevenZipCleanupPlan plan)
    {
        if (!plan.Complete) return false;
        using var dialog = new SevenZipCleanupReviewDialog(plan);
        return dialog.ShowDialog(owner) == DialogResult.Yes;
    }
}
