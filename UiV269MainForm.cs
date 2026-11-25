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
    private bool _v269InitialAttempted;
    private System.Windows.Forms.Timer? _v269LiveTimer;
    private bool _v269LiveSampling;
    private readonly SemaphoreSlim _v269CaptureGate = new(1, 1);
    private CancellationTokenSource? _v269LiveQuery;
    private HardwareSensorPortfolio? _v269LastSnapshot;
    private HardwareSensorPortfolio? _v269MonitorSnapshot;
    private long _v269NextGpuSample;
    private IReadOnlyList<V269HomeModel>? _v269LastHomeModels;
    private readonly Label _v269HardwareStatus = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private V269HomeDashboard? _v269Home;
    private readonly DarkScrollableFlow _v269StartupCards = new() { Dock = DockStyle.Fill };
    private readonly Label _v269StartupTitle = new() { Dock = DockStyle.Fill, AutoEllipsis = true,
        Text = "Consulte os itens de inicialização do Windows." };
    private V269AppearanceOptions _v269Appearance = V269AppearanceOptions.Default;
    private Action? _v269ResizeFromSettings;
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
            if (_v269LastSnapshot is not { } snapshot || _busy || _v269Confirming ||
                _nav.RightPane.Current?.Kind is V269PanelKind.Progress or V269PanelKind.Confirmation) return;
            // Formatamos a fotografia mais recente, não o texto armazenado da consulta inicial.
            string description = "Fotografia local no momento da abertura; não é atualizada automaticamente." +
                Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine + Environment.NewLine,
                    V269HardwareCards(snapshot).Select(x => x.Title + Environment.NewLine + x.Body));
            if (_nav.RightPane.Current?.Kind is V269PanelKind.Result or V269PanelKind.History)
            {
                // Um resultado/histórico deve continuar acessível e intacto.
                // A ação explícita abre uma consulta somente leitura separada.
                using var dialog = CreateV269HardwareDetailsDialog(description);
                dialog.ShowDialog(this);
                return;
            }
            PresentV269Details("Leituras detalhadas do hardware", description);
            if (!_nav.RightPane.Visible)
                _nav.Controls.Find("V269PaneToggle", true).OfType<Button>().Single().PerformClick();
        };
        _v269HardwareStatus.Text = "Aguardando consulta local.";
        _v269HardwareStatus.TextChanged += (_, _) =>
        {
            refresh.Text = _v269HardwareQuery is { IsCancellationRequested: false }
                ? "Cancelar consulta" : "Consultar hardware";
            details.Enabled = _v269LastSnapshot is not null;
        };
        _v269Home = new V269HomeDashboard(refresh, details, _v269HardwareStatus);
        _v269Home.VisibleChanged += (_, _) =>
        {
            if (!_v269Home.Visible)
            {
                if (!V269MonitorWantsCollection()) _v269LiveQuery?.Cancel();
                _v269HardwareQuery?.Cancel();
                if (!_v269HardwareLoaded) _v269InitialAttempted = false;
            }
            else if (_v269LastHomeModels is { } models) _v269Home.SetModels(models);
        };
        _v269Home.SetModels(V269HomeModels(null));
        _v269Home.ApplyAppearance(_v269Appearance);
        return _v269Home;
    }

    private Form CreateV269HardwareDetailsDialog(string description)
    {
        var palette = V269Palette;
        var dialog = new Form
        {
            Name = "V269HardwareDetailsDialog",
            Text = "Leituras detalhadas do hardware",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(690, 500),
            MinimumSize = new Size(380, 270),
            ShowInTaskbar = false,
            MinimizeBox = false,
            MaximizeBox = false,
            BackColor = palette.Content,
            ForeColor = palette.Text
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Padding = new Padding(12), BackColor = palette.Content
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var text = new TextBox
        {
            Name = "V269HardwareDetailsText", AccessibleName = "Leituras locais de hardware",
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, WordWrap = true,
            BackColor = palette.Card, ForeColor = palette.Text, Text = description
        };
        var close = CreateSecondaryButton("Fechar");
        close.Name = "V269HardwareDetailsClose";
        close.Dock = DockStyle.Right;
        close.Width = 105;
        close.BackColor = palette.Card;
        close.ForeColor = palette.Text;
        close.DialogResult = DialogResult.Cancel;
        close.Click += (_, _) => dialog.Close();
        layout.Controls.Add(text, 0, 0);
        layout.Controls.Add(close, 0, 1);
        dialog.Controls.Add(layout);
        dialog.CancelButton = close;
        return dialog;
    }

    private async Task RefreshV269HardwareAsync()
    {
        if (_v269HardwareQuery is not null || IsDisposed || Disposing || !V269OwnsMonitorSession()) return;
        _v269InitialAttempted = true;
        using var request = new CancellationTokenSource();
        _v269HardwareQuery = request;
        _v269LiveQuery?.Cancel();
        bool ownsGate = false;
        HardwareSensorPortfolio? publish = null;
        _v269HardwareStatus.Text = "Consultando sensores e inventário locais…";
        try
        {
            await _v269CaptureGate.WaitAsync(request.Token);
            ownsGate = true;
            _ = await Task.Run(() => GfMonitorSessionLease.Capture(_v269Monitor.Capture, request.Token), request.Token);
            await Task.Delay(250, request.Token);
            var snapshot = await StaWorker.Run(() => GfMonitorSessionLease.Capture(() =>
                HardwareSensorPortfolioReader.CaptureOnDemand(_v269Monitor, request.Token,
                    existingInventory: null, probeCpuVisibleVram: true), request.Token));
            // Cancelamento no retorno da última chamada também deve passar pelo catch,
            // senão o texto do botão pode ficar em "Cancelar consulta".
            request.Token.ThrowIfCancellationRequested();
            if (IsDisposed || Disposing) return;
            _v269LastSnapshot = snapshot;
            publish = snapshot;
            _v269NextGpuSample = Environment.TickCount64 + 8000;
            _v269LastHomeModels = V269HomeModels(snapshot);
            _v269Home?.SetModels(_v269LastHomeModels);
            _v269HardwareLoaded = true;
            _v269HardwareQuery = null;
            _v269HardwareStatus.Text = "Inventário consultado às " + snapshot.CapturedAtUtc.ToLocalTime().ToString("HH:mm:ss");
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
        finally
        {
            if (ownsGate) _v269CaptureGate.Release();
            _v269HardwareQuery = null;
            if (publish is not null && !request.IsCancellationRequested && V269CollectionAllowed())
                PublishV269Monitor(publish);
        }
    }

    // CPU/RAM a cada 2 s; GPU a cada 8 s, sem repetir inventário PnP,
    // TPM, firmware ou armazenamento. Uma única consulta por vez, cancelável
    // na saída da aba, bandeja ou fechamento.
    private void StartV269LiveSampling()
    {
        if (_v269LiveTimer is not null) return;
        _v269LiveTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        bool HomeDemand() =>
            !IsInTray && Visible && Enabled && WindowState != FormWindowState.Minimized &&
            _nav.SelectedPage == V269Page.Inicio;
        bool CanSample() => _v269HardwareQuery is null && V269CollectionAllowed() &&
            (HomeDemand() && _v269HardwareLoaded || V269MonitorWantsCollection());
        _v269LiveTimer.Tick += async (_, _) =>
        {
            AcceptV269MonitorLaunch();
            if (_v269LiveSampling || _v269HardwareQuery is not null || !V269CollectionAllowed()) return;
            // Inicialização oculta não consulta hardware. Ao restaurar
            // diretamente em Início, iniciar a primeira consulta uma vez.
            if (HomeDemand() && !_v269HardwareLoaded && !_v269InitialAttempted)
            {
                await RefreshV269HardwareAsync();
                return;
            }
            if (!CanSample()) return;
            using var request = new CancellationTokenSource();
            _v269LiveQuery = request;
            _v269LiveSampling = true;
            bool ownsGate = false, lightweightCompleted = false, gpuRefreshStarted = false;
            HardwareSensorPortfolio? publish = null;
            try
            {
                await _v269CaptureGate.WaitAsync(request.Token);
                ownsGate = true;
                if (!CanSample()) return;
                var baseline = _v269LastSnapshot ?? _v269MonitorSnapshot;
                var sample = await Task.Run(() => GfMonitorSessionLease.Capture(_v269Monitor.Capture, request.Token), request.Token);
                if (!CanSample() || request.IsCancellationRequested) return;
                lightweightCompleted = true;
                baseline = baseline is null ? GfMonitorSampling.CountersOnly(sample) : baseline with { WindowsCounters = sample };
                RememberV269MonitorSample(baseline);
                publish = baseline;
                if (_v269LastHomeModels is { } homeModels)
                {
                    _v269LastHomeModels = UpdateV269LiveRows(homeModels, sample);
                    if (HomeDemand()) _v269Home?.SetModels(_v269LastHomeModels);
                }
                if (Environment.TickCount64 < _v269NextGpuSample) return;
                gpuRefreshStarted = true;
                try
                {
                    var refreshed = await StaWorker.Run(() => GfMonitorSessionLease.Capture(
                        () => GfMonitorSampling.RefreshGpu(baseline, request.Token), request.Token));
                    if (!CanSample() || request.IsCancellationRequested ||
                        !ReferenceEquals(baseline, _v269LastSnapshot ?? _v269MonitorSnapshot)) return;
                    RememberV269MonitorSample(refreshed);
                    publish = refreshed;
                    if (_v269LastHomeModels is { } currentModels)
                    {
                        var current = currentModels.ToArray();
                        current[1] = V269GpuModel(refreshed);
                        _v269LastHomeModels = current;
                        if (HomeDemand()) _v269Home?.SetModels(current);
                    }
                }
                finally { _v269NextGpuSample = Environment.TickCount64 + 8000; }
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) { }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Invalidar apenas a leitura que falhou (e GPU vencida), também na
                // fotografia usada pelos detalhes; conservar o inventário estático.
                if (CanSample() && !request.IsCancellationRequested)
                {
                    var current = _v269LastHomeModels;
                    var snapshot = _v269LastSnapshot ?? _v269MonitorSnapshot;
                    if (!lightweightCompleted)
                    {
                        var unavailable = V269UnavailableLiveCounters(DateTimeOffset.UtcNow);
                        if (current is not null) current = UpdateV269LiveRows(current, unavailable);
                        snapshot = snapshot is null ? GfMonitorSampling.CountersOnly(unavailable) : snapshot with { WindowsCounters = unavailable };
                    }
                    if (gpuRefreshStarted || Environment.TickCount64 >= _v269NextGpuSample)
                    {
                        if (snapshot is not null)
                            snapshot = V269WithoutGpuReadings(snapshot, DateTimeOffset.UtcNow);
                        if (current is not null)
                        {
                            var updated = current.ToArray();
                            updated[1] = V269GpuModel(snapshot);
                            current = updated;
                        }
                    }
                    if (snapshot is not null) RememberV269MonitorSample(snapshot);
                    publish = snapshot;
                    if (current is not null)
                    {
                        _v269LastHomeModels = current;
                        if (HomeDemand()) _v269Home?.SetModels(current);
                    }
                }
            }
            finally
            {
                if (ownsGate) _v269CaptureGate.Release();
                _v269LiveQuery = null;
                _v269LiveSampling = false;
                if (publish is not null && !request.IsCancellationRequested && V269CollectionAllowed())
                    PublishV269Monitor(publish);
            }
        };
        FormClosed += (_, _) =>
        {
            _v269LiveQuery?.Cancel();
            _v269LiveTimer?.Stop();
            _v269LiveTimer?.Dispose();
            _v269LiveTimer = null;
            StopV269Monitor();
        };
        _v269LiveTimer.Start();
    }

    private void RememberV269MonitorSample(HardwareSensorPortfolio snapshot)
    {
        if (_v269LastSnapshot is not null) _v269LastSnapshot = snapshot;
        else _v269MonitorSnapshot = snapshot;
    }

    internal static LiveHardwareSample V269UnavailableLiveCounters(DateTimeOffset at) =>
        GfMonitorSampling.UnavailableCounters(at);

    internal static HardwareSensorPortfolio V269WithoutGpuReadings(HardwareSensorPortfolio snapshot, DateTimeOffset at) =>
        GfMonitorSampling.WithoutGpuReadings(snapshot, at);

    internal static IReadOnlyList<V269HomeModel> UpdateV269LiveRows(
        IReadOnlyList<V269HomeModel> models, LiveHardwareSample sample)
    {
        static IReadOnlyList<V269HomeRow> Replace(IReadOnlyList<V269HomeRow> rows,
            string label, string value) => rows.Select(r => r.Label == label ? r with { Value = value } : r).ToArray();
        static string Show<T>(MonitorReading<T> reading, Func<T, string> format) where T : struct =>
            reading.Availability == MonitorAvailability.Available && reading.Value is { } value
                ? format(value) : "N/D";
        var mem = sample.Memory;
        return models.Select(model => model.Key switch
        {
            "cpu" => model with { Sensors = Replace(model.Sensors, "Uso",
                Show(sample.CpuUsagePercent, x => $"{x:0.#}%")) },
            "ram" => model with { Sensors = Replace(Replace(model.Sensors, "Uso atual",
                mem is { TotalBytes: > 0 } ? Gib(mem.UsedBytes) : "N/D"), "Disponível",
                mem is { TotalBytes: > 0 } ? Gib(mem.AvailableBytes) : "N/D") },
            "windows" => model with { Specs = Replace(model.Specs, "Tempo de atividade",
                Show(sample.SystemUptime, V269WindowsPresentation.Uptime)) },
            _ => model
        }).ToArray();
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

        foreach (var adapter in snapshot.DxgiAdapters?.Adapters ?? [])
            if (adapter.DiscreteGpuUploadHeapSupported is { } capability)
                yield return (adapter.Name + " · D3D12 OPTIONS16", capability
                    ? "VRAM integral acessível pela CPU (GPU discreta, consulta D3D12). Não confirma o estado exato de ReBAR."
                    : "GPU upload heap não suportado nesta consulta; não comprova ReBAR desativado.");
        foreach (var gpu in snapshot.NvidiaGpus.Devices)
        {
            if (gpu.Bar1TotalBytes is { } bar1)
                yield return (gpu.Name + " · BAR1 (NVML)", "Abertura reportada: " + Gib(bar1) +
                    "\nNão confirma ativação de Resizable BAR.");
            if (gpu.IntendedFanRpms.Count > 0)
                yield return (gpu.Name + " · ventoinhas NVML",
                    "Rotação alvo: " + string.Join(" / ", gpu.IntendedFanRpms.Select(x => $"{x} RPM")) +
                    "\nNão é medição física de rotação.");
            yield return (gpu.Name + " · NVML", $"Uso: {Measure(gpu.UsagePercent, x => $"{x:0.#}%")}\n" +
                $"Temperatura: {Measure(gpu.TemperatureC, x => $"{x:0.#} °C")}\n" +
                $"Potência: {Measure(gpu.PowerWatts, x => $"{x:0.#} W")}\n" +
                $"Ventoinha: {Measure(gpu.FanPercent, x => $"{x:0.#}%")}\n" +
                $"VRAM usada: {Measure(gpu.VramUsedBytes, Gib)}");
        }

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
            if (platform.CpuSocketsState == HardwareDiscoveryState.Complete && platform.CpuSockets.Count == 1 &&
                V269CpuSpecCatalog.Resolve(platform.CpuSockets[0]) is { } specification)
                yield return ("Especificação nominal AMD",
                    $"Base: {specification.BaseMhz} MHz · Boost até {specification.BoostMhz} MHz\n" +
                    "Valores de fábrica, não medições do clock ativo.\n" + specification.Source);
            if (platform.IntelNominalBaseMhz is { } intelBase)
                yield return ("Intel · CPUID 16H", $"Base nominal: {intelBase} MHz\n" +
                    (platform.IntelReportedMaximumMhz is { } intelMax
                        ? $"Máximo reportado: {intelMax} MHz (não confirma Turbo Boost)" : "Máximo não informado"));
            foreach (var cpu in platform.CpuSockets)
                yield return (cpu.ReportedName ?? "Processador · WMI",
                    $"Núcleos físicos: {cpu.PhysicalCores?.ToString() ?? "indisponível"}\n" +
                    $"Processadores lógicos: {cpu.LogicalProcessors?.ToString() ?? "indisponível"}\n" +
                    $"Frequência reportada: {Measure(cpu.ReportedCurrentMhz, x => $"{x:0} MHz")}");
            if (platform.ReportedPopulatedMemoryChannels is { } channels)
                yield return ("Canais de memória · SMBIOS", channels + " canais com módulos instalados.\n" +
                    "Topologia reportada pelo firmware, não confirmação do modo de interleaving ativo.");
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
        string cpuName = Join(cpu.Select(c => c.ReportedName));
        string coreCounts = cpu.Count > 0 && platform?.CpuSocketsState == HardwareDiscoveryState.Complete &&
            cpu.All(x => x.PhysicalCores.HasValue && x.LogicalProcessors.HasValue)
            ? $"{cpu.Sum(x => (long)x.PhysicalCores!.Value)} / {cpu.Sum(x => (long)x.LogicalProcessors!.Value)}" : nd;
        var nominal = platform?.CpuSocketsState == HardwareDiscoveryState.Complete && cpu.Count == 1
            ? V269CpuSpecCatalog.Resolve(cpu[0]) : null;
        string cpuArchitecture = nominal is not null ? nominal.Microarchitecture + " (AMD)" :
            Join(cpu.Select(x => x.ReportedArchitecture is { } isa ? isa + " (ISA)" : null));
        string cpuL3 = Join(cpu.Select(x => x.ReportedL3KiB is { } kib
            ? $"{kib / 1024d:0.##} MiB" : null));
        if (cpuL3.Length == 0 && nominal?.L3MiB is { } l3)
            cpuL3 = $"{l3} MiB (AMD)";
        // Não somar caches de topologias distintas nem deduzir Zen/Core
        // ou frequências base/turbo pelo clock reportado pelo firmware.
        if (cpu.Count > 1 && cpuL3.Length != 0) cpuL3 += " (por CPU)";
        var cpuRows = new V269HomeRow[] { R("Arquitetura", cpuArchitecture), R("Núcleos / Threads", coreCounts),
            R("Frequência base", nominal is not null ? $"{nominal.BaseMhz / 1000d:0.00} GHz (AMD)" :
                platform?.IntelNominalBaseMhz is { } intelBase ? $"{intelBase / 1000d:0.00} GHz (CPUID)" : nd),
            R("Frequência turbo", nominal is null ? nd : $"Até {nominal.BoostMhz / 1000d:0.00} GHz (AMD)"),
            R("Cache L3", cpuL3) };
        var cpuSensors = new V269HomeRow[] { R("Temperatura", M(sample?.Sensors.CpuTemperatureC, Temp)),
            R("Uso", M(sample?.CpuUsagePercent, Percent)),
            R("Consumo", M(sample?.Sensors.CpuPowerWatts, x => $"{x:0.#} W")),
            R("Ventoinha (CPU)", M(sample?.Sensors.CpuFanRpm, Rpm)) };

        static string MemoryType(uint? type) => type switch {
            // SMBIOS Type 17 (não a enumeração WMI MemoryType).
            18 => "DDR", 19 => "DDR2", 20 => "DDR2 FB-DIMM", 24 => "DDR3", 26 => "DDR4", 34 => "DDR5",
            30 => "LPDDR4", 35 => "LPDDR5", _ => nd };
        var mem = sample?.Memory;
        var ramRows = new V269HomeRow[] {
            R("Capacidade", mem is { TotalBytes: > 0 } ? Gib(mem.TotalBytes) : nd),
            R("Tipo", Join(ram.Select(x => MemoryType(x.SmbiosMemoryType)))),
            R("Frequência", Join(ram.Select(x => M(x.ConfiguredMhz, Freq)))),
            R("Canais", platform?.ReportedPopulatedMemoryChannels is { } channels
                ? channels + " (SMBIOS)" : nd), R("Latência", nd) };
        var ramSensors = new V269HomeRow[] {
            R("Uso atual", mem is { TotalBytes: > 0 } ? Gib(mem.UsedBytes) : nd),
            R("Disponível", mem is { TotalBytes: > 0 } ? Gib(mem.AvailableBytes) : nd) };
        var boardNominal = platform?.MotherboardsState == HardwareDiscoveryState.Complete && boards.Count == 1
            ? V269BoardSpecCatalog.Resolve(boards[0]) : null;
        var boardRows = new V269HomeRow[] { R("Fabricante", Join(boards.Select(x => x.Manufacturer))),
            R("Modelo", Join(boards.Select(x => x.Product))), R("Chipset", boardNominal is null ? nd : boardNominal.Chipset + " (ASUS)"),
            R("BIOS", platform?.ReportedBiosVersion),
            R("Soquete", boardNominal is not null ? boardNominal.Socket + " (ASUS)" :
                nominal?.Socket is { } socket ? socket + " (CPU, AMD)" :
                Join(cpu.Select(x => x.ReportedSocketDesignation is { } designation
                    ? "Designação: " + designation : null))) };
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
            architecture = V269WindowsPresentation.Architecture(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture);
            version = Environment.OSVersion.VersionString;
            try
            {
                using var osKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string edition = osKey?.GetValue("EditionID") as string ?? "";
                string display = osKey?.GetValue("DisplayVersion") as string ?? "";
                string installation = osKey?.GetValue("InstallationType") as string ?? "";
                version = V269WindowsPresentation.Version(os, installation, edition, display);
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
            R("Tempo de atividade", M(sample?.SystemUptime, V269WindowsPresentation.Uptime)) };
        string virtualization = nd;
        if (cpu.Count > 0 && platform?.CpuSocketsState == HardwareDiscoveryState.Complete &&
            cpu.All(x => x.FirmwareVirtualizationEnabled.HasValue))
            virtualization = cpu.All(x => x.FirmwareVirtualizationEnabled == true) ? "Ativa (firmware)" :
                cpu.All(x => x.FirmwareVirtualizationEnabled == false) ? "Não ativa (WMI)" : "Mista (firmware)";
        var windowsSensors = new V269HomeRow[] { R("Inicialização segura", secureBoot),
            R("TPM", snapshot?.TpmDescription), R("Virtualização", virtualization) };
        return [
            new("cpu", "Processador (CPU)", R("", cpuName).Value, cpuRows, cpuSensors),
            V269GpuModel(snapshot),
            new("ram", "Memória (RAM)", "", ramRows, ramSensors),
            new("motherboard", "Placa-mãe / Chipset", "", boardRows, boardSensors),
            new("windows", "Windows", "", windowsRows, windowsSensors)
        ];
    }

    // Exatamente os cinco campos e quatro sensores do cartão aprovado.
    // Fontes diferentes permanecem rotuladas; dados extras nos detalhes.
    internal static V269HomeModel V269GpuModel(HardwareSensorPortfolio? snapshot)
    {
        const string nd = "N/D";
        static V269HomeRow R(string label, string? value) => new(label,
            string.IsNullOrWhiteSpace(value) || value == "indisponível" ? "N/D" : value);
        static string Join(IEnumerable<string?> values) => string.Join(" / ", values
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal));
        static string? Reading<T>(MonitorReading<T> reading, Func<T, string> format,
            string identity, string source) where T : struct =>
            reading.Availability == MonitorAvailability.Available && reading.Value is { } value
                ? identity + ": " + format(value) + " (" + source + ")" : null;
        static string Temp(double x) => $"{x:0.#} °C";
        static string Percent(double x) => $"{x:0.#}%";
        var dxgi = snapshot?.DxgiAdapters?.Adapters.Where(x => !x.IsSoftware).ToArray() ?? [];
        var nvml = snapshot?.NvidiaGpus.Devices ?? [];
        var pnp = snapshot?.DeviceInventory.Devices.Where(x => x.Kind == HardwareDeviceKind.Graphics).ToArray() ?? [];
        // Só retirar prefixos de identidade quando as três fontes comprovam
        // uma única GPU. Nunca fundir duas placas por nome ou índice.
        if (snapshot is not null && TryV269SingleGpu(snapshot, out var singleDxgi, out var singlePnp, out var singleNvml))
        {
            ulong? capacity = singleDxgi.DedicatedVideoMemory > 0 ? singleDxgi.DedicatedVideoMemory :
                singleNvml?.DedicatedMemoryBytes;
            string? memory = capacity is { } total ? Gib(total) : null;
            if (singleNvml is { } verified && capacity is { } actual &&
                V269GpuMemoryType(verified, actual) is { } type)
                memory += " " + type + " (catálogo)";
            var wddm = snapshot.MatchedGpuDevices.Where(x =>
                x.Counters.Key.LuidHigh == singleDxgi.LuidHigh && x.Counters.Key.LuidLow == singleDxgi.LuidLow &&
                x.DxgiDescription is not null).ToArray();
            var d3d = snapshot.D3dKmtGpuSensors?.Adapters.Where(x =>
                x.Key.LuidHigh == singleDxgi.LuidHigh && x.Key.LuidLow == singleDxgi.LuidLow).ToArray() ?? [];
            string? Value<T>(MonitorReading<T>? reading, Func<T, string> format) where T : struct =>
                reading is { Availability: MonitorAvailability.Available, Value: { } measured } ? format(measured) : null;
            string? temp = d3d.Length == 1 ? Value(d3d[0].TemperatureC, Temp) : null;
            string? use = Value(singleNvml?.UsagePercent, Percent);
            if (use is null && wddm.Length == 1)
            {
                var engine = Value(wddm[0].Counters.BusiestEnginePercent, Percent);
                if (engine is not null) use = engine + " (motor WDDM)";
            }
            string? used = Value(singleNvml?.VramUsedBytes, Gib);
            if (used is null && wddm.Length == 1) used = Value(wddm[0].Counters.DedicatedUsedBytes, Gib);
            string? fan = d3d.Length == 1 ? Value(d3d[0].FanRpm, x => $"{x:0} RPM") : null;
            if (fan is null && singleNvml is { IntendedFanRpms.Count: > 0 } target)
                fan = string.Join("/", target.IntendedFanRpms) + " RPM (alvo)";
            if (fan is null && Value(singleNvml?.FanPercent, Percent) is { } pct)
                fan = pct + " (alvo)";
            return new("gpu", "Placa de vídeo (GPU)", singleDxgi.Name,
                [R("Memória", memory), R("Barramento", singleNvml?.CurrentPcieLink ?? singlePnp.ReportedPcieLink),
                 R("Resizable BAR", V269BarEvidence(singleDxgi, singleNvml)),
                 R("Driver", singlePnp.InstalledDriverVersion ??
                    (snapshot.NvidiaGpus.NvidiaDriverVersion is { } singleDriverVersion && singleNvml is not null ? singleDriverVersion + " (NVML)" : null)),
                 R("Arquitetura", singleNvml?.ReportedArchitecture)],
                [R("Temperatura", temp ?? Value(singleNvml?.TemperatureC, Temp)), R("Uso", use),
                 R("VRAM usada", used), R("Ventoinha (GPU)", fan)]);
        }
        string links = Join(nvml.Where(x => x.CurrentPcieLink is not null)
            .Select(x => x.Name + ": " + x.CurrentPcieLink)
            .Concat(pnp.Where(x => x.ReportedPcieLink is not null)
                .Select(x => x.Name + ": " + x.ReportedPcieLink)));
        var driverValues = pnp.Where(x => x.InstalledDriverVersion is not null)
            .Select(x => x.Name + ": " + x.InstalledDriverVersion + " (Windows)").ToList();
        if (nvml.Count > 0 && snapshot?.NvidiaGpus.NvidiaDriverVersion is { } version)
            driverValues.Add(version + " (NVIDIA NVML)");
        var graphics = new V269HomeRow[] {
            R("Memória", Join(dxgi.Where(x => x.DedicatedVideoMemory > 0)
                .Select(x => x.Name + ": " + Gib(x.DedicatedVideoMemory) + " (DXGI)")
                .Concat(nvml.Where(x => x.DedicatedMemoryBytes.HasValue)
                    .Select(x => x.Name + ": " + Gib(x.DedicatedMemoryBytes!.Value) + " (NVML)")))),
            R("Barramento", links), R("Resizable BAR", Join(nvml.Where(x => x.Bar1TotalBytes.HasValue)
                .Select(x => x.Name + ": N/D · BAR1 " + Gib(x.Bar1TotalBytes!.Value)))),
            R("Driver", Join(driverValues)),
            R("Arquitetura", Join(nvml.Where(x => x.ReportedArchitecture is not null)
                .Select(x => x.Name + ": " + x.ReportedArchitecture + " (NVML)"))) };
        var temperatures = new List<string?>();
        var usages = new List<string?>();
        var memories = new List<string?>();
        var fans = new List<string?>();
        foreach (var gpu in nvml)
        {
            temperatures.Add(Reading(gpu.TemperatureC, Temp, gpu.Name, "NVML"));
            usages.Add(Reading(gpu.UsagePercent, Percent, gpu.Name, "NVML"));
            memories.Add(Reading(gpu.VramUsedBytes, Gib, gpu.Name, "NVML"));
            if (gpu.IntendedFanRpms.Count > 0)
                fans.Add(gpu.Name + ": " + string.Join("/", gpu.IntendedFanRpms) + " RPM (alvo NVML)");
            else fans.Add(Reading(gpu.FanPercent, Percent, gpu.Name, "NVML; não RPM"));
        }
        foreach (var gpu in snapshot?.MatchedGpuDevices ?? [])
        {
            if (gpu.DxgiDescription is not { } identity) continue;
            usages.Add(Reading(gpu.Counters.BusiestEnginePercent, Percent, identity, "WDDM; motor mais ocupado"));
            memories.Add(Reading(gpu.Counters.DedicatedUsedBytes, Gib, identity, "WDDM"));
        }
        foreach (var gpu in snapshot?.D3dKmtGpuSensors?.Adapters ?? [])
        {
            var match = dxgi.Where(x => x.LuidHigh == gpu.Key.LuidHigh &&
                x.LuidLow == gpu.Key.LuidLow).ToArray();
            if (match.Length != 1) continue;
            string identity = match[0].Name + " · físico " + gpu.Key.Physical;
            temperatures.Add(Reading(gpu.TemperatureC, Temp, identity, "D3DKMT"));
            fans.Add(Reading(gpu.FanRpm, x => $"{x:0} RPM", identity, "D3DKMT"));
        }
        string name = Join(dxgi.Select(x => x.Name).Concat(nvml.Select(x => x.Name))
            .Concat(pnp.Select(x => x.Name)));
        return new("gpu", "Placa de vídeo (GPU)", R("", name).Value, graphics,
            [R("Temperatura", Join(temperatures)), R("Uso", Join(usages)),
             R("VRAM usada", Join(memories)), R("Ventoinha (GPU)", Join(fans))]);
    }

    // Correlacionar pelo triplo VEN/DEV/SUBSYS único em inventários
    // completos. Sem essa evidência, conservar a apresentação separada.
    internal static string V269BarEvidence(DxgiGpuIdentity adapter, NvidiaGpuSensorSample? nvidia)
    {
        if (!adapter.IsSoftware && adapter.DiscreteGpuUploadHeapSupported == true)
            return "N/D · VRAM acessível (D3D12)";
        return nvidia?.Bar1TotalBytes is { } bar ? "N/D · BAR1 " + Gib(bar) : "N/D";
    }

    internal static bool TryV269SingleGpu(HardwareSensorPortfolio snapshot,
        out DxgiGpuIdentity adapter, out HardwareDeviceDescriptor device, out NvidiaGpuSensorSample? nvidia)
    {
        adapter = null!; device = null!; nvidia = null;
        if (snapshot.DxgiAdapters?.State != HardwareDiscoveryState.Complete ||
            snapshot.DeviceInventory.State != HardwareDiscoveryState.Complete) return false;
        var dxgi = snapshot.DxgiAdapters.Adapters.Where(x => !x.IsSoftware).ToArray();
        var pnp = snapshot.DeviceInventory.Devices.Where(x => x.Kind == HardwareDeviceKind.Graphics).ToArray();
        if (dxgi.Length != 1 || pnp.Length != 1 ||
            DxgiGpuIdentityProvider.MatchUniquePnp(dxgi[0], pnp) is not { } id ||
            !string.Equals(id, pnp[0].InstanceId, StringComparison.OrdinalIgnoreCase))
            return false;
        var nvml = snapshot.NvidiaGpus.Devices;
        if (nvml.Count > 0)
        {
            if (!snapshot.NvidiaGpus.ProviderAvailable || snapshot.NvidiaGpus.Limitation is not null ||
                nvml.Count != 1 || nvml[0].PciIdentity is not { } pci ||
                pci.Vendor != dxgi[0].VendorId || pci.Device != dxgi[0].DeviceId ||
                pci.Subsystem != dxgi[0].SubSystemId) return false;
            nvidia = nvml[0];
        }
        adapter = dxgi[0]; device = pnp[0];
        return true;
    }

    // Tipo de memória nominal, não uma leitura elétrica/firmware. A
    // RTX 5060 Ti desktop é oferecida em 8 ou 16 GB GDDR7 pela NVIDIA.
    // Não inferir tipo por arquitetura, geração, capacidade isolada ou
    // nomes parciais de placas móveis/variantes.
    internal static string? V269GpuMemoryType(NvidiaGpuSensorSample gpu, ulong dedicatedBytes) =>
        gpu.PciIdentity?.Vendor == 0x10de && gpu.ReportedArchitecture == "Blackwell" &&
        string.Equals(gpu.Name, "NVIDIA GeForce RTX 5060 Ti", StringComparison.OrdinalIgnoreCase) &&
        (dedicatedBytes == 8UL << 30 || dedicatedBytes == 16UL << 30)
        ? "GDDR7" : null;

    private Control BuildV269StartupPage()
    {
        var page = new Panel { Name="V269StartupPage",Dock=DockStyle.Fill,BackColor=Color.Transparent };
        _v269StartupCards.Name="V269StartupViewport";
        _v269StartupCards.Margin=Padding.Empty;
        _v269StartupCards.ApplyCleanupSurface(_v269Appearance.Theme);
        _v269StartupCards.Content.Padding=Padding.Empty;
        page.Controls.Add(_v269StartupCards);
        _v269StartupTitle.Dock=DockStyle.Top;
        _v269StartupTitle.Height=30;
        _v269StartupTitle.ForeColor=Muted;
        page.Controls.Add(_v269StartupTitle);
        _v269StartupTitle.BringToFront();
        _v269StartupCards.Content.SizeChanged+=(_,_)=>ResizeV269StartupCards();
        _v269StartupCards.VisibleChanged+=(_,_)=>ResizeV269StartupCards();
        _v269StartupCards.ToolViewportResized+=(_,_)=>ResizeV269StartupCards();
        _v269StartupCards.DpiChangedAfterParent+=(_,_)=>ResizeV269StartupCards();
        return page;
    }

    private void ResizeV269StartupCards()
    {
        if(_fittingV269Startup||_v269StartupCards.IsDisposed||!_v269StartupCards.Visible)return;
        _fittingV269Startup=true;
        try
        {
            var flow=_v269StartupCards.Content;
            for(int pass=0;pass<2;pass++)
            {
                int width=Math.Max(1,flow.ClientSize.Width-flow.Padding.Horizontal);
                int S(int n)=>V269AppTable.Scale(n,_v269Appearance.InterfacePercent,_v269StartupCards.DeviceDpi);
                int columns=width>=S(540)?3:width>=S(330)?2:1,gap=S(9);
                var cards=flow.Controls.OfType<V269StartupCard>().ToArray();
                int w=Math.Max(1,(width-gap*(columns-1)-1)/columns);
                flow.SuspendLayout();
                try
                {
                    for(int i=0;i<cards.Length;i+=columns)
                    {
                        var row=cards.Skip(i).Take(columns).ToArray();
                        int h=row.Max(c=>c.HeightForWidth(w));
                        for(int j=0;j<row.Length;j++)
                        {row[j].Margin=new Padding(0,0,j==columns-1?0:gap,gap);row[j].Size=new Size(w,h);}
                    }
                }
                finally{flow.ResumeLayout(true);}
                _v269StartupCards.Reflow();
                if(flow.ClientSize.Width-flow.Padding.Horizontal==width)break;
            }
        }
        finally{_fittingV269Startup=false;}
    }

    private V269GeneralOptions _v269General = new();
    private V269SettingsPage? _v269Settings;
    private bool _v269SettingsBusy;
    private bool _v269SettingsLive;
    private V269AdminLaunchTicket? _v269AdminRestartTicket;
    private bool _v269AdminClosedNormally;
    internal bool V269CanAnimateButtons => CanAnimateV269;

    private Control BuildV269Settings(bool loadPreferences)
    {
        _v269SettingsLive=loadPreferences;
        if(loadPreferences)
        {
            _v269Appearance=ReadV269Appearance();
            _v269General=V269GeneralSettingsStore.Read(V269GeneralSettingsStore.PathForUser);
        }
        var view=_v269Settings=new V269SettingsPage();
        var appearance=view.Appearance;
        var binder=new V269AppearanceBinder(_nav,appearance);
        view.Disposed+=(_,_)=>binder.Dispose();
        binder.SetOptions(_v269Appearance);
        _programList.V269Options().AnimateButtonFeedback=CanAnimateV269;
        System.Windows.Forms.Timer? saveAppearance=null;
        bool dirtyAppearance=false;
        void SaveAppearance()
        {
            if(!dirtyAppearance)return;
            try{WriteV269Appearance(_v269Appearance);dirtyAppearance=false;view.SetNotice(null);}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                view.SetNotice("Aparência aplicada nesta sessão; não foi possível salvar a preferência.");
                DiagnosticTrace.Write("PREFERENCIAS.APARENCIA_FALHA "+ex.GetType().Name);
            }
        }
        if(loadPreferences)
        {
            saveAppearance=new System.Windows.Forms.Timer{Interval=250};
            saveAppearance.Tick+=(_,_)=>{saveAppearance.Stop();SaveAppearance();};
            FormClosing+=(_,e)=>
            {
                if(e.CloseReason!=CloseReason.UserClosing)_v269AdminRestartTicket=null;
                saveAppearance.Stop();SaveAppearance();
            };
            FormClosed+=(_,e)=>
            {
                _v269AdminClosedNormally=e.CloseReason==CloseReason.UserClosing&&!_betaClose.IsShutdown;
                saveAppearance.Dispose();
            };
        }
        appearance.AppearanceChanged+=(_,_)=>
        {
            int previousInterface=_v269Appearance.InterfacePercent;
            _v269Appearance=appearance.GetOptions().Normalize();
            _v269MonitorHost?.Configure(_v269Appearance.Theme, CanAnimateV269);
            if(previousInterface!=_v269Appearance.InterfacePercent)_v269ResizeFromSettings?.Invoke();
            _programList.V269Options().AnimateButtonFeedback=CanAnimateV269;
            ApplyV269ContentAppearance(this);
            RefreshLegacyOperationMeter();
            if(saveAppearance is not null){dirtyAppearance=true;saveAppearance.Stop();saveAppearance.Start();}
        };
        view.General.Notifications.Click+=(_,_)=>ChangeV269General(_v269General with{Notifications=!_v269General.Notifications});
        view.General.Tray.Click+=(_,_)=>ChangeV269General(_v269General with{MinimizeToTray=!_v269General.MinimizeToTray});
        view.General.StartWindows.Click+=async(_,_)=>await ToggleV269StartupAsync();
        view.General.Administrator.Click+=async(_,_)=>await ToggleV269AdminSessionAsync();
        view.General.Collect.Click+=(_,_)=>
        {
            if(!_v269SettingsLive||_v269SettingsBusy||_busy||_v269Confirming||_betaClose.IsClosing)return;
            OfferAnonymousDiagnosticConsent();RefreshV269General();
        };
        view.General.Privacy.Click+=(_,_)=>PresentV269SettingsInformation("Privacidade e coleta de dados",
            (BetaRuntimePolicy.IsLocalBeta?BetaRuntimePolicy.LocalNotice:TelemetryConsentPolicy.RemoteNotice)+
            Environment.NewLine+Environment.NewLine+
            PrivacyConsentPresentation.Explanation(AnonymousTelemetryContext.Current?.Consent??TelemetryConsent.NotAsked));
        view.General.Diagnostic.Click+=(_,_)=>
        {
            if(_busy||_v269Confirming||_betaClose.IsClosing)return;
            var report="GERENCIADOR FINAL — diagnóstico local"+Environment.NewLine+
                "Versão: "+DiagnosticTrace.VersionLabel+Environment.NewLine+
                "Windows: "+Environment.OSVersion.Version+Environment.NewLine+
                "Arquitetura: "+System.Runtime.InteropServices.RuntimeInformation.OSArchitecture+Environment.NewLine+
                "Privilégios: "+(V269SessionIsAdministrator() switch {true=>"elevados",false=>"comuns",null=>"não verificados"})+Environment.NewLine+
                Environment.NewLine+_reliability.Report(_installed,_inventoryIncomplete);
            if(_v269LastSnapshot is{} snapshot)
                report+=Environment.NewLine+Environment.NewLine+"Última consulta de hardware (não é uma leitura nova):"+
                    Environment.NewLine+HardwareMonitoringDiagnosticReport.Format(snapshot);
            PresentV269SettingsInformation("Diagnóstico local",report);
        };
        view.General.History.Click+=(_,_)=>
        {
            if(_busy||_v269Confirming||_betaClose.IsClosing)return;
            if(_nav.RightPane.Current?.Kind is V269PanelKind.Result or V269PanelKind.History)
            {
                ShowLocalReliabilityReport();return;
            }
            PresentV269History();
            if(!_nav.RightPane.Visible)
                _nav.Controls.Find("V269PaneToggle",true).OfType<Button>().Single().PerformClick();
        };
        view.VisibleChanged+=(_,_)=>{if(view.Visible)RefreshV269General();};
        if (loadPreferences) BindV269Monitor(view);
        view.ApplyAppearance(_v269Appearance);RefreshV269General();
        return view;
    }

    private static bool? V269SessionIsAdministrator()
    {
        try
        {
            using var identity=System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch(Exception ex) when(ex is UnauthorizedAccessException or System.Security.SecurityException or
            System.ComponentModel.Win32Exception){return null;}
    }

    private bool? ReadV269Startup()
    {
        if(!_v269SettingsLive)return false;
        try{return StartupTrayRegistration.IsEnabled();}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or
            InvalidDataException or InvalidOperationException){return null;}
    }

    private void RefreshV269General()
    {
        if(_v269Settings is null||_v269Settings.IsDisposed)return;
        _v269Settings.General.SetObserved(_v269SettingsLive?V269SessionIsAdministrator():false,
            ReadV269Startup(),_v269General,AnonymousTelemetryContext.Current?.Consent??TelemetryConsent.NotAsked,
            _v269SettingsLive&&!_v269SettingsBusy&&!_betaClose.IsClosing,
            AnonymousTelemetryContext.Current is not null&&BetaRuntimePolicy.AllowsCollaborativeDiagnostics);
    }

    private void ChangeV269General(V269GeneralOptions candidate)
    {
        if(!_v269SettingsLive||_v269SettingsBusy||_betaClose.IsClosing)return;
        try
        {
            V269GeneralSettingsStore.Write(V269GeneralSettingsStore.PathForUser,candidate);
            _v269General=candidate;
            _v269Settings?.SetNotice(null);
            Status("Preferência geral salva.");
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _v269Settings?.SetNotice("Não foi possível salvar a preferência; escolha anterior preservada.");
            Status("Não foi possível salvar a preferência; escolha anterior preservada.");
            DiagnosticTrace.Write("PREFERENCIAS.GERAL_FALHA "+ex.GetType().Name);
        }
        RefreshV269General();
    }

    private async Task ToggleV269AdminSessionAsync()
    {
        if(!_v269SettingsLive||_v269SettingsBusy||_busy||_batchUpdatesActive||_v269Confirming||
            _betaClose.IsClosing||_windowsUpdateInstalling||V269SessionIsAdministrator() is not bool elevated)return;
        _v269SettingsBusy=true;RefreshV269General();
        try
        {
            string description=elevated
                ?"Encerrar a sessão elevada atual? Abra o GF normalmente depois. Nenhuma preferência de elevação é gravada."
                :"Encerrar esta sessão e solicitar UAC para abrir uma única sessão elevada? "+
                 "Nenhuma elevação automática será configurada. Todos os comandos de manutenção continuam sujeitos às confirmações existentes.";
            if(!await ConfirmV269Async("Modo Administrador",description))return;
            if(_busy||_batchUpdatesActive||_windowsUpdateInstalling||_betaClose.IsClosing||
                V269SessionIsAdministrator()!=elevated)return;
            if(!elevated)
            {
                try{_v269AdminRestartTicket=V269AdminSessionLauncher.Prepare(Environment.ProcessPath??"");}
                catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
                {
                    PresentV269SettingsInformation("Modo Administrador","Não foi possível verificar o executável atual para reabertura.");return;
                }
            }
            Close();
            if(!IsDisposed&&!_betaClose.IsClosing)_v269AdminRestartTicket=null;
        }
        finally
        {
            _v269SettingsBusy=false;
            if(!IsDisposed&&!Disposing)RefreshV269General();
        }
    }

    internal V269AdminLaunchTicket? ConsumeV269AdminRestart()
    {
        var ticket=_v269AdminClosedNormally&&!_betaClose.IsShutdown?_v269AdminRestartTicket:null;
        _v269AdminRestartTicket=null;
        return ticket;
    }

    private async Task ToggleV269StartupAsync()
    {
        if(!_v269SettingsLive||_v269SettingsBusy||_busy||_v269Confirming||_betaClose.IsClosing||
            ReadV269Startup() is not bool previous)return;
        _v269SettingsBusy=true;RefreshV269General();
        try
        {
            if(!await ConfirmV269Async("Inicialização do Gerenciador Final",
                previous?"Desativar o início automático do GF nesta conta?":
                "Iniciar o GF na bandeja ao entrar nesta conta? Nenhum serviço ou privilégio administrativo será criado."))return;
            if(ReadV269Startup()!=previous)
            {
                _v269Settings?.SetNotice("O estado da inicialização mudou; revise antes de alterar.");
                Status("O estado da inicialização mudou; revise antes de alterar.");return;
            }
            StartupTrayRegistration.SetEnabled(!previous);
            bool verified=ReadV269Startup()==!previous;
            _v269Settings?.SetNotice(verified?null:"Não foi possível confirmar a alteração da inicialização.");
            Status(verified?"Preferência de inicialização confirmada.":"Não foi possível confirmar a alteração da inicialização.");
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or
            InvalidDataException or InvalidOperationException)
        {
            _v269Settings?.SetNotice("Não foi possível alterar a inicialização.");
            Status("Não foi possível alterar a inicialização.");DiagnosticTrace.Write("PREFERENCIAS.INICIO_FALHA "+ex.GetType().Name);
        }
        finally{_v269SettingsBusy=false;RefreshV269General();}
    }

    private void PresentV269SettingsInformation(string title,string description)
    {
        if(_busy||_v269Confirming||_betaClose.IsClosing)return;
        // Nunca substituir um resultado de manutenção não dispensado.
        if(_nav.RightPane.Current?.Kind is V269PanelKind.Result or V269PanelKind.History)
        {
            using var dialog=new Form
            {
                Name="V269SettingsInformation",Text=title,Size=new Size(690,500),
                MinimumSize=new Size(380,270),StartPosition=FormStartPosition.CenterParent,
                ShowInTaskbar=false,MinimizeBox=false,MaximizeBox=false,
                BackColor=V269Palette.Content,ForeColor=V269Palette.Text
            };
            var text=new TextBox{Name="V269SettingsInformationText",AccessibleName=title,
                Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,WordWrap=true,ScrollBars=ScrollBars.Vertical,
                BackColor=V269Palette.Card,ForeColor=V269Palette.Text,Text=description};
            var close=new Button{Text="Fechar",Dock=DockStyle.Bottom,Height=34,DialogResult=DialogResult.Cancel};
            dialog.Controls.Add(text);dialog.Controls.Add(close);dialog.CancelButton=close;
            dialog.ShowDialog(this);return;
        }
        PresentV269Details(title,description);
        if(!_nav.RightPane.Visible)
            _nav.Controls.Find("V269PaneToggle",true).OfType<Button>().Single().PerformClick();
    }

    private void MinimizeByV269Preference()
    {
        if(_v269General.MinimizeToTray)MinimizeToTray();
        else WindowState=FormWindowState.Minimized;
    }

    private void NotifyV269OperationResult()
    {
        if(!_v269General.Notifications||!IsInTray||_trayIcon?.Visible!=true)return;
        try
        {
            // Aviso genérico: não exporta nomes, caminhos ou resultado incerto.
            _trayIcon.ShowBalloonTip(4000,"Gerenciador Final",
                "Uma operação terminou. Abra o GF para consultar o resultado registrado.",ToolTipIcon.Info);
        }
        catch(InvalidOperationException){ }
    }

    private static string V269AppearancePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GerenciadorFinal", "aparencia-v269.json");

    private static V269AppearanceOptions ReadV269Appearance() =>
        V269AppearanceSettingsStore.Read(V269AppearancePath);

    private static void WriteV269Appearance(V269AppearanceOptions value) =>
        V269AppearanceSettingsStore.Write(V269AppearancePath,value);

    private void ApplyV269ContentAppearance(Control root)
    {
        if (root == _nav)
        {
            foreach (var page in Enum.GetValues<V269Page>())
                if (_nav.PageContent(page) is { } view) ApplyV269ContentAppearance(view);
            _fitV269CleanupDiskSection?.Invoke();
            _relayoutCleaning?.Invoke();
            _relayoutRepair?.Invoke();
            if(_nav.SelectedPage==V269Page.Sistema)_relayoutSystem?.Invoke();
            if(_nav.SelectedPage==V269Page.Inicializacao)ResizeV269StartupCards();
            if (_log.Parent is { } logHost) ApplyV269ContentAppearance(logHost);
            return;
        }
        if (root is V269HomeDashboard home)
        {
            home.ApplyAppearance(_v269Appearance);
            return;
        }
        if (root is V269BrandHeader brand)
        {
            brand.RefreshHomeTheme(_v269Appearance);
            if (brand.IsHomePresentation) return;
        }
        if (root is V269SettingsPage settings) { settings.ApplyAppearance(_v269Appearance); return; }
        if (root is V269AppearancePanel) return;
        if (!_v269Colors.TryGetValue(root, out var original))
        {
            original = new(root.BackColor, root.ForeColor);
            _v269Colors.Add(root, original);
            root.ControlAdded += (_, e) => { if (e.Control is { } child) ApplyV269ContentAppearance(child); };
        }

        var palette = V269Palette;
        if (root is DarkDriverList driverList)
        {
            driverList.Theme = _v269Appearance.Theme;
            if (driverList.TableMode)
            {
                driverList.ApplyTableAppearance(_v269Appearance, _nav.LayoutWindowWidth);
                return;
            }
        }
        if (root is DarkProgramList appsList) appsList.ApplyV269Table(_v269Appearance);
        if(root==_v269AppSortChoice)
        {
            float size=9f*_v269Appearance.TextPercent/100f;
            if(_v269AppSortFont is null||Math.Abs(_v269AppSortFont.Size-size)>.01f)
            {var old=_v269AppSortFont;_v269AppSortFont=new Font("Segoe UI",size);_v269AppSortChoice.Font=_v269AppSortFont;old?.Dispose();}
            _v269AppSortChoice.ApplyOptions(_v269Appearance);
            _v269AppSortChoice.ItemHeight=Math.Max(_v269AppSortChoice.Font.Height+2,V269AppTable.Scale(42,_v269Appearance.InterfacePercent,root.DeviceDpi)-6);
            _v269AppSortChoice.DropDownHeight=_v269AppSortChoice.ItemHeight*5+4;
        }
        if(root is V269AppSearchBox appSearch){appSearch.ApplyOptions(_v269Appearance);return;}
        if(root is V269AppToolbarButton appButton){appButton.ApplyOptions(_v269Appearance);return;}
        if(root is V269ModuleHeading moduleHeading){moduleHeading.ApplyOptions(_v269Appearance);return;}
        if(root is V269StartupCard startupCard){startupCard.ApplyOptions(_v269Appearance);return;}
        if(root is V269SystemUpdateRow updateRow){updateRow.ApplyOptions(_v269Appearance);return;}
        if(root is V269SystemSwitch systemSwitch){systemSwitch.ApplyOptions(_v269Appearance);return;}
        if(root is MemoryRamChart ramChart){ramChart.ApplyOptions(_v269Appearance);return;}
        if(root is V269ToolCard toolCard){toolCard.ApplyOptions(_v269Appearance);return;}
        if(root is V269CleanupButton cleanupButton){cleanupButton.ApplyOptions(_v269Appearance);return;}
        if(root is V269CleanupDiskCard cleanupDisk){cleanupDisk.ApplyOptions(_v269Appearance);return;}
        if(root is V269CleanupSection cleanupSection)cleanupSection.ApplyOptions(_v269Appearance);
        if(root is DarkScrollableFlow cleanupFlow &&
            root.Name is ("V269CleanupActions" or "V269CleanupViewport" or "V269CleanupDiskList" or "RepairActions" or "V269RepairViewport" or "V269SystemViewport" or "V269SystemUpdateList" or "V269StartupViewport"))
            cleanupFlow.ApplyCleanupSurface(_v269Appearance.Theme);
        if((root.Name is "V269CleanupActions" or "RepairActions") && root.Tag is Action fitTools)fitTools();
        if(root==_diskCards)ResizeDiskCards();
        if(root.Name is "V269AppsTableFrame" or "V269DriversTableFrame"){root.BackColor=V269AppTable.Colors(_v269Appearance.Theme).Border;
            root.Margin=new Padding(0,V269AppTable.Scale(10,_v269Appearance.InterfacePercent,root.DeviceDpi),0,0);
            foreach(Control child in root.Controls)ApplyV269ContentAppearance(child);return;}
        if(root==_search){root.BackColor=V269AppTable.Colors(_v269Appearance.Theme).Row;root.ForeColor=V269AppTable.Colors(_v269Appearance.Theme).Ink;}
        if((root.Name is "V269AppsPage" or "DriversPlaceholder")&&root.Tag is Action fitInventory)fitInventory();
        if (root is DarkProgramList or DarkDriverList)
        {
            root.BackColor = root is DarkProgramList
                ? V269AppTable.EmptySurface(_v269Appearance.Theme) : palette.Content;
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
        if(root==_log.Parent)ApplyV269AppOperationChrome();
        root.Invalidate();
    }

    private bool _v269AppOperationChrome;
    private void ApplyV269AppOperationChrome()
    {
        if(_log.Parent is not TableLayoutPanel root||_status.Parent is not TableLayoutPanel top||
            uninstallCancel is not ModernButton cancel||_nav is null)return;
        bool active=_nav.RightPane.AppProgressChromeActive;
        if(!active&&!_v269AppOperationChrome)return;
        _v269AppOperationChrome=active;
        var palette=active?V269AppDetailStyle.Palette(_v269Appearance.Theme):V269Palette;
        int S(int n)=>V269AppTable.Scale(n,_v269Appearance.InterfacePercent,root.DeviceDpi);
        root.SuspendLayout();top.SuspendLayout();
        try
        {
            root.BackColor=active?palette.RightPanel:palette.Content;
            top.BackColor=Color.Empty;
            // A descrição corrente já contém o estado publicado por Status; o log real permanece acessível.
            _status.Visible=!active;
            root.RowStyles[0].Height=active?Math.Max(S(36),cancel.Font.Height+S(12)):60;
            top.RowStyles[0].Height=active?100:55;top.RowStyles[1].Height=active?0:45;
            top.SetColumn(cancel,active?0:3);
            if(active)
            {
                cancel.Dock=DockStyle.None;cancel.Anchor=AnchorStyles.Left|AnchorStyles.Top;
                cancel.Size=new Size(Math.Max(S(100),TextRenderer.MeasureText(cancel.Text,cancel.Font).Width+S(24)),
                    Math.Max(S(32),cancel.Font.Height+S(10)));
            }
            else cancel.Dock=DockStyle.Fill;
            cancel.BackColor=palette.Card;cancel.ForeColor=palette.Text;
            cancel.OutlineColor=active?palette.Border:Color.Transparent;
            // O HTML não contém console técnico dentro do acompanhamento de Aplicativos.
            // Ocultar somente sua apresentação; a fila e o texto do registro não são limpos.
            _log.Visible=!active;
            root.RowStyles[2].SizeType=active?SizeType.Absolute:SizeType.Percent;
            root.RowStyles[2].Height=active?0:100;
            _log.BackColor=active?palette.RightPanel:palette.Content;
            cancel.Invalidate();_log.Invalidate();
        }
        finally{top.ResumeLayout(true);root.ResumeLayout(true);}
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
            bool accepted = await _nav.RightPane.ConfirmAsync(title, description, token,
                _v269RepairCommand is not null ? V269Page.Reparo :
                _nav.SelectedPage is V269Page.Sistema or V269Page.Inicializacao ? _nav.SelectedPage : null);
            return accepted && !IsDisposed && !Disposing && !_betaClose.IsClosing && !token.IsCancellationRequested;
        }
        finally
        {
            _v269Confirming = false;
            if (!IsDisposed && !Disposing)
            {
                _nav.SetPageInteraction(true);
                if (_nav.RightPane.Current is null && !_betaClose.IsClosing)
                {
                    switch (_nav.SelectedPage)
                    {
                        case V269Page.Aplicativos when _programList.SelectedItem is { } item:
                            PresentV269AppDetails(item); break;
                        case V269Page.Drivers: _presentV269DriverDetails?.Invoke(false); break;
                        case V269Page.Limpeza: PresentV269CleanupRetained(); break;
                        case V269Page.Reparo: PresentV269RepairRetained(); break;
                        case V269Page.Sistema: PresentV269SystemDetails(); break;
                        case V269Page.Inicializacao: PresentV269StartupDetails(); break;
                    }
                }
            }
            if (previouslyFocused is { IsDisposed: false, CanFocus: true } && !IsInTray)
                previouslyFocused.Focus();
        }
    }
}


// Catálogo nominal local, pequeno e auditável. Não usa correspondência
// parcial de nome nem classifica CPUs OEM/ES ou variantes X3D como X.
internal sealed record V269CpuNominal(string Microarchitecture, int BaseMhz,
    int BoostMhz, int? L3MiB, string? Socket, uint Cores, uint Threads, string Source);

internal static class V269CpuSpecCatalog
{
    private const string Release = "https://www.amd.com/en/newsroom/press-releases/2022-3-15-amd-launches-the-ultimate-gaming-processor-brings.html";
    private const string Support = "https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-5000-series/";
    private static readonly Dictionary<string, V269CpuNominal> Models = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AMD Ryzen 7 5700X"] = new("Zen 3 (Vermeer)",3400,4600,32,"AM4",8,16,Support+"amd-ryzen-7-5700x.html"),
        ["AMD Ryzen 5 5600"] = new("Zen 3 (Vermeer)",3500,4400,32,"AM4",6,12,Support+"amd-ryzen-5-5600.html"),
        ["AMD Ryzen 5 5600X"] = new("Zen 3 (Vermeer)",3700,4600,32,"AM4",6,12,Support+"amd-ryzen-5-5600x.html"),
        ["AMD Ryzen 5 5600G"] = new("Zen 3 (Cezanne)",3900,4400,16,"AM4",6,12,Support+"amd-ryzen-5-5600g.html"),
        ["AMD Ryzen 5 5500"] = new("Zen 3",3600,4200,null,null,6,12,Release),
        ["AMD Ryzen 5 4500"] = new("Zen 2",3600,4100,null,null,6,12,Release),
        ["AMD Ryzen 3 4100"] = new("Zen 2",3800,4000,null,null,4,8,Release),
        ["AMD Ryzen 5 4600G"] = new("Zen 2",3700,4200,null,null,6,12,Release)
    };

    internal static V269CpuNominal? Resolve(WindowsCpuSocket cpu)
    {
        if (cpu.ReportedArchitecture != "x64" || cpu.ReportedName is not { } raw ||
            cpu.PhysicalCores is not { } cores || cpu.LogicalProcessors is not { } threads)
            return null;
        string name = string.Join(" ", raw.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        // Aceitar somente sufixos conhecidos do identificador WMI.
        string coreSuffix = " " + cores + "-Core Processor";
        if (name.EndsWith(coreSuffix, StringComparison.OrdinalIgnoreCase))
            name = name[..^coreSuffix.Length];
        else if (name.EndsWith(" with Radeon Graphics", StringComparison.OrdinalIgnoreCase))
            name = name[..^" with Radeon Graphics".Length];
        if (!Models.TryGetValue(name, out var value) || cores != value.Cores ||
            threads != value.Threads) return null;
        return value;
    }
}

internal sealed record V269BoardNominal(string Chipset, string Socket, string Source);
internal static class V269BoardSpecCatalog
{
    internal static V269BoardNominal? Resolve(WindowsMotherboard board)
    {
        if (board.Manufacturer is not { } manufacturer ||
            !(manufacturer.Equals("ASUSTeK COMPUTER INC.", StringComparison.OrdinalIgnoreCase) ||
              manufacturer.Equals("ASUS", StringComparison.OrdinalIgnoreCase)))
            return null;
        if (string.Equals(board.Product, "TUF GAMING B550-PLUS", StringComparison.OrdinalIgnoreCase))
            return new("AMD B550", "AM4", "https://www.asus.com/us/motherboards-components/motherboards/all-series/tuf-gaming-b550-plus/techspec/");
        if (string.Equals(board.Product, "TUF GAMING B550-PLUS (WI-FI)", StringComparison.OrdinalIgnoreCase))
            return new("AMD B550", "AM4", "https://www.asus.com/us/motherboards-components/motherboards/all-series/tuf-gaming-b550-plus-wi-fi/techspec/");
        return null;
    }
}

internal static class V269WindowsPresentation
{
    internal static string Architecture(System.Runtime.InteropServices.Architecture value) => value switch
    {
        System.Runtime.InteropServices.Architecture.X64 => "64 bits (x64)",
        System.Runtime.InteropServices.Architecture.X86 => "32 bits (x86)",
        System.Runtime.InteropServices.Architecture.Arm64 => "64 bits (ARM64)",
        System.Runtime.InteropServices.Architecture.Arm => "32 bits (ARM)",
        _ => value.ToString()
    };

    internal static string Version(Version os, string installation, string edition, string display)
    {
        // Windows Server e builds não reconhecidos não viram Windows 11.
        string? family = installation.Equals("Client", StringComparison.OrdinalIgnoreCase) && os.Major == 10
            ? os.Build >= 22000 ? "Windows 11" : os.Build >= 10240 ? "Windows 10" : null
            : null;
        string? suffix = edition switch
        {
            "Professional" => "Pro", "ProfessionalN" => "Pro N",
            "ProfessionalWorkstation" => "Pro for Workstations",
            "Core" => "Home", "CoreN" => "Home N", "CoreSingleLanguage" => "Home Single Language",
            "Enterprise" => "Enterprise", "EnterpriseN" => "Enterprise N",
            "Education" => "Education", "EducationN" => "Education N", _ => null
        };
        if (family is null)
            return string.Join(" / ", new[] { "Windows " + os, edition, display }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        string result = family + (suffix is not null ? " " + suffix :
            string.IsNullOrWhiteSpace(edition) ? "" : " (" + edition + ")");
        return string.IsNullOrWhiteSpace(display) ? result : result + " (" + display + ")";
    }

    internal static string Uptime(TimeSpan time) =>
        time < TimeSpan.Zero ? "N/D" : $"{time.Days} {(time.Days == 1 ? "dia" : "dias")}, {time.Hours} h e {time.Minutes} min";
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
    private int _scrollY, _contentHeight, _wheelRemainder;
    internal IReadOnlyList<V269HomeCard> Cards => _cards;
    // HTML: overflow-y:auto; scrollbar-width:none; nunca overflow-x.
    // O viewport possui um único deslocamento vertical e não cria
    // barras nativas claras nos temas escuros.
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public new Point AutoScrollPosition
    {
        get => new(0, -_scrollY);
        set => ScrollTo(value.Y);
    }
    internal int ContentHeight => _contentHeight;
    private void ScrollTo(int value)
    {
        int next = Math.Clamp(value, 0, Math.Max(0, _contentHeight - ClientSize.Height));
        if (next == _scrollY) return;
        _scrollY = next;
        Arrange();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (e is HandledMouseEventArgs handled && handled.Handled) return;
        int notches = AccumulateWheel(e.Delta, ref _wheelRemainder);
        if (notches != 0)
            ScrollTo(_scrollY - notches * Math.Max(24, 48 * DeviceDpi / 96));
        if (e is HandledMouseEventArgs accepted) accepted.Handled = true;
        base.OnMouseWheel(e);
    }

    // Dispositivos de alta resolução podem enviar frações de WHEEL_DELTA.
    // Não descartar dois eventos de 60 que juntos equivalem a um passo.
    internal static int AccumulateWheel(int delta, ref int remainder)
    {
        long combined = (long)delta + remainder;
        int notches = (int)(combined / 120);
        remainder = (int)(combined % 120);
        return notches;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        int page = Math.Max(24, ClientSize.Height - 24);
        switch (keyData)
        {
            case Keys.PageDown: ScrollTo(_scrollY + page); return true;
            case Keys.PageUp: ScrollTo(_scrollY - page); return true;
            case Keys.Control | Keys.Home: ScrollTo(0); return true;
            case Keys.Control | Keys.End: ScrollTo(_contentHeight); return true;
            default: return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    internal V269HomeDashboard(Button refresh, Button details, Label status)
    {
        Name = "V269HardwarePage";
        Dock = DockStyle.Fill;
        AutoScroll = false;
        SetStyle(ControlStyles.Selectable | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor, true);
        TabStop = true;
        AccessibleName = "Informações gerais do sistema e do hardware";
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

    // Acesso existente ao histórico/operações: fica abaixo dos cartões,
    // fora do menu principal previsto pelo HTML, e conserva foco/rolagem.
    internal void AttachPaneToggle(Button toggle)
    {
        if (toggle.Parent == _footer) return;
        toggle.Enter -= PaneToggleFocus;
        toggle.GotFocus -= PaneToggleFocus;
        toggle.Dock = DockStyle.None;
        toggle.AutoSize = true;
        toggle.MinimumSize = new Size(115, 28);
        toggle.Margin = new Padding(3);
        _footer.Controls.Add(toggle);
        toggle.Enter += PaneToggleFocus;
        toggle.GotFocus += PaneToggleFocus;
        Arrange();
    }

    internal void DetachPaneToggle(Button toggle)
    {
        toggle.Enter -= PaneToggleFocus;
        toggle.GotFocus -= PaneToggleFocus;
        if (toggle.Parent == _footer) _footer.Controls.Remove(toggle);
        Arrange();
    }

    private void PaneToggleFocus(object? sender, EventArgs e)
    {
        if (sender is Control action) RevealAction(action);
    }

    internal void SetModels(IReadOnlyList<V269HomeModel> models)
    {
        if (models.Count != 5 || !_cards.Select(x => x.Key).SequenceEqual(models.Select(x => x.Key)))
            throw new ArgumentException("Cinco cartões do HTML, em ordem.");
        bool changed = false;
        for (int i = 0; i < _cards.Length; i++) changed |= _cards[i].SetModel(models[i]);
        if (changed) Arrange();
    }

    internal void ApplyAppearance(V269AppearanceOptions appearance)
    {
        _appearance = appearance.Normalize();
        var palette = V269VisualTokens.ForTheme(_appearance.Theme);
        BackColor = Color.Transparent;
        ForeColor = palette.Text;
        _notice.ForeColor = _status.ForeColor = palette.SecondaryText;
        _footer.BackColor = Color.Transparent;
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
            // Breakpoints da janela do HTML (gfwindow), não da largura
            // restante do painel: 940..1090 restaura as duas colunas.
            Control? ancestor = Parent;
            while (ancestor is not null && ancestor is not V269Shell) ancestor = ancestor.Parent;
            int windowWidth = ancestor is V269Shell shell ? shell.LayoutWindowWidth :
                (int)Math.Round((FindForm()?.ClientSize.Width ?? ClientSize.Width) * 96d / DeviceDpi);
            bool compactHeight = Screen.FromControl(this).WorkingArea.Height * 96d / DeviceDpi <= 950;
            int padding = S(2), gap = S(compactHeight ? 7 : 10);
            int width = Math.Max(1, ClientSize.Width - 2 * padding);
            bool two = windowWidth >= 940 && width >= S(470);
            int column = two ? (width - gap) / 2 : width;
            int ColumnWidth(int j) => two && j == 1 ? width - gap - column : column;
            int y = S(1);
            Point offset = AutoScrollPosition;
            for (int i = 0; i < 4;)
            {
                int count = two ? 2 : 1;
                int rowHeight = 0;
                for (int j = 0; j < count; j++)
                    rowHeight = Math.Max(rowHeight, _cards[i + j].PreferredCardHeight(ColumnWidth(j), windowWidth, compactHeight));
                for (int j = 0; j < count; j++)
                {
                    var card = _cards[i + j];
                    card.SetGeometry(windowWidth, compactHeight);
                    card.SetBounds(offset.X + padding + j * (column + gap), offset.Y + y, ColumnWidth(j), rowHeight);
                }
                y += rowHeight + gap;
                i += count;
            }
            var windows = _cards[4];
            windows.SetGeometry(windowWidth, compactHeight);
            int winHeight = windows.PreferredCardHeight(width, windowWidth, compactHeight);
            windows.SetBounds(offset.X + padding, offset.Y + y, width, winHeight);
            y += winHeight + gap;
            _notice.MaximumSize = new Size(width, 0);
            _notice.Location = new Point(offset.X + padding, offset.Y + y);
            y += _notice.Height + S(4);
            _status.MaximumSize = new Size(Math.Max(1, width - S(12)), 0);
            int footerHeight = Math.Max(S(34), _footer.GetPreferredSize(new Size(width, 0)).Height);
            _footer.SetBounds(offset.X + padding, offset.Y + y, width, footerHeight);
            _contentHeight = y + footerHeight + padding;
            int clamped = Math.Clamp(_scrollY, 0, Math.Max(0, _contentHeight - ClientSize.Height));
            if (clamped != _scrollY)
            {
                int shift = _scrollY - clamped;
                _scrollY = clamped;
                foreach (Control child in Controls)
                    child.Top += shift;
            }
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
    private bool _artRequested, _compactHeight;
    private int _windowWidth = 1440;
    private V269AppearanceOptions _appearance = V269AppearanceOptions.Default;

    internal V269HomeCard(string key)
    {
        Key = key;
        Name = "V269HomeCard_" + key;
        AccessibleRole = AccessibleRole.Grouping;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor, true);
    }

    internal bool SetModel(V269HomeModel model)
    {
        if (model.Key != Key) throw new ArgumentException("Identidade do cartão.");
        if (Model is { } current && current.Title == model.Title && current.Name == model.Name &&
            current.Specs.SequenceEqual(model.Specs) && current.Sensors.SequenceEqual(model.Sensors))
            return false;
        Model = model;
        AccessibleName = model.Title;
        AccessibleDescription = string.Join("; ", new[] { model.Name }.Concat(
            model.Specs.Concat(model.Sensors).Select(x => x.Label + ": " + x.Value)));
        Invalidate();
        return true;
    }

    internal void ApplyAppearance(V269AppearanceOptions appearance, Color background)
    { _appearance = appearance; BackColor = background; Invalidate(); }
    internal void SetGeometry(int windowWidth, bool compactHeight)
    {
        if (_windowWidth == windowWidth && _compactHeight == compactHeight) return;
        _windowWidth = windowWidth; _compactHeight = compactHeight; Invalidate();
    }

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

    internal int PreferredCardHeight(int width, int windowWidth, bool compactHeight) =>
        Render(null, width, windowWidth, compactHeight);
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Render(e.Graphics, Width, _windowWidth, _compactHeight);
    }

    private int Render(Graphics? g, int width, int windowWidth, bool compactHeight)
    {
        if (Model is not { } model || width < 20) return S(150);
        // Valores finais da cascata CSS congelada V2.6.9. Separar
        // título, nome, rótulo, valor, linha e borda da arte.
        var (card, border, soft, ink, muted, photo, titleInk, nameInk, photoBorder) = _appearance.Theme switch
        {
            V269Theme.Dark => (Hex("#303438"), Hex("#565c62"), Hex("#3c4249"),
                Hex("#ced4d9"), Hex("#aab4bd"), Hex("#33383d"), Hex("#dce0e4"),
                Hex("#ced4d9"), Hex("#575e65")),
            V269Theme.Claro => (Hex("#eaf4fc"), Hex("#9bbacf"), Hex("#b7d1e2"),
                Hex("#17374c"), Hex("#3f627a"), Hex("#dcecf9"), Hex("#17374c"),
                Hex("#123b5d"), Hex("#b7d1e2")),
            _ => (Hex("#0a2135"), Hex("#25445f"), Hex("#1c354b"), Hex("#e4edf6"),
                Hex("#9ab0c2"), Hex("#10283e"), Hex("#e4edf6"), Hex("#e4edf6"), Hex("#1c354b"))
        };
        if (model.Key == "windows")
            photo = _appearance.Theme switch {
                V269Theme.Dark => Hex("#232b33"), V269Theme.Claro => Hex("#d6e8f7"), _ => Hex("#102b46") };
        bool narrowTwo = windowWidth is >= 940 and <= 1090;
        int fontPixels = narrowTwo ? 9 : windowWidth <= 1370 ? 10 : 11;
        using var title = TextFont(compactHeight ? 14 : 16, FontStyle.Bold);
        using var font = TextFont(fontPixels);
        // CSS .hw-kv strong/.hw-sensor strong: peso 600, não 700.
        // Segoe UI Semibold é a família correspondente no Windows.
        using var bold = new Font("Segoe UI Semibold",
            fontPixels * _appearance.TextPercent / 100f * DeviceDpi / 96f,
            FontStyle.Regular, GraphicsUnit.Pixel);
        using var nameFont = TextFont(13, FontStyle.Bold);
        // A cascata de .app-window .hw-card mantém o padding 10/11
        // também nos breakpoints compactos; gap de 12px prevalece no CSS.
        int pad = S(12), gap = S(12), verticalPad = S(11);
        int heading = Math.Max(S(compactHeight ? 28 : 31), title.Height + S(compactHeight ? 7 : 10));
        int contentWidth = Math.Max(1, width - 2 * pad);
        bool wide = windowWidth > 850 && contentWidth >= S(235);
        int logicalImage = model.Key == "windows"
            ? (windowWidth <= 850 ? 150 : narrowTwo ? 143 : windowWidth <= 1090 ? 240 :
                windowWidth <= 1370 ? 95 : 320)
            : (windowWidth <= 850 ? 100 : narrowTwo ? 86 : windowWidth <= 1090 ? 130 :
                windowWidth <= 1370 ? (model.Key is "ram" or "motherboard" ? 105 : 95) :
                model.Key is "ram" or "motherboard" ? 160 : 123);
        int imageWidth = Math.Min(S(logicalImage), Math.Max(1, contentWidth - S(100)));
        if (wide && model.Key != "windows") imageWidth = Math.Min(imageWidth, Math.Max(1, contentWidth / 3));
        int imageHeight = model.Key == "windows"
            ? Math.Clamp(imageWidth * 5 / 11, S(96), S(116))
            : compactHeight ? Math.Min(S(118), imageWidth) : imageWidth;
        int remainder = Math.Max(1, contentWidth - imageWidth - gap);
        double ratio = model.Key == "windows" && (narrowTwo || windowWidth > 1370) ? .5 :
            narrowTwo ? 1 / 1.78 : windowWidth <= 1090 ? .5 :
            windowWidth <= 1370 ? 1 / 1.83 : model.Key is "ram" or "motherboard" ? 1.15 / 1.95 : 1.12 / 2.02;
        int specsWidth = wide ? Math.Max(1, (int)Math.Round((remainder - gap) * ratio)) : remainder;
        int sensorWidth = wide ? Math.Max(1, remainder - gap - specsWidth) : contentWidth;
        int sensorPad = wide ? S(narrowTwo ? 9 : 12) : 0;
        int bodyY = verticalPad + heading + S(compactHeight ? 6 : 10);
        int dataX = pad + imageWidth + gap;
        int sensorX = wide ? dataX + specsWidth + gap + sensorPad : pad;
        sensorWidth = Math.Max(1, sensorWidth - sensorPad);
        int nameHeight = model.Name.Length == 0 ? 0 :
            Math.Max(S(20), HeightOf(model.Name, nameFont, specsWidth)) + S(4);
        int dataHeight = nameHeight + Rows(null, model.Specs, dataX, bodyY + nameHeight, specsWidth);
        bool sensorGrid = windowWidth <= 850 && !wide && sensorWidth >= S(200);
        // A grade interna de sensores do HTML tem duas colunas sem column-gap.
        int sensorColumnWidth = sensorGrid ? Math.Max(1, sensorWidth / 2) : sensorWidth;
        int DrawSensors(Graphics? target, int y)
        {
            if (!sensorGrid) return Rows(target, model.Sensors, sensorX, y, sensorWidth);
            // CSS .hw-sensors: grid-template-columns:1fr 1fr em <=850.
            int height = 0;
            for (int i = 0; i < model.Sensors.Count; i += 2)
            {
                int leftHeight = Rows(target, new[] { model.Sensors[i] }, sensorX, y + height, sensorColumnWidth);
                int rightHeight = i + 1 < model.Sensors.Count
                    ? Rows(target, new[] { model.Sensors[i + 1] }, sensorX + sensorColumnWidth,
                        y + height, sensorWidth - sensorColumnWidth) : 0;
                height += Math.Max(leftHeight, rightHeight);
            }
            return height;
        }
        int sensorsHeight = DrawSensors(null, bodyY);
        int sensorY = wide ? bodyY : bodyY + Math.Max(imageHeight, dataHeight) + gap;
        int totalHeight = bodyY + (wide
            ? Math.Max(imageHeight, Math.Max(dataHeight, sensorsHeight))
            : Math.Max(imageHeight, dataHeight) + gap + sensorsHeight) + verticalPad;
        if (g is null) return totalHeight;

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var shape = Rounded(new Rectangle(0, 0, Math.Max(1, width - 1), Math.Max(1, Height - 1)), S(10));
        using Brush fill = _appearance.Theme == V269Theme.Dark
            ? new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, width), Math.Max(1, Height)),
                card, Hex("#282c30"), 65f)
            : new SolidBrush(card);
        using var outline = new Pen(border);
        using var rule = new Pen(soft);
        g.FillPath(fill, shape);
        g.DrawPath(outline, shape);
        // Os ícones são desenho nativo: chip, monitor e módulo de memória.
        Color icon = (_appearance.Theme, model.Key) switch
        {
            (V269Theme.Dark, "cpu" or "motherboard") => Hex("#ee9d68"),
            (V269Theme.Dark, "ram") => Hex("#55cbb5"),
            (V269Theme.Dark, _) => Hex("#65b8ef"),
            (V269Theme.Claro, "cpu" or "motherboard") => Hex("#bc592b"),
            (V269Theme.Claro, "ram") => Hex("#087d82"),
            (V269Theme.Claro, _) => Hex("#167bbb"),
            (_, "cpu" or "motherboard") => Hex("#e8a784"),
            (_, "ram") => Hex("#6bd6b2"),
            _ => Hex("#75c4ed")
        };
        DrawIcon(g, new Rectangle(pad, verticalPad, S(21), S(21)), model.Key, icon);
        TextRenderer.DrawText(g, model.Title, title,
            new Rectangle(pad + S(30), verticalPad, Math.Max(1, contentWidth - S(30)), heading - S(6)), titleInk,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        g.DrawLine(rule, pad, verticalPad + heading, width - pad, verticalPad + heading);
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
                    var destination = new RectangleF(picture.X + (picture.Width - w) / 2,
                        picture.Y + (picture.Height - h) / 2, w, h);
                    if (_appearance.Theme == V269Theme.Claro)
                    {
                        // CSS computado: filter:saturate(.8). A arte original
                        // permanece intacta; somente a pintura no tema Claro muda.
                        using var attributes = new System.Drawing.Imaging.ImageAttributes();
                        attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][] {
                            new[] { .8426f, .0426f, .0426f, 0f, 0f },
                            new[] { .143f, .943f, .143f, 0f, 0f },
                            new[] { .0144f, .0144f, .8144f, 0f, 0f },
                            new[] { 0f, 0f, 0f, 1f, 0f },
                            new[] { 0f, 0f, 0f, 0f, 1f }
                        }));
                        g.DrawImage(_art, Rectangle.Round(destination), 0, 0, _art.Width, _art.Height,
                            GraphicsUnit.Pixel, attributes);
                    }
                    else g.DrawImage(_art, destination);
                }
                finally { g.Restore(saved); }
            }
            else TextRenderer.DrawText(g, ArtError is null ? "Carregando imagem" : "Imagem indisponível",
                font, picture, muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            using var photoOutline = new Pen(photoBorder);
            g.DrawPath(photoOutline, picShape);
        }
        if (nameHeight > 0) TextRenderer.DrawText(g, model.Name, nameFont,
            new Rectangle(dataX, bodyY, specsWidth, nameHeight), nameInk, Flags);
        Rows(g, model.Specs, dataX, bodyY + nameHeight, specsWidth);
        if (wide) g.DrawLine(rule, sensorX - sensorPad, bodyY, sensorX - sensorPad,
            bodyY + sensorsHeight);
        DrawSensors(g, sensorY);
        return totalHeight;

        int Rows(Graphics? target, IReadOnlyList<V269HomeRow> rows, int x, int y, int w)
        {
            int start = y, compactSingleLineRows = 0;
            double cssCompactLine = fontPixels * _appearance.TextPercent / 100d * DeviceDpi / 96d * 1.22;
            foreach (var row in rows)
            {
                // .hw-kv/.hw-sensor continuam lado a lado no CSS de 940–1090.
                // Distribuir conforme a largura natural do rótulo/valor,
                // sem a quebra artificial em duas linhas para toda coluna estreita.
                bool stacked = w < S(48);
                int rowGap = S(8);
                int available = Math.Max(2, w - rowGap);
                int labelNatural = TextRenderer.MeasureText(row.Label, font, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
                int valueNatural = TextRenderer.MeasureText(row.Value, bold, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
                int left = stacked ? w : Math.Clamp(
                    (int)Math.Round(available * labelNatural / (double)Math.Max(1, labelNatural + valueNatural)),
                    Math.Max(1, available / 3), Math.Max(1, available - available / 4));
                int right = stacked ? w : Math.Max(1, available - left);
                int h1 = HeightOf(row.Label, font, left), h2 = HeightOf(row.Value, bold, right);
                int vertical = S(compactHeight ? 3 : 6);
                int h = (stacked ? h1 + h2 : Math.Max(h1, h2)) + 2 * vertical + S(1);
                // No viewport 992×659 com fonte CSS de 9px, a linha
                // medida no HTML é 17,97px (10,98 + 6 + 1). O GDI
                // arredonda a altura do glifo para 12px. Compensar o
                // pixel de métrica no padding inferior, sem cortar texto.
                if (narrowTwo && !stacked && fontPixels == 9)
                    h -= Math.Min(S(1), vertical);
                else if (compactHeight && windowWidth <= 850 && fontPixels == 10 && !stacked &&
                    labelNatural <= left && valueNatural <= right &&
                    !row.Label.Contains('\n') && !row.Label.Contains('\r') &&
                    !row.Value.Contains('\n') && !row.Value.Contains('\r'))
                {
                    // Evidência CI 45384308903, 96 DPI, 760x596:
                    // linhas GDI de 21px contra 19,19px do HTML (12,2+6+1).
                    // Distribuir a fração CSS pelas linhas; usar somente o
                    // padding inferior para compensar, nunca cortar glifos
                    // nem reduzir altura de texto que precisa quebrar linha.
                    int before = (int)Math.Round(compactSingleLineRows * cssCompactLine);
                    int after = (int)Math.Round(++compactSingleLineRows * cssCompactLine);
                    h -= Math.Clamp(Math.Max(h1, h2) - (after - before), 0, vertical);
                }
                if (target is not null)
                {
                    TextRenderer.DrawText(target, row.Label, font, new Rectangle(x, y + vertical, left, h1), muted, Flags);
                    TextRenderer.DrawText(target, row.Value, bold,
                        new Rectangle(stacked ? x : x + left + rowGap, y + vertical + (stacked ? h1 : 0), right, h2),
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
        // Geometria do SVG original (viewBox 0 0 24 24, stroke 1.8).
        var saved = g.Save();
        try
        {
            g.TranslateTransform(r.X, r.Y);
            g.ScaleTransform(r.Width / 24f, r.Height / 24f);
            using var p = new Pen(color, 1.8f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round
            };
            void Line(int x1, int y1, int x2, int y2) => g.DrawLine(p, x1, y1, x2, y2);
            void Rect(int x, int y, int w, int h, int radius)
            {
                using var shape = Rounded(new Rectangle(x, y, w, h), radius);
                g.DrawPath(p, shape);
            }
            if (key is "gpu" or "windows")
            {
                Rect(3, 4, 18, 14, 2); Line(8, 22, 16, 22); Line(12, 18, 12, 22);
            }
            else if (key == "ram")
            {
                Rect(2, 7, 20, 10, 1);
                foreach (int x in new[] { 6, 10, 14, 18 }) Line(x, 10, x, 14);
                foreach (int x in new[] { 5, 9, 15, 19 })
                { Line(x, 17, x, 20); Line(x, 4, x, 7); }
            }
            else
            {
                Rect(6, 6, 12, 12, 1); g.DrawRectangle(p, 9, 9, 6, 6);
                foreach (int n in new[] { 9, 15 })
                { Line(n, 2, n, 6); Line(n, 18, n, 22); Line(2, n, 6, n); Line(18, n, 22, n); }
            }
        }
        finally { g.Restore(saved); }
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
        "gpu" => "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",
        "ram" => "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",
        "motherboard" => "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",
        "windows" => "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",
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    });

    // PNG sem recompressão com perda, obtido dos pixels decodificados dos WebP
    // congelados. Mantém Original para verificar a identidade da referência.
    internal static byte[] PortablePng(string key) => Convert.FromBase64String(key switch
    {
        "cpu" => """
            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
            """,
        "gpu" => """
            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
            AAAASUVORK5CYII=
            """,
        "ram" => """
            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
            +vn/B6aGAAmZ9DU2AAAAAElFTkSuQmCC
            """,
        "motherboard" => """
            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
            """,
        "windows" => """
            iVBORw0KGgoAAAANSUhEUgAAAUAAAACLCAYAAAAQyEBpAADy0UlEQVR42uz9d7ht2VXeCf9mWGvtcPLNoW5V3cpJJalKEQnJMhIiCokgE61GBskJh3a3A2DjD7dD28bQDh+03V/jxsYBGxsDbWOSQEISEgqUVKocb04n7bTCDN8fc661197nnHtvqUoYmzr17OfWSfvsvdacY47xjne8r+DwXR5ACEH7Y/5zAO89wod/6w+Jm/0hF77n537dsfuH
            3PM7M0+35xfENX7/hX7s9r6v9/U675Hx+sz/VH09hOe6nl8IgRMSRwpIkPI630D9rwUfLrz0gIivyF/redQ1vm/5b/uhruun3PxtlOEL0l/9Pl/t/njv8cLhfOvuirk7LfZYH0Lusc4kL2T/tf8Nf85/Sa/2C91dQooX9gvXWo/q6u9v/n61rw2AR171ukpe/vh9/dEE+Otd5z4GKd/6HeGuvUGvK8DZ3wdX5L/la3AvL8j/wT70y5fg9+eHm8kY
            HQJ/7e1XH24zGQq41m+GbPB6guhuz//7JAB8UUmP3ONiXd+XmwAodguELwfGP7ABcD7lFH9ALpyL71uKL/U7dghAIpB+l9Ju18Dg5srAF7hBxe/zDEi8iJ9pIALxEgRX9/toPbq4Hv9gFnXzceiLCoDXwr+EENDGIZzFCz9z0X28Ee5aGEjECsQcWDj/RuZvqJNuDgMQ13Ux9npv17pwYv51t96jdx7j3a5/Zx4LEUIEDFWE36t/RsmAaVln8c4j
            hGi9Vk8EXZvrrnzAE8Ue17O9y50Aa8xM5ubDm77ue/7fesOLFvbpnbv+xT7/vsRsgNvrKUQrck4xNzdzvaWor5tqfqa+jk3mHu93/Tx7Y3x7Y3+7YX7Ta+FnflsKOYtNzgXG686Tr7qurueyi5c0ULX3ym5f3+vn90z8dwuAs5vuGlvAGhAy/I5OEN7OvDkhZBMg5l7ZTCCsX6ifO3KvecPcPMjpd9yA+r3MLzy/y8af/rs7GH3tALn7jakzRK1D
            gPNShkzOh5uy1+vy3s++Tu+QEShuZ387NpQTOzM+UdfQbkcw2Pn+xQtIn34vT/Zd8Ev/YlLAaywv53YNQM2amssgpXhpUaT237/aRp8mEK1178WLyoZ+X2d41xvwXmgG+EKCH4BUutmk3lnmDwh/zQBm53IKN/8EVy9JdnR55hZMfD/zgfDFpNXt9+RxOO9x1u2aRsx/qQ6E4ip/wzrbPJ9QYgavE4AUO59jR4IjxI6vOQFSyl3KZrnL8/i938Q0
            Gv+eL3zVelPO14fndazX+Yxhrgu85/I0u9zMmLm3A6CSMgAT8XNZP3/rIK3Xj3O++f6OA2yuC2qsndvgZtcFVl8DK2b/7o4E4gUGiPkKC/cCS+ovdQX+Egd3fb3BTxBaykm3S1kaKHIoHS5RYe8IMbej5C5P4OoacmdmuNsbFDtL511BGj/7h0IDVLRek7wKXOZnv9BeqK4uPe3sevIerN3jZoiZf5xLwnP6cAWtk+H3RMxonAWfhNfqweRVvJ4u
            ZG5+D1yqeU/xXxHvw/x1T1V4Lj8XGcP2mf33moFF/Z4HQDqd6f1xAQ0NZ56aew+7XJ92lSkE4K7dQ66q2bUY77cXrfvqJVaG+0Wazq7/+vpLERdLuN9W+HgfJAgDsgLMzptbmfg7e1zzJuKKucOufn1650p/ITFjpsx44Zm03ytgzscZKa4zUXez92M+flwrfs1XaHUyEZ9Cu2uAplmWYMoSaT1WSIqtAnRKcuwO1vYfZKnbo6xKyrKkqgxJoq+K
            G+3g6Vxl0818K2JlIdORO0o3KdRc5qMDLiLCoqwXsJQ7M8S9yt/m9cbSSzgXOH7O73ngqbkF28t6eAkmia/F6xaQZHC2YH00YpgtUZLCqIoHRRkDZL2Z44azFpwD48IGMwJUGhd+Ek5sLxFChXWv6mAa36vS+MKAB5lpXOoCsChkxNtkg+82h4NSKKWQSs4n8DNJofce6eXM9bGmmvuF+QPPzi7U3ZoUxiJtQkd2ydJFytKBkQjhqcxkz+60FyDS
            JKyHGAB3ZGBzv2tsOZOBO2NnskbnPd6JUBU4z2KnB5VBIkiVRimF8Y4Ch3egSAGHYYJ1ho7uYM2ATK+zvCLDa4qvQXpHWeVILxFOhr8/l5HNN93cjopJ7oRTaljGWby3OyCXq4WeHRjgDt6e3FkxubBnwt83M69x5/6f3UvO1cw9F3/WxWttm/cyDxNdLRW13uGxmKqkyEe47e1wvgiJEh4hj94XYpDYI0WyjmyhR3F5G2TCoTvu5tbb7mWcl5w5
            d558PGE83MYMh61AJfegC7SitxA1GHZ10Lr5V81tFD39XOwSjoRoguYX9ffrzeH9znKiHZn9LidOa5EkSQenBFaHIBOyvfjcCri8Drfcwcl3v5fe6jHG62NcZcntNpWrUD4B5xEubLj6MBLO49A4NFnaRxgoxwbldNw8IeCOinxmk9uyQghBqhOSTEMHnHDNQi8Li1ISKeVceS0RUmAKs2MTSN9e3HJmwVtj5g61uQ0t55sIbuZQ0lkClafYyhlv
            jEnokogu0gcoRiq7ZxboBMgsCRtZiJnrUAfEmUNWuJkNGW7TTOpMmqZIKZlMJgzWN6Go6KmURCm0AyUVDkcuPR6JLDXOGQwTvLd0sx6SAevPfwS2H4dkDJTTLL0+UZysQ8Iemf9ejSm5awa7J4zh7bUwr6vXuPMBsflbrb3T/hsz+28uI/St19zwX23r+XapGK9ViwsHUpF0Uxa7PZRUjMcDJqMxrizRwsfLLGY7ZQ1jvtOluLCOOHkHb/nyP0S1
            ucEXPvlRNs5ehLIEV4aLqtRsYNkrkO2k6L8wLHuvVLp+rnrBSrEjGLUxnenzqas8/y44X9zkVw2Are9XKg1/oz4YXCxJhYG0A6s3cPDt74XDd/HIuXVW3CrOeSZ0MN5AoRAGnDG4yuKNQSFQhCxvde0wxdgwGIypBuPwfr2MDWSJEL2GPuMFeOlJkxSXJhRCoEqPqQoqYzBVRbfbRcgQABUiBt9w4joPld158tZrxe4Cws9jwi4uypmsW7iIqdXL
            wzUBMfEh4LgFiVaW4ulzFEaB7JCkKRZz9aI9T6YBT8xmgE44rGyFEOF2ZFh+ZgLH4XJLp9MJjaTOPtzmRUaVCxdXKlKtEEJQaYEQGnKBtw7nF8BbCuvYv6+PTm/BXHkcJs8CV6YbXukYees/qa++/uuycLcM218VNL6+ACjUVQBseU3sdc8m5vzP1YHdu92wty/uQ4qYxjuqy7CuMtTCIqsHDtJf2s/FK5cRyeH7vJXxwGkHQAcOCaOcE6//Mt74
            5V/BR3/jQzz/sQ+DFVBVpFLi9HyXcTos5nZrLIZ6ZCZbajpaL5BT19BBanC8pqfgY2Yorx1g57OZ1o1pSrmZTuTsc86D6mImAEqkSEEqBBovJJUrw1hbIiFb4rY/99dRN97BE5dH2FKiiwTlJFYGLMhsGSh8OGwqG35XKBKpUFIjfcp4YwR5HgKqb70nP4dJCkfS76E64TaYAtx4BEKhtEIrjdICZ8E6h3AOVxqECU0ai0Cg9qRGOD/FvHbdeLtU
            BTrVrYA03cAyfq4zjcGjEkk3hfwyFGc2YGMEaRdUdvU9MFMCx+DqQ2nnAStdCyNsBZC6skh0KwC4+HWBVBLtJdVghL90CSYVqtvDFkW4/YlGSo2oJMJ5DBU4g0CCHbDSHzC6+HHKU7+I4CyKnDZ/sy59hdMIL2cz13ZGv1sp2Pq+ugbI5q5VSl41oMmdkMLcPpg/UOZf73wpuxe/9otuaGpQMsFajzMOZICJxOoqh48eQ6gj93kvWs0eJaGo6HS6
            5GcvcvTtX8eDD76OX/2Zf8no6acg6aFcHebiCXpdccvtegGvh3sodwP34yuQUu4IYk7E4F3fJHH9AXDXG94EFL8HUNz6NNHNaxVOonwXKROskBg7Rq1k2EkBJ+7i5Hd9L9mxWzk7KBkMLFJ0cBV0U4WfwHgjh0EVy30JzqF1QiIk/Y5ksGEpBuUUV/R2upl9OPkQAhKNSBOyXjy7yhjUpaSbgYz9HGOgnFSzGUTlZtknfm+emG+3SecDYANVuPqk
            iV/2Mwea0q0DSHlIZDjo/LQnog0MnhvBhW0QGTrtBfyuKMJhE/FdL8CrOhNQLeqKnwZcwItpxil8naGG33eqBbEI1+CxQkqUEHRTyeDcOpy6ADqdNp1cuCC9xQVKU2DMGIoJqC44R+JLepxlcvFXKM98GNUfo8gDvgUYbZoMUHp51X00u//cS96QdS+g7btzMEBe4/V+aT/q+9g0xVwsh1yoWrVVc+lyYUi7XfLz51m+9z6+/E1v5l//1E/DE08h
            el3ETMPwKtIAwl1XqbvrprpqDTx9LjkTwOZxqWkD5OoptNzlBe3xfRHfuNjrpUn8TBNEYxz4ypAt9TA+ww6HcNtdHPvW70Ucv51Tg5xRXqFkSiIVSChGYIYVGAlpP7xH48BbOommk8Dl82OwCqwOx1x9vasKdEz9FdDrkGUCoSEvwn3WXciy8L6KYaB+OOMQSsYDoJVBatlkIAJwppoD2sVcA8PP3c+57zUHUqhtvJjb3L7NZ6wXr2uec1JBJ4XV
            W/oMuinmmQ3MKAedhOvgWklw3X13hMNAepAqVGGRlBn4czISoFuCEXXTyYs5IQk5k9UWxnPohjXWS0N1/iLozrRq8FBW43gvPCQKvImvs4dljaM3v4lnt89jB5/H9hW6wUH1NUPQzkDidv1MXu/hv+fzu5ckfL7wwCdf4Nf3CjBzLBQZDhdpKnSNs0pPyOyUppjksLzEO77iD/PJ//qf4blnYfUgvirw3gIO6WSrXHBfxJtobZA5YNfP0VrcVZ5T
            tnCjefAznJzuKhd+J8gsvZrNbOZwLdHKOOefN2Qqc+8lAdXTFGIcas6Td3DoW7+b/s238/z6mHxSgsrwQoYgVMgQ/HKHEFngOVYVKk1Y3SdxJVy+mIeSWHVDFu1bDJheEiKolKjlBTo9wSQHV3nSVJDo8GPlAIpJ2WRjQojQ+ZWtw1CAUnL2UJAqYoyuhel4rgKKTi+Ot1N6SCzlhFezm0WI6d2tVXW8axZtmqTkVYXtJ/RuTMjLA5RntqCagNYt
            eCUefDZCLlKE56mz4phR+1jSCjFdzz5m0a5Fn5ntfIuwtoRDSph4WL35ABfzDZjksZkRu8pFhdQSqTROSygrJAbpBFYkbLujLN/xFWw9ehmqZzFazq5NYXY0jprAdZ0BZZYKOI/PXwuDd7s3Y15o0LzeOXIvm1cUOMIvLo/1zT6ewh/UjQ88iuUjPwRB5kp7SKWm3Njitte+Dq1TPv2bHwmbTbS5Yw5fz2AId40bcS2ejtr9V+JDIFqfx/KjHdyF
            uEqwFfNP13rIOCghEV5MH/H5BR5BZNcLGak3EkcoyWjRaULDRU3HpfwUUNVSY7FQjOHESW794F9AHbmF5y9tUxY2lE0qRViBmRjMZg6VQJAipcBbg15I6GVgS9i6WAQMI+2Ehoqpb6gJtawwyJU+armLw1FWHqklqRYoAePNknJUYo0ny9IGk2nTioSQKCkDHipESHpUuPwqkahEoFKJSmVYpK6K3Tbf4jDSIhC3UuaZLqDYMcInlGg1bEKl7IVv
            amBnLTpNMUVBYTVHDgiMy6jGAzD5dLH7VtfTx3q07i5618J1w/97fGhAximZsHFav9cu5euAKkMJX9kSn0jShS7l5UvxIIk8P1uGQ8wLUqWw1sQgHDb3sKzoryyilwTlpVMRVHaBvyR8kDUTfm7x+tnHNfdY+yFmP7/m7/o9DrY9nq/5vPWaG5z1enI+P7NvfXMz64eY+3yPR3N9wmsX3seZ+jBbX8cOXYN/ThiMBGEq6PY5eOAEjz3yTCypCN3e
            GZB4j0Nf7HUBd0+7xTW+78XebffwVuXVS1Z2p2zU5UyNr/j6G3EBNhMmPsV53dIlc9Of8yEo+lZjxE8m6M4CWkpMUYb6sirhzW/h5Le9j4FdZP3MNpWtIEkCvld63KSCcaC6iCRDO0E1KWEhobMAxRUoL42RJgGvcVVYaLIDzhZhs2mgE6gtqgeiUrgixKeijKRulYSH8xTV9ISt45KMJ0toivkmo2qahvMsokXdlJ5UBj+YBEDZChA68DxbQU54
            BdI38cd7gRQiJJUi0ByR09TeuhqX1U2T1BtAZEjgYgkrtwpyvYR74gxUAFnsvAPKzmQjIkni80i8jQSamlwrPGG+txU15iqBUK0IfDww8QIlBZWHtJPSO3GE8fPPhiCoFVgDVYE3OaVJkCrBSTCELDpN+mwOcg4cfC2jW3LcE78EMo8YqWRn5BAvriS8JlS1Wxp1lb+xoyu9Gx3nhRbMLwF66Xe/WjVTof6+YvHYD9URW3hPtT3m+M130F/ax6MP
            fx7KPEbN8Ng9Ir8A2grXEn4U1/799pqQ1zrR9kIUYwZCyDhCIIufS4cXgf3vYgNCtOgZMpGIRCOTBJFkoDt4mUSgXeKUwPg4MjeakH3V13HTe7+LS3KR9Q2L8xrZyci6i7hC4CcWxlUgNcsMrMCNJ2TLXbKuYHQxxw5KBEnsc0RitwA/2YTEQS8lXerSWdRIJNaAmVj8REAVcTAvWrdN7poXN/OkU4C2yYDad7uByFQslXUIKkknA6nCOrFVJGzH
            7K7OMuuNLaavy++Wrcg6MIlZGNFNKZpWGoyXLC4k5CWwlYcXFTPXGW1ET8z7A7+vgQ7ELqe5b61v0cI58TMTRknMmA0OpKPb7zCZjGB7M66XSCqPWajQegZREMLhvGU4KVlZXSYfX4bNi5CAljaMtomrZRg7D/j2Jbzm2PQXK4225/68Zgp6HY+XtglyjSaxa8BBJwKP7viJG9geDWBjE5L/QQWuIvF0OilS71MJPo3TETWoX8XNE5RvjAdvygDw
            WUn4Qsw0FrthwWoB3T5861dx6zu+jrNjxeDMBtCHLEMrha80blTCyMVmBlAWYBzZvmU6EoYXgIEEqfHOgpY4TOwnOOh5WFlkdVWQStheB5t77KRCeBkzW7/nQr3WbOxeuUezseLklgvJU8A8tUIuqDC0slVAAcI6hHE4FRVURP2yRIuwK3bf39YipNqZbAiPkJ6iKtCdjKWj+9i+sA2lieW22JNX5oXAyUACFLGk9TUZ19lpA6Wd/Ugxc+577/G2
            njCSGAf0O6j9a9jLV8BYhAoz84iw2JypkDoJs984TFmitcCMLFWywOoNr2XjykVwp0l0gRGOl3WLv3QfGl9NS0Ivod9Hd7qUg8tx8fweXnwvf8+DYB0fvK8zBjWLOUoJQuKRWCR2fR16i7C8DJ1lOHSSzuI+lvfvI1tZQC32KIVHL/WQa/upDpzgC+s5ftOAXCbt9dACytxhhiMoFBg9pYa4nOUjy1gLW+cncMWGZoe3oYso6pLOgnR0Dq/S7YIZ
            w8blEgwIK1EuZK5mF4xW+JfunBUx9ntBKFdrDFCF/88OZhSb4Ic5vnSzAaWGaURodPl6tE/PpSASfAyC8/CJLwwi04xGOSvdDuxbgvNbAX90Ei/mGxiORlqiEdtW03JTtjPTdikl40sXAcOLVUEDD8aSf30wYPXAATYurcPF83gLSnisrZoDTig1fZ5qgpcJKl1kONji8IFb4dj9cOpcWBZe/sER2fxvEgDrTkvNHXCSEos3NuAX19BM3SEHtYNW
            8kKD3gsNgl980JQ+JLjOhTQmS1Mqb0GpEGJMBT6SbVfW4IYbEbffx9KRE+w/egLng19HojNK7xgXORNTsrC6jEkTtscFk0sOP1BgOkidoDyUk0hzGVWITgcpJXYyQXW7LB9aYDyBfGMEBYEGY+I1rgwkPnRDVnocOJww2YDxRSjyKmxCp/BCYhvMXtIeiPcx2/L4XcujeXWg3eSGhBQzVZmQMSbELLChHEiFdbCyH4p+h8mlAoaTcD2VAusRSiGd
            RymJ9T40QMx0daJcUwZ7b5Gtjq0FUBJvHALBKLfsv2Mfl4sJnN3AewlZOhNAnE8ROtBeZIQ/wsBPjOL1G6hBTl97q4Ts33s1vW5e4uLL88LjrAehKaxj6fBhts+fw1eTiDOqEEQVuGKM1ClSenymcK5C+gRsxubAs7T/LrbPfYZqNIaejGn2yx9fVE51DeK0DqWbDx3FSElxyMDwc+4PwAWyyESSS/DVZEqf6Pfg8EGye1/Hvhtv5+Dtd2M6i2z7
            lHGlOF+BE5qyyvGRR4dV6OVDGCGpCjATA7kHmyB1QgIUW1FyqdQgewihsMMBortIvw/5CPItC3kA+hvgzcbMT2vWji/jE7h81sLYIYwMaL6QU71F0f69Pc6kXRgrnqn+nW93aFvdD+98hBFFwOrrcUo8U+ZCmAgxzjG0iiyD/pGM0aUUhnmADhBh1tYHmbF2YPWCmFLaaZolRMQ+Q+krlAxBTji8AINhJBTJ0X1UF65A5XYc4N5VYFWgMwmBlDEI
            1s1iHJDEISUX8ETnIhvBI5qDI7xRF9NAX4XrghKYwiOzLuzfB2eeC3CmlvgokNBcdi2RKmgABlhAkU8MvYX9dA+/ksnpCfiNKVO9VcK//PFSZoA1wRMJwreyuHlp9V0CiHiBoOmXrJyda1NeT8dJOApyfGXDyd/pww0n4RUPcOA1r2ffTXdhXJ9JLnhuVFJcsYGaYsCUnsrlYdMLH0Zskh4aSTkBW7gQ/AASTQKQgx+USKdxhPlgV+WIpS5LCzBc
            Bzu0kDugG+gu1gVqSCcFDfuOwGQE48sWJg4qjXACiQrNnLq6FC6Wp9X1Z8oNjUGGJWH9tCSc767FajQIeDqEVuEvOHBxsFwI0InH2JJJmZKlsHhIME662CsVjHN8ljTqyuHehTK0HlVr8wx9rYoTcUPvI3crVrBeOHJr2b/a5VJfw8YYSdriwYWMzuPwXoGqKVDTbneQc6rwSsXgZ4Hg0CdqZW4/vca+JnYLgXAC7QTWGtxyhjp0AHv+NMJWoYno
            XQNLBoqiIEkF3ji8K1EovPVgMpb33c9kex2GvzOniCWnGfZ1YLgvf1wzANaBzsTdE1ps4vdE/vyla3fvjpPI1olZB3M5xfYUMJ7A4jLc9wAHX/0G9t37IOnBE5weVzy6XsFkGDG6Ll2lKPOWQohUUBUhM8g6LC3BcByan6EG7YCWJISq1U8s2iu8lyFwWkO22GGhD1vrYLfGYBSINGR0eYFXoJKMbAmSHly5DEwslDaQtl3Imnwc/ohYe6tElbOS
            VTsmNXY5SIRvqGhBSsVP+W/zpbGI6osxK5RCoLxoGsiuCuRqJxyllZQeemtgSRibChvVmnzssHvEdPAjynRhpzyymq7kvQ8BmkgY1GHKxJcFdHqsHTnE+uaTOOFao2Qulq0+BsKgpI0MnM9I+YxB0EZYVoe47AxCJHgCTycMMNlwErgpZ9A5AzKIRmT9PuOlPm59HahQcvYg8dYDKUKFIOi8o9vJ2NzaZHVplZVj97P55NNh8SjiqKNvBfOXA9iL
            DoD15IIXpqGRCP/SBa1r6Yl5qV8YRthS4BUelAsliY3MvVCSyViexE3sHcqDFpJiUoSycHUVjt0A993Hvte+iZtvu5vNgePMpTH5E5uUhYIki0xcDZWisuBLcFKgFmPZmymSxS6Jgq2tZt+QSEGaCopxgFJ9Cb6UeC/CJukEqaeuh8EZMJMiNDsSAcaGo6mv6fYVvX5obG6fsyEYRKzW+1CCilq007sAWc1IeAl22lbJqx8okeiNcbE3IEMwameE
            M2rJsvHrcPF+ihgcLWHUDB85fQ4mEpJ9IHUPd6HA25ANe2MCKVmI0PBwMgTahngcCMWhsRGD8vQmR+UPzzg3dFcWw1yujRBPe3yyqRQsTrfpF+0OdRC9dXVzChGTLjkNej5o+PlQx0bqng7yYrlAqi7dA0eYXD6H0ylSm9g1jpM0DmxlAlleVTjrqFyJ6HTYrizdZI1k/x1U50bQB+wkAqRuVjJq1+qGmSpub5cDdf00gP8+Ma7dK8UpBtjSzcKA
            V2Fk0v331Xp3e52KCsh62EmO3RqGZsa993Pw1a/mwP0PMlo9xHol+Z3nh7BVgOwjRIcs6eG8oHJVs22cgKwLOoNB5SAViDTFeKhG8e/ZsA+1BlmAzcGXUdHFBlkrudglXYAUGF8An4Miw1ahAYMPIgCdnmRhCTavQDXMA8HYiQaUD8KQtjGr97uJNey6gsVVm1q1tXDoVIposi7qsYw4/VOnmTJS5kKgEm7q/eKFD1JWRPFWScDIXOjrpKtgZIZ7
            fgS2M71/0gcpeOlDGarq0lPMxu5afca1yK1eYhHYTMOBVbi4hfNJk/2hwrhjwP1szNTr8UdLIwgrYwpbY6haBizOETFHGRszLuDlMnaO46ikM+FAThcWmSwtQjkKqtB1Wl0rhlcekSYoqfDOYwl/26IpfZf+6i1sXj4Ho/OIrsbXDREZaVwvl8EvRQl8HbjQf2uMj2nm18YfnQoD9QIdMrWoIossY5XaxV3ZgANH4Q1vY+m1b+Tg3fdidJen10fY
            i5aqFOC6qP4yzkqEE1TE01+ocFonJc5q0JJRAfgxvf4CZekwuYXSgU6RhJEzZ2BSgp/4OD1YheRhfw+pwY1hNAI7qkhEQiqhcgJjKsgS9FJQe7p0wYfur1fhDZvYdqyntHyLs+uvt/N+jfsdx7waiaB21levhxggmq6xj65kzeRIxLvwkd7jmjFDF9UIKgHdFRhvpnCpCPzLREcenpm+OSdAiYj1+VmZr6YD45tut3cO2U1YOLSP4YUr00TJS1yc
            EJEIHC78mnNBQdureIIxbbzE72NV2C7Szoz7eRU4fW2VcgBnHU5BZ3GRrf4yDNbxIouxdtoJ8TZIXkkZZOGcdSitgvyYTEiXboGVC3D2PCJxUd0m6klCC79/+eOlCYC+JTntd1N08ddfrl7z+/KLe56ZJkw4cetxtOAFEqSe0DJQE974Vm5489tZu+8BLqkuTw0L/LgAtYiyGT3dwTuBLS2BsxpLaA9GTEsdgMnFTVjo0N+3QDEqQukqg+Yfk4Kk
            00HYYJniJw7i83kpQRq6GYwHBrYdGIVSGlNZvBUIJVlYkKh+6J9sX3KQl1DaEIxUOlU2tbINwc0GwOt2UN8DA/SuHoqdHScUbnrLXLzG9YRINGBqxJkljRS/wCOkjORfiaznfQvHJJN0DydMNiqECeNpkVgXZaXiSFuUoEeoMN7mW7ikawVB5xA6jJvJLI1B1E6DpXNNWVtPhTjnCcV6XHouZsL1GF4LEwzXRU2vg7V4pfDWIVVwRPQxA66sxaYa
            0e/jkzRCF2Zu3WuMMSTJNAucmikpcpOyuP9WBlcexeXPh1K4JaP/cvB7iQLg1bvrcq6mnC2ZZvaV350H2NAyriWpPfP33A43rdmfl3gjkDoB7xCJRsuMalyEP/bK+1l5xx9m5ZY76fYP89TZTcbG4FUH1AKC6OGQe1xhqQYlie+E+dmiCvvGGjAxG3QTONFHLXfwFZhBFDNw4eBI0ozEB21SPy5hYsl0lyIfkx3usbAMV57fCjJXlQwCpyIJWnxa
            sLQaErzRNvhJGcqvyC1D6bAHTVBjEa4WtZiaOjWNiWvNP/mrQ7wiTlGEGBjndmUz/jDNAiP+KoVouIB1o5ba4ziWrp5YDscgKCB0t60h6WrEgR7jp7eh02uV2HFR+QgL1EIBOjawlG9KZuF9aGB4qKxBWkmnm4V53O0R9LpB8DBOf4QufBTsjNKJvp6KUrUIRyxrxSxFJpTCteWCmxmVC1V5UNax3mBFxtLafrbOPgdFjkpSnHARx4wKNE4GorcI
            GoPW1Otesz3yrPUPIpaP4q9cjAfP3rje1CNnFvNyf1Aj3LVH4a6WAcbpkN9TO0R5/RlnPL2d84jFRXxeUG1twCse5MaveCdrr3ol57OUi0JjtkqcXCBN+6GiMbHKmkA5KGFYQO6pJtuBd2pjd9ERR+EKxE37WT2esr6VMx5GodKa6aICdjoalPi8gjIEjLLKOXCkxwS4cjaHspaSCiWdzQtUr8/SvpDs5RPwkyqqP6vpJrQ+vC4TA0qbGlaXwe56
            qUB2bsNMD5xmLCzA/vHgqjPQusU8xbCEkA0e32hORkFRIeMLEw7hRPDIkMQgGJs10lBZjVoE1rIwyytjpiTjxER7p9t2p2ba+q79O6QUOO8pnA1TnGkMnraFGcQM1qFC11kEQQZXzxk3qkOimVJpMsJwt2N1HO9R7XFdd8tb17/0oEQSskZL8DARtRK1DTC897jYRGm8UVpuhZXTpMvHKfJzYE+9cKvKlz9eBAbIvIn3lypSz2YnUohd05UZnEV4
            VD/BovCDERw4RP973svx17we2V3j2Y0BeaHIjcMjUKKDsbGZNgQ7AgYOMZH4MpaWRTUlahFVTVKLvPMgvQOa0VaJ9hbTjbWSEyQyQXoo8yIEPysQWqO0JMtSjIXxeoEwHm9lwLOkDfPC/T5ZDza2I72lsmGGVCqEVzhjQ8bo4oFk6wrIT4VK50rgF3KPdnCha+dGGfA8gQtk3wi6hw5ZWwS1RZCOzRmhAsEYCy42NGRE3XzkB3oZ3pMATGmhrxBr
            CX5jC+E6UVEsCrSq+aZOW6gg8mhEYK96GQKrsxVOp5DqUAKbImZsbWkuF4QnZC3OEJVqbAVKhaBdi204EFKFuV5o8QR9I23l5Zw4iJBUHpTWgSdqXczaXY0wh66uD7aWtf2IlCIEQOHAlhgH6YETFKNTsHUuVvv65RG5lzwA+pBxuZpdIOULwwBfUHZ3lZ/y00K7Em3/DdkIVXrhQGqskKEsf8vbePXXvYfJTTfx7LBgsjGCIqFLn77XWMAWnnJQ
            BfGBLQMDB07jrUSKDuBwnWjSI1x4bp2ydrKDXYGxdWghMCZEGa1VMCdyUBRFmHXVGpVoEpkEnygLmxsTEqcpSzttKkgNOmFhBYYXgXERgpt1IWOwPjiqOdU4VgkTzLbr4Cf9dLSNqHfmvZ9rlPprHG5q9v541yjX+9qXQvnIUY6d2EiHEdLPyOTVGmt13wIVBQucrNUVIyIYREVrsrUh4GjZsiTvSPyoDCOIog4Q0bqzHfx8o6bQBB0vg2UlUdTD
            WIvQCu9Nq6sdu7lB/SNMltgWkTLeH++Cf0RIdi1CKJyzM0GwQYRqdrMTO2x8DQKvdbjfbupJG4yqZMvo3OHcVGPS1xqGAiaFpdtZgf4R2HokWiDUlYT7ku/H/6EDoBdzaq8RiJ0Kfc5L1FyHUXktGnodDQ7ZqLBE7AKB9pBVYKSkUqrpSNbSRTKVWF+FNuprvpylr3oXt77qdVzYrti+WJIbASIjSfp0Kqi2oNguceMStiYx7ivodUh02sjeeQm6
            l0EcIXUK9EqQ6RsXQGWwSIRL0VKgrEA5SZUX+LyMgcFjE4XuwnhkEUOLL3yYMRYyEKd1RrbYAQfD5+vAF2+Dl1g3DXLeRLKucw1TqbbIrO9aTYMJAe/qPszX6oeIRibLTWXiXcDbagaMl0E2y0eZeRHxumZIg6jvVwcoGcUEXDSslzFb9AFDEzaMkQkpSFcWKAdboBRSaFw1AeWjhJac4pO+7jCHKORkEoKfJwQob+klKSUe62zsLruWRtTMC25d
            iKrpuHtHzAQVSItstAJlIx/oGz5elNn1olb/iqWsQGd9ZCfDeRsnZ2gZ2cdqAIctK5JOJ/yMcLEbrsE4ynHC0vJNbJ8JvjFkCVRjZKp3N0ny0yz0xQXB/97RQ3k9GeC8xHbdWlcxQ4iYjbuG+uxL2JIPlg1T+fKg3Ra7vKMJLPfhq9/OK7/jj3NZLfHQlTFm4khEn67IyC1U27BxBRhZyA04ie6soZVC6IadEdTAIrthNATMNiM5pn/XYUYSqprn
            JhTOSbQQISFxMBlN8FUV94ACndBbTANGOJjgXSdMEDgXOH5a01/V5GOw28U08DlPo2DgbJhGsD4IfNYkYxt5a87PrEvbDnStgCf89dTDYuaw8sJHZey67+EjBuanjax698s4kiYCgB8OTNlqqAXytI9DxkK3MtaIbYo4meNsGE/L+hllR0Fe4WpV5tgBFsLHTFLFOOYDJgohk6+Xal2Kx6a19a2SV8S5dzGHifpa5UVMfRYkeOuaLC94hwQt9Jro
            TUutui7Hm2w4XgMbi39aXE1fcwbFLLbk5ufvoyKTKxWy04dkGaqLIBWy05sKFb/88aXDAL+4CLaHz+d1nC9egFXBt1X5MhBKnaSUaSDC33Ena+/9Vm79srfz5DNjNoYFdBKU0GiX4XJwWxYu5eB7CK/wuoeUkHWicrwl/FwVGiK2JCjgbG9Bp4DX7MMvQ7UeeXw+BuLIZfMOKuPwNmZ2SYLSKVmmKLYr2BgEm0oiBQaBkJp+F+wQ7PokYmqRyGv8
            NBC6iLe5OBZmI/7nBDKapE9VWmYd62Y8eWHeEGKXM8vOJPECEYUVREslO2KQokFJprPCUZgA4UJWFjFCX3eMo9KAVzK8DyUbgdRQw4fDwduQzaX9iPnl45B1dWOmWYsgNCFFTfE/42IWpaf0G+vwtor0pVapGGOciIepqLvdkfZT00jrbnFD85EJXthAucLGLNFNmci+nk+W08sZ76f3LqrK2DhSJ+d4m1NPbWstqvbY9jQUHm9LtErIVo5QlOfA
            jEl6iqp4OYi9BAHQMRVF2KWF7CM7HnHNkvZFNUCIM7JCUOjpKvJCUqQakg7dL3879737mxkevYFPPHYOqhW66SJmEpq1k+0chhWUCugitAgmaUmYzjCj2JobT7ClxxRZSPHKQeiOpLB07zHYD9sjF0jIgXMRcK9IKzOVCSIKMgGl6KSaTMFo22IHo0CPqfxU2lxKEg3FAKrBBIRGESYGvPOBaOtit9LHwFdbV9RB0bmQUbW4YjtK3fmAd40DaL6j
            6KUIKsbtrqf0wadWRmCv7RMk4gYXtYCBi+WtnFJn6gBdW17WQTDigCJOX5iY8JJqUFVoXFQ6TGs1GSBNEJRCzHk31yW3C/w8K1E+lpK1yGnEH/FB86i5py2PEt++bm1tQBWvl5DgbLRcdNPuu4gz0TOK1jY2sEzI7H18EL+XdmcYGDiHrfedjNm/8wihMEawuHac4soTUA2ojP2DTHB5iQKga5FE44nqfdyU3gd3++vAAoSY4+fhZjNBMRcwm26u
            nOnueutxicJZEwRAHTDO4a47Ofyeb+WWt7yTJy4PuPTUFYRcotPrkm+AXwdGHqo0LNJEwJJkqROyu6KA0QAYeBgVqLFHWBX8OlQs2hZh8TXHyA6GZjBjGQJcvTijR7EpKyhz8AlCpnTT0J3dujIJ2QhJEBQTgWum0zCrXwzBjyuwGmFcI34SJjxs7IoKsCKUXrU6ce1j6nxDh5sGwLnSdz7g7cgAXXO9vdjt5+O9k6LpxLs6gZJimhE2zY/wJoQQ
            tU5o/LmYHenW/TZ+FiERNdPKN6U2WiAXOrhnL4Hu4YwPY5nSBVhAytCZlZG2IlpUrZobpC1J0kU7w3hjc3rAu9mlJ5xDRPtUKaYzwS42WXyU9WrogF5Mm0a1QblvianugH9Cya11MFJ30uOtQSkZJ/vU7l5DNQaPwFVVdK3zTEYla8sHIdsHxTnikOEcpi7mXsHLjY4XUQLLXYH1l7SzJKadXwAbN5dcXsQNRuAkyTvfzV3v+SbMkZv5rWcvgloi
            k110lTDZBH8ZWC9jNuMRSynZYni+7Q3w49hlrTyMDZDirMdPclhaDE7ba8ss3bwPdQCGHswk8l0bY+3YsTQhu0BKhEzoS4nPYTwJkx2oBOE8rjJgDQv7UiZjyLc8DPLYDYxudC52+9y0CSJ8DXvVWZ+YTkHEABgyLLd7ALxqyeta5bHfOeaIQ8iQcTdzqUQBVBn17oxrqDABv5vq1DX9hPpFylrYQbXcy+oSe8pi8QT8zQlJCcFtLhGBuCxUiDFe
            NF3u0ASJmVjlIJFTLpAIuKOsR+OiH4dsdW5lK9tFBmqKkDI0UZoXFbNwXGPyJ6K1p/A+Nnpqe81ZDHDevc0Zg7Nl5Cy2SMy1sUk7CjqPl2E0T8T7LL0ItCQ0E5tBZw3GCd69XP++BAHQ7dgkovbSFTPZ/e4Y0pxp1jyPz71Q4qAK7X23cRkOHWftOz7A8Te+jaHQPHvmColepuO6iAmU6w532cI4nv69hGQtZDZmDGbThYBXirBRTJwiMAW+nyAP
            rdBfhYlWpIdArcJ2DrYqkE4ilJ72g4wPm7msAk8vSelIhcxD9ezLKhjeGI8vClCKbKXHaBP8iJBSJp3wOlwgvtbTXmHiI6Ravs7ubB3M7GwjoyE9+1YLhBl1lp03aQ/EdT4A1uWcsHPcT9mikagG26sztzAHO7X08M0ER/zjxgbJp3Z3pmWhKFrByDhJp5tQdVLYKhsMburPLBq8bRpoTFg3bTtE56dZobOINnWlWdPB3a0+5H3j+OZb/iRTHxDn
            BYIy6mXqmP3FkdFAQJxNFiI0YGwJppqebjNKLXO2E7Es8N6jYgOowZ2RlDZF9w5gBhn4rZepLC95Bji/f/wXizHIPTak3IEv1qvSShnoCvkY3vjlPPDub2Ny8gEeGVRUG5cgW6JjethtKC+XmG0Dk1CC6G6XzmJQkTfbwHYJlQoPQxxm92Ehriyytk/S6cG5bU+yJMgljAdFmLslbO7AkghzngGgMlCEBZvIBGFguFGgrEIrjTOhOaF6C0DA+9go
            poojtmrwJOGiwKZxIWOss6tYAgVKx1x24Hc/iKS/WgJ4dYPqRha+jSXWU2+tUpG4AZsXUM/exiBo64ywDXc0Nm6hrBao2AD2U85gHcRcyAxtlEMUicT7KSWl1iVsj2wKa+L0RRIwNj0Ngt75gM/ZGltrBUDpInWy9Sa9Cs0ldKQEqBg81VQAoQmYvmWJV/P7XKT3zGZzUvhQxhoT8WzX+pm6bV0LP8ims46QDR9XxYPQSYmxmqSzgpF9rEujr83L
            OOAXHQBlLe3dBKPWkLXYmelNMTy5O/YgpttOetnwQm38eelkIIDWWBQhwAiVQF5BmiDf9z3c/zXvYlMscP5CRTX20FulI3vYK1BedvgtE60SCtTiAvtWYTCAasOHIFbWGZsjTTuUzgeD9+MrLK9CMoSzj01AW8rFBTIJhROheeEBbxDOoYTCVA4mFVSehcV+wOaHlnIwRsosLPzK4ypLlnXoJjDYdLA1BoLNZWg1uyhp5cPsZ0PXcEjrojR6q6Sa
            W9hiF7qLbLqFvgmeu4BKex5wTrpdcajGIY1poBKtPplwMXg5EUVWBU7GTkGra0wU1ZAq2A8IKRs+9cxESQws3kWJOu2BMnBnGumpQKoOB0j8+7aW65KIqOTibQkupSzLEE1dkCFLdS0/H0jIPmJ3UsmopxBJ0yJOj3gV525joKp9Q2SdGdqdvrotG0+URnuFLB0MhgHS0GGyRnoJlcG5MV5F3+MazyRBCI9ONEUexuY0QdjVG4nOlqB7ENYvQMfh
            xJfSM+R/7AxT78zYXKAl1BSG6LnwYi7c1Ny8Dn7TQBoMhWygc910kpMf/CDZ7Xfx+MgxGo5h0kOTIa3ADj3VuoUR4WRWgnSpR38BrqyD2R5HD1zRBBqtJeVgA3o9usdW6O6D9dOwdWkbqbPAvq+xtzj7LCK47rxDWh+Cn5N0uh1cDrYAm7tgml5ZnDNIkbCw0EEJ2DyXxw5w7DAbF4Ofb/4VcSxKAJgQ/GrsTPgW73IvWoufemn4mS7wXJC71r3z
            e+CyLXJg7Q0SpjpUk72J2PxqvEPqNSNjZiinGWNAzWKDRcRxuHm7TitaEl81MBqnYSJpWdSYnRB4G3X9bN2ws6EtkGoSBGVexNegEb4Mii11s0Xopvz03jeiMlMrzCjLJWqlGRWHx8W0LHbuGvHBkThHOdwOzbZYAvs2dRAPNloQ+GB/SpPNR+qRc5GCI7DWI0QC2TLIHsE85mXTpJeuBI5TCLYGsq/TWHm3beZEK4usDSPqrCaSpstRDlkGf+jN
            3Po//TEmyQoXhp7x0EKpSJMOCii3HXa9gs04G5tpko5mdTnIyZtLG4FS4OJGciE7MWUBviLdl5F2Yf15YBjB9RgAklo02Om4dx1WhgzBVR4l05DhVJBv52G91bNelUXqhCwJpuVbWwYKEySy8ljC2ToAutYm97Eyc801b66Na9WzDVDOrs2OekJtx+/ORs29750T18YGG9hCIYRtAmATYBtbUT0VDY04XZBTlM0I3IwjXU10bzC90EgKsnwuZPDC
            gaym0ltetLJS1+KbykCc9haVZGg824MBSgusDMKxxhqSpF6T9fWKWXfdzI1YHqK2iGg1b1BTbLFZz/VztXyE42uUDrQrGG1cgioPZX37YPJ1GTwHOTWEaTGL34pwUAqh6WQ98i8hjfcPWABsbYCY/flaE67eAC+ol9EGdesMsl1ayxAgVApCcux97+PIe97N07ln/VIO9KGq6GcZQkA5JAY/EzqtUqK6moUluHzRY0dF8Oodx7LYi2mtNhmgDu1n
            30E4d7qCzZDNYQgTHJ0UFXm09eSAr5WGvUR4x0JXUuYwuTyeqjLbWGI7yUo/wRvYuFTA9hh0N0yeKD2VtKqDoJdBz9JHrM/Gxe6mHcG6jPVuZt5qekD52c58exQuZJBtepLfcxzOiz0yxPnpxxquiKWfdLIxz5tpfvgqlohxUN/bEFxiaVzjdw0G2EjItERe6z9nTezgtqY0Gq2tmIE6PyVJU0tUVSjZQePxkwItFFYEc3LvKyDZBRYIVgJiBgVq
            n9y+1WV20yaO97WPWJPViZZvkvIOZQ2MtsBWiCyZ9bARbnf6TL3/QnkQrmMj0x9HCp2KroHjl0URXnwXePYGlGUZA5WIpcLVMsCrX31FEP0sG8tJFTTlxyXcczc3vP+DdE/ewaNncvJKoVwf7wRJloVJsC2HGxCaHSZuJi1YXobxMEqKk+KLiNU4EZsePhhxLKasHE7ZuAwMithVlUF92Ic5t3ITlvfH/kY9y2qCaRFCMxo5zCTQbJTMsEXI7PqZ
            ptcJVpaDzTIEPdmJSH4SVKJ9pH1YER8WZ31oXLho2NM67V0kvk7LXLMjQ/e7lLGyDUH52c2k2h150ZoiEZF/ORcE/Uwno1UOu9pUvBahbTUyZlP9qZJLJHbXnVzfJl7LELAaXC1mhbLO6oyFZDoKF54iUHKCWVL8peagMNCVLK/02Dh3CsoiytVHkUYNxlbh1/x0yiUoWptdgmArK5vx/6yjnQRj8OiY4cbDRnhkmrDY7cDkHAw2oZPiYpZYix2I
            OojXr0fsbG7N4BpCotMUaws6nQWKukF0tS14rUEF8d9ZA2XHsIZ7sQFwZ0Br9ACFehEYYI0Da4QQdHp9cmNCc8IrxNd/M6/79u/imaTDqa0cSoUmBStJYsO2GoHdsoHgPPBRdt6ycmwBayHfrEJGWLqYZUXsz9fguIFUki3C9iCWVW5a7oIEU2EvjFhXfRaOwySJ/rZeRdzbYcfTTq6dVEiZ0FVhgmAyhOF2FcpdmTaw1Qzm56OZUc0h9EHvrd29
            bbI066bZnfNX7cKL+bn32hxpLpzZNo44M2vqahRqtmPsW0RpaLhQM9Bi08EVU/BeiqmXtGx1TFwcG9MyYHgto6aZuWHnkV6jRcgXrQtKMMJVzfURQiC9ipL7KgaTiCUKi1hYJc8LzHAYfte7mAXPU4Jb1zhiktAOgmraOZbEYCsDBOACO0BIFw6Vqg6CsqlaZZS9qkZbkNdTRgmIjFnVLDl7H6OSTSDFq4YKFW6tAmcCbk0S1tvLHy82AO4cf7PO
            BRqGfPEdoMqWJElGMcrDF248wcq7v5Vb3/BOntoq2RIGiop0oYfJQw3qbDQZuuJgICPOG0/dlRTVge1zwDieznXwq5sYDWwWnswp6KxCteGgSFAmDV3pZgrDw9kB484i7mhc8NF8y03KSPdQSKeCdaESUQgBbF6F7jIqPE/pp7O9NVDuW8HPTDdeG2b17c7v1biTbrYpMmt5Oe0Ci7nMXMg44dOU1oEg7GUL3hBtorTYAXvMOvyJJiOrO71hXlkR
            RKDqhgfTn3NhcmIqvhKc7QLXwwd6inHoVKKFxNpauVZM37eMXWcZpmU8MgrHhvG63mKP0WQbxiN0Ix0/lZD3zHuKBMVpUeN63kXKTgyCvuYUmjgtmkRh7DDSFomc4eASGqGm2KjynsuXzgcMJxPT5/e1xL7aXSV9BkZyjSACOJwx+EQgagjJRrUdIa6t/PPyxzUyQBHKBYvHtAxurprhxSpvprsbuUlOOGTWocwnIUi8/o284v0fxB89yRfO5Yyt
            orvQg0pRDsfgJVppqjGwTsj6KtVyNXSsHEnZ2AA3CHLymKoJftLJZr2HdSth7CgHocQdnAWGcQqj3uROQtqHfII7O0Id7mNrzNPFTA6F9BIJZB2NsDH4jQuoDEKmYZh/YmLp3R52dy3Cf3hx0rWaGHVQarKvOQFYvwvNYi88j6lvbvg5E7NIP83MapkXKXZmQ62yq+0LvbvQdJyhJYoeNqqhAfjw3sUgKCOVRE7T1KazGj1ConSWV0HaXkrwSaS3
            YGLWHp/DRZ6eU8jYVXWNKYlCGIsbDCAvEFnb1MlNic0xmBDvaU0mF02cqTO6mMQ25kgEs/o4Jy2EanyCiZdb6wSHQ3kT7ArWz4a55iQDHyS1dpR0bS66UM1h6P0u96jGVSFgrfblIPbSlsDXS5WIsHaQhZIYGSguQqZ4U5EkBq8FZTmA/gLiu/4o937NN3N2W7F9zmBshtYZk1Ewh0YmSC/JPJgtFxseaRjkFRqMR64Fjp7bLoA0rMvYVRVxAYrW
            hJhwEj/wFOcNK/s0C4dXGG5sYMYGRBeRpMGTtgJEDzYG2DNjsht6FHkE1VUgUmsh6XUk+QDGE4MryrgxFRRRydnaqfFN0/k1UezABSA8vlYVX6eJtIyp11A1U97KKETho6pIqlXI0H1sVjVUjPgLKiquaB3ubpLMbiJrQhZaRtK3TGNMUFGIVjT8Ze9tY7LuBPV3mo7wtDsZA4uTRDmDZoJDCt0a34uZXt3Q8B6JCw0oQXDiKytym7By4ggX1y/C
            5jZSK/Aa66MAa62GbH3gwCUJmDG9AzcgJmWQS1OKylQomeGFDkK61Qip+iG41z2NOiD6Wlo/puQxCAYCY2zc1PaewiOVC7qDyjSSbQG2GdLppSz0FOfPPgHFdsweZfR/8c3t8t4HTDOqDCF0zOTEVIDEhTXk0NSyXeH8ElOpOFStnfrCs8AXKmbyYjHDa2F4L/D1CNTvQQC8rhcSXrzSGpcmFNUQNjfhnru5+7u/l+qOV/HYSFKWGZQiKPWa6BlW
            AkqQpTC64gNNpdKR3hRF+7DIRJHnNF1Vb6Kfqw9uYt6FgXlp6mAo8fQZPb/JmaTPDXd0uViusn1qDANHosNkm4gwHbYDlwuyfeC0oioMTHLS7iI9LXEGyokJZXHtcGaDJp03oQySwuOjTJaIbmNNBuYttad3XYnVbYVAHI/KJcJNcSFfO42FXyyNnRoGCWCph0gTsm4HlSR0Fzo4JRtZKuvFzEJz1mKdwxqPrwzV+haMchgOg5etShBSkYpWQ0AG
            9WnXOglr1RPh2oZXQTlHiGA+FBoeDqEi+ZsWbSVSWGTEQoPKhKWbZBRFhXUV6eEDlJvr2LJCqE5spghEFG/0SJJEUk1yWFmiv7jI5nAz0JB0UNUO/r3pTuHXOjv3EiGiqICfNqabLNZFs6XaBtlN271eGoSvYg8rHjTWI3BIM8Jvno90GtFIW4mG6d0qc/1OpXUpkyDN7+yUH+pEk/0577+E/hR/kDHAeRxCBpXbKQQV+4DxhC9rloOXoeNmxkHJ
            pduFd7+Xu77lPbj9Jzh1bkw5iTOlQiGswzmDJwXnUUrgJ8DGJKiliNA9lU5jjQVVITtZk3s22EwN7MfZWW/BuUAVwHqEz/C5o3hmwuVel9XbwC73GD0eJkqCPWJsNFYazud0j4Nchs3Sky0u0xVQjGFyZRA7uUQqTaCxTLO9iLPZiAk1GnBxFMuZkPy09UxduwURTnuEa4362qBWk4ow6pWm0EtgoY/OUvq9Hs4HOrRxlnEaKCGWOE7nxKxxto5G
            5tojOin9tRuYbA9w2yMYFbA5wFeWoqqQSiMzHQ4WGeWj4gb0YkpIrpsmLoxm4K1D1Vag0S5OKNl49jYtX+mC2rIQAeb3HqUVZVVSjMd0koSym8H2EC9MUy56Z0EopFAIl4J1JPtXqBJBNRxCUaG6Gmt3ioU2tpV++nmYwpg2o0JTxTZUJE8I6CIGbh9VZySieS3UyjQ6ob+8RLn9OFx8HkSF0Gqmkgq/L+MYX5sOE1kAKgmYbQ1fREpaPR7p6+vY
            WjuNWMVLldA0Lnf+f/QA+OI/XI35KY0bD+HIMRa/5Tu45W1v58k8Z3h2AEUPchUkrizBxhCBtYYk1WTA8LKFSQDKtReBLuIEtgoUgKzW0mzzx1yrszsnDYePAUr3YWObzYcuMVAHOHwTLN8JF56U2Aslzqdh2MkBucQMgF4oBzMB+ciTD/NQitvokFaPcJraoChmdXXm13SAq6k6ijWzWNqMqgtTQL0uKwXQSaDXQS91Sbpdkl4HpzxOBTWS7XyC
            NyY2WCxUcS7WRk6k7OxOW4qWB2VZIbOExSOHcJOCSZLgBmMYmHCQtZaIc3baOqjpLLX+nZspkHFSIFQ0d3FxlExGT5A4UyesCsIb3mMjRmdLgynL8DXlUStL2O0BVDmIBCGCGKoXFUbIwABYXKC3tMj2aBiugQdn7DTwt4UdGvP0tpmTmVHK9+2JkHr+z8aGUfRD9o3iTX1opSGwdzskieLy6WeC/llPxSDi92Cr1BxZNT3wkhQliV1wM+MB9fLH
            SxwA96roGy7oLh4e7Z8SpqK3uMBoaxDcvL/ia7n9uz9A0d3PFzZKStMLvLhCBo+HesbYehJE8L8wgdPHdgEmC+Y/VZABciZmW+OE0Xk4cCeMRioQjm0klgo9tZqcP7EaSaZFGFvs54ZsmAWWTsCBe+D8yOAvFTi5CNUEZEXloCehRDPcKBCm3qwC52T06ggCCdpbvPXYyjSbb4r5hVaylD5MfOhWN8FHInW0iETLEGs6CXQT9EIP3cmQ3S7GO5x1
            lN5TlnmYZ60FUhsAIsyTKgvSa5xX0dFMzXQZGzpJlKVy3uPKgiEF0kO2f4Vk/xrbF87BpfWQlZqIEWgVsqG5oIIIjmkzHzZWCb4K3V0vg/pK9PWoib5KCSprQAlkp4PwFjfJweboTorJEsh0cNozprHEdSr6BecF/RuP0+v12Lp0JWoQJuDKRpxBJRor1R52ki42c8xscxiBEDpmg9fCtVzAUjopa6sLbG+chvF2kOlqIby+kZueQiCNHH8s6ace
            ow5bjOJBL6/Kt/UxmXgpMr5rfV3O/R33gv+s2P1Annt+d7Wq9ItL066RAYpr9jx2yfwkpF1G4xxW1jj87j/Cwa/5Zp4uJcMNE8jGpQcjkFZG1v60w2U8SCGDEfnIQKFRJkFUIIyNPDwTJO1NgtuC7YvQ62rGqx24OAr8rFoYt7QgOo2KbihLKzBRxokULhvGT1fYXsLCGqSHepSDDdz2BpgJyV1HWTgAm1sw3t5EiW5jTBTI0aFEEja4g9UD9D7q
            2jVz8lEgT0uJdVUIIGU51cmr72VHQZaRLvRQHU1/aRGDC514PJPWc015jL5xMpPRRayGJWRZY4nBajG46PmdAVBM+cftM2MyHlJ0OizedJzxQh/75KmQ3XWSKHrqZjZzMD9nxwhekH2P/L1I2HXRelIIifAqVn8Ba0OKYJRuTJMVl9aSLSww7mawOUQJiYwS8da7MGmzvMLqgf1sbm7BpJjpyVyVRtQEo5oHGGw7w2CJn3aK29mZmH14EalBVoHq
            hA7w5Aqjy8+FjLVmOe4xViq8Q9TZZ7weDoESIgg6VGUTJES8l2GNCYSsieYvZ3EvEQbodkZaX3tgiB0bKBytOtyk+x/g5Le9j+6JO3jmSs7YJkBnullxOGwI9iag6y70/0iVCvy/kYFKIavYNHNEzMyGcssC24bJs4aVkx2S/SmmnzI6VcC2mzG8DsGPqMASAoWOB60dWzi9Trl/jWQx4dBROPXMJEhdHVvlhlfD5TGMrwzo6D5FHhoXrnKhpKyi
            Fp7xUDmMrfBlia9CGWrrYBDLX+PL4JlRt6qVRHQysoUeopPieilW1lNOno1iELrrSgXFYBc6oBiLsBKNB1crSnuMcA0fsKbaBLZN1HVUkRgt3Oz9q13/2hQaQGcdzHDISHpW19YYHyqYXLiMLwpEpzNLyxGuObHnMxBBqA6lMJiaquJClhoIzIHfZrwOf1kpRKKo8rw5gauqJE0TxPIS/vImxlm6SuOkQQqHFZAdO0Cy0GPy1CXITcAaxfVnJ94H
            Co+o+ZC7jACGa6da/9/O6mJQTzJ6aYIcnYGLT0f3vzR2xd3UdlDMWU+IOa6i0GgtMa4KQTQVc53OuYaJlEGlupbpeokiYs35fPG4onvhmVtr+MJf4x4K/2IDYHvUSpgpENyccrVUkIviMCqaVaeQddBf+dW88mu/kVOkPH15RCddRXtBOTJBCdjPZR+N2GT4f2tj9leBMKLR//TeY2Mgk7WBtlNwccimh3R/yv79ku7RjHVJ4H5JD8WkIcXWYLty
            daMGbNaH4Rh/dgxHlxGrQKeEBcuN9y9ycQjDKxsI1yHfylFJD289ogpYlo9qw94CrsLl60GAsNwCU0SlZx/oLK4EYZFakqYJMkvoL/VBjvByIwhUF4FzKaICtI5lgBQC63uk/WMYG4B+ZQsYX0K7AmEcxjuUmJYpMmKR0pqAQ3ofGiit4f/2RIUQgqyTYEmpRI9SdHGyj1xYwFUVuTQcvOlGThcGe+ECLnUxxw3H12zAcLP0nVpEe55O5aMXM0kY
            eXNloO0IgVaa3JSh4xybS7mr6C0sMVrIYGMjHNneRShFcuDQfjY2L2FHW0idNtqFTkQdQyUbcd+Z4CYi0927ps8ghApKKyJqCdaCBW2unq/FUOMEhzd4qUi0p6ctm2eehWJrOirX6P6L2WtVaycIgRDxsIqZaWUj9he76r7d9RcCp1RLVOFLLVfV8uXGtTLjWd7orgnUtQ4h4Wo5yRjoZOtw2C0xk1+KDNC0Fkfa0Ep0mkKSEkalBDIRwV/V2JCR
            vPG1nPjmbyfdf4LPb03ISwN6maKKdo6JDv82JUeYA051ijUeWxooPLoLzqV4Z8JcbGS5OBF4ZcIIqCLNxAuwPThvKTcdVzYlqzfAvlugtItsXXawMYQrk2DmW6UovdC8DiEgUZqqswTrFXYMk2XgpmXU7atsdmC4bqHo4XOJIMWOIknWC7SDynpcmSNRuOEFssUxX/+2O7n9gEVMroANpaJVOQhHt9ttIoIQgnGRh3EuIfBahYaGoOkgq2jMI4Sg
            9Gv81udLfveRi0zGOV/+utu5/4ZlOu5KZEc4nJTNaS08QYq/pql4j0qTHXBVXaIq4XF2jJX7ePxCn1/88COUpoLeMkprRqMxZeVZuvlGNvIcRmN8qpEtodbGpN47Up2QqjQYqiqN9Y7ShII+kgIj5uNY6CyhZYJ2jsuTAZ3FfWTdhPxiHmTIvML7BFtB2k/xa13GxWWkLXGk+JGjc/MRJsMtNs+dh1Kh+pIqr1BKor3DOk9pyzDRlCUxg6+bTGXo
            WCsVO9iuVsWM1CMVmluxPJ2SIRNUkpFqxWTrCsmioqo2EEnKaDsnXz8LFKFb35CkZUOFmUq61kredZMkWhWqjCRNKbbWI5AqW7PYIgR0JRiXk9acuEQKhcNMo7xwc7w6eV3YmBKtcqAO9k2wc7ia5xn/hikdQnqyVOCcwTmHUgnSiTD0pOogGIKnUiGOeO/pZ120DawBqRPyMvBOQ10Rs8Ad2OScidd8xuvlHnxFuVcAtK0ukwUfmOzhBDOgeyit
            qGwe5l1vuoUD73oPR9/6h7mk+jx/bgNkN7DS0VPgGLtzjlWANT6Uk6WDCowGX7kwk2sUnnTqKhYpHNp5bCQPW68DR7AQFAbO5xa1qlhYg/2HJGLfEsWBJYbnPe7iCLu1DV5hXYKWKcKKQEkxjnIE1Rg6R1apxp6tKznYbhBdsHUnt2xgnKq+RpkkSxNKpbnt5mXe/0czXnUEFulNm4bMmDE2Z0zZ+v+pBflso69+5ED1Uxkf+81P4Rx89Vvu4Tvf
            ntCjXxvENSO48+vZ+91xZtEaCGkM1DT8l0/Bb3zcsF4VCGtiBuqp8jGm0+HAyZNc+uxnghqJd82kiLe1+W6BTyYMB5dAhk6tSPrI/oHQpFKyMRDKdMp46wxuPCSVIQMs1gu0PBBnfyXSCbxQeCmZTIYUxUXYeIpR7mHfEegssrhY8oZXr7G8fCOf+O2Heezhp0CuoFQncAVFFW2D5+efTShNo5VnEEUNdyIQ0SPdxlcIbxGiDJzGWGpa55iMxjz4
            2rt44L6DSOXorK3wC//hQwyKTRJhcEIHdzcrWl7KglnxEbeTjJFIqjIPpjQYavUa4eeweV+GR9z0zrs4OTKdcnnJPmqKG6CkioBHCO4Wg5YCW5VUo+2QwKRdhNeBqqTSmSwx1Qlp2mE4GjLa2oB8C0kPnXVIe31K52YguBnpsi8FBth0XWrkWNQd4BzEGESK3Sqhuwpf+w0cf+fX0LvhJM9f3GJzuB46bt0MqRRuXARlZx+dzeoTqtaZciC1QHiN
            sYEY6hSgbZxg8KH0qRVU6vnHmpUgaq5KHBIfVLA9wa5rhpdT0tWESVXRWUxYukEwXlmg3FyAcxtwpcCJgJ1hSpA5vlijn8LEQ745jnaaNggs1GrAi5HoGDFzrbsoIcjShHTYwdrzDK/chN8P4/HsxJaTNqq9COomqXdTylj9nhq6l/exNA1BrfQwXh/h8lGYRChCxVhTD2vVLiOn5YiYazTP4yV18lZrGFRFaJBvbEA+WUd2DiFEhaCmmyjGwwHd
            Xh/2r4UftA4rwrxsIjRUliof8GVvvY/bbr+d8fAcrlI8+dQWn39mjJfLwe7AS6Tt4PMJd9yccNdNJ0jLTfYdOcZlscwvf/QRUtPH+c60hKoqZMfw5i+7D/XAKmZSsJ1bLk88f/JPv5lv+ipYVvC5J17JD/9vm3zk4+eRyX6kcqA8shRT7l88kmTE5UJxEiY+fDRFF0rEktNFjHOMF1ngq/oU3UkwVUWiS77x62/le78lnPVjD6d+p+Tp8hJJsoz3
            gtJP+Y3hlJrX+NuZoWSJphhvBdfBVDaue03QNC6sYVsGipUELaaNuOa5hJuZlNiLz3ddXdyY4QtJaOi1ZPgXF3tsX74AWvD2d34lr3jVK/FO8Av/6T/zxJNPx7ZOO8P0TEYDzGCTO+65j3d/zdexvLDAw488zr//uZ+bqk+JqTbaDA74EtMStdvlyZ2I4hdKB1b9rfdw7Gu/mYMPvplnlOL0xTFqJEnlAjbLwty/A6V61OpD3gucl1EPLlJZnKbf
            EUFKz2kqNImEiUogjW94XMUAF/iCrq6eZ7xk4qIwAlwCmwY7GDC5osiOLVNuQ5mFddc5ANm+Vfx52H7sQlwfOWQOkcFoBONiHKgeIghjqjRFJSrM2OtpcFEe8vEkEIXLknK0QekGKA/9BGRmEFaHDFAZnDCoqLTZphOoaCPp41C9i+TbaQCMJTKKxDk6QgMVogpunylgRdkoKvuWHUGidPSumD7nji6oD2WzFAIvLd6qOEFXIV2FFLYRafXOYa2j
            6qWsHTnA+vrlMNERI3k3yXCmoBpt8+53nuR974PNzRtY6cG//zn4c9//81ixHLm8klQk5MMzfPVb38Cf/mMpy/JGpIIvnINnH4NHtgwFaRDfEQYmQ97y5rv4wT8BK8m9KA2/+TH4//2bj/Cm10JPwfYm3HsrfPVXPMjHfutnkNaiUhca5wRfDu9c7KBWCEyU3Yrz4phpbKqbCY6mTBXIOMoWOJ6pVijpUBZ0FXK0sgRZPY8iR8sVHB4lguqPbyls
            y5apskMifBLGOesDwtnQvPN2JrWXvh6J9khXEagTBUKaGPz8HqXgi2b5NhJfAKbKZ2qbcjLi9rvv4oMfeD/vetfXceKGfRjg1HOnefzRx5E+mwmkgwunodvjO/6n7+LPft+f4b67TqCAX/jFj/KzP/8f5zY6u7+nPRomM9+/ThUrjWoN/jdde8Gl9e1AY/nq93LybV9HsnaEJ7YmjLwGUpRPYwUtArQTVZjtsEKIBC1BCUWv36UsA7+zyA3bw9hA
            MFHc0UehjIUMJWCsHG7LwijUZl6IwJgn+mjU3Tcb5bpq3olRsG0oyhF0FdlqJyiGy1B2ZsvAiR4MBiwcP0G2JhhgKa8MIZPQ6yKWghSTNAEiEhZMXkBV4UxoLGgvoLKh61lU6F6KjkFJxgDqJSgdwGvlZMzMxLT0bJ83QjVIcr2IwwRAfE6jcBVoqego6GrQZWyUaNGY5zSNkLiTAy4o9oR8GhtcBz6BbgrdRFJ4g3cG5ROEjY0Aa9jYvsJikpAs
            9alOPw/9HsoLinyDVHmocj7ziaf4lm+6hZV+CEz33Q69tGIyyVlZOcQgr5DWk/oRr7w9ZTWFXiwSDmXwqpNLPPbwFTr9owxNgRIOO7zE/TfcyW1r0SZEwcXnrvCF33qKz334Tdz1DbC0EtCZS6evUI1cwIUUeGVxxpF6i9IJhQeBwVZ55Aems0IN81mPcEgXDsIky6i8wltDkiq0VMHXPAlMplyAdxNKW9DrZeSiwkZeqiT8vFQKZy1KB5iorGzM
            DSTOQaeTBeOsfNT4MvtGEFYhvcd6i3QV48ElcBMEJjQomwmtebGFnb2SRm5NKbQCay3W2tlgW1cIZYnSgUqlhEfqMNZ45MgRHnzwQd7w2tfxjnd8Ba+85wZyA5vbBZ0so/ZQ6Xf6TCYTjK24//67efA1D/CV7/zDvP3tb6WvYWvbs9gXDEcDlJaUk4q032dSVM1qngY2v+Ne1WOLzPOW2537qwZAN2toTjdjfTJh4Z5X0/3GV3Pwte/g3ESyvj4B
            kaFIwE8JzU3AjntlcTEJWWD8+uAi2EkFeQFO4IyKasrhgLUlWBkiiFKSA6sptiPJOzC8aKbZHj5EUSGbqQtEcNnCRXFMq0MJOzYU29uw6OFwil5JKW1F75Ye+/YvsrkF61WYLV7ctxzUq6LIczUMZQaVjS5wFTpOd9RWF9jgISGtAJs0ZW0bW3PtbK+9EOcpoW1wZ57DXQ/MG4fVphYgjkMwPnL5ooCdkDGO7gUetzx4W6BkMNdWKBEVjJ3EGRFN
            4CW2vrHGUDhDd61HtZGBKdFK4EWFkJJ0ocsTTzzGhXO3cNtNAZ9f7sOBtQ4Xnh1jnaGbZuRbA2698TDH90HqIzfcw2oPHrz3MP/6Pz7HcHsT303xZgwMWVuAzIef3xjAxz70SbbOWX7q/34EMznJzSczPvybz/AvfvrXSNJllFD4qsQbg7SRiO5C2aYEdLoJUmqc0oyKIpZ4Zi5SqMgy0WRJMNXSUtBfWMDaKgx/RIWvmBziXQnOYHwRIQhBojTO
            VOANUmg6aUpZFdjKINEoHUbbkm6PJO1QjNYDd1WF5oLzFukibu0D51KUJcIWeEpk21C6lQHVJPA62LUrkMbc3UOapIztBIBOklKV5VQy05rQEY/lr/cOZ0q63Q5/4X/587z3W95Lr6PIOnBlu6DTSel0UtIUVDycE6XZGE/4pm96D3/th76fm24+RDcLe20wLtGqFoAI3jrEjvjU9F2+aPxy96ZIDICdSoeSV0iskpCPmLiKe975LrjxPp47O2Jc
            aXBZSBVU2pSlyMYVsVlDzoXGQjm0QfW5qKe7ktgdUE3wwwQXLwoDmxW2sly6YFg+0GPxECwc1GycgmLDN2bk+FkbSCtNpEaliErgTQwA0oApYPAs5nUnOXp/h80xXDAWv6bohFBOMYziLWUgwWJMo8ARJjU8zoV5aNGMCkRzbjQ+2krapgStWQ9BQNUJ11QywrNrYGqsEpsGXF0iKQwTKjFC4UPSLAKzpfarcq3JNhebGrDH4IBgTi6/FhiNnD2n
            kU6DSxEiiYMmeZgX8g5jDN2VRbZ7CVwZg1QYPUKmmmyhw+e+8CRPPWO49UbNpISlVbj91hM89NRTTPIRWV9RFhvcd+993Hg8jtvG83CxC69+ZYeVlYzNCwOS3gpVvs3aPsXxG8KgjLCwNYCnnx2DXebTn7nCZx99nKzrGQ4KvFtBpQsYb9HWYMwklpFRedlaynKbUpRIkeFViuymwZJ0Nw6glyiRUYzAqAKbT/AuRcoE5aOYR2xCJR5SJ0BUpN4i
            vMR6KE2OUjpYO0zGjDfWEWlKohNA4EwJukOSJDgsZZWHBo32gT9qynDw+YBLBx5o2TRJJDYEDK9maEh1pSRlTZb3TdfUOY+1hspa8nwYNpNSlHVTq8YGvZ/yx1zwd9E6NKX27VtldUUxGcPGxgQtQ4aYJBJNmPBBemxZgfMcOrifu+88FPidpWNz+xJp2iHJluK2NlhTzhLQxVUCoBAvCSioVcwabK3q60JQGnYWuDQoGRuBkp2QItWWka3qSpqA
            jfkoRTW6PImCAX7qhxtlfgRh8zbe1kLiqhJhJcp1kQjKywM2hgVsKjr7ljh4GNaFYLQeQZBiriNWA4S40GR0ZdjNKjQ6ePVdHLsd1ELYRL5STIaeaiCYGCC3U/XmaURqvcm2mnJUI4n9WxHH3hoNPRnlzcX0tomWmOdu6uUN/4nZv++8Cwsj4kHeuynXW+ythF6bsu09vTNdP2qPEcd6sN7XF7rVXaykhIUeDCdYCoT2GG9Q6QKl7XH6fIERGlvC
            Yh9uPLkPJT9PORqilYJynZM3rXJkX9jnlYoHB7DShRsOdDhzaUJfLbM53uSmWzvcfBxSFXpXTz4BV9YdyVKf3nIgT5fViEQpkKFBIUUJ5QWUGKDShMwrrCuxfsDimuOOkzeyvLzK5jDnc488yQSJ9z2cqzuPIdpKUZKPz9PrdHjgvjs4fOwo25s5Dz/yFOfOXaIcvh5brQZdjgJsVYGxIUN0IYexZoKZjNl/cIU77ruF1dVVlFJcOH+Rx55+nu3h
            JlKUCLuAqUD6IahtMjFBWI8gw7kOwi6FdaQF3o5wdgiUzGprt2e9TTjcpI24crjbRZ5TjbZZWDvAvXfcyf5Dq4zHE06fPs3zz58O+o4+jnvOsYy9ExjjyLIuVWWpLPzmb36Un/l3/4Z3vuOtfOu3vLthM0gZGK2VCxXKeDJmNIaHHnqIv/23fphjxw7xV77/L9HvLjWEdIf9PZ931pWUMRi5xpOBomI8GCMWUpAySColaVOF4ipIPP3FFLcOZgDV
            ZBL5dlHS24tG8y1C0cFlzQSAQUTcNyEJp5KSWC/RnWWM8XChIr8y4ez+LodOQJklVKc3UGUfJRROCqwTIXv0Duw2MITDXdifkt58gOVDsLIvdGevnId8bLGlC8Hc1GKSqgWG+Zb8UD0rFhoKzsaJi+jnoaRC+Appi2kgEQaJxPkpd1/Op2NzEvhTjEPMdIGddSgNvrCIwpNkMvhzR70EEXXq1DRtjF4Zs/KAV6POBxfLKehsihKrHAhDJWrxB2Kb
            OfjZToyhs7qP/MIVKgfKCUxlUP0limqbD3/yWb7qK+9hXxbwsbe86Qg/9W8lw1zih2MWFyyvuidQmpIMPv2Y4egRzeEuHOzA2954N5/9/K+jRxlZNeK1d93KiYPxtUr4+G/nbA4sSX+b93/vV/DNX59QDKCq4O//3d/mQ7/2OFIW/Nk/+07e9e6b0SlsX4G//pf/Mb30Zn7wr76PV7yiQ1WEXsMzz3n+6t/4Cf7Lhx5lafkQRnSwWIrJZZza4Fu+
            5a38ifd9Na+8IyRdaQbPn3sd3/e//gSDzd9F2rfWgjchd9AdjJOQKMp8m+MnFvn2P/KNfNPXv5Xbb0qCMn6cED1/GX7pNz7D//f/+jmeOnUGUwqWV0v+wd/5AV5z9yKmzMldh7/+N/4l/+U/P0y3v4+i3GTpoOEH/vIP8tY338nB/fv4whce5c9835/n6SefY2FxkTwf80N//Qd41zd8TaDyCMVf+8G/w8/+m3/HviP7+M4/9QHe/Z738Ip7byZV
            wSxqMJhw4dIW//pnfp6//3f/IZURdDp9ymocJ5lCQpCoPlVp+LEf+Ql+7Ed+gsefeBxczh96yxtC2WwsHoVWGYiECs/C2hK/+uHf5G1v/xqeeuxRNq5c4hu+4V1oOlhnKI2kKEexBFZz5es1ZqH3ovvMf32Pn9N+6iAZpduDmKd3Auda7XdpmkxF6IRuF0abNrisFSIOzNdqGTpMgbiW8mfDnRVQgS8dtqyQkQPlRMDcTCXRTmOiEKk9tc1wcYkD
            R+HsMMGMtjG+pS6cCehI1MEVugcPsHAU1DKUKWwPYf2UwxUeX4UMUoskagfOUUVE6zW2Hy5KHvng5etsPc4f3qv0bsrpiwrFQTZKXF8Xbu7G1IHRWYewIIxDGItX0b/K1hmewsVjpZns4IXLovuGLhF7lC6IAzhnoiukbpWHktJY0iSBThdyi/UlZIvQ7SPlEZ46N2J9CEdXw2ju2hqsLAnGA8dwPOIVr1jkxuNheRQT+Bf/6pf56q/8Kg6+Evod
            eM0rU/YtWkbDIambcNvxRfppmLgsS/jUp5+gGFuSJGd1peLWEwnawfY2LHfHTLbOk6aGO25e4u7bQoPiKWV4/3e/jdc9eBcnbgrZ7nh7hDGee+9a4Kd/8oP80Q/8LP/xFz/NgeN3szXaIO05vv8vfpD3fcdJuj54M3UzSBfgzhvhx3/sA4wmdopH18wHY0BUTIpNXv2a2/nH/+gvcvIGWMlguO7pZ4KkF4qTkyfgj7//VbzjK1/FB/7UP+fDH/ld
            7MSxef457v7qe7FlB9+Dm44tQjlEZMu4asCNx2/g2771K1hdCc3iBx98FbfddgtPP/oEo/EQJeHue27lrlv2M3Zw8cIGjz/xGEdO3MBP/6uf5HWvvxXvoSvqSeeETpZxeP8qp9/wen5U/yOqvAjjrM1aFhGOV2Rpl0e+8BRVldPpdVAqRamkEfttCUkiE0VZ5pw/f45zZ07RyzqARnod1tZVJ1C+9KbsegfZ0AXvCudcM/iOcFH5Fuh26WkYXapg
            Ow8Ob1LFjrBFJVPio2+TbiPOpImNj1IiywyRQzU2oRRNBIwrjPVB6UUAlWL7oUvk4gCH7lzgQjEMp0RHQ6/Dwv4e3cWwHy1BDLjchEleBRzPhzJcaR3026q9uXHUTYRaa9D5ILnvRBQ5nQZOq6a4eYPxvYixpDpw2WjEY12YQbbxPoRHzMBfwjJBSDFjqiRsCLjKOLw203ZgjbmWFdlCn0l3IahK99fo9PpBlLW3xJOnLvD0BbjnliCUffQGuPv2
            42HeOi951b13cMtN4ekmFTz8+bPcdusGb3twFSvg0AE4cqjDJ547y8mbDnDXXcdQMkzUfv5RePzRZ1GiR1WVDLZznOshouDPaLIJPseUOUnqUTLw8w4d17z32+7CAmdPw3g4Yf++Pvv2h6wtS+B7/8R7+I2PP8zm5SvIxPCBP/61/LkPnmQyga4Kb/XsOTj1yZJDh1NO3ApJonD5lPSOl2QdzWR0gRtu2ceP/dhf5K47Qk5hHOhU8OQTniKvOHZL
            yr6F8PXbTsA/+8d/lO/94D/kN37lt/jIr/wW3/2ue0m6gY1w7MgSidY4Y6EqedMbXsNiF4qJQwnJvmW48/bb+CX3n/BGcOj4YQ7sW6P0geDwuc9+gUcffpy/9Jf+Em96/a14Ajx/6ux5PvOJj9Pvdzhw4AiveuB+JpNJyMSUw1BED5bILYgUrco60k6X/tIixWRMkng6SbrrCJyzDp0kOOPROts90PnWowl8fjYICjknHSdng8wXGwBrT4qpYXmQ
            WXLOBUXaGhORcbd7y+jcEMoUoRfoJaKR5bM2AMPRIyY8VVE1b046ESTqIldQ2nCqWqkxmcZXMKqqUB9UZXxNOZhNynPrVHfcwdIDh1EKer1AFRwauJKD28jDqIb1YVRJahwC40KL1lYV1ktk9HpoJ33zQbDR9oucuVDfiCYDEzWPz/s4tlaPabbmPl9EIPQudC0bv9+a0+daa2EGDxZ7yhldLdjOLMOa720d0jikdZGCFAjD0oOTYXpHqRSVdbEi
            pbu8j6zbYVgG4H+yVXHqXJhiEQkc6sIr7jnOf/75z5Bpz9237qffCRs/r+D0GcPDn7tA8S2r4GB1DU4c6fGxwRPcfNNd3Hg8yu1p+MTvnObypW3S5ABGFMjo7WJiH6zfTYCKJJM4k4dMb1CQdjMGJfzTH/8v/It//h8Zj3I+8Ce+kz/xgT+MVmFq85674ZX338Gv//JTPPjGe3jP178GT6AHKQ8f+tiIv/k3f5zPfvZz3HbyEH/m+76R93zDa6cT
            n4BXnrIa4l3F9/yx93Pv3dFPC7hwEX7y//x/+YWf/U3OXbjEm//QK/iBv/onuecezfYQjh+FP/W97+bTH/4kTz3+LM+fHnLXvQtYDa+47yRHj6zx/HMbHDq6zKteeTdZAvnEUvkKS8Zdd93O4v5VhoMB99x7F0eOHEQLyEv4r//111hb3cd9993f+HSVec4H/tj7+Y1f+xBSag4cOMTbv/JrSPt9yrJEpRnOOurp9FrwwhtLf3GRqqpQPnBZQwaY
            zvUnpsmAkpLKWKQSKKVnylvvAo/Ve9EoKnmnIrNCTM3qkV+SiZCdOWiShIHsqgoZoCWUsSLggWyXZN0l+hn4ATAIrn+T4SQcw2VkL1cVjU9E9Op11uOsjoboGpxh7HLkQhelErp9wb6bE66sA2cvho7HiocbD5LctsygExkvJWxsxU5yTV8SnSALiA3ae+2MNuIIAt2MtdWxXtS+GnXWZ6Nv7oykVpDDCp5RodQM/L5gxNQ+zbz3MwHwWnGpKXlb
            dIXa2LJpTDiHrSrK6tpGfeIaunF7TgRUMB6OML5HkoYgWFYGl9jGehckdFKKvKK/ssJQSCyKUe4wUgWfimSFX/v1Z/jaP3QzR9ZCoH7w/htI5YdY3bfKPbetBkK5hc9+fsLZMxW/86nnuLJ5J4cOwtoy3HH7GsgRt9x0iIP7YiKv4QuPn2c0tujUU5VjbFWFaiM4bpKPR+AtVV5QFZNodSLRAv7dzzzPD/ylfwOij0wW+Vt/5z9w3/1v4u1vyTAE
            bO/IwSWwEx58xR284o5QXnYU/O6nLX/lr/w4n/v8KRaWV3no80/yv//tn+TWm+/gVQ8sU9WlpARvRzzw+lfxjrc/ECbrBQwn8KM/8kv86I/8NIu9/WTZGj/3Hz+L0v83//iffA8Ly+H3H3zgOPfddyef+8zDPPToU9x5fwhYb3jtCU6eOMhzT5/h4MEj3HXbfnoKVCdhMJ4gPNxx+0luuuE4n3voIe656zYO7F8BYDKqeORzj1AVgYZUFgF7NeWY
            EzfewMLiEsWo4sKFS/yL/+ef013YF0Q/nMLrBKVaUznWItOMvHRImUSDQ4k1BmsDmVyrMOhQr1vnLMYKlEpaY4BytsytR1+9bmWEQUxCqSQ0S52fTiW0eWcvskyWYYZwDhATAiklWil0ollb7rO22CU1KbrqUl2xrD+Xs3FqwsaZgsl6HpScKxEEFVQHekuQLaHSJXS6iEoX0ckSSi8gXYIsRGSPdnGXx1QXNth+7jJXzmzACiy9/iALb7qB1bed
            YOGuZcQCVHECiIqpAdLOZi0NrtlOsWsCtWt12dulbpx8IAa/8P/1E839XCOLL+IURy1A7XcD2b6oslQI0XR7X+oPHxWrvfPY2NwJ9sUV3lURdwxit9gyUoKiynRZhskflSDTXjAO8mFMTLgUqfo88oWnGWxDFocpjh6BAwccNx3v8sD9EleFBsnHPvk0w1HGqdMDLlwKtzVN4PCBLlnfceTwClmIuQyG8MSzG3GTuKnnb8xO60dtOFerzacqwVl4
            7NFLVPkKS8sn0ckRNoc9zl3IZ+a0pSjpdiS3nzxGR4E3Fc7Chz/yGR5//DxpZz8LS2ss7TvCxUs5n/3sk0GdJo45Kx3S6JtuPMjhQwsNm/Cxh9f5qZ/6RaQ+SrZ8FKuWQKzyoV//LA997jxlGf5+1oUbbjzKxFg+98TTVBry3LK4AA8+cDepznnVK27m+HG4cAF+8T/9CufPniYRcNttt3DkyCHA8Yr776XTCYoxp0+d5ezZi5jKcfrUWfpZyE1W
            1xb53//23+BH//7f413vehfHj91AkvYRQrG0vI8sXUSQRapXfMgU1/487i1jfWhc1rEpNoSCBqTCIVtsh1k2gpKRKKIivUhqhNKgNELq4OlyrYbHi9K6kbV3g5iRS0+ShCRNWez30RZG56F8doh5agv3zCZcnoSSU7pwTPaDV0W2v0u6mpEspajFhGRRkS6Eh15Q+MThlMWJCMaNBJqVIOVeVnBomaUTsHwSOscgT2BUQrk+hM2gOi0MpDYA07Uh
            1zTOhbljr/aYnPHtGxVHpHw7+EXxUy+aIChs6xF9NrwPn4eRv1r60O1KO6kf0KYAzQY3KSRSyKacFY1Z9/WNK72gh7DNwzkXSjgX/ChEbehkap8TG6YTamP3GgsUYXEKp1E2RdkOynTopz0unzvPs49Xgbrp4dgRuP3OBW67rce+pTDDP57Aw5+/gCk7nDu/xUMPb5AXgSZ14tg+bjt5iP37QotMS/jdz8PnHzkdnAcxIHKkKJtgNx3gkFO5/jiC
            ay1cunQJcExMToHDS41I+g3hvA6evcyzb7VDJoOydmHh7KXLFJMRZT5gtD1CCc24rJj4IpjqJdGETwu0hGNHDrOyFGYxE+C5p89QWUdncYHtvGJiIV1a5NylSzz0hSfopuE81R1YObCKSTy//fAXeP4SLC0qMgX33HMTyyuS17/hDg6swJlnz/OT/+xf8PijjyGBYwcXuPuu21laWuC2kzcHdR7goYc+z6WLVygLw7/9t/+Whx87T5bAYLhNkgq+
            4zv/CP/nP/0n/PP/5yf5M9/3Z0h0l+3NEkEXSacZevBCBexdhIDmRP0AU1WxoRmXTMSuG2P5PSoUH6EXJZkJfuH/E6RSLYjmS8OPkdM6UkVWavA80ELTUR0mGxMuPrtN8ew6bFoQfZD90PzoZKRLXdKFhGwxobuYBlPzCqpxiR0V5OOK8aRiMqnIi2ByLbVGZCkqSxGoIP5o1unfd4Sjd0lcB85swuUNy2QUmw0yRcoMJQXa7yaE6HdyP8QuOlDO
            t+ZkA7ZXBzURgx3x4V0ruraibNumssbm/HXQTpz4olO23aabrru83evDBsBgqvHpLMJZhKvAl0hrp5Jm8WgXaYqSEuMrbFU2f1daERzerGc8KPj4xx+myIOraL8Ld966zOFDgU3V6cEzz8JjT10BOvhK8ZlHTrM1ChOSa6spd965SicpwAT4+VOfWOfK6e2QdajYlXdyKvtY05CkBWWbA6YWmM6yLiRhAgSRQNLHKx1QDhszR+/wvmI02ArZYyLo
            plBUOeiCNGL4eWVYWF6iKIrgQBpx2aAoI8nSLsb7RgykmoQ2SV5NQk8p1Y2XtrOSsQkNI2OgKAqSpMfp0xs8+cQ61oeXe/T4CkeO9ThybIHtEZSm4onHn+GZZ55rmjA333wjr371K1lZWQEkm8OCX/u1X2N7e5usk/ChX/9V3v/dH+Dn/tOvsG95H0m6zHCYo7XmzW9+Nf/b3/oL/H9++IfoZgnl2NBJ+lNlnHk17CaZkBhjmxK4jnlBSNW1DpfZ
            RodrkfmFjNCOkgihoy7jlLv4pfyQgbsWT/YWnuUKGJwbkJ8dwkCC6wSJE1PBQkZ2uEdnNbxAM7EUG2MmVwqqK2P8xgS2LIx8QMNzAYWD0uIqFy0ow0XTfQnlBXj1PpbvgwsTGBaxfE1UQ9OTTuOcClAdYfTXyqmklBAiauklUxEyK1qWhOArM1cmyRD8rEDY+IRNIFSIIOkS5IaignVbh8y6wF6fIgfyurOzJsuTkfG/hzqI1pGGUtogDiHAxZ+v
            B+QtvllQNs6R7PoQYKPcvm2FUa2C7Yb0UJkJzuYIXyClRVuC9V8V8IZOp0NlCsrJCFxBRYXxFcKOUGZCJhVa9vnQhx/j8nYws1Me7rptPzfd2G2cTn/7dwueeH4bOkuQLfDJz51iowwbbXHJ8KbXH+XmI56UIHpy+olL6GSN7nIfKx04hTMpKo0deQ1eTBCqwFNR4Sls5O7bMOkjjAKfkSQL6KQXmmOmMYBDaYGUmiTrB0ZBXkXlnjEyy/F60hjP
            26rk8P5VFFCU46DpKi3OSAbDEicEQgbMam3fKr1OhhIWRElRDqhMzuJinwNroVkhIo556fw6diJ57pErPPyZ58hdmGU/fsMCR48usLSU0OnD+tZlHn3qMU6duhhYrBaWF5e4/7772L92gFTA2TOX+MxnPoPKBE6W9FcW+O1PfJYPfOAv8q73/Cn+7b/5ZS6fHZNIxWRUgoTv+qPv4t3f+NVxRM/T6XTQaTqLLccGoHcCa1zw3zIiHDZ1NWEF6B4a
            gUag0AgRoRKdkWZdOt2kIVsLaYPUlp+FrGYbdaKlURgeNb+4eexRYV0lA4xImZdT0q7zVOOSfFiG5LAKtabs9Vk80KfbE5QG8kFJOS5wVQVOIr1AigThkzA2RxqCZjv6+1CThJEbS5VvoO48ytrtC2wJsMru4OQ1M641GCunj3YJ00hCzXH5RMz86i4nFrzxwXDLBHN1b30YozMy/OtEUOmw0d/WijBCRPCj8D4EkRoDfMGJ9wvAA0OLNkEJrtng
            CF193zRSQrCO/3m/AwucTZijKKgwjeSRMzaMLgodBXKhHI5hMo5WnyXWlfj4yCcjup0Vzl4qePZ5mJhwjr3py+7gLW++F0vY0F949DTFEHQnAeV59swWz58NvPYTxzq8+6tezavu6aC6cPkynD19CZPbmDkZcFVwTYm8s5DFlniXh6kPa7DRqz4Y5pV4ZyM+JTFRY1IHuAkVVeWvbAx45tkz0b01QQGvedWtdLMx1eQ8SWLIhxsc3NfhVfffFg5T
            KSN53eNQ/M6nP8+pU1eoYsp/1703cuvth6lGF5AiZ3kxYzK5xK23H+XkLUfD66tgc93z/POn8E5QFYqHP/cUo5gVHzwC3/4dX8vNNx7DGXjksScAydNPP8/lK2GW9667buMd73gbaSfBAZ/57Bc4dfo8zjqkDvQVfML2oOAX/sP/y//8576fr/369/CP/uE/RUhPUVj6C/C6N74aL00gybcJyXOP3SqOxgDRtcqiusHhdcSM9RSr9w7bgspDNuux
            scr60meAUkdfgV2oFMJB4tDLKcsHuqwdEMFxcQx+y4XsLnoHi0QhdCjopdZIHaXz27V7bDr42kfXV7CacOBWiUtgNK6aqlG5MIL6YmFPGZ+rlsUXxsZH2Oc+CjPU9J1p4yPeOKkiEz3IWlkflPJMbITU5mzhYXe9xPXpRI2dIJpHfTBMZbAUQqiIBxLUN5RESIlWVyFTtx7eiWZsL9AKak/ZFjhaE1X91a+xK8qon+jpdTOK8QjyyXQkxRRgCpwp
            sLbC2pys2+HixS1+7UNPhDFTDXfe0eXQkUBBOnUKPvOJx8EqZFZA13P+yVN89rfPB8KxhxMHYCELwe2J5wuefO7pOOgcA7QcI+V2cGeIVYCKB5ywFirb8DadB+NzoMDK4JPSlFfxducVjHODFwmf+uyjPHMKFjrh6+/95tfy577v21joTdg48yiLnZI/9v6v4Z57+lEGrhPMB41HqITf+dTv8tBnnyORcGlkuelW+Mt/7YPcfPcByo1zbF0+Ta9X
            8qe/79u56/4+g9IhE/jkpx/lyWeeQXcVCMsnP/VpLp0rECWsLME3fdOXcexIj/WL8NEPfwLo8NyzZ3jqqWfRCu56xS287steSacrMR4+8tFPMBoZHCnbG2Pe8MY38e73fguLK8ugJduDTZ5+7kl+5UO/xHA8RkmJn6lcbCs5UkgRcjkRR+WanxMGLwxegvEG48FFYQhXj7yKUEF5qVqf60Z0wqHD5+2G5e/BWJzeNYTLhG6nR9HvYYRnqdOhyuHy
            hSqUtT4NyKVOm4vkXaw4dWxh16vSuN2PCB8unDi6Qp4El8upSbRExXfvr3Eh2kbt18eBi3OubXPewG+JWZObEu0kUyZ8NDTyZYVp3MQ81rsZQdMdPJXW0K7nhZu4yDgzGIzG53FEEYWGxTWgw70UJe2Ol7oDp7QlaBksEXyJm0xaqq9B8kg6wEZ3t4g+yHSJj3z0d9n8nttY6ZWMRkOcT+n0Fnj48THPPn8e3e/jRIHsghsnfOK3H8Z952GShZAm
            SsI0yTPnrnDh8iVU5xBKeLxyGG2QykcAPajKKKniqWYQwjed4CCaEbEsnSCURniHkmF5WhPEikZjC0byWx9/iP/6y/fw3e+7iU4SMO2/8he/mbtuuY0nHjvDg695FV/1VUfxPiATOvYHjAsbHCv5iR//d7z29a/mhmOKywN421uX+Zl//ff49V/5PFsbW7zu9bfzujccoCgh60guXIB/8KP/jMnmFZaOHMAIx3Onnuczv/N57rjlAaoovKAVnD+/
            zkMPPYISCc8+c45Pffp3ufeVt+G8QyWaynqeO7XOxz/2aczEINJF1g6u8J3f+V2879u/jF/+jcf4hV/8edYvn+f4wVW+/uu+huXVVZIMtkbw0Y99FkeKJEF43Ri571TZ1TgRR1xr9RnnA1UtSoF5diM5y6aL3FjC+9BhDoGxzg6nzoe7r1939cmq69IDnGdrxD+mlKK/0Kf0jsF2TrVVgU1RnQzlpgP5tvb5rcJMqVBiWpZYpsKfQkaBTYtMNM5a
            WOqw/0YYdwmmQ/WLUH7KZWkFOdoinzVvLr4Fa0zEs3T0n408JBsNdqKLW7BgiwRLJULtI1yjNNNJY6xrZZBB2ixDOvB5RjkaIbzFjILbnSnD02AkZVnOlOsqlTttXVrjd9LvDOhSBFc4qaDTDcbmpqowVdxsPpTj4S36afM+ihjsKKHn1F/aMghSJlQRu8nzHNQCXkps5ShtAUlCd3kBlSaMJuMAqHkQNqiQCBe7+T6JVscVUkC3d4CHPv8YDz+e
            89bXd8j8GkrDdgUf/cyTnLu8Qba0ikkjVpsscenKhHEBqwsBPak5a7/0oc+yvT2iuyTRqaCchKkkqVVsVoBchLzK8bLEuYLKVXR6IUEVHja2S9ALJP0V0k6XrY3LaB3uQScLrC0nuiD7DAeK/+Mf/Cy33PznecuXhzLZ5PCeb3wlOnklGri8DpMyzJpXBnoJpL0+GIFeOMhHP3KaH/z+f82P/J0/wqEDQezhgXvggbvvnWK54dzg2dPw1/76P+Z3
            fush5PJBJiYP0zZnz/D4U6dAPAACMhWW8Ec++jHOXzjH0uoKg/GAhz73OL1UA4tsTXJ6nYSPfvwzPPXUWdLeIaROUTJheWURD7zpLXfwhi+7Nbz/+CKSJGyRf/Wvfpn/9AsfBrlGmq0wyreRUu9QLArLWOCNAJUhZBrUqdMk3M60C7qLFwnWy8jhkyjdAZnSW1hmcVmgopZmZUL1I1GUtR1osyfEHp3Na2U71xaL0VeTmqkKy3g4wQ5KMEkgSUfL
            BCtaTnzz3aHWqMUO1RIppumzcNg0YELSB3VeL03o3knZKNFf7U14NxvlTWmmpGZij91HOWcvUb0EIULGUAcO52RQvjKETrWNRtf15XGRAuIs/Syl08/oKIUfLODsqLlkUiqMN2FpxOeuSheCv2gLXU3xt4hqzri1WQIlxTrVlMZeSea9x50NznAChVKy5WbfSrbt9ACp/2YtuuBjJit1qPSVquXTXfMznX4PJTxFMcHXCzNqIgpMUF6JVEk8GAqk
            T7A2xdkl/vOvPsatt94f1Mk8uC48+twI5zMqobHO4GUC2TIX1i2ffwzczeHP9BZhMIHLWxqSBKEdVT4JlIukg6XH2SthokgL6PTXcM7QXV7EiJTLl6EawcY2LC3eAMun6S3tY1JUJEnGcBueewb6PbgyAOsz6HZJswWeevwZ/uSf+mF+8Pu/kzd/2U2sLYb3pxN49vltfuxH/zm5yfmhH/4+vHRUeRfrJGhN2lnEK8XP/fxHOX/+DP/zn/1O3vSa
            0OzIdAjY4wIursNTz6/z9/7BT/DLv/ArqOXDyKQXlKIF0F3ic4+c4blzsLAAeR4O6MefvsTYCpbSFLNhePypMzz02AaLSyk6STAW/usvf5jB+ibdpeOA5NLpM/zsv/8FDh3ex/JqwvJihtICrToMByO2Nif8y5/+Wf7Jj/80RaFZXF6mqML8v3Nu6nA31+ATukuqJVuDnDNnx2xvb7Owsg8bvC7wRO3QOFFSmqBqURrPM8+MyBSkSYfxqGAyLpFZ
            N5qj1XOn8ioYzbWmQ649PSLkwdt92/3Jba/Dba/h8Pt+mM2F28jPDoKygMpQsoOrouukJLCR45RH6A7EyY/a0a8eHTO1XFZQg1YO7GQCa46VNywwSUp8kWCcx+mY69dqLY23cPh3Oh5Wp4dyrrSW04kKOS1lk1gmBe2/KKdWRvCnMtPJjzqKW9cyzI0B0FoSKXBVyWLWpdw6zYn+kL/7/a/j7a8GqnLa7q9xjDn6i7wKUyd0fYmzrR6RCH7gx4b8
            vf/jF3CV40f+5rfxPd8exEF9NG2y8ZSRYue45E64YPr/KkKbRTFGZz1+9SPwHe//v9gq+3QWD2DKHJ2lZP1FxlVFWRSNLSMepPVob3Ayj5coBS+xviTRGbZKcZNNbrkJHrzvRjbPXcTrBL+yj9/45KOUZZ8kW6EqhgEktILM59x/U5fjB1NsNWFxbR9DI/nwRz/NYMviK43WCZPxEMyIO+4/zm03aUbb51lbOsbvfOoLPPf485DAA2+4nSP7+9jt
            Da5sCB49u0CyfJKBcQjhsaN17rnpIH27TTdTyN4Cn/jsI4wLFTUSR1TlBkrnvPrek9x35810lCDPc37jox/n8UefZPHwGvfddwBbjRlOHM8+d5Gq6mLp0ekeYDwp8WaITOC1r7ibe04e5/CBRQAubYw5c36d3/jwbzPc3EQtZFhnUN0FrIM07VJORiwvLfLAq+4j0R6J4cYbb+RXfvWXePKJTyG1bew9X//aB0lSxdLSEgLFb/76bzIaGqTrhKaK
            DfqBC4sJN5w4ys233MDa6hreC86du8Djjz7NqWeeh+4CvcVVrFOhydfGtf1s40NgULLCV0OOH11m/0oPrRMMCc+dWufSpcvojoqSgtHwVQiK4Qb7jq5y843HoJzQ6y7z9JkLXLi4jvVhCMNh48F9jS5A29Z3Vxaa+yIwQBG07lxuwEikyvBaB8FCKfFK1RK41EZK4a8pGvljMd3eU4/hCDor0QwzT23wWl5ptUDDLlMeu8yyTHe3CIFHSUGiIsky
            YjhVHkoWRgQCd+nC6F4954sO3tUmTIGI/393fx5vW3bW9cLfMcbsVrP7ffbpz6m+kqp0lR5IBIMIvAHFFr3kQgQB0QsRFBTlRoG8CghcuaDGq4KCCCKN0kiPECBNJYEkVlWqKtWcvtv9amczmvePMeZcc+2zz6mqVJDc93w++3POPrtba+05n/E8v+fXuLjJR3ZOgjP+oVQGhW/Vi9wRrR9FC9gvwAwSrwA8+HLa+WrXLoLpAX14EiSV2giiHuiq
            j9UxTjvv1apDNrsVWCMIeL8X3Ot5iKau6Qc15FIE4q4EY7ve2GUCReHjICvjsELS7y/6jVxZ+EVIqNDeANenqwljQlaFDie9w7h6wZPy9OUpFy8/jh2XuEhi5GXoLYHMqPLSh4oLD4w70eXhj1zi4WroZ1frIBHECwtUeUWaxVhriJMUOglPfOwcT3xoE0QB5nGIE9TyMaSyfPjhJ2G042dpsU6y/jLWT28w3LxBsb8FyvLRDz2K1JY0kkz1lM7K
            MlJKyipHOE2aLZLnEX/wsSu8/3c/5l98FDLJWDrxSopqh/f+/qMw2fO3UncF1ZX+d1L6hYrqH0cIwfvfe473v/dpcAOkkljZARKW+yeQWYYZbaOWFhGiA6ZCE0OyTK47/NbvPO7J6GiYvsfnCLgYazRxqrBW8N5f+x1/73T7XskUJ2RZD6dVMCLoYbUmLwSP/c+LPPbRZ/xFUJaQdpBRSn/jDirtmBYVQjlsWSGT+JYKDCckVQVSply5vs+5Zy95
            DX+UQdzzI/ABZ2cBqN4C2zsDtjd3fCqXjIGI3uIq06LgkETpP7I/Qh3xHWAtObHTAZx9iNNvexfb6V2YscKS+JAkJxFOYCozu9PqlMGmA5w5PovAsXaGWTenmcWiuiHJZywglqAYGUQ78Nl5qZoSAmesx/isRahk1gHWo20sUXFMEmiDVe5DtSjBFVXDQcQob9XVbHztPE5g6qIYAP526xSY7dJZlLNIKSkG+8TC8eZX3MWGK9GDrZDHG2yiTIG0
            rnEEq9vxOnvVBV5TzR8UUhDVObXWkiwe5eHHNnn86esI63jT61/CvacMwuxTGYu2UGrTjMPOOYqiuGlBdFCh4pxDSoUSDuVKFhePc+Ga4/1/+DhFrNAKspUVP17rCnSFM7ZRzfjiqhFYonChGqdwwf7IqRg7KiFJWDmyhDUllAbtLOM8n9eDOr+AEhKwGuVKZMi5EFhcWeKM13cba70BQpAJQiA+uwppS4QzHsd0hm4qKXKH0X3Wj91Db+Uog9GY
            3Z3N8KNLIhyJ8zxMK7x1/mQ6bjabEk2n6xcrst58OonWFQaDjCdM80sIl6NUhnMJuMyn2jkvEXFSomSCEn0PLUSFt4+WCbGKKHe30MMBSIfq9nAq9YdB8xqlKCk9VScqUW7IaPcc5FfpdzSlGXtVi8xwIgWRhoPI4mPsFc560w7pIMs6zXVYDzjaaI+VC6/3PYy03xzawt60hIik8xCK8Nd+ZRzGBKy+SZCT818n/D0UB3MEU1bNY6gt2mxdBG+n
            ArHPVSjtJ4EBhj2EdLM6bMOTl7UPvj4ANLo2NCoOcOPmXWtrbjJWYgYe69GZQkqopvNjodah2HqxJjKSIdLRByIlHSiD+/00dzCuPFLsBath+eH/LZAI7QItpH5Sclb8GqpIveV0ze/c1dpgF5wrQpx9Ncn5rd98mGh/B8YDbKU9zdiWwSbYzHV84hCcdu4XEvsuRxcFsrsKnRN0FjdwxvJbv/V7/I/8IpEYzWgFvkWdmbneyoVhTlNpA73HF8BY
            LZBPMpL1k97deWUBpEDnngIjjEWakBJXb+pDVGIjLXQGJ5w3nKWCOCJZ7LE3mjbqG2HdTMtLaE+95xfOGSIp0MVk9nFnicLPNs4L6wk3c81Rcy7yl0fwZcSCTLtMJgZUj4Wjd2PjJW5s7jKdDGmsi5xBC4+hSut//kwqOLOFn5STcAMkgTZkkeG1q0qDK31so9WAVAgZrjOhqK1XtNUY4UPYvULaX2NVMfUdE4RuqcWVE3UBjBpHFoGlKqdA4YtN
            cAsXdUifUl4/60KmssBnE0vP8TXC+gyUA1LNhsTvmNfePkchqReSpfVGwUKK1qLYMyeEk8FrtJ3NUfMIBUaH6yoUTCeENxWuA7b/CDOBfQG8VXt78OeKA2HLoSoLJzw73vqbrtGwtkQY7btdUtsu4Vnkl3OS1Qzdg8I5hLAoowIlQ0CkmgxbETZ2zYts8eHmeenn27zyEhGCP2FwKRAtnbMH/1s0SOe7p8bRoO4KayeYenkQbj4XzCMcAkxOp5fz
            lje/hHtWNNXOVXZ2dz0h1tqA0fm0e38S3uLSavkxKimRyv/ie/07+OiTFU8/u09VTXjNG+7l/rNnQW9R6BytQ0fkaH5e20zhlhrM0AFK6bA658jR+7lwNeVXf+ujxOkiWZoxLsb+NLIBb6SVJIe9NZ3HhuCqTsfHUU7HzRc28Z21+3Zbp+Ks52QaPStCwQMR63xAj3VeBWPrrsSiggltVafgJQlWS1AL9DfuJI17jEdDpsM9X6USEUaSGhQNVuz4
            Dl/Ief2MrdPnvT8aUkqUAm0rzHTiD1gVeWhAep7cLJKxTfso/GRRRsQyonIjGO37VbcQxHGEdu7mHWfQKUoXIR0Uk22wE5COyvgCV8NHXrrpb5bmOg/muQIRQt8PmHZIcYAF8lybVdkooTymV98jZlYzRPv5Hx7S7uyMrC9rUZrwa0IbnGBuFe71R8ADPOC31bxAh0tS5ovI7TSw4sBOxmOGUgiMrHyVvTaAsxkiBeuKsLzwIU3+wvBYVRQwvbL0
            h6bOK7+p1SaM1AJE6nGNYFMl61xW9Lz6L6hFLNYXP+sHDs8PrIufa9Qj1oX3nfMOMs5592ebs5Dk/KUvWeRPvgKiatVTHIL6IPRCczxG0aIK1kFGovV+HJRvVeW72+/5AfjoR38WrOYv/MXP5a/+Od+d1y9tTfCu/RcP4o9zv8421TNoMK31kNyv/Ta854OPoha6REnEZJz7DjaINZ11YUdU59q2FlL1zxDhZuv06HW7jPb2qM0ElVQe+zJBrFcX
            QFdHbxo/+gQeqfdc9OlutjaqqH8PtRwL3xn6ts/juEymsLjOkWP3o03E9tZ1sBqZxtiylaAmDrw4oo4srVPQQryo8BiYDH/jKi/S96Z6/rBVCkTafO4ctSzg2QLtx1ojUM5ibYEpR+AUMs4CJUndfF81I6GHXigH3k4aPNZqvURMiLZefd7Fu/5GUqaz8beGMmodoLtFNOEtCpBvdMIEgvVmCc7MPt8dKIKHKZxq3iA+EXIWOtbCDR3PweuTzzHu
            3n6JEhldhWza2Cc5hT95niO6oaOr1/JC+ovIEZ7882X1Cn/SCge6oIxjRBzq737O9rNTFtc6RFGKLieIVNFd8O10asBNA65nfahLsJsIT07NyMp401XpvCOIcz5WsOkED+SECjvT1DrnVfFhqeYVBTbkgATXGKzD4W2Y/AivccUeiYD1Bc9llAf4R5WZPwDjA2wCc/M5gXO+AKoOmGob5ASkRSpYWQv5uLV+tKXkOOxaNeZm/mrb+KcoQab+Hpaq
            ROsp+bAAk3uTwMZWRzRY4+wmal9eIRYhyUjTlHzq3bil8qObtAZntF8kuQALhBvbOYM1Oc56iRtCB668C43XIdm9zgWVS5ALVZXX3a3fzfrGWayN2dveRPgQaqzxUZMyANF2buQNQeTC+vgDoRvCknB+mvDxEI40jiiKCXq6F5RSvTDqxDjr12O2Hg9DCKnfgnpMs9Ppgi3I9zZ9h50sBimdvLmxCLBPkqWkAsZ7W+AKcBVWCJRUuNKHjkmVIKMO
            1iUIIqwIUQ1CHfTkmF0rwiJV5alI9ULwoNyoLpSIA+YespnonAvGBXMXoDfbF00+b33NyMPraxN0ZjyG6drj8q1dQOSBb2ZfoC41OpQrIWWI8nvx/lvNPkf4ja1c6mKLMa4sAkdvAqWiqDosrQlY6FFUMC0cprJMxyZgeH47jWkBo037FOZjF05CKxpRNg12J3HWYkx4v+7wgg+gtSYUulAYrW1hgG42BtfGEcJXF2m9YYB/FDlKpM3sb53nfM1N
            HAckIYfhuz7rVyNFhnNjoABnsU4jXBRuKh/zaGx7tDz8pJZKNEXv4PURiQIhUh/VbKdUlQ8Aj6XBOY0v9VHr69whPEzZUGTiOA7RMn5kjWPpO9YWD6deykgE2liMqfz4aYzv1l1rErHiNrhm4A0pCVPBwqm76a+eYXevIN+74RkCzuBs1RprdYNf0tI9I7xwUMqWMa0T4JRPiwudjJQKY0pPPpQKZNZsskWgUMkDsMZs8rC4ckpR7Pn0wkR5Bxkp
            bu0UJH3IvS6G2GrY4pcpmom3+fnKd6FNAakzgENXLQ5kxjjtubFS4uLY69q1vunn33YitmK+KDbFT97i7xewsmi6yOfJ63MvfHscUVvPyHn/ubIqGQwGZGrptjeXCH7wLqgMhGwpDsTMzKD+Zdly3188S5EXe75kEbGiiJe97Kma+IORsmxUB55lHFKoa2JzHStnlTdcCPegsa371LZan9rd2rb1viJok413hg65H1gbOkB3AAMMUm0XTjUDTksf
            JBfsz+JoJuGT0vlNb+uXJg4oNRpD63aQ9WxDEzSVVcDINM5EKFf6DWkge9ed7WGSOCVlg6G6gwiHsEiPBXh6TTWhMiki7qCC2WjZHj1up+WzjqiTksQZZd3CCp97K1pWZPXCpPnb1ZiBDoXKhPHTetwKdWsjTGH9mG4TFk4/wEJng62rO1RVhVACJS1aVweWG3b+/fr/nB/NjdOtz5EI4ll4j4CinPpOlQoigZCJ/5xbyK5s6GKkjZHOUlUDzOA6
            xIY07mCkDEsVcfitLWOfr6uHfvyt9eMRfrsuvEZXEgfcO7ifCzkjnoTogOBhNafkMMaECE3poann1dIcuIJvkp/J2xRBcxscnBcb8/vCMcA2VFcXKYylGo5hd5Pk2JK3x5PSx/y2NjrSypnrSINVSM9Nrq3hIwcqBBQJgzqzTGcRsr7PStep1wGPtiehK0v82CUThHLzOmAVRh3RAltvir8zMxfnmhhdmy/UBgd1ELb1rjBY5YuLEyGDd5bDoYJP
            Xr2BlMFiQKG9m7J2DWldGDHrqaVPiRNhCTLLEDlAS3Hitts255x/7cJzkpYZJ9E6kiy5ZfE7HJedXWXCeepCjR+aagpJH+HShkYjhA1b1nD5zrWQck5CGccxOI0rDdiKKLK46SaOSRhr3cxPMZBcXbAbE/UCQNh6cPQqFer360feCskREuKE/rEzONPjyuXrIZKhABmC0SPXKna+u7RUTfET1kv6IGzvi3HosnIQoNL1Jg9HUlLmW2BGICaIJCaS
            sbeKc9HssTEj/jZLIyqEKCnHN4BtEAkW5Yt9sDaXWKSYYIVE0/fWURhcMUHrPZAVWL/4cZS4fAhpFycFTorWq2RBlH6hWD8q4Wk8JvyPFRIhDIx28WdMhlUHOi7nbkcUuXUXdnABIl54J/i/rADG1nrb6pqslkaoKCLKDewOGOydo3/sFAsnuuzlJcQxkRCoAnShkSpqWnhnwU0Dz0vh7Vy6GrHcZ3ElrfcTTKewNQRMUBdIF6I0a+H0DBQQbVDZ
            ev7UzIE0fK41COcfQIijnhVgE/vNcM3tqy1rQz6xs5knk9dcBlOTo4MkzoBs2eQr67EcpUBWlkgJlPOXiUpmLhYubCqkU74/cwfAX+fmh4bQuprDTtyog3Dey9BocEIhlCWKAs7ZAovVgWInWptXv79olZNaTyu8ZVWiJBWOJFaUReWLdVkFykLrsYnWY1NdKCuy5R6RgGI6QFmHMxV6fJ3Pee0J3vym13Nj6xzaacrK4aSi1JrBnubS+RGPPXIB
            5zLirIOKI3JjcNrDGBF2tmms2YFOQmlRK0dY3TjOYH9CMdnxU7jUCOmtfpyUOCp/4ASczwrdjMHSWaRzfrkgNFbnaJ2TqIIvfOtrWd9Y45d+44Ncu74NruLkyVVe9uADnDm5wvraAtY6nnpqk9/97Y+wuZmTxAuQSC8s0r4AyhiEqxBmQjne5PjxjC9/+9/GOMfP/Ndf5dmn9+gu9CmKinvuOc5rHtpgMi34/Q/dYGtH0xcFVg+wZoSVBZ0oZjrZ
            4577j/OX/vLf4MKVTX7jNx5mZ0s3vNlpOeDlr76Dz/yMl/GqB+5laaHPYGvKe377YX7jf3yAwcRSCThz5ihf9he+lGeefpqf/dnfwFaWuKOoTIk2xu8GrJnv+uTzWDK08Dt3sPhJO6dFP8jjm7n0WOav6MMxxIPFVB7A+e1z9LSRbRxYVDMqWutIXGi1h5pRtYVKjtI/ljIKm3+Dd+VAlLNnrYDlhDiWpGnIPvLxsYwN6KkjrkNTVGtjJKOGQuO0
            mWOaizmOoWi96CEpu2nr3SH0AeVt622d4Sv9zRB4Z1gVxmI16wobTp0NrsN+bPU8ONNQQGQYZ6XwBFNZB+d9qrf2tbQvEHGFq2NGa9xTfkp+prcNs7QhnQbfay9N2/pvQnfd6XiJUzn1JGZncDrHlQM+4zVv4h/+nZch4pdRXynTyu9LeilceBZ+6ief5l/8q59kNBkj0iVS1UMnClOVUE68L6H0CWXW+QyS7tGjpJ0+gz2NMRKlJLG0lKYMtBHr
            R1qqwMjXs789duGXOLUTtKkQpkToMX/lr34x/+7f/nme+AT8+m++h1iNePvb/zJf9RVfzL13hnzgcB9q4Md//FH+2Xf/GJ94eotOso6MOiRRRmENaSZDqlNB2tV82z/8Wr7i7a+jsvD0s+d49tH/QZlMMXnFQy+/nx/5f97K1evwV77iP7B5/iPIdAlT7oEeIdE+d8RqXv+aV/Kd/+dX8vFnLY8/9vXcuHyObiZYWcn48rd/BW/78s/m+MmgkZae
            LfC1X/VG/vuvXucfveuf8aH3/iH6xBJ//ku+iJfeB8LE/MSP/2ekWkBFAhVJKuObo08K+Xe32NrehOv9MXeAOrgCCCk8PUNrLwUTwjs4mARGYK7sQ2+DpSUv+3KVgcj4ESNWqCQhjgQq0O902F0UIzsj6QqJdsr/rEjhwqZWfDJcH9EOHhfN2OQaXMIH6DqnmpPEGjdTrOhWWEeT4WkbxQeBHmOtX9W6gAHKgAEKFMbqm7B5IT85KEM0vMpPrxHh
            OakEyhInEZUuEEajgjutcwZbTNndu0FewN4W/N77L7C9bVDJInHa4967M177EPz9b7mbEye/ln/wzh9kNKmIukso5dCmQgtPQLaixBKRZhuk3XWSTpetzU2P00V+ealtGTq91vpNaL9EquGRhnrjIRsrre/qrcAZzStfeSfv+IY/z7Pn4Nu+7Z9z/tFH+Zq//Q7e9c4vJp/AT/7Y01w8f4Fut+TBl5/mT7/1Af7qlz1I3PlyvvXv/yA3bhR0Oqug
            JLosccaSxZKdnSt8wVtfxxd98euotJdoRtIvX/qdHnvjHcp8yu42jAaQpQ5shXVTtJ6ibYFEh+tRMx6N2N710ROJTYgqxbQY8ne/8cv45m/+bMZj+Kn/+CEeefQjOG15zSse4k997uv40194lJ3xX+Hv3LjG5Y8/zrd/+3fxb9/99/mmb/w6Hv7gezl3cZNE9SD2VJ3ne0A/d1FrLUie99f8LyiAtae8a0UwuvpoVB4ATnqLlOMho6evIO45AT2g
            FxF3I2JVT46O0pgQf6m9wUBlIe56jKcOOGktAYT0JM4X7fzazvKtrf3rEbj20HfBtEFLROUQWvqVuQ2sbxM8/urtbyskyboZD82FbtPLwiyytSn/VGzNP9V/3AF+l3guePAFYT0QpTHOFujJlE63448gaz1A7woqPSWOYWcb/tl3/yCP/M9r9BZPUGnB6TPLvPPvfSlf+NY7eduXrfPb73kD//kn/gClViCroJoinVcFeNMFS5J6jfbO9p43DKjG
            RMri3Ahd7qKkCHGlibcWw2LaS5x6enDSj71Co12BsIaIgrd/xV/kpQ/Cd3z7r/HzP//rQEKWdfnDj1b84A/+K37xp9+PlClJJ6fbzfne7/+7fPGf+Sy+6M88wK//5hv40X/zMHHkFxBlWZIC+8MbHDu5yDe94+0cPwb7A0McK5LYgRJMx1OoKvLRkCTyl2M1HYEo0SVoMwnUnYJKeyA+imPiGJIkBimIsw7VaEx3oc/jT8L3fe+/5Kf+889ijEHF
            ktj+BH/jb3wZ3/Jtb+dPvuW1PPTyB/nVZ6/zC7/06/zUT7+Fv/V1r+ftX/GlvPMf/1+eOiNl2JC/2MXEbQ70mmD+vHz95G14fva5C/QtH52ws7vCSZBeHKIWutBPIJOU0yEkDo6vEq34GIf+or9p8oEjH1RUwxI70djcQiWBpCl+9Y1YB4k3THDr5iyaXL3dbRPVOMSH0TlfYEsNFY278+zNQIh2ROdgNJGxRBYYV4jcEleCpFKIEEDeRLtZFUjP
            sxvm5rQ32XSVWmuSpH6MtoEn28/tJnzEhXB1xKFKDYA0TTEaopoZDcRx7Anhwh8aNW6oWm8vuJG2foccRf5mqrNVFN6FmsapOhhTagOFd4GOOwlKCazVxJ0IFTmGo32KYkySSJAG56rG/9aalDReQ5cddJXw5GNX+a5/8iNceMbzEN/yllcTxZZ8tOPZAvkWncSSRlOOrDve9Kb7yDr7DPcvkCiDFCWKEVVxmUhc4c47Eo4dyVleHOKKq1Tj61hd
            gJZNcIi0EdJ1ILypOEElinK8wxvf8Er+8l98gMc/Dj/9Mz+HqSLEwhF+5D/9N/7CX/l6fvEXP0Tv6H2snX4FKyfewCA/wb/+V7/BJz5ekUTwwIP30elmVJWlLCzdtItwBlNs846//Tbe9KaTfPj9n+DJx56hG4OrxuAgdgKhYrIkxpagnKbI98COMW5KFFui2KEiSZJEQUknfLi8dRQKJnpEd32NH/zXP82Xvu3v8V/+6/tIenexfuKVrK0+QFkt
            8LM/82s8+sgTLPfhzIk7fMCZjvixH/1xrl2DP/dnv5D77ryT8f4AE5xLhEoQ0ext/mrz05xnkKhWmFHNH1aHTBDyEIGFvP1b22HpgPv5c329FOq2b1FridtyYHHsVzmMtsAuou66m5X7uyRrsDv1uQ35Xu01Hnhx0j/5g6eF+9S3NOE5h0VLu1bhAmXEzLa5woHQaBf7LWoSo0rZcAuFqDl+rhVqWlsjuyb8xTkRfMp4jhPteZ5Mz/f8DLwir4wQ
            gRHkOwwRAqOdFLckQt/cAYrbnIZ1JohtpHuHdoYqgiRCCEFVVcSxAqeZTsezrkG4Gc+utRnNp5Y4lURRgkw1Tz9zgyc+sc/9L19ibTUjUwKRSP7Pb/sb3HV3h2/9e99Db3GBf/q9X8/Js/DRP4Rv+eb/yNbmNVCG48cj3v72/40/+SeWyBJPr1MCzn8C/u9/8e953wceg+5q2P4HWknrpqyqiqoaISLLZ3/261ldhv/447/KJ554mmThFDLuMtzx
            jrfJwilUssS4ilDEiHiDCxcGXL64yytfs0Gaxt6wQQqv6nAl0/3r/IW/9Fa+5qv/NI89eonv/I7v4qu+6qspi3tJ4wxlZcCR/WuvJChZ+fFYGaTyLjAqYN79TofxwJOmkwxUoogTCVFMFHXY29bk+RaLi+vkpWFnd0yvk9HprlCUE3Zu7NNJQBpPAev0FvnYI4/zP377A3zJF7+Bz33Lm3nymf+EtpKos4Cp9B8ttDJnl3SrkbotqfvUusREdQMY
            1Q7PUQTCkR5ZJjp9F0uLK8Qdj6Jc3cMTJdstmQq3VSAauYO0jk91BSyKmTBYSh8OLM1MJtUNpq3hF4yTnhox9X5RVmus9GOD0GJW/GyNG7rZxrb+WHtkd6EYSTHHn/Rv8rmfrru9IaBogo7rWqOIlEI5QaQEUQTSqaaQSSle8EvsXnCKU3scUYhIESepVzDgCdO6KrH52PMKG9KZPeRCpvl/KzQlORORU7KEZUwvERxZ6fNZr+3wildC/D1fztLK
            Kg+92iMqJz8XfvyBFX7lF/8Hn//Ff5J3fvtbeeA+/2OmQ79YcRpe+1J41UNv51v+8X/jF376t2Bx2e9BVIqzwQyx5vvlY+657wT/ny98PdMJfPjDHwYVoaIMnTsS0UGpFOliL7F0sjkM14+scvToBlUFV69uoq2kE8fICAo95c77Nvimb/prxAp+4J+/m8cefYaF3hEv+Co9Lu0qg7Uaq0POcW0060ry4RBUiVQ5UhqGowpM5Y0HDNhq6uU8ucYp
            SyfLyDoKzIhIGKJM4GRJoSccX+hy5MhRnIHJyJOxVdxjtFfw8Hsf5q/8hTfwRW/9U/zHn/o5BpMCubDklU9Y5K1UFjXGLszzv4ZuscH948EAwwaQ4BJPUUInY/nYEVhcYXu3wo5bAnLpA7xtq7GQ7hY31W3m7/leSswqQbvlOPA9hQOZZT4DIuQUNX59dhYt6EqfNF03gJHDB11rzyXNBzDeLXGj3BdNYWfcV5SXliKCSUIgUYtgORU+0bNmtCfw
            1vUX26zxpfvUbISbImvFrOt3s8r5qT1fDkZ3qkOfRBJ752HnDFmcMJ4Mw8E0+z2JQ4q8xJJ1IqS0FHZEVe1y5s4V7rnrKNrC9es7TMdD5Noq1WRKEnV4y1uOMRjAt3zzz/De9/5P7rjjFbzvd3+PVz10nO/77rdy9gycPwf/5Dv+E7/5G+9lqdvh8/706/kH3/qXuPMsfOe3/1kuXr3IR97/KIvrdzCdVDh8x4XQqEhRGM19957g3nvgwoWSxx99
            EqykKgqU7fpNqEiJ4kXyypF1EuJYMahG3H/fg7zkpXD9Kvz+ez6KNQ4ZGYQqEeU+7/jbf5NXvQp+6Wcf4Zd+4be46+y9LPSOeJyvLBu+5ew1B6UMMh5BssuRo8vEkSKOUiIhWeouU4xWuOPkBqkMNgfCx3kqJamqnLIak6SGSmuETDG2Qpf7PPS6N/KKV9/NB/9gi/d+4ANARZQmqDTl4Q99jPMX4OxdJ7nv/jv40EeeIVYJpa5us6xQLxyT+yRw
            uk8aY3w+BdD5vQCR8ME7RCmUhvF0Sp55lb1smOQz30DZmLmHEJwDLZ8IILltO2G5mfadJvSodpJp3S3NKO7zRLIUsrjJLqKYQDHyHIQqx4uEq8BBqapZmLd1yMif1iN8EHWWpsgMlk8lRDJhNCjJNwewaxCqS6QjsGCoiESCtZOwVa57Gs+BidME0GQdSZqFWEVz82JBvMBiJ2scNIy0kYp8yp6znkfZNN+Ow+ZecaDzno28otGnzpU851BhV1SU
            U4zKUOQ+r5iWsD2YQsgoIU07WGGZTCYh/DsodJAzqWJziyh0oFQKWTEZXqOzuEGV73PkWMa3/p238eqXwd4+/PZvPEJeVlR2wqTcQ8oO4yF87df9KL/8Kw/TSY/y7BOfQKiUv/nVX8LZ4/D4x+Ad3/ADvP8DT9LtrzIt4N3/9ld45vwVfvCH3sFdJ+Dr/9rn8bc+/Cij3SFRvAjOuytHzhA7RSUFZ06ukmVw5eIVrl3bptvL6PT6DAYWkXap4gW0
            y1CJIMokm5efYWNN8TV//Ys4sgw/8iMf5UMfvIBSi3R7juvXz/G2L/s8/vcve4CnPpHzL37oX7O7V5Let4oTKWkGIvY3hYg8E0wlAhHB0ZPL/PhPfx+lNqRKkSpBrCQJ3krQah8uTwRCSWyUYKMYEwvixGImJTZJkQhSp9jbvsib/uRr+Sff83dI+vBdP/AveObqJZLVVUQEaW+Bpy/s8OzlPV7z2mXue+AOPvThp6kmJWk3xTmF0QaJa8KPbubx
            h6hLp+cueunmeX3uJiL9LUKObrmtU3P3l9d0v7AgpPkO8KYUMNNIz3QdPORmGlYT4DH5gueuWXNna/kTzlNt5GwDHXdmmR1RUMAVY9jbtVA4H6agmXn36WDn7zyA73Jf+JrFxtT45YTwsqTJXj6L65SOtY1FVrvrVCuweXFEpSsPckqJSBW28v5krrboaSqQbEa52irASdlIeqTj/wV/5E0tpLXae/s13ErRFEopBCurqwyHQ4oq5zmX3g46UURH
            wfF1+Nqv/POc+1P7ZN1lVo+t8MY33smrXwZFDv/Pv/wD3vM7j2BEhAtEZgv88L/7n/z3X/4gcXyMY8fu58qFG9x7dpXPefO9CAM/9sO/xft//0nWj9+FSjNvKx9F/Movf5if+okP8M3f8gYevP84d505zmOP7sJyHxAop1FYlAHlLN2Ol/Zubg8YjQylcYi4QGQLEGcI1UUlKYKSrUtPcMfdq/zjf/DVvP71a/zEf/k4P/hDP0IxgbUjPfZ2zvOS
            exf4tm/9i+gC/um7vouPfuwJFvtHGY5LpnkJIqXXS0MGjs/gNfis4k4KJncknQRK5/O2Q5PgN8QQC6DrIXhtLbgC7QqwlrSbgBOMiyFiOuFLv/TP8B3/5B0cPQ7f9HffzX/7+V9DJqtY5cLPFUymju2dMXFnmfUjS6hOQhKlfonzPGa4wxgCL7wze2HdnHvRG+rDdC7Gm2VKKTFm/sG0BdsHtXv2wAR78HGpkOcBFisNNtahxQGUQsTQkQo78bQt
            PS0pSosZmbDFC3hDFQeaS1vcWlNg3EznG3IrlE292UHA6YyLPJM7VPPtj+7TWVli6QScfajP5nXH5MY+DC2mnKcBqNCaCgeZFRROeDP/uml9HjZhN0EFQvwvL3uzrfbNMjxrDcZarK3CR5PGLCCKE/b2d1FS+vAdXdzc8bZHf2eJXIko4dQx+JqvfA0yWP6X3iyGa9fgX/2r9/I93/+jxPERrPOOxvX1NB5pbBWRdLtMxhMme1vcccfrWV6F8+dL
            PvaRT5BEy1irKfJdvFNVBbbLxz7yDKPRGzhxYpGzd5zmscc2UcJgnW2I0dYKhDQsLnYRCrZ2R0wKgU1SBDEi6YLKiKIYYUvyyQ0+90+9kn/8zi/jFS+DX/6Fy/yf//hfcunyNmtH72B78xJHj0R83z/7Bu46A//0n/4s/+Unf52lxaMUpmJze4tJMaGyCwwne8CI0o1wlb/enYDtLfiB//u/8Zu//TDLi10SCUnkUEIiNIz2tnnoVXfwHd/9jQgp
            ULICWaGEpZjmJEnMZDxgsR/zTf/gb/ENf+vPAfCP3vnT/PC/+TkQq6RJL8BHzpshaEeRV0QC1teXiaIYpVSjpPCYo3thhfDQMdc8B93ljwEDvAmkV96U07kX/oCcOKAamFGTqd0IRAQyjZFRHGy4gRzyEuz2wC8tKg1VOOZcWLvX7i/hRmtIzjW1xAZ3CuuQBLDaRo1dnM9xssE81HMWlVhkulsxHY2gL1k/s0S2uMz4OhQ7+zAugh8d3j3YWJ9o
            WtkgKvCmnRYw1sz4jrcoeu30t8Pev11trJfTNStKuBdT/A48Hjf/f95G3V/YzgqUECRJwrQsqHQRrNPNTWCnlD7qUIXnlKYpceqhivc//Aw7e468cOyNh+zt5/z3X/kA7/m9R5DRCll3lelkMDfSVJUAF1GWJbkaQyrp9hK6HRgOx+zujsjzko4tyfUYaws6cYJzgrIQFLknSatIQDEEsRpuNJ/NYIIBa9bpgPSGHFZ2EOkyIlnGxV2iKELYAdPJ
            Nl/1FX+G7/2uz6AYw7/+F+/he777h9kfKo6ffQn7e/vEieMbvuEr+bzPPcOzz2zznt/5TR544CVImVCWJUvLPYQq0BY2ji3yqjfeR553eeqJGxRFRVXB/mDCR//gWR75wDOQSETkiCPrpYajMc6M6SwpplXApY2/Fp1xJCphtHWNB1/9IN/+7d/CF7zlOOfOG971rh/iJ3/sv5H01hFJB2egsgWmnCLwsZ1eVgndXg/nHFrr5sB63sR41Ce/qW13
            ji/UCWbue9gXUACFnW1yAndGRDFxFB8Ok4t5TO8mu5w6C6B+EWqbFCkhjUh6oSDhrc30HjABlxeBxJw2BU0qia2c//WEnGBpZ3GPHIgncCH/V+ANPI0V3qwAEZzXbeD9ucYJxtSpUjaDQcHWE7ssnFhh6RjsxQuIaUqxtevp+c4hVYQtChzKm5FK6WVweCqQaxcvFzBDccheR9x6DQHejUckGZXVjTzN6EMOG26xdBeHgoz+11HrkBuXlpB4WmmI
            fE0Lgy/OaoSUKKXI84k3kbU6nEAtkuYhgKcTkrERaAUffwL+wTt/gt/7/cfo9U9QUmJwdLqLLKzegZNdCuOhCZUojx1WEEcpKkqJY0kUK9AFQkq0hizrYUzp6SfG5wEjvMwxjiXT6Yg0gRvbeIpO7LBMmg2wcxpsSZmP2Nnb9YeYkCAzsoXjqHSFOO2Q51t0Ovt8+zu/kq98271ceBq+/7v/M//xx36eTq9Pv7tEMYmZ7Je8+XNeydv+99diNCwv
            L/Mj/+EH57TaUQT9BY/O/L1v/Zt85wr88L/7GH/7676LqnQe7866ZFkCWcbGiRMekrBThCnpd/vsbF9Epknju2WmQNFBdboMhpd53We8nHe/+5/y4IMJP/dT5/j/vuv7eezjT7O4cppKK4yRvgsWCXESo6tpkL8JJKArgzEeCpHBadq7TcvZFXYTXeVAh3eTRPPwoiQO1A/nDtJenosMfYg2+AUsVaIXtYk5xMJGRBJXZ/NGEUSg0pgopJ+VY3BT
            C+Op59boLJBUk9niwh34Gbb1urtbVA0zwwUbCpqtU92CjX3w/Gs2M1bMfoFWekfpqmR4aRt5do3FFUmeZLhojXJXwnCELkowDiuDfMp5c/BglOW3tge5kC0qja2Ljmnx/OplEO0kTu9ObULYkXPOx5zQUBT5ZHZnB6kM7e70MN6fUN6t11mDNto/zgYH1SFQ3jTdpXUu7Ez852gEJZ6ZVLll0t599BZPkziDERVWllRWY2zQWh9auFvefVg2Nzep
            KlhZSdg4uszjT15FRSukUYqQjlRKhmaTU2fWSVMY7JdcvnIRqDB22hrXLVGkweVcubKJkLC4ugZKYWWfbnedKh/QUZpv/Za38/a33ctTHy/4+q/7+zz83musrd2BNVOEtl70LyKkTLmxCfs73mqpsqXnGpYllS4R0nHnnSfp9WF3d8QzFy17u/uARFfGJxiWlqqqoKqYTnJv6GBzpK2QVocC5V2PXOkzcLK4jys1L73/FP/X934HL31Jwj/77p/l
            u971wxSTmOWVkxSlnvlnutntH6kSZws63RjtYPPGLqbSJIsRxunm+nv+ZqOhCzxM1vnppgX+lHwXceBmTyKfYpV4oqbTUI7AjqxPurYR2AQh4mAL71pVzs2bQ4owvxo//jpVa/dqtxcaj0hRb5+tC6+xwDTxnf7vGWHazVl7zQq5hL0p+3KX1dMr6BjcQkIcrxIv9ple3wI3RQvQuqKqKnTlnZ9tVZGkSQPOOnEzdnrwEtIHV8ehOdXB1ks7i7Z+
            kWMO2Efb20GI4jbAcVMMDymQ1uOA1lif+xplOKsxxuBMKFDOzkYVG3IbAom8mWBEIJqr2HvXReBiTWlKcm3QJkFLsHGBERXOBi26NIHWEfsxXxqErHBKe2JwGrO9tcP1q3DmNHzhF/0Jfvd9DzMaSZaPbDCZDNm6fomN4x0+7/PfgFLw+OOf4KmnnoS0Cy4P+cW+AErlscDHn3iWq9fgJa+4l1MvvYerOx20SRhcu8ZXf9Wf5u1/9T52rsHXf+07
            efh95+iv3k1hDaoce8/BuIKq5APvf5K/9td/gJgBxXjPm9ZWJUU5pDJDltc7fPf3vIu3fM7d/Jt3/wS/9KsfZm9XgUmRMg5EaIkMafXWeJketsQ64/mgTpHJHp0Y0igjkoK82ieO+nzVV38Fn/WGHj/879/Pt3/b92N1l/7CMrqqgpOKnPkgCoskxljBykqX48ePsLMFjz36lP9dijoDxz03tve8tcDmjx33O7wAtlvM1gtkg/LAHlQ51PNXe/Rx
            kC4Istiv6sdTKEfa238UvssSqotCBO7g4WjBrAOs9bqtvGAb1CZhJKYV8yBDXq40dVhTuEFNMDdwbr4COWaRfSEG02oNaQ+u77OfdFi9O2N74DfXcRazdvo41d4+blrBIEYlhij2dlKWeB4iqEPbW6l4NlyAzrpWUqrPLqkLlJe51UWuTv7yF6Jthd34o8DN7Mpbr139OFzLQUccWFj47x/NsEWpfbGzwTLe+g2p1VWLAM+8PrSm7biaxyYCk8n/
            gmVIJPOW+ypEnEJlLdo6bxsmPTHZOiDKcCryRq4KhFII5d1epJRk3Q5/8NGP80u/9hjv+D8e4C/++Qe5fv1/49/+h//C5tXHiCLB3fcf4eu/7sv4/M8/zmAXfvanfxldavprC4zzMSCJrMQ5jbEFQkU8e+4qjz+pefDVEW98w0P83C9eYrQ/IIocf+JNr6GXwE/+8i9x6cIzLK90IR5gdcnKsmI6LSAZI1JJWZU88rEnwRVeqmctShrvmiQMRhRY
            nRLFcPXakE987CrJwlmE7OPNv1rlwlnv0SisP3xMhRA+sNyIuo5IsrSLQPOSB87w+V/4WVzbgR//Tz9GmimWlxcwVc5oso91EhX5VDoXsnGkiMjLnBPHT3PyRMLlS4annryEjNKm83Mhe+RTb3X0v573d3MBdDfnddaGik5oEBFWypnVspWBmBs6t6iETko3C7EK2nssmtHUU/dV6nG9OvXJ+JvdOjGz9bOisaSqxzCf1ub8eIwvfKIxMQ2Fr/XW
            FNNQ62RjilDf3a2U8GCDT9DTylYrpKTEFAaiDubKNrvdkyyuw0gnFGWOiATZkUWvBKhWUOk2wvoOcDoaYZXyLr0yDlkQOthlxf7wTQSVVQhj6qyuuZZNICiLAl1qok6NJZrgWq2BxAcLVRVC++5QRF6dEQmJNWEMFd430MoYyyxCMYu9+av/PEWpBTqSTE1FZXMoE5x1xP2eH8tG03DzaQ/7SXETplO7XCvhkFGELjRaeBJ5IiS1gXIxLkiSRWwl
            iBKNVI7CCcgddHscOXmGzUtXmRpN2u1QVjCtNFVpiFIJIoO4JF1Y5/t+6Gc4c+d9/NnPi/iH3/J5vPULXsUHP/gHrK0tc9/99/Kah1aJFXzv9/4Gv/oLDyOjLsZUIY/EIE2EdA5jK5yG/UHOwx/6A173Wa/ngXtP8tOjR6GcsL4mWV2JqSx8yV/6At74+jd4OV8cYytNL+tRaY1TEbsDzXe+692853f+gLWjZ5hMRyAnOBzWJmAVKumiXYzRkMZL
            yO4GSq1RMkXIhFEebOR0CeUY4Rw6iJ6QEZWrcOTESx0KQEhJEqXEwnDkSJflxYzlJfiBH/gOhoMpaZp6yaL2xXNvb8ry0nH+8MMf57u++we4eGUboTQPvfxlnDoGv/c7F7h0cQsp+kSqS1VOWsHevGA1hxXPe3g8sAxUB76/Cgf6Ad7fi5TGRbdVBAiLSGNc2TITjf06XMaCXk8Q9VImBUzH4CZekuO3o357h47mKRKuXWxpefrV7U/wTGym4pD1
            4ebrs2sXwHY2cShsbu6ptNanNUna+SLuH6eb25L64B5vU67P30AsbLDQl+zuQYWPTpQyRvS7dNa7qEXP1JG9BZScjaUKwCZNFGgFTD01EYEiVuCMnMM84wh00qWii5HQXz8DcRdnHb3lxDfSQhFJ5RcW8UzAosPTVyFWtm6QjfV7PuViCgvCxTMYLwIj4ch6TJIukNsElaQgFOV04rsObZtCPSNWz9vcy5CV4owIsZsCZML+yH+804XV1Q3K8VMk
            Pe/XV5QlOEG2cQfrJ04zGU6giNA65srVglc81EXILiJbQpAxGpZoq0iyZW5cu87Xf+N38OzXfiFv+8ufwWe/+QhvfP3nN8YUjz465gd/6Ef4Dz/y68hoCRk5psMhIq4BdhsOzQiyRSqb8sT5q3QVPHj/XSz1LPv7OzgNaeT9/5YWFUdfsU5VQBTPEjx1OOeHI8in+2Arxvt7GGn9iI/xB7mV7I8so1Hk/TbkCs4tUVUJYIg7i42+XRdeZWScDiyD
            kEmCgUjgIkll/e1mSNBCUhoYTh0rwMmzawSjGJTwpAolfaJirwOjvbNYXSEdpHHE5/6pNzEt4D3v+T2m45J0OfX5wlZgneBm0uenm2XbJ4verd/tQBEJn0Bf7F9HvPrN3PM138k5cQyt+0jrYwAtPvw66SekmcfehltVUGKEBDE9o6vIkCZW4/7CNeFbjS1bE38RRllvzFnnBtfVXjQWf6KW7OpbFMBKz8xN65nSWu9i0uoA625zxsF2zQnjTCuM
            XZewnLHx0Do7Yw2TKZGEWCYU+7ssi5K3vvkM6xlsX3sWo/3HOkoSSUEsFbrMmUwmjHVJngjKWFAZQz4tSePuXAHMssyf2GWJzFZ4/OkxH//4BRiXfNZnvoIHH+gSuSGRUz4VLJC6a5C6yguklORlwWg0CqHTFmcrhA2dozM4U4VtrmbjyCmuXdX8wq/8HjrtoDo9IMdMJt5SzzLrVOVtCiDhQAHiNGI63uLIsZh77zmGc4YnH7/K7maMlEthuI9Z
            PHEvS6tH2d7cZrK7TRZF6OIKG0f3WFlLuHzNMh4rjImIVcdHEwiNii3l9AYpe5w+tsQDD97L0WPrJIng6Wee4OEPvo+t69sk2QZxZ5HxcOArmTNIp1FOY4kwWkJ3jbWzL6Xf0fzou/8OZ47D13/jz/OL/+XX6HY6vOS+Y6T9PSbjbZx2jUWXI2E6jUmShJX1DGsMH/7gE2BielmPSTWmSPKAVac+7sEKXvrSB7j3njt43/s+xs5WhhRdqskmnWV4
            yX3raL3LpXNPsT/cQ3ZjjJwtD5wuEJGmt5RydO0opoi5fnmAMzFJKjl+OiXrREwnBqclCocSjkpPkQqU9K44OzsDphPD3u6IN3/2a/iZ//odXLy8zxd/0ddw5fKI7uoJtBUYbUJOr71FAbQHlB5mvjM7+HUHMO+bk1Bu/7472PFZ86IKsy+AQqGEF7dX+9cRD80KYDX1tIAkiYhjQZTBeAx6tA+Fhco79CJ9OEsNHzrTcq7nhRVAaTylxdZxcqb1
            dfXIO1cA3U0FsIYm7SEFUIZOcW74P1gAgyM0ZQWiIH7VHaSrEdO9EVaXdJIOelphxjl2MoDxVVSZo6el/5rJEPIcIok0BusK/wIsd0jOnEB2euR7I795tq1HUhSek1KWEGVQSOiuIlC4yT6YTXAlUsVIGfsG15rw/Awy63grLaMpB3vBFzRkslgD+TT8W4MpQQQ9dJ7SPXEWm6Xk5RSqMVIKbGnCa/ncBdBZhzMOISRpJ2GaD3B6BFUIwkqXIe+B
            yVBLa6xtnKWgy/61bdAVnSymGI9IkpyCCzg9gahPpBZQsut/HUVBFCuMy5EiJ4tLpqNdbDEOgeGVt26ThjhJUColn/gsDZmm2CoPF50GMojWOfbg64mjlIt/+Lv82b/8Bv7Tv/1yfu23LO/4hn/I9fMDep2UwegSKjZUlW0wNEcCrgsYVKfAjPeBjFgmmKpAJgqdhPbc9sFmdHtrGAPFeAAuprt4F9Y4inILwRhh94ExptwmzhSVnCXuISxpLJHK
            c/h05SCXJP0jOBOjFOTFtr/OtAKjSOMUKRxaTxHSoSJ/vUQqAiz5dMS/fPf38Ve/7GX8H+/4fn703T9D/+g9GJuSl/o2y4rnKoChCH6aF8DoMK6YVJIkTsiSDJFK0jQmwedA71/eb7a4/lTyGJ3P4Q1F7haPwdr2qNpysp9pwWfUMis8adfWr2MgONtQEGu3ZtdOeLMI4x1baoMG0YQc+R8oTeiIzCEnW5MkbprdRRSn6LKkOn+Z5bWzuG6HycQw
            1ZU3hlhYJe4uEi2vonRJVZTo8RC9dxXGI1SpyZSgm8LmYBtUzNL6UXKVoONlpFZI4zmKzjnoh+2wtb6byiIfuOMkLCz7g6oV9O60o6MS7CinmE7ob5wg63bZuXQVkSnixOBsjjOVt5iPVFiqGHAVwqUe3l1bQHRS8v2Bx1+lDGYQISy+DkC38xf+QUJ3DSNMpxMEkjRbwKUZhS49sT1ZQK2cpNNfYa8SlHvXQVkioXF6SpYZnDREyTKIPrrQHjPW
            lQ8oVxHW+WWLk4LSSuLuEvQ6OAosOdaVOFdRYaiKPOQVCKwpkXFGlKSUu/uQrHLsgS9gNKoo9y4hOov86m+9j//ws2/hz33BKb7xb30J/+jb/iXaONbWjmGFYVpUVAakypgOp6ycOEGcODa3noJEIIXD2ByURQvnlTTBe1Aky3QWjjIal0QLC8RxgsOSFwMQFUkqAsl8CJ2Qq2SY49+V2gT2UQclJfQUxvlsHgMknYVQWDKEUM3CMRJ9v/JKBEUx
            Ik4le3uX+dqv+1K+/Mtfxs/8/DP8xE/+BtHqGUoTY7TzD6CODb1dcbmV4w/PRaK2n8TIqmbXm7CHXn8vWgkSRzFZJyOVCVEiGO1VDEdT3/GFmEAZ5GgHeWT103IHOGWuVfxc/QlOzI+vrTiOJhO2VfyaOAdjWkFGM9wPY5sFCnWYebBqEa1kM9eisrmbkNgDJ05ZIVSC29ljeHVA9+giZZqgC43D0ymsjbCii5MJdC2GCOXAyn3M1g7j6TSMbn4p
            o52idCkaixIysHnsvFFBkJ/Nku8kuATjLEbEzWvtqoLcecPP3toxugtrXDt/HvYmkEVNDrKpLcKMbJ64/3kpUb9P2l1ivL3fdOrKyvDyPn+Q2ccbzBREDom1knKiIeqjltY5cvJOdkclo+kU8uBm6zQOjUUjMBih0VYEnr0I/D85u1xr51ts0Kvb2cbbRbPRo86lCFEGNjgelntTSI+xcs/rGA0V1aSCaQllQa5zvue7/zn3bPx9vuSL38BP/fiv
            877f/zDTchXrBCJJcUSYHNKFDaIoYm/vqu/OpcWKOGy1hbcOI8WJDjJdIE6W2BsWGKeQcYZLYvLx1HtYRoa4Jyn2xyDLBsNFhshT2SbDhqnLeaqMNzIOfFIbt7olh60/Hl6L8SSn011kf7rDiTN38rV/86v5yCMV7/z276MaR0TLPe86bcWnAPNTt84GadgkL4IK4+SLeGy36QCjkA7vSsPulQkUAiUiOp0Ok7wEauDeYmpFANZvi+tcDtu22T/w
            Vhc/e7AI+vddQ3Mxs/cNEILAG+1ZbYgQMn/nip8Jcrdg0V77L9dFWcx2Ibf9Y7QmVopqVJKfu8LyiUWiLKUynggty9K73gjvNoNzEPcQ3ZTELVCUMQyuM9UjvxV3gfTtkub5ccC0tNmPtVtpGwXcMkY44yV+gf9jp0PoJKQrfa5dugSjMSz60cw7Z3sDTGeDM3Kw+zJCQWeZpL/MeGcvcC0jjNTEAcooX4TY3AlJOXWQrBEvH2V59Qj7o5xiMoZy
            6t0uTBmiMB1OCIq6IIZxwTr/+oqWukgIg3VThNAIVwbFSsiQdrI5pLElygqkKwINRWLLCpI1Fu/7bPIqo9wdQbEH+R5OT4i6EecefZa//uXfzAMveSnPPnORTneVUmc4lWJNILiKmCTrMhpfody/DLIMxh6x7/gCbcshQcV0OouIqEOxN4Q0wyrIq8CUSDJEXFKYAbgyHH6x/1p7GHFY+kJnE6RLaIxF291YOO21NZ4m5UJ8qkwZ749BZly9lvNd
            7/r3PPbYYzzz+GXoraBNzbI3iP8/WXS8wC2wIHKCqijZHw5RLiNOMoTWTPbHyCQNC1V5QJohb7NQrgudbSRozI2vrbbMHsjxqEOLrJgVCyNmGJ0X4TaJaXXxI4Sde5PWGkLx9Je6Zktsg/3NOG3zJqg4Q5WXyDjFXr7BaPcs6ZEOhU6wuW6swJyosZrIb+mMQ3RTFuOEoXS4IZCPIc5QcUYkuxSRwZYhftKKpgMUMxwgFMGQgRvOhPp5WOGwpoBO
            j8WNNXZ2dmB/6FeUSYLCYMZDpHY4W7sAe06lET62sdtfY7I7CA7ZquXgPG9N5HljtqnRsgblW2Rv4SSu4bJJ79CzuMjq+gmEzNje3PEGnrHwxMDpGGSt/BEYTHCisS2LNHlACUJjwSVsKJwufHJ9MCLmxg8bukdLBizTO/NqLEsUkyl2uo0ot3F6D8wUPbWotM+Fy/tcePY9ILv0FzaIbI9Ch2tQxiyvraKLXaablyGuiPtLVHkJIg5jb9TwZUW0
            QBR3qYxf0UsVYcsSJhNwMb21LtYNmW5fCwa/3nZO1Niwk7OlXNsavm0Z33DEZFMA/e0h5uMdpER2F5BRip7s81M/+nPQSUkXNyjycqbhbIXD34bk8iKJzM8n40PefvR29lNTAJunahzSOp8LEQlKYdFWI6Ul6qQhK0DOyLdiRmqWLQzQuQNB9AeTK207kd3Od4JWNKlu8zO0aMlrZND8mllcZKv4NcHoIRjahZQ3Z/WMB+gM6hCjAufmwV+nCx/N
            qCJGl69zcuMOhj5kF6dirPPaYx8qDxhPVgWFthHJ4lFKJXET3wU6JMQZ5AUuUhjjcCr2z1EHtUr9gppgmyJ0cBjwC6AoFeR5CVlMf32dclrC/hiiiChNcTi/xcU10eLOOdIIcudgcZEjR4+zfeECoihwtmpMQv3g6zzgPOftJhsYQYabyziLcaXvdlQGU+uzmHtrZMvrdOMOxbgkz/exVdjqGx1IQl0wJba+kUxd9Wr9Yy3Try8dG1L5DLLmrtfF
            gTZ3SiMocFR+g0rs8cfOMVbv+BNMiy7lVoEo9yA/D2IPEee4SPuHh0QsHPF65KmhcjGldZ6r2l9mcakH+R6j3YsQW1SaoXP883Gxv61EeMuW6fXWmI5KyklFb2WZKq/QhcLamPWNPqXeYTy47n+xQvoi6mQrID6Y/oY0Qp+S539XLmjNlYgO8O+EL6Ii8Z2/kEipkMJhbYWpvKZarK8hXcAWVXBYCrCRazPeb3LesAemlucRTHQrLPFQze9NlWlu
            4SJEczIfwKJfRAcoXSiA7QZIeh1t++m2xd2NcYGdUfucPZjINp+y1ohZbWsUDhezL+rtz2WmBMHOfY0L3ZEMRbQee2WguNQNpay1tcZgnEUZM6vOrS2SCzfSDNe0SOlxLWGtP9kvbVHedQdpL2Ki9cwQogm4qu36wUqFjGIcKSpbQHcWQVY+YEZGnu8jAhYnPIaJMrOEOnxQtHMVjQVOAPTzwmNHS0dPUZY5+f7Af13m7atMUQVqkg6LDL+Rm5oS
            2e1x5NRpNq9u4ooSpavZaR6WR446pIpWWH2QT7VoMZ5imPnnXQqQXcT6CXr9IwgbMdjb8/SbYKteg/o29OAeu7JzxFjn2qd/qwM8rOtoLLL1rCNocoDxXe0Y6J3i5L2fxd6gj84NFAMoriPEACVGODRGuqbbdiJBuwRiS+lSnEghjVlcXEDn+0y2n4Fii2RBeVWlVOBSHJkvuCIFkRLHS0ymYAc5/bV19DRH5xWRi+j2e1TVHuPxdTAjfx0J1RJ8
            t8KDapnmnPZR46Q/VKxsTWHBiMCbGIQmJVJEkcNRQVXgTAm2ahxgZjGzf4RGli860+NTHxl7Sy3wCzX0tM7jNLa+gRzzGF/9Sc3IK2djbH2K29niAxfchW0rI0PXd0dti+W/3tYLD2vmjgAhglmBrXN9rZeOOdeoF+ooy1tZf1nhXWmMAykj7PYOo8vXWXrdUSYig90c1wQ0udkL58Ap5/M7XISKO+juIoihP+EjFU5c4ekmDcNdNhwigfcgtNZg
            hQmdq0WICOty5NoqWJhu7Xl6SxIhu97dg+k00F/CkWWN/xm9BZaOn2S8vYfb2wVd3bTImCX0zZ/OVvgOTCsd4iYlTqngyhmD6JCevY9IdiknmnJvGygRwodPCRk68JrJLnSr8Jp5oLiJsmxxpYTFBeG3M2bWn7QDmML3bLqnPIaVuzh9z59gb0djxzmq3EFXT+H0DRR56+YKzPB6yRAWGo4Eoh5ZJ4Vij8n2U1Beh6TAytSrbQJEI5Mu1mXgOiSd
            FYRLqXb3EMsblBZsXpA4iUzBqYL9wSVwe/7ga8Zd0RpxD9CP3CGbV1dn8YTNLQJhFIIIZyRJJ0PKijwfeKzS6MAjm5dpzqgahE7Uvki7+v+XYYA2+PfZOsKydla+nYbFtra29XYxdH6unoPtLGlNhGLnbAgxqjG/Fn5Xu6x4lQYz0wM36wybPOHm59hD+19Xd3nBQ8uFDk+0KBu+Ns8yf2eAcnjsMmiRrIU4Y3rxKv2XHUUqsLFqfU6rACr/fISS
            mMpv1ukuQmWJki4qThBJkMLVDtNu5sZN0OL6JLiZ76ECdD6BlRUWlhfZv7oJo4n/RXQSoshRjidQjBtbMJx3rwFYPXGGyjjG165CUSKVPLQAtjsJ2pzyGTXTj/JaebXP4jFWj9/FaFwx3t2HqkR0IiInEa7CBKdpS0nja4b1Grl67Go6uINbM5r/c02hNLORg9ZNW/9tu+BS0pN3s7x6H9vbDltIVD7AltfB3IBqE+KE0O/ONs0uDUUwawyCSQUL
            SzGbTz8FxQ1kXBFnMVrUIIuPgvUdYBdkhyxbYXBtG7G4StaJmQ52wVr6/RSnDOPpDah2ICpmj92pVvF7sX8sC4sLTPMJZT7yPMnowJh5O/cdbrWh/eNajtg/ugLonofO2eNcLScW2njpTPHhbhp7Q9EKkjYxq3ctM1OBs4Gk7Fp8yfpzjG06RBG6x+Z8bKfUtXKITbvbqD9ufKc1K3xmjmY590KbehOpg7sxCBVjdgaMdwrWzqRsDoUfZWUA9jEt
            JwKB1hohIrQBkj4QaBCR9DedVIHuYg946vmb0jiBcAoZJIG6KiDpsrC4zHB3H0aFr7a9iKSbUu7tQDEFUXlTUxXhKn9Br9xxF5GI2Ll6CYoCEcdhRK45pfamzJC5BqFJvUtCJRbQ2aCzcopEZAxuDNHTcXDwKXFGU7kQQNSYbJTzBcy1NI7WNB3erBgyY9ULe0APacFV4fAIAGli/LeuFomOPsCxky9he3PKZDAktjmiukg1OYdzOxC1iLqudhlS
            EKcgajxPIPoZi4uS7a1HQVwn6VQ4GVNoh+p2cS5GxD2EXMaODcnKEkJ2GVzdgf4KiwsdhrvnYbxDvLDO2E7Q4+s4veWLX6TDYRMzbyjJTXY/TspDcDPlO0BjwVbIJCGOFEI6hvuXAxxQQwRi5qjeUIraC6/nMWYKext+33MVKdV6TvbmWMznU/zmLLUOxKsJdZvHyE1Z3fKQz/Ak43YncNuqeMgPaIpfq2iFmErRjLmiKY6ikabJwFUL3ZMNF7Z2
            TcRlvfC43YjunPM+eral6Jj7uDcMsAcXILXT6sEk9gMOK5Qlk0tXiAWkWQSxBBUelAr4Qf2+FIjGG6wLMgtFjzBeyfC+8qB5s/H1F6mtaUBWNMqY1WPHKacVdjjx+SVxwtrGUcrh0G+adeELg6s8fjgeEK0fI4673Lh23W8fs2zuIrH2OWyP6u5IpL5Dmgrob7B87G6c6DIcTtHjUSioZXjTYKdYW2BdgTWTMKJWoXBVrRvTHBh/7SEjcW1rZmZi
            cGFn30MAZQzTjOjYQxw9+kquXdXoHDqxJWKbfHIByy5RSsjubXndOQlRh0j1fPHLDelqnyPrPQY7T2P3nkVEFTZyVFaC7GFMDC7D0cG5hGR1A22h2B9CEiMixXA0wNqSeKFDlBZU1RauugHRxJuJoG/2NWvG+ee490x9L/nliMpiokhSmSn5/g2v9JF5q9C1Orv6tasPnXbH/YKK3yfL3+PTyA7rUwZ0uhaFw81t1EUojiJQY2SLSF2P3K7pCEOX
            Nmd/5fG/2rDg0PHceYcX6uInajut8HjazjY3nTC36H5aC2sXBS7dhcu4V95Jt0egR7Q8ueoFkBRBq+vpMVKlWJkgZOIFNFFwMFCytYyRbTmM564J6UXwAtKNNbRwFFe3vLI9S1ldX6bY3oFpHjC9urgYTzVZW2ZleZ393SEM9v3PM0XDrLhl8RM2dOstusVwClmf3sl7iJMFBtsTT+nQOciikTfJwMvUVgcb73CzNZ2cuQWlor5YzAFu2yEF0VlP
            yBcaK3OgA8UC/bs/g9X1B7hyeRehFamd4KaXGe88CXITkWqQCU4L5lNcvOuQLv3H0+WE5aWIG9cewV17HLoCJxTaRRB1IQqFUnbAZQiVIVSCHU49H7Cb4KoRbjKELCXpGabj85BfgcT6DhQbFh8Hu7IaE/Q8PyHVvOJB2HAgdbwWX0Ha6xJJGI/3/BSgXKuwytaOQ88YDsLOL1n+KAvfp0HBO6QAerqBcHicSNTTrcTWVtgmeIBYEdj24vkVvxbn
            TwTLeuGcj0i0NYYnAnlZYI2eOTvX7GLDAcaynevYhJE4E4qarUfllhD5QCfXthOVLRa6aykxXKvyKRuygOsbVzjodWE6Zv/SDr27V5Fd5UdpHcTMdQGT4JREO4ey0hPFdezhJcksnc60Np7mAEM74AQWi+xmdLtddp9+CsoCcPRWFrBuymiwg6jKsO01ocPyWNvqyRNMplOKG9f94y9G/gBSISrT1aRX29BNboZGIj/6Lq1w9PRdaOvYvnrDXxux
            9I2hFTjt6TSqpiu5WrjdWma41pKjGUl0Cxtsd4NhOy7mlyH+cAt5zUKGIrTM2oOfjYxOcOGy5zYmbkKRX8WNL4PbBpV7aMGIBgrxHoaRf44GyEvUuuPE2VWefeZ9cOMp36lVDmQXZAJRJyxKPKldiA5pssB0cwBxguh2SJShGHhyc7fXQYoJdnzVL8LqgifV/LZ3juMWuK0tpcfseq4fr0T1eiSxxtmS8XAIpvDFVbVGaFcTs0EId2D055PDHN0h
            j/l5b2o/PQphJJxXSTgZHnwSYeLIZyPUwUJOooI+1+MNNIacxjBvXUXLBNQJhPP6XXuA1+dpKSCd8PertoiAczWsfuNmBVG0lhbWj+lCC0SlEFqFRUzbiWJG62hwRucfsLD+hvcW7L5g1jw5G/5dP0ZbA++hC5EKb3JgKiYXL7L6qlVfMEcTlEgDTVGGm9L58Rjr+ZPKO/9qGSYWW3rgvCZ52xbRtVE2hAeTSlbvOMrOs5e8KDt2LJzeQKkxe+ee
            8ZCBBKfrLV8F0wmrr3wNua6YbF7zXaGpfCau8K93FciLtb7YIoN3IP6xm8Lf5EmXzsppOt0Vbmxt44oxSgqkclhb4KrKczKx3ilFaJzRCDTSzLo7G0YvZ918UQRf/Iw+UPx0cxBJZ6lKjYwkkfR8Nm1LMBFsPMDJBz+P7WuQ7/tsGMw+VfU0bnIB9CYyyr0DuXU4UQXHIn9NyzjF0oEyJTp5lKOnFnn22ffD3lOQ5sGgNUGbcOGXzkfbJRn9tVVi
            2WX3yhbIjO7KMriKyfVriKTDwmJKVVxnuP0JiEYEMNlfBDXscVDW5SIkEbHIQEh0SDb0JhbS+2+pDNHNUJFhOtqCYoiMvO+kVS0fvRYnVsgAK81hu/Y2qJg9vNgd9rk3ydrMLTq/2Rh+S9RF2MNZOeI5zBOe8+PuQAHEAmoOUyutwWgXEtb8m2sK4Ox72vqgqotVfdNaN1vnBxWHcDI4y7rmMJdOhKLnX0wRZjIRMDAjg3Ss6QR1Cz8E5wTKqWB6
            4PukuTGpNcKKgEk22+2waZHWzojPjRB51tGKlp1+27Yf52Brh8nAkmSCIokgt0ihcEIEsX4NnSn/9cYXQasO4RpZZl1r3fXakIJdTOndc4rxaITdveEL/coC6ULE1tPnwE0RUYKrtC9YysGooHfyLP1ul50nn4LJKKS3l4HGbsOhoBBi5vcnsbjUa3j95iaD/hqr63dSlRE7l676zGSpAI21lXeMdgFjkv719O8blK1/VvCXbGyCWhpeaZut99yy
            w826vVodEqvgb2c0pjIgMhbuez3rZ97I1Wsl+RgPDdg9KC/i8gtQXkUyQUodyN4W02y4JdDB2h4kHdK1Ixw5vsqlJ98Hu+dBFH7EFgkC6bFcYh+ilS3RX1pDV5LR1g50V1hY6FMWBcVkB4RmsS8piy0/+opRWLyIRinjlzftrslz/YQM90xNrwlfY0j81ycL9BcXmUwHlLtXgTFEfsnUqLSQc4bCACrO5pYBBjf/Wr9gi/tbdXTPYaD6aTIKH4oB
            VpXPung+cN+sUzPBraXG9xzCCa+ScCCMauIWnfHdojPhhLdiNvppM8PoKqBK/AkvQbioRZWx4WX2QTOO4AMoDgC5ruZJ+eIihHhRLbjVGqG8pIitbXavXGf9/uMMs4w8n5LIJJCvhWfjqzp/JAQTi9n1j5htxe0coB0oQs6BLpHdDitJwqVzF/3rpQxrx4+wP7gOgzHEEmkc1pSo2GGGe9Bb5NTd93PhmXMwHHuLrUB4NtL6kdrORiwv5dMoWVFW
            xi86ZI9s4y76nWWGOwOKyTR0mglOCExR0ISyNDSVYLXlzIFtbrDMb/4dZG2h+M5sjg4sQ4IZpw0dYoKlKKY4GUPnGEuv+Dy6nVNsPj0g39rxbjp2D4rLML4I1Q7STohEibLaDymW4LEX+a7PLoDYIFtaZeWI49ITvw+b570IxUYgI5wIv3MjwcSozgqdzhFi1WF0fQvSFTr9FaTJKUZ74CzZSg9tt5mOL0K1C93G2nm2iRUH1A5ChQWYaihH/isU
            wiUYEpKFRSLpyIdb2P0roApUN/HLPVvNFlbGTxzIhCiOQwKbbaU2HhZY9Hy3u/NuQDUXs2kmDo7YtwxDkn+sI3F0+E1uQyxe8HgLxGZx4NWRdcNkZ3581rnA/aoBW+URLEktZA3grPVgvIhbGk48vlLjQhV+TMgd5JVPm6v8xsynjklQMgTw2FZ7K1v6YnOzXlDYF5Qo72yrKzFmRkWoDO7qFurMcf+YpfTQVFh+iBrKEwTr5hKkD/+xgXJws1O2
            mHfNdiXHz55mdGMTdrZBVGQnjhBhqc4946VvOPR0EBQgAzAVp1/xKkaTnOnmjcYr0LuOzDplK31EqGiNLmXl/IGztsHS8Xso9ixbl/fAFiRK4ITFUOBMXckPLCaa4mdmFkDiFtvdpvOc4X6uXQDbWsqQT5OXFmyGOnYfp+9/IyY6y8WnrsP2iFgZrN7H6MsweRbyGyC9jFPZerGgsDWeSdf7+UUL9JZX2Vhb5NlHfg1GFyFTRFHiVSMu3CbOj8hk
            K3S6KyRJj50LN6Dri1+WxOxevgCyJFvt0V8QbD3zLIgJdGUo+vW4e+DWE63cGydBeqDY1lG14XEvLS9T6orJaBtGW9CxJElEWRWBLB2I3GRARHdxxZsLG4MxlS+Qc8smZhv05939fZIY4VzB/7RZghwIO4piyHPG4xwyNTc6O+t8CmKoZTMVmZwj7el6A0vkOxkdVu2Rg6XYKxYSnzUrQ3PUCCPCfSKV//5GghICqgQzSiiHeDujQelxnt3CF0od
            nE9U5PlptDBJ6zMt0CZkWjhsyAduFh+HxP4JIXxIUrvuKzWrVnEK13ZQI00nSShTN+f36JsX5a8DhT+RZev11hrhlM8XNwKrg2dhKII2nxCdPkohCvauXILxCDY6HD1xhCuPP+6T5GOD1oUvqsHrKzp7J6urR/joex/2r70zs6WBDkqJIHGM4rjRSZupAdGjd9eDdBc32N7cww4qD2GAl08J3dJr20B3qeapLE6HDtdidDUrcsLz1Ors3psyDoRF
            RQ7rvCmCcJo4iTBVjrUVLgeiEyy+7DNZWX85+7tQDHZgkCOiAiP2seUVKK56E1ZVooTHDv0yR1EYcHEG6ZKfLtQaaydOspSVXHz8d2By3Xd+0qJNBVECMgUbg+lDcpLFtZOouGLn4kXorrO4sIopJ+xuXwHhiNeWiJMRW88+DqqYLSNENCtwDT7iWkuQCKW8kYJ1AmckkcrQRYFcWOXEsTW2rj9LPtkGN4UlCZWhrBxxnFJNSj+ap0tk/SPESUQ5
            3cWaHK2LOSx7dtyKWYJhE+l3kBc4f0+07/n5fWhb2lhHXojnGIvti+8EWwW74fk9FxY43wEezjz3kXgi/O0voEZYUR/upl5MzAFu4YUsQCWIhYysE7b9sa9blTVYVwULdek3bLatwQ2JcIlP7U5SSaRgYRnUyQ6i6OCmXkKpR5APoRyVuOvb/mbShK1s7L+/8qRWdBkcRwzC2aYbmldAuNmGskk8q7kwQREilP+eeyMY5XSO9dkdh1zgemdTk2tx
            h8MH+JNZGjnD/SpPFbHTHJRg7cgC18+fB5ND5Fg4eZTJZJ9qZzNwJb26xeoCqgK6i9z/8tdw7ulzsLcXog/9RW3b3Zf1eJ0OqWhuWkFnnZMvfSNlmbB5cRuKqnFPlmisCLw9Y1smBOZAR2HnlxsHRmDXxlPFzHwBDAKNqTRCOs8rlw7hCkwxhcKgTj7Aifs+h7xa5vyzW5BHiMqi3AQnh1h9A4prUG6C9ITsxmjB+gQ8kXZxMoNcwuIKR0/fjc33
            eeaR3wGzhYhKpBJ+aSVUuH5SMBkiWWP12L2Mc0N+6SL0+/T76+BgvLcNytA7tkpRbTE89whkVdj0RwE3vgV+JvyYL1BYq8AplIyQSUqZVywfO0XS6XD52kXsdBNnRhB7ExAnY7CKaqBB9kkWj7DQX2Y8rRju7oAY+PtwTlVwsPN8sV1eG8M82O27eRJ0O1Xw0wAHfN48QCdqXpf1zhSBcuKCeYJ19VrYj3n0O6SLEhUwDF15l3czbPPdAuArAn9Y
            hcMyuPuY0Gmamk6oYKTBas9Mi7oCkYFcgbSETpVQnj2OGRuK4RQGFWzmkFu/OW3TMAK9xTXgb6sAz43Mh1wwDRMhguGQ0Y1dFk/1kXGMzWdbZPE8/AbrJZFt4Z/OWShKlk8fgUkB25uez7fS48jGCs889jEP9Mt6XPSEYxysvvx13Ngv2X/2nPfb07PiJGnRR/DuKy5SuEGOOHY/p+98FZvXx0w3NwFJjMKaAUJpjPTE3NqKqjGllu2lU0t5w6yz
            cy5gf60CLEKwiwgF1GEQVIjQHVobHLv3BhCt0n3pZ7F67EG29xyTGzfCcsyn1Bk3RVTXEPkVXHEdKYZYqUEG01obBVeYxHMFdQJLx9m46y6q/avsPPUhsFtEPU+e94uBJBSusPBIj9LpbpAXQ/IrW7B0lP7SCtWkpNjdASVZP7VKZfcYX38SkglE0o+iNmos7ee7Ec/zJDAHHIJIRujcoJ1AZo6NM2coqwk3nvlwWHQYiIPBa6E9hKRjiI/TWz9G
            msD25mWEGRMrg3bjWZLa3Jg7f2GKg/Sn+Q/MF7HmcyMEKe043aazbwpcdesuzEleEBb1x1IAxQyidULi9EHr5nDKhtFOLvfIev7Gn04D7FGGpDgUnVjN8Ne6maj8va8N3iW4Jiy7QKIVYLIE0YtYWIAo9i3WpIShCaYGShADagFkqkh7feJliFeh2LNM9vegyD0RuL3JndtKHvTwOnCh1B9rxgXfuQ63tum608Q9RTGqZheJsLN/H6yEzoPwGLC1
            +sVYhBa4Ioc0YmExY+vqOZiMIdKsnTnOeH8Ptnd88RAujJ9455cjx9hY2+DxD/8h6JJOJCgaaZluOi5ZY4BEuLFh/f7X4rKjXLi0BSMDUYQsKqBE4ZUK1mlPmDE65KnUjEE7c4pB36zrpWWs2VCOZgsRETpAGbbIURpRlEAhoBRERx/ixJ2vpFDHuHSpwudGKjBjlDQYY72eNr+AK66TuCFSlniWpJzxF2vMT3ZQx05y5OhxRoOrjJ54P8ghpBpN
            5b2FnPKKHXpeU5yukHXWibM++1euIVdXkUmf8XiK2x+A0qyfWmEyvs7k8kegC6SZv6CbDknMdz0B/BVosFHIr4nQJgKVsrDQYX19hfNXnsXuXIG0gkQG7maEzQ3oFMQCYukYy8snMaZi58rjIPxo5JxGKOcx8mCjddDg4pbjoWC+UTns/w9Oj3VgdV0Q3S1ka45PAguUhy9ihP2jLoDS58sG00oRC3/QC+FBO6aQShZ7CUrBeAjl0C+FazWYsFEI
            LPLduCn9GxXY/XoLHIjEbeulsGjwf6aUwLaUkCWsrMUsHIN0XbBThGW08WHbkVS42FvTFT0QiSRaXUUZSzno44YD2NnzGFo+Cb+MkMurvExNOsIY1JrsHSil0DqMhkkE3T52c4thPiVa6lAFVZzDImQUFtqt4qei2VRtIi+VA899LC0yd5hCI0/1KGyB3dmFvV04vYFc6bH1+FMwqUA4lDREUUw+nAAJL33jm7j25BOwdRVsidF6RnAWGisroshh
            TIkb5pCucPJlnw3xKpcvXfevv3Vgcu8cY3Mi4SMIFA5rffEULQqLamWENE4tYcyVzicJti/SeuEiTBE6yjIUQP89cz3242l2hoUHXsvi4hkGE8FgVMI0jOB2AhRYPQluzleR1WWEHSCjKDyuCIvPRcZ2wK2BXGDp7pewuBizc/7DjK8+4otfZPzUYmOcylAiQ5ouxvahs8HK0bNMRiP2L1+ClZN0+ytM833c4DpRf4GlpYxieoHJ1pPQLT1Hduqt
            sVzkght1WBiFA1VIT8WKpOe9JjIi1zHGxKyeOkUvnfDs478PxT50BTJWWK2xOoIyAbuCXDiFSI+SLfQp3B6TG0+A3AI3RQqLs8GlvSlCMixIZtIm58Q8Jlg/vmCu6hpTWsLycda9CaHCIVxv8mqRRPByFCJ8f9W6Blo2X/XS7NCRXB0gftt5JQwHifS34AFa86kZgW3QnrkydDXedYf+YsdLMKeQ70+I0y7KeEqSKR06dzgdsj2MwBWBpKqDA7GO
            GhNRPw7KufwOXan5bBED7Bt2r2l2P1HB6YyNhxJUBrs7koISg8MKiVMCE9c8AoPWjs6RRcxCBkfWsOMJ+solmEz9fO5EIAEHxUtz8gQupLDosvJEtDRBJgprKihKRsN9Fpc7REIGXFF6iZmRrUzisISof0dBseZlsRoqg5nmIAy9fso036fY3/HF5Ng6kzzHDL3vn88mtlRlCYM95Bs/Cy0l051ryHKIEjrIDk1TaITT6NLAVCOWN7j/pW9kZ9jh
            xoWrPpDYVAHgrakt/k0Yh3We4iCD354QHNCO+gLocdXZCByFrZANn2/IEU4jpEaIEqUcWmsqE7Jz5TLxqZdy/OzrmOY9rt2wOC28maoNihE3BjfEVTtQbIHeQdgBSnrfRC1iHBHOZCAWvLGBWOTky1+JiAwXP/4B2H4U2IdUIoXxEISMcS5DyEWIlxD06C+fZDAsqLb2YXmdxd4Kg909yDehr1hZixnuXSLf/jjISeA6RkjX8XuukPdxs7lBXWgU
            zkWMxwYiyfE7TzOe7HPxEx8FMUEu+OJhdeXDp00GC6fpds4g43UKG6OtppjsgR147JPccxaRCOspPE3hcQcmk0O7vzaNLGqgqjaGKURL9CBu9z3qbvAQGsxN+OHz6AKft3nCp6oDtLULcPCTS4BUoRIPMY0HDjepEE7S73XJx1AVBlsZr+ed1guJQKQunF9XNvpcOYfPtoufaL0/d0JEEU4bKIBHtrhxHvqvOsHpe2BrnDDMS19U6l9665CbTsYo
            JHEsoJuy/IqXMN7bo9rZ85y6UQ6lxVbhtJYtixthg+tRDInECoeIY9x0DJsDkhPHKGOJjsCWJiw8HU7L2VKmLHzuMXgjgyKC0nhpWzUFkyOXM7rKcf3aNRgPYKnPkeUVrl+9AqOBHx+lodJTXD6Bfp+zL72H8+eepdy/QmwmiCjCGY3BmxJIo33XOShQx+7inpd9JpeuDhldv+xlXbbwNJ3GaMCPyzb8n3XBxKBZZtj5jaGwwbh1ppyRtZa7xoek
            BpnjRIlxBRKNHo39z0+XQJ1g7d4vIImOc/3Kvrdo1wEjcQWQh9FhCu465NfAepsnoSI/SMh62SM9xWXag+UTnLj3Tpzd5vJH3wPTC9AJVBMjsNb7LmJjjInQMkUsHGF57QT7+7vY/TFi/RSduMdg8xoUY+gqFhdjtjb/J273AiSGdLFHMRrjhMTKwDmUZWD9t3h+9XVsI4TuUI2BlbOcuvMsNy78AeXeJUgsUZyhTR5s4TJgCbX+IGmyAbLHOC/p
            dQVGD2C8FfJVauy5jomIQrcm5gZeiQ2HkriZydF0foFSI+KGAudqLu3Bhc5ti9JhNvcHN8CfRjzAuS2V0A0OCLCwmjEtQU8qzKREqR6RTKhyw2hU+DHFKQ/qOue7nBrj0sITOoVESjETPrj5MbMufrL1fnt7qlTkG8ZKetBvNGb0+0/yifN97v7MEyS9hO3CoFUdNCwb4F51E2xhqEqDEY6yKFDdjG73BG5SML2+A8MJjKbh5qupAUFKFEXIJPYq
            lbIkihTVoITdMVkFEwVWOY8/GYu0kddAa+ET2ipD0shfSygtorS4qvAuLqJgeXkFWQ5h5zpUBfH6aToyht3dmamALHHaF83jb/4sEmEpn3kc9BghikBstmAmYVKQODKio6c5fvZlnLs4oLixF1yI/U0qlcVWRbMwkWgcFc4Vs65OtNyX53CYtoa3anh7OOG/lyixQnv6RjhIrMuCzfwxeifuo7t0P5vXY8inJCIhxUFcYqzG6AmuGoIZ+mxkfRnM
            ls8BjgyGYEXvrC8WYhHkIvHJezlx+hT7o/Psffx3wVwiU5a8Jp2ToEgRJEHnu8DisbtxyRq7W2MYTkmOHqfbW2Lv+g0o9lg6soBMNLuXH4HyorfgSlOKyTBQXYKuvo29EjHn2Iw3Ws1zSe/oGdbOnOHC4x+BwdOQVAih0GMg6YDqQ/cIaf8U1h1hUvjNL0oh5ZTR7hWY7vk8kZsWGTOSde0JOcc7nMsX8QXRBRmsD3iqi6C4xdL4hRBqD/IA5W0I
            0v+rCmCb/e0CHaMoKIoKm3junlARC1lGLP0OQecWpg5MgrHWS5KqsBk0aZh+pB95dRh9tZsB/haMbrlBH0AHsA6rLbZ2/bUtl2Kkt3tvTE6t52hVfXhyxNODc9zxOXewvKLY2h2EhK4ZjuGcQDqfuSC0wZkSg2VqLUkUs3DiGBQVk/0hZjCG61thPPTefULFmKp2gVUehO8twPlrqFfDwkLEZBpwIJRXaJQOU1pPK9kbUI79YpBxDkWCKIzPB6nG
            QMXiWsbepfMw2oNuzNqJ4wy2t2AwDLyjAiG1B1uPrrBx1xluPPM0bF4DN8XJEisExmqUnWJyC/Eai2dewuLqnVy8suNxxCz1Li5V7tcZ1vhlhDNNPoipwlgXBPQ6pLjNA9Ct901Qh9S2YkmKEYbYTXAm9yTxynhbrcWTpHe+CimWkSJjsGVhNAKbe/zK5gg1xdoJrizB7EJxDuSeJxerwv8tvW0YJqUnlhhXGch1ll72EOsbK1y98AiTZ98Dbps4
            Jkg/Yw8P2YKsc4yx7gIr9DfuwNBjfGHLO8IcewmxEuydPw9iQrYicWqT3UuP+eVLl9lWXESBwhDN2O0ybMyaJESBiFIEEXasyY6dYWl1gQtP/A7sXyRayNDTCqc1JMvQPQPxBiI+Sqk7Pt8jVj6VThn0dBOKTd9ptuSfHrLSODf1tmtKAYokSTDWYWq3dR34iWHjHUVdhIoRxGijcZiA6R7kAdIoeQ43z7hdF3hYx/ciusDnY9oqbscDbFvxOH+j
            p2lKt9NFZRlCC6aDnP3dMaiOD72xUXBckgHHCmOtdt6O27XAw8ZpmdaSQ8w2yW16knU4PQs2coGeMrPNqj/W0o9agbKZ13de2ubc7z3NmS+4m6WlJfb3p+GCNL7eW+F9B+tTTwQ6D5YKkCpCZBHdpAOLBXmvT3XhYlOsXePW4jlwVmhPsh5Lqt0CjqaIxCtVbOVwhUEUvvMjjHRxfYFWviiKooIyx1VTOL6CFQX5cBPGe7BxjChLyff3/ajsDJgC
            YyrIh8QveT3DKufq049AtQfKUYmg+rDGHzTWsXL2JXSXTnLp4panBdURAqZscQOrQFkJyWxiZqTp6i2vOIz3R9P5Cem7RCGE19zrKRhLVRa+SGQ91NEz9DfuwCUb5NUy+STGjUvIS4QriV2OMBrLGGeGYMZgh74AqmteU1vjKsWUZGEVK5fQk4Rx2UFmxzj9ijdSUPH0x94D+0+D2KfTF+hCo53CahCkxMky4yKms3KKtHeCQmdMr2xD2qO/vkGu
            HcW1q5BZVte6VPYagyuPgN5Hph70t8iw2Gq5toiDm1Tv3iKiDq5wOO1YPnU3neVlrjzxUXA3EJlAFwaiFej1kOlRVHIcywqIJayN/LUlLCpRoEvK6Q2P/VEEalHLJVtGXmEgaukdlKPcm1uozKuukh5KxcgoQQiFtBHWgnYCh09mr6MlXvyfF5kD/CnY+r4gDFC6GL2rqQYaW1akUZ8ydHWY4LxrJFJLr3IL96cyQSYnQNcW73VqmmwvoWamqE19
            DPm/TYqbCyFNrm2DZVt5Iv7zbUh5I8vgsWe40Lfc/zn3orMuk7LAoRBCIK03VxAWVBx50UjocIRxGIcPK5cgkw7LxzOmaczo4kUoSv+4AocOaT0Xz/hClm/tER0/ioikF/iXJW5S+cVRab1fX6WJa0hBG08RKjS2LECXdFc65HpEvrfluYsbR1CdlNHuvl/U1AVoMoB+l+MnznL56iXYOg+9mkZk/OhrJbhFjr7yM1FqkcufOB/CemL/uKfTGZew
            dld2Fmd1S72h50nL4oCCY24U9vIgb4Zg/DVSlX5c6xyDlTMsrd+LiJeoqojxXuCIlSUUPhEvwiJMjmOM1QOM2wO35cddu+1NRJ32nrNWEokOdhyhbRdYJD77Go4eP8l074rnzulnQQyQsWM6HEOaNBEGLsoo1TLp4lEWl0+zu1VQToYQ91lc80snvb8LqWXpSI9J8TT59h8C2yS9HuXUEUcdn70RqbAErXGbaEb8dbLJGXETC6SsHr+TbqfLpUff
            B6oEmeFyBZ0Vks46aecYhi5W9HAuxtgKh0GKFIdFRVCO96HYQtp9hJLzBiACTC0IEDWnsQP9JaCLUF2Uiv0W2mmMNj790VXBLb22xjOHM/ifQ2FxkxzuOQuXfYH//6mWwgnb5OiiK5gOMewzvW6JxAKp7IYRN7TyJmR21LZzLdupBsNjFpM5F3vZhg7MzPZJwMwUwXqDBQFzPn7SzVvp1z5yzhkP5JcGljfgw89yY/0Iyy9fJjfSu5s7i0xlwBrr
            rVzkuVLG4IzAaG8PhvYLkFFV0F9dY6G7zPULF7CD/SZpTVjnC53WMM0phjsk6ig2qdPdpqHwGX+DlzkYTVQ/Bx2KpC68pMWOkAkU5dQD7QrEQpf9fIKejDxGGJVeUjgaI+6/n36WcuHc054aUlWz3GQjwMWs3f1qcAtcefaq945LFMYUYTwl+ApqAj4xT2QW3s6qcSZ2+EhKYVqEZo0TweaKYG5SCigCnLJ2F/3VE3SXz+CiIxRVl8G+wU3K4Kdn
            w/ZZe7WLGOPYx+mBDwvSW2Bv+Dezi4wSf8W6DFyGEuvktovqnWbxxN0sHj/JlYtPUZ37Q+//182R0vpuW8R+0yy6EC2DWCBdPkmnc4Tr22PYz+msHKPfW2B3NEDvb8FSTG9JsD98GoafAH0DmTjKckqSLGNs5DulhjLVzuqVLYu4zHMRnePUSx/Eabh07slwVcegMnpHVrGiR5ysYFyfQkeeSiVEY1xqjYcoIldQ5lfADRBqGuz0IyxJcKjGy65U
            AmkP2emRJj2MiXCVwmiFqQSmzloOBg1GHNgCOBECrSTO2tb2/xAE8Dlzeu3tE/4+qQL5IgugjBKv8bUVUoJOBexdZ3rtAp3TJzHpEqZSVJWZ2dlb1dxk9URaS7lkvS0K3ZwMHxe1KqbCp7NZ4WMs28WvDjkyNrhHE6RcNN2f0TWBWYebXTfdh4wktqj845ML7L7nI/Tv+RyOrCVs3ygwosIlFkmEtMpnG0chUa4S2MBlstrbD0krUarDpDBILVk8
            fgfDbAtz7ZLXFVclUea8uVQ1YLJ/mYXkPrq9mMnUwh4wCfmw1ME3gjiG0RivcqgqJFOs24c+dDopw909/+J1UrLVFUoFmIEfA8upLxZJh7tf9gr2L12Creve7YWS2EyptACxzvrZV6LMEtsXdqBwZFlKPhnPLkKt/Ua1zukIoVHCGe/pZ8uwIHHNTS2U8ols0i9J0qhCu5LCVP7wKnxOCGdfweL6PXRVH53DZOLIpxprpoESFZxDlPDdajWGakBl
            dvxIp/fBboG9DgwQYoQSBgqDIQmazz5CvYyltTvonl0j7pRc/fivUW09BW6IiAyOEmstUkSQ9DwnUKzCwh2sHD2LFIbtZy+A6xIfP06KYmf7Gqaa0jmSsdCZsLn9cRg+49UdncgPM0mXqqGHiCbCgIAv+21vgIV6y7BbQtbn5Ktfzmi4y97FJyBRsHAGZIwSEaVIECrFmNhjb0KGAhVSB0UKOqbTTxGTx/022+4gUotAUpUKqgzUCsR9VLJEb2nV
            /y4pKPIxusxn6YyETbgMxbvJFpaBRB4SwGddTGN7Vis+RMDunKuJ3cxNBe6goV/TvZhDc4KFvRXvrxVU9py5wi+gAPonOnPikBLseMjg6iU2jhXhSdQdmEQ4Xxhc8OM72BG7EI3ZRGRq6cUcwRHaGosM8jnjrVM8sTZkg1jrg9lrX74mZTLkfPgO1M48pOysrXTBdDW2DkcHPZ5w8Xc+yh1f/EoWl1O297zkxDsYRzOxtm3lHGt/4flktjBNCoGR
            YI1h9cQpNsscblzyF7/RRApK6XCjfawrMDL1Guasxbyv5YO0w+OsB/xD94fqkXUSNi+PfDe30CXqpIyL3FNUREUcSarhEO48TX91jWtPfdALoZU3QZVoqCxLd9/HwuodXH76KnpaIZWiqiaoOoVNWKAMKW0zArPEhrFn1gE6UTuiyFAkK5wrsa7ClhY7raC7DKsn2Dh9L3H/FKU4wXQUszeymHFJVUzDEtkgQxdjhfFEaDPwRc9t+3GXHWAX7D6w
            gwrcQVyEpgvROrJ3BqHuYH3t9cSdRQbFBa6cexj2HwG3RRTLELGr0LUDjOuCOgadYxy741Vcu7oFO9cg7pGun0AZyd71a1BNiRZisk7OjRsfheoqpIUPvwqvg2tvdQU+qKvl9CKd8qa4cQZDA8unOHnPA+ztbDO+cBH6vSCP64HIPGFbtjJhgl7Hc4IDtigUotMljXMm29dgeg1Shy78Aoh4g87GXaSdVTppH2sFk8mYshxT6rE/hBtdcHDlrZ1j
            Iq+ZV2nHX5/WgCtxpsRajbXm1ssGYbmVXdZNn1f7a94qJP05lx2fYgzQBlejmhoZxzFF4WBzywvTD9n8POe22/muyhgXYjC9C2897ja5wW0Hh+Dzh5tlfrS/nw85qqkWLe6MEBxMiBd1epmM4YnzDF9xL92Xd9nOI+zUoqzABm8ENFAFMbL1bi/Czlb+ta9rXSv3BvssH1mntFMmN3K0NqSx8BfQZEJVachS4iSh6ngFy02/R9EOxqow5TRYVQnS
            NIZiEt6XdDpdhoN9360JgS691jo9dRKDZnT9PJRj6FRgK4rRhO7GfRy7+0GeeOwajPaRUYRzGlvmqKgVNF5ndDwf8mltXyUdjjHoof+9xmtkd76a7MzrSRbvIJ9I9kaGcj/3Jqy4oDCpnby9xtfDIxXOjn2hM1vADeA6sAliE8QYEUw6nYswLCM7dyM795IsPUjaOY6OEjZ3nqTafA+Yc+B2kGmFTLx4XBlPNDYyAdFj6cxrkGqNa+evw2RMtHIS
            FccU4xJ2t0EIeqsLiDhn99oTkD8FUeXzfq0KljuSYBkTcqUlUrbGXqTvjITEDkuSo3dx6qVv4sLFq+iLlxCdHk6EICyZIGSMEApnRfP9cN4cwcks4LY+l1qpyON/020PGeQxJOuoI69maeNBhOiTT8aM9y5STTZ94hxF6PKrlkNLsKKTiyTpGjLugYooiinG5FANEBQI5wOV5mzKWouWJgTtxXgqCHsbVs2LK3hSiJtcntp//n9Ya9jVinPeBQAA
            AABJRU5ErkJggg==
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    });

    internal static Task<Bitmap> DecodeAsync(string key)
    {
        using var stream = new MemoryStream(PortablePng(key), writable: false);
        using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        if (source.Width is < 1 or > 2048 || source.Height is < 1 or > 2048)
            throw new InvalidDataException("Dimensão de arte inválida.");
        // Clone desvinculado do stream; o consumidor é responsável por Dispose.
        return Task.FromResult(new Bitmap(source));
    }
}
