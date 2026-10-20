using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Politica interna de dados: valida somente objetos de contagem, nunca arquivos
// anexados, comentarios, comandos, logs completos ou URLs de terceiros.
// NENHUMA rotina de envio de rede existe neste componente.
internal static class DiagnosticPayloadPolicy
{
    private static readonly Regex VersionPattern =
        new(@"^v[0-9]{1,3}(?:\.[0-9]{1,4}){3}\z",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100));

    internal static bool IsValidVersion(string? version) =>
        version is not null && VersionPattern.IsMatch(version);

    internal static bool IsValidReport(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) ||
            Encoding.UTF8.GetByteCount(json) > 4096) return false;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 5
            });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var properties = root.EnumerateObject().ToArray();
            if (properties.Length != 4 ||
                properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != 4 ||
                !root.TryGetProperty("schema", out var schema) ||
                schema.ValueKind != JsonValueKind.Number ||
                !schema.TryGetInt32(out int schemaVersion) || schemaVersion != 1 ||
                !root.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.String ||
                !IsValidVersion(version.GetString()) ||
                !root.TryGetProperty("exit", out var exit) ||
                exit.ValueKind != JsonValueKind.String ||
                exit.GetString() is not ("normal" or "incompleto") ||
                !root.TryGetProperty("counts", out var counts) ||
                counts.ValueKind != JsonValueKind.Object)
                return false;
            var rows = counts.EnumerateObject().ToArray();
            if (rows.Length is < 1 or > 14 ||
                rows.Select(r => r.Name).Distinct(StringComparer.Ordinal).Count() != rows.Length)
                return false;
            foreach (var row in rows)
                if (!AnonymousTelemetry.IsAllowedCategory(row.Name) ||
                    row.Value.ValueKind != JsonValueKind.Number ||
                    !row.Value.TryGetInt32(out int n) || n is < 1 or > 100)
                    return false;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or
            FormatException or ArgumentException or OverflowException)
        {
            return false;
        }
    }
}
