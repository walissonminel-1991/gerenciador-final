namespace GerenciadorAplicativos;

// Impede que componentes protegidos sejam tratados como desinstaláveis mesmo
// quando o Registro Win32 não define SystemComponent=1.
internal static class ProtectedApplications
{
    internal static bool IsProtectedName(string? name) =>
        string.Equals(name?.Trim(), "Microsoft GameInput", StringComparison.OrdinalIgnoreCase);

    internal static bool IsInventoryOnly(string? name, bool systemComponent) =>
        systemComponent || IsProtectedName(name);
}
