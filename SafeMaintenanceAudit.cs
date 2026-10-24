using System.Globalization;

namespace GerenciadorAplicativos;

// Somente categorias fixas e contagens autorizadas, jamais texto livre do worker.
internal static class SafeMaintenanceAudit
{
    private static string? Category(string task) => task switch
    {
        "clean-user-temp" => "temporarios_usuario",
        "clean-directx-cache" => "cache_directx",
        "clean-windows-temp" => "temporarios_windows",
        "clear-update-cache" => "cache_windows_update",
        "cleanmgr-auto" => "limpeza_disco_windows",
        _ => null
    };

    internal static string? Begin(string task) =>
        Category(task) is { } type ? "LIMPEZA.INICIO tipo=" + type : null;

    internal static string? Cancelled(string task) =>
        Category(task) is { } type ? "LIMPEZA.CANCELADA tipo=" + type : null;

    internal static string? MissingSource(string task) =>
        Category(task) is { } type ? "LIMPEZA.SEM_ORIGEM tipo=" + type : null;

    internal static string? NoReturn(string task) =>
        Category(task) is { } type ? "LIMPEZA.SEM_RETORNO tipo=" + type : null;

    internal static string? LocalResult(string task, int files, int folders,
        int kept, int links, int failures)
    {
        string? type = Category(task);
        if (type is not ("temporarios_usuario" or "cache_directx" or "temporarios_windows" or "cache_windows_update") ||
            new[] { files, folders, kept, links, failures }.Any(n => n is < 0 or > 100_000_000))
            return null;
        return "LIMPEZA.RESUMO tipo=" + type +
            " arquivos=" + files.ToString(CultureInfo.InvariantCulture) +
            " pastas=" + folders.ToString(CultureInfo.InvariantCulture) +
            " preservados=" + kept.ToString(CultureInfo.InvariantCulture) +
            " links=" + links.ToString(CultureInfo.InvariantCulture) +
            " falhas=" + failures.ToString(CultureInfo.InvariantCulture);
    }

    // Codigo zero representa o retorno do processo, nao bytes recuperados.
    internal static string? ElevatedExit(string task, string code)
    {
        string? type = Category(task);
        if (type is null || !int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ||
            n is < 0 or > 65535) return null;
        return "LIMPEZA.PROCESSO_RESULTADO tipo=" + type + " codigo=" + n.ToString(CultureInfo.InvariantCulture);
    }
}
