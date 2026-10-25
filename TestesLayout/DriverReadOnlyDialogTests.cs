using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class DriverReadOnlyDialogTests
{
    internal static void Run(MainForm owner)
    {
        var device = new DriverInventoryEntry(@"PCI\TEST\FIXTURE", "Dispositivo sintético", "Fornecedor de teste",
            "1.0", "oem17.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1234&DEV_5678"],
            PackageEvidence = new("oem17.inf", DriverStoreState.Mapped),
            WindowsEvents = [new(@"PCI\TEST\FIXTURE", "oem17.inf", new DateTime(2026, 10, 25, 12, 34, 56), DriverInstallOutcome.Failed)]
        };
        var inventory = new DriverInventoryResult([device], false, "fixture");
        var method = typeof(MainForm).GetMethod("ShowDriverReadOnlyReport", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Exception? failure = null;
        int inspected = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var dialog = Application.OpenForms.Cast<Form>().Single(f => f.Text == "Drivers — investigar sem alterações");
            try
            {
                foreach (Size size in new[] { new Size(500, 340), new Size(790, 510) })
                {
                    dialog.Size = size;
                    dialog.PerformLayout();
                    Application.DoEvents();
                    var controls = Descendants(dialog).ToArray();
                    var body = controls.OfType<DarkLogView>().Single(c => c.Name == "DriverReadOnlyReportText");
                    var buttons = controls.OfType<Button>().ToArray();
                    if (!Descendants(body).OfType<TextBox>().Single().ReadOnly || !body.RenderedText.Contains("falha registrada") || body.RenderedText.Contains("C:\\Users\\") ||
                        !body.RenderedText.Contains("nenhuma exclusão é autorizada") || buttons.Length != 3)
                        throw new Exception("Relatório de drivers deve distinguir falha histórica e manter consulta sem escrita.");
                    foreach (var button in buttons)
                        if (!dialog.RectangleToScreen(dialog.ClientRectangle).Contains(button.RectangleToScreen(button.ClientRectangle)) ||
                            button.Height < 26 || button.Width < 75)
                            throw new Exception("Ação de consulta fora do viewport do diálogo de drivers.");
                    if (size.Width == 500)
                    {
                        var scroll = Descendants(body).OfType<DarkVScrollBar>().Single();
                        if (!scroll.Visible || scroll.MaximumValue <= 0 ||
                            !body.RectangleToScreen(body.ClientRectangle).Contains(scroll.RectangleToScreen(scroll.ClientRectangle)))
                            throw new Exception("Relatório longo sem barra de rolagem escura acessível na janela pequena.");
                        var editor = Descendants(body).OfType<TextBox>().Single();
                        if (editor.RectangleToScreen(editor.ClientRectangle).IntersectsWith(
                            scroll.RectangleToScreen(scroll.ClientRectangle)))
                            throw new Exception("Editor cobre a barra do relatório de drivers.");
                        scroll.Value = scroll.MaximumValue;
                        Application.DoEvents();
                        if (scroll.Value == 0) throw new Exception("Relatório de drivers não permite alcançar o conteúdo inferior.");
                        scroll.Value = 0;
                    }
                    if (!buttons.Single(b => b.Name == "DriverVerifyInfReadOnly").Enabled ||
                        !buttons.Single(b => b.Name == "DriverOfficialReadOnly").Enabled)
                        throw new Exception("Consulta oficial e verificação do INF inacessíveis para seleção válida.");
                    Directory.CreateDirectory("LayoutCaptures");
                    using var bitmap = new Bitmap(dialog.Width, dialog.Height);
                    dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                    bitmap.Save($"LayoutCaptures/drivers-leitura-{size.Width}x{size.Height}.png");
                    inspected++;
                }
            }
            catch (Exception ex) { failure = ex; }
            finally { dialog.Close(); }
        };
        timer.Start();
        method.Invoke(owner, [inventory, device]);
        if (failure is not null) throw failure;
        if (inspected != 2) throw new Exception("Diálogo de drivers não inspecionado nas duas dimensões.");
        Console.WriteLine("OK Drivers: diálogo real de investigação em 500x340 e 790x510, sem executar consultas de rede ou manutenção");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
