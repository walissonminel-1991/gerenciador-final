using System.IO.Pipes;
using System.Text;
using GerenciadorAplicativos;

// Transporte real, in-process, sem UAC, sem processo elevado e sem manutenção.
// O peer/HELLO do produto permanece responsabilidade do chamador autenticado.
internal static class ElevatedWorkerPipeTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);
    internal static void Run(Action<bool, string> check) => RunAsync(check).GetAwaiter().GetResult();

    private static async Task RunAsync(Action<bool, string> check)
    {
        await WithPipe(async (server, client) =>
        {
            using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
            var exit = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var provisional = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var observe = ElevatedWorkerReadSupervisor.ObserveAsync(
                token => reader.ReadLineAsync(token).AsTask(), token => exit.Task.WaitAsync(token),
                line => provisional.TrySetResult(line), Budget);
            await client.WriteAsync(Encoding.UTF8.GetBytes("RESULT|fixture\nDONE|0\n"));
            await client.FlushAsync();
            check(await provisional.Task.WaitAsync(Budget) == "RESULT|fixture" && !observe.IsCompleted,
                "GF-04 pipe: resultado provisório não confirma antes do encerramento");
            client.Dispose();
            exit.SetResult(0);
            var completed = await observe.WaitAsync(Budget);
            check(completed.Confirmed && completed.ExitCode == 0,
                "GF-04 pipe: retorno, EOF real e saída sintética compatíveis confirmam");
        });
        await WithPipe(async (server, client) =>
        {
            using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
            using var cancellation = new CancellationTokenSource();
            var admission = new ElevatedOperationAdmission();
            using var lease = admission.TryAcquire() ?? throw new InvalidOperationException("fixture");
            int callbacks = 0;
            var pending = ElevatedWorkerReadSupervisor.ObserveAsync(
                token => reader.ReadLineAsync(token).AsTask(), _ => Task.FromResult(0),
                _ => callbacks++, Budget, cancellation.Token);
            cancellation.Cancel();
            var result = await pending.WaitAsync(Budget);
            lease.Complete(result);
            check(result.State == ElevatedObservationState.Cancelled && !result.Confirmed &&
                admission.IsInconclusive && admission.TryAcquire() is null && callbacks == 0,
                "GF-04 pipe: cancelamento de leitura pendente mantém posse e nenhum resultado");
        });
        await WithPipe(async (server, client) =>
        {
            using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
            var pending = ElevatedWorkerReadSupervisor.ObserveAsync(
                token => reader.ReadLineAsync(token).AsTask(), _ => Task.FromResult(0),
                _ => { }, Budget);
            client.Dispose();
            var result = await pending.WaitAsync(Budget);
            check(result.State == ElevatedObservationState.MissingDone && !result.Confirmed,
                "GF-04 pipe: EOF real sem DONE permanece inconclusivo");
        });
    }

    private static async Task WithPipe(Func<NamedPipeServerStream, NamedPipeClientStream, Task> work)
    {
        string name = "GF04_TEST_" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var connect = new CancellationTokenSource(Budget);
        await Task.WhenAll(server.WaitForConnectionAsync(connect.Token), client.ConnectAsync(connect.Token));
        await work(server, client);
    }
}
