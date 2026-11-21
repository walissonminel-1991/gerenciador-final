using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// Exercita o PAINEL WinForms real; leituras são Task sintéticas e não
// acessam WMI/SetupAPI/Windows Update nem escrevem histórico no runner.
internal static class DriverMainPanelTemporalTests
{
    internal static void Run(MainForm owner)
    {
        var a = new DriverInventoryEntry(@"PCI\G112\A", "Dispositivo com falha PnP",
            "Fornecedor A", "1.0", "oem17.inf", true, true) { PnpProblemCode = 28 };
        var b = new DriverInventoryEntry(@"PCI\G112\B", "Dispositivo ausente",
            "Fornecedor B", "2.0", "oem18.inf", null, false);
        var c = new DriverInventoryEntry(@"PCI\G112\C", "Dispositivo presente",
            "Fornecedor C", "3.0", "oem19.inf", true, true);
        var snapshots = new[]
        {
            new DriverInventoryResult([a, b, c], false, "completo sintético"),
            new DriverInventoryResult([a], true, "parcial sintético"),
            new DriverInventoryResult([], false, "vazio sintético")
        };
        int reads = 0;
        using (var page = Open(owner,
            _ => Task.FromResult(snapshots[Math.Min(reads++, snapshots.Length - 1)]),
            TimeSpan.FromSeconds(3), out var read, out var filter))
        {
            var list = Find<DarkDriverList>(page, "DriverInventoryList");
            var status = Find<Label>(page, "DriverInventoryStatus");
            var refresh = Find<Button>(page, "DriverInventoryRefresh");
            var investigate = Find<Button>(page, "DriverReadOnlyInvestigation");
            var search = Find<TextBox>(page, "DriverInventorySearch");
            var title = Find<Label>(page, "DriverInventoryTitle");
            var copySummary = Find<Button>(page, "DriverCopySafetySummary");
            var sim = Find<Button>(page, "DriverSyntheticLabOpen");
            if (list.ActionsCurrent)
                throw new Exception("Drivers: ações por cartão habilitadas antes da consulta.");
            if (!sim.Enabled) throw new Exception("UX3: laboratório inacessível antes da consulta.");
            string initialStatus = status.Text;
            sim.PerformClick();
            // ModernButton despacha Click após animação de 145 ms.
            Pump(250);
            var lab = Application.OpenForms.Cast<Form>().SingleOrDefault(
                form => form.Name == "DriverLabSimulationForm");
            if (lab is null) throw new Exception("UX3: janela fictícia não abriu.");
            var selector = Find<ComboBox>(lab, "DriverLabScenarioSelect");
            var preview = Find<TextBox>(lab, "DriverLabPreview");
            if (selector.Items.Count != 17 ||
                !preview.Text.Contains("CENÁRIO INTEIRAMENTE FICTÍCIO") ||
                reads != 0 || status.Text != initialStatus)
                throw new Exception("UX3: laboratório não é independente.");
            sim.PerformClick();
            Pump(250);
            if (Application.OpenForms.Cast<Form>().Count(
                form => form.Name == "DriverLabSimulationForm") != 1)
                throw new Exception("UX3: janela duplicada.");
            selector.SelectedIndex = selector.Items.Count - 1;
            Pump(20);
            if (reads != 0 || !preview.Text.Contains("NENHUM DRIVER FOI ALTERADO"))
                throw new Exception("UX3: cenário fictício fez consulta nativa.");
            lab.Close();
            Pump(20);
            if (copySummary.Enabled)
                throw new Exception("DRIVERS: resumo disponível sem consulta.");
            read();
            Pump();
            if (reads != 1 || list.Items.Count != 3 || !investigate.Enabled ||
                !title.Text.Contains("Exibidos: 3") || !status.Text.Contains("completo sintético") ||
                !copySummary.Enabled)
                throw new Exception("G1.12: publicação sintética inicial não atualizou a página.");
            CheckStatus(status, refresh);
            if (!Descendants(page).OfType<Button>().Any(x => x.Text == "Outros" &&
                x.AccessibleDescription?.Contains("indeterminada", StringComparison.Ordinal) == true))
                throw new Exception("G1.12: filtro Outros perdeu explicação sobre presença incerta.");
            if (list.AccessibleDescription?.Contains("Nenhum dispositivo selecionado") != true ||
                investigate.AccessibleDescription?.Contains("sem dispositivo selecionado") != true)
                throw new Exception("G1.12: lista/botão Investigar sem seleção não são acessíveis.");
            typeof(DarkDriverList).GetMethod("OnKeyDown",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(list, [new KeyEventArgs(Keys.Down)]);
            if (list.SelectedItem?.DeviceId != a.DeviceId ||
                list.AccessibleDescription?.Contains(a.DeviceName) != true ||
                investigate.AccessibleDescription?.Contains("dispositivo selecionado") != true)
                throw new Exception("G1.12: seta não selecionou/anunciou dispositivo ou ação Investigar.");
            var problems = Descendants(page).OfType<Button>().Single(x => x.Text == "Falhas PnP");
            filter("Problemas");
            Pump(20);
            if (list.Items.Count != 1 || list.SelectedItem?.DeviceId != a.DeviceId ||
                problems.AccessibleDescription?.Contains("Filtro ativo") != true)
                throw new Exception($"G1.12: filtro PnP falhou: itens={list.Items.Count}; selecionado={list.SelectedItem?.DeviceId ?? "(null)"}; esperado={a.DeviceId}; descrição={problems.AccessibleDescription ?? "(null)"}; título={title.Text}.");
            search.Text = "nome inexistente";
            Pump();
            if (list.Items.Count != 0 || list.SelectedItem is not null ||
                list.AccessibleDescription?.Contains("Nenhum dispositivo selecionado") != true ||
                investigate.AccessibleDescription?.Contains("sem dispositivo selecionado") != true)
                throw new Exception("G1.12: refiltro não invalidou seleção ou texto acessível de Investigar.");
            search.Clear();
            filter("Sem driver");
            Pump(20);
            if (list.Items.Count != 0)
                throw new Exception("DRIVERS: filtro sem vínculo WMI confundiu falha PnP com ausência WMI.");
            filter("Todos");
            Pump(20);
            if (list.Items.Count != 3) throw new Exception("G1.12: filtro Todos não restaurou as linhas.");
            foreach (var size in new[] { new Size(540, 320), new Size(500, 210),
                new Size(500, 460), new Size(940, 610), new Size(1200, 720),
                new Size(1450, 820) })
            {
                page.ClientSize = size;
                Pump();
                CheckStatus(status, refresh);
                var compact = Find<ComboBox>(page, "DriverCompactFilter");
                if (compact.Visible != (size.Height < 230 || size.Width < 760) ||
                    compact.Items.Count != 5)
                    throw new Exception("DRIVERS: filtro compacto não acompanhou altura e largura.");
                if (size.Width < 760 && copySummary.Visible)
                    throw new Exception("DRIVERS: botão de resumo não liberou espaço em janela estreita.");
                if (!sim.Visible || !sim.Enabled || sim.Width < 75)
                    throw new Exception("UX3: botão Simular inacessível em " + size);
                // Regressão visual observada na captura normal: os rótulos
                // não podem quebrar ou perder espaço para o texto completo.
                if (size.Width == 940)
                {
                    var filterButtons = Descendants(page).OfType<ModernButton>()
                        .Where(b => b.Text is "Falhas PnP" or "Sem driver").ToArray();
                    if (filterButtons.Length != 2 || filterButtons.Any(b =>
                        b.AllowMultilineText ||
                        b.Width < TextRenderer.MeasureText(b.Text, b.Font,
                            new Size(int.MaxValue, b.Height),
                            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + 24))
                        throw new Exception("Drivers: rótulos Falhas PnP/Sem driver cortados em 940 px.");
                }
                if (list.ActionsCurrent || compact.Visible && compact.Width <= 24)
                    throw new Exception("Drivers: manutenção sintética ativa ou filtro cortado em " + size);
                int dpi = list.DeviceDpi;
                int width = Math.Max(1, list.ClientSize.Width - 20);
                int rowHeight = DriverRowGeometry.Height(dpi, width);
                using var actionFont = new Font("Segoe UI", 9f, FontStyle.Bold);
                var card = new Rectangle(0, 0, width, rowHeight);
                var actions = DriverRowGeometry.Actions(card, dpi, actionFont);
                if (!card.Contains(actions.Update) || !card.Contains(actions.Uninstall) ||
                    actions.Update.IntersectsWith(actions.Uninstall) ||
                    actions.Update.IntersectsWith(DriverRowGeometry.Status(card, dpi)))
                    throw new Exception("Drivers: ações de cartão sobrepostas ou fora da viewport em " + size);
                // Evidência visual de cartão REAL WinForms com dados de teste.
                // Nenhum leitor nativo, comando WUA ou manutenção é iniciado.
                if ((size.Width == 940 && size.Height == 610) ||
                    (size.Width == 500 && size.Height == 460))
                {
                    Directory.CreateDirectory("LayoutCaptures");
                    using var screenshot = new Bitmap(page.Width, page.Height);
                    page.DrawToBitmap(screenshot, new Rectangle(Point.Empty, page.Size));
                    screenshot.Save(Path.Combine("LayoutCaptures",
                        $"Drivers-Cartoes-{size.Width}x{size.Height}.png"));
                }
            }
            read();
            Pump();
            if (reads != 2 || list.Items.Count != 1 || !title.Text.Contains("Exibidos: 1") ||
                !status.Text.Contains("parcial sintético") || !investigate.Enabled || !copySummary.Enabled)
                throw new Exception("G1.12: inventário parcial não ficou consultivo.");
            CheckStatus(status, refresh);
            read();
            Pump();
            if (reads != 3 || list.Items.Count != 0 ||
                !title.Text.Contains("Exibidos: 0") || !investigate.Enabled)
                throw new Exception("G1.12: inventário vazio não preservou diagnóstico.");
            CheckStatus(status, refresh);
            Console.WriteLine("OK G1.12: filtros, seleção por teclado, vazio/parcial, viewport compacta e acessibilidade");
        }

        // Cancelamento da espera não interrompe a Task nativa fictícia;
        // segundo acionamento não deve iniciar outra captura.
        {
            var pending = new TaskCompletionSource<DriverInventoryResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int started = 0;
            using var page = Open(owner, _ => { started++; return pending.Task; },
                TimeSpan.FromSeconds(4), out var read, out _);
            var status = Find<Label>(page, "DriverInventoryStatus");
            var refresh = Find<Button>(page, "DriverInventoryRefresh");
            var list = Find<DarkDriverList>(page, "DriverInventoryList");
            read();
            if (started != 1 || refresh.Text != "Cancelar" ||
                refresh.AccessibleName != "Cancelar" ||
                status.Text != "Consultando drivers em segundo plano…")
                throw new Exception("G1.12: estado de leitura pendente não anuncia cancelamento.");
            CheckStatus(status, refresh);
            read();
            Pump();
            if (started != 1 || list.Items.Count != 0 ||
                !status.Text.Contains("Leitura nativa ainda encerrando"))
                throw new Exception("G1.12: cancelamento iniciou duplicata ou publicou resultado.");
            read();
            if (started != 1) throw new Exception("G1.12: segunda leitura iniciada antes da anterior terminar.");
            pending.TrySetResult(new DriverInventoryResult([a], false, "resultado tardio"));
            Pump();
            if (list.Items.Count != 0)
                throw new Exception("G1.12: resposta cancelada foi publicada após retornar tarde.");
            CheckStatus(status, refresh);
            Console.WriteLine("OK G1.12: cancelamento, resposta tardia e trava contra segunda leitura");
        }

        // Timeout curto do chamador e descarte da página: nada pode ser publicado
        // posteriormente, mesmo que a Task sintética conclua depois do fechamento.
        {
            var pending = new TaskCompletionSource<DriverInventoryResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int started = 0;
            var page = Open(owner, _ => { started++; return pending.Task; },
                TimeSpan.FromMilliseconds(35), out var read, out _);
            var status = Find<Label>(page, "DriverInventoryStatus");
            var list = Find<DarkDriverList>(page, "DriverInventoryList");
            read();
            Pump(240);
            if (started != 1 || list.Items.Count != 0 ||
                (!status.Text.Contains("Tempo esgotado") &&
                 !status.Text.Contains("Leitura nativa ainda encerrando")))
                throw new Exception($"G1.12: timeout divergente: iniciou={started}; itens={list.Items.Count}; estado={status.Text}.");
            // O Text do botão é restaurado no finally do handler assíncrono.
            // Não pressionar de novo enquanto a primeira ação ainda está
            // terminando o próprio cancelamento na thread WinForms.
            var refresh = Find<Button>(page, "DriverInventoryRefresh");
            PumpUntil(() => refresh.Text == "Consultar drivers");
            read();
            Pump(30);
            if (started != 1 || list.Items.Count != 0 ||
                !status.Text.Contains("leitura nativa", StringComparison.OrdinalIgnoreCase) ||
                !status.Text.Contains("encerrando", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"G1.12: pós-timeout: iniciou={started}; itens={list.Items.Count}; estado={status.Text}; botão={Find<Button>(page, "DriverInventoryRefresh").Text}.");
            CheckStatus(status, Find<Button>(page, "DriverInventoryRefresh"));
            page.Dispose();
            pending.TrySetResult(new DriverInventoryResult([a], false, "tardio após descarte"));
            Pump();
            Console.WriteLine("OK G1.12: timeout WaitAsync e descarte impedem publicação tardia");
        }
    }

    private static Form Open(MainForm owner,
        Func<CancellationToken, Task<DriverInventoryResult>> fake,
        TimeSpan timeout, out Action read, out Action<string> filter)
    {
        Action? action = null;
        Action<string>? applyFilter = null;
        var method = typeof(MainForm).GetMethod("BuildDriversPlaceholder",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var control = (Control)method.Invoke(owner, [fake, (TimeSpan?)timeout,
            (Action<Action>)(callback => action = callback),
            (Action<Action<string>>)(callback => applyFilter = callback)])!;
        var form = new Form { Text = "G1.12 Fixture", ClientSize = new Size(800, 520),
            ShowInTaskbar = false };
        control.Dock = DockStyle.Fill;
        form.Controls.Add(control);
        form.Show(owner);
        Pump();
        read = action ?? throw new Exception("G1.12: callback real não disponível.");
        filter = applyFilter ?? throw new Exception("G1.12: ação real de filtro não disponível.");
        return form;
    }

    private static void Pump(int milliseconds = 90)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Application.DoEvents();
    }

    private static void PumpUntil(Func<bool> condition, int timeoutMs = 1800)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        if (!condition()) throw new Exception("G1.12: handler de timeout não restaurou a ação Consultar drivers.");
    }

    private static void CheckStatus(Label status, Button refresh)
    {
        if (status.AccessibleName != "Estado da consulta de drivers" ||
            status.AccessibleDescription?.Contains(status.Text, StringComparison.Ordinal) != true ||
            refresh.AccessibleName != refresh.Text ||
            string.IsNullOrWhiteSpace(refresh.AccessibleDescription))
            throw new Exception($"G1.12: acessibilidade divergente: estadoNome={status.AccessibleName}; estadoTexto={status.Text}; descriçãoStatus={status.AccessibleDescription}; botãoTexto={refresh.Text}; botãoNome={refresh.AccessibleName}; descriçãoBotão={refresh.AccessibleDescription}.");
    }

    private static T Find<T>(Control parent, string name) where T : Control =>
        Descendants(parent).OfType<T>().Single(x => x.Name == name);

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var child in Descendants(c)) yield return child;
        }
    }
}
