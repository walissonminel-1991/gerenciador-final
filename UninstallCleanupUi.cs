using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private Button? uninstallCancel;
    private ColumnStyle? uninstallCancelColumn;
    private TaskCompletionSource<bool>? uninstallRestore;
    internal bool HasPendingUninstallConfirmation => uninstallRestore is not null;

    private Task<T> OnUninstallUiAsync<T>(Func<Task<T>> action)
    {
        if (IsDisposed || Disposing) return Task.FromCanceled<T>(new CancellationToken(true));
        if (!InvokeRequired) return action();
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginInvoke((Action)(async () =>
        {
            try { result.TrySetResult(await action()); }
            catch (OperationCanceledException) { result.TrySetCanceled(); }
            catch (Exception ex) { result.TrySetException(ex); }
        }));
        return result.Task;
    }
    internal async Task<bool> ConfirmUninstallCleanupAsync(IReadOnlyList<IntegratedResidualPlan> plans, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (IsInTray || !Visible)
        {
            uninstallRestore = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Log("Desinstalação concluída. A decisão sobre os resíduos aguarda a abertura do GF.");
            try { await uninstallRestore.Task.WaitAsync(token); }
            finally { uninstallRestore = null; }
        }
        token.ThrowIfCancellationRequested();
        if (IsDisposed || Disposing) throw new OperationCanceledException(token);
        return UninstallCleanupDialog.Confirm(this, plans);
    }
    private void ResumeUninstallCleanupConfirmation() => uninstallRestore?.TrySetResult(true);
    private void ShowUninstallCancellation(bool visible)
    {
        if (uninstallCancel is not null) uninstallCancel.Visible = visible;
        if (uninstallCancelColumn is not null) uninstallCancelColumn.Width = visible ? 96 : 0;
    }
    private void AddUninstallCancelButton(TableLayoutPanel top)
    {
        top.ColumnCount = 3;
        uninstallCancelColumn = new ColumnStyle(SizeType.Absolute, 0);
        top.ColumnStyles.Add(uninstallCancelColumn);
        uninstallCancel = new ModernButton { Name = "UninstallCancel", Text = "Cancelar", Dock = DockStyle.Fill,
            Margin = Padding.Empty, Visible = false, BackColor = AppServices.Gray, ForeColor = Color.White,
            AllowMultilineText = false, AccessibleName = "Cancelar desinstalação ou limpeza em andamento" };
        uninstallCancel.Click += (_, _) =>
        {
            _uninstallCts?.Cancel();
            Status("Cancelamento solicitado. Aguardando a operação atual encerrar.");
        };
        top.Controls.Add(uninstallCancel, 2, 0);
    }
}
