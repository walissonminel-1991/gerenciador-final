using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace GerenciadorAplicativos;

internal sealed record AutomaticInstaller(string PackageId, string Name, string Publisher, string Version, string Type,
    string Architecture, string Scope, Uri Download, string Sha256, string ProductCode, string UpgradeCode, string Arguments);

// Interpreta dados autenticados; nunca desserializa objetos, scripts ou comandos externos.
internal static class AutomaticPackageManifest
{
    internal static AutomaticInstaller? Select(byte[] bytes, CatalogManifestReference reference, string architecture, string scope, string? installedKind = null)
    {
        if (bytes.Length is < 20 or > 2 * 1024 * 1024) throw new InvalidDataException("Manifesto fora do limite.");
        string text = new UTF8Encoding(false, true).GetString(bytes);
        var parser = new Parser(new StringReader(text)); int events = 0, depth = 0;
        while (parser.MoveNext())
        {
            if (++events > 100000 || parser.Current is AnchorAlias || parser.Current is NodeEvent node && !node.Anchor.IsEmpty)
                throw new InvalidDataException("Aliases/anchors YAML nao sao autorizados.");
            if (parser.Current is MappingStart or SequenceStart) { if (++depth > 24) throw new InvalidDataException("YAML excede profundidade."); }
            if (parser.Current is MappingEnd or SequenceEnd) depth--;
        }
        var stream = new YamlStream(); stream.Load(new StringReader(text));
        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root) throw new InvalidDataException("Manifesto sem mapa unico.");
        if (S(root, "PackageIdentifier") != reference.Id || S(root, "PackageVersion") != reference.Version ||
            S(root, "ManifestType") is not ("merged" or "singleton") || !S(root,"ManifestVersion").StartsWith("1.", StringComparison.Ordinal))
            throw new InvalidDataException("Identidade do manifesto diverge do indice.");
        string name = S(root,"PackageName"), publisher = S(root,"Publisher");
        if (name.Length == 0 || publisher.Length == 0 || !VerifiedUpdaters.TryVersion(reference.Version,out _)) return null;
        if (N(root,"Installers") is not YamlSequenceNode installers || installers.Children.Count > 150) return null;
        var options = new List<AutomaticInstaller>();
        foreach (var item in installers.Children.OfType<YamlMappingNode>())
        {
            string Get(string key) => S(item,key) is { Length: > 0 } own ? own : S(root,key);
            YamlNode? GetNode(string key) => N(item,key) ?? N(root,key);
            string type = Get("InstallerType"), arch = Get("Architecture"), targetScope = Get("Scope");
            if (installedKind is not null && (installedKind=="msi" ? type is not ("msi" or "wix") : type!=installedKind))continue;
            if (arch != architecture || targetScope != scope || type is not ("nullsoft" or "inno" or "msi" or "wix")) continue;
            if (Get("UpgradeBehavior") is not ("" or "install") || GetNode("Dependencies") is not null ||
                Get("InstallerAbortsTerminal") == "true" || Get("InstallLocationRequired") == "true" ||
                GetNode("UnsupportedOSArchitectures") is not null || GetNode("Platform") is not null ||
                GetNode("Markets") is not null) continue;
            if (GetNode("InstallModes") is YamlSequenceNode modes && !modes.Children.OfType<YamlScalarNode>().Any(n => n.Value == "silent")) continue;
            if (Get("MinimumOSVersion") is { Length: > 0 } minimum &&
                (!System.Version.TryParse(minimum, out var required) || OperatingSystem.IsWindows() && Environment.OSVersion.Version < required)) continue;
            if (!Uri.TryCreate(Get("InstallerUrl"),UriKind.Absolute,out var url) || !IsPublicHttps(url) ||
                !Regex.IsMatch(Get("InstallerSha256"),"^[A-Fa-f0-9]{64}$")) continue;
            string? args = Arguments(type,scope, GetNode("InstallerSwitches") as YamlMappingNode);
            if (args is null) continue;
            string code = Get("ProductCode"), upgrade = "";
            if (GetNode("AppsAndFeaturesEntries") is YamlSequenceNode entries)
            {
                if (entries.Children.Count != 1 || entries.Children[0] is not YamlMappingNode feature) continue;
                if (S(feature,"InstallerType") is {Length:>0} featureType && featureType != type) continue;
                if (S(feature,"ProductCode") is {Length:>0} nested) { if (code.Length>0 && !code.Equals(nested,StringComparison.OrdinalIgnoreCase)) continue; code=nested; }
                upgrade=S(feature,"UpgradeCode");
            }
            if (type is "msi" or "wix" && !Guid.TryParse(code,out _)) continue;
            options.Add(new(reference.Id,name,publisher,reference.Version,type,arch,targetScope,url,Get("InstallerSha256"),code,upgrade,args));
        }
        var unique = options.Distinct().ToArray();
        return unique.Length == 1 ? unique[0] : null;
    }
    internal static string? Arguments(string type, string scope, YamlMappingNode? switches)
    {
        string silent = switches is null ? "" : S(switches,"Silent");
        string[] known = type switch
        {
            "nullsoft" => ["/S"],
            "inno" => ["/VERYSILENT","/SILENT","/SUPPRESSMSGBOXES","/NORESTART","/SP-","/NOCLOSEAPPLICATIONS","/NORESTARTAPPLICATIONS"],
            "msi" or "wix" => ["/quiet","/qn","/norestart"], _=>[]
        };
        if (known.Length == 0 || silent.Split(' ',StringSplitOptions.RemoveEmptyEntries).Any(t => !known.Contains(t,StringComparer.Ordinal))) return null;
        var extra = new List<string>();
        foreach (string key in new[] {"Custom","Upgrade"})
        {
            string raw=switches is null ? "" : S(switches,key);
            foreach(string token in raw.Split(' ',StringSplitOptions.RemoveEmptyEntries))
                if (type=="inno" && token is "/NORUN" or "/UPDATE") extra.Add(token); else return null;
        }
        if (switches is not null && switches.Children.Keys.OfType<YamlScalarNode>().Any(k => k.Value is "InstallLocation" or "Log" or "Repair")) return null;
        return type switch
        {
            "nullsoft" => "/S",
            "inno" => "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS " +
                (scope=="machine" ? "/ALLUSERS" : "/CURRENTUSER") + (extra.Count>0?" "+string.Join(' ',extra):""),
            _ => "/qn /norestart"
        };
    }
    internal static bool IsPublicHttps(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme!="https" || uri.Port!=443 || uri.UserInfo.Length>0 || uri.Fragment.Length>0 ||
            uri.IsLoopback || uri.HostNameType!=UriHostNameType.Dns || uri.Host.IndexOf('.')<0 || uri.Host.EndsWith(".local",StringComparison.OrdinalIgnoreCase)) return false;
        return !uri.Host.EndsWith(".localhost",StringComparison.OrdinalIgnoreCase);
    }
    private static YamlNode? N(YamlMappingNode map,string key) => map.Children.TryGetValue(new YamlScalarNode(key),out var node)?node:null;
    private static string S(YamlMappingNode map,string key) => N(map,key) is YamlScalarNode scalar ? scalar.Value ?? "" : "";
}
