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
    // Reutilizada na validacao do formulario PUBLICO, sem duplicar allowlist.
    internal static bool IsAllowedCategory(string category) =>
        AllowedEvents.Contains(category);

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
            using var json = JsonDocument.Parse(ReadBoundedFile(ConsentFile, 512));
            var root = json.RootElement;
            // Falha fechada: nao aceitar campos extras, chaves repetidas ou tipos ambiguos.
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
                !root.TryGetProperty("schema", out var schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int schemaValue) || schemaValue != 1 ||
                !root.TryGetProperty("state", out var value) ||
                value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int state))
                return TelemetryConsent.NotAsked;
            return state switch
            {
                1 => TelemetryConsent.Declined,
                2 => TelemetryConsent.LocalOnly,
                _ => TelemetryConsent.NotAsked
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            JsonException or InvalidOperationException or FormatException or OverflowException or System.Security.SecurityException) { }
        return TelemetryConsent.NotAsked;
    }

    internal bool SetConsent(TelemetryConsent consent)
    {
        if (consent is not (TelemetryConsent.Declined or TelemetryConsent.LocalOnly))
            return false;
        lock (_gate)
        {
            // Revogar interrompe a coleta mesmo se o disco impedir a persistencia.
            // O retorno ainda informa falhas de gravacao ou exclusao para a interface.
            bool preserveEvents = consent == TelemetryConsent.LocalOnly && Consent == consent;
            bool deleted = true;
            if (consent == TelemetryConsent.Declined)
            {
                Consent = consent;
                _events.Clear();
                deleted = TryDelete(PendingFile);
            }
            try
            {
                EnsureStorageFolder();
                WriteAtomic(ConsentFile, JsonSerializer.Serialize(new { schema = 1, state = (int)consent }));
                Consent = consent;
                if (!preserveEvents) _events.Clear();
                return deleted;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or System.Security.SecurityException) { return false; }
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

    // Prefere a sessao atual; se ainda nao houver eventos, permite revisar a
    // unica pendencia anterior. Nenhum conteudo do disco e confiado sem validacao.
    internal string? ReviewableReport(string version)
    {
        lock (_gate)
        {
            if (Consent != TelemetryConsent.LocalOnly) return null;
            ExpirePendingReport();
            if (_events.Count > 0) return Serialize(version, normalExit: false);
            try
            {
                if (!File.Exists(PendingFile) || new FileInfo(PendingFile).Length > 4096 || PendingHasExpired())
                    return null;
                string pending = ReadBoundedFile(PendingFile, 4096);
                return GitHubDiagnosticShare.CreateReviewUrl(pending) is not null ? pending : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException) { return null; }
        }
    }

    private string Serialize(string version, bool normalExit)
    {
        // Versao validada para impedir campos arbitrarios. Sem ID de sessao,
        // hostname, IP, caminhos, inventario, timestamps ou eventos detalhados.
        if (!GitHubDiagnosticShare.IsValidVersion(version))
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
            // Uma sessao sem eventos nao apaga a pendencia util da sessao anterior.
            if (_events.Count == 0) { ExpirePendingReport(); return false; }
            try
            {
                EnsureStorageFolder();
                WriteAtomic(PendingFile, Serialize(version, true));
                return true; // Guardou localmente. Nao significa envio.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or System.Security.SecurityException) { return false; }
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
                PendingHasExpired())
                TryDelete(PendingFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }

    private bool PendingHasExpired() =>
        DateTime.UtcNow - File.GetLastWriteTimeUtc(PendingFile) > TimeSpan.FromDays(7);

    private static bool TryDelete(string path)
    {
        try { if (Directory.Exists(path)) return false; if (File.Exists(path)) File.Delete(path); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return false; }
    }

    private void EnsureStorageFolder()
    {
        void CheckAncestors()
        {
            for (var dir = new DirectoryInfo(Path.GetFullPath(_folder)); dir is not null; dir = dir.Parent)
                if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Pasta de privacidade redirecionada.");
        }
        CheckAncestors();
        Directory.CreateDirectory(_folder);
        CheckAncestors();
    }

    private static string ReadBoundedFile(string path, int maxBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maxBytes) throw new IOException("Arquivo de privacidade acima do limite.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static void WriteAtomic(string path, string content)
    {
        // CreateNew nunca trunca/segue um arquivo temporario preexistente.
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
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
