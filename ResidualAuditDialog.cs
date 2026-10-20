using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Resultado de varredura de possíveis sobras, sem botões de exclusão.
// Encontrar algo não prova que seja resíduo; exige política específica.
internal sealed class ResidualAuditDialog : Form
{
    private ResidualAuditDialog(ResidualAuditReport report, string? groupedDetails = null)
    {
        Text = report.Historical ? "Revisão de dados e instalações históricas" : "Revisão após desinstalação";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = AppServices.Background;
        ForeColor = Color.FromArgb(234, 242, 249);
        Font = new Font("Segoe UI", 9.5F);
        ClientSize = new Size(690, 475);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(20, 16, 20, 14),
            BackColor = AppServices.Background
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        string intro = report.Historical
            ? "A análise encontrou dados de fabricantes, possíveis instalações e vestígios sem confirmação. " +
              "Eles podem pertencer a programas ou jogos ativos. Nenhum item será apagado."
            : report.UninstallConfirmed
                ? $"O registro de desinstalação de {report.Product} não está mais presente. " +
                  "O desinstalador do fabricante ainda pode estar aberto. " +
                  "Os possíveis vestígios abaixo NÃO serão excluídos automaticamente."
                : $"{report.Product} ainda aparece instalado ou possui registros associados. " +
                  "O estado completo não foi confirmado. Nenhuma exclusão será autorizada.";

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = intro + "\n\nArquivos desconhecidos, drivers e chaves compartilhadas exigem análise individual.",
            ForeColor = Color.FromArgb(226, 238, 249),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10F),
            Padding = new Padding(6, 2, 5, 2)
        }, 0, 0);

        string details = groupedDetails ?? string.Join("\r\n\r\n", report.Findings.Select((f, i) =>
            $"{i + 1}. {f.DecisionLabel} — {f.Kind}: {f.Location}\r\n" +
            $"   {f.Items} item(ns) encontrados — {f.Evidence}"));
        if (report.RelatedRegisteredPrograms.Count > 0)
            details += "\r\n\r\nComponentes relacionados que ainda aparecem instalados:\r\n" +
                string.Join("\r\n", report.RelatedRegisteredPrograms.Select(n => "• " + n)) +
                "\r\nEsses componentes NÃO são classificados como resíduos.";
        var text = new DarkLogView { Dock = DockStyle.Fill };
        text.SetText(details);
        layout.Controls.Add(text, 0, 1);

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Padding = new Padding(0, 11, 0, 0)
        };
        var close = new ModernButton
        {
            Text = "Fechar", DialogResult = DialogResult.OK,
            Size = new Size(120, 34), BackColor = AppServices.Blue,
            ForeColor = Color.White
        };
        bar.Controls.Add(close);
        layout.Controls.Add(bar, 0, 2);
        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int enabled = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref enabled, 4) != 0)
                DwmSetWindowAttribute(Handle, 19, ref enabled, 4);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);

    internal static void ShowHistoricalResults(IWin32Window owner,
        IReadOnlyList<ResidualAuditReport> historical)
    {
        if (historical.Count == 0) return;
        // Uma única janela, por produto e risco, sem repetir cada advertência.
        // O relatório permanece somente leitura, SEM botões para apagar dados.
        var sorted = historical.OrderBy(r => r.Product, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var grouped = string.Join("\r\n\r\n", sorted.Select(report =>
        {
            string locations = string.Join("\r\n", report.Findings.Select(f =>
                "  • " + f.DecisionLabel + " — " + f.Location + "\r\n    " + f.Evidence));
            return report.Product + (report.Incomplete ? " — REVISÃO PARCIAL" : " — REVISÃO SEM EXCLUSÃO") +
                "\r\n" + locations;
        }));
        var merged = new ResidualAuditReport("Revisão histórica", false,
            sorted.SelectMany(r => r.Findings).ToArray(),
            Array.Empty<string>(), Historical: true);
        using var window = new ResidualAuditDialog(merged, grouped);
        window.ShowDialog(owner);
    }

    internal static void ShowResults(IWin32Window owner, ResidualAuditReport report)
    {
        if (report.Findings.Count == 0 && report.RelatedRegisteredPrograms.Count == 0)
            return;
        using var window = new ResidualAuditDialog(report);
        window.ShowDialog(owner);
    }
}
