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
        var nav = (DarkTabHost)typeof(MainForm).GetField("_nav", Private)!.GetValue(form)!;
        var title = (Label)typeof(MainForm).GetField("_systemPanelTitle", Private)!.GetValue(form)!;
        var setView = typeof(MainForm).GetMethod("SetSystemView", Private)!;
        var refresh = typeof(MainForm).GetMethod("RefreshStartupEntriesAsync", Private)!;
        nav.SelectTab(5);
        foreach (bool leaveMainTab in new[] { false, true })
        {
            setView.Invoke(form, ["startup"]);
            var pending = new TaskCompletionSource<IReadOnlyList<StartupItem>>();
            var task = (Task)refresh.Invoke(form,
                [(Func<Task<IReadOnlyList<StartupItem>>>)(() => pending.Task)])!;
            if (leaveMainTab) { nav.SelectTab(0); nav.SelectTab(5); }
            else { setView.Invoke(form, ["update"]); setView.Invoke(form, ["startup"]); }
            title.Text = "RESULTADO DA NAVEGACAO MAIS NOVA";
            pending.SetResult([new StartupItem("GF W9 OLD", "synthetic", "registry", "synthetic", true, false)]);
            Pump(task);
            check(title.Text == "RESULTADO DA NAVEGACAO MAIS NOVA",
                leaveMainTab ? "W9-UI2 sair e voltar a aba principal ignora consulta antiga" :
                    "W9-UI1 Inicializacao Update Inicializacao ignora conclusao antiga");
        }
        setView.Invoke(form, ["startup"]);
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
