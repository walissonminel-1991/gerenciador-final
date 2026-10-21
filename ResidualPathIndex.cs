namespace GerenciadorAplicativos;

// Índice de instalações: fronteira de diretório, sem busca quadrática por candidato.
internal sealed class ResidualPathIndex
{
    private readonly string[] paths;
    private readonly HashSet<string> exact;
    internal ResidualPathIndex(IEnumerable<string> installedPaths)
    {
        paths = installedPaths.Where(ResidualReadSafety.IsLocalAbsolute).Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        exact = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
    internal bool Overlaps(string path)
    {
        if (!ResidualReadSafety.IsLocalAbsolute(path)) return false;
        string value = Normalize(path);
        if (exact.Contains(value)) return true;
        string prefix = value + Path.DirectorySeparatorChar;
        int index = Array.BinarySearch(paths, prefix, StringComparer.OrdinalIgnoreCase);
        if (index >= 0) return true;
        index = ~index;
        if (index < paths.Length && paths[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        for (int end = value.LastIndexOf(Path.DirectorySeparatorChar); end > 0; end = value.LastIndexOf(Path.DirectorySeparatorChar, end - 1))
            if (exact.Contains(value[..end])) return true;
        return false;
    }
}
