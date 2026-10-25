using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

internal sealed record AutomaticAppInstallation(string Key, string Name, string Publisher, string Version, string Scope,
    string Architecture, string Root, string Binary, string Uninstaller, string Kind, string ProductCode, string InventoryId="")
{
#if GF_PROGRESSIVE
    internal static string? SyntheticBaseRoot;
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
            string architecture=VerifiedUpdaters.GetPortableExecutableMachine(verified[0]) switch{0x8664=>"x64",0x14c=>"x86",_=>""};
            if(architecture.Length==0 || !InstallerEngineEvidence.MatchesArchitecture(app.Name,
                architecture=="x64" ? (ushort)0x8664 : (ushort)0x14c))return null;
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
        if(VerifiedUpdaters.GetPortableExecutableMachine(Binary)!=(Architecture=="x64"?0x8664:0x14c) ||
            Kind!="msi" && InstallerEngineEvidence.InstalledKind(new(Uninstaller,"",null))!=Kind)return false;
        return items.Count(p=>Binding(p)==Key)==1 && items.Count(p=>InstallerEngineEvidence.SameProduct(p.Name,Name) &&
            Matching.NormalizePublisher(p.Publisher)==Matching.NormalizePublisher(Publisher))==1 && BinaryMatches(Binary,Version,Name,true);
        }
        catch(Exception ex)when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or System.ComponentModel.Win32Exception){return false;}
    }
    internal bool Confirm(AutomaticInstaller installer,IEnumerable<ProgramEntry> inventory)
    {
        var matches=inventory.Where(p=>InstallerEngineEvidence.SameProduct(p.Name,Name) &&
            p.Publisher.Equals(Publisher,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(matches.Length!=1 || !Eligible(matches[0]) || !VerifiedUpdaters.TryVersion(matches[0].Version,out var current) ||
            !VerifiedUpdaters.TryVersion(installer.Version,out var target) || current<target)return false;
        var after=Read(matches[0]);
        return after is not null && after.Root.Equals(Root,StringComparison.OrdinalIgnoreCase) && after.Binary.Equals(Binary,StringComparison.OrdinalIgnoreCase) &&
            after.Scope==Scope && after.Architecture==Architecture && after.Kind==Kind &&
            (Kind=="msi"? after.ProductCode.Equals(installer.ProductCode,StringComparison.OrdinalIgnoreCase):
                matches[0].InventoryId==InventoryId) && BinaryMatches(Binary,installer.Version,Name,false);
    }
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
