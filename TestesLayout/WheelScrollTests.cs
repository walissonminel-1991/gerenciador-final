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
        var listBar = list.Controls.OfType<DarkVScrollBar>().Single();
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
        Check(driverBar.Value == 3 * 73, "drivers devem acumular -120 e rolar tres linhas");
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
        Console.WriteLine("OK V5: rolagem proporcional com deltas parciais e inversao nas listas de aplicativos/drivers e paineis.");
    }
}
