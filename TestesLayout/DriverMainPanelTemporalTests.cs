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
            read();
            Pump();
            if (reads != 1 || list.Items.Count != 3 || !investigate.Enabled ||
                !title.Text.Contains("Exibidos: 3") || !status.Text.Contains("completo sintético"))
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
            filter("Todos");
            Pump(20);
            if (list.Items.Count != 3) throw new Exception("G1.12: filtro Todos não restaurou as linhas.");
            foreach (var size in new[] { new Size(540, 320), new Size(500, 210),
                new Size(1200, 720), new Size(1450, 820) })
            {
                page.ClientSize = size;
                Pump();
                CheckStatus(status, refresh);
                var compact = Find<ComboBox>(page, "DriverCompactFilter");
                if (size.Height == 210 && !compact.Visible)
                    throw new Exception("G1.12: filtro compacto não apareceu na viewport mínima.");
            }
            read();
            Pump();
            if (reads != 2 || list.Items.Count != 1 || !title.Text.Contains("Exibidos: 1") ||
                !status.Text.Contains("parcial sintético") || !investigate.Enabled)
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
