namespace GerenciadorAplicativos;

// Impede que componentes protegidos sejam tratados como desinstaláveis mesmo
// quando o Registro Win32 não define SystemComponent=1.
internal static class ProtectedApplications
{
    internal static bool IsProtectedName(string? name) =>
        string.Equals(name?.Trim(), "Microsoft GameInput", StringComparison.OrdinalIgnoreCase);

    // Exclusão expressa do mantenedor: somente inventário, sem consulta ou ações.
    // Não representa componente do Windows nem autoriza exceções de segurança.
    internal static bool IsExcludedFromManagement(string? name)
    {
        string value = name?.Trim() ?? "";
        return value.Equals("VLC", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("VLC ", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("VLC(", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("VideoLAN.VLC", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsInventoryOnly(string? name, bool systemComponent) =>
        systemComponent || IsProtectedName(name) || IsExcludedFromManagement(name);
}
