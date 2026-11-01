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
            // Na tela larga a 100%, o nome pode caber por inteiro:
            // truncamento e exigido quando ha restricao real de largura,
            // nao artificialmente em todo viewport.
            var constrainedRow = new Rectangle(0, 0, 800, DriverRowGeometry.Height(dpi));
            var constrainedTitle = DriverRowGeometry.Text(constrainedRow, dpi, 5, 22);
            Require(natural.Width > constrainedTitle.Width,
                $"nome extenso nao exercita truncamento em largura limitada {screen} DPI={dpi}");
            // Hit-test por indice, desenho e scroll partilham a altura DPI.
            Require(DriverRowGeometry.Height(dpi) == DriverRowGeometry.Scale(73, dpi) &&
                DriverRowGeometry.Height(dpi) >= inf.Bottom,
                $"rolagem nao acompanha linhas escaladas {screen} DPI={dpi}");
            checkedCases++;
        }
        // Reproduzir o nome que exige mais de duas linhas no layout antigo
        // (faixa de 29 px). Renderizar o cartao real de Aplicativos sem executar
        // nenhuma acao; a lista e o tooltip preservam o titulo completo.
        using var owner = new MainForm(startServices: false);
        var appList = (DarkProgramList)(typeof(MainForm).GetField("_programList",
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
            ?? throw new Exception("V5.3: lista de aplicativos indisponivel"));
        appList.Size = new Size(800, 170);
        appList.SetItems([new ProgramEntry
        {
            Name = longName, Version = "1.0", Publisher = "Fabricante ficticio",
            InventoryId = "V53|Sintetico", InventoryOnly = true
        }]);
        using var testFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        int wrapped = TextRenderer.MeasureText(longName, testFont,
            new Size(280, 200), TextFormatFlags.WordBreak).Height;
        Require(wrapped > 29, "nome longo nao reproduz clipping com WordBreak anterior");
        using var appCapture = new Bitmap(800, 67);
        using (var appGraphics = Graphics.FromImage(appCapture))
        {
            typeof(MainForm).GetMethod("DrawProgramCard",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner,
                [appList, new DrawItemEventArgs(appGraphics, appList.Font,
                    new Rectangle(0, 0, 800, 67), 0, DrawItemState.Default)]);
        }
        // O contrato do tooltip era existente e permanece no controle real.
        typeof(DarkProgramList).GetMethod("OnMouseMove",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(appList,
            [new MouseEventArgs(MouseButtons.None, 0, 80, 10, 0)]);
        var tips = (ToolTip)typeof(DarkProgramList).GetField("_itemTips",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(appList)!;
        Require(tips.GetToolTip(appList).Contains(longName, StringComparison.Ordinal),
            "reticencias nao podem descartar o nome completo do tooltip");

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
        appCapture.Save("LayoutCaptures/V53-aplicativos-nome-extenso-96dpi.png");
        Console.WriteLine($"OK V5.3: {checkedCases} combinacoes de geometria de drivers e nomes longos sem sobreposicao; desenho GDI 96 DPI; sem troca fisica de monitor.");
    }
}
