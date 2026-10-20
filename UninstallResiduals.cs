namespace GerenciadorAplicativos;

// Inspeção POS-desinstalação. Jamais varre o disco inteiro ou remove configurações,
// documentos, pastas compartilhadas e chaves de Registro de forma heurística.
// Só autoriza arquivos executáveis de nome EXATO em diretorios de programas conhecidos.
internal sealed record ResidualCandidate(string Product, string Root, IReadOnlyList<string> Files, int Preserved);
internal sealed record ResidualCleanupResult(int RemovedFiles, int RemovedDirectories, int Failed, int Preserved);

internal static class UninstallResiduals
{
    private sealed record Policy(string Product, string[] KnownFileNames, string[] Roots);
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private static Policy? GetPolicy(ProgramEntry entry)
    {
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (entry.Name.Equals("SumatraPDF", StringComparison.OrdinalIgnoreCase) &&
            (entry.Publisher.Equals("Krzysztof Kowalczyk", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("SumatraPDF", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("SumatraPDF Reader", StringComparison.OrdinalIgnoreCase)))
            return new("SumatraPDF",
                ["SumatraPDF.exe", "uninstall.exe", "unins000.exe", "unins000.dat"],
                [Path.Combine(pf, "SumatraPDF"),
                 Path.Combine(pf86, "SumatraPDF"),
                 Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SumatraPDF")]);

        if ((entry.Name.Equals("Notepad++ (64-bit x64)", StringComparison.OrdinalIgnoreCase) ||
             entry.Name.Equals("Notepad++ (x64)", StringComparison.OrdinalIgnoreCase) ||
             entry.Name.Equals("Notepad++ (32-bit x86)", StringComparison.OrdinalIgnoreCase)) &&
            (entry.Publisher.Equals("Notepad++ Team", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("Don HO", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("Notepad++", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("Notepad Team", StringComparison.OrdinalIgnoreCase)))
            return new("Notepad++", ["notepad++.exe", "uninstall.exe", "unins000.exe", "unins000.dat"],
                [Path.Combine(pf, "Notepad++"), Path.Combine(pf86, "Notepad++")]);
        return null;
    }

    internal static bool IsSupported(ProgramEntry entry) => GetPolicy(entry) is not null;

    // Se mesmo aplicativo ainda aparece no Registro não apagar nada.
    internal static bool StillInstalled(ProgramEntry original, IReadOnlyCollection<ProgramEntry> installed)
    {
        if (installed.Any(p =>
            (!string.IsNullOrWhiteSpace(original.MsiProductCode) &&
                original.MsiProductCode.Equals(p.MsiProductCode, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(original.InventoryId) &&
                original.InventoryId.Equals(p.InventoryId, StringComparison.OrdinalIgnoreCase)) ||
            ResidualReadSafety.PathsOverlap(original.InstallLocation, p.InstallLocation))) return true;
        // Mesmo ID em todas as etapas: se uma variante continua instalada,
        // o software NÃO está removido e nenhum resíduo pode ser apagado.
        var identity = ApplicationIdentityCatalog.Resolve(original.Name, original.Publisher);
        if (identity is not null &&
            (ApplicationIdentityCatalog.IsStillInstalled(original.Name, original.Publisher,
                installed.Select(p => (p.Name, p.Publisher))) ||
             installed.Any(p => identity.Names.Any(n =>
                 ApplicationIdentityCatalog.NameMatches(p.Name, n)))))
            return true;
        // Fallback conservador para instalações não classificadas.
        if (original.Name.StartsWith("Notepad++", StringComparison.OrdinalIgnoreCase))
            return installed.Any(p => p.Name.StartsWith("Notepad++", StringComparison.OrdinalIgnoreCase));
        if (original.Name.Equals("SumatraPDF", StringComparison.OrdinalIgnoreCase))
            return installed.Any(p => p.Name.Equals("SumatraPDF", StringComparison.OrdinalIgnoreCase));
        return installed.Any(p => p.Name.Equals(original.Name, StringComparison.OrdinalIgnoreCase));
    }

    internal static IReadOnlyList<(ProgramEntry App, IReadOnlyList<ResidualCandidate> Files)> FindPreviouslyUninstalled(
        IReadOnlyCollection<ProgramEntry> installed)
    {
        ProgramEntry[] known =
        [
            new() { Name = "SumatraPDF", Publisher = "Krzysztof Kowalczyk" },
            new() { Name = "Notepad++ (64-bit x64)", Publisher = "Notepad++ Team" }
        ];
        return known.Where(p => !StillInstalled(p, installed))
            .Select(p => (App: p, Files: Inspect(p)))
            .Where(x => x.Files.Count > 0).ToArray();
    }

    internal static IReadOnlyList<ResidualCandidate> Inspect(ProgramEntry entry)
    {
        var policy = GetPolicy(entry);
        if (policy is null) return [];
        var candidates = new List<ResidualCandidate>();
        foreach (string root in policy.Roots.Distinct(PathComparer))
        {
            ResidualCandidate? result = InspectDirectory(policy.Product, root, policy.KnownFileNames);
            if (result is { Files.Count: > 0 }) candidates.Add(result);
        }
        return candidates;
    }

    private static bool SafeRoot(string root)
    {
        try
        {
            if (!Path.IsPathFullyQualified(root) || !Directory.Exists(root)) return false;
            var dir = new DirectoryInfo(root);
            if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            // Impede navegar por junções nos pais de instalações ou perfis.
            for (DirectoryInfo? cursor = dir.Parent; cursor is not null; cursor = cursor.Parent)
                if ((cursor.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return false; }
    }

    // Usado também pelos testes com raiz temporária. Em produção, a raiz só pode
    // vir da lista fixa GetPolicy, não de caminho fornecido pelo usuário/Registro.
    internal static ResidualCandidate? InspectDirectory(string product, string root,
        IReadOnlyCollection<string> allowedNames)
    {
        if (!SafeRoot(root)) return null;
        try
        {
            var files = new List<string>();
            int preserved = 0;
            int visited = 0;
            foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.TopDirectoryOnly))
            {
                if (++visited > 1000) return null; // Pasta estranha: nenhum apagamento.
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                    (attributes & FileAttributes.Directory) != 0)
                {
                    preserved++;
                    continue;
                }
                if (allowedNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                    files.Add(path);
                else preserved++; // Documentos, preferências, plugins e arquivos desconhecidos.
            }
            return files.Count == 0 ? null : new ResidualCandidate(product, root, files, preserved);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return null; }
    }

    // Candidata de auditoria: nomes exatos e nova enumeração não provam
    // propriedade exclusiva nem preservam o consentimento sobre o conteúdo.
    // Nenhuma chamada interna pode contornar o bloqueio de exclusão.
    internal static ResidualCleanupResult Clean(ProgramEntry original, ResidualCandidate candidate) =>
        new(0, 0, 1, candidate.Preserved + candidate.Files.Count);

    internal static ResidualCleanupResult CleanCandidateFiles(
        ResidualCandidate refreshed, IReadOnlyCollection<string> allowedNames) =>
        new(0, 0, 1, refreshed.Preserved + refreshed.Files.Count);
}
