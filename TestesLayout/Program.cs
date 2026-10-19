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
            // A geometria do monitor e simulada sem alterar resolucao do runner.
            // A tela virtual 8K nao precisa criar bitmaps/GDI de 5 mil pixels:
            // testar limites no modelo e rolagem no viewport acessivel.
            var monitor = new Rectangle(0, 0, resolution.Width,
                Math.Max(300, resolution.Height - 48));
            var opening = StartupWindowSizing.Fit(monitor);
            surface.Size = new Size(
                Math.Clamp(opening.Width - 16, 520, 1400),
                Math.Clamp(opening.Height - 39, 400, 1040));
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
        var referenceArea = new Rectangle(0, 0, 1440, 852);
        var reference = StartupWindowSizing.Fit(referenceArea);
        if (reference.Size != new Size(1008, 698) ||
            reference.X != 216 || reference.Y != 77)
            throw new Exception("1440x900: dimensao deve acompanhar area util e ocupar cerca de 57%.");
        var wxga = new Rectangle(0, 0, 1366, 720);
        var wxgaWindow = StartupWindowSizing.Fit(wxga);
        if (wxgaWindow.Size != new Size(956, 590))
            throw new Exception("1366x768: nao abriu com tamanho proporcional.");
        var lowResolution = new Rectangle(0, 0, 800, 552);
        var lowWindow = StartupWindowSizing.Fit(lowResolution);
        if (!StartupWindowSizing.ApplyAreaLimit(lowResolution) ||
            lowWindow.Size != new Size(560, 452))
            throw new Exception("800x600 tambem deve respeitar a regra universal de 60%.");
        var hugeArea = new Rectangle(0, 0, 2560, 1392);
        var quadHd = StartupWindowSizing.Fit(hugeArea);
        if (quadHd.Size != new Size(1792, 1141))
            throw new Exception("2560x1440 deve ampliar a janela proporcionalmente; DPI nao define resolucao.");
        if (quadHd.Width <= reference.Width || quadHd.Height <= reference.Height)
            throw new Exception("Janela em 1440p permanece artificialmente pequena.");
        var resized = StartupWindowSizing.KeepVisible(
            new Rectangle(0, 0, 4000, 4000), hugeArea);
        if (resized.Width * (long)resized.Height >
            3L * hugeArea.Width * hugeArea.Height / 5L)
            throw new Exception("Troca de monitor permitiu exceder o limite de 60%.");
        var largestArea = new Rectangle(0, 0, 7680, 4272);
        if (StartupWindowSizing.Fit(largestArea).Width <= quadHd.Width)
            throw new Exception("8K nao está utilizando proporcionalmente a area do monitor.");
        foreach (var (name, resolution) in StartupWindowSizing.ValidationMatrix)
        {
            // Simular barra de tarefas de 48 pixels e area util com origem
            // deslocada, como ocorre em um monitor secundario.
            var workArea = new Rectangle(100, 64, resolution.Width, resolution.Height - 48);
            Rectangle opening = StartupWindowSizing.Fit(workArea);
            long pixels = (long)opening.Width * opening.Height;
            long available = (long)workArea.Width * workArea.Height;
            if (!StartupWindowSizing.ApplyAreaLimit(workArea) ||
                pixels > 3L * available / 5L)
                throw new Exception($"Area acima de 60%: {name} {opening}");
            if (pixels < 55L * available / 100L)
                throw new Exception($"Janela ficou pequena demais: {name} {opening}");
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
            Size maximum = StartupWindowSizing.MaximumSizeFor(workArea);
            if (opening.Width > maximum.Width || opening.Height > maximum.Height)
                throw new Exception($"Janela ultrapassou tamanho maximo: {name}");
            if ((long)maximum.Width * maximum.Height > 3L * available / 5L)
                throw new Exception($"Redimensionamento excederia 60%: {name}");
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
