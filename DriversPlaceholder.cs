using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private Task<DriverInventoryResult>? _driverReadTask;

    private Control BuildDriversPlaceholder()
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
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = new Padding(0, 2, 0, 2)
        };
        actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var refresh = CreateButton("Consultar drivers", AppServices.Blue, Color.White);
        refresh.Name = "DriverInventoryRefresh";
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
            { "Todos", "Presentes", "Não confirmados" });
        compactFilter.SelectedIndex = 0;
        _toolTips.SetToolTip(compactFilter,
            "Filtro apenas visual; sem atualização ou remoção de drivers.");
        actionInfo.Controls.Add(actionHint);
        actionInfo.Controls.Add(compactFilter);
        actionPanel.Controls.Add(actionInfo, 1, 0);
        root.Controls.Add(actionPanel, 0, 1);

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
            Margin = Padding.Empty
        };
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 73));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 147));
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
        var other = Filter("Não confirmados", 3);
        _toolTips.SetToolTip(other,
            "Inclui dispositivos não presentes e os de presença indeterminada. Não significa driver obsoleto.");
        root.Controls.Add(heading, 0, 2);

        var list = new DarkDriverList { Dock = DockStyle.Fill, Margin = Padding.Empty };
        list.AccessibleName = "Drivers associados a dispositivos, somente leitura";
        var empty = new Label
        {
            Name = "DriverInventoryEmpty", Dock = DockStyle.Fill,
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
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
            Text = "Nenhuma consulta iniciada. Não é o inventário completo do Driver Store."
        };
        var history = new Label
        {
            Name = "DriverHistoryStatus", Dock = DockStyle.Fill, ForeColor = Muted,
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
            Text = "Histórico próprio: comparações locais a partir da primeira consulta."
        };
        footer.Controls.Add(state, 0, 0);
        footer.Controls.Add(history, 0, 1);
        root.Controls.Add(footer, 0, 4);

        IReadOnlyList<DriverInventoryEntry>? snapshot = null;
        string activeFilter = "Todos";
        void Render()
        {
            if (root.IsDisposed) return;
            string query = search.Text.Trim();
            var visible = DriverListFilters.Apply(
                snapshot ?? Array.Empty<DriverInventoryEntry>(), activeFilter, query);
            list.SetItems(visible);
            list.ScrollToTop();
            int count = snapshot?.Count ?? 0;
            title.Text = snapshot is null ? "Drivers · consulta não iniciada"
                : $"Drivers: {count} · Exibidos: {visible.Length}";
            _toolTips.SetToolTip(title,
                "Associações de drivers a dispositivos, não pacotes exclusivos do Driver Store.");
            empty.Text = snapshot is null ? "Clique em Consultar drivers para iniciar a leitura."
                : query.Length > 0 ? "Nenhum driver corresponde à pesquisa."
                : count == 0 ? "Nenhuma associação retornada pela consulta."
                : "Nenhuma associação nesta categoria.";
            empty.Visible = visible.Length == 0;
            if (empty.Visible) empty.BringToFront();
            foreach (var (button, name) in new[]
                { (all, "Todos"), (present, "Presentes"), (other, "Não confirmados") })
            {
                bool selected = name == activeFilter;
                button.BackColor = selected ? Color.FromArgb(43, 66, 89)
                    : Color.FromArgb(43, 56, 70);
                button.ForeColor = selected ? Accent : Ink;
            }
        }

        search.TextChanged += (_, _) => Render();
        all.Click += (_, _) => { activeFilter = "Todos"; Render(); };
        present.Click += (_, _) => { activeFilter = "Presentes"; Render(); };
        other.Click += (_, _) => { activeFilter = "Não confirmados"; Render(); };
        compactFilter.SelectedIndexChanged += (_, _) =>
        {
            if (compactFilter.SelectedItem is string selected)
            {
                activeFilter = selected;
                Render();
            }
        };
        refresh.Click += async (_, _) =>
        {
            if (_driverReadTask is { IsCompleted: false })
            {
                state.Text = "A consulta anterior ainda está em andamento.";
                return;
            }
            refresh.Enabled = false;
            state.Text = "Consultando drivers em segundo plano…";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var token = timeout.Token;
            _driverReadTask = Task.Run(() => DriverInventory.Capture(token));
            try
            {
                var result = await _driverReadTask.WaitAsync(token);
                if (root.IsDisposed) return;
                snapshot = result.Items;
                Render();
                var links = DriverPackageLinks.Analyze(result);
                state.Text = $"{result.Status}: {result.Items.Count} associações, " +
                    $"{links.Packages.Count} INFs, {links.WithoutInf} sem INF válido. Sem remoções.";
                _toolTips.SetToolTip(state,
                    state.Text + " Ausência não autoriza remoção; assinatura informada pelo Windows.");
                DiagnosticTrace.Write($"DRIVERS.VINCULOS infs={links.Packages.Count} compartilhados={links.Packages.Count(p => p.Devices > 1)} versoes_divergentes={links.Packages.Count(p => p.Versions > 1)} assinaturas_conflitantes={links.Packages.Count(p => p.ConflictingSignatures)} sem_inf={links.WithoutInf}");
                DiagnosticTrace.Write($"DRIVERS.INVENTARIO quantidade={result.Items.Count} parcial={result.Partial}");
                var recorded = await Task.Run(() => DriverHistory.Record(
                    DriverHistory.DefaultPath, result, DateTimeOffset.UtcNow));
                if (!root.IsDisposed)
                {
                    history.Text = recorded.Message;
                    history.AccessibleDescription = recorded.Message + Environment.NewLine + recorded.Timeline;
                    _toolTips.SetToolTip(history,
                        recorded.Timeline.Length == 0 ? recorded.Message : recorded.Timeline);
                    FitVerticalSpace();
                }
                DiagnosticTrace.Write($"DRIVERS.HISTORICO salvo={recorded.Saved} base={recorded.Baseline} novos={recorded.Added} nao_observados={recorded.Missing} alterados={recorded.Changed}");
            }
            catch (OperationCanceledException)
            {
                if (!root.IsDisposed) state.Text =
                    "Tempo esgotado. Leitura anterior preservada. Nenhuma alteração foi feita.";
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Failure("DRIVERS.INVENTARIO_FALHA", ex);
                if (!root.IsDisposed) state.Text =
                    "Consulta indisponível. Nenhuma alteração foi feita.";
            }
            finally { if (!refresh.IsDisposed) refresh.Enabled = true; }
        };
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
            state.AccessibleDescription = state.Text + Environment.NewLine +
                history.Text + Environment.NewLine + history.AccessibleDescription;
            if (compact) _toolTips.SetToolTip(state, state.AccessibleDescription);
        }
        root.SizeChanged += (_, _) => FitVerticalSpace();
        root.DpiChangedAfterParent += (_, _) => FitVerticalSpace();
        Render();
        FitVerticalSpace();
        return root;
    }
}
