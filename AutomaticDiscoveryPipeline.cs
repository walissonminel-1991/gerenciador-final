using System.Diagnostics;

namespace GerenciadorAplicativos;

// These contracts can only read data. A result produced here has no authority:
// only AutomaticUpdateDiscovery can seal a production scan from the signed index.
internal interface IAutomaticCatalogSource
{
    IReadOnlyList<CatalogManifestReference> Find(ProgramEntry app, CancellationToken token);
    IReadOnlyList<CatalogManifestReference> ByProductCode(ProgramEntry app, CancellationToken token);
    IReadOnlyList<CatalogManifestReference> ByPackageFamily(ProgramEntry app, CancellationToken token);
    IReadOnlyList<CatalogManifestReference> ByPackageId(ProgramEntry app, string id, CancellationToken token);
    Task<byte[]> ManifestAsync(CatalogManifestReference reference, CancellationToken token);
}
internal sealed class SignedAutomaticCatalog(PackageCatalogIndex index) : IAutomaticCatalogSource
{
    public IReadOnlyList<CatalogManifestReference> Find(ProgramEntry app,CancellationToken token)=>index.Find(app.Name,app.Publisher,app.MsiProductCode,token);
    public IReadOnlyList<CatalogManifestReference> ByProductCode(ProgramEntry app,CancellationToken token)=>index.FindForReviewByProductCode(app.Publisher,app.MsiProductCode,token);
    public IReadOnlyList<CatalogManifestReference> ByPackageFamily(ProgramEntry app,CancellationToken token)=>index.FindForReviewByPackageFamily(app.Publisher,app.MsixPackageFamilyName,token);
    public IReadOnlyList<CatalogManifestReference> ByPackageId(ProgramEntry app,string id,CancellationToken token)=>index.FindForReviewByPackageId(app.Publisher,id,token);
    public Task<byte[]> ManifestAsync(CatalogManifestReference reference,CancellationToken token)=>PackageCatalogIndex.ReadManifestAsync(reference,token);
}
internal static class AutomaticDiscoveryPipeline
{
    internal static bool IsItemFailure(Exception ex)=>ex is IOException or InvalidDataException or UnauthorizedAccessException or
        HttpRequestException or System.Runtime.InteropServices.ExternalException or YamlDotNet.Core.YamlException or
        DllNotFoundException or EntryPointNotFoundException or ArgumentException or BadImageFormatException or
        NotSupportedException or System.Security.SecurityException;
    internal static async Task<AutomaticDiscoveryResult> RunAsync(ProgramEntry[] apps,InstalledApp[] identities,
        IAutomaticCatalogSource source,CancellationToken userToken,CancellationToken budgetToken)
    {
        if(apps.Length!=identities.Length || apps.Where((a,i)=>a.Name!=identities[i].Name ||
            a.Publisher!=identities[i].Publisher || a.Version!=identities[i].Version).Any())
            throw new ArgumentException("Identidades nao pertencem ao inventario consultado.");
        var clock=Stopwatch.StartNew();
        var findings=new List<OfficialRelease>();
        var plans=new List<(ProgramEntry,AutomaticAppInstallation,AutomaticInstaller)>();
        var counts=new Dictionary<string,int>(StringComparer.Ordinal);
        void Count(string code)=>counts[code]=counts.GetValueOrDefault(code)+1;
        int inspected=0;bool partial=false;
        for(int i=0;i<apps.Length;i++)
        {
            userToken.ThrowIfCancellationRequested();
            if(budgetToken.IsCancellationRequested){Count("budget_exhausted");partial=true;break;}
            var app=apps[i];CatalogManifestReference? latest=null;int advisory=-1;
            void Advisory()
            {
                if(latest is null || advisory>=0)return;
                advisory=findings.Count;
                findings.Add(new(identities[i],latest.Version,"Catálogo Microsoft (consulta)",
                    "https://github.com/microsoft/winget-pkgs","catalog-review","",DateTimeOffset.UtcNow,
                    Channel:"stable",EvidenceKind:"catalog-version-only"));
            }
            try
            {
                bool executionContext=AutomaticAppInstallation.Eligible(app);
                if(!identities[i].EligibleForUpdates || ProtectedApplications.IsProtectedName(app.Name) ||
                    (!executionContext && app.MsixPackageFamilyName.Length==0 && EcosystemInventory.Get(app) is null))
                {Count("policy_or_inventory_only");continue;}
                if(inspected==512){Count("inventory_limit");partial=true;break;}
                inspected++;
                var rows=source.Find(app,budgetToken);bool strict=rows.Count>0;bool stableMsi=false;
                if(!strict){rows=source.ByProductCode(app,budgetToken);stableMsi=rows.Count>0;}
                if(rows.Count==0 && app.MsixPackageFamilyName.Length>0)rows=source.ByPackageFamily(app,budgetToken);
                if(rows.Count==0 && EcosystemInventory.Get(app) is {Identity.Provider:"winget",Identity.Managed:true} origin)
                {rows=source.ByPackageId(app,origin.Identity.PackageId,budgetToken);stableMsi=rows.Count>0 && app.MsiProductCode.Length>0;}
                latest=AutomaticUpdateDiscovery.NewestCandidate(rows,app.Version);
                if(latest is null){Count(rows.Count==0?"identity_unmatched":"no_newer_version");continue;}
                Advisory();
                if(!executionContext || EcosystemInventory.Ambiguous(app))
                {Count(EcosystemInventory.Ambiguous(app)?"identity_ambiguous":"consultative_only");continue;}
                AutomaticAppInstallation? installation;
                string reason;
                if(strict)installation=AutomaticAppInstallation.Read(app,out reason);
                else if(stableMsi)installation=AutomaticAppInstallation.ReadByCatalogIdentity(app,latest.Name,out reason);
                else{Count("consultative_only");continue;}
                if(installation is null){Count(reason);continue;}
                if(!installation.Current(apps)){Count("installation_changed_or_shared");continue;}
                var bytes=await source.ManifestAsync(latest,budgetToken);
                var installer=AutomaticPackageManifest.Select(bytes,latest,installation.Architecture,installation.Scope,installation.Kind);
                if(installer is null || (installation.Kind=="msi"?installer.Type is not ("msi" or "wix"):installer.Type!=installation.Kind) ||
                    !InstallerEngineEvidence.SameProduct(installer.Name,installation.BinaryProduct) ||
                    Matching.NormalizePublisher(installer.Publisher)!=Matching.NormalizePublisher(installation.Publisher) ||
                    installer.ProductCode.Length>0 && installation.Kind!="msi" &&
                        !installer.ProductCode.Equals(app.InventoryId.Split('|').Last(),StringComparison.OrdinalIgnoreCase))
                {Count("manifest_not_eligible");continue;}
                budgetToken.ThrowIfCancellationRequested();userToken.ThrowIfCancellationRequested();
                findings[advisory]=new(identities[i],installer.Version,"Catalogo Microsoft HTTPS","https://github.com/microsoft/winget-pkgs",
                    "automatic-manifest","",DateTimeOffset.UtcNow,installation.Architecture,"stable",installation.Scope,
                    installer.Type,EvidenceKind:"authenticated-installer-manifest");
                plans.Add((app,installation,installer));
                Count(installation.HasCatalogIdentity?"msi_catalog_identity_plan":installer.Interactive?"supervised_plan":"silent_plan");
            }
            catch(OperationCanceledException)when(userToken.IsCancellationRequested){throw;}
            catch(OperationCanceledException)when(budgetToken.IsCancellationRequested)
            {Advisory();Count("budget_exhausted");partial=true;break;}
            catch(OperationCanceledException)
            {Advisory();Count("item_unavailable");partial=true;}
            catch(Exception ex)when(IsItemFailure(ex) || ex is TimeoutException)
            {Advisory();Count("item_unavailable");partial=true;}
        }
        userToken.ThrowIfCancellationRequested();
        return new(findings.AsReadOnly(),plans.AsReadOnly(),new("automatic-manifest",partial?"parcial":"ok",clock.ElapsedMilliseconds),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string,int>(counts));
    }
}
