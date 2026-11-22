using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// VF02 fase 2: destinos OFICIAIS fixos para conferência humana.
// Nenhuma busca, parsing HTML, download, comparação de versões ou instalação.
internal sealed record GraphicsDriverOfficialSource(
    string Manufacturer, string Name, string PageUrl,
    string OfferedChannel, string OfferedVersion, string ReviewState);

internal static class GraphicsDriverOfficialSources
{
    internal const string ManualReview = "inconclusiva — consulta manual necessária";
    internal const string UnknownOffer = "não verificada";

    private const string NvidiaGeForce = "https://www.nvidia.com/pt-br/geforce/drivers/";
    private const string NvidiaAll = "https://www.nvidia.com/pt-br/drivers/";
    private const string AmdAll = "https://www.amd.com/en/support/download/drivers.html";
    private const string IntelArc = "https://www.intel.com/content/www/us/en/products/docs/discrete-gpus/arc/software/drivers.html";
    // Registro oficial Intel #785597: versão, Windows e lista de produtos.
    // Conteúdo remoto muda com o tempo; somente abrir para revisão humana.
    private const string IntelArcConsumerDetails =
        "https://www.intel.com/content/www/us/en/download/785597/intel-arc-graphics-windows.html";
    private const string IntelAll = "https://www.intel.com/content/www/us/en/download-center/home.html";

    private static readonly HashSet<string> OfficialPages =
        new(StringComparer.Ordinal)
        { NvidiaGeForce, NvidiaAll, AmdAll, IntelArc, IntelArcConsumerDetails, IntelAll };

    // Apenas SKUs de consumo listados nominalmente no registro Intel #785597.
    // Arc Pro, SKU desconhecido e família genérica NÃO são mapeados como oferta.
    // Mesmo para SKU conhecido, a compatibilidade do equipamento/OEM é inconclusiva.
    private static readonly Regex IntelConsumerSku = new(
        @"\bArc(?:\s*(?:\(TM\)|\(R\)|[®™]))*\s+(?:A(?:310|380|580|750|770|350M|370M|530M|550M|570M|730M|770M)|B(?:570|580))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    internal static bool IsApprovedOfficialPage(string? url) =>
        url is not null && OfficialPages.Contains(url) &&
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
        parsed.Scheme == Uri.UriSchemeHttps && parsed.IsDefaultPort &&
        parsed.UserInfo.Length == 0 && parsed.Query.Length == 0 && parsed.Fragment.Length == 0;

    internal static GraphicsDriverOfficialSource? For(GraphicsDriverSnapshot? gpu)
    {
        if (gpu is null) return null;
        // O modelo PnP não comprova que a página selecionou esse produto.
        // O usuário deve selecionar GPU, Windows e canal no site do fabricante.
        string url = gpu.Manufacturer switch
        {
            "NVIDIA" when gpu.Model.Contains("GeForce", StringComparison.OrdinalIgnoreCase) => NvidiaGeForce,
            "NVIDIA" => NvidiaAll,
            "AMD" => AmdAll,
            "Intel" when IntelConsumerSku.IsMatch(gpu.Model) => IntelArcConsumerDetails,
            "Intel" when gpu.Model.Contains("Arc", StringComparison.OrdinalIgnoreCase) => IntelArc,
            "Intel" => IntelAll,
            _ => ""
        };
        if (!IsApprovedOfficialPage(url)) return null;
        return new(gpu.Manufacturer, "Página de drivers do fabricante", url,
            UnknownOffer, UnknownOffer, ManualReview);
    }
}
