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
        var metadata = typeof(BetaTelemetryUploader).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
        string? source = metadata.FirstOrDefault(x => x.Key == "GfSourceCommit")?.Value;
        if (source is not { Length: 40 } || source.Any(c => !char.IsAsciiHexDigit(c)) ||
            metadata.FirstOrDefault(x => x.Key == "GfPrivacyNoticeVersion")?.Value != TelemetryConsentPolicy.Version ||
            metadata.FirstOrDefault(x => x.Key == "GfReviewedPrivacyNoticeSha256")?.Value != TelemetryConsentPolicy.NoticeHash)
            return null;
        return BetaEndpointPolicy.Validate(metadata.FirstOrDefault(x => x.Key == "GfBetaEndpoint")?.Value,
            metadata.Where(x => x.Key == "GfApprovedBetaEndpoint").Select(x => x.Value ?? ""));
    }
    internal static async Task<bool> TrySendAsync(AnonymousTelemetry recorder)
    {
        if (Endpoint is null) return false;
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(1700) };
            return await BetaTelemetryTransport.TrySendAsync(recorder, Endpoint, client);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
            IOException or InvalidOperationException or System.Security.SecurityException) { return false; }
    }
}
