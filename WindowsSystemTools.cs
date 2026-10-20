namespace GerenciadorAplicativos;

// Comandos internos fixos: nunca procurar no diretorio do portatil nem no PATH,
// especialmente quando o trabalhador roda elevado. Nao aceita caminhos ou argumentos.
internal static class WindowsSystemTools
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "powercfg.exe", "net.exe", "cleanmgr.exe", "dism.exe", "sfc.exe",
        "ipconfig.exe", "netsh.exe", "defrag.exe", "taskmgr.exe"
    };

    internal static string Resolve(string name)
    {
        if (!Allowed.Contains(name))
            throw new ArgumentException("Ferramenta do Windows nao autorizada.", nameof(name));
        string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (string.IsNullOrWhiteSpace(system) || !Path.IsPathFullyQualified(system))
            throw new InvalidOperationException("Pasta de sistema do Windows indisponivel.");
        return Path.Combine(system, name.ToLowerInvariant());
    }
}
