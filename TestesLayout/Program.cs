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
        using var deadline = new System.Threading.Timer(_ => Environment.FailFast("Timeout no teste de layout"), null, 90000, Timeout.Infinite);
        Application.EnableVisualStyles();
        Console.WriteLine("Criando janela");
        using var form = new MainForm(startServices: false);
        form.MaximumSize = new Size(4096, 4096);
        Console.WriteLine("Exibindo janela");
        form.Show();
        Console.WriteLine("Janela exibida");
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        surface.Dock = DockStyle.None;
        var nav = Descendants(form).OfType<DarkTabHost>().First();
        Directory.CreateDirectory("LayoutCaptures");
        foreach (var size in new[] { new Size(1250, 1160), new Size(1250, 852), new Size(970, 840) })
        {
            // O desktop do runner e 1024x768. Validar a superficie real do app
            // em tamanhos virtuais, sem o limite externo imposto ao Form pelo Windows.
            surface.Size = new Size(size.Width - 16, size.Height - 39);
            Application.DoEvents();
            foreach (int tab in new[] { 1, 2, 3, 4, 0, 1, 2 })
            {
                nav.SelectTab(tab);
                Application.DoEvents();
                using (var shot = new Bitmap(surface.Width, surface.Height))
                {
                    surface.DrawToBitmap(shot, new Rectangle(Point.Empty, surface.Size));
                    shot.Save($"LayoutCaptures/{size.Width}x{size.Height}-aba{tab}-inicio.png");
                }
                var buttons = Descendants(form).OfType<Button>().Where(b => b.Visible && b.Enabled).ToArray();
                if (tab == 1)
                {
                    string[] required = { "Temporários do usuário", "Limpeza avançada", "Otimizar unidades", "Atualizar discos", "Analisar limpeza", "Diagnóstico completo" };
                    foreach (string title in required)
                    {
                        var button = buttons.Single(b => (b.AccessibleName ?? b.Text) == title);
                        if (size.Width == 1250 && !FullyVisible(button))
                            throw new Exception($"Acao fora da tela em {size}: {title} bounds={button.Bounds}");
                    }
                }
                foreach (var button in buttons)
                {
                    button.Focus();
                    Application.DoEvents();
                    if (!FullyVisible(button)) throw new Exception($"Botao inacessivel: {size} aba={tab} {button.Text}");
                }
                using var bitmap = new Bitmap(surface.Width, surface.Height);
                surface.DrawToBitmap(bitmap, new Rectangle(Point.Empty, surface.Size));
                bitmap.Save($"LayoutCaptures/{size.Width}x{size.Height}-aba{tab}.png");
                Console.WriteLine($"OK {size} aba={tab}: {buttons.Length} botoes acessiveis");
            }
        }
        form.Close();
    }
    static bool FullyVisible(Control control)
    {
        Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
        for (Control? parent = control.Parent; parent != null && parent is not Form; parent = parent.Parent)
            if (!parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds))
            {
                Console.WriteLine($"CLIP {control.Text} tela={bounds} pai={parent.GetType().Name} area={parent.RectangleToScreen(parent.ClientRectangle)} bounds={parent.Bounds}");
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
