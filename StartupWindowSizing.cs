using System.Drawing;

namespace GerenciadorAplicativos;

// A janela acompanha a area util do monitor em todas as resolucoes.
// Inicial: aprox. 57,4% da area; limite absoluto: 59,76%.
// Nao multiplica fontes por resolucao: quem controla tipografia e o DPI do Windows.
// Nao grava tamanho, DPI ou posicao no disco.
internal static class StartupWindowSizing
{
    // Utilizado APENAS se o Windows ainda nao informou uma area valida.
    internal static readonly Size PreferredSize = new(875, 745);
    internal static readonly Size NormalMinimum = new(800, 600);

    private const double InitialWidthRatio = 0.70;
    private const double InitialHeightRatio = 0.82;
    private const double MaximumWidthRatio = 0.72;
    private const double MaximumHeightRatio = 0.83;

    // A regra agora e universal, inclusive em 800x600 e monitores antigos.
    internal static bool ApplyAreaLimit(Rectangle workingArea) =>
        workingArea.Width > 0 && workingArea.Height > 0;

    internal static Size MaximumSizeFor(Rectangle workingArea)
    {
        if (!ApplyAreaLimit(workingArea)) return PreferredSize;
        return new Size(
            Math.Max(1, (int)Math.Floor(workingArea.Width * MaximumWidthRatio)),
            Math.Max(1, (int)Math.Floor(workingArea.Height * MaximumHeightRatio)));
    }

    internal static Size LimitSize(Size requested, Rectangle workingArea)
    {
        if (!ApplyAreaLimit(workingArea)) return requested;
        Size maximum = MaximumSizeFor(workingArea);
        return new Size(
            Math.Clamp(requested.Width, 1, maximum.Width),
            Math.Clamp(requested.Height, 1, maximum.Height));
    }

    internal static Rectangle Fit(Rectangle workingArea)
    {
        if (!ApplyAreaLimit(workingArea))
            return new Rectangle(0, 0, PreferredSize.Width, PreferredSize.Height);
        Size size = LimitSize(new Size(
            Math.Max(1, (int)Math.Floor(workingArea.Width * InitialWidthRatio)),
            Math.Max(1, (int)Math.Floor(workingArea.Height * InitialHeightRatio))),
            workingArea);
        return new Rectangle(
            workingArea.Left + (workingArea.Width - size.Width) / 2,
            workingArea.Top + (workingArea.Height - size.Height) / 2,
            size.Width, size.Height);
    }

    internal static Size AllowedMinimum(Rectangle planned) => new(
        Math.Min(NormalMinimum.Width, Math.Max(1, planned.Width)),
        Math.Min(NormalMinimum.Height, Math.Max(1, planned.Height)));

    // Troca de monitor ou DPI: preservar quando couber, limitar em qualquer
    // resolucao e reposicionar sem jamais passar dos 60% da area util.
    internal static Rectangle KeepVisible(Rectangle desired, Rectangle workingArea)
    {
        if (!ApplyAreaLimit(workingArea)) return desired;
        Size limited = LimitSize(desired.Size, workingArea);
        int x = Math.Clamp(desired.Left, workingArea.Left, workingArea.Right - limited.Width);
        int y = Math.Clamp(desired.Top, workingArea.Top, workingArea.Bottom - limited.Height);
        return new Rectangle(x, y, limited.Width, limited.Height);
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
