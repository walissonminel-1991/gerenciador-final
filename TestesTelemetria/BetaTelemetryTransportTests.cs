using System.Net;
using System.Text.Json;
using GerenciadorAplicativos;

internal static class BetaTelemetryTransportTests
{
    private static readonly Uri Endpoint = new("https://receiver.invalid/ingest");
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
            => callback(request,cancellationToken);
    }
    private static AnonymousTelemetry Pending(string root)
    {
        var recorder=new AnonymousTelemetry(Path.Combine(root,Guid.NewGuid().ToString("N")));
        if(!recorder.SetConsent(TelemetryConsent.RemoteBeta))throw new Exception("fixture consent");
        recorder.Record("MSIX.LEITURA");
        if(!recorder.SaveOnNormalExit("v5.13.11.66"))throw new Exception("fixture pending");
        return recorder;
    }
    internal static async Task Run(string root,Action<bool,string> verify)
    {
        string approved=Endpoint.AbsoluteUri;
        verify(BetaEndpointPolicy.Validate(approved,[approved])==Endpoint,
            "R2 endpoint exato previamente aprovado e permitido apenas na fixture");
        foreach(string url in new[]{"http://receiver.invalid/ingest","https://receiver.invalid.evil/ingest",
            "https://receiver.invalid/ingest?extra=1","https://receiver.invalid/ingest#fragment",
            "https://user@receiver.invalid/ingest","https://receiver.invalid:444/ingest",
            "https://receiver.invalid/ingest/","https://receiver.invalid/%69ngest"})
            verify(BetaEndpointPolicy.Validate(url,[approved]) is null,"R2 endpoint adulterado bloqueado: "+url);
        verify(BetaEndpointPolicy.Validate(approved,[]) is null,"R2 URL de build nao autoriza dominio por si so");

        var choice=Pending(root);
        using(var json=JsonDocument.Parse(File.ReadAllText(choice.ConsentFile)))
            verify(json.RootElement.EnumerateObject().Count()==4 &&
                json.RootElement.GetProperty("notice_version").GetString()==TelemetryConsentPolicy.Version &&
                json.RootElement.GetProperty("notice_sha256").GetString()==TelemetryConsentPolicy.NoticeHash,
                "R2 comprovacao local contem apenas schema, escolha, versao e hash do aviso");
        verify(new AnonymousTelemetry(Path.GetDirectoryName(choice.ConsentFile)).Consent==TelemetryConsent.RemoteBeta,
            "R2 aceite do aviso atual persiste entre aberturas");
        File.WriteAllText(choice.ConsentFile,"{\"schema\":1,\"state\":3}");
        verify(new AnonymousTelemetry(Path.GetDirectoryName(choice.ConsentFile)).Consent==TelemetryConsent.NotAsked,
            "R2 aceite remoto antigo exige nova escolha e nao habilita envio");
        var altered=Pending(root);
        File.WriteAllText(altered.ConsentFile,File.ReadAllText(altered.ConsentFile).Replace(TelemetryConsentPolicy.NoticeHash,"obsolete"));
        verify(new AnonymousTelemetry(Path.GetDirectoryName(altered.ConsentFile)).Consent==TelemetryConsent.NotAsked,
            "R2 aviso diferente ou adulterado falha fechado");

        var declined=Pending(root);declined.SetConsent(TelemetryConsent.Declined);
        int calls=0;
        using(var client=new HttpClient(new Handler((_,_)=>{calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));})))
            verify(!await BetaTelemetryTransport.TrySendAsync(declined,Endpoint,client)&&calls==0,
                "R2 recusa antes do envio nao chama transporte");

        var revoked=Pending(root);
        var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var client=new HttpClient(new Handler(async(_,token)=>{
            entered.TrySetResult(true);await Task.Delay(Timeout.Infinite,token);return new HttpResponseMessage(HttpStatusCode.Accepted);
        })))
        {
            Task<bool> send=BetaTelemetryTransport.TrySendAsync(revoked,Endpoint,client);
            await entered.Task;revoked.SetConsent(TelemetryConsent.Declined);
            verify(!await send&&!File.Exists(revoked.PendingFile),"R2 revogar durante envio cancela transporte e exclui pendencia");
        }

        var concurrent=Pending(root);
        var started=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        calls=0;
        using(var client=new HttpClient(new Handler(async(_,token)=>{
            calls++;started.TrySetResult(true);await release.Task.WaitAsync(token);return new HttpResponseMessage(HttpStatusCode.Accepted);
        })))
        {
            Task<bool> first=BetaTelemetryTransport.TrySendAsync(concurrent,Endpoint,client);
            await started.Task;
            verify(!await BetaTelemetryTransport.TrySendAsync(concurrent,Endpoint,client)&&calls==1,
                "R2 duas tentativas concorrentes nao duplicam POST");
            release.TrySetResult(true);verify(await first,"R2 202 remove somente a pendencia transmitida");
        }

        var newer=Pending(root);
        started=new(TaskCreationOptions.RunContinuationsAsynchronously);release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var client=new HttpClient(new Handler(async(_,token)=>{
            started.TrySetResult(true);await release.Task.WaitAsync(token);return new HttpResponseMessage(HttpStatusCode.Accepted);
        })))
        {
            Task<bool> send=BetaTelemetryTransport.TrySendAsync(newer,Endpoint,client);await started.Task;
            newer.Record("APP.FALHA_INTERFACE");newer.SaveOnNormalExit("v5.13.11.66");release.TrySetResult(true);
            verify(!await send&&File.Exists(newer.PendingFile)&&newer.PendingForRemote()!.Contains("APP.FALHA_INTERFACE"),
                "R2 resposta antiga nao apaga pendencia mais nova");
        }

        foreach(var status in new[]{HttpStatusCode.Redirect,HttpStatusCode.Forbidden,HttpStatusCode.TooManyRequests,HttpStatusCode.InternalServerError})
        {
            var retry=Pending(root);calls=0;
            using var client=new HttpClient(new Handler((_,_)=>{calls++;return Task.FromResult(new HttpResponseMessage(status));}));
            verify(!await BetaTelemetryTransport.TrySendAsync(retry,Endpoint,client)&&calls==1&&File.Exists(retry.PendingFile),
                "R2 falha/redirect nao confirma recebimento nem inicia outra requisicao: "+status);
        }

        var timeout=Pending(root);bool cancelled=false;
        using(var client=new HttpClient(new Handler(async(_,token)=>{
            try{await Task.Delay(Timeout.Infinite,token);}catch(OperationCanceledException){cancelled=true;throw;}
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        })))
            verify(!await BetaTelemetryTransport.TrySendAsync(timeout,Endpoint,client,TimeSpan.FromMilliseconds(20))&&cancelled&&File.Exists(timeout.PendingFile),
                "R2 timeout cancela espera e conserva pendencia para a proxima abertura");

        var disk=Pending(root);started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var client=new HttpClient(new Handler(async(_,token)=>{
            started.TrySetResult(true);await Task.Delay(Timeout.Infinite,token);return new HttpResponseMessage(HttpStatusCode.Accepted);
        })))
        {
            Task<bool> send=BetaTelemetryTransport.TrySendAsync(disk,Endpoint,client);await started.Task;
            File.Delete(disk.ConsentFile);Directory.CreateDirectory(disk.ConsentFile);
            verify(!disk.SetConsent(TelemetryConsent.LocalOnly)&&disk.Consent==TelemetryConsent.LocalOnly&&!await send,
                "R2 retirar envio remoto cancela mesmo se persistir a escolha falhar");
            Directory.Delete(disk.ConsentFile);
        }

        var shared=Pending(root);
        var peer=new AnonymousTelemetry(Path.GetDirectoryName(shared.ConsentFile));
        started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var client=new HttpClient(new Handler(async(_,token)=>{
            started.TrySetResult(true);await Task.Delay(Timeout.Infinite,token);return new HttpResponseMessage(HttpStatusCode.Accepted);
        })))
        {
            Task<bool> send=BetaTelemetryTransport.TrySendAsync(shared,Endpoint,client);await started.Task;
            peer.SetConsent(TelemetryConsent.Declined);
            verify(!await send&&shared.Consent!=TelemetryConsent.RemoteBeta,
                "R2 revogacao persistida por outra instancia cancela envio em andamento");
        }
        var identical=Pending(root);
        peer=new AnonymousTelemetry(Path.GetDirectoryName(identical.ConsentFile));
        started=new(TaskCreationOptions.RunContinuationsAsynchronously);release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        using(var client=new HttpClient(new Handler(async(_,token)=>{
            started.TrySetResult(true);await release.Task.WaitAsync(token);return new HttpResponseMessage(HttpStatusCode.Accepted);
        })))
        {
            Task<bool> send=BetaTelemetryTransport.TrySendAsync(identical,Endpoint,client);await started.Task;
            peer.Record("MSIX.LEITURA");
            verify(peer.SaveOnNormalExit("v5.13.11.66"),"R2 outra instancia pode gravar nova pendencia");
            release.TrySetResult(true);
            verify(!await send&&File.Exists(identical.PendingFile),
                "R2 confirmacao antiga nao remove arquivo novo mesmo com conteudo identico em outra instancia");
        }

        var interrupted=Pending(root);
        interrupted.Record("APP.FALHA_NAO_TRATADA");
        verify(interrupted.SaveOnExit("v5.13.11.66",normalExit:false)&&
            interrupted.PendingForRemote()!.Contains("incompleto"),
            "R3 falha interrompida preserva somente contagens com estado preexistente incompleto");
        using(var client=new HttpClient(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))))
            verify(await BetaTelemetryTransport.TrySendAsync(interrupted,Endpoint,client),
                "R3 pendencia de falha e enviada apenas em nova tentativa explicitamente autorizada");

        var twoInstances=Pending(root);
        peer=new AnonymousTelemetry(Path.GetDirectoryName(twoInstances.ConsentFile));
        started=new(TaskCreationOptions.RunContinuationsAsynchronously);release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        calls=0;
        using(var client=new HttpClient(new Handler(async(_,token)=>{
            calls++;started.TrySetResult(true);await release.Task.WaitAsync(token);return new HttpResponseMessage(HttpStatusCode.Accepted);
        })))
        {
            Task<bool> send=BetaTelemetryTransport.TrySendAsync(twoInstances,Endpoint,client);await started.Task;
            verify(!await BetaTelemetryTransport.TrySendAsync(peer,Endpoint,client)&&calls==1,
                "R3 instancias diferentes nao enviam a mesma pendencia simultaneamente");
            release.TrySetResult(true);verify(await send,"R3 lock entre instancias e liberado depois do 202");
        }
        peer.Record("APP.FALHA_INTERFACE");peer.SaveOnNormalExit("v5.13.11.66");
        using(var client=new HttpClient(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)))))
            verify(await BetaTelemetryTransport.TrySendAsync(peer,Endpoint,client),"R3 outra instancia pode enviar depois da liberacao");

        var processPeer=Pending(root);
        var startInfo=new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
        { UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardInput=true, RedirectStandardError=true };
        if(Path.GetFileNameWithoutExtension(Environment.ProcessPath)=="dotnet")
            startInfo.ArgumentList.Add(typeof(BetaTelemetryTransportTests).Assembly.Location);
        startInfo.ArgumentList.Add("--hold-beta-send-lease");
        startInfo.ArgumentList.Add(Path.GetDirectoryName(processPeer.ConsentFile)!);
        using(var child=System.Diagnostics.Process.Start(startInfo)!)
        {
            try
            {
                verify(await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10))=="GF_FIXTURE_LEASE_READY",
                    "R3 processo filho real segura lease local sem HTTP");
                calls=0;
                using var client=new HttpClient(new Handler((_,_)=>{calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));}));
                verify(!await BetaTelemetryTransport.TrySendAsync(processPeer,Endpoint,client)&&calls==0,
                    "R3 processos distintos nao duplicam envio da pendencia compartilhada");
                verify(processPeer.SetConsent(TelemetryConsent.Declined),
                    "R3 lease em outro processo nao impede revogacao");
                await child.StandardInput.WriteLineAsync("release");
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                verify(child.ExitCode==0,"R3 processo sintetico libera lease ao terminar");
            }
            finally { if(!child.HasExited)child.Kill(entireProcessTree:true); }
        }

        var stale=Pending(root);
        peer=new AnonymousTelemetry(Path.GetDirectoryName(stale.ConsentFile));
        peer.Record("APP.FALHA_INTERFACE");stale.SetConsent(TelemetryConsent.Declined);
        verify(!peer.SaveOnNormalExit("v5.13.11.66")&&!File.Exists(peer.PendingFile),
            "R3 instancia antiga nao recria relatorio depois de revogacao persistida");
        peer.Record("MSIX.LEITURA");
        verify(!peer.Preview("v5.13.11.66",true).Contains("MSIX.LEITURA"),
            "R3 instancia antiga interrompe tambem a contagem local apos revogacao");

        var drop=Pending(root);
        using(var client=new HttpClient(new Handler((_,_)=>throw new HttpRequestException("synthetic_connection_loss"))))
            verify(!await BetaTelemetryTransport.TrySendAsync(drop,Endpoint,client)&&File.Exists(drop.PendingFile),
                "R3 perda de conexao preserva a unica pendencia");
        // Um handler que ignora cancelamento e devolve 202 atrasado nao confirma
        // recebimento dentro do prazo nem apaga uma pendencia.
        var late=Pending(root);
        using(var client=new HttpClient(new Handler(async(_,_)=>{
            await Task.Delay(60);return new HttpResponseMessage(HttpStatusCode.Accepted);
        })))
            verify(!await BetaTelemetryTransport.TrySendAsync(late,Endpoint,client,TimeSpan.FromMilliseconds(10))&&File.Exists(late.PendingFile),
                "R3 202 depois do timeout nao apaga pendencia");

        var close=new BetaCloseCoordinator();
        verify(!close.TryBegin(true,true)&&!close.IsClosing,"R3 fechamento vetado por operacao nao inicia telemetria");
        verify(!close.TryBegin(false,false)&&!close.IsClosing,"R3 recusa ou endpoint ausente permite fechamento sem envio");
        verify(close.TryBegin(false,true)&&close.IsWaiting&&close.IsClosing,"R3 primeiro fechamento inicia uma unica espera");
        verify(!close.TryBegin(false,true),"R3 segundo fechamento durante espera nao duplica envio");
        verify(close.Finish()&&close.IsComplete&&!close.IsWaiting,"R3 concluir espera permite um unico fechamento final");
        verify(!close.TryBegin(false,true),"R3 fechamento final nao reinicia transporte");
        close=new BetaCloseCoordinator();close.TryBegin(false,true);close.Shutdown();
        verify(close.IsShutdown&&!close.IsWaiting&&!close.Finish(),
            "R3 Windows Shutdown prevalece sobre envio e impede reagendar Close");

        var tampered=Pending(root);File.WriteAllText(tampered.PendingFile,"{\"schema\":1,\"username\":\"synthetic\"}");calls=0;
        using(var client=new HttpClient(new Handler((_,_)=>{calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));})))
            verify(!await BetaTelemetryTransport.TrySendAsync(tampered,Endpoint,client)&&calls==0,
                "R2 relatorio adulterado nao chega ao transporte");
    }
}
