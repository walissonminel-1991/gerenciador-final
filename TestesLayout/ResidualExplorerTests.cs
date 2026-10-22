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
        Require(MainForm.InventoryCoverageTitle(true) == "Inventário PARCIAL" &&
            MainForm.InventoryCoverageTitle(false) == "Inventário completo",
            "Falha em fonte do Windows não permite legenda de inventário completo");
        var failedMsix = new LocalMsixResult([], 0,
            "COMException(0x80040154)", Failures: 1);
        var incompleteInventory = new UnifiedInventoryScan([],
            new RegistryInventoryScan([], 0, 0, 0, 0, 0),
            failedMsix, 0, new GameLibraryReport(0, 0, 0, 0, false, []), 0);
        Require(incompleteInventory.Incomplete,
            "Falha MSIX propaga inventário incompleto para proteção de resíduos");
        foreach (var area in new[] { new Rectangle(0, 0, 1280, 680), new Rectangle(0, 0, 1440, 860),
            new Rectangle(-2560, 0, 2560, 1400), new Rectangle(1920, -900, 1440, 860),
            new Rectangle(0, 0, 3440, 1400), new Rectangle(0, 0, 3840, 2120) })
        foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            var requested = new Rectangle(area.Right - 120, area.Bottom - 120,
                (int)(1080 * scale), (int)(740 * scale));
            var fit = ResidualExplorerDialog.FitBounds(requested, area);
            Require(area.Contains(fit) && fit.Width <= requested.Width && fit.Height <= requested.Height,
                "Modal físico precisa caber no monitor do owner após DPI, inclusive com coordenadas negativas");
        }
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
        PreservationDetails();
        // Regressão do PC fraco: filtrar origem nao deve deixar outros
        // locais do mesmo produto visiveis no painel direito.
        var distinctOrigins = new ResidualAuditReport("App de teste", false,
            [
                new ResidualAuditFinding(@"C:\ProgramData\AplicativoTeste", "Dados comuns", 1,
                    "comum", ResidualDisposition.PreserveData, ResidualSource.CommonData),
                new ResidualAuditFinding(@"C:\Users\Teste\AppData\Roaming\AplicativoTeste", "Configuração", 1,
                    "usuario", ResidualDisposition.PreserveData, ResidualSource.UserData),
                new ResidualAuditFinding(@"HKEY_LOCAL_MACHINE\SOFTWARE\AplicativoTeste", "Registro", 1,
                    "registro", ResidualDisposition.PreserveShared, ResidualSource.Registry)
            ], [], Incomplete: true, Coverage: new ResidualCoverage(ResidualScanIssue.DepthLimit));
        using (var window = new ResidualExplorerDialog(new[] { distinctOrigins }))
        {
            window.Show(); Until(() => window.Ready && !window.Busy, "Filtro de origem não carregou");
            var origins = Descendants(window).OfType<ComboBox>().Single(c => c.Name == "ResidualSource");
            var kinds = Descendants(window).OfType<ComboBox>().Single(c => c.Name == "ResidualFilter");
            origins.SelectedIndex = 2;
            Until(() => !window.Busy && window.CurrentPage?.LocationsAvailable == 1 &&
                window.Locations.Rows.Count == 1 &&
                window.Locations.Rows[0].Key.Contains("ProgramData"),
                "Dados comuns não isolou a origem nos locais visíveis");
            Require(window.CurrentPage!.Reports.Single().Findings.All(f => f.Sources == ResidualSource.CommonData) &&
                window.SummaryText.Contains("1 locais disponíveis"),
                "Contagem do painel inclui locais ocultos por filtro");
            kinds.SelectedIndex = 3;
            Until(() => !window.Busy && window.CurrentPage?.Matching == 0 &&
                window.Locations.Rows.Count == 0,
                "Categoria compartilhada e Dados comuns deveriam ter interseção vazia");
            Require(window.Locations.EmptyMessage.Contains("Nenhum local corresponde") &&
                window.Groups.EmptyMessage.Contains("Nenhum grupo corresponde"),
                "Filtro vazio deve explicar que não houve correspondência");
            kinds.SelectedIndex = 1; origins.SelectedIndex = 0;
            Until(() => !window.Busy && window.CurrentPage?.Matching == 0,
                "Análise parcial não deve inventar candidato revisável");
            Require(window.Groups.EmptyMessage.Contains("Nenhum candidato revisável") &&
                window.SummaryText.Contains("ANÁLISE PARCIAL"),
                "A classificação revisável vazia deve preservar o alerta parcial");
            kinds.SelectedIndex = 0; origins.SelectedIndex = 4;
            Until(() => !window.Busy && window.Locations.Rows.Count == 1 &&
                window.Locations.Rows[0].Key.Contains("HKEY_LOCAL_MACHINE"),
                "Alternar entre origens não restaurou Registro");
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
        using (var window = new ResidualExplorerDialog(reports,
            (_, _) => Task.FromException<ResidualInspectionResult>(new InvalidDataException("Recusa sintética do guard"))))
        {
            window.Show(); Until(() => window.Ready && !window.Busy, "Fotografia para recusa indisponível");
            string previousKey = window.Locations.SelectedRow!.Key;
            Find(window, "ResidualInspect").PerformClick();
            Until(() => !window.Busy && window.SummaryText.Contains("Análise parcial"),
                "Recusa de segurança escapou da fronteira da interface");
            Require(window.Locations.SelectedRow?.Key == previousKey && window.CurrentPage?.Matching == 1000 &&
                Find(window, "ResidualInspect").Enabled && !window.DetailText.Contains("Recusa sintética"),
                "Recusa deve preservar fotografia, liberar interface e não expor detalhe interno");
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
            Require(window.SummaryText.Contains("ANÁLISE PARCIAL") && window.SummaryText.Contains("outros itens preservados"), "Parcial vazio foi escondido"); window.Close();
        }
        // PC fraco .23: o botão Resumo deve distinguir falha pontual
        // de origem inteira indisponível, sem misturar indicadores.
        using (var window = new ResidualExplorerDialog(new ResidualReportSet([],
            new ResidualCoverage(ResidualScanIssue.ReadFailure | ResidualScanIssue.DepthLimit,
                EntriesExamined: 2110,
                SourcesWithReadFailures: ["ProgramData", "HKEY_LOCAL_MACHINE"]))))
        {
            window.Show(); Until(() => window.Ready && !window.Busy, "Resumo de parcialidade não carregou");
            Find(window, "ResidualCoverage").PerformClick();
            Require(window.DetailText.Contains("FONTES COM FALHAS PONTUAIS", StringComparison.Ordinal) &&
                window.DetailText.Contains("Entradas contabilizadas: 2110 (total agregado)", StringComparison.Ordinal) &&
                !window.DetailText.Contains("FONTES NÃO CONSULTADAS OU INACESSÍVEIS", StringComparison.Ordinal) &&
                window.SummaryText.Contains("ANÁLISE PARCIAL", StringComparison.Ordinal),
                "Janela não diferencia origem parcial e contagem agregada");
            window.Close();
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

    private static void PreservationDetails()
    {
        string folder = @"C:\Fixture\PerfilAlternativo";
        var report = new ResidualAuditReport("Perfil sintético", false,
            [new(folder, "Perfil", 1, "Vínculo não confirmado", ResidualDisposition.PreserveData)], []);
        ResidualInspectionEntry[] entries =
        [
            new("places.sqlite", folder + @"\places.sqlite", false, ResidualDisposition.PreserveData),
            new("cache2", folder + @"\cache2", true, ResidualDisposition.PreserveData, true),
            new("Backup", folder + @"\Backup", true, ResidualDisposition.PreserveData),
            new("key4.db", folder + @"\key4.db", false, ResidualDisposition.PreserveData),
            new("desconhecido", folder + @"\desconhecido", false, ResidualDisposition.PreserveData)
        ];
        foreach (var size in new[] { new Size(1280, 720), new Size(1440, 900), new Size(2560, 1440) })
        {
            using var window = new ResidualExplorerDialog([report], (path, _) => Task.FromResult(
                new ResidualInspectionResult(path, entries, new(), "Metadados sintéticos; conteúdo não lido.")));
            window.Show(); window.ClientSize = ResidualExplorerDialog.FitClient(new Rectangle(Point.Empty, size));
            Until(() => window.Ready && !window.Busy, "Fotografia de preservação indisponível");
            Find(window, "ResidualInspect").PerformClick();
            Until(() => !window.Busy && window.Locations.Rows.Count == entries.Length,
                "Motivos não carregaram na inspeção");
            for (int i = 0; i < entries.Length; i++)
            {
                window.Locations.SelectRow(i);
                Require(window.DetailText.Contains(entries[i].PreservationExplanation) &&
                    window.DetailText.IndexOf(entries[i].PreservationExplanation, StringComparison.Ordinal) <
                        window.DetailText.IndexOf("COBERTURA DAS ANÁLISES", StringComparison.Ordinal) &&
                    window.DetailText.Contains("Pista pelo nome; finalidade e vínculo não confirmados") &&
                    window.DetailText.Contains("outros itens preservados") &&
                    Find(window, "ResidualInspect").Enabled == entries[i].CanInspect,
                    "Detalhe deve explicar preservação e incerteza sem autorizar ação adicional");
                var editor = Descendants(window).OfType<TextBox>().Single(t => t.Multiline);
                Require(editor.ReadOnly && editor.WordWrap && editor.Text == window.DetailText &&
                    editor.ClientSize.Width > 0 && editor.ClientSize.Height > 0 &&
                    editor.Parent!.ClientRectangle.Contains(editor.Bounds),
                    "Explicação deve permanecer disponível por texto nativo legível e dentro do painel");
            }
            window.Locations.SelectRow(0); window.Refresh(); Application.DoEvents();
            Directory.CreateDirectory("LayoutCaptures");
            using var shot = new Bitmap(window.Width, window.Height);
            window.DrawToBitmap(shot, new Rectangle(Point.Empty, window.Size));
            shot.Save($"LayoutCaptures/Residuos-Preservacao-{size.Width}x{size.Height}.png");
            Require(Descendants(window).OfType<Button>().All(b => !b.Text.Contains("Excluir") && !b.Text.Contains("Remover")),
                "Motivos de preservação não podem criar ação destrutiva");
            window.Close();
        }
        Console.WriteLine("OK Preservação: explicações e incerteza na inspeção em 720p e nos dois PCs; sem ações destrutivas");
    }
}
