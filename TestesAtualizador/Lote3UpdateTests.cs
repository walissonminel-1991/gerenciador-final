using System.ComponentModel;
using System.Net;
using GerenciadorAplicativos;

internal static class Lote3UpdateTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        foreach (int code in new[] { 225, 226, 5, 32 })
        {
            var app = new ProgramEntry { InventoryId = "GF-L3-"+code, Name="GF Synthetic", Publisher="GF Test" };
            var op = new UpdateOperationStore().Begin(app,"2.0")!;
            op.Fail(new Win32Exception(code), false);
            check(op.Snapshot.Progress.Stage == UpdateStage.Blocked &&
                op.Snapshot.Reason.Contains("antivírus") == WindowsExecutionFailure.IsAntivirus(code),
                "L3 erro Windows distingue antivírus de acesso negado/compartilhamento: "+code);
        }
        int starts = 0;
        var gate = new UpdateLaunchGate();
        var lateError = await gate.RunAsync(() => Task.FromResult(true), () => { starts++; return Task.FromResult(0); },
            () => throw new Win32Exception(1223));
        check(lateError.State == UpdateAttemptState.Indeterminate && !lateError.AuthorizationDeclined && starts == 1,
            "L3 pós-verificação erro 1223 conserva instalação iniciada, sem falso UAC recusado");
        check((await gate.RunAsync(() => Task.FromResult(true), () => { starts++; return Task.FromResult(0); },
            () => Task.FromResult(true))).State == UpdateAttemptState.AlreadyAttempted && starts == 1,
            "L3 autorização consumida nunca permite segundo lançamento");
        var denied = await new UpdateLaunchGate().RunAsync(() => Task.FromResult(true),
            () => throw new Win32Exception(1223), () => Task.FromResult(true));
        check(denied.State == UpdateAttemptState.Cancelled && denied.AuthorizationDeclined,
            "L3 recusa UAC real antes de obter processo permanece separada");

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var concurrent = new UpdateLaunchGate();
        int launches = 0;
        var first = concurrent.RunAsync(() => Task.FromResult(true), async () =>
        { Interlocked.Increment(ref launches); entered.SetResult(); await release.Task; return 0; }, () => Task.FromResult(true));
        await entered.Task;
        var attempts = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => concurrent.RunAsync(
            () => Task.FromResult(true), () => { Interlocked.Increment(ref launches); return Task.FromResult(0); }, () => Task.FromResult(true))));
        release.SetResult();
        check((await first).State == UpdateAttemptState.Confirmed && launches == 1 &&
            attempts.All(a => a.State == UpdateAttemptState.AlreadyAttempted),
            "L3 32 concorrentes não sobrepõem o instalador já iniciado");

        static HttpResponseMessage Response(byte[] bytes, long? declared = null)
        {
            var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(bytes, false)) };
            r.Content.Headers.ContentLength = declared ?? bytes.Length;
            return r;
        }
        byte[] payload = new byte[512 * 1024];
        using (var warm = Response(payload)) await UpdateDownloadTransfer.CopyAsync(warm, Stream.Null, 1, payload.Length, default);
        const int rounds = 64;
        long before = GC.GetTotalAllocatedBytes(true);
        for (int i = 0; i < rounds; i++)
        {
            using var r = Response(payload);
            await UpdateDownloadTransfer.CopyAsync(r, Stream.Null, 1, payload.Length, default);
        }
        long allocated = GC.GetTotalAllocatedBytes(true) - before;
        Console.WriteLine($"L3_DOWNLOAD memoria_alocada_bytes={allocated} rodadas={rounds} payload_bytes={payload.Length}");
        check(allocated < rounds * 48L * 1024,
            "L3 downloads sucessivos reutilizam buffer sem alocar 128 KiB por chamada");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        bool stopped = false, excess = false;
        try { using var r = Response(payload); await UpdateDownloadTransfer.CopyAsync(r, Stream.Null, 1, payload.Length, cancel.Token); }
        catch (OperationCanceledException) { stopped = true; }
        try { using var r = Response(payload, 100); await UpdateDownloadTransfer.CopyAsync(r, Stream.Null, 1, payload.Length, default); }
        catch (InvalidDataException) { excess = true; }
        check(stopped && excess, "L3 cancelamento e corpo maior que Content-Length continuam recusados");
        using (var after = Response(payload)) await UpdateDownloadTransfer.CopyAsync(after, Stream.Null, 1, payload.Length, default);
        check(true, "L3 buffer continua utilizável após cancelamento e falha de tamanho");
    }
}
