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
    internal static AutomaticAppInstallation? Read(ProgramEntry app)
    {
        if (!Eligible(app) || !UninstallCommand.TryParse(app.UninstallString,out var uninstall)) return null;
        try
        {
            string scope=app.InventoryId.StartsWith("LocalMachine|",StringComparison.Ordinal)?"machine":
                app.InventoryId.StartsWith("CurrentUser|",StringComparison.Ordinal)?"user":"";
            if(scope.Length==0) return null;
            string root=app.InstallLocation.Trim().Trim('"').TrimEnd('\\');
            if(root.Length==0 && uninstall.ProductCode is null)root=Path.GetDirectoryName(uninstall.FileName)??"";
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
                    if(Matching.NormalizeName(info.ProductName)==Matching.NormalizeName(app.Name))paths.Add(file);
                }
            }
            var verified=paths.Distinct(StringComparer.OrdinalIgnoreCase).Where(p=>BinaryMatches(p,app.Version,app.Name,exact:true)).ToArray();
            if(verified.Length!=1)return null;
            string architecture=VerifiedUpdaters.GetPortableExecutableMachine(verified[0]) switch{0x8664=>"x64",0x14c=>"x86",_=>""};
            if(architecture.Length==0)return null;
            string kind=uninstall.ProductCode is not null?"msi":InstallerEngineEvidence.Detect(uninstall.FileName);
            if(kind is not ("msi" or "nullsoft" or "inno"))return null;
            return new(Binding(app),app.Name,app.Publisher,app.Version,scope,architecture,root,verified[0],uninstall.FileName,kind,app.MsiProductCode,app.InventoryId);
        }
        catch(Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            BadImageFormatException or System.ComponentModel.Win32Exception or NotSupportedException){return null;}
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
            return Matching.NormalizeName(info.ProductName)==Matching.NormalizeName(name) &&
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
        if(items.Any(p=>Binding(p)!=Key && p.InstallLocation.Trim().TrimEnd('\\') is {Length:>0} other &&
            (Root.StartsWith(other+"\\",StringComparison.OrdinalIgnoreCase) || other.StartsWith(Root+"\\",StringComparison.OrdinalIgnoreCase) || Root.Equals(other,StringComparison.OrdinalIgnoreCase))))return false;
        if(VerifiedUpdaters.GetPortableExecutableMachine(Binary)!=(Architecture=="x64"?0x8664:0x14c) ||
            Kind!="msi" && InstallerEngineEvidence.Detect(Uninstaller)!=Kind)return false;
        return items.Count(p=>Binding(p)==Key)==1 && items.Count(p=>Matching.NormalizeName(p.Name)==Matching.NormalizeName(Name) &&
            Matching.NormalizePublisher(p.Publisher)==Matching.NormalizePublisher(Publisher))==1 && BinaryMatches(Binary,Version,Name,true);
        }
        catch(Exception ex)when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or System.ComponentModel.Win32Exception){return false;}
    }
    internal bool Confirm(AutomaticInstaller installer,IEnumerable<ProgramEntry> inventory)
    {
        var matches=inventory.Where(p=>Matching.NormalizeName(p.Name)==Matching.NormalizeName(Name) &&
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
