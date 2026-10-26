using Microsoft.Management.Deployment;
using System.Text;
using System.Text.Json;
using System.Security.Principal;

namespace GerenciadorAplicativos;

internal static class WinGetComReader
{
    // Official ComInterop example: samples/MinimalCallers/C#/WinGet-OutOfProc.
    // No CLI parsing, Repair-WinGetPackageManager, Install/Upgrade/Uninstall,
    // source registration, agreement acceptance, or lower-trust COM activation.
    internal static int Run()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
                return 3; // never activate a per-user COM registration from an elevated host
            Console.OutputEncoding = new UTF8Encoding(false);
            var manager = new Microsoft.Management.Deployment.PackageManager();
            var local = manager.GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages).Connect();
            if (local.Status != ConnectResultStatus.Ok) return 4;
            var rows = Read(local.PackageCatalog, tracked: false);
            // The installed tracking catalog is local-only and must already
            // exist. It supplies Microsoft PackageIdentifier without contacting
            // a source, refreshing it or accepting its terms.
            try
            {
                var tracking = manager.GetPackageCatalogByName("winget");
                if (tracking is not null)
                {
                    tracking.InstalledPackageInformationOnly = true;
                    tracking.PackageCatalogBackgroundUpdateInterval = TimeSpan.Zero;
                    tracking.AcceptSourceAgreements = false;
                    var connected = tracking.Connect();
                    if (connected.Status == ConnectResultStatus.Ok)
                    {
                        var tracked = Read(connected.PackageCatalog, tracked: true);
                        foreach (var row in tracked)
                        {
                            var matches = rows.Where(r => PackageIdentityMatcher.Match(r.Identity, row.Identity).Confidence == IdentityConfidence.Strong).ToArray();
                            if (matches.Length == 1) { rows.Remove(matches[0]); rows.Add(row); }
                        }
                    }
                }
            }
            catch (Exception) { /* local ARP/MSIX reading remains useful */ }
            Console.Write(JsonSerializer.Serialize(rows));
            return 0;
        }
        catch (Exception ex)
        {
            // Fixed protocol codes only: no exception text, paths or account data.
            return ex.HResult switch { unchecked((int)0x80040154)=>6,unchecked((int)0x80070005)=>7,_=>5 };
        }
    }
    private static List<ProviderInstallation> Read(PackageCatalog catalog, bool tracked)
    {
        var result = catalog.FindPackages(new FindPackagesOptions { ResultLimit = 2001 });
        if (result.Status != FindPackagesResultStatus.Ok || result.Matches.Count > 2000 || result.WasLimitExceeded)
            throw new InvalidDataException("Incomplete installed catalog");
        var rows = new List<ProviderInstallation>();
        for (int i = 0; i < result.Matches.Count; i++)
        {
            var package = result.Matches[i].CatalogPackage;
            var version = package.InstalledVersion;
            if (version is null || version.ProductCodes.Count > 1 || version.PackageFamilyNames.Count > 1) continue;
            string scope = version.GetMetadata(PackageVersionMetadataField.InstalledScope).ToLowerInvariant();
            if (scope == "system") scope = "machine";
            if (scope is not ("user" or "machine")) scope = "unknown";
            string code = version.ProductCodes.Count == 1 ? version.ProductCodes[0] : "";
            if (!Guid.TryParse(code, out _)) code = "";
            string id = tracked && PackageIdentityMatcher.ValidId(package.Id) ? package.Id : "";
            rows.Add(new(new("winget", id, package.Name,
                version.GetMetadata(PackageVersionMetadataField.PublisherDisplayName), version.Version,
                Scope: scope, Channel: version.Channel.Length > 0 ? version.Channel : "unknown",
                Location: version.GetMetadata(PackageVersionMetadataField.InstalledLocation), ProductCode: code,
                PackageFamilyName: version.PackageFamilyNames.Count == 1 ? version.PackageFamilyNames[0] : "",
                Managed: id.Length > 0), "local-com", Reason: "winget_installed_catalog"));
        }
        return rows;
    }
}

internal sealed partial class MainForm
{
    private readonly ApplicationProviderCoordinator ecosystemProviders = new([new WinGetComDiscoveryProvider(new WinGetComProcessCatalog())]);
}
