using Windows.Management.Deployment;

namespace GerenciadorAplicativos;

// A leitura de aplicativos da Microsoft Store é separada do catálogo de
// receitas. Ter um pacote instalado NÃO concede permissão de atualizar ou
// remover. Para todas as identidades não auditadas: somente inventário.
internal sealed record LocalMsixApp(string Name, string Version, string Publisher,
    string PackageName, bool RecipeEligible = false);
internal sealed record LocalMsixResult(IReadOnlyList<LocalMsixApp> Items, int Seen,
    string Status, int Launchable = 0, int Filtered = 0, int Failures = 0);

internal static class LocalMsixInventory
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.WindowsTerminal"] = "Windows Terminal",
        ["Microsoft.OutlookForWindows"] = "Outlook for Windows"
    };

    // Identidades que podem participar das receitas oficiais existentes.
    // Qualquer outro pacote é APENAS um programa identificado.
    internal static LocalMsixApp? Recognize(string packageName, string publisherId,
        ushort major, ushort minor, ushort build, ushort revision)
    {
        if (publisherId != "8wekyb3d8bbwe" ||
            !Known.TryGetValue(packageName, out var title))
            return null;
        return new LocalMsixApp(title, $"{major}.{minor}.{build}.{revision}",
            "Microsoft Corporation", packageName, RecipeEligible: true);
    }

    internal static LocalMsixApp? Describe(string packageName, string publisherId,
        string? friendlyName, string? publisher, bool isFramework, bool isResource,
        bool hasLaunchableApp, ushort major, ushort minor, ushort build, ushort revision)
    {
        // O catálogo verificado continua sendo a única autorização de receitas.
        var trusted = Recognize(packageName, publisherId, major, minor, build, revision);
        if (trusted is not null) return trusted;
        // Não listar runtimes, frameworks, idiomas ou dependências do Windows.
        if (isFramework || isResource || !hasLaunchableApp) return null;
        string name = (friendlyName ?? "").Trim();
        if (name.Length is < 2 or > 120 ||
            name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase) ||
            name.Contains('{') || name.Contains('}') || name.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(packageName)) return null;
        string maker = (publisher ?? "").Trim();
        if (maker.Length > 120 || maker.Any(char.IsControl))
            maker = "";
        return new LocalMsixApp(name, $"{major}.{minor}.{build}.{revision}",
            maker, packageName, RecipeEligible: false);
    }

    internal static LocalMsixResult ReadCurrentUser()
    {
        try
        {
            var found = new Dictionary<string, LocalMsixApp>(StringComparer.OrdinalIgnoreCase);
            int seen = 0, launchable = 0, filtered = 0, failures = 0;
            // Usuário corrente; não requer permissões administrativas nem
            // enumeração de apps privados instalados em outros perfis.
            foreach (var package in new PackageManager().FindPackagesForUser(string.Empty))
            {
                seen++;
                if (seen > 2400) { failures++; break; }
                try
                {
                    var id = package.Id;
                    var v = id.Version;
                    LocalMsixApp? candidate = Recognize(id.Name, id.PublisherId,
                        v.Major, v.Minor, v.Build, v.Revision);
                    if (candidate is null)
                    {
                        if (package.IsFramework || package.IsResourcePackage)
                        { filtered++; continue; }
                        // A aplicação precisa estar efetivamente listada pelo
                        // Windows como aplicativo do usuário (não só dependência).
                        var apps = package.GetAppListEntries();
                        var app = apps.FirstOrDefault();
                        if (app is null) { filtered++; continue; }
                        launchable++;
                        candidate = Describe(id.Name, id.PublisherId,
                            app.DisplayInfo.DisplayName, package.PublisherDisplayName,
                            package.IsFramework, package.IsResourcePackage, true,
                            v.Major, v.Minor, v.Build, v.Revision);
                    }
                    if (candidate is null) { filtered++; continue; }
                    if (found.TryGetValue(id.Name, out var previous) &&
                        Matching.CompareVersions(candidate.Version, previous.Version) is <= 0)
                        continue;
                    found[id.Name] = candidate;
                }
                catch (Exception)
                {
                    failures++; // não invalidar as outras identidades MSIX
                }
            }
            return new LocalMsixResult(found.Values.ToArray(), seen, "ok",
                launchable, filtered, failures);
        }
        catch (Exception ex)
        {
            return new LocalMsixResult([], 0, ex.GetType().Name);
        }
    }

    internal static IReadOnlyList<LocalMsixApp> MergeWithRegistry(
        IReadOnlyList<LocalMsixApp> packages, IReadOnlyList<InstalledApp> registry)
    {
        // A entrada de Registro tem precedência APENAS quando descreve o
        // mesmo aplicativo e fabricante, não um homônimo de outro editor.
        return packages.Where(p => !registry.Any(r =>
        {
            bool matchingManufacturer =
                r.Publisher.Equals(p.Publisher, StringComparison.OrdinalIgnoreCase) ||
                (p.RecipeEligible && r.Publisher.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase));
            if (!matchingManufacturer) return false;
            if (r.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)) return true;
            return p.PackageName switch
            {
                "Microsoft.WindowsTerminal" =>
                    r.Name.Equals("Terminal do Windows", StringComparison.OrdinalIgnoreCase),
                "Microsoft.OutlookForWindows" =>
                    r.Name.Equals("Outlook para Windows", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        })).ToArray();
    }
}
