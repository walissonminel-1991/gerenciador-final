using System.Drawing;

namespace GerenciadorAplicativos;

// Tamanho aprovado em teste visual: quatro cartões por linha na aba Limpeza.
// A janela respeita a área de trabalho disponível, inclusive a barra de tarefas;
// nenhum tamanho anterior ou configuração é persistido no computador.
internal static class StartupWindowSizing
{
    internal static readonly Size PreferredSize = new(1250, 1160);
    internal static readonly Size NormalMinimum = new(970, 690);

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
}
