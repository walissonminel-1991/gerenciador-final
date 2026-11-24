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
        var homeSnapshot = VerifyHome(form, views[0]);
        var paneToggle = form.Controls.Find("V269PaneToggle", true).OfType<Button>().Single();
        Require(paneToggle.Parent?.Name == "V269HomeFooter",
            "controle adicional não altera o menu do HTML aprovado");
        Require(!shell.RightPane.Visible && paneToggle.Visible && paneToggle.Enabled,
            "Início usa toda a largura, mas mantém acesso explícito ao painel");
        paneToggle.PerformClick();
        Application.DoEvents();
        Require(shell.RightPane.Visible &&
            form.Controls.Find("LocalReliabilityReportButton", true).OfType<Button>().Single().Visible,
            "abertura manual conserva o acesso à exportação do histórico local");
        paneToggle.PerformClick();
        Application.DoEvents();
        Require(!shell.RightPane.Visible, "fechar painel restaura o dashboard");
        var confirm = typeof(MainForm).GetMethod("ConfirmV269Async",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var waiting = (Task<bool>)confirm.Invoke(form, ["Revisão de teste",
            "Alvo sintético. Nenhuma manutenção.", CancellationToken.None])!;
        shell.SelectPage(V269Page.Drivers);
        Require(!form.Controls.Find("V269HomeMinimize", true).Single().Visible &&
            paneToggle.Parent?.Name == "V269Navigation",
            "as demais abas conservam a barra e o controle de operações anteriores");
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
        form.Close();
        Console.WriteLine("PASS V2.6.9 integração: MainForm ativa, oito views, confirmação, cancelamento e resultado persistente.");
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
        var themeChoice=page.Appearance.Controls.Find("V269ThemeChoice",true).OfType<ComboBox>().Single();
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
                    var origin=surface.PointToScreen(Point.Empty);
                    var geom=new{Theme=theme.ToString(),Width=surface.Width,Height=surface.Height,Dpi=page.DeviceDpi,
                        WorkAreaHeight=Screen.FromControl(page).WorkingArea.Height,
                        Cards=new[]{(Control)page.General,page.Appearance}.Select(card=>{
                            var p=card.PointToScreen(Point.Empty);return new{card.Name,X=p.X-origin.X,Y=p.Y-origin.Y,card.Width,card.Height};}).ToArray()};
                    Console.WriteLine("V269-SETTINGS-GEOMETRY "+System.Text.Json.JsonSerializer.Serialize(geom));
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
                    int previousOffset=page.ScrollOffset,previousScale=range.Value;
                    if(previousOffset>0)
                    {
                        page.ScrollByWheel(60);page.ScrollByWheel(60);
                        Require(page.ScrollOffset<previousOffset&&range.Value==previousScale,
                            "roda fracionada rola Configurações sem alterar escala");
                        page.ScrollTo(int.MaxValue);
                    }
                    using(var shot=new Bitmap(surface.Width,surface.Height))
                    {
                        surface.DrawToBitmap(shot,new Rectangle(Point.Empty,surface.Size));
                        shot.Save($"LayoutCaptures/v269-configuracoes-RODAPE-SINTETICO-{theme}-{size.Width}x{size.Height}.png");
                    }
                }
            }
        }
        finally
        {
            surface.Size=originalSize;themeChoice.SelectedIndex=(int)old.Theme;page.ScrollTo(0);
            shell.PresentPanel(originalPanel);surface.PerformLayout();Application.DoEvents();
        }
        Console.WriteLine("PASS Configurações V2.6.9: preferências descartáveis, estados observados, três temas, rolagem e capturas.");
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
