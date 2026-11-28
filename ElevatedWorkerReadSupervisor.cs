using System.Globalization;

namespace GerenciadorAplicativos;

internal enum ElevatedObservationState
{
    Completed, TimedOut, Cancelled, MissingDone, InvalidProtocol, ExitMismatch, CommunicationLost
}

internal sealed record ElevatedObservation(ElevatedObservationState State, int? ExitCode = null)
{
    internal bool Confirmed => State == ElevatedObservationState.Completed && ExitCode.HasValue;
}

// Somente após autenticação existente do peer e token. Não inicia, interrompe,
// mata nem autoriza processos. Prazo total é fornecido pelo contrato de cada tarefa.
// DONE não é confirmação até EOF e saída do processo original concordarem.
internal static class ElevatedWorkerReadSupervisor
{
    internal static async Task<ElevatedObservation> ObserveAsync(
        Func<CancellationToken, Task<string?>> read,
        Func<CancellationToken, Task<int>> waitExit,
        Action<string> nonTerminal,
        TimeSpan totalDeadline,
        CancellationToken cancellation = default)
    {
        if (totalDeadline <= TimeSpan.Zero || totalDeadline == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(totalDeadline));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(totalDeadline);
        CancellationToken token = deadline.Token;
        int? reported = null;
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                string? line = await BoundedAsync(read(token), token);
                token.ThrowIfCancellationRequested();
                if (line is null) break;
                if (reported.HasValue) return new(ElevatedObservationState.InvalidProtocol);
                if (line.StartsWith("DONE|", StringComparison.Ordinal))
                {
                    if (!int.TryParse(line.AsSpan(5), NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out int code))
                        return new(ElevatedObservationState.InvalidProtocol);
                    reported = code;
                }
                else if (line.StartsWith("LOG|", StringComparison.Ordinal) ||
                    line.StartsWith("RESULT|", StringComparison.Ordinal))
                {
                    // RESULT antes do terminal é evidência provisória; não publicar
                    // sucesso nem liberar outra operação neste callback.
                    nonTerminal(line);
                }
                else return new(ElevatedObservationState.InvalidProtocol);
            }
            if (!reported.HasValue) return new(ElevatedObservationState.MissingDone);
            token.ThrowIfCancellationRequested();
            int actual = await BoundedAsync(waitExit(token), token);
            token.ThrowIfCancellationRequested();
            return actual == reported.Value
                ? new(ElevatedObservationState.Completed, actual)
                : new(ElevatedObservationState.ExitMismatch);
        }
        catch (OperationCanceledException)
        {
            return new(cancellation.IsCancellationRequested ? ElevatedObservationState.Cancelled :
                deadline.IsCancellationRequested ? ElevatedObservationState.TimedOut :
                ElevatedObservationState.CommunicationLost);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or
            InvalidOperationException or System.ComponentModel.Win32Exception)
        { return new(ElevatedObservationState.CommunicationLost); }
    }

    // Também limita uma leitura/espera que não coopera com o token. Não
    // iniciar outra leitura no mesmo canal: o chamador encerra a observação,
    // mantém a posse inconclusiva e libera apenas seus handles de comunicação.
    private static async Task<T> BoundedAsync<T>(Task<T> task, CancellationToken token)
    {
        try { return await task.WaitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            _ = task.ContinueWith(t => { _ = t.Exception; },
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
    }
}

// A perda de comunicação não prova que DISM/SFC/WUA ou filhos terminaram.
// Quarentena é terminal nesta instância; não há reset por timeout ou PID.
// Integração exige uma instância compartilhada entre rotas e o checkpoint durável.
internal sealed class ElevatedOperationAdmission(IElevatedOperationCheckpoint checkpoint)
{
    private const int Idle = 0, Active = 1, Quarantined = 2;
    private int _state;
    internal bool IsBlocked => Volatile.Read(ref _state) != Idle;
    internal bool IsInconclusive => Volatile.Read(ref _state) == Quarantined;

    private readonly IElevatedOperationCheckpoint _checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));

    internal Lease? TryAcquire()
    {
        if (Interlocked.CompareExchange(ref _state, Active, Idle) != Idle) return null;
        bool persisted = false;
        try
        {
            persisted = _checkpoint.TryBegin();
            return persisted ? new(this) : null;
        }
        finally
        {
            if (!persisted) Interlocked.Exchange(ref _state, Quarantined);
        }
    }

    internal sealed class Lease : IDisposable
    {
        private ElevatedOperationAdmission? _owner;
        internal Lease(ElevatedOperationAdmission owner) => _owner = owner;

        private void Finish(bool release)
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is null) return;
            bool cleared = false;
            try
            {
                if (release) cleared = owner._checkpoint.TryClear();
                else owner._checkpoint.Retain();
            }
            finally { Interlocked.CompareExchange(ref owner._state, cleared ? Idle : Quarantined, Active); }
        }

        // Usar somente quando há prova de que nenhum processo foi iniciado.
        internal void NoLaunch() => Finish(true);
        internal void Complete(ElevatedObservation observation) => Finish(observation.Confirmed);
        public void Dispose() => Finish(false);
    }
}
