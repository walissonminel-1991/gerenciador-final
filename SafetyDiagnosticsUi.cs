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
    private sealed record V269CleanupOutcome(string Title,string Description,bool Complete);
    private V269CleanupOutcome? _v269CleanupOutcome;
    private bool _v269CleanupExecutionActive;
    private readonly List<V269AppPreviousResult> _v269CleanupPreviousResults = new();
    private V269PanelPresentation? _v269CleanupRetained;
    private Control? _v269CleanupSelected;
    private Func<V269AppDetailModel>? _v269CleanupDetails;
    private Action? _v269CleanupDefault;
    private void SelectV269CleanupItem(Control control, Func<V269AppDetailModel> details)
    {
        if(_v269CleanupSelected!=control)
        {
            if(_v269CleanupSelected is V269CleanupCard {IsDisposed:false} tool)tool.SetSelected(false);
            if(_v269CleanupSelected is V269CleanupDiskCard {IsDisposed:false} disk)disk.SetSelected(false);
            _v269CleanupSelected=control;
            if(control is V269CleanupCard selectedTool)selectedTool.SetSelected(true);
            if(control is V269CleanupDiskCard selectedDisk)selectedDisk.SetSelected(true);
        }
        _v269CleanupDetails=details;
        PresentV269CleanupSelection();
    }
    private void PresentV269CleanupSelection()
    {
        if(_busy||_v269Confirming||_nav.SelectedPage!=V269Page.Limpeza||
            _nav.RightPane.Current?.Kind is V269PanelKind.Progress or V269PanelKind.Confirmation or V269PanelKind.Result or V269PanelKind.History)
            return;
        if(_v269CleanupSelected is null||_v269CleanupSelected.IsDisposed)
        {
            _v269CleanupDefault?.Invoke();
            return;
        }
        if(_v269CleanupDetails is not {} modelProvider)return;
        var model=modelProvider();
        _nav.PresentPanel(new(V269PanelKind.Details,
            _v269CleanupSelected is V269CleanupDiskCard?"Detalhes do armazenamento":"Detalhes da limpeza","",
            Cleanup:model with {PreviousResults=_v269CleanupPreviousResults.ToArray(),DismissResult=DismissV269CleanupResult}));
    }

    private void RecordV269CleanupResult(string title,string description,bool complete)
    {
        if(IsDisposed||Disposing)return;
        var entry=new V269AppPreviousResult(Guid.NewGuid(),title,description,complete);
        _v269CleanupPreviousResults.Insert(0,entry);
        if(_v269CleanupPreviousResults.Count>4)_v269CleanupPreviousResults.RemoveAt(4);
        var presentation=new V269PanelPresentation(V269PanelKind.Result,title,description,
            FlowResult:new(V269Page.Limpeza,complete,()=>DismissV269CleanupResult(entry.Id),entry.Id));
        _v269CleanupRetained=presentation;
        if(_nav.SelectedPage==V269Page.Limpeza)_nav.PresentPanel(presentation);
        _nav.RightPane.RefreshAppOperationHistory();
    }
    private void DismissV269CleanupResult(Guid id)
    {
        if(IsDisposed||Disposing||_nav.SelectedPage!=V269Page.Limpeza)return;
        if(_v269CleanupPreviousResults.RemoveAll(x=>x.Id==id)==0)return;
        if(_v269CleanupRetained?.FlowResult?.HistoryId==id)_v269CleanupRetained=null;
        if(_nav.RightPane.Current?.FlowResult is { Page: V269Page.Limpeza,HistoryId:var current }&&current==id)
            _nav.PresentPanel(null);
        _nav.RightPane.RefreshAppOperationHistory();
        PresentV269CleanupSelection();
    }
    private void PresentV269CleanupRetained()
    {
        if(!_busy&&!_v269Confirming&&_v269CleanupRetained is {} result&&
            _nav.RightPane.Current?.Kind is not (V269PanelKind.Progress or V269PanelKind.Confirmation or V269PanelKind.Result or V269PanelKind.History))
            _nav.PresentPanel(result);
        else PresentV269CleanupSelection();
    }
    private TableLayoutPanel? _cleanupReport;
    private RowStyle? _cleanupReportRow;
    private Button? _cleanupPreviewButton;
    private CancellationTokenSource? _cleanupCancellation;
    private Button? _repairNetworkButton;
    private NetworkScan? _lastNetworkScan;
    private Task<NetworkScan>? _networkQuery;
    private Task<AudioOutputDiagnosis>? _audioQuery;
    private Task<PrinterQueueDiagnosis>? _printerQuery;
    private Task<UsbPnpDiagnosis>? _usbQuery;
    private Task<ChipsetPnpDiagnosis>? _chipsetQuery;
    private Task<BlueScreenDiagnosis>? _blueScreenQuery;

    private void InvalidateNetworkDiagnosis()
    {
        _lastNetworkScan = null;
        _networkQuery = null;
        if (_repairNetworkButton is not null) _repairNetworkButton.Enabled = false;
    }

    private async Task<NetworkScan> AnalyzeNetworkCoreAsync(CancellationToken token)
    {
        _lastNetworkScan = null;
        if (_networkQuery is not { IsCompleted: false }) _networkQuery = NetworkDiagnostics.AnalyzeAsync();
        var result = await _networkQuery.WaitAsync(token);
        token.ThrowIfCancellationRequested();
        _lastNetworkScan = result;
        foreach (var check in result.Checks)
            Log($"Rede [{check.State}]: {check.Name} — {check.Detail}");
        Log("Diagnóstico de rede somente consultivo. Os testes TCP/443 e ICMP se limitam aos destinos consultados.");
        Log(GetSuggestedNetworkRepair(result) switch
        {
            "flush-dns" => "Limpeza do cache DNS disponível mediante confirmação.",
            "network-repair-winsock" => "Redefinição orientada do catálogo Winsock disponível mediante confirmação.",
            _ => "Nenhum reparo guiado indicado nesta análise."
        });
        return result;
    }

    private static string NetworkDiagnosisReport(NetworkScan result) => string.Join(Environment.NewLine,
        result.Checks.Select(check => $"{check.Name} [{check.State}]: {check.Detail}"));

    private Task RunNetworkDiagnosticAsync() => ExecuteV269RepairQueryAsync(
        "Verificando adaptador, gateway, DNS e conexão TCP/443...", async token =>
        {
            var result = await AnalyzeNetworkCoreAsync(token);
            string summary = GetSuggestedNetworkRepair(result) switch
            {
                "flush-dns" => "Os dois nomes de teste não resolveram. Reparar rede oferece a limpeza do cache DNS.",
                "network-repair-winsock" => "DNS respondeu e os dois testes TCP falharam. Reparar rede oferece uma redefinição orientada do catálogo Winsock.",
                _ when result.HasWarnings => "Há alertas, mas nenhuma correção automática segura foi identificada.",
                _ => "Rede sem falhas nos testes disponíveis. Nenhum reparo indicado."
            };
            bool complete = result.Checks.Count > 0 && result.Checks.All(check => check.State != "Info");
            ShowRepairDiagnostic(summary, summary + Environment.NewLine + Environment.NewLine + NetworkDiagnosisReport(result), complete);
        });

    // Delegar a recomendação ao contrato testado de rede, mantendo a UI.
    private static string? GetSuggestedNetworkRepair(NetworkScan? scan) =>
        NetworkDiagnostics.SuggestTargetedRepair(scan);

    private Task RunAudioDiagnosticAsync() => ExecuteV269RepairQueryAsync("Consultando saídas de áudio...", async token =>
    {
        try
        {
            var diagnosis = await WaitForRepairQueryAsync(ref _audioQuery, AudioOutputDiagnostic.Read, token);
            ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe(), diagnosis.State != AudioOutputState.Inconclusive);
        }
        catch (TimeoutException)
        {
            var diagnosis = new AudioOutputDiagnosis(AudioOutputState.Inconclusive);
            ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe() + "\n\nA consulta ao Windows continua em andamento.", false);
        }
    });

    private Task RunPrinterDiagnosticAsync() => ExecuteV269RepairQueryAsync("Consultando a impressora padrão...", async token =>
    {
        try
        {
            var diagnosis = await WaitForRepairQueryAsync(ref _printerQuery, ReadDefaultPrinter, token);
            ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe(), diagnosis.State != PrinterQueueState.Inconclusive);
        }
        catch (TimeoutException)
        {
            var diagnosis = new PrinterQueueDiagnosis(PrinterQueueState.Inconclusive);
            ShowRepairDiagnostic(diagnosis.Summary,
                diagnosis.Describe() + "\n\nO Windows ainda está respondendo à consulta.", false);
        }
    });

    private Task RunUsbDiagnosticAsync() => ExecuteV269RepairQueryAsync("Consultando dispositivos USB...", async token =>
    {
        try
        {
            var diagnosis = await WaitForRepairQueryAsync(ref _usbQuery, ReadUsbDiagnosis, token);
            ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe(), !diagnosis.Partial);
        }
        catch (TimeoutException)
        {
            var diagnosis = UsbPnpDiagnostic.Evaluate([], partial: true);
            ShowRepairDiagnostic(diagnosis.Summary,
                diagnosis.Describe() + "\n\nA consulta ao Windows continua em andamento.", false);
        }
    });

    private Task RunChipsetDiagnosticAsync() => ExecuteV269RepairQueryAsync("Consultando controladores do chipset...", async token =>
    {
        try
        {
            var diagnosis = await WaitForRepairQueryAsync(ref _chipsetQuery,
                () => ChipsetPnpDiagnostic.Evaluate(DriverInventory.Capture(CancellationToken.None)), token);
            ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe(), !diagnosis.Partial);
        }
        catch (TimeoutException)
        {
            var diagnosis = new ChipsetPnpDiagnosis([], Partial: true);
            ShowRepairDiagnostic(diagnosis.Summary,
                diagnosis.Describe() + "\n\nA consulta ao Windows continua em andamento.", false);
        }
    });

    private Task RunBlueScreenDiagnosticAsync() => ExecuteV269RepairQueryAsync("Consultando registros de tela azul...", async token =>
    {
        try
        {
            var diagnosis = await WaitForRepairQueryAsync(ref _blueScreenQuery, BlueScreenEventLog.Read, token);
            ShowRepairDiagnostic(diagnosis.Summary, diagnosis.Describe(), !diagnosis.Partial);
        }
        catch (TimeoutException)
        {
            var diagnosis = new BlueScreenDiagnosis([], Partial: true);
            ShowRepairDiagnostic(diagnosis.Summary,
                diagnosis.Describe() + "\n\nA consulta ao Windows continua em andamento.", false);
        }
    });

    private static Task<T> WaitForRepairQueryAsync<T>(ref Task<T>? query, Func<T> read, CancellationToken token)
    {
        // Não iniciar outra leitura do mesmo tipo enquanto o Windows processa a primeira.
        if (query is not { IsCompleted: false }) query = Task.Run(read);
        return query.WaitAsync(TimeSpan.FromSeconds(10), token);
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

    private void ShowRepairDiagnostic(string summary, string report, bool complete)
    {
        if (IsDisposed || Disposing || _betaClose.IsClosing) return;
        Log(report);
        Status(summary);
        SetV269RepairOutcome(report, complete);
    }

    private async Task RepairNetworkAfterDiagnosisAsync()
    {
        if (_busy) return;
        var before = _lastNetworkScan;
        string? task = GetSuggestedNetworkRepair(before);
        if (task is null)
        {
            const string blocked = "Confira o diagnóstico. O reparo guiado exige uma rota IPv4 inequívoca e evidências concordantes nos dois testes.";
            Status(blocked);
            SetV269RepairOutcome(blocked, false);
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
        if (!await ConfirmV269Async("Reparar rede", confirmation))
        {
            Log("Reparo de rede orientado cancelado pelo usuário.");
            return;
        }
        if (_busy || !ReferenceEquals(before, _lastNetworkScan) || GetSuggestedNetworkRepair(_lastNetworkScan) != task)
        {
            SetV269RepairOutcome("A fotografia de rede mudou. Execute uma nova análise antes de reparar.", false);
            return;
        }
        Log("Reparo orientado selecionado pelo diagnóstico: " + (dnsRepair ? "cache DNS" : "Winsock") + ".");
        InvalidateNetworkDiagnosis();
        await ExecuteAsync(dnsRepair ? "Limpando cache DNS..." : "Redefinindo catálogo Winsock...", async () =>
        {
            var workerResult = NetworkRepairWorkerState.NotConfirmed;
            await RunElevatedAsync(task, networkRepairCompletion: outcome => workerResult = outcome);
            if (IsDisposed || Disposing || _betaClose.IsClosing) return;
            if (workerResult != NetworkRepairWorkerState.CommandReturnedZero)
            {
                string result = workerResult == NetworkRepairWorkerState.CommandReturnedError
                    ? "O Windows informou erro na operação. Nenhum reparo foi confirmado."
                    : "Operação sem retorno conclusivo. Confira o registro antes de repetir.";
                ShowRepairDiagnostic(result, result + " Refaça a análise antes de decidir outra operação.", false);
                return;
            }
            if (!dnsRepair)
            {
                const string result = "Redefinição Winsock solicitada. Reinicie o Windows e execute Analisar rede novamente.";
                ShowRepairDiagnostic(result, result + " O resultado depende da reinicialização.", false);
                return;
            }
            // Código zero comprova o flush; a recuperação exige nova consulta dos dois nomes.
            Status("Consultando novamente a rede após a limpeza do cache DNS...");
            var after = await AnalyzeNetworkCoreAsync(CancellationToken.None);
            var checkedDns = NetworkRepairOutcome.ReviewDnsAfterFlush(before, after, workerResult);
            string verification = checkedDns switch
            {
                NetworkDnsVerification.TestRecovered => "Pós-flush DNS: os dois nomes resolveram na nova consulta; isso não prova que o flush foi a causa.",
                NetworkDnsVerification.PartiallyRecovered => "Pós-reparo DNS parcial: somente um dos dois nomes resolveu.",
                NetworkDnsVerification.TestStillFailing => "Pós-reparo: nenhum dos dois nomes resolveu. Confira rede, proxy ou servidor.",
                _ => "Pós-reparo DNS inconclusivo; consulte o registro."
            };
            ShowRepairDiagnostic(verification, verification + Environment.NewLine + Environment.NewLine + NetworkDiagnosisReport(after),
                checkedDns == NetworkDnsVerification.TestRecovered);
        });
    }

    private Control BuildCleanupReport(RowStyle rowStyle)
    {
        var report = new TableLayoutPanel
        {
            Name = "CleanupCategoryReport", Dock = DockStyle.Left, Margin = Padding.Empty,
            RowCount = 5, ColumnCount = 2, BackColor = Color.Transparent
        };
        report.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        report.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        foreach (var target in new[] { CleanupTarget.UserTemp, CleanupTarget.WindowsTemp,
            CleanupTarget.UpdateCache, CleanupTarget.RecycleBin, CleanupTarget.DirectX })
        {
            int row = _cleanupRows.Count;
            report.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            string caption = target == CleanupTarget.RecycleBin ? "Esvaziar Lixeira" :
                target == CleanupTarget.DirectX ? "Cache de shaders DirectX" : target.Title;
            var title = new Label { Text = caption, Dock = DockStyle.Fill, ForeColor = Muted,
                BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.25F), Margin = new Padding(2, 2, 3, 2) };
            var state = new Label
            {
                Text = "Aguardando análise", Dock = DockStyle.Fill, ForeColor = Ink,
                BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                Margin = new Padding(2, 2, 7, 2), AccessibleName = target.Title + " — situação"
            };
            _cleanupRows.Add(target, (title, state));
            report.Controls.Add(title, 0, row);
            report.Controls.Add(state, 1, row);
        }
        report.Paint += (_, e) =>
        {
            using var pen = new Pen(V269Palette.Border);
            int y = 0;
            foreach (int h in report.GetRowHeights())
            {
                y += h;
                if (y > 0) e.Graphics.DrawLine(pen, 0, y - 1, report.Width - 1, y - 1);
            }
        };
        _cleanupReport = report;
        _cleanupReportRow = rowStyle;
        return report;
    }

    private sealed record CleanupReportMetric(string Text, string FontFamily, float FontSize,
        FontStyle FontStyle, GraphicsUnit FontUnit, int Dpi, Padding Margin);
    private (int Width, int Gap, int Pairs, CleanupReportMetric[] Metrics, int Height)? _cleanupReportMeasurement;

    private int CleanupReportHeight(int pageWidth)
    {
        if (_cleanupReport is null || _cleanupReportRow is null) return 0;
        var report = _cleanupReport;
        int S(int x) => V269AppTable.Scale(x, _v269Appearance.InterfacePercent, DeviceDpi);
        int usable = Math.Max(1, Math.Min(S(560), pageWidth - (_v269CleanupDiskSection?.Padding.Horizontal??S(26))));
        int pairs = usable >= S(500) ? 2 : 1;
        int rows = (_cleanupRows.Count + pairs - 1) / pairs;
        var values = _cleanupRows.Values.ToArray();
        CleanupReportMetric Metric(Label label) => new(label.Text, label.Font.FontFamily.Name,
            label.Font.Size, label.Font.Style, label.Font.Unit, label.DeviceDpi, label.Margin);
        var metrics=values.SelectMany(pair=>new[]{Metric(pair.Title),Metric(pair.State)}).ToArray();
        int gap=S(10);
        if(_cleanupReportMeasurement is {} cached && cached.Width==usable && cached.Gap==gap &&
            cached.Pairs==pairs && cached.Metrics.SequenceEqual(metrics) &&
            report.Width==usable && report.RowCount==rows && report.ColumnCount==pairs*2 &&
            report.RowStyles.Count==rows && _cleanupReportRow.Height==cached.Height)
            return cached.Height;
        report.SuspendLayout();
        try
        {
            if(report.Width!=usable)report.Width = usable;
            // Retirar apenas os controles da grade, sem destruí-los: o motor mantém as referências.
            bool rebuild = report.ColumnCount != pairs * 2 || report.RowCount != rows;
            if (rebuild)
            {
                report.Controls.Clear();
                report.ColumnCount = pairs * 2;
                report.RowCount = rows;
                report.ColumnStyles.Clear();
                for (int i = 0; i < pairs; i++)
                {
                    report.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65f / pairs));
                    report.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f / pairs));
                }
            }
            report.RowStyles.Clear();
            int[] heights = new int[rows];
            int titleWidth = Math.Max(1, (int)(usable * .65 / pairs) - S(10));
            int stateWidth = Math.Max(1, (int)(usable * .35 / pairs) - S(10));
            for (int i = 0; i < values.Length; i++)
            {
                var pair = values[i];
                int row = i / pairs;
                int col = (i % pairs) * 2;
                if(rebuild)
                {
                    report.Controls.Add(pair.Title, col, row);
                    report.Controls.Add(pair.State, col + 1, row);
                }
                // CSS aprovado: padding vertical de 5px, borda de 1px
                // e espaçamento de 5px entre as células.
                heights[row] = Math.Max(heights[row], Math.Max(S(28),
                    Math.Max(ReportTextHeight(pair.Title, titleWidth),
                    ReportTextHeight(pair.State, stateWidth)) + S(16)));
            }
            foreach (int height in heights) report.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            int total = heights.Sum();
            if(_cleanupReportRow.Height!=total)_cleanupReportRow.Height = total;
            _cleanupReportMeasurement=(usable,gap,pairs,metrics,total);
            return total;
        }
        finally { report.ResumeLayout(true); }
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
        if(_nav.RightPane.Current?.Cleanup is not null)PresentV269CleanupSelection();
    }

    private void ShowCleanupStatus(string text)
    {
        if (IsDisposed || Disposing) return;
        _cleanupPreview.Text = "Estimativa por categoria";
        _cleanupPreview.AccessibleDescription = text;
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
            _v269CleanupOutcome=new("Analisar limpeza",_operationResult,!cancelled&&uncertain==0);
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
                _v269CleanupOutcome=new(target.Title,description,result.State==CleanupState.Complete);
                Log($"Limpeza · {target.Title} · {description}" +
                    (result.DirectoriesRemoved > 0 ? $" · {result.DirectoriesRemoved:N0} pastas vazias removidas" : ""));
            });
    }

    private async Task EmptyRecycleBinAsync()
    {
        if (_busy || _betaClose.IsClosing) return;
        if (!await ConfirmV269Async("Esvaziar Lixeira",
            "Esvaziar a Lixeira de todas as unidades deste usuário? A exclusão é permanente."))
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
            _v269CleanupOutcome=new("Esvaziar Lixeira",text,result.State==CleanupState.Complete);
            Log("Limpeza · Lixeira · " + text);
        });
    }
}
