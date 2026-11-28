using GerenciadorAplicativos;

internal static class ElevatedWorkerReadSupervisorTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    internal static void Run(Action<bool, string> check) =>
        RunAsync(check).GetAwaiter().GetResult();

    private static async Task RunAsync(Action<bool, string> check)
    {
        Task<ElevatedObservation> Observe(string?[] lines, int exit, Action<string>? onLine = null)
        {
            var queue = new Queue<string?>(lines);
            return ElevatedWorkerReadSupervisor.ObserveAsync(
                _ => Task.FromResult(queue.Dequeue()), _ => Task.FromResult(exit),
                onLine ?? (_ => { }), Budget);
        }

        var messages = new List<string>();
        var success = await Observe(["LOG|progresso", "RESULT|fixture", "DONE|0", null], 0, messages.Add);
        check(success.Confirmed && success.ExitCode == 0 &&
            messages.SequenceEqual(new[] { "LOG|progresso", "RESULT|fixture" }),
            "GF-04: terminal confirmado apenas com EOF e saída correspondente");
        var warning = await Observe(["DONE|2", null], 2);
        check(warning.Confirmed && warning.ExitCode == 2, "GF-04: retorno não zero preservado, não convertido em sucesso");
        var missing = await Observe(["LOG|fixture", null], 0);
        check(missing.State == ElevatedObservationState.MissingDone && !missing.Confirmed,
            "GF-04: EOF sem DONE não confirma operação");
        check((await Observe(["DONE|0", null], 3)).State == ElevatedObservationState.ExitMismatch,
            "GF-04: divergência de exit code não confirma operação");
        check((await Observe(["DONE|0", "DONE|0", null], 0)).State == ElevatedObservationState.InvalidProtocol,
            "GF-04: duplicação terminal recusada");
        check((await Observe(["DONE|invalid", null], 0)).State == ElevatedObservationState.InvalidProtocol,
            "GF-04: terminal inválido recusado");

        var waiting = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var cancellation = new CancellationTokenSource())
        {
            int callbacks = 0;
            var pending = ElevatedWorkerReadSupervisor.ObserveAsync(
                _ => waiting.Task, _ => Task.FromResult(0), _ => callbacks++, Budget, cancellation.Token);
            cancellation.Cancel();
            var canceled = await pending.WaitAsync(Budget);
            check(canceled.State == ElevatedObservationState.Cancelled && !canceled.Confirmed,
                "GF-04: cancelamento encerra espera mesmo com leitura não cooperativa");
            waiting.SetResult("RESULT|tardio");
            check(callbacks == 0 && !canceled.Confirmed, "GF-04: retorno tardio não publica estado");
        }

        var silent = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeout = await ElevatedWorkerReadSupervisor.ObserveAsync(
            _ => silent.Task, _ => Task.FromResult(0), _ => { },
            TimeSpan.FromMilliseconds(50)).WaitAsync(Budget);
        check(timeout.State == ElevatedObservationState.TimedOut && !timeout.Confirmed,
            "GF-04: silêncio pós-handshake tem prazo total");
        silent.SetResult(null);

        var exitPending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new Queue<string?>(["DONE|0", null]);
        var exitTimeout = await ElevatedWorkerReadSupervisor.ObserveAsync(
            _ => Task.FromResult(done.Dequeue()), _ => exitPending.Task, _ => { },
            TimeSpan.FromMilliseconds(50)).WaitAsync(Budget);
        check(exitTimeout.State == ElevatedObservationState.TimedOut && !exitTimeout.Confirmed,
            "GF-04: DONE sem saída observada não libera posse");
        exitPending.SetResult(0);

        var broken = await ElevatedWorkerReadSupervisor.ObserveAsync(
            _ => Task.FromException<string?>(new IOException("fixture")),
            _ => Task.FromResult(0), _ => { }, Budget);
        check(broken.State == ElevatedObservationState.CommunicationLost, "GF-04: perda do canal inconclusiva");

        var admission = new ElevatedOperationAdmission(new FixtureElevatedOperationCheckpoint());
        using (var first = admission.TryAcquire() ?? throw new InvalidOperationException("fixture"))
        {
            check(admission.IsBlocked && admission.TryAcquire() is null, "GF-04: concorrência recusada");
            first.Complete(success);
        }
        using (var second = admission.TryAcquire() ?? throw new InvalidOperationException("fixture"))
        {
            second.Complete(timeout);
            second.Complete(success); // tentativa tardia não altera quarentena
        }
        check(admission.IsInconclusive && admission.IsBlocked && admission.TryAcquire() is null,
            "GF-04: timeout mantém posse; terminal tardio não rearma");
        var noLaunch = new ElevatedOperationAdmission(new FixtureElevatedOperationCheckpoint());
        using (var lease = noLaunch.TryAcquire() ?? throw new InvalidOperationException("fixture")) lease.NoLaunch();
        using (var lease = noLaunch.TryAcquire() ?? throw new InvalidOperationException("fixture")) { }
        check(noLaunch.IsInconclusive && noLaunch.TryAcquire() is null,
            "GF-04: somente recusa comprovada antes de lançar libera; descarte sem desfecho bloqueia");

        var competing = new ElevatedOperationAdmission(new FixtureElevatedOperationCheckpoint());
        int winners = 0;
        Parallel.For(0, 32, _ =>
        {
            var lease = competing.TryAcquire();
            if (lease is not null) Interlocked.Increment(ref winners);
        });
        check(winners == 1 && competing.IsBlocked, "GF-04: uma única posse sob concorrência");
    }
}

// Exclusivamente para as provas unitárias da máquina de estados, sem I/O.
internal sealed class FixtureElevatedOperationCheckpoint : IElevatedOperationCheckpoint
{
    public bool TryBegin() => true;
    public bool TryClear() => true;
    public void Retain() { }
}
