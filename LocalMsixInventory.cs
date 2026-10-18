using Windows.Management.Deployment;

namespace GerenciadorAplicativos;

// Inventário exclusivamente local e de leitura; não instala pacotes,
// não chama PowerShell, WinGet, COM auxiliar nem enumera contas de outros usuários.
internal sealed record LocalMsixApp(string Name, string Version, string Publisher, string PackageName);
internal sealed record LocalMsixResult(IReadOnlyList<LocalMsixApp> Items, int Seen, string Status);

internal static class LocalMsixInventory
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.WindowsTerminal"] = "Windows Terminal",
        ["Microsoft.OutlookForWindows"] = "Outlook for Windows"
    };

    // Famílias assinadas pela Microsoft Store; qualquer outra identidade fica fora.
    internal static LocalMsixApp? Recognize(string packageName, string publisherId,
        ushort major, ushort minor, ushort build, ushort revision)
    {
        if (publisherId != "8wekyb3d8bbwe" ||
            !Known.TryGetValue(packageName, out var title))
            return null;
        return new LocalMsixApp(title, $"{major}.{minor}.{build}.{revision}",
            "Microsoft Corporation", packageName);
    }

    internal static LocalMsixResult ReadCurrentUser()
    {
        try
        {
            var found = new Dictionary<string, LocalMsixApp>(StringComparer.OrdinalIgnoreCase);
            int seen = 0;
            // String.Empty representa o usuário corrente. Não precisa elevar privilégios.
            foreach (var package in new PackageManager().FindPackagesForUser(string.Empty))
            {
                seen++;
                if (seen > 2000) break; // limite de segurança para inventários inesperados
                try
                {
                    var id = package.Id;
                    var v = id.Version;
                    var normalized = Recognize(id.Name, id.PublisherId,
                        v.Major, v.Minor, v.Build, v.Revision);
                    if (normalized is null) continue;
                    if (found.TryGetValue(id.Name, out var previous) &&
                        Matching.CompareVersions(normalized.Version, previous.Version) is <= 0)
                        continue;
                    found[id.Name] = normalized;
                }
                catch { /* Pacote transitório não invalida os demais. */ }
            }
            return new LocalMsixResult(found.Values.ToArray(), seen, "ok");
        }
        catch (Exception ex)
        {
            // Diagnóstico registra apenas o tipo do erro, sem nomes de pacotes ou SID.
            return new LocalMsixResult([], 0, ex.GetType().Name);
        }
    }

    internal static IReadOnlyList<LocalMsixApp> MergeWithRegistry(
        IReadOnlyList<LocalMsixApp> packages, IReadOnlyList<InstalledApp> registry)
    {
        // Evitar duas linhas para a mesma instalação. O registro Win32 tem prioridade.
        return packages.Where(p => !registry.Any(r =>
            (p.PackageName == "Microsoft.WindowsTerminal" &&
             (r.Name.Equals("Windows Terminal", StringComparison.OrdinalIgnoreCase) ||
              r.Name.Equals("Terminal do Windows", StringComparison.OrdinalIgnoreCase))) ||
            (p.PackageName == "Microsoft.OutlookForWindows" &&
             (r.Name.Equals("Outlook for Windows", StringComparison.OrdinalIgnoreCase) ||
              r.Name.Equals("Outlook para Windows", StringComparison.OrdinalIgnoreCase))))).ToArray();
    }
}
