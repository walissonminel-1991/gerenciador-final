using System.Net;
using System.Reflection;
using System.Text;

namespace GerenciadorAplicativos;

// Nunca envia logs e nunca carrega tokens GitHub. Endpoint injeta-se no build.
internal static class BetaTelemetryUploader
{
    private static readonly Uri? Endpoint = FindEndpoint();
    internal static bool IsConfigured => Endpoint is not null;
    private static Uri? FindEndpoint()
    {
        string? value = typeof(BetaTelemetryUploader).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == "GfBetaEndpoint")?.Value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
            uri.HostNameType != UriHostNameType.Dns ||
            !AutomaticPackageManifest.IsPublicHttps(uri)) return null;
        return uri;
    }
    internal static async Task<bool> TrySendAsync(AnonymousTelemetry recorder)
    {
        if (Endpoint is null || recorder.Consent != TelemetryConsent.RemoteBeta) return false;
        string? report = recorder.PendingForRemote();
        if (report is null || !DiagnosticPayloadPolicy.IsValidReport(report)) return false;
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(1700) };
            using var content = new StringContent(report, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(Endpoint, content);
            if (response.StatusCode != HttpStatusCode.Accepted ||
                recorder.Consent != TelemetryConsent.RemoteBeta) return false;
            return recorder.DeletePending();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
            IOException or InvalidOperationException or System.Security.SecurityException) { return false; }
    }
}
