namespace GerenciadorAplicativos;

// URLs oficiais revisadas manualmente. Nunca usar endereco fornecido por catalogo remoto.
// Apenas abre o navegador com consentimento; nao baixa nem executa instaladores.
internal static class OfficialUpdates
{
    internal static Uri? GetPage(string? appName)
    {
        string name = (appName ?? "").Trim();
        string? url = name switch
        {
            _ when name.StartsWith("Adobe Acrobat", StringComparison.OrdinalIgnoreCase) =>
                "https://helpx.adobe.com/br/acrobat/desktop/get-set-up/access-the-app/update-manual.html",
            _ when name.Equals("OBS Studio", StringComparison.OrdinalIgnoreCase) =>
                "https://obsproject.com/pt-br/download",
            _ when name.StartsWith("Microsoft Visual C++ 2013 Redistributable", StringComparison.OrdinalIgnoreCase) =>
                "https://learn.microsoft.com/pt-br/cpp/windows/latest-supported-vc-redist",
            _ when name.Equals("Plex", StringComparison.OrdinalIgnoreCase) =>
                "https://www.plex.tv/media-server-downloads/",
            _ => null
        };
        return url is null ? null : new Uri(url, UriKind.Absolute);
    }
}
