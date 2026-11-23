using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private readonly Label _cleanupPreview = new();
    private readonly Dictionary<CleanupTarget, (Label Title, Label State)> _cleanupRows = new();
    private TableLayoutPanel? _cleanupReport;
    private RowStyle? _cleanupReportRow;
    private Button? _cleanupPreviewButton;
    private CancellationTokenSource? _cleanupCancellation;
    private readonly Label _networkSummary = new();
    private readonly Label _networkDetails = new();
    private Button? _repairNetworkButton;
    private NetworkScan? _lastNetworkScan;

    private Control BuildNetworkResultPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3, ColumnCount = 1,
            BackColor = PanelBackground,
            Margin = new Padding(15, 2, 15, 4),
            Padding = new Padding(16, 8, 16, 8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = "DIAGNÓSTICO DE REDE", Dock = DockStyle.Fill,
            ForeColor = Ink, Font = new Font("Segoe UI", 10.2F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _networkSummary.Text = "Clique em Analisar rede para executar os testes. Nenhuma alteração será feita.";
        _networkSummary.ForeColor = Muted;
        _networkSummary.Dock = DockStyle.Fill;
        _networkSummary.TextAlign = ContentAlignment.MiddleLeft;
        _networkDetails.Dock = DockStyle.Fill;
        _networkDetails.ForeColor = Muted;
        _networkDetails.Font = new Font("Segoe UI", 8.8F);
        _networkDetails.TextAlign = ContentAlignment.TopLeft;
        _networkDetails.Padding = new Padding(0, 4, 0, 0);
        _networkDetails.AutoEllipsis = false;
        panel.Controls.Add(_networkSummary, 0, 1);
        panel.Controls.Add(_networkDetails, 0, 2);
        return panel;
    }

    private async Task RunNetworkDiagnosticAsync()
    {
        if (_busy) return;
        await ExecuteAsync("Verificando conexão, gateway, DNS e acesso HTTPS...", async () =>
        {
            var result = await NetworkDiagnostics.AnalyzeAsync();
            _lastNetworkScan = result;
            bool actionable = GetSuggestedNetworkRepair(result) is not null;
            _networkSummary.ForeColor = result.HasWarnings ? Color.FromArgb(244, 183, 102) : Color.FromArgb(127, 217, 166);
            _networkSummary.Text = !result.HasWarnings
                ? "Rede sem falhas nos testes disponíveis. Não há reparo indicado."
                : actionable
                    ? "Foram encontrados alertas. O botão Reparar rede pode aplicar uma correção específica."
                    : "Há alertas, mas nenhuma correção automática segura foi identificada. Confira os detalhes.";
            var lines = result.Checks.Select(check => $"{check.Name}: {check.Detail}").ToArray();
            _networkDetails.Text = string.Join(Environment.NewLine, lines.Take(5)) +
                (lines.Length > 5 ? $"{Environment.NewLine}Mais {lines.Length - 5} resultado(s) no registro." : "");
            _relayoutRepair?.Invoke();
            foreach (var check in result.Checks)
            {
                Log($"Rede [{check.State}]: {check.Name} — {check.Detail}");
            }
            Log("Diagnóstico de rede somente consultivo. Falha isolada pode resultar de firewall, proxy ou bloqueio de ICMP.");
            Log(actionable ? "Uma correção orientada está disponível mediante confirmação." : "Nenhuma correção automática indicada nesta análise.");
            Status(result.HasWarnings ? "Rede: consulte os alertas do diagnóstico" : "Diagnóstico de rede concluído");
        });
    }

    // Não confunde gateway/ICMP bloqueado com problema que precisa ser corrigido.
    // Só sugere correções quando a interface e o gateway foram identificados.
    private static string? GetSuggestedNetworkRepair(NetworkScan? scan)
    {
        if (scan is null) return null;
        bool localReady = scan.Checks.Any(c => c.Name == "Adaptador" && c.State == "OK") &&
                          scan.Checks.Any(c => c.Name == "Gateway" && c.State == "OK");
        if (!localReady) return null;
        if (scan.Checks.Any(c => c.Name == "DNS" && c.State == "Atenção"))
            return "flush-dns";
        // Somente quando DNS passou e a conectividade TCP/443 falhou; pode ser
        // proxy/firewall e, portanto, a ação requer confirmação explícita.
        if (scan.Checks.Any(c => c.Name == "DNS" && c.State == "OK") &&
            scan.Checks.Any(c => c.Name == "Conexão HTTPS" && c.State == "Atenção"))
            return "network-repair-winsock";
        return null;
    }

    private async Task RepairNetworkAfterDiagnosisAsync()
    {
        if (_busy) return;
        string? task = GetSuggestedNetworkRepair(_lastNetworkScan);
        if (task is null)
        {
            MessageBox.Show(this,
                "Execute Analisar rede primeiro. Se a análise estiver normal ou não indicar uma correção segura, " +
                "nenhuma configuração será alterada. Verifique os alertas antes de usar a redefinição avançada.",
                "Reparar rede", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool dnsRepair = task == "flush-dns";
        string confirmation = dnsRepair
            ? "O teste detectou uma falha na resolução DNS. Deseja limpar apenas o cache DNS do Windows? " +
              "Isso não altera os servidores DNS configurados. O teste será repetido depois da operação."
            : "O DNS respondeu, mas a conexão TCP/443 ao endereço de teste falhou. Isso pode ocorrer por proxy, firewall " +
              "ou bloqueio do servidor, e não comprova um defeito no Windows.\n\n" +
              "Deseja tentar redefinir APENAS o catálogo Winsock? A rede pode ser interrompida e será necessário " +
              "reiniciar o Windows para completar a alteração. Nenhum reinício automático será feito.";
        if (MessageBox.Show(this, confirmation, "Confirmar reparo orientado de rede",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            Log("Reparo de rede orientado cancelado pelo usuário.");
            return;
        }

        Log("Reparo orientado selecionado pelo diagnóstico: " + (dnsRepair ? "cache DNS" : "Winsock") + ".");
        var workerResult = NetworkRepairWorkerState.NotConfirmed;
        await ExecuteAsync(dnsRepair ? "Limpando cache DNS..." : "Redefinindo catálogo Winsock...",
            () => RunElevatedAsync(task,
                networkRepairCompletion: outcome => workerResult = outcome));

        if (workerResult != NetworkRepairWorkerState.CommandReturnedZero)
        {
            // UAC recusado, conexão interrompida e exit code não zero não
            // autorizam mostrar "reset solicitado" nem repetir às cegas.
            _lastNetworkScan = null;
            _networkSummary.ForeColor = Muted;
            _networkSummary.Text = workerResult == NetworkRepairWorkerState.CommandReturnedError
                ? "O Windows informou erro na operação. Nenhum reparo foi confirmado."
                : "Operação sem retorno conclusivo. Confira o registro antes de repetir.";
            _networkDetails.Text = "O GF não comprova qualquer mudança na rede. Refaça a análise antes de decidir outra operação.";
            if (_repairNetworkButton is not null) _repairNetworkButton.Enabled = false;
            Log("Resultado de manutenção de rede não confirmado (" + workerResult +
                "); nenhuma repetição automática.");
            return;
        }

        if (dnsRepair)
        {
            // Reexecutar os testes: não declarar sucesso apenas porque o comando terminou.
            await RunNetworkDiagnosticAsync();
        }
        else
        {
            _lastNetworkScan = null;
            _networkSummary.ForeColor = Muted;
            _networkSummary.Text = "Redefinição Winsock solicitada. Reinicie o Windows e execute Analisar rede novamente.";
            _networkDetails.Text = "O resultado depende da reinicialização. Nenhuma mudança no TCP/IP completo foi aplicada.";
            if (_repairNetworkButton is not null) _repairNetworkButton.Enabled = false;
            Log("A redefinição Winsock pode requerer reinicialização. Refaça a análise depois de reiniciar.");
        }
    }

    private Control BuildCleanupReport(RowStyle rowStyle)
    {
        var report = new TableLayoutPanel
        {
            Name = "CleanupCategoryReport", Dock = DockStyle.Fill, Margin = Padding.Empty,
            RowCount = CleanupTarget.All.Count, ColumnCount = 2, BackColor = PanelBackground
        };
        report.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 41));
        report.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 59));
        foreach (var target in CleanupTarget.All)
        {
            int row = _cleanupRows.Count;
            report.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            var title = new Label { Text = target.Title, Dock = DockStyle.Fill, ForeColor = Ink, TextAlign = ContentAlignment.MiddleLeft };
            var state = new Label
            {
                Text = "Aguardando análise", Dock = DockStyle.Fill, ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleLeft, AccessibleName = target.Title + " — situação"
            };
            _cleanupRows.Add(target, (title, state));
            report.Controls.Add(title, 0, row);
            report.Controls.Add(state, 1, row);
        }
        _cleanupReport = report;
        _cleanupReportRow = rowStyle;
        return report;
    }

    private int CleanupReportHeight(int pageWidth)
    {
        if (_cleanupReport is null || _cleanupReportRow is null) return 0;
        int usable = Math.Max(1, pageWidth - 42);
        int left = Math.Max(1, (int)(usable * .41) - 8);
        int right = Math.Max(1, usable - left - 16);
        int total = 0;
        int row = 0;
        foreach (var pair in _cleanupRows.Values)
        {
            int height = Math.Max(CleanupTextHeight(pair.Title, left), CleanupTextHeight(pair.State, right)) + 5;
            _cleanupReport.RowStyles[row++].Height = height;
            total += height;
        }
        _cleanupReportRow.Height = total;
        return total;
    }

    private static int CleanupTextHeight(Label label, int width) =>
        TextRenderer.MeasureText(label.Text, label.Font, new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;

    private void SetCleanupRow(CleanupTarget target, string text)
    {
        if (IsDisposed || Disposing || !_cleanupRows.TryGetValue(target, out var row)) return;
        row.State.Text = text;
        row.State.AccessibleDescription = text;
        _relayoutCleaning?.Invoke();
    }

    private void ShowCleanupStatus(string text)
    {
        if (IsDisposed || Disposing) return;
        _cleanupPreview.Text = text;
        _toolTips.SetToolTip(_cleanupPreview, text);
        Status(text);
    }

    private static string DisplayCleanupSize(double bytes) => bytes switch
    {
        < 1024 => $"{bytes:N0} B",
        < 1048576 => $"{bytes / 1024:N1} KiB",
        < 1073741824 => $"{bytes / 1048576:N1} MiB",
        _ => $"{bytes / 1073741824:N2} GiB"
    };

    private static string DescribeCleanupState(CleanupState state) => state switch
    {
        CleanupState.Partial => "Parcial",
        CleanupState.Missing => "Pasta ausente",
        CleanupState.Unavailable => "Não foi possível acessar ou verificar a categoria",
        CleanupState.Cancelled => "Cancelada",
        CleanupState.Busy => "Serviços de atualização ativos",
        CleanupState.Inconclusive => "Resultado inconclusivo",
        _ => "Concluída"
    };

    private static string DescribeCleanupEstimate(CleanupEstimate estimate)
    {
        string state = estimate.State switch
        {
            CleanupState.Complete => "Prévia",
            CleanupState.Partial => "Prévia parcial",
            _ => DescribeCleanupState(estimate.State)
        };
        string unit = estimate.Target == CleanupTarget.RecycleBin ? "itens" : "arquivos";
        if (estimate.State is CleanupState.Complete or CleanupState.Partial || estimate.Items > 0)
            state += $" · {estimate.Items:N0} {unit} · {DisplayCleanupSize(estimate.Bytes)}";
        return state + CleanupError(estimate.Error);
    }

    private static string CleanupError(int error) => error == 0 ? "" : $" (0x{error:X8})";

    private Action<CleanupProgress> CreateCleanupProgress(CleanupTarget target, CancellationTokenSource operation, bool preview)
    {
        IProgress<CleanupProgress> reporter = new Progress<CleanupProgress>(current =>
        {
            if (!ReferenceEquals(_cleanupCancellation, operation) || operation.IsCancellationRequested || IsDisposed ||
                !_cleanupRows[target].State.Text.StartsWith(preview ? "Analisando" : "Limpando", StringComparison.Ordinal))
                return;
            string prefix = preview ? "Analisando" : "Limpando";
            string text = $"{prefix} · {current.Examined:N0} examinados";
            if (!preview) text += $" · {current.Removed:N0} removidos";
            SetCleanupRow(target, text);
            Status($"{target.Title} · {text}");
        });
        return reporter.Report;
    }

    private async Task RunCancelableCleanupAsync(string initialStatus, string cancelCaption,
        Func<CancellationTokenSource, Task> work)
    {
        await ExecuteAsync(initialStatus, async () =>
        {
            using var operation = new CancellationTokenSource();
            _cleanupCancellation = operation;
            if (_cleanupPreviewButton is not null) _cleanupPreviewButton.Text = cancelCaption;
            ShowCleanupStatus(initialStatus);
            try { await work(operation); }
            finally
            {
                _cleanupCancellation = null;
                if (_cleanupPreviewButton is { IsDisposed: false }) _cleanupPreviewButton.Text = "Analisar limpeza";
            }
        });
    }

    private void RequestCleanupCancellation()
    {
        if (_cleanupCancellation is not { IsCancellationRequested: false } operation) return;
        operation.Cancel();
        ShowCleanupStatus("Cancelamento solicitado…");
    }

    private async Task ShowCleanupPreviewAsync()
    {
        if (_cleanupCancellation is not null) { RequestCleanupCancellation(); return; }
        if (_busy) return;

        await RunCancelableCleanupAsync("Analisando limpeza…", "Cancelar análise", async operation =>
        {
            foreach (var target in CleanupTarget.All) SetCleanupRow(target, "Aguardando análise");
            double totalBytes = 0;
            int uncertain = 0;
            bool cancelled = false;
            foreach (var target in CleanupTarget.All)
            {
                if (operation.IsCancellationRequested) { cancelled = true; break; }
                SetCleanupRow(target, "Analisando…");
                var estimate = await SafetyDiagnostics.EstimateCleanupAsync(target, operation.Token,
                    CreateCleanupProgress(target, operation, preview: true));
                string description = DescribeCleanupEstimate(estimate);
                SetCleanupRow(target, description);
                Log($"Prévia de limpeza · {target.Title} · {description}");
                totalBytes += estimate.Bytes;
                if (estimate.State is not (CleanupState.Complete or CleanupState.Missing)) uncertain++;
                if (estimate.State == CleanupState.Cancelled) { cancelled = true; break; }
            }
            if (cancelled)
                ShowCleanupStatus("Análise cancelada · confira os resultados já obtidos.");
            else
                ShowCleanupStatus((uncertain == 0 ? "Recuperável estimado" : "Prévia incompleta · identificado") +
                    $" · {DisplayCleanupSize(totalBytes)}");
        });
    }

    private async Task CleanTemporaryCategoryAsync(CleanupTarget target)
    {
        await RunCancelableCleanupAsync(target.Elevated ? "Aguardando autorização administrativa…" : "Iniciando limpeza…",
            "Cancelar limpeza", async operation =>
            {
                SetCleanupRow(target, "Limpando…");
                var progress = CreateCleanupProgress(target, operation, preview: false);
                TemporaryCleanupResult result;
                if (target.Elevated)
                {
                    var protocol = new ElevatedCleanupProtocol(operation.Token, progress, Log);
                    try { await RunElevatedAsync(target.Task, cleanup: protocol); }
                    catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
                    { DiagnosticTrace.Failure("LIMPEZA.SEM_RETORNO", ex); }
                    result = protocol.Result;
                    if (result.State == CleanupState.Inconclusive && SafeMaintenanceAudit.NoReturn(target.Task) is { } noReturn)
                        DiagnosticTrace.Write(noReturn);
                    else if (SafeMaintenanceAudit.ElevatedExit(target.Task,
                        ElevatedCleanupProtocol.ExitCode(result).ToString(System.Globalization.CultureInfo.InvariantCulture)) is { } elevated)
                        DiagnosticTrace.Write(elevated);
                }
                else result = await Task.Run(() => target.Clean(operation.Token, progress, Log));

                if (result.State == CleanupState.Missing && SafeMaintenanceAudit.MissingSource(target.Task) is { } missing)
                    DiagnosticTrace.Write(missing);
                if (result.State == CleanupState.Cancelled && SafeMaintenanceAudit.Cancelled(target.Task) is { } cancel)
                    DiagnosticTrace.Write(cancel);
                if (result.State != CleanupState.Inconclusive &&
                    SafeMaintenanceAudit.LocalResult(target.Task, result.FilesRemoved, result.DirectoriesRemoved,
                        result.Skipped, result.ReparseSkipped, result.Failed) is { } verified)
                    DiagnosticTrace.Write(verified);

                string description = DescribeCleanupState(result.State);
                if (result.State == CleanupState.Inconclusive)
                    description += " · consulte novamente antes de repetir";
                else if (result.State is CleanupState.Complete or CleanupState.Partial || result.FilesRemoved > 0 ||
                    result.DirectoriesRemoved > 0 || result.Failed > 0 || result.Skipped + result.ReparseSkipped > 0)
                    description += $" · {result.FilesRemoved:N0} arquivos · {DisplayCleanupSize(result.BytesRemoved)} removidos" +
                        $" · {result.Skipped + result.ReparseSkipped:N0} preservados" +
                        (result.Failed > 0 ? $" · {result.Failed:N0} falhas/limites" : "");
                SetCleanupRow(target, description);
                ShowCleanupStatus($"{target.Title} · {DescribeCleanupState(result.State)}");
                Log($"Limpeza · {target.Title} · {description}" +
                    (result.DirectoriesRemoved > 0 ? $" · {result.DirectoriesRemoved:N0} pastas vazias removidas" : ""));
            });
    }

    private async Task EmptyRecycleBinAsync()
    {
        if (_busy || _betaClose.IsClosing) return;
        if (DarkConfirmDialog.Confirm(this, "Esvaziar a Lixeira de todas as unidades deste usuário? A exclusão é permanente.",
            "Esvaziar Lixeira") != DialogResult.Yes)
        {
            Status("Operação cancelada");
            return;
        }
        await ExecuteAsync("Verificando e esvaziando a Lixeira…", async () =>
        {
            SetCleanupRow(CleanupTarget.RecycleBin, "Verificando no Windows…");
            var result = await StaWorker.Run(RecycleBinCleanup.Empty);
            string state = result.State == CleanupState.Complete ? "Vazia, confirmado pelo Windows" : DescribeCleanupState(result.State);
            string text = state + (result.State != CleanupState.Complete && result.Current.Known
                ? $" · {result.Current.Items:N0} itens restantes · {DisplayCleanupSize(result.Current.Bytes)}" : "")
                + CleanupError(result.Error);
            SetCleanupRow(CleanupTarget.RecycleBin, text);
            ShowCleanupStatus("Lixeira · " + state);
            Log("Limpeza · Lixeira · " + text);
        });
    }
}
