using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace GerenciadorAplicativos;

internal enum UpdateStage
{
    Preparing, Downloading, VerifyingIntegrity, ValidatingSignature, ValidatingIdentity, Revalidating,
    Installing, Confirming, Confirmed, Failed, Pending, Blocked, Cancelled, AuthorizationDeclined
}

// Download em azul mede bytes; somente versão confirmada recebe resultado verde e 100%.
internal sealed record UpdateProgress(UpdateStage Stage, long Bytes = 0, long? Total = null, bool DownloadCompleted = false)
{
    internal int? Percent => Stage == UpdateStage.Confirmed ? 100 :
        Stage == UpdateStage.Downloading && Total is > 0 && Bytes >= 0
            ? (int)Math.Min(100, (decimal)Bytes * 100 / Total.Value) : null;
}

internal sealed record UpdateOperationSnapshot(Guid Id, string Key, string Target, UpdateProgress Progress,
    int? ExitCode, bool RestartRequired, string Reason, long DurationMs, bool DiagnosticFailed)
{
    internal bool Active => Progress.Stage < UpdateStage.Confirmed;
    internal bool PreventRetry => Active || Progress.Stage is UpdateStage.Pending or UpdateStage.Confirmed;
    internal string Text => Progress.Stage switch
    {
        UpdateStage.Preparing => "Preparando atualização...",
        UpdateStage.Downloading => Progress.Percent is int p ? $"Baixando instalador — {p}%" : "Baixando instalador...",
        UpdateStage.VerifyingIntegrity => "Verificando integridade...",
        UpdateStage.ValidatingIdentity => "Conferindo identidade do pacote...",
        UpdateStage.ValidatingSignature => "Validando assinatura...",
        UpdateStage.Revalidating => "Conferindo instalação e arquivos em uso...",
        UpdateStage.Installing => "Instalando atualização...",
        UpdateStage.Confirming => "Conferindo versão instalada...",
        UpdateStage.Confirmed => RestartRequired ? "✓ 100% — Atualizado; reinicie o PC" : "✓ 100% — Atualização concluída",
        UpdateStage.Failed => "Falha na atualização — veja detalhes",
        UpdateStage.Pending => RestartRequired ? "Resultado pendente — reinicie e confira" : "Instalação iniciada — não confirmada",
        UpdateStage.Blocked => "Atualização bloqueada — veja detalhes",
        UpdateStage.AuthorizationDeclined => "Autorização do Windows recusada",
        _ => "Atualização cancelada antes da instalação"
    };
    internal string Details => Text + (ExitCode is int code ? $" · código {code}" : "") +
        (Reason.Length > 0 ? " · " + Reason : "") +
        (DiagnosticFailed ? " · Não foi possível gravar parte do diagnóstico." : "");
}

// Memória da sessão, independente de cartões, posições, nomes com versão e objetos do inventário.
// Sem associação por nome: hive/visão/registro, pasta, editor e produto devem continuar iguais.
internal sealed class UpdateOperationStore(Func<string, bool>? diagnostic = null)
{
    private readonly object gate = new();
    private readonly Dictionary<string, UpdateOperation> operations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UpdateOperation> runtimeAttempts = new(StringComparer.Ordinal);
    private sealed record KeyHolder(string? Key);
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ProgramEntry, KeyHolder> identities = new();
    private string? KeyFor(ProgramEntry app) => identities.GetValue(app, p => new(InstallationKey(p))).Key;
    internal static string? InstallationKey(ProgramEntry app)
    {
        if (string.IsNullOrWhiteSpace(app.InventoryId)) return null;
        string family = System.Text.RegularExpressions.Regex.Replace(app.Name, @"\b\d+(?:\.\d+)+\b", "").Trim();
        string[] parts = [family, app.InventoryId, app.Publisher, app.InstallLocation.TrimEnd('\\', '/'),
            app.MsiProductCode, app.InventoryOnly.ToString()];
        string canonical = string.Concat(parts.Select(p => p.Trim().ToUpperInvariant()).Select(p => p.Length + ":" + p));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
    internal UpdateOperation? Begin(ProgramEntry app, string target)
    {
        string? key = KeyFor(app);
        if (key is null) return null;
        lock (gate)
        {
            // Apenas bloqueio de repeticao: GUID novo nao autoriza atribuir sucesso.
            string? runtime = UpdateInstallationContract.RuntimeScopeKey(app);
            if (runtime is not null && runtimeAttempts.TryGetValue(runtime, out var pendingRuntime) &&
                pendingRuntime.Snapshot.PreventRetry) return null;
            if (operations.TryGetValue(key, out var previous) && previous.Snapshot is var snapshot &&
                (snapshot.Active || snapshot.Progress.Stage == UpdateStage.Pending ||
                 snapshot.Progress.Stage == UpdateStage.Confirmed && snapshot.Target == target)) return null;
            // Limite de memória, sem expulsar operações ativas ou pendentes (protegem repetição).
            if (operations.Count >= 256 && !operations.ContainsKey(key))
            {
                string? oldest = operations.FirstOrDefault(p => !p.Value.Snapshot.Active && p.Value.Snapshot.Progress.Stage != UpdateStage.Pending).Key;
                if (oldest is null) return null;
                operations.Remove(oldest);
            }
            var operation = new UpdateOperation(key, target, diagnostic);
            operations[key] = operation;
            if (runtime is not null) runtimeAttempts[runtime] = operation;
            return operation;
        }
    }
    internal bool BindConfirmedInstallation(VerifiedUpdateRecipe recipe, ProgramEntry original,
        IEnumerable<ProgramEntry> inventory, UpdateOperation operation)
    {
        var snapshot = inventory.ToArray();
        if(operation.Snapshot.Progress.Stage!=UpdateStage.Confirmed)return false;
        ProgramEntry? after;
        if(AutomaticUpdateDiscovery.IsAutomatic(recipe)) after=AutomaticUpdateDiscovery.ConfirmedEntry(recipe,snapshot);
        else
        {
            if(recipe.Identifier is not ("vc2013-x64" or "vc2013-x86") || !VerifiedUpdaters.ConfirmInstallation(recipe,original,snapshot))return false;
            string marker=recipe.Identifier=="vc2013-x64"?"(x64)":"(x86)";
            after=snapshot.Single(p=>p.Name.StartsWith("Microsoft Visual C++ 2013 Redistributable "+marker,StringComparison.OrdinalIgnoreCase));
        }
        if(after is null)return false;
        string? oldKey = KeyFor(original), newKey = KeyFor(after);
        lock (gate)
        {
            if (oldKey is null || newKey is null || !operations.TryGetValue(oldKey, out var existing) ||
                existing != operation || operations.TryGetValue(newKey, out var other) && other != operation) return false;
            operations.Remove(oldKey);
            operations[newKey] = operation;
            return true;
        }
    }

    internal UpdateOperationSnapshot? Find(ProgramEntry app)
    {
        string? key = KeyFor(app);
        lock (gate)
        {
            if (key is not null && operations.TryGetValue(key, out var op)) return op.Snapshot;
            string? runtime = UpdateInstallationContract.RuntimeScopeKey(app);
            if (runtime is not null && runtimeAttempts.TryGetValue(runtime, out var pending) &&
                pending.Snapshot.Progress.Stage == UpdateStage.Pending)
                return pending.Snapshot with { Reason = "Há uma operação desta família pendente; a identidade da nova entrada ainda não foi confirmada." };
            return null;
        }
    }
}

internal sealed class UpdateOperation
{
    private readonly object gate = new();
    private readonly Func<string, bool>? diagnostic;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private UpdateOperationSnapshot snapshot;
    private int reportedBucket = -1;
    internal UpdateOperation(string key, string target, Func<string, bool>? diagnostic)
    {
        this.diagnostic = diagnostic;
        snapshot = new(Guid.NewGuid(), key, target, new(UpdateStage.Preparing), null, false, "", 0, false);
        Record("INICIO");
    }
    internal UpdateOperationSnapshot Snapshot { get { lock (gate) return snapshot; } }
    internal void Report(UpdateProgress progress)
    {
        lock (gate)
        {
            if (!snapshot.Active || progress.Stage >= UpdateStage.Confirmed ||
                progress.Stage < snapshot.Progress.Stage) return; // callback antigo ou conclusão tardia
            if (progress.Stage == UpdateStage.Downloading &&
                (progress.Bytes < snapshot.Progress.Bytes || progress.Bytes < 0)) return;
            bool changed = snapshot.Progress.Stage != progress.Stage;
            snapshot = snapshot with { Progress = progress, DurationMs = clock.ElapsedMilliseconds };
            int bucket = (progress.Percent ?? -10) / 10;
            if (changed || bucket != reportedBucket || progress.DownloadCompleted)
            {
                reportedBucket = bucket;
                Record(progress.DownloadCompleted ? "DOWNLOAD_CONCLUIDO" : "ETAPA");
            }
        }
    }
    internal void Finish(UpdateStage stage, int? exit = null, string reason = "")
    {
        if (stage < UpdateStage.Confirmed) throw new ArgumentOutOfRangeException(nameof(stage));
        lock (gate)
        {
            if (!snapshot.Active) return;
            snapshot = snapshot with { Progress = new(stage), ExitCode = exit, RestartRequired = exit == 3010,
                Reason = reason, DurationMs = clock.ElapsedMilliseconds };
            Record("FIM");
        }
    }
    internal void Complete(UpdateAttemptResult result) => Finish(result.State switch
    {
        UpdateAttemptState.Confirmed => UpdateStage.Confirmed,
        UpdateAttemptState.Failed => UpdateStage.Failed,
        UpdateAttemptState.Blocked or UpdateAttemptState.AlreadyAttempted => UpdateStage.Blocked,
        UpdateAttemptState.Cancelled => result.AuthorizationDeclined ? UpdateStage.AuthorizationDeclined : UpdateStage.Cancelled,
        _ => UpdateStage.Pending
    }, result.ExitCode, result.State switch
    {
        UpdateAttemptState.Failed => "O instalador informou erro. Consulte Registro e diagnóstico.",
        UpdateAttemptState.Blocked => "A instalação mudou ou não atende à receita autorizada.",
        UpdateAttemptState.Indeterminate => "Confira a versão antes de uma nova tentativa; o instalador pode continuar.",
        _ => ""
    });
    internal void Fail(Exception exception, bool installerStarted)
    {
        if (!Snapshot.Active) return; // falha posterior do catálogo não apaga versão confirmada
        string reason = exception switch
        {
            HttpRequestException => "Falha ao baixar o instalador (HTTP/rede). Tente novamente quando a conexão voltar.",
            CryptographicException => exception is UpdateSignatureException || Snapshot.Progress.Stage == UpdateStage.ValidatingSignature
                ? "Assinatura digital rejeitada; o instalador não foi executado."
                : "Integridade SHA-256 rejeitada; o instalador não foi executado.",
            InvalidDataException => "Pacote incompleto, incompatível ou não autorizado; instalação interrompida.",
            IOException => "Download ou acesso ao arquivo interrompido.",
            System.ComponentModel.Win32Exception => "O Windows bloqueou o início do instalador.",
            _ => "Não foi possível concluir esta etapa. Consulte Registro e diagnóstico."
        };
        if (installerStarted) reason = "O instalador foi iniciado; confira a versão antes de tentar novamente.";
        lock (gate)
        {
            // Tipo fechado; nunca registrar Message, URL, linha de comando ou caminho externo.
            Record("ERRO_" + exception.GetType().Name);
            Finish(installerStarted ? UpdateStage.Pending : exception is System.ComponentModel.Win32Exception ? UpdateStage.Blocked : UpdateStage.Failed,
                exit: exception is System.ComponentModel.Win32Exception native ? native.NativeErrorCode : exception.HResult, reason: reason);
        }
    }
    private void Record(string evt)
    {
        // Só metadados fechados e hash local: nenhum caminho, nome de usuário ou saída externa.
        try
        {
            if (diagnostic is not null && !diagnostic($"ATUALIZACAO.{evt} op={snapshot.Id:N} app={snapshot.Key} " +
                $"etapa={snapshot.Progress.Stage} bytes={snapshot.Progress.Bytes} total={snapshot.Progress.Total} " +
                $"percentual={snapshot.Progress.Percent} retorno={snapshot.ExitCode} reinicio={snapshot.RestartRequired} ms={snapshot.DurationMs}"))
                snapshot = snapshot with { DiagnosticFailed = true };
        }
        catch { snapshot = snapshot with { DiagnosticFailed = true }; }
    }
}

internal static class UpdateDownloadTransfer
{
    // Usado pelos dois motores e por respostas HTTP sintéticas; não autoriza URLs nem instaladores.
    internal static async Task CopyAsync(HttpResponseMessage response, Stream output, long minimum, long maximum,
        CancellationToken token, Action<UpdateProgress>? report = null)
    {
        response.EnsureSuccessStatusCode();
        long? expected = response.Content.Headers.ContentLength;
        if (expected is long length && (length < minimum || length > maximum))
            throw new InvalidDataException("Tamanho do instalador fora do limite autorizado.");
        report?.Invoke(new(UpdateStage.Downloading, 0, expected));
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        byte[] buffer = new byte[128 * 1024];
        long bytes = 0, lastReport = 0;
        var clock = Stopwatch.StartNew();
        int count;
        while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            bytes += count;
            if (bytes > maximum || expected is long size && bytes > size)
                throw new InvalidDataException("Download excede o tamanho autorizado.");
            await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            if (clock.ElapsedMilliseconds - lastReport >= 120)
            {
                lastReport = clock.ElapsedMilliseconds;
                report?.Invoke(new(UpdateStage.Downloading, bytes, expected));
            }
        }
        token.ThrowIfCancellationRequested();
        if (bytes < minimum || expected is long total && total != bytes)
            throw new InvalidDataException("Download incompleto.");
        report?.Invoke(new(UpdateStage.Downloading, bytes, expected, DownloadCompleted: true));
    }
}
