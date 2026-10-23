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
    private void OfferRestartReview(IReadOnlyList<RebootReviewItem> review)
    {
        try
        {
            // Persistir ANTES de solicitar reinicio. Nenhum nome/caminho e
            // registrado em texto aberto; DPAPI protege os dados do usuario.
            RebootResidualReview.Save(review);
            DiagnosticTrace.Write("REINICIO.RESIDUOS estado=pendente quantidade=" + review.Count);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or
            UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            DiagnosticTrace.Write("REINICIO.RESIDUOS estado=persistencia_falhou motivo=" + ex.GetType().Name);
            Status("Revisão após reinício indisponível. Nenhum reinício foi solicitado.");
            return;
        }
        bool accepted = DarkConfirmDialog.Confirm(this,
            "O desinstalador solicitou reinicialização ou um arquivo residual " +
            "permaneceu em uso. Deseja reiniciar o Windows agora?\n\n" +
            "O GF voltará na bandeja após o próximo login e verificará os resíduos, " +
            "sem excluir arquivos automaticamente. Salve seus trabalhos antes de continuar. " +
            "Se não reiniciar agora, a verificação poderá ocorrer quando abrir o GF após uma reinicialização.",
            "Reiniciar para conferir a limpeza") == DialogResult.Yes;
        DiagnosticTrace.Write("REINICIO.RESIDUOS decisao=" + (accepted ? "sim" : "nao"));
        if (!accepted) return;
        try
        {
            StartupTrayRegistration.QueueOneTimeRecovery();
            string command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = "/r /t 60",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null) throw new IOException("Windows nao iniciou comando de reinicio.");
            DiagnosticTrace.Write("REINICIO.RESIDUOS estado=solicitado atraso_segundos=60");
            Status("Reinicialização solicitada para 60 segundos. Salve seu trabalho.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            DiagnosticTrace.Write("REINICIO.RESIDUOS estado=falha_solicitar motivo=" + ex.GetType().Name);
            Status("O Windows não iniciou a reinicialização. A pendência foi preservada.");
        }
    }

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
