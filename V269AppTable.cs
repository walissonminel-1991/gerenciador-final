using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GerenciadorAplicativos;

internal static class V269AppTable
{
    internal static readonly string[] Headers=["Aplicativo","Versão","Editor","Instalado em","Situação","Ações"];
    internal static int Scale(int px,int scale,int dpi)=>Math.Max(1,(int)Math.Round(px*scale*dpi/9600d));
    internal static int MinimumWidth(int scale,int dpi)=>new[]{150,68,96,80,90,65}.Sum(n=>Scale(n,scale,dpi));
    internal static Rectangle[] Columns(Rectangle bounds,int scale,int dpi)
    {
        int[] minimum=[150,68,96,80,90,65];
        double[] weights=[2,.8,1.1,.9,1,.6];
        int[] sizes=minimum.Select(x=>Scale(x,scale,dpi)).ToArray();
        int extra=Math.Max(0,bounds.Width-sizes.Sum());
        for(int i=0;i<sizes.Length;i++)sizes[i]+=(int)(extra*weights[i]/weights.Sum());
        sizes[0]+=Math.Max(0,bounds.Width-sizes.Sum());
        int left=bounds.Left;
        return sizes.Select(w=>{var rect=new Rectangle(left,bounds.Top,w,bounds.Height);left+=w;return rect;}).ToArray();
    }
    internal static Rectangle Action(Rectangle bounds,int scale,int dpi,bool remove)
    {
        var cell=Columns(bounds,scale,dpi)[5];
        int gap=Scale(2,scale,dpi),width=Math.Min(Scale(32,scale,dpi),(cell.Width-gap)/2);
        int height=Math.Min(bounds.Height-2,Scale(30,scale,dpi));
        int start=cell.Right-width*2-gap;
        return new Rectangle(start+(remove?width+gap:0),bounds.Top+(bounds.Height-height)/2,width,height);
    }
    internal static Rectangle Grip(Rectangle bounds,int scale,int dpi)=>
        new(bounds.X,bounds.Y,Scale(19,scale,dpi),bounds.Height);
    internal static Color H(string s)=>ColorTranslator.FromHtml(s);
    internal static (Color Head,Color Row,Color Alternate,Color Selected,Color Ink,Color Border,Color Rail) Colors(V269Theme theme)=>theme switch
    {
        V269Theme.Dark=>(H("#34383d"),H("#282b2f"),H("#2d3136"),H("#50575f"),H("#cbd1d6"),H("#3d4349"),H("#f1f5f8")),
        V269Theme.Claro=>(H("#bcdcf3"),H("#eaf4fb"),H("#e4f2fd"),H("#c3dff3"),H("#20435f"),H("#9bbacf"),H("#2374ab")),
        _=>(H("#17364a"),H("#0b2033"),H("#0c273a"),H("#1c4666"),H("#e4edf6"),H("#25445f"),H("#69bfe0"))
    };
    internal static void DrawHeader(Graphics g,Rectangle bounds,V269AppearanceOptions options,Font font,int dpi)
    {
        var colors=Colors(options.Theme);
        using var bg=new SolidBrush(colors.Head);g.FillRectangle(bg,bounds);
        var columns=Columns(bounds,options.InterfacePercent,dpi);
        for(int i=0;i<Headers.Length;i++)
        {
            var rect=columns[i];rect.Inflate(-Scale(5,options.InterfacePercent,dpi),0);
            TextRenderer.DrawText(g,Headers[i],font,rect,colors.Ink,TextFormatFlags.VerticalCenter|
                TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.PreserveGraphicsClipping);
        }
        using var line=new Pen(colors.Border);g.DrawLine(line,bounds.Left,bounds.Bottom-1,bounds.Right,bounds.Bottom-1);
    }
    internal static void DrawAction(Graphics g,Rectangle r,V269Theme theme,bool remove,bool allowed,bool hover,bool pressed)
    {
        if(r.Width<2||r.Height<2)return;
        var kind=remove?V269ActionKind.Remover:V269ActionKind.Atualizar;
        Color back=V269VisualTokens.ActionBackground(theme,kind,allowed),ink=V269VisualTokens.ActionColor(theme,kind,allowed);
        if(theme==V269Theme.Claro&&allowed)ink=Color.White;
        if(allowed&&hover)back=ControlPaint.Light(back,pressed?.05f:.15f);
        using var path=ModernButton.RoundRect(new Rectangle(r.X,r.Y,r.Width-1,r.Height-1),6);
        using var fill=new SolidBrush(back);using var border=new Pen(V269VisualTokens.ActionBorder(theme,kind,allowed));
        g.FillPath(fill,path);g.DrawPath(border,path);
        var state=g.Save();
        try
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            float size=Math.Min(r.Width,r.Height)*.62f;
            g.TranslateTransform(r.X+(r.Width-size)/2,r.Y+(r.Height-size)/2);g.ScaleTransform(size/24,size/24);
            using var p=new Pen(ink,2.4f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
            if(remove)
            {
                g.DrawLine(p,4,6,20,6);g.DrawLine(p,9,3,15,3);
                g.DrawLines(p,new PointF[]{new(6,9),new(7,21),new(17,21),new(18,9)});
                g.DrawLine(p,10,10,10,17);g.DrawLine(p,14,10,14,17);
            }
            else
            {
                g.DrawArc(p,4,4,16,16,-45,155);g.DrawArc(p,4,4,16,16,135,155);
                g.DrawLines(p,new PointF[]{new(19,2),new(19,8),new(13,8)});
                g.DrawLines(p,new PointF[]{new(5,22),new(5,16),new(11,16)});
            }
        }
        finally{g.Restore(state);}
    }
}
