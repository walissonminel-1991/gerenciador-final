using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
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
    private readonly Label _repairReport = new()
    {
        Name = "RepairDiagnosticResult", Dock = DockStyle.Fill, Visible = false,
        Margin = new Padding(15, 2, 15, 4), Padding = new Padding(16, 8, 16, 8),
        BackColor = PanelBackground, ForeColor = Ink, UseMnemonic = false,
        AccessibleName = "Resultado do diagnóstico"
    };
    private Task<PrinterQueueDiagnosis>? _printerQuery;
    private Task<UsbPnpDiagnosis>? _usbQuery;
    private Task<ChipsetPnpDiagnosis>? _chipsetQuery;
    private Task<BlueScreenDiagnosis>? _blueScreenQuery;

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
        await ExecuteAsync("Verificando adaptador, gateway, DNS e conexão TCP/443...", async () =>
        {
            var result = await NetworkDiagnostics.AnalyzeAsync();
            _lastNetworkScan = result;
            bool actionable = GetSuggestedNetworkRepair(result) is not null;
            _networkSummary.ForeColor = result.HasWarnings ? Color.FromArgb(244, 183, 102) : Color.FromArgb(127, 217, 166);
            _networkSummary.Text = !result.HasWarnings
                ? "Rede sem falhas nos testes disponíveis. Não há reparo indicado."
                : actionable
                    ? "A resolução DNS falhou. Reparar rede oferece a limpeza do cache DNS."
                    : "Há alertas, mas nenhuma correção automática segura foi identificada. Confira os detalhes.";
            var lines = result.Checks.Select(check => $"{check.Name}: {check.Detail}").ToArray();
            _networkDetails.Text = string.Join(Environment.NewLine, lines.Take(5)) +
                (lines.Length > 5 ? $"{Environment.NewLine}Mais {lines.Length - 5} resultado(s) no registro." : "");
            _relayoutRepair?.Invoke();
            foreach (var check in result.Checks)
            {
                Log($"Rede [{check.State}]: {check.Name} — {check.Detail}");
            }
            Log("Diagnóstico de rede somente consultivo. Os testes TCP/443 e ICMP se limitam aos destinos consultados.");
            Log(actionable ? "A limpeza do cache DNS está disponível mediante confirmação." : "Nenhum reparo guiado indicado nesta análise.");
            Status(result.HasWarnings ? "Rede: consulte os alertas do diagnóstico" : "Diagnóstico de rede concluído");
        });
    }

    // Delegar a recomendação ao contrato testado de rede, mantendo a UI.
    // Um único destino TCP/443 nunca justifica sugerir Winsock.
    private static string? GetSuggestedNetworkRepair(NetworkScan? scan) =>
        NetworkDiagnostics.SuggestTargetedRepair(scan);

    private Task RunAudioDiagnosticAsync() => ExecuteAsync("Consultando saídas de áudio...", async () =>
    {
        var diagnosis = await Task.Run(AudioOutputDiagnostic.Read);
        ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe());
    });

    private Task RunPrinterDiagnosticAsync() => ExecuteAsync("Consultando a impressora padrão...", async () =>
    {
        try
        {
            var diagnosis = await WaitForRepairQueryAsync(ref _printerQuery, ReadDefaultPrinter);
            ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe());
        }
        catch (TimeoutException)
        {
            var diagnosis = new PrinterQueueDiagnosis(PrinterQueueState.Inconclusive);
            ShowRepairDiagnostic(diagnosis.Summary,
                diagnosis.Describe() + "\n\nO Windows ainda está respondendo à consulta.");
        }
    });

    private Task RunUsbDiagnosticAsync() => ExecuteAsync("Consultando dispositivos USB...", async () =>
    {
        try
        {
            var diagnosis = await WaitForRepairQueryAsync(ref _usbQuery, ReadUsbDiagnosis);
            ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe());
        }
        catch (TimeoutException)
        {
            var diagnosis = UsbPnpDiagnostic.Evaluate([], partial: true);
            ShowRepairDiagnostic(diagnosis.Summary,
                diagnosis.Describe() + "\n\nA consulta ao Windows continua em andamento.");
        }
    });

    private Task RunChipsetDiagnosticAsync() => ExecuteAsync("Consultando controladores do chipset...", async () =>
    {
        try
        {
            var diagnosis = await WaitForRepairQueryAsync(ref _chipsetQuery,
                () => ChipsetPnpDiagnostic.Evaluate(DriverInventory.Capture(CancellationToken.None)));
            ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe());
        }
        catch (TimeoutException)
        {
            var diagnosis = new ChipsetPnpDiagnosis([], Partial: true);
            ShowRepairDiagnostic(diagnosis.Summary,
                diagnosis.Describe() + "\n\nA consulta ao Windows continua em andamento.");
        }
    });

    private Task RunBlueScreenDiagnosticAsync() => ExecuteAsync("Consultando registros de tela azul...", async () =>
    {
        try
        {
            var diagnosis = await WaitForRepairQueryAsync(ref _blueScreenQuery, BlueScreenEventLog.Read);
            ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe());
        }
        catch (TimeoutException)
        {
            var diagnosis = new BlueScreenDiagnosis([], Partial: true);
            ShowRepairDiagnostic(diagnosis.Summary,
                diagnosis.Describe() + "\n\nA consulta ao Windows continua em andamento.");
        }
    });

    private static Task<T> WaitForRepairQueryAsync<T>(ref Task<T>? query, Func<T> read)
    {
        // Depois de encerrar a espera da interface, não iniciar outra leitura
        // do mesmo tipo enquanto o Windows ainda processa a primeira.
        if (query is not { IsCompleted: false }) query = Task.Run(read);
        return query.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static UsbPnpDiagnosis ReadUsbDiagnosis()
    {
        var observations = new List<UsbPnpObservation>();
        var snapshot = MaintenanceAudit.ReadPnpDevices((id, present, code) =>
        {
            if (UsbPnpDiagnostic.IsUsbInstance(id)) observations.Add(new(id, present, code));
        });
        return UsbPnpDiagnostic.Evaluate(observations, snapshot.Partial || !snapshot.Supported);
    }

    private static PrinterQueueDiagnosis ReadDefaultPrinter()
    {
        try
        {
            return PrinterQueueDiagnostic.Evaluate(DetailedHardware.ReadLocalWmi(
                "SELECT PrinterStatus, ExtendedPrinterStatus, DetectedErrorState FROM Win32_Printer WHERE Default = TRUE",
                "PrinterStatus", "ExtendedPrinterStatus", "DetectedErrorState"));
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or
            InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return new(PrinterQueueState.Inconclusive);
        }
    }

    private void ShowRepairDiagnostic(string summary, string report)
    {
        if (IsDisposed || Disposing || _betaClose.IsClosing) return;
        _repairReport.Text = report;
        _repairReport.Visible = true;
        _relayoutRepair?.Invoke();
        Log(report);
        Status(summary);
    }

    private int RepairDiagnosticHeight(int pageWidth)
    {
        if (_repairReport.Text.Length == 0) return 0;
        var padding = _repairReport.Padding;
        var margin = _repairReport.Margin;
        int width = Math.Max(1, pageWidth - padding.Horizontal - margin.Horizontal);
        return ReportTextHeight(_repairReport, width) + padding.Vertical + margin.Vertical;
    }

    private async Task RepairNetworkAfterDiagnosisAsync()
    {
        if (_busy) return;
        string? task = GetSuggestedNetworkRepair(_lastNetworkScan);
        if (task is null)
        {
            MessageBox.Show(this,
                "Confira o diagnóstico. O reparo guiado exige uma rota IPv4 inequívoca e evidências concordantes nos dois testes.",
                "Reparar rede", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool dnsRepair = task == "flush-dns";
        string confirmation = dnsRepair
            ? "Dois nomes de teste independentes não resolveram. Isso pode ocorrer por rede indisponível, " +
              "firewall ou DNS e NÃO comprova que o cache esteja corrompido.\n\n" +
              "Deseja limpar apenas o cache DNS do Windows? Isso não altera os servidores DNS " +
              "configurados nem reinicia o Windows. Ambos os nomes serão consultados novamente."
            : "O DNS respondeu, mas dois destinos TCP/443 independentes falharam. Isso pode ocorrer por proxy, firewall " +
              "ou bloqueio externo e não comprova defeito no catálogo Winsock.\n\n" +
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

        if (IsDisposed || Disposing || _betaClose.IsClosing) return;
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
            // Código zero comprova somente o flush, não recuperação.
            // Reconsultar os mesmos dois nomes após a operação e preservar
            // estados mistos como parciais, jamais sucesso pleno.
            var before = _lastNetworkScan;
            _lastNetworkScan = null; // impedir reutilização de consulta anterior
            await RunNetworkDiagnosticAsync();
            var checkedDns = NetworkRepairOutcome.ReviewDnsAfterFlush(
                before, _lastNetworkScan, workerResult);
            switch (checkedDns)
            {
                case NetworkDnsVerification.TestRecovered:
                    Log("Pós-flush DNS: os dois nomes resolveram na nova consulta; não prova que o flush foi a causa.");
                    Status("Pós-reparo: resolução dos dois nomes de teste observada.");
                    break;
                case NetworkDnsVerification.PartiallyRecovered:
                    Log("Pós-flush DNS: somente um dos dois nomes resolveu. Resultado parcial; não repetir automaticamente.");
                    Status("Pós-reparo DNS parcial; confira os alertas sem inferir correção geral.");
                    break;
                case NetworkDnsVerification.TestStillFailing:
                    Log("Pós-flush DNS: nenhum dos dois nomes resolveu. Não repetir automaticamente.");
                    Status("Pós-reparo: alerta DNS permanece; confira rede, proxy ou servidor.");
                    break;
                default:
                    Log("Pós-flush DNS: observação incompleta. Não declarar correção.");
                    Status("Pós-reparo DNS inconclusivo; consulte o registro.");
                    break;
            }
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
            int height = Math.Max(ReportTextHeight(pair.Title, left), ReportTextHeight(pair.State, right)) + 5;
            _cleanupReport.RowStyles[row++].Height = height;
            total += height;
        }
        _cleanupReportRow.Height = total;
        return total;
    }

    private static int ReportTextHeight(Label label, int width) =>
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
