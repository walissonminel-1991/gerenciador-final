using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Uma identidade de inventário do Windows não é uma receita de instalação.
// Não combinar duas instalações só porque usam nome e versão iguais.
internal static class InventoryReconciler
{
    private static readonly Regex MsiId = new(
        @"^\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static string? ProductCodeFromRegistryKey(string? key)
    {
        var id = (key ?? "").Trim();
        return MsiId.IsMatch(id) ? id.ToUpperInvariant() : null;
    }

    internal static IReadOnlyList<ProgramEntry> DistinctRegistryEntries(
        IEnumerable<ProgramEntry> candidates)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<ProgramEntry>();
        foreach (ProgramEntry candidate in candidates)
        {
            // Registro de aplicativos tem identificador por visão/hive/subchave.
            // Identidades desconhecidas não podem ser colapsadas por nome.
            string id = candidate.InventoryId.Trim();
            if (id.Length > 0 && !seen.Add(id)) continue;
            output.Add(candidate);
        }
        return output;
    }

    internal static bool IsSameRegisteredProgram(ProgramEntry first, ProgramEntry second)
    {
        // Usado para detectar mesmo produto se houver leitura repetida, mas NÃO
        // mescla arquiteturas com nomes iguais nem registros de usuário diferentes.
        return first.InventoryId.Length > 0 &&
            first.InventoryId.Equals(second.InventoryId, StringComparison.OrdinalIgnoreCase);
    }
}
