using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Resultado de varredura de possíveis sobras, sem botões de exclusão.
// Encontrar algo não prova que seja resíduo; exige política específica.
internal sealed class ResidualAuditDialog : Form
{
    private readonly CancellationTokenSource reviewCancellation = new();
    internal ResidualAuditDialog(ResidualAuditReport report, string? groupedDetails = null,
        Func<CancellationToken, Task<ResidualAuditReport>>? refresh = null)
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

        string intro = report.Incomplete
            ? "Análise parcial — alguns locais não puderam ser verificados. Preserve os itens e confira os limites abaixo."
            : report.Historical
            ? "A análise encontrou dados de fabricantes, possíveis instalações e vestígios sem confirmação. " +
              "Eles podem pertencer a programas ou jogos ativos. Nenhum item será apagado."
            : report.UninstallConfirmed
                ? $"O registro de desinstalação de {report.Product} não está mais presente. " +
                  "O desinstalador do fabricante ainda pode estar aberto. " +
                  "Os possíveis vestígios abaixo NÃO serão excluídos automaticamente."
                : $"O estado completo de {report.Product} não foi confirmado. " +
                  "Nenhuma exclusão será autorizada.";

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = intro + "\n\n" +
                (report.Product.StartsWith("foobar2000", StringComparison.OrdinalIgnoreCase)
                    ? "Limpeza voluntária limitada aos perfis do foobar2000."
                    : "Exclusão indisponível para este aplicativo na versão de teste."),
            ForeColor = Color.FromArgb(226, 238, 249),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10F),
            Padding = new Padding(6, 2, 5, 2)
        }, 0, 0);

        string details = groupedDetails ?? FormatSingle(report);
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
        if (refresh is not null)
        {
            var recheck = new ModernButton
            {
                Name = "ResidualRecheck", Text = "Conferir novamente", Size = new Size(165, 34),
                BackColor = AppServices.Gray, ForeColor = Color.White
            };
            recheck.Click += async (_, _) =>
            {
                recheck.Enabled = false;
                try
                {
                    var current = await refresh(reviewCancellation.Token);
                    if (!IsDisposed) text.SetText("CONFERÊNCIA SOMENTE LEITURA — NENHUM ITEM APAGADO\r\n\r\n" + FormatSingle(current));
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    if (!IsDisposed) text.SetText("Não foi possível conferir todos os dados. Preserve os itens e repita a análise.");
                }
                finally { if (!IsDisposed) recheck.Enabled = true; }
            };
            bar.Controls.Add(recheck);
        }
        if (report.Product.StartsWith("foobar2000", StringComparison.OrdinalIgnoreCase))
        {
            var cleanup = new ModernButton
            {
                Text = "Limpar perfis foobar", Size = new Size(160, 34),
                BackColor = AppServices.Red, ForeColor = Color.White
            };
            cleanup.Click += async (_, _) =>
            {
                cleanup.Enabled = false;
                try
                {
                    string result = await FoobarProfileCleanupFlow.ConfirmAndCleanAsync(this, reviewCancellation.Token);
                    if (!IsDisposed) text.SetText(result);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                { if (!IsDisposed) text.SetText("Limpeza não concluída: " + ex.Message); }
                finally { if (!IsDisposed) cleanup.Enabled = true; }
            };
            bar.Controls.Add(cleanup);
        }
        layout.Controls.Add(bar, 0, 2);
        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;
    }

    private static string FormatSingle(ResidualAuditReport report) =>
        (report.Coverage?.Describe() ?? (report.Incomplete ? ResidualCoverage.PartialMessage : "Exclusão indisponível nesta versão de teste.")) + "\r\n\r\n" +
        string.Join("\r\n\r\n", report.Findings.Select((f, i) =>
            $"{i + 1}. {f.DecisionLabel} — {f.Kind}: {f.Location}\r\n   {f.Items} item(ns) — {f.Evidence}")) +
        (report.RelatedRegisteredPrograms.Count > 0 ? "\r\n\r\nComponentes ainda registrados (preservar):\r\n" +
            string.Join("\r\n", report.RelatedRegisteredPrograms.Select(n => "• " + n)) : "");

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        reviewCancellation.Cancel();
        base.OnFormClosed(e);
        reviewCancellation.Dispose();
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
        var coverage = (historical as ResidualReportSet)?.Coverage ?? ResidualCoverage.Combine(historical.Select(r => r.Coverage));
        if (historical.Count == 0 && !coverage.Incomplete) return;
        using var window = new ResidualExplorerDialog(historical);
        window.ShowDialog(owner);
    }

    internal static void ShowResults(IWin32Window owner, ResidualAuditReport report,
        Func<CancellationToken, Task<ResidualAuditReport>>? refresh = null)
    {
        if (report.Findings.Count == 0 && report.RelatedRegisteredPrograms.Count == 0 && !report.Incomplete)
            return;
        using var window = new ResidualAuditDialog(report, refresh: refresh);
        window.ShowDialog(owner);
    }
}
