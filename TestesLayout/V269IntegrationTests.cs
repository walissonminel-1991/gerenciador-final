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
        VerifyHome(form, views[0]);
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
        form.Close();
        Console.WriteLine("PASS V2.6.9 integração: MainForm ativa, oito views, confirmação, cancelamento e resultado persistente.");
    }

    private static void VerifyHome(MainForm form, Control view)
    {
        Require(view is V269HomeDashboard, "Início deve usar o dashboard nativo reconstruído");
        var home = (V269HomeDashboard)view;
        var header = form.Controls.Find("V269BrandHeader", true).OfType<V269BrandHeader>().Single();
        Require(header.IsHomePresentation &&
            form.Controls.Find("V269HomeMinimize", true).Single().Visible &&
            form.Controls.Find("V269HomeClose", true).Single().Visible &&
            !form.MaximizeBox && header.Height >= 60,
            "cabeçalho único com comandos reais, sem maximização");
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
        var live = MainForm.UpdateV269LiveRows(models, sample with {
            CpuUsagePercent = MonitorReading<double>.Available(37, "fixture ao vivo"),
            Memory = new MemoryRamSample(DateTime.UtcNow, 16UL << 30, 4UL << 30),
            SystemUptime = MonitorReading<TimeSpan>.Available(TimeSpan.FromHours(4), "fixture ao vivo")
        });
        Require(live[0].Sensors.Single(x => x.Label == "Uso").Value == "37%" &&
            live[2].Sensors.Single(x => x.Label == "Disponível").Value == "4 GiB" &&
            live[4].Specs.Single(x => x.Label == "Tempo de atividade").Value == "0d 4h 0min" &&
            live[0].Specs.SequenceEqual(models[0].Specs) &&
            ReferenceEquals(live[1], models[1]) && ReferenceEquals(live[3], models[3]) &&
            live[4].Sensors.SequenceEqual(models[4].Sensors),
            "amostra leve atualiza CPU/RAM/uptime sem inventar ou recapturar dados estáticos");
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
        var identity = home.Cards.ToArray();
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
            Require(home.Cards.SequenceEqual(identity), "tema não recria cartões");
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
        themeChoice.SelectedIndex = (int)V269Theme.AzulGf;
        home.SetModels(empty);
        home.ApplyAppearance(V269AppearanceOptions.Default);
        home.AutoScrollPosition = Point.Empty;
        Console.WriteLine("PASS Início V2.6.9: cinco cartões, artes originais, fontes reais, três temas, geometria e capturas.");
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
