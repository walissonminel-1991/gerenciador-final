namespace GerenciadorAplicativos;

internal static class BetaEndpointPolicy
{
    // Uma URL de build, sozinha, nunca autoriza um receptor. A lista revisada
    // vem de BetaTelemetryPolicy.props (vazia nesta missao).
    internal static Uri? Validate(string? value, IEnumerable<string> approved)
    {
        if (value is null || !approved.Contains(value, StringComparer.Ordinal) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.HostNameType != UriHostNameType.Dns || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || !uri.IsDefaultPort ||
            !uri.Host.Contains('.') || uri.Host.EndsWith('.') || uri.Host.Any(c => c > 127) ||
            uri.AbsolutePath != "/ingest" || uri.AbsoluteUri != value ||
            !value.StartsWith("https://", StringComparison.Ordinal)) return null;
        return uri;
    }
}
