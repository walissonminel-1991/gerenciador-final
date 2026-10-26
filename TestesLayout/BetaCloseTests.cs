using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class BetaCloseTests
{
    internal static void Run()
    {
        int checks = 0;
        void Verify(bool value, string message)
        {
            if (!value) throw new Exception(message);
            checks++;
            Console.WriteLine("OK BETA janela: " + message);
        }
        var prior = AnonymousTelemetryContext.Current;
        string folder = Path.Combine(Path.GetTempPath(), "gf-close-fixture-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recorder = new AnonymousTelemetry(folder);
            Verify(recorder.SetConsent(TelemetryConsent.RemoteBeta), "consentimento somente na fixture local");
            AnonymousTelemetryContext.Current = recorder;
            using var form = new MainForm(startServices: false);
            form.Show();
            Application.DoEvents();
            form.MinimizeToTray();
            Verify(form.IsInTray && form.HasActiveTrayIcon, "minimizar preserva janela sem iniciar envio");
            form.RestoreFromTray();
            Application.DoEvents();
            Verify(!form.IsInTray && form.Visible, "restaurar pela bandeja permanece disponivel");

            var fields = BindingFlags.NonPublic | BindingFlags.Instance;
            var coordinator = (BetaCloseCoordinator)typeof(MainForm).GetField("_betaClose", fields)!.GetValue(form)!;
            Verify(coordinator.TryBegin(false, true), "iniciar estado de espera sintetico sem HTTP");
            int work = 0;
            var execute = typeof(MainForm).GetMethod("ExecuteAsync", fields)!;
            ((Task)execute.Invoke(form, ["fixture", (Func<Task>)(() => { work++; return Task.CompletedTask; }), false])!).GetAwaiter().GetResult();
            Verify(work == 0, "nova operacao e impedida enquanto fechamento aguarda envio");
            var notifyClosing = typeof(Form).GetMethod("OnFormClosing", fields)!;
            var repeat = new FormClosingEventArgs(CloseReason.UserClosing, false);
            notifyClosing.Invoke(form, [repeat]);
            Verify(repeat.Cancel, "segundo fechamento conserva uma unica espera");
            typeof(MainForm).GetField("_busy", fields)!.SetValue(form, true);
            var shutdown = new FormClosingEventArgs(CloseReason.WindowsShutDown, false);
            notifyClosing.Invoke(form, [shutdown]);
            Verify(!shutdown.Cancel && coordinator.IsShutdown, "Shutdown passa mesmo com trabalho e espera sinteticos");
            Verify(!coordinator.Finish(), "resposta atrasada nao agenda outro fechamento apos Shutdown");
            recorder.Record("APP.FALHA_INTERFACE");
            form.Close();
            Application.DoEvents();
            Verify(form.IsDisposed && !form.HasActiveTrayIcon, "janela e bandeja sao liberadas");
            Verify(File.Exists(recorder.PendingFile), "Shutdown guarda uma pendencia para proximo encerramento autorizado");
            Verify(Directory.EnumerateFiles(folder, "diagnostico-pendente*").Count() == 1,
                "reabertura mantem somente uma pendencia local");
            Verify(new AnonymousTelemetry(folder).Consent == TelemetryConsent.RemoteBeta,
                "reinicializacao simulada recupera somente consentimento do aviso atual");
            Console.WriteLine($"{checks}/{checks} verificacoes BETA de janela aprovadas; nenhum Shutdown do SO ou envio executado.");
        }
        finally
        {
            AnonymousTelemetryContext.Current = prior;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
