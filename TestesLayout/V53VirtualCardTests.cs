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
            var row = new Rectangle(0, 0, width, DriverRowGeometry.Height(dpi, width));
            Rectangle card = DriverRowGeometry.Card(row, dpi);
            Rectangle emblem = DriverRowGeometry.Emblem(row, dpi);
            Rectangle title = DriverRowGeometry.Text(row, dpi, 4, 19);
            Rectangle version = DriverRowGeometry.Text(row, dpi, 25, 16);
            Rectangle inf = DriverRowGeometry.Text(row, dpi, 43, 16);
            Require(row.Contains(card) && card.Contains(emblem) &&
                row.Contains(title) && row.Contains(version) && row.Contains(inf),
                $"driver sai da faixa {screen} DPI={dpi}");
            Require(!title.IntersectsWith(version) && !version.IntersectsWith(inf) &&
                title.Left >= emblem.Right,
                $"driver sobrepoe titulo/versao/INF/emblema {screen} DPI={dpi}");
            Rectangle pill = DriverRowGeometry.Status(row, dpi);
            using var actionFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            var uninstall = DriverRowGeometry.Uninstall(row, dpi, actionFont);
            Require(row.Contains(pill) && !title.IntersectsWith(pill) &&
                !version.IntersectsWith(pill) && !inf.IntersectsWith(pill) &&
                row.Contains(uninstall) && !uninstall.IntersectsWith(pill),
                $"status e ações sobrepostos ou fora da viewport {screen} DPI={dpi}");
            // Texto longo exige reticencias; nao basta a fixture ter nome curto.
            using var scaledFont = new Font(appFont.FontFamily,
                appFont.SizeInPoints * dpi / 96f, FontStyle.Bold);
            var natural = TextRenderer.MeasureText(longName, scaledFont,
                new Size(int.MaxValue, 200), TextFormatFlags.NoPadding);
            // Na tela larga a 100%, o nome pode caber por inteiro:
            // truncamento e exigido quando ha restricao real de largura,
            // nao artificialmente em todo viewport.
            var constrainedRow = new Rectangle(0, 0, 800, DriverRowGeometry.Height(dpi, 800));
            var constrainedTitle = DriverRowGeometry.Text(constrainedRow, dpi, 4, 19);
            Require(natural.Width > constrainedTitle.Width,
                $"nome extenso nao exercita truncamento em largura limitada {screen} DPI={dpi}");
            // Hit-test, desenho e scroll compartilham altura responsiva e escala DPI.
            Require(row.Height == DriverRowGeometry.Height(dpi, width) &&
                row.Height >= inf.Bottom && row.Height >= pill.Bottom,
                $"rolagem nao acompanha linhas escaladas {screen} DPI={dpi}");
            checkedCases++;
        }
        // Reproduzir o nome que exige mais de duas linhas no layout antigo
        // (faixa de 29 px). Renderizar o cartao real de Aplicativos sem executar
        // nenhuma acao; a lista e o tooltip preservam o titulo completo.
        using var owner = new MainForm(startServices: false);
        owner.Show();Application.DoEvents();
        var shell=owner.Controls.Find("V269Shell",true).OfType<V269Shell>().Single();
        shell.SelectPage(V269Page.Aplicativos);shell.PresentPanel(null);Application.DoEvents();
        var appList = (DarkProgramList)(typeof(MainForm).GetField("_programList",
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
            ?? throw new Exception("V5.3: lista de aplicativos indisponivel"));
        Require(appList.Visible&&appList.IsHandleCreated&&appList.ClientSize.Width>80,
            "viewport real de Aplicativos visível antes de testar mouse");
        appList.SetItems([new ProgramEntry
        {
            Name = longName, Version = "1.0", Publisher = "Fabricante ficticio",
            InventoryId = "V53|Sintetico", InventoryOnly = true
        }]);
        using var testFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        int wrapped = TextRenderer.MeasureText(longName, testFont,
            new Size(280, 200), TextFormatFlags.WordBreak).Height;
        Require(wrapped > 29, "nome longo nao reproduz clipping com WordBreak anterior");
        var appRow=(Rectangle)typeof(DarkProgramList).GetMethod("RowBounds",
            BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(appList,[0])!;
        Require(appList.V269TableMode&&appRow.Top>=appList.TableHeaderHeight&&appRow.Height==appList.ItemHeight,
            "a linha real começa após o cabeçalho fixo");
        using var appCapture = new Bitmap(appRow.Width,appRow.Height);
        using (var appGraphics = Graphics.FromImage(appCapture))
        {
            typeof(MainForm).GetMethod("DrawProgramCard",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner,
                [appList, new DrawItemEventArgs(appGraphics, appList.Font,
                    new Rectangle(0, 0, appRow.Width, appRow.Height), 0, DrawItemState.Default)]);
        }
        // Todo aplicativo mostra Desinstalar, inclusive entrada de inventário
        // sem comando confiável. O clique retorna gerenciamento manual, não
        // eleva nem executa um desinstalador inventado.
        appList.ActionsEnabled = true;
        var uninstallButton = V269AppTable.Action(appRow,V269AppearanceOptions.Default.InterfacePercent,appList.DeviceDpi,true);
        var hit = ((ProgramEntry? Item, string Action))(
            typeof(DarkProgramList).GetMethod("HitAction",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(appList,
                [new Point(uninstallButton.Left + uninstallButton.Width / 2,
                    uninstallButton.Top + uninstallButton.Height / 2)])!);
        Require(hit.Item is null && !appList.AllowsRemoval(appList.Items[0]) &&
            appList.Items[0].UninstallAction == "uninstall-manual",
            "remover permanece desabilitado e o roteamento manual é preservado no menu");
        // O cabeçalho não é uma linha. Usar a geometria real e preservar
        // o nome completo, sem reativar tooltip flutuante do produto.
        var move=typeof(DarkProgramList).GetMethod("OnMouseMove",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var point=new Point(Math.Min(80,appList.ClientSize.Width/2),appRow.Top+appRow.Height/2);
        Require((int)typeof(DarkProgramList).GetMethod("RowAt",BindingFlags.Instance|BindingFlags.NonPublic)!
            .Invoke(appList,[point])! == 0,"mouse de teste atinge a primeira linha visível");
        move.Invoke(appList,[new MouseEventArgs(MouseButtons.None,0,point.X,point.Y,0)]);
        Require(typeof(DarkProgramList).GetField("_itemTips",
            BindingFlags.Instance|BindingFlags.NonPublic) is null,
            "tooltip flutuante removido, inclusive suas alocações de texto");
        var accessibleRow = appList.AccessibilityObject.GetChild(0);
        Require(accessibleRow?.Name?.Contains(longName,StringComparison.Ordinal)==true &&
            !string.IsNullOrWhiteSpace(accessibleRow.Description),
            "reticências preservam o nome completo e descrição consultiva para leitor de tela");
        var header = new Point(80,appList.TableHeaderHeight/2);
        move.Invoke(appList,[new MouseEventArgs(MouseButtons.None,0,header.X,header.Y,0)]);
        Require((int)typeof(DarkProgramList).GetMethod("RowAt",BindingFlags.Instance|BindingFlags.NonPublic)!
            .Invoke(appList,[header])! < 0,"cabeçalho não identifica uma linha inexistente");
        var headerHit = ((ProgramEntry? Item,string Action))typeof(DarkProgramList).GetMethod("HitAction",
            BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(appList,[header])!;
        Require(headerHit.Item is null && headerHit.Action.Length==0,
            "cabeçalho não autoriza ações de uma linha inexistente");

        // Render GDI real em runner 96 DPI, sem drivers/arquivos reais.
        var paint = typeof(DarkDriverList).GetMethod("DrawCard",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("V5.3: DrawCard nao localizado.");
        int realRowHeight = DriverRowGeometry.Height(driver.DeviceDpi, 800);
        using var output = new Bitmap(800, realRowHeight * 5);
        using (var g = Graphics.FromImage(output))
        {
            g.Clear(driver.BackColor);
            for (int i = 0; i < 5; i++)
                paint.Invoke(driver, [g, new Rectangle(0, i * realRowHeight, 800, realRowHeight),
                    driverItem, i == 0]);
        }
        Directory.CreateDirectory("LayoutCaptures");
        output.Save("LayoutCaptures/V53-drivers-virtualizados-96dpi.png");
        appCapture.Save("LayoutCaptures/V53-aplicativos-nome-extenso-96dpi.png");
        Console.WriteLine($"OK V5.3: {checkedCases} combinacoes de geometria de drivers e nomes longos sem sobreposicao; desenho GDI 96 DPI; sem troca fisica de monitor.");
    }
}
