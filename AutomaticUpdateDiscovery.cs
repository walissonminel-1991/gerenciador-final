using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
namespace GerenciadorAplicativos;

internal sealed record AutomaticDiscoveryResult(IReadOnlyList<OfficialRelease> Findings, IReadOnlyList<(ProgramEntry App, AutomaticAppInstallation Installation, AutomaticInstaller Installer)> Plans,
    ProviderDiagnostic Diagnostic);

// Autoridade efemera proveniente de catalogo Microsoft assinado + manifesto SHA.
// Catalogos consultivos, IDs, comandos e nomes isolados nao criam essa autoridade.
internal static class AutomaticUpdateDiscovery
{
    private sealed record Permit(WeakReference<ProgramEntry> App,AutomaticAppInstallation Installation,AutomaticInstaller Installer,VerifiedUpdateRecipe Recipe,DateTimeOffset Expires);
    private sealed record Issued((ProgramEntry App,AutomaticAppInstallation Installation,AutomaticInstaller Installer)[] Plans,DateTimeOffset Expires);
    private static readonly ConditionalWeakTable<AutomaticDiscoveryResult,Issued> IssuedScans=new();
    private static readonly ConcurrentDictionary<string,byte> Executed=new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConditionalWeakTable<ProgramEntry,Permit> Attached=new();
    private static readonly ConcurrentDictionary<string,Permit> Permits=new();
    private static readonly SemaphoreSlim SourceGate=new(1,1);
    private static PackageCatalogIndex? catalog;
    private static DateTimeOffset catalogAt;
    static AutomaticUpdateDiscovery(){AppDomain.CurrentDomain.ProcessExit+=(_,_)=>{if(SourceGate.Wait(0)){try{catalog?.Dispose();}finally{SourceGate.Release();}}};}

    internal static bool IsAutomatic(VerifiedUpdateRecipe recipe)=>recipe.Identifier.StartsWith("auto-",StringComparison.Ordinal);
    internal static bool IsApproved(VerifiedUpdateRecipe recipe)=>Permits.TryGetValue(recipe.Identifier,out var permit) &&
        ReferenceEquals(recipe,permit.Recipe) && permit.Expires>DateTimeOffset.UtcNow;
    private static Permit Require(VerifiedUpdateRecipe recipe)=>IsApproved(recipe)?Permits[recipe.Identifier]:throw new InvalidDataException("Autorizacao automatica ausente ou expirada.");
    internal static VerifiedUpdateRecipe? Resolve(ProgramEntry app)=>Attached.TryGetValue(app,out var permit) &&
        app.IsUpdate && !app.UpdateRequiresReview && AutomaticAppInstallation.Binding(app)==permit.Installation.Key &&
        app.AvailableVersion==permit.Installer.Version && !Executed.ContainsKey(ExecutionKey(permit)) && IsApproved(permit.Recipe)?permit.Recipe:null;
    internal static void Attach(AutomaticDiscoveryResult scan)
    {
        if(!IssuedScans.TryGetValue(scan,out var issued) || issued.Expires<=DateTimeOffset.UtcNow)return;
        IssuedScans.Remove(scan);
        foreach(var item in issued.Plans)
        {
            if(!item.App.IsUpdate || item.App.UpdateRequiresReview || item.App.AvailableVersion!=item.Installer.Version ||
                AutomaticAppInstallation.Binding(item.App)!=item.Installation.Key)continue;
            foreach(var expired in Permits.Where(p=>p.Value.Expires<=DateTimeOffset.UtcNow).Select(p=>p.Key))Permits.TryRemove(expired,out _);
            if(Permits.Count>=512)break;
            var legacyApp=new ProgramEntry{Name=item.App.Name,Publisher=item.App.Publisher,Version=item.App.Version,AvailableVersion=item.App.AvailableVersion,
                IsUpdate=item.App.IsUpdate,Source="GitHub oficial",InventoryId=item.App.InventoryId,InstallLocation=item.App.InstallLocation,
                UninstallString=item.App.UninstallString,MsiProductCode=item.App.MsiProductCode};
            var reviewed=VerifiedUpdaters.ResolveReviewed(legacyApp);
            if(reviewed is not null && (!string.Equals(reviewed.Sha256,item.Installer.Sha256,StringComparison.OrdinalIgnoreCase) ||
                !VerifiedUpdaters.SingleSafeSilentInstallation(reviewed,legacyApp,[legacyApp])))reviewed=null;
            var recipe=(!item.Installer.Interactive ? reviewed : null) ?? new VerifiedUpdateRecipe("auto-"+Guid.NewGuid().ToString("N"),item.Installer.Name,item.Installer.Version,
                item.Installer.Download,item.Installer.Sha256,item.Installer.Publisher,item.Installer.Arguments,item.Installer.Interactive,"https://github.com/microsoft/winget-pkgs");
            var permit=new Permit(new(item.App),item.Installation,item.Installer,recipe,DateTimeOffset.UtcNow.AddMinutes(30));
            Attached.Remove(item.App);Attached.Add(item.App,permit);Permits[recipe.Identifier]=permit;
        }
    }
    internal static async Task<AutomaticDiscoveryResult> QueryAsync(ProgramEntry[] apps,InstalledApp[] identities,CancellationToken token)
    {
        if(!OperatingSystem.IsWindows())return new([],[],new("automatic-manifest","indisponivel",0));
        if(!await SourceGate.WaitAsync(0,token))return new([],[],new("automatic-manifest","ocupado",0));
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(token);stop.CancelAfter(TimeSpan.FromSeconds(45));
        var workToken=stop.Token;
        var clock=Stopwatch.StartNew();
        var worker=Task.Run(async()=>
        {
            try
            {
                if(catalog is null || DateTimeOffset.UtcNow-catalogAt>TimeSpan.FromMinutes(30))
                {catalog?.Dispose();catalog=null;catalog=await PackageCatalogIndex.DownloadAsync(workToken);catalogAt=DateTimeOffset.UtcNow;}
                var findings=new List<OfficialRelease>();var plans=new List<(ProgramEntry,AutomaticAppInstallation,AutomaticInstaller)>();int inspected=0;
                for(int i=0;i<apps.Length;i++)
                {
                    workToken.ThrowIfCancellationRequested();var app=apps[i];
                    bool executionContext = AutomaticAppInstallation.Eligible(app);
                    if(!identities[i].EligibleForUpdates || ProtectedApplications.IsProtectedName(app.Name) ||
                        (!executionContext && app.MsixPackageFamilyName.Length == 0 && EcosystemInventory.Get(app) is null))continue;
                    if(++inspected>512)throw new InvalidDataException("Limite de descoberta automatica atingido.");
                    var rows=catalog.Find(app.Name,app.Publisher,app.MsiProductCode,workToken);
                    bool strictIdentity = rows.Count>0;
                    // Só para consulta: ProductCode MSI exato, fornecedor idêntico e
                    // identificador único no catálogo assinado podem localizar versão
                    // mesmo com DisplayName diferente. Nunca promove a execução.
                    if(!strictIdentity)
                        rows=catalog.FindForReviewByProductCode(app.Publisher,app.MsiProductCode,workToken);
                    if(rows.Count == 0 && app.MsixPackageFamilyName.Length > 0)
                        rows=catalog.FindForReviewByPackageFamily(app.Publisher,app.MsixPackageFamilyName,workToken);
                    if(rows.Count == 0 && EcosystemInventory.Get(app) is { Identity.Provider: "winget", Identity.Managed: true } origin)
                        rows=catalog.FindForReviewByPackageId(app.Publisher,origin.Identity.PackageId,workToken);
                    var latest=NewestCandidate(rows,app.Version);
                    if(latest is null)continue;
                    void Advisory()
                    {
                        findings.Add(new(identities[i],latest.Version,"Catálogo Microsoft (consulta)",
                            "https://github.com/microsoft/winget-pkgs",
                            "catalog-review","",DateTimeOffset.UtcNow,
                            Channel:"stable",EvidenceKind:"catalog-version-only"));
                    }
                    if(!strictIdentity || !executionContext) { Advisory(); continue; }
                    var installation=AutomaticAppInstallation.Read(app);
                    if(installation is null || !installation.Current(apps)) { Advisory(); continue; }
                    var bytes=await PackageCatalogIndex.ReadManifestAsync(latest,workToken);
                    var installer=AutomaticPackageManifest.Select(bytes,latest,installation.Architecture,installation.Scope,installation.Kind);
                    if(installer is null || (installation.Kind=="msi"?installer.Type is not ("msi" or "wix"):installer.Type!=installation.Kind))
                    { Advisory(); continue; }
                    if(installer.ProductCode.Length>0 && installation.Kind!="msi" &&
                        !installer.ProductCode.Equals(app.InventoryId.Split('|').Last(),StringComparison.OrdinalIgnoreCase))
                    { Advisory(); continue; }
                    if(!InstallerEngineEvidence.SameProduct(installer.Name,app.Name))
                    { Advisory(); continue; }
                    findings.Add(new(identities[i],installer.Version,"Catalogo Microsoft HTTPS","https://github.com/microsoft/winget-pkgs",
                        "automatic-manifest","",DateTimeOffset.UtcNow,installation.Architecture,"stable",installation.Scope,
                        installer.Type,EvidenceKind:"authenticated-installer-manifest"));
                    plans.Add((app,installation,installer));
                }
                var result=new AutomaticDiscoveryResult(findings.AsReadOnly(),plans.AsReadOnly(),new("automatic-manifest","ok",clock.ElapsedMilliseconds));
                IssuedScans.Add(result,new(plans.ToArray(),DateTimeOffset.UtcNow.AddMinutes(2)));
                return result;
            }
            finally{SourceGate.Release();}
        },CancellationToken.None);
        _=worker.ContinueWith(t=>{_=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
        try{return await worker.WaitAsync(TimeSpan.FromSeconds(46),token);}
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch(Exception ex)when(ex is IOException or InvalidDataException or HttpRequestException or OperationCanceledException or TimeoutException or
            System.Runtime.InteropServices.ExternalException or YamlDotNet.Core.YamlException or DllNotFoundException or EntryPointNotFoundException or ArgumentException or BadImageFormatException)
        {return new([],[],new("automatic-manifest",ex is TimeoutException or OperationCanceledException?"prazo":"indisponivel",clock.ElapsedMilliseconds));}
    }
    // Escolha isolada e determinística: versões desconhecidas, prévias e
    // revisões iguais não viram atualizações nem iniciam downloads.
    internal static CatalogManifestReference? NewestCandidate(
        IReadOnlyList<CatalogManifestReference> references, string installedVersion)
    {
        if(!VerifiedUpdaters.TryVersion(installedVersion,out var installed))return null;
        var newest=references.Where(r=>VerifiedUpdaters.TryVersion(r.Version,out _))
            .OrderByDescending(r=>{VerifiedUpdaters.TryVersion(r.Version,out var v);return v;})
            .FirstOrDefault();
        return newest is not null && VerifiedUpdaters.TryVersion(newest.Version,out var next) && next>installed
            ? newest : null;
    }
    internal static bool Safe(VerifiedUpdateRecipe recipe,ProgramEntry app,IEnumerable<ProgramEntry> inventory)
    {var p=Require(recipe);return AutomaticAppInstallation.Binding(app)==p.Installation.Key && p.Installation.Current(inventory);}
    internal static bool Confirm(VerifiedUpdateRecipe recipe,IEnumerable<ProgramEntry> inventory)=>Require(recipe).Installation.Confirm(Require(recipe).Installer,inventory);
    internal static string Binary(VerifiedUpdateRecipe recipe)=>Require(recipe).Installation.Binary;
    internal static bool Machine(VerifiedUpdateRecipe recipe)=>Require(recipe).Installation.Scope=="machine";
    internal static bool Msi(VerifiedUpdateRecipe recipe)=>Require(recipe).Installation.Kind=="msi";
    internal static string Extension(VerifiedUpdateRecipe recipe)=>Msi(recipe)?".msi":".exe";
    internal static void RequireClosed(VerifiedUpdateRecipe recipe)=>Require(recipe).Installation.RequireClosed();
    internal static void VerifyFile(VerifiedUpdateRecipe recipe,string file)
    {
        var p=Require(recipe);
        if(!p.Installation.SameSigner(file))throw new UpdateSignatureException("Assinaturas da instalacao e do pacote nao correspondem.");
        if(Msi(recipe)){MsiPackageEvidence.RequireUpgrade(p.Installer,p.Installation,file);return;}
        string kind=InstallerEngineEvidence.Detect(file);
        if(kind!=p.Installer.Type)throw new InvalidDataException("Motor real diverge do manifesto autenticado.");
        var meta=FileVersionInfo.GetVersionInfo(file);
        if(!string.IsNullOrWhiteSpace(meta.ProductName) && !InstallerEngineEvidence.SameProduct(meta.ProductName,p.Installer.Name))
            throw new InvalidDataException("Produto do executavel diverge do manifesto.");
        if(!string.IsNullOrWhiteSpace(meta.FileVersion) && VerifiedUpdaters.TryVersion(meta.FileVersion,out var actual) &&
            VerifiedUpdaters.TryVersion(recipe.Version,out var target) &&actual!=target)throw new InvalidDataException("Versao do instalador diverge do alvo.");
    }
#if GF_PROGRESSIVE
    internal static AutomaticDiscoveryResult SimulateIssued(ProgramEntry app,AutomaticAppInstallation installation,AutomaticInstaller installer)
    {
        var plans=new[]{(app,installation,installer)};
        var result=new AutomaticDiscoveryResult([],plans,new("simulation","ok",0));
        IssuedScans.Add(result,new(plans,DateTimeOffset.UtcNow.AddMinutes(2)));return result;
    }
#endif
    private static string ExecutionKey(Permit permit)=>permit.Installation.Binary+"|"+permit.Installation.Version;
    internal static void Reserve(VerifiedUpdateRecipe recipe)
    {if(!Executed.TryAdd(ExecutionKey(Require(recipe)),0))throw new InvalidOperationException("Atualizacao desta instalacao ja enviada; confira a versao antes de repetir.");}
    internal static void ReleaseBeforeStart(VerifiedUpdateRecipe recipe)=>Executed.TryRemove(ExecutionKey(Require(recipe)),out _);
    internal static ProgramEntry? ConfirmedEntry(VerifiedUpdateRecipe recipe,IEnumerable<ProgramEntry> inventory)
    {var list=inventory.ToArray();var p=Require(recipe);return p.Installation.Confirm(p.Installer,list)?list.Single(a=>InstallerEngineEvidence.SameProduct(a.Name,p.Installation.Name) && a.Publisher.Equals(p.Installation.Publisher,StringComparison.OrdinalIgnoreCase)):null;}
    internal static bool PublicAddress(System.Net.IPAddress address)
    {
        if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
        byte[] bytes=address.GetAddressBytes();
        if(System.Net.IPAddress.IsLoopback(address))return false;
        if(bytes.Length==4)return bytes[0] is not (0 or 10 or 127) && bytes[0]<224 &&
            !(bytes[0]==169 && bytes[1]==254) &&
            !(bytes[0]==172 && bytes[1]>=16 && bytes[1]<=31) && !(bytes[0]==192 && bytes[1]==168) &&
            !(bytes[0]==100 && bytes[1]>=64 && bytes[1]<=127) && !(bytes[0]==198 && bytes[1] is 18 or 19);
        return !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal && !address.IsIPv6Multicast && (bytes[0]&0xfe)!=0xfc &&
            !address.Equals(System.Net.IPAddress.IPv6Any);
    }
    internal static HttpMessageHandler DownloadHandler()=>new SocketsHttpHandler
    {
        AllowAutoRedirect=false,UseCookies=false,UseProxy=false,
        ConnectCallback=async(context,token)=>
        {
            var addresses=await System.Net.Dns.GetHostAddressesAsync(context.DnsEndPoint.Host,token);
            if(addresses.Length==0 || addresses.Any(a=>!PublicAddress(a)))throw new HttpRequestException("Destino de download nao e publico.");
            foreach(var address in addresses)
            {
                var socket=new System.Net.Sockets.Socket(address.AddressFamily,System.Net.Sockets.SocketType.Stream,System.Net.Sockets.ProtocolType.Tcp);
                try{await socket.ConnectAsync(new System.Net.IPEndPoint(address,context.DnsEndPoint.Port),token);return new System.Net.Sockets.NetworkStream(socket,true);}
                catch(System.Net.Sockets.SocketException){socket.Dispose();}
                catch{socket.Dispose();throw;}
            }
            throw new HttpRequestException("Destino de download indisponivel.");
        }
    };
    internal static bool AllowedUri(Uri uri)=>AutomaticPackageManifest.IsPublicHttps(uri);
}
