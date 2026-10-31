using System.ComponentModel;
namespace GerenciadorAplicativos;

internal enum UpdateAttemptState { Blocked, Cancelled, Failed, Indeterminate, Confirmed, AlreadyAttempted }
internal sealed record UpdateAttemptResult(UpdateAttemptState State, int? ExitCode = null, bool AuthorizationDeclined = false);
internal sealed class UpdateInstallerFailureException(int exitCode) : Exception("O instalador informou falha.")
{
    internal int ExitCode { get; } = exitCode;
}

// Uma tentativa por autorização. Cancelar a espera após iniciar não cancela o
// instalador do fabricante e jamais deve provocar repetição automática.
internal sealed class UpdateLaunchGate
{
    private int attempted;
    internal async Task<UpdateAttemptResult> RunAsync(Func<Task<bool>> revalidate,
        Func<Task<int>> launch, Func<Task<bool>> confirm, CancellationToken token = default,
        Action<UpdateProgress>? progress = null, Func<bool>? hasConfirmedInstallerStart = null)
    {
        if (Interlocked.Exchange(ref attempted, 1) != 0)
            return new(UpdateAttemptState.AlreadyAttempted);
        bool launched = false;
        bool installerReturned = false;
        try
        {
            token.ThrowIfCancellationRequested();
            progress?.Invoke(new(UpdateStage.Revalidating));
            if (!await revalidate()) return new(UpdateAttemptState.Blocked);
            token.ThrowIfCancellationRequested();
            // A partir deste ponto a chamada pode ter criado um processo externo.
            progress?.Invoke(new(UpdateStage.Installing));
            token.ThrowIfCancellationRequested();
            launched = true;
            int exit = await launch();
            installerReturned = true;
            if (token.IsCancellationRequested) return new(UpdateAttemptState.Indeterminate, exit);
            if (exit is not (0 or 3010)) return new(UpdateAttemptState.Failed, exit);
            progress?.Invoke(new(UpdateStage.Confirming));
            bool confirmed = await confirm();
            // A conferencia pode aguardar inventario/rede. O cancelamento
            // durante essa espera nao transforma uma operacao em sucesso.
            if (token.IsCancellationRequested) return new(UpdateAttemptState.Indeterminate, exit);
            return new(confirmed ? UpdateAttemptState.Confirmed : UpdateAttemptState.Indeterminate, exit);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // A recusa só é confirmada quando a chamada não produziu processo.
            // Um processo já iniciado pode continuar mesmo se uma API posterior
            // devolver ERROR_CANCELLED. Nesse caso, nunca liberar repetição.
            bool started = installerReturned || hasConfirmedInstallerStart?.Invoke() == true;
            return new(started ? UpdateAttemptState.Indeterminate : UpdateAttemptState.Cancelled,
                AuthorizationDeclined: !started);
        }
        catch (UpdateInstallerFailureException ex) { return new(UpdateAttemptState.Failed, ex.ExitCode); }
        catch (TimeoutException) { return new(UpdateAttemptState.Indeterminate); }
        catch (OperationCanceledException)
        { return new(launched ? UpdateAttemptState.Indeterminate : UpdateAttemptState.Cancelled); }
    }
}
