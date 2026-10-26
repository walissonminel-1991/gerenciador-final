using System.Text;
using System.Text.Json;

namespace GerenciadorAplicativos;

// Etapa 1: somente coleta LOCAL. Nao contem rotinas de rede, tokens,
// nomes de programas, hardware, usuarios, caminhos ou identificadores unicos.
internal enum TelemetryConsent { NotAsked = 0, Declined = 1, LocalOnly = 2, RemoteBeta = 3 }

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
        // Sem criar pasta antes da escolha. Limpeza inicial tambem participa
        // da coordenacao, para nao apagar pendencia criada por outra instancia.
        if (Directory.Exists(_folder))
        {
            using var state = TryStorageLock("estado-v1.lock");
            if (state is not null) { Consent = ReadConsent(); ExpirePendingReport(); }
        }
    }

    private TelemetryConsent _consent;
    private CancellationTokenSource? _remoteSend;
    private long _generation;
    internal TelemetryConsent Consent { get { lock (_gate) return _consent; } private set { _consent = value; } }
    internal string ConsentFile => Path.Combine(_folder, "consentimento-v1.json");
    internal string PendingFile => Path.Combine(_folder, "diagnostico-pendente-v1.json");

    private FileStream? TryStorageLock(string name)
    {
        try { return InstallerFileGuard.OpenCoordinationLock(Path.Combine(_folder, name)); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException) { return null; }
    }

    // Uma escolha mais restritiva em memoria nunca e ampliada por leitura do
    // disco. Mudanca externa descarta eventos anteriores, inclusive LocalOnly.
    private bool RefreshStoredConsent()
    {
        var stored = ReadConsent();
        if (stored == Consent) return true;
        _events.Clear();
        _generation++;
        try { _remoteSend?.Cancel(); } catch (AggregateException) { }
        if (stored is TelemetryConsent.NotAsked or TelemetryConsent.Declined ||
            (Consent == TelemetryConsent.RemoteBeta && stored == TelemetryConsent.LocalOnly))
            Consent = stored;
        return false;
    }

    private TelemetryConsent ReadConsent()
    {
        try
        {
            InstallerFileGuard.ValidateLocalPath(ConsentFile);
            if (!File.Exists(ConsentFile) || new FileInfo(ConsentFile).Length > 512)
                return TelemetryConsent.NotAsked;
            using var json = JsonDocument.Parse(ReadBoundedFile(ConsentFile, 512));
            var root = json.RootElement;
            // Falha fechada: nao aceitar campos extras, chaves repetidas ou tipos ambiguos.
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schema", out var schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int schemaValue) || schemaValue is not (1 or 2) ||
                !root.TryGetProperty("state", out var value) ||
                value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int state))
                return TelemetryConsent.NotAsked;
            int fields = root.EnumerateObject().Count();
            if (schemaValue == 2)
            {
                if (fields != 4 || state != 3 ||
                    !root.TryGetProperty("notice_version", out var notice) || notice.ValueKind != JsonValueKind.String ||
                    notice.GetString() != TelemetryConsentPolicy.Version ||
                    !root.TryGetProperty("notice_sha256", out var hash) || hash.ValueKind != JsonValueKind.String ||
                    hash.GetString() != TelemetryConsentPolicy.NoticeHash) return TelemetryConsent.NotAsked;
                return TelemetryConsent.RemoteBeta;
            }
            if (fields != 2) return TelemetryConsent.NotAsked;
            return state switch
            {
                1 => TelemetryConsent.Declined,
                2 => TelemetryConsent.LocalOnly,
                3 => TelemetryConsent.NotAsked, // aceite remoto antigo nao abrange o aviso atual
                _ => TelemetryConsent.NotAsked
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            JsonException or InvalidOperationException or FormatException or OverflowException or System.Security.SecurityException) { }
        return TelemetryConsent.NotAsked;
    }

    internal bool SetConsent(TelemetryConsent consent)
    {
        if (consent is not (TelemetryConsent.Declined or TelemetryConsent.LocalOnly or TelemetryConsent.RemoteBeta))
            return false;
        lock (_gate)
        {
            // Revogar interrompe a coleta mesmo se o disco impedir a persistencia.
            // O retorno ainda informa falhas de gravacao ou exclusao para a interface.
            bool preserveEvents = consent != TelemetryConsent.Declined && Consent == consent;
            bool deleted = true;
            // Reduzir consentimento e imediato. Ampliar de recusa/NotAsked
            // para coleta local so pode vigorar depois da gravacao bem-sucedida.
            if (consent == TelemetryConsent.Declined ||
                (consent == TelemetryConsent.LocalOnly && Consent == TelemetryConsent.RemoteBeta))
            {
                Consent = consent;
                _generation++;
                try { _remoteSend?.Cancel(); } catch (AggregateException) { }
            }
            if (consent == TelemetryConsent.Declined) _events.Clear();
            try { EnsureStorageFolder(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or System.Security.SecurityException) { return false; }
            using var state = TryStorageLock("estado-v1.lock");
            if (state is null) return false; // nao esperar nem bloquear a revogacao em memoria
            if (consent == TelemetryConsent.Declined) deleted = TryDelete(PendingFile);
            // A autorizacao remota nao abrange relatorios coletados quando
            // o usuario havia autorizado apenas armazenamento local.
            // Falha de exclusao bloqueia a transicao: nunca enviar pendencia antiga.
            if (consent == TelemetryConsent.RemoteBeta &&
                Consent != TelemetryConsent.RemoteBeta && !TryDelete(PendingFile))
                return false;
            try
            {
                EnsureStorageFolder();
                string choice = consent == TelemetryConsent.RemoteBeta
                    ? JsonSerializer.Serialize(new { schema = 2, state = 3,
                        notice_version = TelemetryConsentPolicy.Version, notice_sha256 = TelemetryConsentPolicy.NoticeHash })
                    : JsonSerializer.Serialize(new { schema = 1, state = (int)consent });
                WriteAtomic(ConsentFile, choice);
                Consent = consent;
                if (!preserveEvents) _events.Clear();
                return deleted;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or System.Security.SecurityException) { return false; }
        }
    }

    internal void Record(string diagnosticEvent)
    {
        if (Consent is not (TelemetryConsent.LocalOnly or TelemetryConsent.RemoteBeta) || diagnosticEvent.Length is < 3 or > 4096)
            return;
        // Somente o codigo exato antes do primeiro espaco. Nada apos ele
        // (versoes de apps, excecoes, IDs, caminhos) pode entrar no JSON.
        int end = diagnosticEvent.IndexOfAny([' ', '\t', '\r', '\n']);
        string category = end < 0 ? diagnosticEvent : diagnosticEvent[..end];
        if (!AllowedEvents.Contains(category)) return;
        lock (_gate)
        {
            if (Consent is not (TelemetryConsent.LocalOnly or TelemetryConsent.RemoteBeta) || !RefreshStoredConsent()) return;
            _events.TryGetValue(category, out int count);
            _events[category] = Math.Min(MaximumEventsPerCategory, count + 1);
        }
    }

    internal string Preview(string version, bool normalExit)
    {
        lock (_gate) return Serialize(version, normalExit);
    }
    // Texto voltado a iniciantes: apenas o resumo ja autorizado, sem campos brutos.
    // Nunca muda consentimento, nao cria arquivo e nao consulta a rede.
    internal string ExplainLocalCollection(string version)
    {
        lock (_gate)
        {
            if (Consent is not (TelemetryConsent.LocalOnly or TelemetryConsent.RemoteBeta))
                return "COLETA DESATIVADA\n\n" +
                    "Nenhuma ocorrência foi registrada por este recurso.\n" +
                    "Você pode simular com dados fictícios sem ativar a coleta.";

            var explanation = new StringBuilder();
            explanation.AppendLine(Consent == TelemetryConsent.RemoteBeta ? "BETA: RESUMO E ENVIO AUTORIZADOS" : "COLETA LOCAL ATIVA — NENHUM ENVIO");
            explanation.AppendLine();
            explanation.AppendLine("Versão do programa: " +
                (DiagnosticPayloadPolicy.IsValidVersion(version) ? version : "nao disponivel"));
            if (_events.Count == 0)
            {
                explanation.AppendLine("Nenhuma ocorrência técnica registrada nesta sessão.");
            }
            else
            {
                explanation.AppendLine("Ocorrências técnicas desta sessão:");
                foreach (var entry in _events)
                    explanation.AppendLine("- " + HumanCategory(entry.Key) + ": " +
                        entry.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            explanation.AppendLine();
            explanation.AppendLine("Não inclui nomes de programas, arquivos, senhas, usuário nem histórico de navegação.");
            explanation.AppendLine(Consent == TelemetryConsent.RemoteBeta ? "Resumo enviado apenas sob consentimento, ao fechar, via HTTPS privado; revogável na bandeja." : "O relatório permanece neste computador e não é enviado automaticamente.");
            return explanation.ToString();
        }
    }

    // Demonstracao deterministica, 100% ficticia: nao usa _events, _folder,
    // arquivos, nome de maquina ou chamadas HTTP. Funciona mesmo sem consentir.
    internal static string SimulateContributionWithoutSending(string version)
    {
        if (!DiagnosticPayloadPolicy.IsValidVersion(version))
            throw new ArgumentException("Versao do programa invalida para simulacao.", nameof(version));

        string syntheticReport = JsonSerializer.Serialize(new
        {
            schema = 1,
            version,
            exit = "normal",
            counts = new SortedDictionary<string, int>(StringComparer.Ordinal)
            {
                ["APP.FALHA_INTERFACE"] = 1,
                ["MULTIFONTES.CONSULTA_FALHA"] = 2
            }
        });
        if (!DiagnosticPayloadPolicy.IsValidReport(syntheticReport))
            throw new InvalidOperationException("Simulacao bloqueada pela politica de privacidade.");

        return "SIMULAÇÃO CONCLUÍDA — NADA FOI ENVIADO\n\n" +
            "EXEMPLO COM DADOS INVENTADOS\n" +
            "Versão do programa: " + version + "\n" +
            "Falhas na interface: 1\n" +
            "Falhas em consultas de atualização: 2\n\n" +
            "Nenhuma informação real do computador foi lida.\n" +
            "Nenhum arquivo foi criado ou anexado.\n" +
            "Nenhuma conexão de envio foi feita.\n" +
            "Seu consentimento não foi alterado.";
    }

    private static string HumanCategory(string category) => category switch
    {
        "APP.FALHA_INTERFACE" => "Falha da interface",
        "APP.FALHA_NAO_TRATADA" => "Falha inesperada",
        "REGISTRO_PROGRAMAS.FALHA" => "Falha ao identificar aplicativos",
        "MSIX.LEITURA" => "Leituras de aplicativos da Microsoft Store",
        "MULTIFONTES.CONSULTA_FALHA" => "Falha em consultas de atualização",
        "MULTIFONTES.CONSULTA_CANCELADA" => "Consultas de atualização canceladas",
        "DESINSTALACAO.FALHA" => "Falha na desinstalação",
        "INSTALACAO_OFICIAL.FALHA" => "Falha no instalador verificado",
        "TERMINAL_GITHUB.FALHA" => "Falha na atualização do Windows Terminal",
        "JANELA.BANDEJA_RESTAURAR.INICIO" => "Restaurações iniciadas pela bandeja",
        "JANELA.BANDEJA_RESTAURAR.FIM" => "Restaurações concluídas pela bandeja",
        "JANELA.BANDEJA_OCULTAR.INICIO" => "Minimizações iniciadas",
        "JANELA.BANDEJA_OCULTAR.FIM" => "Minimizações concluídas",
        "DIAGNOSTICO_COMPLETO.FIM" => "Diagnósticos concluídos",
        _ => "Ocorrência técnica"
    };


    // Prefere a sessao atual; se ainda nao houver eventos, permite revisar a
    // unica pendencia anterior. Nenhum conteudo do disco e confiado sem validacao.
    internal string? ReviewableReport(string version)
    {
        lock (_gate)
        {
            if (Consent is not (TelemetryConsent.LocalOnly or TelemetryConsent.RemoteBeta)) return null;
            ExpirePendingReport();
            if (_events.Count > 0) return Serialize(version, normalExit: false);
            try
            {
                InstallerFileGuard.ValidateLocalPath(PendingFile);
                if (!File.Exists(PendingFile) || new FileInfo(PendingFile).Length > 4096 || PendingHasExpired())
                    return null;
                string pending = ReadBoundedFile(PendingFile, 4096);
                return DiagnosticPayloadPolicy.IsValidReport(pending) ? pending : null;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                System.Security.SecurityException) { return null; }
        }
    }

    private string Serialize(string version, bool normalExit)
    {
        // Versao validada para impedir campos arbitrarios. Sem ID de sessao,
        // hostname, IP, caminhos, inventario, timestamps ou eventos detalhados.
        if (!DiagnosticPayloadPolicy.IsValidVersion(version))
            version = "versao-indisponivel";
        return JsonSerializer.Serialize(new
        {
            schema = 1,
            version,
            exit = normalExit ? "normal" : "incompleto",
            counts = _events.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    internal bool SaveOnNormalExit(string version) => SaveOnExit(version, normalExit: true);

    // O valor incompleto ja pertence ao schema fechado. Nunca enviar daqui:
    // falha/desligamento apenas preservam contagens para uma proxima sessao.
    internal bool SaveOnExit(string version, bool normalExit)
    {
        lock (_gate)
        {
            if (Consent is not (TelemetryConsent.LocalOnly or TelemetryConsent.RemoteBeta)) return false;
            using var state = TryStorageLock("estado-v1.lock");
            if (state is null || !RefreshStoredConsent()) return false;
            // Uma sessao sem eventos nao apaga a pendencia util da sessao anterior.
            if (_events.Count == 0) { ExpirePendingReport(); return false; }
            try
            {
                EnsureStorageFolder();
                WriteAtomic(PendingFile, Serialize(version, normalExit));
                _generation++;
                return true; // Guardou localmente. Nao significa envio.
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or System.Security.SecurityException) { return false; }
        }
    }

    internal string? PendingForRemote() => ReadPendingForRemote(out _);

    private string? ReadPendingForRemote(out InstallerFileGuard.FileIdentity? identity)
    {
        identity = null;
        lock (_gate)
        {
            if (!IsRemoteConsentCurrent()) return null;
            ExpirePendingReport();
            try
            {
                InstallerFileGuard.ValidateLocalPath(PendingFile);
                if (!File.Exists(PendingFile) || new FileInfo(PendingFile).Length > 4096 || PendingHasExpired()) return null;
                var report = ReadBoundedFile(PendingFile, 4096, out var captured);
                identity = captured;
                return DiagnosticPayloadPolicy.IsValidReport(report) ? report : null;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                System.Security.SecurityException) { return null; }
        }
    }

    internal sealed class SendLease : IDisposable
    {
        private readonly AnonymousTelemetry _owner;
        internal SendLease(AnonymousTelemetry owner, string report, long generation, CancellationTokenSource source,
            InstallerFileGuard.FileIdentity identity, FileStream processLock)
        { _owner=owner; Report=report; Generation=generation; Source=source; Identity=identity; ProcessLock=processLock; }
        internal string Report { get; }
        internal long Generation { get; }
        internal InstallerFileGuard.FileIdentity Identity { get; }
        internal FileStream ProcessLock { get; }
        internal CancellationTokenSource Source { get; }
        internal CancellationToken Cancellation => Source.Token;
        public void Dispose() => _owner.EndRemoteSend(this);
    }

    internal SendLease? TryBeginRemoteSend()
    {
        lock (_gate)
        {
            if (Consent != TelemetryConsent.RemoteBeta || _remoteSend is not null) return null;
            // Este lock dura somente a tentativa HTTP. E separado do lock de
            // estado: outra instancia pode revogar ou gravar pendencia nova.
            var processLock = TryStorageLock("envio-v1.lock");
            if (processLock is null) return null;
            using var state = TryStorageLock("estado-v1.lock");
            if (state is null) { processLock.Dispose(); return null; }
            string? report = ReadPendingForRemote(out var identity);
            if (report is null || identity is null) { processLock.Dispose(); return null; }
            _remoteSend = new CancellationTokenSource();
            return new SendLease(this, report, _generation, _remoteSend, identity, processLock);
        }
    }
    internal bool CompleteRemoteSend(SendLease lease)
    {
        lock (_gate)
        {
            using var state = TryStorageLock("estado-v1.lock");
            if (state is null) return false;
            if (Consent != TelemetryConsent.RemoteBeta || !ReferenceEquals(_remoteSend, lease.Source) ||
                lease.Cancellation.IsCancellationRequested || _generation != lease.Generation ||
                !IsRemoteConsentCurrent()) return false;
            try
            {
                // Validar e remover o MESMO arquivo por handle, sem janela
                // check/delete por caminho. Identidade fica apenas na memoria.
                using var held = InstallerFileGuard.OpenResidual(PendingFile, deleteAccess: true);
                if (held.Length > 4096 || InstallerFileGuard.ReadIdentity(held) != lease.Identity) return false;
                using var reader = new StreamReader(held, Encoding.UTF8, true, 4096, leaveOpen: true);
                if (reader.ReadToEnd() != lease.Report) return false;
                InstallerFileGuard.MarkResidualForDeletion(held);
                return true;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                System.Security.SecurityException) { return false; }
        }
    }
    internal bool IsRemoteConsentCurrent()
    {
        lock (_gate)
        {
            if (Consent != TelemetryConsent.RemoteBeta) return false;
            return RefreshStoredConsent();
        }
    }

    private void EndRemoteSend(SendLease lease)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_remoteSend, lease.Source)) _remoteSend = null;
            lease.Source.Dispose();
            lease.ProcessLock.Dispose();
        }
    }
    internal void CancelRemoteSend()
    {
        lock (_gate)
        {
            _generation++;
            try { _remoteSend?.Cancel(); } catch (AggregateException) { }
        }
    }

    internal bool DeletePending()
    {
        lock (_gate)
        {
            using var state = TryStorageLock("estado-v1.lock");
            if (state is null) return false;
            CancelRemoteSend();
            return TryDelete(PendingFile);
        }
    }

    private void ExpirePendingReport()
    {
        try
        {
            InstallerFileGuard.ValidateLocalPath(PendingFile);
            if (!File.Exists(PendingFile)) return;
            if (Consent is not (TelemetryConsent.LocalOnly or TelemetryConsent.RemoteBeta) ||
                PendingHasExpired())
                TryDelete(PendingFile);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }

    private bool PendingHasExpired()
    {
        // Timestamps futuros/adulterados nao podem manter dados indefinidamente.
        DateTime changed = File.GetLastWriteTimeUtc(PendingFile);
        DateTime now = DateTime.UtcNow;
        return changed > now.AddMinutes(5) ||
               now - changed > TimeSpan.FromDays(7);
    }

    private static bool TryDelete(string path)
    {
        try
        {
            InstallerFileGuard.ValidateLocalPath(path);
            string parent = Path.GetDirectoryName(Path.GetFullPath(path))!;
            if (!Directory.Exists(parent)) return true;
            // Inclusive expiração/revogação: não seguir um ancestral que tenha
            // sido trocado por junction antes de apagar a entrada da folha.
            using var directoryLock = InstallerFileGuard.PinDirectory(parent, allowLeafWrites: true);
            if (Directory.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException) { return false; }
    }

    private void EnsureStorageFolder()
    {
        InstallerFileGuard.ValidateLocalPath(_folder);
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

    private static string ReadBoundedFile(string path, int maxBytes) => ReadBoundedFile(path, maxBytes, out _);
    private static string ReadBoundedFile(string path, int maxBytes, out InstallerFileGuard.FileIdentity identity)
    {
        // Recusar links da folha e prender seus ancestrais também na leitura.
        // O limite é conferido no handle efetivamente aberto, sem confiar no
        // tamanho observado anteriormente pelo caminho.
        using var stream = InstallerFileGuard.OpenRead(path, allowDirectoryWrites: true);
        if (stream.Length > maxBytes) throw new IOException("Arquivo de privacidade acima do limite.");
        identity = InstallerFileGuard.ReadIdentity(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static void WriteAtomic(string path, string content)
    {
        using var directoryLock = InstallerFileGuard.PinDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!, allowLeafWrites: true);
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
