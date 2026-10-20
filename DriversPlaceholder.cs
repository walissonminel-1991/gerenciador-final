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
            Name = "DriversPlaceholder", Dock = DockStyle.Fill, BackColor = AppServices.Background,
            ColumnCount = 1, RowCount = 4, Padding = new Padding(10)
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        var intro = new Label { Dock = DockStyle.Fill, ForeColor = Ink,
            Text = "Drivers associados a dispositivos: fabricante, versão, INF e presença. Consulta local, somente quando solicitada." };
        var refresh = new Button { Name = "DriverInventoryRefresh", Text = "Consultar drivers", Dock = DockStyle.Left,
            Width = 180, FlatStyle = FlatStyle.Flat, ForeColor = Ink, BackColor = PanelBackground };
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
        host.Controls.Add(intro, 0, 0); host.Controls.Add(refresh, 0, 1);
        host.Controls.Add(grid, 0, 2); host.Controls.Add(state, 0, 3);
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
                state.Text = $"{result.Status}: {result.Items.Count} associações. Ausência não autoriza remoção; assinatura informada pelo Windows não substitui validação do pacote.";
                DiagnosticTrace.Write($"DRIVERS.INVENTARIO quantidade={result.Items.Count} parcial={result.Partial}");
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
        return host;
    }
}
