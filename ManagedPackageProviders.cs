using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace GerenciadorAplicativos;

// All paths remain local. No PowerShell, scripts, manifests-as-code or CLI is
// evaluated. Metadata is a receipt for external management, never a GF permit.
internal static class ManagedMetadataFiles
{
    internal static string Read(string path, int limit = 1024 * 1024)
    {
        if (!Safe(path) || new FileInfo(path).Length > limit) throw new InvalidDataException("Unsafe metadata");
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var text = new StreamReader(input, new System.Text.UTF8Encoding(false, true), true);
        var data = text.ReadToEnd();
        if (data.Length > limit) throw new InvalidDataException("Metadata limit");
        return data;
    }
    internal static bool Safe(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal)) return false;
        var current = new FileInfo(Path.GetFullPath(path)) as FileSystemInfo;
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            current = Directory.GetParent(current.FullName);
        }
        return true;
    }
    internal static string Receipt(params string[] texts) => Convert.ToHexString(SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(string.Concat(texts.Select(t => t.Length + ":" + t)))));
    internal static string String(JsonElement element, string key) => element.TryGetProperty(key, out var item) &&
        item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : "";
    internal static JsonDocument Json(string path) => JsonDocument.Parse(Read(path), new() { MaxDepth = 24 });
    internal static IEnumerable<string> Directories(string root) => !Directory.Exists(root) || !Safe(root) ? [] :
        Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly).Take(2001);
    internal static bool Expected(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or
        InvalidDataException or JsonException or XmlException or System.Security.SecurityException or NotSupportedException;
}

internal sealed class ScoopDiscoveryProvider(string root, string scope = "user") : IApplicationDiscoveryProvider
{
    public ProviderDescriptor Descriptor => new(scope == "machine" ? "scoop-global" : "scoop", 10,
        ProviderCapabilities.DetectInstallations | ProviderCapabilities.QueryVersions | ProviderCapabilities.AssociateIdentities,
        TimeSpan.FromSeconds(5));
    public Task<ApplicationProviderResult> DiscoverAsync(ProviderQuery query, CancellationToken token)
    {
        var rows = new List<ProviderInstallation>(); bool partial = false;
        string id = Descriptor.Id;
        try
        {
            if (!Directory.Exists(root)) return Task.FromResult(new ApplicationProviderResult(id, "absent", []));
            foreach (string app in ManagedMetadataFiles.Directories(Path.Combine(root, "apps")))
            {
                token.ThrowIfCancellationRequested();
                if (rows.Count >= 2000) { partial = true; break; }
                try
                {
                    string package = Path.GetFileName(app);
                    if (!PackageIdentityMatcher.ValidId(package) || !ManagedMetadataFiles.Safe(app)) continue;
                    string current = Path.Combine(app, "current");
                    if (!Directory.Exists(current)) continue;
                    var info = new DirectoryInfo(current);
                    // Scoop normally uses a junction. Follow only this one known
                    // link and require a real version directory in the same app.
                    string target = (info.Attributes & FileAttributes.ReparsePoint) != 0
                        ? info.ResolveLinkTarget(true)?.FullName ?? "" : info.FullName;
                    if (target.Length == 0 || !Path.GetDirectoryName(target)!.Equals(Path.GetFullPath(app), StringComparison.OrdinalIgnoreCase) ||
                        !ManagedMetadataFiles.Safe(target)) { partial = true; continue; }
                    string manifest = Metadata(target, "scoop-manifest.json", "manifest.json");
                    string install = Metadata(target, "scoop-install.json", "install.json");
                    using var release = ManagedMetadataFiles.Json(manifest);
                    using var receipt = ManagedMetadataFiles.Json(install);
                    string version = ManagedMetadataFiles.String(release.RootElement, "version");
                    string arch = ManagedMetadataFiles.String(receipt.RootElement, "architecture") switch
                        { "64bit" => "x64", "32bit" => "x86", "arm64" => "arm64", _ => "unknown" };
                    if (version.Length == 0 || version.Length > 128 ||
                        Path.GetFileName(target) != version && Path.GetFileName(target) != "current") continue;
                    string bucket = ManagedMetadataFiles.String(receipt.RootElement, "bucket");
                    string latest = "";
                    // Only an already installed clone of the official buckets.
                    // Its version is explicitly described as locally cached.
                    if (bucket is "main" or "extras")
                    {
                        string bucketRoot = Path.Combine(root, "buckets", bucket);
                        string config = Path.Combine(bucketRoot, ".git", "config");
                        string expected = bucket == "main" ? "Main" : "Extras";
                        if (File.Exists(config) && OfficialBucket(ManagedMetadataFiles.Read(config, 65536), expected))
                        {
                            string path = Path.Combine(bucketRoot, "bucket", package + ".json");
                            if (File.Exists(path) && File.GetLastWriteTimeUtc(path) >= DateTime.UtcNow.AddDays(-7))
                            { using var remote = ManagedMetadataFiles.Json(path); latest = ManagedMetadataFiles.String(remote.RootElement, "version"); }
                        }
                    }
                    // Description is prose, not product identity. Keep the manager's
                    // exact package ID as display name and do not match it to ARP by name.
                    rows.Add(new(new(id, package, package, "Scoop (metadados locais)", version, arch, scope,
                        Location: target, Managed: true), ManagedMetadataFiles.Receipt(ManagedMetadataFiles.Read(manifest),
                            ManagedMetadataFiles.Read(install)), latest, "scoop_local_bucket"));
                }
                catch (Exception ex) when (ManagedMetadataFiles.Expected(ex)) { partial = true; }
            }
            return Task.FromResult(new ApplicationProviderResult(id, partial ? "partial" : "ok", rows));
        }
        catch (Exception ex) when (ManagedMetadataFiles.Expected(ex))
        { return Task.FromResult(new ApplicationProviderResult(id, "partial", rows)); }
    }
    private static string Metadata(string root, string modern, string legacy)
    {
        string first = Path.Combine(root, modern), second = Path.Combine(root, legacy);
        // New names have priority: old manifest.json may belong to the app.
        return File.Exists(first) ? first : second;
    }
    private static bool OfficialBucket(string config, string repo)
    {
        string section = ""; var urls = new List<string>();
        foreach (string row in config.Split('\n'))
        {
            string line = row.Trim();
            if (line.StartsWith('[')) section = line;
            else if (section == "[remote \"origin\"]" && line.StartsWith("url", StringComparison.Ordinal) && line.Contains('='))
                urls.Add(line[(line.IndexOf('=') + 1)..].Trim().TrimEnd('/'));
        }
        return urls.Count == 1 && (urls[0].Equals("https://github.com/ScoopInstaller/" + repo, StringComparison.OrdinalIgnoreCase) ||
            urls[0].Equals("https://github.com/ScoopInstaller/" + repo + ".git", StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class ChocolateyDiscoveryProvider(string root) : IApplicationDiscoveryProvider
{
    public ProviderDescriptor Descriptor => new("chocolatey", 20, ProviderCapabilities.DetectInstallations |
        ProviderCapabilities.AssociateIdentities, TimeSpan.FromSeconds(5));
    public Task<ApplicationProviderResult> DiscoverAsync(ProviderQuery query, CancellationToken token)
    {
        var rows = new List<ProviderInstallation>(); bool partial = false;
        try
        {
            if (!Directory.Exists(root)) return Task.FromResult(new ApplicationProviderResult("chocolatey", "absent", []));
            foreach (string folder in ManagedMetadataFiles.Directories(Path.Combine(root, "lib")))
            {
                token.ThrowIfCancellationRequested();
                if (rows.Count >= 2000) { partial = true; break; }
                try
                {
                    string package = Path.GetFileName(folder);
                    if (!PackageIdentityMatcher.ValidId(package) || !ManagedMetadataFiles.Safe(folder) ||
                        File.Exists(Path.Combine(folder, ".chocolateyPending"))) continue;
                    string spec = Path.Combine(folder, package + ".nuspec");
                    string text = ManagedMetadataFiles.Read(spec);
                    using var xml = XmlReader.Create(new StringReader(text), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                    var metadata = XDocument.Load(xml).Root?.Elements().SingleOrDefault(e => e.Name.LocalName == "metadata");
                    string Get(string key) => metadata?.Elements().SingleOrDefault(e => e.Name.LocalName == key)?.Value ?? "";
                    string version = Get("version");
                    if (Get("id") != package || version.Length is 0 or > 128) continue;
                    string nupkg = Path.Combine(folder, package + ".nupkg");
                    if (!File.Exists(nupkg) || !ManagedMetadataFiles.Safe(nupkg) || new FileInfo(nupkg).Length > 256L * 1024 * 1024) continue;
                    using var input = File.OpenRead(nupkg);
                    using var zip = new ZipArchive(input, ZipArchiveMode.Read);
                    if (zip.Entries.Count > 10000) continue;
                    var entry = zip.Entries.Where(e => e.FullName.Equals(package + ".nuspec", StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (entry.Length != 1 || entry[0].Length > 1024 * 1024) continue;
                    using var reader = new StreamReader(entry[0].Open());
                    using var embeddedXml = XmlReader.Create(new StringReader(reader.ReadToEnd()), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                    var embedded = XDocument.Load(embeddedXml).Root?.Elements().SingleOrDefault(e => e.Name.LocalName == "metadata");
                    if (embedded?.Elements().SingleOrDefault(e => e.Name.LocalName == "id")?.Value != package ||
                        embedded.Elements().SingleOrDefault(e => e.Name.LocalName == "version")?.Value != version) continue;
                    // A local Chocolatey package is not proof that its underlying
                    // product is present. Keep it separate from ARP and disclose it.
                    rows.Add(new(new("chocolatey", package, package, "Chocolatey (registro de pacote)", version,
                        Scope: "unknown", Location: folder, Managed: true), ManagedMetadataFiles.Receipt(text),
                        Reason: "chocolatey_package_not_product"));
                }
                catch (Exception ex) when (ManagedMetadataFiles.Expected(ex) || ex is InvalidOperationException) { partial = true; }
            }
            return Task.FromResult(new ApplicationProviderResult("chocolatey", partial ? "partial" : "ok", rows));
        }
        catch (Exception ex) when (ManagedMetadataFiles.Expected(ex))
        { return Task.FromResult(new ApplicationProviderResult("chocolatey", "partial", rows)); }
    }
}
