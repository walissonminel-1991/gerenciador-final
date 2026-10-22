using GerenciadorAplicativos;
internal static class PendingDeploymentTests
{
    private sealed class Lease : IDisposable { internal int Releases; public void Dispose()=>Releases++; }
    internal static async Task Run(Action<bool,string> check)
    {
        var coordinator=new PendingDeployment(); var lease=new Lease(); int starts=0;
        await coordinator.RunAsync(()=>{starts++;return Task.CompletedTask;},lease,TimeSpan.FromMinutes(10));
        check(starts==1 && lease.Releases==1 && !coordinator.IsPending,"W6-MSIX-CONTROL provedor concluído libera fonte uma vez");
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); lease=new();
        bool timeout=false;
        try { await coordinator.RunAsync(()=>{starts++;return completion.Task;},lease,TimeSpan.Zero); }
        catch(TimeoutException) { timeout=true; }
        check(timeout && coordinator.IsPending && lease.Releases==0 && starts==2,
            "W6-MSIX-LEASE prazo da espera não libera pacote ainda usado pelo provedor");
        var secondLease=new Lease();bool blocked=false;
        try { await coordinator.RunAsync(()=>{starts++;return Task.CompletedTask;},secondLease,TimeSpan.FromMinutes(10)); }
        catch(InvalidOperationException) { blocked=true; }
        check(blocked && starts==2 && secondLease.Releases==1,"W6-MSIX-CONCURRENT segunda operação pendente não chama provedor");
        completion.SetException(new IOException("falha tardia simulada"));
        await coordinator.WhenIdle;
        check(lease.Releases==1 && !coordinator.IsPending && starts==2,"W6-MSIX-LATE erro tardio observado libera fonte sem repetir ou anunciar sucesso");
        using var cancel=new CancellationTokenSource();cancel.Cancel();lease=new();
        bool cancelled=false;
        try { await coordinator.RunAsync(()=>{starts++;return Task.CompletedTask;},lease,TimeSpan.FromMinutes(10),cancel.Token); }
        catch(OperationCanceledException) { cancelled=true; }
        check(cancelled && lease.Releases==1 && starts==2 && !coordinator.IsPending,"W6-MSIX-PRE cancelamento anterior produz zero novas chamadas e libera fonte");
        lease=new();bool failed=false;
        try { await coordinator.RunAsync(()=>{starts++;throw new IOException("início simulado falhou");},lease,TimeSpan.FromMinutes(10)); }
        catch(IOException) { failed=true; }
        check(failed && lease.Releases==1 && starts==3 && !coordinator.IsPending,"W6-MSIX-START falha de início não retém fonte ou bloqueio indevido");
        using var after=new CancellationTokenSource();
        completion=new(TaskCreationOptions.RunContinuationsAsynchronously);lease=new();
        var pending=coordinator.RunAsync(()=>{starts++;return completion.Task;},lease,TimeSpan.FromMinutes(10),after.Token);
        after.Cancel();cancelled=false;
        try { await pending; } catch(OperationCanceledException) { cancelled=true; }
        check(cancelled && coordinator.IsPending && lease.Releases==0,"W6-MSIX-POST cancelar espera depois do início mantém fonte protegida");
        completion.SetResult();await coordinator.WhenIdle;
        check(lease.Releases==1 && !coordinator.IsPending && starts==4,"W6-MSIX-END conclusão posterior libera somente a operação original");
    }
}
