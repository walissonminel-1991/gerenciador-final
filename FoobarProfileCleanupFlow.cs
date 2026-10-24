using System.Windows.Forms;

namespace GerenciadorAplicativos;

// O resumo de arquivos nao fixa identidades/conteudo nem o estado do Registro.
// R4 continua suspenso ate existir execucao por alvos presos e revalidados.
internal static class FoobarProfileCleanupFlow
{
    internal static Task<FoobarProfileResult> DeleteConfirmedAsync(
        FoobarProfilePlan authorized, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(new FoobarProfileResult(0, 0, 0,
            ScopedAppProfileCleanup.PreservationReason));
    }

    internal static Task<string> ConfirmAndCleanAsync(Form owner, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(ScopedAppProfileCleanup.PreservationReason);
    }
}
