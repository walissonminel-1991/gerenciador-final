using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class ResidualExplorerTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Until(Func<bool> condition, string message)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(12)) { Application.DoEvents(); Thread.Sleep(10); }
        Require(condition(), message);
    }
    private static IEnumerable<Control> Descendants(Control root)
    { foreach (Control c in root.Controls) { yield return c; foreach (var nested in Descendants(c)) yield return nested; } }
    private static Button Find(Form form, string name) => Descendants(form).OfType<Button>().Single(b => b.Name == name);
    internal static void Run()
    {
        var reports = Enumerable.Range(0, 1000).Select(i => new ResidualAuditReport("Produto" + i.ToString("D4"), false,
            Enumerable.Range(0, i == 0 ? 65 : 2).Select(j => new ResidualAuditFinding(@"C:\Fixture\Produto" + i + "\\Local" + j,
                "Dados", 1, "Fabricante" + i % 5, i % 2 == 0 ? ResidualDisposition.PreserveData : ResidualDisposition.PreserveShared,
                ResidualSource.UserData)).ToArray(), [])).ToArray();
        foreach (var size in new[] { new Size(800, 600), new Size(1280, 720), new Size(1366, 768), new Size(1440, 900),
            new Size(1600, 900), new Size(1920, 1080), new Size(2560, 1440), new Size(3840, 2160) })
        {
            using var window = new ResidualExplorerDialog(reports);
            window.Show(); window.ClientSize = ResidualExplorerDialog.FitClient(new Rectangle(Point.Empty, size));
            Until(() => window.Ready && !window.Busy, "Explorador não carregou a fotografia");
            Require(window.CurrentPage!.Matching == 1000 && window.Groups.Rows.Count == 50 && window.SummaryText.Contains("950"), "Explorador perdeu grupos antes da página");
            window.Refresh(); Application.DoEvents();
            Require(window.Groups.Controls.Count == 1 && window.Groups.PaintedRows is > 0 and < 25, "Lista criou controles ou desenhou todos os mil grupos");
            foreach (var button in Descendants(window).OfType<Button>().Where(b => b.Visible))
            {
                var bounds = window.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
                Require(window.ClientRectangle.Contains(bounds), $"Botão cortado {button.Name} em {size}");
                Require(button.Parent!.ClientRectangle.Contains(button.Bounds), $"Botão fora do painel {button.Name} em {size}");
                Require(!button.Text.Contains("Excluir") && !button.Text.Contains("Remover"), "Explorador expôs ação destrutiva");
            }
            Find(window, "ResidualLocationNext").PerformClick();
            Until(() => window.Locations.Rows.Count == 30 && window.Locations.Rows[0].Key.EndsWith("Local30"), "Página de locais perdeu continuidade após animação do botão");
            Find(window, "ResidualNext").PerformClick(); Until(() => !window.Busy && window.CurrentPage?.PageIndex == 1, "Próxima página não carregou");
            window.Groups.SelectRow(3); string selected = window.Groups.SelectedRow!.Key;
            Find(window, "ResidualPrevious").PerformClick(); Until(() => !window.Busy && window.CurrentPage?.PageIndex == 0, "Página anterior não carregou");
            Find(window, "ResidualNext").PerformClick(); Until(() => !window.Busy && window.CurrentPage?.PageIndex == 1, "Retorno à página falhou");
            Require(window.Groups.SelectedRow?.Key == selected, "Seleção da página não foi preservada");
            var search = Descendants(window).OfType<TextBox>().Single(t => t.Name == "ResidualSearch"); search.Text = "Fabricante3";
            Until(() => !window.Busy && window.CurrentPage?.Matching == 200, "Busca não percorreu todos os grupos");
            var filter = Descendants(window).OfType<ComboBox>().Single(t => t.Name == "ResidualFilter"); filter.SelectedIndex = 3;
            Until(() => !window.Busy && window.CurrentPage?.Matching == 100, "Filtro global não foi aplicado");
            Directory.CreateDirectory("LayoutCaptures");
            using (var shot = new Bitmap(window.Width, window.Height))
            {
                window.DrawToBitmap(shot, new Rectangle(Point.Empty, window.Size));
                shot.Save($"LayoutCaptures/Residuos-{size.Width}x{size.Height}.png");
            }
            Console.WriteLine($"OK Explorador de resíduos {size.Width}x{size.Height}: busca, filtro, páginas, seleção e botões");
            window.Close();
        }
        using (var window = new ResidualExplorerDialog(reports,
            (path, _) => Task.FromResult(new ResidualInspectionResult(
                path, [], new ResidualCoverage(), "Nível consultado sem itens listados."))))
        {
            window.Show(); Until(() => window.Ready && !window.Busy,
                "Fotografia para inspeção vazia indisponível");
            Find(window, "ResidualInspect").PerformClick();
            Until(() => !window.Busy && window.Locations.Rows.Count == 0 &&
                window.DetailText.Contains("Nível consultado sem itens"),
                "Inspeção vazia não atualizou o resumo");
            Require(window.Locations.EmptyMessage.Contains("Nenhum item neste nível") &&
                !window.Locations.EmptyMessage.Contains("Nenhum grupo"),
                "Inspeção vazia não deve sugerir ausência de grupos de aplicativos");
            window.Close();
        }
        var pending = new TaskCompletionSource<ResidualInspectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var window = new ResidualExplorerDialog(reports, (_, _) => pending.Task))
        {
            window.Show(); Until(() => window.Ready && !window.Busy, "Fotografia para cancelamento indisponível");
            Find(window, "ResidualInspect").PerformClick(); Until(() => window.Busy, "Inspeção não iniciou");
            Find(window, "ResidualCancel").PerformClick();
            Until(() => window.SummaryText.Contains("Consulta cancelada"), "Animação de cancelamento não entregou o evento");
            pending.SetResult(new("fictício", [], new(), "NÃO APLICAR RESULTADO ANTIGO"));
            Until(() => !window.Busy, "Cancelamento não liberou a interface");
            Require(!window.DetailText.Contains("NÃO APLICAR") && window.CurrentPage?.Matching == 1000, "Resultado atrasado/cancelado substituiu fotografia");
            window.Close();
        }
        var empty = new ResidualReportSet([], new ResidualCoverage(ResidualScanIssue.Timeout, CandidatesNotEvaluated: null));
        using (var window = new ResidualExplorerDialog(empty))
        {
            window.Show(); Until(() => window.Ready && !window.Busy, "Parcial vazio não carregou");
            Require(window.SummaryText.Contains("ANÁLISE PARCIAL") && window.SummaryText.Contains("Exclusão indisponível"), "Parcial vazio foi escondido"); window.Close();
        }
        // Fonte historica pode marcar Incomplete sem inserir flags em Coverage.
        // O resumo exibido deve preservar a advertencia, mesmo com zero locais.
        using (var window = new ResidualExplorerDialog(new[]
        {
            new ResidualAuditReport("Produto incompleto", false, [], [],
                Incomplete: true)
        }))
        {
            window.Show(); Until(() => window.Ready && !window.Busy,
                "Relatorio parcial legado nao abriu");
            Require(window.SummaryText.Contains("ANÁLISE PARCIAL") &&
                window.CurrentPage!.Coverage.Incomplete,
                "UI nao deve exibir relatório incompleto como análise concluída");
            window.Close();
        }
        using (var host = new MainForm(startServices: false))
        {
            host.Show(); Application.DoEvents(); host.MinimizeToTray();
            host.PresentResidualReports(reports); Application.DoEvents();
            Require(host.IsInTray && host.HasPendingResidualReview && !Application.OpenForms.OfType<ResidualExplorerDialog>().Any(),
                "Consulta concluída na bandeja abriu janela inesperada");
            using var closer = new System.Windows.Forms.Timer { Interval = 25 };
            bool reopened = false;
            closer.Tick += (_, _) =>
            {
                var explorer = Application.OpenForms.OfType<ResidualExplorerDialog>().FirstOrDefault();
                if (explorer?.Ready != true) return;
                reopened = true; closer.Stop(); explorer.Close();
            };
            closer.Start(); host.RestoreFromTray();
            Until(() => reopened && !host.HasPendingResidualReview && !Application.OpenForms.OfType<ResidualExplorerDialog>().Any(),
                "Resultado da bandeja não foi apresentado uma vez após restauração");
            host.Close();
        }
        Console.WriteLine("OK Explorador: parcial vazio, cancelamento e restauração da bandeja sem janela inesperada");
    }
}
