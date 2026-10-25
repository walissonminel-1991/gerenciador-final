namespace GerenciadorAplicativos;

internal static class WindowsExecutionFailure
{
    // ERROR_VIRUS_INFECTED / ERROR_VIRUS_DELETED são relatos do Windows.
    // Erro 5/32, WSC ou assinatura ausente não identificam antivírus nem fabricante.
    internal static bool IsAntivirus(int code) => code is 225 or 226;
    internal static string? Describe(int code) => IsAntivirus(code)
        ? $"O Windows informou bloqueio por antivírus (código {code}). Consulte o alerta do antivírus; o GF não restaura arquivos da quarentena nem altera suas proteções."
        : null;
}
