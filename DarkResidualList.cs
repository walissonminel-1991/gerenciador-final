using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed record ResidualDisplayRow(string Key, string Title, string Detail, string Decision, object Target);

// Desenha apenas as linhas visíveis; nunca cria um controle por candidato.
internal sealed class DarkResidualList : Control
{
    private ResidualDisplayRow[] rows = [];
    private readonly DarkVScrollBar scroll = new() { Dock = DockStyle.Right };
    private readonly ToolTip tips = new() { InitialDelay = 400, AutoPopDelay = 12000 };
    private readonly Font titleFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    private readonly Font bodyFont = new("Segoe UI", 8.8f);
    private int selected = -1, tipIndex = -1;
    internal event Action? SelectionChanged;
    internal IReadOnlyList<ResidualDisplayRow> Rows => rows;
    internal ResidualDisplayRow? SelectedRow => selected >= 0 && selected < rows.Length ? rows[selected] : null;
    internal int PaintedRows { get; private set; }
    private int RowHeight => Math.Max(54, (int)(66 * DeviceDpi / 96d));
    internal DarkResidualList()
    {
        Name = "ResidualVirtualList"; BackColor = Color.FromArgb(25, 34, 45); ForeColor = Color.FromArgb(235, 242, 250);
        TabStop = true;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Controls.Add(scroll); scroll.ValueChanged += (_, _) => Invalidate();
        Resize += (_, _) => Metrics();
    }
    internal void SetRows(IEnumerable<ResidualDisplayRow> items, string? selectedKey = null)
    {
        string? old = selectedKey ?? SelectedRow?.Key;
        rows = items.ToArray(); tipIndex = -1; tips.SetToolTip(this, ""); selected = old is null ? -1 : Array.FindIndex(rows, r => r.Key == old);
        if (selected < 0 && rows.Length > 0) selected = 0;
        scroll.Value = 0; Metrics(); SelectRow(selected);
    }
    private void Metrics() { scroll.SetMetrics(rows.Length * RowHeight, Math.Max(1, Height)); Invalidate(); }
    internal void SelectRow(int index)
    {
        selected = index >= 0 && index < rows.Length ? index : -1;
        if (selected >= 0)
        {
            int top = selected * RowHeight;
            if (top < scroll.Value) scroll.Value = top;
            else if (top + RowHeight > scroll.Value + Height) scroll.Value = top + RowHeight - Height;
        }
        Invalidate(); SelectionChanged?.Invoke();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor); PaintedRows = 0;
        int width = Math.Max(1, Width - (scroll.Visible ? scroll.Width : 0));
        int first = Math.Max(0, scroll.Value / RowHeight), last = Math.Min(rows.Length - 1, (scroll.Value + Height) / RowHeight);
        for (int i = first; i <= last; i++)
        {
            PaintedRows++;
            int top = i * RowHeight - scroll.Value;
            var box = new Rectangle(3, top + 3, Math.Max(1, width - 7), RowHeight - 6);
            using var fill = new SolidBrush(i == selected ? Color.FromArgb(42, 66, 92) : Color.FromArgb(35, 46, 60));
            e.Graphics.FillRectangle(fill, box);
            var item = rows[i];
            const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, item.Title, titleFont, new Rectangle(12, top + 7, Math.Max(1, width - 24), 21), ForeColor, flags);
            TextRenderer.DrawText(e.Graphics, item.Decision, bodyFont, new Rectangle(12, top + 27, Math.Max(1, width - 24), 18), Color.FromArgb(158, 205, 245), flags);
            TextRenderer.DrawText(e.Graphics, item.Detail, bodyFont, new Rectangle(12, top + 45, Math.Max(1, width - 24), 18), Color.FromArgb(172, 186, 202), flags);
        }
        if (rows.Length == 0) TextRenderer.DrawText(e.Graphics, "Nenhum grupo nesta seleção.", bodyFont, ClientRectangle,
            ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left && e.X < Width - (scroll.Visible ? scroll.Width : 0)) { Focus(); SelectRow((e.Y + scroll.Value) / RowHeight); } }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); int index = (e.Y + scroll.Value) / RowHeight;
        if (index == tipIndex) return; tipIndex = index;
        tips.SetToolTip(this, index >= 0 && index < rows.Length ? rows[index].Title + "\n" + rows[index].Decision + "\n" + rows[index].Detail : "");
    }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); scroll.Value -= Math.Sign(e.Delta) * RowHeight * 3; }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        int jump = Math.Max(1, Height / RowHeight);
        int next = e.KeyCode switch { Keys.Down => selected + 1, Keys.Up => selected - 1, Keys.PageDown => selected + jump,
            Keys.PageUp => selected - jump, Keys.Home => 0, Keys.End => rows.Length - 1, _ => selected };
        if (next != selected) { SelectRow(Math.Clamp(next, 0, Math.Max(0, rows.Length - 1))); e.Handled = true; }
        base.OnKeyDown(e);
    }
    protected override void Dispose(bool disposing)
    { if (disposing) { rows = []; selected = -1; } base.Dispose(disposing); if (disposing) { tips.Dispose(); titleFont.Dispose(); bodyFont.Dispose(); } }
}
