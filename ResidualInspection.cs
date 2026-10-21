using Microsoft.Win32;

namespace GerenciadorAplicativos;

internal sealed record ResidualInspectionEntry(string Name, string Location, bool Container,
    ResidualDisposition Disposition, bool CanInspect = false)
{
    internal ResidualPreservationKind PreservationKind => ResidualPreservationHints.Kind(Name, Container);
    internal string PreservationExplanation => ResidualPreservationHints.Explain(PreservationKind);
}

internal enum ResidualPreservationKind
{
    Unknown, BrowserHistoryAndBookmarks, BrowserCredentials, BrowserCookies,
    BrowserSession, BrowserProfile, Cache, Backup, Playlist, SavedGames, Installer
}

// Pistas locais de finalidade, não regras de descoberta ou de autorização.
// Só nomes de metadados já enumerados; não abre bases, perfis ou arquivos.
// Implementação própria; procedência/documentação no relatório consolidado.
internal static class ResidualPreservationHints
{
    internal static ResidualPreservationKind Kind(string name, bool container)
    {
        if (container)
            return name.ToUpperInvariant() switch
            {
                "BOOKMARKBACKUPS" => ResidualPreservationKind.BrowserHistoryAndBookmarks,
                "SESSIONSTORE-BACKUPS" or "SESSIONS" => ResidualPreservationKind.BrowserSession,
                "PROFILES" or "USER DATA" => ResidualPreservationKind.BrowserProfile,
                "CACHE" or "CACHES" or "CACHE2" or "GPUCACHE" or "CODE CACHE" or "CACHESTORAGE"
                    => ResidualPreservationKind.Cache,
                "BACKUP" or "BACKUPS" or "MOBILESYNC" => ResidualPreservationKind.Backup,
                "PLAYLISTS" => ResidualPreservationKind.Playlist,
                "SAVED GAMES" or "SAVEGAMES" or "SAVES" => ResidualPreservationKind.SavedGames,
                "INSTALLER" or "PACKAGE CACHE" or "MSOCACHE" => ResidualPreservationKind.Installer,
                _ => ResidualPreservationKind.Unknown
            };
        var known = name.ToUpperInvariant() switch
        {
            "PLACES.SQLITE" or "FAVICONS.SQLITE" or "BOOKMARKS" or "HISTORY"
                => ResidualPreservationKind.BrowserHistoryAndBookmarks,
            "LOGINS.JSON" or "KEY4.DB" or "LOGIN DATA" => ResidualPreservationKind.BrowserCredentials,
            "COOKIES.SQLITE" or "COOKIES" => ResidualPreservationKind.BrowserCookies,
            "SESSIONSTORE.JSONLZ4" => ResidualPreservationKind.BrowserSession,
            "PROFILES.INI" or "INSTALLS.INI" => ResidualPreservationKind.BrowserProfile,
            _ => ResidualPreservationKind.Unknown
        };
        if (known != ResidualPreservationKind.Unknown) return known;
        return Path.GetExtension(name).ToUpperInvariant() switch
        {
            ".M3U" or ".M3U8" or ".PLS" or ".FPL" or ".XSPF" => ResidualPreservationKind.Playlist,
            ".MSI" or ".MSP" => ResidualPreservationKind.Installer,
            _ => ResidualPreservationKind.Unknown
        };
    }

    internal static string Explain(ResidualPreservationKind kind) => kind switch
    {
        ResidualPreservationKind.BrowserHistoryAndBookmarks => "Pelo nome, pode integrar favoritos, histórico ou seus backups de navegador. Preservar esses dados pessoais.",
        ResidualPreservationKind.BrowserCredentials => "Pelo nome, pode guardar senhas salvas ou chaves usadas para protegê-las. Preservar; conteúdo não consultado.",
        ResidualPreservationKind.BrowserCookies => "Pelo nome, pode guardar cookies, sessões autenticadas e preferências de sites. Preservar.",
        ResidualPreservationKind.BrowserSession => "Pelo nome, pode guardar abas e janelas abertas ou dados para restaurar a sessão. Preservar.",
        ResidualPreservationKind.BrowserProfile => "Pelo nome, pode guardar ou indicar perfis de navegador com favoritos, senhas e configurações. Podem continuar úteis após desinstalar; preservar.",
        ResidualPreservationKind.Cache => "Pelo nome, pode ser cache ainda usado por um aplicativo. Isso não comprova abandono nem espaço recuperável; preservar nesta investigação.",
        ResidualPreservationKind.Backup => "Pelo nome, pode conter backups de aplicativos ou dispositivos, necessários para recuperar dados. Preservar.",
        ResidualPreservationKind.Playlist => "Pelo nome, pode guardar playlists ou organização da biblioteca de mídia. Preservar.",
        ResidualPreservationKind.SavedGames => "Pelo nome, pode guardar progresso e configurações de jogos. Preservar mesmo se o jogo não aparecer no inventário.",
        ResidualPreservationKind.Installer => "Pelo nome, pode ser fonte de instalação ou reparo, necessária para atualizar, desinstalar ou reverter um aplicativo. Preservar.",
        _ => "Finalidade não confirmada pelos metadados. Pode conter dados pessoais ou compartilhados; preservar."
    };
}
internal sealed record ResidualInspectionResult(string Location, IReadOnlyList<ResidualInspectionEntry> Entries,
    ResidualCoverage Coverage, string Explanation)
{
    internal bool CanExecute => false;
}

// Sessão somente leitura. Alvos vêm da fotografia exibida ou dos filhos nela
// descobertos, nunca do texto de busca. Não lê conteúdo de arquivos/valores.
internal sealed class ResidualInspectionSession
{
    private const int MaximumTargets = 4096;
    private readonly Dictionary<string, int> targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] roots;
    private readonly int maximumTargets;
    internal ResidualInspectionSession(ResidualAuditReport report, IReadOnlyList<string>? allowedRoots = null)
    {
        roots = (allowedRoots ?? BroadResidualDiscovery.Roots().Select(r => r.Path).ToArray()).ToArray();
        maximumTargets = report.Findings.Count + MaximumTargets;
        foreach (var finding in report.Findings) targets.TryAdd(finding.Location, 0);
    }
    internal ResidualInspectionResult Inspect(string location, CancellationToken token = default,
        int maximumEntries = 512, TimeSpan? timeout = null)
    {
        using var measurement = LocalPerformanceMetrics.Measure(PerformanceStage.ResidualInspection);
        var budget = new ResidualScanBudget(token, timeout ?? TimeSpan.FromSeconds(5), Math.Clamp(maximumEntries, 1, 2048));
        budget.Source("Inspection");
        var found = new List<ResidualInspectionEntry>();
        if (!targets.TryGetValue(location, out int depth))
        { budget.MarkIncomplete(ResidualScanIssue.SourceUnavailable); return Result("Local fora da fotografia desta revisão."); }
        if (depth >= 2)
        { budget.MarkIncomplete(ResidualScanIssue.DepthLimit); return Result("Limite de profundidade desta inspeção. Preserve e faça nova investigação específica."); }
        if (!budget.CanContinue()) return Result("Inspeção interrompida antes de ler o local.");
        try
        {
            string canonical = ResidualReportMerger.LocationKey(location);
            if (canonical.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase)) ReadRegistry(canonical);
            else ReadDirectory();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException or
            ArgumentException or NotSupportedException or InvalidOperationException)
        { budget.MarkIncomplete(); }
        return Result(budget.Incomplete ? "Inspeção parcial. Alguns itens não puderam ser examinados; preservar." :
            "Fotografia do nível consultado. Conteúdo de arquivos e valores do Registro não foi lido. Nenhum vínculo exclusivo comprovado.");

        ResidualInspectionResult Result(string explanation) => new(location, found.AsReadOnly(),
            budget.Coverage with { CandidatesFound = found.Count, CandidatesNotEvaluated = budget.Incomplete ? null : 0 }, explanation);
        void Register(string name, string path, bool container)
        {
            bool navigable = container && depth + 1 < 2 && targets.Count < maximumTargets;
            found.Add(new(name, path, container, ResidualDisposition.PreserveData, navigable));
            if (container && targets.Count < maximumTargets) targets.TryAdd(path, depth + 1);
            else if (container) budget.MarkIncomplete(ResidualScanIssue.EntryLimit);
        }
        void ReadDirectory()
        {
            if (!ResidualReadSafety.IsLocalAbsolute(location) || !roots.Any(root => UnderRoot(location, root)))
            { budget.MarkIncomplete(ResidualScanIssue.SourceUnavailable); return; }
            if (!Directory.Exists(location))
            {
                if (ResidualReadSafety.HasExistingLocalFile(location, budget))
                    found.Add(new(Path.GetFileName(location), location, false, ResidualDisposition.PreserveData));
                else budget.MarkIncomplete(); // alvo da fotografia desapareceu/ficou ilegível
                return;
            }
            if (!ResidualReadSafety.IsUnlinkedDirectory(location))
            { budget.MarkIncomplete(ResidualScanIssue.LinkSkipped); return; }
            // No Windows, prende ancestrais contra renomeação durante a leitura.
            using var lease = OperatingSystem.IsWindows() ? InstallerFileGuard.PinDirectory(location, allowLeafWrites: true) : null;
            foreach (var item in new DirectoryInfo(location).EnumerateFileSystemInfos())
            {
                if (!budget.Visit()) break;
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                { budget.MarkIncomplete(ResidualScanIssue.LinkSkipped); continue; }
                Register(item.Name, item.FullName, (item.Attributes & FileAttributes.Directory) != 0);
            }
        }
        void ReadRegistry(string path)
        {
            if (!OperatingSystem.IsWindows()) { budget.MarkIncomplete(ResidualScanIssue.SourceUnavailable); return; }
            foreach (var (prefix, hive, view) in new[]
            {
                (@"HKEY_CURRENT_USER (64 bits)\", RegistryHive.CurrentUser, RegistryView.Registry64),
                (@"HKEY_CURRENT_USER (32 bits)\", RegistryHive.CurrentUser, RegistryView.Registry32),
                (@"HKEY_LOCAL_MACHINE (64 bits)\", RegistryHive.LocalMachine, RegistryView.Registry64),
                (@"HKEY_LOCAL_MACHINE (32 bits)\", RegistryHive.LocalMachine, RegistryView.Registry32)
            })
            {
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string subkey = path[prefix.Length..];
                if (subkey.Split('\\').Any(p => p is "" or "." or "..")) { budget.MarkIncomplete(); return; }
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(subkey, writable: false);
                if (key is null) { budget.MarkIncomplete(); return; }
                // GetSubKeyNames aloca a lista toda; recusar chaves enormes antes da alocação.
                if (key.SubKeyCount > 2048) { budget.MarkIncomplete(ResidualScanIssue.EntryLimit); return; }
                foreach (string name in key.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase))
                {
                    if (!budget.Visit()) break;
                    Register(name, path + @"\" + name, true);
                }
                return;
            }
            budget.MarkIncomplete(ResidualScanIssue.SourceUnavailable);
        }
    }
    internal static bool UnderRoot(string path, string root)
    {
        if (!ResidualReadSafety.IsLocalAbsolute(path) || !ResidualReadSafety.IsLocalAbsolute(root)) return false;
        string canonical = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        string trusted = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        return canonical.StartsWith(trusted + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
