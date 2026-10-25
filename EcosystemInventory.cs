using System.Runtime.CompilerServices;

namespace GerenciadorAplicativos;

internal static class EcosystemInventory
{
    private sealed record Binding(string Key, ProviderInstallation Installation, IdentityMatch Match);
    private static readonly ConditionalWeakTable<ProgramEntry, Binding> Bindings = new();
    internal static PackageIdentity LocalIdentity(ProgramEntry app) => new("windows", "",
        InstallerEngineEvidence.FamilyName(app.Name), app.Publisher, app.Version,
        Scope: app.InventoryId.StartsWith("LocalMachine|", StringComparison.Ordinal) ? "machine" :
            app.InventoryId.StartsWith("CurrentUser|", StringComparison.Ordinal) || app.InventoryId.StartsWith("MSIX|", StringComparison.Ordinal) ? "user" : "unknown",
        Location: app.InstallLocation, ProductCode: app.MsiProductCode,
        PackageFamilyName: app.MsixPackageFamilyName);
    private static string Key(ProgramEntry app) => string.Join('\0', app.InventoryId, app.Name, app.Publisher,
        app.Version, app.InstallLocation, app.MsiProductCode, app.MsixPackageFamilyName);
    internal static ProviderInstallation? Get(ProgramEntry app) => Bindings.TryGetValue(app, out var value) && value.Key == Key(app) ? value.Installation : null;
    internal static bool Managed(ProgramEntry app) => Get(app)?.Identity.Provider is "scoop" or "scoop-global" or "chocolatey";
    internal static bool CanQueryVersions(ProgramEntry app) => !app.InventoryOnly || app.VerifiedMsixIdentity ||
        app.MsixPackageFamilyName.Length > 0 && app.InventoryId.StartsWith("MSIX|", StringComparison.Ordinal) || Managed(app);
    internal static bool Identified(ProgramEntry app) => Bindings.TryGetValue(app, out var value) && value.Key == Key(app) && value.Match.Discovered;
    internal static string Describe(ProgramEntry app) => Get(app) is { } row ? row.Identity.Provider switch
    {
        "chocolatey" => "Pacote registrado no Chocolatey; presença do programa precisa de confirmação. Gerencie no Chocolatey. Scripts não executados pelo GF.",
        "scoop" or "scoop-global" => "Instalação Scoop identificada. Gerencie no Scoop; versão de bucket local não autoriza atualização pelo GF.",
        _ => "Identidade corroborada pelo catálogo local WinGet; operação exige validação independente."
    } : "";

    internal static void Associate(ProgramEntry[] apps, IReadOnlyList<ApplicationProviderResult> providers)
    {
        var candidates = providers.Where(p => p.Provider == "winget" && p.Status == "ok")
            .SelectMany(p => p.Installations).ToArray();
        foreach (var app in apps)
        {
            if (Managed(app)) continue;
            var identity = LocalIdentity(app);
            var match = PackageIdentityMatcher.Resolve(identity, candidates.Select(c => c.Identity));
            if (match.Identity is null || match.Match.Confidence != IdentityConfidence.Strong) continue;
            // One API identity may never silently bind multiple ARP registrations.
            if (apps.Count(a => PackageIdentityMatcher.Match(LocalIdentity(a), match.Identity).Confidence == IdentityConfidence.Strong) != 1) continue;
            var row = candidates.Single(c => ReferenceEquals(c.Identity, match.Identity));
            Bindings.Remove(app); Bindings.Add(app, new(Key(app), row, match.Match));
        }
    }
    internal static int AppendManaged(List<ProgramEntry> inventory, IEnumerable<ApplicationProviderResult> providers)
    {
        int added = 0;
        foreach (var row in providers.Where(p => p.Provider is "scoop" or "scoop-global" or "chocolatey")
            .SelectMany(p => p.Installations))
        {
            var identity = row.Identity;
            string id = "MANAGED|" + identity.Provider + "|" + identity.PackageId + "|" + identity.Scope;
            if (inventory.Any(a => a.InventoryId.Equals(id, StringComparison.OrdinalIgnoreCase))) continue;
            var app = new ProgramEntry { Name = identity.Name, Version = identity.InstalledVersion, Publisher = identity.Publisher,
                InventoryId = id, InventorySource = identity.Provider == "chocolatey" ? "Pacote Chocolatey (não confirma o programa)" : "Instalação Scoop",
                InventoryOnly = true, InstallLocation = identity.Location };
            Bindings.Add(app, new(Key(app), row, new(IdentityConfidence.Strong, "local_manager_receipt", ["manager_metadata", "version_directory"])));
            inventory.Add(app); added++;
        }
        return added;
    }
    internal static IApplicationDiscoveryProvider[] LocalProviders()
    {
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string data = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return [new ScoopDiscoveryProvider(Environment.GetEnvironmentVariable("SCOOP") ?? Path.Combine(user, "scoop")),
            new ScoopDiscoveryProvider(Environment.GetEnvironmentVariable("SCOOP_GLOBAL") ?? Path.Combine(data, "scoop"), "machine"),
            new ChocolateyDiscoveryProvider(Environment.GetEnvironmentVariable("ChocolateyInstall") ?? Path.Combine(data, "chocolatey"))];
    }
    internal static IReadOnlyList<ApplicationProviderResult> ReadLocal()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var coordinator = new ApplicationProviderCoordinator(LocalProviders());
        try { return coordinator.DiscoverAsync(new(true), stop.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { return []; }
    }
    internal static IEnumerable<OfficialRelease> Findings(ProgramEntry[] apps, InstalledApp[] installed)
    {
        for (int i = 0; i < apps.Length; i++)
            if (Get(apps[i]) is { AvailableVersion.Length: > 0 } row && Managed(apps[i]) &&
                Matching.CompareVersions(apps[i].Version, row.AvailableVersion) is < 0)
                yield return new(installed[i], row.AvailableVersion, "Scoop (bucket local)",
                    "https://scoop.sh/", "scoop-local", ObservedAt: DateTimeOffset.UtcNow,
                    EvidenceKind: "managed-version-only");
    }
}
