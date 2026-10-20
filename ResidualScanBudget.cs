using System.Diagnostics;

namespace GerenciadorAplicativos;

// Orçamento cooperativo compartilhado por todas as fontes. Não interrompe uma
// chamada nativa bloqueada; interrompe antes do próximo item/abertura.
internal sealed class ResidualScanBudget
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly CancellationToken token;
    private readonly TimeSpan timeout;
    private readonly int maxEntries;
    internal int Entries { get; private set; }
    internal bool Incomplete { get; private set; }
    internal ResidualScanBudget(CancellationToken token = default,
        TimeSpan? timeout = null, int maxEntries = 30000)
    {
        this.token = token;
        this.timeout = timeout ?? TimeSpan.FromSeconds(14);
        this.maxEntries = Math.Max(0, maxEntries);
    }
    internal bool CanContinue()
    {
        if (token.IsCancellationRequested || clock.Elapsed >= timeout || Entries >= maxEntries)
        { Incomplete = true; return false; }
        return true;
    }
    internal bool Visit()
    {
        if (!CanContinue()) return false;
        Entries++;
        return true;
    }
    internal void MarkIncomplete() => Incomplete = true;
}

internal static class ResidualReadSafety
{
    // Somente caminhos locais absolutos; sem dispositivos, ADS ou traversal.
    internal static bool IsLocalAbsolute(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('\0') || path.Split('\\', '/').Any(p => p is "." or "..")) return false;
        return !OperatingSystem.IsWindows() ||
            (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' &&
             path[2] == '\\' && !path[2..].Contains(':'));
    }
    internal static bool IsUnlinkedDirectory(string path)
    {
        if (!IsLocalAbsolute(path)) return false;
        try
        {
            for (DirectoryInfo? dir = new(path); dir is not null; dir = dir.Parent)
                if (!dir.Exists || (dir.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException) { return false; }
    }
}
