using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class LayoutTests
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        using var deadline = new System.Threading.Timer(
            _ => Environment.FailFast("Timeout no teste de layout"),
            null, TimeSpan.FromMinutes(4), Timeout.InfiniteTimeSpan);
        Application.EnableVisualStyles();

        TestStartupBounds();

        Console.WriteLine("Criando janela de teste, sem iniciar os servicos");
        using var form = new MainForm(startServices: false);
        form.MaximumSize = new Size(4096, 4096);
        form.Show();
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        surface.Dock = DockStyle.None;
        var nav = Descendants(form).OfType<DarkTabHost>().First();
        Directory.CreateDirectory("LayoutCaptures");

        int tested = 0;
        foreach (var (name, resolution) in StartupWindowSizing.ValidationMatrix)
        {
            // O runner Windows pode ter desktop fisico de 1024x768. Usamos uma
            // superficie virtual na resolucao alvo, sem mudar o monitor do runner.
            var screen = new Size(resolution.Width, Math.Max(480, resolution.Height - 48));
            surface.Size = new Size(screen.Width - 16, screen.Height - 39);
            Application.DoEvents();

            bool capture = resolution.Width <= 1366 &&
                (resolution.Height <= 800 || resolution == new Size(1280, 1024));
            foreach (int tab in new[] { 0, 1, 2, 3, 4 })
            {
                nav.SelectTab(tab);
                Application.DoEvents();

                var allButtons = Descendants(form).OfType<Button>()
                    .Where(b => b.Visible).ToArray();
                // A aba Energia so habilita seus comandos depois de consultar
                // o Windows. No teste isolado sem servicos eles devem continuar
                // visiveis, mas desabilitados por seguranca.
                if (allButtons.Length == 0)
                    throw new Exception($"Nenhum botao apresentado: {name} {resolution} aba={tab}");
                var buttons = allButtons.Where(b => b.Enabled).ToArray();

                // Navegacao somente: nao clicar nem executar comandos destrutivos.
                foreach (var button in buttons)
                {
                    if (!button.Focus())
                        throw new Exception($"Botao sem foco: {resolution} aba={tab} {button.Text}");
                    Application.DoEvents();
                    if (!FullyVisible(button))
                        throw new Exception($"Botao inacessivel: {resolution} aba={tab} {button.Text}");
                }

                if (capture)
                {
                    // Nao criar bitmaps gigantes (5K/8K); nesses casos a
                    // cobertura verifica geometria, foco e navegabilidade.
                    using var shot = new Bitmap(surface.Width, surface.Height);
                    surface.DrawToBitmap(shot, new Rectangle(Point.Empty, surface.Size));
                    shot.Save($"LayoutCaptures/{resolution.Width}x{resolution.Height}-aba{tab}.png");
                }
                Console.WriteLine($"OK {name} {resolution.Width}x{resolution.Height} aba={tab}: {buttons.Length} botoes");
            }
            tested++;
        }
        if (tested != 21) throw new Exception($"Esperadas 21 resolucoes; encontradas {tested}");
        form.Close();
        Console.WriteLine($"PASSOU: {tested} resolucoes, 5 abas, acessibilidade por foco, janela na area util.");
    }

    private static void TestStartupBounds()
    {
        if (StartupWindowSizing.ValidationMatrix.Length != 21)
            throw new Exception("A matriz precisa de 21 resolucoes.");
        foreach (var (name, resolution) in StartupWindowSizing.ValidationMatrix)
        {
            // Simular barra de tarefas de 48 pixels e area util com origem
            // deslocada, como ocorre em um monitor secundario.
            var workArea = new Rectangle(100, 64, resolution.Width, resolution.Height - 48);
            Rectangle opening = StartupWindowSizing.Fit(workArea);
            if (!workArea.Contains(opening) ||
                opening.Width > workArea.Width || opening.Height > workArea.Height)
                throw new Exception($"Janela inicial fora da tela: {name} {opening}");
            Size min = StartupWindowSizing.AllowedMinimum(opening);
            if (min.Width > opening.Width || min.Height > opening.Height)
                throw new Exception($"Tamanho minimo impede abertura: {name}");
            var outside = new Rectangle(-3000, -3000, 2000, 1400);
            Rectangle recovered = StartupWindowSizing.KeepVisible(outside, workArea);
            if (!workArea.Contains(recovered))
                throw new Exception($"Nao recuperou a janela apos troca de monitor: {name}");
            // Ao mover para monitor menor, o tamanho e limitado sem alterar os
            // limites das janelas ja validas.
            if (StartupWindowSizing.KeepVisible(opening, workArea) != opening)
                throw new Exception($"Posicao valida alterada: {name}");
            Console.WriteLine($"Fit OK {name}: {resolution.Width}x{resolution.Height}");
        }
    }

    static bool FullyVisible(Control control)
    {
        Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
        for (Control? parent = control.Parent; parent != null && parent is not Form; parent = parent.Parent)
            if (!parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds))
            {
                Console.WriteLine($"CLIP {control.Text} tela={bounds} pai={parent.GetType().Name} area={parent.RectangleToScreen(parent.ClientRectangle)}");
                return false;
            }
        return bounds.Width > 0 && bounds.Height > 0;
    }

    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child)) yield return nested;
        }
    }
}
