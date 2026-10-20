namespace GerenciadorAplicativos;

internal static class StartupCardLayout
{
    // "space" ja reserva margens/rolagem do painel, entao nao contabilizar
    // colunas ficticias. >=885 acomoda 3 cartoes de ~278 px sem texto cortado
    // (texto longo fica integralmente disponivel na dica e acessibilidade).
    internal static int ColumnsForSpace(int space) =>
        space >= 885 ? 3 : space >= 590 ? 2 : 1;

    internal static int CardWidth(int space, int columns) =>
        Math.Max(250, space / Math.Max(1, columns) - 17);
}
