using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private V269PanelPresentation? _pendingUninstallResult;
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
        DrawOperationMeter(graphics, track, GfUniversalMeter.Uninstall(state.Stage, _v269Appearance.Theme),
            V269Palette.Border);
    }

    internal void ShowUninstallResult(string summary)
    {
        if (IsDisposed || Disposing) return;
        var presentation=new V269PanelPresentation(V269PanelKind.Result,"Resultado da desinstalação",summary);
        if(_v269AppExecutionActive)_v269AppExecutionResult=presentation;
        PresentUninstallResult(presentation);
    }
    private void PresentUninstallResult(V269PanelPresentation presentation)
    {
        if(IsDisposed||Disposing)return;
        if(IsInTray||!Visible)
        {
            // Um único resultado conserva a identidade usada pelo histórico
            // contextual. Resultados acumulados conservam o texto integral,
            // sem atribuir a combinação a uma operação individual.
            _pendingUninstallResult=_pendingUninstallResult is {} pending
                ?new(V269PanelKind.Result,presentation.Title,pending.Description+"\n\n"+presentation.Description)
                :presentation;
            return;
        }
        _nav.PresentPanel(presentation);
    }
    private void ResumeUninstallResult()
    {
        if(_pendingUninstallResult is not {} presentation)return;
        _pendingUninstallResult=null;
        // A restauração é só apresentação: não recriar a identidade nem
        // atribuir um resultado antigo à eventual operação em andamento.
        BeginInvoke((Action)(()=>PresentUninstallResult(presentation)));
    }
}
