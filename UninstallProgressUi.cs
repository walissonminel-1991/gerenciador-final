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
            "pending" => UninstallStage.Pending, "failed" => UninstallStage.Failed, _ => UninstallStage.Cancelled
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
            state.Stage == UninstallStage.Pending ? Color.FromArgb(245, 204, 103) :
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
        using var dialog = new UninstallResultDialog(summary);
        dialog.ShowDialog(this);
    }
    private void ResumeUninstallResult()
    {
        if (pendingUninstallSummary is not string summary) return;
        pendingUninstallSummary = null;
        BeginInvoke((Action)(() => ShowUninstallResult(summary)));
    }
}

internal sealed class UninstallResultDialog : Form
{
    internal UninstallResultDialog(string summary)
    {
        Name = "UninstallResult"; Text = "Resultado da desinstalação";
        BackColor = AppServices.Background; ForeColor = Color.White;
        Font = new Font("Segoe UI", 10); ClientSize = new Size(620, 430);
        AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Padding = new Padding(16), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        var text = new DarkLogView { Name = "UninstallResultText", Dock = DockStyle.Fill };
        text.SetText(summary); root.Controls.Add(text, 0, 0);
        var close = new ModernButton { Name = "UninstallResultClose", Text = "OK", Width = 110,
            Dock = DockStyle.Right, DialogResult = DialogResult.OK, BackColor = AppServices.Blue, ForeColor = Color.White,
            AllowMultilineText = false, Margin = new Padding(0, 8, 0, 0) };
        root.Controls.Add(close, 0, 1); Controls.Add(root);
        AcceptButton = close; CancelButton = close;
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int dark = 1; if (NativeMethods.DwmSetWindowAttribute(Handle, 20, ref dark, 4) != 0)
                _ = NativeMethods.DwmSetWindowAttribute(Handle, 19, ref dark, 4); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Bounds = UninstallCleanupDialog.FitBounds(Bounds, Screen.FromControl(Owner ?? this).WorkingArea);
    }
}
