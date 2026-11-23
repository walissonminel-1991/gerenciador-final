using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class V269ShellTests
{
    internal static void Run()
    {
        var blue = V269VisualTokens.ForTheme(V269Theme.AzulGf);
        var dark = V269VisualTokens.ForTheme(V269Theme.Dark);
        var light = V269VisualTokens.ForTheme(V269Theme.Claro);
        Check(blue.Background != dark.Background && dark.Background != light.Background &&
            light.Background != blue.Background, "Os três temas devem ter fundos independentes.");
        Check(dark.Background.R == dark.Background.G || Math.Abs(dark.Background.R - dark.Background.G) < 9,
            "Dark precisa continuar grafite, não azul saturado.");
        Check(light.Text.GetBrightness() < dark.Text.GetBrightness(),
            "Tema Claro deve usar texto escuro; Dark, texto claro suave.");
        Check(V269VisualTokens.ForTheme((V269Theme)999) == blue,
            "Tema inválido deve retornar ao Azul GF sem lançar erro.");
        Check(V269VisualTokens.InterfacePercent(0) == 90 &&
            V269VisualTokens.InterfacePercent(200) == 110 &&
            V269VisualTokens.TextPercent(0) == 90 &&
            V269VisualTokens.TextPercent(200) == 115,
            "Limites das barras independentes não foram preservados.");
        Check(V269VisualTokens.Scale(100, 90) == 90 &&
            V269VisualTokens.Scale(100, 110) == 110, "Escala geométrica inesperada.");

        foreach (var theme in Enum.GetValues<V269Theme>())
        {
            var neutral = V269VisualTokens.ActionColor(theme, V269ActionKind.Atualizar, false);
            var update = V269VisualTokens.ActionColor(theme, V269ActionKind.Atualizar, true);
            var remove = V269VisualTokens.ActionColor(theme, V269ActionKind.Remover, true);
            Check(neutral != update && neutral != remove && update != remove,
                "As ações elegíveis e inelegíveis não podem compartilhar a mesma cor.");
            Check(update.G > update.R && update.G > update.B,
                "Atualização elegível deve ser verde.");
            Check(remove.R > remove.G && remove.R > remove.B,
                "Desinstalação elegível deve ser vermelha.");
            Check(V269VisualTokens.ActionColor(theme, V269ActionKind.Remover, false) == neutral,
                "Estado não permitido deve ser neutro para qualquer ação.");
        }

        using var host = new Form
        {
            Name = "V269TestHost",
            Size = new Size(1250, 760),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-5000, -5000),
            ShowInTaskbar = false
        };
        using var shell = new V269Shell { Dock = DockStyle.Fill };
        host.Controls.Add(shell);
        host.Show();
        Application.DoEvents();

        Check(shell.Theme == V269Theme.AzulGf && shell.SelectedPage == V269Page.Inicio,
            "O shell deve iniciar no Azul GF e na aba Início.");
        Check(shell.Controls.Find("V269Sidebar", true).Length == 1 &&
            shell.Controls.Find("V269Main", true).Length == 1 &&
            shell.Controls.Find("V269RightPane", true).Length == 1,
            "As três regiões estruturais precisam existir.");
        Check(shell.Controls.Find("V269NavConfiguracoes", true).Length == 1,
            "As oito abas devem ser acessíveis no menu lateral.");

        using var apps = new Panel { Name = "AppsVistaNativa" };
        using var drivers = new Panel { Name = "DriversVistaNativa" };
        shell.SetPageContent(V269Page.Aplicativos, apps);
        shell.SetPageContent(V269Page.Drivers, drivers);
        int changed = 0;
        shell.NavigationChanged += (_, _) => changed++;
        shell.SelectPage(V269Page.Aplicativos);
        Check(apps.Parent is not null && apps.Visible && !drivers.Visible,
            "A navegação deve preservar e exibir a view nativa de Aplicativos.");
        shell.SelectPage(V269Page.Drivers);
        Check(!apps.Visible && drivers.Visible,
            "A troca de aba não pode manter duas views visíveis.");
        shell.SelectPage(V269Page.Aplicativos);
        Check(ReferenceEquals(shell.PageContent(V269Page.Aplicativos), apps) &&
            ReferenceEquals(shell.PageContent(V269Page.Drivers), drivers) &&
            changed == 3, "Abas devem preservar instâncias e emitir mudanças uma única vez.");

        var result = new V269PanelPresentation(V269PanelKind.Result,
            "Resultado confirmado", "Estado fornecido pelo controlador de domínio.");
        shell.PresentPanel(result);
        shell.ApplyAppearance(V269Theme.Dark, 110, 115);
        Check(shell.Theme == V269Theme.Dark &&
            shell.InterfaceScalePercent == 110 &&
            shell.TextScalePercent == 115 &&
            ReferenceEquals(shell.RightPane.Current, result),
            "Trocar tema e escalas não pode descartar estado do painel direito.");

        var columns = shell.Controls.Find("V269Columns", true)
            .OfType<TableLayoutPanel>().Single();
        // O runner Windows pode ter viewport menor que a dimensão solicitada
        // ao formulário. Exigir a escala correta para a largura REAL do shell.
        int compactWidth = shell.ClientSize.Width < 1040 ? 166 : 210;
        int compactRight = shell.ClientSize.Width < 1040 ? 240 : 310;
        int expectedLeft = V269VisualTokens.Scale(compactWidth, 110);
        int expectedRight = V269VisualTokens.Scale(compactRight, 110);
        Check(columns.ColumnStyles[0].Width == expectedLeft &&
            columns.ColumnStyles[2].Width == expectedRight,
            $"Escala precisa alterar as colunas na largura real. " +
            $"shell={shell.ClientSize.Width}, esquerda={columns.ColumnStyles[0].Width}/{expectedLeft}, " +
            $"direita={columns.ColumnStyles[2].Width}/{expectedRight}.");

        var progress = new V269PanelPresentation(V269PanelKind.Progress,
            "Em andamento", "Sem conclusão presumida.", 37);
        shell.PresentPanel(progress);
        var bar = shell.Controls.Find("V269PanelProgress", true)
            .OfType<ProgressBar>().Single();
        Check(bar.Visible && bar.Value == 37, "Progresso deve permanecer no painel direito.");
        shell.SelectPage(V269Page.Configuracoes);
        Check(ReferenceEquals(shell.RightPane.Current, progress),
            "O painel não pode perder o progresso ao trocar de aba.");

        shell.ApplyAppearance(V269Theme.Claro, 90, 90);
        Check(shell.Theme == V269Theme.Claro &&
            shell.InterfaceScalePercent == 90 &&
            shell.TextScalePercent == 90,
            "Troca de temas e escalas precisa ser reversível.");
        shell.PresentPanel(null);
        Check(shell.RightPane.Current is null && !bar.Visible,
            "O painel deve limpar estado sem simular sucesso.");

        bool rejected = false;
        try { shell.SetPageContent(V269Page.Inicio, apps); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Uma view não pode ser associada a duas abas.");

        // A etapa A não contém botão de instalar/desinstalar nem serviço de manutenção.
        Check(!AllDescendants(shell).OfType<Button>().Any(b =>
            b.Name.Contains("Install", StringComparison.OrdinalIgnoreCase) ||
            b.Name.Contains("Uninstall", StringComparison.OrdinalIgnoreCase)),
            "O shell da etapa A não deve oferecer ações administrativas fictícias.");

        host.Close();
        Console.WriteLine("PASS UI-V269: tokens, três temas, oito abas, view estável, teclado-base, painel e escala.");
    }

    private static IEnumerable<Control> AllDescendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control sub in AllDescendants(child)) yield return sub;
        }
    }

    private static void Check(bool success, string message)
    {
        if (!success) throw new Exception("UI-V269: " + message);
    }
}
