using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class V269IntegrationTests
{
    internal static void Run()
    {
        using var form = new MainForm(startServices: false);
        form.Show();
        Application.DoEvents();
        var shell = form.Controls.Find("V269Shell", true).OfType<V269Shell>().Single();
        var pages = Enum.GetValues<V269Page>();
        var views = pages.Select(page => shell.PageContent(page) ??
            throw new InvalidOperationException($"View ausente: {page}")).ToArray();
        Require(views.Distinct().Count() == 8 && shell.SelectedPage == V269Page.Inicio,
            "oito instâncias nativas com Início ativo");
        var confirm = typeof(MainForm).GetMethod("ConfirmV269Async",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var waiting = (Task<bool>)confirm.Invoke(form, ["Revisão de teste",
            "Alvo sintético. Nenhuma manutenção.", CancellationToken.None])!;
        shell.SelectPage(V269Page.Drivers);
        Require(!waiting.IsCompleted && shell.RightPane.Visible &&
            shell.RightPane.Current?.Kind == V269PanelKind.Confirmation && !views[2].Enabled,
            "troca de aba conserva a confirmação e impede uma operação concorrente");
        shell.RightPane.ResolveConfirmation(false);
        Pump(waiting);
        Require(!waiting.Result && views[2].Enabled,
            "cancelamento explícito reabilita a aba sem autorizar operação");
        using var token = new CancellationTokenSource();
        waiting = (Task<bool>)confirm.Invoke(form, ["Segunda revisão", "Fixture", token.Token])!;
        token.Cancel();
        Pump(waiting);
        Require(!waiting.Result, "cancelamento do motor encerra a espera de confirmação");

        form.ShowUninstallResult("Resultado preservado");
        foreach (var page in pages)
        {
            shell.SelectPage(page);
            Application.DoEvents();
            Require(ReferenceEquals(shell.PageContent(page), views[(int)page]) &&
                shell.RightPane.Current?.Description == "Resultado preservado",
                "identidade e resultado preservados durante navegação");
        }
        var appearance = Find<V269AppearancePanel>(views[(int)V269Page.Configuracoes]);
        Require(appearance is not null, "Configurações usa o painel de aparência real");
        var theme = appearance!.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
        theme.SelectedIndex = (int)V269Theme.Claro;
        Application.DoEvents();
        var log = Find<DarkLogView>(shell.RightPane);
        var editor = log is null ? null : Find<TextBox>(log);
        Require(editor is not null && editor.BackColor.GetBrightness() > editor.ForeColor.GetBrightness() &&
            shell.RightPane.Current?.Description == "Resultado preservado",
            "tema Claro preserva contraste do registro e o resultado publicado");
        form.Close();
        Console.WriteLine("PASS V2.6.9 integração: MainForm ativa, oito views, confirmação, cancelamento e resultado persistente.");
    }

    private static T? Find<T>(Control control) where T : Control
    {
        if (control is T match) return match;
        return control.Controls.Cast<Control>().Select(Find<T>).FirstOrDefault(child => child is not null);
    }

    private static void Pump(Task task)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(4))
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        if (!task.IsCompleted) throw new TimeoutException("Confirmação do painel não encerrou.");
        task.GetAwaiter().GetResult();
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("V2.6.9 integrada: " + message);
    }
}
