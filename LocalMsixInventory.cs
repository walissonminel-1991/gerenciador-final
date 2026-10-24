using Windows.Management.Deployment;

namespace GerenciadorAplicativos;

// A leitura de aplicativos da Microsoft Store é separada do catálogo de
// receitas. Ter um pacote instalado NÃO concede permissão de atualizar ou
// remover. Para todas as identidades não auditadas: somente inventário.
internal sealed record LocalMsixApp(string Name, string Version, string Publisher,
    string PackageName, bool RecipeEligible = false, bool Stale = false);
internal sealed record LocalMsixResult(IReadOnlyList<LocalMsixApp> Items, int Seen,
    string Status, int Launchable = 0, int Filtered = 0, int Failures = 0,
    bool Retried = false, string FailureStage = "", string Apartment = "");
internal sealed record LocalMsixObservation(LocalMsixApp? App, bool Launchable = false);

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

    // REGDB_E_CLASSNOTREG e ERROR_KEY_DELETED podem indicar ativacao WinRT ou
    // Registro do usuario momentaneamente indisponivel. Uma unica nova tentativa ANTES de observar pacotes, sem PowerShell, processo
    // auxiliar, elevacao ou acesso a outros perfis. Se persistir, falha
    // explicitamente: lista MSIX vazia nunca significa zero apps instalados.
    private static readonly LocalMsixSnapshot Snapshot = new();
    internal static LocalMsixResult ReadCurrentUser() =>
        Snapshot.Read(() => ReadWithRetry(ReadOnce, () => Thread.Sleep(250)));

    internal static LocalMsixResult ReadWithRetry(Func<LocalMsixResult> read,
        Action? beforeRetry = null)
    {
        var first = read();
        // Uma enumeração já iniciada tem evidência parcial útil. Não a trocar
        // por outra tentativa que pode falhar antes do primeiro pacote.
        if (!IsTransientMsixFailure(first.Status) || first.Seen != 0 || first.Items.Count != 0)
            return first;
        // Falhas COM 0x80040154/0x800703FA podem ser transitorias: um
        // intervalo curto evita repetir imediatamente a mesma ativacao
        // enquanto o Registro/COM ainda esta sendo atualizado. Apenas em
        // worker de inventario, sem bloquear a thread da interface.
        beforeRetry?.Invoke();
        var second = read();
        return second with { Retried = true };
    }

    // Confirmação operacional nunca usa retenção ou observação parcial.
    // A identidade de publisherId já foi verificada por Recognize; nome/editor
    // de exibição de um pacote genérico não substituem essa autorização.
    internal static Version? ConfirmedMicrosoftVersion(LocalMsixResult scan, string packageName)
    {
        if (scan.Status != "ok" || scan.Failures != 0 || !Known.ContainsKey(packageName)) return null;
        var item = scan.Items.FirstOrDefault(x => !x.Stale && x.RecipeEligible &&
            x.PackageName.Equals(packageName, StringComparison.OrdinalIgnoreCase) &&
            x.Publisher.Equals("Microsoft Corporation", StringComparison.Ordinal));
        return item is not null && Version.TryParse(item.Version, out var version) ? version : null;
    }

    internal static bool IsClassNotRegistered(string status) =>
        string.Equals(status, "COMException(0x80040154)", StringComparison.Ordinal);

    // 0x800703FA = ERROR_KEY_DELETED: uma segunda consulta pode se recuperar,
    // mas jamais declarar ausencia de aplicativos quando ambos acessos falham.
    internal static bool IsTransientMsixFailure(string status) =>
        IsClassNotRegistered(status) ||
        string.Equals(status, "COMException(0x800703FA)", StringComparison.Ordinal);

    private static LocalMsixResult ReadOnce()
    {
        PackageManager manager;
        try
        {
            manager = new PackageManager();
        }
        catch (Exception ex) { return Failed(ex, "activation"); }

        IEnumerable<Windows.ApplicationModel.Package> packages;
        try
        {
            // Usuário corrente; não requer permissões administrativas nem
            // enumeração de apps privados instalados em outros perfis.
            packages = manager.FindPackagesForUser(string.Empty);
        }
        catch (Exception ex) { return Failed(ex, "query"); }

        return ReadPackages(packages, package =>
        {
            var id = package.Id;
            var v = id.Version;
            var candidate = Recognize(id.Name, id.PublisherId,
                v.Major, v.Minor, v.Build, v.Revision);
            if (candidate is not null) return new(candidate);
            if (package.IsFramework || package.IsResourcePackage) return new(null);
            // Apenas apps efetivamente listados pelo Windows para o usuário.
            var app = package.GetAppListEntries().FirstOrDefault();
            if (app is null) return new(null);
            return new(Describe(id.Name, id.PublisherId,
                app.DisplayInfo.DisplayName, package.PublisherDisplayName,
                package.IsFramework, package.IsResourcePackage, true,
                v.Major, v.Minor, v.Build, v.Revision), Launchable: true);
        });
    }

    // A enumeração COM é preguiçosa: MoveNext pode falhar DEPOIS de produzir
    // itens. Preservar o que foi observado, sempre com cobertura parcial.
    internal static LocalMsixResult ReadPackages<T>(IEnumerable<T> packages,
        Func<T, LocalMsixObservation> describe)
    {
        var found = new Dictionary<string, LocalMsixApp>(StringComparer.OrdinalIgnoreCase);
        int seen = 0, launchable = 0, filtered = 0, failures = 0;
        string failureStage = "";
        try
        {
            foreach (var package in packages)
            {
                seen++;
                if (seen > 2400) { failures++; failureStage = "limit"; break; }
                try
                {
                    var observation = describe(package);
                    if (observation.Launchable) launchable++;
                    var candidate = observation.App;
                    if (candidate is null) { filtered++; continue; }
                    if (found.TryGetValue(candidate.PackageName, out var previous) &&
                        Matching.CompareVersions(candidate.Version, previous.Version) is <= 0)
                        continue;
                    found[candidate.PackageName] = candidate;
                }
                catch (Exception)
                {
                    failures++; // não invalidar as outras identidades MSIX
                    failureStage = "package";
                }
            }
            return new LocalMsixResult(found.Values.ToArray(), seen, "ok",
                launchable, filtered, failures, FailureStage: failureStage,
                Apartment: Thread.CurrentThread.GetApartmentState().ToString());
        }
        catch (Exception ex)
        {
            return new LocalMsixResult(found.Values.ToArray(), seen, FailureStatus(ex),
                launchable, filtered, failures + 1, FailureStage: "enumeration",
                Apartment: Thread.CurrentThread.GetApartmentState().ToString());
        }
    }

    private static LocalMsixResult Failed(Exception ex, string stage) =>
        new([], 0, FailureStatus(ex), Failures: 1, FailureStage: stage,
            Apartment: Thread.CurrentThread.GetApartmentState().ToString());

    internal static string FailureStatus(Exception ex)
    {
        // A projeção pode envolver a falha de ativação em TypeInitializationException.
        // Desembrulhar somente wrappers conhecidos, sem mensagens, stack ou SID.
        for (int depth = 0; depth < 4 && ex.InnerException is not null &&
            ex is TypeInitializationException or System.Reflection.TargetInvocationException; depth++)
            ex = ex.InnerException;
        return ex is System.Runtime.InteropServices.COMException com
            ? $"COMException(0x{unchecked((uint)com.HResult):X8})"
            : ex.GetType().Name;
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
