using System.Text;

namespace GerenciadorAplicativos;

// Instrumentacao temporaria para uma maquina isolada, sem dados dos aplicativos ou telemetria.
// O diagnostico nao sera iniciado se a gravacao inicial nao puder ser confirmada.
internal static class DiagnosticTrace
{
    private static readonly object Gate = new();
    private static readonly UTF8Encoding NoBom = new(false);
    // Versionamento vem do assembly compilado para impedir logs com versao
    // antiga quando o executavel muda. Nao grava caminhos pessoais no conteudo.
    private static readonly Version BuildVersion =
        typeof(DiagnosticTrace).Assembly.GetName().Version ?? new Version(0, 0, 0);
    internal static string FormatVersion(Version version) =>
        $"v{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}.{Math.Max(version.Revision, 0)}";
    internal static readonly string VersionLabel = FormatVersion(BuildVersion);
    private static readonly string FileName =
        $"GerenciadorFinal-Diagnostico-{VersionLabel}.log";
    private static string? _logFile;
    // O handle de leitura nega remoção/renomeação por toda a sessão, sem
    // impedir que leitores comuns consultem/copiem o log entre gravações.
    private static FileStream? _identityLock;
    private static IDisposable? _directoryLock;
    private static string _lastFailure = "O arquivo de diagnostico nao foi iniciado.";

    public static string LogFilePath
    {
        get { lock (Gate) return _logFile ?? "(indisponivel)"; }
    }

    public static string LastFailure
    {
        get { lock (Gate) return _lastFailure; }
    }

    // Grava no perfil local por padrao. Se nao for gravavel, usa a pasta temporaria
    // do processo. O caminho REAL e exibido na interface (nao um %TEMP% ficticio).
    public static bool Start()
    {
        lock (Gate)
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string temp;
            try { temp = Path.GetTempPath(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                temp = "";
                _lastFailure = ex.GetType().Name;
            }
            var folders = new List<string>();
            if (!string.IsNullOrWhiteSpace(localData))
                folders.Add(Path.Combine(localData, "GerenciadorFinal", "Diagnosticos"));
            if (!string.IsNullOrWhiteSpace(temp)) folders.Add(temp);

            return StartInFolders(folders);
        }
    }

    // Separado para testar falhas e links apenas em diretórios sintéticos.
    internal static bool StartInFolders(IEnumerable<string> folders)
    {
        lock (Gate)
        {
            Stop();
            foreach (string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                FileStream? file = null;
                FileStream? identityLock = null;
                IDisposable? directoryLock = null;
                try
                {
                    InstallerFileGuard.ValidateLocalPath(folder);
                    Directory.CreateDirectory(folder);
                    directoryLock = InstallerFileGuard.PinDirectory(folder, allowLeafWrites: true);
                    string candidate = Path.Combine(folder, FileName);
                    // Apagar a entrada não segue o link/hard link da folha.
                    // CreateNew recusa qualquer entrada inserida antes da abertura;
                    // nunca truncar conteúdo de um arquivo preexistente por nome.
                    File.Delete(candidate);
                    file = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write,
                        FileShare.ReadWrite, 4096, FileOptions.WriteThrough);
                    using (var writer = new StreamWriter(file, NoBom, bufferSize: 1024, leaveOpen: true))
                    {
                        writer.WriteLine($"Gerenciador Final {VersionLabel} | {DateTimeOffset.Now:O}");
                        writer.WriteLine("Registro local de etapas; sem nomes de aplicativos nem saida de comandos.");
                        writer.WriteLine("Ultima etapa nao determina a causa do alerta do antivirus.");
                        // Dados tecnicos locais para comparar ambientes; sem identificadores pessoais.
                        writer.WriteLine($"AMBIENTE Windows={Environment.OSVersion.Version} " +
                            $"sistema={System.Runtime.InteropServices.RuntimeInformation.OSArchitecture} " +
                            $"processo={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} " +
                            $"runtime={Environment.Version}");
                        writer.Flush();
                        file.Flush(flushToDisk: true);
                    }
                    identityLock = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (!SameIdentity(InstallerFileGuard.ReadIdentity(file), InstallerFileGuard.ReadIdentity(identityLock)))
                        throw new IOException("Identidade do arquivo de diagnóstico alterada.");
                    _identityLock = identityLock;
                    _directoryLock = directoryLock;
                    identityLock = null;
                    directoryLock = null;
                    _logFile = candidate;
                    _lastFailure = "";
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                    NotSupportedException or System.Security.SecurityException)
                {
                    // Nao oculta o erro: a mensagem e exibida ao usuario se nenhuma pasta funcionar.
                    _lastFailure = ex.GetType().Name;
                }
                finally { file?.Dispose(); identityLock?.Dispose(); directoryLock?.Dispose(); }
            }
            return false;
        }
    }

    internal static void Stop()
    {
        lock (Gate)
        {
            try { _identityLock?.Dispose(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _lastFailure = ex.GetType().Name; }
            finally
            {
                _identityLock = null;
                _logFile = null;
                _directoryLock?.Dispose();
                _directoryLock = null;
            }
        }
    }

    public static bool Write(string evt)
    {
        lock (Gate)
        {
            if (_identityLock is null || _logFile is null) return false;
            try
            {
                // Open não cria/trunca. Só escrever depois de conferir o handle
                // real; mesmo uma alteração de reparse em uso deve falhar fechada.
                using var stream = new FileStream(_logFile, FileMode.Open, FileAccess.Write,
                    FileShare.ReadWrite, 1024, FileOptions.WriteThrough);
                if (!SameIdentity(InstallerFileGuard.ReadIdentity(_identityLock), InstallerFileGuard.ReadIdentity(stream)))
                    throw new IOException("Identidade do arquivo de diagnóstico alterada.");
                stream.Seek(0, SeekOrigin.End);
                using var writer = new StreamWriter(stream, NoBom, bufferSize: 1024, leaveOpen: true);
                writer.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {evt}");
                // So categorias em allowlist sao somadas, e apenas apos consentimento.
                // Nao enviar linhas brutas nem textos de excecoes para telemetria.
                AnonymousTelemetryContext.Current?.Record(evt);
                writer.Flush();
                stream.Flush(flushToDisk: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                NotSupportedException or System.Security.SecurityException)
            {
                _lastFailure = ex.GetType().Name;
                return false;
            }
        }
    }

    private static bool SameIdentity(InstallerFileGuard.FileIdentity first, InstallerFileGuard.FileIdentity second) =>
        first.Volume == second.Volume && first.Index == second.Index;

    public static void Failure(string stage, Exception ex)
    {
        // Mensagens completas de excecao podem conter caminhos com dados pessoais.
        Write($"{stage} excecao={ex.GetType().Name} hresult=0x{ex.HResult:X8}");
    }
}
