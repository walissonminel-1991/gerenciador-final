using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private Button? residualCancel;
    private ColumnStyle? residualCancelColumn;
    private async Task WithResidualCancellationAsync(Func<CancellationToken, Task> action)
    {
        using var cancellation = new CancellationTokenSource();
        _residualScanCts = cancellation;
        if (residualCancel is not null) residualCancel.Visible = true;
        if (residualCancelColumn is not null) residualCancelColumn.Width = 96;
        try { await action(cancellation.Token); }
        catch (OperationCanceledException)
        {
            Log("ANÁLISE PARCIAL — consulta cancelada. Nenhuma ausência ou conclusão total pode ser presumida; nenhum resíduo apagado.");
            Status("Consulta de resíduos cancelada; análise incompleta.");
        }
        finally
        {
            _residualScanCts = null;
            if (residualCancel is not null) residualCancel.Visible = false;
            if (residualCancelColumn is not null) residualCancelColumn.Width = 0;
        }
    }
    private void AddResidualCancelButton(TableLayoutPanel top)
    {
        top.ColumnCount = 3;
        residualCancelColumn = new ColumnStyle(SizeType.Absolute, 0);
        top.ColumnStyles.Add(residualCancelColumn);
        residualCancel = new ModernButton { Name = "ResidualScanCancel", Text = "Cancelar", Dock = DockStyle.Fill,
            Margin = Padding.Empty, Visible = false, BackColor = AppServices.Gray, ForeColor = Color.White,
            AllowMultilineText = false, AccessibleName = "Cancelar consulta de resíduos" };
        residualCancel.Click += (_, _) =>
        {
            _residualScanCts?.Cancel();
            Status("Cancelamento solicitado; aguardando a leitura atual do Windows retornar.");
        };
        top.Controls.Add(residualCancel, 2, 0);
    }
}
