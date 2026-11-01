using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class V53VirtualCardTests
{
    private static void Require(bool predicate, string description)
    {
        if (!predicate) throw new Exception("V5.3 virtualizacao: " + description);
    }

    internal static void Run()
    {
        Require(AppRowGeometry.TitleTextFlags.HasFlag(TextFormatFlags.SingleLine) &&
            AppRowGeometry.TitleTextFlags.HasFlag(TextFormatFlags.EndEllipsis) &&
            !AppRowGeometry.TitleTextFlags.HasFlag(TextFormatFlags.WordBreak) &&
            AppRowGeometry.DetailTextFlags.HasFlag(TextFormatFlags.EndEllipsis),
            "nomes e detalhes devem ficar em uma linha com reticencias; tooltip contem texto integral");

        const string longName =
            "Controlador de video de teste sintético com descrição deliberadamente longa — " +
            "fabricante fictício, múltiplos adaptadores e identificação de hardware extensa";
        var driverItem = new DriverInventoryEntry(@"PCI\V53\FIXTURE", longName,
            "Fabricante de teste sem driver instalado", "1.2.3.4", "oem123.inf", true, true);
        using var driver = new DarkDriverList { Size = new Size(800, 280) };
        driver.SetItems([driverItem, driverItem, driverItem, driverItem, driverItem]);
        driver.CreateControl();
        using var appFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        int checkedCases = 0;
        foreach (Size screen in new[] { new Size(1440, 900), new Size(2560, 1440) })
        foreach (int dpi in new[] { 96, 144, 192 })
        {
            int screenViewport = StartupWindowSizing.Fit(
                new Rectangle(0, 0, screen.Width, screen.Height - 48)).Width - 48;
            int width = Math.Max(520, screenViewport);
            var row = new Rectangle(0, 0, width, DriverRowGeometry.Height(dpi));
            Rectangle card = DriverRowGeometry.Card(row, dpi);
            Rectangle emblem = DriverRowGeometry.Emblem(row, dpi);
            Rectangle title = DriverRowGeometry.Text(row, dpi, 5, 22);
            Rectangle version = DriverRowGeometry.Text(row, dpi, 28, 19);
            Rectangle inf = DriverRowGeometry.Text(row, dpi, 48, 19);
            Require(row.Contains(card) && card.Contains(emblem) &&
                row.Contains(title) && row.Contains(version) && row.Contains(inf),
                $"driver sai da faixa {screen} DPI={dpi}");
            Require(!title.IntersectsWith(version) && !version.IntersectsWith(inf) &&
                title.Left >= emblem.Right,
                $"driver sobrepoe titulo/versao/INF/emblema {screen} DPI={dpi}");
            if (DriverRowGeometry.ShowStatus(row, dpi))
            {
                Rectangle pill = DriverRowGeometry.Status(row, dpi);
                Require(row.Contains(pill) && title.Right <= pill.Left &&
                    version.Right <= pill.Left && inf.Right <= pill.Left,
                    $"status sobrepoe texto/viewport {screen} DPI={dpi}");
            }
            // Texto longo exige reticencias; nao basta a fixture ter nome curto.
            using var scaledFont = new Font(appFont.FontFamily,
                appFont.SizeInPoints * dpi / 96f, FontStyle.Bold);
            var natural = TextRenderer.MeasureText(longName, scaledFont,
                new Size(int.MaxValue, 200), TextFormatFlags.NoPadding);
            Require(natural.Width > title.Width,
                $"nome longo nao exercita truncamento {screen} DPI={dpi}");
            // Hit-test por indice, desenho e scroll partilham a altura DPI.
            Require(DriverRowGeometry.Height(dpi) == DriverRowGeometry.Scale(73, dpi) &&
                DriverRowGeometry.Height(dpi) >= inf.Bottom,
                $"rolagem nao acompanha linhas escaladas {screen} DPI={dpi}");
            checkedCases++;
        }
        // Render GDI real em runner 96 DPI, sem drivers/arquivos reais.
        var paint = typeof(DarkDriverList).GetMethod("DrawCard",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("V5.3: DrawCard nao localizado.");
        using var output = new Bitmap(800, 73 * 5);
        using (var g = Graphics.FromImage(output))
        {
            g.Clear(driver.BackColor);
            for (int i = 0; i < 5; i++)
                paint.Invoke(driver, [g, new Rectangle(0, i * 73, 800, 73), driverItem, i == 0]);
        }
        Directory.CreateDirectory("LayoutCaptures");
        output.Save("LayoutCaptures/V53-drivers-virtualizados-96dpi.png");
        Console.WriteLine($"OK V5.3: {checkedCases} combinacoes de geometria de drivers e nomes longos sem sobreposicao; desenho GDI 96 DPI; sem troca fisica de monitor.");
    }
}
