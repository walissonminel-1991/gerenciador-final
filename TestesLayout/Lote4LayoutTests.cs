using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// Exercita o diálogo real com contagens sintéticas; nunca abre o seletor de arquivo.
internal static class Lote4LayoutTests
{
    internal static void Run()
    {
        using var owner = new MainForm(startServices: false);
        owner.Show();
        Application.DoEvents();
        var history = (LocalReliabilityHistory)typeof(MainForm)
            .GetField("_reliability", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        const string secret = @"C:\Users\PRIVATE\nao-publicar.exe";
        for (int i = 0; i < 150; i++)
            history.RecordUninstall(new(UninstallStage.Pending, secret, secret));
        string expected = DarkLogView.NormalizeWindowsLineBreaks(history.Report([], false));
        var open = Descendants(owner).OfType<Button>()
            .Single(b => b.Name == "LocalReliabilityReportButton");
        Exception? failure = null;
        int inspected = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var dialog = Application.OpenForms.Cast<Form>()
                .Single(f => f.Text == "Histórico local — somente leitura");
            try
            {
                Directory.CreateDirectory("LayoutCaptures");
                foreach (Size size in new[] { new Size(520, 360), new Size(770, 530) })
                {
                    dialog.Size = size;
                    dialog.PerformLayout();
                    Application.DoEvents();
                    using (var capture = new Bitmap(dialog.Width, dialog.Height))
                    {
                        dialog.DrawToBitmap(capture, new Rectangle(Point.Empty, dialog.Size));
                        capture.Save($"LayoutCaptures/lote4-historico-{size.Width}x{size.Height}.png");
                    }
                    var body = Descendants(dialog).OfType<DarkLogView>()
                        .SingleOrDefault(c => c.Name == "LocalReliabilityReportText")
                        ?? throw new Exception("L4 relatório local usa rolagem nativa em vez da prévia escura acessível.");
                    var editor = Descendants(body).OfType<TextBox>().Single();
                    if (!editor.ReadOnly || !editor.WordWrap || editor.ScrollBars != ScrollBars.None ||
                        body.RenderedText != expected || body.RenderedText.Contains(secret))
                        throw new Exception("L4 relatório alterado, truncado, editável ou com identificador privado.");
                    var scroll = Descendants(body).OfType<DarkVScrollBar>().Single();
                    if (!scroll.Visible || scroll.MaximumValue <= 0 ||
                        !body.RectangleToScreen(body.ClientRectangle).Contains(scroll.RectangleToScreen(scroll.ClientRectangle)))
                        throw new Exception("L4 relatório longo sem barra escura acessível.");
                    scroll.Value = scroll.MaximumValue;
                    Application.DoEvents();
                    if (scroll.Value == 0 || body.RenderedText != expected)
                        throw new Exception("L4 não alcança o fim do histórico ou rolagem altera o relatório.");
                    scroll.Value = 0;
                    foreach (var button in Descendants(dialog).OfType<Button>())
                        if (!dialog.RectangleToScreen(dialog.ClientRectangle).Contains(button.RectangleToScreen(button.ClientRectangle)) ||
                            button.Width < 90 || button.Height < 26)
                            throw new Exception("L4 ação de relatório fora do viewport compacto.");
                    inspected++;
                }
                var close = Descendants(dialog).OfType<Button>()
                    .SingleOrDefault(b => b.Name == "CloseLocalReliabilityReport")
                    ?? throw new Exception("L4 relatório sem botão Fechar.");
                if (dialog.CancelButton != close || dialog.ShowInTaskbar || dialog.MinimizeBox)
                    throw new Exception("L4 diálogo local não oferece fechamento por Esc junto da janela principal.");
                close.PerformClick();
                if (dialog.Visible) throw new Exception("L4 botão Fechar não encerrou o diálogo.");
            }
            catch (Exception ex) { failure = ex; }
            finally { dialog.Close(); }
        };
        timer.Start();
        open.PerformClick();
        if (failure is not null) throw failure;
        if (inspected != 2) throw new Exception("L4 cenário não executou as duas dimensões do diálogo real.");
        Console.WriteLine("PASS L4 relatório real: 520x360 e 770x530, rolagem escura, texto íntegro, privacidade e fechamento");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
