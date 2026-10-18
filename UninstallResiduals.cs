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
        // Também evita apagar uma segunda arquitetura ou variante ainda instalada.
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

    internal static ResidualCleanupResult Clean(ProgramEntry original, ResidualCandidate candidate)
    {
        var policy = GetPolicy(original);
        if (policy is null || !policy.Product.Equals(candidate.Product, StringComparison.Ordinal) ||
            !policy.Roots.Any(root => PathComparer.Equals(root, candidate.Root)))
            return new(0, 0, 1, 0);
        // Voltar ao disco agora; nunca confiar em caminhos capturados antes do dialogo.
        ResidualCandidate? refreshed = InspectDirectory(policy.Product, candidate.Root, policy.KnownFileNames);
        if (refreshed is null) return new(0, 0, 0, 0);
        return CleanCandidateFiles(refreshed, policy.KnownFileNames);
    }

    // Testável com pasta TEMP, mas a chamada de produção Clean() exige a política
    // e os diretórios fixos previamente aprovados.
    internal static ResidualCleanupResult CleanCandidateFiles(
        ResidualCandidate refreshed, IReadOnlyCollection<string> allowedNames)
    {
        if (!SafeRoot(refreshed.Root)) return new(0, 0, 1, 0);
        var recheck = InspectDirectory(refreshed.Product, refreshed.Root, allowedNames);
        if (recheck is null) return new(0, 0, 0, 0);
        int removed = 0, errors = 0, directories = 0;
        foreach (string file in recheck.Files)
        {
            try
            {
                if (!SafeRoot(recheck.Root) ||
                    !PathComparer.Equals(Path.GetDirectoryName(file), recheck.Root) ||
                    !allowedNames.Contains(Path.GetFileName(file), PathComparer))
                { errors++; continue; }
                if ((File.GetAttributes(file) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                { errors++; continue; }
                File.Delete(file);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { errors++; }
        }
        try
        {
            if (SafeRoot(recheck.Root) && !Directory.EnumerateFileSystemEntries(recheck.Root).Any())
            {
                Directory.Delete(recheck.Root);
                directories++;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { errors++; }
        return new(removed, directories, errors, recheck.Preserved);
    }
}
