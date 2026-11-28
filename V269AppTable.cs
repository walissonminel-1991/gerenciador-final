using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GerenciadorAplicativos;

internal static class V269AppTable
{
    internal static readonly string[] Headers=["Aplicativo","Versão","Editor","Instalado em","Situação","Ações"];
    internal static readonly string[] DriverHeaders = ["Dispositivo / Driver", "Versão atual", "Versão disponível", "Ações"];
    internal static int Scale(int px,int scale,int dpi)=>Math.Max(1,(int)Math.Round(px*scale*dpi/9600d));
    // CSS congelado da PR109: minmax(min,fr), não min + fração do excedente.
    private static readonly int[] WideWidths=[185,70,100,88,98,75];
    private static readonly int[] CompactWidths=[150,68,96,80,90,65];
    private static readonly int[] DriverWideWidths = [190, 90, 100, 74];
    private static readonly int[] DriverCompactWidths = [155, 87, 90, 68];
    internal static bool IsCompact(int windowWidth)=>windowWidth<=1050;
    // Tracks e padding das células não recebem --uiScale no HTML.
    // Somente a coluna de ações pode crescer para não cortar alvos reais
    // quando os botões escalados ultrapassarem a reserva do protótipo.
    private static int[] MinimumTracks(int scale,int dpi,bool compact,bool drivers)
    {
        var widths = drivers ? (compact ? DriverCompactWidths : DriverWideWidths) : (compact ? CompactWidths : WideWidths);
        var tracks = widths.Select(n => Scale(n, 100, dpi)).ToArray();
        tracks[^1] = Math.Max(tracks[^1], 2 * Scale(32, scale, dpi));
        return tracks;
    }
    internal static int MinimumWidth(int scale,int dpi,bool compact=false,bool drivers=false)=>
        MinimumTracks(scale,dpi,compact,drivers).Sum();
    internal static int CellPadding(int scale,int dpi,bool compact)=>Scale(compact?5:9,100,dpi);
    internal static Rectangle[] Columns(Rectangle bounds,int scale,int dpi,bool compact=false,bool drivers=false)
    {
        double[] weights = drivers ? (compact ? [2, 1, 1, .55] : [2.3, 1, 1, .55])
            : (compact ? [2, .8, 1.1, .9, 1, .6] : [2.1, .8, 1.1, .9, 1, .65]);
        int[] minimum=MinimumTracks(scale,dpi,compact,drivers);
        int target=Math.Max(bounds.Width,minimum.Sum());
        double remaining=target, fractions=weights.Sum();
        var fixedTrack=new bool[minimum.Length];
        // Tracks abaixo do mínimo são congeladas; recalcular o fr restante.
        for(int pass=0;pass<minimum.Length;pass++)
        {
            bool changed=false;
            double unit=fractions>0?remaining/fractions:0;
            for(int i=0;i<minimum.Length;i++)
                if(!fixedTrack[i]&&unit*weights[i]<minimum[i])
                {fixedTrack[i]=true;remaining-=minimum[i];fractions-=weights[i];changed=true;}
            if(!changed)break;
        }
        double finalUnit=fractions>0?remaining/fractions:0;
        var sizes=new int[minimum.Length];
        for(int i=0;i<sizes.Length;i++)
            sizes[i]=fixedTrack[i]?minimum[i]:Math.Max(minimum[i],(int)Math.Floor(finalUnit*weights[i]+1e-8));
        for(int i=0;target>sizes.Sum();i=(i+1)%sizes.Length)
            if(!fixedTrack[i]||fractions<=0)sizes[i]++;
        int left=bounds.Left;
        return sizes.Select(w=>{var rect=new Rectangle(left,bounds.Top,w,bounds.Height);left+=w;return rect;}).ToArray();
    }
    internal static Rectangle Action(Rectangle bounds,int scale,int dpi,bool remove,bool compact=false,bool drivers=false)
    {
        var cell=Columns(bounds,scale,dpi,compact,drivers)[^1];
        int width=Scale(32,scale,dpi);
        // O CSS compacto reserva 65px para dois alvos de 32px.
        // Reduzir só folga/padding se necessário, nunca sobrepor alvos.
        int gap=Math.Max(0,Math.Min(Scale(2,scale,dpi),cell.Width-width*2));
        width=Math.Min(width,(cell.Width-gap)/2);
        int padding=Math.Max(0,Math.Min(Scale(3,scale,dpi),cell.Width-width*2-gap));
        int height=Math.Min(bounds.Height-2,Scale(30,scale,dpi));
        int start=cell.Right-padding-width*2-gap;
        return new Rectangle(start+(remove?width+gap:0),bounds.Top+(bounds.Height-height)/2,width,height);
    }
    internal static void FitScrollBars(Control viewport, DarkVScrollBar vertical,
        DarkVScrollBar horizontal, int minimum, int headerHeight, int rowsHeight)
    {
        int width = Math.Max(1, viewport.ClientSize.Width);
        int height = viewport.ClientSize.Height;
        int h = Math.Max(12, horizontal.Height);
        int v = Math.Max(12, vertical.Width);
        bool needsHorizontal = minimum > width;
        bool needsVertical = rowsHeight > Math.Max(1, height - headerHeight - (needsHorizontal ? h : 0));
        needsHorizontal = minimum > Math.Max(1, width - (needsVertical ? v : 0));
        int content = Math.Max(1, width - (needsVertical ? v : 0));
        int body = Math.Max(1, height - headerHeight - (needsHorizontal ? h : 0));
        horizontal.Bounds = new Rectangle(0, Math.Max(0, height - h), content, h);
        horizontal.SetMetrics(Math.Max(minimum, content), content);
        vertical.Bounds = new Rectangle(Math.Max(0, width - v), headerHeight, v, body);
        vertical.SetMetrics(rowsHeight, body);
    }

    internal static Rectangle Grip(Rectangle bounds,int scale,int dpi)=>
        new(bounds.X,bounds.Y,Scale(19,scale,dpi),bounds.Height);
    internal static Color H(string s)=>ColorTranslator.FromHtml(s);
    // Cores do HTML, somente para a área de inventário e suas barras.
    internal static Color EmptySurface(V269Theme theme) => V269VisualTokens.InventorySurface(theme);
    internal static (Color Track,Color Thumb,Color Hot) ScrollColors(V269Theme theme) => V269VisualTokens.InventoryScroll(theme);
    internal static (Color Head,Color Row,Color Alternate,Color Selected,Color Ink,Color Border,Color Rail) Colors(V269Theme theme) => V269VisualTokens.InventoryPalette(theme);
    internal static Rectangle AppInstallDateHeaderBounds(IDeviceContext context, Rectangle cell, Font font, int dpi, bool compact)
    {
        const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        int titleWidth = TextRenderer.MeasureText(context, Headers[3], font, Size.Empty, flags).Width;
        int padding = Math.Min(CellPadding(100, dpi, compact), Math.Max(0, (cell.Width-titleWidth)/2));
        cell.Inflate(-padding, 0);
        return cell;
    }

    internal static void DrawHeader(Graphics g,Rectangle bounds,V269AppearanceOptions options,Font font,int dpi,bool compact=false,bool drivers=false)
    {
        if(bounds.Width<1||bounds.Height<1)return;
        var colors=Colors(options.Theme);
        using Brush bg=options.Theme==V269Theme.Dark
            ?new LinearGradientBrush(bounds,V269VisualTokens.InventoryHeaderTop(options.Theme),colors.Head,LinearGradientMode.Vertical)
            :new SolidBrush(colors.Head);
        g.FillRectangle(bg,bounds);
        Color ink=V269VisualTokens.InventoryHeaderInk(options.Theme);
        var columns=Columns(bounds,options.InterfacePercent,dpi,compact,drivers);
        var headers = drivers ? DriverHeaders : Headers;
        for(int i=0;i<headers.Length;i++)
        {
            var rect=columns[i];
            if (!drivers && i==3) rect=AppInstallDateHeaderBounds(g,rect,font,dpi,compact);
            else rect.Inflate(-CellPadding(options.InterfacePercent,dpi,compact),0);
            TextRenderer.DrawText(g,headers[i],font,rect,ink,TextFormatFlags.VerticalCenter|
                TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.PreserveGraphicsClipping);
        }
        using var line=new Pen(V269VisualTokens.InventoryHeaderDivider(options.Theme));
        g.DrawLine(line,bounds.Left,bounds.Bottom-1,bounds.Right,bounds.Bottom-1);
    }

    internal static Color RowInk(V269Theme theme,bool selected) => V269VisualTokens.InventoryRowInk(theme,selected);
    internal static Color StateInk(V269Theme theme,bool selected,bool available) => V269VisualTokens.InventoryStateInk(theme,selected,available);
    internal static void DrawRowBackground(Graphics g,Rectangle bounds,V269Theme theme,bool selected,bool alternate,int scale,int dpi)
    {
        if(bounds.Width<1||bounds.Height<1)return;
        var colors=Colors(theme);
        var (first,last,selectedBorder)=V269VisualTokens.InventorySelection(theme);
        using Brush fill=selected?new LinearGradientBrush(bounds,first,last,LinearGradientMode.Horizontal):
            new SolidBrush(alternate?colors.Alternate:colors.Row);
        g.FillRectangle(fill,bounds);
        using var line=new Pen(V269VisualTokens.InventoryRowDivider(theme));
        g.DrawLine(line,bounds.Left,bounds.Bottom-1,bounds.Right,bounds.Bottom-1);
        if(selected&&bounds.Width>1&&bounds.Height>1)
        {
            using var border=new Pen(selectedBorder);
            g.DrawRectangle(border,bounds.X,bounds.Y,bounds.Width-1,bounds.Height-1);
            using var rail=new SolidBrush(V269VisualTokens.InventorySelectionRail(theme));
            int gap=Scale(4,scale,dpi);
            g.FillRectangle(rail,bounds.X,bounds.Y+gap,Math.Min(gap,bounds.Width),Math.Max(1,bounds.Height-2*gap));
        }
    }
    internal static void DrawAction(Graphics g,Rectangle r,V269Theme theme,bool remove,bool allowed,bool hover,bool pressed)
    {
        if(r.Width<2||r.Height<2)return;
        var (back,ink,outline)=ActionColors(theme,remove,allowed);
        if(allowed&&hover)back=ControlPaint.Light(back,pressed ? .05f : .15f);
        using var path=ModernButton.RoundRect(new Rectangle(r.X,r.Y,r.Width-1,r.Height-1),6);
        using var fill=new SolidBrush(back);using var border=new Pen(outline);
        g.FillPath(fill,path);g.DrawPath(border,path);
        var state=g.Save();
        try
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            float size=Math.Min(r.Width*20f/32f,r.Height*20f/30f);
            g.TranslateTransform(r.X+(r.Width-size)/2,r.Y+(r.Height-size)/2);g.ScaleTransform(size/24,size/24);
            using var glyph=ActionGlyph(remove);
            using var symbol=new SolidBrush(ink);
            g.FillPath(symbol,glyph);
        }
        finally{g.Restore(state);}
    }

    // Última cascata do HTML aprovado: cores específicas da tabela de Aplicativos.
    // Não alterar os tokens compartilhados pelas demais páginas.
    internal static (Color Back,Color Ink,Color Border) ActionColors(V269Theme theme,bool remove,bool allowed) => V269VisualTokens.InventoryActionColors(theme,remove,allowed);

    // ViewBox 24x24 dos SVG refresh-solid e trash-solid do HTML congelado.
    internal static GraphicsPath ActionGlyph(bool remove)
    {
        var glyph=new GraphicsPath(FillMode.Winding);
        if(remove)
        {
            glyph.StartFigure();
            glyph.AddLine(6,7,18,7);
            glyph.AddLine(18,7,18,19);
            glyph.AddBezier(18,19,18,20.10457f,17.10457f,21,16,21);
            glyph.AddLine(16,21,8,21);
            glyph.AddBezier(8,21,6.89543f,21,6,20.10457f,6,19);
            glyph.AddLine(6,19,6,7);
            glyph.CloseFigure();
            glyph.StartFigure();
            glyph.AddLine(9,9,9,18);
            glyph.AddLine(9,18,11,18);
            glyph.AddLine(11,18,11,9);
            glyph.AddLine(11,9,9,9);
            glyph.CloseFigure();
            glyph.StartFigure();
            glyph.AddLine(13,9,13,18);
            glyph.AddLine(13,18,15,18);
            glyph.AddLine(15,18,15,9);
            glyph.AddLine(15,9,13,9);
            glyph.CloseFigure();
            glyph.StartFigure();
            glyph.AddLine(5,4,9,4);
            glyph.AddLine(9,4,10,3);
            glyph.AddLine(10,3,14,3);
            glyph.AddLine(14,3,15,4);
            glyph.AddLine(15,4,19,4);
            glyph.AddLine(19,4,19,6);
            glyph.AddLine(19,6,5,6);
            glyph.AddLine(5,6,5,4);
            glyph.CloseFigure();
        }
        else
        {
            glyph.StartFigure();
            glyph.AddLine(12,4,12,1);
            glyph.AddLine(12,1,8,5);
            glyph.AddLine(8,5,12,9);
            glyph.AddLine(12,9,12,6);
            glyph.AddBezier(12,6,15.31f,6,18,8.69f,18,12);
            glyph.AddBezier(18,12,18,13.01f,17.75f,13.97f,17.31f,14.8f);
            glyph.AddLine(17.31f,14.8f,18.77f,16.26f);
            glyph.AddBezier(18.77f,16.26f,19.57673f,14.98584f,20.00341f,13.50807f,20,12);
            glyph.AddBezier(20,12,20,7.58f,16.42f,4,12,4);
            glyph.CloseFigure();
            glyph.StartFigure();
            glyph.AddBezier(12,18,8.69f,18,6,15.31f,6,12);
            glyph.AddBezier(6,12,6,10.99f,6.25f,10.03f,6.69f,9.2f);
            glyph.AddLine(6.69f,9.2f,5.23f,7.74f);
            glyph.AddBezier(5.23f,7.74f,4.42327f,9.01416f,3.99659f,10.49193f,4,12);
            glyph.AddBezier(4,12,4,16.42f,7.58f,20,12,20);
            glyph.AddLine(12,20,12,23);
            glyph.AddLine(12,23,16,19);
            glyph.AddLine(16,19,12,15);
            glyph.AddLine(12,15,12,18);
            glyph.CloseFigure();
        }
        return glyph;
    }
}
