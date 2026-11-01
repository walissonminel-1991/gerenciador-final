using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class V52DpiActionLayoutTests
{
    internal static void Run()
    {
        int checkedCases = 0;
        foreach (Size screen in new[] { new Size(1440, 900), new Size(2560, 1440) })
        {
            var workingArea = new Rectangle(0, 0, screen.Width, screen.Height - 48);
            int width = StartupWindowSizing.Fit(workingArea).Width - 24;
            AppActionLayout? prior = null;
            foreach (int dpi in new[] { 96, 144, 192 })
            {
                var current = AppActionLayout.Measure(width, dpi);
                var next = AppActionLayout.Measure(width, dpi);
                int expectedHeight = (int)Math.Ceiling((current.Compact ? 76 : 44) * dpi / 96d);
                if (current.Dpi != dpi || current.RowHeight != expectedHeight ||
                    !current.NeedsReflow(prior) || next.NeedsReflow(current))
                    throw new Exception($"V5.2: estado/recálculo incorreto {screen}, DPI {dpi}.");

                using var font = new Font("Segoe UI", 9.5f * dpi / 96f);
                int textHeight = TextRenderer.MeasureText("Desinstalar selecionados",
                    font, new Size(int.MaxValue, 600),
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height;
                int buttonRow = current.Compact ? current.RowHeight / 2 : current.RowHeight;
                if (buttonRow < textHeight + 6)
                    throw new Exception($"V5.2: altura insuficiente {screen}, DPI {dpi}, botoes {buttonRow}, texto {textHeight}.");

                if (prior is AppActionLayout old && old.Compact == current.Compact &&
                    old.Dpi != current.Dpi && old.RowHeight == current.RowHeight)
                    throw new Exception("V5.2: mesmo modo com DPI novo precisa recalcular altura.");
                prior = current;
                checkedCases++;
            }
        }
        var weak150 = AppActionLayout.Measure(1440 * 70 / 100 - 24, 144);
        var weak200 = AppActionLayout.Measure(1440 * 70 / 100 - 24, 192);
        var strong150 = AppActionLayout.Measure(2560 * 70 / 100 - 24, 144);
        var strong200 = AppActionLayout.Measure(2560 * 70 / 100 - 24, 192);
        // Contraexemplo do cache antigo: somente o booleano Compact era
        // comparado. Assim, transicoes 150->200 eram ignoradas nas duas telas.
        if (!weak150.Compact || !weak200.Compact ||
            strong150.Compact || strong200.Compact ||
            weak150.RowHeight == weak200.RowHeight ||
            strong150.RowHeight == strong200.RowHeight ||
            !weak200.NeedsReflow(weak150) || !strong200.NeedsReflow(strong150))
            throw new Exception("V5.2: contraexemplo de cache booleano nao reproduzido.");
        Console.WriteLine($"OK V5.2: {checkedCases} combinacoes de dimensao/DPI, recálculo 150%-200% sem trocar modo de colunas; sem DPI fisico.");
    }
}
