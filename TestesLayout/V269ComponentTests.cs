using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

/// <summary>
/// Somente componentes visuais isolados: nenhuma consulta ou ação no Windows.
/// Não acoplar a MainForm, que será integrada exclusivamente pelo WORK.
/// </summary>
internal static class V269ComponentTests
{
    internal static void Run(Form host, V269Shell shell)
    {
        using var update = new V269ActionButton(V269ActionKind.Atualizar);
        using var remove = new V269ActionButton(V269ActionKind.Remover);
        host.Controls.Add(update);
        host.Controls.Add(remove);

        int updateClicks = 0, removeClicks = 0;
        update.Click += (_, _) => updateClicks++;
        remove.Click += (_, _) => removeClicks++;
        Check(!update.Enabled && !remove.Enabled &&
            !update.TabStop && !remove.TabStop,
            "Ações devem nascer bloqueadas e sem ativação por teclado.");

        foreach (V269Theme theme in Enum.GetValues<V269Theme>())
        {
            // Contagens são acumuladas entre temas; medir contra o valor
            // imediatamente anterior ao acionamento bloqueado.
            int clicksBeforeUpdate = updateClicks;
            int clicksBeforeRemove = removeClicks;
            update.ApplyState(false, theme);
            remove.ApplyState(false, theme);
            Check(!update.Enabled && !remove.Enabled &&
                !update.TabStop && !remove.TabStop,
                "Ações não permitidas devem permanecer desabilitadas.");
            Check(update.BackColor == V269VisualTokens.ActionBackground(theme, V269ActionKind.Atualizar, false) &&
                remove.BackColor == V269VisualTokens.ActionBackground(theme, V269ActionKind.Remover, false),
                "Estado neutro deve ser visível independentemente da ação.");
            Check(update.ForeColor == V269VisualTokens.ActionColor(theme, V269ActionKind.Atualizar, false) &&
                remove.ForeColor == V269VisualTokens.ActionColor(theme, V269ActionKind.Remover, false),
                "Cores de ações não permitidas devem corresponder aos tokens.");
            Check(update.AccessibleName?.Contains("não pode", StringComparison.Ordinal) == true &&
                remove.AccessibleName?.Contains("não pode", StringComparison.Ordinal) == true,
                "Leitor de tela deve anunciar a indisponibilidade binária.");
            update.PerformClick(); remove.PerformClick();
            Check(updateClicks == clicksBeforeUpdate &&
                removeClicks == clicksBeforeRemove,
                $"Botão bloqueado não pode disparar Click ({theme}): " +
                $"atualizar={updateClicks}/{clicksBeforeUpdate}, " +
                $"remover={removeClicks}/{clicksBeforeRemove}.");

            update.ApplyState(true, theme);
            remove.ApplyState(true, theme);
            Check(update.Enabled && remove.Enabled && update.TabStop && remove.TabStop,
                "Ação permitida deve estar habilitada e acessível pelo teclado.");
            Check(update.BackColor != remove.BackColor &&
                update.BackColor != V269VisualTokens.ActionBackground(theme, V269ActionKind.Atualizar, false),
                "Verde e vermelho disponíveis devem diferir do neutro.");
            Check(update.ForeColor == V269VisualTokens.ActionColor(theme, V269ActionKind.Atualizar, true) &&
                remove.ForeColor == V269VisualTokens.ActionColor(theme, V269ActionKind.Remover, true),
                "Cores de ícone disponíveis não corresponderam aos tokens.");
            Check(update.AccessibleName?.Contains(": pode", StringComparison.Ordinal) == true &&
                remove.AccessibleName?.Contains(": pode", StringComparison.Ordinal) == true,
                "Leitor de tela deve anunciar a permissão binária.");
            update.PerformClick(); remove.PerformClick();
        }
        Check(updateClicks == 3 && removeClicks == 3,
            "Eventos de componentes permitidos devem funcionar uma vez por acionamento.");
        update.ApplyState(false, V269Theme.Dark);
        remove.ApplyState(false, V269Theme.Dark);
        update.PerformClick(); remove.PerformClick();
        Check(updateClicks == 3 && removeClicks == 3,
            "Revogação de elegibilidade deve impedir cliques posteriores.");
        host.Controls.Remove(update);
        host.Controls.Remove(remove);

        using var appearance = new V269AppearancePanel();
        host.Controls.Add(appearance);
        int changed = 0;
        appearance.AppearanceChanged += (_, _) => changed++;
        Check(appearance.GetOptions() == V269AppearanceOptions.Default,
            "Aparência deve iniciar em Azul GF, 100% e animações ligadas.");
        Check(appearance.Controls.Find("V269UiScale", true).OfType<TrackBar>().Single().Minimum == 90 &&
            appearance.Controls.Find("V269TextScale", true).OfType<TrackBar>().Single().Maximum == 115,
            "Barras devem manter os intervalos aprovados.");
        Check(appearance.Controls.Find("V269ThemeChoice", true)
            .OfType<ComboBox>().Single().DropDownStyle == ComboBoxStyle.DropDownList,
            "Tema deve ser uma seleção fechada, sem texto arbitrário.");

        appearance.SetOptions(new V269AppearanceOptions(V269Theme.Dark, 110, 115, false));
        Check(appearance.GetOptions() == new V269AppearanceOptions(V269Theme.Dark, 110, 115, false) &&
            changed == 0, "Aplicação programática de preferências não pode disparar eventos.");
        Check(appearance.Controls.Find("V269UiPercent", true).Single().Text == "110%" &&
            appearance.Controls.Find("V269TextPercent", true).Single().Text == "115%",
            "Indicadores precisam exibir os valores efetivos.");
        Check(!appearance.GetOptions().CanAnimate(reducedMotion: false),
            "Animações desligadas não podem ser ativadas.");
        appearance.SetOptions(new V269AppearanceOptions(V269Theme.Claro, 1, 500, true));
        Check(appearance.GetOptions().InterfacePercent == 90 &&
            appearance.GetOptions().TextPercent == 115 && changed == 0,
            "Preferências devem respeitar limites sem eventos espúrios.");
        Check(!appearance.GetOptions().CanAnimate(reducedMotion: true) &&
            appearance.GetOptions().CanAnimate(reducedMotion: false),
            "Movimento reduzido deve prevalecer sobre a opção visual.");
        var uiScale = appearance.Controls.Find("V269UiScale", true)
            .OfType<TrackBar>().Single();
        uiScale.Value = 96;
        Check(changed == 1 && appearance.GetOptions().InterfacePercent == 96,
            "Mudança pelo usuário deve publicar exatamente um evento.");
        var themeChoice = appearance.Controls.Find("V269ThemeChoice", true)
            .OfType<ComboBox>().Single();
        themeChoice.SelectedIndex = 0;
        Check(changed == 2 && appearance.GetOptions().Theme == V269Theme.AzulGf,
            "Seleção de tema deve atualizar somente as preferências visuais.");
        Check(uiScale.AccessibleName == "Tamanho da interface" &&
            appearance.Controls.Find("V269TextScale", true).Single().AccessibleName == "Tamanho dos textos",
            "As escalas devem ter nomes acessíveis.");
        // Vinculação apenas visual: muda tema/escalas do shell ao editar
        // controles de aparência, sem reconstruir views ou mexer no painel.
        var pageBeforeBinding = shell.SelectedPage;
        var panelBeforeBinding = shell.RightPane.Current;
        using (var binder = new V269AppearanceBinder(shell, appearance))
        {
            Check(shell.Theme == V269Theme.AzulGf && shell.InterfaceScalePercent == 96 &&
                shell.TextScalePercent == 115,
                "Conexão inicial deve refletir o estado real dos controles.");
            binder.SetOptions(new V269AppearanceOptions(V269Theme.Dark, 110, 114, false));
            Check(shell.Theme == V269Theme.Dark && shell.InterfaceScalePercent == 110 &&
                shell.TextScalePercent == 114 &&
                appearance.GetOptions().AnimationsEnabled == false,
                "Alteração programática deve sincronizar os dois controles nativos.");
            themeChoice.SelectedIndex = 2;
            Check(shell.Theme == V269Theme.Claro &&
                shell.SelectedPage == pageBeforeBinding &&
                ReferenceEquals(shell.RightPane.Current, panelBeforeBinding),
                "Trocar tema no controle deve preservar página e estado do painel.");
            uiScale.Value = 103;
            Check(shell.InterfaceScalePercent == 103 && shell.TextScalePercent == 114,
                "Slider de interface deve atualizar o shell sem alterar a escala de texto.");
            var textSlider = appearance.Controls.Find("V269TextScale", true)
                .OfType<TrackBar>().Single();
            textSlider.Value = 94;
            Check(shell.TextScalePercent == 94 && shell.InterfaceScalePercent == 103,
                "Slider de texto deve atualizar o shell sem alterar a geometria.");
        }
        var themeAfterDispose = shell.Theme;
        var uiAfterDispose = shell.InterfaceScalePercent;
        // Após Dispose, alterar controles de verdade, não só SetOptions
        // (que suprime eventos por definição).
        themeChoice.SelectedIndex = 0;
        uiScale.Value = 100;
        Check(shell.Theme == themeAfterDispose && shell.InterfaceScalePercent == uiAfterDispose,
            "Dispose deve desconectar eventos reais sem assinaturas penduradas.");
        host.Controls.Remove(appearance);

        Check(V269VisualTokens.ForTheme(V269Theme.Dark).Sidebar == ColorTranslator.FromHtml("#1c252d") &&
            V269VisualTokens.ForTheme(V269Theme.Dark).Content == ColorTranslator.FromHtml("#202a33") &&
            V269VisualTokens.ForTheme(V269Theme.Claro).Sidebar == ColorTranslator.FromHtml("#d5eaf9"),
            "Tokens de superfície devem preservar a referência visual V2.6.9.");

        Console.WriteLine("PASS UI-V269-COMPONENTES: 3 temas, pode/não pode, botões, preferências e acessibilidade.");
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("UI-V269-COMPONENTES: " + message);
    }
}
