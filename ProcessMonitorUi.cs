using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed class FlickerFreeProcessGrid : DataGridView
{
    public FlickerFreeProcessGrid()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
}

internal sealed partial class MainForm
{
    private FlickerFreeProcessGrid? _processGrid;
    private Panel? _processMonitorView;
    private TextBox? _processFilter;
    private ProcessMonitorSnapshot? _lastMonitorSnapshot;
    private int _processSortColumn = 2;
    private bool _processSortDescending = true;
    private int[] _visibleProcessIds = Array.Empty<int>();

    private Control BuildProcessMonitorView()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PanelBackground,
            Margin = Padding.Empty,
            Padding = new Padding(2, 1, 2, 2)
        };
        _processMonitorView = panel;
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 37, RowCount = 1, ColumnCount = 3,
            Margin = Padding.Empty, Padding = new Padding(2, 2, 1, 5)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 166));
        toolbar.Controls.Add(new Label
        {
            Text = "Filtrar:", ForeColor = Muted, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _processFilter = new TextBox
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(19, 26, 34), ForeColor = Ink,
            PlaceholderText = "Nome ou PID", Margin = new Padding(0, 4, 8, 3)
        };
        _processFilter.TextChanged += (_, _) => RefreshProcessTable();
        toolbar.Controls.Add(_processFilter, 1, 0);
        var native = CreateSecondaryButton("Tarefas do Windows");
        native.Dock = DockStyle.Fill;
        native.Margin = new Padding(1, 2, 0, 2);
        native.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ReportError(ex);
            }
        };
        _toolTips.SetToolTip(native,
            "Abre o Gerenciador de Tarefas original do Windows para consultar recursos avançados, inclusive rede por aplicativo.");
        toolbar.Controls.Add(native, 2, 0);
        _actionControls.Add(native);

        var grid = new FlickerFreeProcessGrid
        {
            Dock = DockStyle.Fill,
            BackgroundColor = PanelBackground,
            GridColor = Color.FromArgb(53, 67, 82),
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 33,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            RowHeadersVisible = false,
            RowTemplate = { Height = 28 },
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToOrderColumns = false,
            MultiSelect = false,
            ReadOnly = true,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ScrollBars = ScrollBars.Vertical,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            Font = new Font("Segoe UI", 9F),
            DefaultCellStyle =
            {
                BackColor = Color.FromArgb(38, 50, 63),
                ForeColor = Ink,
                SelectionBackColor = Color.FromArgb(49, 77, 103),
                SelectionForeColor = Ink,
                Padding = new Padding(4, 1, 4, 1)
            },
            ColumnHeadersDefaultCellStyle =
            {
                BackColor = Color.FromArgb(29, 40, 51),
                ForeColor = Ink,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            }
        };
        // E/S sao bytes transferidos por processos; nao equivalem
        // necessariamente a atividade do disco fisico ou da rede.
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ProcessName", HeaderText = "Processo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 100, FillWeight = 100, SortMode = DataGridViewColumnSortMode.Programmatic
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "PID", HeaderText = "PID", Width = 76,
            SortMode = DataGridViewColumnSortMode.Programmatic
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "CPU", HeaderText = "CPU %", Width = 79,
            SortMode = DataGridViewColumnSortMode.Programmatic
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Memory", HeaderText = "RAM (MB)", Width = 100,
            SortMode = DataGridViewColumnSortMode.Programmatic
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "IO", HeaderText = "E/S (KB/s)", Width = 107,
            SortMode = DataGridViewColumnSortMode.Programmatic
        });
        foreach (int column in new[] { 1, 2, 3, 4 })
            grid.Columns[column].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.ColumnHeaderMouseClick += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            if (_processSortColumn == e.ColumnIndex)
                _processSortDescending = !_processSortDescending;
            else
            {
                _processSortColumn = e.ColumnIndex;
                _processSortDescending = e.ColumnIndex != 0 && e.ColumnIndex != 1;
            }
            RefreshProcessTable();
        };
        grid.ShowCellToolTips = true;
        grid.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 4)
                e.ToolTipText = "E/S: leitura e escrita de dados do processo. Inclui operações que não chegam ao disco físico.";
        };
        _processGrid = grid;
        panel.Controls.Add(grid);
        panel.Controls.Add(toolbar);
        return panel;
    }

    // Permite exercitar o monitor no runner Windows sem iniciar servicos
    // nem depender do consumo real do PC do usuario.
    internal void PreviewProcessSnapshotForTests(ProcessMonitorSnapshot snapshot) =>
        ShowProcessMonitorSnapshot(snapshot);

    private void ShowProcessMonitorSnapshot(ProcessMonitorSnapshot snapshot)
    {
        _lastMonitorSnapshot = snapshot;
        if (_systemPanelCards is null || IsDisposed) return;
        _systemPanelTitle.Text = "Processos · monitoramento em tempo real";
        static string Percent(double? value) =>
            value.HasValue ? value.Value.ToString("N1", CultureInfo.CurrentCulture) + "%" : "calculando";
        string totalCpu = Percent(snapshot.TotalCpuPercent);
        string memory = snapshot.UsedMemoryPercent?.ToString(CultureInfo.CurrentCulture) + "%"
            ?? "indisponível";
        string network = snapshot.NetworkBytesPerSecond.HasValue
            ? (snapshot.NetworkBytesPerSecond.Value / 1024d).ToString("N1", CultureInfo.CurrentCulture) + " KB/s"
            : "calculando";
        _systemPanelSummary.Text =
            $"{snapshot.ProcessCount} processos · CPU total: {totalCpu} · Memória em uso: {memory} · Rede total: {network}. E/S = leitura + escrita, não necessariamente disco físico.";
        RefreshProcessTable();
    }

    private void RefreshProcessTable()
    {
        if (_processGrid is null || _processGrid.IsDisposed || _lastMonitorSnapshot is null) return;
        var grid = _processGrid;
        string filter = _processFilter?.Text.Trim() ?? "";
        IEnumerable<ProcessMonitorEntry> filtered = _lastMonitorSnapshot.Processes;
        if (filter.Length > 0)
            filtered = filtered.Where(row =>
                row.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                row.Pid.ToString(CultureInfo.InvariantCulture).Contains(filter, StringComparison.Ordinal));
        Func<ProcessMonitorEntry, IComparable> sortKey = _processSortColumn switch
        {
            0 => r => r.Name,
            1 => r => r.Pid,
            3 => r => r.MemoryBytes,
            4 => r => r.IoBytesPerSecond ?? -1,
            _ => r => r.CpuPercent ?? -1
        };
        var rows = (_processSortDescending
                ? filtered.OrderByDescending(sortKey)
                : filtered.OrderBy(sortKey))
            .ThenBy(r => r.Pid).ToArray();
        int[] pids = rows.Select(r => r.Pid).ToArray();
        int first = -1;
        try { first = grid.FirstDisplayedScrollingRowIndex; }
        catch (InvalidOperationException) { }
        int? selected = grid.SelectedRows.Count > 0
            ? grid.SelectedRows[0].Tag as int? : null;

        grid.SuspendLayout();
        try
        {
            // Se a ordem dos processos nao mudou, atualizar SOMENTE os
            // valores alterados; assim o texto nao pisca a cada amostra.
            if (!_visibleProcessIds.SequenceEqual(pids) || grid.Rows.Count != rows.Length)
            {
                grid.Rows.Clear();
                foreach (var row in rows)
                {
                    int index = grid.Rows.Add(row.Name, row.Pid, "", "", "");
                    grid.Rows[index].Tag = row.Pid;
                }
                _visibleProcessIds = pids;
            }
            for (int i = 0; i < rows.Length; i++)
            {
                ProcessMonitorEntry row = rows[i];
                string[] values =
                {
                    row.Name,
                    row.Pid.ToString(CultureInfo.CurrentCulture),
                    row.CpuPercent?.ToString("N1", CultureInfo.CurrentCulture) ?? "—",
                    (row.MemoryBytes / 1048576d).ToString("N0", CultureInfo.CurrentCulture),
                    row.IoBytesPerSecond.HasValue
                        ? (row.IoBytesPerSecond.Value / 1024d).ToString("N1", CultureInfo.CurrentCulture)
                        : "—"
                };
                for (int column = 0; column < values.Length; column++)
                    if (!Equals(grid.Rows[i].Cells[column].Value, values[column]))
                        grid.Rows[i].Cells[column].Value = values[column];
            }
            grid.ClearSelection();
            if (selected.HasValue)
            {
                int index = Array.IndexOf(pids, selected.Value);
                if (index >= 0) grid.Rows[index].Selected = true;
            }
            if (first >= 0 && first < grid.RowCount)
            {
                try { grid.FirstDisplayedScrollingRowIndex = first; }
                catch (InvalidOperationException) { }
            }
            grid.Columns[_processSortColumn].HeaderCell.SortGlyphDirection =
                _processSortDescending ? SortOrder.Descending : SortOrder.Ascending;
        }
        finally { grid.ResumeLayout(); }
    }
}
