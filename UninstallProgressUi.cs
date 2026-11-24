using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private string? pendingUninstallSummary;
    private void SetUninstallProgress(ProgramEntry app, string phase, string? details = null)
    {
        var stage = phase switch
        {
            "preparing" => UninstallStage.Preparing, "awaiting_consent" => UninstallStage.AwaitingConsent,
            "working" => UninstallStage.Working, "confirming" => UninstallStage.Confirming,
            "inspecting" => UninstallStage.Inspecting, "awaiting_cleanup" => UninstallStage.AwaitingCleanup,
            "cleaning" => UninstallStage.Cleaning, "finished" or "preserved" => UninstallStage.Finished,
            "partial" => UninstallStage.Partial, "pending" => UninstallStage.Pending, "failed" => UninstallStage.Failed, _ => UninstallStage.Cancelled
        };
        _uninstallOperations.Set(app, stage, details);
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke((Action)(() => { if (!_programList.IsDisposed) _programList.RefreshOperations(); })); }
            catch (InvalidOperationException) { }
        }
        else _programList.RefreshOperations();
    }

    private void DrawUninstallOperation(Graphics graphics, Rectangle area, UninstallOperationSnapshot state)
    {
        Color color = state.Stage == UninstallStage.Failed ? AppServices.Red :
            state.Stage is UninstallStage.Pending or UninstallStage.Partial ? Color.FromArgb(245, 204, 103) :
            state.Stage == UninstallStage.Finished ? Color.FromArgb(120, 222, 165) : Accent;
        TextRenderer.DrawText(graphics, state.Text, _rowInfo,
            new Rectangle(area.X, area.Y - 2, area.Width, area.Height - 4), color,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        var track = new Rectangle(area.X + 1, area.Bottom - 5, area.Width - 2, 4);
        if (track.Width < 4) return;
        using var background = new SolidBrush(Color.FromArgb(20, 30, 42));
        using var fill = new SolidBrush(color);
        graphics.FillRectangle(background, track);
        var clip = graphics.Save();
        try
        {
            graphics.SetClip(track);
            if (state.Active)
            {
                int segment = Math.Max(12, track.Width / 4);
                int offset = _programList.OperationFrame * (track.Width + segment) / 18 - segment;
                graphics.FillRectangle(fill, track.X + offset, track.Y, segment, track.Height);
            }
            else graphics.FillRectangle(fill, track);
        }
        finally { graphics.Restore(clip); }
    }

    internal void ShowUninstallResult(string summary)
    {
        if (IsDisposed || Disposing) return;
        if (IsInTray || !Visible)
        { pendingUninstallSummary = pendingUninstallSummary is null ? summary : pendingUninstallSummary + "\n\n" + summary; return; }
        _nav.PresentPanel(new(V269PanelKind.Result, "Resultado da desinstalação", summary));
    }
    private void ResumeUninstallResult()
    {
        if (pendingUninstallSummary is not string summary) return;
        pendingUninstallSummary = null;
        BeginInvoke((Action)(() => ShowUninstallResult(summary)));
    }
}
