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

        if (DiagnosticTrace.FormatVersion(new Version(5, 13, 10, 18)) != "v5.13.10.18")
            throw new Exception("Diagnostico perdeu a revisao da compilacao.");
        TestStartupBounds();
        TestWorkspaceProportions();
        TestMonitorMathAndSampling();
        TestSamplingCadence();

        Console.WriteLine("Criando janela de teste, sem iniciar os servicos");
        using var form = new MainForm(startServices: false);
        if (form.MaximizeBox || form.FormBorderStyle != FormBorderStyle.None ||
            !form.HasCustomCaption)
            throw new Exception("Barra da janela nao pode mostrar botao de maximizar.");
        form.Show();
        // O PC FRACO nao pode receber WS_EX_COMPOSITED global: esse estilo
        // transforma cada repintura nativa numa composicao de toda a arvore.
        if ((GetWindowLong(form.Handle, -20) & 0x02000000) != 0)
            throw new Exception("Composicao global pesada continua habilitada.");
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        surface.Dock = DockStyle.None;
        // O log recebe muitas mensagens na inicializacao. Validar que cada
        // uma chega completa, mas a interface faz uma so atualizacao visual.
        var log = Descendants(form).OfType<DarkLogView>().Single();
        _ = log.RenderedText; // Drenar mensagens do construtor, se houver.
        int flushesBefore = log.BatchFlushCount;
        for (int i = 0; i < 150; i++)
            log.AppendText($"Lote de diagnostico {i:D2}\n");
        if (log.BatchFlushCount != flushesBefore)
            throw new Exception("Registro redesenhou antes de consolidar a rajada.");
        string allLines = log.RenderedText;
        if (log.BatchFlushCount != flushesBefore + 1 || !allLines.Contains("Lote de diagnostico 00") ||
            !allLines.Contains("Lote de diagnostico 149") ||
            allLines.Split("Lote de diagnostico ").Length != 151)
            throw new Exception("Registro perdeu linhas ou redesenhou por mensagem.");
        Console.WriteLine("OK: 150 linhas em uma pintura do Registro.");
        var nav = Descendants(form).OfType<DarkTabHost>().First();
        string[] expectedTitles =
        {
            "Aplicativos", "DRIVERS", "Limpeza e desempenho",
            "Reparo e rede", "Planos de energia", "Inicialização e sistema"
        };
        if (nav.TabCount != 6 || !nav.TabTitles.SequenceEqual(expectedTitles))
            throw new Exception("A ordem das seis abas principais foi alterada.");
        // DRIVERS permite somente consulta local.
        nav.SelectTab(1);
        Application.DoEvents();
        Control placeholder = Descendants(form).SingleOrDefault(c =>
            c.Name == "DriversPlaceholder")
            ?? throw new Exception("Pagina DRIVERS ausente.");
        if (!placeholder.Visible || Descendants(placeholder).OfType<Button>().Count() != 1 ||
            !Descendants(placeholder).OfType<DataGridView>().Any(g => g.Name == "DriverInventoryGrid" && g.ReadOnly))
            throw new Exception("DRIVERS precisa permitir apenas consulta e mostrar tabela somente leitura.");
        if (!Descendants(placeholder).OfType<Label>().Any(c =>
                c.Text.Contains("nenhuma instalação, remoção ou alteração")))
            throw new Exception("Aviso de modulo futuro ausente.");
        Directory.CreateDirectory("LayoutCaptures");

        int tested = 0;
        // 21 resolucoes da matriz e a referencia real de 1440x900 do usuario.
        var scenarios = StartupWindowSizing.ValidationMatrix
            .Concat(new[] { ("Referencia 1440x900", new Size(1440, 900)) });
        foreach (var (name, resolution) in scenarios)
        {
            // A geometria do monitor e simulada sem alterar resolucao do runner.
            // A tela virtual 8K nao precisa criar bitmaps/GDI de 5 mil pixels:
            // testar limites no modelo e rolagem no viewport acessivel.
            var monitor = new Rectangle(0, 0, resolution.Width,
                Math.Max(300, resolution.Height - 48));
            var opening = StartupWindowSizing.Fit(monitor);
            surface.Size = new Size(
                Math.Clamp(opening.Width - 16, 520, 1400),
                Math.Clamp(opening.Height - 39, 400, 1040));
            Application.DoEvents();

            bool capture = (resolution.Width <= 1366 &&
                (resolution.Height <= 800 || resolution == new Size(1280, 1024))) ||
                resolution == new Size(1440, 900);
            // 21 resolucoes, seis abas e verificacao de margens, simetria,
            // cliques e foco (antes eram apenas cinco abas).
            for (int tab = 0; tab < 6; tab++)
            {
                nav.SelectTab(tab);
                Application.DoEvents();
                var bounds = nav.NavigationBounds;
                if (bounds.Length != 6 ||
                    bounds.Any(b => b.Width < 1 || b.Height < 1))
                    throw new Exception("Nao ha seis botoes de navegacao visiveis.");
                int firstMargin = bounds[0].Left;
                int lastMargin = nav.ClientSize.Width - bounds[^1].Right;
                if (firstMargin > 18 || lastMargin > 18 ||
                    Math.Abs(firstMargin - lastMargin) > 3)
                    throw new Exception($"Abas desalinhadas: {name} esquerda={firstMargin} direita={lastMargin}");
                if (bounds.Max(b => b.Width) - bounds.Min(b => b.Width) > 1)
                    throw new Exception($"Abas nao estao uniformes em {name}.");
                for (int i = 0; i < bounds.Length; i++)
                {
                    if (i > 0 && bounds[i].Left <= bounds[i - 1].Right)
                        throw new Exception("Abas encostadas ou sobrepostas.");
                    Point center = new(bounds[i].Left + bounds[i].Width / 2,
                        bounds[i].Top + bounds[i].Height / 2);
                    if (nav.NavigationHitTest(center) != i)
                        throw new Exception($"Hit-test incorreto: {name}, aba {i}");
                }

                // Em monitores largos, nao comprimir oito ferramentas numa
                // linha de cinco nem cinco comandos de rede numa unica linha.
                if ((resolution == new Size(1920, 1080) ||
                     resolution == new Size(2560, 1440)) && (tab == 2 || tab == 3))
                {
                    int expected = tab == 2 ? 4 : 3;
                    int toolCount = tab == 2 ? 8 : 5;
                    var toolFlow = Descendants(form).OfType<DarkScrollableFlow>()
                        .FirstOrDefault(x => x.EmbeddedInPage &&
                            x.Content.Controls.Count == toolCount);
                    if (toolFlow is null)
                        throw new Exception("Cartoes de ferramentas indisponiveis.");
                    var row = toolFlow.Content.Controls.Cast<Control>().ToArray();
                    int firstTop = row.Min(c => c.Top);
                    int firstRowCount = row.Count(c => c.Top == firstTop);
                    if (firstRowCount > expected)
                        throw new Exception("Ferramentas excessivamente comprimidas: " +
                            resolution + " aba=" + tab + " colunas=" + firstRowCount);
                }

                // Nao deixar cartes enormes quando a largura permite
                // duas ou tres linhas e nao sacrificar textos em PC FRACO.
                if ((tab == 2 || tab == 3) && resolution == new Size(1440, 900))
                {
                    int count = tab == 2 ? 8 : 5;
                    var toolFlow = Descendants(form).OfType<DarkScrollableFlow>()
                        .Single(x => x.EmbeddedInPage && x.Content.Controls.Count == count);
                    var cards = toolFlow.Content.Controls.OfType<Panel>().ToArray();
                    if (cards.Length != count || cards.Any(x => x.Height > 116))
                        throw new Exception("Cartoes ainda excessivamente altos em 1440x900: aba " + tab);
                    foreach (Panel card in cards)
                    {
                        var label = Descendants(card).OfType<Label>().Single();
                        int required = TextRenderer.MeasureText(label.Text, label.Font,
                            new Size(Math.Max(90, label.ClientSize.Width -
                                label.Padding.Horizontal), 800),
                            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
                        if (label.Height - label.Padding.Vertical + 4 < required)
                            throw new Exception("Texto de ferramenta cortado em 1440x900: " + label.Text);
                    }
                }
                if (tab == 0)
                {
                    var commands = Descendants(form).OfType<TableLayoutPanel>()
                        .FirstOrDefault(panel => panel.Controls.OfType<Button>()
                            .Any(button => button.Text == "Verificar atualizações"));
                    string[] expectedCommands = { "Atualizar lista", "Verificar atualizações",
                        "Selecionar vários", "Desinstalar selecionados", "Limpar seleção" };
                    if (commands is null ||
                        !commands.Controls.OfType<Button>()
                            .Select(button => button.Text).SequenceEqual(expectedCommands))
                        throw new Exception("Os cinco comandos de Aplicativos nao estao juntos.");
                    var search = Descendants(form).OfType<TableLayoutPanel>()
                        .FirstOrDefault(panel => panel.Controls.OfType<TextBox>()
                            .Any(t => t.PlaceholderText.Contains("Nome, versão")));
                    if (search is null || search.Controls.OfType<Button>()
                        .Any(button => button.Text == "Atualizar lista"))
                        throw new Exception("Atualizar lista nao deve aparecer junto da pesquisa.");
                    bool expectCompact = commands.ClientSize.Width <
                        (int)Math.Ceiling(870d * Math.Max(1, commands.DeviceDpi) / 96d);
                    if (commands.RowCount != (expectCompact ? 2 : 1))
                        throw new Exception("Acoes com numero incorreto de linhas: " + resolution);
                    foreach (Button button in commands.Controls.OfType<Button>())
                    {
                        if (commands.GetRow(button) < 0 ||
                            commands.GetRow(button) >= commands.RowCount ||
                            commands.GetColumn(button) < 0 ||
                            button.Right > commands.ClientSize.Width + 2)
                            throw new Exception("Acao cortada: " + name + " / " + button.Text);
                    }
                }
                if (tab == 5)
                {
                    var descriptions = Descendants(form).OfType<Label>()
                        .Where(label => label.Name.StartsWith("SystemActionDescription",
                            StringComparison.Ordinal)).OrderBy(label => label.Name).ToArray();
                    var expectedDescriptions = new[]
                    {
                        "CPU, memória e E/S por processo. Atualiza a cada 2 s com a aba aberta.",
                        "Ative ou desative entradas do seu usuário de modo reversível.",
                        "Procure, selecione e instale atualizações oficiais.",
                        "Detalhes de CPU, GPU, RAM, BIOS, placa-mãe e discos."
                    };
                    if (descriptions.Length != 4 ||
                        !descriptions.Select(x => x.Text).SequenceEqual(expectedDescriptions) ||
                        descriptions.Any(x => x.Height < 35 || x.Width < 60))
                        throw new Exception("Descricoes dos quatro botoes do Sistema ausentes ou comprimidas.");
                    if (descriptions.Any(x => !x.Visible))
                        throw new Exception("Descricoes do Sistema ocultas na aba selecionada.");
                    foreach (var label in descriptions)
                    {
                        int heightNeeded = TextRenderer.MeasureText(label.Text,
                            label.Font, new Size(Math.Max(80, label.ClientSize.Width -
                                label.Padding.Horizontal), 800),
                            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
                        if (label.ClientSize.Height - label.Padding.Vertical + 4 < heightNeeded)
                            throw new Exception($"Descricao cortada no Sistema: {name} / {label.Name}, altura={label.Height}, texto={heightNeeded}");
                    }
                }

                // Somente a pagina pode rolar quando o conteudo ultrapassar
                // seu viewport; ferramentas e discos embutidos jamais mostram
                // uma segunda barra vertical.
                foreach (var flow in Descendants(form).OfType<DarkScrollableFlow>())
                {
                    if (flow.EmbeddedInPage && flow.HasVerticalScrollbar)
                        throw new Exception($"Rolagem duplicada: {name} aba={tab}");
                    if (flow.IsPageViewport && flow.Visible &&
                        flow.Content.Controls.Count == 1 &&
                        flow.Content.Controls[0].Height <= flow.ClientSize.Height &&
                        flow.HasVerticalScrollbar)
                        throw new Exception($"Rolagem fantasma: {name} aba={tab}");
                }

                var allButtons = Descendants(form).OfType<Button>()
                    .Where(b => b.Visible).ToArray();
                // DRIVERS permite consulta, sem instalacao ou remocao.
                if (tab == 1)
                {
                    var driverGrid = Descendants(placeholder).OfType<DataGridView>().Single();
                    if (driverGrid.Height < 100)
                        throw new Exception($"Tabela DRIVERS sem altura util: {name} {resolution}");
                    if (Descendants(placeholder).OfType<Button>().Any(b => b.Name != "DriverInventoryRefresh"))
                        throw new Exception("DRIVERS ganhou uma acao diferente da consulta.");
                }
                else if (allButtons.Length == 0)
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
        const int expectedCombinations = 132;
        if (tested != 22 || tested * nav.TabCount != expectedCombinations)
            throw new Exception($"Esperadas 132 combinacoes; encontradas {tested * nav.TabCount}.");
        Console.WriteLine($"OK: {expectedCombinations} combinacoes resolucao/aba validadas.");
        // Provar no runner Windows que o botao Minimizar oculta a janela
        // e que Abrir restaura, sem encerrar a coleta nem criar janelas.
        IntPtr originalHandle = form.Handle;
        int originalTab = nav.SelectedIndex;
        var duration = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 12; i++)
        {
            form.MinimizeToTray();
            Application.DoEvents();
            // ShowInTaskbar permanece true para nao destruir o HWND.
            // A janela oculta desaparece da barra de tarefas por Hide().
            if (form.Visible || !form.ShowInTaskbar || !form.IsInTray ||
                !form.HasActiveTrayIcon || form.Handle != originalHandle)
                throw new Exception("Minimizar recriou a janela ou falhou na bandeja.");
            form.RestoreFromTray();
            Application.DoEvents();
            if (!form.Visible || !form.ShowInTaskbar || form.IsInTray ||
                form.HasActiveTrayIcon || form.Handle != originalHandle ||
                nav.SelectedIndex != originalTab)
                throw new Exception("Restaurar recriou o HWND ou perdeu o layout selecionado.");
        }
        duration.Stop();
        Console.WriteLine($"OK: 12 ciclos de bandeja, HWND estavel, {duration.ElapsedMilliseconds} ms no runner");
        // Layout util em espaco limitado: os itens e botoes devem permanecer
        // focaveis mesmo quando as paginas fazem rolagem.
        nav.SelectTab(0);
        var commandButtons = Descendants(form).OfType<ModernButton>()
            .Where(x => x.Visible && !x.IsDisposed).ToArray();
        if (commandButtons.Any(x => x.Text.Contains('\n')))
            throw new Exception("Botoes ainda carregam quebra de linha forcada.");
        var programList = Descendants(form).OfType<DarkProgramList>().Single();
        if (programList.ItemHeight > 70)
            throw new Exception("Lista de programas ainda usa linhas excessivamente altas.");
        nav.SelectTab(4);
        Application.DoEvents();
        var activateButtons = Descendants(form).OfType<ModernButton>()
            .Where(b => b.Text == "Ativar" && b.Parent is TableLayoutPanel).ToArray();
        if (activateButtons.Length != 3 ||
            activateButtons.Any(b => b.Width > 130 || b.Height > 33))
            throw new Exception("Botoes Ativar ainda demasiadamente largos.");
        nav.SelectTab(3);
        Application.DoEvents();
        var repairTools = Descendants(form).OfType<ModernButton>()
            .Where(b => b.Text is "Analisar rede" or "Reparar rede" or
                "Criar ponto de restauração").ToArray();
        if (repairTools.Length != 3 || repairTools.Any(b => b.Width > 215))
            throw new Exception("Botoes de ferramentas ainda ocupam cartoes inteiros.");
        nav.SelectTab(5);
        Application.DoEvents();
        TestProcessGrid(form);
        var monitorGrid = Descendants(form).OfType<FlickerFreeProcessGrid>().Single(g => g.Name != "DriverInventoryGrid");
        if (monitorGrid.ColumnHeadersHeight > 30 ||
            monitorGrid.RowTemplate.Height > 26 ||
            monitorGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor !=
                monitorGrid.ColumnHeadersDefaultCellStyle.BackColor)
            throw new Exception("Monitor continua com cabecalho destacado ou linhas muito altas.");
        var pageViewports = Descendants(form).OfType<DarkScrollableFlow>()
            .Where(flow => flow.IsPageViewport).ToArray();
        if (pageViewports.Length != 4)
            throw new Exception($"Esperadas quatro paginas com rolagem controlada; encontradas {pageViewports.Length}");
        var systemCards = Descendants(form).OfType<DarkScrollableFlow>()
            .SelectMany(flow => flow.Content.Controls.Cast<Control>());
        if (systemCards.Any(c => c is TableLayoutPanel panel &&
            panel.BackColor == Color.FromArgb(38, 50, 63) && panel.Height > 150))
            throw new Exception("Cartoes do sistema extrapolam layout compacto.");
        form.Close();
        Console.WriteLine($"PASSOU: {tested} cenarios de tela, bandeja, foco e fechamento.");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr handle, int index);


    private static void TestSamplingCadence()
    {
        if (ProcessSamplingPolicy.IntervalMs(true, false) != 2_000 ||
            ProcessSamplingPolicy.IntervalMs(false, false) != 12_000 ||
            ProcessSamplingPolicy.IntervalMs(false, true) != 20_000 ||
            ProcessSamplingPolicy.IntervalMs(true, true) != 20_000)
            throw new Exception("Coleta economica deve priorizar bandeja e permitir 2s na aba Processos.");
        if (!(ProcessSamplingPolicy.ForegroundIntervalMs <
              ProcessSamplingPolicy.BackgroundIntervalMs &&
              ProcessSamplingPolicy.BackgroundIntervalMs <
              ProcessSamplingPolicy.TrayIntervalMs))
            throw new Exception("Amostragem de processos nao esta adaptativa.");
        Console.WriteLine("OK: amostragem 2s na aba, 12s em segundo plano e 20s na bandeja.");
    }

    private static void TestMonitorMathAndSampling()
    {
        double halfCpu = ProcessMonitorSampler.CpuPercent(
            TimeSpan.FromSeconds(1), 1, 2);
        if (Math.Abs(halfCpu - 50) > 0.0001 ||
            ProcessMonitorSampler.CpuPercent(TimeSpan.FromSeconds(30), 1, 2) != 100 ||
            ProcessMonitorSampler.CpuPercent(TimeSpan.Zero, 0, 0) != 0)
            throw new Exception("Percentual de CPU nao normalizado corretamente.");
        if (ProcessMonitorSampler.Throughput(1400, 400, 2) != 500 ||
            ProcessMonitorSampler.Throughput(100, 200, 1) is not null)
            throw new Exception("Contadores de E/S nao podem gerar taxas negativas.");

        var sampler = new ProcessMonitorSampler();
        var first = sampler.TakeSample();
        System.Threading.Thread.Sleep(400);
        var second = sampler.TakeSample();
        if (first.ProcessCount <= 0 || second.ProcessCount <= 0 ||
            second.Processes.Count != second.ProcessCount ||
            second.Processes.GroupBy(x => x.Pid).Any(x => x.Count() > 1) ||
            second.Processes.Any(x => x.MemoryBytes < 0 ||
                (x.CpuPercent.HasValue &&
                    (x.CpuPercent.Value < 0 || x.CpuPercent.Value > 100)) ||
                (x.IoBytesPerSecond.HasValue && x.IoBytesPerSecond.Value < 0)) ||
            (second.TotalCpuPercent.HasValue &&
                (second.TotalCpuPercent.Value < 0 || second.TotalCpuPercent.Value > 100)) ||
            (second.UsedMemoryPercent.HasValue && second.UsedMemoryPercent.Value > 100))
            throw new Exception("Metrica de processos invalida no Windows.");
        Console.WriteLine("OK: monitor de processos somente leitura, taxas e baseline.");
    }

    private static void TestProcessGrid(MainForm form)
    {
        var grid = Descendants(form).OfType<FlickerFreeProcessGrid>().Single(g => g.Name != "DriverInventoryGrid");
        if (!grid.Visible || grid.Columns.Count != 5 ||
            !grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)
                .SequenceEqual(new[] { "Processo", "PID", "CPU %", "RAM (MB)", "E/S (KB/s)" }))
            throw new Exception("Tabela de processos nao apresenta as cinco colunas.");
        var filter = Descendants(form).OfType<TextBox>()
            .Single(x => x.PlaceholderText == "Nome ou PID");
        var testSnapshot = new ProcessMonitorSnapshot(DateTime.UtcNow, 2, 12, 55, 1024,
            new[]
            {
                new ProcessMonitorEntry(301, "TesteMonitorA", 40, 256 * 1048576L, 4096),
                new ProcessMonitorEntry(302, "TesteMonitorB", 10, 128 * 1048576L, 1024)
            });
        form.PreviewProcessSnapshotForTests(testSnapshot);
        Application.DoEvents();
        if (grid.RowCount != 2 || grid.Rows[0].Cells[0].Value?.ToString() != "TesteMonitorA")
            throw new Exception("Monitor nao ordenou por consumo instantaneo de CPU.");
        var original = grid.Rows[0];
        var next = testSnapshot with
        {
            Processes = new[]
            {
                new ProcessMonitorEntry(301, "TesteMonitorA", 35, 260 * 1048576L, 8192),
                new ProcessMonitorEntry(302, "TesteMonitorB", 15, 150 * 1048576L, 2048)
            }
        };
        form.PreviewProcessSnapshotForTests(next);
        Application.DoEvents();
        if (!ReferenceEquals(original, grid.Rows[0]) ||
            grid.Rows[0].Cells[2].Value?.ToString() != "35,0" &&
            grid.Rows[0].Cells[2].Value?.ToString() != "35.0")
            throw new Exception("Tabela foi reconstruida ou nao atualizou valores.");
        filter.Text = "302";
        Application.DoEvents();
        if (grid.RowCount != 1 || grid.Rows[0].Cells[0].Value?.ToString() != "TesteMonitorB")
            throw new Exception("Filtro de nome/PID nao funciona.");
        filter.Clear();
        Application.DoEvents();
        if (grid.RowCount != 2 ||
            Descendants(form).OfType<Button>().All(x => x.Text != "Tarefas do Windows"))
            throw new Exception("Opcao de abrir o Gerenciador de Tarefas ausente.");
        Console.WriteLine("OK: grade, ordenacao, filtro e atualizacao sem reconstruir linhas.");
    }

    private static void TestStartupBounds()
    {
        if (StartupWindowSizing.ValidationMatrix.Length != 21)
            throw new Exception("A matriz precisa de 21 resolucoes.");
        var referenceArea = new Rectangle(0, 0, 1440, 852);
        var reference = StartupWindowSizing.Fit(referenceArea);
        if (reference.Size != new Size(1008, 698) ||
            reference.X != 216 || reference.Y != 77)
            throw new Exception("1440x900: dimensao deve acompanhar area util e ocupar cerca de 57%.");
        var wxga = new Rectangle(0, 0, 1366, 720);
        var wxgaWindow = StartupWindowSizing.Fit(wxga);
        if (wxgaWindow.Size != new Size(956, 590))
            throw new Exception("1366x768: nao abriu com tamanho proporcional.");
        var lowResolution = new Rectangle(0, 0, 800, 552);
        var lowWindow = StartupWindowSizing.Fit(lowResolution);
        if (!StartupWindowSizing.ApplyAreaLimit(lowResolution) ||
            lowWindow.Size != new Size(560, 452))
            throw new Exception("800x600 tambem deve respeitar a regra universal de 60%.");
        var hugeArea = new Rectangle(0, 0, 2560, 1392);
        var quadHd = StartupWindowSizing.Fit(hugeArea);
        if (quadHd.Size != new Size(1792, 1141))
            throw new Exception("2560x1440 deve ampliar a janela proporcionalmente; DPI nao define resolucao.");
        if (quadHd.Width <= reference.Width || quadHd.Height <= reference.Height)
            throw new Exception("Janela em 1440p permanece artificialmente pequena.");
        var resized = StartupWindowSizing.KeepVisible(
            new Rectangle(0, 0, 4000, 4000), hugeArea);
        if (resized.Width * (long)resized.Height >
            3L * hugeArea.Width * hugeArea.Height / 5L)
            throw new Exception("Troca de monitor permitiu exceder o limite de 60%.");
        var largestArea = new Rectangle(0, 0, 7680, 4272);
        if (StartupWindowSizing.Fit(largestArea).Width <= quadHd.Width)
            throw new Exception("8K nao está utilizando proporcionalmente a area do monitor.");
        foreach (var (name, resolution) in StartupWindowSizing.ValidationMatrix)
        {
            // Simular barra de tarefas de 48 pixels e area util com origem
            // deslocada, como ocorre em um monitor secundario.
            var workArea = new Rectangle(100, 64, resolution.Width, resolution.Height - 48);
            Rectangle opening = StartupWindowSizing.Fit(workArea);
            long pixels = (long)opening.Width * opening.Height;
            long available = (long)workArea.Width * workArea.Height;
            if (!StartupWindowSizing.ApplyAreaLimit(workArea) ||
                pixels > 3L * available / 5L)
                throw new Exception($"Area acima de 60%: {name} {opening}");
            if (pixels < 55L * available / 100L)
                throw new Exception($"Janela ficou pequena demais: {name} {opening}");
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
            Size maximum = StartupWindowSizing.MaximumSizeFor(workArea);
            if (opening.Width > maximum.Width || opening.Height > maximum.Height)
                throw new Exception($"Janela ultrapassou tamanho maximo: {name}");
            if ((long)maximum.Width * maximum.Height > 3L * available / 5L)
                throw new Exception($"Redimensionamento excederia 60%: {name}");
            // Ao mover para monitor menor, o tamanho e limitado sem alterar os
            // limites das janelas ja validas.
            if (StartupWindowSizing.KeepVisible(opening, workArea) != opening)
                throw new Exception($"Posicao valida alterada: {name}");
            Console.WriteLine($"Fit OK {name}: {resolution.Width}x{resolution.Height}");
        }
    }

    private static void TestWorkspaceProportions()
    {
        foreach (int dpi in new[] { 96, 120, 144, 168, 192, 240, 288 })
        foreach (int height in new[] { 480, 560, 680, 900, 1100 })
        {
            var (header, log) = StartupWindowSizing.WorkspaceRows(height, dpi);
            if (header <= 0 || log < 95 || height - header - log < 140)
                throw new Exception($"Layout sem registro legivel ou area de ferramentas: {height} dpi={dpi}, header={header}, log={log}");
            if (dpi == 96 && height >= 560 && log < 130)
                throw new Exception($"Registro demasiadamente fino: {height}, log={log}");
        }
        Console.WriteLine("OK: proporcoes de registro/cabecalho em 100%, 125%, 150% e 200%.");
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
