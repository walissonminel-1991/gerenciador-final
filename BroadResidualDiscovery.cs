using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Varredura geral para localizar possiveis dados de software ANTERIORES a
// instalacao do Gerenciador Final. SOMENTE LEITURA; resultados sao HIPOTESES.
// Nao varre bibliotecas pessoais nem elimina arquivos/Registro. Nenhuma receita
// de exclusao pode usar o resultado deste motor como autorizacao.
internal sealed record BroadResidualEvidence(string Name, string Location, string Kind, string RootGroup, string Container = "", ResidualSource Sources = ResidualSource.None);
internal sealed record BroadResidualResult(
    IReadOnlyList<ResidualAuditReport> Reports,
    int RootsChecked, int EntriesSeen, int RestrictedRoots, bool Incomplete, ResidualCoverage? Coverage = null);

internal static class BroadResidualDiscovery
{
    private const int MaxEntriesPerRoot = 2500;
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    // Chaves com nomes de empresas podem representar perfis compartilhados de
    // vários jogos ainda instalados. A lista é de PROTEÇÃO, não de exclusão.
    private static readonly HashSet<string> KnownSharedPublishers = new(Comparer)
    {
        "Aquiris", "BANDAI NAMCO Entertainment", "Colossal Order",
        "Crystal Dynamics", "Eidos Montreal", "Guerrilla Games",
        "nWay Inc", "Red Dot Games", "Sucker Punch Productions",
        "Western Digital", "Opera Software", "Netease"
    };

    internal static bool IsSharedPublisherName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string normalized = name.Trim();
        return KnownSharedPublishers.Contains(normalized) ||
            Regex.IsMatch(normalized, @"(?:\b(?:Games|Entertainment|Productions|Studios|Software|Interactive|Technologies|Corporation|Inc)\.?$)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

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
        string low = string.IsNullOrWhiteSpace(local) ? "" :
            Path.Combine(Path.GetDirectoryName(local) ?? local, "LocalLow");
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
        return IsProtectedByNames(product, ProtectionNames(installed));
    }

    private static string[] ProtectionNames(IReadOnlyCollection<ProgramEntry> installed) =>
        installed.SelectMany(app => new[] { app.Name, app.Publisher, app.CatalogDisplayName }
            .Concat(ApplicationIdentityCatalog.Resolve(app.Name, app.Publisher)?.Names.Select(n => n.TrimEnd('*').Trim()) ?? []))
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizedCompare)
            .Where(x => x.Length > 0).Distinct(Comparer).Order(StringComparer.Ordinal).ToArray();

    // Índice ordenado: igualdade/prefixo por fronteira, sem varrer todo inventário por grupo.
    private static bool HasNameRelation(string candidate, string[] sorted)
    {
        int index = Array.BinarySearch(sorted, candidate, StringComparer.Ordinal);
        if (index >= 0) return true;
        index = ~Array.BinarySearch(sorted, candidate + " ", StringComparer.Ordinal);
        if (index < 0) return true;
        if (index < sorted.Length && sorted[index].StartsWith(candidate + " ", StringComparison.Ordinal)) return true;
        for (int i = candidate.IndexOf(' '); i >= 0; i = candidate.IndexOf(' ', i + 1))
            if (Array.BinarySearch(sorted, candidate[..i], StringComparer.Ordinal) >= 0) return true;
        return false;
    }

    private static bool IsProtectedByNames(string product, IReadOnlyCollection<string> names)
    {
        string candidate = NormalizedCompare(product);
        if (candidate.Length < 4) return true;
        if (HasNameRelation(candidate, names as string[] ?? names.Order(StringComparer.Ordinal).ToArray())) return true;
        return candidate switch
        {
            "GOG COM" => names.Any(n => n.StartsWith("GOG GALAXY", StringComparison.Ordinal)),
            "MICROVIRT" => names.Any(n => n == "MEMU" || n.StartsWith("MEMU PLAY", StringComparison.Ordinal)),
            "MPC HC" => names.Any(n => n.StartsWith("K LITE CODEC PACK", StringComparison.Ordinal) ||
                n.StartsWith("MEDIA PLAYER CLASSIC HOME CINEMA", StringComparison.Ordinal)),
            "ORIGIN" => names.Any(n => n == "EA APP" || n.StartsWith("EA DESKTOP", StringComparison.Ordinal)),
            _ => false
        };
    }

    private static bool IsReparse(FileSystemInfo item)
    {
        try { return (item.Attributes & FileAttributes.ReparsePoint) != 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return true; }
    }

    // Dois níveis no máximo. Não lê conteúdo nem resolve/executa atalhos.
    // O segundo nível encontra Produto em Fabricante/Produto sem lista fixa.
    internal static void InspectDirectory(string path, string category, string group,
        List<BroadResidualEvidence> evidence, ref int entries, ref int restricted,
        ref bool incomplete, ResidualScanBudget budget)
    {
        budget.Source(group);
        if (!budget.CanContinue()) { incomplete = true; budget.Unavailable(group); return; }
        if (!ResidualReadSafety.IsUnlinkedDirectory(path))
        { restricted++; incomplete = true; budget.Unavailable(group); return; }
        try
        {
            int inspected = 0;
            foreach (FileSystemInfo item in new DirectoryInfo(path).EnumerateFileSystemInfos())
            {
                if (!budget.Visit()) { incomplete = true; break; }
                if (++inspected > MaxEntriesPerRoot) { incomplete = true; budget.MarkIncomplete(ResidualScanIssue.EntryLimit); break; }
                entries++;
                if (IsReparse(item)) { incomplete = true; budget.MarkIncomplete(ResidualScanIssue.LinkSkipped); continue; }
                bool isDir = (item.Attributes & FileAttributes.Directory) != 0;
                if (!isDir && group != "StartMenu") continue;
                if (!isDir && !item.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
                    !item.Extension.Equals(".url", StringComparison.OrdinalIgnoreCase)) continue;
                string? name = NormalizeName(item.Name);
                if (name is null) continue;
                evidence.Add(new(name, item.FullName, isDir ? category : "Atalho", group));
                if (!isDir || !budget.CanContinue()) continue;
                // Uma falha num fabricante não abandona os demais diretórios.
                try
                {
                    if (!ResidualReadSafety.IsUnlinkedDirectory(item.FullName))
                    { incomplete = true; continue; }
                    int children = 0;
                    foreach (FileSystemInfo child in ((DirectoryInfo)item).EnumerateFileSystemInfos())
                    {
                        if (!budget.Visit() || ++children > 128) { incomplete = true; budget.MarkIncomplete(ResidualScanIssue.EntryLimit); break; }
                        entries++;
                        if (IsReparse(child)) { incomplete = true; budget.MarkIncomplete(ResidualScanIssue.LinkSkipped); continue; }
                        bool directory = (child.Attributes & FileAttributes.Directory) != 0;
                        if (!directory && (group != "StartMenu" ||
                            !new[] { ".lnk", ".url" }.Contains(child.Extension, Comparer))) continue;
                        string? childName = NormalizeName(child.Name);
                        if (directory) { incomplete = true; budget.MarkIncomplete(ResidualScanIssue.DepthLimit); }
                        if (childName is not null)
                            evidence.Add(new(childName, child.FullName,
                                directory ? category : "Atalho", group, name));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                    System.Security.SecurityException) { restricted++; incomplete = true; budget.MarkIncomplete(); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException) { restricted++; incomplete = true; budget.MarkIncomplete(); }
    }

    internal static void InspectRegistryHive(RegistryHive hive, RegistryView view,
        string identifier, List<BroadResidualEvidence> evidence, ref int entries,
        ref int restricted, ref bool incomplete, ResidualScanBudget budget, string softwarePath = "SOFTWARE")
    {
        if (!budget.CanContinue()) { incomplete = true; return; }
        budget.Source(identifier);
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? software = baseKey.OpenSubKey(softwarePath, writable: false);
            if (software is null) { incomplete = true; budget.Unavailable(identifier); return; }
            int inspected = 0;
            foreach (string keyName in software.GetSubKeyNames())
            {
                if (!budget.Visit()) { incomplete = true; break; }
                if (++inspected > MaxEntriesPerRoot) { incomplete = true; budget.MarkIncomplete(ResidualScanIssue.EntryLimit); break; }
                entries++;
                string? normalized = NormalizeName(keyName);
                if (normalized is null) continue;
                evidence.Add(new(normalized,
                    identifier + @"\" + softwarePath + @"\" + keyName, "Chave de Registro", "Registry"));
                if (!budget.CanContinue()) { incomplete = true; break; }
                try
                {
                    using RegistryKey? vendor = software.OpenSubKey(keyName, writable: false);
                    if (vendor is null) { incomplete = true; budget.MarkIncomplete(); continue; }
                    string[] children = vendor.GetSubKeyNames();
                    if (children.Length > 128) { incomplete = true; budget.MarkIncomplete(ResidualScanIssue.EntryLimit); }
                    foreach (string child in children.Take(128))
                    {
                        if (!budget.Visit()) { incomplete = true; break; }
                        entries++;
                        string? product = NormalizeName(child);
                        using var leaf = vendor.OpenSubKey(child, writable: false);
                        if (leaf is null) { incomplete = true; budget.MarkIncomplete(); }
                        else if (leaf.SubKeyCount > 0) { incomplete = true; budget.MarkIncomplete(ResidualScanIssue.DepthLimit); }
                        if (product is not null)
                            evidence.Add(new(product, identifier + @"\" + softwarePath + @"\" + keyName + @"\" + child,
                                "Configuração sob fabricante", "Registry", normalized));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                    System.Security.SecurityException) { restricted++; incomplete = true; budget.MarkIncomplete(); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException) { restricted++; incomplete = true; budget.MarkIncomplete(); }
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
             sources.Contains("ProgramFiles") || sources.Contains("Integration"));
    }

    // Um programa portatil pode estar ativo sem uma entrada ARP. Reconhecer
    // apenas nomes de processos EM EXECUCAO; nenhuma ausencia e interpretada
    // como prova de desinstalacao.
    internal static bool IsRunningProduct(string product,
        IReadOnlyCollection<string> runningNames)
    {
        string target = NormalizedCompare(product);
        return target.Length >= 4 && runningNames.Any(name =>
        {
            string candidate = NormalizedCompare(name);
            // Ex.: MPC-HC e mpc-hc64.exe (sem ler caminhos privados).
            candidate = Regex.Replace(candidate, @"(?:\s(?:64|32|X64|X86)|(?<=\p{L})(?:64|32))$", "");
            return candidate == target ||
                candidate.StartsWith(target + " ", StringComparison.OrdinalIgnoreCase);
        });
    }

    internal static IReadOnlyCollection<string> ReadRunningProcessNames(ResidualScanBudget? budget = null)
    {
        budget?.Source("Processes");
        var names = new HashSet<string>(Comparer);
        try
        {
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    if (budget is not null && !budget.CanContinue()) continue;
                    try { names.Add(process.ProcessName); }
                    catch (Exception ex) when (ex is InvalidOperationException or
                        System.ComponentModel.Win32Exception) { budget?.MarkIncomplete(); }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or
            System.ComponentModel.Win32Exception) { budget?.MarkIncomplete(); }
        return names;
    }

    // Outro sinal de aplicativo portatil presente: Windows App Paths pode
    // registrar um executavel mesmo sem entrada de desinstalacao. So proteger
    // quando o arquivo referenciado ainda existir. Somente leitura e sem
    // executar o binario; entradas antigas apontando para arquivos ausentes
    // nao sao consideradas instalacao.
    internal static IReadOnlyCollection<string> ReadRegisteredExecutableNames(ResidualScanBudget? budget = null)
    {
        budget?.Source("AppPaths");
        var names = new HashSet<string>(Comparer);
        foreach (var (hive, view) in new[]
        {
            (RegistryHive.CurrentUser, RegistryView.Default),
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32)
        })
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? paths = baseKey.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths", false);
                if (paths is null) continue;
                string[] executables = paths.GetSubKeyNames();
                if (executables.Length > 1200) budget?.MarkIncomplete(ResidualScanIssue.EntryLimit);
                foreach (string executable in executables.Take(1200))
                {
                    if (budget is not null && !budget.Visit()) break;
                    if (!executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        continue;
                    using RegistryKey? entry = paths.OpenSubKey(executable, false);
                    string rawPath = entry?.GetValue(null) as string ?? "";
                    if (rawPath.Length == 0 || rawPath.Length > 1024) continue;
                    string path = Environment.ExpandEnvironmentVariables(rawPath).Trim('"');
                    if (ResidualReadSafety.HasExistingLocalFile(path, budget))
                        names.Add(Path.GetFileNameWithoutExtension(executable));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or ArgumentException)
            { budget?.MarkIncomplete(); }
        }
        return names;
    }

    // Somente extrai uma pista de identidade. Nunca executa comando de Run/serviço,
    // expande shell, consulta rede ou trata o texto como uma receita de remoção.
    internal static string? ExecutableName(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || command.Length > 2048) return null;
        var match = Regex.Match(command.Trim(), "^\"([^\"]+\\.exe)\"(?:\\s|$)|^([^\"\\s]+\\.exe)(?:\\s|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        string path = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return ResidualReadSafety.IsLocalAbsolute(path) ? NormalizeName(Path.GetFileNameWithoutExtension(path)) : null;
    }

    private static void InspectIntegrations(List<BroadResidualEvidence> found,
        ref int entries, ref int restricted, ref bool incomplete, ResidualScanBudget budget)
    {
        budget.Source("RunServices");
        foreach (var (hive, view, label, subkey, services) in new[]
        {
            (RegistryHive.CurrentUser, RegistryView.Registry64, "HKEY_CURRENT_USER (64 bits)", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false),
            (RegistryHive.CurrentUser, RegistryView.Registry32, "HKEY_CURRENT_USER (32 bits)", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false),
            (RegistryHive.LocalMachine, RegistryView.Registry64, "HKEY_LOCAL_MACHINE (64 bits)", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false),
            (RegistryHive.LocalMachine, RegistryView.Registry32, "HKEY_LOCAL_MACHINE (32 bits)", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false),
            (RegistryHive.LocalMachine, RegistryView.Registry64, "HKEY_LOCAL_MACHINE", @"SYSTEM\CurrentControlSet\Services", true)
        })
        {
            if (!budget.CanContinue()) { incomplete = true; break; }
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(subkey, writable: false);
                if (key is null) continue;
                string[] names = services ? key.GetSubKeyNames() : key.GetValueNames();
                if (names.Length > 1200) { incomplete = true; budget.MarkIncomplete(ResidualScanIssue.EntryLimit); }
                foreach (string name in names.Take(1200))
                {
                    if (!budget.Visit()) { incomplete = true; break; }
                    entries++;
                    try
                    {
                        string raw;
                        if (services)
                        {
                            using var service = key.OpenSubKey(name, writable: false);
                            // Drivers (1/2) nunca entram na auditoria de aplicativos.
                            if (service?.GetValue("Type") is not int type || (type & 0x30) == 0 ||
                                (type & 0x03) != 0) continue;
                            raw = service.GetValue("ImagePath", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
                        }
                        else raw = key.GetValue(name, "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
                        string? product = ExecutableName(raw);
                        if (product is null) continue;
                        found.Add(new(product, label + @"\" + subkey + @"\" + name,
                            services ? "Serviço registrado — preservar" : "Inicialização registrada — preservar",
                            "Integration"));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                        System.Security.SecurityException) { restricted++; incomplete = true; budget.MarkIncomplete(); }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException) { restricted++; incomplete = true; budget.MarkIncomplete(); }
        }
    }

    // Classificação independente do ambiente: o mesmo algoritmo pode ser testado
    // em dados fictícios, sem acessar o Registro do usuário nem excluir nada.
    internal static ResidualReportSet Classify(
        IEnumerable<BroadResidualEvidence> raw,
        IReadOnlyCollection<ProgramEntry> installed,
        IReadOnlyCollection<string>? activeGameNames = null,
        IReadOnlyCollection<string>? runningProcessNames = null, bool inventoryIncomplete = false, ResidualCoverage? coverage = null, CancellationToken cancellationToken = default)
    {
        // Jogos Steam/Epic podem estar instalados sem entradas ARP. Nunca
        // classificar suas pastas ou preferencias como vestigios de remocao.
        var protectedGames = (activeGameNames ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizedCompare)
            .Where(name => name.Length >= 4)
            .ToHashSet(Comparer);
        var installationPaths = new ResidualPathIndex(installed.Select(p => p.InstallLocation));
        var protectedNames = ProtectionNames(installed); // normalizar só uma vez por análise
        var runningNames = (runningProcessNames ?? []).Select(NormalizedCompare)
            .Select(n => Regex.Replace(n, @"(?:\s(?:64|32|X64|X86)|(?<=\p{L})(?:64|32))$", ""))
            .Where(n => n.Length > 0).Distinct(Comparer).Order(StringComparer.Ordinal).ToArray();
        var input = new List<BroadResidualEvidence>();
        int? remaining = 0;
        var issues = inventoryIncomplete ? ResidualScanIssue.InventoryPartial : ResidualScanIssue.None;
        foreach (var item in raw)
        {
            if (cancellationToken.IsCancellationRequested || input.Count == 30000)
            {
                issues |= cancellationToken.IsCancellationRequested ? ResidualScanIssue.Cancelled : ResidualScanIssue.EntryLimit;
                remaining = raw.TryGetNonEnumeratedCount(out int total) ? total - input.Count : null;
                break;
            }
            input.Add(item);
        }
        var classificationClock = Stopwatch.StartNew();
        bool classificationStopped = false;
        int groupsExamined = 0;
        var classified = input.Where(e => EligibleName(e.Name))
            .GroupBy(e => NormalizedCompare(e.Name), Comparer)
            .TakeWhile(group =>
            {
                if (!cancellationToken.IsCancellationRequested && classificationClock.Elapsed < TimeSpan.FromSeconds(5))
                { groupsExamined++; return true; }
                issues |= cancellationToken.IsCancellationRequested ? ResidualScanIssue.Cancelled : ResidualScanIssue.Timeout;
                classificationStopped = true;
                return false;
            })
            .Where(group => !IsProtectedByNames(group.First().Name, protectedNames) &&
                !protectedGames.Contains(NormalizedCompare(group.First().Name)) &&
                !HasNameRelation(group.Key, runningNames) &&
                HasIndependentResidueEvidence(group))
            .Select(group =>
            {
                if (group.GroupBy(e => ResidualReportMerger.LocationKey(e.Location), Comparer)
                    .Any(g => g.Select(e => e.RootGroup).Distinct(Comparer).Count() > 1))
                    issues |= ResidualScanIssue.ContradictoryEvidence;
                var all = group.GroupBy(e => ResidualReportMerger.LocationKey(e.Location), Comparer)
                    .Select(g => g.OrderByDescending(e => e.RootGroup == "Integration")
                        .ThenByDescending(e => e.RootGroup == "AppData" || e.RootGroup == "ProgramData")
                        .ThenBy(e => e.Kind, StringComparer.Ordinal).First() with
                        { Kind = string.Join(" / ", g.Select(e => e.Kind).Distinct().Order(StringComparer.Ordinal)),
                          Sources = g.Aggregate(ResidualSource.None, (a, e) => a | SourceOf(e.RootGroup)) })
                    .ToArray();
                int sourceTypes = all.Select(e => e.RootGroup).Distinct(Comparer).Count();
                // Um diretório em Program Files pode ser uma instalação real,
                // inclusive portátil, sem UninstallString. Uma pasta com nome
                // de fabricante pode guardar saves de vários jogos diferentes.
                bool hasProgramFiles = all.Any(e => e.Sources.HasFlag(ResidualSource.Installation));
                bool activePath = all.Any(e => installationPaths.Overlaps(e.Location));
                bool ambiguous = group.Where(e => e.Container.Length > 0).Select(e => NormalizedCompare(e.Container)).Distinct(Comparer).Take(2).Count() > 1;
                bool sharedPublisher = IsSharedPublisherName(group.First().Name);
                string status = hasProgramFiles || activePath ? "Instalação não confirmada" :
                    sharedPublisher ? "Dados compartilhados de fabricante" :
                    "Vestígio não confirmado";
                string explanation = ambiguous ? "Nome associado a fabricantes distintos; vínculo ambíguo. Preservar todos os locais."
                    : activePath ? "Local sobreposto a uma instalação registrada sob outro nome ou versão. Preservar e conferir."
                    : hasProgramFiles
                    ? "Há diretório de instalação. O aplicativo pode continuar presente sem registro de desinstalação. Não apagar."
                    : sharedPublisher
                    ? "Nome de fabricante; pode conter configurações e jogos salvos de títulos ainda instalados. Não apagar."
                    : "Indícios em fontes diferentes; não comprovam desinstalação nem uso seguro para limpeza.";
                var findings = all.OrderBy(e => e.Location, Comparer)
                    .Select(e => new ResidualAuditFinding(e.Location,
                        status + " — " + e.Kind, 1,
                        explanation + (e.Container.Length > 0 ? " Local subordinado a " + e.Container + "; vínculo exclusivo não comprovado." : "") + (inventoryIncomplete ? " Inventário de proteção parcial; ausência não comprova remoção." : "") + " Somente leitura.",
                        inventoryIncomplete || ambiguous ? ResidualDisposition.Unknown :
                        hasProgramFiles || activePath || group.Key == "NETEASE" ? ResidualDisposition.InstallationCheck :
                        sharedPublisher || e.Sources.HasFlag(ResidualSource.Integration) ? ResidualDisposition.PreserveShared :
                        e.RootGroup is "AppData" or "ProgramData" or "Registry" ? ResidualDisposition.PreserveData : ResidualDisposition.Review,
                        e.Sources, ambiguous ? ResidualDisposition.PreserveShared : inventoryIncomplete
                            ? hasProgramFiles || activePath || group.Key == "NETEASE" ? ResidualDisposition.InstallationCheck
                            : sharedPublisher || e.Sources.HasFlag(ResidualSource.Integration) ? ResidualDisposition.PreserveShared
                            : e.RootGroup is "AppData" or "ProgramData" or "Registry" ? ResidualDisposition.PreserveData : ResidualDisposition.Review
                            : null)).ToArray();
                return new
                {
                    Group = group.Select(e => e.Name).OrderBy(n => n.Length).ThenBy(n => n, StringComparer.Ordinal).First(),
                    Sources = sourceTypes,
                    Total = all.Length,
                    Findings = findings
                };
            })
            .OrderByDescending(g => g.Sources)
            .ThenByDescending(g => g.Total)
            .ThenBy(g => g.Group, Comparer)
            .ToArray();
        coverage = ResidualCoverage.Combine(new[] { coverage, new ResidualCoverage(issues, EntriesExamined: coverage is null ? input.Count : 0,
            CandidatesFound: classified.Length, CandidatesNotEvaluated: classificationStopped ? null : remaining,
            CandidatesExamined: groupsExamined) });
        var reports = classified.Select(g => new ResidualAuditReport(g.Group, false,
            g.Findings.Select(f => coverage.Incomplete ? f with { BasisDisposition = f.BasisDisposition ?? f.Disposition, Disposition = ResidualDisposition.Unknown } : f).ToArray(),
            Array.Empty<string>(), Historical: true, Incomplete: coverage.Incomplete, Coverage: coverage)).ToArray();
        return new(reports, coverage);
    }

    internal static ResidualSource SourceOf(string group) => group switch
    {
        "AppData" => ResidualSource.UserData, "ProgramData" => ResidualSource.CommonData,
        "ProgramFiles" => ResidualSource.Installation, "Registry" => ResidualSource.Registry,
        "StartMenu" => ResidualSource.Shortcut, "Integration" => ResidualSource.Integration,
        _ => ResidualSource.None
    };

    internal static BroadResidualResult Inspect(IReadOnlyCollection<ProgramEntry> installed,
        IReadOnlyCollection<string>? activeGameNames = null, bool gameInventoryPartial = false,
        bool inventoryIncomplete = false, CancellationToken cancellationToken = default)
    {
        var budget = new ResidualScanBudget(cancellationToken);
        var found = new List<BroadResidualEvidence>();
        int entries = 0, restricted = 0, rootsChecked = 0;
        bool incomplete = false;
        foreach (var (path, category) in Roots())
        {
            if (!budget.CanContinue()) { incomplete = true; break; }
            rootsChecked++;
            string group = category.StartsWith("Atalhos", StringComparison.Ordinal) ?
                "StartMenu" : category.StartsWith("Arquivos de instalação", StringComparison.Ordinal) ?
                "ProgramFiles" : category == "Dados de aplicativos" ? "ProgramData" : "AppData";
            InspectDirectory(path, category, group, found, ref entries,
                ref restricted, ref incomplete, budget);
        }
        foreach (var (hive, view, identifier) in new[]
        {
            (RegistryHive.CurrentUser, RegistryView.Registry64, "HKEY_CURRENT_USER (64 bits)"),
            (RegistryHive.CurrentUser, RegistryView.Registry32, "HKEY_CURRENT_USER (32 bits)"),
            (RegistryHive.LocalMachine, RegistryView.Registry64, "HKEY_LOCAL_MACHINE"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, "HKEY_LOCAL_MACHINE (32 bits)")
        })
        {
            if (!budget.CanContinue()) { incomplete = true; break; }
            rootsChecked++;
            InspectRegistryHive(hive, view, identifier, found, ref entries,
                ref restricted, ref incomplete, budget);
        }
        InspectIntegrations(found, ref entries, ref restricted, ref incomplete, budget);
        var liveEvidence = ReadRunningProcessNames(budget)
            .Concat(ReadRegisteredExecutableNames(budget))
            .Distinct(Comparer).ToArray();
        if (incomplete && !budget.Incomplete) budget.MarkIncomplete();
        if (incomplete || budget.Incomplete)
        {
            foreach (string source in new[] { "AppData", "ProgramData", "ProgramFiles", "StartMenu",
                "HKEY_CURRENT_USER (64 bits)", "HKEY_CURRENT_USER (32 bits)", "HKEY_LOCAL_MACHINE",
                "HKEY_LOCAL_MACHINE (32 bits)", "RunServices", "Processes", "AppPaths" })
                if (!budget.SourcesConsulted.Contains(source)) budget.SourcesUnavailable.Add(source);
        }
        var scanCoverage = budget.Coverage with { CandidatesNotEvaluated = incomplete || budget.Incomplete ? null : 0 };
        var reports = Classify(found, installed, activeGameNames, liveEvidence,
            inventoryIncomplete || gameInventoryPartial, scanCoverage, cancellationToken);
        return new(reports, rootsChecked, entries, restricted, reports.Incomplete, reports.Coverage);
    }
}
