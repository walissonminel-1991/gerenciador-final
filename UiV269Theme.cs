using System.Drawing;

namespace GerenciadorAplicativos;

/// <summary>
/// Identidades dos temas aprovados no protótipo V2.6.9.
/// Não contém lógica de manutenção ou de elegibilidade de operações.
/// </summary>
internal enum V269Theme
{
    AzulGf,
    Dark,
    Claro
}

internal enum V269ActionKind
{
    Atualizar,
    Remover
}

/// <summary>Tokens nativos de aparência. Não derivar autorização da cor.</summary>
internal sealed record V269Palette(
    Color Background,
    Color Sidebar,
    Color Content,
    Color RightPanel,
    Color Card,
    Color Border,
    Color Text,
    Color SecondaryText,
    Color Selection,
    Color Accent,
    Color DisabledAction,
    Color EnabledUpdate,
    Color EnabledRemove);

internal static class V269VisualTokens
{
    internal const int MinimumInterfacePercent = 90;
    internal const int MaximumInterfacePercent = 110;
    internal const int MinimumTextPercent = 90;
    internal const int MaximumTextPercent = 115;

    private static Color Hex(string html) => ColorTranslator.FromHtml(html);

    // Paleta derivada da V2.6.9: azul GF vivo, mas sem saturar o texto.
    private static readonly V269Palette Azul = new(
        Hex("#06111e"), Hex("#091a2a"), Hex("#091d30"), Hex("#0e2334"),
        Hex("#0b2033"), Hex("#25445f"), Hex("#e4edf6"), Hex("#9ab0c2"),
        Hex("#15578d"), Hex("#2d9cdb"), Hex("#abcfe6"),
        Hex("#49ce81"), Hex("#f36c82"));

    // Dark deliberadamente neutro: preto/grafite, texto suavizado.
    private static readonly V269Palette Grafite = new(
        Hex("#14181d"), Hex("#1f252c"), Hex("#1a2128"), Hex("#252d35"),
        Hex("#242c34"), Hex("#4c5b67"), Hex("#d5dee6"), Hex("#aebbc7"),
        Hex("#3b4957"), Hex("#8fa6b9"), Hex("#9aa4ac"),
        Hex("#54cb8b"), Hex("#fa788d"));

    // Claro: fundo de gelo, texto escuro e ação desabilitada azul muito suave.
    private static readonly V269Palette Claro = new(
        Hex("#dbeaf6"), Hex("#d9e9f6"), Hex("#e6f1fa"), Hex("#edf5fb"),
        Hex("#f2f8fd"), Hex("#8db1ca"), Hex("#17374c"), Hex("#3f627a"),
        Hex("#cce3f4"), Hex("#2374ab"), Hex("#8fb9d5"),
        Hex("#278f5d"), Hex("#e25971"));

    internal static V269Palette ForTheme(V269Theme theme) => theme switch
    {
        V269Theme.Dark => Grafite,
        V269Theme.Claro => Claro,
        _ => Azul
    };

    internal static int InterfacePercent(int percent) =>
        Math.Clamp(percent, MinimumInterfacePercent, MaximumInterfacePercent);

    internal static int TextPercent(int percent) =>
        Math.Clamp(percent, MinimumTextPercent, MaximumTextPercent);

    internal static int Scale(int logicalPixels, int interfacePercent) =>
        (int)Math.Round(logicalPixels * InterfacePercent(interfacePercent) / 100.0,
            MidpointRounding.AwayFromZero);

    internal static Color ActionColor(V269Theme theme, V269ActionKind action, bool allowed)
    {
        V269Palette palette = ForTheme(theme);
        if (!allowed) return palette.DisabledAction;
        return action == V269ActionKind.Atualizar
            ? palette.EnabledUpdate : palette.EnabledRemove;
    }
}
