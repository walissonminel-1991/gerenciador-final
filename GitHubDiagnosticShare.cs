using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// GitHub-only piloto: monta uma Issue PUBLICA a ser revisada/publicada pelo
// proprio usuario no navegador. Nenhum token, upload ou requisicao HTTP no GF.
internal static class GitHubDiagnosticShare
{
    internal const string RepositoryIssuesUrl =
        "https://github.com/walissonminel-1991/gerenciador-final/issues/new";
    private static readonly Regex VersionPattern =
        new(@"^v[0-9]{1,3}(?:\.[0-9]{1,4}){3}$", RegexOptions.CultureInvariant);

    // Esta URL nunca dispara criacao de Issue: so abre o formulario do GitHub.
    // Valida tudo outra vez, embora AnonymousTelemetry gere o JSON local.
    internal static Uri? CreateReviewUrl(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) ||
            Encoding.UTF8.GetByteCount(json) > 4096) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Count() != 4 ||
                !root.TryGetProperty("schema", out var schema) ||
                schema.ValueKind != JsonValueKind.Number || schema.GetInt32() != 1 ||
                !root.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.String ||
                !VersionPattern.IsMatch(version.GetString() ?? "") ||
                !root.TryGetProperty("exit", out var exit) ||
                exit.ValueKind != JsonValueKind.String ||
                exit.GetString() is not ("normal" or "incompleto") ||
                !root.TryGetProperty("counts", out var counts) ||
                counts.ValueKind != JsonValueKind.Object)
                return null;

            // Nada que nao seja categoria oficial reconhecida pode tornar-se
            // texto publico; tambem nao enviar relatorio vazio ou arquivos editados.
            var rows = counts.EnumerateObject().ToArray();
            if (rows.Length is < 1 or > 14 || rows.Select(x => x.Name).Distinct().Count() != rows.Length)
                return null;
            foreach (var row in rows)
            {
                if (!AnonymousTelemetry.IsAllowedCategory(row.Name) ||
                    row.Value.ValueKind != JsonValueKind.Number ||
                    !row.Value.TryGetInt32(out int n) || n is < 1 or > 100)
                    return null;
            }

            string gfVersion = version.GetString()!;
            var body = new StringBuilder();
            body.AppendLine("## Diagnóstico voluntário do GERENCIADOR FINAL");
            body.AppendLine();
            body.AppendLine("> Esta Issue será PÚBLICA e ficará associada à sua conta GitHub.");
            body.AppendLine("> Confira o conteúdo antes de clicar em Submit new issue.");
            body.AppendLine("> Nenhum relatório completo, nome de programa, arquivo ou caminho foi incluído.");
            body.AppendLine();
            body.AppendLine("**Versão do GF:** " + gfVersion);
            body.AppendLine("**Encerramento observado:** " + exit.GetString());
            body.AppendLine();
            body.AppendLine("### Ocorrências técnicas (agregadas; não representam usuários únicos)");
            foreach (var row in rows.OrderBy(x => x.Name, StringComparer.Ordinal))
                body.Append("- ").Append(row.Name).Append(": ")
                    .AppendLine(row.Value.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture));
            body.AppendLine();
            body.AppendLine("### Observações");
            body.AppendLine("Se desejar, descreva o problema SEM publicar dados pessoais, arquivos ou logs completos.");
            body.AppendLine("Este formulário é enviado somente após sua confirmação no GitHub.");
            string title = "[GF Diagnostico] " + gfVersion + " - resumo voluntario";
            string url = RepositoryIssuesUrl + "?title=" + Uri.EscapeDataString(title) +
                "&body=" + Uri.EscapeDataString(body.ToString());
            return url.Length <= 7000 ? new Uri(url) : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or
            FormatException or ArgumentException or OverflowException)
        { return null; }
    }
}
