namespace GerenciadorAplicativos;

// Seleciona apenas um volume com letra de unidade. Nao recebe argumentos de
// comando, UNC, pontos de montagem ou entradas livres do usuario.
internal static class SingleVolumeOptimization
{
    private const string Prefix = "optimize-volume:";

    internal static string? TaskForRoot(string? root)
    {
        if (root is not { Length: 3 } || root[1] != ':' || root[2] != '\\' ||
            !IsLetter(char.ToUpperInvariant(root[0]))) return null;
        return Prefix + char.ToUpperInvariant(root[0]);
    }

    internal static bool TryParseTask(string? task, out string volume)
    {
        volume = "";
        if (task is null || task.Length != Prefix.Length + 1 ||
            !task.StartsWith(Prefix, StringComparison.Ordinal) ||
            !IsLetter(task[^1])) return false;
        volume = task[^1] + ":";
        return true;
    }

    internal static bool IsEligible(DriveType type, bool ready, string? format) =>
        type == DriveType.Fixed && ready &&
        (string.Equals(format, "NTFS", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(format, "ReFS", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(format, "FAT", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(format, "FAT32", StringComparison.OrdinalIgnoreCase));

    private static bool IsLetter(char c) => c is >= 'A' and <= 'Z';
}
