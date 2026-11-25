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
    private readonly DiagnosticRecoveryJournal _recovery = new();
    internal void StartRecovery(string diagnosticPath) => _recovery.Start(Path.GetDirectoryName(diagnosticPath)!);
    internal void CloseRecovery(bool normal) => _recovery.Close(normal);
    private long _lastUiTick;
    private int _previousUiInterval;
    internal void ObserveUiCadence(int expectedMs)
    {
        long now=Stopwatch.GetTimestamp();
        lock(_gate)
        {
            if(_lastUiTick!=0)
            {
                double elapsed=Stopwatch.GetElapsedTime(_lastUiTick,now).TotalMilliseconds;
                int expected=Math.Max(expectedMs,_previousUiInterval);
                if(elapsed>expected+5000d)
                {
                    Append(_events,FormattableString.Invariant($"{DateTimeOffset.UtcNow:O} UI.LACUNA intervalo_ms={elapsed:F0} esperado_ms={expected} (não prova travamento)"),ref _eventTotal);
                    _recovery.Record(DiagnosticRecoveryKind.UiDelay,(long)elapsed,expected,checkpoint:true);
                }
            }
            _lastUiTick=now;_previousUiInterval=expectedMs;
        }
    }
    private const long JournalLimit = 16 * 1024 * 1024;
    private FileStream? _journalStream;
    private StreamWriter? _journal;
    private IDisposable? _journalDirectory;
    private long _journalBytes, _journalWritten, _journalOmitted;
    private bool _journalFailed;
    private readonly DateTimeOffset _sessionStart = DateTimeOffset.UtcNow;

    // Arquivo temporário privado da sessão, sem upload, com buffer e limite
    // explícito. O histórico recente em RAM é fallback, não substitui a
    // cronologia completa. Não deixar um arquivo temporário após fechar.
    internal void StartJournal(string diagnosticPath)
    {
        lock (_gate)
        {
            if (_journal is not null || _journalFailed) return;
            try
            {
                string directory = InstallerFileGuard.ValidateLocalPath(Path.GetDirectoryName(diagnosticPath)!);
                _journalDirectory = InstallerFileGuard.PinDirectory(directory, allowLeafWrites: true);
                _journalStream = new FileStream(Path.Combine(directory,"GF-session-"+Guid.NewGuid().ToString("N")+".tmp"),
                    FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,16384,FileOptions.DeleteOnClose);
                _journal = new StreamWriter(_journalStream,new UTF8Encoding(false),8192,leaveOpen:true);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or System.Security.SecurityException)
            {
                _journalFailed = true;
                CloseJournal();
            }
        }
    }

    internal void CloseJournal()
    {
        lock (_gate)
        {
            try { _journal?.Dispose(); } catch (IOException) { _journalFailed = true; }
            finally
            {
                _journal = null;
                try { _journalStream?.Dispose(); } catch (IOException) { _journalFailed = true; }
                finally { _journalStream = null; _journalDirectory?.Dispose(); _journalDirectory = null; }
            }
        }
    }

    private void WriteJournal(string line)
    {
        if (_journal is null || _journalFailed) { _journalOmitted++; return; }
        int size = Encoding.UTF8.GetByteCount(line) + Encoding.UTF8.GetByteCount(Environment.NewLine);
        if (_journalBytes + size > JournalLimit) { _journalOmitted++; return; }
        try
        {
            _journal.WriteLine(line);
            _journalBytes += size;
            _journalWritten++;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or UnauthorizedAccessException)
        { _journalFailed = true; _journalOmitted++; }
    }

    private void AddJournalTo(StringBuilder text)
    {
        text.AppendLine();
        text.AppendLine("=== CRONOLOGIA DA SESSÃO ATÉ A GERAÇÃO DO RELATÓRIO ===");
        text.AppendLine($"Início UTC: {_sessionStart:O}; eventos gravados: {_journalWritten}; omitidos: {_journalOmitted}; limite: {JournalLimit} bytes.");
        if (_journal is null || _journalStream is null || _journalFailed)
        {
            text.AppendLine("AVISO: cronologia integral indisponível ou falhou; consultar históricos recentes e totais.");
            return;
        }
        try
        {
            _journal.Flush();
            _journalStream.Position=0;
            using var reader = new StreamReader(_journalStream,Encoding.UTF8,false,8192,leaveOpen:true);
            string? line;
            while ((line=reader.ReadLine()) is not null) text.AppendLine(line);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or UnauthorizedAccessException)
        { _journalFailed=true; text.AppendLine("AVISO: leitura da cronologia incompleta."); }
        finally
        {
            try { _journalStream.Seek(0,SeekOrigin.End); }
            catch (IOException) { _journalFailed=true; }
        }
        if (_journalOmitted>0) text.AppendLine("AVISO: nem todos os eventos couberam no limite da cronologia.");
    }
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
    private long _lastHardwareTick;
    private double? _lastCpu;
    private readonly Queue<string> _hardware = new();
    private long _hardwareTotal;

    internal void RecordDriverAuditPhase(DriverStoreAudit.AuditPhase phase,bool returned,long elapsedMs)
    {
        lock(_gate)
            Append(_events,FormattableString.Invariant(
                $"{DateTimeOffset.UtcNow:O} DRIVER_AUDIT fase={phase} estado={(returned?"retorno":"inicio")} duracao_ms={elapsedMs}"),ref _eventTotal);
    }

    internal void RecordOperation(V269Page page, string state, bool? complete = null)
    {
        // Estados fechados; nenhum título de aplicativo, comando ou descrição de usuário.
        if (state is not ("inicio" or "fim" or "resultado" or "bloqueado" or "cancelado" or "confirmado" or "sem_desfecho")) return;
        lock (_gate)
        {
            Append(_events, FormattableString.Invariant(
                $"{DateTimeOffset.UtcNow:O} operacao aba={page} estado={state} resultado_confirmado={complete}"),ref _eventTotal);
            var kind=state switch
            {
                "inicio"=>DiagnosticRecoveryKind.OperationBegin,
                "fim"=>DiagnosticRecoveryKind.OperationEnd,
                "confirmado"=>DiagnosticRecoveryKind.ConfirmationAccepted,
                "cancelado"=>DiagnosticRecoveryKind.ConfirmationCanceled,
                "bloqueado"=>DiagnosticRecoveryKind.OperationBlocked,
                "sem_desfecho"=>DiagnosticRecoveryKind.OutcomeMissing,
                _=>DiagnosticRecoveryKind.OperationResult
            };
            _recovery.Record(kind,(long)page,complete==true?1:complete==false?0:-1,checkpoint:true);
        }
    }

    internal void SampleHardware(HardwareSensorPortfolio snapshot)
    {
        lock (_gate)
        {
            long now=Stopwatch.GetTimestamp();
            if(_lastHardwareTick!=0&&Stopwatch.GetElapsedTime(_lastHardwareTick,now).TotalSeconds<10)return;
            _lastHardwareTick=now;
            var counters=snapshot.WindowsCounters;
            var sensors=counters.Sensors;
            string line=FormattableString.Invariant(
                $"{DateTimeOffset.UtcNow:O} MONITOR fotografia_utc={snapshot.CapturedAtUtc:O} amostra_cpu_utc={counters.CapturedAtUtc:O} cpu_pct={Reading(counters.CpuUsagePercent)} cpu_mhz={Reading(counters.CpuFrequencyMhz)} cpu_c={Reading(sensors.CpuTemperatureC)} cpu_w={Reading(sensors.CpuPowerWatts)} cpu_rpm={Reading(sensors.CpuFanRpm)} ram_usada_bytes={counters.Memory?.UsedBytes} ram_total_bytes={counters.Memory?.TotalBytes}");
            Append(_hardware,line,ref _hardwareTotal);
            // WDDM e D3DKMT não são combinados por posição. Não registrar LUID,
            // ID PnP, nomes de dispositivos ou valores NVML como RPM físicos.
            int i=0;
            foreach(var gpu in snapshot.D3dKmtGpuSensors?.Adapters??[])
                Append(_hardware,FormattableString.Invariant(
                    $"{DateTimeOffset.UtcNow:O} MONITOR fonte=D3DKMT adaptador_na_fotografia={++i} temperatura_c={Reading(gpu.TemperatureC)} rpm={Reading(gpu.FanRpm)} potencia_relativa_pct={Reading(gpu.RelativePowerPercent)} memoria_mhz={Reading(gpu.MemoryFrequencyMhz)}"),ref _hardwareTotal);
            i=0;
            foreach(var gpu in snapshot.NvidiaGpus.Devices)
                Append(_hardware,FormattableString.Invariant(
                    $"{DateTimeOffset.UtcNow:O} MONITOR fonte=NVML adaptador_na_fotografia={++i} uso_pct={Reading(gpu.UsagePercent)} temperatura_c={Reading(gpu.TemperatureC)} potencia_w={Reading(gpu.PowerWatts)} ventoinha_pct={Reading(gpu.FanPercent)} vram_usada_bytes={Reading(gpu.VramUsedBytes)}"),ref _hardwareTotal);
        }
    }

    private static string Reading<T>(MonitorReading<T> reading) where T:struct =>
        reading.Availability==MonitorAvailability.Available&&reading.Value is T value
        ? Convert.ToString(value,CultureInfo.InvariantCulture)??"N/D" : reading.Availability.ToString();

    internal static string ModelText(string? value)
    {
        // Somente campos fabricante/modelo de fontes de hardware, nunca
        // FriendlyName customizável, serial, UUID, host, caminho ou DeviceId.
        string? s=value?.Trim();
        if(string.IsNullOrWhiteSpace(s)||s.Length>160||
            s.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is ' ' or '-' or '_' or '.' or '(' or ')' or '+' or ','))||
            s.Equals(Environment.MachineName,StringComparison.OrdinalIgnoreCase)||
            s.Equals(Environment.UserName,StringComparison.OrdinalIgnoreCase)||
            s.Equals("To Be Filled By O.E.M.",StringComparison.OrdinalIgnoreCase)||
            s.Equals("Default string",StringComparison.OrdinalIgnoreCase)||
            s.Equals("Unknown",StringComparison.OrdinalIgnoreCase)||
            Guid.TryParse(s,out _)||
            (s.Length>=12&&s.All(char.IsAsciiHexDigit)))return "N/D";
        return s;
    }

    internal static string FormatWindowsConfiguration()
    {
        var text=new StringBuilder("=== CONFIGURAÇÃO DO WINDOWS ==="+Environment.NewLine);
        try
        {
            using var os=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            text.AppendLine("Edição: "+ModelText(os?.GetValue("EditionID") as string));
            text.AppendLine("Versão: "+ModelText(os?.GetValue("DisplayVersion") as string));
            text.AppendLine("Tipo de instalação: "+ModelText(os?.GetValue("InstallationType") as string));
            text.AppendLine("Compilação: "+Environment.OSVersion.Version.Build+
                (os?.GetValue("UBR") is int ubr&&ubr>=0?"."+ubr:" (revisão N/D)"));
            using var boot=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            text.AppendLine("Secure Boot: "+(boot?.GetValue("UEFISecureBootEnabled") switch{1=>"ativo",0=>"inativo",_=>"N/D"}));
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {text.AppendLine("Parte da configuração indisponível.");}
        return text.ToString();
    }

    internal static string FormatHardware(HardwareSensorPortfolio snapshot)
    {
        var text=new StringBuilder();
        text.AppendLine("=== CONFIGURAÇÃO DE HARDWARE (fontes declaradas, sem identificadores únicos) ===");
        text.AppendLine($"Fotografia UTC: {snapshot.CapturedAtUtc:O}; valores podem estar desatualizados.");
        var platform=snapshot.PlatformHardware;
        int i=0;
        foreach(var cpu in platform?.CpuSockets??[])
            text.AppendLine($"CPU {++i}: modelo={ModelText(cpu.ReportedName)}; núcleos={cpu.PhysicalCores}; threads={cpu.LogicalProcessors}; virtualização firmware={cpu.FirmwareVirtualizationEnabled}; frequência reportada MHz={Reading(cpu.ReportedCurrentMhz)}");
        if(i==0)text.AppendLine("CPU: modelo indisponível.");
        i=0;
        foreach(var board in platform?.Motherboards??[])
            text.AppendLine($"Placa-mãe {++i}: fabricante={ModelText(board.Manufacturer)}; modelo={ModelText(board.Product)}; revisão={ModelText(board.Version)}");
        if(i==0)text.AppendLine("Placa-mãe: modelo indisponível.");
        text.AppendLine("BIOS: "+ModelText(platform?.ReportedBiosVersion));
        i=0;
        foreach(var ram in platform?.RamModules??[])
            text.AppendLine($"RAM módulo {++i}: fabricante={ModelText(ram.ReportedManufacturer)}; modelo/part number={ModelText(ram.ReportedPartNumber)}; capacidade bytes={Reading(ram.CapacityBytes)}; clock configurado MHz={Reading(ram.ConfiguredMhz)}; tipo SMBIOS={ram.SmbiosMemoryType}");
        if(i==0)text.AppendLine("RAM: módulos não identificados.");
        i=0;
        foreach(var gpu in snapshot.DxgiAdapters?.Adapters.Where(x=>!x.IsSoftware)??[])
        {
            string vendor=gpu.VendorId switch{0x10DE=>"NVIDIA",0x1002=>"AMD",0x8086=>"Intel",_=>"N/D"};
            text.AppendLine($"GPU {++i}: fabricante chip={vendor}; modelo DXGI={ModelText(gpu.Name)}; VRAM dedicada bytes={gpu.DedicatedVideoMemory}; heap de VRAM acessível à CPU reportado (não confirma ReBAR)={gpu.DiscreteGpuUploadHeapSupported}");
        }
        if(i==0)text.AppendLine("GPU: modelo DXGI indisponível.");
        if(snapshot.NvidiaGpus.NvidiaDriverVersion is { } nvDriver)
            text.AppendLine("Versão driver NVIDIA (NVML): "+ModelText(nvDriver));
        i=0;
        foreach(var gpu in snapshot.DeviceInventory.Devices.Where(x=>x.Kind==HardwareDeviceKind.Graphics))
            text.AppendLine($"GPU PnP {++i}: driver instalado={ModelText(gpu.InstalledDriverVersion)}; barramento reportado={ModelText(gpu.ReportedPcieLink)}");
        i=0;
        foreach(var disk in snapshot.Storage.Disks)
            text.AppendLine($"Disco físico {++i}: fabricante={ModelText(disk.ReportedManufacturer)}; modelo={ModelText(disk.ReportedModel)}; temperatura C={Reading(disk.TemperatureC)}; desgaste reportado pct={Reading(disk.WearPercent)}; horas={Reading(disk.PowerOnHours)}");
        if(i==0)text.AppendLine("Discos físicos: modelo indisponível.");
        text.AppendLine("=== PERIFÉRICOS PnP (descrição do driver, não identificação comercial garantida) ===");
        text.AppendLine("Não são usados nomes amigáveis, nomes de impressoras em rede, endereços, IDs ou números de série.");
        var peripherals=snapshot.DeviceInventory.Devices.Where(x=>x.Kind is HardwareDeviceKind.Network or
            HardwareDeviceKind.Audio or HardwareDeviceKind.Bluetooth or HardwareDeviceKind.Usb or
            HardwareDeviceKind.Input or HardwareDeviceKind.Monitor or HardwareDeviceKind.Camera or
            HardwareDeviceKind.Battery or HardwareDeviceKind.Sensor ||
            (x.Kind==HardwareDeviceKind.Printer&&x.SetupClass.Equals("Printer",StringComparison.OrdinalIgnoreCase)))
            .Where(x=>!x.SetupClass.Equals("AudioEndpoint",StringComparison.OrdinalIgnoreCase))
            .GroupBy(x=>(x.Kind,Manufacturer:ModelText(x.Manufacturer),Description:ModelText(x.ReportedDeviceDescription)))
            .OrderBy(x=>x.Key.Kind).ThenBy(x=>x.Key.Manufacturer,StringComparer.Ordinal)
            .ThenBy(x=>x.Key.Description,StringComparer.Ordinal).ToArray();
        foreach(var group in peripherals.Take(128))
            text.AppendLine($"{group.Key.Kind}: fabricante={group.Key.Manufacturer}; descrição={group.Key.Description}; quantidade={group.Count()}");
        if(peripherals.Length==0)text.AppendLine("Periféricos: sem descrição técnica disponível.");
        if(peripherals.Length>128)text.AppendLine($"AVISO: {peripherals.Length-128} grupos adicionais não incluídos.");
        return text.ToString();
    }

    private void Append(Queue<string> queue, string line, ref long total)
    {
        WriteJournal(line);
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
            if (sample.Stage == PerformanceStage.Startup) { _startup ??= line; WriteJournal(line); }
            else if (sample.Stage == PerformanceStage.InitialInventoryLoad) { _initialInventory ??= line; WriteJournal(line); }
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
            {
                owner.Append(owner._ui, FormattableString.Invariant(
                    $"{DateTimeOffset.UtcNow:O} etapa={stage} aba={page} duracao_ms={elapsed:F3} alocacoes_thread_ui_bytes={bytes}"),
                    ref owner._uiTotal);
                if(stage==DiagnosticUiStage.Navigation&&page.HasValue)
                    owner._recovery.Record(DiagnosticRecoveryKind.Navigation,(long)page.Value,(long)elapsed);
            }
        }
    }

    internal void RecordFailure(string stage, Exception error)
    {
        // Somente código técnico delimitado; nunca Exception.Message/StackTrace.
        string code = stage.Length is > 0 and <= 80 &&
            stage.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_')
            ? stage : "ETAPA_NAO_IDENTIFICADA";
        // A localização do defeito usa somente símbolos do assembly GF,
        // nunca StackTrace textual, arquivo fonte, linha de usuário ou parâmetros.
        string type = error.GetType().FullName ?? "Exception";
        if(type.Length>120||type.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '.' or '_' or '+' or '`')))
            type="Exception";
        string[] frames=(new StackTrace(error,false).GetFrames()??[])
            .Select(frame=>frame.GetMethod())
            .Where(method=>method?.DeclaringType?.Assembly==typeof(LocalDiagnosticReport).Assembly)
            .Take(8)
            .Select(method=>method!.DeclaringType!.Name+"."+method.Name)
            .Where(name=>name.Length<=200&&name.All(c=>char.IsAsciiLetterOrDigit(c)||c is '.' or '_' or '+' or '`' or '<' or '>' or '-'))
            .ToArray();
        lock (_gate)
        {
            Append(_failures, FormattableString.Invariant(
                $"{DateTimeOffset.UtcNow:O} etapa={code} tipo={type} hresult=0x{error.HResult:X8} simbolos_gf={string.Join(",",frames)}"),
                ref _failureTotal);
            _recovery.Record(DiagnosticRecoveryKind.Failure,error.HResult,checkpoint:true);
        }
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
                _recovery.Record(DiagnosticRecoveryKind.ProcessSample,(long)cpu,process.WorkingSet64,process.PrivateMemorySize64,checkpoint:true);
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
            Section("Amostras parciais de sensores (fontes separadas, sem nova consulta)", _hardware, _hardwareTotal);
            Section("Eventos técnicos selecionados (somente códigos e contagens)", _events, _eventTotal);
            Section("Falhas técnicas (sem mensagens ou caminhos)", _failures, _failureTotal);
            AddJournalTo(text);
            text.AppendLine(_recovery.FormatPrevious());
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
