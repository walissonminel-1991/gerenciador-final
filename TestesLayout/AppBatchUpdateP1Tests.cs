using GerenciadorAplicativos;

// Contrato dirigido do novo lote: sem rede, download, WinForms modal ou instalador.
internal static class AppBatchUpdateP1Tests
{
    private static AppBatchUpdateCandidate Target(int index) =>
        new("REG|" + index, "KEY|" + index, "1.0", "recipe-" + index,
            "2.0." + index, "Programa sintético " + index.ToString("00"));

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new Exception("P1-batch: " + message);
    }

    internal static void Run()
    {
        var three = Enumerable.Range(1, 3).Select(Target).ToArray();
        var twenty = Enumerable.Range(1, 20).Select(Target).ToArray();
        string preview = AppBatchUpdateRun.Preview(twenty, 25);
        Require(preview.Contains("que não entram neste lote: 5") &&
            !preview.Contains("…e mais") && !preview.Contains("...e mais"),
            "consentimento deve explicar exclusões sem omitir alvos");
        foreach (var target in twenty)
            Require(preview.Contains(target.Name + " → " + target.TargetVersion),
                "confirmação deve mostrar TODOS os 20 nomes e versões");
        var thirteen = twenty.Take(13).ToArray();
        Require(AppBatchUpdateRun.Preview(thirteen, 13).Split(" → ").Length == 14,
            "prévia dos 13 alvos deve conter todas as versões");
        Require(!AppBatchUpdateRun.MayExecute(true, true, true) &&
            !AppBatchUpdateRun.MayExecute(false, true, false) &&
            AppBatchUpdateRun.MayExecute(false, true, true) &&
            AppBatchUpdateRun.MayExecute(false, false, false),
            "exclusividade entre itens do lote e executores externos");

        var good = AppBatchUpdateRun.RunAsync(three,
            _ => Task.FromResult(AppBatchUpdateStepResult.Confirmed), () => false)
            .GetAwaiter().GetResult();
        Require(good.ConservesItems && good.Confirmed == 3 && good.NotStarted == 0,
            "todas as versões confirmadas");
        int started = 0;
        var failed = AppBatchUpdateRun.RunAsync(three, _ =>
        {
            started++;
            return Task.FromResult(started == 2 ? AppBatchUpdateStepResult.Inconclusive :
                AppBatchUpdateStepResult.Confirmed);
        }, () => false).GetAwaiter().GetResult();
        Require(failed.ConservesItems && started == 2 && failed.Confirmed == 1 &&
            failed.Inconclusive == 1 && failed.NotStarted == 1,
            "falha ou pendência interrompe antes do próximo item");
        started = 0;
        var declined = AppBatchUpdateRun.RunAsync(three, _ =>
        {
            started++;
            return Task.FromResult(AppBatchUpdateStepResult.BlockedBeforeStart);
        }, () => false).GetAwaiter().GetResult();
        Require(declined.ConservesItems && started == 1 && declined.BlockedBeforeStart == 1 &&
            declined.NotStarted == 2 && declined.Confirmed == 0,
            "identidade revogada não é contada como execução");
        started = 0;
        var exception = AppBatchUpdateRun.RunAsync(three, _ =>
        {
            started++;
            throw new InvalidOperationException("falha sintética");
#pragma warning disable CS0162
            return Task.FromResult(AppBatchUpdateStepResult.Confirmed);
#pragma warning restore CS0162
        }, () => false).GetAwaiter().GetResult();
        Require(exception.ConservesItems && started == 1 &&
            exception.Inconclusive == 1 && exception.NotStarted == 2,
            "exceção não inicia a próxima atualização");

        // Executa a fixture assíncrona fora do dispatcher WinForms: o teste
        // não deve bloquear o mesmo contexto que o await do coordenador usa.
        using var stop = new CancellationTokenSource();
        using var firstEntered = new ManualResetEventSlim(false);
        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        started = 0;
        var pending = Task.Run(() => AppBatchUpdateRun.RunAsync(three, async _ =>
        {
            started++;
            if (started == 1)
            {
                firstEntered.Set();
                await barrier.Task.ConfigureAwait(false);
            }
            return AppBatchUpdateStepResult.Confirmed;
        }, () => stop.IsCancellationRequested));
        Require(firstEntered.Wait(TimeSpan.FromSeconds(5)) && !pending.IsCompleted,
            "primeira operação deve permanecer em curso");
        stop.Cancel();
        barrier.SetResult(true);
        var halted = pending.GetAwaiter().GetResult();
        Require(halted.ConservesItems && halted.Confirmed == 1 && halted.NotStarted == 2 &&
            started == 1, "Parar lote preserva instalação atual e bloqueia próximos itens");
        Console.WriteLine("OK P1-BATCH: prévia 13/20, consentimento, exclusividade, " +
            "fila, confirmação, falha, exceção, revogação, cancelamento e conservação de contagens.");
    }
}
