using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace GerenciadorAplicativos;

// Dados agregados da sessão, sem caminhos, nomes de aplicativos, IDs de
// dispositivos, mensagens de exceção ou leitura de arquivos pessoais.
// A exportação manual não é telemetria e não depende de consentimento remoto.
internal sealed class LocalDiagnosticReport
{
    internal static LocalDiagnosticReport Session { get; } = new();
    private const int Limit = 128;
    private readonly object _gate = new();
    private readonly Queue<string> _performance = new();
    private readonly Queue<string> _process = new();
    private readonly Queue<string> _failures = new();
    private long _performanceTotal, _processTotal, _failureTotal;
    private long _lastTick;
    private double? _lastCpu;

    private static void Append(Queue<string> queue, string line, ref long total)
    {
        total++;
        if (queue.Count == Limit) queue.Dequeue();
        queue.Enqueue(line);
    }

    internal void RecordPerformance(PerformanceSample sample)
    {
        lock (_gate)
            Append(_performance, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                + " " + sample.Format(), ref _performanceTotal);
    }

    internal void RecordFailure(string stage, Exception error)
    {
        // Somente código técnico delimitado; nunca Exception.Message/StackTrace.
        string code = stage.Length is > 0 and <= 80 &&
            stage.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_')
            ? stage : "ETAPA_NAO_IDENTIFICADA";
        lock (_gate)
            Append(_failures, FormattableString.Invariant(
                $"{DateTimeOffset.UtcNow:O} etapa={code} hresult=0x{error.HResult:X8}"),
                ref _failureTotal);
    }

    // Acoplado à cadência já existente de memória. Não cria timer, não
    // enumera processos e não grava em disco em cada amostra.
    // O contexto é instantâneo; não comprova repouso durante todo o intervalo.
    internal void SampleProcess(bool tray, bool maintenance)
    {
        lock (_gate)
        {
            long now = Stopwatch.GetTimestamp();
            if (_lastTick != 0 && Stopwatch.GetElapsedTime(_lastTick, now).TotalSeconds < 10)
                return;
            try
            {
                using var process = Process.GetCurrentProcess();
                double cpu = process.TotalProcessorTime.TotalMilliseconds;
                double? elapsed = _lastTick == 0 ? null : Stopwatch.GetElapsedTime(_lastTick, now).TotalMilliseconds;
                double? percent = elapsed is > 0 && _lastCpu is double previous && cpu >= previous
                    ? 100 * (cpu - previous) / elapsed.Value / Math.Max(1, Environment.ProcessorCount) : null;
                string context = tray ? "bandeja_ou_oculto" : "visivel";
                string activity = maintenance ? "manutencao_ou_consulta" : "sem_operacao_sinalizada";
                string line = FormattableString.Invariant(
                    $"{DateTimeOffset.UtcNow:O} janela={context} estado={activity} intervalo_ms={elapsed:F0} cpu_pct_normalizada={percent:F3} cpu_acumulada_ms={cpu:F2} ram_residente_bytes={process.WorkingSet64} ram_privada_bytes={process.PrivateMemorySize64} pico_residente_sessao_bytes={process.PeakWorkingSet64} handles={process.HandleCount}");
                Append(_process, line, ref _processTotal);
                _lastCpu = cpu;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                Append(_process, "Amostra indisponível; sem valor estimado.", ref _processTotal);
                _lastCpu = null;
            }
            finally { _lastTick = now; }
        }
    }

    internal string Format()
    {
        lock (_gate)
        {
            var text = new StringBuilder();
            text.AppendLine("=== DESEMPENHO DO PROCESSO PRINCIPAL ===");
            text.AppendLine("Unidades: tempo em ms, memória em bytes, CPU normalizada pelos processadores lógicos disponíveis.");
            text.AppendLine("CPU por intervalo; primeira amostra sem percentual. Contexto instantâneo, não prova de repouso contínuo.");
            text.AppendLine("Alocações e CPU das etapas incluem atividade concorrente; pico de RAM é da sessão.");
            Section("Etapas concluídas", _performance, _performanceTotal);
            Section("Amostras do processo", _process, _processTotal);
            Section("Falhas técnicas (sem mensagens ou caminhos)", _failures, _failureTotal);
            return text.ToString();

            void Section(string title, Queue<string> entries, long total)
            {
                text.AppendLine();
                text.AppendLine($"{title}: {entries.Count} retidas de {total} nesta sessão (limite {Limit}).");
                if (entries.Count == 0) text.AppendLine("Ainda não há dados observados.");
                foreach (string line in entries) text.AppendLine(line);
            }
        }
    }

    internal static string SaveToDocuments(string report) =>
        SaveToFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), report);

    internal static string SaveToFolder(string folder, string report)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            throw new IOException("Pasta Documentos indisponível.");
        string directory = InstallerFileGuard.ValidateLocalPath(folder);
        // Falhar fechado em UNC/reparse ou diretório não confiável. Não
        // escolher outro destino nem sobrescrever um relatório anterior.
        using var pinned = InstallerFileGuard.PinDirectory(directory, allowLeafWrites: true);
        string name = $"GF-Diagnostico-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}-{Guid.NewGuid():N}.txt";
        string path = Path.Combine(directory, name);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
        {
            writer.Write(report);
            writer.Flush();
        }
        stream.Flush(flushToDisk: true);
        return path;
    }
}
