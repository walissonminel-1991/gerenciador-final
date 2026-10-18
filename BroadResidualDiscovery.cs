using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Varredura geral para localizar possiveis dados de software ANTERIORES a
// instalacao do Gerenciador Final. SOMENTE LEITURA; resultados sao HIPOTESES.
// Nao varre bibliotecas pessoais nem elimina arquivos/Registro. Nenhuma receita
// de exclusao pode usar o resultado deste motor como autorizacao.
internal sealed record BroadResidualEvidence(string Name, string Location, string Kind, string RootGroup);
internal sealed record BroadResidualResult(
    IReadOnlyList<ResidualAuditReport> Reports,
    int RootsChecked, int EntriesSeen, int RestrictedRoots, bool Incomplete);

internal static class BroadResidualDiscovery
{
    private const int MaxEntriesPerRoot = 2500;
    private const int MaxReportGroups = 100;
    private const int MaxEvidencePerGroup = 7;
    private static readonly TimeSpan MaxElapsed = TimeSpan.FromSeconds(14);
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> Reserved = new(Comparer)
    {
        "Windows", "Microsoft", "System", "System32", "Temp", "Cache",
        "Caches", "Package Cache", "Packages", "Common Files", "Common",
        "Shared", "Programs", "Start Menu", "Startup", "WindowsApps",
        "Application Data", "AppData", "Local", "Roaming", "LocalLow",
        "Program Files", "Program Files (x86)", "OneDriveTemp", "Drivers",
        "DriverStore", "Resources", "Installer", "Installation",
        "Office", "Windows Kits", "WindowsPowerShell", "Appx",
        "Diagnostics", "NativeImages", "Fonts", "Templates",
        "Documents", "Pictures", "Images", "Music", "Videos", "Downloads",
        "Desktop", "Saved Games", "Contacts", "Favorites",
        "Searches", "Links", "Public", "Users", "Default", "All Users",
        // Infraestrutura do Windows, SDKs, stores de drivers e namespaces de
        // Registro compartilhados nao constituem residuos de aplicativos.
        "Classes", "Clients", "Policies", "RegisteredApplications",
        "Wow6432Node", "Accessibility", "Accessories",
        "Administrative Tools", "Maintenance", "System Tools",
        "Windows Defender", "Windows Mail", "Windows NT",
        "Windows Photo Viewer", "Windows PowerShell", "Windows Sidebar",
        "Internet Explorer", "MSBuild", "NuGet", "Reference Assemblies",
        "PackageManagement", "ODBC", "MozillaPlugins", "Khronos",
        "Icaros", "AGEIA Technologies", "D3DSCache", "CrashDumps",
        "CrashReportClient", "DWriteCore", "ConnectedDevicesPlatform",
        "Comms", "ASP.NET", "dotnet", "Intel", "OneDrive",
        "Setup", "Backup", "DefaultUserEnvironment", "Applogon",
        "DeviceInfo", "Downloaded Installations", "OverwolfPersist"
    };

    // Somente os diretórios controlados pela própria ferramenta; nunca executar
    // busca global em C:\ nem atravessar Documents, Pictures, Music, Downloads
    // ou outros volumes e perfis de usuário.
    internal static IReadOnlyList<(string Path, string Category)> Roots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var userPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var commonPrograms = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        string low = Path.GetFullPath(Path.Combine(local, "..", "LocalLow"));
        return new (string, string)[]
        {
            (local, "Dados locais"),
            (roaming, "Configurações"),
            (low, "Dados locais restritos"),
            (programData, "Dados de aplicativos"),
            (pf, "Arquivos de instalação"),
            (pf86, "Arquivos de instalação (x86)"),
            (userPrograms, "Atalhos do usuário"),
            (commonPrograms, "Atalhos compartilhados")
        }.Where(r => !string.IsNullOrWhiteSpace(r.Item1))
         .DistinctBy(r => Path.GetFullPath(r.Item1), Comparer)
         .ToArray();
    }

    internal static bool EligibleName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return false;
        string name = rawName.Trim();
        if (name.Length is < 4 or > 70 || name is "." or ".." || Reserved.Contains(name) ||
            Guid.TryParse(name, out _) || Regex.IsMatch(name, @"^\d+$"))
            return false;
        if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("~", StringComparison.Ordinal))
            return false;
        return Regex.IsMatch(name, @"^[\p{L}\p{Nd}][\p{L}\p{Nd} +._()\-\u00B5]{2,69}$");
    }

    internal static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string baseName = name.Trim();
        if (baseName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
            baseName.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            baseName = Path.GetFileNameWithoutExtension(baseName);
        baseName = Regex.Replace(baseName,
            @"\s*(?:\((?:x64|x86|64-bit|32-bit)\)|(?:64-bit|32-bit|x64|x86))$",
            "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        return EligibleName(baseName) ? baseName : null;
    }

    private static string NormalizedCompare(string name) =>
        Regex.Replace(name.ToUpperInvariant(), @"[^\p{L}\p{Nd}]+", " ").Trim();

    internal static bool IsProtectedByInstalled(string product,
        IReadOnlyCollection<ProgramEntry> installed)
    {
        string candidate = NormalizedCompare(product);
        if (candidate.Length < 4) return true;
        string[] names = installed.SelectMany(app => new[]
            { app.Name, app.Publisher, app.CatalogDisplayName }).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizedCompare).Distinct(Comparer).ToArray();
        foreach (string full in names)
        {
            if (full == candidate ||
                full.StartsWith(candidate + " ", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(full + " ", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        // EA app replaces Origin. The historical cache may contain cloud saves.
        if (candidate == "ORIGIN" &&
            installed.Any(p => p.Name.Equals("EA app", StringComparison.OrdinalIgnoreCase)
                || p.Name.StartsWith("EA Desktop", StringComparison.OrdinalIgnoreCase)))
            return true;
        return false;
    }

    private static bool IsReparse(FileSystemInfo item)
    {
        try { return (item.Attributes & FileAttributes.ReparsePoint) != 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return true; }
    }

    private static void InspectDirectory(string path, string category, string group,
        List<BroadResidualEvidence> evidence, ref int entries, ref int restricted,
        ref bool incomplete, Stopwatch clock)
    {
        try
        {
            var root = new DirectoryInfo(path);
            if (!root.Exists || IsReparse(root)) return;
            int inspected = 0;
            foreach (FileSystemInfo item in root.EnumerateFileSystemInfos())
            {
                if (clock.Elapsed > MaxElapsed) { incomplete = true; break; }
                if (++inspected > MaxEntriesPerRoot) { incomplete = true; break; }
                entries++;
                if (IsReparse(item)) continue;
                bool isDir = (item.Attributes & FileAttributes.Directory) != 0;
                if (!isDir && group != "StartMenu") continue;
                if (!isDir && !item.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
                    !item.Extension.Equals(".url", StringComparison.OrdinalIgnoreCase)) continue;
                string? name = NormalizeName(item.Name);
                if (name is null) continue;
                evidence.Add(new(name, item.FullName, isDir ? category : "Atalho", group));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException) { restricted++; incomplete = true; }
    }

    private static void InspectRegistryHive(RegistryHive hive, RegistryView view,
        string identifier, List<BroadResidualEvidence> evidence, ref int entries,
        ref int restricted, ref bool incomplete, Stopwatch clock)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? software = baseKey.OpenSubKey("SOFTWARE", writable: false);
            if (software is null) return;
            int inspected = 0;
            foreach (string keyName in software.GetSubKeyNames())
            {
                if (clock.Elapsed > MaxElapsed) { incomplete = true; break; }
                if (++inspected > MaxEntriesPerRoot) { incomplete = true; break; }
                entries++;
                string? normalized = NormalizeName(keyName);
                if (normalized is null) continue;
                evidence.Add(new(normalized,
                    identifier + @"\SOFTWARE\" + keyName, "Chave de Registro", "Registry"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException) { restricted++; incomplete = true; }
    }

    // Nenhuma pista isolada autoriza uma afirmacao de desinstalacao. Para
    // exibir um candidato geral, cruzamos dados de usuario/aplicativo e outra
    // fonte de natureza diferente. Diretorio + atalho, ou duas chaves nas
    // visoes do Registro, pode significar software AINDA instalado.
    internal static bool HasIndependentResidueEvidence(IEnumerable<BroadResidualEvidence> candidates)
    {
        var sources = candidates.Select(e => e.RootGroup)
            .ToHashSet(Comparer);
        return sources.Count >= 2 &&
            (sources.Contains("AppData") || sources.Contains("ProgramData")) &&
            (sources.Contains("Registry") || sources.Contains("StartMenu") ||
             sources.Contains("ProgramFiles"));
    }

    // Classificação independente do ambiente: o mesmo algoritmo pode ser testado
    // em dados fictícios, sem acessar o Registro do usuário nem excluir nada.
    internal static IReadOnlyList<ResidualAuditReport> Classify(
        IEnumerable<BroadResidualEvidence> raw,
        IReadOnlyCollection<ProgramEntry> installed,
        IReadOnlyCollection<string>? activeGameNames = null)
    {
        // Jogos Steam/Epic podem estar instalados sem entradas ARP. Nunca
        // classificar suas pastas ou preferencias como vestigios de remocao.
        var protectedGames = (activeGameNames ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizedCompare)
            .Where(name => name.Length >= 4)
            .ToHashSet(Comparer);
        return raw.Where(e => EligibleName(e.Name))
            .GroupBy(e => NormalizedCompare(e.Name), Comparer)
            .Where(group => !IsProtectedByInstalled(group.First().Name, installed) &&
                !protectedGames.Contains(NormalizedCompare(group.First().Name)) &&
                HasIndependentResidueEvidence(group))
            .Select(group =>
            {
                var all = group
                    .DistinctBy(e => e.Location, Comparer)
                    .ToArray();
                int sourceTypes = all.Select(e => e.RootGroup).Distinct(Comparer).Count();
                string confidence =
                    "Indícios em fontes diferentes; não comprovam desinstalação nem uso seguro para limpeza.";
                var findings = all.Take(MaxEvidencePerGroup)
                    .Select(e => new ResidualAuditFinding(e.Location,
                        "Vestígio não confirmado — " + e.Kind, 1,
                        confidence + " Somente leitura.")).ToArray();
                return new
                {
                    Group = group.First().Name,
                    Sources = sourceTypes,
                    Total = all.Length,
                    Findings = findings
                };
            })
            .OrderByDescending(g => g.Sources)
            .ThenByDescending(g => g.Total)
            .ThenBy(g => g.Group, Comparer)
            .Take(MaxReportGroups)
            .Select(g => new ResidualAuditReport(g.Group, false, g.Findings,
                Array.Empty<string>(), Historical: true))
            .ToArray();
    }

    internal static BroadResidualResult Inspect(IReadOnlyCollection<ProgramEntry> installed,
        IReadOnlyCollection<string>? activeGameNames = null, bool gameInventoryPartial = false)
    {
        var clock = Stopwatch.StartNew();
        var found = new List<BroadResidualEvidence>();
        int entries = 0, restricted = 0, rootsChecked = 0;
        bool incomplete = false;
        foreach (var (path, category) in Roots())
        {
            if (clock.Elapsed > MaxElapsed) { incomplete = true; break; }
            rootsChecked++;
            string group = category.StartsWith("Atalhos", StringComparison.Ordinal) ?
                "StartMenu" : category.StartsWith("Arquivos de instalação", StringComparison.Ordinal) ?
                "ProgramFiles" : category == "Dados de aplicativos" ? "ProgramData" : "AppData";
            InspectDirectory(path, category, group, found, ref entries,
                ref restricted, ref incomplete, clock);
        }
        foreach (var (hive, view, identifier) in new[]
        {
            (RegistryHive.CurrentUser, RegistryView.Default, "HKEY_CURRENT_USER"),
            (RegistryHive.LocalMachine, RegistryView.Registry64, "HKEY_LOCAL_MACHINE"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, "HKEY_LOCAL_MACHINE (32 bits)")
        })
        {
            if (clock.Elapsed > MaxElapsed) { incomplete = true; break; }
            rootsChecked++;
            InspectRegistryHive(hive, view, identifier, found, ref entries,
                ref restricted, ref incomplete, clock);
        }
        var reports = Classify(found, installed, activeGameNames);
        if (reports.Count >= MaxReportGroups || gameInventoryPartial) incomplete = true;
        return new(reports, rootsChecked, entries, restricted, incomplete);
    }
}
