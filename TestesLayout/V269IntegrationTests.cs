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
        VerifyAppOrdering();
        VerifyAppTableGeometry();
        VerifyAppIconSources();
        Require(form.Controls.Find("V269WindowsUpdateHistory", true).OfType<Button>().Single().Text == "Histórico" &&
            form.Controls.Find("V269History", true).OfType<Button>().Single().Text == "Histórico de registro",
            "históricos específicos previstos no HTML, sem botão genérico compartilhado");
        var homeSnapshot = VerifyHome(form, views[0]);
        var paneToggle = form.Controls.Find("V269PaneToggle", true).OfType<Button>().Single();
        Require(paneToggle.Parent?.Name == "V269HomeFooter",
            "controle adicional não altera o menu do HTML aprovado");
        Require(!shell.RightPane.Visible && paneToggle.Visible && paneToggle.Enabled,
            "Início usa toda a largura, mas mantém acesso explícito ao painel");
        paneToggle.PerformClick();
        Application.DoEvents();
        Require(shell.RightPane.Visible &&
            form.Controls.Find("V269LocalHistory", true).Length == 0 &&
            form.Controls.Find("LocalReliabilityReportButton", true).Length == 0,
            "painel compartilhado sem atalhos duplicados de histórico; registro acessível em Configurações");
        paneToggle.PerformClick();
        Application.DoEvents();
        Require(!shell.RightPane.Visible, "fechar painel restaura o dashboard");
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
            shell.SelectPage(V269Page.Drivers);
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
        }
        // A consulta de hardware não pode tornar um resultado anterior inacessível.
        // Usar somente a fotografia sintética já validada, sem chamar provedores reais.
        shell.SelectPage(V269Page.Inicio);
        Application.DoEvents();
        var snapshotField = typeof(MainForm).GetField("_v269LastSnapshot",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var previousSnapshot = snapshotField.GetValue(form);
        var detailButton = form.Controls.Find("V269HardwareDetails", true).OfType<Button>().Single();
        var previousEnabled = detailButton.Enabled;
        bool openedReadOnly = false;
        using (var closeDialog = new System.Windows.Forms.Timer { Interval = 50 })
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            closeDialog.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Name == "V269HardwareDetailsDialog");
                if (dialog is not null)
                {
                    var detailText = dialog.Controls.Find("V269HardwareDetailsText", true).OfType<TextBox>().SingleOrDefault();
                    openedReadOnly = detailText is { ReadOnly: true } &&
                        detailText.Text.Contains("Processador") && detailText.Text.Contains("Fotografia local") &&
                        shell.RightPane.Current?.Description == "Resultado preservado";
                    dialog.Close();
                }
                else if (clock.Elapsed > TimeSpan.FromSeconds(3)) closeDialog.Stop();
            };
            try
            {
                snapshotField.SetValue(form, homeSnapshot);
                detailButton.Enabled = true;
                detailButton.Focus();
                Application.DoEvents();
                // ModernButton dispara Click somente após 145 ms de feedback.
                // Manter a fotografia e o timer até o despacho real e fechamento.
                Require(detailButton.Visible && detailButton.Enabled && detailButton.CanSelect,
                    "botão de leituras alcançável com resultado preservado");
                closeDialog.Start();
                detailButton.PerformClick();
                var dispatchClock = System.Diagnostics.Stopwatch.StartNew();
                while (!openedReadOnly && dispatchClock.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }
            }
            finally
            {
                closeDialog.Stop();
                detailButton.Enabled = previousEnabled;
                snapshotField.SetValue(form, previousSnapshot);
            }
        }
        Require(openedReadOnly && shell.RightPane.Current?.Description == "Resultado preservado",
            "botão de leituras funciona sem substituir resultado anterior nem consultar hardware real");
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
        VerifySettings(form,shell);
        VerifyApps(form,shell,confirmationContext);
        VerifyDrivers(form,shell);
        VerifyCleanup(form,shell);
        form.Close();
        Console.WriteLine("PASS V2.6.9 integração: MainForm ativa, oito views, confirmação, cancelamento e resultado persistente.");
    }

    private static void VerifyCleanup(MainForm form,V269Shell shell)
    {
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
                theme.SelectedIndex=(int)palette;surface.Size=size;surface.PerformLayout();relayout();Application.DoEvents();
                var cards=form.Controls.Find("V269CleanupActions",true).OfType<DarkScrollableFlow>().Single();
                Require(cards.Content.Controls.Count==6&&cards.Content.Controls.OfType<V269CleanupCard>().Count()==6,
                    "Limpeza mantém seis ferramentas, sem executar manutenção");
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
                var toolbar=form.Controls.Find("V269CleanupToolbar",true).OfType<TableLayoutPanel>().Single();
                Require(toolbar.Controls.OfType<Button>().Count()==3&&toolbar.Controls.OfType<Button>()
                    .All(button=>toolbar.ClientRectangle.Contains(button.Bounds)&&button.Width>0&&button.Height>0),
                    "três comandos da barra permanecem dentro da área útil");
                using var capture=new Bitmap(surface.Width,surface.Height);
                surface.DrawToBitmap(capture,new Rectangle(Point.Empty,surface.Size));
                capture.Save($"LayoutCaptures/v269-limpeza-SINTETICO-{palette}-{size.Width}x{size.Height}.png");
            }
            // Captura do estado exato da referência: lixeira selecionada e quatro resultados anteriores.
            var context=SynchronizationContext.Current;
            try
            {
                foreach(V269Theme palette in Enum.GetValues<V269Theme>())
                {
                    theme.SelectedIndex=(int)palette;surface.Size=new Size(1844,1116);
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
                        using var capture=new Bitmap(surface.Width,surface.Height);
                        surface.DrawToBitmap(capture,new Rectangle(Point.Empty,surface.Size));
                        capture.Save($"LayoutCaptures/v269-limpeza-REFERENCIA-CONFIRMACAO-{palette}-1844x1116.png");
                    }
                    finally{shell.RightPane.ResolveConfirmation(false);Pump(confirmation);}
                    Require(!confirmation.Result,"fixture de referência nunca autoriza manutenção");
                }
            }
            finally{SynchronizationContext.SetSynchronizationContext(context);}
            VerifyInventoryFlowStates(form,shell,V269Page.Limpeza);
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
                        description is TextBox {ScrollBars:ScrollBars.None},"conteúdo curto não apresenta barras de rolagem");
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
                    Require(longFlowViewport.VerticalScroll.Visible&&!longFlowViewport.HorizontalScroll.Visible&&
                        shell.RightPane.Controls.Find("V269PanelDescription",true).Single() is TextBox {ScrollBars:ScrollBars.None}&&
                        !shell.RightPane.Controls.Find("V269AppOperationHistoryScroll",true).Single().Visible,
                        "overflow real usa somente a rolagem externa do painel");
                    shell.PresentPanel(operational);
                    surface.PerformLayout();Application.DoEvents();
                    Require(!longFlowViewport.VerticalScroll.Visible,"reduzir conteúdo remove a rolagem externa");
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

    private static void VerifyInventoryFlowStates(MainForm form, V269Shell shell, V269Page page)
    {
        var theme = form.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        var originalSize = surface.Size;
        var originalTheme = theme.SelectedIndex;
        var previous = shell.RightPane.Current;
        string tab = page == V269Page.Drivers ? "drivers" : page == V269Page.Limpeza ? "limpeza" : "aplicativos";
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
                var before = surface.Size;
                try
                {
                    foreach (var size in new[] { new Size(1200, 659), new Size(1844, 1116) })
                    {
                        surface.Size = size;
                        surface.PerformLayout();
                        Application.DoEvents();
                        using var image = new Bitmap(surface.Width, surface.Height);
                        surface.DrawToBitmap(image, new Rectangle(Point.Empty, surface.Size));
                        image.Save($"LayoutCaptures/v269-{tab}-PAINEL-{stage}-{palette}-{surface.Width}x{surface.Height}.png");
                    }
                }
                finally { surface.Size = before; surface.PerformLayout(); }
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
                Action? cancel = page is V269Page.Drivers or V269Page.Limpeza ? () => cancelled = true : null;
                shell.PresentPanel(new(V269PanelKind.Progress, $"Verificar {tab}", "7 de 25 itens consultados.",
                    28, CancelFlow: cancel, FlowPage:page));
                surface.PerformLayout();
                Application.DoEvents();
                var meter = Find("V269FlowPercentMeter");
                var note = Find("V269FlowNotice");
                Require(meter.Visible && meter.AccessibleDescription == "28%" && note.Visible &&
                    meter.PointToScreen(Point.Empty).Y < note.PointToScreen(Point.Empty).Y,
                    "progresso comprovado aparece antes do aviso");
                Capture("ANDAMENTO", palette);
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
        finally
        {
            cancellation.Cancel();
            SynchronizationContext.SetSynchronizationContext(oldContext);
            theme.SelectedIndex = originalTheme;
            surface.Size = originalSize;
            shell.PresentPanel(previous);
        }
    }

    private static void VerifyAppIconSources()
    {
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
                        page.ScrollByWheel(60);page.ScrollByWheel(60);
                        Require(page.ScrollOffset<previousOffset&&range.Value==previousScale,
                            "roda fracionada rola Configurações sem alterar escala");
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
            shell.PresentPanel(null);Application.DoEvents();
            Require(general.Diagnostic.Focus(),"diagnóstico alcançável");
            general.Diagnostic.PerformClick();Application.DoEvents();
            Require(shell.RightPane.Visible &&
                shell.RightPane.Current is {Kind:V269PanelKind.Details,Title:"Diagnóstico local"} diagnostic &&
                diagnostic.Description.Contains(DiagnosticTrace.VersionLabel) &&
                diagnostic.Description.Contains("RELATÓRIO LOCAL DE CONFIABILIDADE"),
                "diagnóstico apresenta versão e relatório local reais");
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

    private static HardwareSensorPortfolio VerifyHome(MainForm form, Control view)
    {
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
            home.Cards[0].AccessibleDescription.Contains("37%") &&
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
        var read = home.Controls.Find("V269ReadHardware", true).OfType<Button>().Single();
        Require(read.Focus(), "consulta manual alcançável pelo teclado");
        Application.DoEvents();
        Require(LayoutTests.FullyVisible(read), "foco traz a consulta para a área visível");
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
