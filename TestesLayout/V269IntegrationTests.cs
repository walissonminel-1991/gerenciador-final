using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class V269IntegrationTests
{
    internal static void Run(bool includeMaintenanceVisual = true)
    {
        VerifyDirectDisposeLifetime();
        using var form = new MainForm(startServices: false);
        form.Show();
        Application.DoEvents();
        var shell = form.Controls.Find("V269Shell", true).OfType<V269Shell>().Single();
        V269RootGeometryProbe.VerifyCurrent(form, shell);
        var pages = Enum.GetValues<V269Page>();
        var views = pages.Select(page => shell.PageContent(page) ??
            throw new InvalidOperationException($"View ausente: {page}")).ToArray();
        Require(views.Distinct().Count() == 8 && shell.SelectedPage == V269Page.Inicio,
            "oito instâncias nativas com Início ativo");
        Timed("Navegação sequencial nativa", () => VerifySequentialNavigation(form, shell));
        Timed("Medição de viewports ao ocultar", () => VerifyViewportVisibilityMeasurements(form,shell));
        Timed("Dock do viewport externo", VerifyPageViewportDock);
        Timed("Feedback independente", VerifyIndependentFeedback);
        Timed("Barras universais", VerifyUniversalMeterPainting);
        Timed("REC-FIS-PAINT recorte parcial de slider", VerifySettingsRangePaintClip);
        Timed("REC-FIS-GLYPH comandos indisponíveis", VerifyDisabledCommandGlyph);
        Timed("REC-FIS-PAINT texto recortado e transladado", VerifyTranslatedTextPaint);
        Timed("REC-FIS-PAINT arte de Início com recorte parcial", VerifyHomeArtPaintClip);
        Timed("REC-FIS-STARTUP texto de comandos", VerifyStartupCaptionLayout);
        Timed("Rolagem temática de texto", VerifyThemedTextScroll);
        VerifyNoLegacyControls();
        VerifyAppOrdering();
        VerifyLocalDiagnosticExport();
        VerifyDiagnosticCaptureCost();
        VerifyInventoryOrderingCost();
        VerifyAppTableGeometry();
        VerifyAppIconSources();
        Require(form.Controls.Find("V269WindowsUpdateHistory", true).OfType<Button>().Single().Text == "Histórico" &&
            form.Controls.Find("V269History", true).OfType<Button>().Single().Text == "Histórico de registro",
            "históricos específicos previstos no HTML, sem botão genérico compartilhado");
        var homeSnapshot = VerifyHome(form, views[0]);
        var paneToggle = form.Controls.Find("V269PaneToggle", true).OfType<Button>().Single();
        Require(paneToggle.Parent?.Name == "V269HomeFooter",
            "controle adicional não altera o menu do HTML aprovado");
        Require(!shell.RightPane.Visible && !paneToggle.Visible &&
            form.Controls.Find("V269LocalHistory", true).Length == 0 &&
            form.Controls.Find("LocalReliabilityReportButton", true).Length == 0,
            "REC-FIS-PANE: Início sem painel nem comando genérico; histórico acessível por Configurações");
        // A suíte bombeia mensagens com DoEvents sem Application.Run.
        // Fixar o contexto neste trecho para entregar as duas continuações ao STA.
        var previousContext = SynchronizationContext.Current;
        using var confirmationContext = new WindowsFormsSynchronizationContext();
        try
        {
            SynchronizationContext.SetSynchronizationContext(confirmationContext);
            var confirm = typeof(MainForm).GetMethod("ConfirmV269Async",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            var waiting = (Task<bool>)confirm.Invoke(form, ["Revisão de teste",
                "Alvo sintético. Nenhuma manutenção.", CancellationToken.None])!;
            form.Controls.Find("V269NavDrivers", true).OfType<Button>().Single().PerformClick();
            Require(form.Controls.Find("V269HomeMinimize", true).Single().Visible && !paneToggle.Visible,
                "Drivers reutiliza a barra do HTML e o acesso contextual ao painel");
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
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }

        form.ShowUninstallResult("Resultado preservado");
        foreach (var page in pages)
        {
            shell.SelectPage(page);
            Application.DoEvents();
            Require(ReferenceEquals(shell.PageContent(page), views[(int)page]) &&
                shell.RightPane.Current?.Description == "Resultado preservado",
                "identidade e resultado preservados durante navegação");
            if(page is V269Page.Inicio or V269Page.Configuracoes)
                Require(!shell.RightPane.Visible && !paneToggle.Visible,"resultado alheio retido sem painel ordinário");
        }
        // REC-FIS-02: decisão posterior do proprietário retira os dois comandos.
        // A prova dos comandos consultivos reais permanece em VerifySettings.
        // Exercitar também o contrato do painel com uma fixture somente leitura.
        shell.SelectPage(V269Page.Inicio);
        Application.DoEvents();
        Require(form.Controls.Find("V269ReadHardware", true).Length == 0 &&
            form.Controls.Find("V269HardwareDetails", true).Length == 0 &&
            !shell.RightPane.Visible && !paneToggle.Visible,
            "REC-FIS-02: Início informativo sem comandos ou painel ordinário");
        var retained = shell.RightPane.Current;
        var origin = form.Controls.Find("V269NavInicio", true).OfType<Button>().Single();
        void OpenReadOnlyFixture()
        {
            Require(shell.RightPane.TryShowReadOnly("Consulta sintética", "Fixture somente leitura",
                "V269HomeReadOnlyFixture", origin), "painel aceita consulta sem substituir resultado");
            shell.EnsureDashboardPane();
            shell.RightPane.FocusReadOnly();
            Application.DoEvents();
            Require(shell.RightPane.ReadOnlyViewActive && ReferenceEquals(shell.RightPane.Current, retained),
                "consulta preserva resultado");
        }
        OpenReadOnlyFixture();
        var readOnlyEditor = shell.RightPane.Controls.Find("V269HomeReadOnlyFixture", true).OfType<TextBox>().Single();
        Require(readOnlyEditor.Visible && readOnlyEditor.ReadOnly && readOnlyEditor.WordWrap &&
            readOnlyEditor.ScrollBars == ScrollBars.None, "consulta usa editor temático somente leitura");
        shell.RightPane.Controls.Find("V269ReadOnlyClose", true).OfType<Button>().Single().PerformClick();
        Application.DoEvents();
        Require(!shell.RightPane.ReadOnlyViewActive && !shell.RightPane.Visible &&
            ReferenceEquals(shell.RightPane.Current, retained), "fechar consulta conserva resultado e dashboard");
        OpenReadOnlyFixture();
        shell.SelectPage(V269Page.Configuracoes);
        Application.DoEvents();
        Require(!shell.RightPane.ReadOnlyViewActive && ReferenceEquals(shell.RightPane.Current, retained),
            "navegação encerra consulta sem descartar resultado");
        shell.SelectPage(V269Page.Inicio);
        Application.DoEvents();
        OpenReadOnlyFixture();
        var progress = new V269PanelPresentation(V269PanelKind.Progress, "Operação sintética", "Fixture", 25);
        shell.PresentPanel(progress);
        Require(!shell.RightPane.ReadOnlyViewActive && ReferenceEquals(shell.RightPane.Current, progress),
            "publicação de operação tem prioridade sobre consulta");
        Require(!shell.RightPane.TryShowReadOnly("Bloqueado", "Fixture", "BlockedReadOnly", origin),
            "consulta não pode cobrir progresso");
        shell.PresentPanel(retained);
        shell.SelectPage(V269Page.Configuracoes);
        Application.DoEvents();
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
        Timed("Configurações", () => VerifySettings(form,shell));
        Timed("Aplicativos", () => VerifyApps(form,shell,confirmationContext));
        void Timed(string name, Action test)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Console.WriteLine("V269-BEGIN " + name);
            test();
            Console.WriteLine($"V269-END {name} ms={clock.ElapsedMilliseconds}");
        }
        Timed("Drivers", () => VerifyDrivers(form,shell));
        if (includeMaintenanceVisual)
        {
            Timed("Limpeza", () => VerifyCleanup(form,shell));
            Timed("Reparo", () => VerifyRepair(form,shell));
        }
        else Console.WriteLine("V269: Limpeza e Reparo aprovados no percurso WORK18 desta mesma execução.");
        Timed("Sistema e Inicialização",()=>VerifyLastTabs(form,shell));
        Timed("REC-FIS-PANE inicialização silenciosa", () => VerifyQuietStartupPane(form,shell));
        Timed("Sessão contínua V269", () => VerifyContinuousSession(form, shell, homeSnapshot));
        Timed("Pintura real V269", () => VerifyLiveWindowPaint(form, shell));
        Timed("REC-FIS-02-PAINT-FRAMES", () => VerifyFramePaintLoop(form, shell));
        if (!form.IsDisposed) form.Close();
        Console.WriteLine("PASS V2.6.9 integração: MainForm ativa, oito views, confirmação, cancelamento e resultado persistente.");
    }

    // Uma sessão na mesma MainForm, sem reiniciar views entre etapas.
    // Provedores de hardware/driver e ações destrutivas não são executados:
    // o objetivo é a concorrência e publicação dos estados na interface real.
    private static void VerifyQuietStartupPane(MainForm form,V269Shell shell)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var execute=typeof(MainForm).GetMethod("ExecuteAsync",flags)!;
        var status=typeof(MainForm).GetMethod("Status",flags)!;
        var label=(Label)typeof(MainForm).GetField("_status",flags)!.GetValue(form)!;
        var saved=shell.RightPane.Current;var page=shell.SelectedPage;
        var previousContext=SynchronizationContext.Current;
        using var context=new WindowsFormsSynchronizationContext();
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            shell.SelectPage(V269Page.Inicio);
            foreach(V269PanelPresentation? prior in new V269PanelPresentation?[]{
                null,new(V269PanelKind.Result,"Resultado anterior","Fixture preservada")})
            {
                shell.PresentPanel(prior);
                var pending=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                const string observed="7 aplicativos com ação disponíveis antes da consulta online";
                bool completed=false;
                Func<Task> work=async ()=>
                {
                    status.Invoke(form,[observed]);
                    await pending.Task;
                    status.Invoke(form,[observed]);completed=true;
                };
                var operation=(Task)execute.Invoke(form,["Carregando programas instalados...",work,true])!;
                try
                {
                    Require(!operation.IsCompleted && ReferenceEquals(shell.RightPane.Current,prior) &&
                        !shell.RightPane.Visible && label.AccessibleDescription==observed,
                        "REC-FIS-STARTUP: Status silencioso não cria progresso órfão, mantém informação acessível");
                    shell.SelectPage(V269Page.Configuracoes);Application.DoEvents();
                    Require(!shell.RightPane.Visible && ReferenceEquals(shell.RightPane.Current,prior),
                        "inventário silencioso não reabre painel em Configurações");
                }
                finally{pending.TrySetResult(true);Pump(operation);}
                Require(completed && ReferenceEquals(shell.RightPane.Current,prior) && !shell.RightPane.Visible,
                    "REC-FIS-STARTUP: fim silencioso conserva estado anterior sem progresso órfão");
                shell.SelectPage(V269Page.Inicio);
            }
            shell.PresentPanel(null);shell.SelectPage(V269Page.Configuracoes);
            bool observedProgress=false;
            Func<Task> explicitFixture=()=>
            {
                status.Invoke(form,["Andamento explícito sintético"]);
                observedProgress=shell.RightPane.Visible&&shell.RightPane.Current is
                    {Kind:V269PanelKind.Progress,Description:"Andamento explícito sintético"};
                return Task.CompletedTask;
            };
            Pump((Task)execute.Invoke(form,["Operação explícita sintética",explicitFixture,false])!);
            Require(observedProgress,"proteção negativa: operação explícita continua apresentando progresso");
            Console.WriteLine("PASS REC-FIS-STARTUP: carga silenciosa sem painel órfão; progresso explícito preservado.");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            shell.SelectPage(page);shell.PresentPanel(saved);
        }
    }

    private static void VerifyContinuousSession(MainForm form, V269Shell shell, HardwareSensorPortfolio snapshot)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var views = Enum.GetValues<V269Page>().ToDictionary(p => p, p => shell.PageContent(p)!);
        var buttons = views.Keys.ToDictionary(p => p,
            p => form.Controls.Find("V269Nav" + p, true).OfType<Button>().Single());
        var apps = (DarkProgramList)typeof(MainForm).GetField("_programList", flags)!.GetValue(form)!;
        var installed = (List<ProgramEntry>)typeof(MainForm).GetField("_installed", flags)!.GetValue(form)!;
        var render = typeof(MainForm).GetMethod("RenderPrograms", flags)!;
        var search = form.Controls.Find("V269AppsSearch", true).OfType<TextBox>().Single();
        var drivers = form.Controls.Find("DriverInventoryList", true).OfType<DarkDriverList>().Single();
        var theme = form.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
        var motion = form.Controls.Find("V269Animations", true).OfType<CheckBox>().Single();
        var home = (V269HomeDashboard)views[V269Page.Inicio];
        var cardIds = home.Cards.ToArray();
        var savedModels = cardIds.Select(c => c.Model!).ToArray();
        var savedApps = installed.ToArray();
        var savedDrivers = drivers.Items.ToArray();
        var savedSearch = search.Text;
        var savedPage = shell.SelectedPage;
        var savedPanel = shell.RightPane.Current;
        var savedTheme = theme.SelectedIndex;
        var savedMotion = motion.Checked;
        var savedContext = SynchronizationContext.Current;
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        var savedSize = surface.Size;
        var timings = new List<double>();
        void Step(string label, Action action)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            action();
            Application.DoEvents();
            timings.Add(clock.Elapsed.TotalMilliseconds);
            Require(views.All(pair => ReferenceEquals(pair.Value, shell.PageContent(pair.Key)) &&
                pair.Value.Visible == (pair.Key == shell.SelectedPage)),
                "sessão perdeu uma view: " + label);
            Require(home.Cards.SequenceEqual(cardIds), "sessão recriou cartões: " + label);
            Console.WriteLine(FormattableString.Invariant(
                $"V269-SESSION {label} ms={clock.Elapsed.TotalMilliseconds:F2}"));
        }
        void Go(V269Page page)
        {
            buttons[page].PerformClick();
            Require(shell.SelectedPage == page, "navegação não imediata: " + page);
        }
        void Wheel(Control control, int delta) =>
            control.GetType().GetMethod("OnMouseWheel", flags)!.Invoke(control,
                [new MouseEventArgs(MouseButtons.None, 0, 20, 40, delta)]);
        try
        {
            SynchronizationContext.SetSynchronizationContext(new PaneTestSynchronizationContext(form));
            shell.PresentPanel(null);
            surface.Size = new Size(1200, 659);
            installed.Clear();
            installed.AddRange(Enumerable.Range(0, 90).Select(i => new ProgramEntry
            {
                Name = "GF Sessão App " + i.ToString("D2"), InventoryId = "GF-SESSION-" + i,
                Version = "1.0", InventoryOnly = true
            }));
            var fixture = Enumerable.Range(0, 80).Select(i => new DriverInventoryEntry(
                @"ROOT\GF-SESSION\" + i, "Dispositivo de sessão " + i, "Fixture", "1.0", "test.inf", true, true)).ToArray();
            Step("aplicativos-inventario", () => { Go(V269Page.Aplicativos); search.Clear(); render.Invoke(form, []); });
            Require(apps.Items.Count == 90, "inventário sintético não publicado");
            Step("pesquisa", () => { search.Focus(); search.Text = "App 42"; });
            Require(apps.Items.Count == 1 && apps.Items[0].Name.EndsWith("42"), "pesquisa perdeu identidade");
            Step("rolagem-apps", () => { search.Clear(); apps.Focus(); Wheel(apps, -240); });
            var appBar = apps.Controls.Find("V269AppsVerticalScroll", false).OfType<DarkVScrollBar>().Single();
            int appOffset = appBar.Value;
            Require(appOffset > 0, "roda não rolou aplicativos");
            ProbeNativeWheel(form, apps, appBar, "Aplicativos", snapshot);
            Step("drivers-inventario", () => { Go(V269Page.Drivers); drivers.SetItems(fixture); drivers.Focus(); Wheel(drivers, -240); });
            var driverBar = drivers.Controls.Find("V269DriversVerticalScroll", true).OfType<DarkVScrollBar>().Single();
            Require(driverBar.Value > 0 && !drivers.ActionsCurrent, "drivers sem autoridade ou rolagem inválida");
            ProbeNativeWheel(form, drivers, driverBar, "Drivers", snapshot);
            Step("hardware", () => { Go(V269Page.Inicio); home.SetModels(MainForm.V269HomeModels(snapshot)); });
            Step("retorno-apps", () => Go(V269Page.Aplicativos));
            Require(appBar.Value == appOffset && apps.Items.Count == 90, "voltar perdeu rolagem de aplicativos");
            using var cancellation = new CancellationTokenSource();
            Step("progresso", () =>
            {
                Go(V269Page.Drivers);
                shell.PresentPanel(new(V269PanelKind.Progress, "Consulta sintética", "2 de 10", 20,
                    CancelFlow: cancellation.Cancel, FlowPage: V269Page.Drivers));
            });
            Step("progresso-em-outra-aba", () => Go(V269Page.Configuracoes));
            Require(shell.RightPane.Current is { Kind: V269PanelKind.Progress, ProgressPercent: 20 },
                "troca de aba descartou progresso em curso");
            Step("tema-e-movimento", () =>
            {
                theme.SelectedIndex = (int)V269Theme.Dark;
                motion.Checked = !savedMotion;
            });
            Step("progresso-incremental", () =>
            {
                shell.PresentPanel(new(V269PanelKind.Progress, "Consulta sintética", "6 de 10", 60,
                    CancelFlow: cancellation.Cancel, FlowPage: V269Page.Drivers));
                Go(V269Page.Drivers);
            });
            Require(shell.RightPane.Current is { ProgressPercent: 60 } &&
                shell.RightPane.Controls.Find("V269FlowPercentMeter", true).Single().AccessibleDescription == "60%",
                "painel não refletiu progresso informado");
            Step("cancelar", () => shell.RightPane.Controls.Find("V269CancelOwnedFlow", true)
                .OfType<Button>().Single().PerformClick());
            Require(cancellation.IsCancellationRequested, "cancelamento não chegou à operação sintética");
            shell.PresentPanel(null);
            foreach(var owner in new[]{V269Page.Aplicativos,V269Page.Drivers,V269Page.Sistema})
            {
                Go(owner);
                bool cancelled=false;
                var query=new V269PanelPresentation(V269PanelKind.Progress,"Consulta somente leitura","Fixture",
                    CancelFlow:()=>cancelled=true,FlowPage:owner,BackgroundQuery:true);
                shell.PresentPanel(query);Application.DoEvents();
                Require(shell.RightPane.Visible,"consulta mostra atividade na aba de origem");
                foreach(var dashboard in new[]{V269Page.Inicio,V269Page.Configuracoes})
                {
                    Go(dashboard);Application.DoEvents();
                    Require(!shell.RightPane.Visible && !form.Controls.Find("V269PaneToggle",true).Single().Visible &&
                        ReferenceEquals(shell.RightPane.Current,query),"REC-FIS-PANE: consulta retida sem painel em dashboard");
                    query=query with {Description="Atualização de consulta"};
                    shell.PresentPanel(query);Application.DoEvents();
                    Require(!shell.RightPane.Visible && ReferenceEquals(shell.RightPane.Current,query),"publicação incremental não reabre painel indevido");
                }
                Go(owner);Application.DoEvents();
                Require(shell.RightPane.Visible && ReferenceEquals(shell.RightPane.Current,query),"retorno à origem restaura consulta");
                shell.RightPane.Controls.Find("V269CancelOwnedFlow",true).OfType<Button>().Single().PerformClick();
                Require(cancelled,"cancelamento permanece acessível na origem");shell.PresentPanel(null);
            }
            Console.WriteLine("PASS REC-FIS-PANE: consultas somente leitura retidas, dashboards limpos, cancelamento na origem.");
            Step("inicializacao-consulta", () => Go(V269Page.Inicializacao));
            var pending = new TaskCompletionSource<IReadOnlyList<StartupItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var refresh = typeof(MainForm).GetMethod("RefreshStartupEntriesAsync", flags)!;
            var task = (Task)refresh.Invoke(form, [(Func<Task<IReadOnlyList<StartupItem>>>)(() => pending.Task)])!;
            Step("alternar-durante-consulta", () => { Go(V269Page.Sistema); Go(V269Page.Inicio); Go(V269Page.Inicializacao); });
            var title = (Label)typeof(MainForm).GetField("_v269StartupTitle", flags)!.GetValue(form)!;
            string previousTitle = title.Text;
            pending.SetResult([new StartupItem("GF resultado antigo", "Fixture", "registry", "fixture", true, false)]);
            Pump(task);
            Require(title.Text == previousTitle, "resultado obsoleto sobrescreveu a sessão atual");
            Step("viewport-compacto", () => { surface.Size = new Size(992, 659); Go(V269Page.Reparo); Go(V269Page.Limpeza); });
            Step("tema-claro", () => { Go(V269Page.Configuracoes); theme.SelectedIndex = (int)V269Theme.Claro; });
            Step("final-hardware", () => Go(V269Page.Inicio));
            timings.Sort();
            Console.WriteLine(FormattableString.Invariant(
                $"V269-SESSION-LATENCY n={timings.Count} p50_ms={timings[(timings.Count-1)/2]:F2} p95_ms={timings[(int)Math.Ceiling(timings.Count*.95)-1]:F2} max_ms={timings[^1]:F2}"));
            Console.WriteLine("PASS V269-SESSION: sessão contínua com dados sintéticos e controles reais.");
        }
        finally
        {
            shell.PresentPanel(null);
            shell.SelectPage(V269Page.Aplicativos);
            installed.Clear(); installed.AddRange(savedApps); search.Text = savedSearch; render.Invoke(form, []);
            drivers.SetItems(savedDrivers);
            home.SetModels(savedModels);
            shell.SelectPage(V269Page.Configuracoes);
            theme.SelectedIndex = savedTheme; motion.Checked = savedMotion;
            surface.Size = savedSize;
            shell.SelectPage(savedPage); shell.PresentPanel(savedPanel);
            SynchronizationContext.SetSynchronizationContext(savedContext);
            Application.DoEvents();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendWheelMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    // O observador existe apenas no teste e mede a passagem efetiva por
    // WM_PAINT do HWND, inclusive código de desenho do controle.
    private sealed class ScrollPaintProbe : NativeWindow, IDisposable
    {
        internal int Count { get; private set; }
        internal double Milliseconds { get; private set; }
        internal ScrollPaintProbe(Control control) => AssignHandle(control.Handle);
        protected override void WndProc(ref Message m)
        {
            if (m.Msg != 0x000F) { base.WndProc(ref m); return; }
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            try { base.WndProc(ref m); }
            finally
            {
                Count++;
                Milliseconds += System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
        }
        public void Dispose() { if (Handle != IntPtr.Zero) ReleaseHandle(); }
    }

    private static void ProbeNativeWheel(MainForm form, Control target, DarkVScrollBar bar,
        string page, HardwareSensorPortfolio snapshot)
    {
        Require(target.Visible && target.IsHandleCreated && bar.MaximumValue > 200,
            "sonda exige lista visível com overflow");
        int saved = bar.Value;
        var models = MainForm.V269HomeModels(snapshot);
        using var paint = new ScrollPaintProbe(target);
        static string Stats(List<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            return FormattableString.Invariant(
                $"n={sorted.Length} p50={sorted[(int)Math.Ceiling(sorted.Length*.50)-1]:F3} p95={sorted[(int)Math.Ceiling(sorted.Length*.95)-1]:F3} max={sorted[^1]:F3}");
        }
        try
        {
            foreach (bool queueModel in new[] { false, true })
            {
                bar.Value = bar.MaximumValue / 2;
                Application.DoEvents();
                var dispatch = new List<double>();
                var drain = new List<double>();
                var paints = new List<double>();
                var modelCost = new List<double>();
                int paintCount = 0;
                for (int i = 0; i < 8; i++)
                {
                    bool modelDone = !queueModel;
                    double modelMs = 0;
                    if (queueModel)
                    {
                        form.BeginInvoke((Action)(() =>
                        {
                            long began = System.Diagnostics.Stopwatch.GetTimestamp();
                            models = MainForm.UpdateV269LiveRows(models, snapshot.WindowsCounters);
                            modelMs = System.Diagnostics.Stopwatch.GetElapsedTime(began).TotalMilliseconds;
                            modelDone = true;
                        }));
                    }
                    int delta = i % 2 == 0 ? -30 : 30;
                    int prior = bar.Value;
                    var point = target.PointToScreen(new Point(20, Math.Min(target.Height-1, target.Height/2)));
                    int packedPoint = unchecked((int)((uint)(ushort)point.X | ((uint)(ushort)point.Y << 16)));
                    int beforePaints = paint.Count;
                    double beforePaintMs = paint.Milliseconds;
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    _ = SendWheelMessage(target.Handle, 0x020A, new IntPtr(delta << 16), new IntPtr(packedPoint));
                    dispatch.Add(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                    Require(delta < 0 ? bar.Value > prior : bar.Value < prior,
                        "WM_MOUSEWHEEL fracionado deve deslocar a lista na própria chamada: " + page);
                    start = System.Diagnostics.Stopwatch.GetTimestamp();
                    Application.DoEvents();
                    drain.Add(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                    Require(modelDone, "callback sintético de modelo deve concluir na fila STA");
                    modelCost.Add(modelMs);
                    paints.Add(paint.Milliseconds-beforePaintMs);
                    paintCount += paint.Count-beforePaints;
                }
                Require(paintCount > 0, "sonda deve observar WM_PAINT reais: " + page);
                string scenario = queueModel ? "modelo-cpu-ram-sintetico" : "sem-callback";
                Console.WriteLine($"V269-WHEEL page={page} scenario={scenario} paints={paintCount} dispatch_ms [{Stats(dispatch)}] drain_ms [{Stats(drain)}] paint_ms [{Stats(paints)}] model_ms [{Stats(modelCost)}]");
            }
        }
        finally { bar.Value = saved; Application.DoEvents(); }
    }

    private static void VerifyViewportVisibilityMeasurements(MainForm form,V269Shell shell)
    {
        var original=shell.SelectedPage;
        try
        {
            foreach(var (page,name) in new[]{
                (V269Page.Limpeza,"V269CleanupViewport"),
                (V269Page.Reparo,"V269RepairViewport"),
                (V269Page.Sistema,"V269SystemViewport")})
            {
                shell.SelectPage(page);
                Application.DoEvents();
                var viewport=form.Controls.Find(name,true).OfType<DarkScrollableFlow>().Single();
                Require(viewport.Visible,"viewport ativo "+page);
                var content=viewport.Content.Controls.Cast<Control>().Single();
                long before=viewport.PageMeasurementCount;
                viewport.Hide();
                Require(viewport.PageMeasurementCount==before,
                    "AUT-UX-01: ocultar viewport não deve medir novamente "+page);
                viewport.Show();
                Application.DoEvents();
                long passes = viewport.PageMeasurementCount - before;
                Require(passes > 0 &&
                    content.Width==viewport.CleanupViewportWidth &&
                    content.Height>=viewport.CleanupViewportHeight,
                    "reexibir deve reconciliar geometria efetiva e conteúdo "+page);
                // Medir os controles nativos, não apenas comparar largura
                // do conteúdo e do viewport (ambos podiam estar errados).
                var bar = viewport.Controls.OfType<DarkVScrollBar>().Single();
                var physicalViewport = viewport.Controls.OfType<Panel>().Single();
                Require(bar.Visible == (bar.MaximumValue > 0) &&
                    physicalViewport.Width == viewport.ClientSize.Width - (bar.Visible ? bar.Width : 0) &&
                    (!bar.Visible || !physicalViewport.Bounds.IntersectsWith(bar.Bounds)),
                    $"REC-UX-04 barra não pode cobrir viewport: {page} outer={viewport.ClientSize.Width} effective={physicalViewport.Width} bar={bar.Width} visible={bar.Visible}");
                Require(passes <= 2,
                    $"REC-UX-04 reexibição estável não pode repetir W, W-barra, W: {page} passes={passes}");
                Console.WriteLine($"V269-VIEWPORT-MEASURE page={page} hide=0 show={passes}");
                Console.WriteLine($"V269-UX-DOCK page={page} outer={viewport.ClientSize.Width} effective={physicalViewport.Width} bar={bar.Width} visible={bar.Visible} passes={passes}");
                // O callback explícito não pode ser deduplicado pelo tamanho:
                // dados de domínio, texto e opções podem mudar sem resize.
                string callback = page switch
                {
                    V269Page.Limpeza => "_relayoutCleaning",
                    V269Page.Reparo => "_relayoutRepair",
                    _ => "_relayoutSystem"
                };
                var relayout = (Action)typeof(MainForm).GetField(callback,
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                long afterShow = viewport.PageMeasurementCount;
                relayout();
                Require(viewport.PageMeasurementCount > afterShow &&
                    content.Width==viewport.CleanupViewportWidth &&
                    content.Height>=viewport.CleanupViewportHeight,
                    "REC-UX-04B: conteúdo explícito é medido mesmo sem mudança externa "+page);
            }
        }
        finally { shell.SelectPage(original); Application.DoEvents(); }
    }

    // Exercita o mesmo DarkScrollableFlow em uma janela real, com um
    // único conteúdo sintético que alterna entre overflow e espaço livre.
    // A ordem antiga falha na interseção/consumo da largura, sem depender de ms.
    private static void VerifyPageViewportDock()
    {
        using var window = new Form { ClientSize = new Size(420,240), ShowInTaskbar = false };
        using var host = new DarkScrollableFlow { Dock = DockStyle.Fill, IsPageViewport = true };
        host.Content.Padding = Padding.Empty;
        host.Content.WrapContents = false;
        var content = new Panel { Size = new Size(200,480), Margin = Padding.Empty };
        host.Content.Controls.Add(content);
        window.Controls.Add(host);
        window.Show();
        Application.DoEvents();
        var viewport = host.Controls.OfType<Panel>().Single();
        var bar = host.Controls.OfType<DarkVScrollBar>().Single();
        foreach (var theme in Enum.GetValues<V269Theme>())
        {
            host.ApplyCleanupSurface(theme);
            foreach (var size in new[] { new Size(420,240), new Size(300,180) })
            {
                window.ClientSize=size;
                content.Height=480;
                host.Reflow();Application.DoEvents();host.Reflow();
                Require(bar.Visible && bar.MaximumValue>0 &&
                    viewport.Width==host.ClientSize.Width-bar.Width &&
                    !viewport.Bounds.IntersectsWith(bar.Bounds),
                    "REC-UX-04 controle real reserva a barra sem sobreposição");
                bar.Value=bar.MaximumValue;
                Require(host.Content.Top==-bar.Value && host.Content.Bottom==viewport.ClientSize.Height,
                    "REC-UX-04 alcança o fim da página");
                content.Height=40;
                host.Reflow();Application.DoEvents();host.Reflow();
                Require(!bar.Visible && bar.MaximumValue==0 &&
                    viewport.Width==host.ClientSize.Width && host.Content.Top==0,
                    "REC-UX-04 página curta recupera toda a largura e posição");
            }
        }
        window.Close();
        Console.WriteLine("PASS REC-UX-04 Dock do viewport: overflow, retorno, largura e rolagem em três temas.");
    }

    private static void VerifyThemedTextScroll()
    {
        using var window = new Form { ClientSize = new Size(300,140), ShowInTaskbar = false };
        var editor = new TextBox { Multiline = true, ReadOnly = true, WordWrap = true, Text = "Curto" };
        using var host = new V269TextScrollHost(editor);
        window.Controls.Add(host);
        window.Show();
        Application.DoEvents();
        var scroll = host.Controls.OfType<DarkVScrollBar>().Single();
        foreach (var theme in Enum.GetValues<V269Theme>())
        {
            host.Configure(theme);
            editor.Text = "Curto";
            Application.DoEvents();
            Require(!scroll.Visible && editor.ScrollBars == ScrollBars.None,
                "texto curto sem barra ou espaço reservado");
            editor.Text = string.Join(Environment.NewLine, Enumerable.Range(0, 80).Select(i => "Linha " + i));
            Application.DoEvents();
            Require(scroll.Visible && scroll.MaximumValue > 0 && editor.Right <= scroll.Left,
                "overflow real com barra temática sem sobreposição");
            scroll.Value = scroll.MaximumValue;
            Require(editor.GetLineFromCharIndex(editor.GetCharIndexFromPosition(new Point(2,2))) > 0,
                "barra temática alcança linhas inferiores do editor");
            host.Configure(theme, false);
            Require(!scroll.Visible && editor.ScrollBars == ScrollBars.None,
                "fluxo externo não instala segunda barra");
            host.Configure(theme, true);
        }
        window.Close();
        using var reviewWindow = new Form { ClientSize = new Size(300,160), ShowInTaskbar = false };
        var reviewContent = new Panel();
        var confirm = new CheckBox { Text = "Autorização sintética", Bounds = new Rectangle(5,290,210,24) };
        reviewContent.Controls.Add(confirm);
        using var review = new V269ReviewScrollHost(reviewContent,368);
        reviewWindow.Controls.Add(review);
        reviewWindow.Show();
        Application.DoEvents();
        var reviewBar = review.Controls.OfType<DarkVScrollBar>().Single();
        foreach (var theme in Enum.GetValues<V269Theme>())
        {
            review.Configure(theme);
            Require(reviewBar.Visible && !review.AutoScroll && !reviewWindow.AutoScroll,
                "revisão compacta usa barra temática externa");
            reviewBar.Value = reviewBar.MaximumValue;
            Require(reviewContent.Top == -reviewBar.Value &&
                reviewContent.Top + confirm.Bottom <= review.ClientSize.Height,
                "aceite permanece alcançável na revisão compacta");
            reviewWindow.ClientSize = new Size(300,500);
            Application.DoEvents();
            Require(!reviewBar.Visible && reviewContent.Top == 0,
                "revisão com espaço suficiente não exibe barra");
            reviewWindow.ClientSize = new Size(300,160);
            Application.DoEvents();
        }
        reviewWindow.Close();
    }

    private static void VerifyTranslatedTextPaint()
    {
        using var tab=new V269SettingsTab("FixtureTab","Monitor") {Size=new Size(120,32),ForeColor=Color.White};
        using var action=new V269SettingsButton("FixtureAction","Gerar diagnóstico",5){Size=new Size(180,32)};
        using var choice=new V269SettingsCombo{Width=180};
        choice.Items.Add("Tema do Monitor");choice.SelectedIndex=0;
        using var heading=new V269ModuleHeading("Memória RAM","memory"){Size=new Size(180,32)};
        foreach(var theme in Enum.GetValues<V269Theme>())
        {
            var options=V269AppearanceOptions.Default with{Theme=theme};
            action.Options=options;choice.ApplyOptions(options);heading.ApplyOptions(options);
            tab.ForeColor=V269VisualTokens.ForTheme(theme).Text;
            foreach(var control in new Control[]{tab,action,choice,heading})
            {
                using var image=new Bitmap(440,100);
                using var g=Graphics.FromImage(image);
                g.Clear(Color.Magenta);
                var origin=new Point(210,34);
                g.TranslateTransform(origin.X,origin.Y);
                var dirty=new Rectangle(8,4,control.Width-16,control.Height-8);
                g.SetClip(dirty);
                if(control is V269SettingsCombo)
                    typeof(V269SettingsCombo).GetMethod("PaintClosedWithinClip",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(control,[g]);
                else
                {
                    using var args=new PaintEventArgs(g,dirty);
                    control.GetType().GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(control,[args]);
                }
                var screenDirty=dirty;screenDirty.Offset(origin);
                int changed=0;
                for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
                {
                    if(image.GetPixel(x,y).ToArgb()==Color.Magenta.ToArgb())continue;
                    Require(screenDirty.Contains(x,y),"REC-FIS-TEXT-CLIP: pixels fora da região transladada "+control.GetType().Name+"/"+theme);
                    changed++;
                }
                Require(changed>0,"REC-FIS-TEXT-CLIP: desenho visível dentro do recorte");
            }
        }
        Console.WriteLine("PASS REC-FIS-TEXT-CLIP: texto GDI respeita recorte e translação nos três temas.");
    }

    private static void VerifyDisabledCommandGlyph()
    {
        // Comandos compartilhados de Sistema, Inicialização e Limpeza:
        // a cor muda quando indisponível, não a identidade do ícone.
        var paint=typeof(V269CleanupButton).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic)!;
        foreach(var theme in Enum.GetValues<V269Theme>())
        foreach(var (caption,glyph) in new[]{("Somente leitura","lock"),("Verificando...","refresh"),("Histórico","clock"),("Ativar na inicialização","power")})
        {
            using var button=new V269CleanupButton(caption,glyph){Size=new Size(260,42),Enabled=false};
            button.ApplyOptions(V269AppearanceOptions.Default with {Theme=theme});
            int S(int n)=>V269AppTable.Scale(n,100,button.DeviceDpi);
            int icon=S(15), measured=TextRenderer.MeasureText(button.Text,button.Font,Size.Empty,
                TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width;
            int left=Math.Max(S(5),(button.Width-icon-S(6)-measured)/2),top=(button.Height-icon)/2;
            using var actual=new Bitmap(button.Width,button.Height);
            using(var g=Graphics.FromImage(actual))
            using(var args=new PaintEventArgs(g,button.ClientRectangle))
                paint.Invoke(button,[args]);
            // Interior do gradiente vertical é constante em x. Reconstruir
            // a área do ícone a partir de uma coluna de fundo não desenhada.
            using var expected=new Bitmap(icon,icon);
            for(int y=0;y<icon;y++)for(int x=0;x<icon;x++)
                expected.SetPixel(x,y,actual.GetPixel(button.Width-12,top+y));
            using(var g=Graphics.FromImage(expected))
                V269CleanupGlyph.Draw(g,glyph,new Rectangle(0,0,icon,icon),theme,
                    V269VisualTokens.ForTheme(theme).SecondaryText);
            for(int y=0;y<icon;y++)for(int x=0;x<icon;x++)
                Require(actual.GetPixel(left+x,top+y).ToArgb()==expected.GetPixel(x,y).ToArgb(),
                    "REC-FIS-GLYPH: forma semântica preservada em botão indisponível "+glyph+"/"+theme);
            bool executed=false;button.Click+=(_,_)=>executed=true;button.PerformClick();
            Require(!executed && !button.Enabled,"ícone correto não autoriza comando bloqueado");
        }
        Console.WriteLine("PASS REC-FIS-GLYPH: identidade visual preservada nos comandos indisponíveis.");
    }

    private static void VerifySettingsRangePaintClip()
    {
        using var range = new V269SettingsRange { AutoSize=false, Size=new Size(180,26), Minimum=0, Maximum=100, Value=80 };
        var paint=typeof(V269SettingsRange).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic)!;
        foreach(var theme in Enum.GetValues<V269Theme>())
        {
            range.ApplyOptions(V269AppearanceOptions.Default with { Theme=theme });
            using var image=new Bitmap(range.Width,range.Height);
            using var g=Graphics.FromImage(image);
            g.Clear(Color.Magenta);
            var dirty=new Rectangle(70,0,20,range.Height);
            g.SetClip(dirty);
            using var args=new PaintEventArgs(g,dirty);
            paint.Invoke(range,[args]);
            Require(g.ClipBounds==new RectangleF(dirty.X,dirty.Y,dirty.Width,dirty.Height),"slider preserva clip recebido");
            Require(image.GetPixel(80,range.Height/2).ToArgb()!=Color.Magenta.ToArgb(),"slider pinta dentro da região suja");
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
                if(!dirty.Contains(x,y))Require(image.GetPixel(x,y).ToArgb()==Color.Magenta.ToArgb(),"slider não pode pintar fora do clip parcial: "+theme);
        }
        Console.WriteLine("PASS REC-FIS-PAINT-CLIP: sliders preservam região suja nos três temas.");
    }

    private static void VerifyUniversalMeterPainting()
    {
        // Renderizador virtual usa a mesma rotina da barra WinForms,
        // com origem não zero e clip do desenho preservado.
        foreach (var theme in Enum.GetValues<V269Theme>())
        {
            // Pixels interiores sem antialias: as quatro âncoras reais
            // precisam sobreviver aos três temas e à pintura em cápsula.
            foreach (var (percent, expected) in new[] {
                (40,Color.FromArgb(0,162,232)),(60,Color.FromArgb(34,177,76)),
                (75,Color.FromArgb(255,223,0)),(100,Color.FromArgb(237,28,36)) })
            {
                var usage = GfMeterScale.Frame(GfMeterKind.Usage,percent,theme);
                using var pixels = new Bitmap(120,16);
                using (var graphics = Graphics.FromImage(pixels))
                    GfDynamicProgressBar.PaintFrame(graphics,new Rectangle(4,3,112,10),usage,
                        V269VisualTokens.ForTheme(theme).Border);
                Require(pixels.GetPixel(16,8).ToArgb()==expected.ToArgb(),
                    "RGB interior da barra universal: "+theme+"/"+percent);
                Require(V269VisualTokens.ContrastRatio(GfMeterScale.InkColor(usage,theme),
                    V269VisualTokens.ForTheme(theme).Card)>=4.5,
                    "texto usa paleta própria, não o RGB do preenchimento");
            }
            var success=GfUniversalMeter.Operation(null,GfMeterState.Success,true,theme);
            Require(success.Color.ToArgb()==Color.FromArgb(34,177,76).ToArgb() &&
                success.State==GfMeterState.Success && success.Fraction==1,
                "verde vivo somente após sucesso confirmado");
            var frame = GfUniversalMeter.Operation(65,GfMeterState.Running,true,theme);
            var track = V269VisualTokens.ForTheme(theme).Border;
            using var image = new Bitmap(120,30);
            using (var g = Graphics.FromImage(image))
            {
                g.Clear(Color.Magenta);
                g.SetClip(new Rectangle(8,4,104,20));
                GfDynamicProgressBar.PaintFrame(g,new Rectangle(10,8,100,10),frame,track);
                Require(g.ClipBounds == new RectangleF(8,4,104,20), "barra virtual preserva clip da lista");
            }
            Require(image.GetPixel(30,13).ToArgb() == frame.Color.ToArgb() &&
                image.GetPixel(100,13).ToArgb() == track.ToArgb() &&
                image.GetPixel(5,13).ToArgb() == Color.Magenta.ToArgb(),
                "barra virtual preserva preenchimento, trilha e limites nos três temas");
        }
    }

    private static void VerifyIndependentFeedback()
    {
        using var window = new Form { Size = new Size(620, 320), ShowInTaskbar = false };
        using var button = new ModernButton { Text = "Fixture sem manutenção", Dock = DockStyle.Top, Height = 42 };
        using var tabs = new DarkTabHost { Dock = DockStyle.Fill };
        using var list = new DarkProgramList { Dock = DockStyle.Fill, ItemHeight = 67 };
        var target = new ProgramEntry { Name = "GF fixture ação", CanUninstall = true };
        list.SetItems(new[] { target }.Concat(Enumerable.Range(0, 79)
            .Select(i => new ProgramEntry { Name = "Fixture " + i })));
        tabs.AddTab("A", list);
        tabs.AddTab("B", new Panel());
        tabs.AddTab("C", new Panel());
        window.Controls.Add(tabs);
        window.Controls.Add(button);
        window.Show();
        Application.DoEvents();
        int clicks = 0, changes = 0, actions = 0;
        list.ItemActionClicked += (item, action) =>
        {
            Require(ReferenceEquals(item, target) && action == "uninstall",
                "ação sintética não pode trocar identidade ou comando");
            actions++; // sem motor, sem registro do Windows, sem processo
        };
        var row = (Rectangle)typeof(DarkProgramList).GetMethod("RowBounds",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(list, [0])!;
        var area = AppRowGeometry.UninstallButton(row);
        var mouse = new MouseEventArgs(MouseButtons.Left, 1,
            area.Left + area.Width / 2, area.Top + area.Height / 2, 0);
        var down = typeof(DarkProgramList).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var up = typeof(DarkProgramList).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Require(list.AllowsRemoval(target) && list.ClientRectangle.Contains(mouse.Location),
            "fixture de ação visível e elegível");
        foreach (bool animate in new[] { true, false })
        {
            list.V269Options().AnimateButtonFeedback = animate;
            int previous = actions;
            down.Invoke(list, [mouse]); up.Invoke(list, [mouse]);
            Require(actions == previous + 1, "clique de linha despacha imediatamente em ambos os modos");
        }
        down.Invoke(list, [mouse]);
        list.ActionsEnabled = false;
        up.Invoke(list, [mouse]);
        Require(actions == 2, "revogação de capacidade entre pressionar e soltar bloqueia ação");
        list.ActionsEnabled = true;
        var bar = list.Controls.Find("V269AppsVerticalScroll", false).OfType<DarkVScrollBar>().Single();
        Require(bar.MaximumValue > 200, "fixture de rolagem");
        bar.Value = 200;
        button.Click += (_, _) => clicks++;
        tabs.SelectedIndexChanged += (_, _) => changes++;
        button.PerformClick();
        Require(clicks == 1, "clique ModernButton precisa despachar antes de qualquer Tick");
        button.PerformClick();
        Require(clicks == 2, "segundo clique não pode depender da duração da animação");
        button.Enabled = false;
        button.PerformClick();
        Require(clicks == 2, "botão desabilitado não despacha");
        var strip = tabs.Controls.Cast<Control>().Single(c => c.GetType().Name == "TabStrip");
        var key = strip.GetType().GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
        key.Invoke(strip, [new KeyEventArgs(Keys.Right)]);
        Require(tabs.SelectedIndex == 1 && changes == 1, "teclado seleciona aba sem esperar animação");
        key.Invoke(strip, [new KeyEventArgs(Keys.Right)]);
        Require(tabs.SelectedIndex == 2 && changes == 2, "rajada de teclado não perde navegação");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 200)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Require(clicks == 2 && actions == 2 && changes == 2 && tabs.SelectedIndex == 2,
            "timer de pintura não despacha comandos nem restaura seleção");
        tabs.SelectTab(0);
        Application.DoEvents();
        Require(bar.Value == 200 && list.Items.Count == 80 && ReferenceEquals(list.Items[0], target),
            "voltar à aba mantém rolagem e identidade do inventário");
        window.ClientSize = new Size(520, 240);
        Application.DoEvents();
        Require(bar.Value == 200 && bar.MaximumValue >= bar.Value,
            "redimensionamento mantém a posição válida da lista");
        window.Close();
        Console.WriteLine("PASS V269-UX feedback gráfico independente do clique e do teclado.");
    }

    // Percurso de uso contínuo: o mesmo Form, os mesmos controles e o
    // despacho Click dos botões reais. Não iniciar serviços nem executar ações
    // de manutenção. Tempos separados de despacho e drenagem da fila STA.
    private static void VerifySequentialNavigation(MainForm form, V269Shell shell)
    {
        var pages = Enum.GetValues<V269Page>();
        var views = pages.ToDictionary(p => p, p => shell.PageContent(p)!);
        var buttons = pages.ToDictionary(p => p,
            p => form.Controls.Find("V269Nav" + p, true).OfType<Button>().Single());
        var themeChoice = form.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
        int originalTheme = themeChoice.SelectedIndex;
        var originalPage = shell.SelectedPage;
        int notifications = 0;
        var dispatch = new List<double>();
        var settle = new List<double>();
        var transitions = new List<(V269Page From, V269Page To, V269Theme Theme, bool Pump, double Dispatch, double? Drain)>();
        var measuredViewports=new[]{"V269CleanupViewport","V269RepairViewport","V269SystemViewport"}
            .Select(name=>form.Controls.Find(name,true).OfType<DarkScrollableFlow>().Single()).ToArray();
        var measurementDeltas=new List<(V269Page From,V269Page To,long Cleanup,long Repair,long System)>();
        var trace = new List<(string View, string Origin, string Pass, int Outer, int Effective, int Target, int Previous)>(64);
        var traceReport = new List<(V269Page From, V269Page To, V269Theme Theme, bool Pump,
            string View, string Origin, string Pass, int Outer, int Effective, int Target, int Previous)>();
        foreach (var viewport in measuredViewports)
            viewport.PageMeasurementProbe = (origin, pass, outer, effective, target, previous) =>
                trace.Add((viewport.Name, origin, pass, outer, effective, target, previous));
        void Changed(object? sender, V269NavigationEventArgs e)
        {
            notifications++;
            Require(shell.SelectedPage == e.Page &&
                views.All(pair => pair.Value.Visible == (pair.Key == e.Page)),
                "evento de navegação publicado antes do estado visual coerente");
            int measuredIndex = e.Page switch
            {
                V269Page.Limpeza => 0, V269Page.Reparo => 1, V269Page.Sistema => 2, _ => -1
            };
            if (measuredIndex >= 0)
            {
                var viewport = measuredViewports[measuredIndex];
                var pageContent = viewport.Content.Controls.Cast<Control>().Single();
                // O evento observa a geometria final ainda na pilha do
                // clique, não uma correção após Application.DoEvents.
                Require(pageContent.Width == viewport.CleanupViewportWidth &&
                    pageContent.Height >= viewport.CleanupViewportHeight,
                    "REC-UX-04B: navegação publicada após geometria reconciliada");
                var samples = trace.Where(x=>x.View==viewport.Name).ToArray();
                Require(samples.Count(x=>x.Pass=="candidate" && x.Origin=="SelectionCommitted")==1 &&
                    samples.All(x=>x.Outer==viewport.ClientSize.Width) &&
                    samples.All(x=>x.Origin=="SelectionCommitted"),
                    $"REC-UX-04B: uma transação na largura final {e.Page}, outer={viewport.ClientSize.Width}; "+string.Join(",",samples.Select(x=>$"{x.Origin}:{x.Pass}:{x.Outer}")));
            }
        }
        void Click(V269Page page, bool pump)
        {
            var from = shell.SelectedPage;
            var palette = shell.Theme;
            bool changed = from != page;
            int count = notifications;
            trace.Clear();
            var beforeMeasurements=measuredViewports.Select(v=>v.PageMeasurementCount).ToArray();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var button = buttons[page];
            Require(button.Visible && button.Enabled && button.CanSelect,
                "navegação indisponível: " + page);
            button.PerformClick();
            double dispatchMs = clock.Elapsed.TotalMilliseconds;
            dispatch.Add(dispatchMs);
            double? drainMs = null;
            Require(shell.SelectedPage == page && notifications == count + (changed ? 1 : 0),
                "clique perdido, duplicado ou adiado: " + page);
            Require(views.All(pair => ReferenceEquals(shell.PageContent(pair.Key), pair.Value) &&
                pair.Value.Visible == (pair.Key == page)),
                "view recriada ou seleção simultânea: " + page);
            if (pump)
            {
                clock.Restart();
                Application.DoEvents();
                drainMs = clock.Elapsed.TotalMilliseconds;
                settle.Add(drainMs.Value);
                Require(shell.SelectedPage == page &&
                    views.All(pair => pair.Value.Visible == (pair.Key == page)),
                    "fila STA restaurou uma aba antiga: " + page);
            }
            transitions.Add((from, page, palette, pump, dispatchMs, drainMs));
            var delta = measuredViewports.Select((v,i)=>v.PageMeasurementCount-beforeMeasurements[i]).ToArray();
            measurementDeltas.Add((from,page,delta[0],delta[1],delta[2]));
            Require(trace.Count == delta.Sum(), "REC-UX-03: rastreio de cada medição");
            foreach (var sample in trace)
                traceReport.Add((from,page,palette,pump,sample.View,sample.Origin,sample.Pass,
                    sample.Outer,sample.Effective,sample.Target,sample.Previous));
        }
        shell.NavigationChanged += Changed;
        try
        {
            foreach (var theme in Enum.GetValues<V269Theme>())
            {
                Click(V269Page.Configuracoes, true);
                themeChoice.SelectedIndex = (int)theme;
                Application.DoEvents();
                Require(shell.Theme == theme, "mudança de tema não aplicada");
                foreach (var page in pages) Click(page, true);
                // Rajadas sem bombear mensagens entre os cliques: última
                // seleção vence; nenhum callback atrasado pode restaurar outra aba.
                foreach (var page in pages.Reverse()) Click(page, false);
                Click(V269Page.Inicio, true);
                Click(V269Page.Inicio, true); // clique repetido é idempotente
            }
            void Report(string phase, List<double> samples)
            {
                samples.Sort();
                double P(double q) => samples[Math.Clamp((int)Math.Ceiling(q * samples.Count) - 1, 0, samples.Count - 1)];
                Console.WriteLine(FormattableString.Invariant(
                    $"V269-UX-LATENCY {phase} n={samples.Count} p50_ms={P(.50):F2} p95_ms={P(.95):F2} max_ms={samples[^1]:F2}"));
            }
            Report("click_dispatch", dispatch);
            Report("sta_drain", settle);
            // Registrar depois do percurso: escrita no console não pode
            // alterar artificialmente a latência de cada clique/rajada.
            foreach (var sample in transitions)
            {
                string drain = sample.Drain?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "NA";
                Console.WriteLine(FormattableString.Invariant(
                    $"V269-UX-TRANSITION from={sample.From} to={sample.To} theme={sample.Theme} pump={sample.Pump} dispatch_ms={sample.Dispatch:F2} drain_ms={drain}"));
            }
            foreach(var sample in measurementDeltas)
                Console.WriteLine($"V269-UX-MEASURE from={sample.From} to={sample.To} limpeza={sample.Cleanup} reparo={sample.Repair} sistema={sample.System}");
            foreach (var sample in traceReport)
                Console.WriteLine($"V269-UX-MEASURE-ORIGIN from={sample.From} to={sample.To} theme={sample.Theme} pump={sample.Pump} view={sample.View} origin={sample.Origin} pass={sample.Pass} outer={sample.Outer} effective={sample.Effective} target={sample.Target} previous={sample.Previous}");
            foreach (var group in transitions.GroupBy(s => s.To))
            {
                var sorted = group.Select(s => s.Dispatch).OrderBy(x => x).ToArray();
                Console.WriteLine(FormattableString.Invariant(
                    $"V269-UX-PAGE page={group.Key} n={sorted.Length} p50_ms={sorted[(int)Math.Ceiling(sorted.Length*.5)-1]:F2} p95_ms={sorted[(int)Math.Ceiling(sorted.Length*.95)-1]:F2} max_ms={sorted[^1]:F2}"));
            }
            Console.WriteLine("PASS V269-UX navegação sequencial: cliques nativos, rajadas, 3 temas, identidade e seleção única.");
        }
        finally
        {
            foreach (var viewport in measuredViewports) viewport.PageMeasurementProbe = null;
            shell.NavigationChanged -= Changed;
            themeChoice.SelectedIndex = originalTheme;
            shell.SelectPage(originalPage);
            Application.DoEvents();
        }
    }

    // REC-FIS-02-PAINT: a seleção correta não prova que os pixels da
    // área útil pertencem à view correta. Usar exclusivamente o desktop,
    // modelos fixos e regiões internas dos cartões (sem rodapé dinâmico).
    private static void VerifyHomeReturnPixels(MainForm form, V269Shell shell,
        IReadOnlyDictionary<V269Page, Button> buttons, V269Theme theme)
    {
        var home = (V269HomeDashboard)shell.PageContent(V269Page.Inicio)!;
        var motion = form.Controls.Find("V269Animations", true).OfType<CheckBox>().Single();
        bool savedMotion = motion.Checked;
        try
        {
            motion.Checked = false;
            buttons[V269Page.Inicio].PerformClick();
            Application.DoEvents();
            Require(home.Visible && home.Cards.All(c => c.ArtReady),
                "REC-FIS-02-PAINT: cartões e artes prontos para referência");
            var bounds = home.RectangleToScreen(home.ClientRectangle);
            Require(Screen.FromControl(form).Bounds.Contains(bounds),
                "REC-FIS-02-PAINT: viewport dentro do desktop");
            var regions = home.Cards.Select(c =>
            {
                var region = Rectangle.Intersect(home.ClientRectangle, c.Bounds);
                region.Inflate(-3, -3);
                return region;
            }).Where(region => region.Width > 8 && region.Height > 8).ToArray();
            Require(regions.Length >= 2, "REC-FIS-02-PAINT: regiões visíveis suficientes");
            Bitmap Shot()
            {
                var shot = new Bitmap(bounds.Width, bounds.Height);
                using var graphics = Graphics.FromImage(shot);
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size,
                    CopyPixelOperation.SourceCopy);
                return shot;
            }
            // Oráculo RGB amostrado em toda a área interna visível dos
            // cartões, não só no trilho ou numa cor de fundo. Sem WM_PRINT.
            int Differences(Bitmap expected, Bitmap actual)
            {
                int changed = 0;
                foreach (var region in regions)
                    for (int y = region.Top; y < region.Bottom; y += 3)
                        for (int x = region.Left; x < region.Right; x += 3)
                            if (expected.GetPixel(x, y).ToArgb() != actual.GetPixel(x, y).ToArgb())
                                changed++;
                return changed;
            }
            var referenceCards = home.Cards.Select(c => c.Bounds).ToArray();
            using var reference = Shot();
            using (var repeated = Shot())
                Require(Differences(reference, repeated) == 0,
                    "REC-FIS-02-PAINT: referência instável sem mudança de estado");
            foreach (var other in new[] { V269Page.Aplicativos, V269Page.Limpeza, V269Page.Configuracoes })
            {
                buttons[other].PerformClick();
                Application.DoEvents();
                Require(!home.Visible && shell.SelectedPage == other,
                    "REC-FIS-02-PAINT: origem da transição ativa");
                using (var wrongPage = Shot())
                    Require(Differences(reference, wrongPage) > 0,
                        "REC-FIS-02-PAINT: controle negativo deve distinguir outra aba");
                buttons[V269Page.Inicio].PerformClick();
                // Antes de bombear: medir, mas não impor sincronização
                // artificial do compositor DWM com o despacho do clique.
                using (var immediate = Shot())
                    Console.WriteLine($"REC-FIS-02-PAINT immediate theme={theme} from={other} changed={Differences(reference, immediate)}");
                Application.DoEvents();
                Require(home.Visible && shell.SelectedPage == V269Page.Inicio &&
                    home.RectangleToScreen(home.ClientRectangle) == bounds &&
                    home.Cards.Select(c => c.Bounds).SequenceEqual(referenceCards),
                    "REC-FIS-02-PAINT: geometria e identidade restauradas");
                using var returned = Shot();
                int changed = Differences(reference, returned);
                Console.WriteLine($"REC-FIS-02-PAINT drained theme={theme} from={other} changed={changed} regions={regions.Length}");
                if (changed != 0)
                {
                    reference.Save($"LayoutCaptures/REC-FIS-02-PAINT-{theme}-{other}-reference.png");
                    returned.Save($"LayoutCaptures/REC-FIS-02-PAINT-{theme}-{other}-returned.png");
                }
                Require(changed == 0, $"REC-FIS-02-PAINT: pixels residuais após retorno {theme}/{other}: {changed}");
            }
        }
        finally { motion.Checked = savedMotion; }
    }

    // REC-FIS-02-PAINT-FORWARD: no retorno físico, o sentido Início→
    // Aplicativos expôs cabeçalho/trilho antigos e conteúdo novo. Não
    // confundir o oráculo de retorno ao Início com prova do sentido inverso.
    // Sonda passiva: não intercepta resultados, não cria HWNDs,
    // não envia mensagens e não força pintura.
    private sealed class NativeTransitionPaintProbe : NativeWindow, IDisposable
    {
        private readonly string _name;
        private readonly Action<string> _record;
        internal NativeTransitionPaintProbe(Control control, string name, Action<string> record)
        {
            if (!control.IsHandleCreated) throw new InvalidOperationException("HWND ausente: " + name);
            _name = name;
            _record = record;
            AssignHandle(control.Handle);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", ExactSpelling = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetUpdateRect(IntPtr hwnd, IntPtr rect,
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool erase);

        internal void ObservePending(string stage)
        {
            if (Handle != IntPtr.Zero)
                _record($"{stage} target={_name} update-region={GetUpdateRect(Handle, IntPtr.Zero, false)}");
        }

        protected override void WndProc(ref Message m)
        {
            string? name = m.Msg switch
            {
                0x0003 => "WM_MOVE",
                0x0005 => "WM_SIZE",
                0x000F => "WM_PAINT",
                0x0014 => "WM_ERASEBKGND",
                0x0018 => "WM_SHOWWINDOW",
                0x0047 => "WM_WINDOWPOSCHANGED",
                0x0085 => "WM_NCPAINT",
                0x0088 => "WM_SYNCPAINT",
                _ => null
            };
            if (name is null) { base.WndProc(ref m); return; }
            _record($"native target={_name} message={name} enter");
            try { base.WndProc(ref m); }
            finally { _record($"native target={_name} message={name} exit"); }
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero) ReleaseHandle();
        }
    }

    private static void VerifyForwardAppPaint(MainForm form, V269Shell shell,
        IReadOnlyDictionary<V269Page, Button> buttons, V269Theme theme, bool captureImmediate = true)
    {
        var home = shell.PageContent(V269Page.Inicio)!;
        var apps = shell.PageContent(V269Page.Aplicativos)!;
        var title = form.Controls.Find("V269PageTitle", true).Single();
        var subtitle = form.Controls.Find("V269PageSubtitle", true).Single();
        var glyph = form.Controls.Find("V269HomeHeadingIcon", true).OfType<V269HomeHeadingIcon>().Single();
        var list = (DarkProgramList)typeof(MainForm).GetField("_programList",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
        var motion = form.Controls.Find("V269Animations", true).OfType<CheckBox>().Single();
        bool savedMotion = motion.Checked;
        var savedPage = shell.SelectedPage;
        // Controle experimental: mesma transição, mesmos HWNDs e referências,
        // removendo somente CopyFromScreen entre o retorno do clique e DoEvents.
        string capturePrefix = captureImmediate ? "REC-FIS-02-FORWARD" : "REC-FIS-02-NOBLOCK";
        string tracePrefix = captureImmediate ? "REC-FIS-02-PAINT-NATIVE" : "REC-FIS-02-PAINT-NOBLOCK";
        var trace = new List<string>();
        var nativeTrace = new List<string>(512);
        var probes = new List<NativeTransitionPaintProbe>();
        long traceStart = System.Diagnostics.Stopwatch.GetTimestamp();
        string tracePhase = "setup";
        void RecordNative(string detail) => nativeTrace.Add(
            FormattableString.Invariant($"seq={nativeTrace.Count} elapsedMs={System.Diagnostics.Stopwatch.GetElapsedTime(traceStart).TotalMilliseconds:F3} phase={tracePhase} {detail}"));
        void Observe(string stage)
        {
            RecordNative("event " + stage);
            trace.Add(
            $"{stage} selected={shell.SelectedPage} home={home.Visible} apps={apps.Visible} " +
            $"title={title.Text} transition={shell.IsPageLayoutTransition} list={list.Bounds} " +
            $"glyph={glyph.Page} railHome={buttons[V269Page.Inicio].AccessibleDescription} railApps={buttons[V269Page.Aplicativos].AccessibleDescription}");
        }
        void Visibility(object? sender, EventArgs e)
        {
            Observe(sender == home ? "home-visible" : "apps-visible");
            if (sender == apps && apps.Visible)
                Require(shell.SelectedPage == V269Page.Aplicativos && !home.Visible &&
                    title.Text == "Aplicativos" && glyph.Page == V269Page.Aplicativos &&
                    buttons.All(pair => pair.Value.AccessibleDescription ==
                        (pair.Key == V269Page.Aplicativos ? "Aba atual" : "Abrir aba")),
                    "REC-FIS-02-PAINT-IDENTITY: view revelada antes de trilho/cabeçalho coerentes");
        }
        void Committed(object? sender, EventArgs e) => Observe("layout-committed");
        void Navigated(object? sender, V269NavigationEventArgs e) => Observe("navigation-changed");
        try
        {
            motion.Checked = false;
            buttons[V269Page.Inicio].PerformClick();
            Application.DoEvents();
            buttons[V269Page.Aplicativos].PerformClick();
            Application.DoEvents();
            Require(apps.Visible && list.Visible && title.Text == "Aplicativos",
                "REC-FIS-02-PAINT-FORWARD: referência Aplicativos ativa");
            var bounds = form.RectangleToScreen(form.ClientRectangle);
            Require(Screen.FromControl(form).Bounds.Contains(bounds),
                "REC-FIS-02-PAINT-FORWARD: janela dentro do desktop");
            Rectangle Relative(Rectangle screen)
            {
                screen.Offset(-bounds.X, -bounds.Y);
                return screen;
            }
            (string Name, Rectangle Area)[] Regions() =>
            [
                ("title", Relative(title.RectangleToScreen(title.ClientRectangle))),
                ("subtitle", Relative(subtitle.RectangleToScreen(subtitle.ClientRectangle))),
                ("home-rail", Relative(buttons[V269Page.Inicio].RectangleToScreen(
                    new Rectangle(1, buttons[V269Page.Inicio].Height / 2 - 4, 5, 8)))),
                ("apps-rail", Relative(buttons[V269Page.Aplicativos].RectangleToScreen(
                    new Rectangle(1, buttons[V269Page.Aplicativos].Height / 2 - 4, 5, 8)))),
                ("table-header", Relative(list.RectangleToScreen(new Rectangle(0, 0,
                    list.ClientSize.Width, Math.Min(list.ClientSize.Height, list.TableHeaderHeight)))))
            ];
            var regions = Regions();
            Require(regions.All(r => r.Area.Width > 0 && r.Area.Height > 0 &&
                new Rectangle(Point.Empty, bounds.Size).Contains(r.Area)),
                "REC-FIS-02-PAINT-FORWARD: regiões completas");
            Bitmap Shot()
            {
                var shot = new Bitmap(bounds.Width, bounds.Height);
                using var graphics = Graphics.FromImage(shot);
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size,
                    CopyPixelOperation.SourceCopy);
                return shot;
            }
            int[] Differences(Bitmap expected, Bitmap actual) => regions.Select(region =>
            {
                int changed = 0;
                for (int y = region.Area.Top; y < region.Area.Bottom; y++)
                    for (int x = region.Area.Left; x < region.Area.Right; x++)
                        if (expected.GetPixel(x,y).ToArgb() != actual.GetPixel(x,y).ToArgb()) changed++;
                return changed;
            }).ToArray();
            string Describe(int[] changes) => string.Join(" ", regions.Select((region,i) =>
                $"{region.Name}={changes[i]}"));
            using var reference = Shot();
            using (var repeated = Shot())
                Require(Differences(reference, repeated).All(n => n == 0),
                    "REC-FIS-02-PAINT-FORWARD: referência instável");
            buttons[V269Page.Inicio].PerformClick();
            Application.DoEvents();
            using (var negative = Shot())
            {
                var changed = Differences(reference, negative);
                Require(changed[0] > 0 && changed[4] > 0,
                    "REC-FIS-02-PAINT-FORWARD: controle negativo distingue título e tabela");
                negative.Save($"LayoutCaptures/{capturePrefix}-{theme}-source.png");
            }
            var body = form.Controls.Find("V269BodyHost", true).Single();
            var observed = new (string Name, Control Control)[]
            {
                ("form",form), ("shell",shell), ("body",body), ("home",home),
                ("apps",apps), ("list",list), ("title",title), ("subtitle",subtitle),
                ("glyph",glyph), ("home-rail",buttons[V269Page.Inicio]),
                ("apps-rail",buttons[V269Page.Aplicativos])
            };
            foreach (var item in observed)
            {
                if (item.Control.IsHandleCreated)
                    probes.Add(new NativeTransitionPaintProbe(item.Control,item.Name,RecordNative));
                else RecordNative("missing-hwnd target=" + item.Name);
            }
            Require(body.IsHandleCreated && apps.IsHandleCreated && list.IsHandleCreated &&
                title.IsHandleCreated && buttons[V269Page.Aplicativos].IsHandleCreated,
                "REC-FIS-02-PAINT-NATIVE: controles essenciais já possuem HWND");
            traceStart = System.Diagnostics.Stopwatch.GetTimestamp();
            tracePhase = "before-click";
            foreach (var probe in probes) probe.ObservePending("before-click");
            home.VisibleChanged += Visibility;
            apps.VisibleChanged += Visibility;
            shell.PageLayoutCommitted += Committed;
            shell.NavigationChanged += Navigated;
            tracePhase = "click";
            RecordNative("click-enter");
            buttons[V269Page.Aplicativos].PerformClick();
            RecordNative("click-return");
            tracePhase = "immediate";
            foreach (var probe in probes) probe.ObservePending("before-immediate");
            using var immediate = captureImmediate ? Shot() : null;
            RecordNative(captureImmediate ? "immediate-captured" : "immediate-skipped");
            tracePhase = "drain";
            Application.DoEvents();
            RecordNative("drain-return");
            tracePhase = "drained";
            foreach (var probe in probes) probe.ObservePending("after-drain");
            Require(apps.Visible && !home.Visible && shell.SelectedPage == V269Page.Aplicativos &&
                title.Text == "Aplicativos" && form.RectangleToScreen(form.ClientRectangle) == bounds &&
                Regions().SequenceEqual(regions),
                "REC-FIS-02-PAINT-FORWARD: estado e geometria restaurados");
            using var drained = Shot();
            RecordNative("drained-captured");
            var immediateDiff = immediate is null ? null : Differences(reference, immediate);
            var drainedDiff = Differences(reference, drained);
            foreach (var item in trace)
                Console.WriteLine($"REC-FIS-02-PAINT-{(captureImmediate ? "FORWARD" : "NOBLOCK")} event theme={theme} {item}");
            if (captureImmediate)
            {
                Console.WriteLine($"REC-FIS-02-PAINT-FORWARD immediate theme={theme} {Describe(immediateDiff!)}");
                Console.WriteLine($"REC-FIS-02-PAINT-FORWARD drained theme={theme} {Describe(drainedDiff)}");
            }
            else Console.WriteLine($"REC-FIS-02-PAINT-NOBLOCK drained theme={theme} {Describe(drainedDiff)}");
            if (!captureImmediate || immediateDiff!.Any(n => n != 0) || drainedDiff.Any(n => n != 0))
            {
                reference.Save($"LayoutCaptures/{capturePrefix}-{theme}-reference.png");
                immediate?.Save($"LayoutCaptures/{capturePrefix}-{theme}-immediate.png");
                drained.Save($"LayoutCaptures/{capturePrefix}-{theme}-drained.png");
            }
            Require(drainedDiff.All(n => n == 0),
                $"REC-FIS-02-PAINT-FORWARD: resíduos após drenagem {theme}: {Describe(drainedDiff)}");
        }
        finally
        {
            home.VisibleChanged -= Visibility;
            apps.VisibleChanged -= Visibility;
            shell.PageLayoutCommitted -= Committed;
            shell.NavigationChanged -= Navigated;
            foreach (var probe in probes) probe.Dispose();
            foreach (var line in nativeTrace)
                Console.WriteLine($"{tracePrefix} theme={theme} {line}");
            motion.Checked = savedMotion;
            shell.SelectPage(savedPage);
            Application.DoEvents();
        }
    }

    // Captura do desktop real (WM_PAINT), não WM_PRINT/DrawToBitmap.
    // A barra de seleção do HTML só pode existir no botão ativo.
    // Percurso separado: loop normal da STA, sem DoEvents ou captura síncrona
    // entre o envio do clique e o término da observação. MainForm de fixture.
    private sealed class FrameLoopContext : SynchronizationContext
    {
        private readonly MainForm _owner;
        private readonly int _staThread;
        internal FrameLoopContext(MainForm owner)
        {
            _owner = owner;
            _staThread = Environment.CurrentManagedThreadId;
        }

        public override SynchronizationContext CreateCopy() => this;

        public override void Post(SendOrPostCallback d, object? state)
        {
            // O HWND da própria fixture permanece vivo até o fim de todos os
            // awaits. Não usar o marshalling control oculto de outro contexto.
            _owner.BeginInvoke(new Action(() =>
            {
                if (Environment.CurrentManagedThreadId != _staThread)
                    throw new InvalidOperationException("FRAMES: despacho fora da STA");
                var previous = Current;
                SetSynchronizationContext(this);
                try { d(state); }
                finally { SetSynchronizationContext(previous); }
            }));
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (Environment.CurrentManagedThreadId != _staThread)
                throw new InvalidOperationException("FRAMES: envio síncrono entre threads proibido");
            d(state);
        }
    }

    private static void VerifyFramePaintLoop(MainForm form, V269Shell shell)
    {
        Exception? failure = null;
        bool completed = false;
        var previous = SynchronizationContext.Current;
        Require(form.IsHandleCreated && !form.IsDisposed && !form.InvokeRequired,
            "FRAMES: fixture deve possuir HWND na STA");
        // Não depender do controle oculto de marshaling do contexto WinForms,
        // cujo HWND pode ter sido descartado por percursos anteriores da suíte.
        // Este contexto pertence somente a esta janela e a este loop.
        var context = new FrameLoopContext(form);
        form.BeginInvoke(new Action(async () =>
        {
            var loopContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                void Checkpoint(string stage)
                {
                    Require(ReferenceEquals(SynchronizationContext.Current, context) &&
                        form.IsHandleCreated && !form.IsDisposed && !form.InvokeRequired,
                        "FRAMES: continuação fora da STA/loop " + stage);
                    Console.WriteLine("REC-FIS-02-FRAMES-HARNESS " + stage);
                }
                Checkpoint("loop-enter");
                RecFis02FrameSampler.ObserveForeground(form.Handle, "loop-before-topmost");
                form.TopMost = true;
                RecFis02FrameSampler.ObserveForeground(form.Handle, "loop-after-topmost");
                form.Activate();
                RecFis02FrameSampler.ObserveForeground(form.Handle, "loop-after-activate");
                var themeChoice = form.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
                var motion = form.Controls.Find("V269Animations", true).OfType<CheckBox>().Single();
                var buttons = Enum.GetValues<V269Page>().ToDictionary(p => p,
                    p => form.Controls.Find("V269Nav" + p, true).OfType<Button>().Single());
                var home = shell.PageContent(V269Page.Inicio)!;
                var apps = shell.PageContent(V269Page.Aplicativos)!;
                var title = form.Controls.Find("V269PageTitle", true).Single();
                var glyph = form.Controls.Find("V269HomeHeadingIcon", true).OfType<V269HomeHeadingIcon>().Single();
                var list = (DarkProgramList)typeof(MainForm).GetField("_programList",
                    BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
                var body = form.Controls.Find("V269BodyHost", true).Single();
                var destination = buttons[V269Page.Aplicativos];
                motion.Checked = false;
                // Idle apenas como barreira das referências estáveis, não para
                // adiar o clique em observação. Sem timer, Sleep ou fila manual.
                Task IdleAsync()
                {
                    var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    EventHandler? handler = null;
                    handler = (_, _) => { Application.Idle -= handler; ready.TrySetResult(true); };
                    Application.Idle += handler;
                    return ready.Task;
                }
                foreach (var theme in Enum.GetValues<V269Theme>())
                {
                    themeChoice.SelectedIndex = (int)theme;
                    shell.SelectPage(V269Page.Aplicativos);
                    RecFis02FrameSampler.ObserveForeground(form.Handle, "target-before-focus-" + theme);
                    destination.Focus();
                    RecFis02FrameSampler.ObserveForeground(form.Handle, "target-after-focus-" + theme);
                    await IdleAsync();
                    Checkpoint("target-idle " + theme);
                    RecFis02FrameSampler.ObserveForeground(form.Handle, "target-idle-" + theme);
                    var bounds = form.RectangleToScreen(form.ClientRectangle);
                    var hwnd = form.Handle;
                    Require(Screen.FromControl(form).Bounds.Contains(bounds),
                        "FRAMES: janela inteira no desktop");
                    // Observações STA independentes, fora do intervalo de amostragem.
                    // Não substituem a guarda worker nem alteram foco/geometria reais.
                    bool ObserveReference(string phase)
                    {
                        long begin = System.Diagnostics.Stopwatch.GetTimestamp();
                        bool owned = RecFis02FrameSampler.OwnsDesktop(hwnd, bounds, phase);
                        Console.WriteLine(FormattableString.Invariant(
                            $"REC-FIS-02-FRAMES-GUARD-STA phase={phase} theme={theme} begin_tick={begin} end_tick={System.Diagnostics.Stopwatch.GetTimestamp()} thread={Environment.CurrentManagedThreadId} owned={owned}"));
                        return owned;
                    }
                    bool negativePassed = false;
                    if (ObserveReference("target-sta-control"))
                    {
                        var wrong = new Rectangle(bounds.Location, new Size(checked(bounds.Width + 1), bounds.Height));
                        bool accepted = RecFis02FrameSampler.OwnsDesktop(hwnd, wrong, "negative-size", out var rejected, out var observedClient);
                        negativePassed = !accepted && rejected == RecFis02FrameSampler.GuardFailure.ClientSize &&
                            observedClient == bounds.Size;
                    }
                    bool dispatchOwned = ObserveReference("target-sta-dispatch");
                    negativePassed = negativePassed && dispatchOwned;
                    Console.WriteLine($"REC-FIS-02-FRAMES-GUARD-NEGATIVE theme={theme} passed={negativePassed}");
                    var targetGuard = new Func<bool>(() => RecFis02FrameSampler.OwnsDesktop(hwnd, bounds, "target-worker"));
                    using var target = await Task.Run(() => RecFis02FrameSampler.StableReference(bounds, targetGuard));
                    // Só exigir o controle após a referência: preservar a falha
                    // original do worker se a primeira guarda não for válida.
                    Require(negativePassed, "FRAMES: controle negativo de tamanho inconclusivo ou incorreto");
                    Checkpoint("target-reference " + theme);
                    shell.SelectPage(V269Page.Inicio);
                    destination.Focus();
                    await IdleAsync();
                    Require(form.RectangleToScreen(form.ClientRectangle) == bounds && home.Visible && !apps.Visible,
                        "FRAMES: origem e geometria coerentes");
                    ObserveReference("source-sta-dispatch");
                    var sourceGuard = new Func<bool>(() => RecFis02FrameSampler.OwnsDesktop(hwnd, bounds, "source-worker"));
                    using var source = await Task.Run(() => RecFis02FrameSampler.StableReference(bounds, sourceGuard));
                    var guard = new Func<bool>(() => RecFis02FrameSampler.OwnsDesktop(hwnd, bounds, "transition-worker"));
                    Checkpoint("source-reference " + theme);
                    var events = new List<string>(512);
                    void Mark(string stage) => events.Add(FormattableString.Invariant(
                        $"seq={events.Count} tick={System.Diagnostics.Stopwatch.GetTimestamp()} {stage}"));
                    void Visible(object? sender, EventArgs e)
                    {
                        Mark(sender == home ? "home-visible" : "apps-visible");
                        if (sender == apps && apps.Visible)
                            Require(!home.Visible && shell.SelectedPage == V269Page.Aplicativos &&
                                title.Text == "Aplicativos" && glyph.Page == V269Page.Aplicativos &&
                                buttons.All(p => p.Value.AccessibleDescription ==
                                    (p.Key == V269Page.Aplicativos ? "Aba atual" : "Abrir aba")),
                                "FRAMES: identidade antes da exposição");
                    }
                    void Committed(object? sender, EventArgs e) => Mark("layout-committed");
                    void Navigated(object? sender, V269NavigationEventArgs e) => Mark("navigation-changed");
                    int clicks = 0;
                    void Clicked(object? sender, EventArgs e) { clicks++; Mark("click-delivered"); }
                    var probes = new List<NativeTransitionPaintProbe>();
                    using var stop = new CancellationTokenSource();
                    home.VisibleChanged += Visible; apps.VisibleChanged += Visible;
                    shell.PageLayoutCommitted += Committed; shell.NavigationChanged += Navigated;
                    destination.Click += Clicked;
                    RecFis02FrameSampler.Session? session = null;
                    try
                    {
                        foreach (var (control, name) in new (Control, string)[] {
                            (body, "body"), (home, "home"), (apps, "apps"), (list, "list"),
                            (title, "title"), (glyph, "glyph"),
                            (buttons[V269Page.Inicio], "home-rail"), (destination, "apps-rail") })
                        {
                            Require(control.IsHandleCreated, "FRAMES: HWND ausente " + name);
                            probes.Add(new NativeTransitionPaintProbe(control, name, Mark));
                        }
                        session = RecFis02FrameSampler.Start(bounds, source, target, guard, stop.Token);
                        await session.Ready;
                        Require(ReferenceEquals(SynchronizationContext.Current, context) && !form.InvokeRequired,
                            "FRAMES: ready fora do contexto da fixture");
                        Mark("sampler-ready");
                        Require(RecFis02FrameSampler.OwnsDesktop(hwnd, bounds, "pre-click-sta") && home.Visible && !apps.Visible && destination.Enabled &&
                            destination.IsHandleCreated && destination.Focused,
                            "FRAMES: origem/foco/permissão antes do clique");
                        Mark("click-posted");
                        // Mensagem BM_CLICK endereçada apenas ao botão de navegação.
                        // Sem input global, clique em coordenada de outro app ou chamada SelectPage.
                        Require(RecFis02FrameSampler.PostButtonClick(destination.Handle),
                            "FRAMES: postagem nativa falhou");
                        var result = await session.Finished;
                        // Não escrever no console durante o intervalo observado.
                        Mark("sampler-finished");
                        await IdleAsync();
                        Checkpoint("sampler-idle " + theme);
                        foreach (var probe in probes) probe.ObservePending("after-idle");
                        Require(RecFis02FrameSampler.OwnsDesktop(hwnd, bounds, "post-idle-sta") && form.RectangleToScreen(form.ClientRectangle) == bounds &&
                            clicks == 1 && apps.Visible && !home.Visible &&
                            shell.SelectedPage == V269Page.Aplicativos && !shell.IsPageLayoutTransition,
                            "FRAMES: clique único, foco/geometria/identidade finais");
                        string prefix = $"LayoutCaptures/REC-FIS-02-FRAMES-{theme}";
                        await Task.Run(() => RecFis02FrameSampler.Export(result, source, target, prefix));
                        Checkpoint("exported " + theme);
                        Require(result.DestinationObserved, "FRAMES inconclusivo: " + result.StopReason);
                        Console.WriteLine($"PASS REC-FIS-02-PAINT-FRAMES theme={theme} samples={result.Frames.Count} stop={result.StopReason}");
                    }
                    finally
                    {
                        stop.Cancel();
                        // Observe qualquer falha da worker sem impedir a remoção das sondas.
                        if (session is not null)
                        {
                            try { await session.Finished; }
                            catch (Exception ex) { Mark("sampler-ended " + ex.GetType().Name); }
                        }
                        home.VisibleChanged -= Visible; apps.VisibleChanged -= Visible;
                        shell.PageLayoutCommitted -= Committed; shell.NavigationChanged -= Navigated;
                        destination.Click -= Clicked;
                        foreach (var probe in probes) probe.Dispose();
                        foreach (string e in events) Console.WriteLine($"REC-FIS-02-FRAMES-EVENT theme={theme} {e}");
                    }
                }
                completed = true;
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                // Todas as continuações da sessão foram observadas antes de
                // destruir o HWND usado por Post. Não fechar no worker.
                SynchronizationContext.SetSynchronizationContext(loopContext);
                form.Close();
            }
        }));
        RecFis02FrameSampler.ObserveForeground(form.Handle, "before-run");
        try { Application.Run(form); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        if (failure is not null) throw new InvalidOperationException("REC-FIS-02-PAINT-FRAMES", failure);
        Require(completed, "FRAMES: loop encerrado sem completar a prova");
    }

    private static void VerifyLiveWindowPaint(MainForm form, V269Shell shell)
    {
        var themeChoice = form.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
        int oldTheme = themeChoice.SelectedIndex;
        var oldPage = shell.SelectedPage;
        bool oldTopMost = form.TopMost;
        Directory.CreateDirectory("LayoutCaptures");
        try
        {
            form.TopMost = true;
            form.Activate();
            var buttons = Enum.GetValues<V269Page>().ToDictionary(p => p,
                p => form.Controls.Find("V269Nav" + p, true).OfType<Button>().Single());
            foreach (var theme in Enum.GetValues<V269Theme>())
            {
                themeChoice.SelectedIndex = (int)theme;
                foreach (var page in Enum.GetValues<V269Page>())
                {
                    shell.SelectPage(page);
                    Application.DoEvents();
                    var bounds = form.RectangleToScreen(form.ClientRectangle);
                    Require(Screen.FromControl(form).Bounds.Contains(bounds),
                        "captura real exige janela inteira visível no desktop");
                    void Capture(string phase)
                    {
                        using var shot = new Bitmap(bounds.Width, bounds.Height);
                        using (var graphics = Graphics.FromImage(shot))
                            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                        string path = $"LayoutCaptures/v269-DESKTOP-{theme}-{page}-{phase}-{bounds.Width}x{bounds.Height}.png";
                        shot.Save(path);
                        Color rail = theme == V269Theme.Dark ? ColorTranslator.FromHtml("#c8d0d7") : Color.White;
                        int Distance(Color a, Color b) => Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);
                        foreach (var (key, button) in buttons)
                        {
                            var point = button.PointToScreen(new Point(2, button.Height/2));
                            point.Offset(-bounds.X, -bounds.Y);
                            Require(new Rectangle(Point.Empty, shot.Size).Contains(point),
                                "botão de navegação fora do desktop: " + key);
                            bool hasRail = Distance(shot.GetPixel(point.X, point.Y), rail) < 30;
                            Require(hasRail == (key == page),
                                $"WM_PAINT: trilho de seleção incorreto em {theme}/{page}/{key}; evidência {path}");
                        }
                    }
                    Capture("inicio");
                    IEnumerable<Control> Children(Control control)
                    {
                        foreach (Control child in control.Controls)
                        {
                            yield return child;
                            foreach (var descendant in Children(child)) yield return descendant;
                        }
                    }
                    var scroll = Children(shell.PageContent(page)!).OfType<DarkVScrollBar>()
                        .FirstOrDefault(bar => bar.Visible && bar.MaximumValue > 0);
                    if (scroll is not null)
                    {
                        int saved = scroll.Value;
                        try
                        {
                            scroll.Value = scroll.MaximumValue;
                            Application.DoEvents();
                            Capture("rolagem");
                        }
                        finally { scroll.Value = saved; Application.DoEvents(); }
                    }
                }
                VerifyHomeReturnPixels(form, shell, buttons, theme);
                VerifyForwardAppPaint(form, shell, buttons, theme);
                VerifyForwardAppPaint(form, shell, buttons, theme, captureImmediate: false);
            }
            Console.WriteLine("PASS REC-FIS-02-PAINT: retorno ao Início comparado no desktop real nos três temas.");
            Console.WriteLine("PASS V2.6.9: 24 estados reais do desktop e rolagem quando disponível; trilho único de seleção nos três temas.");
        }
        finally
        {
            themeChoice.SelectedIndex = oldTheme;
            shell.SelectPage(oldPage);
            form.TopMost = oldTopMost;
        }
    }

    // As duas abas de ferramentas são independentes das fixtures de inventário.
    // O mesmo conjunto de testes e capturas pode ser executado no percurso WORK18
    // sem aumentar o prazo de quatro minutos nem repetir a matriz visual.
    internal static void RunMaintenanceVisual()
    {
        using var form = new MainForm(startServices:false);
        form.Show(); Application.DoEvents();
        var shell=form.Controls.Find("V269Shell",true).OfType<V269Shell>().Single();
        var clock=System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("V269-BEGIN Limpeza");
        VerifyCleanup(form,shell);
        Console.WriteLine($"V269-END Limpeza ms={clock.ElapsedMilliseconds}");
        clock.Restart();
        Console.WriteLine("V269-BEGIN Reparo");
        VerifyRepair(form,shell);
        Console.WriteLine($"V269-END Reparo ms={clock.ElapsedMilliseconds}");
        form.Close();
        Console.WriteLine("PASS V2.6.9: partição visual de Limpeza e Reparo completa.");
    }

    // Recuperado da rota L04/L11: mesmo sampler e mesmas guardas, sem
    // repetir a bateria integral para diagnosticar o ambiente interativo.
    internal static void RunFramePaintOnly()
    {
        using var form = new MainForm(startServices:false);
        form.Show();
        Application.DoEvents();
        var shell = form.Controls.Find("V269Shell", true).OfType<V269Shell>().Single();
        VerifyFramePaintLoop(form, shell);
        Console.WriteLine("PASS V2.6.9: FRAMES isolado concluído.");
    }

    private static void VerifyCleanup(MainForm form,V269Shell shell)
    {
        var cleanupClock=System.Diagnostics.Stopwatch.StartNew();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var disks=(DarkScrollableFlow)typeof(MainForm).GetField("_diskCards",flags)!.GetValue(form)!;
        var actions=(List<Control>)typeof(MainForm).GetField("_actionControls",flags)!.GetValue(form)!;
        var create=typeof(MainForm).GetMethod("CreateDiskCard",flags)!;
        var set=typeof(MainForm).GetMethod("SetCleanupRow",flags)!;
        var relayout=(Action)typeof(MainForm).GetField("_relayoutCleaning",flags)!.GetValue(form)!;
        var theme=form.Controls.Find("V269ThemeChoice",true).OfType<ComboBox>().Single();
        var surface=form.Controls.OfType<TableLayoutPanel>().Single();
        var originalSize=surface.Size;int originalTheme=theme.SelectedIndex;
        var previous=shell.RightPane.Current;var previousPage=shell.SelectedPage;
        var original=disks.Content.Controls.Cast<Control>().ToArray();
        var history=(List<V269AppPreviousResult>)typeof(MainForm).GetField("_v269CleanupPreviousResults",flags)!.GetValue(form)!;
        var originalHistory=history.ToArray();
        var fixtures=new[]
        {
            (Control)create.Invoke(form,[new DiskUsageInfo(@"C:\","Disco local",1024000000000L,558700000000L,"NTFS")])!,
            (Control)create.Invoke(form,[new DiskUsageInfo(@"D:\","Dados",931500000000L,652900000000L,"NTFS")])!
        };
        try
        {
            Directory.CreateDirectory("LayoutCaptures");
            disks.Content.Controls.Clear();
            foreach(var fixture in fixtures)disks.Content.Controls.Add(fixture);
            var observedDisks = new[]
            {
                new DiskUsageInfo(@"C:\","Disco local",1024000000000L,558700000000L,"NTFS"),
                new DiskUsageInfo(@"D:\","Dados",931500000000L,652900000000L,"NTFS")
            };
            Require(MainForm.V269DiskCardsMatch(disks.Content.Controls, observedDisks),
                "discos idênticos preservam cartões e seleção");
            Require(!MainForm.V269DiskCardsMatch(disks.Content.Controls,
                [observedDisks[0] with { FreeBytes = observedDisks[0].FreeBytes - 1 }, observedDisks[1]]) &&
                !MainForm.V269DiskCardsMatch(disks.Content.Controls,[observedDisks[1],observedDisks[0]]) &&
                !MainForm.V269DiskCardsMatch(disks.Content.Controls,[observedDisks[0]]),
                "mudança de espaço, ordem ou volumes exige reconstrução");
            string[] estimates=["1,1 GB","2,4 GB","820 MB","440 MB","720 MB"];
            for(int i=0;i<estimates.Length;i++)set.Invoke(form,[CleanupTarget.All[i],estimates[i]]);
            history.Clear();
            history.AddRange(new V269AppPreviousResult[]{
                new(Guid.NewGuid(),"Limpeza avançada","Fixture: nenhuma limpeza real foi executada.",true),
                new(Guid.NewGuid(),"Analisar limpeza","Fixture: estimativas sintéticas.",true),
                new(Guid.NewGuid(),"Analisar limpeza","Fixture: estimativas sintéticas.",true),
                new(Guid.NewGuid(),"Analisar limpeza","Fixture: estimativas sintéticas.",true)});
            shell.SelectPage(V269Page.Limpeza);
            foreach(var size in new[]{new Size(1200,659),new Size(1844,1116)})
            foreach(V269Theme palette in Enum.GetValues<V269Theme>())
            {
                var caseClock=System.Diagnostics.Stopwatch.StartNew();
                theme.SelectedIndex=(int)palette;surface.Size=size;surface.PerformLayout();relayout();Application.DoEvents();
                var cards=form.Controls.Find("V269CleanupActions",true).OfType<DarkScrollableFlow>().Single();
                Require(cards.Content.Controls.Count==6&&cards.Content.Controls.OfType<V269CleanupCard>().Count()==6,
                    "Limpeza mantém seis ferramentas, sem executar manutenção");
                var stableBounds = cards.Content.Controls.Cast<Control>().Select(c => c.Bounds).ToArray();
                int repeatedLayouts = 0;
                void CountCleanupLayout(object? sender, LayoutEventArgs args) => repeatedLayouts++;
                cards.Content.Layout += CountCleanupLayout;
                try
                {
                    for (int repeat = 0; repeat < 3; repeat++) ((Action)cards.Tag!)();
                    Require(repeatedLayouts == 0 && stableBounds.SequenceEqual(cards.Content.Controls.Cast<Control>().Select(c => c.Bounds)),
                        "Fit de Limpeza sem entradas novas não deve refazer layout");
                }
                finally { cards.Content.Layout -= CountCleanupLayout; }
                Require(disks.Content.Controls.Cast<Control>().All(x=>x.Width>0&&x.Height>0)&&
                    fixtures[0].Top<fixtures[1].Top,"discos em linhas verticais");
                var first=cards.Content.Controls.OfType<V269CleanupCard>().First();
                var select=typeof(Control).GetMethod("OnClick",flags)!;
                shell.PresentPanel(null);
                Require(first.TabStop&&fixtures.All(disk=>disk.TabStop),"ferramentas e discos alcançáveis pelo teclado");
                select.Invoke(first,[EventArgs.Empty]);Application.DoEvents();
                Require(shell.RightPane.Current?.Cleanup?.CleanupGlyph=="folder","seleção de ferramenta apresenta detalhes estruturados");
                select.Invoke(fixtures[0],[EventArgs.Empty]);Application.DoEvents();
                Require(shell.RightPane.Current?.Cleanup?.Fields.Any(x=>x.Label=="Espaço livre")==true,"seleção de disco apresenta leitura de espaço da fixture");
                select.Invoke(first,[EventArgs.Empty]);Application.DoEvents();
                foreach(var card in cards.Content.Controls.OfType<V269CleanupCard>())
                {
                    Require(card.ClientRectangle.Contains(card.Command.Bounds)&&card.Command.Width>0,
                        "comando de limpeza permanece dentro do cartão");
                }
                var section=form.Controls.Find("V269DiskSection",true).OfType<TableLayoutPanel>().Single();
                var report=form.Controls.Find("CleanupCategoryReport",true).OfType<TableLayoutPanel>().Single();
                var cleanupSurface=form.Controls.Find("V269CleanupSurface",true).Single();
                var cleanupViewport=form.Controls.Find("V269CleanupViewport",true).OfType<DarkScrollableFlow>().Single();
                Require(cleanupSurface.BackColor==Color.Transparent && cleanupViewport.BackColor==Color.Transparent &&
                    cards.Content.BackColor==Color.Transparent && disks.Content.BackColor==Color.Transparent,
                    "gradiente aprovado permanece visível entre cartões e discos");
                Require(cards.Content.Controls.Cast<Control>().Max(c=>c.Right)>=cards.Content.Width-3,
                    "cartões ocupam a largura útil, sem faixa vazia lateral");
                var cleanupPage=form.Controls.Find("V269CleanupPage",true).Single();
                Require(cleanupPage.Width==cleanupViewport.CleanupViewportWidth,
                    $"Limpeza usa a largura efetiva do viewport, sem reservar barra inexistente: conteúdo={cleanupPage.Width}, viewport={cleanupViewport.CleanupViewportWidth}, host={cleanupViewport.ClientSize}, barra={cleanupViewport.HasVerticalScrollbar}");
                int cardPad=V269AppTable.Scale(shell.LayoutWindowWidth<=1250?9:12,100,first.DeviceDpi);
                Require(first.Command.Right==first.ClientSize.Width-cardPad &&
                    first.Command.Bottom==first.ClientSize.Height-cardPad,
                    "cartão de Limpeza usa padding do breakpoint do HTML");
                Require(report.RowStyles.Cast<RowStyle>().All(row=>row.Height>=V269AppTable.Scale(28,100,report.DeviceDpi)),
                    "estimativas preservam o espaçamento mínimo do HTML");
                if(size.Width==1844 && palette==V269Theme.AzulGf)
                {
                    Require(report.ColumnCount==4,"REC-FIS-CLEAN-ESTIMATE: viewport amplo deve apresentar dois pares");
                    // Dados reais têm estado, contagem e tamanho, ao contrário
                    // dos valores curtos da captura de referência.
                    var labels=report.Controls.OfType<Label>()
                        .Where(label=>report.GetColumn(label)%2==1).ToArray();
                    var previousTexts=labels.Select(label=>label.Text).ToArray();
                    int S(int value)=>V269AppTable.Scale(value,100,report.DeviceDpi);
                    try
                    {
                        const string longState="Prévia parcial · 123.456 arquivos · 123,45 GiB (0x80070005)";
                        foreach(var label in labels)label.Text=longState;
                        relayout();Application.DoEvents();
                        int oldHeight=0;
                        for(int row=0;row<report.RowCount;row++)
                        {
                            int rowHeight=0;
                            for(int col=0;col<report.ColumnCount;col++)
                            {
                                if(report.GetControlFromPosition(col,row) is not Label label)continue;
                                int pairWidth=report.Width/2+(col/2<report.Width%2?1:0);
                                int titleWidth=(int)(pairWidth*.65);
                                int width=col%2==0?titleWidth:pairWidth-titleWidth;
                                int textHeight=TextRenderer.MeasureText(label.Text,label.Font,
                                    new Size(Math.Max(1,width-S(10)),int.MaxValue),
                                    TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPrefix).Height;
                                rowHeight=Math.Max(rowHeight,Math.Max(S(28),textHeight+S(16)));
                            }
                            oldHeight+=rowHeight;
                        }
                        int newHeight=(int)report.RowStyles.Cast<RowStyle>().Sum(row=>row.Height);
                        Require(labels.Length==5 && labels.All(label=>label.Text==longState) &&
                            report.Controls.Count==10 && newHeight<oldHeight,
                            $"REC-FIS-CLEAN-ESTIMATE: altura anterior={oldHeight}, atual={newHeight}; manter informação completa");
                        Console.WriteLine($"PASS REC-FIS-CLEAN-ESTIMATE: anterior={oldHeight} atual={newHeight} categorias={labels.Length}");
                    }
                    finally
                    {
                        for(int i=0;i<labels.Length;i++)labels[i].Text=previousTexts[i];
                        relayout();Application.DoEvents();
                    }
                }
                var stableRows=report.RowStyles.Cast<RowStyle>().ToArray();
                relayout();
                Require(report.RowStyles.Cast<RowStyle>().SequenceEqual(stableRows),
                    "medição idêntica de Limpeza não reconstrói a grade de estimativas");
                Require(section.RectangleToScreen(section.ClientRectangle).Bottom>=fixtures[1].RectangleToScreen(fixtures[1].ClientRectangle).Bottom && section.Height<=section.RowStyles.Cast<RowStyle>().Where(row=>row.SizeType==SizeType.Absolute).Sum(row=>row.Height)+fixtures.Sum(disk=>disk.Height+disk.Margin.Vertical)+section.Padding.Vertical+20,
                    "Espaço em disco termina após as unidades, sem esticar até o rodapé");
                var heading=form.Controls.Find("V269HomeHeadingIcon",true).Single();
                Require(heading.Visible,"Limpeza utiliza o ícone do cabeçalho aprovado");
                var toolbar=form.Controls.Find("V269CleanupToolbar",true).OfType<TableLayoutPanel>().Single();
                Require(toolbar.Controls.OfType<Button>().Count()==3&&toolbar.Controls.OfType<Button>()
                    .All(button=>toolbar.ClientRectangle.Contains(button.Bounds)&&button.Width>0&&button.Height>0),
                    "três comandos da barra permanecem dentro da área útil");
                if(size.Width==1844 && palette==V269Theme.AzulGf && toolbar.ColumnCount==4)
                {
                    // A mudança de largura em uma grade de três colunas não
                    // altera os comandos nem suas métricas de reconstrução.
                    int initialWidth=section.Width, removed=0;
                    void CountRemoved(object? sender,ControlEventArgs args)=>removed++;
                    toolbar.ControlRemoved+=CountRemoved;
                    try
                    {
                        section.Width=initialWidth-1;
                        section.Width=initialWidth;
                        relayout();
                        Require(toolbar.ColumnCount==4 && removed==0,
                            "variação de largura com três colunas não recria comandos de Limpeza");
                    }
                    finally{toolbar.ControlRemoved-=CountRemoved;}
                }
                long validationMs=caseClock.ElapsedMilliseconds;
                TimedMaintenanceCapture(surface,$"LayoutCaptures/v269-limpeza-SINTETICO-{palette}-{size.Width}x{size.Height}.png",
                    "Limpeza_detalhes",palette,size,validationMs);
                Console.WriteLine($"V269-CLEANUP-CAPTURE {palette} {size.Width}x{size.Height}");
            }
            Console.WriteLine($"TEMPO Limpeza: detalhes e geometria={cleanupClock.ElapsedMilliseconds} ms");
            // Captura do estado exato da referência: lixeira selecionada e quatro resultados anteriores.
            var context=SynchronizationContext.Current;
            try
            {
                foreach(var size in new[]{new Size(1200,659),new Size(1844,1116)})
                foreach(V269Theme palette in Enum.GetValues<V269Theme>())
                {
                    var caseClock=System.Diagnostics.Stopwatch.StartNew();
                    theme.SelectedIndex=(int)palette;surface.Size=size;
                    shell.PresentPanel(null);
                    var recycle=form.Controls.Find("V269CleanupCard3",true).Single();
                    typeof(Control).GetMethod("OnClick",flags)!.Invoke(recycle,[EventArgs.Empty]);
                    SynchronizationContext.SetSynchronizationContext(new PaneTestSynchronizationContext(form));
                    using var token=new CancellationTokenSource();
                    var confirmation=shell.RightPane.ConfirmAsync("Esvaziar Lixeira",
                        "Exclui permanentemente itens descartados. Fixture: nenhuma manutenção real.",token.Token);
                    try
                    {
                        surface.PerformLayout();relayout();Application.DoEvents();
                        Require(shell.RightPane.Controls.Find("V269AppPreviousResult",true).Length==4,
                            "quatro resultados anteriores no estado de confirmação da limpeza");
                        // A captura anterior deixou cartões da resolução grande cortados
                        // após retornar à janela compacta. Verificar geometria neste estado,
                        // não somente no painel de detalhes.
                        var tools=form.Controls.Find("V269CleanupActions",true).OfType<DarkScrollableFlow>().Single();
                        var page=form.Controls.Find("V269CleanupPage",true).Single();
                        var pageViewport=form.Controls.Find("V269CleanupViewport",true).OfType<DarkScrollableFlow>().Single();
                        var diskSection=form.Controls.Find("V269DiskSection",true).Single();
                        var visibleCards=tools.Content.Controls.OfType<V269CleanupCard>().ToArray();
                        Require(page.Width==pageViewport.CleanupViewportWidth && tools.Width==page.Width &&
                            visibleCards.Length==6 && visibleCards.All(card=>card.Left>=0 && card.Right<=tools.Width && card.Bottom<=tools.Height) &&
                            visibleCards[0].Top==visibleCards[2].Top && visibleCards[3].Top==visibleCards[5].Top &&
                            visibleCards[3].Top>visibleCards[0].Top && diskSection.Top>=tools.Bottom,
                            $"confirmação de Limpeza preserva seis cartões em três colunas e discos abaixo: página={page.Width}, viewport={pageViewport.CleanupViewportWidth}, ferramentas={tools.Size}, discos={diskSection.Bounds}");
                        Require(diskSection.RectangleToScreen(diskSection.ClientRectangle).Top <
                            pageViewport.RectangleToScreen(pageViewport.ClientRectangle).Bottom,
                            "Espaço em disco permanece visível na captura de confirmação");
                        var viewport=shell.RightPane.Controls.Find("V269AppFlowViewport",true).OfType<Panel>().Single();
                        var scroll=shell.RightPane.Controls.Find("V269CleanupFlowScroll",true).OfType<DarkVScrollBar>().Single();
                        Require(!viewport.AutoScroll,"Limpeza não usa a barra branca nativa no painel de operação");
                        var note=shell.RightPane.Controls.Find("V269FlowNotice",true).OfType<V269AppFlowNote>().Single();
                        var accept=shell.RightPane.Controls.Find("V269AcceptConfirmation",true).Single();
                        Require(note.Visible && note.BottomGap==V269AppTable.Scale(30,100,note.DeviceDpi) &&
                            accept.PointToScreen(Point.Empty).Y>=note.PointToScreen(Point.Empty).Y+note.Height,
                            "confirmação de Limpeza preserva o respiro do HTML entre aviso e comandos");
                        if(scroll.MaximumValue>0)
                        {
                            Require(scroll.Visible,"overflow de Limpeza usa a barra temática");
                            scroll.Value=scroll.MaximumValue;
                            Require(viewport.Controls.OfType<TableLayoutPanel>().Single().Top==-scroll.Value,
                                "rolagem temática alcança o último resultado");
                        }
                        // O HTML de referência está no início da rolagem. A captura
                        // deve ocorrer após a prova de alcance, não antes de restaurar a posição.
                        scroll.Value=0;
                        surface.PerformLayout();
                        Application.DoEvents();
                        Require(viewport.Controls.OfType<TableLayoutPanel>().Single().Top==0,
                            "captura comparativa no início da rolagem de Limpeza");
                        TimedMaintenanceCapture(surface,$"LayoutCaptures/v269-limpeza-REFERENCIA-CONFIRMACAO-{palette}-{size.Width}x{size.Height}.png",
                            "Limpeza_confirmacao",palette,size,caseClock.ElapsedMilliseconds);
                    }
                    finally{shell.RightPane.ResolveConfirmation(false);Pump(confirmation);}
                    Require(!confirmation.Result,"fixture de referência nunca autoriza manutenção");
                }
            }
            finally{SynchronizationContext.SetSynchronizationContext(context);}
            Console.WriteLine($"TEMPO Limpeza: confirmação e rolagem acumuladas={cleanupClock.ElapsedMilliseconds} ms");
            VerifyInventoryFlowStates(form,shell,V269Page.Limpeza);
            Console.WriteLine($"TEMPO Limpeza: fluxo operacional acumulado={cleanupClock.ElapsedMilliseconds} ms");
        }
        finally
        {
            history.Clear();history.AddRange(originalHistory);
            disks.Content.Controls.Clear();
            foreach(var fixture in fixtures)fixture.Dispose();
            actions.RemoveAll(c=>c.IsDisposed);
            foreach(var card in original)disks.Content.Controls.Add(card);
            foreach(var target in CleanupTarget.All)set.Invoke(form,[target,"Aguardando análise"]);
            theme.SelectedIndex=originalTheme;surface.Size=originalSize;shell.SelectPage(previousPage);shell.PresentPanel(previous);
            surface.PerformLayout();Application.DoEvents();
        }
        Console.WriteLine("PASS Limpeza V2.6.9: seis cartões, discos verticais, painel operacional e seis capturas sintéticas.");
    }

    // Separar pintura e compressão PNG das validações geométricas/ações.
    // Não muda seleção, quantidade de capturas, resolução ou conteúdo.
    private static void TimedMaintenanceCapture(Control surface,string path,string stage,V269Theme theme,Size size,long validationMs)
    {
        var clock=System.Diagnostics.Stopwatch.StartNew();
        using var capture=new Bitmap(surface.Width,surface.Height);
        surface.DrawToBitmap(capture,new Rectangle(Point.Empty,surface.Size));
        long paintMs=clock.ElapsedMilliseconds;
        capture.Save(path);
        Console.WriteLine($"LAYOUT-VISUAL fase={stage} tema={theme} dimensoes={size.Width}x{size.Height} dpi={surface.DeviceDpi} validacao_ms={validationMs} pintura_ms={paintMs} png_ms={clock.ElapsedMilliseconds-paintMs}");
    }

    private static void VerifyDrivers(MainForm form, V269Shell shell)
    {
        var root = shell.PageContent(V269Page.Drivers)!;
        var list = root.Controls.Find("DriverInventoryList", true).OfType<DarkDriverList>().Single();
        var frame = root.Controls.Find("V269DriversTableFrame", true).Single();
        var sort = root.Controls.Find("V269DriversSort", true).OfType<V269AppSortCombo>().Single();
        var details = root.Controls.Find("V269DriversDetails", true).OfType<Button>().Single();
        var theme = form.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        var originalSize = surface.Size;
        int originalTheme = theme.SelectedIndex;
        var panel = shell.RightPane.Current;
        var inventory = new[]
        {
            new DriverInventoryEntry(@"PCI\GF-UI\1", "AMD B550 Chipset", "AMD", "6.07.22.037", "oem401.inf", true, true)
                { ReportedPnpClass = "System" },
            new DriverInventoryEntry(@"USB\GF-UI\2", "Intel Wireless Bluetooth", "Intel", "23.100.0.4", "oem402.inf", true, true)
                { ReportedPnpClass = "Bluetooth" },
            new DriverInventoryEntry(@"PCI\GF-UI\3", "NVIDIA GeForce RTX 5060 Ti", "NVIDIA", "32.0.15.7602", "oem403.inf", true, true)
                { ReportedPnpClass = "Display" },
            new DriverInventoryEntry(@"PCI\GF-UI\4", "Realtek High Definition Audio", "Realtek", "6.0.9733.1", "oem404.inf", true, true)
                { ReportedPnpClass = "MEDIA" },
            new DriverInventoryEntry(@"USB\GF-UI\5", "USB Composite Device", "Microsoft", "10.0.26100.1", "usb.inf", true, true)
                { ReportedPnpClass = "USB" }
        };
        var fixture = inventory.Concat(Enumerable.Range(0, 12).Select(i => new DriverInventoryEntry(
            @"ROOT\GF-UI\" + i, "Dispositivo de teste " + i, "Microsoft", "10.0.26100.1", "test.inf", null, true))).ToArray();
        try
        {
            shell.PresentPanel(null);
            shell.SelectPage(V269Page.Drivers);
            Require(list.TableMode && list.Items.Count == 0 && !list.ActionsCurrent &&
                sort.Items.Cast<string>().SequenceEqual(new[] { "Nome (A–Z)", "Nome (Z–A)", "Atualizações primeiro", "Ordem manual" }),
                "Drivers inicia sem consulta e usa as quatro opções do HTML");
            Require(V269AppTable.DriverHeaders.SequenceEqual(new[] { "Dispositivo / Driver", "Versão atual", "Versão disponível", "Ações" }),
                "tabela de Drivers tem as quatro colunas aprovadas");
            Require(root.Controls.Find("DriverCopySafetySummary", true).Length == 0 &&
                root.Controls.Find("DriverInventoryStatus", true).Length == 0,
                "rodapé legado saiu da superfície de Drivers");

            list.SetItems(fixture);
            Directory.CreateDirectory("LayoutCaptures");
            foreach (var size in new[] { new Size(992, 659), new Size(1200, 659) })
            {
                surface.Size = size;
                foreach (V269Theme palette in Enum.GetValues<V269Theme>())
                {
                    theme.SelectedIndex = (int)palette;
                    surface.PerformLayout();
                    Application.DoEvents();
                    var vertical = list.Controls.Find("V269DriversVerticalScroll", true).OfType<DarkVScrollBar>().Single();
                    var horizontal = list.Controls.Find("V269DriversHorizontalScroll", true).OfType<DarkVScrollBar>().Single();
                    Require(frame.Region is { } edge && !edge.IsVisible(0, 0) &&
                        edge.IsVisible(frame.Width / 2, frame.Height / 2),
                        "moldura arredondada de Drivers acompanha o viewport");
                    Require(vertical.Visible && vertical.Top == list.TableHeaderHeight && !horizontal.Visible,
                        "cabeçalho fixo e barras somente quando há excesso");
                    Require(details.Visible == (size.Width <= 1040), "Detalhes aparece no breakpoint do HTML");
                    if (details.Visible && !shell.RightPane.Visible)
                    {
                        details.PerformClick();
                        Application.DoEvents();
                    }
                    Require(shell.RightPane.Visible && shell.RightPane.Current?.Driver is { } model &&
                        model.Fields.Select(field => field.Label).SequenceEqual(new[]
                        {
                            "Categoria", "Fabricante", "Assinatura", "Barramento", "Versão instalada", "Oferta"
                        }) && model.Fields[^1].Value == "Não confirmada" &&
                        !shell.RightPane.Controls.Find("V269AppDescriptionHeading", true).Any(),
                        "painel apresenta seis campos reais e reserva a indicação de oferta comprovada");
                    using var capture = new Bitmap(size.Width, size.Height);
                    surface.DrawToBitmap(capture, new Rectangle(Point.Empty, size));
                    capture.Save($"LayoutCaptures/v269-drivers-SINTETICO-{palette}-{size.Width}x{size.Height}.png");
                }
            }
            VerifyInventoryFlowStates(form, shell, V269Page.Drivers);
        }
        finally
        {
            list.SetItems([]);
            shell.SelectPage(V269Page.Configuracoes);
            theme.SelectedIndex = originalTheme;
            surface.Size = originalSize;
            shell.PresentPanel(panel);
        }
    }

    private static void VerifyApps(MainForm form,V269Shell shell,WindowsFormsSynchronizationContext confirmationContext)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var inventory=(List<ProgramEntry>)typeof(MainForm).GetField("_installed",flags)!.GetValue(form)!;
        var list=(DarkProgramList)typeof(MainForm).GetField("_programList",flags)!.GetValue(form)!;
        var sort=form.Controls.Find("V269AppSort",true).OfType<ComboBox>().Single();
        var search=form.Controls.Find("V269AppsSearch",true).OfType<TextBox>().Single();
        var details=form.Controls.Find("V269AppsDetails",true).OfType<Button>().Single();
        var apps=form.Controls.Find("V269AppsPage",true).OfType<TableLayoutPanel>().Single();
        var theme=form.Controls.Find("V269ThemeChoice",true).OfType<ComboBox>().Single();
        var surface=form.Controls.OfType<TableLayoutPanel>().Single();
        var originalSize=surface.Size;int originalTheme=theme.SelectedIndex;
        var original=inventory.ToArray();var panel=shell.RightPane.Current;
        var render=typeof(MainForm).GetMethod("RenderPrograms",flags)!;
        var entries=new[]{
            new ProgramEntry{Name="7-Zip 24.09 (x64)",Version="24.09",Publisher="Igor Pavlov",InventoryId="GF-APP-FIXTURE-1",
                InventorySource="Registro: Computador (64 bits)",InstallDate=new DateOnly(2026,11,18),CanUninstall=true,
                DeclaredDescription="Descrição sintética declarada para testar apresentação."},
            new ProgramEntry{Name="Discord",Version="1.0.9172",Publisher="Discord Inc.",InventoryId="GF-APP-FIXTURE-2",
                InventorySource="Registro: Usuário atual (64 bits)",InstallDate=new DateOnly(2026,11,18)},
            new ProgramEntry{Name="Microsoft Edge",Version="142.0.3595.44",Publisher="Microsoft Corporation",InventoryId="GF-APP-FIXTURE-3",InventoryOnly=true},
            new ProgramEntry{Name="OBS Studio",Version="31.0.3",Publisher="OBS Project",InventoryId="GF-APP-FIXTURE-4",
                InventorySource="Registro: Computador (64 bits)",InstallDate=new DateOnly(2026,11,12),IsUpdate=true,AvailableVersion="32.0.1"},
            new ProgramEntry{Name="VLC media player",Version="3.0.21",Publisher="VideoLAN",InventoryId="GF-APP-FIXTURE-5",
                InventorySource="Registro: Computador (64 bits)",InstallDate=new DateOnly(2026,11,15)}
        };
        try
        {
            shell.PresentPanel(null);shell.SelectPage(V269Page.Aplicativos);
            inventory.Clear();inventory.AddRange(entries);render.Invoke(form,[]);
            Application.DoEvents();
            Require(list.V269TableMode&&list.Items.Count==5&&sort.Items.Count==5&&sort is V269AppSortCombo,
                "view real de seis colunas e seletor nativo dedicado");
            var accessible=list.AccessibilityObject;
            Require(accessible.GetChildCount()==5&&accessible.GetChild(0)?.Name?.Contains("7-Zip")==true&&
                accessible.GetChild(2)?.GetChild(1)?.State.HasFlag(AccessibleStates.Unavailable)==true,
                "leitor de tela recebe identidade e ação bloqueada da linha real");
            sort.SelectedIndex=1;Require(list.Items[0].Name=="VLC media player","seletor Z–A conectado ao inventário");
            sort.SelectedIndex=2;Require(list.Items[0].InstallDate.HasValue&&list.Items[^1].InstallDate is null,"datas reais e ausências no fim");
            sort.SelectedIndex=3;Require(list.Items[0].Name=="OBS Studio","ofertas primeiro");
            sort.SelectedIndex=0;search.Text="VLC";Require(list.Items.Count==1&&list.Items[0]==entries[4],"pesquisa real");
            search.Clear();list.Focus();Application.DoEvents();
            Require(search is V269AppSearchEditor {ShowsCue:true}&&search.Visible&&search.TabStop&&
                search.PlaceholderText=="Pesquisar aplicativo..."&&search.TextLength==0,
                "dica nativa sem ocultar o campo nem modificar a pesquisa");
            search.Focus();Application.DoEvents();
            Require(search.Visible&&search.Focused&&search is V269AppSearchEditor {ShowsCue:false},
                "campo nativo aceita foco direto e mantém acesso por Tab");
            search.Text="VLC";Require(list.Items.Count==1,"texto real continua filtrando");
            search.Clear();list.Focus();Application.DoEvents();
            Require(search.Visible&&search is V269AppSearchEditor {ShowsCue:true}&&list.Items.Count==5,
                "dica reaparece ao deixar pesquisa vazia sem ocultar o campo");
            list.SelectedIndex=0;
            typeof(DarkProgramList).GetMethod("OnKeyDown",flags)!.Invoke(list,[new KeyEventArgs(Keys.Alt|Keys.Down)]);
            Require(sort.SelectedIndex==4&&ReferenceEquals(list.Items[1],entries[0]),"reordenação pelo teclado conectada");
            sort.SelectedIndex=0;
            foreach(var size in new[]{new Size(760,596),new Size(992,659),new Size(1200,659)})
            foreach(int index in new[]{0,1,2})
            {
                surface.Size=size;theme.SelectedIndex=index;
                shell.PresentPanel(null);render.Invoke(form,[]);
                list.SelectedIndex=0;
                if(list.SelectedItem is {} selected)
                    typeof(MainForm).GetMethod("PresentV269AppDetails",flags)!.Invoke(form,[selected]);
                surface.PerformLayout();Application.DoEvents();
                Require(list.TableHeaderHeight>0&&list.Items.Count==5,"cabeçalho fixo e inventário preservado");
                var hScroll=list.Controls.Find("V269AppsHorizontalScroll",false).OfType<DarkVScrollBar>().Single();
                var vScroll=list.Controls.Find("V269AppsVerticalScroll",false).OfType<DarkVScrollBar>().Single();
                int contentWidth=list.ClientSize.Width-(vScroll.Visible?vScroll.Width:0);
                int minWidth=V269AppTable.MinimumWidth(shell.InterfaceScalePercent,list.DeviceDpi,list.TableCompact);
                Require(list.TableCompact==V269AppTable.IsCompact(shell.LayoutWindowWidth)&&
                    hScroll.Visible==(minWidth>contentWidth),
                    "breakpoint CSS e rolagem somente quando as larguras mínimas não cabem");
                Require(list.BackColor==V269AppTable.EmptySurface((V269Theme)index),
                    "aplicação geral de temas conserva a superfície exclusiva de Aplicativos");
                var rowMethod=typeof(DarkProgramList).GetMethod("RowBounds",flags)!;
                var row=(Rectangle)rowMethod.Invoke(list,[0])!;
                var cells=V269AppTable.Columns(row,shell.InterfaceScalePercent,list.DeviceDpi,list.TableCompact);
                Require(cells.Length==6&&cells.Sum(x=>x.Width)==row.Width,"geometria das colunas");
                using var headerFont=new Font("Segoe UI",9f*shell.TextScalePercent/100f,FontStyle.Bold);
                using var headerGraphics=list.CreateGraphics();
                var dateHeading=V269AppTable.AppInstallDateHeaderBounds(headerGraphics,cells[3],headerFont,list.DeviceDpi,list.TableCompact);
                int dateHeadingWidth=TextRenderer.MeasureText(headerGraphics,"Instalado em",headerFont,Size.Empty,
                    TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding).Width;
                Require(cells[3].Contains(dateHeading)&&dateHeading.Width>=dateHeadingWidth,
                    "Instalado em legível por inteiro com fonte padrão, sem invadir outra coluna");
                var origin=surface.PointToScreen(Point.Empty);
                Rectangle Relative(Control x)=>new(surface.PointToClient(x.PointToScreen(Point.Empty)),x.Size);
                Console.WriteLine($"V269-APPS-GEOMETRY {size.Width}x{size.Height} theme={(V269Theme)index} dpi={list.DeviceDpi} toolbar={Relative(form.Controls.Find("V269AppsSearch",true).Single().Parent!)} list={Relative(list)} header={list.TableHeaderHeight} row={row.Height} pane={Relative(shell.RightPane)} paneVisible={shell.RightPane.Visible}");
                using(var image=new Bitmap(surface.Width,surface.Height))
                {surface.DrawToBitmap(image,new Rectangle(Point.Empty,surface.Size));image.Save($"LayoutCaptures/v269-aplicativos-SINTETICO-{(V269Theme)index}-{size.Width}x{size.Height}.png");}
                if(size.Width<=1040)
                {
                    if(shell.RightPane.Visible)details.PerformClick();
                    details.PerformClick();Application.DoEvents();
                    Require(shell.RightPane.Visible&&shell.RightPane.Current?.Kind==V269PanelKind.Details,"detalhes compactos acessíveis");
                    var columns=(TableLayoutPanel)shell.RightPane.Parent!;
                    Require(columns.Name=="V269MainLayout"&&columns.GetRow(shell.RightPane)==2&&columns.GetColumn(shell.RightPane)==0&&list.Visible,
                        "painel compacto usa o arranjo original do HTML");
                    Require(form.Controls.Find("V269AppsBackToList",true).Length==0,
                        "nenhum botão de retorno adicional ao HTML");
                    Require(shell.RightPane.Current?.Application is {Fields.Count:7,Description:"Descrição sintética declarada para testar apresentação."}&&
                        shell.RightPane.Controls.Find("V269StructuredAppDetails",true).Single().Visible,
                        "painel estruturado expõe fatos locais e motivos");
                    var scrollbar=shell.RightPane.Controls.Find("V269AppDetailsScroll",true).OfType<DarkVScrollBar>().Single();
                    var fields=shell.RightPane.Controls.Find("V269AppDetailsFields",true).OfType<TableLayoutPanel>().Single();
                    var structure=shell.RightPane.Controls.Find("V269StructuredAppDetails",true).Single();
                    Require(fields.RowCount==8&&
                        fields.Controls.OfType<Label>().Where(x=>fields.GetColumn(x)==0&&fields.GetColumnSpan(x)==1)
                            .Select(x=>x.Text).SequenceEqual(new[]{"Tipo","Origem","Arquitetura","Local"})&&
                        structure.AccessibleDescription?.Contains("Data declarada:") == true&&
                        structure.AccessibleDescription?.Contains("Versão:")==true&&
                        structure.AccessibleDescription?.Contains("Oferta:")==true,
                        "quatro campos do HTML sem perda dos dados completos na acessibilidade");
                    Require(scrollbar.Visible==(scrollbar.MaximumValue>0)&&scrollbar.AccessibleRole==AccessibleRole.ScrollBar,
                        "painel compacto mostra rolagem temática somente quando necessária");
                    Require(shell.RightPane.BackColor==V269AppDetailStyle.Palette((V269Theme)index).RightPanel,
                        "superfície do detalhe segue apenas o tema de Aplicativos");
                    scrollbar.Value=scrollbar.MaximumValue;
                    Require(fields.Top==-scrollbar.Value&&(scrollbar.MaximumValue==0||fields.Bottom==fields.Parent!.ClientSize.Height),
                        "rolagem alcança fim exato do conteúdo sem extensão vazia");
                    var key=typeof(DarkVScrollBar).GetMethod("OnKeyDown",flags)!;
                    key.Invoke(scrollbar,[new KeyEventArgs(Keys.Home)]);
                    Require(fields.Top==0&&scrollbar.Value==0,"tecla Home retorna ao início");
                    key.Invoke(scrollbar,[new KeyEventArgs(Keys.End)]);
                    Require(scrollbar.Value==scrollbar.MaximumValue,"tecla End alcança último conteúdo");
                    scrollbar.Value=0;
                    var icon=shell.RightPane.Controls.Find("V269AppDetailIcon",true).Single();
                    Require(icon.Visible&&icon.AccessibleName?.Contains("7-Zip",StringComparison.Ordinal)==true,
                        "identidade visual do painel vinculada à seleção, sem arte fictícia");
                    using var image=new Bitmap(surface.Width,surface.Height);
                    surface.DrawToBitmap(image,new Rectangle(Point.Empty,surface.Size));image.Save($"LayoutCaptures/v269-aplicativos-DETALHES-SINTETICO-{(V269Theme)index}-{size.Width}x{size.Height}.png");
                    details.PerformClick();Application.DoEvents();
                    Require(list.Visible&&!shell.RightPane.Visible,"botão Detalhes do HTML fecha painel");
                }
                if(size.Width>1040)
                {
                    var splitter=form.Controls.Find("V269AppSplitter",true).Single();
                    int previous=shell.RightPane.Width;
                    typeof(V269AppSplitter).GetMethod("OnKeyDown",flags)!.Invoke(splitter,[new KeyEventArgs(Keys.Left)]);
                    Application.DoEvents();Require(shell.RightPane.Width>previous,"divisor real redimensiona painel por teclado");
                    typeof(V269AppSplitter).GetMethod("OnKeyDown",flags)!.Invoke(splitter,[new KeyEventArgs(Keys.Right)]);
                }
                Require(apps.RowCount==2&&apps.Controls.Count==2&&
                    apps.Controls.Find("V269AppsMore",true).Length==0&&
                    apps.Controls.Find("V269AppsFooter",true).Length==0&&
                    apps.Controls.Find("V269AppsExtraCommands",true).Length==0&&
                    apps.Controls.Find("V269AppsExtraScroll",true).Length==0,
                    "somente a barra superior e a tabela do HTML; sem rodapé ou comandos extras");
            }
            // Testar o hit-test sem disparar nenhum executável, inclusive com
            // o deslocamento horizontal aplicado.
            var horizontal=list.Controls.Find("V269AppsHorizontalScroll",false).OfType<DarkVScrollBar>().Single();
            surface.Size=new Size(760,596);surface.PerformLayout();Application.DoEvents();
            if(shell.RightPane.Visible)details.PerformClick();
            if(horizontal.Visible)horizontal.Value=horizontal.MaximumValue;
            var bounds=(Rectangle)typeof(DarkProgramList).GetMethod("RowBounds",flags)!.Invoke(list,[2])!;
            var action=V269AppTable.Action(bounds,shell.InterfaceScalePercent,list.DeviceDpi,true,list.TableCompact);
            var hit=((ProgramEntry? Item,string Action))typeof(DarkProgramList).GetMethod("HitAction",flags)!.Invoke(list,
                [new Point(action.X+action.Width/2,action.Y+action.Height/2)])!;
            Require(hit.Item is null,"remoção bloqueada também após deslocamento horizontal");
            // Contratos de cancelamento na própria interface, só tokens sintéticos:
            // nenhuma consulta, instalador, desinstalador ou limpeza é iniciado.
            var verify=typeof(MainForm).GetMethod("CheckUpdatesFromButtonAsync",flags)!;
            foreach(string fieldName in new[]{"_consultaCatalogoCts","_installerDownloadCts","_deploymentWaitCts"})
            {
                var field=typeof(MainForm).GetField(fieldName,flags)!;
                Require(field.GetValue(form) is null,"fixture sem operação prévia");
                using var cancel=new CancellationTokenSource();
                try
                {
                    field.SetValue(form,cancel);
                    ((Task)verify.Invoke(form,[])!).GetAwaiter().GetResult();
                    Require(cancel.IsCancellationRequested,"botão Verificar encaminha cancelamento: "+fieldName);
                }
                finally{field.SetValue(form,null);}
            }
            var previousOperation=list.UninstallOperationFor;
            try
            {
                list.UninstallOperationFor=item=>ReferenceEquals(item,entries[2])?
                    new UninstallOperationSnapshot(UninstallStage.Inspecting,"Verificando resíduos","Fixture consultiva"):null;
                Require(list.AccessibilityObject.GetChild(2)?.Description?.Contains("Verificando resíduos",StringComparison.Ordinal)==true,
                    "estado de desinstalação legível sem depender de cor ou tooltip flutuante");
                const string syntheticResult="Resultado sintético da seleção; nenhuma remoção executada.";
                list.UninstallOperationFor=item=>ReferenceEquals(item,entries[0])?
                    new UninstallOperationSnapshot(UninstallStage.Cancelled,"Operação cancelada",syntheticResult):null;
                shell.PresentPanel(null);list.SelectedIndex=0;
                var present=typeof(MainForm).GetMethod("PresentV269AppDetails",flags)!;
                present.Invoke(form,[entries[0]]);
                Require(shell.RightPane.Current?.Application?.Reasons.Contains(syntheticResult,StringComparison.Ordinal)==true,
                    "resultado de sessão aparece na nota do painel direito, sem botão de histórico");
                list.SelectedIndex=1;present.Invoke(form,[entries[1]]);
                Require(shell.RightPane.Current?.Application is {} context&&
                    !context.Reasons.Contains(syntheticResult,StringComparison.Ordinal),
                    "resultado de outro aplicativo nunca é atribuído à seleção");
            }
            finally{list.UninstallOperationFor=previousOperation;}
            // Só estados de apresentação; nenhum comando, rede ou manutenção.
            surface.Size=new Size(1200,659);surface.PerformLayout();
            shell.PresentPanel(null);list.SelectedIndex=0;
            var record=typeof(MainForm).GetMethod("RecordV269AppResult",flags)!;
            var oldResult=new V269PanelPresentation(V269PanelKind.Result,"Resultado de fixture","Resultado real da fixture, sem execução.");
            shell.PresentPanel(oldResult);record.Invoke(form,[oldResult.Title,oldResult.Description,oldResult]);
            Require(shell.RightPane.Current?.Application?.PreviousResults is {Count:1},
                "resultado retido migra para seção contextual sem bloquear a seleção");
            list.SelectedIndex=1;Application.DoEvents();
            Require(shell.RightPane.Current?.Application is {Name:"Discord",PreviousResults:{Count:1}} contextHistory&&
                contextHistory.PreviousResults[0].Description==oldResult.Description&&
                !contextHistory.Reasons.Contains(oldResult.Description,StringComparison.Ordinal),
                "seleção independente; resultado anterior não atribuído ao novo aplicativo");
            // Evidência adicional: mesma tabela preenchida, mesmas dimensões,
            // seleção real e resultados de fixture, sem invocar manutenção.
            int historyTheme=theme.SelectedIndex;
            foreach(int historyIndex in new[]{0,1,2})
            {
                theme.SelectedIndex=historyIndex;
                surface.PerformLayout();Application.DoEvents();
                Require(shell.RightPane.Current?.Application?.PreviousResults is {Count:1} history&&
                    history[0].Description==oldResult.Description,
                    "mudar tema não descarta nem atribui o histórico a outra seleção");
                var panelTitle=shell.RightPane.Controls.Find("V269PanelTitle",true).OfType<Label>().Single();
                int S(int n)=>V269AppTable.Scale(n,shell.InterfaceScalePercent,panelTitle.DeviceDpi);
                Require(panelTitle.TextAlign==ContentAlignment.TopLeft&&panelTitle.Margin.Bottom==S(12)&&
                    panelTitle.Height>=S(33),"título de detalhes alinhado ao topo, com separador e margem do HTML");
                var historyHeading=shell.RightPane.Controls.Find("V269AppHistoryHeading",true).OfType<Label>().Single();
                var descriptionHeading=shell.RightPane.Controls.Find("V269AppDescriptionHeading",true).OfType<Label>().Single();
                var note=shell.RightPane.Controls.Find("V269AppDetailNote",true).OfType<Label>().Single();
                Require(historyHeading.Font.Bold&&descriptionHeading.Font.Bold&&
                    descriptionHeading.Font.SizeInPoints>historyHeading.Font.SizeInPoints&&
                    note.Padding.Left>0&&historyHeading.Padding.Top>0,
                    "hierarquia tipográfica, nota destacada e separação do histórico do HTML");
                var historyTable=shell.RightPane.Controls.Find("V269AppDetailsFields",true).OfType<TableLayoutPanel>().Single();
                var detailPane=shell.RightPane.Controls.Find("V269StructuredAppDetails",true).OfType<V269AppDetailPanel>().Single();
                Require(Enumerable.Range(0,5).All(detailPane.HasAppFieldDivider)&&!detailPane.HasAppFieldDivider(5),
                    "divisores do HTML limitados à identidade e aos quatro campos");
                int halfGap=V269AppTable.Scale(6,100,detailPane.DeviceDpi);
                for(int fieldRow=1;fieldRow<=4;fieldRow++)
                {
                    Require(historyTable.GetControlFromPosition(0,fieldRow) is Label caption&&caption.Margin.Right==halfGap&&
                        historyTable.GetControlFromPosition(1,fieldRow) is Label value&&value.Margin.Left==halfGap,
                        "intervalo horizontal de 12 px entre rótulo e valor");
                }
                Require(historyTable.ColumnStyles[0].SizeType==SizeType.Absolute&&
                    historyTable.ColumnStyles[0].Width<historyTable.ClientSize.Width*.4,
                    "rótulos reservam a largura necessária e deixam o restante para os valores");
                var lastHistoryRow=historyTable.Controls.OfType<V269AppHistoryRow>().Single();
                Require(historyHeading.Width>=historyTable.ClientSize.Width-2&&!lastHistoryRow.HasBottomDivider,
                    "divisor de largura integral e ausência de borda após o último resultado, conforme HTML");
                var captureScroll=shell.RightPane.Controls.Find("V269AppDetailsScroll",true)
                    .OfType<DarkVScrollBar>().Single();
                foreach(bool bottom in new[]{false,true})
                {
                    captureScroll.Value=bottom?captureScroll.MaximumValue:0;
                    Application.DoEvents();
                    using var capture=new Bitmap(surface.Width,surface.Height);
                    surface.DrawToBitmap(capture,new Rectangle(Point.Empty,surface.Size));
                    capture.Save($"LayoutCaptures/v269-aplicativos-HISTORICO-SINTETICO-{(V269Theme)historyIndex}-{(bottom?"final":"inicio")}-{surface.Width}x{surface.Height}.png");
                }
            }
            theme.SelectedIndex=historyTheme;surface.PerformLayout();Application.DoEvents();
            Console.WriteLine("PASS Aplicativos V2.6.9: seis capturas de histórico contextual nos três temas.");
            // Reproduzir a restauração de um resultado já registrado enquanto
            // a janela estava oculta, sem iniciar a bandeja ou manutenção real.
            var pendingResult=typeof(MainForm).GetField("_pendingUninstallResult",flags)!;
            var resumeResult=typeof(MainForm).GetMethod("ResumeUninstallResult",flags)!;
            Require(pendingResult.GetValue(form) is null,"fixture sem resultado pendente");
            pendingResult.SetValue(form,oldResult);
            resumeResult.Invoke(form,[]);Application.DoEvents();
            Require(ReferenceEquals(shell.RightPane.Current,oldResult)&&pendingResult.GetValue(form) is null,
                "restauração preserva identidade e consome resultado pendente");
            list.SelectedIndex=0;Application.DoEvents();
            Require(shell.RightPane.Current?.Application is {PreviousResults:{Count:1}} restored&&
                restored.Name==entries[0].Name&&restored.PreviousResults[0].Description==oldResult.Description,
                "resultado restaurado não bloqueia seleção e permanece no histórico contextual");
            Console.WriteLine("PASS Aplicativos V2.6.9: resultado restaurado conserva identidade e seleção.");
            void CaptureOperationalHistory(string phase)
            {
                int before=theme.SelectedIndex;
                foreach(int index in new[]{0,1,2})
                {
                    theme.SelectedIndex=index;surface.PerformLayout();Application.DoEvents();
                    Require(shell.RightPane.Controls.Find("V269AppOperationHistory",true).Single().Visible,
                        "histórico operacional presente nos três temas");
                    var description=shell.RightPane.Controls.Find("V269PanelDescription",true).Single();
                    Require(description.Height<surface.Height/3,"descrição curta não ocupa espaço desnecessário");
                    var viewport=shell.RightPane.Controls.Find("V269AppFlowViewport",true).OfType<Panel>().Single();
                    Require(!viewport.VerticalScroll.Visible&&!viewport.HorizontalScroll.Visible&&
                        !shell.RightPane.Controls.Find("V269CleanupFlowScroll",true).Single().Visible && description is TextBox {ScrollBars:ScrollBars.None},"conteúdo curto não apresenta barras de rolagem");
                    var operationalHistoryBar=shell.RightPane.Controls.Find("V269AppOperationHistoryScroll",true).Single();
                    Require(!operationalHistoryBar.Visible,"histórico que cabe não possui rolagem interna");
                    var heading=shell.RightPane.Controls.Find("V269AppFlowHeading",true).Single();
                    Require(heading.Padding.Left>0&&heading.Padding.Bottom>0,"cabeçalho operacional reserva ícone e divisor");
                    if(phase=="CONFIRMACAO")
                    {
                        foreach(string name in new[]{"V269AcceptConfirmation","V269CancelConfirmation"})
                        {
                            var action=shell.RightPane.Controls.Find(name,true).OfType<V269AppFlowButton>().Single();
                            Require(action.Visible&&action.Width>=action.RequiredAppTextWidth(),"texto operacional completo nos três temas");
                        }
                        var operationHost=(Control)typeof(V269RightPane).GetField("_operationContent",flags)!.GetValue(shell.RightPane)!;
                        Require(!operationHost.Visible,"autorização não exibe status residual de outra operação");
                    }
                    else
                    {
                        var cancel=form.Controls.Find("UninstallCancel",true).OfType<ModernButton>().Single();
                        Require(cancel.Visible&&cancel.OutlineColor==V269AppDetailStyle.Palette((V269Theme)index).Border,
                            "cancelamento operacional mantém botão real e contorno temático");
                        var status=(Control)typeof(MainForm).GetField("_status",flags)!.GetValue(form)!;
                        Require(!status.Visible,"progresso utiliza descrição corrente sem status duplicado");
                        var log=(DarkLogView)typeof(MainForm).GetField("_log",flags)!.GetValue(form)!;
                        Require(!log.Visible&&log.RenderedText.Length>0,"console legado oculto sem apagar registro técnico");
                    }
                    using var image=new Bitmap(surface.Width,surface.Height);
                    surface.DrawToBitmap(image,new Rectangle(Point.Empty,surface.Size));
                    image.Save($"LayoutCaptures/v269-aplicativos-{phase}-HISTORICO-SINTETICO-{(V269Theme)index}-{surface.Width}x{surface.Height}.png");
                }
                theme.SelectedIndex=before;surface.PerformLayout();Application.DoEvents();
            }
            Rectangle ScreenBounds(Control c)=>c.RectangleToScreen(c.ClientRectangle);
            var confirmationMethod=typeof(MainForm).GetMethod("ConfirmV269Async",flags)!;
            // A suíte completa abre e fecha outros formulários e diálogos.
            // Use um contexto WinForms explícito para esta espera assíncrona,
            // sem alterar o agendamento do motor em produção.
            var previousContext=SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(confirmationContext);
            using var confirmationToken=new CancellationTokenSource();
            Task<bool> confirmTask;
            try
            {
            confirmTask=(Task<bool>)confirmationMethod.Invoke(form,["Confirmar fixture","Alvo sintético",confirmationToken.Token])!;
            Application.DoEvents();
            var confirmationPresentation=shell.RightPane.Current;
            var operationHistory=shell.RightPane.Controls.Find("V269AppOperationHistory",true).Single();
            Require(operationHistory.Visible&&!confirmTask.IsCompleted&&
                operationHistory.Controls.Find("V269AppPreviousResult",true).Single().AccessibleDescription==oldResult.Description&&
                shell.RightPane.Controls.Find("V269AcceptConfirmation",true).Single().Enabled,
                "histórico durante confirmação sem resolver ou ocultar autorização");
            shell.SelectPage(V269Page.Drivers);Application.DoEvents();
            Require(shell.RightPane.Controls.Find("V269AppOperationHistory",true).Length==0&&!confirmTask.IsCompleted,
                "histórico de Aplicativos não vaza para outra aba nem resolve confirmação");
            shell.SelectPage(V269Page.Aplicativos);Application.DoEvents();
            Require(shell.RightPane.Controls.Find("V269AppOperationHistory",true).Single().Visible&&
                ReferenceEquals(shell.RightPane.Current,confirmationPresentation),"retorno conserva confirmação e histórico");
            var confirmationHistory=shell.RightPane.Controls.Find("V269AppOperationHistory",true).OfType<V269AppOperationHistory>().Single();
            var confirmationDescription=shell.RightPane.Controls.Find("V269PanelDescription",true).Single();
            var accept=shell.RightPane.Controls.Find("V269AcceptConfirmation",true).Single();
            Require(!ScreenBounds(confirmationHistory).IntersectsWith(ScreenBounds(confirmationDescription))&&
                confirmationDescription.Height>0&&confirmationHistory.Height>0&&
                ScreenBounds(confirmationHistory).Top>=ScreenBounds(accept).Bottom,
                "histórico abaixo da autorização, sem cobrir a descrição nem os botões");
            Require(shell.RightPane.Controls.Find("V269AppFlowHeading",true).Single().Text=="Confirmação da ação",
                "cabeçalho operacional distingue estado de autorização e alvo");
            Require(shell.RightPane.Controls.Find("V269AppFlowState",true).Single() is {Visible:true,AccessibleName:"Aguardando autorização"}&&
                accept is V269AppFlowButton,"estado explícito e botão temático de autorização");
            CaptureOperationalHistory("CONFIRMACAO");
            var confirmationDismiss=confirmationHistory.Controls.Find("V269AppDismissResult",true).OfType<Button>().Single();
            confirmationHistory.EnsureVisible(confirmationDismiss);
            confirmationDismiss.PerformClick();Application.DoEvents();
            Require(!confirmTask.IsCompleted&&ReferenceEquals(shell.RightPane.Current,confirmationPresentation)&&
                shell.RightPane.Controls.Find("V269AppOperationHistory",true).Length==0,
                "dispensar não autoriza, cancela ou substitui confirmação");
            var pendingConfirmation=typeof(V269RightPane).GetField("_confirmation",flags)!
                .GetValue(shell.RightPane) as TaskCompletionSource<bool>;
            Require(pendingConfirmation is not null,"autorização ainda pendente antes do botão real");
            var cancelConfirmation=shell.RightPane.Controls.Find("V269CancelConfirmation",true).OfType<Button>().Single();
            Require(cancelConfirmation.Visible&&cancelConfirmation.Enabled,"cancelamento de confirmação acessível");
            cancelConfirmation.PerformClick();
            Require(pendingConfirmation!.Task.IsCompletedSuccessfully&&!pendingConfirmation.Task.Result,
                "botão real resolve a fonte da confirmação como recusa");
            Pump(confirmTask);
            Require(!confirmTask.Result&&shell.RightPane.Current?.Application?.PreviousResults is {Count:0},
                "cancelar não ressuscita resultado dispensado");
            Console.WriteLine("PASS Aplicativos V2.6.9: confirmação, histórico e cancelamento preservados.");
            }
            finally
            {
                confirmationToken.Cancel();
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
            record.Invoke(form,[oldResult.Title,oldResult.Description,null]);
            var previousRow=shell.RightPane.Controls.Find("V269AppPreviousResult",true).Single();
            var dismiss=previousRow.Controls.Find("V269AppDismissResult",true).OfType<Button>().Single();
            Require(dismiss.Enabled&&dismiss.AccessibleName?.Contains(oldResult.Title,StringComparison.Ordinal)==true,
                "Dispensar contextual acessível e associado à identidade do resultado");
            // PerformClick exige que a janela e a linha estejam visíveis.
            var historyScroll=shell.RightPane.Controls.Find("V269AppDetailsScroll",true).OfType<DarkVScrollBar>().Single();
            historyScroll.Value=historyScroll.MaximumValue;Application.DoEvents();dismiss.PerformClick();
            Require(shell.RightPane.Current?.Application?.PreviousResults is {Count:0}&&
                shell.RightPane.Controls.Find("V269AppPreviousResult",true).Length==0,
                "dispensar remove somente o resultado da apresentação");
            surface.Size=new Size(1200,659);surface.PerformLayout();
            var cancelField=typeof(MainForm).GetField("_uninstallCts",flags)!;
            var showCancel=typeof(MainForm).GetMethod("ShowUninstallCancellation",flags)!;
            Require(cancelField.GetValue(form) is null,"fixture sem desinstalação real");
            using(var cancel=new CancellationTokenSource())
            {
                try
                {
                    cancelField.SetValue(form,cancel);
                    shell.PresentPanel(new(V269PanelKind.Progress,"Operação em andamento","Fixture sem execução"));
                    record.Invoke(form,[oldResult.Title,oldResult.Description,null]);
                    showCancel.Invoke(form,[true]);Application.DoEvents();
                    var operational=shell.RightPane.Current;
                    var flowHistory=shell.RightPane.Controls.Find("V269AppOperationHistory",true).Single();
                    Require(flowHistory.Visible&&flowHistory.Controls.Find("V269AppPreviousResult",true).Length==1,
                        "histórico permanece acessível durante progresso");
                    Require(shell.RightPane.Controls.Find("V269AppFlowState",true).Single() is
                        {Visible:true,AccessibleName:"Em andamento · progresso não informado"},
                        "progresso sem porcentagem observada não inventa percentual");
                    CaptureOperationalHistory("PROGRESSO");
                    // Overflow real: o painel, não a descrição ou o histórico, é rolável.
                    var longFlowViewport=shell.RightPane.Controls.Find("V269AppFlowViewport",true).OfType<Panel>().Single();
                    shell.PresentPanel(operational! with {Description=string.Join("\r\n",Enumerable.Repeat("Linha de teste do acompanhamento.",80))});
                    surface.PerformLayout();Application.DoEvents();
                    var themedScroll = shell.RightPane.Controls.Find("V269CleanupFlowScroll",true).OfType<DarkVScrollBar>().Single();
                    Require(themedScroll.Visible && !longFlowViewport.AutoScroll && !longFlowViewport.HorizontalScroll.Visible &&
                        shell.RightPane.Controls.Find("V269PanelDescription",true).Single() is TextBox {ScrollBars:ScrollBars.None}&&
                        !shell.RightPane.Controls.Find("V269AppOperationHistoryScroll",true).Single().Visible,
                        "overflow real usa somente a rolagem externa do painel");
                    themedScroll.Value = themedScroll.MaximumValue;
                    Require(longFlowViewport.Controls.OfType<TableLayoutPanel>().Single().Top == -themedScroll.Value,
                        "rolagem temática alcança o fim do painel");
                    shell.PresentPanel(operational);
                    surface.PerformLayout();Application.DoEvents();
                    Require(!themedScroll.Visible && !longFlowViewport.VerticalScroll.Visible,"reduzir conteúdo remove a rolagem externa");
                    var flowDescription=shell.RightPane.Controls.Find("V269PanelDescription",true).Single();
                    var realCancel=form.Controls.Find("UninstallCancel",true).Single();
                    Require(!ScreenBounds(flowHistory).IntersectsWith(ScreenBounds(flowDescription))&&flowDescription.Height>0&&
                        ScreenBounds(flowHistory).Top>=ScreenBounds(realCancel).Bottom,
                        "histórico abaixo do cancelamento, sem sobrepor descrição de progresso");
                    var flowDismiss=flowHistory.Controls.Find("V269AppDismissResult",true).OfType<Button>().Single();
                    ((V269AppOperationHistory)flowHistory).EnsureVisible(flowDismiss);
                    flowDismiss.PerformClick();Application.DoEvents();
                    Require(ReferenceEquals(shell.RightPane.Current,operational)&&!cancel.IsCancellationRequested&&
                        shell.RightPane.Controls.Find("V269AppOperationHistory",true).Length==0,
                        "dispensar resultado durante progresso não substitui estado nem cancela motor");
                    var progressTitle=shell.RightPane.Controls.Find("V269PanelTitle",true).OfType<Label>().Single();
                    Require(progressTitle.TextAlign==ContentAlignment.MiddleLeft&&progressTitle.Margin==Padding.Empty,
                        "geometria exclusiva dos detalhes não modifica o cabeçalho operacional");
                    var button=form.Controls.Find("UninstallCancel",true).OfType<Button>().Single();
                    Require(button.Visible&&button.Enabled,"cancelamento acessível no painel de progresso");
                    button.PerformClick();
                    // ModernButton pode despachar o Click após a animação.
                    var dispatch=System.Diagnostics.Stopwatch.StartNew();
                    while(!cancel.IsCancellationRequested&&dispatch.Elapsed<TimeSpan.FromSeconds(3))
                    {Application.DoEvents();Thread.Sleep(10);}
                    Require(cancel.IsCancellationRequested,"cancelamento de desinstalação/limpeza encaminha token real");
                    Console.WriteLine("PASS Aplicativos V2.6.9: progresso, histórico e token de cancelamento preservados.");
                }
                finally
                {
                    cancelField.SetValue(form,null);showCancel.Invoke(form,[false]);
                    shell.PresentPanel(null);
                }
            }
            VerifyInventoryFlowStates(form, shell, V269Page.Aplicativos);
        }
        finally
        {
            inventory.Clear();inventory.AddRange(original);search.Clear();sort.SelectedIndex=0;
            theme.SelectedIndex=originalTheme;surface.Size=originalSize;
            render.Invoke(form,[]);shell.SelectPage(V269Page.Configuracoes);shell.PresentPanel(panel);
            surface.PerformLayout();Application.DoEvents();
        }
        Console.WriteLine("PASS Aplicativos V2.6.9: tabela real, pesquisa, ordenação, detalhes, ausência de controles extras e 15 capturas sintéticas.");
    }

    private static void VerifyRepair(MainForm form, V269Shell shell)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        var theme = form.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
        var originalSize = surface.Size;
        var originalTheme = theme.SelectedIndex;
        var originalPage = shell.SelectedPage;
        var originalPanel = shell.RightPane.Current;
        var oldContext = SynchronizationContext.Current;
        var paneContext = new PaneTestSynchronizationContext(form);
        var page = shell.PageContent(V269Page.Reparo)!;
        var cards = page.Controls.Find("RepairActions", true).OfType<DarkScrollableFlow>().Single()
            .Content.Controls.OfType<V269RepairCard>().ToArray();
        var history = (List<V269AppPreviousResult>)typeof(MainForm).GetField("_v269RepairPreviousResults", flags)!.GetValue(form)!;
        var repairable = typeof(MainForm).GetField("_windowsRepairable", flags)!;
        var scanned = typeof(MainForm).GetField("_windowsScannedAt", flags)!;
        var network = typeof(MainForm).GetField("_lastNetworkScan", flags)!;
        var refresh = typeof(MainForm).GetMethod("RefreshV269RepairCommands", flags)!;
        var active = typeof(MainForm).GetField("_v269RepairCommand", flags)!;
        var audioQuery = typeof(MainForm).GetField("_audioQuery", flags)!;
        var printerQuery = typeof(MainForm).GetField("_printerQuery", flags)!;
        var beforeRepairable = repairable.GetValue(form);
        var beforeScanned = scanned.GetValue(form);
        var beforeNetwork = network.GetValue(form);
        var beforeAudio = audioQuery.GetValue(form);
        var beforePrinter = printerQuery.GetValue(form);
        var printer = new TaskCompletionSource<PrinterQueueDiagnosis>(TaskCreationOptions.RunContinuationsAsynchronously);
        var audio = new TaskCompletionSource<AudioOutputDiagnosis>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Select(int index) => typeof(Control).GetMethod("OnClick", flags)!.Invoke(cards[index], [EventArgs.Empty]);
        void WaitUntil(Func<bool> ready, string message)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!ready() && clock.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents(); Thread.Sleep(5);
            }
            Require(ready(), message);
        }
        try
        {
            shell.PresentPanel(null);
            shell.SelectPage(V269Page.Reparo);
            surface.Size = new Size(1844, 1116);
            surface.PerformLayout(); Application.DoEvents();
            Require(cards.Length == 12 && cards.All(card => card.TabStop && card.Command.TabStop) &&
                !cards[1].Command.Enabled && !cards[3].Command.Enabled,
                "Reparo possui doze cartões e os dois reparos começam bloqueados");
            Require(cards.Select(card => card.Command.Text).SequenceEqual(new[]
            {
                "Verificar", "Reparar", "Analisar", "Reparar", "Analisar", "Analisar", "Analisar", "Analisar",
                "Analisar", "Restaurar", "Criar", "Abrir"
            }), "comandos seguem os verbos do HTML");
            repairable.SetValue(form, true); scanned.SetValue(form, DateTimeOffset.UtcNow);
            refresh.Invoke(form, null);
            Require(cards[1].Command.Enabled && !cards[3].Command.Enabled, "somente integridade reparável habilita Reparo Windows");
            scanned.SetValue(form, DateTimeOffset.UtcNow.AddMinutes(-31)); refresh.Invoke(form, null);
            Require(!cards[1].Command.Enabled, "a verificação expirada revoga a ação");
            repairable.SetValue(form, false);
            network.SetValue(form, new NetworkScan([
                new("Adaptador", "OK", "Fixture"), new("Gateway", "OK", "Fixture"),
                new("DNS", "Atenção", "Fixture"), new("DNS alternativo", "Atenção", "Fixture")], true));
            refresh.Invoke(form, null);
            Require(cards[3].Command.Enabled, "diagnóstico concordante habilita reparo guiado");
            network.SetValue(form, null); refresh.Invoke(form, null);
            Require(!cards[3].Command.Enabled, "revogar a leitura volta a bloquear o reparo");

            foreach (var size in new[] { new Size(1200, 659), new Size(1844, 1116) })
            foreach (V269Theme palette in Enum.GetValues<V269Theme>())
            {
                var caseClock=System.Diagnostics.Stopwatch.StartNew();
                theme.SelectedIndex = (int)palette; surface.Size = size; surface.PerformLayout(); Application.DoEvents();
                Select(0); Application.DoEvents();
                Require(shell.RightPane.Visible && shell.RightPane.Current?.Repair is { Name: "Verificar Windows" } model &&
                    model.Fields.Single() == ("Estado", "Aguardando execução") &&
                    shell.RightPane.Controls.Find("V269AppDescriptionHeading", true).Single().Text == "Função",
                    "identidade, função e estado ficam no painel lateral");
                // Repetir a solicitação sem mudança de entradas não pode
                // reconstruir os detalhes ou alterar a geometria medida.
                var retained = shell.RightPane.Current;
                var repairHost = page.Controls.Find("RepairActions", true).OfType<DarkScrollableFlow>().Single();
                var bounds = cards.Select(card => card.Bounds).ToArray();
                var margins = cards.Select(card => card.Margin).ToArray();
                int layouts = 0;
                void CountLayout(object? sender, LayoutEventArgs args) => layouts++;
                repairHost.Content.Layout += CountLayout;
                try
                {
                    for (int repeat = 0; repeat < 3; repeat++) ((Action)repairHost.Tag!)();
                    Require(layouts == 0 && bounds.SequenceEqual(cards.Select(card => card.Bounds)) &&
                        margins.SequenceEqual(cards.Select(card => card.Margin)),
                        "Fit repetido não remede nem relayouta cartões estáveis");
                }
                finally { repairHost.Content.Layout -= CountLayout; }
                Select(0);
                Require(ReferenceEquals(retained, shell.RightPane.Current),
                    "seleção repetida conserva a mesma apresentação de detalhes");
                var repairSurface = form.Controls.Find("V269RepairSurface", true).Single();
                var repairViewport = form.Controls.Find("V269RepairViewport", true).OfType<DarkScrollableFlow>().Single();
                var repairFlow = form.Controls.Find("RepairActions", true).OfType<DarkScrollableFlow>().Single();
                Require(repairSurface.BackColor == Color.Transparent && repairViewport.BackColor == Color.Transparent &&
                    repairFlow.Content.BackColor == Color.Transparent && repairFlow.Width == repairViewport.CleanupViewportWidth,
                    "Reparo preserva o fundo do HTML e ocupa a largura efetiva do viewport");
                var rows = cards.GroupBy(card => card.Top).ToArray();
                Require(rows.All(row => row.Count() <= 3) && (size.Width < 1844 || rows.Length == 4) &&
                    cards.All(card => card.Command.Right <= card.ClientSize.Width && card.Command.Bottom <= card.ClientSize.Height),
                    "grade responsiva preserva todos os comandos dentro dos cartões");
                TimedMaintenanceCapture(surface,$"LayoutCaptures/v269-reparo-DETALHES-{palette}-{surface.Width}x{surface.Height}.png",
                    "Reparo_detalhes",palette,size,caseClock.ElapsedMilliseconds);
            }

            // As consultas exercitam os manipuladores reais com tarefas sintéticas já pendentes.
            // Nenhuma API do Windows, consulta de rede ou manutenção é iniciada pelo fixture.
            SynchronizationContext.SetSynchronizationContext(paneContext);
            printerQuery.SetValue(form, printer.Task);
            cards[5].Command.PerformClick();
            WaitUntil(() => shell.RightPane.Current is { Kind: V269PanelKind.Progress, FlowPage: V269Page.Reparo }, "diagnóstico começa no painel");
            printer.SetResult(new(PrinterQueueState.NoOfflineIndication));
            WaitUntil(() => active.GetValue(form) is null && shell.RightPane.Current?.FlowResult is { Page: V269Page.Reparo, Complete: true },
                "resultado real do manipulador depende da evidência da consulta");
            Require(history.Count == 1 && history[0].Title == "Diagnosticar impressora", "resultado associado à ferramenta de origem");

            SynchronizationContext.SetSynchronizationContext(paneContext);
            audioQuery.SetValue(form, audio.Task);
            cards[4].Command.PerformClick();
            WaitUntil(() => shell.RightPane.Current is { Kind: V269PanelKind.Progress, CancelFlow: not null }, "consulta cancelável usa a espera real");
            shell.SelectPage(V269Page.Aplicativos); Application.DoEvents();
            Require(shell.RightPane.Current?.FlowPage == V269Page.Reparo, "navegação mantém operação de Reparo ativa");
            shell.SelectPage(V269Page.Reparo); Application.DoEvents();
            shell.RightPane.Controls.Find("V269CancelOwnedFlow", true).OfType<Button>().Single().PerformClick();
            WaitUntil(() => active.GetValue(form) is null && shell.RightPane.Current?.FlowResult is { Page: V269Page.Reparo, Complete: false },
                "cancelamento encerra somente a espera e não inventa sucesso");
            Require(!audio.Task.IsCompleted && history.Count == 2 && history[0].Title == "Diagnosticar áudio", "consulta em segundo plano não vira reparo nem é repetida");
            audio.SetResult(new(AudioOutputState.DefaultAvailable, 1));

            SynchronizationContext.SetSynchronizationContext(paneContext);
            cards[10].Command.PerformClick();
            WaitUntil(() => shell.RightPane.Current is { Kind: V269PanelKind.Confirmation, Title: "Criar ponto de restauração" },
                "o comando sensível abre confirmação com a identidade da ferramenta");
            Require(shell.RightPane.Controls.Find("V269AppOperationHistory", true).Single().Visible, "resultados anteriores durante a confirmação");
            shell.RightPane.ResolveConfirmation(false);
            WaitUntil(() => active.GetValue(form) is null, "recusa encerra a confirmação sem executar manutenção");
            Require(history.Count == 2 && shell.RightPane.Current?.Repair?.Name == "Criar ponto de restauração", "recusa preserva histórico e detalhes");
            VerifyInventoryFlowStates(form, shell, V269Page.Reparo);
            Console.WriteLine("PASS Reparo e Rede V2.6.9: doze cartões, três temas, elegibilidade, painel, cancelamento e histórico.");
        }
        finally
        {
            shell.RightPane.ResolveConfirmation(false);
            printer.TrySetResult(new(PrinterQueueState.Inconclusive));
            audio.TrySetResult(new(AudioOutputState.Inconclusive));
            repairable.SetValue(form, beforeRepairable); scanned.SetValue(form, beforeScanned);
            network.SetValue(form, beforeNetwork); audioQuery.SetValue(form, beforeAudio); printerQuery.SetValue(form, beforePrinter);
            refresh.Invoke(form, null);
            SynchronizationContext.SetSynchronizationContext(oldContext);
            history.Clear(); theme.SelectedIndex = originalTheme; surface.Size = originalSize;
            shell.SelectPage(originalPage); shell.PresentPanel(originalPanel);
        }
    }

    private static void VerifyLastTabs(MainForm form,V269Shell shell)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var surface=form.Controls.OfType<TableLayoutPanel>().Single();
        var theme=form.Controls.Find("V269ThemeChoice",true).OfType<ComboBox>().Single();
        var savedSize=surface.Size;int savedTheme=theme.SelectedIndex;
        var savedContext=SynchronizationContext.Current;
        var catalog=typeof(MainForm).GetField("_updateCatalog",flags)!;
        var checkedField=typeof(MainForm).GetField("_windowsUpdateChecked",flags)!;
        var draw=typeof(MainForm).GetMethod("DrawWindowsUpdates",flags)!;
        var setModule=typeof(MainForm).GetMethod("SetSystemView",flags)!;
        var refresh=typeof(MainForm).GetMethod("RefreshStartupEntriesAsync",flags)!;
        var confirm=typeof(MainForm).GetMethod("ConfirmV269Async",flags)!;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new PaneTestSynchronizationContext(form));
            shell.PresentPanel(null);shell.SelectPage(V269Page.Sistema);
            draw.Invoke(form,null);
            Require(form.Controls.Find("WindowsUpdateExplicitSearch",true).OfType<Button>().Single().Enabled&&
                !(bool)checkedField.GetValue(form)!,"Windows Update aguarda comando e não busca ao abrir");
            catalog.SetValue(form,new WinUpdateChoice[]{
                new("00000000-0000-0000-0000-000000000101:1","Atualização cumulativa de teste","KB-FIXTURE",false,true),
                new("00000000-0000-0000-0000-000000000102:1","Atualização com licença pendente","KB-FIXTURE",true,false)});
            checkedField.SetValue(form,true);draw.Invoke(form,null);
            var balanced=Guid.NewGuid();var saver=Guid.NewGuid();var ultimate=Guid.NewGuid();
            typeof(MainForm).GetMethod("ApplyV269PowerSnapshot",flags)!.Invoke(form,[
                new PowerPlanSnapshot(balanced,new Dictionary<string,Guid>{{"balanced",balanced},{"saver",saver},{"ultimate",ultimate}},null),false]);
            Require(form.Controls.Find("V269Hibernation",true).OfType<V269SystemSwitch>().Single().State==false,
                "estado de hibernação vem da consulta e não de simulação");

            var installs=form.Controls.Find("V269InstallWindowsUpdate",true).OfType<Button>().ToArray();
            Require(installs.Length==2&&installs[0].Enabled&&!installs[1].Enabled,"ofertas e licença real governam cada botão");
            form.PreviewMemoryRamForTests(new MemoryRamSample(DateTime.UtcNow,32UL<<30,18UL<<30));
            form.PreviewMemoryRamForTests(new MemoryRamSample(DateTime.UtcNow,32UL<<30,16UL<<30));
            Directory.CreateDirectory("LayoutCaptures");
            foreach(var size in new[]{new Size(1844,1116),new Size(1200,659)})
            foreach(var palette in Enum.GetValues<V269Theme>())
            {
                surface.Size=size;theme.SelectedIndex=(int)palette;surface.PerformLayout();Application.DoEvents();
                var ramMeter = form.Controls.Find("V269RamUsageMeter",true).OfType<GfDynamicProgressBar>().Single();
                var expectedRam = GfMeterScale.Frame(GfMeterKind.Usage,50,palette);
                Require(ramMeter.CurrentFrame == expectedRam &&
                    ramMeter.AccessibleDescription?.Contains("50") == true,
                    "RAM utiliza componente e escala cromática do Monitor nos três temas");
                var modules=form.Controls.Find("V269SystemModules",true).Single();
                var sections=new[]{"V269WindowsUpdateSection","V269EnergySection","MemoryRamDashboard"}
                    .Select(n=>modules.Controls.Find(n,false).Single()).ToArray();
                Require(sections.All(c=>c.Visible&&c.Right<=modules.ClientSize.Width)&&sections[0].Bottom<=sections[1].Top&&sections[1].Bottom<=sections[2].Top,
                    "Sistema: três seções sequenciais sem sobreposição");
                var ramSection=(TableLayoutPanel)sections[2];
                var graph=ramSection.Controls.OfType<MemoryRamChart>().Single();
                int S(int n)=>V269AppTable.Scale(n,shell.InterfaceScalePercent,graph.DeviceDpi);
                Require(graph.PlotArea.Height>=S(56) && graph.ClientRectangle.Contains(graph.PlotArea),
                    "REC-FIS-RAM: altura útil real, não apenas altura do cartão");
                foreach(string labelName in new[]{"V269RamUsageCaption","MemoryUsed","MemoryAvailable","MemoryCandidates","MemoryCleaned","V269RamAutomaticCaption"})
                {
                    var label=ramSection.Controls.Find(labelName,true).OfType<Label>().Single();
                    int required=TextRenderer.MeasureText(label.Text,label.Font,
                        new Size(Math.Max(1,label.ClientSize.Width-label.Padding.Horizontal),1000),
                        TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.TextBoxControl).Height;
                    Require(label.Height-label.Padding.Vertical>=required,
                        "REC-FIS-RAM: texto completo "+labelName+"/"+palette+"/"+size);
                }
                var manualRam=ramSection.Controls.Find("MemoryManualOptimization",true).OfType<Button>().Single();
                var autoRam=ramSection.Controls.Find("MemoryAutoOptimization",true).OfType<Button>().Single();
                Require(manualRam.Parent!.ClientRectangle.Contains(manualRam.Bounds)&&
                    autoRam.Parent!.ClientRectangle.Contains(autoRam.Bounds)&&!manualRam.Bounds.IntersectsWith(autoRam.Bounds),
                    "REC-FIS-RAM: comandos dentro do viewport local sem sobreposição");
                var updateRows=form.Controls.Find("V269SystemUpdateRow",true).OfType<V269SystemUpdateRow>().ToArray();
                Require(updateRows.Length==2&&updateRows[1].Top>=updateRows[0].Bottom&&
                    updateRows.All(row=>row.Right<=row.Parent!.ClientSize.Width&&row.Bottom<=row.Parent.ClientSize.Height),
                    "ofertas em duas linhas verticais, inclusive após alternar tema e tamanho");
                using var image=new Bitmap(surface.Width,surface.Height);surface.DrawToBitmap(image,new Rectangle(Point.Empty,surface.Size));
                image.Save($"LayoutCaptures/v269-sistema-SINTETICO-{palette}-{size.Width}x{size.Height}.png");
                shell.PresentPanel(new(V269PanelKind.Progress,"Verificar atualizações","Consultando ofertas oficiais.",FlowPage:V269Page.Sistema));
                surface.PerformLayout();Application.DoEvents();
                var systemProgress=shell.RightPane.Controls.Find("V269FlowPercentMeter",true).Single();
                var systemBar=systemProgress.Controls.OfType<GfDynamicProgressBar>().Single();
                Require(systemProgress.Visible && systemBar.Visible && systemBar.CurrentFrame.State==GfMeterState.Indeterminate &&
                    systemBar.CurrentFrame.Fraction is null && !shell.RightPane.Controls.Find("V269PanelProgress",true).Single().Visible,
                    "REC-FIS-PROGRESS: Windows Update indeterminado, sem duplicação");
                shell.PresentPanel(null);
            }
            shell.PresentPanel(null);setModule.Invoke(form,["update"]);
            var pending=(Task<bool>)confirm.Invoke(form,["Confirmar Windows Update","Fixture consultiva, nenhuma operação.",CancellationToken.None])!;
            Require(shell.RightPane.Current?.Kind==V269PanelKind.Confirmation,"Sistema confirma no painel lateral");
            shell.SelectPage(V269Page.Inicializacao);shell.RightPane.ResolveConfirmation(false);Pump(pending);
            Require(!pending.Result,"cancelamento após navegação não autoriza Windows Update");
            IReadOnlyList<StartupItem> entries=[
                new("Discord","Meu usuário · Registro Run · C:/Fixture/Discord.exe","registry","Discord",true,true),
                new("NVIDIA App","Meu usuário · Registro Run · C:/Fixture/Nvidia.exe","registry","Nvidia",false,true),
                new("Entrada protegida","Todos os usuários · Registro 64 bits · somente leitura · C:/Fixture/System.exe","machine","Protected",true,false),
                new("Aplicativo de teste","Meu usuário · Pasta Inicializar · C:/Fixture/Startup.lnk","file","C:/Fixture/Startup.lnk",true,true),
                new("Aplicativo de outro usuário","Todos os usuários · Pasta Inicializar · somente leitura","machine","C:/Fixture/Startup2.lnk",true,false)];
            var read=(Func<Task<IReadOnlyList<StartupItem>>>)(()=>Task.FromResult(entries));
            var consult=(Task)refresh.Invoke(form,[read])!;Pump(consult);
            var cards=form.Controls.Find("StartupEntryCard",true).OfType<V269StartupCard>().ToArray();
            Require(cards.Length==entries.Count&&cards.All(c=>c.Command.Enabled==c.Item!.Editable)&&
                cards.Where(c=>!c.Item!.Editable).All(c=>c.Command.Text=="Somente leitura"),"Inicialização respeita motor de elegibilidade");
            typeof(Control).GetMethod("OnClick",flags)!.Invoke(cards[0],[EventArgs.Empty]);
            Require(shell.RightPane.Current?.Module?.Name==cards[0].AccessibleName,$"seleção de inicialização apresenta exatamente a entrada: estado={shell.RightPane.Current?.Kind}, nome={shell.RightPane.Current?.Module?.Name}, esperado={cards[0].AccessibleName}");
            foreach(var size in new[]{new Size(1844,1116),new Size(1200,659)})
            foreach(var palette in Enum.GetValues<V269Theme>())
            {
                surface.Size=size;theme.SelectedIndex=(int)palette;surface.PerformLayout();Application.DoEvents();
                var viewport=form.Controls.Find("V269StartupViewport",true).OfType<DarkScrollableFlow>().Single();
                Require(cards.All(c=>c.Right<=viewport.Content.ClientSize.Width&&c.Command.Right<=c.Width&&c.Command.Bottom<=c.Height),
                    "cartões e comandos de Inicialização respeitam área útil");
                using var image=new Bitmap(surface.Width,surface.Height);surface.DrawToBitmap(image,new Rectangle(Point.Empty,surface.Size));
                image.Save($"LayoutCaptures/v269-inicializacao-SINTETICO-{palette}-{size.Width}x{size.Height}.png");
            }
            Console.WriteLine("PASS V2.6.9 Sistema e Inicialização: três módulos simultâneos, inventário, elegibilidade, confirmação e capturas nos três temas.");
        }
        finally
        {
            catalog.SetValue(form,Array.Empty<WinUpdateChoice>());checkedField.SetValue(form,false);
            theme.SelectedIndex=savedTheme;surface.Size=savedSize;surface.PerformLayout();
            SynchronizationContext.SetSynchronizationContext(savedContext);shell.PresentPanel(null);
        }
    }

    private static void VerifyInventoryFlowStates(MainForm form, V269Shell shell, V269Page page)
    {
        var theme = form.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        var originalSize = surface.Size;
        var originalTheme = theme.SelectedIndex;
        var previous = shell.RightPane.Current;
        string tab = page == V269Page.Drivers ? "drivers" : page == V269Page.Limpeza ? "limpeza" : page == V269Page.Reparo ? "reparo" : "aplicativos";
        var oldContext = SynchronizationContext.Current;
        using var cancellation = new CancellationTokenSource();
        try
        {
            // Manter as continuações no HWND da janela principal, inclusive depois
            // de fechar um formulário filho no fixture sem Application.Run.
            var paneContext = new PaneTestSynchronizationContext(form);
            shell.SelectPage(page);
            surface.Size = new Size(1200, 659);
            Control Find(string name) => shell.RightPane.Controls.Find(name, true).Single();
            void Capture(string stage, V269Theme palette)
            {
                using var image = new Bitmap(surface.Width, surface.Height);
                surface.DrawToBitmap(image, new Rectangle(Point.Empty, surface.Size));
                image.Save($"LayoutCaptures/v269-{tab}-PAINEL-{stage}-{palette}-{surface.Width}x{surface.Height}.png");
            }
            if (page == V269Page.Drivers)
            {
                using var review = new Form { TopLevel = false, Dock = DockStyle.Fill, FormBorderStyle = FormBorderStyle.None };
                review.Controls.Add(new Label { Text = "Revisão de oferta de teste", Dock = DockStyle.Fill });
                shell.PresentPanel(new(V269PanelKind.DriverReview, "Revisar driver de teste", "", DriverReview: review));
                surface.PerformLayout();
                Application.DoEvents();
                Require(review.Visible && !review.TopLevel && review.Parent == Find("V269DriverReviewHost"),
                    "a revisão WUA usa a área do painel");
                SynchronizationContext.SetSynchronizationContext(paneContext);
                var authorization = shell.RightPane.ConfirmAsync("Confirmar instalação física de driver",
                    "Alvo sintético. Nenhum executor é chamado.", cancellation.Token);
                Application.DoEvents();
                Require(!review.Visible && Find("V269AcceptConfirmation").Visible,
                    "a confirmação lateral substitui temporariamente a revisão");
                shell.RightPane.ResolveConfirmation(false);
                Pump(authorization);
                Require(!authorization.Result && review.Visible && ReferenceEquals(shell.RightPane.Current?.DriverReview, review),
                    "recusar restaura a mesma revisão lateral");
                shell.SelectPage(V269Page.Aplicativos);
                Require(review.IsDisposed && shell.RightPane.Current?.DriverReview is null,
                    "trocar de aba encerra a revisão sem operação");
                shell.SelectPage(page);
            }
            foreach (var size in new[] { new Size(1200, 659), new Size(1844, 1116) })
            {
                surface.Size = size;
                surface.PerformLayout();
                Application.DoEvents();
                foreach (V269Theme palette in Enum.GetValues<V269Theme>())
                {
                    theme.SelectedIndex = (int)palette;
                    SynchronizationContext.SetSynchronizationContext(paneContext);
                    var confirmation = shell.RightPane.ConfirmAsync($"Atualizar {tab}", "Alvo sintético. Nenhum executor é chamado.", cancellation.Token);
                    surface.PerformLayout();
                    Application.DoEvents();
                    Require(Find("V269FlowNotice").Visible && Find("V269AcceptConfirmation").Visible &&
                        !Find("V269FlowPercentMeter").Visible,
                        "confirmação mantém aviso próprio, dois comandos e nenhuma barra");
                    Capture("CONFIRMACAO", palette);
                    shell.RightPane.ResolveConfirmation(false);
                    Pump(confirmation);
                    Require(!confirmation.Result, "a captura não confirma uma ação");

                    bool cancelled = false;
                    Action? cancel = page is V269Page.Drivers or V269Page.Limpeza or V269Page.Reparo ? () => cancelled = true : null;
                    shell.PresentPanel(new(V269PanelKind.Progress, $"Verificar {tab}", "7 de 25 itens consultados.",
                        28, CancelFlow: cancel, FlowPage:page));
                    surface.PerformLayout();
                    Application.DoEvents();
                    var meter = Find("V269FlowPercentMeter");
                    var note = Find("V269FlowNotice");
                    Require(meter.Visible && meter.AccessibleDescription == "28%" && note.Visible &&
                        meter.PointToScreen(Point.Empty).Y < note.PointToScreen(Point.Empty).Y,
                        "progresso comprovado aparece antes do aviso");
                    var chromatic = meter.Controls.Find("V269FlowChromaticBar",true).OfType<GfDynamicProgressBar>().Single();
                    Require(chromatic.CurrentFrame == GfUniversalMeter.Operation(28,GfMeterState.Running,true,palette),
                        "progresso usa a escala universal também no Dark, sem cor substituta");
                    Capture("ANDAMENTO", palette);
                    shell.PresentPanel(new(V269PanelKind.Progress, $"Verificar {tab}", "Consulta sem total conhecido.",
                        CancelFlow:cancel, FlowPage:page));
                    surface.PerformLayout();Application.DoEvents();
                    Require(meter.Visible && meter.AccessibleDescription=="Progresso não informado" &&
                        chromatic.Visible && chromatic.CurrentFrame.State==GfMeterState.Indeterminate &&
                        chromatic.CurrentFrame.Fraction is null && !Find("V269PanelProgress").Visible,
                        "REC-FIS-PROGRESS: barra indeterminada única, sem percentual inventado");
                    Capture("ANDAMENTO-INDETERMINADO",palette);
                    if (cancel is not null)
                    {
                        var cancelAction = (Button)Find("V269CancelOwnedFlow");
                        Require(cancelAction.Visible, "cancelamento segue acessível no painel da operação");
                        cancelAction.PerformClick();
                        Require(cancelled, "cancelamento usa a ação da operação");
                    }

                    foreach (bool complete in new[] { true, false })
                    {
                        const string result = "Resultado sintético, sem manutenção.";
                        shell.PresentPanel(new(V269PanelKind.Result, $"Verificar {tab}", result,
                            FlowResult: new(page, complete, () => shell.PresentPanel(null))));
                        surface.PerformLayout();
                        Application.DoEvents();
                        var dismiss = (Button)Find("V269DismissFlowResult");
                        Require(Find("V269AppFlowHeading").Text == (complete ? "Operação concluída" : "Resultado da operação") &&
                            note.Visible && note.AccessibleName == result && dismiss.Visible && !meter.Visible,
                            "o resultado mostra o desfecho informado e pode ser dispensado");
                        Capture(complete ? "CONCLUIDO" : "ATENCAO", palette);
                        dismiss.PerformClick();
                        Require(shell.RightPane.Current is null, "dispensar remove somente o resultado apresentado");
                    }
                }
            }
            shell.PresentPanel(new(V269PanelKind.Result, "Resultado contextual", "Fixture",
                FlowResult: new(page, true, () => { })));
            var destination = page == V269Page.Drivers ? V269Page.Aplicativos : V269Page.Drivers;
            shell.SelectPage(destination);
            Require(shell.RightPane.Current?.FlowResult?.Page != page,
                "resultado contextual não bloqueia o painel de outra aba");
            shell.SelectPage(page);
        }
        finally
        {
            cancellation.Cancel();
            SynchronizationContext.SetSynchronizationContext(oldContext);
            theme.SelectedIndex = originalTheme;
            surface.Size = originalSize;
            shell.PresentPanel(previous);
        }
    }

    private static void VerifyAppIconQueueVisibility()
    {
        using var owner=new Control();
        _=owner.Handle;
        var first=new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads=0;
        using var cache=new V269AppIconCache(owner,_=>
            Interlocked.Increment(ref reads)==1?first.Task:Task.FromResult<byte[]?>(null));
        void WaitFor(Func<bool> ready,string message)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(!ready()&&watch.Elapsed<TimeSpan.FromSeconds(5))
            {Application.DoEvents();Thread.Sleep(5);}
            Require(ready(),message);
        }
        cache.Get(@"C:\GF\fixture-one.ico");
        WaitFor(()=>Volatile.Read(ref reads)==1,"primeira leitura de ícone iniciada");
        cache.Get(@"C:\GF\fixture-two.ico");
        Require(cache.PendingCount==2,"leitura ativa e fila pendente");
        owner.Visible=false;
        Require(cache.PendingCount==1,"ocultar Aplicativos descarta somente a fila");
        first.SetResult(null);
        WaitFor(()=>cache.PendingCount==0,"leitura ativa termina sem agendar outra");
        Require(Volatile.Read(ref reads)==1,"nenhuma extração adicional fora da aba");
        cache.Get(@"C:\GF\fixture-two.ico");
        Require(cache.PendingCount==0,"aba oculta não enfileira leitura");
        owner.Visible=true;
        cache.Get(@"C:\GF\fixture-two.ico");
        WaitFor(()=>Volatile.Read(ref reads)==2&&cache.PendingCount==0,"retorno à aba retoma extração sob demanda");
        Console.WriteLine("PASS Aplicativos V2.6.9: fila de ícones suspensa fora da aba.");
    }

    private static void VerifyAppIconPartialInvalidation()
    {
        using var window=new Form{ClientSize=new Size(800,260),ShowInTaskbar=false};
        var first=new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second=new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var third=new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads=0;
        using var list=new DarkProgramList(_=>Interlocked.Increment(ref reads) switch
        {1=>first.Task,2=>second.Task,3=>third.Task,_=>Task.FromResult<byte[]?>(null)})
        {Dock=DockStyle.Fill,LoadLocalAppIcons=true};
        list.ApplyV269Table(V269AppearanceOptions.Default);
        var rows=Enumerable.Range(0,50).Select(i=>new ProgramEntry
        {Name="Ícone "+i,DisplayIcon=i==0||i==2?@"C:\GF\one.ico":i==1?@"C:\GF\two.ico":i==21?@"C:\GF\three.ico":""}).ToArray();
        list.SetItems(rows);window.Controls.Add(list);window.Show();Application.DoEvents();
        var bar=list.Controls.Find("V269AppsVerticalScroll",false).OfType<DarkVScrollBar>().Single();
        var dirty=new List<Rectangle>();
        list.Invalidated+=(_,e)=>dirty.Add(e.InvalidRect);
        void WaitFor(Func<bool> ready,string message)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(!ready()&&watch.Elapsed<TimeSpan.FromSeconds(5))
            {Application.DoEvents();Thread.Sleep(5);}
            Require(ready(),message);
        }
        using var icon=new Bitmap(16,16);
        using(var graphics=Graphics.FromImage(icon))graphics.Clear(Color.Red);
        using var bytes=new MemoryStream();icon.Save(bytes,System.Drawing.Imaging.ImageFormat.Png);
        byte[] png=bytes.ToArray();
        int header=list.TableHeaderHeight,row=list.ItemHeight;
        Require(header+3*row<=list.ClientSize.Height,"fixture de ícones exige três linhas inteiras");
        list.LocalAppIcon(rows[0]);list.LocalAppIcon(rows[1]);
        WaitFor(()=>Volatile.Read(ref reads)==1,"primeiro ícone em leitura");
        dirty.Clear();first.SetResult(png);
        WaitFor(()=>Volatile.Read(ref reads)==2,"segundo ícone em leitura");
        Require(dirty.Any(r=>r.Top==header&&r.Height==row)&&
            dirty.Any(r=>r.Top==header+2*row&&r.Height==row)&&
            dirty.All(r=>r.Top>=header&&r.Height<=row),
            "ícone compartilhado invalida somente as duas linhas visíveis");
        bar.Value=20*row;Application.DoEvents();dirty.Clear();
        second.SetResult(png);
        WaitFor(()=>Volatile.Read(ref reads)==3,"fila reposta com ícone agora visível");
        Require(dirty.Count==0,"ícone concluído fora do viewport não repinta a tabela");
        third.SetResult(null);
        WaitFor(()=>(int)(typeof(V269AppIconCache).GetProperty("PendingCount",BindingFlags.Instance|BindingFlags.NonPublic)!
                .GetValue(typeof(DarkProgramList).GetField("_appIcons",BindingFlags.Instance|BindingFlags.NonPublic)!
                    .GetValue(list))!)==0,"resultado sem imagem encerra fila");
        Require(dirty.Count==0&&Volatile.Read(ref reads)==3,
            "falha de extração conserva fallback e não repinta nem repete leitura");
        window.Close();
        Console.WriteLine("PASS V269-ICONS: somente linhas atuais, fila reposta, offscreen e fallback sem invalidação total.");
    }

    private static void VerifyAppIconSources()
    {
        VerifyAppIconQueueVisibility();
        VerifyAppIconPartialInvalidation();
        Require(V269LocalIconSource.Parse("\"C:\\Program Files\\GF\\app.exe\",-12") is {Index:-12}&&
            V269LocalIconSource.Parse("C:\\GF\\app.ico") is {Index:0},"ícones locais com índice documentado");
        foreach(string invalid in new[]{"https://site/app.ico","\\\\server\\share\\a.exe","C:\\GF\\..\\a.exe",
            "C:\\GF\\a.exe:stream","C:\\GF\\a.lnk","C:\\GF\\a.exe /run","%TEMP%\\app.exe",
            "\"C:\\GF\\a.exe\" -x","C:\\GF\\a.exe,999999","\\\\?\\C:\\GF\\a.exe"})
            Require(V269LocalIconSource.Parse(invalid) is null,"origem de ícone inválida rejeitada: "+invalid);
        Require(V269AppIconCache.ReadResource(new V269LocalIconSource(
            Path.Combine(Path.GetTempPath(),"GF-inexistente-"+Guid.NewGuid().ToString("N")+".ico"),0)) is null,
            "ícone ausente mantém abreviação");
        var packaged=new ProgramEntry{Name="Fixture MSIX",InventoryId="MSIX|Fixture.App_abcdefghijklm",
            MsixPackageName="Fixture.App",MsixPublisherId="abcdefghijklm",
            MsixPackageFamilyName="Fixture.App_abcdefghijklm",MsixPackageFullName="Fixture.App_1.2.3.4_x64__abcdefghijklm",
            MsixInventoryComplete=true};
        Require(V269MsixIconSource.From(packaged) is {FullName:"Fixture.App_1.2.3.4_x64__abcdefghijklm"}&&
            V269MsixIconSource.From(new ProgramEntry{Name="Mesmo nome"}) is null,
            "ícone MSIX depende de identidade observada completa, não do nome");
        var png=new byte[33];new byte[]{137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82}.CopyTo(png,0);
        png[19]=48;png[23]=48;
        Require(V269AppIconCache.IsBoundedPng(png),"cabeçalho PNG dentro do limite");
        png[18]=3;
        Require(!V269AppIconCache.IsBoundedPng(png)&&!V269AppIconCache.IsBoundedPng(new byte[33]),
            "rejeitar dimensões excessivas ou formato não confirmado antes de decodificar");
        using(var image=new Bitmap(96,48))
        using(var buffer=new MemoryStream())
        {
            image.Save(buffer,System.Drawing.Imaging.ImageFormat.Png);
            var normalized=V269AppIconCache.NormalizeLogo(buffer.ToArray())??
                throw new InvalidOperationException("normalização do ícone PNG");
            using var stream=new MemoryStream(normalized);using var result=new Bitmap(stream);
            Require(result.Size==new Size(48,48),"logotipo retido em memória tem dimensão fixa");
        }
        Require(V269AppIconCache.NormalizeLogo(new byte[33]) is null,"PNG inválido não entra no cache");
        Console.WriteLine("PASS Aplicativos V2.6.9: origens de ícones locais e MSIX limitadas e falha fechada.");
        // Evidência consultiva do Windows real do runner, sem logar identidade.
        // Runner sem pacote adequado não confirma extração; não inventar êxito.
        try
        {
            var real=Task.Run(async ()=>{
                var scan=LocalMsixInventory.ReadCurrentUser();
                if(scan.Status!="ok"||scan.Failures!=0)return (attempts:0,confirmed:0,complete:false);
                int attempts=0,confirmed=0;
                foreach(var app in scan.Items.Where(x=>!x.Stale))
                {
                    var entry=new ProgramEntry{InventoryId=LocalMsixInventory.InventoryId(app),
                        MsixPackageName=app.PackageName,MsixPublisherId=app.PublisherId,
                        MsixPackageFamilyName=app.PackageFamilyName,MsixPackageFullName=app.PackageFullName,
                        MsixInventoryComplete=true};
                    if(V269MsixIconSource.From(entry) is not {} source)continue;
                    attempts++;
                    var logo=await V269AppIconCache.ReadMsixAsync(source);
                    if(logo is not null)
                    {
                        using var stream=new MemoryStream(logo);using var bitmap=new Bitmap(stream);
                        if(bitmap.Size==new Size(48,48))confirmed++;
                    }
                    if(attempts>=3||confirmed>0)break;
                }
                return (attempts,confirmed,complete:true);
            }).WaitAsync(TimeSpan.FromSeconds(25)).GetAwaiter().GetResult();
            Console.WriteLine($"V269-MSIX-LOGO tentativa={real.attempts} confirmado={real.confirmed} inventario_completo={real.complete} estado={(real.confirmed>0?"CONFIRMADO":"INCONCLUSIVO")}");
        }
        catch(Exception ex)
        {
            Console.WriteLine("V269-MSIX-LOGO estado=INCONCLUSIVO motivo="+ex.GetType().Name);
        }
    }

    private static void VerifyAppTableGeometry()
    {
        using(var bar=new DarkVScrollBar())
        {
            bar.SetAppHorizontalAxis();bar.Size=new Size(240,12);
            bar.SetMetrics(600,240);
            foreach(var theme in new[]{V269Theme.AzulGf,V269Theme.Dark,V269Theme.Claro})
            {
                bar.ApplyAppScrollTheme(theme);
                Require(bar.BackColor==V269AppTable.ScrollColors(theme).Track&&bar.AccessibleRole==AccessibleRole.ScrollBar,
                    "contingência horizontal mantém tema e acessibilidade");
            }
            var key=typeof(DarkVScrollBar).GetMethod("OnKeyDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            key.Invoke(bar,[new KeyEventArgs(Keys.End)]);
            Require(bar.Value==360,"fim da rolagem horizontal");
            key.Invoke(bar,[new KeyEventArgs(Keys.Left)]);
            Require(bar.Value==325,"seta horizontal desloca a barra");
            key.Invoke(bar,[new KeyEventArgs(Keys.Home)]);
            Require(bar.Value==0,"início da rolagem horizontal");
            bar.SetMetrics(240,240);
            Require(!bar.Visible&&bar.MaximumValue==0,"barra desaparece sem excesso");
        }
        using(var trash=V269AppTable.ActionGlyph(true))
        using(var update=V269AppTable.ActionGlyph(false))
        {
            Require(trash.IsVisible(new PointF(7,12))&&!trash.IsVisible(new PointF(10,12))&&
                !trash.IsVisible(new PointF(14,12))&&trash.IsVisible(new PointF(12,4)),
                "lixeira sólida preserva corpo e vazados do SVG aprovado");
            Require(update.IsVisible(new PointF(10,5))&&update.IsVisible(new PointF(14,19))&&
                !update.IsVisible(new PointF(12,12)),"atualização sólida preserva setas e centro vazio");
        }
        foreach(var theme in new[]{V269Theme.AzulGf,V269Theme.Dark,V269Theme.Claro})
        {
            var update=V269AppTable.ActionColors(theme,false,true);
            var noUpdate=V269AppTable.ActionColors(theme,false,false);
            var remove=V269AppTable.ActionColors(theme,true,true);
            var noRemove=V269AppTable.ActionColors(theme,true,false);
            Require(update.Back.G>update.Back.R&&remove.Back.R>remove.Back.G&&
                update.Back!=noUpdate.Back&&remove.Back!=noRemove.Back,
                "ações binárias têm contraste de elegibilidade nos três temas");
        }
        Require(V269AppTable.ActionColors(V269Theme.AzulGf,false,true).Back==V269AppTable.H("#1d633e")&&
            V269AppTable.ActionColors(V269Theme.Dark,true,true).Back==V269AppTable.H("#772d39")&&
            V269AppTable.ActionColors(V269Theme.Claro,false,false).Back==V269AppTable.H("#f0f8fd"),
            "paleta da última cascata V2.6.9, sem sobrescrever tokens compartilhados");
        Require(V269AppTable.MinimumWidth(100,96)==616&&V269AppTable.MinimumWidth(100,96,true)==549&&
            V269AppTable.IsCompact(1050)&&!V269AppTable.IsCompact(1051),"mínimos e breakpoint congelados do HTML");
        Require(V269AppTable.Columns(new Rectangle(0,0,616,52),100,96).Select(c=>c.Width).SequenceEqual(new[]{185,70,100,88,98,75})&&
            V269AppTable.Columns(new Rectangle(0,0,549,52),100,96,true).Select(c=>c.Width).SequenceEqual(new[]{150,68,96,80,90,65}),"seis tracks com medidas CSS exatas");
        Require(V269AppTable.MinimumWidth(90,96)==616&&
            V269AppTable.Columns(new Rectangle(0,0,V269AppTable.MinimumWidth(110,96,true),52),110,96,true)
                .Take(5).Select(c=>c.Width).SequenceEqual(new[]{150,68,96,80,90})&&
            V269AppTable.CellPadding(90,96,false)==9&&V269AppTable.CellPadding(110,96,true)==5,
            "uiScale não altera tracks/padding CSS; reserva de ações preserva alvos");
        foreach(bool compact in new[]{false,true})
        foreach(int scale in new[]{90,100,110})
        foreach(int dpi in new[]{96,120,144,192})
        {
            var row=new Rectangle(-30,42,V269AppTable.MinimumWidth(scale,dpi,compact),V269AppTable.Scale(52,scale,dpi));
            var cells=V269AppTable.Columns(row,scale,dpi,compact);
            var u=V269AppTable.Action(row,scale,dpi,false,compact);var d=V269AppTable.Action(row,scale,dpi,true,compact);
            Require(cells.Length==6&&cells.Sum(c=>c.Width)==row.Width&&
                cells[5].Contains(u)&&cells[5].Contains(d)&&!u.IntersectsWith(d),
                "seis colunas e alvos binários disjuntos em escalas/DPI");
        }
    }

    private static void VerifyDirectDisposeLifetime()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var fixture = new MainForm(startServices: false);
        var watcher = typeof(MainForm).GetField("_releaseDisplayWatcher", flags)!;
        var timer = (System.Windows.Forms.Timer)typeof(MainForm).GetField("_memoryTimer", flags)!.GetValue(fixture)!;
        var reliability = typeof(MainForm).GetField("_reliability", flags)!.GetValue(fixture)!;
        var reliabilityDisposed = reliability.GetType().GetField("disposed", flags)!;
        var trayField = typeof(MainForm).GetField("_trayIcon", flags)!;
        var trayMenuField = typeof(MainForm).GetField("_trayMenu", flags)!;
        var tray = (NotifyIcon)trayField.GetValue(fixture)!;
        var trayMenu = (ContextMenuStrip)trayMenuField.GetValue(fixture)!;
        bool timerReleased = false, trayReleased = false, trayMenuReleased = false;
        timer.Disposed += (_, _) => timerReleased = true;
        tray.Disposed += (_, _) => trayReleased = true;
        trayMenu.Disposed += (_, _) => trayMenuReleased = true;
        Require(watcher.GetValue(fixture) is Action && !(bool)reliabilityDisposed.GetValue(reliability)!,
            "recursos registrados antes do descarte direto");
        fixture.Dispose();
        Require(watcher.GetValue(fixture) is null && timerReleased &&
            (bool)reliabilityDisposed.GetValue(reliability)! &&
            trayReleased && trayMenuReleased && trayField.GetValue(fixture) is null &&
            trayMenuField.GetValue(fixture) is null &&
            typeof(MainForm).GetField("_windowIcon", flags)!.GetValue(fixture) is null,
            "descarte direto libera evento de tela, timer RAM, histórico e bandeja sem FormClosed");
        fixture.Dispose(); // A liberação deve ser idempotente.
        Console.WriteLine("PASS V2.6.9: ciclo de vida do descarte direto.");
    }

    private static void VerifyNoLegacyControls()
    {
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        string[] removedFields = ["_updateAllButton","_bulkUninstallButton","_selectManyButton",
            "_clearSelectionButton","_allFilterButton","_selectedFilterButton","_hardwareGrid",
            "_exportHardwareButton","_hardwareGroups","_multiSelectMode","_checkedApps",
            "_batchUpdatesActive","_batchStopRequested","_batchLastAttempt","_rowInfo"];
        foreach (string name in removedFields)
            Require(typeof(MainForm).GetField(name, fields) is null, "campo legado não deve retornar: " + name);
        string[] removedMethods = ["UpdateAllApprovedAsync","ToggleMultiSelection","ClearMultiSelection",
            "UninstallCheckedAsync","UpdateBulkButtons","ApplyFilterAppearance","ExportHardwareImage","ShowHardwareCompact",
            "ShowHiddenAppsReviewAsync","DrawV269RowAction","DrawUpdateOperation","DrawRowButton","AddTools",
            "ToolCaption","CreateToolsFlow","AddAction","InvalidarAtualizacoes","UninstallSelectedAsync",
            "AddIntegratedAction","FitSystemActionsHeight","ShowIntegratedSnapshot","BuildInfoCard"];
        foreach (string name in removedMethods)
            Require(typeof(MainForm).GetMethod(name, fields) is null, "entrada legada não deve retornar: " + name);
        Require(typeof(DarkProgramList).GetProperty("MultiSelectMode", fields) is null &&
            typeof(DarkProgramList).GetEvent("ItemCheckboxClicked", fields) is null &&
            typeof(DarkProgramList).GetField("_itemTips", fields) is null &&
            typeof(DarkProgramList).GetField("_tipRow", fields) is null,
            "controle de lista conserva ações individuais sem seleção múltipla legada");
        foreach (string type in new[] { "AppBatchUpdateCandidate", "AppBatchUpdatePolicy",
            "AppBatchUpdateStepResult", "AppBatchUpdateSummary", "AppBatchUpdateRun" })
            Require(typeof(MainForm).Assembly.GetType("GerenciadorAplicativos." + type, false) is null,
                "motor legado de lote deve existir apenas nos testes: " + type);
        Require(typeof(AppBatchUpdatePolicy).Assembly == typeof(V269IntegrationTests).Assembly,
            "fixtures de lote conservadas no projeto de testes");
        Require(typeof(MainForm).GetFields(fields).All(field => field.FieldType != typeof(ToolTip)) &&
            typeof(DarkProgramList).GetFields(fields).All(field => field.FieldType != typeof(ToolTip)) &&
            typeof(DarkTabHost).GetNestedTypes(BindingFlags.NonPublic).All(type =>
                type.GetFields(fields).All(field => field.FieldType != typeof(ToolTip))),
            "sem componentes tooltip na janela, lista e navegação anterior");
        Console.WriteLine("PASS V2.6.9: controles e entradas legadas ausentes; ações individuais preservadas pelas regressões.");
    }

    private static void VerifyLocalDiagnosticExport()
    {
        var report=new LocalDiagnosticReport();
        report.RecordPerformance(new PerformanceSample(PerformanceStage.Startup,500,10,null,null,null,0,0,null));
        report.RecordPerformance(new PerformanceSample(PerformanceStage.InitialInventoryLoad,800,10,null,null,null,0,0,null));
        for(int i=0;i<130;i++)
            report.RecordPerformance(new PerformanceSample(PerformanceStage.Inventory,i,10,null,null,null,0,0,null));
        string bounded=report.Format();
        Require(bounded.Contains("128 retidas de 130")&&!bounded.Contains("duracao_ms=0.00 ")&&
            bounded.Contains("duracao_ms=129.00 ")&&bounded.Contains("etapa=Startup duracao_ms=500.00")&&
            bounded.Contains("etapa=InitialInventoryLoad duracao_ms=800.00"),
            "diagnóstico mantém histórico limitado e referências de abertura durante toda a sessão");
        using(report.MeasureUi(DiagnosticUiStage.Navigation,V269Page.Inicio)) { }
        report.RecordFailure(@"C:\USUARIO_TESTE\arquivo.txt",new IOException("CONTEUDO_PRIVADO_TESTE"));
        report.RecordFailure("FIXTURE.FALHA",new IOException("CONTEUDO_PRIVADO_TESTE"));
        report.RecordEvent("REGISTRO_PROGRAMAS.LIDO quantidade=12 erros=2 nome=CONTEUDO_PRIVADO_TESTE");
        report.RecordEvent("EVENTO.NAO_PERMITIDO quantidade=99");
        report.SampleProcess(false,false);
        string text=report.Format();
        Require(text.Contains("etapa=Navigation aba=Inicio")&&text.Contains("alocacoes_thread_ui_bytes=")&&
            text.Contains("etapa=FIXTURE.FALHA")&&text.Contains("ETAPA_NAO_IDENTIFICADA")&&
            text.Contains("REGISTRO_PROGRAMAS.LIDO quantidade=12 erros=2")&&
            !text.Contains("EVENTO.NAO_PERMITIDO")&&!text.Contains("USUARIO_TESTE")&&!text.Contains("CONTEUDO_PRIVADO_TESTE"),
            "exportação registra códigos e métricas, sem caminhos ou mensagens de exceção");
        Require(text.Contains("Amostras do processo: 1 retidas de 1"),"amostra do processo principal registrada");
        string folder=Path.Combine(Path.GetTempPath(),"GF-diagnostic-export-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string first=LocalDiagnosticReport.SaveToFolder(folder,text);
            string second=LocalDiagnosticReport.SaveToFolder(folder,text+"\nSegunda exportação.");
            Require(first!=second&&Path.GetDirectoryName(first)==folder&&Path.GetDirectoryName(second)==folder&&
                Path.GetFileName(first).StartsWith("GF-Diagnostico-",StringComparison.Ordinal)&&
                Path.GetExtension(first)==".txt"&&File.ReadAllText(first)==text&&
                File.ReadAllText(second)==text+"\nSegunda exportação."&&Directory.GetFiles(folder).Length==2,
                "exportações TXT únicas, legíveis, sem sobrescrever a anterior");
            // A falha acontece depois de gravar o cabeçalho e parte do prefixo.
            // Não pode deixar um TXT aparentemente válido nem apagar os anteriores.
            string incompleteInput=Path.Combine(folder,"prefixo-fixture.bin");
            File.WriteAllBytes(incompleteInput,new byte[]{65,66,67});
            bool incompleteRejected=false;
            using(var source=new FileStream(incompleteInput,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var incomplete=new LocalDiagnosticReport.ExportSnapshot("resumo","recuperação",source,4))
            {
                try { LocalDiagnosticReport.SaveToFolder(folder,"GF-DIAG fixture",incomplete); }
                catch(IOException) { incompleteRejected=true; }
            }
            File.Delete(incompleteInput);
            Require(incompleteRejected&&Directory.GetFiles(folder).Length==2&&
                File.ReadAllText(first)==text&&File.ReadAllText(second)==text+"\nSegunda exportação.",
                "falha de exportação não publica TXT parcial, não deixa temporário e preserva os anteriores");
            Console.WriteLine("PASS Diagnóstico GF: falha de prefixo não publica TXT parcial.");
            bool refused=false;
            try { LocalDiagnosticReport.SaveToFolder("",text); }
            catch(IOException) { refused=true; }
            Require(refused,"destino indisponível não usa fallback oculto");
            refused=false;
            try { LocalDiagnosticReport.SaveToFolder(@"\\servidor-invalido\documentos",text); }
            catch(InvalidDataException) { refused=true; }
            Require(refused,"caminho UNC recusado antes de consultar o destino");
            var chronological=new LocalDiagnosticReport();
            chronological.StartJournal(Path.Combine(folder,"fixture.log"));
            try
            {
                for(int i=1;i<=150;i++)
                    chronological.RecordEvent("REGISTRO_PROGRAMAS.LIDO quantidade="+i+" nome=PRIVADO");
                using(var export=chronological.CaptureForExport())
                {
                    chronological.RecordEvent("REGISTRO_PROGRAMAS.LIDO quantidade=777777");
                    string exported=LocalDiagnosticReport.SaveToFolder(folder,"GF-DIAG-1 fixture",export);
                    string captured=File.ReadAllText(exported);
                    Require(captured.Contains("eventos gravados: 150; omitidos: 0")&&
                        captured.Contains("REGISTRO_PROGRAMAS.LIDO quantidade=1"+Environment.NewLine)&&
                        !captured.Contains("quantidade=777777")&&!captured.Contains("AVISO: cronologia integral indisponível")&&
                        captured.EndsWith("=== FIM DO RELATÓRIO ==="+Environment.NewLine,StringComparison.Ordinal),
                        "exportação contém exatamente o prefixo capturado; eventos posteriores ficam na sessão");
                }
                string firstSnapshot=chronological.Format();
                Require(firstSnapshot.Contains("128 retidas de 151")&&
                    firstSnapshot.Contains("eventos gravados: 151; omitidos: 0")&&
                    firstSnapshot.Contains("REGISTRO_PROGRAMAS.LIDO quantidade=1"+Environment.NewLine)&&
                    !firstSnapshot.Contains("PRIVADO"),"cronologia conserva registros anteriores ao limite de RAM");
                chronological.RecordOperation(V269Page.Reparo,"resultado",false);
                string secondSnapshot=chronological.Format();
                Require(secondSnapshot.Contains("eventos gravados: 152; omitidos: 0")&&
                    secondSnapshot.Contains("operacao aba=Reparo estado=resultado resultado_confirmado=False"),
                    "segunda exportação conserva cronologia e resultado não confirmado");
            }
            finally{chronological.CloseJournal();}
            Require(Directory.GetFiles(folder,"GF-session-*.tmp").Length==0,
                "arquivo temporário da sessão removido ao fechar");
            var crashed=new DiagnosticRecoveryJournal();
            crashed.Start(folder);
            crashed.Record(DiagnosticRecoveryKind.OperationBegin,(long)V269Page.Limpeza,checkpoint:true);
            crashed.Record(DiagnosticRecoveryKind.Failure,unchecked((int)0x80004005),checkpoint:true);
            crashed.Close(false); // Simular ausência da marca normal, sem matar processo.
            var restarted=new DiagnosticRecoveryJournal();
            restarted.Start(folder);
            try
            {
                string recovered=restarted.FormatPrevious();
                Require(recovered.Contains("OperationBegin aba=Limpeza")&&recovered.Contains("Failure hresult=0x80004005"),
                    "sessão seguinte recupera operação interrompida e código de falha");
                restarted.Record(DiagnosticRecoveryKind.ProcessSample,100,200,300,checkpoint:true);
            }
            finally{restarted.Close(true);}
            var third=new DiagnosticRecoveryJournal();
            third.Start(folder);
            try
            {
                Require(third.FormatPrevious().Contains("OperationBegin aba=Limpeza"),
                    "encerramento normal não substitui evidência anterior de interrupção");
                Require(Directory.GetFiles(folder,"GF-recovery-v1-*.bin").Length==2,
                    "recuperação mantém no máximo dois slots");
            }
            finally{third.Close(true);}
            string invalidFolder=Path.Combine(folder,"invalid-recovery");
            Directory.CreateDirectory(invalidFolder);
            string invalidPath=Path.Combine(invalidFolder,"GF-recovery-v1-0.bin");
            byte[] unknown=[1,2,3,4];
            File.WriteAllBytes(invalidPath,unknown);
            var invalid=new DiagnosticRecoveryJournal();
            invalid.Start(invalidFolder);
            try
            {
                Require(invalid.FormatPrevious().Contains("checkpoint local indisponível")&&
                    File.ReadAllBytes(invalidPath).SequenceEqual(unknown),
                    "checkpoint desconhecido é recusado sem sobrescrita");
            }
            finally{invalid.Close(false);}
            string truncatedFolder=Path.Combine(folder,"truncated-recovery");
            Directory.CreateDirectory(truncatedFolder);
            var interrupted=new DiagnosticRecoveryJournal();
            interrupted.Start(truncatedFolder);
            interrupted.Record(DiagnosticRecoveryKind.OperationBegin,(long)V269Page.Reparo,checkpoint:true);
            interrupted.Close(false);
            string truncatedPath=Directory.GetFiles(truncatedFolder,"GF-recovery-v1-*.bin").Single();
            using(var tail=new FileStream(truncatedPath,FileMode.Append,FileAccess.Write,FileShare.None))
                tail.Write(System.Text.Encoding.UTF8.GetBytes("PRIVADO"));
            var partial=new DiagnosticRecoveryJournal();
            partial.Start(truncatedFolder);
            try
            {
                string recovered=partial.FormatPrevious();
                Require(recovered.Contains("OperationBegin aba=Reparo")&&
                    recovered.Contains("AVISO: recuperação parcial")&&!recovered.Contains("PRIVADO"),
                    "checkpoint truncado recupera somente registros completos sem importar texto");
            }
            finally{partial.Close(true);}
            Require(LocalDiagnosticReport.ModelText("Samsung MZ-V8P1T0")== "Samsung MZ-V8P1T0"&&
                LocalDiagnosticReport.ModelText(@"PCI\VEN_1234")== "N/D"&&
                LocalDiagnosticReport.ModelText("1234567890ABCDEF")== "N/D"&&
                LocalDiagnosticReport.ModelText("teste@exemplo.local")== "N/D",
                "modelo técnico aceito; identificadores e caminhos recusados");
        }
        finally{Directory.Delete(folder,true);}
        Console.WriteLine("PASS Diagnóstico GF: métricas limitadas, privacidade e exportação TXT única em diretório descartável.");
    }

    private static void VerifyDiagnosticCaptureCost()
    {
        const int entries=8000, rounds=5;
        string folder=Path.Combine(Path.GetTempPath(),"GF-diagnostic-cost-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var session=new LocalDiagnosticReport();
        session.StartJournal(Path.Combine(folder,"fixture.log"));
        try
        {
            for(int i=1;i<=entries;i++)
                session.RecordEvent("REGISTRO_PROGRAMAS.LIDO quantidade="+i);
            string full=session.Format();
            Require(full.Contains("eventos gravados: 8000; omitidos: 0"),
                "benchmark exige journal real, sem aceitar fallback vazio");
            using(var sample=session.CaptureForExport())
            {
                string captured=sample.FormatForTest("GF-DIAG fixture");
                Require(captured.Contains("eventos gravados: 8000; omitidos: 0")&&
                    captured.Contains("REGISTRO_PROGRAMAS.LIDO quantidade=1"+Environment.NewLine)&&
                    !captured.Contains("AVISO: cronologia integral indisponível"),
                    "benchmark compara capturas com a mesma cronologia completa");
                static string Chronology(string report)
                {
                    const string start="=== CRONOLOGIA DA SESSÃO ATÉ A GERAÇÃO DO RELATÓRIO ===";
                    const string end="=== RECUPERAÇÃO DE SESSÃO ANTERIOR ===";
                    int first=report.IndexOf(start,StringComparison.Ordinal);
                    int last=report.IndexOf(end,StringComparison.Ordinal);
                    Require(first>=0&&last>first,"seções da cronologia presentes");
                    return report[first..last].TrimEnd('\r','\n');
                }
                string original=Chronology(full);
                Require(System.Text.Encoding.UTF8.GetByteCount(original)>16*1024*8&&
                    string.Equals(original,Chronology(captured),StringComparison.Ordinal),
                    "exportação em múltiplos blocos preserva toda a cronologia, ordem e UTF-8");
            }
            // Aquecer os dois caminhos; comparar somente trabalho síncrono de
            // captura. A leitura/gravação assíncrona não entra nestes números.
            _=session.Format();
            using(session.CaptureForExport()) { }
            long before=0,after=0;
            var beforeMs=new double[rounds];var afterMs=new double[rounds];
            for(int i=0;i<rounds;i++)
            {
                void Old()
                {
                    long bytes=GC.GetAllocatedBytesForCurrentThread();
                    long start=System.Diagnostics.Stopwatch.GetTimestamp();
                    string result=session.Format();
                    beforeMs[i]=System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    before+=GC.GetAllocatedBytesForCurrentThread()-bytes;
                    GC.KeepAlive(result);
                }
                void New()
                {
                    long bytes=GC.GetAllocatedBytesForCurrentThread();
                    long start=System.Diagnostics.Stopwatch.GetTimestamp();
                    using(var snapshot=session.CaptureForExport()) { }
                    afterMs[i]=System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    after+=GC.GetAllocatedBytesForCurrentThread()-bytes;
                }
                if(i%2==0){Old();New();}else{New();Old();}
            }
            Array.Sort(beforeMs);Array.Sort(afterMs);
            Console.WriteLine(FormattableString.Invariant(
                $"V269-PERF diagnostico_captura_ui registros={entries} repeticoes={rounds} bytes_antes={before} bytes_depois={after} mediana_antes_ms={beforeMs[rounds/2]:F3} mediana_depois_ms={afterMs[rounds/2]:F3}"));
            Require(after<before,"captura por prefixo deve reduzir alocações síncronas, sem perder cronologia");
        }
        finally
        {
            session.CloseJournal();
            Directory.Delete(folder,true);
        }
    }

    private static void VerifyInventoryOrderingCost()
    {
        var apps = Enumerable.Range(0, 512).Select(i => new ProgramEntry
            { Name = "Fixture " + (511-i), InventoryId = "APP|" + i }).ToArray();
        var keys = apps.Select(V269AppOrdering.Key).ToArray();
        ProgramEntry[] OldAppSort()
        {
            var items = apps.ToArray();
            var comparer = StringComparer.CurrentCultureIgnoreCase;
            var order = keys.Select((key,index)=>(key,index)).GroupBy(x=>x.key,StringComparer.Ordinal)
                .ToDictionary(g=>g.Key,g=>g.First().index,StringComparer.Ordinal);
            return items.OrderBy(x=>x.Name,comparer).ThenBy(x=>x.Version,comparer)
                .ThenBy(x=>x.InventoryId,StringComparer.OrdinalIgnoreCase).ToArray();
        }
        ProgramEntry[] NewAppSort() => V269AppOrdering.Sort(apps,V269AppSort.NameAscending,keys);
        Require(OldAppSort().SequenceEqual(NewAppSort()),"ordenação normal preservada");
        Require(V269AppOrdering.Sort(apps,V269AppSort.Manual,[keys[1],keys[1],keys[0]])
            .Take(2).SequenceEqual(new[]{apps[1],apps[0]}),"primeiro índice manual preservado");
        long MeasureAllocations(Func<ProgramEntry[]> sort)
        {
            _=sort();
            long start=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<5;i++) _=sort();
            return GC.GetAllocatedBytesForCurrentThread()-start;
        }
        long before=MeasureAllocations(OldAppSort),after=MeasureAllocations(NewAppSort);
        Require(after<before,"ordenação comum evita alocações do índice manual");
        Console.WriteLine($"V269-PERF ordenacao_aplicativos itens=512 repeticoes=5 bytes_antes={before} bytes_depois={after}");
        var drivers=Enumerable.Range(0,512).Select(i=>new DriverInventoryEntry(
            @"ROOT\GF-PERF\"+i,"Fixture "+i,"Microsoft","1.0","test.inf",true,true)).ToArray();
        var manual=drivers.Take(256).Reverse().Append(drivers[255]).ToArray();
        var old=drivers.OrderBy(item=>
        {
            int index=Array.FindIndex(manual,ordered=>ReferenceEquals(item,ordered));
            return index<0?int.MaxValue:index;
        }).ToArray();
        Require(old.SequenceEqual(DriverListFilters.SortManual(drivers,manual),ReferenceEqualityComparer.Instance),
            "drivers preservam referência, primeira ocorrência e ordem estável");
        var equalButDistinct=new DriverInventoryEntry(@"ROOT\GF-PERF\0","Fixture 0","Microsoft","1.0","test.inf",true,true);
        Require(ReferenceEquals(DriverListFilters.SortManual([equalButDistinct,drivers[0]],[drivers[0]])[0],drivers[0]),
            "identidade de referência não confunde instâncias equivalentes");
        Console.WriteLine("PASS V2.6.9: ordenações preservadas, alocações comparadas e índice manual de drivers.");
    }

    private static void VerifyAppOrdering()
    {
        Require(LocalMsixInventory.NormalizeDescription("  Texto\r\n declarado  ")=="Texto declarado"&&
            LocalMsixInventory.NormalizeDescription("ms-resource:Description")==""&&
            LocalMsixInventory.NormalizeDescription(new string('x',601))==""&&
            LocalMsixInventory.NormalizeDescription("a\0b")=="","descrições locais delimitadas e recursos não resolvidos rejeitados");
        var a=new ProgramEntry{Name="Alfa",InventoryId="A",InstallDate=V269AppOrdering.ParseRegistryDate("20260101")};
        var b=new ProgramEntry{Name="Beta",InventoryId="B",InstallDate=V269AppOrdering.ParseRegistryDate("20261124"),IsUpdate=true};
        var z=new ProgramEntry{Name="Zeta",InventoryId="Z"};
        ProgramEntry[] entries=[z,b,a];
        Require(V269AppOrdering.ParseRegistryDate("20260230") is null&&V269AppOrdering.ParseRegistryDate(20261124) is null&&
            V269AppOrdering.ParseRegistryDate("24/11/2026") is null&&V269AppOrdering.ParseRegistryDate("00010101") is null&&
            LocalMsixInventory.NormalizeDeclaredDate(DateTimeOffset.MinValue) is null&&
            LocalMsixInventory.NormalizeDeclaredDate(new DateTimeOffset(2026,11,24,12,0,0,TimeSpan.Zero)).HasValue,
            "datas ARP e MSIX estritas e ausências explícitas");
        Require(V269AppOrdering.Sort(entries,V269AppSort.NameAscending,[]).SequenceEqual(new[]{a,b,z})&&
            V269AppOrdering.Sort(entries,V269AppSort.NameDescending,[]).SequenceEqual(new[]{z,b,a})&&
            V269AppOrdering.Sort(entries,V269AppSort.RecentlyInstalled,[]).SequenceEqual(new[]{b,a,z})&&
            V269AppOrdering.Sort(entries,V269AppSort.UpdatesFirst,[]).SequenceEqual(new[]{b,a,z}),"quatro ordenações consultivas");
        var manual=V269AppOrdering.Move(entries,[],z,a)??throw new InvalidOperationException("ordem manual");
        Require(V269AppOrdering.Sort(entries,V269AppSort.Manual,manual).SequenceEqual(new[]{z,a,b}),"ordem manual por identidade");
        Require(V269AppOrdering.Move([a,new ProgramEntry{Name="Outra",InventoryId="A"},z],[],a,z) is null,
            "identidade ambígua não permite mover");
        string directory=Path.Combine(Path.GetTempPath(),"GF-app-order-"+Guid.NewGuid().ToString("N"));
        string path=Path.Combine(directory,"order.json");
        try
        {
            V269AppOrderStore.Write(path,manual);
            Require(V269AppOrderStore.Read(path).SequenceEqual(manual)&&
                V269AppOrderStore.ReadPreferences(path).Sort==V269AppSort.Manual,"ordem manual persistente em fixture");
            V269AppOrderStore.WritePreferences(path,V269AppSort.UpdatesFirst,manual);
            var restored=V269AppOrderStore.ReadPreferences(path);
            Require(restored.Sort==V269AppSort.UpdatesFirst&&restored.Keys.SequenceEqual(manual),
                "modo escolhido persiste sem perder ordem manual");
            File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(manual));
            Require(V269AppOrderStore.ReadPreferences(path).Sort==V269AppSort.Manual&&
                V269AppOrderStore.Read(path).SequenceEqual(manual),"migração de arquivo legado");
            File.WriteAllText(path,"{}");
            Require(V269AppOrderStore.Read(path).Length==0,"ordem corrompida não impede inventário");
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("PASS Aplicativos V2.6.9: datas consultivas e cinco ordenações.");
    }

    private static void VerifySettings(MainForm form,V269Shell shell)
    {
        var page=shell.PageContent(V269Page.Configuracoes) as V269SettingsPage
            ?? throw new InvalidOperationException("Configurações não usa a view nativa.");
        var general=page.General;
        // Dados isolados de teste; nenhum Registro do Windows, telemetria,
        // preferência real ou ação administrativa é alterado.
        string folder=Path.Combine(Path.GetTempPath(),"GF-config-test-"+Guid.NewGuid().ToString("N"));
        string path=Path.Combine(folder,"geral.json");
        try
        {
            Require(V269GeneralSettingsStore.Read(path)==new V269GeneralOptions(),
                "preferências gerais ausentes retornam padrão seguro");
            var expected=new V269GeneralOptions(false,false);
            V269GeneralSettingsStore.Write(path,expected);
            Require(V269GeneralSettingsStore.Read(path)==expected &&
                Directory.GetFiles(folder).Length==1,"preferências persistem atomicamente em cópia descartável");
            V269GeneralSettingsStore.Write(path,new V269GeneralOptions());
            Require(V269GeneralSettingsStore.Read(path)==new V269GeneralOptions(),
                "preferências de bandeja/notificação são reversíveis");
            File.WriteAllText(path,"{");
            Require(V269GeneralSettingsStore.Read(path)==new V269GeneralOptions(),"JSON inválido retorna padrão");
            string appearancePath=Path.Combine(folder,"aparencia.json");
            Require(V269AppearanceSettingsStore.Read(appearancePath)==V269AppearanceOptions.Default,
                "preferências de aparência ausentes retornam padrão");
            var appearanceExpected=new V269AppearanceOptions(V269Theme.Dark,105,110,false);
            V269AppearanceSettingsStore.Write(appearancePath,appearanceExpected);
            Require(V269AppearanceSettingsStore.Read(appearancePath)==appearanceExpected,
                "tema, escalas e animação persistem sem interferir nas preferências gerais");
            File.WriteAllText(appearancePath,"{}");
            Require(V269AppearanceSettingsStore.Read(appearancePath)==V269AppearanceOptions.Default,
                "preferência incompleta não aplica escalas mínimas nem desativa animações");
            File.WriteAllText(appearancePath,"{\"Theme\":\"inválido\"}");
            Require(V269AppearanceSettingsStore.Read(appearancePath)==V269AppearanceOptions.Default,
                "tipos inválidos em aparência retornam padrão");
            string exe=Path.Combine(folder,"GerenciadorFinal.exe");
            File.WriteAllText(exe,"fixture de caminho; nunca executar");
            var launch=V269AdminSessionLauncher.Build(exe);
            Require(launch.FileName==exe&&launch.UseShellExecute&&launch.Verb=="runas"&&
                launch.ArgumentList.Count==0&&string.IsNullOrEmpty(launch.Arguments)&&
                !V269AdminSessionLauncher.IsEligible(path)&&!V269AdminSessionLauncher.IsEligible("GerenciadorFinal.exe"),
                "modo administrador solicita apenas UAC do executável exato, sem argumentos ou persistência");
            var ticket=V269AdminSessionLauncher.Prepare(exe);
            Require(V269AdminSessionLauncher.Verify(ticket),"identidade do executável de teste verificável");
            File.AppendAllText(exe,"alteração sintética");
            Require(!V269AdminSessionLauncher.Verify(ticket),"troca do executável invalida solicitação administrativa");
            File.Delete(exe);
            Require(!V269AdminSessionLauncher.Verify(ticket),"executável ausente invalida solicitação sem propagar exceção");
            Require(!V269AdminSessionLauncher.Verify(new V269AdminLaunchTicket(path,ticket.Sha256)),
                "caminho não elegível invalida solicitação sem propagar exceção");
            Require(form.ConsumeV269AdminRestart() is null,"abertura normal/testes não solicitam elevação");
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        general.SetObserved(false,null,new V269GeneralOptions(false,true),TelemetryConsent.Declined,true);
        Require(!general.StartWindows.Enabled&&!general.StartWindows.Checked &&
            !general.Notifications.Checked&&general.Tray.Checked&&!general.Collect.Checked,
            "estado desconhecido de inicialização falha fechado; seletores usam estados observados");
        general.SetObserved(true,true,new V269GeneralOptions(),TelemetryConsent.RemoteBeta,true);
        Require(general.Administrator.Checked&&general.StartWindows.Checked&&general.Collect.Checked,
            "estado administrativo e consentimento provêm da fotografia efetiva");
        Require(!general.Collect.AutoCheck&&!general.StartWindows.AutoCheck&&!general.Notifications.AutoCheck,
            "ações não alteram estado por otimismo antes do motor");
        general.SetObserved(null,null,new V269GeneralOptions(),TelemetryConsent.Declined,true,false);
        Require(!general.Administrator.Enabled&&!general.Collect.Enabled,"privilégios e coleta indisponíveis não são habilitados");
        var range=page.Appearance.Controls.Find("V269UiScale",true).OfType<V269SettingsRange>().Single();
        Require(range.ValueAtPosition(-10)==90&&range.ValueAtPosition(range.Width+10)==110,
            "barra visual conserva os limites do motor TrackBar");
        var originalPanel=shell.RightPane.Current;
        shell.PresentPanel(null);
        var surface=form.Controls.OfType<TableLayoutPanel>().Single();
        Size originalSize=surface.Size;
        var old=page.Appearance.GetOptions();
        var themeChoice=page.Appearance.Controls.Find("V269ThemeChoice",true).OfType<V269SettingsCombo>().Single();
        Require(themeChoice.DrawMode==DrawMode.OwnerDrawFixed&&themeChoice.DropDownStyle==ComboBoxStyle.DropDownList,
            "seletor de tema personalizado preserva a lista nativa");
        try
        {
            foreach(var size in new[]{new Size(760,596),new Size(992,659)})
            {
                surface.Size=size;
                foreach(var theme in Enum.GetValues<V269Theme>())
                {
                    themeChoice.SelectedIndex=(int)theme;
                    page.ApplyAppearance(new V269AppearanceOptions(theme,100,100,true));
                    general.SetObserved(false,false,new V269GeneralOptions(),TelemetryConsent.Declined,true);
                    page.ScrollTo(0);
                    surface.PerformLayout();page.PerformLayout();Application.DoEvents();
                    Require(page.General.Width>0&&page.General.Height>0&&
                        page.Appearance.Top>page.General.Bottom&&page.ContentHeight>page.Appearance.Bottom,
                        "Geral e Aparência em cartões sem sobreposição");
                    // Em ambas as janelas da referência, o seletor de
                    // tema começa fora da área visível em abertura.
                    if(page.DeviceDpi==96&&Screen.FromControl(page).WorkingArea.Height<=810&&
                        surface.Size==size)
                    {
                        var comboOnPage=page.RectangleToClient(themeChoice.RectangleToScreen(themeChoice.ClientRectangle));
                        if(!page.ClientRectangle.IntersectsWith(comboOnPage))
                            Require(themeChoice.VisibleSettingsBounds().IsEmpty,
                                "seletor fora do viewport não deve pintar no rodapé da janela");
                    }
                    var origin=surface.PointToScreen(Point.Empty);
                    var geom=new{Theme=theme.ToString(),Width=surface.Width,Height=surface.Height,Dpi=page.DeviceDpi,
                        WorkAreaHeight=Screen.FromControl(page).WorkingArea.Height,
                        Cards=new[]{(Control)page.General,page.Appearance}.Select(card=>{
                            var p=card.PointToScreen(Point.Empty);return new{card.Name,X=p.X-origin.X,Y=p.Y-origin.Y,card.Width,card.Height};}).ToArray()};
                    Console.WriteLine("V269-SETTINGS-GEOMETRY "+System.Text.Json.JsonSerializer.Serialize(geom));
                    if(geom.Dpi==96&&geom.WorkAreaHeight<=810&&surface.Size==size)
                    {
                        // Medidas do HTML original PR #109, mesma classe
                        // de viewport e escalas 100%. Tolerância de arredondamento.
                        Rectangle[] expected=size.Width==760
                            ? [new(212,152,520,437),new(212,596,520,258)]
                            : [new(212,152,752,419),new(212,578,752,187)];
                        for(int cardIndex=0;cardIndex<expected.Length;cardIndex++)
                        {
                            var actual=geom.Cards[cardIndex];var target=expected[cardIndex];
                            Require(Math.Abs(actual.X-target.X)<=2&&Math.Abs(actual.Y-target.Y)<=2&&
                                Math.Abs(actual.Width-target.Width)<=2&&Math.Abs(actual.Height-target.Height)<=2,
                                $"geometria HTML Configurações {theme}/{size}: {actual} vs {target}");
                        }
                    }
                    using(var shot=new Bitmap(surface.Width,surface.Height))
                    {
                        surface.DrawToBitmap(shot,new Rectangle(Point.Empty,surface.Size));
                        shot.Save($"LayoutCaptures/v269-configuracoes-REFERENCIA-SINTETICA-{theme}-{size.Width}x{size.Height}.png");
                    }
                    Require(general.History.Focus(),"histórico acessível pelo teclado");
                    Application.DoEvents();
                    Require(LayoutTests.FullyVisible(general.History),"foco revela histórico no viewport");
                    page.ScrollTo(int.MaxValue);
                    Require(page.ScrollOffset==Math.Max(0,page.ContentHeight-page.ClientSize.Height),"rolagem alcança rodapé");
                    Require(themeChoice.VisibleSettingsBounds().Width>0&&themeChoice.VisibleSettingsBounds().Height>0,
                        "seletor visível ao alcançar o rodapé");
                    int previousOffset=page.ScrollOffset,previousScale=range.Value;
                    if(previousOffset>0)
                    {
                        long measures=page.GeometryArrangeCount;
                        var beforeCards=new[]{page.General.Bounds,page.Appearance.Bounds};
                        int contentHeight=page.ContentHeight;
                        page.ScrollByWheel(60);
                        Require(page.ScrollOffset<previousOffset,"Configurações V269 responde ao primeiro delta parcial");
                        page.ScrollByWheel(60);
                        Require(page.ScrollOffset<previousOffset&&range.Value==previousScale,
                            "roda fracionada rola Configurações sem alterar escala");
                        int dy=previousOffset-page.ScrollOffset;
                        var afterCards=new[]{page.General.Bounds,page.Appearance.Bounds};
                        Require(page.GeometryArrangeCount==measures&&page.ContentHeight==contentHeight,
                            "rolagem pura não deve executar ArrangeCard nem medir o conteúdo");
                        for(int card=0;card<beforeCards.Length;card++)
                        {
                            var expected=beforeCards[card];expected.Offset(0,dy);
                            Require(afterCards[card]==expected,"rolagem deve transladar cartões sem redimensionar");
                        }
                        page.ScrollTo(0);page.ScrollTo(int.MaxValue);
                        Require(page.GeometryArrangeCount==measures&&page.ScrollOffset==previousOffset&&
                            page.General.Bounds==beforeCards[0]&&page.Appearance.Bounds==beforeCards[1],
                            "extremos de rolagem preservam geometria e não medem novamente");
                        Console.WriteLine($"V269-SCROLL-SETTINGS {theme}/{size}: measures=0 dy={dy}");
                        page.ScrollTo(int.MaxValue);
                        int beforeTheme=themeChoice.SelectedIndex;
                        void WheelOn(Control control)
                        {
                            var wndProc=control.GetType().GetMethod("WndProc",BindingFlags.NonPublic|BindingFlags.Instance)!;
                            object[] arguments=[Message.Create(control.Handle,0x020A,(IntPtr)(120<<16),IntPtr.Zero)];
                            wndProc.Invoke(control,arguments);
                        }
                        WheelOn(themeChoice);
                        Require(page.ScrollOffset<previousOffset&&themeChoice.SelectedIndex==beforeTheme,
                            "roda sobre seletor fechado rola sem trocar tema");
                        page.ScrollTo(int.MaxValue);
                        WheelOn(range);
                        Require(page.ScrollOffset<previousOffset&&range.Value==previousScale,
                            "roda sobre barra rola sem modificar escala");
                        page.ScrollTo(int.MaxValue);
                    }
                    using(var shot=new Bitmap(surface.Width,surface.Height))
                    {
                        surface.DrawToBitmap(shot,new Rectangle(Point.Empty,surface.Size));
                        shot.Save($"LayoutCaptures/v269-configuracoes-RODAPE-SINTETICO-{theme}-{size.Width}x{size.Height}.png");
                    }
                }
            }
            // Exercitar extremos das duas escalas reais, mantendo a página
            // alcançável em viewport compacto. Nenhuma preferência real é salva.
            surface.Size=new Size(760,596);
            var textRange=page.Appearance.Controls.Find("V269TextScale",true).OfType<TrackBar>().Single();
            try
            {
                foreach(var (ui,text) in new[]{(90,115),(110,90),(110,115)})
                {
                    range.Value=ui;textRange.Value=text;
                    Application.DoEvents();
                    Require(shell.InterfaceScalePercent==ui&&shell.TextScalePercent==text&&
                        page.Appearance.GetOptions().InterfacePercent==ui&&page.Appearance.GetOptions().TextPercent==text,
                        "escalas independentes aplicadas pelo binder da MainForm");
                    var screen=Screen.FromControl(form);
                    var expectedWindow=StartupWindowSizing.Fit(screen.WorkingArea,screen.Bounds.Height,ui);
                    Require(form.Size==expectedWindow.Size&&form.MinimumSize==expectedWindow.Size&&
                        form.MaximumSize==expectedWindow.Size&&screen.WorkingArea.Contains(form.Bounds)&&!form.MaximizeBox,
                        "controle de Configurações ajusta a janela real e mantém dimensão fixa no monitor");
                    // A janela real mudou; manter o viewport compacto explícito
                    // para que os testes de acesso não passem por área maior.
                    surface.Size=new Size(760,596);
                    surface.PerformLayout();Application.DoEvents();
                    foreach(Control action in new Control[]{general.Administrator,general.StartWindows,
                        general.Notifications,general.Tray,general.Collect,general.Privacy,general.Diagnostic,general.History,
                        themeChoice,range,textRange,page.Appearance.Controls.Find("V269Animations",true).Single()})
                    {
                        Require(action.Focus(),"controle de Configurações acessível em escala extrema: "+action.Name);
                        Application.DoEvents();
                        Require(LayoutTests.FullyVisible(action),"foco revela controle na escala extrema: "+action.Name);
                    }
                }
            }
            finally
            {
                range.Value=100;textRange.Value=100;surface.Size=new Size(992,659);
                page.ScrollTo(0);Application.DoEvents();
            }
            // Acionar os três botões consultivos da view real, em MainForm
            // sem serviços, e inspecionar o painel produzido pelo controlador.
            // Não gravar Registro, não coletar dados e não solicitar UAC.
            shell.PresentPanel(null);
            page.ScrollTo(0);Application.DoEvents();
            Require(general.Privacy.Focus(),"Saiba mais alcançável");
            general.Privacy.PerformClick();Application.DoEvents();
            Require(shell.RightPane.Visible &&
                shell.RightPane.Current is {Kind:V269PanelKind.Details,Title:"Privacidade e coleta de dados"} privacy &&
                privacy.Description.Contains(BetaRuntimePolicy.IsLocalBeta?BetaRuntimePolicy.LocalNotice:TelemetryConsentPolicy.RemoteNotice),
                "Saiba mais apresenta o aviso efetivo de consentimento");
            var retainedSettings = new V269PanelPresentation(V269PanelKind.Result,"Resultado anterior","Fixture preservada");
            shell.PresentPanel(retainedSettings);
            void VerifySettingsReadOnly(Button action,string expectedTitle,string expectedContent)
            {
                var existing = Application.OpenForms.Cast<Form>().ToArray();
                bool modal = false;
                using var watchdog = new System.Windows.Forms.Timer { Interval = 50 };
                watchdog.Tick += (_,_) =>
                {
                    foreach(var unexpected in Application.OpenForms.Cast<Form>().Where(f=>!existing.Contains(f)).ToArray())
                    { modal=true; unexpected.Close(); }
                };
                watchdog.Start();
                action.PerformClick();Application.DoEvents();
                watchdog.Stop();
                var editor = shell.RightPane.Controls.Find("V269SettingsInformationText",true).OfType<TextBox>().Single();
                Require(!modal && Application.OpenForms.Count==existing.Length && shell.RightPane.ReadOnlyViewActive &&
                    ReferenceEquals(shell.RightPane.Current,retainedSettings) && editor.Visible && editor.ReadOnly &&
                    editor.Text.Contains(expectedContent) &&
                    shell.RightPane.Controls.Find("V269ReadOnlyTitle",true).Single().Text==expectedTitle,
                    "AUT-SHELL-02 informação de Configurações preserva resultado sem modal");
                var original = themeChoice.SelectedIndex;
                string text = editor.Text;
                themeChoice.SelectedIndex=(original+1)%3;Application.DoEvents();
                Require(shell.RightPane.ReadOnlyViewActive && editor.Text==text &&
                    ReferenceEquals(shell.RightPane.Current,retainedSettings),
                    "tema conserva consulta e resultado");
                themeChoice.SelectedIndex=original;Application.DoEvents();
                shell.RightPane.Controls.Find("V269ReadOnlyClose",true).OfType<Button>().Single().PerformClick();
                Application.DoEvents();
                Require(!shell.RightPane.ReadOnlyViewActive && ReferenceEquals(shell.RightPane.Current,retainedSettings) &&
                    action.Focused && !shell.RightPane.Visible && !form.Controls.Find("V269PaneToggle",true).Single().Visible,
                    "retorno ao resultado e foco de Configurações sem painel ordinário");
            }
            VerifySettingsReadOnly(general.Privacy,"Privacidade e coleta de dados",
                BetaRuntimePolicy.IsLocalBeta?BetaRuntimePolicy.LocalNotice:TelemetryConsentPolicy.RemoteNotice);
            VerifySettingsReadOnly(general.Diagnostic,"Diagnóstico local","RELATÓRIO LOCAL DE CONFIABILIDADE");
            shell.PresentPanel(null);Application.DoEvents();
            Require(general.Diagnostic.Focus(),"diagnóstico alcançável");
            general.Diagnostic.PerformClick();Application.DoEvents();
            Require(shell.RightPane.Visible &&
                shell.RightPane.Current is {Kind:V269PanelKind.Details,Title:"Diagnóstico local"} diagnostic &&
                diagnostic.Description.Contains(DiagnosticTrace.VersionLabel) &&
                diagnostic.Description.Contains("RELATÓRIO LOCAL DE CONFIABILIDADE"),
                "diagnóstico apresenta versão e relatório local reais");
            string exportFolder=Path.Combine(Path.GetTempPath(),"GF-diagnostic-button-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(exportFolder);
            try
            {
                form.V269DiagnosticExportForTest=report=>LocalDiagnosticReport.SaveToFolder(exportFolder,report);
                shell.PresentPanel(null);Application.DoEvents();
                general.Diagnostic.PerformClick();
                var wait=System.Diagnostics.Stopwatch.StartNew();
                while(!general.Diagnostic.Enabled&&wait.ElapsedMilliseconds<5000)
                { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                Application.DoEvents();
                var exported=Directory.GetFiles(exportFolder,"GF-Diagnostico-*.txt");
                Require(general.Diagnostic.Enabled&&exported.Length==1&&
                    File.ReadAllText(exported[0]).Contains("GF-DIAG-1")&&
                    File.ReadAllText(exported[0]).Contains("DESEMPENHO DO PROCESSO PRINCIPAL")&&
                    page.Controls.Find("V269SettingsHint",true).OfType<Label>().Single().Text.Contains("Relatório criado em Documentos")&&
                    shell.RightPane.Current is null,"botão real exporta TXT e apresenta confirmação sem substituir painel");
            }
            finally
            {
                form.V269DiagnosticExportForTest=null;page.SetNotice(null);
                Directory.Delete(exportFolder,true);
            }
            shell.PresentPanel(null);Application.DoEvents();
            Require(general.History.Focus(),"histórico alcançável");
            general.History.PerformClick();Application.DoEvents();
            Require(shell.RightPane.Visible &&
                shell.RightPane.Current is {Kind:V269PanelKind.History,Title:"Histórico local"} history &&
                history.Description.Contains("RELATÓRIO LOCAL DE CONFIABILIDADE"),
                "histórico aciona o relatório do controlador real");
        }
        finally
        {
            surface.Size=originalSize;themeChoice.SelectedIndex=(int)old.Theme;page.ScrollTo(0);
            shell.PresentPanel(originalPanel);surface.PerformLayout();Application.DoEvents();
        }
        Console.WriteLine("PASS Configurações V2.6.9: preferências descartáveis, estados observados, três temas, rolagem, botões consultivos e capturas.");
    }

    private static void VerifyStartupCaptionLayout()
    {
        using var card=new V269StartupCard("Aplicativo com nome extenso de inicialização","Todos os usuários",
            "document","Desativar na inicialização",false,null,null);
        foreach(var theme in Enum.GetValues<V269Theme>())
        foreach(int scale in new[]{100,120})
        {
            card.ApplyOptions(V269AppearanceOptions.Default with{Theme=theme,TextPercent=scale});
            int S(int n)=>V269AppTable.Scale(n,100,card.DeviceDpi);
            int natural=TextRenderer.MeasureText(card.Command.Text,card.Command.Font,Size.Empty,
                TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width;
            foreach(int width in new[]{S(150),Math.Max(S(320),card.RequiredSingleLineWidth)})
            {
                card.Size=new Size(width,card.HeightForWidth(width));
                var command=card.Command;
                int left=Math.Max(S(5),(command.Width-S(15)-S(6)-natural)/2);
                int available=Math.Max(1,command.Width-left-S(15)-S(6)-S(3));
                bool wraps=natural>available;
                int textHeight=wraps?TextRenderer.MeasureText(command.Text,command.Font,
                    new Size(available,1000),TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|
                    TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl).Height:command.Font.Height;
                var labels=card.Controls.OfType<Label>().OrderBy(label=>label.Top).ToArray();
                Require(labels.Length==2&&labels.All(label=>card.ClientRectangle.Contains(label.Bounds))&&
                    labels[0].Bottom<labels[1].Top&&labels[1].Bottom<command.Top&&
                    card.ClientRectangle.Contains(command.Bounds)&&command.Height>=textHeight+S(8),
                    "REC-FIS-STARTUP-TEXT: texto e comandos sem recorte nem sobreposição "+theme+"/"+scale+"/"+width);
                Require(wraps==(width==S(150))&&!command.Enabled&&
                    command.Text=="Desativar na inicialização","REC-FIS-STARTUP-TEXT: quebra real preserva comando bloqueado");
            }
        }
        Console.WriteLine("PASS REC-FIS-STARTUP-TEXT: comandos inteiros, quebra responsiva e bloqueio nos três temas.");
    }

    private static void VerifyHomeArtPaintClip()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        using var card=new V269HomeCard("cpu");
        card.SetModel(new V269HomeModel("cpu","Processador","Fixture",
            [new V269HomeRow("Núcleos","8")],[new V269HomeRow("Uso","10%")]));
        card.SetGeometry(1200,true);
        var art=new Bitmap(120,120);
        using(var graphics=Graphics.FromImage(art))graphics.Clear(Color.Lime);
        // Fixture exclusivamente de pintura: sem abrir HWND ou carregar arte assíncrona.
        typeof(V269HomeCard).GetField("_art",flags)!.SetValue(card,art);
        foreach(var theme in Enum.GetValues<V269Theme>())
        {
            card.ApplyAppearance(V269AppearanceOptions.Default with{Theme=theme},Color.Transparent);
            card.Size=new Size(420,card.PreferredCardHeight(420,1200,true));
            using var image=new Bitmap(card.Width,card.Height);
            using var g=Graphics.FromImage(image);
            g.Clear(Color.Magenta);
            int S(int n)=>V269AppTable.Scale(n,100,card.DeviceDpi);
            var dirty=new Rectangle(S(35),S(65),S(20),S(12));
            Require(card.ClientRectangle.Contains(dirty),"REC-FIS-HOME-ART: região válida da fixture");
            g.SetClip(dirty);
            // Render é a rotina de produção chamada por OnPaint.
            typeof(V269HomeCard).GetMethod("Render",flags)!.Invoke(card,[g,card.Width,1200,true]);
            Require(g.ClipBounds==new RectangleF(dirty.X,dirty.Y,dirty.Width,dirty.Height),
                "REC-FIS-HOME-ART: clip original preservado");
            int changed=0,artPixels=0;
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
            {
                Color pixel=image.GetPixel(x,y);
                if(pixel.ToArgb()==Color.Magenta.ToArgb())continue;
                Require(dirty.Contains(x,y),"REC-FIS-HOME-ART: desenho fora da região suja "+theme);
                changed++;
                if(pixel.G>pixel.R+50&&pixel.G>pixel.B+50)artPixels++;
            }
            Require(changed>0&&artPixels>0,"REC-FIS-HOME-ART: arte realmente pintada dentro do clip "+theme);
        }
        Console.WriteLine("PASS REC-FIS-HOME-ART: arte e clip parcial preservados nos três temas.");
    }

    private static void VerifyHomeHeightCache()
    {
        using var card = new V269HomeCard("cpu");
        var model = new V269HomeModel("cpu","Processador","Fixture",
            [new V269HomeRow("Núcleos","8")],[new V269HomeRow("Uso","10%")]);
        card.SetModel(model);
        int height = card.PreferredCardHeight(420,1200,true);
        int count = card.PreferredHeightMeasurements;
        var metrics = new List<PerformanceSample>();
        using (LocalPerformanceMetrics.Measure(PerformanceStage.HardwareInventory, metrics.Add))
            for (int i = 0; i < 20; i++)
                Require(card.PreferredCardHeight(420,1200,true)==height,"altura idêntica em cache");
        Require(card.PreferredHeightMeasurements==count && metrics.Count==1,
            "vinte medidas repetidas não repetem a renderização de geometria");
        card.SetModel(model with { Sensors = [new V269HomeRow("Uso","20%")] });
        card.PreferredCardHeight(420,1200,true);
        Require(card.PreferredHeightMeasurements==++count,"mudança de leitura invalida a altura");
        card.PreferredCardHeight(400,1200,true);
        Require(card.PreferredHeightMeasurements==++count,"mudança da largura invalida a altura");
        card.PreferredCardHeight(400,1440,true);
        Require(card.PreferredHeightMeasurements==++count,"mudança de breakpoint invalida a altura");
        card.PreferredCardHeight(400,1440,false);
        Require(card.PreferredHeightMeasurements==++count,"mudança de altura compacta invalida o cache");
        card.ApplyAppearance(V269AppearanceOptions.Default with { TextPercent = 110 },Color.Transparent);
        card.PreferredCardHeight(400,1440,false);
        Require(card.PreferredHeightMeasurements==++count,"mudança de escala de texto invalida a altura");
        // Altura comprovadamente constante não deve disparar rearranjo.
        using var incremental = new V269HomeCard("cpu");
        incremental.SetGeometry(1440, false);
        incremental.SetModel(model);
        int initialHeight = incremental.PreferredCardHeight(650,1440,false);
        Require(!incremental.SetModelAndCheckGeometry(model with { Sensors = [new V269HomeRow("Uso","20%")] }) &&
            incremental.PreferredCardHeight(650,1440,false) == initialHeight &&
            incremental.AccessibleDescription?.Contains("20%") == true,
            "leitura com altura constante repinta cartão sem redistribuir dashboard");
        Console.WriteLine("PASS Início V2.6.9: cache de geometria e atualização incremental.");
    }

    private static HardwareSensorPortfolio VerifyHome(MainForm form, Control view)
    {
        VerifyHomeHeightCache();
        Require(V269HomeMetrics.HeaderHeight(992, 768) == 69 &&
            V269HomeMetrics.HeaderHeight(760, 810) == 69 &&
            V269HomeMetrics.HeaderHeight(760, 811) == 70 &&
            V269HomeMetrics.HeaderHeight(992, 900) == 80 &&
            V269HomeMetrics.LogoSize(992, 768) == 45 &&
            V269HomeMetrics.LogoSize(992, 900) == 55 &&
            V269HomeMetrics.TitlePixels(992, 768) == 22 &&
            V269HomeMetrics.TitlePixels(760, 900) == 20 &&
            V269HomeMetrics.TitlePixels(992, 900) == 24 &&
            V269HomeMetrics.TitlePixels(1440, 900) == 28,
            "media query de 810px e breakpoints de largura do HTML");
        Require(view is V269HomeDashboard, "Início deve usar o dashboard nativo reconstruído");
        var home = (V269HomeDashboard)view;
        int wheelRemainder = 0;
        Require(V269HomeDashboard.AccumulateWheel(60, ref wheelRemainder) == 0 &&
            wheelRemainder == 60 &&
            V269HomeDashboard.AccumulateWheel(60, ref wheelRemainder) == 1 &&
            wheelRemainder == 0 &&
            V269HomeDashboard.AccumulateWheel(-30, ref wheelRemainder) == 0 &&
            V269HomeDashboard.AccumulateWheel(-90, ref wheelRemainder) == -1 &&
            wheelRemainder == 0 &&
            V269HomeDashboard.AccumulateWheel(60, ref wheelRemainder) == 0 &&
            V269HomeDashboard.AccumulateWheel(-60, ref wheelRemainder) == 0 &&
            wheelRemainder == 0, "rolagem de alta resolução acumula frações e respeita o sentido");
        var header = form.Controls.Find("V269BrandHeader", true).OfType<V269BrandHeader>().Single();
        Require(header.IsHomePresentation &&
            form.Controls.Find("V269HomeMinimize", true).Single().Visible &&
            form.Controls.Find("V269HomeClose", true).Single().Visible &&
            !form.MaximizeBox && header.Height >= 60,
            "cabeçalho único com comandos reais, sem maximização");
        int logicalHeaderHeight = V269HomeMetrics.HeaderHeight(
            shellWidth(), V269HomeMetrics.LogicalScreenHeight(form));
        Require(Math.Abs(header.Height - logicalHeaderHeight * form.DeviceDpi / 96d) <= 2,
            "altura nativa do cabeçalho segue a media query da referência");
        int shellWidth() => (int)Math.Round(
            form.Controls.Find("V269Shell", true).OfType<V269Shell>().Single().LayoutWindowWidth * 1d);
        Require(form.Controls.Find("V269BrandMark", true).Length == 1 &&
            form.Controls.Find("V269HomeHeadingIcon", true).Single().Visible &&
            form.Controls.Find("V269PageSubtitle", true).Single().Text ==
                "Informações gerais do sistema e do hardware.",
            "marca e identidade de Início da especificação visual");
        string[] keys = ["cpu", "gpu", "ram", "motherboard", "windows"];
        Require(home.Cards.Select(x => x.Key).SequenceEqual(keys),
            "cinco cartões do HTML em ordem, sem duplicar um cartão por provedor");
        int[] lengths = [4268, 6516, 8300, 12772, 11864];
        int[] pngLengths = [17661, 27275, 39960, 54991, 62218];
        Size[] dimensions = [new(134,131), new(146,140), new(188,143), new(156,141), new(320,139)];
        string[] pngHashes = [
            "0a5d21217f7fde5a9d8d5cfdec4e5bdbb5a758307ac30c22471b67390b592dfc",
            "053edb738433c9c71526655ec4d5d714ca5ed1474270229033613113c03c4e56",
            "d64f414cbe1ed91f051f641375fcd276eb8f92dd7e1e2acb7165c78a54d7e4ab",
            "911466eee92df36798767e6a46574e38fe7af363664b9d0e09e9485344c6bc37",
            "db7e4d6e03f640ea320ca75e45ed67b823b561486b6cc7e2087b6b2449d02eb4"];
        for (int i = 0; i < keys.Length; i++)
        {
            var bytes = V269HomeArt.Original(keys[i]);
            Require(System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
                System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" &&
                Convert.ToBase64String(bytes).Length == lengths[i],
                "arte WebP original " + keys[i]);
            var png = V269HomeArt.PortablePng(keys[i]);
            Require(png.Length == pngLengths[i] &&
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png))
                    .Equals(pngHashes[i], StringComparison.OrdinalIgnoreCase),
                "identidade do PNG convertido " + keys[i]);
            using var art = V269HomeArt.DecodeAsync(keys[i]).GetAwaiter().GetResult();
            Require(art.Size == dimensions[i], "decodificação PNG nativa " + keys[i]);
        }
        var empty = MainForm.V269HomeModels(null);
        Require(empty.SelectMany(x => x.Specs.Concat(x.Sensors)).All(x => x.Value == "N/D"),
            "nenhum sensor ou dado ilustrativo antes de uma consulta");
        var at = DateTimeOffset.UtcNow;
        var missing = MonitorReading<double>.Unavailable("fixture");
        var available = MonitorReading<double>.Available(0, "fixture");
        var sample = new LiveHardwareSample(at, available,
            MonitorReading<double>.Available(3600, "fixture"),
            new MemoryRamSample(DateTime.UtcNow, 16UL << 30, 8UL << 30),
            MonitorReading<TimeSpan>.Available(TimeSpan.FromHours(3), "fixture"),
            LiveHardwareSensors.Unavailable);
        var pnp = new HardwareDiscoverySnapshot(at, HardwareDiscoveryState.Complete,
            [new HardwareDeviceDescriptor("PCI\\VEN_1002&DEV_1234&SUBSYS_00000001\\fixture",
                "GPU fixture AMD", "AMD", "Display", "PCI", HardwareDeviceKind.Graphics)
                { InstalledDriverVersion = NativeHardwareDiscovery.ValidateInstalledDriver("32.0.1.123"),
                    ReportedPcieLink = NativeHardwareDiscovery.FormatWindowsPcie(3, 16) }],
            "fixture", null);
        var gpu = new NvidiaGpuSensorSnapshot(at, true,
            [new NvidiaGpuSensorSample(0, "GPU fixture NVML", null,
                available, MonitorReading<double>.Available(61, "fixture"),
                missing, MonitorReading<double>.Available(35, "fixture"),
                MonitorReading<ulong>.Available(2UL << 30, "fixture"))
                { CurrentPcieLink = NvidiaNvmlSensorProvider.FormatPcie(4, 8),
                    ReportedArchitecture = NvidiaNvmlSensorProvider.FormatArchitecture(10) }], null)
            { NvidiaDriverVersion = NvidiaNvmlSensorProvider.ValidateDriverVersion("576.02") };
        var storage = WindowsStorageSensorProvider.Join([], []);
        var acpi = new AcpiThermalZoneSnapshot(at, HardwareDiscoveryState.Unavailable, [], null);
        var platform = WindowsPlatformHardwareProvider.Join(
            [new WmiCpuSocketRaw("CPU0", "CPU fixture", 8U, 16U, 3600U,
                9U, 32768U, true, "Socket fixture")],
            [new WmiRamModuleRaw("BANK0", "DIMM0", 16UL << 30, 3200U, 26U)],
            [new WmiMotherboardRaw("Base Board", "Fabricante fixture", "Modelo fixture", "Rev 1")],
            [], reportedBiosVersion: "BIOS fixture");
        var snapshot = new HardwareSensorPortfolio(at, sample, pnp, gpu, storage, acpi,
            PlatformHardware: platform)
            { TpmDescription = V269TpmReader.Describe(0, 1, 2) };
        Require(V269BoardSpecCatalog.Resolve(new WindowsMotherboard("fixture", "ASUSTeK COMPUTER INC.", "TUF GAMING B550-PLUS", null))?.Chipset == "AMD B550" &&
            V269BoardSpecCatalog.Resolve(new WindowsMotherboard("fixture", "ASUS", "TUF GAMING B550-PLUS (WI-FI)", null))?.Socket == "AM4" &&
            V269BoardSpecCatalog.Resolve(new WindowsMotherboard("fixture", "Outro fabricante", "TUF GAMING B550-PLUS", null)) is null &&
            V269BoardSpecCatalog.Resolve(new WindowsMotherboard("fixture", "ASUS", "TUF GAMING B550-PLUS II", null)) is null,
            "chipset nominal exige fabricante e modelo exatos");
        Require(V269WindowsPresentation.Version(new Version(10, 0, 26100), "Client", "Professional", "24H2") == "Windows 11 Pro (24H2)" &&
            V269WindowsPresentation.Version(new Version(10, 0, 19045), "Client", "Core", "22H2") == "Windows 10 Home (22H2)" &&
            !V269WindowsPresentation.Version(new Version(10, 0, 26100), "Server", "ServerStandard", "").Contains("Windows 11", StringComparison.Ordinal) &&
            V269WindowsPresentation.Architecture(System.Runtime.InteropServices.Architecture.X64) == "64 bits (x64)" &&
            V269WindowsPresentation.Uptime(new TimeSpan(2, 14, 27, 0)) == "2 dias, 14 h e 27 min",
            "apresentação do Windows fiel à referência, sem confundir cliente e servidor");
        static byte[] SmbiosFixture(bool duplicateMember = false, bool missingChannel = false)
        {
            var records = new List<byte>();
            void Add(byte[] bytes) { records.AddRange(bytes); records.Add(0); records.Add(0); }
            Add([16, 15, 1, 0, 3, 3, 3, 0, 0, 0, 1, 255, 255, 2, 0]);
            Add([17, 21, 2, 0, 1, 0, 255, 255, 64, 0, 64, 0, 0, 32, 9, 0, 0, 0, 26, 0, 0]);
            Add([17, 21, 3, 0, 1, 0, 255, 255, 64, 0, 64, 0, 0, 32, 9, 0, 0, 0, 26, 0, 0]);
            Add([37, 10, 4, 0, 1, 4, 1, 1, 2, 0]);
            if (!missingChannel) Add([37, 10, 5, 0, 1, 4, 1, 1, (byte)(duplicateMember ? 2 : 3), 0]);
            Add([127, 4, 255, 255]);
            var result = new byte[8 + records.Count];
            result[1] = 3; result[2] = 9;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)records.Count);
            records.CopyTo(result, 8);
            return result;
        }
        Require(V269SmbiosChannels.Parse(SmbiosFixture(), 2) == 2 &&
            V269SmbiosChannels.Parse(SmbiosFixture(), 1) is null &&
            V269SmbiosChannels.Parse(SmbiosFixture(duplicateMember: true), 2) is null &&
            V269SmbiosChannels.Parse(SmbiosFixture(missingChannel: true), 2) is null &&
            V269SmbiosChannels.Parse(SmbiosFixture()[..^1], 2) is null,
            "canais só aparecem com todos os handles de RAM corroborados, sem truncamento ou duplicação");
        var channels = MainForm.V269HomeModels(snapshot with { PlatformHardware = platform with {
            ReportedPopulatedMemoryChannels = 2 } });
        Require(channels[2].Specs.Single(x => x.Label == "Canais").Value == "2 (SMBIOS)" &&
            channels[2].Specs.Single(x => x.Label == "Latência").Value == "N/D",
            "topologia não é convertida em dual channel ativo ou CAS");
        var models = MainForm.V269HomeModels(snapshot);
        Require(models[4].Sensors.Single(x => x.Label == "TPM").Value == "Presente (2.0)" &&
            V269TpmReader.Describe(0, 1, 1) == "Presente (1.2)" &&
            V269TpmReader.Describe(0x8028400F, 0, 0) == "Não detectado (TBS)" &&
            V269TpmReader.Describe(0x80284002, 1, 2) is null &&
            V269TpmReader.Describe(0, 99, 2) is null &&
            V269TpmReader.Describe(0, 1, 99) is null,
            "TPM confirmado, não encontrado e inconclusivo permanecem distintos");
        Require(models[1].Specs.Single(x => x.Label == "Barramento").Value ==
                "GPU fixture NVML: PCIe 4.0 x8 (atual) / GPU fixture AMD: PCIe 3.0 x16 (Windows, consulta)" &&
            NativeHardwareDiscovery.FormatWindowsPcie(5, 8) == "PCIe 5.0 x8 (Windows, consulta)" &&
            NativeHardwareDiscovery.FormatWindowsPcie(6, 8) is null &&
            NativeHardwareDiscovery.FormatWindowsPcie(3, 0) is null &&
            NvidiaNvmlSensorProvider.Bar1Capacity(8UL << 30, 7UL << 30, 1UL << 30) == 8UL << 30 &&
            NvidiaNvmlSensorProvider.Bar1Capacity(0, 0, 0) is null &&
            NvidiaNvmlSensorProvider.Bar1Capacity(1, 2, 3) is null &&
            models[1].Specs.Single(x => x.Label == "Driver").Value ==
                "GPU fixture AMD: 32.0.1.123 (Windows) / 576.02 (NVIDIA NVML)" &&
            NativeHardwareDiscovery.ValidateInstalledDriver("32.0.1.123") == "32.0.1.123" &&
            NativeHardwareDiscovery.ValidateInstalledDriver("32.0.1.65536") is null &&
            NativeHardwareDiscovery.ValidateInstalledDriver("32.0") is null &&
            models[1].Specs.Single(x => x.Label == "Resizable BAR").Value == "N/D" &&
            models[1].Specs.Single(x => x.Label == "Arquitetura").Value == "GPU fixture NVML: Blackwell (NVML)" &&
            NvidiaNvmlSensorProvider.FormatArchitecture(7) == "Ampere" &&
            NvidiaNvmlSensorProvider.FormatArchitecture(uint.MaxValue) is null &&
            NvidiaNvmlSensorProvider.FormatArchitecture(null) is null &&
            NvidiaNvmlSensorProvider.FormatPcie(4, null) is null &&
            NvidiaNvmlSensorProvider.FormatPcie(4, 3) is null &&
            NvidiaNvmlSensorProvider.FormatPcie(0, 8) is null &&
            NvidiaNvmlSensorProvider.ValidateDriverVersion("unknown") is null,
            "PCIe e driver vêm da NVML sem presumir ReBAR ou converter versões");
        var identity = NvidiaNvmlSensorProvider.ParsePciIdentity(0x2d0410de, 0x12341043);
        Require(identity is { Vendor: 0x10de, Device: 0x2d04, Subsystem: 0x12341043 } &&
            NvidiaNvmlSensorProvider.ParsePciIdentity(0x12341002, 1) is null &&
            NvidiaNvmlSensorProvider.ParsePciIdentity(0x2d0410de, 0) is null &&
            NvidiaNvmlSensorProvider.ValidateIntendedRpm(0) == 0 &&
            NvidiaNvmlSensorProvider.ValidateIntendedRpm(uint.MaxValue) is null,
            "PCI e RPM alvo validados sem misturar unidades");
        var unifiedGpu = gpu.Devices[0] with { Name = "NVIDIA GeForce RTX 5060 Ti", PciIdentity = identity,
            DedicatedMemoryBytes = 8UL << 30, Bar1TotalBytes = 8UL << 30,
            IntendedFanRpms = [1301U, 1302U] };
        var exactDevice = new HardwareDeviceDescriptor("PCI\\VEN_10DE&DEV_2D04&SUBSYS_12341043\\fixture",
            unifiedGpu.Name, "NVIDIA", "Display", "PCI", HardwareDeviceKind.Graphics)
            { InstalledDriverVersion = "32.0.15.7602" };
        var exactAdapter = new DxgiGpuIdentity(1, 2, unifiedGpu.Name, 0x10de, 0x2d04, 0x12341043,
            8UL << 30, false);
        var exact = snapshot with {
            DeviceInventory = pnp with { Devices = [exactDevice] },
            NvidiaGpus = gpu with { Devices = [unifiedGpu] },
            DxgiAdapters = new DxgiGpuSnapshot(at, HardwareDiscoveryState.Complete, [exactAdapter], null)
        };
        Require(DxgiGpuIdentityProvider.InterpretDiscreteUploadHeap(1, 0, 0, 0, 1) == true &&
            DxgiGpuIdentityProvider.InterpretDiscreteUploadHeap(1, 0, 0, 0, 0) == false &&
            DxgiGpuIdentityProvider.InterpretDiscreteUploadHeap(1, 0, 1, 0, 1) is null &&
            DxgiGpuIdentityProvider.InterpretDiscreteUploadHeap(2, 0, 0, 0, 1) is null &&
            DxgiGpuIdentityProvider.InterpretDiscreteUploadHeap(1, 0, 0, -1, 1) is null,
            "D3D12: UMA, adaptadores vinculados e falhas não confirmam VRAM acessível");
        var priorDxgi = exact.DxgiAdapters! with {
            Adapters = [exactAdapter with { DiscreteGpuUploadHeapSupported = true }]
        };
        var retained = DxgiGpuIdentityProvider.RetainUploadHeapCapability(exact.DxgiAdapters!, priorDxgi);
        Require(retained.Adapters.Single().DiscreteGpuUploadHeapSupported == true &&
            DxgiGpuIdentityProvider.RetainUploadHeapCapability(exact.DxgiAdapters! with {
                Adapters = [exactAdapter with { LuidLow = 3 }]
            }, priorDxgi).Adapters.Single().DiscreteGpuUploadHeapSupported is null &&
            MainForm.V269BarEvidence(retained.Adapters[0], unifiedGpu) == "N/D · VRAM acessível (D3D12)" &&
            MainForm.V269BarEvidence(exactAdapter with { DiscreteGpuUploadHeapSupported = false }, null) == "N/D",
            "D3D12 só conserva capacidade consultada para identidade correspondente; não afirma ReBAR");
        var compactGpu = MainForm.V269GpuModel(exact);
        Require(MainForm.TryV269SingleGpu(exact, out _, out _, out _) &&
            compactGpu.Name == unifiedGpu.Name &&
            compactGpu.Specs.Single(x => x.Label == "Memória").Value == "8 GiB GDDR7 (catálogo)" &&
            compactGpu.Specs.Single(x => x.Label == "Driver").Value == "32.0.15.7602" &&
            compactGpu.Specs.Single(x => x.Label == "Resizable BAR").Value == "N/D · BAR1 8 GiB" &&
            compactGpu.Sensors.Single(x => x.Label == "Ventoinha (GPU)").Value == "1301/1302 RPM (alvo)" &&
            !MainForm.TryV269SingleGpu(exact with { DeviceInventory = pnp with {
                Devices = [exactDevice, exactDevice with { InstanceId = exactDevice.InstanceId + "2" }] } },
                out _, out _, out _) &&
            !MainForm.TryV269SingleGpu(exact with { NvidiaGpus = gpu with {
                Devices = [unifiedGpu with { PciIdentity = null }] } }, out _, out _, out _) &&
            MainForm.V269GpuMemoryType(unifiedGpu with { Name = unifiedGpu.Name + " Laptop GPU" }, 8UL << 30) is null,
            "cartão compacto apenas com identidade corroborada; BAR1 e RPM alvo não viram estado/medição");
        var live = MainForm.UpdateV269LiveRows(models, sample with {
            CpuUsagePercent = MonitorReading<double>.Available(37, "fixture ao vivo"),
            Memory = new MemoryRamSample(DateTime.UtcNow, 16UL << 30, 4UL << 30),
            SystemUptime = MonitorReading<TimeSpan>.Available(TimeSpan.FromHours(4), "fixture ao vivo")
        });
        Require(live[0].Sensors.Single(x => x.Label == "Uso").Value == "37%" &&
            live[2].Sensors.Single(x => x.Label == "Disponível").Value == "4 GiB" &&
            live[4].Specs.Single(x => x.Label == "Tempo de atividade").Value == "0 dias, 4 h e 0 min" &&
            live[0].Specs.SequenceEqual(models[0].Specs) &&
            ReferenceEquals(live[1], models[1]) && ReferenceEquals(live[3], models[3]) &&
            live[4].Sensors.SequenceEqual(models[4].Sensors),
            "amostra leve atualiza CPU/RAM/uptime sem inventar ou recapturar dados estáticos");
        var unavailableCounters = MainForm.V269UnavailableLiveCounters(at);
        var unavailableLive = MainForm.UpdateV269LiveRows(live, unavailableCounters);
        Require(unavailableLive[0].Sensors.Single(x => x.Label == "Uso").Value == "N/D" &&
            unavailableLive[2].Sensors.All(x => x.Value == "N/D") &&
            unavailableLive[4].Specs.Single(x => x.Label == "Tempo de atividade").Value == "N/D" &&
            unavailableLive[0].Specs.SequenceEqual(models[0].Specs) &&
            ReferenceEquals(unavailableLive[1], live[1]),
            "falha da leitura leve invalida CPU/RAM/uptime, sem apagar inventário ou GPU independente");
        var gpuFailed = MainForm.V269WithoutGpuReadings(exact, at);
        Require(MainForm.V269GpuModel(gpuFailed).Sensors.All(x => x.Value == "N/D") &&
            gpuFailed.NvidiaGpus.Devices.Count == 0 && gpuFailed.WindowsGpuCounters is null &&
            gpuFailed.D3dKmtGpuSensors is null && ReferenceEquals(gpuFailed.DeviceInventory, exact.DeviceInventory) &&
            ReferenceEquals(gpuFailed.WindowsCounters, exact.WindowsCounters),
            "falha GPU elimina leituras vencidas também dos detalhes e mantém inventário e CPU");
        Require(models[0].Name == "CPU fixture" &&
            models[0].Specs.Any(x => x.Label == "Núcleos / Threads" && x.Value == "8 / 16") &&
            models[0].Sensors.Any(x => x.Label == "Uso" && x.Value == "0%") &&
            models[0].Sensors.Any(x => x.Label == "Temperatura" && x.Value == "N/D"),
            "zero real não é ausência; núcleos e temperatura respeitam as fontes");
        Require(models[1].Name == "GPU fixture NVML / GPU fixture AMD" &&
            models[1].Sensors.Select(x => x.Label).SequenceEqual(
                new[] { "Temperatura", "Uso", "VRAM usada", "Ventoinha (GPU)" }) &&
            models[1].Sensors.Single(x => x.Label == "Ventoinha (GPU)").Value ==
                "GPU fixture NVML: 35% (NVML; não RPM)",
            "quatro sensores do HTML, sem conversão indevida de percentual em RPM");
        var noNvml = snapshot with { NvidiaGpus = new NvidiaGpuSensorSnapshot(at, false, [], "fixture indisponível") };
        var noNvmlModel = MainForm.V269GpuModel(noNvml);
        Require(noNvmlModel.Sensors.Count == 4 && noNvmlModel.Sensors.All(x => x.Value == "N/D") &&
            noNvmlModel.Specs.Single(x => x.Label == "Driver").Value == "GPU fixture AMD: 32.0.1.123 (Windows)",
            "falha NVML não conserva sensor antigo nem elimina driver Windows");
        var newerGpu = snapshot with { NvidiaGpus = gpu with {
            Devices = [gpu.Devices[0] with { TemperatureC = MonitorReading<double>.Available(66, "fixture ao vivo") }]
        }};
        Require(MainForm.V269GpuModel(newerGpu).Sensors.Single(x => x.Label == "Temperatura").Value.Contains("66 °C") &&
            MainForm.V269GpuModel(newerGpu).Sensors.Count == 4,
            "nova amostra GPU substitui leituras sem acrescentar linhas");
        Require(models[2].Specs.Any(x => x.Label == "Tipo" && x.Value == "DDR4") &&
            models[2].Specs.Any(x => x.Label == "Capacidade" && x.Value == "16 GiB") &&
            models[3].Specs.Any(x => x.Label == "Modelo" && x.Value == "Modelo fixture") &&
            models[3].Sensors.All(x => x.Value == "N/D"),
            "RAM e placa-mãe vêm das fontes reais; nenhuma temperatura é inventada");
        Require(models[0].Specs.Any(x => x.Label == "Arquitetura" && x.Value == "x64 (ISA)") &&
            models[0].Specs.Any(x => x.Label == "Cache L3" && x.Value == "32 MiB") &&
            models[0].Specs.Where(x => x.Label is "Frequência base" or "Frequência turbo")
                .All(x => x.Value == "N/D") &&
            models[3].Specs.Any(x => x.Label == "BIOS" && x.Value == "BIOS fixture") &&
            models[3].Specs.Any(x => x.Label == "Soquete" && x.Value == "Designação: Socket fixture") &&
            models[4].Sensors.Any(x => x.Label == "Virtualização" && x.Value == "Ativa (firmware)"),
            "ISA, cache, designação do soquete, BIOS e virtualização não são inferidos");
        var nominal = V269CpuSpecCatalog.Resolve(platform.CpuSockets[0] with {
            ReportedName = "AMD Ryzen 7 5700X 8-Core Processor" });
        Require(nominal is { BaseMhz: 3400, BoostMhz: 4600, L3MiB: 32, Socket: "AM4" } &&
            V269CpuSpecCatalog.Resolve(platform.CpuSockets[0] with {
                ReportedName = "AMD Ryzen 7 5700X3D 8-Core Processor" }) is null &&
            V269CpuSpecCatalog.Resolve(platform.CpuSockets[0] with {
                ReportedName = "AMD Ryzen 7 5700X", LogicalProcessors = 8 }) is null,
            "catálogo nominal exige SKU exato, arquitetura e topologia corroboradas");
        Require(V269IntelCpuidClocks.Parse(3200, 4600) is { BaseMhz: 3200, MaximumMhz: 4600 } &&
            V269IntelCpuidClocks.Parse(0, 4600) is null &&
            V269IntelCpuidClocks.Parse(3200, 1000) is { MaximumMhz: null },
            "CPUID: base zero e máximo contraditório não são convertidos em dados");
        var intel = MainForm.V269HomeModels(snapshot with { PlatformHardware = platform with {
            IntelNominalBaseMhz = 3200, IntelReportedMaximumMhz = 4600 } });
        Require(intel[0].Specs.Single(x => x.Label == "Frequência base").Value.Contains("CPUID") &&
            intel[0].Specs.Single(x => x.Label == "Frequência turbo").Value == "N/D",
            "CPUID máximo não é confundido com Turbo Boost");
        foreach (var pair in new[] { (18U, "DDR"), (19U, "DDR2"), (20U, "DDR2 FB-DIMM"), (21U, "N/D") })
        {
            var legacy = WindowsPlatformHardwareProvider.Join([], 
                [new WmiRamModuleRaw("B0", "D0", 1UL << 30, 400U, pair.Item1)], [], []);
            var legacyModel = MainForm.V269HomeModels(snapshot with { PlatformHardware = legacy });
            Require(legacyModel[2].Specs.Single(x => x.Label == "Tipo").Value == pair.Item2,
                "enumeração SMBIOS Type 17 " + pair.Item1);
            Require(legacyModel[4].Sensors.Single(x => x.Label == "Virtualização").Value == "N/D",
                "virtualização desconhecida não vira inativa");
        }

        // O dashboard real conserva instâncias e permite comparação nos três temas.
        var cardIdentity = home.Cards.ToArray();
        home.SetModels(models);
        var unchangedCpu = models[0] with { Specs = models[0].Specs.ToArray(), Sensors = models[0].Sensors.ToArray() };
        Require(!home.Cards[0].SetModel(unchangedCpu) &&
            home.Cards[0].SetModel(live[0]) &&
            home.Cards[0].AccessibleDescription?.Contains("37%") == true &&
            !home.Cards[0].SetModel(live[0]),
            "amostras idênticas não invalidam cartão; alterações atualizam também a descrição acessível");
        home.SetModels(models);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (home.Cards.Any(x => !x.ArtReady && x.ArtError is null) &&
            clock.Elapsed < TimeSpan.FromSeconds(15))
        { Application.DoEvents(); Thread.Sleep(10); }
        var failures = home.Cards.Where(x => !x.ArtReady)
            .Select(x => x.Key + "=" + (x.ArtError ?? "timeout")).ToArray();
        Require(failures.Length == 0, "decodificação das cinco artes originais: " + string.Join(", ", failures));
        Require(!home.HorizontalScroll.Visible && !home.VerticalScroll.Visible,
            "Início não exibe barras de rolagem nativas");
        home.AutoScrollPosition = new Point(0, int.MaxValue);
        Require(-home.AutoScrollPosition.Y == Math.Max(0, home.ContentHeight - home.ClientSize.Height),
            "rolagem alcança o final sem ultrapassar o conteúdo");
        Require(home.Controls.Find("V269ReadHardware", true).Length == 0 &&
            home.Controls.Find("V269HardwareDetails", true).Length == 0,
            "REC-FIS-02: comandos removidos do dashboard");
        home.AutoScrollPosition = Point.Empty;
        Require(home.AutoScrollPosition == Point.Empty && home.Cards[0].Top >= 0,
            "rolagem retorna ao topo sem cortar o primeiro cartão");

        Directory.CreateDirectory("LayoutCaptures");
        var themeChoice = form.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
        foreach (var theme in Enum.GetValues<V269Theme>())
        {
            // Usa a mesma ligação das Configurações, não apenas a paleta do cartão.
            themeChoice.SelectedIndex = (int)theme;
            home.ApplyAppearance(new V269AppearanceOptions(theme, 100, 100, true));
            home.AutoScrollPosition = Point.Empty;
            home.PerformLayout();
            Application.DoEvents();
            Require(home.Cards.SequenceEqual(cardIdentity), "tema não recria cartões");
            var cards = home.Cards;
            Require(cards.All(c => c.Width > 0 && c.Height > 0) &&
                cards[4].Width >= cards[0].Width &&
                cards[4].Top >= cards[3].Bottom,
                "cartão Windows ocupa linha própria e não sobrepõe os outros");
            if (cards[0].Top == cards[1].Top)
                Require(cards[0].Right < cards[1].Left &&
                    cards[4].Width > cards[0].Width, "duas colunas e Windows de largura integral");
            using var capture = new Bitmap(home.Width, home.Height);
            home.DrawToBitmap(capture, new Rectangle(Point.Empty, home.Size));
            capture.Save($"LayoutCaptures/v269-inicio-{theme}.png");
            using var full = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(full, new Rectangle(Point.Empty, form.Size));
            full.Save($"LayoutCaptures/v269-inicio-janela-{theme}.png");
            foreach (var card in cards)
            {
                using var detail = new Bitmap(card.Width, card.Height);
                card.DrawToBitmap(detail, new Rectangle(Point.Empty, card.Size));
                detail.Save($"LayoutCaptures/v269-inicio-{theme}-{card.Key}.png");
            }
        }
        // Captura comparável à arte congelada: os mesmos textos ilustrativos
        // do HTML, apenas em fixture. Nunca passados ao motor nem à versão real.
        // A matriz funcional continua usando leituras sintéticas independentes.
        V269HomeRow[] Rows(params string[] fields)
        {
            var rows = new List<V269HomeRow>();
            for (int i = 0; i < fields.Length; i += 2)
                rows.Add(new(fields[i], fields[i + 1]));
            return rows.ToArray();
        }
        V269HomeModel[] visualFixture = [
            new("cpu", "Processador (CPU)", "AMD Ryzen 7 5700X",
                Rows("Arquitetura", "Zen 3 (Vermeer)", "Núcleos / Threads", "8 / 16",
                    "Frequência base", "3,40 GHz", "Frequência turbo", "4,60 GHz", "Cache L3", "32 MB"),
                Rows("Temperatura", "41 °C", "Uso", "14%", "Consumo", "47 W", "Ventoinha (CPU)", "1.126 RPM")),
            new("gpu", "Placa de vídeo (GPU)", "GeForce RTX 5060 Ti",
                Rows("Memória", "8 GB GDDR7", "Barramento", "PCIe 4.0 x8", "Resizable BAR", "Ativo",
                    "Driver", "32.0.15.7602", "Arquitetura", "Blackwell"),
                Rows("Temperatura", "47 °C", "Uso", "12%", "VRAM usada", "1,6 GB", "Ventoinha (GPU)", "1.301 RPM")),
            new("ram", "Memória (RAM)", "",
                Rows("Capacidade", "32 GB", "Tipo", "DDR4", "Frequência", "3.200 MHz", "Canais", "Dual Channel", "Latência", "CL16"),
                Rows("Uso atual", "13,4 GB", "Disponível", "18,6 GB")),
            new("motherboard", "Placa-mãe / Chipset", "",
                Rows("Fabricante", "ASUS", "Modelo", "TUF GAMING B550-PLUS", "Chipset", "AMD B550", "BIOS", "3405", "Soquete", "AM4"),
                Rows("Temp. chipset", "36 °C", "Temp. VRM", "40 °C")),
            new("windows", "Windows", "",
                Rows("Versão", "Windows 11 Pro (24H2)", "Compilação", "26100.7171", "Arquitetura", "64 bits (x64)",
                    "Tempo de atividade", "2 dias, 14 h e 27 min"),
                Rows("Inicialização segura", "Ativa", "TPM", "Presente (2.0)", "Virtualização", "Ativa"))
        ];
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        Size originalSurfaceSize = surface.Size;
        try
        {
            Require(V269HomeCard.UsesVersionedWindowsArt(visualFixture[4]) &&
                !V269HomeCard.UsesVersionedWindowsArt(visualFixture[4] with {
                    Specs = Rows("Versão", "Windows 11 Pro (26H2)") }) &&
                !V269HomeCard.UsesVersionedWindowsArt(empty[4]),
                "REC-FIS-02: arte com edição/versão somente quando coincide com inventário");
            home.SetModels(models);
            // Reproduzir janela larga e baixa da evidência física, não altura do monitor.
            surface.Size = new Size(1536,828);
            foreach (var theme in Enum.GetValues<V269Theme>())
            {
                themeChoice.SelectedIndex = (int)theme;
                home.ApplyAppearance(new V269AppearanceOptions(theme, 100, 100, true));
                home.AutoScrollPosition = Point.Empty;
                surface.PerformLayout(); home.PerformLayout(); Application.DoEvents();
                Require(home.Cards.All(c => home.ClientRectangle.Contains(c.Bounds)) &&
                    home.ContentHeight <= home.ClientSize.Height,
                    $"REC-FIS-02: cinco cartões e rodapé inteiros sem rolagem {theme} " +
                    $"viewport={home.ClientSize} content={home.ContentHeight}");
                home.AutoScrollPosition = new Point(0, int.MaxValue);
                Require(home.AutoScrollPosition == Point.Empty,
                    "REC-FIS-02: sem deslocamento ordinário real");
            }
            Console.WriteLine("PASS REC-FIS-02-HOME: cinco cartões sem rolagem ordinária nos três temas, sem comandos.");
            home.SetModels(visualFixture);
            // 716px é um teste de estresse nativo; o HTML impõe min-width:760px.
            // Conservar o estresse e acrescentar os dois tamanhos efetivamente comparáveis.
            foreach (Size viewportSize in new[] { new Size(716,596), new Size(760,596), new Size(992,659) })
            {
                surface.Size = viewportSize;
                foreach (var theme in Enum.GetValues<V269Theme>())
                {
                    themeChoice.SelectedIndex = (int)theme;
                    home.ApplyAppearance(new V269AppearanceOptions(theme, 100, 100, true));
                    var headingIcon = form.Controls.Find("V269HomeHeadingIcon", true).Single();
                    Require(headingIcon.ForeColor == ColorTranslator.FromHtml(theme switch {
                        V269Theme.Dark => "#65b8ef", V269Theme.Claro => "#2577a8", _ => "#69bfe0" }),
                        "cor efetiva do SVG de Início no tema " + theme);
                    home.AutoScrollPosition = Point.Empty;
                    surface.PerformLayout(); home.PerformLayout(); Application.DoEvents();
                    Require(home.Cards[0].Width > 0 && home.Cards[0].Height > 0 &&
                        (viewportSize.Width >= 940) == (home.Cards[0].Top == home.Cards[1].Top),
                        "colunas do viewport de referência " + viewportSize);
                    Require(surface.Size == viewportSize, "dimensão efetiva da captura " + viewportSize);
                    if (home.Cards[0].Top == home.Cards[1].Top)
                        Require(home.Cards[1].Right == home.Cards[4].Right &&
                            home.Cards[3].Right == home.Cards[4].Right,
                            "colunas completam a largura da grade sem perder pixel ímpar");
                    // Evidência textual comparável mesmo sem extrair o PNG do artefato.
                    Point origin = surface.PointToScreen(Point.Empty);
                    var geometry = new { Theme = theme.ToString(), Width = surface.Width, Height = surface.Height,
                        Dpi = home.DeviceDpi, WorkAreaHeight = Screen.FromControl(home).WorkingArea.Height,
                        Cards = home.Cards.Select(card => {
                            Point position = card.PointToScreen(Point.Empty);
                            return new { card.Key, X = position.X - origin.X, Y = position.Y - origin.Y,
                                card.Width, card.Height };
                        }).ToArray() };
                    string geometryJson = System.Text.Json.JsonSerializer.Serialize(geometry);
                    Console.WriteLine("V269-HOME-GEOMETRY " + geometryJson);
                    // HTML aprovado SHA-256 9942b8ed29335cb526825b7658d0f8ab11f2c7241b46a6a99b959941b31dca44.
                    // DOM medido em Chromium com janela compacta e zoom 100%.
                    // Validar coordenadas inclusive fora da área visível, em
                    // 96 DPI e faixa da media query homologada. Tolerância de
                    // 2px para frações CSS/GDI, não para diferenças de estrutura.
                    if (geometry.Dpi == 96 && geometry.WorkAreaHeight <= 810 &&
                        viewportSize.Width is 760 or 992)
                    {
                        (int X, int Y, int Width, int Height)[] expected =
                            viewportSize.Width == 760
                            ? [(214,153,518,226), (214,386,518,226), (214,620,518,187),
                               (214,814,518,187), (214,1008,518,202)]
                            : [(214,153,371,170), (592,153,372,170), (214,330,371,157),
                               (592,330,372,157), (214,494,750,152)];
                        for (int i = 0; i < expected.Length; i++)
                        {
                            var actual = geometry.Cards[i];
                            var reference = expected[i];
                            Require(Math.Abs(actual.X - reference.X) <= 2 &&
                                Math.Abs(actual.Y - reference.Y) <= 2 &&
                                Math.Abs(actual.Width - reference.Width) <= 1 &&
                                Math.Abs(actual.Height - reference.Height) <= 2,
                                $"geometria do HTML {theme} {viewportSize} {actual.Key}: " +
                                $"esperada {reference}; observada ({actual.X},{actual.Y},{actual.Width},{actual.Height})");
                        }
                    }
                    File.WriteAllText($"LayoutCaptures/v269-inicio-geometria-{theme}-{viewportSize.Width}x{viewportSize.Height}.json", geometryJson);
                    using var same = new Bitmap(surface.Width, surface.Height);
                    surface.DrawToBitmap(same, new Rectangle(Point.Empty, surface.Size));
                    string captureKind = viewportSize.Width < 760 ? "ESTRESSE-SINTETICO" : "REFERENCIA-SINTETICA";
                    same.Save($"LayoutCaptures/v269-inicio-{captureKind}-{theme}-{viewportSize.Width}x{viewportSize.Height}.png");
                }
            }
        }
        finally { surface.Size = originalSurfaceSize; surface.PerformLayout(); Application.DoEvents(); }
        themeChoice.SelectedIndex = (int)V269Theme.AzulGf;
        home.SetModels(empty);
        home.ApplyAppearance(V269AppearanceOptions.Default);
        home.AutoScrollPosition = Point.Empty;
        Console.WriteLine("PASS Início V2.6.9: cinco cartões, artes originais, fontes reais, três temas, geometria e capturas.");
        return snapshot;
    }

    private static T? Find<T>(Control control) where T : Control
    {
        if (control is T match) return match;
        return control.Controls.Cast<Control>().Select(Find<T>).FirstOrDefault(child => child is not null);
    }

    private sealed class PaneTestSynchronizationContext(Control owner) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            owner.BeginInvoke(new Action(() => callback(state)));
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
