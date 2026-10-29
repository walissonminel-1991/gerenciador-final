using System.Net.Http.Headers;

namespace GerenciadorAplicativos;

internal static class ProductHttpIdentity
{
    // A versao efetiva do mesmo assembly que executa o cliente; sem ID de maquina.
    internal static string Version => typeof(ProductHttpIdentity).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    internal static ProductInfoHeaderValue UserAgent => new("GerenciadorFinal", Version);
}
