using System.Text;
using System.Text.Json;

namespace GerenciadorAplicativos;

// Etapa 1: somente coleta LOCAL. Nao contem rotinas de rede, tokens,
// nomes de programas, hardware, usuarios, caminhos ou identificadores unicos.
internal enum TelemetryConsent { NotAsked = 0, Declined = 1, LocalOnly = 2 }

internal sealed class AnonymousTelemetry
{
    private static readonly HashSet<string> AllowedEvents = new(StringComparer.Ordinal)
    {
        "APP.FALHA_INTERFACE", "APP.FALHA_NAO_TRATADA",
        "REGISTRO_PROGRAMAS.FALHA", "MSIX.LEITURA",
        "MULTIFONTES.CONSULTA_FALHA", "MULTIFONTES.CONSULTA_CANCELADA",
        "DESINSTALACAO.FALHA", "INSTALACAO_OFICIAL.FALHA",
        "TERMINAL_GITHUB.FALHA", "JANELA.BANDEJA_RESTAURAR.INICIO",
        "JANELA.BANDEJA_RESTAURAR.FIM", "JANELA.BANDEJA_OCULTAR.INICIO",
        "JANELA.BANDEJA_OCULTAR.FIM", "DIAGNOSTICO_COMPLETO.FIM"
    };
    private const int MaximumEventsPerCategory = 100;
    private readonly object _gate = new();
    private readonly string _folder;
    private readonly SortedDictionary<string, int> _events = new(StringComparer.Ordinal);

    internal AnonymousTelemetry(string? storageFolder = null)
    {
        _folder = storageFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GerenciadorFinal", "Privacidade");
        Consent = ReadConsent();
        ExpirePendingReport();
    }

    internal TelemetryConsent Consent { get; private set; }
    internal string ConsentFile => Path.Combine(_folder, "consentimento-v1.json");
    internal string PendingFile => Path.Combine(_folder, "diagnostico-pendente-v1.json");

    private TelemetryConsent ReadConsent()
    {
        try
        {
            if (!File.Exists(ConsentFile) || new FileInfo(ConsentFile).Length > 512)
                return TelemetryConsent.NotAsked;
            using var json = JsonDocument.Parse(File.ReadAllText(ConsentFile));
            if (json.RootElement.TryGetProperty("schema", out var schema) &&
                schema.GetInt32() == 1 &&
                json.RootElement.TryGetProperty("state", out var value) &&
                value.ValueKind == JsonValueKind.Number)
                return value.GetInt32() switch
                {
                    1 => TelemetryConsent.Declined,
                    2 => TelemetryConsent.LocalOnly,
                    _ => TelemetryConsent.NotAsked
                };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            JsonException or InvalidOperationException or FormatException) { }
        return TelemetryConsent.NotAsked;
    }

    internal bool SetConsent(TelemetryConsent consent)
    {
        if (consent is not (TelemetryConsent.Declined or TelemetryConsent.LocalOnly))
            return false;
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                WriteAtomic(ConsentFile, JsonSerializer.Serialize(new { schema = 1, state = (int)consent }));
                Consent = consent;
                _events.Clear();
                if (consent != TelemetryConsent.LocalOnly)
                    TryDelete(PendingFile);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException) { return false; }
        }
    }

    internal void Record(string diagnosticEvent)
    {
        if (Consent != TelemetryConsent.LocalOnly || diagnosticEvent.Length is < 3 or > 4096)
            return;
        // Somente o codigo exato antes do primeiro espaco. Nada apos ele
        // (versoes de apps, excecoes, IDs, caminhos) pode entrar no JSON.
        int end = diagnosticEvent.IndexOfAny([' ', '\t', '\r', '\n']);
        string category = end < 0 ? diagnosticEvent : diagnosticEvent[..end];
        if (!AllowedEvents.Contains(category)) return;
        lock (_gate)
        {
            if (Consent != TelemetryConsent.LocalOnly) return;
            _events.TryGetValue(category, out int count);
            _events[category] = Math.Min(MaximumEventsPerCategory, count + 1);
        }
    }

    internal string Preview(string version, bool normalExit)
    {
        lock (_gate) return Serialize(version, normalExit);
    }

    private string Serialize(string version, bool normalExit)
    {
        // Versao validada para impedir campos arbitrarios. Sem ID de sessao,
        // hostname, IP, caminhos, inventario, timestamps ou eventos detalhados.
        if (version.Length > 30 || !System.Text.RegularExpressions.Regex.IsMatch(
            version, @"^v[0-9]+(?:\.[0-9]+){3}$"))
            version = "versao-indisponivel";
        return JsonSerializer.Serialize(new
        {
            schema = 1,
            version,
            exit = normalExit ? "normal" : "incompleto",
            counts = _events.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    internal bool SaveOnNormalExit(string version)
    {
        lock (_gate)
        {
            if (Consent != TelemetryConsent.LocalOnly) return false;
            try
            {
                Directory.CreateDirectory(_folder);
                WriteAtomic(PendingFile, Serialize(version, true));
                return true; // Guardou localmente. Nao significa envio.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException) { return false; }
        }
    }

    internal bool DeletePending()
    {
        lock (_gate) return TryDelete(PendingFile);
    }

    private void ExpirePendingReport()
    {
        try
        {
            if (!File.Exists(PendingFile)) return;
            if (Consent != TelemetryConsent.LocalOnly ||
                DateTime.UtcNow - File.GetLastWriteTimeUtc(PendingFile) > TimeSpan.FromDays(7))
                TryDelete(PendingFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static bool TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static void WriteAtomic(string path, string content)
    {
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        finally { TryDelete(temporary); }
    }
}

internal static class AnonymousTelemetryContext
{
    // Somente processo principal: trabalhadores elevados nao inicializam coleta.
    internal static AnonymousTelemetry? Current { get; set; }
}
