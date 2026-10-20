using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private Task<DriverInventoryResult>? _driverReadTask;
    private Task<(DriverStoreResult Packages, DriverInventoryResult Devices)>? _driverStoreTask;

    private Control BuildDriversPlaceholder()
    {
        var host = new TableLayoutPanel
        {
            Name = "DriversPlaceholder", Dock = DockStyle.Top, BackColor = AppServices.Background,
            ColumnCount = 1, RowCount = 5, Padding = new Padding(10)
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        var intro = new Label { Dock = DockStyle.Fill, ForeColor = Ink,
            Text = "Drivers associados a dispositivos: fabricante, versão, INF e presença. Consulta local, somente quando solicitada." };
        var refresh = new Button { Name = "DriverInventoryRefresh", Text = "Consultar drivers", Dock = DockStyle.Left,
            Width = 180, FlatStyle = FlatStyle.Flat, ForeColor = Ink, BackColor = PanelBackground };
        var storeMode = new CheckBox { Name = "DriverStoreMode", Text = "Pacotes do Windows (admin)",
            AutoSize = true, ForeColor = Ink, Margin = new Padding(10, 8, 0, 0) };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty };
        refresh.Dock = DockStyle.None; refresh.Height = 34;
        actions.Controls.Add(refresh); actions.Controls.Add(storeMode);
        var grid = new FlickerFreeProcessGrid
        {
            Name = "DriverInventoryGrid", Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            BackgroundColor = AppServices.Background, BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = PanelBackground, ForeColor = Ink },
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = AppServices.Background, ForeColor = Ink,
                SelectionBackColor = PanelBackground, SelectionForeColor = Ink },
            RowTemplate = { Height = 26 }
        };
        foreach (var (name, title, width) in new[] {
            ("Device", "Dispositivo", 220), ("Provider", "Fornecedor", 150),
            ("Version", "Versão", 125), ("Inf", "INF associado", 115),
            ("Presence", "Presença", 115), ("Signature", "Assinatura informada", 145) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = title, Width = width,
                SortMode = DataGridViewColumnSortMode.Automatic });
        var state = new Label { Dock = DockStyle.Fill, ForeColor = Muted,
            Text = "Nenhuma consulta iniciada. Não é o inventário completo do Driver Store. Nesta aba: nenhuma instalação, remoção ou alteração de drivers." };
        host.Controls.Add(intro, 0, 0); host.Controls.Add(actions, 0, 1);
        host.Controls.Add(grid, 0, 2); host.Controls.Add(state, 0, 3);
        var history = new Label { Name = "DriverHistoryStatus", Dock = DockStyle.Fill, ForeColor = Muted,
            Text = "Histórico próprio: consultas completas serão comparadas e salvas localmente, sem nomes nem IDs de hardware. Até 20 registros de mudanças." };
        host.Controls.Add(history, 0, 4);
        refresh.Click += async (_, _) =>
        {
            if (_driverReadTask is { IsCompleted: false } || _driverStoreTask is { IsCompleted: false })
            { state.Text = "A consulta anterior ainda está em andamento; não será iniciada outra em paralelo."; return; }
            refresh.Enabled = false; storeMode.Enabled = false;
            state.Text = "Consultando drivers em segundo plano…";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(storeMode.Checked ? 60 : 20));
            var token = timeout.Token;
            try
            {
                if (storeMode.Checked)
                {
                    _driverStoreTask = Task.Run(() =>
                    {
                        var packages = DriverStoreInventory.Capture(token);
                        var devices = packages.Items.Count > 0 ? DriverInventory.Capture(token) : new DriverInventoryResult([], true, "Não consultado");
                        return (packages, devices);
                    });
                    var captured = await _driverStoreTask.WaitAsync(token);
                    if (host.IsDisposed) return;
                    var correlated = DriverStoreInventory.Correlate(captured.Packages, captured.Devices);
                    if (captured.Packages.Items.Count > 0 || !captured.Packages.Partial)
                    {
                        string[] titles = ["INF publicado", "Fornecedor", "Versão", "Classe", "Vínculo WMI", "Proteção"];
                        grid.Rows.Clear();
                        for (int i = 0; i < titles.Length; i++) grid.Columns[i].HeaderText = titles[i];
                        foreach (var item in correlated)
                            grid.Rows.Add(item.Package.PublishedName, item.Package.Provider, item.Package.Version,
                                item.Package.ClassName, item.LinkLabel + (item.EvidencePartial ? " (parcial)" : ""),
                                item.Package.BootCritical ? "Crítico de boot" : item.Package.InBox ? "Do Windows" : "Terceiros");
                    }
                    state.Text = captured.Packages.Status + $" · {captured.Packages.Items.Count} pacotes. Sem vínculo não significa pacote dispensável.";
                    history.Text = "Esta consulta não altera o histórico de dispositivos. A proteção informada não autoriza remoção. Nenhum pacote foi instalado ou removido.";
                    _toolTips.SetToolTip(history, history.Text);
                    history.AccessibleDescription = history.Text;
                    DiagnosticTrace.Write($"DRIVERS.PACOTES quantidade={captured.Packages.Items.Count} parcial={captured.Packages.Partial} vinculos_parciais={captured.Devices.Partial}");
                    return;
                }
                _driverReadTask = Task.Run(() => DriverInventory.Capture(token));
                var result = await _driverReadTask.WaitAsync(token);
                if (host.IsDisposed) return;
                grid.Rows.Clear();
                string[] deviceTitles = ["Dispositivo", "Fornecedor", "Versão", "INF associado", "Presença", "Assinatura informada"];
                for (int i = 0; i < deviceTitles.Length; i++) grid.Columns[i].HeaderText = deviceTitles[i];
                foreach (var item in result.Items)
                    grid.Rows.Add(item.DeviceName, item.Provider, item.Version, item.InfName, item.PresenceLabel, item.SignatureLabel);
                var links = DriverPackageLinks.Analyze(result);
                state.Text = $"{result.Status}: {result.Items.Count} associações, {links.Packages.Count} INFs, {links.WithoutInf} sem INF válido. Ausência não autoriza remoção; assinatura informada pelo Windows.";
                DiagnosticTrace.Write($"DRIVERS.VINCULOS infs={links.Packages.Count} compartilhados={links.Packages.Count(p => p.Devices > 1)} versoes_divergentes={links.Packages.Count(p => p.Versions > 1)} assinaturas_conflitantes={links.Packages.Count(p => p.ConflictingSignatures)} sem_inf={links.WithoutInf}");
                DiagnosticTrace.Write($"DRIVERS.INVENTARIO quantidade={result.Items.Count} parcial={result.Partial}");
                var recorded = await Task.Run(() => DriverHistory.Record(DriverHistory.DefaultPath, result, DateTimeOffset.UtcNow));
                if (!host.IsDisposed)
                {
                    history.Text = recorded.Message;
                    history.AccessibleDescription = recorded.Message + Environment.NewLine + recorded.Timeline;
                    _toolTips.SetToolTip(history, recorded.Timeline.Length == 0 ? recorded.Message : recorded.Timeline);
                }
                DiagnosticTrace.Write($"DRIVERS.HISTORICO salvo={recorded.Saved} base={recorded.Baseline} novos={recorded.Added} nao_observados={recorded.Missing} alterados={recorded.Changed}");
            }
            catch (OperationCanceledException)
            {
                if (!host.IsDisposed) state.Text = "Tempo de consulta esgotado. O retrato anterior, se houver, foi mantido. O Windows pode ainda estar finalizando; nenhuma alteração foi feita.";
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Failure("DRIVERS.INVENTARIO_FALHA", ex);
                if (!host.IsDisposed) state.Text = "Consulta indisponível. Nenhuma alteração foi feita.";
            }
            finally { if (!refresh.IsDisposed) refresh.Enabled = true; if (!storeMode.IsDisposed) storeMode.Enabled = true; }
        };
        // Reutiliza a rolagem escura da aplicacao, com navegacao por foco e DPI.
        var viewport = ScrollablePage(host, _ => (int)Math.Ceiling(440d * Math.Max(1d, DeviceDpi / 96d)));
        viewport.Name = "DriverInventoryViewport";
        return viewport;
    }
}
