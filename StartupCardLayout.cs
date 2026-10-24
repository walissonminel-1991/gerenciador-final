namespace GerenciadorAplicativos;

// Um unico modelo de largura para TODAS as resolucoes e escalas.
// Trabalha com a area util da janela, nao com o tamanho nominal do monitor.
internal static class StartupCardLayout
{
    internal const int BaseMinCardWidth = 286;
    internal const int BaseCardMargin = 9;
    internal const int MaxColumns = 12;

    private static int Scale(int size, int dpi) =>
        (int)Math.Ceiling(size * Math.Max(1d, dpi / 96d));

    internal static int MarginWidth(int dpi = 96) => Scale(BaseCardMargin, dpi);

    internal static int ColumnsForSpace(int space, int dpi = 96)
    {
        int cell = Scale(BaseMinCardWidth, dpi) + MarginWidth(dpi);
        return Math.Clamp(Math.Max(0, space) / Math.Max(1, cell), 1, MaxColumns);
    }

    internal static int CardWidth(int space, int columns, int dpi = 96)
    {
        int count = Math.Clamp(columns, 1, MaxColumns);
        return Math.Max(1, (Math.Max(0, space) - count * MarginWidth(dpi)) / count);
    }
}
