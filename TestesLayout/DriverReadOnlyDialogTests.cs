using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class DriverReadOnlyDialogTests
{
    internal static void Run(MainForm owner)
    {
        RunCase(owner, gaming: false);
        RunCase(owner, gaming: true);
        Console.WriteLine("OK Drivers: diálogo real de investigação em 500x340 e 790x510, sem executar consultas de rede ou manutenção");
    }

    private static void RunCase(MainForm owner, bool gaming)
    {
        const string gpuId = @"PCI\VEN_10DE&DEV_2D04";
        string deviceId = gaming ? gpuId + @"\FIXTURE" : @"PCI\TEST\FIXTURE";
        string driverClass = gaming ? "Display" : "Printer";
        var device = new DriverInventoryEntry(deviceId, "Dispositivo sintético", gaming ? "NVIDIA" : "Fornecedor de teste",
            gaming ? "32.0.16.2124" : "1.0", "oem17.inf", true, true)
        {
            HardwareIds = [gaming ? gpuId : @"PCI\VEN_1234&DEV_5678"],
            PackageEvidence = new("oem17.inf", DriverStoreState.Mapped),
            ReportedDriverClass = driverClass,
            WindowsEvents = [new(deviceId, "oem17.inf", new DateTime(2026, 10, 25, 12, 34, 56), DriverInstallOutcome.Failed)]
        };
        var inventory = new DriverInventoryResult([device], false, "fixture");
        var method = typeof(MainForm).GetMethod("ShowDriverReadOnlyReport", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Exception? failure = null;
        int inspected = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var dialog = Application.OpenForms.Cast<Form>().Single(f => f.Text == "Drivers — detalhes e evidências");
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
                    var officialGpu = controls.OfType<LinkLabel>().Single(c => c.Name == "GameDriverOfficialSite");
                    if (officialGpu.AccessibleName != "Abrir página oficial de drivers da GPU selecionada" ||
                        (!gaming && (officialGpu.Enabled || officialGpu.Visible)))
                        throw new Exception("VF02: link oficial GPU apareceu para impressora ou perdeu acessibilidade.");
                    if (gaming && (!officialGpu.Visible || !officialGpu.Enabled))
                        throw new Exception("VF02: link oficial GPU indisponível para NVIDIA selecionada.");
                    if (!Descendants(body).OfType<TextBox>().Single().ReadOnly ||
                        !body.RenderedText.Contains("falha registrada") ||
                        !body.RenderedText.Contains("Classe do driver (WMI SignedDriver): " + driverClass) ||
                        !body.RenderedText.Contains("TRIAGEM DO PC FRACO") ||
                        !body.RenderedText.Contains("WUA: consulta ainda não solicitada") ||
                        body.RenderedText.Contains("C:\\Users\\") ||
                        !body.RenderedText.Contains("nenhuma exclusão é autorizada") || buttons.Length != 3)
                        throw new Exception("Relatório de drivers deve distinguir falha histórica e manter consulta sem escrita.");
                    var graphicsLink = controls.OfType<LinkLabel>().SingleOrDefault(c => c.Name == "DriverGamingSourcesLink");
                    if (gaming && (graphicsLink is null || !graphicsLink.CanSelect ||
                        !body.RenderedText.Contains("NVIDIA GeForce RTX 5060 Ti") ||
                        !dialog.RectangleToScreen(dialog.ClientRectangle).Contains(
                            graphicsLink.RectangleToScreen(graphicsLink.ClientRectangle))))
                        throw new Exception("Fonte gráfica oficial inacessível no viewport do diálogo.");
                    if (!gaming && graphicsLink is not null)
                        throw new Exception("Fonte gráfica exibida para dispositivo de outra classe.");
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
                    if (!gaming)
                    {
                        Directory.CreateDirectory("LayoutCaptures");
                        using var bitmap = new Bitmap(dialog.Width, dialog.Height);
                        dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
                        bitmap.Save($"LayoutCaptures/drivers-leitura-{size.Width}x{size.Height}.png");
                    }
                    inspected++;
                }
            }
            catch (Exception ex) { failure = ex; }
            finally { dialog.Close(); }
        };
        timer.Start();
        // A mesma barreira temporal exigida pela interface real. A fixture
        // não inicia consulta externa; preserva o teste visual em duas resoluções.
        var guard = new DriverReportPublicationGuard(inventory, device, 1);
        method.Invoke(owner, [inventory, device,
            (Func<bool>)(() => guard.IsCurrent(inventory, device, 1)),
            (Func<CancellationToken, DriverOfficialResult, bool>)((token, result) =>
                guard.CanPublish(inventory, device, 1, token, result)),
            (Func<CancellationToken, DriverInfVerificationResult, bool>)((token, result) =>
                guard.CanPublishInf(inventory, device, 1, token, result)),
            (Action<Action?>)(_ => { }), null, null, null, null, false]);
        if (failure is not null) throw failure;
        if (inspected != 2) throw new Exception("Diálogo de drivers não inspecionado nas duas dimensões.");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
