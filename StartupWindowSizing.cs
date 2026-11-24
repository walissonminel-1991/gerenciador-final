using System.Drawing;

namespace GerenciadorAplicativos;

// Política global de janela fixa. A classe usa a resolução nominal do
// monitor para escolher a faixa e a área útil para calcular as dimensões.
// O ajuste de interface existente nas Configurações é o único multiplicador.
internal static class StartupWindowSizing
{
    internal static readonly Size PreferredSize = new(875, 745);
    internal static bool ApplyAreaLimit(Rectangle workingArea) =>
        workingArea.Width > 0 && workingArea.Height > 0;

    internal static int BasePercent(int screenHeight) =>
        screenHeight >= 1080 ? 60 : screenHeight >= 720 ? 80 : 90;

    internal static Size MaximumSizeFor(Rectangle workingArea,int screenHeight=0,int interfacePercent=100)
    {
        if (!ApplyAreaLimit(workingArea)) return PreferredSize;
        int percent=BasePercent(screenHeight>0?screenHeight:workingArea.Height);
        // Geometria pura: também compilada isoladamente pelos testes de
        // resíduos, sem dependência de tokens visuais ou WinForms.
        int scale=Math.Clamp(interfacePercent,90,110);
        int Dimension(int available)=>Math.Clamp((int)(((long)available*percent*scale+5000L)/10000L),1,available);
        return new Size(Dimension(workingArea.Width),Dimension(workingArea.Height));
    }

    internal static Size LimitSize(Size requested, Rectangle workingArea,int screenHeight=0,int interfacePercent=100)
    {
        if (!ApplyAreaLimit(workingArea)) return requested;
        return MaximumSizeFor(workingArea,screenHeight,interfacePercent);
    }

    internal static Rectangle Fit(Rectangle workingArea,int screenHeight=0,int interfacePercent=100)
    {
        if (!ApplyAreaLimit(workingArea))
            return new Rectangle(0, 0, PreferredSize.Width, PreferredSize.Height);
        Size size=MaximumSizeFor(workingArea,screenHeight,interfacePercent);
        return new Rectangle(
            workingArea.Left+(workingArea.Width-size.Width)/2,
            workingArea.Top+(workingArea.Height-size.Height)/2,size.Width,size.Height);
    }

    internal static Size AllowedMinimum(Rectangle planned)=>planned.Size;

    internal static Rectangle KeepVisible(Rectangle desired, Rectangle workingArea,int screenHeight=0,int interfacePercent=100)
    {
        if (!ApplyAreaLimit(workingArea)) return desired;
        Size fixedSize=MaximumSizeFor(workingArea,screenHeight,interfacePercent);
        int x=Math.Clamp(desired.Left,workingArea.Left,workingArea.Right-fixedSize.Width);
        int y=Math.Clamp(desired.Top,workingArea.Top,workingArea.Bottom-fixedSize.Height);
        return new Rectangle(x,y,fixedSize.Width,fixedSize.Height);
    }

    // Em qualquer resolucao, o rodape de diagnostico continua legivel.
    // Os tamanhos sao em pixels da superficie disponivel, nao da resolucao
    // nominal do monitor; 4K/8K nao fazem a interface expandir sem limite.
    internal static (int Header, int Log) WorkspaceRows(int available, int dpi)
    {
        if (available <= 0) return (0, 0);
        float scale = Math.Clamp(dpi / 96F, 1F, 1.5F);
        int header = Math.Min((int)Math.Round(54F * scale),
            Math.Max(42, (int)Math.Round(available * .13F)));
        int preferredLog = Math.Max((int)Math.Round(130F * scale),
            (int)Math.Round(available * .195F));
        int minimumTools = Math.Min((int)Math.Round(180F * scale),
            (int)Math.Round(available * .43F));
        int room = Math.Max(45, available - header - minimumTools);
        int log = Math.Max(45, Math.Min(preferredLog, room));
        return (header, log);
    }

    internal static readonly (string Name, Size Resolution)[] ValidationMatrix =
    {
        ("HD 720p", new Size(1280, 720)),
        ("WXGA", new Size(1366, 768)),
        ("HD+", new Size(1600, 900)),
        ("Full HD", new Size(1920, 1080)),
        ("WQHD", new Size(2560, 1440)),
        ("4K", new Size(3840, 2160)),
        ("5K", new Size(5120, 2880)),
        ("8K", new Size(7680, 4320)),
        ("WFHD", new Size(2560, 1080)),
        ("UWQHD", new Size(3440, 1440)),
        ("UWQHD+", new Size(3840, 1600)),
        ("DQHD", new Size(5120, 1440)),
        ("5K2K", new Size(5120, 2160)),
        ("SVGA", new Size(800, 600)),
        ("XGA", new Size(1024, 768)),
        ("WXGA 16:10", new Size(1280, 800)),
        ("SXGA", new Size(1280, 1024)),
        ("UXGA", new Size(1600, 1200)),
        ("WSXGA+", new Size(1680, 1050)),
        ("WUXGA", new Size(1920, 1200)),
        ("WQXGA", new Size(2560, 1600))
    };
}
