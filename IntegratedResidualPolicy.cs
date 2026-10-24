using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GerenciadorAplicativos;

internal sealed record ResidualAppIdentity(string Name, string Publisher, string Version,
    string InventoryId, string InstallLocation, string MsiProductCode,
    string UninstallString, string QuietUninstallString)
{
    internal static ResidualAppIdentity From(ProgramEntry app) => new(app.Name, app.Publisher,
        app.Version, app.InventoryId, app.InstallLocation, app.MsiProductCode,
        app.UninstallString, app.QuietUninstallString);
    internal ProgramEntry Entry() => new() { Name = Name, Publisher = Publisher, Version = Version,
        InventoryId = InventoryId, InstallLocation = InstallLocation, MsiProductCode = MsiProductCode,
        UninstallString = UninstallString, QuietUninstallString = QuietUninstallString };
    internal string TechnicalId => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(InventoryId + "|" + Name + "|" + Publisher)))[..16];
}
internal sealed record ResidualKeyLocation(RegistryHive Hive, RegistryView View, string Subkey);
internal sealed record ResidualProblem(string Stage, string Reason, int Error = 0);
internal sealed record ResidualRootEvidence(string Path, IReadOnlyList<InstallerFileGuard.DirectoryIdentity> Chain);
internal sealed record ResidualUninstallContext(ResidualAppIdentity App, string Policy,
    IReadOnlyList<ResidualRootEvidence> Roots, IReadOnlyList<ResidualKeyLocation> Keys,
    IReadOnlyList<string> Processes, int Preserved, IReadOnlyList<ResidualProblem> Problems);
internal sealed record ResidualFileEvidence(string Id, string Path, long Bytes, string Digest,
    InstallerFileGuard.FileIdentity Identity, IReadOnlyList<InstallerFileGuard.DirectoryIdentity> Parents);
internal sealed record ResidualDirectoryEvidence(string Id, string Path,
    IReadOnlyList<InstallerFileGuard.DirectoryIdentity> Chain);
internal sealed record ResidualKeyEvidence(string Id, RegistryHive Hive, RegistryView View,
    string Subkey, string Digest, int Entries);
internal sealed record IntegratedResidualPlan(ResidualUninstallContext Context,
    IReadOnlyList<ResidualFileEvidence> Files, IReadOnlyList<ResidualDirectoryEvidence> Directories,
    IReadOnlyList<ResidualKeyEvidence> Keys, int Preserved, bool Complete,
    IReadOnlyList<ResidualProblem> Problems)
{
    internal int Eligible => Files.Count + Directories.Count + Keys.Count;
    // Complete protege a selecao executavel; Conclusive descreve a cobertura,
    // inclusive quando outra parte acessivel da selecao pode ser limpa.
    internal bool Conclusive => Complete && Problems.All(p => p.Reason is
        "personal_or_shared_root" or "shared_root" or "link_preserved" or
        "personal_folder_preserved" or "personal_or_unknown_file" or "ownership_not_proven");
    internal ResidualAssessment Assessment => !Conclusive ? ResidualAssessment.Inconclusive :
        Eligible > 0 ? ResidualAssessment.Removable : Preserved > 0 ? ResidualAssessment.Preserved : ResidualAssessment.None;
}
internal enum ResidualAssessment { None, Removable, Preserved, Inconclusive }
internal sealed record ResidualScope(string Policy, IReadOnlyList<string> Roots,
    IReadOnlyList<ResidualKeyLocation> Keys, IReadOnlyList<string> Processes);

internal static class IntegratedResidualPolicy
{
    internal static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;
    internal static bool Within(string root, string target) =>
        Paths.Equals(root.TrimEnd('\\'), target.TrimEnd('\\')) ||
        target.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

    internal static ResidualScope Build(ResidualAppIdentity identity)
    {
#if GF_PROGRESSIVE
        if (SyntheticScope is not null) return SyntheticScope(identity);
#endif
        var app = identity.Entry();
        if (ProtectedApplications.IsProtectedName(app.Name) || string.IsNullOrWhiteSpace(app.InventoryId))
            return new("unsupported", [], [], []);
        var known = ApplicationIdentityCatalog.Resolve(app.Name, app.Publisher);
        if (known?.CleanupPolicy is null && app.Publisher.Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
            return new("unsupported_system_scope", [], [], []);
        var rule = UninstallRuleCatalog.Rules.SingleOrDefault(r => r.Id == known?.CleanupPolicy);
        var roots = rule?.Folders.ToList() ?? [];
        var keys = new List<ResidualKeyLocation>();
        foreach (string key in rule?.RegistryKeys ?? [])
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            // Software\\Produto em HKCU e compartilhado entre as visoes WOW64.
            if (hive == RegistryHive.CurrentUser && view == RegistryView.Registry32) continue;
            keys.Add(new(hive, view, key));
        }
        string? install = DedicatedInstallRoot(identity, known);
        if (install is not null)
        {
            roots.Add(install);
            // Sem receita: nome exato + comando dentro da instalacao dedicada.
            // A chave ainda exigira um valor de caminho corroborante ANTES e
            // DEPOIS da desinstalacao; nome semelhante sozinho nao autoriza.
            string product = Path.GetFileName(install);
            if (rule is null && SafeProductLeaf(product))
                foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                    if (hive != RegistryHive.CurrentUser || view != RegistryView.Registry32)
                        keys.Add(new(hive, view, @"Software\" + product));
        }
        // Locais canonicos mantidos pelas politicas anteriores, agora no mesmo motor.
        string? leaf = rule?.Id switch { "notepadpp" => "Notepad++", "sumatrapdf" => "SumatraPDF",
            "7zip" => "7-Zip", _ => null };
        if (leaf is not null)
            foreach (var location in new[] { Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86 })
                roots.Add(Path.Combine(Environment.GetFolderPath(location), leaf));
        string[] processes = known?.Id switch
        {
            "notepadpp" => ["notepad++"], "sumatrapdf" => ["SumatraPDF"],
            "sevenzip" => ["7zFM", "7zG", "7z", "7za", "7zr"],
            "obs" => ["obs64", "obs32"], "vlc" => ["vlc"],
            "audacity" => ["audacity"], "foobar2000" => ["foobar2000"], _ => []
        };
        return new(rule?.Id ?? (install is null ? "unsupported" : "registered-install"),
            roots.Distinct(Paths).ToArray(), keys.ToArray(), processes);
    }

    // Sem receita: somente uma instalacao dedicada, corroborada pelo comando
    // registrado, nome exato do produto e base conhecida. Nao inventa chaves
    // nem pastas de perfil por semelhanca de nomes.
    internal static string? DedicatedInstallRoot(ResidualAppIdentity app, UnifiedAppRule? known)
    {
        try
        {
            string raw = app.InstallLocation;
            if (string.IsNullOrWhiteSpace(raw))
            {
                var commands = new[] { app.UninstallString, app.QuietUninstallString }
                    .Select(value => UninstallCommand.TryParse(value, out var command) && command.ProductCode is null
                        ? Path.GetDirectoryName(command.FileName) : null)
                    .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(Paths).ToArray();
                if (commands.Length != 1) return null;
                raw = commands[0]!;
            }
            string path = InstallerFileGuard.ValidateLocalPath(raw).TrimEnd('\\');
            string? parent = Path.GetDirectoryName(path);
            string[] bases = [Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")];
            if (!bases.Any(b => !string.IsNullOrWhiteSpace(b) && Paths.Equals(b, parent))) return null;
            string leaf = Path.GetFileName(path);
            if (new[] { "Common Files", "WindowsApps", "Microsoft", "Windows", "Shared", "Packages",
                "Windows Defender", "Windows Defender Advanced Threat Protection", "Windows NT", "Windows Mail",
                "Windows Media Player", "Windows Photo Viewer", "NVIDIA Corporation", "Intel", "AMD", "Realtek" }.Contains(leaf, Paths))
                return null;
            string product = Regex.Replace(app.Name, @"\s*\((?:x64|x86|64-bit(?: x64)?|32-bit(?: x86)?)\)$", "",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            product = Regex.Replace(product, @"\s+v?\d+(?:\.\d+){1,4}$", "",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            static string Normalize(string value) => new(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            if (Normalize(leaf).Length < 4 ||
                (Normalize(leaf) != Normalize(product) && Normalize(leaf) != Normalize(known?.Name ?? ""))) return null;
            if (!new[] { app.UninstallString, app.QuietUninstallString }.Any(raw =>
                UninstallCommand.TryParse(raw, out var command) && command.ProductCode is null &&
                Within(path, command.FileName))) return null;
            return path;
        }
        catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex)) { return null; }
    }

    private static bool SafeProductLeaf(string leaf) => leaf.Length is >= 4 and <= 100 &&
        leaf.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-' or '_' or '+' or '.') &&
        !new[] { "Microsoft", "Windows", "Policies", "Classes", "Common", "Shared", "Software", "Mozilla", "Google" }.Contains(leaf, Paths);

    internal static bool RegistryOwned(ResidualUninstallContext context, RegistryKey key)
    {
        if (context.Policy != "registered-install") return true;
        string? install = DedicatedInstallRoot(context.App, null);
        if (install is null) return false;
        foreach (string name in new[] { "InstallPath", "InstallDir", "InstallLocation", "Path", "ExecutablePath" })
            if (key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string value)
                try
                {
                    string path = InstallerFileGuard.ValidateLocalPath(value.Trim('"')).TrimEnd('\\');
                    if (Paths.Equals(install, path) || new[] { context.App.UninstallString, context.App.QuietUninstallString }
                        .Any(raw => UninstallCommand.TryParse(raw, out var command) && Paths.Equals(command.FileName, path)))
                        return true;
                }
                catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex)) { }
        return false;
    }

    internal static bool Shared(string root, ResidualAppIdentity app, IReadOnlyCollection<ProgramEntry> inventory)
        => inventory.Any(other => !Paths.Equals(other.InventoryId, app.InventoryId) &&
            (ResidualReadSafety.PathsOverlap(root, other.InstallLocation) ||
             new[] { other.UninstallString, other.QuietUninstallString }.Any(raw =>
                 UninstallCommand.TryParse(raw, out var command) && command.ProductCode is null && Within(root, command.FileName))));

    internal static bool ProcessesClosed(ResidualUninstallContext context, IEnumerable<ResidualFileEvidence>? files = null)
    {
        try
        {
            var names = context.Processes.Concat((files ?? []).Where(f =>
                Path.GetExtension(f.Path).Equals(".exe", StringComparison.OrdinalIgnoreCase)).Select(f => Path.GetFileNameWithoutExtension(f.Path)))
                .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(Paths);
            foreach (string name in names)
            {
                var found = Process.GetProcessesByName(name);
                try { if (found.Length != 0) return false; }
                finally { foreach (var process in found) process.Dispose(); }
            }
            return true;
        }
        catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex)) { return false; }
    }

    internal static bool PersonalDirectory(string path) => new[] { "backup", "backups", "recordings", "documents",
        "downloads", "pictures", "music", "videos", "playlists", "saved games", "saves", "projects",
        "steamapps", "games", "gog games", "epic games", "userdata", "libraries", "credentials", "passwords", "wallets" }
        .Contains(Path.GetFileName(path), Paths);

    internal static bool EligibleFile(string path)
    {
        string name = Path.GetFileName(path);
        if (Regex.IsMatch(name, @"^(?:credentials?|passwords?|secrets?|wallets?|logins?|tokens?|accounts?)[._-]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        if (name.Equals("SumatraPDF-settings.txt", StringComparison.OrdinalIgnoreCase)) return true;
        // Formatos pessoais, bancos de credenciais, projetos e tipos desconhecidos
        // sao preservados inclusive dentro de uma pasta dedicada ao aplicativo.
        if (new[] { "config.xml", "langs.xml", "shortcuts.xml", "session.xml", "stylers.xml", "contextMenu.xml",
            "nativeLang.xml", "settings.json", "config.json", "global.json", "package.json", "package-lock.json", "preferences.json" }.Contains(name, Paths) ||
            name.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)) return true;
        return new[] { ".exe", ".dll", ".pdb", ".ini", ".cfg", ".config",
            ".log", ".tmp", ".cache", ".manifest", ".pak", ".qm", ".mo", ".mui" }
            .Contains(Path.GetExtension(name), Paths);
    }
#if GF_PROGRESSIVE
    internal static Func<ResidualAppIdentity, ResidualScope>? SyntheticScope;
#endif
}
