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
        VerifyShellSettings(preferences);
        VerifySessionOwnership();
        Console.WriteLine("OK Monitor GF: sete configurações, tema independente e inicialização observada.");
    }

    private static void VerifyShellSettings(GfResourceMonitorPreferences preferences)
    {
        using var view = new V269SettingsPage { Dock = DockStyle.Fill };
        using var form = new Form { ClientSize = new System.Drawing.Size(760, 596), ShowInTaskbar = false };
        var options = new V269MonitorOptions();
        view.AddMonitor(options);
        form.Controls.Add(view);
        form.Show();
        var general = view.Controls.Find("V269GeneralTab", true).OfType<V269SettingsTab>().Single();
        var monitor = view.Controls.Find("V269MonitorTab", true).OfType<V269SettingsTab>().Single();
        Require(general.Selected && !monitor.Selected && !options.Visible && view.General.Tray.Visible,
            "Geral é selecionada ao abrir Configurações");
        int changes = 0;
        bool? startupRequested = null;
        var edited = preferences;
        options.PreferenceChanged += change => { ++changes; edited = change(edited); };
        options.DisplaySizeChanged += (scale, opacity) =>
        { ++changes; edited = edited with { ScalePercent = scale, OpacityPercent = opacity }; };
        options.StartupChanged += enabled => startupRequested = enabled;
        options.LoadObserved(preferences, null);
        monitor.AccessibilityObject.DoDefaultAction();
        Require(monitor.Selected && !general.Selected && options.Visible && !view.General.Tray.Visible &&
            view.Appearance.Visible && view.Appearance.Top > view.General.Bottom,
            "Monitor substitui somente o conteúdo do cartão Geral");
        Require(All(options).Count(c => c is CheckBox or TrackBar or ComboBox) == 7 && changes == 0,
            "sete opções no shell, sem efeitos durante hidratação");
        var startup = options.Controls.Find("V269MonitorStartup", true).OfType<CheckBox>().Single();
        Require(!startup.Enabled, "inicialização desconhecida é indisponível");
        options.LoadObserved(preferences, false);
        typeof(CheckBox).GetMethod("OnClick", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!.Invoke(startup, [EventArgs.Empty]);
        Require(startupRequested == true && !startup.Checked && changes == 0,
            "solicitação de startup só muda após leitura observada");
        options.Controls.Find("V269MonitorScale", true).OfType<TrackBar>().Single().Value = 130;
        options.Controls.Find("V269MonitorTransparency", true).OfType<TrackBar>().Single().Value = 20;
        Require(changes == 2 && edited.ScalePercent == 130 && edited.OpacityPercent == 80 &&
            edited.X == preferences.X && edited.Y == preferences.Y && edited.Expanded == preferences.Expanded,
            "edições de escala preservam modo e posição atuais");
        foreach (var theme in Enum.GetValues<V269Theme>())
        {
            view.ApplyAppearance(new V269AppearanceOptions(theme, 100, 100, true));
            Require(monitor.Accent == V269VisualTokens.ForTheme(theme).Accent && options.Width > 0,
                "seção selecionada reutiliza os tokens dos três temas");
        }
        general.AccessibilityObject.DoDefaultAction();
        Require(general.Selected && !options.Visible && view.General.Tray.Visible && view.Appearance.Visible,
            "retornar a Geral preserva os controles originais");
    }

    private static void VerifySessionOwnership()
    {
        const string path = @"C:\GF Teste\GerenciadorFinal.exe";
        Require(StartupTrayRegistration.MonitorCommand(path) == $"\"{path}\" --monitor" &&
            StartupTrayRegistration.Command(path) == $"\"{path}\" --tray" &&
            StartupTrayRegistration.MonitorValueName != StartupTrayRegistration.ValueName,
            "comandos do mesmo executável possuem entradas independentes");
        string role = "Fixture." + Guid.NewGuid().ToString("N");
        Require(!GfMonitorSessionLease.IsPresent(role), "consultar presença não cria o objeto da sessão");
        bool TryClaim()
        {
            using var lease = GfMonitorSessionLease.TryTake(role);
            return lease is not null;
        }
        using (var owner = GfMonitorSessionLease.TryTake(role))
        {
            Require(owner is not null && GfMonitorSessionLease.IsPresent(role) &&
                !Task.Run(TryClaim).GetAwaiter().GetResult(),
                "segunda thread não assume uma sessão ocupada");
        }
        Require(!GfMonitorSessionLease.IsPresent(role), "consulta não mantém uma sessão já encerrada");
        Require(Task.Run(TryClaim).GetAwaiter().GetResult(), "sessão pode ser retomada após o proprietário sair");
    }
}
