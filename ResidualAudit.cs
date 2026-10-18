using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Complementa as receitas verificadas. Detecta, mas NUNCA apaga.
// Um registro ainda existente ou uma pasta com nome semelhante NAO prova lixo.
internal sealed record ResidualAuditFinding(string Location, string Kind, int Items, string Evidence);
internal sealed record ResidualAuditReport(
    string Product, bool UninstallConfirmed, IReadOnlyList<ResidualAuditFinding> Findings,
    IReadOnlyList<string> RelatedRegisteredPrograms);

internal static class ResidualAudit
{
    private const int ScanLimit = 2000;
    private const int MaxDetails = 16;
    internal static ResidualAuditReport Inspect(ProgramEntry previous,
        IReadOnlyCollection<ProgramEntry> current)
    {
        bool removed = !UninstallResiduals.StillInstalled(previous, current);
        var findings = new List<ResidualAuditFinding>();
        var examined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? familyName = CanonicalName(previous);
        // Caminho de instalação registrado: evidência, mas não autorização de exclusão.
        InspectFolder(previous.InstallLocation, "Instalação informada pelo fabricante",
            findings, examined);

        if (familyName is not null)
        {
            foreach (string root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            })
                InspectFolder(Path.Combine(root, familyName),
                    "Possíveis preferências e arquivos do aplicativo", findings, examined);

            if (IsApprovedRegistryLeaf(familyName))
            {
                try
                {
                    string key = @"Software\" + familyName;
                    using var handle = Registry.CurrentUser.OpenSubKey(key, writable: false);
                    if (handle is not null)
                        findings.Add(new(@"HKEY_CURRENT_USER\" + key, "Registro do usuário",
                            Math.Min(ScanLimit, handle.ValueCount + handle.SubKeyCount),
                            "Chave de nome exato; a propriedade ainda requer confirmação"));
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or
                    System.Security.SecurityException or IOException) { }
            }
        }
        string[] related = FindRelatedRegistrations(previous, current);
        return new(previous.Name, removed, findings.Take(MaxDetails).ToArray(), related);
    }

    internal static string[] FindRelatedRegistrations(ProgramEntry previous,
        IReadOnlyCollection<ProgramEntry> current)
    {
        // Só informar sem nunca classificar ou remover como lixo.
        string? head = BrandToken(previous.Name);
        if (head is null) return [];
        return current.Where(x =>
            !x.Name.Equals(previous.Name, StringComparison.OrdinalIgnoreCase) &&
            x.Name.StartsWith(head + " ", StringComparison.OrdinalIgnoreCase) &&
            !ProtectedApplications.IsProtectedName(x.Name))
            .Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToArray();
    }

    internal static string? CanonicalName(ProgramEntry app)
    {
        var matched = ApplicationIdentityCatalog.Resolve(app.Name, app.Publisher);
        string raw = matched?.Name ?? app.Name;
        raw = Regex.Replace(raw, @"\s+\((?:64-bit|32-bit|x64|x86)[^\)]*\)$", "",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        raw = Regex.Replace(raw, @"\s+(?:Client|Desktop App)$", "",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (raw.Length is < 6 or > 56) return null;
        if (Regex.IsMatch(raw, @"(?i)(windows|microsoft|driver|update|runtime|service|system|setup|plugin)"))
            return null; // never suggest generic system/vendor directories
        return Regex.IsMatch(raw, @"^[\p{L}\p{Nd} _+.\-]+$") ? raw : null;
    }

    private static bool IsApprovedRegistryLeaf(string name)
    {
        return name.Length is >= 6 and <= 56 &&
               Regex.IsMatch(name, @"^[\p{L}\p{Nd} _+.\-]+$");
    }

    private static string? BrandToken(string name)
    {
        string token = name.Trim().Split(' ', '(')[0];
        return token.Length >= 6 && token.Length <= 28 &&
               Regex.IsMatch(token, @"^[\p{L}\p{Nd}+-]+$") ? token : null;
    }

    internal static bool SafeAuditRoot(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return false;
        try
        {
            string full = Path.GetFullPath(folder.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar);
            if (!Path.IsPathFullyQualified(full)) return false;
            string[] allowedBases =
            [
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            ];
            bool under = allowedBases.Any(basePath =>
            {
                string baseFull = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar);
                string relative = Path.GetRelativePath(baseFull, full);
                string[] segments = relative.Split(Path.DirectorySeparatorChar);
                // Only one or two levels below trusted roots, never those roots themselves.
                return segments.Length is >= 1 and <= 2 &&
                    segments.All(x => x.Length >= 2 && x != ".." && x != "." &&
                        !new[] { "Microsoft", "Windows", "Packages", "Temp",
                            "Common", "Shared", "System", "Drivers" }
                            .Contains(x, StringComparer.OrdinalIgnoreCase));
            });
            if (!under || !Directory.Exists(full)) return false;
            for (DirectoryInfo? cursor = new DirectoryInfo(full); cursor is not null; cursor = cursor.Parent)
                if (cursor.Exists && (cursor.Attributes & FileAttributes.ReparsePoint) != 0)
                    return false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException) { return false; }
    }

    private static void InspectFolder(string? folder, string label,
        List<ResidualAuditFinding> findings, HashSet<string> examined)
    {
        if (!SafeAuditRoot(folder)) return;
        string full = Path.GetFullPath(folder!.Trim().Trim('"'));
        if (!examined.Add(full)) return;
        try
        {
            int files = 0;
            foreach (string entry in Directory.EnumerateFileSystemEntries(full,
                "*", SearchOption.TopDirectoryOnly))
            {
                if (++files > ScanLimit) return; // suspiciously huge: report nothing
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    return;
            }
            if (files > 0)
                findings.Add(new(full, "Pasta", files,
                    label + "; somente leitura, não é prova de exclusividade"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
