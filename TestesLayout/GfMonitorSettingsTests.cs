using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class GfMonitorSettingsTests
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Configurações Monitor GF: " + message);
    }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control next in All(child)) yield return next;
        }
    }

    internal static void Run()
    {
        using var panel = new GfMonitorSettingsPanel();
        int changes = 0;
        panel.PreferencesChanged += (_, _) => changes++;
        var defaults = GfResourceMonitorPreferences.Default;
        var preferences = defaults with { Enabled = true, Expanded = true, AlwaysOnTop = true, OpacityPercent = 75,
            Fixed = false, ScalePercent = 125, MonitorTheme = V269Theme.Claro, X = -30, Y = 20 };
        panel.LoadPreferences(preferences);
        Require(changes == 0 && panel.ReadPreferences(preferences) == preferences, "hidratação sem efeitos colaterais");
        var enable = (CheckBox)panel.Controls.Find("GfMonitorEnabled", true).Single();
        var fix = (CheckBox)panel.Controls.Find("GfMonitorFixed", true).Single();
        var overlay = (CheckBox)panel.Controls.Find("GfMonitorOverlay", true).Single();
        var startup = (CheckBox)panel.Controls.Find("GfMonitorStartup", true).Single();
        var opacity = (TrackBar)panel.Controls.Find("GfMonitorTransparency", true).Single();
        var scale = (TrackBar)panel.Controls.Find("GfMonitorScale", true).Single();
        var themeChoice = (ComboBox)panel.Controls.Find("GfMonitorTheme", true).Single();
        var inputs = All(panel).Where(c => c is CheckBox or TrackBar or ComboBox).ToArray();
        Require(inputs.Length == 7 && inputs.All(c => c.TabStop), "exatamente sete controles navegáveis");
        Require(!startup.Enabled && !startup.AutoCheck, "startup sem confirmação não é ação real");
        panel.LoadStartupState(true,true);
        Require(startup.Checked && startup.Enabled && !panel.RequestedStartupState(), "estado Windows observado");
        panel.LoadStartupState(false,true);
        Require(!startup.Checked && panel.RequestedStartupState() && changes == 0, "observação não grava preferências");
        enable.Checked = false; opacity.Value = 10; fix.Checked = true; scale.Value = 140; themeChoice.SelectedIndex = 2;
        var result = panel.ReadPreferences(preferences with { X = -100, Y = 50 });
        Require(changes == 5 && !result.Enabled && result.OpacityPercent == 90 && result.Fixed &&
            result.ScalePercent == 140 && result.MonitorTheme == V269Theme.Dark && result.AlwaysOnTop &&
            result.Expanded && result.X == -100 && result.Y == 50, "preferências independentes conservam posição e modo");
        overlay.Checked = false;
        Require(panel.ReadPreferences(result).Fixed && !panel.ReadPreferences(result).AlwaysOnTop, "fixar não equivale a sobrepor");
        foreach (V269Theme theme in Enum.GetValues<V269Theme>())
        {
            panel.Configure(theme);
            Require(panel.BackColor == V269VisualTokens.ForTheme(theme).Card &&
                enable.ForeColor == V269VisualTokens.ForTheme(theme).Text, "tokens dos três temas");
        }
        Require(changes == 6 && panel.ReadPreferences(result).MonitorTheme == V269Theme.Dark,
            "tema da página não altera tema do monitor");
        Console.WriteLine("OK Monitor GF: sete configurações, tema independente e inicialização observada.");
    }
}
