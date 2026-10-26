using System.Net;
using System.Text;

namespace GerenciadorAplicativos;

// Transporte testavel com HttpMessageHandler sintetico; nao resolve endpoint,
// credencial ou servico externo. O chamador real aplica BetaEndpointPolicy.
internal static class BetaTelemetryTransport
{
    internal static async Task<bool> TrySendAsync(AnonymousTelemetry recorder, Uri endpoint,
        HttpClient client, TimeSpan? timeout = null)
    {
        using var lease = recorder.TryBeginRemoteSend();
        if (lease is null) return false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lease.Cancellation);
        deadline.CancelAfter(timeout ?? TimeSpan.FromMilliseconds(1700));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(lease.Report, Encoding.UTF8, "application/json")
            };
            deadline.Token.ThrowIfCancellationRequested();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            return response.StatusCode == HttpStatusCode.Accepted &&
                recorder.CompleteRemoteSend(lease);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or
            IOException or InvalidOperationException or System.Security.SecurityException) { return false; }
    }
}
