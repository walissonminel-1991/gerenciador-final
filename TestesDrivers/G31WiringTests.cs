using GerenciadorAplicativos;

// G3.1: zero COM invocado, zero driver instalado, sem tocar journal de producao.
internal static class G31WiringTests
{
    private sealed class LabBackend : IDriverUpdatePilotBackend
    {
        public Task<DriverUpdatePilotCheck> Revalidate(DriverUpdatePilotChoice choice,
            CancellationToken token) => throw new InvalidOperationException("Nao executar na fixture G3.1");
        public Task<DriverUpdatePilotStep> Download(DriverUpdatePilotChoice choice,
            CancellationToken token) => throw new InvalidOperationException("Nao baixar na fixture G3.1");
        public Task<DriverUpdatePilotStep> Install(DriverUpdatePilotChoice choice) =>
            throw new InvalidOperationException("Nao instalar na fixture G3.1");
        public Task<DriverUpdatePilotObservation> Observe(DriverUpdatePilotChoice choice) =>
            throw new InvalidOperationException("Nao observar na fixture G3.1");
    }

    internal static void Run()
    {
        static void Ensure(bool condition, string message)
        {
            if (!condition) throw new Exception("G3.1: " + message);
        }
        var native = DriverG31Wiring.NewProductionBackend();
        var factory = DriverG31Wiring.NewProductionJournalFactory();
        Ensure(native is DriverWuaNativeBackend && DriverG31Wiring.IsNativeBackend(native),
            "a UI usa o backend WUA nativo, nao uma simulacao");
        var journal = factory();
        try
        {
            Ensure(journal is DriverUpdatePilotFileJournal,
                "a UI usa o diario persistente da PR80");
            Ensure(!DriverG31Wiring.CanExecutePilot(native, factory),
                "instalacao nativa permanece NEGADA na G3.1");
            Ensure(!DriverG31Wiring.CanExecutePilot(null, factory) &&
                !DriverG31Wiring.CanExecutePilot(new LabBackend(), null),
                "dependencias ausentes nao habilitam execucao");
            Ensure(DriverG31Wiring.CanExecutePilot(new LabBackend(), factory),
                "fixtures sinteticas anteriores continuam exercitaveis");
        }
        finally
        {
            if (journal is IDisposable disposable) disposable.Dispose();
        }
        // Cancelar WaitAsync nao encerra a tarefa COM subjacente.
        var gate = new DriverG31ReadGate();
        var pending = new TaskCompletionSource<DriverUpdatePilotCheck>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Ensure(gate.TryStart(() => pending.Task, out var firstTask) &&
            ReferenceEquals(firstTask, pending.Task) && gate.HasPendingWork,
            "consulta nativa inicial nao foi rastreada");
        using var cancelWait = new CancellationTokenSource();
        var canceledAwait = pending.Task.WaitAsync(cancelWait.Token);
        cancelWait.Cancel();
        bool waitCanceled = false;
        try { canceledAwait.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { waitCanceled = true; }
        bool secondExecuted = false;
        Ensure(waitCanceled && gate.HasPendingWork &&
            !gate.TryStart(() => { secondExecuted = true; return pending.Task; }, out var denied) &&
            !secondExecuted && denied is null,
            "cancelamento da espera autorizou segunda consulta COM concorrente");
        // Outra janela nao pode perder a tarefa nativa ao fechar a primeira.
        var reopenedWindow = new DriverG31ReadGate();
        Ensure(reopenedWindow.HasPendingWork &&
            ReferenceEquals(reopenedWindow.LatestTask, pending.Task) &&
            !reopenedWindow.TryStart(() => { secondExecuted = true; return pending.Task; },
                out var deniedReopen) && !secondExecuted && deniedReopen is null,
            "reabertura da janela perdeu tarefa WUA em andamento");
        pending.SetResult(new DriverUpdatePilotCheck(false, false, false, false,
            false, false, false, false, false));
        Ensure(!reopenedWindow.HasPendingWork &&
            gate.IsLatestCompleted(firstTask!),
            "conclusao WUA inicial nao foi reconhecida");
        // A fila do WinForms pode executar o observador antigo DEPOIS de
        // uma segunda janela ter iniciado novo trabalho nativo.
        var secondPending = new TaskCompletionSource<DriverUpdatePilotCheck>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Ensure(gate.TryStart(() => secondPending.Task, out var nextTask) &&
            ReferenceEquals(nextTask, secondPending.Task) && gate.HasPendingWork &&
            !reopenedWindow.IsLatestCompleted(firstTask!) &&
            !gate.IsLatestCompleted(nextTask!),
            "callback antigo poderia sobrescrever o status de novo WUA ativo");
        secondPending.SetResult(new DriverUpdatePilotCheck(false, false, false, false,
            false, false, false, false, false));
        Ensure(gate.IsLatestCompleted(nextTask!) &&
            !gate.IsLatestCompleted(firstTask!) && !reopenedWindow.HasPendingWork,
            "notificacao WUA aceita Task obsoleta depois da conclusao nova");
        Console.WriteLine("OK G3.1: WUA + diario ligados, instalacao nativa NEGADA, fixtures preservadas");
    }
}
