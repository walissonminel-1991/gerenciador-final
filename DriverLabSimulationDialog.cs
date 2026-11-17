using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Nenhuma dependência de inventário, WUA, PnP, backend, journal ou rede.
internal static class DriverLabSimulationDialog
{
    private sealed record Item(DriverLabCase Case)
    {
        public override string ToString() => Case.Name;
    }
    internal static Form Create()
    {
        var form = new Form
        {
            Name = "DriverLabSimulationForm",
            Text = "Drivers — laboratório inteiramente fictício",
            ClientSize = new Size(720, 530), MinimumSize = new Size(440, 330),
            StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            Font = new Font("Segoe UI", 9F), BackColor = Color.FromArgb(26,31,39),
            ForeColor = Color.Gainsboro
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(10), Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var warning = new Label
        {
            Name = "DriverLabWarning", Dock = DockStyle.Fill, AutoEllipsis = true,
            Text = "LABORATÓRIO SINTÉTICO — NÃO LÊ OU ALTERA O WINDOWS",
            AccessibleName = "Aviso de simulação fictícia, sem executar operações",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(238,204,133),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        var select = new ComboBox
        {
            Name = "DriverLabScenarioSelect", Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(37,48,61), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            AccessibleName = "Selecionar cenário fictício multiclasses de driver",
            AccessibleDescription = "Não consulta hardware nem Windows Update."
        };
        foreach (var s in DriverLabSimulation.Cases) select.Items.Add(new Item(s));
        var preview = new TextBox
        {
            Name = "DriverLabPreview", Dock = DockStyle.Fill, ReadOnly = true,
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F),
            BackColor = Color.FromArgb(33,39,49), ForeColor = Color.Gainsboro,
            AccessibleName = "Etapas e resultado fictícios do cenário",
            AccessibleDescription = "Texto local sem chamadas ao sistema, sem gravação ou instalação."
        };
        var close = new Button
        {
            Name = "DriverLabClose", Dock = DockStyle.Right, Width = 106,
            Text = "Fechar", AccessibleName = "Fechar janela de simulação"
        };
        close.Click += (_, _) => form.Close();
        select.SelectedIndexChanged += (_, _) =>
        {
            if (form.IsDisposed || select.SelectedItem is not Item row) return;
            preview.Text = DriverLabSimulation.Format(row.Case);
            preview.SelectionStart = 0;
            preview.SelectionLength = 0;
        };
        root.Controls.Add(warning, 0, 0);
        root.Controls.Add(select, 0, 1);
        root.Controls.Add(preview, 0, 2);
        root.Controls.Add(close, 0, 3);
        form.Controls.Add(root);
        select.SelectedIndex = 0;
        return form;
    }
}
