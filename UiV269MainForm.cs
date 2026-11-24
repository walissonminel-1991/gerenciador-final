using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private readonly LiveHardwareMonitor _v269Monitor = new();
    private CancellationTokenSource? _v269HardwareQuery;
    private bool _v269HardwareLoaded;
    private readonly Label _v269HardwareStatus = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private V269HomeDashboard? _v269Home;
    private string? _v269HardwareDetails;
    private readonly DarkScrollableFlow _v269StartupCards = new() { Dock = DockStyle.Fill };
    private readonly Label _v269StartupTitle = new() { Dock = DockStyle.Fill, AutoEllipsis = true,
        Text = "Consulte os itens de inicialização do Windows." };
    private V269AppearanceOptions _v269Appearance = V269AppearanceOptions.Default;
    private bool _v269Confirming;
    private readonly ConditionalWeakTable<Control, LegacyColors> _v269Colors = new();

    private sealed record LegacyColors(Color Background, Color Foreground);
    private V269Palette V269Palette => V269VisualTokens.ForTheme(_v269Appearance.Theme);
    private bool CanAnimateV269 => _v269Appearance.CanAnimate(!SystemInformation.UIEffectsEnabled ||
        !SystemInformation.IsMenuAnimationEnabled);

    private Control BuildV269Home()
    {
        var refresh = CreateSecondaryButton("Consultar hardware");
        refresh.Name = "V269ReadHardware";
        refresh.Click += async (_, _) =>
        {
            if (_v269HardwareQuery is { IsCancellationRequested: false } active) active.Cancel();
            else await RefreshV269HardwareAsync();
        };
        var details = CreateSecondaryButton("Leituras detalhadas");
        details.Name = "V269HardwareDetails";
        details.Enabled = false;
        details.Click += (_, _) =>
        {
            if (_v269HardwareDetails is null || _busy || _v269Confirming ||
                _nav.RightPane.Current?.Kind is V269PanelKind.Progress or V269PanelKind.Result) return;
            PresentV269Details("Leituras detalhadas do hardware", _v269HardwareDetails);
            if (!_nav.RightPane.Visible)
                _nav.Controls.Find("V269PaneToggle", true).OfType<Button>().Single().PerformClick();
        };
        _v269HardwareStatus.Text = "Aguardando consulta local.";
        _v269HardwareStatus.TextChanged += (_, _) =>
        {
            refresh.Text = _v269HardwareQuery is { IsCancellationRequested: false }
                ? "Cancelar consulta" : "Consultar hardware";
            details.Enabled = _v269HardwareDetails is not null;
        };
        _v269Home = new V269HomeDashboard(refresh, details, _v269HardwareStatus);
        _v269Home.SetModels(V269HomeModels(null));
        _v269Home.ApplyAppearance(_v269Appearance);
        return _v269Home;
    }

    private async Task RefreshV269HardwareAsync()
    {
        if (_v269HardwareQuery is not null || IsDisposed || Disposing) return;
        using var request = new CancellationTokenSource();
        _v269HardwareQuery = request;
        _v269HardwareStatus.Text = "Consultando sensores e inventário locais…";
        try
        {
            _ = await Task.Run(() => _v269Monitor.Capture(), request.Token);
            await Task.Delay(250, request.Token);
            var snapshot = await StaWorker.Run(() =>
                HardwareSensorPortfolioReader.CaptureOnDemand(_v269Monitor, request.Token));
            if (request.IsCancellationRequested || IsDisposed || Disposing) return;
            _v269Home?.SetModels(V269HomeModels(snapshot));
            _v269HardwareDetails = string.Join(Environment.NewLine + Environment.NewLine,
                V269HardwareCards(snapshot).Select(x => x.Title + Environment.NewLine + x.Body));
            _v269HardwareLoaded = true;
            _v269HardwareQuery = null;
            _v269HardwareStatus.Text = "Consultado às " + snapshot.CapturedAtUtc.ToLocalTime().ToString("HH:mm:ss");
            PresentV269Details("Fontes do monitoramento", HardwareMonitoringDiagnosticReport.Format(snapshot));
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            _v269HardwareQuery = null;
            if (!IsDisposed && !Disposing) _v269HardwareStatus.Text = "Consulta cancelada.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _v269HardwareQuery = null;
            if (!IsDisposed && !Disposing)
            {
                _v269HardwareStatus.Text = "Consulta de hardware inconclusiva.";
                Log("Consulta de hardware: " + ex.GetType().Name);
            }
        }
        finally { _v269HardwareQuery = null; }
    }

    private static IEnumerable<(string Title, string Body)> V269HardwareCards(HardwareSensorPortfolio snapshot)
    {
        var counters = snapshot.WindowsCounters;
        yield return ("Processador", $"Uso: {Measure(counters.CpuUsagePercent, x => $"{x:0.#}%")}\n" +
            $"Frequência: {Measure(counters.CpuFrequencyMhz, x => $"{x:0} MHz")}\n" +
            $"Tempo ligado: {Measure(counters.SystemUptime, x => $"{(int)x.TotalDays}d {x:hh\\:mm}")}\n" +
            "Temperatura, potência e ventoinha: indisponíveis");
        yield return ("Memória RAM", counters.Memory is { TotalBytes: > 0 } memory
            ? $"Em uso: {Gib(memory.UsedBytes)} de {Gib(memory.TotalBytes)} ({memory.UsedPercent:0.#}%)\n" +
                $"Disponível: {Gib(memory.AvailableBytes)}"
            : "Memória física indisponível.");

        foreach (var gpu in snapshot.NvidiaGpus.Devices)
            yield return (gpu.Name + " · NVML", $"Uso: {Measure(gpu.UsagePercent, x => $"{x:0.#}%")}\n" +
                $"Temperatura: {Measure(gpu.TemperatureC, x => $"{x:0.#} °C")}\n" +
                $"Potência: {Measure(gpu.PowerWatts, x => $"{x:0.#} W")}\n" +
                $"Ventoinha: {Measure(gpu.FanPercent, x => $"{x:0.#}%")}\n" +
                $"VRAM usada: {Measure(gpu.VramUsedBytes, Gib)}");

        var adapters = snapshot.MatchedGpuDevices;
        foreach (var gpu in adapters)
        {
            if (gpu.DxgiDescription is null) continue;
            yield return (gpu.DxgiDescription + " · WDDM",
                $"Motor mais ocupado: {Measure(gpu.Counters.BusiestEnginePercent, x => $"{x:0.#}%")}\n" +
                $"Memória dedicada usada: {Measure(gpu.Counters.DedicatedUsedBytes, Gib)}\n" +
                $"Memória compartilhada usada: {Measure(gpu.Counters.SharedUsedBytes, Gib)}");
        }

        var dxgi = snapshot.DxgiAdapters?.Adapters ?? [];
        foreach (var gpu in snapshot.D3dKmtGpuSensors?.Adapters ?? [])
        {
            var matching = dxgi.Where(adapter => !adapter.IsSoftware
                && adapter.LuidHigh == gpu.Key.LuidHigh && adapter.LuidLow == gpu.Key.LuidLow).ToArray();
            if (matching.Length != 1) continue;
            bool singlePhysical = snapshot.D3dKmtGpuSensors!.Adapters.Count(adapter =>
                adapter.Key.LuidHigh == gpu.Key.LuidHigh && adapter.Key.LuidLow == gpu.Key.LuidLow) == 1;
            string name = singlePhysical ? matching[0].Name : "Adaptador físico " + gpu.Key.Physical;
            yield return (name + " · D3DKMT",
                $"Temperatura: {Measure(gpu.TemperatureC, x => $"{x:0.#} °C")}\n" +
                $"Ventoinha: {Measure(gpu.FanRpm, x => $"{x:0} RPM")}\n" +
                $"Potência relativa: {Measure(gpu.RelativePowerPercent, x => $"{x:0.#}%")}\n" +
                $"Memória: {Measure(gpu.MemoryFrequencyMhz, x => $"{x:0.#} MHz")}");
        }
        if (snapshot.NvidiaGpus.Devices.Count == 0 && !adapters.Any(gpu => gpu.DxgiDescription is not null))
            yield return ("Placas de vídeo", "Leituras com identidade confirmada indisponíveis.");

        foreach (var disk in snapshot.Storage.Disks)
            yield return (disk.Name, $"Temperatura: {Measure(disk.TemperatureC, x => $"{x:0.#} °C")}\n" +
                $"Desgaste informado: {Measure(disk.WearPercent, x => $"{x:0.#}%")}\n" +
                $"Tempo em funcionamento: {Measure(disk.PowerOnHours, x => $"{x:N0} h")}");
        if (snapshot.Storage.Disks.Count == 0)
            yield return ("Armazenamento", "Leituras de confiabilidade indisponíveis.");

        var platform = snapshot.PlatformHardware;
        yield return ("Plataforma e inventário",
            $"Inventário PnP: {snapshot.DeviceInventory.DeviceCount} dispositivos ({State(snapshot.DeviceInventory.State)})\n" +
            $"Placa-mãe: {string.Join(", ", platform?.Motherboards.Select(board =>
                (board.Manufacturer + " " + board.Product).Trim()).Where(name => name.Length != 0)
                .DefaultIfEmpty("indisponível") ?? ["indisponível"])}\n" +
            "Sensores de chipset e VRM: indisponíveis.");
        if (platform is not null)
        {
            foreach (var cpu in platform.CpuSockets)
                yield return (cpu.ReportedName ?? "Processador · WMI",
                    $"Núcleos físicos: {cpu.PhysicalCores?.ToString() ?? "indisponível"}\n" +
                    $"Processadores lógicos: {cpu.LogicalProcessors?.ToString() ?? "indisponível"}\n" +
                    $"Frequência reportada: {Measure(cpu.ReportedCurrentMhz, x => $"{x:0} MHz")}");
            foreach (var ram in platform.RamModules)
                yield return ("Módulo RAM · " + ram.DeviceLocator,
                    $"Capacidade: {Measure(ram.CapacityBytes, Gib)}\n" +
                    $"Clock configurado: {Measure(ram.ConfiguredMhz, x => $"{x:0} MHz")}\n" +
                    $"Tensão configurada (SMBIOS): {Measure(ram.ConfiguredMilliVolts, x => $"{x:0} mV")}");
        }
        foreach (var zone in snapshot.AcpiPlatform.Zones)
            yield return ("Zona térmica ACPI · plataforma",
                $"Temperatura: {Measure(zone.TemperatureC, x => $"{x:0.#} °C")}");
    }

    private static string State(HardwareDiscoveryState state) => state switch
    {
        HardwareDiscoveryState.Complete => "completo",
        HardwareDiscoveryState.Partial => "parcial",
        _ => "indisponível"
    };

    private static string Gib(ulong value) => $"{value / 1073741824d:0.##} GiB";
    private static string Measure<T>(MonitorReading<T> reading, Func<T, string> format) where T : struct =>
        reading.Availability == MonitorAvailability.Available && reading.Value is { } value
            ? format(value)
            : reading.Availability == MonitorAvailability.WarmingUp ? "medindo" : "indisponível";

    // A referência fixa a apresentação, não o modelo de hardware ilustrado.
    // Fontes independentes não são unificadas por nome ou posição.
    internal static IReadOnlyList<V269HomeModel> V269HomeModels(HardwareSensorPortfolio? snapshot)
    {
        const string nd = "N/D";
        static V269HomeRow R(string label, string? value) =>
            new(label, string.IsNullOrWhiteSpace(value) || value == "indisponível" ? "N/D" : value);
        static string Join(IEnumerable<string?> values) => string.Join(" / ",
            values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal));
        static string M<T>(MonitorReading<T>? reading, Func<T, string> format) where T : struct =>
            reading is null ? nd : Measure(reading, format);
        static string Freq(double x) => $"{x:0.#} MHz";
        static string Percent(double x) => $"{x:0.#}%";
        static string Temp(double x) => $"{x:0.#} °C";
        static string Rpm(double x) => $"{x:0} RPM";
        var platform = snapshot?.PlatformHardware;
        var sample = snapshot?.WindowsCounters;
        var cpu = platform?.CpuSockets ?? [];
        var ram = platform?.RamModules ?? [];
        var boards = platform?.Motherboards ?? [];
        var dxgi = snapshot?.DxgiAdapters?.Adapters.Where(x => !x.IsSoftware).ToArray() ?? [];
        var nvml = snapshot?.NvidiaGpus.Devices ?? [];
        string cpuName = Join(cpu.Select(c => c.ReportedName));
        string coreCounts = cpu.Count > 0 && platform?.CpuSocketsState == HardwareDiscoveryState.Complete &&
            cpu.All(x => x.PhysicalCores.HasValue && x.LogicalProcessors.HasValue)
            ? $"{cpu.Sum(x => (long)x.PhysicalCores!.Value)} / {cpu.Sum(x => (long)x.LogicalProcessors!.Value)}" : nd;
        var cpuRows = new V269HomeRow[] { R("Arquitetura", nd), R("Núcleos / Threads", coreCounts),
            R("Frequência base", nd), R("Frequência turbo", nd), R("Cache L3", nd),
            R("Frequência atual", M(sample?.CpuFrequencyMhz, Freq)) };
        var cpuSensors = new V269HomeRow[] { R("Temperatura", M(sample?.Sensors.CpuTemperatureC, Temp)),
            R("Uso", M(sample?.CpuUsagePercent, Percent)),
            R("Consumo", M(sample?.Sensors.CpuPowerWatts, x => $"{x:0.#} W")),
            R("Ventoinha (CPU)", M(sample?.Sensors.CpuFanRpm, Rpm)) };

        var graphics = new List<V269HomeRow> {
            R("Memória dedicada (DXGI)", Join(dxgi.Select(x => x.Name + ": " + Gib(x.DedicatedVideoMemory)))),
            R("Barramento", nd), R("Resizable BAR", nd), R("Driver", nd), R("Arquitetura", nd) };
        var gpuSensors = new List<V269HomeRow>();
        if (nvml.Count == 0)
        {
            gpuSensors.AddRange([R("Temperatura", nd), R("Uso", nd),
                R("VRAM usada", nd), R("Ventoinha (GPU)", nd)]);
        }
        foreach (var gpu in nvml)
        {
            // NVML é uma fonte independente; percentual não vira RPM.
            gpuSensors.Add(R("Fonte NVML", gpu.Name));
            gpuSensors.Add(R("Temperatura", M(gpu.TemperatureC, Temp)));
            gpuSensors.Add(R("Uso", M(gpu.UsagePercent, Percent)));
            gpuSensors.Add(R("VRAM usada", M(gpu.VramUsedBytes, Gib)));
            gpuSensors.Add(R("Ventoinha (%)", M(gpu.FanPercent, Percent)));
            gpuSensors.Add(R("Consumo", M(gpu.PowerWatts, x => $"{x:0.#} W")));
        }
        foreach (var gpu in snapshot?.MatchedGpuDevices ?? [])
        {
            if (gpu.DxgiDescription is null) continue;
            gpuSensors.Add(R("Fonte WDDM", gpu.DxgiDescription));
            gpuSensors.Add(R("Motor mais ocupado", M(gpu.Counters.BusiestEnginePercent, Percent)));
            gpuSensors.Add(R("VRAM usada", M(gpu.Counters.DedicatedUsedBytes, Gib)));
        }
        foreach (var gpu in snapshot?.D3dKmtGpuSensors?.Adapters ?? [])
        {
            var match = dxgi.Where(x => x.LuidHigh == gpu.Key.LuidHigh &&
                x.LuidLow == gpu.Key.LuidLow).ToArray();
            if (match.Length != 1) continue;
            gpuSensors.Add(R("Fonte D3DKMT", match[0].Name + " · físico " + gpu.Key.Physical));
            gpuSensors.Add(R("Temperatura", M(gpu.TemperatureC, Temp)));
            gpuSensors.Add(R("Ventoinha (GPU)", M(gpu.FanRpm, Rpm)));
        }
        string gpuName = Join(dxgi.Select(x => x.Name).Concat(nvml.Select(x => x.Name)));
        if (string.IsNullOrEmpty(gpuName))
            gpuName = Join(snapshot?.DeviceInventory.Devices
                .Where(x => x.Kind == HardwareDeviceKind.Graphics).Select(x => x.Name) ?? []);
        static string MemoryType(uint? type) => type switch {
            20 => "DDR", 21 => "DDR2", 24 => "DDR3", 26 => "DDR4", 34 => "DDR5",
            30 => "LPDDR4", 35 => "LPDDR5", _ => nd };
        var mem = sample?.Memory;
        var ramRows = new V269HomeRow[] {
            R("Capacidade", mem is { TotalBytes: > 0 } ? Gib(mem.TotalBytes) : nd),
            R("Tipo", Join(ram.Select(x => MemoryType(x.SmbiosMemoryType)))),
            R("Frequência", Join(ram.Select(x => M(x.ConfiguredMhz, Freq)))),
            R("Canais", nd), R("Latência", nd) };
        var ramSensors = new V269HomeRow[] {
            R("Uso atual", mem is { TotalBytes: > 0 } ? Gib(mem.UsedBytes) : nd),
            R("Disponível", mem is { TotalBytes: > 0 } ? Gib(mem.AvailableBytes) : nd) };
        var boardRows = new V269HomeRow[] { R("Fabricante", Join(boards.Select(x => x.Manufacturer))),
            R("Modelo", Join(boards.Select(x => x.Product))), R("Chipset", nd),
            R("BIOS", nd), R("Soquete", nd) };
        var boardSensors = new V269HomeRow[] {
            R("Temp. chipset", M(sample?.Sensors.ChipsetTemperatureC, Temp)),
            R("Temp. VRM", M(sample?.Sensors.VrmTemperatureC, Temp)) };

        // A versão/edição vem do Windows local. EditionID não é inferido do
        // nome do processador nem do número de build do sistema.
        string version = nd, build = nd, architecture = nd, secureBoot = nd;
        if (snapshot is not null)
        {
            var os = Environment.OSVersion.Version;
            build = os.Build.ToString();
            architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
            version = Environment.OSVersion.VersionString;
            try
            {
                using var osKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string edition = osKey?.GetValue("EditionID") as string ?? "";
                string display = osKey?.GetValue("DisplayVersion") as string ?? "";
                version = Join(new[] { version, edition, display });
                if (osKey?.GetValue("UBR") is int ubr && ubr >= 0) build += "." + ubr;
                using var bootKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
                secureBoot = bootKey?.GetValue("UEFISecureBootEnabled") switch
                { 1 => "Ativa", 0 => "Inativa", _ => nd };
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or
                UnauthorizedAccessException or IOException) { }
        }
        var windowsRows = new V269HomeRow[] { R("Versão", version), R("Compilação", build),
            R("Arquitetura", architecture),
            R("Tempo de atividade", M(sample?.SystemUptime, x => $"{(int)x.TotalDays}d {x.Hours}h {x.Minutes}min")) };
        var windowsSensors = new V269HomeRow[] { R("Inicialização segura", secureBoot),
            R("TPM", nd), R("Virtualização", nd) };
        return [
            new("cpu", "Processador (CPU)", R("", cpuName).Value, cpuRows, cpuSensors),
            new("gpu", "Placa de vídeo (GPU)", R("", gpuName).Value, graphics, gpuSensors),
            new("ram", "Memória (RAM)", "", ramRows, ramSensors),
            new("motherboard", "Placa-mãe / Chipset", "", boardRows, boardSensors),
            new("windows", "Windows", "", windowsRows, windowsSensors)
        ];
    }

    private Control BuildV269StartupPage()
    {
        var page = new TableLayoutPanel { Name = "V269StartupPage", ColumnCount = 1, RowCount = 3 };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var refresh = CreateSecondaryButton("Consultar inicialização");
        refresh.Dock = DockStyle.Fill;
        refresh.Click += async (_, _) => await RefreshStartupEntriesAsync();
        page.Controls.Add(refresh, 0, 0);
        page.Controls.Add(_v269StartupTitle, 0, 1);
        page.Controls.Add(_v269StartupCards, 0, 2);
        _v269StartupCards.Content.SizeChanged += (_, _) => ResizeV269StartupCards();
        return page;
    }

    private void ResizeV269StartupCards() => SizeV269Cards(_v269StartupCards);

    private static void SizeV269Cards(DarkScrollableFlow host)
    {
        var flow = host.Content;
        int space = flow.ClientSize.Width - flow.Padding.Horizontal;
        if (space <= 0 || host.IsDisposed) return;
        int columns = StartupCardLayout.ColumnsForSpace(space, flow.DeviceDpi);
        int width = StartupCardLayout.CardWidth(space, columns, flow.DeviceDpi);
        flow.SuspendLayout();
        try
        {
            foreach (Control card in flow.Controls)
                if (card.Width != width) card.Width = width;
        }
        finally { flow.ResumeLayout(true); }
    }

    private Control BuildV269Settings(bool loadPreferences)
    {
        var view = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        view.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        view.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        view.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var appearance = new V269AppearancePanel { Dock = DockStyle.Fill };
        var binder = new V269AppearanceBinder(_nav, appearance);
        view.Disposed += (_, _) => binder.Dispose();
        if (loadPreferences) _v269Appearance = ReadV269Appearance();
        binder.SetOptions(_v269Appearance);
        _programList.V269Options().AnimateButtonFeedback = CanAnimateV269;
        appearance.AppearanceChanged += (_, _) =>
        {
            _v269Appearance = appearance.GetOptions().Normalize();
            _programList.V269Options().AnimateButtonFeedback = CanAnimateV269;
            ApplyV269ContentAppearance(this);
            if (_progress.Style == ProgressBarStyle.Marquee && !CanAnimateV269)
            {
                _progress.MarqueeAnimationSpeed = 0;
                _progress.Style = ProgressBarStyle.Blocks;
            }
        };
        var save = CreateSecondaryButton("Salvar preferências de aparência");
        save.Dock = DockStyle.Fill;
        save.Click += async (_, _) =>
        {
            save.Enabled = false;
            var selected = _v269Appearance;
            try
            {
                await Task.Run(() => WriteV269Appearance(selected));
                if (!IsDisposed) PresentV269Details("Aparência", "Preferências de aparência salvas.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                if (!IsDisposed) PresentV269Details("Aparência", "O Windows não permitiu salvar as preferências.");
            }
            finally { if (!save.IsDisposed) save.Enabled = true; }
        };
        var privacy = CreateSecondaryButton("Privacidade e diagnóstico");
        privacy.Dock = DockStyle.Fill;
        privacy.Click += (_, _) => OfferAnonymousDiagnosticConsent();
        view.Controls.Add(appearance, 0, 0);
        view.Controls.Add(save, 0, 1);
        view.Controls.Add(privacy, 0, 2);
        return view;
    }

    private static string V269AppearancePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GerenciadorFinal", "aparencia-v269.json");

    private static V269AppearanceOptions ReadV269Appearance()
    {
        try
        {
            using var stream = File.OpenRead(V269AppearancePath);
            if (stream.Length <= 4096)
                return JsonSerializer.Deserialize<V269AppearanceOptions>(stream).Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException) { }
        return V269AppearanceOptions.Default;
    }

    private static void WriteV269Appearance(V269AppearanceOptions value)
    {
        string path = V269AppearancePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private void ApplyV269ContentAppearance(Control root)
    {
        if (root == _nav)
        {
            foreach (var page in Enum.GetValues<V269Page>())
                if (_nav.PageContent(page) is { } view) ApplyV269ContentAppearance(view);
            if (_log.Parent is { } logHost) ApplyV269ContentAppearance(logHost);
            return;
        }
        if (root is V269HomeDashboard home)
        {
            home.ApplyAppearance(_v269Appearance);
            return;
        }
        if (root is V269AppearancePanel) return;
        if (!_v269Colors.TryGetValue(root, out var original))
        {
            original = new(root.BackColor, root.ForeColor);
            _v269Colors.Add(root, original);
            root.ControlAdded += (_, e) => { if (e.Control is { } child) ApplyV269ContentAppearance(child); };
        }

        var palette = V269Palette;
        if (root is DarkDriverList driverList) driverList.Theme = _v269Appearance.Theme;
        if (root is DarkProgramList or DarkDriverList)
        {
            root.BackColor = palette.Content;
            root.ForeColor = palette.Text;
        }
        else if (root is ModernButton)
        {
            if (original.Background == Color.FromArgb(43, 56, 70) || original.Background == AppServices.Gray)
            {
                root.BackColor = palette.Card;
                root.ForeColor = palette.Text;
            }
        }
        else
        {
            if (original.Background == AppServices.Background ||
                original.Background == Color.FromArgb(17, 24, 32) ||
                original.Background == Color.FromArgb(37, 48, 61))
                root.BackColor = palette.Content;
            else if (original.Background == Color.FromArgb(61, 73, 86))
                root.BackColor = palette.Border;
            else if (original.Background == PanelBackground || original.Background == Color.FromArgb(38, 50, 63))
                root.BackColor = palette.Card;
            if (original.Foreground == Ink) root.ForeColor = palette.Text;
            else if (original.Foreground == Muted) root.ForeColor = palette.SecondaryText;
        }
        foreach (Control child in root.Controls) ApplyV269ContentAppearance(child);
        root.Invalidate();
    }

    private void PresentV269History()
    {
        if (_busy || _v269Confirming) return;
        _nav.PresentPanel(new(V269PanelKind.History, "Histórico local",
            _reliability.Report(_installed, _inventoryIncomplete)));
    }

    private void PresentV269Details(string title, string description)
    {
        if (!_busy && _nav.RightPane.Current?.Kind is not (V269PanelKind.Progress or V269PanelKind.Result))
            _nav.PresentPanel(new(V269PanelKind.Details, title, description));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && _v269Confirming)
        {
            _nav.RightPane.ResolveConfirmation(false);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task<bool> ConfirmV269Async(string title, string description, CancellationToken token = default)
    {
        if (_v269Confirming || IsDisposed || Disposing || !Visible || IsInTray || _betaClose.IsClosing) return false;
        Control? previouslyFocused = ActiveControl;
        _v269Confirming = true;
        _nav.SetPageInteraction(false);
        try
        {
            bool accepted = await _nav.RightPane.ConfirmAsync(title, description, token);
            return accepted && !IsDisposed && !Disposing && !_betaClose.IsClosing && !token.IsCancellationRequested;
        }
        finally
        {
            _v269Confirming = false;
            if (!IsDisposed && !Disposing) _nav.SetPageInteraction(true);
            if (previouslyFocused is { IsDisposed: false, CanFocus: true } && !IsInTray)
                previouslyFocused.Focus();
        }
    }
}


internal sealed class V269ProgramListOptions
{
    internal bool AnimateButtonFeedback = true;
}

internal static class V269ProgramListActions
{
    private static readonly ConditionalWeakTable<DarkProgramList, V269ProgramListOptions> Options = new();

    internal static V269ProgramListOptions V269Options(this DarkProgramList list) =>
        Options.GetValue(list, static _ => new());

    internal static bool AllowsUpdate(this DarkProgramList list, ProgramEntry item) =>
        list.ActionsEnabled && !list.MultiSelectMode &&
        list.UninstallOperationFor?.Invoke(item)?.PreventRetry != true &&
        list.OperationFor?.Invoke(item)?.PreventRetry != true &&
        VerifiedUpdaters.GetPrimaryAction(item) is "update" or "terminal";

    internal static bool AllowsRemoval(this DarkProgramList list, ProgramEntry item) =>
        list.ActionsEnabled && !list.MultiSelectMode &&
        list.UninstallOperationFor?.Invoke(item)?.PreventRetry != true &&
        list.OperationFor?.Invoke(item)?.Active != true &&
        (item.UninstallAction == "msix-uninstall" ||
            item.UninstallAction == "uninstall" && BundlewareReview.CanLaunchIndividual(item));
}

// UI nativa, sem WebView, sem fontes externas e sem consultas de manutenção.
internal sealed record V269HomeRow(string Label, string Value);
internal sealed record V269HomeModel(string Key, string Title, string Name,
    IReadOnlyList<V269HomeRow> Specs, IReadOnlyList<V269HomeRow> Sensors);

internal sealed class V269HomeDashboard : Panel
{
    private readonly V269HomeCard[] _cards;
    private readonly FlowLayoutPanel _footer;
    private readonly Label _notice;
    private readonly Label _status;
    private V269AppearanceOptions _appearance = V269AppearanceOptions.Default;
    private bool _arranging;
    internal IReadOnlyList<V269HomeCard> Cards => _cards;

    internal V269HomeDashboard(Button refresh, Button details, Label status)
    {
        Name = "V269HardwarePage";
        Dock = DockStyle.Fill;
        AutoScroll = true;
        _status = status;
        _cards = new[] { "cpu", "gpu", "ram", "motherboard", "windows" }
            .Select(key => new V269HomeCard(key)).ToArray();
        _notice = new Label { Name = "V269HomeNotice", AutoSize = true,
            Text = "Imagens ilustrativas. N/D = informação ou sensor indisponível." };
        _footer = new FlowLayoutPanel { Name = "V269HomeFooter", WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        foreach (Button button in new[] { refresh, details })
        {
            button.Dock = DockStyle.None;
            button.AutoSize = true;
            button.MinimumSize = new Size(115, 28);
            _footer.Controls.Add(button);
            // O foco precisa deslocar o viewport externo, não apenas o
            // FlowLayoutPanel filho. Sem isso SVGA recorta a ação.
            button.Enter += (_, _) => RevealAction(button);
            button.GotFocus += (_, _) => RevealAction(button);
        }
        _status.Dock = DockStyle.None;
        _status.AutoSize = true;
        _footer.Controls.Add(_status);
        Controls.AddRange(_cards);
        Controls.Add(_notice);
        Controls.Add(_footer);
    }

    internal void SetModels(IReadOnlyList<V269HomeModel> models)
    {
        if (models.Count != 5 || !_cards.Select(x => x.Key).SequenceEqual(models.Select(x => x.Key)))
            throw new ArgumentException("Cinco cartões do HTML, em ordem.");
        for (int i = 0; i < _cards.Length; i++) _cards[i].SetModel(models[i]);
        Arrange();
    }

    internal void ApplyAppearance(V269AppearanceOptions appearance)
    {
        _appearance = appearance.Normalize();
        var palette = V269VisualTokens.ForTheme(_appearance.Theme);
        BackColor = palette.Content;
        ForeColor = palette.Text;
        _notice.ForeColor = _status.ForeColor = palette.SecondaryText;
        _footer.BackColor = palette.Content;
        foreach (Control child in _footer.Controls)
            if (child is Button) { child.BackColor = palette.Card; child.ForeColor = palette.Text; }
        foreach (var card in _cards) card.ApplyAppearance(_appearance, BackColor);
        Arrange();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        Arrange();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        Arrange();
    }

    private void RevealAction(Control action)
    {
        if (!IsHandleCreated || IsDisposed || Disposing || !action.Visible) return;
        void Reveal()
        {
            if (IsDisposed || Disposing || action.IsDisposed || !action.ContainsFocus) return;
            // Coordenadas no viewport real, incluindo os ancestrais do botão.
            Rectangle bounds = RectangleToClient(action.RectangleToScreen(action.ClientRectangle));
            int top = Math.Max(0, -AutoScrollPosition.Y);
            if (bounds.Bottom > ClientSize.Height)
                top += bounds.Bottom - ClientSize.Height;
            else if (bounds.Top < 0)
                top = Math.Max(0, top + bounds.Top);
            AutoScrollPosition = new Point(0, top);
        }
        // O evento Enter pode anteceder a rolagem automática do WinForms.
        // Conferir novamente após a conclusão da mudança de foco.
        Reveal();
        BeginInvoke((Action)Reveal);
    }

    private void Arrange()
    {
        if (_arranging || _cards is null || _footer is null || ClientSize.Width < 80) return;
        _arranging = true;
        try
        {
            int S(int px) => Math.Max(1, (int)Math.Round(px * _appearance.InterfacePercent / 100d * DeviceDpi / 96d));
            int padding = S(2), gap = S(ClientSize.Height < S(700) ? 7 : 10);
            int width = Math.Max(1, ClientSize.Width - 2 * padding - SystemInformation.VerticalScrollBarWidth);
            bool two = width >= S(640);
            int column = two ? (width - gap) / 2 : width;
            bool compact = width < S(1100);
            int y = padding;
            Point offset = AutoScrollPosition;
            for (int i = 0; i < 4;)
            {
                int count = two ? 2 : 1;
                int rowHeight = 0;
                for (int j = 0; j < count; j++)
                    rowHeight = Math.Max(rowHeight, _cards[i + j].PreferredCardHeight(column, compact));
                for (int j = 0; j < count; j++)
                {
                    var card = _cards[i + j];
                    card.SetCompact(compact);
                    card.SetBounds(offset.X + padding + j * (column + gap), offset.Y + y, column, rowHeight);
                }
                y += rowHeight + gap;
                i += count;
            }
            var windows = _cards[4];
            windows.SetCompact(compact);
            int winHeight = windows.PreferredCardHeight(width, compact);
            windows.SetBounds(offset.X + padding, offset.Y + y, width, winHeight);
            y += winHeight + gap;
            _notice.MaximumSize = new Size(width, 0);
            _notice.Location = new Point(offset.X + padding, offset.Y + y);
            y += _notice.Height + S(4);
            _footer.SetBounds(offset.X + padding, offset.Y + y, width, S(72));
            _status.MaximumSize = new Size(Math.Max(1, width - S(12)), 0);
            var min = new Size(0, y + _footer.Height + padding);
            if (AutoScrollMinSize != min) AutoScrollMinSize = min;
        }
        finally { _arranging = false; }
    }
}

internal sealed class V269HomeCard : Control
{
    internal string Key { get; }
    internal V269HomeModel? Model { get; private set; }
    internal bool ArtReady => _art is not null;
    internal string? ArtError { get; private set; }
    private Bitmap? _art;
    private bool _artRequested, _compact;
    private V269AppearanceOptions _appearance = V269AppearanceOptions.Default;

    internal V269HomeCard(string key)
    {
        Key = key;
        Name = "V269HomeCard_" + key;
        AccessibleRole = AccessibleRole.Grouping;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    internal void SetModel(V269HomeModel model)
    {
        if (model.Key != Key) throw new ArgumentException("Identidade do cartão.");
        Model = model;
        AccessibleName = model.Title;
        AccessibleDescription = string.Join("; ", new[] { model.Name }.Concat(
            model.Specs.Concat(model.Sensors).Select(x => x.Label + ": " + x.Value)));
        Invalidate();
    }

    internal void ApplyAppearance(V269AppearanceOptions appearance, Color background)
    { _appearance = appearance; BackColor = background; Invalidate(); }
    internal void SetCompact(bool compact) { _compact = compact; Invalidate(); }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!_artRequested) { _artRequested = true; LoadArt(); }
    }

    private async void LoadArt()
    {
        try
        {
            var bitmap = await Task.Run(() => V269HomeArt.DecodeAsync(Key));
            if (IsDisposed || Disposing) { bitmap.Dispose(); return; }
            _art = bitmap;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ArtError = ex.GetType().Name;
        }
        if (!IsDisposed && !Disposing) Invalidate();
    }

    private int S(int n) => Math.Max(1, (int)Math.Round(n *
        _appearance.InterfacePercent / 100d * DeviceDpi / 96d));
    private Font TextFont(int px, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", px * _appearance.TextPercent / 100f * DeviceDpi / 96f, style, GraphicsUnit.Pixel);
    private static Color Hex(string s) => ColorTranslator.FromHtml(s);

    internal int PreferredCardHeight(int width, bool compact) => Render(null, width, compact);
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Render(e.Graphics, Width, _compact);
    }

    private int Render(Graphics? g, int width, bool compact)
    {
        if (Model is not { } model || width < 20) return S(150);
        var palette = V269VisualTokens.ForTheme(_appearance.Theme);
        var (card, border, soft, ink, muted, photo) = _appearance.Theme switch
        {
            V269Theme.Dark => (Hex("#303438"), Hex("#565c62"), Hex("#484e54"),
                Hex("#ced4d9"), Hex("#aab4bd"), Hex("#33383d")),
            V269Theme.Claro => (Hex("#eaf4fc"), Hex("#9bbacf"), Hex("#bdd3e2"),
                Hex("#17374c"), Hex("#3f627a"), Hex("#dcecf9")),
            _ => (palette.Card, palette.Border, Hex("#1c354b"), palette.Text,
                palette.SecondaryText, Hex("#102b46"))
        };
        using var title = TextFont(compact ? 14 : 16, FontStyle.Bold);
        using var font = TextFont(compact ? 10 : 11);
        using var bold = TextFont(compact ? 10 : 11, FontStyle.Bold);
        using var nameFont = TextFont(13, FontStyle.Bold);
        int pad = S(compact ? 9 : 11), gap = S(compact ? 8 : 12);
        int heading = S(compact ? 28 : 34);
        int contentWidth = Math.Max(1, width - 2 * pad);
        bool wide = contentWidth >= S(270);
        int imageWidth = Math.Min(S(model.Key == "windows" ? 240 : compact ? 86 :
            model.Key is "ram" or "motherboard" ? 140 : 123), Math.Max(1, contentWidth / 3));
        int imageHeight = model.Key == "windows" ? Math.Min(S(116), Math.Max(S(70), imageWidth / 2)) : imageWidth;
        int remainder = Math.Max(1, contentWidth - imageWidth - gap);
        int specsWidth = wide ? Math.Max(1, (remainder - gap) * 56 / 100) : remainder;
        int sensorWidth = wide ? Math.Max(1, remainder - gap - specsWidth) : contentWidth;
        int bodyY = pad + heading + S(8);
        int dataX = pad + imageWidth + gap;
        int sensorX = wide ? dataX + specsWidth + gap : pad;
        int nameHeight = model.Name.Length == 0 ? 0 : HeightOf(model.Name, nameFont, specsWidth) + S(4);
        int dataHeight = nameHeight + Rows(null, model.Specs, dataX, bodyY + nameHeight, specsWidth);
        int sensorsHeight = Rows(null, model.Sensors, sensorX, bodyY, sensorWidth);
        int sensorY = wide ? bodyY : bodyY + Math.Max(imageHeight, dataHeight) + gap;
        int totalHeight = pad + heading + S(8) + (wide
            ? Math.Max(imageHeight, Math.Max(dataHeight, sensorsHeight))
            : Math.Max(imageHeight, dataHeight) + gap + sensorsHeight) + pad;
        if (g is null) return totalHeight;

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var shape = Rounded(new Rectangle(0, 0, Math.Max(1, width - 1), Math.Max(1, Height - 1)), S(10));
        using var fill = new SolidBrush(card);
        using var outline = new Pen(border);
        using var rule = new Pen(soft);
        g.FillPath(fill, shape);
        g.DrawPath(outline, shape);
        // Os ícones são desenho nativo: chip, monitor e módulo de memória.
        DrawIcon(g, new Rectangle(pad, pad + S(2), S(21), S(21)), model.Key,
            _appearance.Theme == V269Theme.Dark ? Hex("#c6cdd3") : palette.Accent);
        TextRenderer.DrawText(g, model.Title, title,
            new Rectangle(pad + S(30), pad, Math.Max(1, contentWidth - S(30)), heading - S(4)), ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        g.DrawLine(rule, pad, pad + heading, width - pad, pad + heading);
        var picture = new Rectangle(pad, bodyY, imageWidth, imageHeight);
        using (var picShape = Rounded(picture, S(9)))
        using (var picFill = new SolidBrush(photo))
        {
            g.FillPath(picFill, picShape);
            if (_art is not null)
            {
                var saved = g.Save();
                try
                {
                    g.SetClip(picShape);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    float scale = model.Key == "windows"
                        ? Math.Min(picture.Width / (float)_art.Width, picture.Height / (float)_art.Height)
                        : Math.Max(picture.Width / (float)_art.Width, picture.Height / (float)_art.Height);
                    float w = _art.Width * scale, h = _art.Height * scale;
                    g.DrawImage(_art, new RectangleF(picture.X + (picture.Width - w) / 2,
                        picture.Y + (picture.Height - h) / 2, w, h));
                }
                finally { g.Restore(saved); }
            }
            else TextRenderer.DrawText(g, ArtError is null ? "Carregando imagem" : "Imagem indisponível",
                font, picture, muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            g.DrawPath(rule, picShape);
        }
        if (nameHeight > 0) TextRenderer.DrawText(g, model.Name, nameFont,
            new Rectangle(dataX, bodyY, specsWidth, nameHeight), ink, Flags);
        Rows(g, model.Specs, dataX, bodyY + nameHeight, specsWidth);
        if (wide) g.DrawLine(rule, sensorX - gap / 2, bodyY, sensorX - gap / 2,
            bodyY + Math.Max(dataHeight, sensorsHeight));
        Rows(g, model.Sensors, sensorX, sensorY, sensorWidth);
        return totalHeight;

        int Rows(Graphics? target, IReadOnlyList<V269HomeRow> rows, int x, int y, int w)
        {
            int start = y;
            foreach (var row in rows)
            {
                bool stacked = w < S(104);
                int left = stacked ? w : Math.Max(1, (w - S(6)) * 48 / 100);
                int right = stacked ? w : Math.Max(1, w - left - S(6));
                int h1 = HeightOf(row.Label, font, left), h2 = HeightOf(row.Value, bold, right);
                int h = (stacked ? h1 + h2 : Math.Max(h1, h2)) + S(6);
                if (target is not null)
                {
                    TextRenderer.DrawText(target, row.Label, font, new Rectangle(x, y + S(2), left, h1), muted, Flags);
                    TextRenderer.DrawText(target, row.Value, bold,
                        new Rectangle(stacked ? x : x + left + S(6), y + S(2) + (stacked ? h1 : 0), right, h2),
                        ink, Flags | (stacked ? TextFormatFlags.Left : TextFormatFlags.Right));
                    using var line = new Pen(soft);
                    target.DrawLine(line, x, y + h - 1, x + w, y + h - 1);
                }
                y += h;
            }
            return y - start;
        }
    }

    private const TextFormatFlags Flags = TextFormatFlags.WordBreak |
        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl;
    private static int HeightOf(string text, Font font, int width) =>
        Math.Max(font.Height, TextRenderer.MeasureText(text, font,
            new Size(Math.Max(1, width), 10000), Flags).Height);

    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static void DrawIcon(Graphics g, Rectangle r, string key, Color color)
    {
        using var p = new Pen(color, Math.Max(1, r.Width / 12f));
        int x = r.X, y = r.Y, w = r.Width, h = r.Height;
        if (key is "gpu" or "windows")
        {
            g.DrawRectangle(p, x + 1, y + 2, w - 3, h * 2 / 3);
            g.DrawLine(p, x + w / 2, y + h * 2 / 3 + 2, x + w / 2, y + h - 2);
            g.DrawLine(p, x + w / 4, y + h - 2, x + w * 3 / 4, y + h - 2);
        }
        else
        {
            int a = Math.Max(3, w / 5);
            g.DrawRectangle(p, x + a, y + a, w - 2 * a, h - 2 * a);
            for (int i = 1; i <= 3; i++)
            {
                int u = w * i / 4;
                g.DrawLine(p, x + u, y, x + u, y + a);
                g.DrawLine(p, x + u, y + h - a, x + u, y + h);
                if (key != "ram")
                {
                    g.DrawLine(p, x, y + u, x + a, y + u);
                    g.DrawLine(p, x + w - a, y + u, x + w, y + u);
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _art?.Dispose(); _art = null; }
        base.Dispose(disposing);
    }
}

internal static class V269HomeArt
{
    // Bytes originais HARDWARE_ART da PR #109, sem redimensionamento ou
    // recompressão. A decodificação usa a API de imagem nativa do Windows.
    internal static byte[] Original(string key) => Convert.FromBase64String(key switch
    {
        "cpu" => "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",
        "gpu" => "UklGRg4TAABXRUJQVlA4IAITAACQTgCdASqSAIwAPkUejESioaEUSgWgKAREoAzmzHkxa0PePwn8rWON39tz9x3ax8VrqJ/0b/i/4D2M/p7/uf8l70fpM9AD+sf4XrZfQA/XT0zf2j+FX9nv2w9mC6Qfi36B/jPz39y/I/91P8/8ddMh6H/yX7qfb/7h+xX5W9EPx//oPy3/ID7Avxj+S/178kP7b+4nMh7H5iPqt87/wv5df4L0qf6D0Q+r3+z9wD+T/zr/Gfm1/fOhB+5/672A/5d/aP+x/hPyc+mP+g/8v+p9B/5n/ef+5/jfgJ/lf9U/2X9+/d7+////62fat6Qf7gmV5lEcqZSIIn9Oah+sqbaVlq650AaiocP/HH2Mkd3lzVJCUJg4TeEXs03W4W748LYgs6Ts+3HcaWwT02DAYu1meH++KwN43O4WcMwRbMLunkfZL2CnWNc+/ZqjwlWWXNw8hriIRD7QgeLOhcP/lPVYHcRqea4EOWMZJpAfbzOGyADV7nUN2a7b6TzBIZ5WyDjmp43NTWpIwzcS/hi3nXQ5YDGJhTRFC/4srQRVJtlt9MQKzgug7aBLZf/YtNnFY42P6PB0sBjU0UX0XtLoNGbyATv2izyq3JK+bz4qX43PAJSuYauvb+X8u+hkXsGxrvdTKs/ueAFxCPs+4ZJnYVAk73Jgwh5NT+oND45xDGA/oWRx8bYx1S6dgIYkj6r3BLQPm9ywjXQdhGkHw7fkCB4LdiC2KrrWmC+7ZDUlE7iHMssh/dNbsIXMfae6ySFfsnc0Xa49Nu/h/hchS7Ujs+rsZDqD8kD+rOTg23gzujATzwQxvjHPEraMAvtr40d6ZJvjYWFO+btd5Ev+dtkAAP7/fVJV+SpQklr+XzU1LJzH/W/6L/Y/jz/f38yPutLy0Tevw8AZlnJdiVS/drZ5HhHTilpq9lvteIvtubqDRFQm5ED8nueJQ8Y8ov7qaT/LnVhG61Tv0Ayvy8t9knOAswNFWdR78YeVEHy9pMMjf1PCMI/0P1OC/75dH24gMkmEtdxKHPuavEk/F1/HGK2hq+5vQYA6ES+m7nYbPD+ObYL+RH6llkYuhC1BQoYzyXGtO3u7uROk/kvh5WRa6APmqF7X7AQ62rW/FqyQ1wWijLE1Esop8++VJXKWRbdaXxelRJBVpiMzCrhlNIYKDUxEFkJ0eIgsAUaNTD/5ya9My8mv08ZkHTOmyjz+68l/mVgdrfCzMhdwbrGpIRYzlqwAs9V6tmN3yCtEgdvJgwOMqrr4kMVxMO5dC27KojiXD+fwFlPMIzk7uxaE+UaJxHvG7kg9KAoO+0hhZoKZdc/Ze3Am2mLT8Nd3n9KXUL1tBVt0G5ycV4yRpgErjnirEC0X0mjncg/ISrPirRnykcnwt9Ijb2mpD8xI57raD/kLYpJv5a9U8IkPMo9/8hpYA2LOkaV4cASL7QVtcVbqJM1Yg2UtUqjEpusn/Zg6bDsdbDwbykl/l8cM5pzB+ZyrEWhh8My917rZ4kul1Q1PSv88w+b/ezxnUy1sUjM2JZ/S8C6OoZLIQZ1p2qRftCHgU//mYPzMgj99JEX9oqyDM9EZjw7B5ttbYvejmMNQCZ099wDr63B/8Oe3Cfq/7o88dneszmCZIEJziHYUxqonf9pZmBq26NYoJb3SZ9V5FX4kyK25OKqD63b/9CH1fFj/JThlyX5b0pO74s+zvbWd0SyI5NrXAu6CQB4Od0oIMiOci3YSPJ//dTFEktKrQfvuNBL94fOzVJLW1PbPts7AUfUyEJ4uQ4gvIyEQ4pWRnXJuvUqtqFPpLXLCkzjeNMk1KrBjLjZKu1IphUdR70391AmAqYDG/zmGZrz2KoLld1ARNMKLxrD95ntqHIoGDeNVlXquMq3NUWT9AgNGnY6F3+5f+tYPNVuUaXxoboIFOZ2rjAE2pTvFR8IaY+HwoLxgugm1oAIXZOlDowtSlaJpX3WmitmJQjLwanVfnGjMWKYxapEnZUHdb9cKQ3OUOpT1XjqvTjrxJAz8YS7BgipRgKWuZX45fSDXhWpvcPpmDD+9AoInJ57tA/IYPRgCrt8srZUymWY/niyq/rX5cYAAIG6D/xP9uQ7M5zi6PILqBDq1LAsoTIX1dAambT4VRD/VXy2309ZXMNVvLL5VQ6//YFCQgA2V2FLr/zsmIdHnFNNgTGG/mnMicD4UsFgY4AXiTast59XXaFAU72QpBpWRjvkSrJQaI6b/w1POUT0hPXT2kselFma2dwMOL85zfJCYWDtDlXBBnR/4ny1nX1ANSRgvz/PuOYEz3OndhTUpNf3wdsiUZR+xJc19dgo0JiYKYYRmsElzS8GNaveAL1U2WfFg54tMH39aL/I6RJ65WBQ8mtc2PkPEdpCZuRUsMZMLBilc1Vscgv//1/Rt9IVHyTC50Norml3AmwfE/RQVr5KzXmMG138odyEAo3ewyof6EI+Qv26VmtTYGKAsxHol3+Xe7GJQkL3bqujCdPFYQdOt4N2QWc/0FuDsFmh5NGeofZgixaxbmr/ZHqBik35AUVOcOHECwBxVDz/+0blNYyyoL454M7VwxtwgGXER73ppSSo/RK+P1G7c/IT7Dif3AE9hwwrpztjQuZppf75gUvuWrcIhheoB/xhfNAeB2hfqb0qzpUdgI7TScRZ5hkDx+u0gS8RK/o7zMobtr0wJV8sYsQujfWhG8b/+NjuZaykFDiJWx9OyzSfNsMXlRMGxxMHBIUlaj7UPlD9eeog+MffbZjlBDpdLBg8MmUnnrgME0ygg5146A+cuCAbnFGiKO19+LU0LrsOSjdOBlmRGKAniSp9nZgqqbuVnfABt93im9wWCuImH0vp9rnuNh0BDlI05+Mp70pcEck/3I3WcI5nJtdedBaDN/r3bnLs3qkQ1P6uv3E9NJz0GQDmtt9VrUoMYLGcnP4ASdZivglllxePCNmwd+2Z6PGvS5Btw0FHTpvJLiYiDCD80AaYStpF7ZhM6VeaZ+m4V9Jo/rT4nRG/70/hNj7+2XMOwUPTQfumY14AUT+TOyfrt/63Pbs3ss0WQqNgXXKVgWDajjLGQk1YgiNZ2/9BV8hvRJsayP1iprV6nGPDIEbmnhAdnIo/xFS8NNSxePHS32Lpw6tO5IrqjvKi7KiOEWXacO/e2kRGhqfgdbv/yHABkg3EE6x1E65h28nxzSWYfbdBTq7/ylYkFKA66o4jYoSTK9BOxtU05V+rUDdAlgeyhP0iEoYkjvMm2zHunvfVKyJ3hKYL1/fPAnhQQC+BNnMSMB8Svi8IE+EQ+MsDgrIORR0Ua9SqBfmhRgrFKa1YFVhylJfUxGNsWVExIFW0DwbuslHlVfd/7OFrMqh4BIJZDyz0BywyPgkgabx4e6ZtsBugwS7v1ga75pSnUun6Xvwk4nDeRBOgFpaqcNTqFrp9GE+ULX/yQVz8vDuVQwZ8K9GKgvZk2duRMDzhnYnuwiVtFOhzfTZdqhl3JpgU+cPuDyp6KemVv8sb7piTE9P4sk3y7M7DgDpPlKBBSw3HqYZZOYWRqeoRk/xHqlomdevzHb97inyNyMGMwKtFYX6Z8s+YSBFCMDzmlLcYlfc0R+hBvonAx6B2a+a7+evJsW3nMGUGO7Nz/9Zl3s4jhHtjMMqWVV93T6Xph8vx/dbhnxDHLQNoLdlbyWgxoXE+igUDNHV9pkl8Js9272JckB2yqr+Sz8yuj3LnhJ0RuzBcEEX29DS7kUYIFjDQosFYLjK0A/5g8GKuIUICOAyqqGF6+LqZFIRo+Wc5MJt6YF9d9cI0ZSBBTqJzNsde16eTv6I8Khp70O1m+eh5nFRVy2vbKcnkcfIuoEeiLBWLydjf7QE4b6WNpsA/lc70HTL2ErDTBhSEqTZYTCJH+kgtJemWiL1Fe56O8WJN+7khgza6zSIsS1MBl6mgr8NmeERKI+gCv6Kn+dIpy0xT7lZIRd+YaCv7v7NLH12o1TMd+4/HDLE3NgZY113q6E98rO/p+31QcTZ94Uc7suViW++MoKXfbuZeXzsVNoOiBiSemPd39btBWkl/0iV27eMkP1cBILqSQlU+vbLsDe1xESs95wKXdMyvrcTThH32oEjjl/TxQxLe9NZj5f/nODQJhTQ8Yq6uhO+BA5ULKFEs6T6DTyttWeqcjteH5Eoc2p3laWUlaQYzj3MZnkcJkkcd84OToTPjdAnAvKDqMS6jCTAi15mBH7WwnMHTAFkJ4C19GWP8y7tyGPYQxQr+O3e75cnLxsH9//05VU3Y93I30OkRYCsaV7yVPeuPTZl6Yo8LMZAYIfV/uPzhyj4Fk8MkCsLcETSUwREKTJR3yWRbW0VpSs2FxoLoeB4g/vVykPvWDlNCZI23Gc3PfpGiQlw4rP7723/rFkZ7IeY5sApflLRWovSA6/+0n5KU9CvHTXNvGcAH2qD+6nFieit4X+0hLZzPEbEKjEHtyMk4Ns6yyZ5cFtBuN5sOCB8TwSdVmYvNh8lQnW80zmxOrNmES0YSY9qHlTiB2nrPMB7ijYtvvJr481OmXQSym/y/FKWnkj36IZW6tJ9hRShGbF1YA+9AoiE84J3TUaRssWAu4GpZcmsyvkAANfPea/pU7YgbbIfg2wJeTcTn5I41A4JkOb+1vSyob2z7FLgQFZg2H8g4fitHBXJ5kvxJdU64j58vTps2D6Ev6TsMo44tYHjcBwZeO7UQ/wqzWR1+MXIfg+QunLEnr7GnXLpaGFQh9K7V6wZRmWUf6HBjEP3v4bZx37XtdhwSFH8SeyUarGJIRh8/ZkKyXgCcWXEOTkDdMyUuErAswvUbwYeK+9svc50CdhgRDzIGaIvbEt5RgphdefVjqDiyEIviC4jZnAsBSqk8Jb+g2Seis/qGplJd9JCUAzpLQboSUPN5FD0YS301nWX6DfmSa5lK3X5hwBpufJwM6EE4URbD3HdCQIR0blOtZ9tNL1Vd1uHLhxL+o2zKmFjb3+gql1441jZnznv/ymlYw1Guqr3gjEju/SLGqzyxFfPnX+DdoVnBD9l569ZxO8p94VL2NF9yToaNAXnkPO55X646LpNTUWu1ys3gLQ21Tx2eRil7nPef/NHYJ/379/B+WI++rWT6S8iGfPbXn0U+L2Hf/iYz+Trt+p1I2i2y6UfQ8jkwh9Yth3xNaIAlnRXMIf4wELipH+OAIu9OvyZtX3dPTv4NJ4X22umX3XjLiPkif8TesfB2+6y4Sg6g0dNH/Pb3FUr56jGg2hXa2P+EA5Hv8Lj4Iq4MHfUb1rn/5f+08iyCnv+m5037HbY77ZXr68bd7WEG1LAut4Xssi0BhEuCfka0qRXBL/YMXNVL8pKfmoPlfqrJvlp0d54HkKwE/7dXbIAr/Jpvm/fSIerEOtDgfr446i+EtD1zHRGoIy6xRlePBYZXazJ3KFS1H/uMi0Izxmr11XAi0j2v4VF/Avt6USWp+0unSbjLbhAqV87tUNOKSEsl60Hc1jKD8062chuZRY4TpJaQ574hyrdPCDSuiXFFU0UjWJgDYCIdx3TebUgntqBuH4ulGjh64B2Bh5/A1icqgQosgDJar8gLjIyCBe0Mlym/3tYR0xDV+EJ9lRYpIRXiM+uPXorSANVa6G4kZPUDjkGrRc8GJ6T+jM+AuQN2JDxEuoWH5bFsKQc5loTcD/mDSFbETAHOvT8HeHrn+SvwanAupJfI7vert1qpwQAW5gw/TAu2pj3+0aYB/RE6mSTj0twF9r5PJyv+mW0S3K1mZ00AEhuAMMPMdqwqDp5uWifUdpeqT2ofEds9Nks3oDPH+xtPcZlgCBqOuwk2Ie6l73wtYlceUk9MnQD4f+RVSzVbfnOqZ+bq7ATIKYGXCJ8UTATNw5sdzbHK3VCLdCosKHmrgECnfpQ992bK4dCrxlDg9RlWkDdAEUXlOfBr0E+GIjx/IyDR9BxzpYSr/GTfc0yekKqhbF3T+tM3kgEGAYVXgj2MI6RTo+lJnxHahcEW7AMVegySIEZe8LniOU9KoXLV9dGERWZyxXDBH8wC/dp387pAzzUfyBNKWcLSZcvnrXRjWenu/zUz8NZ0KOn1+ol+j2a59V+f/inv4rrfdQ0YchrHrG0fxyeIWTp6wrr4G6H2XuabkcSHfzz7g3nYIpsvlpdB/M6WC5+/INZbVL1dfzsJ2bhpICmheBLm7XlK0+uoReiLLObk2B3b2wik3FkhsLylmhfm0+PlqG4bgAWIlTZvHyJ25IuTzH7WZViTEJfbdzTsz77Vqz72+TWOXmKKBSPzTc5Qq/g6NzKZgTwAbPXNFY5VfZa1yTrgFqTcJhHMJuTDcd5ADmIwyVc51XHdP0DYcy/Xizh+ztIxTrzf6IXLPs9habK5vEIXenS91do0yTQJ5/5f4lMlp1yyokVO+mgX2jLUWdt8OBlur4TG8ptCdHR0SMasfsD5rbJdN3P46J5CNXSohNCc0PYOjNhIHx8UeS2Xg7mKsZiK/CzFRrw3XfYzxWw/5ur7MAAA=",
        "ram" => "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",
        "motherboard" => "UklGRmIlAABXRUJQVlA4IFYlAACQZACdASqcAI0APkEaiUMioaEYDVYIKAQEtgBYkqCofv9+7/lr7BNVfvf9j/XP989znM3175YXTfy3fPD/Zfrv7lf0/7AH6ZfsP7g/SJ+7HqG/bD90/ek9GH+W+275Cv6j/3usm9AD9p/Tq9jX+zf8L9vvgM/Yj/7ewB/+PUA/9nObwW2JvkX03+i/Lj1ysgfY3qL/Nvxd/C/wX40e83fn8Xv9T1CPyX+Z/5/8zvRN28euf6/0CPaH7P/xP8d+6vn0f3no54gH9E/vn/a/ND47/7Xg7UAv1H/4v757rX87/6P9R/svSV+bf3z/wf5z4BP5h/T/9f/cf8l/8f81////d91Hsi/Z3/w+6n+xX/d/P9tzSnm5kjaWHyj1kyD7IscP4k026n1xODAywnrlZnJxU169YR170PZWe2aiIIRvye7nGzp42EAlZqnwkvz6rue+hIjOe32tR0iEVMQg6Dfp1wt4jtKIimCsC65K/VCz7i33OzjDd78XOShTUpTCY7+9JPdAHrLCSld99FY7KxvS00EkHldcPYIuRtEwvAeZ1PHP1zjiBgtYvk90uEYNjbwASv9/ZBFzvIRvFvjzD3MCoE4NqUB2OyRlBOcMEM6SIbEdgeOwlGJONwHgb9CLeHDO1FpAzJSimDV7NhchaEya5QSgwybkGb8JPIeyuSENYYaAD+x18nKiSjZaT2ZupwWclXKz7cQrhPdjHwXsUR68fjvMHIyzWf5m8O5fXLdVHP+ipypC8MAwA6qwkTNVloXM2Rm1HIV+mYmAQKz9k0Dess0zmacyPDTPM0hDnovZJ+zGkO4103/s9sWrbfVBrsSQuIXyg+cOH3yMCFxC1GhLqEzcqzl4iAkyPkyVvmfeoRc6TTaFugG1UN2khK5IXI7cUThsRo16jr5yVPsObGzcL9dRJpP0EwShPSaDEEJvKJw0OZ//7rFC0Ee6TEgisgWco8aaRcP/HQ0k/4ihj5Tz9Jwuj4sYdHSJVv+f9Yc5agewyiD7QQ9cCshsKhZbmjU7fMjqSQA2ck1Fy653EgIssC7M03llQPz5hWtyBl7wRQZ1FR7UM+D3YPE0GMAA/v/5FshXV2ZJUgrzSQX63yKMZWqnpSEj79lbfxRjn+QUDNuKuqMtCSGYdhXtTEJDf/hhyx1C/8jVEc7pr9d9bM3f8ecEULlYTY8aD9JrwdDT0/rCgFdExV6MrXpiEZrPW8TuYkbAnT6b2HInYWA/mmw+1NGpe5hDhlYtmXRzzBZNMBRf0BA4r538TpVDO4mZDTBfqdQvMxl0P+D32LrCVwbMsMMaH8u3+qLLkRNZNRQY7IDoTFQqnDc2TJ2Twa102Mf7xs5c6K4kjYw2NAwpueC8g1DyRk8nITj6Z7MeJbZwmGKPI5ya6t5ftNtufcT3qMYphW6LlnWY3S6BUfO3+SHu3AifN5+bTMY4gH5mcTgGx0aS0XfrvYpZciG9qjGNtXK2ay4kvgJckyhoQxxMUHqCKTMlfSSw6ExP5O/vNoz6o+cAIcG86s70OY0IxrQ3mpKDtBjFj8IWXbE/Nr3nGZ5JQQEvrznXZCSxog6z/AqjxNif3tzmSBLnW5jc0QANh8X7lXFrdLInR/lEde7ZLpC5nepSaiRhk6/gnIXjsyP9QNmAJzYmBNPoYLnq/JCLFWmkpEYAo9szozOvy002gZ+/S75gWxtnA45CG477Nj2LnO3gjdYCPG37VaEdE1E0Q1SDoVnMHPYGkYeSvgAjf2jzXCzTO9PQJafTsbw6hfvSx2eMVgY+txZzC+h5ByPKYQTGAdzUv6LTE9+ctVKEAsCOaZvE04VyCr/nfmiy2I6iBe4DktUqua2yv6nKHLAREXYf1xqMKPju1xfz5JiF8BGq//ZwDkRrB9hcLIFuDs9EDRfKOEjXNnF5NkO/S3FexN4AdHa6CqmGc2zxPdHh6WBzIbuOBhkRRFbZpoQ8dezlx4Z33Dp2W9AsX5kED4mNj45ElW16bVL3YW1wy/t84C++9qeOaV7xjO17xB/Xkhsi/eJ4EsAGUHfc3bPfVR3lTQ4y9O0f9KsLe6JCJUQLpL+TQeLq5kGfvtlNeeGHc+RpM7LIzpnKHfLUVUuC4DsZkUYUL2yx+dWPd2QVh2rOlcJfOuskib+Bk0F/gtf8n1vhW6JRhzzdVv9OjXKDz5cePaq8y1txWFzVxp6819xKHUiJ/Txe2TlGAWhnfml/kbpjnjeippCtwlt0MsDHbjVr5Zy58vf/ekMj5/9x21WxXVHuCbnLrzwmpbj7B7C0pEw2xjhvRvVO7tM3TuePExJwm5wuxPTvbpn7zTg0+uLHUNULsMV9R93uuFjSyMAKFKvXjt2e1HJoawVGq9n3LtPe/iYKbfITluHjeLLFfleoGA4eYDoNry+mb232Dx3bKNKqHBJBuK3RbkAbfh3UCQbLvdkdZkp5e+lh/qenu2oxPXxC8DegMUpJU/ftfttkROyGjRElOL6oMx64VjIu/r305U4N9sF/qYX4cyQqDgZ/NJIimWEy5opNbpJS5Zsyn+j5dN10yamCK4VtXQBsjGSlBJBC1SNSNPR/e/IuIdf0Eti9JbgmkHNuYQWco1SMikN4qeXAgb/HAdqZ2tQ6Q61NEX6ZgcLwH2zU83/1Z3iJycpFnyVguAYMyb+wO8gTexIae1O3DvQzYIIuq1IbAp9Oa+yLYw9eqFVRFt9KdCJExj2nFnZADO2/9h6s7T2DzHHxa3F4AxpgAq7Dirof2vxzHMF/Qmpt8KCdOF3VKhqy1+fiX4w23BVGYxEPUVXD3NXaH3l9tFiXgRhdzOU5g2NURTPwp5Oc4sRjjEJmGyM7Xmq+91fRaNArG1IeZAJmxdXnLTPvKKY4+1egHZUupHS2JwyHys8gDmdHrZbqQtFqH3wTycQiyiwVG8tnh+xwnLRj/BJZpE0uUine4tR/0omHdn8kn2bQRpeOwpNP+NUaT/wkkcp4boHES9erxSp+L6UC8Iuxl8oqw0fR6Oyh37uIZ8EeATtxJFXkGGSncqehVqfuY9kGE/LtVhYxxHXVhZxZi7PGuACTs6w9OuydyUfA9FOK0vRlXdBcZh7pWTeYcVdhkLazhZNMd2oPjAC3RA1m2wO/igSlCnZRxtU5pN7ZK06zzVp83q+BoGUVQwTDNK5F5Xt7d9jxY3Vyf2FUUtDoHElOA4UyP/fqfEJBnC+0QJJdcIU3KxsD3nEGBwU8ozjGpVnLQ8rqpzS2lUJt84mT2j1iDIY9kCwCzPM88sHDL7v4odvmE2TVy2aShzweZ+WmmB7Ry/yvuGxP7tyKAlf7jY9rYOhneiTWojirQId6UDqaFkusU4AQWsHwfG14N/zoNvAGM2Iq8D36yRbgi9EXHmtBIF/+j/TxQDzxRGFonpXeYFDozWiD+m56UlrCAhi7UeX4kfmE63UvxkGbW/OM2FZ8UIwfyCO3B8lI1Ggqld2Idm/oN6uauGkgkV2vSOf2sKVRZ+PMgbMRUlp+pYgW4l2dhPnPAiKQKDCfbJcOPpEO+HoKn0fa/YQ3AKEtkIUq8A8Bducm6dLsMmTGURLwhfhi2fLg180iQM2x00QPJWjbdZt/5O3P/DclKX7vlqypVBqbEqvriWv6y+sxgW8fSibxv8SoymPxX/W2eeLfTjHvjlV1UcqqL1SmABGwouKOymxl0Vh2oqo2tw81ZCfOtYT2AA6kD3l/ZwsSj9tHF/inhhOn2jSsIJXlZRm1VoeeK211Q7qeJExMUb6v/WbE1/3xg4Pvg8IFgZKMMA3CHb/1SujprCzuVQw2amxFm4HcIH4qPlr/qQPl8s+3S+zy+/dXDkRykLsUNnapAowZrmyD01ertD9i43SIO7TD36h9bZuE9Iy/tMNiR1sIngy6Cdzqc9T9uOmpodNJFb9tm3qyhP7XSKiYauRcKfR3fiyV2mHTo3BI+hIh2Ri+DCTD82VtC5qHeHP7s3aRychHdjoU3TDVjIpFpIajy3rGk3/ADzX9H+6NGcS6yk8hnJHsnxsTl1/yWwTMGsfPsiWNiwmavlY2QEMR6V2SEDrSCsPWzYpUX6JqWqRxt6hlZWRGFXJSi+sPuVRLjkrwLdU3IC3X+vVQPTX1HiyP6IEKk8cpM3glCk3i0qHqdwiDgMfINP2RFPScE3cgx1UWVkl0KLRxBIVjwJSOCRJ+rs2LVwZQDZpK8/++31aF4B6IDT70JsVZDhGL6okQ7HTRdbIdchKQY6Z8vjgpR4wjPak+7DNsMBFK/AYJT6vnjW1ZwEgdFqxK5SOVEq7IRz+Cr1yDJrxwEipfFa8ARX/8qzdGYXlV4/tO+yJj5tI+F1/IJHrxPc8cYWDoyP17kTFBZoZVP2l0DxhfgVMSlmCFVJGxnE83gH7GotRy0bNwYYblbIdq2LmlpuA1CK5DCsDEKNAiWpyxUtccm8D5hfvD984PwegL6dr968x5AQXC2WY/Lbq/V+aN+SeZH7yf4MZgbe67KK6RwBTAOSsAvZ4lm6cruwqzgGO272avTP7jEEGu3XqGcydZlc/bPPbhi2481zcRmCi7GM8HttibQZJNMPpPS+ozPm4x5cDRA5as5qojKPatiRHeurTMzSKkatim6G2/Dx8XfnXit+eHSekxYjFrnDyt1Fu4LS/Rc201OO88ltAi7xjqslFSVPRMQPwiAdI6nCW7XmA5hfGHIxeFHuvVJEXmCwh97/xF5xYOzNqQ6HNX8avtfiDupd0gQq3a3GVsnBeTF80ReSdfDtkw9IJYIAsrs2dBg+U3uQLpUvyCyo+aL7HYJU2tmy7BP5X4BrOI/XCiQDK/4X799pFmQErYaWS0QZcgAgPw/7rcEKKA1Q7eXPXWYBd/AsnGhkVu8As3AS2+G41VCQbLcVS88jVgcE16csebYem8kqwXmzidUjH3MjfG123xRnGSywhqEyrUQdppmYbqeb+SLNGW1JrSdaR77j9CCZ/wuzpwNIwWjaRFjaEm2LltD6Prde5cliFRsVw/TZHvDJpYqW+pu5vk9vRCscYpuMEzE1MUhffx2KGymGJ4aq8FP9S45R8ZOVhauhXY3ISYl/yALRyesJMoXLfBvIw4vRFp7R6iXcQ0RtgaasBPOf9ni0ySasvCyHfypaDxx/69Mla2Gz56nTm8ewGn8Aid7oBNhdyzWA0IIzhtQhDKuiJoIPNWjQHNdxmksCXpnXAx7M2+ilJPJlsJBgkdAkix4zEvO1jBG0/TnnlNOacP1ZxDL1NfvAH/i1sLOjh4i8zrsuzvK7JvdbxXS6nGq4GiWy7e/CYFVYv7TTJkVqlwN1eHB20CzMlgtyamw8Id/GhLIEqAw1egGHy6x0X//zgucliTPao1ItxjYT/RKFMp0NPclRkhfeIa4BFJJ/tuLlpqrlznQIelIJHEv1Boa6Bidn1ZsRmd/tOiO78LGE650KEy7tJ7M20ZqAJRFkA/oaFrZ11XcCs1Tu0NdToGRxhdKj6A0YI/FX3F3IGFeBgXwtQO7ghmDseVbakU9xnJV7Bz3uxPyehvNDKHiFaZrGYd+QINCe2ur/Jr2zRhcRkxlzCs8HvH2C626V6GQx6yW8vohDgo0W0YgIqaz4U06idfFo60/DNr4cWobHZQfeBiTgEpYkL8vBFL0/D3TLDyRczaLAr7ewEYKBJGkyss6jO9RByH9lxTfTCK/B3b0wAWHHtgHZwZ3+Sl+K38COATUr/ARqlOW304/p7QOz469T8mZdXOWuLZGL10s2c6Su8PkKpbgyl9Qhj1rcmB6xEzzLL7iM0+6KsCSYLkUbzG8f3cbaCFHrA22B3v1hiMOicM3wI8oNI2tc0uCLKQrwAoNiKDlbuSGoNaLTEj/nuGDMaVtAo7DFrgySOKj0ya+382beiRSeXQsp11Utw3ErB8CNlFimLVjnENPhxHQjVj3lb+QCEoB1JMMgQU7XI2uHNS3glvcCI03OB/lfbKykwp14HHLVzeW7nbW4Ot1HQEsUkAHImr4KRryRkZb8D2B+Y0t4CeIVHv/rPRW2LwOzMH8mZ9D7Hu2jUWsJde5haKcb/guc61g1v7ZnG21N9mQwsOBhe5kTLsQzCao/S62VAWHsKPssZVssYDcrh/9jHcrQSXNi7C8TUATjOKkTvrcb985WAR2Q1OBVU7t3sswqjazWn/tORan66GwezZBvw75T3gt3LCZxsRrKiIyQXfPdf/h3U1v6qw993ydb/7PB+aWpgc7JdbrJGq+VuFCGxnvhiZ3tUtb8eCJ2TzuTgIiaPi5/sf4XU3sDFMKH/enqwJdzP1v2acu4UqDD+sa/NRDAbX5AqJS3QMT54TWTXqZFAqn1mG4KUajALzYVSqtWpPjZvlbHDunHLgMOGSZTA9wekCcTGDeZScUipO6tgqwoIEJ6HlyonnxoolouxtcY14+9XqINC5+oyGhzKqq2Bo7AwTRZQb3i9la73pMFx1rwc8FN8I+69ec1Z1vrGEtRF6Uzld4jX27jqnwRM79qGNlJlGzl0bhhrHleaGvxO/NE4PjzlRWzcrWMxpV0uWx0dQu69KRx+L/GCGo9AUw53YuPduGvIUA4MGzQcEifC2RnerOurFW0i628OD3x0XVnlma7emHUWjbiXsUJrQO8Os99DDQLJMd3rSveT/iJ37AzVwVZ5XzP8cMur008+oJHvpW0tAaAS9k92PrOTH4sNMn1bqnTlRqbJCQFMEECSry4jeWeDRSKW+c5O6madWgXs5acDkTuMuYsPmBlSkZYGDnWBzvi/HXYbOAx35JoTZ3bwGKee70bKczxWOoJ93IWkJQ2wfIVa7x55GjAn97TKuNSo/+VaYfxN6pjT6kvPF5CaxVrqOc2p7syGD509VxBk7Cgo0qJXg75w/QxHS7vp4dHSamyOjMC/J8HNN8NcIWPWGA/WWY9sqKZzNS1mMMD+2iVOlMMVe/8ArQP9EeYDFUO9NoQIS50L8pwvqyNXIOyPPNwpfME14Y+gs+xAVmdf1xx3DKTpLFb45A/Ind/j6tACXw5kN7Ukqr020JGJUKc/o28oR4VmuaN/tyLH+6D1xzqTC5KrXYl4kmQEuYNRs0cHu6GRhL/5JLbB34mOusmy2g3TfCBwCewaI2WFWlL+Z//+0DdONc8Xx+/rJVozdzuMsCQR6meHIaEKDRh/vSVj9JIHx7REEzSMfQbzt2KpeSjEjrf8MIp2gKtGfWA27YtJP5t5/qBRllI8kgXDTxqe3VUCBW7f3iOQWB8FVCyFmc9J+GEcP0nZe8HGzMeKrxTiWiJtFmnW9yNuwLGM8mBbc/fRX8blJTX2dqfcaorS9Lua/Ro7uRXXCdv1K7XMOH9hsf0Q44hPz4vXIeh9Xd3k8IHVkshR9xRcIsqJIB412yJT9QhlxcnZhns6vdc9zTbNdEGqs4qRcd162+OQ3mlnBG10baT3t2MVHO4QLR6b6OFVWxFO+bHahstJPdTwXfSxM+GqIf54eEXbUPn+gNVgHdTbHRgwOCJ3yp2coCvmO5FJCq0Y4yTAjPwDLyD/u9mDpBwmN+SiuuKKOsWo/qamDxYAyiOIyTwfR1UttBtC8BCXWS8fuOQtF/OSR7e8vxgKYl7A7Q3bH9GbdpJ59eiaMsnTPB96UJFQTkXPnjGfvxzf8RmSfKYZWfSHxYFnZcaZ4/3lx9dz5hMgllbz/F/15ur5mLu4LwTx99it8roNht6OABnbT7BEex9mKrxXL5YqUzg5n4Yed5FTZg60nYS1nDv779mktfkxbOyc1a+FzpIYR7tpJ3HtaPyRSzMLFCZfxra4nmiMeldztq93Mlauhk4yU/s1FgzlUG8Ju7uqifXtogv7HIwcX2XR+CtCCUmW3LbPz/+57bdac4XXdEMdrRpLZzWJseKlzudJAEliywawWn7yc2TbaDN8R8wZmyS/P15I0cKKWddUtjVwPsba1iqp1Tq1bsAjvYAm6HTpLvXsALgu+x3+t7VuvOPrFp8LoZ3PZKaOc/7mN7NvrUxcWW2FolEBGOQDvFiKIW73QORhIuLgSkMyzviGUfSwkwEJ+aiYyegJXdc7kuohbwK/otrSUmUhQEdHNE5on8EmL5guVYMZqmB/1tYjJhM+9RRn5AqHqOXECwiLJ4e0+kcka9kEIu9e2VN0zsKr5slSENKo914LQe5Js47Wd641N+qoyM1WsP6twngNAD0EKmIa6cJLMHTk222/0kPfxyU7+atRWBByQqHnoOwvuO1pYj1AabWentaCF+eBWyG20FEO6iMkCP/+cMrNOfLvwgUxH3sDBknSRd6uXqig3HpNRPgjq6Wrhoq7xhYNXoz/MSAPVTOt3fUmb2SAu2WrF9ho5mYEn9t0PbDb1l6g8Srpx7GOLzQQzRhTtXyKEooXjFc6f2bIvzLHUa5Rim/3IZteCagGRak5pDIxBkxPTeTJkO3P9L/4d54D68G1F65i6Y9COaA60Jyze/JuGWZTmIdOWxK4sIUCf4sxIyUJfTJc7ni4yVaqzzWYzTfOlL5VQFCXx8aLMN2pCNYik+hhNQcyW1UVR3X72byJH9P0ZiInTlOJLJvzmCL1Y7alzin9jCKSCffkLzgTrOZuhVCvVSk+07dJJcdswZKpWMV1EP2WPoQG1N+1jlGVlulXn0dbPjrevQ4+gFNNGrNU8tD2wYPVa3lG4FoBCHyKYrM2bg4bTHrqjSi/G+VNXdzjBftb4ofzF94OglHjCETVFJCxfhOQA1p7YCkwwm8sUX5a6Mt2yIm8eUkPRl7c0TKPTTRL++speJLoXPC3dcJDfH+cxJI6nb13Bsts3kt11Oc4dgL2PSiWxRxtb8la3dH50hqX6pczSiZhcpFHd167gAt0R25nPvjvny3xHKMhKvefHLBU0TpqCsfoDoMfeWl+29s4mX5LAQrwObTjsPpza2zvDURMwZahMx/ZYTragW9TDxuQDzgXVPGY0qs1KJwi41zndGKQ3xrKgLLxo0Q0WERMfkmJcFwOexGMbWUOuOezCnWvj5nPocc7PtP2hsF5cnZsznZikB46VUSD8dJaROY0h7Vke0K8FcTiSDzViG7CdX1GPZ3n3tqsm6vyFURcuC6xt6NVtfLoHbGYdMYW2YrrYyr+IkpiaUuqB6KezwIyZK5wcY8T3jaT5ergJBU2VTzNVmF3E9in9HM4tQqEvdsOOnkuPSmBovEsC1xnWeuWsTfGxpNDJp5Afuha6ueg7xp9Ym9KU0w84pTulYZoovQXsrjNm3RrYrLw0ySFfqT557MgP1zv9gtjcLe2c65rKJKxL6FRPydzPwA6SYb801ot9S6Ua+LJnU2mH8B7CBt0rFcKZHcm2Jz2zXHUjwxnLtCEhCaI9+GX7UAhaGxRRE2iXvCNaHwtZkeerUaVI/aKUZb5fu+0VP07QSRm7FIeNIAhP1ep0JfhslZvR2/OJCOKu0mVIMcua7JZIIRGtBDs0Orce7mIjZoMzFg/Ym11xrF8tuV1BvXf/0L5+3I5+gPeCzDruY8sS9c6ePGEtmCNziFATRTKWafoRB/eYUipjiDC67T2euHxE8ksng+PL1R80jYEgMXFW82B6xOTqOupgX/53u4F/P8uNQhZQ5J4/s02+iewlsjsdDlFYNJhclv4B+R6Iu8MdwS9k+mZBw0QjGxXttrXwnnDW+edYMIbjFFV8xRS6uVIkSDf763bXDGh/eep3wWQ+CtND8DfYenJsOIJf37/37nn2HY64e3c8uas/hJ0VKn8xik9ieCVSUo+DRTIgGB87aKmcTp3rgbBi2zlN9bsQDAyYEWNXwHAlL5UkpuNcBj9UrlPyG6qAoMAioXBThIA46IgudAdaulFSUQEJ6iNNOTscOPeMgWS6JePseOR89XNTz4S4LMupNczE/glc6gwGHCGSqSxRu7ORhKe+Pkjj64q3/P9uPd7shCbbilDt+do/um1w6JR6SL6M4PFflb/hUfVjQUtb5n1/ipO/4A4fhKZmvBS795BtQ+iijVjM+XeBkZBUPYQT6ijZXBBbulnFzoiZOsU1m62BTDZEjvyO1P4JPJCASjNaFR16Tz/KjRFtwYG52nniiJadfzojmdAA7VOEEY9wP0tSySBvsbvU0hP5ACPuMg3vikmzLiwwDuX/OTrGq2n5HsNsvaln9+K6ts1mOrnm1cBE64iljIAwgL/Wq5kLkZ3fKVSeT8Z196WHlqBq+RNtpGi6X7ss/B+ZRq2OGo4WxWBufPlptpBnrXYYxRK06sgUscIP7JU18HXf83RVZvKre+mmo2KEsobjFSeO6YC3Cjy32m2lC1vrZ9jXaE4KJVBoO14VmZJ65HPJkS5xxBNWfeuaYu5iideuEg192MGm16Iv/DOMfC2yHWPlItT8thKQvrXkrSLrB6nNGrOUF8NQySjqjjzQDZrmirjY36+SHffFFqduVbvr+29kaCLuJcnOVnxbREJHh+t1vQx50ATWlrcxyn/nAtb82oNGPZi7Ay7dt53x7bxnfQfaDk7K+b8VSYzQIIETqFLkhpoK1UNVl9yWonTL6c4DNyhu9x8TDwpybgIXXsYULYmu1Hn7MUQUFtFXBJoT75Q5CJfdvle+8pcSA6oaWdRfLJD+pyancYA+KxNzld2L1dXlfU5tpL8gyFCcA3pfiV3PcsLn0/ozmzcwQgBmL3FRStnS5eXvNILozx1MK4oZ6wQyBXY379TQavTINsdOo/y1oAjXr+e44ZKj3W3A3y4DcwLrBwesnVippwWliBao0jSQmkWxhoeJ43wqBBlIlGsqFnFn5/8yflMgu9v9WuX2PlYM1SdANeNAdyrcL2Nz1QrQqnUDQvt0a6Pld8Z+je0WJglJ9dPsarfzNKjUDIxFCiLsj/BX7Wx5OYE22r5jgvpKO5GK3C1hDDGYdNeTSAmVJMFJ4AGXLYREfgT6W4MKerLUMqSuNJavrPH8QMVDshlsqnLoVNmQ5l7wsP2Z7DneN6q51SsAxyCfyW3hTVhLV7jzINtvxqRC3Aj+g9f7193/Cf9adXytQ+Gj3a8Yi+gxdChAImV09Z9wEDxbKnxunlDKm3MU+ZQupTPYHizQG/5IPziQE+nlQIi8cDurhgBfPKgLCTH/6I4VkJLTu7zt5FZtK8wOrPxa5kyVhIJD+uWtMxGPh86mLhY0/7m5wwoF0u8rViqusOo8gxKYSQUA8QLZNpusdDIHliwD6gU8MiSQl1G9r6gcA7iR/TqvoCeG0LgThz++DhaevbgiwvKUmSlbjwvuWrhCVfhQO5iaSDg58I8VqRqpZumU5hJjsNCp3N7ikqf/G/FfheF1IN2nbckMlozd5u29IoOZaUNn5aXhMpbjPOLozA4Klzy8+NP3ysNk7F5LCT2dA8LH4BLDNvziQ7DZQKpQZm1El+6x6YnstE2cHOa0bZc4GY436wR+JokZtwaM6LMiUmjGHfv6fcE4p0sLiy0W9i/pi8oH9pXIqRT2EZuTzEZheFQlg7Mmupl8rVyOv3EFuhy++XQOasiixCsY4f8CiVR7PmXuspwNqUpBVf7uLxAtSTkIb2pd6PdcuJaeXlff10i4iX7yEcrkdWhitOj6cwtZcznUFX4imbWDdl3kJZ1DMT7lrvLLJ0w3HUJt7cKT46lKX32aY+Gj8lWrdoiKj+dqbo83Jy2lecu4H6lUW0HE9Q7dBcWLMSkEYwj/KeNboD7snO9LKhchfuIuMiP4ytZtTvvcsbHoac5zCUSh9Ho1aiFlCLgjOlGxh1AKimK+K6yzRbgs7nsD9bFCoZQfrFdg0Zd9EyLV9v+yVWzDvuCVYTLq2pX1A4Z1Do2EkP8lp7PIMy/DhfBimUfAG8KsAZIPEQKkLzs3HF+CjtfO9uv9DdMXS06kOMywFiwTgC3UES955Nqkp5Qezd1kci1TfCqNktj7cxhp3TZxjsrB4Uunfqnlydz6jtgYiviX4ZCgLv2UgL2JjiC/wnIyOxnUjpdZsF1eMNRa21AEALD5qnNAhTCq/0RH/py8DjNL4TSuHtVAeqdUgyp+CmjPJMc56pslmQ67kzCHprII3QOWGkJfHRwWd/lMY6aXcTnFv0V9Ty7cvoMiWxCrY8c4zPCBdCn73TS7DSitiIXtyzC1IjAORynekJkeL1QhyxJTE0iWuGoHfeZB0w7iLR+veWMu+DoF7mPx4WNHgy+bujMtQ4OC3M9N0AWNQAkr9xZHXjwUa2Y0YTtHuOb28LjzFDl1Eomua8AasQXQsWwf/Xdi6sj8fqJ5xX0khjO6vfViPfcOf5jPinFcct4A1LkhZAZgV8X+yxEc7C3BNenJzcfcQES1EReon8YWWXaI9gk0gB95uiKlgsL0ia2k8ThMRFcyq7pbUtf0zraC+eHImCyYECSOgecRjr4zH1L9wTDUuTBFehAS+JIdc6Z5Zt9aG0CvaQc9P/94ctyTxBKR4fUia7RFc1ePDEiH5wY93GHKmaRL+hnkfyDnNazh4ZtmHoS6xHbLpFptUJ64Y4iMxcMFssJ+VRMmVMWiF35qMS28dMZhYi5fuEJGYQN1GnpuTpbVA0qVR1gEJpscxJoefRjng530FqhYR4ILdJ4tLiJpl58cgnl5Jc+s3jZWBL6ZoQs0ky4pgfJS9X+LEOFVeScE8OfoEX84UCDCB/iOD+wFAmfDfOaoJmtpQb38v3HJhq1l8jY+rw+r4qhJLMeG2AiuU9X2ERZBnSJdAbrbJCg8SVTWaJtgDogYHkePvIBDgpP3nNWpz6OzNmuAWW//wbFjQ35M00KRdxS8mID0DhsJVi0wT1JEUpAAAAA=",
        "windows" => "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",
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    });

    internal static async Task<Bitmap> DecodeAsync(string key)
    {
        byte[] bytes = Original(key);
        using var input = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        using (var output = input.GetOutputStreamAt(0))
        using (var writer = new Windows.Storage.Streams.DataWriter(output))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }
        input.Seek(0);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(input);
        int width = checked((int)decoder.PixelWidth), height = checked((int)decoder.PixelHeight);
        if (width is < 1 or > 2048 || height is < 1 or > 2048)
            throw new InvalidDataException("Dimensão de arte inválida.");
        var data = await decoder.GetPixelDataAsync(
            Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
            Windows.Graphics.Imaging.BitmapAlphaMode.Straight,
            new Windows.Graphics.Imaging.BitmapTransform(),
            Windows.Graphics.Imaging.ExifOrientationMode.IgnoreExifOrientation,
            Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);
        byte[] pixels = data.DetachPixelData();
        if (pixels.Length != checked(width * height * 4))
            throw new InvalidDataException("Dados da arte incompletos.");
        var result = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var locked = result.LockBits(new Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(pixels, y * width * 4,
                        IntPtr.Add(locked.Scan0, y * locked.Stride), width * 4);
            }
            finally { result.UnlockBits(locked); }
            return result;
        }
        catch { result.Dispose(); throw; }
    }
}
