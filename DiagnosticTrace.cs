using System.Text;

namespace GerenciadorAplicativos;

// Instrumentacao temporaria para uma maquina isolada, sem dados dos aplicativos ou telemetria.
// O diagnostico nao sera iniciado se a gravacao inicial nao puder ser confirmada.
internal static class DiagnosticTrace
{
    private static readonly object Gate = new();
    private static readonly UTF8Encoding NoBom = new(false);
    private const string FileName = "GerenciadorFinal-Diagnostico-v5.12.16.log";
    private static string? _logFile;
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
            _logFile = null;
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

            foreach (string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    string candidate = Path.Combine(folder, FileName);
                    using (var file = new FileStream(candidate, FileMode.Create, FileAccess.Write,
                               FileShare.ReadWrite, 4096, FileOptions.WriteThrough))
                    using (var writer = new StreamWriter(file, NoBom, bufferSize: 1024, leaveOpen: true))
                    {
                        writer.WriteLine($"Gerenciador Final v5.12.16 | {DateTimeOffset.Now:O}");
                        writer.WriteLine("Registro local de etapas; sem nomes de aplicativos nem saida de comandos.");
                        writer.WriteLine("Ultima etapa nao determina a causa do alerta do antivirus.");
                        writer.Flush();
                        file.Flush(flushToDisk: true);
                    }
                    _logFile = candidate;
                    _lastFailure = "";
                    return true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    // Nao oculta o erro: a mensagem e exibida ao usuario se nenhuma pasta funcionar.
                    _lastFailure = ex.GetType().Name;
                }
            }
            return false;
        }
    }

    public static bool Write(string evt)
    {
        lock (Gate)
        {
            if (_logFile is null) return false;
            try
            {
                using var stream = new FileStream(_logFile, FileMode.Append, FileAccess.Write,
                    FileShare.ReadWrite, 1024, FileOptions.WriteThrough);
                using var writer = new StreamWriter(stream, NoBom, bufferSize: 1024, leaveOpen: true);
                writer.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {evt}");
                writer.Flush();
                stream.Flush(flushToDisk: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                _lastFailure = ex.GetType().Name;
                return false;
            }
        }
    }

    public static void Failure(string stage, Exception ex)
    {
        // Mensagens completas de excecao podem conter caminhos com dados pessoais.
        Write($"{stage} excecao={ex.GetType().Name} hresult=0x{ex.HResult:X8}");
    }
}
