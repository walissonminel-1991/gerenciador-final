using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private Task<DriverInventoryResult>? _driverReadTask;
    // Exclusivo das fixtures com readForTest: jamais acessar DefaultPath
    // do usuario nos testes temporais ou iniciar captura nativa.
    private string? _driverHistoryPathForTest;
    private Func<CancellationToken, Task>? _driverHistoryBeforeWriteForTest;
    private Action? _driverHistoryBeforeReplaceForTest;

    private Control BuildDriversPlaceholder(
        Func<CancellationToken, Task<DriverInventoryResult>>? readForTest = null,
        TimeSpan? timeoutForTest = null,
        Action<Action>? onTestReadActionReady = null,
        Action<Action<string>>? onTestFilterReady = null)
    {
        // Ações gerais para todo o inventário. A política de segurança decide
        // individualmente quais pacotes OEM podem ser removidos sem /force.
        // A atualização WUA permanece sujeita aos gates de G3.1/G3.2.
        var root = new TableLayoutPanel
        {
            Name = "DriversPlaceholder", Dock = DockStyle.Fill,
            BackColor = AppServices.Background, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(4, 5, 4, 3), Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 67));

        var searchBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Margin = Padding.Empty, BackColor = AppServices.Background
        };
        searchBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        searchBar.Controls.Add(new Label
        {
            Text = "Pesquisar:", Dock = DockStyle.Fill, ForeColor = Muted,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        var search = new TextBox
        {
            Name = "DriverInventorySearch", Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(19, 26, 34), ForeColor = Ink,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Dispositivo, versão, fabricante ou INF",
            Margin = new Padding(0, 3, 8, 3)
        };
        searchBar.Controls.Add(search, 1, 0);
        var clear = CreateSecondaryButton("Limpar");
        clear.Margin = new Padding(0, 3, 8, 3);
        clear.Dock = DockStyle.Fill;
        clear.Click += (_, _) => search.Clear();
        _toolTips.SetToolTip(clear, "Limpa apenas o campo de pesquisa, sem alterar drivers.");
        searchBar.Controls.Add(clear, 2, 0);
        root.Controls.Add(searchBar, 0, 0);

        // Barra de ações gerais: somente leitura e investigação.
        // Manutenção é desenhada virtualmente por dispositivo em DarkDriverList.
        var actionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Margin = new Padding(0, 2, 0, 2)
        };
        actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var refresh = CreateButton("Consultar drivers", AppServices.Blue, Color.White);
        refresh.Name = "DriverInventoryRefresh";
        refresh.AccessibleName = "Consultar drivers";
        refresh.Dock = DockStyle.Fill;
        refresh.Margin = new Padding(0, 2, 6, 2);
        actionPanel.Controls.Add(refresh, 0, 0);
        _toolTips.SetToolTip(refresh,
            "Consulta os drivers associados aos dispositivos por WMI. Não instala, atualiza nem remove.");
        var actionInfo = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var actionHint = new Label
        {
            Dock = DockStyle.Fill, ForeColor = Muted,
            Text = "Consulta local, somente leitura. Nenhuma alteração em drivers.",
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
        };
        var compactFilter = new ComboBox
        {
            Name = "DriverCompactFilter", Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(37, 48, 61), ForeColor = Ink,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            DropDownWidth = 180,
            Visible = false, Margin = Padding.Empty
        };
        compactFilter.Items.AddRange(new object[]
            { "Todos", "Presentes", "Problemas", "Sem driver", "Não confirmados" });
        compactFilter.SelectedIndex = 0;
        _toolTips.SetToolTip(compactFilter,
            "Filtro somente visual: todos, presentes, problemas PnP, sem driver ou não confirmados.");
        actionInfo.Controls.Add(actionHint);
        actionInfo.Controls.Add(compactFilter);
        var investigate = CreateSecondaryButton("Investigar");
        investigate.Name = "DriverReadOnlyInvestigation";
        investigate.AccessibleName = "Investigar drivers e histórico, somente leitura";
        investigate.Dock = DockStyle.Fill;
        investigate.Margin = new Padding(0, 2, 6, 2);
        investigate.Enabled = false;
        actionPanel.Controls.Add(investigate, 1, 0);
        _toolTips.SetToolTip(investigate,
            "Histórico Windows, INFs publicados, consulta oficial e verificação do INF. Somente leitura.");
        actionPanel.Controls.Add(actionInfo, 2, 0);
        // O laboratório de 17 casos permanece disponível às fixtures; não
        // ocupa espaço nem aparece no produto real.
        Button? labOpen = null;
        if (readForTest is not null)
        {
            labOpen = CreateSecondaryButton("Simular");
            labOpen.Name = "DriverSyntheticLabOpen";
            labOpen.AccessibleName = "Abrir laboratório fictício multiclasses de drivers";
            labOpen.AccessibleDescription = "Apenas fixture; sem acesso WMI, WUA ou manutenção.";
            labOpen.Dock = DockStyle.Fill;
            labOpen.Margin = new Padding(0, 2, 6, 2);
            actionPanel.ColumnCount = 4;
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            actionPanel.Controls.Add(labOpen, 3, 0);
        }
        // Copiar resumo pertence ao rodapé de diagnóstico, nunca à barra
        // de operações; permanece acessível somente após consulta válida.
        var copySummary = CreateSecondaryButton("Copiar resumo");
        copySummary.Name = "DriverCopySafetySummary";
        copySummary.AccessibleName = "Copiar resumo de drivers sem identificadores";
        copySummary.AccessibleDescription = "Copia somente contagens, sem IDs, INFs ou nomes.";
        copySummary.Dock = DockStyle.Fill;
        copySummary.Margin = new Padding(2, 2, 2, 2);
        copySummary.Enabled = false;
        _toolTips.SetToolTip(copySummary,
            "Diagnóstico local: copia contagens sem DeviceId, HardwareID, nomes, fornecedores, INF ou caminhos.");
        root.Controls.Add(actionPanel, 0, 1);

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1,
            Margin = Padding.Empty
        };
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
        var title = new Label
        {
            Name = "DriverInventoryTitle", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Text = "Drivers · consulta não iniciada",
            TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ink,
            AutoEllipsis = true
        };
        heading.Controls.Add(title, 0, 0);
        Button Filter(string label, int col)
        {
            var button = CreateSecondaryButton(label);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(4, 3, 0, 3);
            button.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            // Filtros em faixa curta: proibir quebra de linha que cortava
            // "Falhas PnP" e "Sem driver" na captura normal de 940 px.
            if (button is ModernButton modern) modern.AllowMultilineText = false;
            heading.Controls.Add(button, col, 0);
            return button;
        }
        var all = Filter("Todos", 1);
        var present = Filter("Presentes", 2);
        // Rotulo compacto: a forma extensa ficava cortada ("Não") em
        // resolucoes intermediarias. O significado permanece explicito.
        var problems = Filter("Falhas PnP", 3);
        problems.AccessibleName = "Problemas PnP relatados pelo Windows";
        _toolTips.SetToolTip(problems, "Códigos de problema informados pelo Windows, inclusive drivers não instalados; não indica automaticamente uma atualização.");
        var unbound = Filter("Sem driver", 4);
        unbound.AccessibleName = "Dispositivos sem associação WMI de driver assinado";
        const string unboundExplanation =
            "Sem linha de driver assinado associada no WMI. Não prova ausência de driver nem atualização disponível.";
        unbound.AccessibleDescription = unboundExplanation;
        _toolTips.SetToolTip(unbound, unboundExplanation);
        var other = Filter("Outros", 5);
        other.AccessibleName = "Não confirmados";
        const string uncertainFilterExplanation =
            "Dispositivos ausentes ou com presença indeterminada. Não significa driver obsoleto.";
        other.AccessibleDescription = uncertainFilterExplanation;
        _toolTips.SetToolTip(other, uncertainFilterExplanation);
        root.Controls.Add(heading, 0, 2);

        var list = new DarkDriverList { Dock = DockStyle.Fill, Margin = Padding.Empty };
        list.AccessibleName = "Drivers associados a dispositivos; ações sujeitas à elegibilidade";
        var empty = new Label
        {
            Name = "DriverInventoryEmpty", Dock = DockStyle.Fill,
            AccessibleName = "Estado da lista de drivers",
            Text = "Clique em Consultar drivers para iniciar a leitura.",
            Font = new Font("Segoe UI", 10f), ForeColor = Muted,
            BackColor = PanelBackground,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(20)
        };
        var content = new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = PanelBackground
        };
        content.Controls.Add(list);
        content.Controls.Add(empty);
        empty.BringToFront();
        root.Controls.Add(content, 0, 3);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = Padding.Empty
        };
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        var state = new Label
        {
            Name = "DriverInventoryStatus", Dock = DockStyle.Fill, ForeColor = Muted,
            AccessibleName = "Estado da consulta de drivers",
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
            Text = "Nenhuma consulta iniciada. Não é o inventário completo do Driver Store."
        };
        var history = new Label
        {
            Name = "DriverHistoryStatus", Dock = DockStyle.Fill, ForeColor = Muted,
            AccessibleName = "Histórico local de drivers",
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
            Text = "Histórico próprio: comparações locais a partir da primeira consulta."
        };
        var statusBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty
        };
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        statusBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        statusBar.Controls.Add(state, 0, 0);
        statusBar.Controls.Add(copySummary, 1, 0);
        footer.Controls.Add(statusBar, 0, 0);
        footer.Controls.Add(history, 0, 1);
        root.Controls.Add(footer, 0, 4);
        history.AccessibleDescription = history.Text;
        int refreshRevision = 0;
        void SyncStatusAccessibility()
        {
            if (state.IsDisposed) return;
            // A fonte da descrição deve ser SEMPRE o texto atualmente exibido:
            // eventos WinForms podem reentrar durante uma transição de estado.
            string visible = state.Text;
            var description = visible + Environment.NewLine + history.Text +
                (history.AccessibleDescription == history.Text ? "" : Environment.NewLine + history.AccessibleDescription);
            state.AccessibleDescription = description;
            if (!state.IsDisposed) _toolTips.SetToolTip(state, description);
        }
        state.TextChanged += (_, _) => SyncStatusAccessibility();
        void SetDriverStatus(string message)
        {
            if (state.IsDisposed) return;
            state.Text = message;
            SyncStatusAccessibility();
        }
        void SetRefreshCaption(string caption, string description)
        {
            if (refresh.IsDisposed) return;
            int revision = ++refreshRevision;
            refresh.Text = caption;
            if (refresh.IsDisposed || revision != refreshRevision) return;
            refresh.AccessibleName = caption;
            if (refresh.IsDisposed || revision != refreshRevision) return;
            refresh.AccessibleDescription = description;
            if (refresh.IsDisposed || revision != refreshRevision) return;
            _toolTips.SetToolTip(refresh, description);
        }
        SetDriverStatus(state.Text);
        SetRefreshCaption("Consultar drivers", "Consulta apenas dispositivos e vínculos locais. Não modifica drivers.");

        IReadOnlyList<DriverInventoryEntry>? snapshot = null;
        DriverInventoryResult? lastResult = null;
        long inventoryGeneration = 0;
        // Apenas a geracao publicada nesta consulta habilita NOVA investigacao.
        // Uma leitura cancelada preserva a lista anterior, mas nao sua autoridade.
        long publishedSnapshotGeneration = -1;
        Action? invalidateOpenReport = null;
        void SyncInvestigationDescription()
        {
            if (investigate.IsDisposed || copySummary.IsDisposed) return;
            copySummary.AccessibleDescription = lastResult is null
                ? "Disponível após uma consulta para copiar somente contagens."
                : publishedSnapshotGeneration != inventoryGeneration
                    ? "Fotografia anterior revogada: faça nova consulta para copiar o resumo."
                    : "Copia somente as contagens desta fotografia, sem identificadores.";
            investigate.AccessibleDescription = lastResult is null
                ? "Disponível depois de uma consulta local. Sem instalação ou atualização."
                : publishedSnapshotGeneration != inventoryGeneration
                    ? "Fotografia anterior preservada apenas para leitura. Investigar indisponível até nova consulta válida."
                : list.SelectedItem is null
                    ? "Abre relatório geral de drivers, sem dispositivo selecionado e sem verificação INF."
                    : "Abre o relatório do dispositivo selecionado e suas evidências consultivas.";
        }
        bool uninstallBusy = false;
        void SyncDriverActions()
        {
            // A autoridade é a fotografia publicada. O clique seleciona a
            // identidade do cartão antes do handler; não desenhar ações como
            // desabilitadas para depois executá-las no primeiro clique.
            bool current = !root.IsDisposed &&
                publishedSnapshotGeneration == inventoryGeneration &&
                lastResult is not null;
            bool actionable = current && !uninstallBusy && readForTest is null;
            list.SetActionPolicy(actionable, device => lastResult is not null
                ? DriverSilentUninstall.Evaluate(lastResult, device)
                : new DriverSilentUninstallDecision(false, "Inventário indisponível."));
        }
        list.SelectionChanged += (_, _) =>
        {
            SyncInvestigationDescription();
            SyncDriverActions();
            invalidateOpenReport?.Invoke();
        };
        Form? labWindow = null;
        if (labOpen is not null) labOpen.Click += (_, _) =>
        {
            if (root.IsDisposed) return;
            if (labWindow is { IsDisposed: false })
            {
                labWindow.Activate();
                return;
            }
            // Não lê snapshot, lastResult, WMI, WUA, disco ou backend.
            labWindow = DriverLabSimulationDialog.Create();
            labWindow.Show(this);
        };
        root.Disposed += (_, _) =>
        {
            if (labWindow is { IsDisposed: false }) labWindow.Close();
        };
        copySummary.Click += (_, _) =>
        {
            if (root.IsDisposed || copySummary.IsDisposed || !copySummary.Enabled ||
                publishedSnapshotGeneration != inventoryGeneration ||
                lastResult is not { } captured) return;
            try
            {
                Clipboard.SetText(DriverTriageSummary.Format(captured));
                actionHint.Text = "Resumo de contagens copiado, sem identificadores.";
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                actionHint.Text = "Área de transferência indisponível; nenhum dado enviado.";
            }
            actionHint.AccessibleDescription = actionHint.Text;
            _toolTips.SetToolTip(actionHint, actionHint.Text);
        };
        // Inicializar antes de registrar o evento de desinstalacao:
        // RunRefresh captura os tres locais; o compilador exige atribuicao
        // definitiva antes do callback que pode invoca-la.
        string activeFilter = "Todos";
        var driverQuery = new DriverQueryCoordinator();
        bool resultPublished = false;

        void OpenDriverInvestigation(bool autoStartOfficial)
        {
            // Proteção de geração aplicada tanto a Investigar quanto a Atualizar.
            if (publishedSnapshotGeneration != inventoryGeneration ||
                lastResult is not { } captured || uninstallBusy) return;
            var selection = list.SelectedItem;
            var guard = new DriverReportPublicationGuard(captured, selection, inventoryGeneration);
            ShowDriverReadOnlyReport(captured, selection,
                () => guard.IsCurrent(lastResult, list.SelectedItem, inventoryGeneration),
                (token, result) => guard.CanPublish(lastResult, list.SelectedItem,
                    inventoryGeneration, token, result),
                (token, result) => guard.CanPublishInf(lastResult, list.SelectedItem,
                    inventoryGeneration, token, result),
                callback => invalidateOpenReport = callback,
                startOfficialOnShow: autoStartOfficial);
        }
        investigate.Click += (_, _) => OpenDriverInvestigation(false);
        // Um handler por operação. O clique virtual entrega o mesmo objeto que
        // foi selecionado; nenhuma ação usa índice antigo nem nome do dispositivo.
        async Task RunDriverUninstall(DriverInventoryEntry requested)
        {
            if (uninstallBusy || readForTest is not null ||
                publishedSnapshotGeneration != inventoryGeneration ||
                lastResult is not { } captured || list.SelectedItem is not { } selected ||
                !ReferenceEquals(selected, requested))
                return;
            var assessment = DriverSilentUninstall.Evaluate(captured, selected);
            if (!assessment.Allowed)
            {
                MessageBox.Show(this, assessment.Reason, "GF — driver protegido",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string question = "Remover silenciosamente o pacote OEM deste dispositivo?" +
                Environment.NewLine + Environment.NewLine +
                "Dispositivo: " + selected.DeviceName + Environment.NewLine +
                "Instância: " + selected.DeviceId + Environment.NewLine +
                "Pacote: " + selected.InfName + Environment.NewLine +
                "Fornecedor: " + selected.Provider + Environment.NewLine + Environment.NewLine +
                "O GF exportará um backup antes de remover. O Windows poderá exigir reinício." +
                Environment.NewLine + "Nenhum outro pacote será removido deliberadamente.";
            if (MessageBox.Show(this, question, "GF — confirmar desinstalação",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
                != DialogResult.Yes) return;
            uninstallBusy = true;
            bool refreshAfter = false;
            refresh.Enabled = false;
            SyncDriverActions();
            SetDriverStatus("Preparando backup e revalidação do driver...");
            try
            {
                var outcome = await DriverSilentUninstall.Execute(captured, selected, true,
                    () => !root.IsDisposed && publishedSnapshotGeneration == inventoryGeneration &&
                        ReferenceEquals(list.SelectedItem, selected),
                    progress =>
                    {
                        if (root.IsDisposed || !root.IsHandleCreated) return;
                        try { root.BeginInvoke((Action)(() => SetDriverStatus(progress))); }
                        catch (InvalidOperationException) { }
                    });
                if (root.IsDisposed) return;
                if (outcome.RequiresInventoryRefresh)
                {
                    inventoryGeneration++;
                    invalidateOpenReport?.Invoke();
                    investigate.Enabled = false;
                    copySummary.Enabled = false;
                    refreshAfter = true;
                }
                SetDriverStatus(outcome.Message);
                MessageBox.Show(this, outcome.Message + (outcome.BackupPath is null ? "" :
                    Environment.NewLine + Environment.NewLine + "Backup: " + outcome.BackupPath),
                    "GF — desinstalação " + outcome.State, MessageBoxButtons.OK,
                    outcome.State == DriverSilentUninstallState.Verified ?
                        MessageBoxIcon.Information : MessageBoxIcon.Warning);
                if (DriverResidualReviewPolicy.MayInspect(outcome.State))
                    SetDriverStatus("INF removido; resíduos protegidos, sem limpeza genérica.");
            }
            finally
            {
                uninstallBusy = false;
                if (!root.IsDisposed)
                {
                    refresh.Enabled = true;
                    SyncDriverActions();
                }
            }
            if (refreshAfter && !root.IsDisposed) await RunRefresh();
        }
        list.ActionRequested += (item, action) =>
        {
            if (!ReferenceEquals(list.SelectedItem, item) || uninstallBusy ||
                readForTest is not null || publishedSnapshotGeneration != inventoryGeneration ||
                lastResult is null)
                return;
            if (action == DriverCardAction.VerifyUpdate)
                OpenDriverInvestigation(true);
            else
                _ = RunDriverUninstall(item);
        };
        void Render()
        {
            if (root.IsDisposed) return;
            string query = search.Text.Trim();
            var visible = DriverListFilters.Apply(
                snapshot ?? Array.Empty<DriverInventoryEntry>(), activeFilter, query);
            list.SetItems(visible);
            list.ScrollToTop();
            invalidateOpenReport?.Invoke();
            int count = snapshot?.Count ?? 0;
            title.Text = snapshot is null ? "Drivers · consulta não iniciada"
                : $"Drivers: {count} · Exibidos: {visible.Length}";
            title.AccessibleDescription = title.Text;
            _toolTips.SetToolTip(title,
                "Associações de drivers a dispositivos, não pacotes exclusivos do Driver Store.");
            empty.Text = snapshot is null ? "Clique em Consultar drivers para iniciar a leitura."
                : query.Length > 0 ? "Nenhum driver corresponde à pesquisa."
                : count == 0 ? "Nenhuma associação retornada pela consulta."
                : "Nenhuma associação nesta categoria.";
            empty.Visible = visible.Length == 0;
            empty.AccessibleDescription = empty.Text;
            SyncInvestigationDescription();
            SyncDriverActions();
            if (empty.Visible) empty.BringToFront();
            foreach (var (button, name) in new[]
                { (all, "Todos"), (present, "Presentes"), (problems, "Problemas"),
                  (unbound, "Sem driver"), (other, "Não confirmados") })
            {
                bool selected = name == activeFilter;
                button.BackColor = selected ? Color.FromArgb(43, 66, 89)
                    : Color.FromArgb(43, 56, 70);
                button.ForeColor = selected ? Accent : Ink;
                int inCategory = DriverListFilters.Apply(
                    snapshot ?? Array.Empty<DriverInventoryEntry>(), name, "").Length;
                string caution = ReferenceEquals(button, other) ? uncertainFilterExplanation :
                    ReferenceEquals(button, unbound) ? unboundExplanation :
                    ReferenceEquals(button, problems)
                        ? "Problema relatado pelo Windows, não atualização comprovada." : "";
                button.AccessibleDescription = (selected
                    ? "Filtro ativo: " + name : "Ativar filtro: " + name) +
                    $". {inCategory} associações nesta categoria. " + caution;
                _toolTips.SetToolTip(button, button.AccessibleDescription);
            }
            compactFilter.AccessibleDescription = "Filtro atual: " + activeFilter;
        }

        void ApplyFilter(string name)
        {
            activeFilter = name;
            if (compactFilter.SelectedItem is not string current || current != name)
                compactFilter.SelectedItem = name;
            Render();
        }
        search.TextChanged += (_, _) => Render();
        all.Click += (_, _) => ApplyFilter("Todos");
        present.Click += (_, _) => ApplyFilter("Presentes");
        other.Click += (_, _) => ApplyFilter("Não confirmados");
        problems.Click += (_, _) => ApplyFilter("Problemas");
        unbound.Click += (_, _) => ApplyFilter("Sem driver");
        // Apenas fixture: invoca a mesma ação dos botões e do filtro compacto.
        onTestFilterReady?.Invoke(ApplyFilter);
        compactFilter.SelectedIndexChanged += (_, _) =>
        {
            if (compactFilter.SelectedItem is string selected)
            {
                ApplyFilter(selected);
            }
        };
        root.Disposed += (_, _) =>
        {
            // Uma página descartada invalida a fotografia mesmo que seleção e listas não tenham mudado.
            inventoryGeneration++;
            invalidateOpenReport?.Invoke();
            driverQuery.RequestCancel();
        };
        async Task RunRefresh()
        {
            if (uninstallBusy)
            {
                SetDriverStatus("Desinstalação em andamento: consulta bloqueada até a verificação.");
                return;
            }
            if (driverQuery.InProgress)
            {
                if (resultPublished)
                {
                    SetDriverStatus("Finalizando o histórico da consulta já concluída.");
                    return;
                }
                // Cancel() pode retomar WaitAsync/catch/finally imediatamente
                // na thread da UI. Mudar estado ANTES de cancelar evita a
                // corrida da .36 que deixava Cancelando... desabilitado.
                SetRefreshCaption("Cancelando...", "Cancelamento da espera solicitado. A leitura nativa pode continuar em execução.");
                SetDriverStatus("Cancelamento solicitado. Lista anterior preservada.");
                DiagnosticTrace.Write("DRIVERS.CANCELAMENTO_SOLICITADO");
                driverQuery.RequestCancel();
                return;
            }
            if (_driverReadTask is { IsCompleted: false })
            {
                SetDriverStatus("A leitura nativa anterior ainda está encerrando.");
                return;
            }
            var token = driverQuery.Begin(timeoutForTest ?? TimeSpan.FromSeconds(20));
            // Uma captura antiga conserva sua precedencia temporal mesmo
            // se o gravador for agendado muito depois, inclusive entre janelas.
            var queryStartedAt = DateTimeOffset.UtcNow;
            string? historyTestPath = readForTest is null ? null : _driverHistoryPathForTest;
            Func<CancellationToken, Task>? beforeHistoryWrite =
                readForTest is null ? null : _driverHistoryBeforeWriteForTest;
            Action? beforeHistoryReplace = readForTest is null ? null : _driverHistoryBeforeReplaceForTest;
            bool saveHistory = readForTest is null || historyTestPath is not null;
            // Invalida relatórios antigos antes da próxima leitura nativa,
            // inclusive se ela falhar, cancelar ou terminar fora de ordem.
            inventoryGeneration++;
            invalidateOpenReport?.Invoke();
            resultPublished = false;
            SetRefreshCaption("Cancelar", "Cancelar a espera da consulta; a leitura WMI nativa pode continuar.");
            investigate.Enabled = false;
            copySummary.Enabled = false;
            SyncDriverActions();
            actionHint.Text = "Consulta em andamento. Resumo anterior revogado.";
            actionHint.AccessibleDescription = actionHint.Text;
            SyncInvestigationDescription();
            SetDriverStatus("Consultando drivers em segundo plano…");
            _driverReadTask = readForTest is null
                ? Task.Run(() => DriverStoreAudit.CaptureWithPackages(token)) : readForTest(token);
            try
            {
                var captured = await _driverReadTask.WaitAsync(token);
                if (root.IsDisposed || !driverQuery.CanPublish(token)) return;
                // G1.22: nenhuma lista ou evidencia e publicada antes de
                // detectar alteracao durante a fronteira de congelamento.
                // Nao promete atomicidade contra escritor externo que coopere
                // zero; rejeita incoerencias observadas sem retry/timer.
                var result = DriverInventorySnapshot.FreezeStable(captured, token);
                var historyInventory = result;
                long publishedGeneration = inventoryGeneration;
                publishedSnapshotGeneration = publishedGeneration;
                snapshot = result.Items;
                lastResult = result;
                investigate.Enabled = true;
                copySummary.Enabled = true;
                resultPublished = true;
                Render();
                actionHint.Text =
                    $"PnP: {result.Items.Count(x => x.HasPnpProblem)} · " +
                    $"Sem vínculo WMI: {result.Items.Count(x => x.WithoutSignedDriverAssociation)} · " +
                    $"Não confirmados: {result.Items.Count(x => x.Present != true)}. Só consulta.";
                actionHint.AccessibleDescription = actionHint.Text;
                _toolTips.SetToolTip(actionHint, actionHint.Text);
                // A lista agora representa OUTRA fotografia. O rodapé não deve
                // anunciar a comparação da fotografia anterior enquanto a
                // gravação desta ainda ocorre. Fixtures não gravam histórico.
                history.Text = saveHistory
                    ? "Histórico local: comparação desta consulta em andamento; resultado ainda não confirmado."
                    : "Histórico sintético desta consulta: não gravado nem comparado com o histórico local.";
                history.AccessibleDescription = history.Text;
                _toolTips.SetToolTip(history, history.Text);
                // Depois da publicação, só resta concluir o histórico; não existe
                // mais consulta WMI aguardando cancelamento por este botão.
                refresh.Enabled = false;
                SetRefreshCaption("Finalizando...", "Concluindo o histórico local da consulta. Ações de drivers continuam desabilitadas.");
                var links = DriverPackageLinks.Analyze(result);
                var signedEntries = result.Items.Where(x => !x.WithoutSignedDriverAssociation).ToArray();
                SetDriverStatus($"{result.Status}: {result.Items.Count} dispositivos/associações, " +
                    $"{result.Items.Count(x => x.HasPnpProblem)} com código PnP, " +
                    $"{result.Items.Count(x => x.WithoutSignedDriverAssociation)} sem driver associado, " +
                    $"{links.Packages.Count} INFs. Store: {result.Store?.Packages.Count(x => x.State == DriverStoreState.Mapped) ?? 0} vínculos. Sem alterações.");
                // A dica deve incluir o MESMO historico anunciado ao leitor de
                // tela, e nao apenas o inventario recem-publicado.
                _toolTips.SetToolTip(state,
                    state.AccessibleDescription + Environment.NewLine +
                    result.Store?.Summary + " " + result.WindowsHistory?.Summary + " " + result.PublishedPackages?.Summary);
                DiagnosticTrace.Write($"DRIVERS.VINCULOS infs={links.Packages.Count} compartilhados={links.Packages.Count(p => p.Devices > 1)} versoes_divergentes={links.Packages.Count(p => p.Versions > 1)} assinaturas_conflitantes={links.Packages.Count(p => p.ConflictingSignatures)} sem_inf={links.WithoutInf}");
                if (result.Store is { } store)
                    TestDiagnosticDetail.Record("PACOTES", ("pacotes", store.Packages.Count),
                        ("mapeados", store.Packages.Count(x => x.State == DriverStoreState.Mapped)),
                        ("indisponiveis", store.Packages.Count(x => x.State == DriverStoreState.Unavailable)),
                        ("inconsistentes", store.Packages.Count(x => x.State == DriverStoreState.Inconsistent)),
                        ("nao_consultados", store.Packages.Count(x => x.State == DriverStoreState.NotConsulted)),
                        ("pnp", result.PnpEntities), ("wmi_brutos", result.SignedRows),
                        ("parcial", store.Partial ? 1 : 0));
                DiagnosticTrace.Write($"DRIVERS.INVENTARIO quantidade={result.Items.Count} parcial={result.Partial}");
                TestDiagnosticDetail.Record("DRIVERS",
                    ("associacoes", signedEntries.Length),
                    ("ids_unicos", signedEntries.Select(x => x.DeviceId).Distinct(StringComparer.OrdinalIgnoreCase).Count()),
                    ("sem_nome", signedEntries.Count(x => string.IsNullOrWhiteSpace(x.DeviceName))),
                    ("sem_fornecedor", signedEntries.Count(x => string.IsNullOrWhiteSpace(x.Provider))),
                    ("sem_versao", signedEntries.Count(x => string.IsNullOrWhiteSpace(x.Version))),
                    ("versao_incomum", signedEntries.Count(x => !string.IsNullOrWhiteSpace(x.Version) &&
                        !Version.TryParse(x.Version, out _))),
                    ("sem_inf", links.WithoutInf),
                    ("assinatura_indeterminada", signedEntries.Count(x => !x.Signed.HasValue)),
                    ("presenca_indeterminada", signedEntries.Count(x => !x.Present.HasValue)),
                    ("nao_presentes", signedEntries.Count(x => x.Present == false)),
                    ("parcial", result.Partial ? 1 : 0));
                // Fixtures sintéticas exercitam a mesma publicação WinForms,
                // mas jamais escrevem no histórico pessoal do runner.
                if (saveHistory)
                {
                    if (beforeHistoryWrite is not null)
                        await beforeHistoryWrite(token);
                    if (root.IsDisposed || publishedGeneration != inventoryGeneration ||
                        !driverQuery.CanPublish(token)) return;
                    var recorded = await Task.Run(() => DriverHistory.Record(
                        readForTest is null ? DriverHistory.DefaultPath : historyTestPath!,
                        historyInventory, queryStartedAt, token, beforeHistoryReplace));
                    if (!root.IsDisposed && publishedGeneration == inventoryGeneration &&
                        driverQuery.CanPublish(token))
                    {
                        history.Text = recorded.Message;
                        history.AccessibleDescription = recorded.Message + Environment.NewLine + recorded.Timeline;
                        _toolTips.SetToolTip(history, history.AccessibleDescription);
                        SetDriverStatus(state.Text);
                        FitVerticalSpace();
                    }
                    DiagnosticTrace.Write($"DRIVERS.HISTORICO salvo={recorded.Saved} base={recorded.Baseline} novos={recorded.Added} nao_observados={recorded.Missing} alterados={recorded.Changed}");
                }
            }
            catch (OperationCanceledException)
            {
                if (!root.IsDisposed) SetDriverStatus(refresh.Text == "Cancelando..."
                    ? "Consulta cancelada. Lista anterior preservada; sem alterações."
                    : "Tempo esgotado. Lista anterior preservada; sem alterações.");
                DiagnosticTrace.Write("DRIVERS.CONSULTA_CANCELADA");
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Failure("DRIVERS.INVENTARIO_FALHA", ex);
                if (!root.IsDisposed) SetDriverStatus(
                    "Consulta indisponível. Nenhuma alteração foi feita.");
            }
            finally
            {
                driverQuery.End(token);
                if (!refresh.IsDisposed)
                {
                    SetRefreshCaption("Consultar drivers", "Consulta apenas dispositivos e vínculos locais. Não modifica drivers.");
                    // WMI pode ignorar o cancelamento nativo por algum tempo.
                    // Nunca bloquear a interface: o teste de _driverReadTask
                    // no inicio do clique continua impedindo consultas simultaneas.
                    refresh.Enabled = true;
                    investigate.Enabled = lastResult is not null &&
                        publishedSnapshotGeneration == inventoryGeneration;
                    copySummary.Enabled = investigate.Enabled;
                    SyncInvestigationDescription();
                    SyncDriverActions();
                    if (_driverReadTask is { IsCompleted: false })
                        SetDriverStatus("Leitura nativa ainda encerrando; lista anterior preservada. Tente novamente depois.");
                }
            }
        }
        refresh.Click += async (_, _) => await RunRefresh();
        // Apenas fixture: usa o mesmo handler, com provedor local sintético.
        onTestReadActionReady?.Invoke(() => _ = RunRefresh());
        // Em resolucoes menores, deixar a area da lista prevalecer.
        // O rodape detalhado permanece acessivel pela descricao/tooltip.
        void FitVerticalSpace()
        {
            if (root.IsDisposed) return;
            int dpi = root.DeviceDpi;
            int Scale(int px) => DriverRowGeometry.Scale(px, dpi);
            bool tiny = root.ClientSize.Height < Scale(230);
            bool compact = root.ClientSize.Height < Scale(360);
            // Comparar largura física com dimensões físicas da fonte, e não
            // apenas com uma constante sem DPI.
            int refreshWidth = Math.Max(Scale(156),
                TextRenderer.MeasureText("Consultar drivers", refresh.Font).Width + Scale(20));
            int investigationWidth = Math.Max(Scale(108),
                TextRenderer.MeasureText("Investigar", investigate.Font).Width + Scale(20));
            bool narrow = root.ClientSize.Width < Math.Max(Scale(760),
                refreshWidth + investigationWidth + Scale(182));
            bool compactFilters = tiny || root.ClientSize.Width < Scale(800);
            bool showCopy = !tiny && !narrow;
            bool splitActions = labOpen is null && root.ClientSize.Width <
                refreshWidth + investigationWidth + Scale(92);
            root.RowStyles[0].Height = tiny ? Scale(30) : Scale(35);
            root.RowStyles[1].Height = splitActions ? Scale(79) :
                tiny ? Scale(35) : Scale(44);
            root.RowStyles[2].Height = compactFilters ? 0 : Scale(36);
            root.RowStyles[4].Height = tiny ? Scale(27) :
                compact ? Scale(41) : Scale(78);
            footer.RowStyles[0].Height = compact ? 100 : 55;
            footer.RowStyles[1].Height = compact ? 0 : 45;
            heading.Visible = !compactFilters;
            actionPanel.ColumnStyles[0].Width = refreshWidth;
            actionPanel.ColumnStyles[1].Width = investigationWidth;
            if (splitActions)
            {
                if (actionPanel.RowCount != 2)
                {
                    actionPanel.RowCount = 2;
                    actionPanel.RowStyles.Clear();
                    actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                    actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                }
                actionPanel.SetCellPosition(investigate, new TableLayoutPanelCellPosition(1, 0));
                actionPanel.SetColumnSpan(actionInfo, 3);
                actionPanel.SetCellPosition(actionInfo, new TableLayoutPanelCellPosition(0, 1));
            }
            else
            {
                actionPanel.SetColumnSpan(actionInfo, 1);
                actionPanel.SetCellPosition(actionInfo, new TableLayoutPanelCellPosition(2, 0));
                if (actionPanel.RowCount != 1)
                {
                    actionPanel.RowCount = 1;
                    actionPanel.RowStyles.Clear();
                    actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                }
            }
            // Medir os filtros na fonte real; em espaço insuficiente,
            // usar o ComboBox existente, com os cinco rótulos completos.
            foreach (var (button, column) in new[]
                { (all, 1), (present, 2), (problems, 3), (unbound, 4), (other, 5) })
                heading.ColumnStyles[column].Width = Math.Max(Scale(56),
                    TextRenderer.MeasureText(button.Text, button.Font,
                        new Size(int.MaxValue, Scale(36)),
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width +
                    Scale(36));
            copySummary.Visible = showCopy;
            statusBar.ColumnStyles[1].Width = showCopy
                ? Math.Max(Scale(132), TextRenderer.MeasureText(copySummary.Text, copySummary.Font).Width + Scale(16))
                : 0;
            actionHint.Visible = !compactFilters;
            compactFilter.Visible = compactFilters;
            if (compactFilter.SelectedItem is string current && current != activeFilter)
                compactFilter.SelectedItem = activeFilter;
            history.Visible = !compact;
            _toolTips.SetToolTip(history, history.AccessibleDescription ?? history.Text);
            SetDriverStatus(state.Text);
        }
        root.SizeChanged += (_, _) => FitVerticalSpace();
        root.DpiChangedAfterParent += (_, _) => FitVerticalSpace();
        Render();
        FitVerticalSpace();
        return root;
    }
}
