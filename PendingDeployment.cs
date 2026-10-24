namespace GerenciadorAplicativos;

// O prazo cancela somente a espera do GF. Fonte e operação continuam retidas
// até o provedor terminar; uma segunda instalação não pode correr em paralelo.
internal sealed class PendingDeployment
{
    private int pending;
    private Task? active;
    private Task idle=Task.CompletedTask;
    internal bool IsPending => Volatile.Read(ref pending)!=0;
    internal Task WhenIdle => idle;
    internal async Task RunAsync(Func<Task> begin, IDisposable source, TimeSpan deadline,
        CancellationToken token=default)
    {
        if(Interlocked.CompareExchange(ref pending,1,0)!=0) {
            source.Dispose();
            throw new InvalidOperationException("Deployment anterior ainda pendente. Não repetir.");
        }
        var released=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        idle=released.Task;
        Task? operation=null;
        try {
            token.ThrowIfCancellationRequested();
            operation=begin(); active=operation;
            await operation.WaitAsync(deadline,token);
        } finally {
            if(operation is null || operation.IsCompleted) Release(source,released);
            else _=ReleaseWhenDone(operation,source,released);
        }
    }
    private async Task ReleaseWhenDone(Task operation,IDisposable source,TaskCompletionSource released)
    {
        try { await operation.ConfigureAwait(false); }
        catch(Exception) { /* erro tardio observado; resultado não se transforma em sucesso */ }
        finally { Release(source,released); }
    }
    private void Release(IDisposable source,TaskCompletionSource released)
    {
        try { GC.KeepAlive(active); source.Dispose(); }
        finally { active=null; Volatile.Write(ref pending,0); released.TrySetResult(); }
    }
}
