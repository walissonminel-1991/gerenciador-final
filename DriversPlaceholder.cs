using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private Task<DriverInventoryResult>? _driverReadTask;
    private Action<bool>? _presentV269DriverDetails;
    // Cache WUA válido somente para a mesma fotografia que o usuário consultou.
    private DriverInventoryResult? _driverReusableInventory;
    private DriverOfficialResult? _driverReusableOfficial;
    // Exclusivo das fixtures com readForTest: jamais acessar DefaultPath
    // do usuario nos testes temporais ou iniciar captura nativa.
    // Fixtures atribuem estes campos por reflexão; null é o estado intencional
    // em produção. Preservar os nomes e não ativar nenhum hook automaticamente.
    private string? _driverHistoryPathForTest = null;
    private Func<CancellationToken, Task>? _driverHistoryBeforeWriteForTest = null;
    private Action? _driverHistoryBeforeReplaceForTest = null;

    private Control BuildDriversPlaceholder(
        Func<CancellationToken, Task<DriverInventoryResult>>? readForTest = null,
        TimeSpan? timeoutForTest = null,
        Action<Action>? onTestReadActionReady = null,
        Action<Action<string>>? onTestFilterReady = null)
    {
        bool v269 = readForTest is null;
        const string instruction = "Clique em Verificar para consultar os dispositivos e as ofertas de drivers.";

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
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty, BackColor = AppServices.Background
        };
        searchBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var searchLabel = new Label
        {
            Text = "Pesquisar:", Dock = DockStyle.Fill, ForeColor = Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false, AutoEllipsis = true
        };
        searchBar.Controls.Add(searchLabel, 0, 0);
        var search = new V269AppSearchEditor
        {
            Name = "DriverInventorySearch", Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(19, 26, 34), ForeColor = Ink,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Nome, categoria, fabricante, ID ou INF",
            AccessibleDescription = "Pesquise por nome, categoria como vídeo ou controles de jogo, fabricante, ID de hardware, versão ou INF.",
            Margin = new Padding(0, 3, 8, 3)
        };
        searchBar.Controls.Add(search, 1, 0);
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
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var refresh = v269 ? new V269AppToolbarButton("Verificar", true) :
            CreateButton("Consultar drivers", AppServices.Blue, Color.White);
        refresh.Name = "DriverInventoryRefresh";
        refresh.AccessibleName = "Consultar drivers";
        refresh.Dock = DockStyle.Fill;
        refresh.Margin = new Padding(0, 2, 6, 2);
        actionPanel.Controls.Add(refresh, 0, 0);
        refresh.AccessibleDescription = "Consulta dispositivos e ofertas WUA, sem instalar ou remover.";
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
        compactFilter.AccessibleDescription = "Filtro somente visual: todos, presentes, problemas PnP, sem driver ou não confirmados.";
        actionInfo.Controls.Add(actionHint);
        // Filtros legados existem apenas no laboratório sintético, nunca na página do produto.
        if (readForTest is not null) actionInfo.Controls.Add(compactFilter);
        var investigate = CreateSecondaryButton("Detalhes");
        investigate.Name = "DriverReadOnlyInvestigation";
        investigate.AccessibleName = "Detalhes e evidências de drivers";
        investigate.Dock = DockStyle.Fill;
        investigate.Margin = new Padding(0, 2, 6, 2);
        investigate.Enabled = false;
        actionPanel.Controls.Add(investigate, 2, 0);
        investigate.AccessibleDescription = "Histórico Windows, INFs publicados, consulta oficial e verificação do INF. Somente leitura.";
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
        problems.AccessibleDescription = "Códigos de problema informados pelo Windows, inclusive drivers não instalados; não indica automaticamente uma atualização.";
        var unbound = Filter("Sem driver", 4);
        unbound.AccessibleName = "Dispositivos sem associação WMI de driver assinado";
        const string unboundExplanation =
            "Sem linha de driver assinado associada no WMI. Não prova ausência de driver nem atualização disponível.";
        unbound.AccessibleDescription = unboundExplanation;
        var other = Filter("Outros", 5);
        other.AccessibleName = "Não confirmados";
        const string uncertainFilterExplanation =
            "Dispositivos ausentes ou com presença indeterminada. Não significa driver obsoleto.";
        other.AccessibleDescription = uncertainFilterExplanation;
        if (readForTest is not null) root.Controls.Add(heading, 0, 2);
        else root.RowStyles[2].Height = 0;

        var list = new DarkDriverList { Dock = DockStyle.Fill, Margin = Padding.Empty };
        list.AccessibleName = "Drivers associados a dispositivos; ações sujeitas à elegibilidade";
        if (v269) list.UseV269Table();
        var sort = new V269AppSortCombo { Name = "V269DriversSort", AccessibleName = "Ordenar drivers",
            Dock = DockStyle.Fill, Margin = new Padding(0, 1, 9, 1) };
        sort.Items.AddRange(["Nome (A–Z)", "Nome (Z–A)", "Atualizações primeiro", "Ordem manual"]);
        sort.SelectedIndex = 0;
        DriverInventoryEntry[] manualOrder = [];
        Button? details = null;
        TableLayoutPanel? toolbar = null;
        V269AppSearchBox? searchFrame = null;
        Font? sortFont = null;
        var empty = new Label
        {
            Name = "DriverInventoryEmpty", Dock = DockStyle.Fill,
            AccessibleName = "Estado da lista de drivers",
            Text = v269 ? instruction : "Clique em Consultar drivers para iniciar a leitura.",
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
        if (!v269) empty.BringToFront();
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
        V269PanelPresentation? verificationPanel = null;
        bool verifying = false;
        bool verificationComplete = false;
        var driverResults = new List<V269AppPreviousResult>();
        void DismissDriverResult(Guid id)
        {
            if (root.IsDisposed || driverResults.RemoveAll(entry => entry.Id == id) == 0) return;
            bool currentResult = _nav.RightPane.Current?.FlowResult is
                { Page: V269Page.Drivers, HistoryId: var current } && current == id;
            if (currentResult) _nav.PresentPanel(null);
            _nav.RightPane.RefreshAppOperationHistory();
            if (currentResult || _nav.RightPane.Current?.Driver is not null) PresentDetails(true);
        }
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
            if (!v269 || _nav.SelectedPage != V269Page.Drivers) return;
            if (verifying && (_nav.RightPane.Current?.Kind is not
                (V269PanelKind.Confirmation or V269PanelKind.Progress) ||
                ReferenceEquals(_nav.RightPane.Current, verificationPanel)))
            {
                verificationPanel = new(V269PanelKind.Progress, "Verificar drivers", message);
                _nav.PresentPanel(verificationPanel);
            }
            else PresentDetails(false);
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
        }
        SetRefreshCaption(v269 ? "Verificar" : "Consultar drivers", "Consulta dispositivos e ofertas WUA. Não modifica drivers.");

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
        Form? driverPilot = null;
        root.Disposed += (_, _) => driverPilot?.Dispose();
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
            bool actionable = current && !uninstallBusy && driverPilot is null && readForTest is null;
            list.SetActionPolicy(actionable, device => lastResult is not null
                ? DriverSilentUninstall.Evaluate(lastResult, device)
                : new DriverSilentUninstallDecision(false, "Inventário indisponível."));
            list.CanCopySummary = current;
        }
        void PresentDetails(bool explicitRequest)
        {
            if (!v269 || root.IsDisposed || _nav.SelectedPage != V269Page.Drivers) return;
            var current = _nav.RightPane.Current;
            if (current?.Kind is V269PanelKind.Confirmation or V269PanelKind.Progress or V269PanelKind.DriverReview) return;
            if (!explicitRequest && current?.Kind is (V269PanelKind.Result or V269PanelKind.History)) return;

            var selected = list.SelectedItem;
            string note = lastResult is null && state.Text.StartsWith("Nenhuma consulta", StringComparison.Ordinal)
                ? instruction : state.Text;
            if (selected is null)
            {
                if (current?.Driver is { Fields.Count: 0 } previous && previous.Reasons == note &&
                    (previous.PreviousResults ?? []).SequenceEqual(driverResults)) return;
                _nav.PresentPanel(new(V269PanelKind.Details, "Detalhes do driver", note, Driver:
                    new V269AppDetailModel("Selecione um dispositivo", "", [], note,
                        PreviousResults: driverResults.ToArray(), DismissResult: DismissDriverResult, DriverGlyph: V269DriverGlyph.Chip)));
                return;
            }

            string name = DriverHardwareCatalog.DisplayName(selected);
            string provider = string.IsNullOrWhiteSpace(selected.Provider) ? "Não informado" : selected.Provider;
            string installed = string.IsNullOrWhiteSpace(selected.Version) ? "Não informada" : selected.Version;
            string offer = list.OfferVersion(selected) ??
                (list.HasConsultativeOffer(selected) ? "WUA · versão não confirmada" : "Não confirmada");
            var fields = new (string Label, string Value)[]
            {
                ("Categoria", V269DriverVisuals.Category(selected)),
                ("Fabricante", provider),
                ("Assinatura", selected.SignatureLabel),
                ("Barramento", V269DriverVisuals.Enumerator(selected)),
                ("Versão instalada", installed),
                ("Oferta", offer)
            };
            if (lastResult is not null)
            {
                note += Environment.NewLine + history.Text;
                if (!list.CanUninstall(selected) && list.ActionsCurrent)
                    note += Environment.NewLine + list.UninstallReason(selected);
            }
            if (current?.Driver is { } prior && prior.Name == name && prior.Publisher == provider &&
                prior.Reasons == note && prior.Fields.SequenceEqual(fields) &&
                (prior.PreviousResults ?? []).SequenceEqual(driverResults)) return;
            _nav.PresentPanel(new(V269PanelKind.Details, "Detalhes do driver", note, Driver:
                new V269AppDetailModel(name, provider, fields, note,
                    PreviousResults: driverResults.ToArray(), DismissResult: DismissDriverResult,
                    DriverGlyph: V269DriverVisuals.Glyph(selected))));
        }
        void PublishDriverResult(string title, string description, bool complete)
        {
            var result = new V269AppPreviousResult(Guid.NewGuid(), title, description, complete);
            driverResults.Insert(0, result);
            if (driverResults.Count > 4) driverResults.RemoveAt(4);
            _nav.PresentPanel(new(V269PanelKind.Result, result.Title, result.Description,
                FlowResult: new(V269Page.Drivers, complete, () => DismissDriverResult(result.Id), result.Id)));
        }
        if (v269)
        {
            _nav.RightPane.ConfigureDriverHistory(() => driverResults, DismissDriverResult);
            _presentV269DriverDetails = PresentDetails;
            root.Disposed += (_, _) => _presentV269DriverDetails = null;
        }
        list.SelectionChanged += (_, _) =>
        {
            SyncInvestigationDescription();
            SyncDriverActions();
            invalidateOpenReport?.Invoke();
            PresentDetails(false);
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
        void CopySummary()
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
            if (v269) SetDriverStatus(actionHint.Text);
        }
        copySummary.Click += (_, _) => CopySummary();
        list.CopySummaryRequested += CopySummary;
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
            if (root.IsDisposed || _busy || _v269Confirming || _betaClose.IsClosing || uninstallBusy ||
                driverPilot is not null || readForTest is not null ||
                publishedSnapshotGeneration != inventoryGeneration ||
                officialGeneration != inventoryGeneration ||
                lastResult is not { } inventory ||
                latestOfficial is not { Partial: false } official ||
                !eligibleUpdateIds.Contains(requested.DeviceId) ||
                !inventory.Items.Any(item => ReferenceEquals(item, requested)))
                return;
            // A elegibilidade da interface não substitui a nova prova no clique.
            var matches = official.Matches.Where(m =>
                m.Kind == DriverOfficialMatchKind.PrimaryHardwareId &&
                string.Equals(m.DeviceId, requested.DeviceId,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1 ||
                !DriverUpdatePilot.TryPrepare(inventory, official, matches[0],
                    out var choice) || choice is null ||
                !DriverG32RecoveryTicket.CanOffer(inventory, requested, choice))
                return;
            long generation = inventoryGeneration;
            string title = "Atualizar " + DriverHardwareCatalog.DisplayName(requested);
            bool ownsOperation = false;
            bool CurrentOffer() => !root.IsDisposed && !uninstallBusy &&
                (ownsOperation || (_nav.SelectedPage == V269Page.Drivers && !_busy)) &&
                !_betaClose.IsClosing &&
                generation == inventoryGeneration &&
                publishedSnapshotGeneration == generation &&
                ReferenceEquals(lastResult, inventory) &&
                ReferenceEquals(latestOfficial, official) &&
                ReferenceEquals(list.SelectedItem, requested);
            V269PanelPresentation? progress = null;
            void ReportProgress(string message)
            {
                if (root.IsDisposed || progress is null || !ReferenceEquals(_nav.RightPane.Current, progress)) return;
                progress = progress with { Description = message };
                _nav.PresentPanel(progress);
            }
            var hooks = new DriverPilotPaneHooks(
                async (heading, question) =>
                {
                    bool accepted = await ConfirmV269Async(heading, question);
                    if (_nav.SelectedPage == V269Page.Drivers) return accepted;
                    driverPilot?.Close();
                    return false;
                },
                cancel =>
                {
                    if (_busy || _v269Confirming || !CurrentOffer()) return false;
                    ownsOperation = true;
                    _busy = true;
                    _programList.ActionsEnabled = false;
                    progress = new(V269PanelKind.Progress, title, "Preparando atualização WUA.", CancelFlow: cancel);
                    _nav.PresentPanel(progress);
                    return true;
                },
                ReportProgress,
                (message, verified) =>
                {
                    ownsOperation = false;
                    _busy = false;
                    if (!IsDisposed && !Disposing)
                    {
                        _programList.ActionsEnabled = true;
                        if (!root.IsDisposed && ReferenceEquals(_nav.RightPane.Current, progress))
                            PublishDriverResult(title, message, verified);
                        driverPilot?.Close();
                        ExitAfterCurrentOperation();
                    }
                });
            var review = DriverUpdatePilotDialog.Create(inventory, official, requested, CurrentOffer,
                DriverG31Wiring.NewProductionBackend(), DriverG31Wiring.NewProductionJournalFactory(), hooks);
            driverPilot = review;
            review.Disposed += (_, _) =>
            {
                if (!ReferenceEquals(driverPilot, review)) return;
                driverPilot = null;
                if (IsDisposed || Disposing || root.IsDisposed) return;
                refresh.Enabled = !uninstallBusy;
                SyncDriverActions();
                if (ReferenceEquals(_nav.RightPane.Current?.DriverReview, review))
                {
                    _nav.PresentPanel(null);
                    PresentDetails(false);
                }
            };
            refresh.Enabled = false;
            SyncDriverActions();
            _nav.PresentPanel(new(V269PanelKind.DriverReview, title, "", DriverReview: review));
        }

        // Um handler por operação. O clique virtual entrega o mesmo objeto que
        // foi selecionado; nenhuma ação usa índice antigo nem nome do dispositivo.
        async Task RunDriverUninstall(DriverInventoryEntry requested)
        {
            if (_busy || _v269Confirming || uninstallBusy || readForTest is not null ||
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
                if (await ConfirmV269Async("Abrir Gerenciador de Dispositivos", explanation))
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
                if (!await ConfirmV269Async("Solicitar autorização para remoção OEM",
                    $"Dispositivo: {selected.DeviceName}\r\nPacote: {selected.InfName}\r\n" +
                    "O trabalhador elevado verificará o dispositivo e pedirá confirmação antes de exportar o pacote e removê-lo.") ||
                    publishedSnapshotGeneration != inventoryGeneration || !ReferenceEquals(list.SelectedItem, selected))
                    return;
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
                    if (!root.IsDisposed)
                    {
                        SetDriverStatus(elevation.Message);
                        _nav.PresentPanel(new(V269PanelKind.Result, "Solicitação administrativa de Drivers", elevation.Message));
                    }
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
            if (!await ConfirmV269Async("Confirmar remoção OEM", question) ||
                publishedSnapshotGeneration != inventoryGeneration || !ReferenceEquals(list.SelectedItem, selected)) return;
            uninstallBusy = true;
            bool refreshAfter = false;
            refresh.Enabled = false;
            SyncDriverActions();
            SetDriverStatus("Preparando backup e revalidação do driver...");
            _nav.PresentPanel(new(V269PanelKind.Progress, "Remoção OEM", "Preparando backup e revalidação do driver."));
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
                PublishDriverResult("Remoção OEM · " + outcome.State,
                    outcome.Message + (outcome.BackupPath is null ? "" :
                        Environment.NewLine + Environment.NewLine + "Backup: " + outcome.BackupPath),
                    DriverResidualReviewPolicy.MayInspect(outcome.State));
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
            if (!ReferenceEquals(list.SelectedItem, item) || uninstallBusy || driverPilot is not null ||
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
        void Render(bool resetScroll = true)
        {
            if (root.IsDisposed) return;
            string query = search.Text.Trim();
            using var measurement = LocalDiagnosticReport.Session.MeasureUi(
                query.Length == 0 ? DiagnosticUiStage.DriverListRender : DiagnosticUiStage.DriverSearch);
            var visible = DriverListFilters.Apply(
                snapshot ?? Array.Empty<DriverInventoryEntry>(), activeFilter, query,
                eligibleUpdateIds);
            if (v269)
            {
                var byName = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), true);
                visible = sort.SelectedIndex switch
                {
                    1 => visible.OrderByDescending(DriverHardwareCatalog.DisplayName, byName).ToArray(),
                    2 => visible.OrderByDescending(item => eligibleUpdateIds.Contains(item.DeviceId))
                        .ThenBy(DriverHardwareCatalog.DisplayName, byName).ToArray(),
                    3 => DriverListFilters.SortManual(visible, manualOrder),
                    _ => visible.OrderBy(DriverHardwareCatalog.DisplayName, byName).ToArray()
                };
            }
            list.SetItems(visible);
            if (officialGeneration == inventoryGeneration &&
                latestOfficial is { Partial: false } cached)
            {
                list.SetOfferDevices(cached.Matches.Select(match => match.DeviceId));
                list.SetOfferVersions(cached.Matches);
            }
            if (officialGeneration == inventoryGeneration)
                list.SetEligibleUpdateDevices(eligibleUpdateIds);
            if (resetScroll) list.ScrollToTop();
            invalidateOpenReport?.Invoke();
            int count = snapshot?.Count ?? 0;
            title.Text = snapshot is null ? "Drivers · consulta não iniciada"
                : $"Drivers: {count} · Exibidos: {visible.Length}";
            title.AccessibleDescription = title.Text;
            empty.Text = snapshot is null ? (v269 ? instruction : "Clique em Consultar drivers para iniciar a leitura.")
                : query.Length > 0 ? (v269 ? "Nenhum item corresponde à pesquisa." : "Nenhum driver corresponde à pesquisa.")
                : count == 0 ? "Nenhuma associação retornada pela consulta."
                : "Nenhuma associação nesta categoria.";
            empty.Visible = !v269 && visible.Length == 0;
            if (v269) list.EmptyMessage = visible.Length == 0 ? empty.Text : "";
            empty.AccessibleDescription = empty.Text;
            SyncInvestigationDescription();
            SyncDriverActions();
            if (empty.Visible) empty.BringToFront();
            if (v269)
            {
                PresentDetails(false);
                return;
            }
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
        sort.SelectedIndexChanged += (_, _) => Render();
        list.ReorderRequested += (from, to) =>
        {
            var ordered = list.Items.ToList();
            var moved = ordered[from];
            ordered.RemoveAt(from);
            ordered.Insert(to, moved);
            var remaining = (manualOrder.Length > 0 ? manualOrder : snapshot ?? [])
                .Except<DriverInventoryEntry>(ordered, ReferenceEqualityComparer.Instance);
            manualOrder = ordered.Concat(remaining).ToArray();
            if (sort.SelectedIndex != 3) sort.SelectedIndex = 3;
            else Render(resetScroll: false);
        };
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
        async Task RunRefresh(bool showProgress = false)
        {
            if (wuaWait is { IsCancellationRequested: false })
            {
                wuaWait.Cancel();
                SetDriverStatus("Cancelamento solicitado; pesquisa WUA pode continuar encerrando.");
                return;
            }
            if (driverPilot is not null) return;
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
            var token = driverQuery.Begin(timeoutForTest ?? DriverStoreAudit.InteractiveTimeout);
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
            list.SetOfferVersions(Array.Empty<DriverOfficialMatch>());
            list.SetEligibleUpdateDevices(Array.Empty<string>());
            SetRefreshCaption("Cancelar", "Cancelar a espera da consulta; a leitura WMI nativa pode continuar.");
            investigate.Enabled = false;
            copySummary.Enabled = false;
            SyncDriverActions();
            actionHint.Text = "Consulta em andamento. Resumo anterior revogado.";
            actionHint.AccessibleDescription = actionHint.Text;
            SyncInvestigationDescription();
            verifying = v269 && showProgress && _nav.SelectedPage == V269Page.Drivers;
            verificationComplete = false;
            SetDriverStatus("Consultando drivers em segundo plano…");
            try
            {
                _driverReadTask = readForTest is null
                    ? Task.Run(() => DriverStoreAudit.CaptureForDisplay(token, LocalDiagnosticReport.Session.RecordDriverAuditPhase))
                    : readForTest(token);
                var captured = await _driverReadTask.WaitAsync(token);
                if (root.IsDisposed || !driverQuery.CanPublish(token)) return;
                // G1.22: nenhuma lista ou evidencia e publicada antes de
                // detectar alteracao durante a fronteira de congelamento.
                // Nao promete atomicidade contra escritor externo que coopere
                // zero; rejeita incoerencias observadas sem retry/timer.
                var result = DriverInventorySnapshot.FreezeStable(captured, token);
                verificationComplete = !result.Partial && result.Items.Count == 0;
                var historyInventory = result;
                long publishedGeneration = inventoryGeneration;
                publishedSnapshotGeneration = publishedGeneration;
                snapshot = result.Items;
                manualOrder = [];
                lastResult = result;
                investigate.Enabled = true;
                copySummary.Enabled = true;
                resultPublished = true;
                Render();
                string pnpSummary = $"PnP: {result.Items.Count(x => x.HasPnpProblem)} · " +
                    $"Sem vínculo WMI: {result.Items.Count(x => x.WithoutSignedDriverAssociation)} · " +
                    $"Não confirmados: {result.Items.Count(x => x.Present != true)}. Só consulta.";
                string gpuSummary = GraphicsDriverInventory.CompactSummary(result);
                actionHint.Text = gpuSummary.Length > 0 ? gpuSummary : pnpSummary;
                actionHint.AccessibleDescription = gpuSummary.Length > 0
                    ? gpuSummary + ". " + pnpSummary + ". Modelo/canal em Detalhes; sem instalação de Display."
                    : pnpSummary;
                // A lista agora representa OUTRA fotografia. O rodapé não deve
                // anunciar a comparação da fotografia anterior enquanto a
                // gravação desta ainda ocorre. Fixtures não gravam histórico.
                history.Text = saveHistory
                    ? "Histórico local: comparação desta consulta em andamento; resultado ainda não confirmado."
                    : "Histórico sintético desta consulta: não gravado nem comparado com o histórico local.";
                history.AccessibleDescription = history.Text;
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
                    if (root.IsDisposed || publishedGeneration != inventoryGeneration) return;
                    token.ThrowIfCancellationRequested();
                    if (!driverQuery.CanPublish(token)) return;
                    var recorded = await Task.Run(() => DriverHistory.Record(
                        readForTest is null ? DriverHistory.DefaultPath : historyTestPath!,
                        historyInventory, queryStartedAt, token, beforeHistoryReplace));
                    token.ThrowIfCancellationRequested();
                    if (!root.IsDisposed && publishedGeneration == inventoryGeneration &&
                        driverQuery.CanPublish(token))
                    {
                        history.Text = recorded.Message;
                        history.AccessibleDescription = recorded.Message + Environment.NewLine + recorded.Timeline;
                        SetDriverStatus(state.Text);
                        FitVerticalSpace();
                    }
                    DiagnosticTrace.Write($"DRIVERS.HISTORICO salvo={recorded.Saved} base={recorded.Baseline} novos={recorded.Added} nao_observados={recorded.Missing} alterados={recorded.Changed}");
                }
                // O único botão Consultar inclui agora uma pesquisa WUA
                // SOMENTE LEITURA. Não consulta o mesmo catálogo em cada cartão.
                // Fixtures seguem sem COM, sem rede e sem alteração de drivers.
                if (readForTest is null && !result.Partial && result.Items.Count > 0 &&
                    !root.IsDisposed && publishedGeneration == inventoryGeneration && driverQuery.CanPublish(token))
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
                                verificationComplete = true;
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
                                if (v269 && eligibleUpdateIds.Count > 0 && sort.SelectedIndex != 2)
                                    sort.SelectedIndex = 2;
                                else Render(resetScroll: false);
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
                // WaitAsync encerra a espera, não a chamada nativa. Observar
                // eventual falha tardia sem tocar na lista nem liberar a trava.
                if (_driverReadTask is { } pending)
                    _ = pending.ContinueWith(faulted =>
                        DiagnosticTrace.Failure("DRIVERS.INVENTARIO_FALHA", faulted.Exception!.GetBaseException()),
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                if (!root.IsDisposed)
                {
                    if (resultPublished)
                    {
                        history.Text = "Histórico local: finalização interrompida; resultado da gravação não confirmado nesta consulta.";
                        history.AccessibleDescription = history.Text;
                        SetDriverStatus("Inventário concluído e preservado. Finalização do histórico interrompida; nenhuma alteração de drivers.");
                    }
                    else SetDriverStatus(refresh.Text == "Cancelando..."
                        ? "Consulta cancelada. Lista anterior preservada; sem alterações."
                        : "Tempo esgotado. Lista anterior preservada; sem alterações.");
                }
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
                verifying = false;
                if (!refresh.IsDisposed)
                {
                    SetRefreshCaption(v269 ? "Verificar" : "Consultar drivers", "Consulta dispositivos e ofertas WUA. Não modifica drivers.");
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
                    if (v269 && verificationPanel is not null)
                    {
                        if (ReferenceEquals(_nav.RightPane.Current, verificationPanel))
                        {
                            PublishDriverResult("Verificar drivers", state.Text, verificationComplete);
                        }
                        verificationPanel = null;
                    }
                }
            }
        }
        refresh.Click += async (_, _) => await RunRefresh(showProgress: true);
        // Apenas fixture: usa o mesmo handler, com provedor local sintético.
        onTestReadActionReady?.Invoke(() => _ = RunRefresh());
        // Em resolucoes menores, deixar a area da lista prevalecer.
        // O rodape detalhado permanece acessivel pela descricao/tooltip.
        void FitVerticalSpace()
        {
            if (root.IsDisposed) return;
            if (v269)
            {
                FitV269Toolbar();
                return;
            }
            int dpi = root.DeviceDpi;
            int Scale(int px) => DriverRowGeometry.Scale(px, dpi);
            bool tiny = root.ClientSize.Height < Scale(265);
            bool compact = root.ClientSize.Height < Scale(360);
            // Comparar largura física com dimensões físicas da fonte, e não
            // apenas com uma constante sem DPI.
            int refreshWidth = Math.Max(Scale(156),
                TextRenderer.MeasureText("Consultar drivers", refresh.Font).Width + Scale(20));
            int investigationWidth = Math.Max(Scale(108),
                TextRenderer.MeasureText("Detalhes", investigate.Font).Width + Scale(20));
            bool narrow = root.ClientSize.Width < Math.Max(Scale(760),
                refreshWidth + investigationWidth + Scale(110));
            bool compactFilters = readForTest is not null && (tiny || root.ClientSize.Width < Scale(760));
            bool showCopy = !tiny && !narrow;
            // A ação global ocupa uma coluna extra. Em janela estreita,
            // também nas fixtures com Simular, mover o filtro compacto para
            // a segunda linha em vez de deixá-lo com 0-24 px.
            bool splitActions = root.ClientSize.Width <
                refreshWidth + investigationWidth +
                (labOpen is null ? Scale(92) : Scale(230));
            // O rótulo quebrava "Pesquisar:" em duas linhas no PC FORTE
            // porque a primeira coluna era fixa em 80 px, sem medir a fonte.
            searchBar.ColumnStyles[0].Width = Math.Max(Scale(80),
                TextRenderer.MeasureText(searchLabel.Text, searchLabel.Font,
                    new Size(int.MaxValue, Scale(35)),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding |
                    TextFormatFlags.NoPrefix).Width + Scale(18));
            root.RowStyles[0].Height = tiny ? Scale(30) : Scale(35);
            root.RowStyles[1].Height = splitActions ? Scale(tiny ? 68 : 79) :
                tiny ? Scale(35) : Scale(44);
            root.RowStyles[2].Height = readForTest is null || compactFilters ? 0 : Scale(36);
            // Em SVGA, devolver altura à lista; no modo tiny o histórico
            // já fica oculto e o estado completo continua no tooltip.
            root.RowStyles[4].Height = tiny ? Scale(19) :
                compact ? Scale(41) : Scale(78);
            footer.RowStyles[0].Height = compact ? 100 : 55;
            footer.RowStyles[1].Height = compact ? 0 : 45;
            heading.Visible = readForTest is not null && !compactFilters;
            bool proportionalActions = splitActions && labOpen is null;
            foreach (var (column, width) in new[] { (0, refreshWidth), (2, investigationWidth) })
            {
                actionPanel.ColumnStyles[column].SizeType = proportionalActions ? SizeType.Percent : SizeType.Absolute;
                actionPanel.ColumnStyles[column].Width = proportionalActions ? 50f : width;
            }
            actionPanel.ColumnStyles[3].SizeType = proportionalActions ? SizeType.Absolute : SizeType.Percent;
            actionPanel.ColumnStyles[3].Width = proportionalActions ? 0 : 100;
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
            SetDriverStatus(state.Text);
        }
        static void RoundV269TableFrame(Control control, int cssRadius)
        {
            var size = control.ClientSize;
            if (size.Width <= 2 || size.Height <= 2)
            {
                control.Region = null;
                return;
            }
            int radius = Math.Min(V269AppTable.Scale(cssRadius, 100, control.DeviceDpi),
                Math.Min(size.Width, size.Height) / 2);
            using var outline = ModernButton.RoundRect(new Rectangle(Point.Empty, size), radius);
            control.Region = new Region(outline);
        }
        void FitV269Toolbar()
        {
            if (toolbar is null || searchFrame is null || details is null) return;
            int S(int pixels) => V269AppTable.Scale(pixels, _v269Appearance.InterfacePercent, root.DeviceDpi);
            int windowWidth = _nav.LayoutWindowWidth;
            bool compact = root.ClientSize.Width < S(430);
            bool showDetails = windowWidth <= 1040;
            list.ApplyTableAppearance(_v269Appearance, windowWidth);
            float size = 9f * _v269Appearance.TextPercent / 100f;
            if (sortFont is null || Math.Abs(sortFont.Size - size) > .01f)
            {
                var previous = sortFont;
                sortFont = new Font("Segoe UI", size);
                sort.Font = sortFont;
                previous?.Dispose();
            }
            sort.ApplyOptions(_v269Appearance);
            sort.DropDownHeight = sort.ItemHeight * sort.Items.Count + 4;
            toolbar.SuspendLayout();
            try
            {
                toolbar.RowStyles.Clear();
                toolbar.RowCount = compact ? 2 : 1;
                for (int i = 0; i < toolbar.RowCount; i++)
                    toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / toolbar.RowCount));
                toolbar.ColumnStyles[0].Width = S(115);
                toolbar.ColumnStyles[2].Width = compact ? 0 :
                    Math.Clamp(root.ClientSize.Width - S(showDetails ? 355 : 270), S(120), S(207));
                toolbar.ColumnStyles[3].Width = compact || !showDetails ? 0 : S(82);
                toolbar.SetCellPosition(sort, new TableLayoutPanelCellPosition(compact ? 1 : 2, compact ? 1 : 0));
                toolbar.SetCellPosition(details, new TableLayoutPanelCellPosition(compact ? 0 : 3, compact ? 1 : 0));
                details.Visible = showDetails;
                root.RowStyles[0].Height = S(compact ? 84 : 44);
                content.Margin = new Padding(0, S(10), 0, 0);
            }
            finally { toolbar.ResumeLayout(true); }
        }
        if (v269)
        {
            root.Padding = Padding.Empty;
            root.Controls.Remove(searchBar);
            root.Controls.Remove(actionPanel);
            root.Controls.Remove(footer);
            root.RowStyles[1].Height = root.RowStyles[2].Height = root.RowStyles[4].Height = 0;
            toolbar = new TableLayoutPanel { Name = "V269DriversToolbar", Dock = DockStyle.Fill,
                ColumnCount = 4, RowCount = 1, Margin = Padding.Empty, BackColor = AppServices.Background };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));

            refresh.Margin = new Padding(0, 3, 9, 3);
            search.PlaceholderText = "Pesquisar driver ou dispositivo...";
            search.AccessibleName = "Pesquisar drivers";
            searchFrame = new V269AppSearchBox(search) { Name = "V269DriversSearchFrame" };
            details = new V269AppToolbarButton("Detalhes") { Name = "V269DriversDetails", Margin = new Padding(3, 3, 0, 3) };
            details.Click += (_, _) =>
            {
                PresentDetails(true);
                _nav.ShowV269AppPane();
            };
            toolbar.Controls.Add(refresh, 0, 0);
            toolbar.Controls.Add(searchFrame, 1, 0);
            toolbar.Controls.Add(sort, 2, 0);
            toolbar.Controls.Add(details, 3, 0);
            root.Controls.Add(toolbar, 0, 0);
            root.Tag = (Action)FitV269Toolbar;
            content.Name = "V269DriversTableFrame";
            content.Padding = new Padding(1);
            content.SizeChanged += (_, _) => RoundV269TableFrame(content, 10);
            content.DpiChangedAfterParent += (_, _) => RoundV269TableFrame(content, 10);
            list.SizeChanged += (_, _) => RoundV269TableFrame(list, 9);
            list.DpiChangedAfterParent += (_, _) => RoundV269TableFrame(list, 9);
            RoundV269TableFrame(content, 10);
            RoundV269TableFrame(list, 9);
            root.Disposed += (_, _) =>
            {
                searchBar.Dispose();
                actionPanel.Dispose();
                footer.Dispose();
                heading.Dispose();
                compactFilter.Dispose();
                sortFont?.Dispose();
            };
        }
        else sort.Dispose();
        root.SizeChanged += (_, _) => FitVerticalSpace();
        root.DpiChangedAfterParent += (_, _) => FitVerticalSpace();
        Render();
        FitVerticalSpace();
        return root;
    }
}
