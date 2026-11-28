using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class V269IconQueueRecoveryTests
{
    private sealed class RecreatedControl : Control
    {
        internal void DropHandle()=>DestroyHandle();
        internal void RestoreHandle(){_=Handle;}
    }

    internal static void Run(Action<bool,string> check)
    {
        void WaitFor(Func<bool> ready,string message)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(!ready()&&watch.Elapsed<TimeSpan.FromSeconds(5))
            {Application.DoEvents();Thread.Sleep(5);}
            check(ready(),message);
        }

        using var owner=new RecreatedControl();
        owner.RestoreHandle();
        int uiThread=Environment.CurrentManagedThreadId;
        int reads=0,callbacks=0;
        bool wrongThread=false;
        var failure=new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var missingHandle=new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var late=new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var icon=new Bitmap(16,16);
        using var stream=new MemoryStream();
        icon.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
        byte[] png=stream.ToArray();
        using var cache=new V269AppIconCache(owner,_=>Interlocked.Increment(ref reads) switch
        {
            1=>failure.Task,
            2=>throw new InvalidOperationException("fixture reader"),
            3=>missingHandle.Task,
            4=>Task.FromResult<byte[]?>(new byte[]{1,2,3}),
            5=>Task.FromResult<byte[]?>(png),
            6=>late.Task,
            _=>throw new InvalidOperationException("unexpected duplicate read")
        },(_,_)=>{wrongThread|=Environment.CurrentManagedThreadId!=uiThread;callbacks++;});

        cache.Get(@"C:\GF\icon-failed-async.ico");
        WaitFor(()=>Volatile.Read(ref reads)==1,"AUD-15 primeira leitura ativa");
        cache.Get(@"C:\GF\icon-failed-sync.ico");
        cache.Get(@"C:\GF\icon-lost-hwnd.ico");
        cache.Get(@"C:\GF\icon-invalid.ico");
        cache.Get(@"C:\GF\icon-success.ico");
        check(cache.PendingCount==5,"AUD-15 uma leitura e quatro pendências");
        failure.SetException(new IOException("fixture reader failure"));
        WaitFor(()=>Volatile.Read(ref reads)==3,"AUD-15 exceções síncrona e assíncrona não bloqueiam fila");
        owner.DropHandle();
        check(!owner.IsHandleCreated,"AUD-15 HWND removido na leitura");
        int before=callbacks;
        missingHandle.SetResult(null);
        WaitFor(()=>cache.PendingCount==2,"AUD-15 conclusão sem HWND libera item");
        check(!owner.IsHandleCreated&&Volatile.Read(ref reads)==3&&callbacks==before,
            "AUD-15 não criar HWND, iniciar próxima leitura ou publicar callback sem handle");
        owner.RestoreHandle();
        WaitFor(()=>Volatile.Read(ref reads)==5&&cache.PendingCount==0,
            "AUD-15 recriação HWND retoma fila e bytes inválidos não bloqueiam sucessor");
        check(cache.Get(@"C:\GF\icon-success.ico") is not null,
            "AUD-15 imagem válida disponível após erros");
        check(cache.Get(@"C:\GF\icon-invalid.ico") is null&&cache.PendingCount==0&&!wrongThread,
            "AUD-15 fallback não repete leitura; callbacks exclusivamente UI");

        cache.Get(@"C:\GF\icon-late.ico");
        WaitFor(()=>Volatile.Read(ref reads)==6,"AUD-15 leitura ativa antes do descarte");
        cache.Get(@"C:\GF\icon-not-started.ico");
        before=callbacks;
        owner.Dispose();
        late.SetResult(png);
        var elapsed=System.Diagnostics.Stopwatch.StartNew();
        while(elapsed.Elapsed<TimeSpan.FromMilliseconds(150))
        {Application.DoEvents();Thread.Sleep(5);}
        check(cache.PendingCount==0&&Volatile.Read(ref reads)==6&&callbacks==before,
            "AUD-15 descarte libera fila e descarta resultado tardio");
        Console.WriteLine("PASS AUD-15: recuperação de fila de ícones.");
    }
}
