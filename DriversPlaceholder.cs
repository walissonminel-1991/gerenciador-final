using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private Task<DriverInventoryResult>? _driverReadTask;

    private Control BuildDriversPlaceholder(
        Func<CancellationToken, Task<DriverInventoryResult>>? readForTest = null,
        TimeSpan? timeoutForTest = null,
        Action<Action>? onTestReadActionReady = null,
        Action<Action<string>>? onTestFilterReady = null)
    {
        // Mesma hierarquia visual de Aplicativos: pesquisa, acoes, filtros,
        // cartoes e estado. Nada nesta tela instala, remove ou edita drivers.
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

        var actionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Margin = new Padding(0, 2, 0, 2)
        };
        actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
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
            Visible = false, Margin = Padding.Empty
        };
        compactFilter.Items.AddRange(new object[]
            { "Todos", "Presentes", "Não confirmados", "Problemas" });
        compactFilter.SelectedIndex = 0;
        _toolTips.SetToolTip(compactFilter,
            "Filtro apenas visual; sem atualização ou remoção de drivers.");
        actionInfo.Controls.Add(actionHint);
        actionInfo.Controls.Add(compactFilter);
        var investigate = CreateSecondaryButton("Investigar");
        investigate.Name = "DriverReadOnlyInvestigation";
        investigate.AccessibleName = "Investigar drivers e histórico, somente leitura";
        investigate.Dock = DockStyle.Fill;
        investigate.Margin = new Padding(0, 2, 6, 2);
        investigate.Enabled = false;
        actionPanel.Controls.Add(investigate, 1, 0);
        _toolTips.SetToolTip(investigate, "Histórico Windows, INFs publicados, consulta oficial e verificação do INF. Somente leitura.");
        actionPanel.Controls.Add(actionInfo, 2, 0);
        root.Controls.Add(actionPanel, 0, 1);

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
            Margin = Padding.Empty
        };
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 73));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 113));
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
        var other = Filter("Outros", 4);
        other.AccessibleName = "Não confirmados";
        other.AccessibleDescription =
            "Dispositivos ausentes ou com presença indeterminada. Não significa driver obsoleto.";
        _toolTips.SetToolTip(other, other.AccessibleDescription);
        root.Controls.Add(heading, 0, 2);

        var list = new DarkDriverList { Dock = DockStyle.Fill, Margin = Padding.Empty };
        list.AccessibleName = "Drivers associados a dispositivos, somente leitura";
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
        footer.Controls.Add(state, 0, 0);
        footer.Controls.Add(history, 0, 1);
        root.Controls.Add(footer, 0, 4);
        history.AccessibleDescription = history.Text;
        int statusRevision = 0;
        int refreshRevision = 0;
        void SetDriverStatus(string message)
        {
            if (state.IsDisposed) return;
            int revision = ++statusRevision;
            state.Text = message;
            // Atualizações de WinForms podem reentrar entre Text, acessibilidade
            // e ToolTip. Nunca restaurar uma descrição de geração anterior.
            if (state.IsDisposed || revision != statusRevision) return;
            var description = message + Environment.NewLine + history.Text +
                (history.AccessibleDescription == history.Text ? "" : Environment.NewLine + history.AccessibleDescription);
            state.AccessibleDescription = description;
            if (state.IsDisposed || revision != statusRevision) return;
            _toolTips.SetToolTip(state, description);
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
        Action? invalidateOpenReport = null;
        list.SelectionChanged += (_, _) => invalidateOpenReport?.Invoke();
        investigate.Click += (_, _) =>
        {
            if (lastResult is not { } captured) return;
            var selection = list.SelectedItem;
            var guard = new DriverReportPublicationGuard(captured, selection, inventoryGeneration);
            ShowDriverReadOnlyReport(captured, selection,
                () => guard.IsCurrent(lastResult, list.SelectedItem, inventoryGeneration),
                (token, result) => guard.CanPublish(lastResult, list.SelectedItem,
                    inventoryGeneration, token, result),
                (token, result) => guard.CanPublishInf(lastResult, list.SelectedItem,
                    inventoryGeneration, token, result),
                callback => invalidateOpenReport = callback);
        };
        string activeFilter = "Todos";
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
            investigate.AccessibleDescription = lastResult is null
                ? "Disponível depois de uma consulta local. Sem instalação ou atualização."
                : list.SelectedItem is null
                    ? "Abre relatório geral de drivers, sem dispositivo selecionado e sem verificação INF."
                    : "Abre o relatório do dispositivo selecionado e suas evidências consultivas.";
            if (empty.Visible) empty.BringToFront();
            foreach (var (button, name) in new[]
                { (all, "Todos"), (present, "Presentes"), (problems, "Problemas"), (other, "Não confirmados") })
            {
                bool selected = name == activeFilter;
                button.BackColor = selected ? Color.FromArgb(43, 66, 89)
                    : Color.FromArgb(43, 56, 70);
                button.ForeColor = selected ? Accent : Ink;
                button.AccessibleDescription = selected
                    ? "Filtro ativo: " + name : "Ativar filtro: " + name;
            }
            compactFilter.AccessibleDescription = "Filtro atual: " + activeFilter;
        }

        void ApplyFilter(string name)
        {
            activeFilter = name;
            Render();
        }
        search.TextChanged += (_, _) => Render();
        all.Click += (_, _) => ApplyFilter("Todos");
        present.Click += (_, _) => ApplyFilter("Presentes");
        other.Click += (_, _) => ApplyFilter("Não confirmados");
        problems.Click += (_, _) => ApplyFilter("Problemas");
        // Apenas fixture: invoca a mesma ação dos botões e do filtro compacto.
        onTestFilterReady?.Invoke(ApplyFilter);
        compactFilter.SelectedIndexChanged += (_, _) =>
        {
            if (compactFilter.SelectedItem is string selected)
            {
                ApplyFilter(selected);
            }
        };
        var driverQuery = new DriverQueryCoordinator();
        bool resultPublished = false;
        root.Disposed += (_, _) =>
        {
            // Uma página descartada invalida a fotografia mesmo que seleção e listas não tenham mudado.
            inventoryGeneration++;
            invalidateOpenReport?.Invoke();
            driverQuery.RequestCancel();
        };
        async Task RunRefresh()
        {
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
            // Invalida relatórios antigos antes da próxima leitura nativa,
            // inclusive se ela falhar, cancelar ou terminar fora de ordem.
            inventoryGeneration++;
            invalidateOpenReport?.Invoke();
            resultPublished = false;
            SetRefreshCaption("Cancelar", "Cancelar a espera da consulta; a leitura WMI nativa pode continuar.");
            investigate.Enabled = false;
            SetDriverStatus("Consultando drivers em segundo plano…");
            _driverReadTask = readForTest is null
                ? Task.Run(() => DriverStoreAudit.CaptureWithPackages(token)) : readForTest(token);
            try
            {
                var result = await _driverReadTask.WaitAsync(token);
                if (root.IsDisposed || !driverQuery.CanPublish(token)) return;
                snapshot = result.Items;
                lastResult = result;
                investigate.Enabled = true;
                resultPublished = true;
                Render();
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
                _toolTips.SetToolTip(state,
                    state.Text + " " + result.Store?.Summary + " " + result.WindowsHistory?.Summary + " " + result.PublishedPackages?.Summary);
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
                if (readForTest is null)
                {
                    var recorded = await Task.Run(() => DriverHistory.Record(
                        DriverHistory.DefaultPath, result, DateTimeOffset.UtcNow));
                    if (!root.IsDisposed)
                    {
                        history.Text = recorded.Message;
                        history.AccessibleDescription = recorded.Message + Environment.NewLine + recorded.Timeline;
                        _toolTips.SetToolTip(history,
                            recorded.Timeline.Length == 0 ? recorded.Message : recorded.Timeline);
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
                    investigate.Enabled = lastResult is not null;
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
            bool tiny = root.ClientSize.Height < 230;
            bool compact = root.ClientSize.Height < 360;
            root.RowStyles[0].Height = tiny ? 28 : compact ? 30 : 35;
            root.RowStyles[1].Height = tiny ? 31 : compact ? 36 : 44;
            root.RowStyles[2].Height = tiny ? 0 : compact ? 29 : 33;
            root.RowStyles[4].Height = tiny ? 22 : compact ? 31 : 67;
            footer.RowStyles[0].Height = compact ? 100 : 55;
            footer.RowStyles[1].Height = compact ? 0 : 45;
            heading.Visible = !tiny;
            actionHint.Visible = !tiny;
            compactFilter.Visible = tiny;
            if (tiny && compactFilter.SelectedItem is string current && current != activeFilter)
                compactFilter.SelectedItem = activeFilter;
            history.Visible = !compact;
            SetDriverStatus(state.Text);
        }
        root.SizeChanged += (_, _) => FitVerticalSpace();
        root.DpiChangedAfterParent += (_, _) => FitVerticalSpace();
        Render();
        FitVerticalSpace();
        return root;
    }
}
