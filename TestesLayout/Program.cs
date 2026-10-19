using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class LayoutTests
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        using var form = new MainForm(startServices: false);
        form.MaximumSize = new Size(4096, 4096);
        form.Show();
        var nav = Descendants(form).OfType<DarkTabHost>().First();
        Directory.CreateDirectory("LayoutCaptures");
        foreach (var size in new[] { new Size(1250, 1160), new Size(1250, 852), new Size(970, 840) })
        {
            form.Size = size;
            Application.DoEvents();
            foreach (int tab in new[] { 1, 2, 3, 4, 0, 1, 2 })
            {
                nav.SelectTab(tab);
                Application.DoEvents();
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
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save($"LayoutCaptures/{size.Width}x{size.Height}-aba{tab}.png");
                Console.WriteLine($"OK {size} aba={tab}: {buttons.Length} botoes acessiveis");
            }
        }
        form.Close();
    }
    static bool FullyVisible(Control control)
    {
        Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
        for (Control? parent = control.Parent; parent != null; parent = parent.Parent)
            if (!parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds)) return false;
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
