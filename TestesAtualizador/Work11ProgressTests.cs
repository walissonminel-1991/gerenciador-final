using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using GerenciadorAplicativos;

internal static class Work11ProgressTests
{
    internal static ProgramEntry App(string id = "LocalMachine|Registry64|GF-W11", string version = "1.0",
        string location = @"C:\GF-Synthetic", string name = "GF Fixture", string publisher = "GF Tests") => new()
        { InventoryId = id, Version = version, Name = name, Publisher = publisher, InstallLocation = location };
    internal static async Task RunAsync(Action<bool, string> check)
    {
        static HttpResponseMessage Response(Stream source, long? length = null, HttpStatusCode status = HttpStatusCode.OK)
        {
            var response = new HttpResponseMessage(status) { Content = new StreamContent(source) };
            response.Content.Headers.ContentLength = length;
            return response;
        }
        async Task<bool> Rejected(HttpResponseMessage response, Type expected, CancellationToken token = default)
        {
            using (response) using (var output = new MemoryStream())
            { try { await UpdateDownloadTransfer.CopyAsync(response, output, 1, 1000, token); return false; }
              catch (Exception ex) { return expected.IsInstanceOfType(ex); } }
        }
        var updates = new List<UpdateProgress>();
        using (var response = Response(new ChunkStream(400, 100, delay: true), 400))
        using (var output = new MemoryStream())
        {
            await UpdateDownloadTransfer.CopyAsync(response, output, 1, 1000, default, updates.Add);
            check(output.Length == 400 && updates.Select(p => p.Percent).Distinct().SequenceEqual(new int?[] {0,25,50,75,100}),
                "W11 download conhecido usa bytes reais 0/25/50/75/100");
        }
        updates.Clear();
        using (var response = Response(new ChunkStream(300, 100)))
        using (var output = new MemoryStream())
        {
            await UpdateDownloadTransfer.CopyAsync(response, output, 1, 1000, default, updates.Add);
            check(updates.All(p => p.Percent is null) && updates.Last().Bytes == 300 && updates.Last().DownloadCompleted,
                "W11 sem Content-Length indica atividade sem porcentagem");
        }
        check(await Rejected(Response(new ChunkStream(400, 100, failAt: 200), 400), typeof(IOException)),
            "W11 download interrompido rejeitado");
        check(await Rejected(Response(new ChunkStream(100,100), status: HttpStatusCode.Forbidden), typeof(HttpRequestException)),
            "W11 HTTP 403 nao chega a instalacao");
        check(await Rejected(Response(new ChunkStream(100,100), 400), typeof(InvalidDataException)),
            "W11 EOF antecipado nao conclui download");
        check(await Rejected(Response(new ChunkStream(400,100), 100), typeof(InvalidDataException)),
            "W11 excesso sobre Content-Length rejeitado");
        using (var cts = new CancellationTokenSource())
        { cts.Cancel(); check(await Rejected(Response(new ChunkStream(400,100),400),typeof(OperationCanceledException),cts.Token),
            "W11 cancelar download realmente interrompe leitura"); }

        check(await Rejected(Response(new ChunkStream(0,100),0),typeof(InvalidDataException)),"W11 tamanho zero rejeitado");
        using (var midCancel = new CancellationTokenSource())
        using (var response=Response(new ChunkStream(400,100,delay:true),400))
        using (var output=new MemoryStream())
        {
            bool cancelled=false;
            try { await UpdateDownloadTransfer.CopyAsync(response,output,1,1000,midCancel.Token,
                p=>{if(p.Bytes>=100)midCancel.Cancel();}); } catch(OperationCanceledException){cancelled=true;}
            check(cancelled && output.Length==100,"W11 cancelamento durante download nao grava bytes restantes");
        }
        var trace = new List<string>();
        var store = new UpdateOperationStore(line => { trace.Add(line); return true; });
        var app = App();
        var operation = store.Begin(app,"2.0")!;
        operation.Report(new(UpdateStage.Downloading, 74,100));
        check(store.Find(app)!.Progress.Percent == 74 && store.Find(app)!.Active,"W11 progresso 74 verdadeiro permanece ativo");
        operation.Report(new(UpdateStage.Downloading, 20,100));
        check(operation.Snapshot.Progress.Bytes == 74,"W11 callback antigo nao regride bytes");
        var reconstructed = App(version:"2.0");
        check(store.Find(reconstructed)!.Id == operation.Snapshot.Id,"W11 novo objeto e nova versao preservam operacao");
        var other = App(id:"CurrentUser|Registry64|GF-W11");
        check(store.Find(other) is null && store.Find(App(location:@"C:\Other")) is null &&
            store.Find(App(id:"LocalMachine|Registry32|GF-W11")) is null &&
            store.Find(App(name:"Outro produto")) is null && store.Find(App(publisher:"Outro editor")) is null,
            "W11 escopo arquitetura pasta produto e editor nao recebem resultado alheio");
        var zip = App(name:"7-Zip 26.04 (x64)");
        check(UpdateOperationStore.InstallationKey(zip) == UpdateOperationStore.InstallationKey(App(name:"7-Zip 26.05 (x64)")),
            "W11 nome 7-Zip muda versao sem perder identidade");
        check(UpdateOperationStore.InstallationKey(App(id:"")) is null,"W11 sem identidade nao associa apenas por nome");
        check(store.Begin(reconstructed,"2.0") is null,"W11 dupla tentativa rejeitada antes de primeiro await");
        operation.Report(new(UpdateStage.Installing));
        check(operation.Snapshot.Progress.Percent is null,"W11 instalador sem medicao nao inventa numero");
        operation.Complete(new(UpdateAttemptState.Confirmed));
        operation.Report(new(UpdateStage.Downloading,1,100));
        check(operation.Snapshot.Progress.Stage == UpdateStage.Confirmed && operation.Snapshot.Progress.Percent == 100 &&
            store.Find(reconstructed)!.Id == operation.Snapshot.Id,"W11 sucesso rapido persiste e callback tardio nao apaga");
        check(store.Begin(reconstructed,"2.0") is null,"W11 snapshot antigo nao repete alvo confirmado");

        async Task<UpdateOperationSnapshot> Attempt(int exit, bool confirm, Action<CancellationTokenSource>? cancel = null,
            Func<Task<int>>? launch = null, bool valid = true)
        {
            var op = new UpdateOperationStore().Begin(App(),"2.0")!;
            using var token = new CancellationTokenSource(); cancel?.Invoke(token);
            var result = await new UpdateLaunchGate().RunAsync(() => Task.FromResult(valid),
                launch ?? (() => Task.FromResult(exit)), () => Task.FromResult(confirm), token.Token,op.Report);
            op.Complete(result); return op.Snapshot;
        }
        check((await Attempt(0,true)).Progress.Stage == UpdateStage.Confirmed,"W11 retorno zero e versao comprovada confirma");
        check((await Attempt(0,false)).Progress.Stage == UpdateStage.Pending,"W11 retorno zero sem versao permanece amarelo");
        check((await Attempt(0,true,launch:()=>throw new UpdateInstallerFailureException(unchecked((int)0x80073CF9)))).Progress.Stage==UpdateStage.Failed,
            "W11 erro nativo MSIX comprovado e vermelho");
        check((await Attempt(1603,true)).Progress.Stage == UpdateStage.Failed,"W11 retorno de erro permanece vermelho");
        var restart=await Attempt(3010,true);
        check(restart.RestartRequired && restart.Text.Contains("reinicie"),"W11 3010 confirmado preserva necessidade de reinicio");
        var pendingRestart=await Attempt(3010,false);
        check(pendingRestart.RestartRequired && pendingRestart.Progress.Stage == UpdateStage.Pending,"W11 3010 sem versao nao vira sucesso");
        check((await Attempt(0,true, valid:false)).Progress.Stage == UpdateStage.Blocked,"W11 identidade incompativel bloqueia");
        check((await Attempt(0,true,cancel:c=>c.Cancel())).Progress.Stage == UpdateStage.Cancelled,"W11 cancelar antes do lancamento cinza");
        check((await Attempt(0,true,launch:()=>throw new Win32Exception(1223))).Progress.Stage == UpdateStage.AuthorizationDeclined,
            "W11 recusa UAC difere de cancelamento do download");
        using (var token = new CancellationTokenSource())
        {
            var pendingStore = new UpdateOperationStore(); var pending = pendingStore.Begin(App(),"2.0")!;
            var result = await new UpdateLaunchGate().RunAsync(()=>Task.FromResult(true),
                ()=>{token.Cancel();return Task.FromResult(0);},()=>Task.FromResult(true),token.Token,pending.Report);
            pending.Complete(result);
            check(pending.Snapshot.Progress.Stage == UpdateStage.Pending && pendingStore.Begin(App(),"2.0") is null,
                "W11 cancelamento depois de lancar nao afirma encerramento nem autoriza repeticao");
        }
        foreach (var stage in new[] {UpdateStage.VerifyingIntegrity,UpdateStage.ValidatingSignature})
        {
            var op=new UpdateOperationStore().Begin(App(),"2.0")!;op.Report(new(stage));op.Fail(new CryptographicException(),false);
            check(op.Snapshot.Progress.Stage==UpdateStage.Failed && op.Snapshot.Reason.Contains(stage==UpdateStage.ValidatingSignature?"Assinatura":"SHA-256"),
                "W11 rejeicao criptografica identifica etapa "+stage);
        }
        var launchBlocked=new UpdateOperationStore().Begin(App(),"2.0")!;
        launchBlocked.Fail(new Win32Exception(5),false);
        check(launchBlocked.Snapshot.Progress.Stage==UpdateStage.Blocked && launchBlocked.Snapshot.ExitCode==5,
            "W11 Windows bloqueia inicio com motivo e codigo nativo preservados");
        var diag=new UpdateOperationStore(_=>throw new IOException("C:\\Users\\PRIVATE\\secret"));
        var noLog=diag.Begin(App(),"2.0")!;noLog.Complete(new(UpdateAttemptState.Confirmed));
        check(noLog.Snapshot.DiagnosticFailed && noLog.Snapshot.Progress.Stage==UpdateStage.Confirmed,
            "W11 falha de diagnostico informada sem apagar resultado comprovado");
        check(trace.All(t=>!t.Contains(@"C:\")&&!t.Contains("GF Fixture")) && trace.Any(t=>t.Contains("ATUALIZACAO.FIM")),
            "W11 diagnostico correlaciona op sem caminhos nomes ou conteudo privado");
        var next=store.Begin(other,"2.0")!;next.Complete(new(UpdateAttemptState.Failed,1603));
        check(store.Find(app)!.Progress.Stage==UpdateStage.Confirmed && store.Find(other)!.Progress.Stage==UpdateStage.Failed,
            "W11 atualizacoes sequenciais nao trocam resultado");
        var raced = new UpdateOperationStore(); var wins = await Task.WhenAll(Enumerable.Range(0,32).Select(_=>Task.Run(()=>raced.Begin(App(),"2.0"))));
        check(wins.Count(x=>x is not null)==1,"W11 32 tentativas concorrentes possuem um unico dono");
        var bounded=new UpdateOperationStore();
        for(int i=0;i<320;i++) bounded.Begin(App(id:"Synthetic|"+i),"2.0")!.Complete(new(UpdateAttemptState.Confirmed));
        check(bounded.Find(App(id:"Synthetic|0")) is null && bounded.Find(App(id:"Synthetic|319")) is not null,
            "W11 memoria de resultados possui limite de 256 entradas");
        var cachedApp=App();store.Find(cachedApp);
        long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
        for(int i=0;i<100_000;i++)store.Find(cachedApp);
        Console.WriteLine($"W11 lookup: 100000 consultas em {watch.ElapsedMilliseconds} ms; {GC.GetAllocatedBytesForCurrentThread()-allocated} bytes alocados");
        // PE/processo controlado, sem instalacao real, sem UAC e sem acesso a aplicativos pessoais.
        using (var process=Process.Start(new ProcessStartInfo
        {
            FileName=Environment.ProcessPath!, UseShellExecute=false,
            ArgumentList={ typeof(Work11ProgressTests).Assembly.Location,"--work11-installer-fixture" }
        })!)
        {
            await process.WaitForExitAsync();
            check(process.ExitCode==0,"W11 processo descartavel real termina sem instalar aplicativos");
        }
        Console.WriteLine("W11 progress: controles deterministas concluidos");
    }
    private sealed class ChunkStream(int bytes,int chunk,bool delay=false,int failAt=int.MaxValue):Stream
    {
        int position;
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException(); public override long Position{get=>position;set=>throw new NotSupportedException();}
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(position>=failAt)throw new IOException("Fixture interrupted");
            if(position>=bytes)return 0;
            if(delay)await Task.Delay(130,cancellationToken);
            int count=Math.Min(buffer.Length,Math.Min(chunk,bytes-position));buffer.Span[..count].Fill(17);position+=count;return count;
        }
        public override int Read(byte[] b,int o,int c)=>ReadAsync(b.AsMemory(o,c)).AsTask().GetAwaiter().GetResult();
        public override void Flush()=>throw new NotSupportedException(); public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();
        public override void SetLength(long v)=>throw new NotSupportedException();public override void Write(byte[]b,int o,int c)=>throw new NotSupportedException();
    }
}
