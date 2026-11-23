using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;
using Microsoft.Win32;

internal static class UninstallCleanupTests
{
    private static IEnumerable<Control> Children(Control control) => control.Controls.Cast<Control>()
        .SelectMany(c => new[] { c }.Concat(Children(c)));
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Until(Func<bool> condition, string message)
    {
        var until = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(10); }
        Require(condition(), message);
    }
    internal static IntegratedResidualPlan Plan(string name = "Aplicativo de teste", int count = 3)
    {
        var app = new ResidualAppIdentity(name, "Editor de teste", "1.0", name, @"C:\Fixture", "", "", "");
        var context = new ResidualUninstallContext(app, "synthetic", [], [], [], 4, []);
        return new(context, Enumerable.Range(0, count).Select(i => new ResidualFileEvidence("f" + i,
            @"C:\Fixture\configuracao-" + i + ".cfg", 0, new string('A', 64), new(1, (ulong)i, 1, 0, 0), [])).ToArray(),
            [new("d0", @"C:\Fixture", [])], [new("k0", RegistryHive.LocalMachine, RegistryView.Registry32,
                @"Software\AplicativoDeTeste", new string('A', 64), 1)], 4, true, []);
    }
    internal static void Run()
    {
        var plans = new[] { Plan("Primeiro aplicativo", 160), Plan("Segundo aplicativo", 20) };
        foreach (var size in new[] { new Size(1280, 720), new Size(1366, 768), new Size(1440, 900),
            new Size(1600, 900), new Size(1920, 1080), new Size(2560, 1440), new Size(3840, 2160),
            new Size(5120, 2880), new Size(7680, 4320), new Size(3440, 1440), new Size(5120, 1440) })
        foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            var area = new Rectangle(-size.Width, 30, size.Width, size.Height - 40);
            var fitted = UninstallCleanupDialog.FitBounds(new(area.Right - 100, area.Bottom - 100,
                (int)(620 * scale), (int)(460 * scale)), area);
            Require(area.Contains(fitted), "Confirmação precisa caber em monitor deslocado e DPI variável");
            using var dialog = new UninstallCleanupDialog(plans);
            dialog.Show(); Application.DoEvents();
            dialog.ClientSize = new Size(fitted.Width - 16, fitted.Height - 40); Application.DoEvents();
            var buttons = Children(dialog).OfType<Button>().ToArray();
            Require(buttons.Length == 2 && buttons.Select(b => b.Text).Order().SequenceEqual(new[] { "NÃO", "SIM" }.Order()),
                "Confirmação integrada deve oferecer apenas SIM e NÃO");
            foreach (var button in buttons)
            {
                var bounds = dialog.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
                Require(dialog.ClientRectangle.Contains(bounds) && button.Parent!.ClientRectangle.Contains(button.Bounds),
                    $"Botão cortado em {size}, escala {scale}: {button.Name}");
            }
            var summary = Children(dialog).OfType<DarkLogView>().Single();
            Require(summary.RenderedText.Contains("Primeiro aplicativo") && summary.RenderedText.Contains("Segundo aplicativo") &&
                summary.RenderedText.Contains("32 bits") && summary.RenderedText.Contains("Preservados: 4") &&
                summary.RenderedText.Contains("permanentemente") && dialog.AcceptButton == buttons.Single(b => b.Text == "NÃO"),
                "Prévia deve separar apps, informar chaves/contagens/preservação e usar NÃO como padrão");
            Require(dialog.BackColor.R < 80 && summary.BackColor.R < 80,
                "Confirmação deve conservar tema escuro");
            if (scale == 1.0)
            {
                Directory.CreateDirectory("LayoutCaptures"); using var image = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(image, new(Point.Empty, dialog.Size)); image.Save($"LayoutCaptures/Limpeza-Integrada-{size.Width}x{size.Height}.png");
            }
            dialog.Close();
        }
        using (var host = new MainForm(startServices: false))
        {
            host.Show(); Application.DoEvents();
            Require(!Children(host).Any(c => c.Text is "Resíduos de aplicativos" or "Investigação de resíduos"),
                "Interface principal ainda oferece investigação residual independente");
            Require(typeof(MainForm).Assembly.GetType("GerenciadorAplicativos.ResidualExplorerDialog") is null &&
                typeof(MainForm).Assembly.GetType("GerenciadorAplicativos.ResidualAuditDialog") is null,
                "Janelas antigas não devem permanecer no aplicativo");
            Require(typeof(MainForm).Assembly.GetType("GerenciadorAplicativos.SmallAppResidualCleanup") is null &&
                typeof(MainForm).Assembly.GetType("GerenciadorAplicativos.SevenZipCompleteCleanup") is null,
                "Executores antigos devem permanecer somente nas regressões, sem duplicar o motor distribuído");
            Require(Children(host).Any(c => c.Text == "Limpeza avançada") && Children(host).Any(c => c.Text == "Esvaziar Lixeira"),
                "Remoção da investigação não pode eliminar ferramentas de limpeza do Windows");
            VerifyTrayConfirmation(host, plans);
            host.Close();
        }
        Console.WriteLine("OK WORK10 UI: 44 combinações, SIM/NÃO, privacidade visual, botões antigos removidos, bandeja e cancelamento");
    }

    internal static void VerifyTrayConfirmation()
    {
        using var host = new MainForm(startServices: false);
        host.Show();
        Application.DoEvents();
        VerifyTrayConfirmation(host, [Plan("Primeiro aplicativo"), Plan("Segundo aplicativo")]);
        host.Close();
    }

    private static void VerifyTrayConfirmation(MainForm host, IntegratedResidualPlan[] plans)
    {
        var shell = Children(host).OfType<V269Shell>().Single();
        host.MinimizeToTray();
        var task = host.ConfirmUninstallCleanupAsync(plans, default);
        Require(host.HasPendingUninstallConfirmation &&
            shell.RightPane.Current?.Kind != V269PanelKind.Confirmation,
            "Operação na bandeja não pode abrir confirmação inesperada");
        host.RestoreFromTray();
        var decline = shell.RightPane.Controls.Find("V269CancelConfirmation", true).OfType<Button>().Single();
        Until(() => shell.RightPane.Current?.Kind == V269PanelKind.Confirmation &&
            decline.Visible && decline.Enabled && decline.CanSelect,
            $"Recusa indisponível ao restaurar (janela={host.Visible}, painel={shell.RightPane.Visible}, " +
            $"botão visível={decline.Visible}, habilitado={decline.Enabled})");
        Require(shell.RightPane.Current?.Description.Contains("Primeiro aplicativo") == true &&
            shell.RightPane.Current?.Description.Contains("Segundo aplicativo") == true &&
            !Application.OpenForms.OfType<UninstallCleanupDialog>().Any(),
            "O painel conserva a prévia completa sem janela auxiliar");
        decline.PerformClick();
        Until(() => task.IsCompleted, "Recusa no painel não concluiu a operação");
        Require(!task.GetAwaiter().GetResult() && !host.HasPendingUninstallConfirmation &&
            shell.RightPane.Current?.Kind != V269PanelKind.Confirmation,
            "Uma única decisão deve consumir a confirmação pendente");
        host.MinimizeToTray(); using var cancel = new CancellationTokenSource();
        var cancelled = host.ConfirmUninstallCleanupAsync(plans, cancel.Token); cancel.Cancel();
        Until(() => cancelled.IsCompleted, "Cancelamento na bandeja não concluiu");
        Require(cancelled.IsCanceled && !host.HasPendingUninstallConfirmation, "Cancelamento deve descartar confirmação sem apagar");
        host.RestoreFromTray(); Application.DoEvents();
        Require(shell.RightPane.Current?.Kind != V269PanelKind.Confirmation &&
            !Application.OpenForms.OfType<UninstallCleanupDialog>().Any(),
            "Pergunta cancelada foi repetida após restauração");
    }

}
