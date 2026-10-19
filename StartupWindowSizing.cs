using System.Drawing;

namespace GerenciadorAplicativos;

// Trabalha sempre em pixels fisicos da area util do monitor (sem barra de tarefas).
// Nao persiste posicao, tamanho, DPI nem configuracao do usuario no disco.
internal static class StartupWindowSizing
{
    internal static readonly Size PreferredSize = new(1250, 980);
    // 800x600 e a menor resolucao solicitada. Em areas uteis menores (barra
    // de tarefas, DPI, monitores remotos), reduzimos o minimo sem bloquear a tela.
    internal static readonly Size NormalMinimum = new(800, 600);

    internal static Rectangle Fit(Rectangle workingArea)
    {
        if (workingArea.Width <= 0 || workingArea.Height <= 0)
            return new Rectangle(0, 0, PreferredSize.Width, PreferredSize.Height);
        int width = Math.Min(PreferredSize.Width, workingArea.Width);
        int height = Math.Min(PreferredSize.Height, workingArea.Height);
        return new Rectangle(
            workingArea.Left + (workingArea.Width - width) / 2,
            workingArea.Top + (workingArea.Height - height) / 2,
            width, height);
    }

    internal static Size AllowedMinimum(Rectangle planned) => new(
        Math.Min(NormalMinimum.Width, Math.Max(1, planned.Width)),
        Math.Min(NormalMinimum.Height, Math.Max(1, planned.Height)));

    // Preserva o tamanho desejado sempre que possivel. Ao trocar para um
    // monitor menor, reduz a janela e a reposiciona dentro da area visivel.
    internal static Rectangle KeepVisible(Rectangle desired, Rectangle workingArea)
    {
        if (workingArea.Width <= 0 || workingArea.Height <= 0)
            return desired;
        int width = Math.Min(Math.Max(1, desired.Width), workingArea.Width);
        int height = Math.Min(Math.Max(1, desired.Height), workingArea.Height);
        int x = Math.Clamp(desired.Left, workingArea.Left, workingArea.Right - width);
        int y = Math.Clamp(desired.Top, workingArea.Top, workingArea.Bottom - height);
        return new Rectangle(x, y, width, height);
    }

    // Em qualquer resolucao, o rodape de diagnostico continua legivel.
    // Os tamanhos sao em pixels da superficie disponivel, nao da resolucao
    // nominal do monitor; 4K/8K nao fazem a interface expandir sem limite.
    internal static (int Header, int Log) WorkspaceRows(int available, int dpi)
    {
        if (available <= 0) return (0, 0);
        float scale = Math.Clamp(dpi / 96F, 1F, 1.5F);
        int header = Math.Min((int)Math.Round(66F * scale),
            Math.Max(42, (int)Math.Round(available * .14F)));
        int preferredLog = Math.Max((int)Math.Round(130F * scale),
            (int)Math.Round(available * .235F));
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
