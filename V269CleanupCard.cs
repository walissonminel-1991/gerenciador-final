using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Componente exclusivamente visual: nenhuma autorização é inferida pelo desenho.
internal class V269ToolCard : Panel
{
    private readonly Label _title;
    private readonly V269CleanupButton _command;
    private readonly int _kind;
    private readonly V269RepairGlyph? _repairGlyph;
    private bool _selected;
    private V269AppearanceOptions _options = V269AppearanceOptions.Default;
    private Font? _titleFont;
    private int _optionsDpi;
    private (int Width, int Dpi, int Scale, Font Font, int Height)? _titleMeasurement;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Button Command => _command;

    protected V269ToolCard(string title, string description, int kind, string command,
        string commandGlyph, string namePrefix, V269RepairGlyph? repairGlyph = null)
    {
        _kind = kind;
        _repairGlyph = repairGlyph;
        Name = namePrefix + "Card" + kind;
        AccessibleName = title;
        AccessibleDescription = description;
        AccessibleRole = AccessibleRole.Grouping;
        TabStop = true;
        SetStyle(ControlStyles.Selectable,true);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        _title = new Label { Text = title, AccessibleName = title, BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = false, Margin = Padding.Empty };
        _title.Click += (_, _) => OnClick(EventArgs.Empty);
        _command = new V269CleanupButton(command, commandGlyph)
        {
            Name = namePrefix + "Command" + kind, AccessibleName = repairGlyph.HasValue ? title : command + ": " + title,
            AccessibleDescription = description, Dock = DockStyle.None, Margin = Padding.Empty
        };
        Controls.Add(_title);
        Controls.Add(_command);
        Size = new Size(200, 114);
        ApplyOptions(_options);
    }
    private int S(int n) => V269AppTable.Scale(n, _options.InterfacePercent, DeviceDpi);
    private static Color H(string s) => V269AppTable.H(s);
    internal void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected;
        Invalidate(true);
    }
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        options = options.Normalize();
        if (_titleFont is not null && options == _options && _optionsDpi == DeviceDpi) return;
        _options = options;
        _optionsDpi = DeviceDpi;
        float size = 9.75f * _options.TextPercent / 100f;
        if (_titleFont is null || Math.Abs(_titleFont.Size - size) > .01f)
        {
            var old = _titleFont;
            _titleMeasurement = null;
            _titleFont = new Font("Segoe UI", size, FontStyle.Bold);
            _title.Font = _titleFont;
            old?.Dispose();
        }
        _title.ForeColor = H(_options.Theme switch
        {
            V269Theme.Dark => "#d2d7dc", V269Theme.Claro => "#17374c", _ => "#e4edf6"
        });
        _command.ApplyOptions(_options);
        Arrange();
        Invalidate(true);
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_title is not null && _command is not null) Arrange();
    }
    private int TitleHeight(int width)
    {
        if (!_repairGlyph.HasValue) return Math.Max(S(30), _title.Font.Height * 2 + S(2));
        if (_titleMeasurement is { } cached && cached.Width == width && cached.Dpi == DeviceDpi &&
            cached.Scale == _options.InterfacePercent && ReferenceEquals(cached.Font, _title.Font))
            return cached.Height;
        int height = Math.Max(S(30), TextRenderer.MeasureText(_title.Text, _title.Font,
            new Size(Math.Max(1, width - S(55)), int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Height + S(2));
        _titleMeasurement = (width, DeviceDpi, _options.InterfacePercent, _title.Font, height);
        return height;
    }
    internal int HeightForWidth(int width) => Math.Max(S(114),
        S(12) * 2 + TitleHeight(width) + S(8) + Math.Max(S(30), _command.Font.Height + S(8)));
    private void Arrange()
    {
        int pad = S(12), left = pad + S(31);
        int height = TitleHeight(Width);
        _title.Bounds = new Rectangle(left, pad, Math.Max(1, Width - left - pad), height);
        int required=TextRenderer.MeasureText(_command.Text,_command.Font,Size.Empty,TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding).Width+S(32);
        int buttonWidth = Math.Min(Math.Max(1, Width - 2 * pad), Math.Max(S(80),required));
        int buttonHeight = Math.Max(S(30), _command.Font.Height + S(8));
        _command.Bounds = new Rectangle(Math.Max(0, Width - pad - buttonWidth),
            Math.Max(pad, Height - pad - buttonHeight), buttonWidth, buttonHeight);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 3 || Height < 3) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var (top, bottom, border) = _options.Theme switch
        {
            V269Theme.Dark => _selected ? ("#50565d", "#41464c", "#b4bdc6") : ("#33373b", "#2b2f33", "#555c63"),
            V269Theme.Claro => _selected ? ("#c2e0f5", "#c2e0f5", "#4a9cc9") : ("#e2f2fd", "#e2f2fd", "#8ebad6"),
            _ => _selected ? ("#113048", "#113048", "#5e95bb") : ("#0d2437", "#0b2031", "#30516b")
        };
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = ModernButton.RoundRect(bounds, S(9));
        using var fill = new LinearGradientBrush(bounds, H(top), H(bottom), LinearGradientMode.Vertical);
        using var line = new Pen(H(border));
        g.FillPath(fill, path);
        g.DrawPath(line, path);
        DrawSymbol(g);
        if(Focused&&ShowFocusCues&&Width>8&&Height>8)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-8,Height-8));
    }
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)Focus();base.OnMouseDown(e);}
    private void DrawSymbol(Graphics g)
    {
        if (_repairGlyph is { } repairGlyph)
        {
            V269RepairVisuals.Draw(g, repairGlyph, new Rectangle(S(12),
                S(12) + Math.Max(0, (_title.Height - S(22)) / 2), S(22), S(22)), _options.Theme);
            return;
        }
        var save = g.Save();
        float unit = S(21) / 24f;
        g.TranslateTransform(S(12), S(12) + Math.Max(0, (_title.Height - S(21)) / 2));
        g.ScaleTransform(unit, unit);
        string blue = _options.Theme == V269Theme.Claro ? "#167bbb" : _options.Theme == V269Theme.Dark ? "#65b8ef" : "#75c4ed";
        string teal = _options.Theme == V269Theme.Claro ? "#087d82" : _options.Theme == V269Theme.Dark ? "#55cbb5" : "#6bd6b2";
        string rose = _options.Theme == V269Theme.Claro ? "#ba4a5e" : _options.Theme == V269Theme.Dark ? "#eb8293" : "#d8919f";
        string orange = _options.Theme == V269Theme.Claro ? "#bc592b" : _options.Theme == V269Theme.Dark ? "#ee9d68" : "#e8a784";
        using var p = new Pen(H(_kind == 3 ? rose : _kind == 4 ? teal : _kind == 5 ? orange : blue), 2.05f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (_kind)
        {
            case 0:
                g.DrawPolygon(p, new Point[] { new(3, 6), new(10, 6), new(12, 8), new(21, 8), new(21, 20), new(3, 20) });
                break;
            case 1:
                g.DrawRectangle(p, 3, 4, 18, 14); g.DrawLine(p, 12, 18, 12, 22); g.DrawLine(p, 8, 22, 16, 22);
                break;
            case 2:
                g.DrawArc(p, 4, 4, 16, 16, 195, 150); g.DrawArc(p, 4, 4, 16, 16, 15, 150);
                g.DrawLines(p, new Point[] { new(3, 3), new(3, 9), new(9, 9) });
                g.DrawLines(p, new Point[] { new(21, 21), new(21, 15), new(15, 15) });
                break;
            case 3:
                g.DrawLine(p, 3, 6, 21, 6); g.DrawPolygon(p, new Point[] { new(5, 6), new(19, 6), new(18, 21), new(6, 21) });
                g.DrawLines(p, new Point[] { new(9, 6), new(9, 3), new(15, 3), new(15, 6) });
                g.DrawLine(p, 10, 10, 10, 17); g.DrawLine(p, 14, 10, 14, 17);
                break;
            case 4:
                g.DrawLine(p, 12, 2, 12, 14);
                g.DrawPolygon(p, new Point[] { new(7, 14), new(17, 14), new(20, 22), new(4, 22) });
                g.DrawLine(p, 5, 19, 19, 19);
                foreach (int x in new[] { 8, 12, 16 }) g.DrawLine(p, x, 18, x, 22);
                break;
            default:
                g.DrawRectangle(p, 6, 6, 12, 12); g.DrawRectangle(p, 9, 9, 6, 6);
                foreach (int x in new[] { 9, 15 })
                {
                    g.DrawLine(p, x, 2, x, 6); g.DrawLine(p, x, 18, x, 22);
                    g.DrawLine(p, 2, x, 6, x); g.DrawLine(p, 18, x, 22, x);
                }
                break;
        }
        g.Restore(save);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) _titleFont?.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class V269CleanupCard : V269ToolCard
{
    internal V269CleanupCard(string title, string description, int kind)
        : base(title, description, kind, kind == 4 ? "Abrir" : "Limpar",
            kind == 4 ? "folder" : "brush", "V269Cleanup") { }
}

internal sealed class V269RepairCard : V269ToolCard
{
    internal V269RepairCard(string title, string description, int kind, string command, V269RepairGlyph glyph)
        : base(title, description, kind, command, "play", "V269Repair", glyph) { }
}
