using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// Consulta agregada do Windows Security Center (WSC). Nao e um scanner nem
// identifica o fabricante; nao gerencia antivirus, firewall ou excecoes.
internal enum SecurityHealthLevel { Ok, Attention, Unknown }
internal sealed record SecurityHealthResult(
    string Category, SecurityHealthLevel Level, string Description);

internal static class SecurityCenterHealth
{
    private const int S_OK = 0;
    private const int Antivirus = 0x4;
    private const int Firewall = 0x1;

    [DllImport("wscapi.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WscGetSecurityProviderHealth(int providers, out int health);

    // HRESULT S_FALSE (1) indica servico WSC parado. Nunca converter esse
    // resultado em "antivirus desativado": nao houve uma leitura confiavel.
    internal static SecurityHealthResult Interpret(string category, int hresult, int health)
    {
        if (hresult != S_OK)
            return new(category, SecurityHealthLevel.Unknown,
                "Estado indisponivel no Centro de Seguranca do Windows " +
                $"(codigo 0x{unchecked((uint)hresult):X8}). Consulte a Seguranca do Windows.");
        return health switch
        {
            0 => new(category, SecurityHealthLevel.Ok,
                "O Windows informa protecao ativa nesta categoria. Isso nao comprova ausencia de malware."),
            1 => new(category, SecurityHealthLevel.Unknown,
                "Esta categoria nao esta sendo monitorada pelo Centro de Seguranca do Windows."),
            2 => new(category, SecurityHealthLevel.Attention,
                "O Windows sinaliza que esta categoria precisa de atencao. Verifique o antivirus/firewall."),
            3 => new(category, SecurityHealthLevel.Attention,
                "O Windows informa protecao temporariamente suspensa nesta categoria."),
            _ => new(category, SecurityHealthLevel.Unknown,
                "O Windows retornou um estado nao reconhecido; consulte a Seguranca do Windows.")
        };
    }

    private static SecurityHealthResult Read(int category, string name)
    {
        if (!OperatingSystem.IsWindows())
            return new(name, SecurityHealthLevel.Unknown, "Consulta disponivel somente no Windows.");
        try
        {
            int hr = WscGetSecurityProviderHealth(category, out int health);
            return Interpret(name, hr, health);
        }
        catch (Exception ex) when (ex is DllNotFoundException or
            EntryPointNotFoundException or BadImageFormatException or
            UnauthorizedAccessException or COMException)
        {
            return new(name, SecurityHealthLevel.Unknown,
                "Centro de Seguranca indisponivel (" + ex.GetType().Name + ").");
        }
    }

    public static IReadOnlyList<SecurityHealthResult> Capture() =>
    [
        Read(Antivirus, "Protecao antivirus"),
        Read(Firewall, "Firewall do Windows")
    ];
}
