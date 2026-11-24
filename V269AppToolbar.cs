using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GerenciadorAplicativos;

internal sealed class V269AppSearchBox : Panel
{
    private readonly TextBox _editor;
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    private Font? _font;
    internal V269AppSearchBox(TextBox editor)
    {
        Name="V269AppsSearchFrame";_editor=editor;
        Dock=DockStyle.Fill;Margin=new Padding(0,1,9,1);
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|
            ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;_editor.BorderStyle=BorderStyle.None;
        _editor.Dock=DockStyle.None;Controls.Add(_editor);
        Resize+=(_,_)=>ArrangeEditor();
        _editor.GotFocus+=(_,_)=>Invalidate();_editor.LostFocus+=(_,_)=>Invalidate();
    }
    private int S(int x)=>V269AppTable.Scale(x,_options.InterfacePercent,DeviceDpi);
    private (Color Surface,Color Border,Color Ink) Colors()=>_options.Theme switch
    {
        V269Theme.Claro=>(V269AppTable.H("#eaf5fe"),V269AppTable.H("#8eafc7"),V269AppTable.H("#17374c")),
        V269Theme.Dark=>(V269AppTable.H("#353a40"),V269AppTable.H("#737c85"),V269AppTable.H("#d1d6db")),
        _=>(V269AppTable.H("#102638"),V269AppTable.H("#416783"),V269AppTable.H("#dfedf7"))
    };
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        _options=options.Normalize();var palette=Colors();
        float size=9f*_options.TextPercent/100f;
        if(_font is null||Math.Abs(_font.Size-size)>.01f)
        {var old=_font;_font=new Font("Segoe UI",size);_editor.Font=_font;old?.Dispose();}
        _editor.BackColor=palette.Surface;_editor.ForeColor=palette.Ink;ArrangeEditor();Invalidate();
    }
    private void ArrangeEditor()
    {
        int x=S(31);_editor.Bounds=new Rectangle(x,Math.Max(1,(Height-_editor.PreferredHeight)/2),
            Math.Max(1,Width-x-S(9)),_editor.PreferredHeight);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<3||Height<3)return;
        var c=Colors();e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=ModernButton.RoundRect(new Rectangle(0,0,Width-1,Height-1),S(8));
        using var fill=new SolidBrush(c.Surface);using var border=new Pen(_editor.Focused?V269VisualTokens.ForTheme(_options.Theme).Accent:c.Border);
        e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(border,shape);
        int n=S(11),x=S(11),y=(Height-n)/2;
        using var ink=new Pen(c.Ink,1.7f){EndCap=LineCap.Round};
        e.Graphics.DrawEllipse(ink,x,y,n,n);e.Graphics.DrawLine(ink,x+n-1,y+n-1,x+n+S(4),y+n+S(4));
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left)_editor.Focus();}
    protected override void Dispose(bool disposing){if(disposing)_font?.Dispose();base.Dispose(disposing);}
}
internal sealed class V269AppToolbarButton:Button
{
    private readonly bool _primary;
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    private Font? _font;private bool _hover,_pressed;
    internal V269AppToolbarButton(string caption,bool primary=false)
    {
        _primary=primary;Text=caption;AccessibleName=caption;Dock=DockStyle.Fill;FlatStyle=FlatStyle.Flat;
        FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|
            ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;
    }
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        _options=options.Normalize();
        float size=9f*_options.TextPercent/100f;
        if(_font is null||Math.Abs(_font.Size-size)>.01f)
        {var old=_font;_font=new Font("Segoe UI",size,FontStyle.Bold);Font=_font;old?.Dispose();}
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<3||Height<3)return;var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        Color H(string s)=>V269AppTable.H(s);
        var (surface,border,ink)=_options.Theme switch
        {
            V269Theme.Claro=>(_primary?H("#2579b4"):H("#e2f0fb"),H("#8eafc7"),_primary?Color.White:H("#173d59")),
            V269Theme.Dark=>(_primary?H("#515961"):H("#3a3f45"),_primary?H("#b7c0c8"):H("#68717a"),H("#dce0e4")),
            _=>(_primary?H("#205f95"):H("#102638"),_primary?H("#6ea7cf"):H("#416783"),H("#dfedf7"))
        };
        if(!Enabled)ink=V269VisualTokens.ForTheme(_options.Theme).SecondaryText;
        else if(_hover)surface=ControlPaint.Light(surface,_pressed ? .03f : .15f);
        int S(int x)=>V269AppTable.Scale(x,_options.InterfacePercent,DeviceDpi);
        using var shape=ModernButton.RoundRect(new Rectangle(0,0,Width-1,Height-1),S(8));
        using var fill=new SolidBrush(surface);using var line=new Pen(border);g.FillPath(fill,shape);g.DrawPath(line,shape);
        int left=S(8);
        if(_primary&&Width>S(70)&&Text=="Verificar")
        {
            int n=S(15),y=(Height-n)/2;
            using var pen=new Pen(ink,1.7f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawArc(pen,left,y,n,n,-35,300);g.DrawLines(pen,new Point[]{new(left+n,y),new(left+n,y+S(6)),new(left+n-S(6),y+S(6))});
            left+=n+S(4);
        }
        TextRenderer.DrawText(g,Text,Font,new Rectangle(left,0,Math.Max(1,Width-left-S(6)),Height),ink,
            TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-6,Height-6));
    }
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);_hover=true;Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);_hover=_pressed=false;Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);_pressed=true;Invalidate();}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);_pressed=false;Invalidate();}
    protected override void Dispose(bool disposing){if(disposing)_font?.Dispose();base.Dispose(disposing);}
}
