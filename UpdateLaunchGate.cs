using System.ComponentModel;
namespace GerenciadorAplicativos;

internal enum UpdateAttemptState { Blocked, Cancelled, Failed, Indeterminate, Confirmed, AlreadyAttempted }
internal sealed record UpdateAttemptResult(UpdateAttemptState State, int? ExitCode = null);

// Uma tentativa por autorização. Cancelar a espera após iniciar não cancela o
// instalador do fabricante e jamais deve provocar repetição automática.
internal sealed class UpdateLaunchGate
{
    private int attempted;
    internal async Task<UpdateAttemptResult> RunAsync(Func<Task<bool>> revalidate,
        Func<Task<int>> launch, Func<Task<bool>> confirm, CancellationToken token = default)
    {
        if (Interlocked.Exchange(ref attempted, 1) != 0)
            return new(UpdateAttemptState.AlreadyAttempted);
        bool launched = false;
        try
        {
            token.ThrowIfCancellationRequested();
            if (!await revalidate()) return new(UpdateAttemptState.Blocked);
            token.ThrowIfCancellationRequested();
            // A partir deste ponto a chamada pode ter criado um processo externo.
            launched = true;
            int exit = await launch();
            if (token.IsCancellationRequested) return new(UpdateAttemptState.Indeterminate, exit);
            if (exit is not (0 or 3010)) return new(UpdateAttemptState.Failed, exit);
            bool confirmed = await confirm();
            // A conferencia pode aguardar inventario/rede. O cancelamento
            // durante essa espera nao transforma uma operacao em sucesso.
            if (token.IsCancellationRequested) return new(UpdateAttemptState.Indeterminate, exit);
            return new(confirmed ? UpdateAttemptState.Confirmed : UpdateAttemptState.Indeterminate, exit);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { return new(UpdateAttemptState.Cancelled); }
        catch (TimeoutException) { return new(UpdateAttemptState.Indeterminate); }
        catch (OperationCanceledException)
        { return new(launched ? UpdateAttemptState.Indeterminate : UpdateAttemptState.Cancelled); }
    }
}
