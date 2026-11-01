namespace GerenciadorAplicativos;

// Modelo de layout deterministico dos quatro botoes de Aplicativos.
// A transicao de DPI pode manter Compact inalterado e, ainda assim,
// exigir alturas/larguras novas (por exemplo 150% -> 200%).
internal readonly record struct AppActionLayout(int Dpi, bool Compact, int RowHeight)
{
    internal static AppActionLayout Measure(int availableWidth, int dpi)
    {
        int effectiveDpi = Math.Max(96, dpi);
        double scale = effectiveDpi / 96d;
        bool compact = availableWidth < (int)Math.Ceiling(745 * scale);
        return new AppActionLayout(effectiveDpi, compact,
            (int)Math.Ceiling((compact ? 76 : 44) * scale));
    }

    internal bool NeedsReflow(AppActionLayout? previous) =>
        previous is null || previous.Value != this;
}
