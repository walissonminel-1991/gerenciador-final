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
        if (!AppServices.TryParseUninstaller(@"C:\cache.exe_folder\uninstall.exe /S", out var uninstallPath, out var uninstallArgs) ||
            uninstallPath != @"C:\cache.exe_folder\uninstall.exe" || uninstallArgs != "/S" ||
            !AppServices.TryParseUninstaller("MsiExec.exe /I{ABCD}", out _, out var msiArgs) ||
            msiArgs != "/X{ABCD}" ||
            AppServices.TryParseUninstaller(null, out _, out _) ||
            AppServices.TryParseUninstaller("uninstall.exe /S\ncmd.exe", out _, out _))
            throw new Exception("Parser de desinstalacao nao preservou o executavel ou aceitou controles.");
        ResidualExplorerTests.Run();
        // PC fraco v5.13.11.21: 19 MSIX estão no inventário, mas não
        // aparecem na lista de ações. O filtro explícito revela sem permitir
        // atualização nem desinstalação somente pelo nome ou origem.
        var inventoryOnly = new ProgramEntry
        {
            Name = "Leitor MSIX fictício", Version = "1.2.3.4",
            InventoryOnly = true, InventorySource = "Aplicativo empacotado (MSIX)"
        };
        if (ActionableInventory.ShouldShow(inventoryOnly) ||
            ActionableInventory.ShouldShowInMainList(inventoryOnly, "Todos") ||
            ActionableInventory.ShouldShowInMainList(inventoryOnly, "Com atualização") ||
            !ActionableInventory.ShouldShowInMainList(inventoryOnly, "Inventário") ||
            ActionableInventory.CanUninstall(inventoryOnly))
            throw new Exception("MSIX somente leitura deve aparecer exclusivamente na visualizacao de inventario.");
        var normal = new ProgramEntry { Name = "Aplicativo com ação", CanUninstall = true };
        if (!ActionableInventory.ShouldShowInMainList(normal, "Todos") ||
            !ActionableInventory.ShouldShowInMainList(normal, "Inventário"))
            throw new Exception("Visualizacao de inventario nao pode ocultar aplicativos tradicionais.");

        if (DiagnosticTrace.FormatVersion(new Version(5, 13, 10, 18)) != "v5.13.10.18")
            throw new Exception("Diagnostico perdeu a revisao da compilacao.");
        const string knownId = "a2a4b6b8-1212-4343-8585-919191919191";
        if (WindowsUpdateClient.VerifiedIdentity(knownId, 12) != knownId + "@12" ||
            WindowsUpdateClient.VerifiedIdentity(knownId, 13) == knownId + "@12" ||
            WindowsUpdateClient.VerifiedIdentity("invalido", 12) is not null ||
            WindowsUpdateClient.VerifiedIdentity(knownId, 0) is not null)
            throw new Exception("Windows Update deve preservar GUID e revisao antes de instalar.");
        string selectedIdentity = WindowsUpdateClient.CanonicalSelectedIdentity(
            WindowsUpdateClient.VerifiedIdentity(knownId, 12))
            ?? throw new Exception("Selecao recusou identidade produzida pela consulta.");
        if (!WorkerHost.TryParseUpdateTask("wu-install:" + selectedIdentity, out var parsedSelection) ||
            !parsedSelection.SetEquals([knownId + "@12"]) || parsedSelection.Contains(knownId + "@13"))
            throw new Exception("UI e worker precisam preservar a mesma revisao confirmada, sem instalar no teste.");
        foreach (string invalidIdentity in new[] { knownId, knownId + "@0", knownId + "@-1", knownId + "@01",
            knownId + "@2147483648", knownId + "@12\n", Guid.Empty.ToString("D") + "@12", "invalido@12" })
            if (WindowsUpdateClient.CanonicalSelectedIdentity(invalidIdentity) is not null ||
                WorkerHost.TryParseUpdateTask("wu-install:" + invalidIdentity, out _))
                throw new Exception("Identidade Windows Update ambigua foi aceita.");
        foreach (string invalidTask in new[] { "wu-install:", "wu-install:" + selectedIdentity + ",",
            "wu-install:" + selectedIdentity + "," + selectedIdentity,
            "wu-install:" + string.Join(",", Enumerable.Range(1, 31).Select(r => knownId + "@" + r)) })
            if (WorkerHost.TryParseUpdateTask(invalidTask, out _))
                throw new Exception("Worker aceitou selecao vazia, duplicada ou acima do limite.");
        // Regressao da captura real: WinForms TextBox junta parágrafos se receber
        // somente LF em SetText. Validar CRLF idempotente, inclusive com CR solto.
        if (DarkLogView.NormalizeWindowsLineBreaks("um\n\ndois") != "um\r\n\r\ndois" ||
            DarkLogView.NormalizeWindowsLineBreaks("um\r\n\r\ndois") != "um\r\n\r\ndois" ||
            DarkLogView.NormalizeWindowsLineBreaks("um\rdois") != "um\r\ndois")
            throw new Exception("Normalizacao de paragrafo WinForms falhou.");
        var residue = new ResidualAuditReport("Aplicativo fictício", false, new[]
        {
            new ResidualAuditFinding(@"C:\Ficticio\Playlist", "Perfil", 1, "Preservar playlist", ResidualDisposition.PreserveData),
            new ResidualAuditFinding(@"C:\Ficticio\Binario", "Binário", 1, "Sem exclusividade", ResidualDisposition.Unknown)
        }, []);
        using (var review = new ResidualAuditDialog(residue, refresh: _ => Task.FromResult(residue with
            { Incomplete = true, Findings = new[] { residue.Findings[1] with { Evidence = "ALVO ALTERADO — PRESERVAR" } } })))
        {
            review.Show();
            Application.DoEvents();
            var buttons = Descendants(review).OfType<Button>().ToArray();
            if (buttons.Length != 2 || buttons.Any(b => b.Text.Contains("Excluir") || b.Text.Contains("Remover")))
                throw new Exception("Revisão de resíduos não pode expor exclusão.");
            var text = Descendants(review).OfType<DarkLogView>().Single();
            if (!text.RenderedText.Contains("Preservar dados") || !text.RenderedText.Contains("Estado desconhecido"))
                throw new Exception("Decisões individuais da prévia não foram exibidas.");
            buttons.Single(b => b.Name == "ResidualRecheck").PerformClick();
            for (int attempt = 0; attempt < 30 && !text.RenderedText.Contains("ALVO ALTERADO"); attempt++)
            {
                System.Threading.Thread.Sleep(20);
                Application.DoEvents();
            }
            if (!text.RenderedText.Contains("ALVO ALTERADO") || !text.RenderedText.Contains("NENHUM ITEM APAGADO") ||
                !text.RenderedText.Contains("ANÁLISE PARCIAL"))
                throw new Exception("Conferência não atualizou a prévia ou ocultou estado parcial.");
            review.Close();
        }
        var partialEmpty = new ResidualAuditReport("Busca sem achados", false, [], [], Historical: true,
            Incomplete: true, Coverage: new ResidualCoverage(ResidualScanIssue.Timeout, CandidatesNotEvaluated: null));
        using (var review = new ResidualAuditDialog(partialEmpty))
        {
            review.Show(); Application.DoEvents();
            var rendered = Descendants(review).OfType<DarkLogView>().Single().RenderedText;
            if (!rendered.Contains("ANÁLISE PARCIAL") || !rendered.Contains("quantidade desconhecida") ||
                !rendered.Contains("Exclusão indisponível") || Descendants(review).OfType<Button>().Count() != 1)
                throw new Exception("Busca parcial sem achados precisa mostrar limites sem botão destrutivo.");
            review.Close();
        }
        Console.WriteLine("OK Prévia de resíduos tipada, parcial vazio e conferência sem exclusão");
        // Permite experimentar a contribuicao sem gravar dados ou autorizar coleta.
        string dryRunFolder = Path.Combine(Path.GetTempPath(),
            "GF-Preview-Only-" + Guid.NewGuid().ToString("N"));
        var dryRunRecorder = new AnonymousTelemetry(dryRunFolder);
        using (var privacy = new TelemetryPrivacyDialog(dryRunRecorder))
        {
            privacy.Show();
            Application.DoEvents();
            var simulate = Descendants(privacy).OfType<Button>().SingleOrDefault(
                b => b.Name == "PrivacyOfflineSimulation")
                ?? throw new Exception("Botao de simulacao segura ausente.");
            var preview = Descendants(privacy).OfType<DarkLogView>().SingleOrDefault(
                view => view.Name == "PrivacyLocalPreview")
                ?? throw new Exception("Previa de coleta acessivel ausente.");
            if (!preview.RenderedText.Contains("COLETA DESATIVADA"))
                throw new Exception("Consentimento ausente nao foi explicado claramente.");
            if (!preview.RenderedText.Contains("COLETA DESATIVADA\r\n\r\n"))
                throw new Exception("Previa inicial nao preservou separacao de paragrafos.");
            simulate.PerformClick();
            // ModernButton somente despacha Click depois de 145 ms de feedback.
            // Testar o clique REAL, sem executar diretamente o callback.
            for (int attempt = 0; attempt < 30 &&
                !preview.RenderedText.Contains("SIMULAÇÃO CONCLUÍDA"); attempt++)
            {
                System.Threading.Thread.Sleep(20);
                Application.DoEvents();
            }
            if (!preview.RenderedText.Contains("SIMULAÇÃO CONCLUÍDA"))
                throw new Exception("Simulacao nao respondeu ao clique animado.");
            if (!preview.RenderedText.Contains("Nenhuma conexão de envio foi feita.") ||
                !preview.RenderedText.Contains("INVENTADOS"))
                throw new Exception("Simulacao nao explicou os dados ficticios.");
            if (!preview.RenderedText.Contains("SIMULAÇÃO CONCLUÍDA — NADA FOI ENVIADO\r\n\r\n") ||
                !preview.RenderedText.Contains("EXEMPLO COM DADOS INVENTADOS\r\n"))
                throw new Exception("Simulacao perdeu a estrutura de linhas no TextBox Windows.");
            if (dryRunRecorder.Consent != TelemetryConsent.NotAsked ||
                Directory.Exists(dryRunFolder))
                throw new Exception("Simulacao alterou consentimento ou arquivos locais.");
            if (privacy.DialogResult != DialogResult.None)
                throw new Exception("Simulacao aprovou ou recusou consentimento sem autorizacao.");
            privacy.Close();
        }
        if (PrivacyConsentPresentation.ButtonLabel(TelemetryConsent.LocalOnly) != "Coleta local ativa" ||
            PrivacyConsentPresentation.ButtonLabel(TelemetryConsent.Declined) != "Coleta desativada" ||
            !PrivacyConsentPresentation.Explanation(TelemetryConsent.LocalOnly)
                .Contains("Nenhum diagnóstico é enviado", StringComparison.Ordinal) ||
            !PrivacyConsentPresentation.Explanation(TelemetryConsent.LocalOnly)
                .Contains("versão anterior", StringComparison.Ordinal))
            throw new Exception("Estado persistente de privacidade nao e claro.");
        // Os tres cartoes de Inicializacao precisam caber, com margens,
        // sem mudar a quantidade de colunas das outras subabas.
        if (StartupCardLayout.ColumnsForSpace(1000) != 3 ||
            StartupCardLayout.ColumnsForSpace(885) != 3 ||
            StartupCardLayout.ColumnsForSpace(884) != 2 ||
            StartupCardLayout.ColumnsForSpace(720) != 2 ||
            StartupCardLayout.ColumnsForSpace(590) != 2 ||
            StartupCardLayout.ColumnsForSpace(589) != 1 ||
            StartupCardLayout.CardWidth(1000, 3) > 325 ||
            StartupCardLayout.CardWidth(885, 3) < 250 ||
            3 * (StartupCardLayout.CardWidth(885, 3) + 9) > 885)
            throw new Exception("Inicializacao nao comporta tres cartoes proporcionais.");
        TestStartupBounds();
        TestWorkspaceProportions();
        TestRamHistoryAndSampling();
        TestSafeMemoryPolicyAndLogCache();
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
        var driverCards = Descendants(placeholder).OfType<DarkDriverList>().SingleOrDefault()
            ?? throw new Exception("DRIVERS deve usar cartoes do mesmo padrao dos aplicativos.");
        if (!placeholder.Visible || Descendants(placeholder).OfType<DataGridView>().Any() ||
            Descendants(placeholder).OfType<Button>()
                .Count(b => b.Name == "DriverInventoryRefresh") != 1)
            throw new Exception("DRIVERS nao pode usar grade antiga nem oferecer alteracao de drivers.");
        if (!Descendants(placeholder).OfType<TextBox>().Any(t =>
                t.Name == "DriverInventorySearch") ||
            !Descendants(placeholder).OfType<Label>().Any(c =>
                c.Text.Contains("Nenhuma alteração em drivers")))
            throw new Exception("Busca ou aviso de consulta segura ausente.");
        if (Descendants(placeholder).OfType<DarkScrollableFlow>().Any())
            throw new Exception("DRIVERS nao deve mostrar rolagem duplicada.");
        if (!Descendants(placeholder).OfType<Button>().Any(b =>
                b.Text == "Outros" &&
                b.AccessibleDescription?.Contains("indeterminada", StringComparison.Ordinal) == true) ||
            Descendants(placeholder).OfType<Button>().Any(b => b.Text == "Não confirmados"))
            throw new Exception("Filtro DRIVERS deve caber inteiro e explicar os casos incertos.");
        var privacyStatus = Descendants(form).OfType<Button>()
            .SingleOrDefault(b => b.Name == "PrivacyStatusButton")
            ?? throw new Exception("Indicador de privacidade ausente do cabecalho.");
        if (privacyStatus.Text != PrivacyConsentPresentation.ButtonLabel(
                AnonymousTelemetryContext.Current?.Consent ?? TelemetryConsent.NotAsked) ||
            privacyStatus.AccessibleDescription?.Contains("Clique", StringComparison.Ordinal) != true)
            throw new Exception("Consentimento existente nao esta visivel nem revisavel.");
        var fakeDrivers = Enumerable.Range(0, 184).Select(i => new DriverInventoryEntry(
            "TEST\\\\ID" + i, "Dispositivo sintetico " + i,
            "Fabricante de teste", "1.2.3." + i, "oem" + i + ".inf",
            true, i % 3 == 0 ? true : i % 3 == 1 ? false : null)).ToArray();
        driverCards.SetItems(fakeDrivers);
        if (driverCards.Items.Count != 184 || driverCards.SelectedItem is not null)
            throw new Exception("Lista de drivers virtualizada perdeu registros ou selecionou automaticamente.");
        if (DriverListFilters.Apply(fakeDrivers, "Todos", "").Length != 184 ||
            DriverListFilters.Apply(fakeDrivers, "Presentes", "").Length != 62 ||
            DriverListFilters.Apply(fakeDrivers, "Não confirmados", "").Length != 122 ||
            DriverListFilters.Apply(fakeDrivers, "Todos", "oem18.inf").Length != 1 ||
            DriverListFilters.Apply(fakeDrivers, "Todos", fakeDrivers[183].DeviceId).Length != 1 ||
            DriverListFilters.Apply(fakeDrivers, "Todos", "fabricante DE TESTE").Length != 184)
            throw new Exception("Pesquisa e filtros DRIVERS falharam na amostra de 184 itens.");
        // Todos os drivers continuam apenas texto; filtros nao fazem instalacoes.
        driverCards.SetItems(Array.Empty<DriverInventoryEntry>());
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
                    var inventoryFilter = Descendants(form).OfType<Button>()
                        .SingleOrDefault(button => button.Text == "Inventário");
                    if (inventoryFilter is null || !inventoryFilter.Visible ||
                        inventoryFilter.Parent is not TableLayoutPanel heading ||
                        heading.ColumnCount != 5 ||
                        heading.Controls.OfType<Button>().Count() != 4)
                        throw new Exception("Filtro de inventario MSIX ausente ou fora da faixa de Aplicativos.");
                    foreach (var filterButton in heading.Controls.OfType<Button>())
                    {
                        if (filterButton.Right > heading.ClientSize.Width + 2 ||
                            filterButton.Left < 0 ||
                            filterButton.Width < 45)
                            throw new Exception("Filtro de aplicativos cortado: " +
                                name + " / " + filterButton.Text);
                    }
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
                        "Consumo e memória disponível. Histórico local, também na bandeja.",
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
                    bool tinyDrivers = placeholder.Height < 230;
                    int minDriverHeight = tinyDrivers ? 73 : 100;
                    if (driverCards.Height < minDriverHeight ||
                        driverCards.Width < placeholder.Width - 70)
                        throw new Exception($"DRIVERS nao aproveita o espaco para cartoes: {name} {resolution} " +
                            $"lista={driverCards.Size} raiz={placeholder.Size}");
                    var mobileFilter = Descendants(placeholder).OfType<ComboBox>()
                        .SingleOrDefault(c => c.Name == "DriverCompactFilter");
                    if (mobileFilter is null || mobileFilter.Visible != tinyDrivers ||
                        mobileFilter.Items.Count != 3)
                        throw new Exception($"Filtro compacto DRIVERS inacessivel: {name} {resolution}");
                    if (Descendants(placeholder).OfType<DataGridView>().Any() ||
                        Descendants(placeholder).OfType<Button>().Any(b =>
                            b.Text.Contains("Desinstalar", StringComparison.OrdinalIgnoreCase) ||
                            b.Text.Contains("Atualizar driver", StringComparison.OrdinalIgnoreCase) ||
                            b.Text.Contains("Remover driver", StringComparison.OrdinalIgnoreCase)))
                        throw new Exception("DRIVERS ganhou acao destrutiva.");
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
        TestMemoryPanel(form);
        var pageViewports = Descendants(form).OfType<DarkScrollableFlow>()
            .Where(flow => flow.IsPageViewport).ToArray();
        // DRIVERS agora usa a mesma lista virtualizada de Aplicativos,
        // sem rolagem externa duplicada. Outras quatro paginas a preservam.
        if (pageViewports.Length != 4)
            throw new Exception($"Esperadas quatro paginas com rolagem controlada e " +
                $"DRIVERS com lista propria; encontradas {pageViewports.Length}");
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
        if (!ProcessSamplingPolicy.ShouldReadProcessDetails(true, false) ||
            ProcessSamplingPolicy.ShouldReadProcessDetails(false, false) ||
            ProcessSamplingPolicy.ShouldReadProcessDetails(true, true) ||
            ProcessSamplingPolicy.ShouldReadProcessDetails(false, true))
            throw new Exception("Leitura detalhada de processos nao pode ocorrer na bandeja/outras abas.");
        Console.WriteLine("OK: amostragem 2s na aba; contadores globais a 12s ou 20s em background.");
    }

    private static void TestSafeMemoryPolicyAndLogCache()
    {
        const ulong GiB = 1073741824UL;
        var policy = new SafeMemoryOptimizationPolicy();
        var low = new MemoryRamSample(DateTime.UtcNow, 8 * GiB, GiB);
        var safe = new MemoryRamSample(DateTime.UtcNow, 8 * GiB, 4 * GiB);
        var start = DateTime.UtcNow;

        if (policy.ShouldOptimize(low, true, start))
            throw new Exception("Limpeza automática deve começar desligada.");
        policy.AutomaticEnabled = true;
        if (policy.ShouldOptimize(low, false, start) ||
            policy.ShouldOptimize(low, false, start.AddSeconds(20)) ||
            !policy.ShouldOptimize(low, false, start.AddSeconds(40)))
            throw new Exception("Política exige pressão persistente antes de otimizar.");
        policy.RecordRun(start.AddSeconds(40));
        if (policy.ShouldOptimize(low, true, start.AddMinutes(1)) ||
            policy.ShouldOptimize(low, true, start.AddMinutes(2)) ||
            policy.ShouldOptimize(low, true, start.AddMinutes(3)))
            throw new Exception("Cooldown de limpeza foi ignorado.");
        if (!policy.ShouldOptimize(low, true, start.AddMinutes(16)))
            throw new Exception("Auto deveria poder agir após cooldown e pressão contínua.");
        policy.RecordRun(start.AddMinutes(16));
        if (policy.ShouldOptimize(low, true, start.AddMinutes(17)) ||
            policy.ShouldOptimize(safe, false, start.AddMinutes(18)) ||
            policy.ShouldOptimize(low, false, start.AddMinutes(19)) ||
            policy.ShouldOptimize(low, false, start.AddMinutes(20)))
            throw new Exception("Amostra normal deve quebrar a sequência de pressão.");
        policy.AutomaticEnabled = false;
        if (policy.ShouldOptimize(low, true, start.AddMinutes(40)))
            throw new Exception("Otimização desativada não deve agir.");

        var counter = new AppCacheCleanupCounter();
        counter.Register(2048);
        counter.Register(-100);
        if (counter.Operations != 2 || counter.DiscardedBufferBytes != 2048)
            throw new Exception("Contador não pode inventar memória positiva.");

        using var mockLog = new DarkLogView();
        for (int i = 0; i < 250; i++) mockLog.AppendText($"Mensagem {i:D3}: apenas cache do GF.\n");
        long dropped = mockLog.CompactOldLog(2000, 400);
        if (dropped <= 0 || mockLog.RenderedText.Contains("Mensagem 000") ||
            !mockLog.RenderedText.Contains("Mensagem 249"))
            throw new Exception("Compactação do log não preservou mensagens recentes.");

        using var signal = new WindowsLowMemorySignal();
        _ = signal.IsLow(); // disponibilidade é opcional, não exige admin.
        Console.WriteLine("OK: política de pressão persistente, cooldown, log próprio e limite de cache.");
    }

    private static void TestRamHistoryAndSampling()
    {
        const ulong GiB = 1073741824UL;
        var history = new MemoryRamHistory();
        history.Add(new MemoryRamSample(DateTime.UtcNow, 8 * GiB, 2 * GiB));
        if (history.Samples.Count != 1 ||
            history.Samples[0].UsedPercent != 75 ||
            history.Samples[0].UsedBytes != 6 * GiB)
            throw new Exception("RAM: memoria usada/disponivel calculada incorretamente.");
        for (int i = 0; i < MemoryRamHistory.Capacity + 10; i++)
            history.Add(new MemoryRamSample(DateTime.UtcNow, 8 * GiB, GiB));
        if (history.Samples.Count != MemoryRamHistory.Capacity ||
            history.ConfirmedReclaimedBytes != 0)
            throw new Exception("RAM: historico ilimitado ou inventou limpeza.");
        var current = MemoryRamReader.Capture();
        if (current is null || current.TotalBytes == 0 ||
            current.AvailableBytes > current.TotalBytes)
            throw new Exception("RAM: leitura local do Windows invalida.");
        Console.WriteLine("OK: RAM, limite de amostras e limpeza nao inventada.");
    }

    private static void TestMemoryPanel(MainForm form)
    {
        var graph = Descendants(form).OfType<MemoryRamChart>().Single();
        if (!graph.Visible ||
            Descendants(form).OfType<Button>().All(x =>
                x.Name != "OpenWindowsTaskManager" || x.Text != "Abrir tarefas" ||
                x.AccessibleName?.Contains("Gerenciador de Tarefas", StringComparison.Ordinal) != true) ||
            Descendants(form).OfType<Button>().All(x => x.Name != "MemoryAutoOptimization") ||
            Descendants(form).OfType<Button>().All(x => x.Name != "MemoryManualOptimization") ||
            Descendants(form).OfType<Label>().All(x => x.Name != "MemoryCandidates"))
            throw new Exception("RAM: grafico ou acesso a Tarefas do Windows ausente.");
        const ulong GiB = 1073741824UL;
        int samplesBefore = graph.SampleCount;
        form.PreviewMemoryRamForTests(new MemoryRamSample(DateTime.UtcNow, 8 * GiB, 2 * GiB));
        form.PreviewMemoryRamForTests(new MemoryRamSample(DateTime.UtcNow, 8 * GiB, 3 * GiB));
        Application.DoEvents();
        if (graph.SampleCount != Math.Min(MemoryRamHistory.Capacity, samplesBefore + 2) ||
            !Descendants(form).OfType<Label>().Any(x => x.Name == "MemoryAvailable" && x.Text.Contains("3,00") || x.Name == "MemoryAvailable" && x.Text.Contains("3.00")) ||
            !Descendants(form).OfType<Label>().Any(x => x.Name == "MemoryCleaned" && x.Text.Contains("0 KB")))
            throw new Exception("RAM: dados inconsistentes: pontos=" + graph.SampleCount +
                ", disponivel=" + Descendants(form).OfType<Label>().FirstOrDefault(x => x.Name == "MemoryAvailable")?.Text +
                ", liberada=" + Descendants(form).OfType<Label>().FirstOrDefault(x => x.Name == "MemoryCleaned")?.Text);
        Console.WriteLine("OK: grafico de RAM, valores e botao do Windows.");
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
