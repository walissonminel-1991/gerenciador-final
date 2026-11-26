using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class WheelScrollTests
{
    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception("V5 roda de rolagem: " + reason);
    }

    private static void Wheel(Control target, int delta)
    {
        var method = target.GetType().GetMethod("OnMouseWheel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("OnMouseWheel nao encontrado");
        method.Invoke(target, new object[] { new MouseEventArgs(MouseButtons.None, 0, 30, 30, delta) });
    }

    internal static void Run()
    {
        var accumulator = new WheelDeltaAccumulator();
        Check(accumulator.Consume(30) == 0 &&
              accumulator.Consume(30) == 0 &&
              accumulator.Consume(30) == 0 &&
              accumulator.Consume(30) == 1,
              "quatro deltas +30 devem equivaler a um entalhe +120");
        Check(accumulator.Consume(-60) == 0 &&
              accumulator.Consume(60) == 0 &&
              accumulator.Consume(-240) == -2 &&
              accumulator.Consume(360) == 3,
              "inversao, reversao de resto e deltas maiores devem preservar o sinal");

        using var list = new DarkProgramList { Size = new Size(800, 260), ItemHeight = 67 };
        list.SetItems(Enumerable.Range(0, 80).Select(i => new ProgramEntry { Name = "Fixture " + i }));
        var listBar = list.Controls.Find("V269AppsVerticalScroll",false).OfType<DarkVScrollBar>().Single();
        var horizontalBar=list.Controls.Find("V269AppsHorizontalScroll",false).OfType<DarkVScrollBar>().Single();
        Check(!horizontalBar.Visible&&horizontalBar.MaximumValue==0,
            "modo legado nao deve ativar a barra horizontal");
        Check(listBar.MaximumValue > 600, "fixture de lista nao tem rolagem");
        for (int i = 0; i < 3; i++) Wheel(list, -30);
        Check(listBar.Value == 0, "deltas parciais nao podem saltar tres linhas de cada vez");
        Wheel(list, -30);
        Check(listBar.Value == 3 * list.ItemHeight, "quatro deltas parciais devem rolar tres linhas");
        for (int i = 0; i < 3; i++) Wheel(list, 30);
        Check(listBar.Value == 3 * list.ItemHeight, "reversao parcial nao deve produzir salto");
        Wheel(list, 30);
        Check(listBar.Value == 0, "inversao de entalhe deve retornar ao topo");

        // A aba DRIVERS utiliza lista virtualizada propria e deve
        // manter a mesma proporcionalidade sem executar acao em drivers.
        using var drivers = new DarkDriverList { Size = new Size(800, 260) };
        drivers.SetItems(Enumerable.Range(0, 80).Select(i => new DriverInventoryEntry(
            "TEST\\\\ID" + i, "Driver fixture " + i, "Fabricante sintético",
            "1.2.3." + i, "oem" + i + ".inf", true, null)));
        var driverBar = drivers.Controls.OfType<DarkVScrollBar>().Single();
        Check(driverBar.MaximumValue > 600, "fixture de drivers nao tem rolagem");
        for (int i = 0; i < 2; i++) Wheel(drivers, -40);
        Check(driverBar.Value == 0, "drivers nao devem amplificar deltas parciais");
        Wheel(drivers, -40);
        // O limite da barra exclui a altura visível da lista.
        int rowHeight = (driverBar.MaximumValue + drivers.ClientSize.Height) / drivers.Items.Count;
        Check(driverBar.Value == 3 * rowHeight, "drivers devem acumular -120 e rolar tres linhas");
        for (int i = 0; i < 3; i++) Wheel(drivers, 40);
        Check(driverBar.Value == 0, "drivers devem retornar sem salto na inversao");

        using var flow = new DarkScrollableFlow { Size = new Size(300, 220) };
        flow.Content.Controls.Add(new Panel { Size = new Size(210, 850) });
        flow.Reflow();
        var flowBar = flow.Controls.OfType<DarkVScrollBar>().Single();
        Check(flowBar.MaximumValue > 300, "fixture de painel nao tem rolagem");
        for (int i = 0; i < 2; i++) Wheel(flow.Content, -40);
        Check(flowBar.Value == 0, "painel nao deve amplificar deltas -40");
        Wheel(flow.Content, -40);
        Check(flowBar.Value == 85, "painel deve acumular -120 e rolar 85 px");
        Wheel(flow.Controls.OfType<Panel>().Single(), 120);
        Check(flowBar.Value == 0, "painel e viewport devem compartilhar o acumulador");
        // Contrato V2.6.9: frações de entalhe devem mover pixels imediatamente,
        // sem alterar o curso de um entalhe completo ou o comportamento legado.
        var pixel = new PixelWheelDeltaAccumulator();
        int pos = 0;
        for (int i = 1; i <= 4; i++)
        {
            pos = pixel.Move(pos, 1000, -30, 85);
            Check(pos == (85 * i) / 4, "painel V269 deve avançar por frações de pixel acumuladas");
        }
        Check(pixel.Move(pos, 1000, 120, 85) == 0, "pixel deve inverter até o início");
        Check(pixel.Move(0, 1000, 120, 85) == 0 &&
              pixel.Move(0, 1000, -30, 85) == 21, "limite não retém pressão invisível");
        Check(pixel.Move(21, 1000, -int.MaxValue, 85) == 1000 &&
              pixel.Move(1000, 1000, int.MaxValue, 85) == 0, "limites sem overflow");
        var horizontalPixel = new PixelWheelDeltaAccumulator();
        Check(horizontalPixel.Move(0,1000,-30,64)==16 &&
              pixel.Move(0,1000,-30,85)==21, "eixos independentes");

        list.ApplyV269Table(V269AppearanceOptions.Default);
        listBar.Value = 0;
        int appStep = 3 * list.ItemHeight;
        for (int i=1;i<=4;i++)
        {
            Wheel(list,-30);
            Check(listBar.Value == appStep*i/4, "Aplicativos V269 desloca cada delta parcial");
        }
        Wheel(list,120);
        Check(listBar.Value==0, "Aplicativos V269 inverte entalhe");

        drivers.UseV269Table();
        drivers.ApplyTableAppearance(V269AppearanceOptions.Default,800);
        driverBar.Value=0;
        int driverStep=3*drivers.TableRowHeight;
        for (int i=1;i<=3;i++)
        {
            Wheel(drivers,-40);
            Check(driverBar.Value==driverStep*i/3, "Drivers V269 desloca cada delta parcial");
        }
        Wheel(drivers,120);
        Check(driverBar.Value==0, "Drivers V269 inverte entalhe");

        flow.IsPageViewport=true;
        flow.Reflow();
        flowBar.Value=0;
        Wheel(flow.Content,-40);
        Check(flowBar.Value==28, "viewport V269 responde ao primeiro delta parcial");
        Wheel(flow.Controls.OfType<Panel>().Single(),-80);
        Check(flowBar.Value==85, "viewport e conteúdo compartilham fração de pixel");
        Wheel(flow.Content,120);
        Check(flowBar.Value==0, "viewport V269 retorna ao início");
        Console.WriteLine("OK V5: rolagem proporcional com deltas parciais e inversao nas listas de aplicativos/drivers e paineis.");
        Console.WriteLine("OK V269: deltas parciais respondem imediatamente, limites e curso total preservados.");
    }
}
