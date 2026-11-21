using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private Task<DriverInventoryResult>? _driverReadTask;
    // Cache WUA válido somente para a mesma fotografia que o usuário consultou.
    private DriverInventoryResult? _driverReusableInventory;
    private DriverOfficialResult? _driverReusableOfficial;
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
        // Descrições extensas ficam disponíveis na acessibilidade, não no mouse.
        void SuppressHoverHint(Control target, string explanation) =>
            _toolTips.SetToolTip(target, string.Empty);

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
        SuppressHoverHint(clear, "Limpa apenas o campo de pesquisa, sem alterar drivers.");
        searchBar.Controls.Add(clear, 2, 0);
        root.Controls.Add(searchBar, 0, 0);

        // Barra de ações gerais: somente leitura e investigação.
        // Manutenção é desenhada virtualmente por dispositivo em DarkDriverList.
        var actionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
            Margin = new Padding(0, 2, 0, 2)
        };
        actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var refresh = CreateButton("Consultar drivers", AppServices.Blue, Color.White);
        refresh.Name = "DriverInventoryRefresh";
        refresh.AccessibleName = "Consultar drivers";
        refresh.Dock = DockStyle.Fill;
        refresh.Margin = new Padding(0, 2, 6, 2);
        actionPanel.Controls.Add(refresh, 0, 0);
        SuppressHoverHint(refresh,
            "Consulta dispositivos e ofertas WUA, sem instalar ou remover.");
        var updateDrivers = CreateButton("Atualizar drivers", AppServices.Green, Color.White);
        updateDrivers.Name = "DriverUpdateEligible";
        updateDrivers.AccessibleName = "Revisar drivers com oferta WUA elegível";
        updateDrivers.AccessibleDescription =
            "Abre a seleção de ofertas oficiais exatas e elegíveis nesta consulta. " +
            "Cada instalação requer revalidação, backup OEM, UAC e consentimento separado.";
        updateDrivers.Dock = DockStyle.Fill;
        updateDrivers.Margin = new Padding(0, 2, 6, 2);
        updateDrivers.Enabled = false;
        actionPanel.Controls.Add(updateDrivers, 1, 0);
        SuppressHoverHint(updateDrivers, updateDrivers.AccessibleDescription);
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
        SuppressHoverHint(compactFilter,
            "Filtro somente visual: todos, presentes, problemas PnP, sem driver ou não confirmados.");
        actionInfo.Controls.Add(actionHint);
        actionInfo.Controls.Add(compactFilter);
        var investigate = CreateSecondaryButton("Detalhes");
        investigate.Name = "DriverReadOnlyInvestigation";
        investigate.AccessibleName = "Detalhes e evidências de drivers";
        investigate.Dock = DockStyle.Fill;
        investigate.Margin = new Padding(0, 2, 6, 2);
        investigate.Enabled = false;
        actionPanel.Controls.Add(investigate, 2, 0);
        SuppressHoverHint(investigate,
            "Histórico Windows, INFs publicados, consulta oficial e verificação do INF. Somente leitura.");
        actionPanel.Controls.Add(actionInfo, 3, 0);
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
            actionPanel.ColumnCount = 5;
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            actionPanel.Controls.Add(labOpen, 4, 0);
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
        SuppressHoverHint(copySummary,
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
        SuppressHoverHint(problems, "Códigos de problema informados pelo Windows, inclusive drivers não instalados; não indica automaticamente uma atualização.");
        var unbound = Filter("Sem driver", 4);
        unbound.AccessibleName = "Dispositivos sem associação WMI de driver assinado";
        const string unboundExplanation =
            "Sem linha de driver assinado associada no WMI. Não prova ausência de driver nem atualização disponível.";
        unbound.AccessibleDescription = unboundExplanation;
        SuppressHoverHint(unbound, unboundExplanation);
        var other = Filter("Outros", 5);
        other.AccessibleName = "Não confirmados";
        const string uncertainFilterExplanation =
            "Dispositivos ausentes ou com presença indeterminada. Não significa driver obsoleto.";
        other.AccessibleDescription = uncertainFilterExplanation;
        SuppressHoverHint(other, uncertainFilterExplanation);
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
        bool syncingStatusAccessibility = false;
        bool statusSyncPending = false;
        void SyncStatusAccessibility()
        {
            if (state.IsDisposed) return;
            // TextChanged e layout WinForms podem reentrar durante um timeout:
            // uma sincronização externa não pode sobrescrever o texto mais
            // recente com a descrição capturada antes da reentrada.
            if (syncingStatusAccessibility)
            {
                statusSyncPending = true;
                return;
            }
            syncingStatusAccessibility = true;
            try
            {
                do
                {
                    statusSyncPending = false;
                    string visible = state.Text;
                    string historyText = history.Text;
                    string? historyDescription = history.AccessibleDescription;
                    string description = visible + Environment.NewLine + historyText +
                        (historyDescription == historyText ? "" :
                            Environment.NewLine + historyDescription);
                    state.AccessibleDescription = description;
                    if (!string.Equals(state.Text, visible, StringComparison.Ordinal) ||
                        !string.Equals(history.Text, historyText, StringComparison.Ordinal))
                        statusSyncPending = true;
                }
                while (statusSyncPending && !state.IsDisposed);
            }
            finally { syncingStatusAccessibility = false; }
        }
        state.TextChanged += (_, _) => SyncStatusAccessibility();
        history.TextChanged += (_, _) => SyncStatusAccessibility();
        void SetDriverStatus(string message)
        {
            if (state.IsDisposed) return;
            // Publicar primeiro a nova descrição acessível. TextChanged,
            // Paint e DoEvents podem reentrar imediatamente ao atribuir Text:
            // nesse intervalo o leitor de tela já deve enxergar o novo estado,
            // não a descrição anterior de uma consulta em encerramento.
            string historyText = history.Text;
            string? historyDescription = history.AccessibleDescription;
            state.AccessibleDescription = message + Environment.NewLine + historyText +
                (historyDescription == historyText ? "" :
                    Environment.NewLine + historyDescription);
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
            SuppressHoverHint(refresh, description);
        }
        SetDriverStatus(state.Text);
        SetRefreshCaption("Consultar drivers", "Consulta dispositivos e ofertas WUA. Não modifica drivers.");

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
        // Estado WUA declarado antes dos handlers que o observam.
        DriverOfficialResult? latestOfficial = null;
        long officialGeneration = -1;
        IReadOnlySet<string> eligibleUpdateIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
            if (!updateDrivers.IsDisposed)
                updateDrivers.Enabled = actionable &&
                    officialGeneration == inventoryGeneration &&
                    latestOfficial is { Partial: false } && eligibleUpdateIds.Count > 0;
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
            SuppressHoverHint(actionHint, actionHint.Text);
        };
        // Inicializar antes de registrar o evento de desinstalacao:
        // RunRefresh captura os tres locais; o compilador exige atribuicao
        // definitiva antes do callback que pode invoca-la.
        string activeFilter = "Todos";
        var driverQuery = new DriverQueryCoordinator();
        bool resultPublished = false;
        CancellationTokenSource? wuaWait = null;

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
                startOfficialOnShow: autoStartOfficial && latestOfficial is null);
        }
        investigate.Click += (_, _) => OpenDriverInvestigation(false);

        void OpenDriverUpdate(DriverInventoryEntry requested)
        {
            if (root.IsDisposed || uninstallBusy || readForTest is not null ||
                publishedSnapshotGeneration != inventoryGeneration ||
                officialGeneration != inventoryGeneration ||
                lastResult is not { } inventory ||
                latestOfficial is not { Partial: false } official ||
                !eligibleUpdateIds.Contains(requested.DeviceId) ||
                !inventory.Items.Any(item => ReferenceEquals(item, requested)))
                return;
            // A elegibilidade da interface não substitui a nova prova no clique.
            var matches = official.Matches.Where(m =>
                string.Equals(m.DeviceId, requested.DeviceId,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1 ||
                !DriverUpdatePilot.TryPrepare(inventory, official, matches[0],
                    out var choice) || choice is null ||
                !DriverG32RecoveryTicket.CanOffer(inventory, requested, choice))
                return;
            long generation = inventoryGeneration;
            using var dialog = DriverUpdatePilotDialog.Create(inventory, official,
                requested, () => !root.IsDisposed && !uninstallBusy &&
                    generation == inventoryGeneration &&
                    publishedSnapshotGeneration == generation &&
                    ReferenceEquals(lastResult, inventory) &&
                    ReferenceEquals(latestOfficial, official),
                DriverG31Wiring.NewProductionBackend(),
                DriverG31Wiring.NewProductionJournalFactory());
            dialog.ShowDialog(this);
        }

        void SelectDriverUpdate()
        {
            if (!updateDrivers.Enabled || lastResult is not { } inventory) return;
            var candidates = inventory.Items.Where(item =>
                eligibleUpdateIds.Contains(item.DeviceId))
                .OrderBy(item => item.DeviceName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            if (candidates.Length == 0) return;
            using var picker = new Form
            {
                Text = "Drivers — escolher atualização WUA",
                Name = "DriverUpdatePicker",
                Width = 730, Height = 380, MinimumSize = new Size(470, 290),
                StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
                BackColor = AppServices.Background, ForeColor = Ink
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill, ForeColor = Ink,
                Text = "Selecione um dispositivo. A oferta será revalidada; nenhuma instalação automática.",
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            var options = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
                HideSelection = false, MultiSelect = false, BackColor = PanelBackground,
                ForeColor = Ink, AccessibleName = "Drivers com oferta WUA elegível"
            };
            options.Columns.Add("Dispositivo", 380);
            options.Columns.Add("Fornecedor", 220);
            foreach (var candidate in candidates)
            {
                var row = new ListViewItem(string.IsNullOrWhiteSpace(candidate.DeviceName)
                    ? "Dispositivo não identificado" : candidate.DeviceName)
                { Tag = candidate };
                row.SubItems.Add(candidate.Provider);
                options.Items.Add(row);
            }
            layout.Controls.Add(options, 0, 1);
            var confirmUpdate = CreateButton("Revisar atualização", AppServices.Green, Color.White);
            confirmUpdate.Enabled = false;
            confirmUpdate.Dock = DockStyle.Right;
            confirmUpdate.Width = 182;
            confirmUpdate.Click += (_, _) =>
            {
                if (options.SelectedItems.Count == 1)
                {
                    picker.DialogResult = DialogResult.OK;
                    picker.Close();
                }
            };
            options.SelectedIndexChanged += (_, _) =>
                confirmUpdate.Enabled = options.SelectedItems.Count == 1;
            options.DoubleClick += (_, _) =>
            {
                if (confirmUpdate.Enabled) confirmUpdate.PerformClick();
            };
            layout.Controls.Add(confirmUpdate, 0, 2);
            picker.Controls.Add(layout);
            if (picker.ShowDialog(this) == DialogResult.OK &&
                options.SelectedItems.Count == 1 &&
                options.SelectedItems[0].Tag is DriverInventoryEntry chosen)
                OpenDriverUpdate(chosen);
        }
        updateDrivers.Click += (_, _) => SelectDriverUpdate();
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
                string explanation = "Este driver não pode ser removido automaticamente pelo GF." +
                    Environment.NewLine + assessment.Reason + Environment.NewLine +
                    Environment.NewLine + "Abrir o Gerenciador de Dispositivos do Windows para " +
                    "você decidir se deseja desinstalá-lo? Nenhum driver será removido pelo GF.";
                if (MessageBox.Show(this, explanation, "GF — desinstalação manual de driver",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "devmgmt.msc", UseShellExecute = true
                        });
                        SetDriverStatus("Gerenciador de Dispositivos aberto. Decisão e remoção somente pelo Windows.");
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        SetDriverStatus("Não foi possível abrir o Gerenciador de Dispositivos.");
                    }
                }
                return;
            }
            if (!DriverSilentUninstall.IsElevated())
            {
                // Solicitar UAC somente para esta remoção. O trabalhador
                // recaptura todo o inventário e pede confirmação explícita;
                // nenhum snapshot da interface vira autoridade administrativa.
                uninstallBusy = true;
                refresh.Enabled = false;
                SyncDriverActions();
                SetDriverStatus("Solicitando ao Windows autorização para remover o pacote OEM...");
                bool started = false;
                try
                {
                    var elevation = await DriverElevatedUninstall.RequestAsync(selected);
                    started = elevation.Started;
                    if (!root.IsDisposed) SetDriverStatus(elevation.Message);
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
                // Mesmo com saída zero, o resultado do trabalhador não é
                // aceito sem uma NOVA leitura do estado PnP local.
                if (started && !root.IsDisposed) await RunRefresh();
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
            if (action == DriverCardAction.Details)
                OpenDriverInvestigation(false);
            else if (action == DriverCardAction.Update)
                OpenDriverUpdate(item);
            else if (action == DriverCardAction.Uninstall)
                _ = RunDriverUninstall(item);
        };
        void Render()
        {
            if (root.IsDisposed) return;
            string query = search.Text.Trim();
            var visible = DriverListFilters.Apply(
                snapshot ?? Array.Empty<DriverInventoryEntry>(), activeFilter, query,
                eligibleUpdateIds);
            list.SetItems(visible);
            if (officialGeneration == inventoryGeneration &&
                latestOfficial is { Partial: false } cached)
                list.SetOfferDevices(cached.Matches.Select(match => match.DeviceId));
            if (officialGeneration == inventoryGeneration)
                list.SetEligibleUpdateDevices(eligibleUpdateIds);
            list.ScrollToTop();
            invalidateOpenReport?.Invoke();
            int count = snapshot?.Count ?? 0;
            title.Text = snapshot is null ? "Drivers · consulta não iniciada"
                : $"Drivers: {count} · Exibidos: {visible.Length}";
            title.AccessibleDescription = title.Text;
            SuppressHoverHint(title,
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
                SuppressHoverHint(button, button.AccessibleDescription);
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
            if (wuaWait is { IsCancellationRequested: false })
            {
                wuaWait.Cancel();
                SetDriverStatus("Cancelamento solicitado; pesquisa WUA pode continuar encerrando.");
                return;
            }
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
            if (_driverReadTask is { IsCompleted: false } ||
                readForTest is null && _driverOfficialReadTask is { IsCompleted: false })
            {
                SetDriverStatus("Leitura nativa ainda encerrando; lista anterior preservada. Tente novamente depois.");
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
            latestOfficial = null;
            officialGeneration = -1;
            eligibleUpdateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _driverReusableInventory = null;
            _driverReusableOfficial = null;
            list.SetOfferDevices(Array.Empty<string>());
            list.SetEligibleUpdateDevices(Array.Empty<string>());
            updateDrivers.Enabled = false;
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
                SuppressHoverHint(actionHint, actionHint.Text);
                // A lista agora representa OUTRA fotografia. O rodapé não deve
                // anunciar a comparação da fotografia anterior enquanto a
                // gravação desta ainda ocorre. Fixtures não gravam histórico.
                history.Text = saveHistory
                    ? "Histórico local: comparação desta consulta em andamento; resultado ainda não confirmado."
                    : "Histórico sintético desta consulta: não gravado nem comparado com o histórico local.";
                history.AccessibleDescription = history.Text;
                SuppressHoverHint(history, history.Text);
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
                SuppressHoverHint(state,
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
                        SuppressHoverHint(history, history.AccessibleDescription);
                        SetDriverStatus(state.Text);
                        FitVerticalSpace();
                    }
                    DiagnosticTrace.Write($"DRIVERS.HISTORICO salvo={recorded.Saved} base={recorded.Baseline} novos={recorded.Added} nao_observados={recorded.Missing} alterados={recorded.Changed}");
                }
                // O único botão Consultar inclui agora uma pesquisa WUA
                // SOMENTE LEITURA. Não consulta o mesmo catálogo em cada cartão.
                // Fixtures seguem sem COM, sem rede e sem alteração de drivers.
                if (readForTest is null && !result.Partial && result.Items.Count > 0 &&
                    !root.IsDisposed && publishedGeneration == inventoryGeneration)
                {
                    using var officialTimeout = new CancellationTokenSource(
                        TimeSpan.FromSeconds(90));
                    wuaWait = officialTimeout;
                    SetRefreshCaption("Cancelar WUA",
                        "Cancela a espera; a pesquisa COM pode continuar terminando.");
                    refresh.Enabled = true;
                    SetDriverStatus("Inventário concluído. Consultando ofertas oficiais WUA...");
                    try
                    {
                        _driverOfficialReadTask = DriverOfficialLookup.Start(
                            result, officialTimeout.Token);
                        var officialResult = await _driverOfficialReadTask.WaitAsync(
                            officialTimeout.Token);
                        if (!root.IsDisposed && publishedGeneration == inventoryGeneration)
                        {
                            if (officialResult.Partial)
                                SetDriverStatus(
                                    "Inventário concluído; consulta WUA parcial ou indisponível. " +
                                    "Isso não comprova ausência de atualização.");
                            else
                            {
                                latestOfficial = officialResult;
                                officialGeneration = publishedGeneration;
                                eligibleUpdateIds = DriverUpdateEligibility.Find(
                                    result, officialResult);
                                _driverReusableInventory = result;
                                _driverReusableOfficial = officialResult;
                                var deviceIds = officialResult.Matches
                                    .Select(match => match.DeviceId)
                                    .Where(id => !string.IsNullOrWhiteSpace(id))
                                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                                Render();
                                SetDriverStatus(
                                    $"Consulta concluída: {result.Items.Count} dispositivos · " +
                                    $"{deviceIds.Length} com oferta WUA consultiva · " +
                                    $"{eligibleUpdateIds.Count} elegíveis para revisão G3.2. " +
                                    "A oferta não comprova versão superior.");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (!root.IsDisposed && publishedGeneration == inventoryGeneration)
                            SetDriverStatus(
                                "Inventário preservado. Pesquisa WUA interrompida ou esgotada; " +
                                "nenhuma atualização foi instalada.");
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        DiagnosticTrace.Failure("DRIVERS.WUA_CONSULTA_FALHA", ex);
                        if (!root.IsDisposed && publishedGeneration == inventoryGeneration)
                            SetDriverStatus(
                                "Inventário concluído. Consulta de atualizações indisponível.");
                    }
                    finally
                    {
                        wuaWait = null;
                        if (!refresh.IsDisposed) refresh.Enabled = false;
                    }
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
                    SetRefreshCaption("Consultar drivers", "Consulta dispositivos e ofertas WUA. Não modifica drivers.");
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
                TextRenderer.MeasureText("Detalhes", investigate.Font).Width + Scale(20));
            int updateWidth = Math.Max(Scale(148),
                TextRenderer.MeasureText("Atualizar drivers", updateDrivers.Font).Width + Scale(20));
            bool narrow = root.ClientSize.Width < Math.Max(Scale(760),
                refreshWidth + updateWidth + investigationWidth + Scale(110));
            bool compactFilters = tiny || root.ClientSize.Width < Scale(800);
            bool showCopy = !tiny && !narrow;
            // A ação global ocupa uma coluna extra. Em janela estreita,
            // também nas fixtures com Simular, mover o filtro compacto para
            // a segunda linha em vez de deixá-lo com 0-24 px.
            bool splitActions = root.ClientSize.Width <
                refreshWidth + updateWidth + investigationWidth +
                (labOpen is null ? Scale(92) : Scale(230));
            root.RowStyles[0].Height = tiny ? Scale(30) : Scale(35);
            root.RowStyles[1].Height = splitActions ? Scale(79) :
                tiny ? Scale(35) : Scale(44);
            root.RowStyles[2].Height = compactFilters ? 0 : Scale(36);
            // Em SVGA, devolver altura à lista; no modo tiny o histórico
            // já fica oculto e o estado completo continua no tooltip.
            root.RowStyles[4].Height = tiny ? Scale(19) :
                compact ? Scale(41) : Scale(78);
            footer.RowStyles[0].Height = compact ? 100 : 55;
            footer.RowStyles[1].Height = compact ? 0 : 45;
            heading.Visible = !compactFilters;
            actionPanel.ColumnStyles[0].Width = refreshWidth;
            actionPanel.ColumnStyles[1].Width = updateWidth;
            actionPanel.ColumnStyles[2].Width = investigationWidth;
            if (splitActions)
            {
                if (actionPanel.RowCount != 2)
                {
                    actionPanel.RowCount = 2;
                    actionPanel.RowStyles.Clear();
                    actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                    actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                }
                actionPanel.SetCellPosition(investigate, new TableLayoutPanelCellPosition(2, 0));
                // Com laboratório sintético, Simular conserva a sua largura
                // e o filtro ocupa duas colunas inteiras em modo compacto.
                if (labOpen is not null)
                {
                    actionPanel.ColumnStyles[4].Width = 0;
                    actionPanel.SetColumnSpan(actionInfo, 2);
                    actionPanel.SetCellPosition(actionInfo, new TableLayoutPanelCellPosition(0, 1));
                    actionPanel.SetCellPosition(labOpen, new TableLayoutPanelCellPosition(2, 1));
                }
                else
                {
                    actionPanel.SetColumnSpan(actionInfo, 4);
                    actionPanel.SetCellPosition(actionInfo, new TableLayoutPanelCellPosition(0, 1));
                }
            }
            else
            {
                if (labOpen is not null)
                {
                    actionPanel.ColumnStyles[4].Width = Scale(100);
                    actionPanel.SetCellPosition(labOpen, new TableLayoutPanelCellPosition(4, 0));
                }
                actionPanel.SetColumnSpan(actionInfo, 1);
                actionPanel.SetCellPosition(actionInfo, new TableLayoutPanelCellPosition(3, 0));
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
            SuppressHoverHint(history, history.AccessibleDescription ?? history.Text);
            SetDriverStatus(state.Text);
        }
        root.SizeChanged += (_, _) => FitVerticalSpace();
        root.DpiChangedAfterParent += (_, _) => FitVerticalSpace();
        Render();
        FitVerticalSpace();
        return root;
    }
}
