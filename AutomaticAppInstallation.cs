using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

internal sealed record AutomaticAppInstallation(string Key, string Name, string Publisher, string Version, string Scope,
    string Architecture, string Root, string Binary, string Uninstaller, string Kind, string ProductCode, string InventoryId="",
    string ProductName="", string IdentityUpgradeCode="")
{
#if GF_PROGRESSIVE
    internal static string? SyntheticBaseRoot;
    internal static Func<string,string?>? SyntheticSigner=null;
#endif
    internal static bool Eligible(ProgramEntry app) => !app.InventoryOnly && !ProtectedApplications.IsProtectedName(app.Name) &&
        !Regex.IsMatch(app.Name,@"(?i)\b(runtime|redistributable|sdk|driver|framework|server|service|hotfix|windows|update)\b") &&
        app.Publisher.Length>0 && app.InventoryId.Length>0;
    internal static string Binding(ProgramEntry app) => string.Concat(new[]{app.InventoryId,app.Name,app.Publisher,app.Version,
        app.InstallLocation,app.UninstallString,app.MsiProductCode,app.DisplayIcon,app.InventoryOnly.ToString()}.Select(p=>p.Length+":"+p));
    internal static AutomaticAppInstallation? Read(ProgramEntry app) => Read(app, out _);
    internal static AutomaticAppInstallation? Read(ProgramEntry app, out string reason)
    {
        reason = "policy_or_registered_command";
        if (!Eligible(app) || !UninstallCommand.TryParse(app.UninstallString,out var uninstall)) return null;
        reason = "msi_product_mismatch";
        if (uninstall.ProductCode is not null && (!Guid.TryParse(app.MsiProductCode,out var registeredCode) ||
            registeredCode==Guid.Empty || !Guid.TryParse(uninstall.ProductCode,out var commandCode) || registeredCode!=commandCode)) return null;
        try
        {
            string scope=app.InventoryId.StartsWith("LocalMachine|",StringComparison.Ordinal)?"machine":
                app.InventoryId.StartsWith("CurrentUser|",StringComparison.Ordinal)?"user":"";
            reason = "scope_unknown";
            if(scope.Length==0) return null;
            reason = "dedicated_root_unproven";
            string root=app.InstallLocation.Trim().Trim('"').TrimEnd('\\');
            if(root.Length==0 && uninstall.ProductCode is null)root=Path.GetDirectoryName(uninstall.FileName)??"";
            // MSI/WiX may omit InstallLocation. Only the registered executable
            // icon may corroborate a root, never a Windows Installer cache icon.
            // Base, scope, PE, product, version and uniqueness gates below remain.
            if(root.Length==0 && uninstall.ProductCode is not null)
            {
                string candidate=Regex.Replace(app.DisplayIcon.Trim(),@",\s*-?\d+$", "").Trim().Trim('"');
                if(!candidate.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))return null;
                root=Path.GetDirectoryName(InstallerFileGuard.ValidateLocalPath(candidate))??"";
            }
            root=InstallerFileGuard.ValidateLocalPath(root);
            string[] bases=scope=="machine"?[Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)]:
                [Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)];
#if GF_PROGRESSIVE
            if(SyntheticBaseRoot is not null)bases=[SyntheticBaseRoot];
#endif
            if (!bases.Any(b=>b.Length>0 && root.StartsWith(b.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)) ||
                root.Contains("\\Common Files",StringComparison.OrdinalIgnoreCase) || root.Contains("\\WindowsApps",StringComparison.OrdinalIgnoreCase)) return null;
            using var pinned=InstallerFileGuard.PinDirectory(root,allowLeafWrites:true);
            string icon=Regex.Replace(app.DisplayIcon.Trim(),@",\s*-?\d+$","").Trim().Trim('"');
            var paths=new List<string>();
            if(icon.Length>0 && Path.IsPathFullyQualified(icon) && icon.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) &&
                icon.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase) && !icon.Equals(uninstall.FileName,StringComparison.OrdinalIgnoreCase))paths.Add(icon);
            else
            {
                var options=new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=false,AttributesToSkip=FileAttributes.ReparsePoint,MaxRecursionDepth=3};
                var files=Directory.EnumerateFiles(root,"*.exe",options).Take(201).ToArray();
                if(files.Length>200)return null;
                foreach(string file in files)
                {
                    if(file.Equals(uninstall.FileName,StringComparison.OrdinalIgnoreCase))continue;
                    var info=FileVersionInfo.GetVersionInfo(file);
                    if(InstallerEngineEvidence.SameProduct(info.ProductName??"",app.Name))paths.Add(file);
                }
            }
            reason = "binary_identity_or_version";
            var verified=paths.Distinct(StringComparer.OrdinalIgnoreCase).Where(p=>BinaryMatches(p,app.Version,app.Name,exact:true)).ToArray();
            if(verified.Length!=1)return null;
            reason = "architecture_mismatch";
            ushort machine = VerifiedUpdaters.GetPortableExecutableMachine(verified[0]);
            string architecture = ArchitectureFor(machine);
            if (architecture == "arm64" && System.Runtime.InteropServices.RuntimeInformation.OSArchitecture != System.Runtime.InteropServices.Architecture.Arm64)
                return null;
            if(architecture.Length==0 || !InstallerEngineEvidence.MatchesArchitecture(app.Name,
                machine))return null;
            reason = "engine_unrecognized";
            string kind=InstallerEngineEvidence.InstalledKind(uninstall);
            if(kind is not ("msi" or "nullsoft" or "inno"))return null;
            reason = "installation_corroborated";
            return new(Binding(app),app.Name,app.Publisher,app.Version,scope,architecture,root,verified[0],uninstall.FileName,kind,app.MsiProductCode,app.InventoryId);
        }
        catch(Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            BadImageFormatException or System.ComponentModel.Win32Exception or NotSupportedException or System.Security.SecurityException)
        { reason = ex is UnauthorizedAccessException or System.Security.SecurityException or System.ComponentModel.Win32Exception { NativeErrorCode: 5 }
            ? "installation_access_denied" : "installation_unreadable"; return null; }
    }
    // A localized ARP label is never an execution identity. The authenticated
    // catalog name must agree independently with the installed MSI and signed PE.
    internal string BinaryProduct => ProductName.Length > 0 ? ProductName : Name;
    internal bool HasCatalogIdentity => ProductName.Length > 0;
    internal static AutomaticAppInstallation? ReadByCatalogIdentity(ProgramEntry app, string catalogName, out string reason)
    {
        reason="msi_identity_unproven";
        if(!Eligible(app) || !UninstallCommand.TryParse(app.UninstallString,out var command) || command.ProductCode is null ||
            !Guid.TryParse(command.ProductCode,out var commandCode) || !Guid.TryParse(app.MsiProductCode,out var registered) ||
            registered==Guid.Empty || commandCode!=registered || catalogName.Length==0 || !CompatibleVariant(app.Name,catalogName))return null;
        try
        {
            string scope=app.InventoryId.StartsWith("LocalMachine|",StringComparison.Ordinal)?"machine":
                app.InventoryId.StartsWith("CurrentUser|",StringComparison.Ordinal)?"user":"unknown";
            var metadata=MsiPackageEvidence.Installed(app.MsiProductCode,scope);
            if(!Guid.TryParse(metadata.ProductCode,out var installedCode) || installedCode!=registered || metadata.Services ||
                !Guid.TryParse(metadata.UpgradeCode,out var upgrade) || upgrade==Guid.Empty || metadata.Scope!=scope ||
                !InstallerEngineEvidence.SameProduct(metadata.Name,catalogName) ||
                Matching.NormalizePublisher(metadata.Publisher)!=Matching.NormalizePublisher(app.Publisher) ||
                !VerifiedUpdaters.TryVersion(metadata.Version,out var actual) || !VerifiedUpdaters.TryVersion(app.Version,out var expected) || actual!=expected)return null;
            var canonical=new ProgramEntry{Name=metadata.Name,Publisher=app.Publisher,Version=app.Version,InventoryId=app.InventoryId,
                InstallLocation=app.InstallLocation,DisplayIcon=app.DisplayIcon,UninstallString=app.UninstallString,MsiProductCode=app.MsiProductCode};
            var local=Read(canonical,out reason);
            if(local is null || local.Kind!="msi" || local.Architecture!=metadata.Architecture ||
                !InstallerEngineEvidence.MatchesArchitecture(app.Name,VerifiedUpdaters.GetPortableExecutableMachine(local.Binary)) ||
                !PublisherSignature(local.Binary,app.Publisher))return null;
            reason="msi_catalog_cache_signed_binary";
            return local with {Key=Binding(app),Name=app.Name,ProductName=metadata.Name,IdentityUpgradeCode=upgrade.ToString("B")};
        }
        catch(Exception ex) when(AutomaticDiscoveryPipeline.IsItemFailure(ex)) { reason="msi_identity_unreadable";return null; }
    }
    private static bool CompatibleVariant(string registered,string canonical)
    {
        const string variants=@"(?i)\b(preview|beta|alpha|nightly|dev|canary|insider|portable|enterprise|professional|pro|ultimate|business|community|education|lts|esr)\b";
        return Regex.Matches(registered,variants).Select(m=>m.Value.ToLowerInvariant()).Distinct().Order().SequenceEqual(
            Regex.Matches(canonical,variants).Select(m=>m.Value.ToLowerInvariant()).Distinct().Order());
    }
    private static bool PublisherSignature(string binary,string publisher)
    {
#if GF_PROGRESSIVE
        if(SyntheticSigner is not null)return Matching.NormalizePublisher(SyntheticSigner(binary)??"")==Matching.NormalizePublisher(publisher);
#endif
        using var held=InstallerFileGuard.OpenRead(binary,allowDirectoryWrites:true);
        return TrustedFileSignature.TryGetSigner(binary,out var signer,out _) &&
            Matching.NormalizePublisher(signer)==Matching.NormalizePublisher(publisher);
    }
    private bool CatalogIdentityCurrent(ProgramEntry app)
    {
        if(!HasCatalogIdentity)return true;
        var fresh=ReadByCatalogIdentity(app,BinaryProduct,out _);
        return fresh is not null && fresh.Binary==Binary && fresh.Root==Root && fresh.Scope==Scope && fresh.Architecture==Architecture &&
            fresh.IdentityUpgradeCode.Equals(IdentityUpgradeCode,StringComparison.OrdinalIgnoreCase);
    }
    private bool CatalogFamilyUnique(IEnumerable<ProgramEntry> inventory)
    {
        if(!HasCatalogIdentity)return true;
        foreach(var other in inventory.Where(a=>Binding(a)!=Key && a.MsiProductCode.Length>0 &&
            Matching.NormalizePublisher(a.Publisher)==Matching.NormalizePublisher(Publisher)))
        {
            string scope=other.InventoryId.StartsWith("LocalMachine|",StringComparison.Ordinal)?"machine":
                other.InventoryId.StartsWith("CurrentUser|",StringComparison.Ordinal)?"user":"unknown";
            try
            {
                var cached=MsiPackageEvidence.Installed(other.MsiProductCode,scope);
                if(cached.UpgradeCode.Equals(IdentityUpgradeCode,StringComparison.OrdinalIgnoreCase))return false;
            }
            catch(Exception ex)when(AutomaticDiscoveryPipeline.IsItemFailure(ex)){return false;}
        }
        return true;
    }
    internal static string ArchitectureFor(ushort machine) => machine switch { 0x8664 => "x64", 0x14c => "x86", 0xaa64 => "arm64", _ => "" };
    internal static bool BinaryMatches(string file,string version,string name,bool exact)
    {
        try
        {
            using var held=InstallerFileGuard.OpenRead(file,allowDirectoryWrites:true);
            using(var image=new PEReader(held,PEStreamOptions.LeaveOpen))
                if(image.PEHeaders.PEHeader is null || (image.PEHeaders.CoffHeader.Characteristics & Characteristics.ExecutableImage)==0 ||
                    (image.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll)!=0)return false;
            var info=FileVersionInfo.GetVersionInfo(file);
            return InstallerEngineEvidence.SameProduct(info.ProductName??"",name) &&
                VerifiedUpdaters.TryVersion(info.FileVersion,out var actual) && VerifiedUpdaters.TryVersion(version,out var expected) &&
                (exact?actual==expected:actual>=expected);
        }
        catch(Exception ex)when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception or BadImageFormatException){return false;}
    }
    internal bool SameSigner(string file)
    {
        using var installed=InstallerFileGuard.OpenRead(Binary,allowDirectoryWrites:true);
        return TrustedFileSignature.TryGetSigner(Binary,out var previous,out _) &&
            TrustedFileSignature.TryGetSigner(file,out var next,out _) && previous.Equals(next,StringComparison.OrdinalIgnoreCase);
    }
    internal bool Current(IEnumerable<ProgramEntry> inventory)
    {
        try
        {
        var items=inventory.ToArray();
        if(items.Any(p=>Binding(p)!=Key && InstallerEngineEvidence.RegistrationOverlaps(Root,p)))return false;
        if(ArchitectureFor(VerifiedUpdaters.GetPortableExecutableMachine(Binary))!=Architecture ||
            Kind!="msi" && InstallerEngineEvidence.InstalledKind(new(Uninstaller,"",null))!=Kind)return false;
        return items.Count(p=>Binding(p)==Key)==1 && items.Count(p=>(InstallerEngineEvidence.SameProduct(p.Name,Name) || InstallerEngineEvidence.SameProduct(p.Name,BinaryProduct)) &&
            Matching.NormalizePublisher(p.Publisher)==Matching.NormalizePublisher(Publisher))==1 && BinaryMatches(Binary,Version,BinaryProduct,true) &&
            CatalogIdentityCurrent(items.Single(p=>Binding(p)==Key)) && CatalogFamilyUnique(items);
        }
        catch(Exception ex)when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or System.ComponentModel.Win32Exception){return false;}
    }
    internal bool Confirm(AutomaticInstaller installer,IEnumerable<ProgramEntry> inventory)
    {
        var matches=ConfirmationCandidates(inventory);
        if(matches.Length!=1 || !Eligible(matches[0]) || !VerifiedUpdaters.TryVersion(matches[0].Version,out var current) ||
            !VerifiedUpdaters.TryVersion(installer.Version,out var target) || current<target)return false;
        var after=HasCatalogIdentity?ReadByCatalogIdentity(matches[0],BinaryProduct,out _):Read(matches[0]);
        return after is not null && after.Root.Equals(Root,StringComparison.OrdinalIgnoreCase) && after.Binary.Equals(Binary,StringComparison.OrdinalIgnoreCase) &&
            after.Scope==Scope && after.Architecture==Architecture && after.Kind==Kind &&
            (!HasCatalogIdentity || after.IdentityUpgradeCode.Equals(IdentityUpgradeCode,StringComparison.OrdinalIgnoreCase)) &&
            (Kind=="msi"? after.ProductCode.Equals(installer.ProductCode,StringComparison.OrdinalIgnoreCase):
                matches[0].InventoryId==InventoryId) && BinaryMatches(Binary,installer.Version,BinaryProduct,false);
    }
    internal ProgramEntry[] ConfirmationCandidates(IEnumerable<ProgramEntry> inventory) => inventory.Where(p=>
        (InstallerEngineEvidence.SameProduct(p.Name,Name) || HasCatalogIdentity && InstallerEngineEvidence.SameProduct(p.Name,BinaryProduct)) &&
        Matching.NormalizePublisher(p.Publisher)==Matching.NormalizePublisher(Publisher)).ToArray();
    internal void RequireClosed()
    {
        var processes=Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Binary));
        try
        {
            foreach(var process in processes)
                if(!process.HasExited && process.MainModule?.FileName?.Equals(Binary,StringComparison.OrdinalIgnoreCase)==true)
                    throw new InvalidOperationException("Feche o aplicativo antes de atualizar.");
        }
        finally{foreach(var process in processes)process.Dispose();}
    }
}
