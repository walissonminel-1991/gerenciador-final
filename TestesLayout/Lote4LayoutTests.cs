using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// Exercita o caminho real de Configurações, usando exclusivamente contagens
// sintéticas e um arquivo descartável; nunca abre o seletor de arquivo.
internal static class Lote4LayoutTests
{
    internal static void Run()
    {
        using var owner = new MainForm(startServices: false);
        owner.Show();
        Application.DoEvents();
        var history = (LocalReliabilityHistory)typeof(MainForm)
            .GetField("_reliability", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        const string secret = @"C:\Users\PRIVATE\nao-publicar.exe";
        for (int i = 0; i < 150; i++)
            history.RecordUninstall(new(UninstallStage.Pending, secret, secret));
        string raw = history.Report([], false);
        string expected = DarkLogView.NormalizeWindowsLineBreaks(raw);
        var shell = Descendants(owner).OfType<V269Shell>().Single();
        shell.SelectPage(V269Page.Configuracoes);
        shell.PresentPanel(null);
        Application.DoEvents();
        var open = Descendants(owner).OfType<Button>().Single(b => b.Name == "V269History");
        if (!open.Visible) throw new Exception("L4 histórico de registro inacessível em Configurações.");
        void Open()
        {
            // O negativo histórico substitui o método de produção completo.
            // Examinar de fato o controle antigo e fechar o modal para que a
            // mutação não transforme o teste em timeout.
            Exception? modalFailure = null;
            var existing = Application.OpenForms.Cast<Form>().ToArray();
            using var watchdog = new System.Windows.Forms.Timer { Interval = 50 };
            watchdog.Tick += (_, _) =>
            {
                foreach (var dialog in Application.OpenForms.Cast<Form>().Where(f => !existing.Contains(f)).ToArray())
                {
                    var native = Descendants(dialog).OfType<TextBox>().SingleOrDefault(c => c.Name == "LocalReliabilityReportText");
                    modalFailure = native is { ScrollBars: not ScrollBars.None }
                        ? new Exception("L4 relatório local usa rolagem nativa em vez da prévia escura acessível.")
                        : new Exception("L4 histórico retornou a uma janela modal não autorizada.");
                    dialog.Close();
                }
            };
            watchdog.Start();
            open.PerformClick(); Application.DoEvents(); watchdog.Stop();
            if (modalFailure is not null) throw modalFailure;
            if (!shell.RightPane.ReadOnlyViewActive || !shell.RightPane.Visible ||
                Application.OpenForms.Count != existing.Length)
                throw new Exception("L4 relatório não está no painel direito real.");
        }
        Open();
        if (shell.RightPane.Current?.Kind != V269PanelKind.History)
            throw new Exception("L4 histórico não apresentado no painel antes da exportação.");
        var surface = owner.Controls.OfType<TableLayoutPanel>().Single();
        var savedSize = surface.Size;
        var theme = Descendants(owner).OfType<ComboBox>().Single(c=>c.Name=="V269ThemeChoice");
        int savedTheme = theme.SelectedIndex;
        string folder = Path.Combine(Path.GetTempPath(), "GF-L4-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder,"GF-Relatorio-Local.txt");
        try
        {
            Directory.CreateDirectory("LayoutCaptures");
            foreach (var size in new[] { new Size(992,659), new Size(1200,659) })
            foreach (var palette in Enum.GetValues<V269Theme>())
            {
                surface.Size = size; theme.SelectedIndex=(int)palette;
                surface.PerformLayout(); Application.DoEvents();
                var view = Descendants(shell.RightPane).Single(c => c.Name == "V269ReadOnlyView");
                var body = Descendants(view).OfType<DarkLogView>()
                    .SingleOrDefault(c => c.Name == "LocalReliabilityReportText")
                    ?? throw new Exception("L4 relatório local usa rolagem nativa em vez da prévia escura acessível.");
                var editor = Descendants(body).OfType<TextBox>().Single();
                if (!editor.ReadOnly || !editor.WordWrap || editor.ScrollBars != ScrollBars.None ||
                    body.RenderedText != expected || body.RenderedText.Contains(secret))
                    throw new Exception("L4 relatório alterado, truncado, editável ou com identificador privado.");
                editor.SelectAll();
                if (editor.SelectedText != expected)
                    throw new Exception("L4 seleção não preservou o texto completo para cópia.");
                editor.Select(0,0);
                var scroll = Descendants(body).OfType<DarkVScrollBar>().Single();
                if (!scroll.Visible || scroll.MaximumValue <= 0 ||
                    !body.RectangleToScreen(body.ClientRectangle).Contains(scroll.RectangleToScreen(scroll.ClientRectangle)))
                    throw new Exception("L4 relatório longo sem barra escura acessível.");
                if (editor.RectangleToScreen(editor.ClientRectangle).IntersectsWith(
                    scroll.RectangleToScreen(scroll.ClientRectangle)))
                    throw new Exception("L4 editor cobre a barra de rolagem escura.");
                scroll.Value = scroll.MaximumValue;
                Application.DoEvents();
                if (scroll.Value == 0 || body.RenderedText != expected)
                    throw new Exception("L4 não alcança o fim do histórico ou rolagem altera o relatório.");
                scroll.Value = 0;
                Application.DoEvents();
                using (var capture = new Bitmap(view.Width, view.Height))
                {
                    view.DrawToBitmap(capture, new Rectangle(Point.Empty, view.Size));
                    capture.Save($"LayoutCaptures/lote4-historico-{size.Width}x{size.Height}-{palette}.png");
                }
                foreach (var button in Descendants(view).OfType<Button>())
                    if (!view.RectangleToScreen(view.ClientRectangle).Contains(button.RectangleToScreen(button.ClientRectangle)) ||
                        button.Width < 90 || button.Height < 26)
                        throw new Exception("L4 ação de relatório fora do viewport compacto.");
            }
            var save = Descendants(shell.RightPane).OfType<Button>().Single(b=>b.Name=="SaveLocalReliabilityReport");
            owner.V269ReliabilityExportPathForTest=()=>null;
            save.PerformClick();
            if (Directory.GetFiles(folder).Length!=0)
                throw new Exception("L4 cancelamento não preservou ausência de arquivo.");
            owner.V269ReliabilityExportPathForTest=()=>Path.Combine(folder,"inexistente","report.txt");
            save.PerformClick();
            if (Directory.GetFiles(folder).Length!=0 || !shell.RightPane.ReadOnlyViewActive ||
                !Descendants(owner).OfType<Label>().Single(c=>c.Name=="V269SettingsHint").Text.Contains("Relatório não salvo"))
                throw new Exception("L4 falha de gravação não foi apresentada ou perdeu a consulta.");
            owner.V269ReliabilityExportPathForTest=()=>file;
            save.PerformClick();
            if (!File.Exists(file) || File.ReadAllText(file)!=raw || File.ReadAllText(file).Contains(secret) ||
                File.ReadAllBytes(file).Take(3).SequenceEqual(new byte[] {0xEF,0xBB,0xBF}))
                throw new Exception("L4 exportação não preservou texto/privacidade.");
            Descendants(shell.RightPane).OfType<Button>().Single(b=>b.Name=="V269ReadOnlyClose").PerformClick();
            Application.DoEvents();
            if (shell.RightPane.ReadOnlyViewActive || shell.RightPane.Current?.Kind!=V269PanelKind.History)
                throw new Exception("L4 retorno não preservou histórico.");
            var result = new V269PanelPresentation(V269PanelKind.Result,"Anterior","Resultado preservado");
            shell.PresentPanel(result);
            Open();
            if (!ReferenceEquals(shell.RightPane.Current,result))
                throw new Exception("L4 histórico substituiu resultado anterior.");
            // Exercitar também a rota do comando de teclado da MainForm,
            // sem injetar teclas em outra janela do runner.
            var processKey = typeof(MainForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var message = Message.Create(owner.Handle, 0x0100, (IntPtr)Keys.Escape, IntPtr.Zero);
            if (processKey.Invoke(owner, [message, Keys.Escape]) is not true)
                throw new Exception("L4 comando Esc não foi tratado.");
            Application.DoEvents();
            if (shell.RightPane.ReadOnlyViewActive || !ReferenceEquals(shell.RightPane.Current,result) || !open.Focused)
                throw new Exception("L4 retorno por Esc perdeu resultado anterior ou foco.");
        }
        finally
        {
            owner.V269ReliabilityExportPathForTest=null;
            shell.PresentPanel(null);theme.SelectedIndex=savedTheme;surface.Size=savedSize;
            Directory.Delete(folder,true);
        }
        Console.WriteLine("PASS L4 relatório real: painel, dois viewports, três temas, rolagem, texto íntegro, privacidade, exportação/cancelamento/falha e retorno.");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
