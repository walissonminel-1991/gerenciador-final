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
            string id = candidate.InventoryId.Trim();
            // Em algumas versões, HKCU\\Software\\...\\Uninstall é uma visão
            // compartilhada: Registry32 e Registry64 apontam à MESMA chave.
            // Colapsar somente quando chave E metadados forem idênticos.
            // HKLM mantém x86/x64 separados em qualquer hipótese.
            if (id.StartsWith("CurrentUser|Registry", StringComparison.OrdinalIgnoreCase))
            {
                string key = id[(id.IndexOf('|', id.IndexOf('|') + 1) + 1)..];
                bool mirrored = output.Any(previous =>
                {
                    string other = previous.InventoryId;
                    if (!other.StartsWith("CurrentUser|Registry", StringComparison.OrdinalIgnoreCase))
                        return false;
                    int divider = other.IndexOf('|', other.IndexOf('|') + 1);
                    return divider >= 0 &&
                        other[(divider + 1)..].Equals(key, StringComparison.OrdinalIgnoreCase) &&
                        previous.Name.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase) &&
                        previous.Version.Equals(candidate.Version, StringComparison.OrdinalIgnoreCase) &&
                        previous.Publisher.Equals(candidate.Publisher, StringComparison.OrdinalIgnoreCase) &&
                        previous.UninstallString.Equals(candidate.UninstallString, StringComparison.OrdinalIgnoreCase) &&
                        previous.QuietUninstallString.Equals(candidate.QuietUninstallString, StringComparison.OrdinalIgnoreCase) &&
                        previous.InstallLocation.Equals(candidate.InstallLocation, StringComparison.OrdinalIgnoreCase) &&
                        previous.InventoryOnly == candidate.InventoryOnly;
                });
                if (mirrored) continue;
            }
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
