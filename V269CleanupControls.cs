using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal static class V269CleanupGlyph
{
    internal static void Draw(Graphics g, string kind, Rectangle bounds, V269Theme theme, Color? inkOverride = null)
    {
        var save = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(bounds.Left, bounds.Top);
        g.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
        string blue = theme == V269Theme.Claro ? "#167bbb" : theme == V269Theme.Dark ? "#65b8ef" : "#75c4ed";
        string teal = theme == V269Theme.Claro ? "#087d82" : theme == V269Theme.Dark ? "#55cbb5" : "#6bd6b2";
        string rose=theme==V269Theme.Claro?"#ba4a5e":theme==V269Theme.Dark?"#eb8293":"#d8919f";
        string orange=theme==V269Theme.Claro?"#bc592b":theme==V269Theme.Dark?"#ee9d68":"#e8a784";
        using var pen = new Pen(inkOverride ?? V269AppTable.H(kind is "brush" or "shield" ? teal : kind=="trash"?rose:kind=="cpu"?orange:blue), 2.05f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (kind)
        {
            case "brush":
                g.DrawLine(pen, 12, 2, 12, 14);
                g.DrawPolygon(pen, new Point[] { new(7,14), new(17,14), new(20,22), new(4,22) });
                g.DrawLine(pen, 5,19,19,19);
                foreach (int x in new[] {8,12,16}) g.DrawLine(pen,x,18,x,22);
                break;
            case "folder":
                g.DrawPolygon(pen,new Point[] {new(3,6),new(10,6),new(12,8),new(21,8),new(21,20),new(3,20)});
                break;
            case "monitor":
                g.DrawRectangle(pen,3,4,18,14);g.DrawLine(pen,12,18,12,22);g.DrawLine(pen,8,22,16,22);
                break;
            case "trash":
                g.DrawLine(pen,3,6,21,6);g.DrawPolygon(pen,new Point[]{new(5,6),new(19,6),new(18,21),new(6,21)});
                g.DrawLines(pen,new Point[]{new(9,6),new(9,3),new(15,3),new(15,6)});
                g.DrawLine(pen,10,10,10,17);g.DrawLine(pen,14,10,14,17);
                break;
            case "cpu":
                g.DrawRectangle(pen,6,6,12,12);g.DrawRectangle(pen,9,9,6,6);
                foreach(int x in new[]{9,15}){g.DrawLine(pen,x,2,x,6);g.DrawLine(pen,x,18,x,22);g.DrawLine(pen,2,x,6,x);g.DrawLine(pen,18,x,22,x);}
                break;
            case "power":
                g.DrawArc(pen,4,4,16,16,-50,280);g.DrawLine(pen,12,2,12,12);break;
            case "bolt":
                g.DrawPolygon(pen,new Point[]{new(13,2),new(4,14),new(11,14),new(10,22),new(20,10),new(13,10)});break;
            case "memory":
                g.DrawRectangle(pen,2,6,20,11);
                foreach(int x in new[]{5,10,15})g.DrawRectangle(pen,x,9,3,5);
                foreach(int x in new[]{5,9,13,17,21})g.DrawLine(pen,x,17,x,20);break;
            case "clock":
                g.DrawEllipse(pen,3,3,18,18);g.DrawLines(pen,new Point[]{new(12,6),new(12,12),new(16,14)});break;
            case "document":
                g.DrawPolygon(pen,new Point[]{new(5,3),new(14,3),new(20,9),new(20,21),new(5,21)});
                g.DrawLines(pen,new Point[]{new(14,3),new(14,9),new(20,9)});g.DrawLine(pen,8,13,17,13);g.DrawLine(pen,8,17,17,17);break;
            case "apps":
                for(int x=3;x<=14;x+=11)for(int y=3;y<=14;y+=11)g.DrawRectangle(pen,x,y,7,7);break;
            case "lock":
                g.DrawArc(pen,7,2,10,12,180,180);g.DrawRectangle(pen,5,10,14,11);g.DrawLine(pen,12,14,12,17);break;
            case "download":
                g.DrawLine(pen,12,2,12,16);g.DrawLines(pen,new Point[]{new(6,10),new(12,16),new(18,10)});
                g.DrawLines(pen,new Point[]{new(3,16),new(3,21),new(21,21),new(21,16)});break;
            case "check":
                g.DrawLines(pen,new Point[]{new(4,12),new(9,17),new(20,6)});break;
            case "search":
                g.DrawEllipse(pen,3,3,15,15); g.DrawLine(pen,16,16,21,21);
                break;
            case "shield":
                g.DrawPolygon(pen,new Point[] {new(12,2),new(20,5),new(20,13),new(17,18),new(12,22),new(7,18),new(4,13),new(4,5)});
                g.DrawLines(pen,new Point[]{new(9,12),new(11,14),new(15,10)});
                break;
            case "refresh":
                g.DrawArc(pen,4,4,16,16,195,150);g.DrawArc(pen,4,4,16,16,15,150);
                g.DrawLines(pen,new Point[]{new(3,3),new(3,9),new(9,9)});
                g.DrawLines(pen,new Point[]{new(21,21),new(21,15),new(15,15)});
                break;
            default:
                g.DrawRectangle(pen,2,5,20,14);g.DrawLine(pen,4,15,20,15);g.DrawLine(pen,4,17,6,17);
                break;
        }
        g.Restore(save);
    }
}

internal sealed class V269CleanupButton : Button
{
    private readonly string _glyph;
    private readonly bool _primary;
    private V269AppearanceOptions _options = V269AppearanceOptions.Default;
    private Font? _font;
    internal V269CleanupButton(string caption, string glyph, bool primary = false)
    {
        _glyph = glyph; _primary = primary; Text = caption; AccessibleName = caption;
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ApplyOptions(_options);
    }
    private int S(int x) => V269AppTable.Scale(x, _options.InterfacePercent, DeviceDpi);
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        _options = options.Normalize();
        float size = 9f * _options.TextPercent / 100f;
        if (_font is null || Math.Abs(_font.Size - size) > .01f)
        {
            var old = _font; _font = new Font("Segoe UI", size, FontStyle.Bold);
            Font = _font; old?.Dispose();
        }
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 3 || Height < 3) return;
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var (top, bottom, border, text) = _options.Theme switch
        {
            V269Theme.Claro => _primary ? ("#2a8ac7","#13649e","#1c6798","#ffffff") : ("#d8eaf7","#d8eaf7","#89abc3","#173d59"),
            V269Theme.Dark => _primary ? ("#6b747e","#515961","#b7c0c8","#e4e7ea") : ("#3a3f45","#3a3f45","#68717a","#d1d6db"),
            _ => _primary ? ("#205f95","#205f95","#6ea7cf","#eff8ff") : ("#142f44","#142f44","#416783","#dfedf7")
        };
        Color H(string s) => V269AppTable.H(s);
        var bounds = new Rectangle(0,0,Width-1,Height-1);
        using var shape = ModernButton.RoundRect(bounds,S(8));
        using var fill = new LinearGradientBrush(bounds,H(top),H(bottom),LinearGradientMode.Vertical);
        using var outline = new Pen(H(border));
        g.FillPath(fill,shape);g.DrawPath(outline,shape);
        int icon = S(15);
        int measured = TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width;
        int left = Math.Max(S(5),(Width-icon-S(6)-measured)/2);
        if (_glyph == "play")
        {
            Color ink = !Enabled ? V269VisualTokens.ForTheme(_options.Theme).SecondaryText :
                _options.Theme == V269Theme.Dark ? H("#b998f2") : H(text);
            int y = (Height - icon) / 2;
            using var play = new Pen(ink, Math.Max(1f, S(1))) { LineJoin = LineJoin.Round };
            g.DrawPolygon(play, new Point[] { new(left + S(4), y + S(3)),
                new(left + icon - S(3), y + icon / 2), new(left + S(4), y + icon - S(3)) });
        }
        else if(Enabled) V269CleanupGlyph.Draw(g,_glyph,new Rectangle(left,(Height-icon)/2,icon,icon),_options.Theme,
            _primary && _glyph=="search" && Text=="Analisar limpeza" && _options.Theme!=V269Theme.Dark ? H(text) : null);
        else
        {
            using var muted = new Pen(V269VisualTokens.ForTheme(_options.Theme).SecondaryText);
            g.DrawRectangle(muted,left,(Height-icon)/2,icon,icon);
        }
        int x=left+icon+S(6);
        TextRenderer.DrawText(g,Text,Font,new Rectangle(x,0,Math.Max(1,Width-x-S(3)),Height),
            Enabled?H(text):V269VisualTokens.ForTheme(_options.Theme).SecondaryText,
            TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Math.Max(1,Width-6),Math.Max(1,Height-6)));
    }
    protected override void Dispose(bool disposing){if(disposing)_font?.Dispose();base.Dispose(disposing);}
}

internal sealed class V269CleanupSection : TableLayoutPanel
{
    private V269AppearanceOptions _options = V269AppearanceOptions.Default;
    internal void ApplyOptions(V269AppearanceOptions options){_options=options.Normalize();Invalidate(true);}
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        if(Width<3||Height<3)return;
        var (top,bottom,border)=_options.Theme switch
        {
            V269Theme.Claro=>("#eef8ff","#dceefa","#8db1ca"),
            V269Theme.Dark=>("#303438","#282c30","#565c62"),
            _=>("#0d3047","#0b2237","#25445f")
        };
        var bounds=new Rectangle(0,0,Width-1,Height-1);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var path=ModernButton.RoundRect(bounds,V269AppTable.Scale(11,_options.InterfacePercent,DeviceDpi));
        using var fill=new LinearGradientBrush(bounds,V269AppTable.H(top),V269AppTable.H(bottom),LinearGradientMode.Vertical);
        using var line=new Pen(V269AppTable.H(border));
        e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(line,path);
    }
}

internal sealed class V269CleanupDiskCard : Panel
{
    private readonly Label _name,_usage,_free;
    private readonly V269CleanupButton _command;
    private readonly double? _fraction;
    private readonly ulong? _usedBytes,_capacityBytes;
    private bool _selected;
    internal void SetSelected(bool selected){if(_selected==selected)return;_selected=selected;Invalidate(true);}
    private Font? _nameFont,_dataFont;
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Button Command=>_command;
    internal V269CleanupDiskCard(string name,string usage,string free,double fraction)
        : this(name,usage,free,double.IsFinite(fraction)&&fraction>=0&&fraction<=1?fraction:null,null,null) { }
    internal V269CleanupDiskCard(string name,string usage,string free,ulong? usedBytes,ulong? capacityBytes)
        : this(name,usage,free,null,usedBytes,capacityBytes) { }
    private V269CleanupDiskCard(string name,string usage,string free,double? fraction,ulong? usedBytes,ulong? capacityBytes)
    {
        Name="V269CleanupDiskCard";AccessibleName=name;AccessibleDescription=usage+" · "+free;
        AccessibleRole=AccessibleRole.Grouping;TabStop=true;SetStyle(ControlStyles.Selectable,true);
        _fraction=fraction;_usedBytes=usedBytes;_capacityBytes=capacityBytes;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|
            ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;
        _name=new Label{Text=name,BackColor=Color.Transparent,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
        _usage=new Label{Text=usage,BackColor=Color.Transparent,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleRight};
        _free=new Label{Text=free,BackColor=Color.Transparent,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
        _command=new V269CleanupButton("Otimizar","disk");
        foreach(var label in new[]{_name,_usage,_free})label.Click+=(_,_)=>OnClick(EventArgs.Empty);
        Controls.AddRange(new Control[]{_name,_usage,_free,_command});Size=new Size(600,69);
        ApplyOptions(_options);
    }
    private int S(int x)=>V269AppTable.Scale(x,_options.InterfacePercent,DeviceDpi);
    internal int HeightForWidth(int width)=>S(width<S(450)?86:69);
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)Focus();base.OnMouseDown(e);}
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        _options=options.Normalize();float factor=_options.TextPercent/100f;
        if(_nameFont is null||Math.Abs(_nameFont.Size-9*factor)>.01f)
        {
            var oldName=_nameFont;var oldData=_dataFont;
            _nameFont=new Font("Segoe UI",9*factor,FontStyle.Bold);_dataFont=new Font("Segoe UI",8.25f*factor);
            _name.Font=_nameFont;_usage.Font=_free.Font=_dataFont;oldName?.Dispose();oldData?.Dispose();
        }
        var palette=V269AppDetailStyle.Palette(_options.Theme);
        _name.ForeColor=palette.Text;_usage.ForeColor=_free.ForeColor=palette.SecondaryText;
        _command.ApplyOptions(_options);Arrange();Invalidate(true);
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);if(_command is not null)Arrange();}
    private void Arrange()
    {
        int pad=S(11),left=pad+S(42),required=TextRenderer.MeasureText(_command.Text,_command.Font,Size.Empty,TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding).Width+S(32);
        int buttonWidth=Math.Min(Math.Max(S(92),required),Math.Max(1,Width/3));
        int right=Math.Max(left+1,Width-pad-buttonWidth-S(12)),span=Math.Max(1,right-left);
        bool compact=Width<S(450);int row=Math.Max(S(17),_name.Font.Height+1);
        _name.Bounds=new Rectangle(left,S(7),compact?span:Math.Max(1,span/2),row);
        _usage.Bounds=new Rectangle(compact?left:left+span/2,compact?S(7)+row:S(7),compact?span:Math.Max(1,span-span/2),row);
        _free.Bounds=new Rectangle(left,Math.Max(S(41),Height-S(23)),span,Math.Max(S(14),_free.Font.Height));
        _command.Bounds=new Rectangle(Math.Max(0,Width-pad-buttonWidth),Math.Max(0,(Height-S(30))/2),buttonWidth,S(30));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(Width<3||Height<3)return;
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;Color H(string s)=>V269AppTable.H(s);
        var (top,bottom,border,track)=_options.Theme switch
        {
            V269Theme.Claro=>("#edf5fb","#edf5fb","#8db1ca","#c6ddef"),
            V269Theme.Dark=>("#33373b","#2b2f33","#555c63","#393e43"),
            _=>("#0b2134","#0b2134","#25445f","#1d394e")
        };
        if(_selected)(top,bottom,border)=_options.Theme switch
        {V269Theme.Dark=>("#50565d","#41464c","#b4bdc6"),V269Theme.Claro=>("#c2e0f5","#c2e0f5","#4a9cc9"),_=>("#0f2c45","#0f2c45","#478ab4")};
        var bounds=new Rectangle(0,0,Width-1,Height-1);
        using var path=ModernButton.RoundRect(bounds,S(9));using var bg=new LinearGradientBrush(bounds,H(top),H(bottom),LinearGradientMode.Vertical);
        using var line=new Pen(H(border));g.FillPath(bg,path);g.DrawPath(line,path);
        int icon=S(30);var iconBounds=new Rectangle(S(11),(Height-icon)/2,icon,icon);
        using var iconPath=ModernButton.RoundRect(iconBounds,S(8));using var iconFill=new SolidBrush(H(track));
        g.FillPath(iconFill,iconPath);g.DrawPath(line,iconPath);
        V269CleanupGlyph.Draw(g,"disk",new Rectangle(iconBounds.X+S(5),iconBounds.Y+S(5),S(20),S(20)),_options.Theme);
        int y=Math.Max(_usage.Bottom+S(4),_free.Top-S(11));
        var meter=new Rectangle(_name.Left,y,Math.Max(1,_free.Width),S(8));
        using var meterPath=ModernButton.RoundRect(meter,S(4));using var meterFill=new SolidBrush(H(track));
        g.FillPath(meterFill,meterPath);g.DrawPath(line,meterPath);
        var frame=_fraction is double fraction ? GfMeterScale.Frame(GfMeterKind.Usage,fraction*100,_options.Theme) :
            GfUniversalMeter.Disk(_usedBytes,_capacityBytes,_options.Theme);
        int w=frame.Fraction is double amount ? Math.Clamp((int)Math.Round(meter.Width*amount),0,meter.Width) : 0;
        if(w>1)
        {
            var part=new Rectangle(meter.X,meter.Y,w,meter.Height);
            using var bar=ModernButton.RoundRect(part,Math.Min(S(4),w/2));using var fill=new SolidBrush(frame.Color);
            g.FillPath(fill,bar);
        }
        if(Focused&&ShowFocusCues&&Width>8&&Height>8)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-8,Height-8));
    }
    protected override void Dispose(bool disposing){if(disposing){_nameFont?.Dispose();_dataFont?.Dispose();}base.Dispose(disposing);}
}
