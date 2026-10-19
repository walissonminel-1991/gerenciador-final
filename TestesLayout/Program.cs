using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class LayoutTests
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        using var deadline = new System.Threading.Timer(
            _ => Environment.FailFast("Timeout no teste de layout"),
            null, TimeSpan.FromMinutes(4), Timeout.InfiniteTimeSpan);
        Application.EnableVisualStyles();

        TestStartupBounds();
        TestWorkspaceProportions();

        Console.WriteLine("Criando janela de teste, sem iniciar os servicos");
        using var form = new MainForm(startServices: false);
        if (form.MaximizeBox || form.FormBorderStyle != FormBorderStyle.None ||
            !form.HasCustomCaption)
            throw new Exception("Barra da janela nao pode mostrar botao de maximizar.");
        form.Show();
        // O PC FRACO nao pode receber WS_EX_COMPOSITED global: esse estilo
        // transforma cada repintura nativa numa composicao de toda a arvore.
        if ((GetWindowLong(form.Handle, -20) & 0x02000000) != 0)
            throw new Exception("Composicao global pesada continua habilitada.");
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        surface.Dock = DockStyle.None;
        // O log recebe muitas mensagens na inicializacao. Validar que cada
        // uma chega completa, mas a interface faz uma so atualizacao visual.
        var log = Descendants(form).OfType<DarkLogView>().Single();
        _ = log.RenderedText; // Drenar mensagens do construtor, se houver.
        int flushesBefore = log.BatchFlushCount;
        for (int i = 0; i < 70; i++)
            log.AppendText($"Lote de diagnostico {i:D2}\n");
        if (log.BatchFlushCount != flushesBefore)
            throw new Exception("Registro redesenhou antes de consolidar a rajada.");
        string allLines = log.RenderedText;
        if (log.BatchFlushCount != flushesBefore + 1 || !allLines.Contains("Lote de diagnostico 00") ||
            !allLines.Contains("Lote de diagnostico 69") ||
            allLines.Split("Lote de diagnostico ").Length != 71)
            throw new Exception("Registro perdeu linhas ou redesenhou por mensagem.");
        Console.WriteLine("OK: 70 linhas em uma pintura do Registro.");
        var nav = Descendants(form).OfType<DarkTabHost>().First();
        Directory.CreateDirectory("LayoutCaptures");

        int tested = 0;
        // 21 resolucoes da matriz e a referencia real de 1440x900 do usuario.
        var scenarios = StartupWindowSizing.ValidationMatrix
            .Concat(new[] { ("Referencia 1440x900", new Size(1440, 900)) });
        foreach (var (name, resolution) in scenarios)
        {
            // O runner Windows pode ter desktop fisico de 1024x768. Usamos uma
            // superficie virtual na resolucao alvo, sem mudar o monitor do runner.
            // Testar a janela real, que nao ocupa 8K inteiro: a resolucao
            // alta aumenta espaco no monitor, nao fontes e botoes da UI.
            var monitor = new Rectangle(0, 0, resolution.Width,
                Math.Max(300, resolution.Height - 48));
            var opening = StartupWindowSizing.Fit(monitor);
            surface.Size = new Size(opening.Width - 16, opening.Height - 39);
            Application.DoEvents();

            bool capture = (resolution.Width <= 1366 &&
                (resolution.Height <= 800 || resolution == new Size(1280, 1024))) ||
                resolution == new Size(1440, 900);
            foreach (int tab in new[] { 0, 1, 2, 3, 4 })
            {
                nav.SelectTab(tab);
                Application.DoEvents();

                // Somente a pagina pode rolar quando o conteudo ultrapassar
                // seu viewport; ferramentas e discos embutidos jamais mostram
                // uma segunda barra vertical.
                foreach (var flow in Descendants(form).OfType<DarkScrollableFlow>())
                {
                    if (flow.EmbeddedInPage && flow.HasVerticalScrollbar)
                        throw new Exception($"Rolagem duplicada: {name} aba={tab}");
                    if (flow.IsPageViewport && flow.Visible &&
                        flow.Content.Controls.Count == 1 &&
                        flow.Content.Controls[0].Height <= flow.ClientSize.Height &&
                        flow.HasVerticalScrollbar)
                        throw new Exception($"Rolagem fantasma: {name} aba={tab}");
                }

                var allButtons = Descendants(form).OfType<Button>()
                    .Where(b => b.Visible).ToArray();
                // A aba Energia so habilita seus comandos depois de consultar
                // o Windows. No teste isolado sem servicos eles devem continuar
                // visiveis, mas desabilitados por seguranca.
                if (allButtons.Length == 0)
                    throw new Exception($"Nenhum botao apresentado: {name} {resolution} aba={tab}");
                var buttons = allButtons.Where(b => b.Enabled).ToArray();

                // Navegacao somente: nao clicar nem executar comandos destrutivos.
                foreach (var button in buttons)
                {
                    if (!button.Focus())
                        throw new Exception($"Botao sem foco: {resolution} aba={tab} {button.Text}");
                    Application.DoEvents();
                    if (!FullyVisible(button))
                        throw new Exception($"Botao inacessivel: {resolution} aba={tab} {button.Text}");
                }

                if (capture)
                {
                    // Nao criar bitmaps gigantes (5K/8K); nesses casos a
                    // cobertura verifica geometria, foco e navegabilidade.
                    using var shot = new Bitmap(surface.Width, surface.Height);
                    surface.DrawToBitmap(shot, new Rectangle(Point.Empty, surface.Size));
                    shot.Save($"LayoutCaptures/{resolution.Width}x{resolution.Height}-aba{tab}.png");
                }
                Console.WriteLine($"OK {name} {resolution.Width}x{resolution.Height} aba={tab}: {buttons.Length} botoes");
            }
            tested++;
        }
        if (tested != 22) throw new Exception($"Esperados 22 cenarios; encontrados {tested}");
        // Provar no runner Windows que o botao Minimizar oculta a janela
        // e que Abrir restaura, sem encerrar a coleta nem criar janelas.
        IntPtr originalHandle = form.Handle;
        int originalTab = nav.SelectedIndex;
        var duration = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 6; i++)
        {
            form.MinimizeToTray();
            Application.DoEvents();
            // ShowInTaskbar permanece true para nao destruir o HWND.
            // A janela oculta desaparece da barra de tarefas por Hide().
            if (form.Visible || !form.ShowInTaskbar || !form.IsInTray ||
                !form.HasActiveTrayIcon || form.Handle != originalHandle)
                throw new Exception("Minimizar recriou a janela ou falhou na bandeja.");
            form.RestoreFromTray();
            Application.DoEvents();
            if (!form.Visible || !form.ShowInTaskbar || form.IsInTray ||
                form.HasActiveTrayIcon || form.Handle != originalHandle ||
                nav.SelectedIndex != originalTab)
                throw new Exception("Restaurar recriou o HWND ou perdeu o layout selecionado.");
        }
        duration.Stop();
        Console.WriteLine($"OK: 6 ciclos de bandeja, HWND estavel, {duration.ElapsedMilliseconds} ms no runner");
        // Layout util em espaco limitado: os itens e botoes devem permanecer
        // focaveis mesmo quando as paginas fazem rolagem.
        nav.SelectTab(0);
        var commandButtons = Descendants(form).OfType<ModernButton>()
            .Where(x => x.Visible && !x.IsDisposed).ToArray();
        if (commandButtons.Any(x => x.Text.Contains('\n')))
            throw new Exception("Botoes ainda carregam quebra de linha forcada.");
        var programList = Descendants(form).OfType<DarkProgramList>().Single();
        if (programList.ItemHeight > 70)
            throw new Exception("Lista de programas ainda usa linhas excessivamente altas.");
        nav.SelectTab(3);
        Application.DoEvents();
        var activateButtons = Descendants(form).OfType<ModernButton>()
            .Where(b => b.Text == "Ativar" && b.Parent is TableLayoutPanel).ToArray();
        if (activateButtons.Length != 3 ||
            activateButtons.Any(b => b.Width > 130 || b.Height > 33))
            throw new Exception("Botoes Ativar ainda demasiadamente largos.");
        nav.SelectTab(2);
        Application.DoEvents();
        var repairTools = Descendants(form).OfType<ModernButton>()
            .Where(b => b.Text is "Analisar rede" or "Reparar rede" or
                "Criar ponto de restauração").ToArray();
        if (repairTools.Length != 3 || repairTools.Any(b => b.Width > 215))
            throw new Exception("Botoes de ferramentas ainda ocupam cartoes inteiros.");
        nav.SelectTab(4);
        Application.DoEvents();
        var pageViewports = Descendants(form).OfType<DarkScrollableFlow>()
            .Where(flow => flow.IsPageViewport).ToArray();
        if (pageViewports.Length != 4)
            throw new Exception($"Esperadas quatro paginas com rolagem controlada; encontradas {pageViewports.Length}");
        var systemCards = Descendants(form).OfType<DarkScrollableFlow>()
            .SelectMany(flow => flow.Content.Controls.Cast<Control>());
        if (systemCards.Any(c => c is TableLayoutPanel panel &&
            panel.BackColor == Color.FromArgb(38, 50, 63) && panel.Height > 150))
            throw new Exception("Cartoes do sistema extrapolam layout compacto.");
        form.Close();
        Console.WriteLine($"PASSOU: {tested} cenarios de tela, bandeja, foco e fechamento.");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr handle, int index);

    private static void TestStartupBounds()
    {
        if (StartupWindowSizing.ValidationMatrix.Length != 21)
            throw new Exception("A matriz precisa de 21 resolucoes.");
        var reference = StartupWindowSizing.Fit(new Rectangle(0, 0, 1440, 852));
        if (reference.Size != new Size(875, 745) ||
            reference.X != 282 || reference.Y != 53)
            throw new Exception("1440x900 deve abrir em 875x745, centralizado e fora da barra de tarefas.");
        var wxga = new Rectangle(0, 0, 1366, 720);
        var wxgaWindow = StartupWindowSizing.Fit(wxga);
        if ((long)wxgaWindow.Width * wxgaWindow.Height >
            (long)wxga.Width * wxga.Height * 3 / 5)
            throw new Exception("1366x768 excedeu 60% da area util.");
        if (wxgaWindow.Width != 875 || wxgaWindow.Height != 674)
            throw new Exception("1366x768 deve limitar somente a altura, sem ampliar botoes.");
        var lowResolution = new Rectangle(0, 0, 1024, 720);
        if (StartupWindowSizing.ApplyAreaLimit(lowResolution) ||
            StartupWindowSizing.Fit(lowResolution).Size != new Size(875, 720))
            throw new Exception("A regra de 60% nao pode bloquear resolucoes antigas.");
        var huge = StartupWindowSizing.KeepVisible(
            new Rectangle(0, 0, 2000, 2000), wxga);
        if ((long)huge.Width * huge.Height > (long)wxga.Width * wxga.Height * 3 / 5)
            throw new Exception("Trocar de monitor nao pode ultrapassar 60%.");
        var large = StartupWindowSizing.Fit(new Rectangle(0, 0, 7680, 4272));
        if (large.Size != reference.Size)
            throw new Exception("8K nao pode ampliar os botoes ou a janela inicial.");
        foreach (var (name, resolution) in StartupWindowSizing.ValidationMatrix)
        {
            // Simular barra de tarefas de 48 pixels e area util com origem
            // deslocada, como ocorre em um monitor secundario.
            var workArea = new Rectangle(100, 64, resolution.Width, resolution.Height - 48);
            Rectangle opening = StartupWindowSizing.Fit(workArea);
            if (StartupWindowSizing.ApplyAreaLimit(workArea) &&
                (long)opening.Width * opening.Height >
                    3L * workArea.Width * workArea.Height / 5L)
                throw new Exception($"Area acima de 60%: {name} {opening}");
            if (!workArea.Contains(opening) ||
                opening.Width > workArea.Width || opening.Height > workArea.Height)
                throw new Exception($"Janela inicial fora da tela: {name} {opening}");
            Size min = StartupWindowSizing.AllowedMinimum(opening);
            if (min.Width > opening.Width || min.Height > opening.Height)
                throw new Exception($"Tamanho minimo impede abertura: {name}");
            var outside = new Rectangle(-3000, -3000, 2000, 1400);
            Rectangle recovered = StartupWindowSizing.KeepVisible(outside, workArea);
            if (!workArea.Contains(recovered))
                throw new Exception($"Nao recuperou a janela apos troca de monitor: {name}");
            // Tamanhos de janela sao limitados: nunca escalar a interface
            // proporcionalmente a resolucao nominal de 4K/5K/8K.
            if (opening.Width > 875 || opening.Height > 745)
                throw new Exception($"Janela cresceu com a resolucao: {name}");
            // Ao mover para monitor menor, o tamanho e limitado sem alterar os
            // limites das janelas ja validas.
            if (StartupWindowSizing.KeepVisible(opening, workArea) != opening)
                throw new Exception($"Posicao valida alterada: {name}");
            Console.WriteLine($"Fit OK {name}: {resolution.Width}x{resolution.Height}");
        }
    }

    private static void TestWorkspaceProportions()
    {
        foreach (int dpi in new[] { 96, 120, 144, 192 })
        foreach (int height in new[] { 480, 560, 680, 900, 1100 })
        {
            var (header, log) = StartupWindowSizing.WorkspaceRows(height, dpi);
            if (header <= 0 || log < 95 || height - header - log < 140)
                throw new Exception($"Layout sem registro legivel ou area de ferramentas: {height} dpi={dpi}, header={header}, log={log}");
            if (dpi == 96 && height >= 560 && log < 130)
                throw new Exception($"Registro demasiadamente fino: {height}, log={log}");
        }
        Console.WriteLine("OK: proporcoes de registro/cabecalho em 100%, 125%, 150% e 200%.");
    }

    static bool FullyVisible(Control control)
    {
        Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
        for (Control? parent = control.Parent; parent != null && parent is not Form; parent = parent.Parent)
            if (!parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds))
            {
                Console.WriteLine($"CLIP {control.Text} tela={bounds} pai={parent.GetType().Name} area={parent.RectangleToScreen(parent.ClientRectangle)}");
                return false;
            }
        return bounds.Width > 0 && bounds.Height > 0;
    }

    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child)) yield return nested;
        }
    }
}
