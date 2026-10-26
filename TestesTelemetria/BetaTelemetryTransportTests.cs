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

        var tampered=Pending(root);File.WriteAllText(tampered.PendingFile,"{\"schema\":1,\"username\":\"synthetic\"}");calls=0;
        using(var client=new HttpClient(new Handler((_,_)=>{calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));})))
            verify(!await BetaTelemetryTransport.TrySendAsync(tampered,Endpoint,client)&&calls==0,
                "R2 relatorio adulterado nao chega ao transporte");
    }
}
