using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class GfMonitorSettingsTests
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Configurações Monitor GF: " + message);
    }

    internal static void Run()
    {
        using var panel = new GfMonitorSettingsPanel();
        int changes = 0;
        panel.PreferencesChanged += (_, _) => changes++;
        var defaults = GfResourceMonitorPreferences.Default;
        var preferences = defaults with { Enabled = true, Expanded = true, AlwaysOnTop = true, OpacityPercent = 75, X = -30, Y = 20 };
        panel.LoadPreferences(preferences);
        Require(changes == 0 && panel.ReadPreferences(preferences) == preferences, "hidratação sem efeitos colaterais");
        var enable = (CheckBox)panel.Controls.Find("GfMonitorEnabled", true).Single();
        var opacity = (TrackBar)panel.Controls.Find("GfMonitorOpacity", true).Single();
        Require(enable.TabStop && opacity.TabStop, "navegação por teclado");
        enable.Checked = false; opacity.Value = 90;
        var result = panel.ReadPreferences(preferences with { X = -100, Y = 50 });
        Require(changes == 2 && !result.Enabled && result.OpacityPercent == 90 && result.X == -100 && result.Y == 50,
            "mudanças explícitas não sobrescrevem posição atual");
        foreach (V269Theme theme in Enum.GetValues<V269Theme>())
        {
            panel.Configure(theme);
            Require(panel.BackColor == V269VisualTokens.ForTheme(theme).Card &&
                enable.ForeColor == V269VisualTokens.ForTheme(theme).Text, "tokens dos três temas");
        }
        Require(changes == 2, "troca de tema não altera preferências");
        Console.WriteLine("OK Monitor GF: componente de Configurações, três temas e edição explícita.");
    }
}
