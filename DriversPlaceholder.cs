using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private Task<DriverInventoryResult>? _driverReadTask;

    private Control BuildDriversPlaceholder()
    {
        var host = new TableLayoutPanel
        {
            Name = "DriversPlaceholder", Dock = DockStyle.Top, BackColor = AppServices.Background,
            ColumnCount = 1, RowCount = 5, Padding = new Padding(10)
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        var intro = new Label { Dock = DockStyle.Fill, ForeColor = Ink,
            Text = "Drivers associados a dispositivos: fabricante, versão, INF e presença. Consulta local, somente quando solicitada." };
        var refresh = new Button { Name = "DriverInventoryRefresh", Text = "Consultar drivers", Dock = DockStyle.Left,
            Width = 180, FlatStyle = FlatStyle.Flat, ForeColor = Ink, BackColor = PanelBackground };
        var grid = new FlickerFreeProcessGrid
        {
            Name = "DriverInventoryGrid", Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            BackgroundColor = AppServices.Background, BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            // Ajusta todas as colunas a largura disponivel. Em janelas estreitas
            // os MinimumWidth preservam os textos e permitem rolagem horizontal.
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = PanelBackground, ForeColor = Ink },
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = AppServices.Background, ForeColor = Ink,
                SelectionBackColor = PanelBackground, SelectionForeColor = Ink },
            RowTemplate = { Height = 26 }
        };
        foreach (var (name, title, minimum, weight) in new[] {
            ("Device", "Dispositivo", 180, 31f), ("Provider", "Fornecedor", 140, 22f),
            ("Version", "Versão", 115, 16f), ("Inf", "INF associado", 110, 14f),
            ("Presence", "Presença", 95, 12f), ("Signature", "Assinatura informada", 145, 21f) })
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = title,
                MinimumWidth = minimum, FillWeight = weight,
                SortMode = DataGridViewColumnSortMode.Automatic,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    WrapMode = DataGridViewTriState.False
                }
            });
        var state = new Label { Dock = DockStyle.Fill, ForeColor = Muted,
            Text = "Nenhuma consulta iniciada. Não é o inventário completo do Driver Store. Nesta aba: nenhuma instalação, remoção ou alteração de drivers." };
        host.Controls.Add(intro, 0, 0); host.Controls.Add(refresh, 0, 1);
        host.Controls.Add(grid, 0, 2); host.Controls.Add(state, 0, 3);
        var history = new Label { Name = "DriverHistoryStatus", Dock = DockStyle.Fill, ForeColor = Muted,
            Text = "Histórico próprio: consultas completas serão comparadas e salvas localmente, sem nomes nem IDs de hardware. Até 20 registros de mudanças." };
        host.Controls.Add(history, 0, 4);
        refresh.Click += async (_, _) =>
        {
            if (_driverReadTask is { IsCompleted: false })
            { state.Text = "A consulta anterior ainda está em andamento; não será iniciada outra em paralelo."; return; }
            refresh.Enabled = false;
            state.Text = "Consultando drivers em segundo plano…";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var token = timeout.Token;
            _driverReadTask = Task.Run(() => DriverInventory.Capture(token));
            try
            {
                var result = await _driverReadTask.WaitAsync(token);
                if (host.IsDisposed) return;
                grid.Rows.Clear();
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
                if (!host.IsDisposed) state.Text = "Tempo de consulta esgotado. O retrato anterior, se houver, foi mantido. WMI pode ainda estar finalizando; nenhuma alteração foi feita.";
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Failure("DRIVERS.INVENTARIO_FALHA", ex);
                if (!host.IsDisposed) state.Text = "Consulta indisponível. Nenhuma alteração foi feita.";
            }
            finally { if (!refresh.IsDisposed) refresh.Enabled = true; }
        };
        // Reutiliza a rolagem escura da aplicacao, com navegacao por foco e DPI.
        var viewport = ScrollablePage(host, _ => (int)Math.Ceiling(420d * Math.Max(1d, DeviceDpi / 96d)));
        viewport.Name = "DriverInventoryViewport";
        return viewport;
    }
}
