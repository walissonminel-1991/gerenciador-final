using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class Work12UninstallLayoutTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Require(bool ok, string label) { if (!ok) throw new Exception("W12 UI: " + label); }
    private static IEnumerable<Control> Children(Control root) => root.Controls.Cast<Control>()
        .SelectMany(c => new[] { c }.Concat(Children(c)));
    internal static void Run()
    {
        using var host = new MainForm(startServices: false);
        host.Show(); Application.DoEvents();
        var shell = Children(host).OfType<V269Shell>().Single();
        shell.SelectPage(V269Page.Aplicativos); Application.DoEvents();
        var list = (DarkProgramList)typeof(MainForm).GetField("_programList", Private)!.GetValue(host)!;
        var inventory = (List<ProgramEntry>)typeof(MainForm).GetField("_installed", Private)!.GetValue(host)!;
        var store = (UninstallOperationStore)typeof(MainForm).GetField("_uninstallOperations", Private)!.GetValue(host)!;
        var render = typeof(MainForm).GetMethod("RenderPrograms", Private)!;
        var search = (TextBox)typeof(MainForm).GetField("_search", Private)!.GetValue(host)!;
        var app = new ProgramEntry { Name = "Aplicativo sintético", Publisher = "GF Tests", InventoryId = "GF-W12-UI",
            CanUninstall = true, UninstallString = @"C:\GF-Synthetic\uninstall.exe" };
        inventory.Clear(); inventory.Add(app); store.Set(app, UninstallStage.Preparing);
        render.Invoke(host, []); list.RefreshOperations(); Application.DoEvents();
        Require(list.UninstallOperationFor!(list.Items.Single())!.Active && list.OperationTimerRunning,
            "barra e animacao aparecem no primeiro estado, antes do fornecedor");
        store.Set(app, UninstallStage.Working);
        inventory.Clear(); var reconstructed = new ProgramEntry { Name = app.Name, Publisher = app.Publisher, InventoryId = app.InventoryId,
            CanUninstall = true, UninstallString = app.UninstallString };
        inventory.Add(reconstructed); render.Invoke(host, []); search.Text = "nao existe"; Application.DoEvents();
        search.Text = ""; Application.DoEvents();
        Require(list.Items.Count == 1 && list.UninstallOperationFor(reconstructed)!.Stage == UninstallStage.Working,
            "filtro e reconstrucao conservam progresso da instalacao correta");
        store.Set(reconstructed, UninstallStage.Pending); list.RefreshOperations();
        Require(!list.OperationTimerRunning && list.UninstallOperationFor(reconstructed)!.PreventRetry,
            "resultado indeterminado persiste sem animacao nem repeticao");
        int combinations = 0;
        foreach (var size in new[] { new Size(1280,720), new Size(1366,768), new Size(1440,900), new Size(1920,1080), new Size(2560,1440) })
        foreach (float dpi in new[] { 1f, 1.25f, 1.5f, 2f })
        {
            int width = Math.Max(600, StartupWindowSizing.Fit(new Rectangle(Point.Empty, size)).Width - 48);
            var stages = Enum.GetValues<UninstallStage>();
            using var bitmap = new Bitmap(width, 67 * stages.Length); bitmap.SetResolution(96*dpi, 96*dpi);
            using var graphics = Graphics.FromImage(bitmap);
            for (int i = 0; i < stages.Length; i++)
            {
                store.Set(reconstructed, stages[i]);
                var bounds = new Rectangle(0, i*67, width, 67); var area = AppRowGeometry.OperationArea(bounds);
                Require(bounds.Contains(area) && area.Right < AppRowGeometry.UpdateButton(bounds).Left,
                    "andamento nao pode cobrir botoes nem sair do cartao");
                typeof(MainForm).GetMethod("DrawProgramCard", Private)!.Invoke(host,
                    [list, new DrawItemEventArgs(graphics, list.Font, bounds, 0, DrawItemState.Default)]);
                Require(!store.Find(reconstructed)!.Text.Contains('%'), "rotulo nao apresenta porcentagem inventada");
            }
            if (dpi == 1)
            {
                Directory.CreateDirectory("LayoutCaptures");
                bitmap.Save($"LayoutCaptures/Desinstalacao-W12-{size.Width}x{size.Height}.png");
            }
            const string summary = "Desinstalação confirmada.\r\nVerificação parcial.\r\nRegistro: acesso negado (erro 5).\r\nDados pessoais preservados.";
            host.ShowUninstallResult(summary);
            Application.DoEvents();
            var text = Children(shell.RightPane).OfType<TextBox>()
                .Single(control => control.Name == "V269PanelDescription");
            Require(text.ReadOnly && text.Multiline && text.Text == summary &&
                shell.RightPane.Current?.Kind == V269PanelKind.Result,
                "resumo mostra a evidência real em painel de texto rolável");
            combinations++;

        }
        var report = typeof(MainForm).GetMethod("SetUninstallProgress", Private)!;
        Task.Run(() => report.Invoke(host, [reconstructed, "working", null])).GetAwaiter().GetResult();
        Application.DoEvents();
        Require(list.OperationTimerRunning && list.UninstallOperationFor!(reconstructed)!.Stage == UninstallStage.Working,
            "worker relata progresso sem acessar controles fora da thread visual");
        Task.Run(() => report.Invoke(host, [reconstructed, "finished", null])).GetAwaiter().GetResult();
        Application.DoEvents(); Require(!list.OperationTimerRunning, "conclusao no worker desliga animacao");
        host.MinimizeToTray(); host.ShowUninstallResult("Resultado pendente na bandeja"); Application.DoEvents();
        Require(shell.RightPane.Current?.Description != "Resultado pendente na bandeja",
            "bandeja mantém resultado pendente sem abrir janela");
        host.RestoreFromTray(); Application.DoEvents();
        Require(shell.RightPane.Current?.Description == "Resultado pendente na bandeja",
            "resumo aparece ao reabrir o GF");
        host.Close();
        using (var tray = new MainForm(startServices: false, startInTray: true))
        {
            Require(tray.Opacity == 0, "inicio na bandeja oculta primeiro frame");
            tray.Show(); Application.DoEvents();
            Require(tray.IsInTray && !tray.Visible, "argumento tray inicia oculto sem servicos auxiliares");
            tray.RestoreFromTray(); Application.DoEvents();
            Require(tray.Visible && !tray.IsInTray && tray.Opacity == 1, "restauracao do inicio automatico funciona");
            tray.Close();
        }
        Console.WriteLine($"OK WORK12 UI: {combinations} combinacoes, {Enum.GetValues<UninstallStage>().Length} estados, filtro, resumo e bandeja");
    }
}
