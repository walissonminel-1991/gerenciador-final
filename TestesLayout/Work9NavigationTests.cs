using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class Work9NavigationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void Run(Action<bool, string> check)
    {
        using var form = new MainForm(startServices: false);
        form.Show(); Application.DoEvents();
        var nav = (V269Shell)typeof(MainForm).GetField("_nav", Private)!.GetValue(form)!;
        var title = (Label)typeof(MainForm).GetField("_v269StartupTitle", Private)!.GetValue(form)!;
        var setView = typeof(MainForm).GetMethod("SetSystemView", Private)!;
        var refresh = typeof(MainForm).GetMethod("RefreshStartupEntriesAsync", Private)!;
        nav.SelectPage(V269Page.Sistema);
        check(typeof(MainForm).GetFields(Private).All(field => field.FieldType != typeof(ToolTip)),
            "WU-UI1 nenhum componente tooltip na janela principal");
        setView.Invoke(form, ["update"]);
        typeof(MainForm).GetMethod("DrawWindowsUpdates", Private)!.Invoke(form, null);
        var search = (Button)typeof(MainForm).GetField("_searchWindowsUpdatesButton", Private)!.GetValue(form)!;
        var summary = (Label)typeof(MainForm).GetField("_systemPanelSummary", Private)!.GetValue(form)!;
        bool scanning = (bool)typeof(MainForm).GetField("_windowsUpdateScanning", Private)!.GetValue(form)!;
        bool checkedWU = (bool)typeof(MainForm).GetField("_windowsUpdateChecked", Private)!.GetValue(form)!;
        check(search.Name == "WindowsUpdateExplicitSearch" && search.Text == "Verificar atualizações" &&
            search.Enabled && !scanning && !checkedWU &&
            summary.Text.Contains("Verificar atualizações", StringComparison.Ordinal),
            "WU-UI2 visualização sem consulta e botão de busca explícito");
        foreach (bool leaveMainTab in new[] { false, true })
        {
            nav.SelectPage(V269Page.Inicializacao);
            var pending = new TaskCompletionSource<IReadOnlyList<StartupItem>>();
            var task = (Task)refresh.Invoke(form,
                [(Func<Task<IReadOnlyList<StartupItem>>>)(() => pending.Task)])!;
            nav.SelectPage(leaveMainTab ? V269Page.Aplicativos : V269Page.Sistema);
            nav.SelectPage(V269Page.Inicializacao);
            title.Text = "RESULTADO DA NAVEGACAO MAIS NOVA";
            pending.SetResult([new StartupItem("GF W9 OLD", "synthetic", "registry", "synthetic", true, false)]);
            Pump(task);
            check(title.Text == "RESULTADO DA NAVEGACAO MAIS NOVA",
                leaveMainTab ? "W9-UI2 sair e voltar a aba principal ignora consulta antiga" :
                    "W9-UI1 Inicializacao Update Inicializacao ignora conclusao antiga");
        }
        nav.SelectPage(V269Page.Inicializacao);
        var current = (Task)refresh.Invoke(form,
            [(Func<Task<IReadOnlyList<StartupItem>>>)(() => Task.FromResult<IReadOnlyList<StartupItem>>([]))])!;
        Pump(current);
        check(title.Text.Contains("Inicialização"), "W9-UI controle positivo consulta atual atualiza a tela");
        form.Close();
    }

    private static void Pump(Task task)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(5))
        { Application.DoEvents(); Thread.Sleep(5); }
        if (!task.IsCompleted) throw new TimeoutException("Consulta sintetica nao concluiu");
        task.GetAwaiter().GetResult(); Application.DoEvents();
    }
}
