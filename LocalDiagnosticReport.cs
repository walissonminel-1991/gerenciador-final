using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace GerenciadorAplicativos;

internal enum DiagnosticUiStage { AppSearch, AppListRender, DriverSearch, DriverListRender, Navigation }

// Dados agregados da sessão, sem caminhos, nomes de aplicativos, IDs de
// dispositivos, mensagens de exceção ou leitura de arquivos pessoais.
// A exportação manual não é telemetria e não depende de consentimento remoto.
internal sealed class LocalDiagnosticReport
{
    internal static LocalDiagnosticReport Session { get; } = new();
    private const int Limit = 128;
    private readonly object _gate = new();
    private readonly Queue<string> _performance = new();
    private readonly Queue<string> _ui = new();
    private string? _startup, _initialInventory;
    private long _uiTotal;
    private readonly Queue<string> _process = new();
    private readonly Queue<string> _failures = new();
    private readonly Queue<string> _events = new();
    private long _performanceTotal, _processTotal, _failureTotal, _eventTotal;

    // Somente eventos técnicos conhecidos e campos agregados com tipos fechados.
    private static readonly HashSet<string> EventCodes = new(StringComparer.Ordinal)
    {
        "APP.INICIALIZACAO_INICIO", "JANELA.HANDLE_CRIADO", "INICIALIZACAO.BANDEJA_AUTO",
        "REGISTRO_PROGRAMAS.INICIO", "REGISTRO_PROGRAMAS.LIDO", "REGISTRO_PROGRAMAS.RENDER_INICIO",
        "REGISTRO_PROGRAMAS.RENDER_FIM", "MSIX.LEITURA", "JOGOS.LEITURA", "APLICATIVOS.ACOES",
        "MULTIFONTES.CONSULTA_INICIO", "MULTIFONTES.CONSULTA_OK", "MULTIFONTES.CONSULTA_CANCELADA",
        "DRIVERS.INVENTARIO", "DRIVERS.HISTORICO", "DRIVERS.VINCULOS", "DRIVERS.CONSULTA_CANCELADA",
        "DRIVERS.CANCELAMENTO_SOLICITADO", "WINDOWS_UPDATE.CONSULTA_OK",
        "INSTALACAO_OFICIAL.PREFLIGHT_INICIO", "INSTALACAO_OFICIAL.PREFLIGHT_BLOQUEADO",
        "INSTALACAO_OFICIAL.PREFLIGHT_APROVADO", "INSTALACAO_OFICIAL.EXECUCAO_FIM",
        "INSTALACAO_OFICIAL.VERSAO_CONFIRMADA", "INSTALACAO_OFICIAL.VERSAO_NAO_CONFIRMADA",
        "DIAGNOSTICO.EXPORTACAO_OK", "DIAGNOSTICO.EXPORTACAO_FALHA"
    };
    private static readonly HashSet<string> AggregateFields = new(StringComparer.Ordinal)
    {
        "quantidade","examinados","ocultos","updates_arp","erros","duplicados","enumerados",
        "aplicativos","adicionados","filtrados","falhas","retidos","repeticao","steam","epic",
        "parciais","inventario","desinstalaveis","ocultos_sem_acao","instalados","candidatos",
        "parcial","infs","compartilhados","versoes_divergentes","assinaturas_conflitantes",
        "salvo","base","novos","nao_observados","alterados","retorno"
    };

    internal void RecordEvent(string evt)
    {
        int separator = evt.IndexOf(' ');
        string code = separator < 0 ? evt : evt[..separator];
        if (!EventCodes.Contains(code)) return;
        var line = new StringBuilder(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
            .Append(' ').Append(code);
        if (separator >= 0)
        {
            foreach (string token in evt[(separator+1)..].Split(' ', 48, StringSplitOptions.RemoveEmptyEntries))
            {
                int equal = token.IndexOf('=');
                if (equal <= 0 || !AggregateFields.Contains(token[..equal])) continue;
                string value = token[(equal+1)..];
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
                    line.Append(' ').Append(token[..equal]).Append('=').Append(number.ToString(CultureInfo.InvariantCulture));
                else if (bool.TryParse(value, out bool flag))
                    line.Append(' ').Append(token[..equal]).Append('=').Append(flag ? "true" : "false");
            }
        }
        lock (_gate) Append(_events, line.ToString(), ref _eventTotal);
    }
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
        string line = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + sample.Format();
        lock (_gate)
        {
            // As duas referências de abertura sobrevivem a uma sessão longa.
            if (sample.Stage == PerformanceStage.Startup) _startup ??= line;
            else if (sample.Stage == PerformanceStage.InitialInventoryLoad) _initialInventory ??= line;
            else Append(_performance, line, ref _performanceTotal);
        }
    }

    // Instrumentação leve de trechos síncronos de UI: não mede o tempo
    // até a pintura efetiva, não lê o processo e não escreve no disco.
    internal IDisposable MeasureUi(DiagnosticUiStage stage, V269Page? page = null) => new UiScope(this, stage, page);

    private sealed class UiScope(LocalDiagnosticReport owner, DiagnosticUiStage stage, V269Page? page) : IDisposable
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private readonly long _allocated = GC.GetAllocatedBytesForCurrentThread();
        private readonly int _thread = Environment.CurrentManagedThreadId;
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            double elapsed = Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
            long? bytes = _thread == Environment.CurrentManagedThreadId
                ? GC.GetAllocatedBytesForCurrentThread() - _allocated : null;
            lock (owner._gate)
                Append(owner._ui, FormattableString.Invariant(
                    $"{DateTimeOffset.UtcNow:O} etapa={stage} aba={page} duracao_ms={elapsed:F3} alocacoes_thread_ui_bytes={bytes}"),
                    ref owner._uiTotal);
        }
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
            text.AppendLine("Alocações/CPU do processo incluem atividade concorrente; pico de RAM é da sessão. Alocações da thread UI são separadas; navegação mede apenas o controlador síncrono, não a pintura final.");
            text.AppendLine();
            text.AppendLine("Inicialização e primeira carga do inventário (preservadas por toda a sessão):");
            text.AppendLine(_startup ?? "Abertura: sem amostra concluída.");
            text.AppendLine(_initialInventory ?? "Primeira carga: sem amostra concluída.");
            Section("Etapas concluídas", _performance, _performanceTotal);
            Section("Pesquisa, listas e controlador síncrono de navegação", _ui, _uiTotal);
            Section("Amostras do processo", _process, _processTotal);
            Section("Eventos técnicos selecionados (somente códigos e contagens)", _events, _eventTotal);
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
        SaveToFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolderOption.DoNotVerify), report);

    internal static string SaveToFolder(string folder, string report)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new IOException("Pasta Documentos indisponível.");
        string directory = InstallerFileGuard.ValidateLocalPath(folder);
        if (!Directory.Exists(directory)) throw new IOException("Pasta Documentos indisponível.");
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
