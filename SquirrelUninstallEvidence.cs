using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace GerenciadorAplicativos;

// Squirrel.Windows' own --uninstall --silent contract. Not Velopack,
// Squirrel.Mac, an arbitrary Update.exe, or an upstream update-feed command.
internal static class SquirrelUninstallEvidence
{
#if GF_PROGRESSIVE
    internal static string? SyntheticRoot = null;
    internal static Func<string, string?>? SyntheticSigner = null;
#endif
    internal static bool TrySelect(ProgramEntry app, ParsedUninstallCommand command, out ParsedUninstallCommand selected)
    {
        selected = command;
        if (app.InventoryOnly || ProtectedApplications.IsProtectedName(app.Name) || command.ProductCode is not null ||
            app.MsiProductCode.Length > 0 || !app.InventoryId.StartsWith("CurrentUser|", StringComparison.Ordinal) ||
            !Regex.IsMatch(app.Name, @"^[^\r\n]{1,256}$") ||
            Regex.IsMatch(app.Name, @"(?i)\b(runtime|redistributable|sdk|driver|framework|server|service)\b") ||
            command.Arguments != "--uninstall" || !Path.GetFileName(command.FileName).Equals("Update.exe", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            if (!UninstallCommand.TryParse(app.UninstallString, out var registered) || registered != command) return false;
            string root = InstallerFileGuard.ValidateLocalPath(Path.GetDirectoryName(command.FileName)!);
            string allowed = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#if GF_PROGRESSIVE
            allowed = SyntheticRoot ?? allowed;
#endif
            if (allowed.Length == 0 || !root.StartsWith(allowed.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ||
                app.InstallLocation.Length > 0 && !root.Equals(app.InstallLocation.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return false;
            using var pinned = InstallerFileGuard.PinDirectory(root, allowLeafWrites: true);
            using var updater = InstallerFileGuard.OpenRead(command.FileName, allowDirectoryWrites: true);
            using (var pe = new PEReader(updater, PEStreamOptions.LeaveOpen))
            {
                if (!pe.HasMetadata || pe.PEHeaders.PEHeader is null) return false;
                var metadata = pe.GetMetadataReader();
                if (!metadata.IsAssembly || metadata.GetString(metadata.GetAssemblyDefinition().Name) != "Update" ||
                    !metadata.TypeDefinitions.Any(h =>
                    { var t = metadata.GetTypeDefinition(h); return metadata.GetString(t.Namespace) == "Squirrel.Update" && metadata.GetString(t.Name) == "Program"; })) return false;
            }
            if (!Version.TryParse(app.Version, out _)) return false;
            string versionRoot = Path.Combine(root, "app-" + app.Version);
            using var versionPin = InstallerFileGuard.PinDirectory(versionRoot, allowLeafWrites: true);
            var executables = Directory.EnumerateFiles(versionRoot, "*.exe", SearchOption.TopDirectoryOnly).Take(101).ToArray();
            if (executables.Length > 100) return false;
            var binaries = executables.Where(file =>
            {
                var info = FileVersionInfo.GetVersionInfo(file);
                return InstallerEngineEvidence.SameProduct(info.ProductName ?? "", app.Name) &&
                    InstallerEngineEvidence.SameVersion(info.FileVersion ?? "", app.Version);
            }).ToArray();
            if (binaries.Length != 1) return false;
            using var binary = InstallerFileGuard.OpenRead(binaries[0], allowDirectoryWrites: true);
            string? signer = Signer(command.FileName), productSigner = Signer(binaries[0]);
            if (signer is null || productSigner is null || !signer.Equals(productSigner, StringComparison.OrdinalIgnoreCase) ||
                InstallerEngineEvidence.ProductKey(signer) != InstallerEngineEvidence.ProductKey(app.Publisher)) return false;
            string packages = Path.Combine(root, "packages");
            using var packagesPin = InstallerFileGuard.PinDirectory(packages, allowLeafWrites: true);
            using var releases = InstallerFileGuard.OpenRead(Path.Combine(packages, "RELEASES"), allowDirectoryWrites: true);
            if (releases.Length > 1024 * 1024) return false;
            using var text = new StreamReader(releases, leaveOpen: true);
            var lines = text.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 512) return false;
            var candidates = lines.Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(p => p.Length == 3 && Regex.IsMatch(p[0], "^[A-Fa-f0-9]{40}$") &&
                    p[1].EndsWith("-" + app.Version + "-full.nupkg", StringComparison.Ordinal) &&
                    p[1].IndexOfAny(['/', '\\', ':']) < 0 && p[1] != ".." && long.TryParse(p[2], out long size) && size is > 0 and < 256L * 1024 * 1024).ToArray();
            if (candidates.Length != 1) return false;
            var candidate = candidates[0];
            string package = Path.Combine(packages, candidate[1]);
            using var payload = InstallerFileGuard.OpenRead(package, allowDirectoryWrites: true);
            if (payload.Length != long.Parse(candidate[2], System.Globalization.CultureInfo.InvariantCulture) ||
                !Convert.ToHexString(SHA1.HashData(payload)).Equals(candidate[0], StringComparison.OrdinalIgnoreCase)) return false;
            payload.Position = 0;
            using var archive = new ZipArchive(payload, ZipArchiveMode.Read, true);
            if (archive.Entries.Count > 10000) return false;
            var specs = archive.Entries.Where(e => !e.FullName.Contains('/') && e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (specs.Length != 1 || specs[0].Length > 1024 * 1024) return false;
            using var xml = XmlReader.Create(specs[0].Open(), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var meta = XDocument.Load(xml).Root?.Elements().SingleOrDefault(e => e.Name.LocalName == "metadata");
            string Get(string key) => meta?.Elements().SingleOrDefault(e => e.Name.LocalName == key)?.Value ?? "";
            string id = Get("id");
            if (!id.Equals(Path.GetFileName(root), StringComparison.OrdinalIgnoreCase) ||
                !InstallerEngineEvidence.SameVersion(Get("version"), app.Version) ||
                !InstallerEngineEvidence.SameProduct(Get("title"), app.Name) ||
                candidate[1] != id + "-" + app.Version + "-full.nupkg") return false;
            return UninstallCommand.TryParse("\"" + command.FileName + "\" --uninstall --silent", out selected);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidDataException or InvalidOperationException or BadImageFormatException or XmlException or
            System.Security.SecurityException or System.ComponentModel.Win32Exception or CryptographicException)
        { return false; }
    }
    private static string? Signer(string file)
    {
#if GF_PROGRESSIVE
        if (SyntheticSigner is not null) return SyntheticSigner(file);
#endif
        return TrustedFileSignature.TryGetSigner(file, out string signer, out _) ? signer : null;
    }
}
