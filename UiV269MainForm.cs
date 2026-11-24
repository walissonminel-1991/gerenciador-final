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
    private readonly DarkScrollableFlow _v269HardwareCards = new() { Dock = DockStyle.Fill };
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
        var page = new TableLayoutPanel { Name = "V269HardwarePage", ColumnCount = 1, RowCount = 3 };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var refresh = CreateSecondaryButton("Consultar hardware");
        refresh.Name = "V269ReadHardware";
        refresh.Dock = DockStyle.Fill;
        refresh.Click += async (_, _) =>
        {
            if (_v269HardwareQuery is { IsCancellationRequested: false } active) active.Cancel();
            else await RefreshV269HardwareAsync();
        };
        _v269HardwareStatus.Text = "Aguardando consulta local.";
        _v269HardwareStatus.TextChanged += (_, _) =>
            refresh.Text = _v269HardwareQuery is { IsCancellationRequested: false }
                ? "Cancelar consulta" : "Consultar hardware";
        page.Controls.Add(refresh, 0, 0);
        page.Controls.Add(_v269HardwareStatus, 0, 1);
        page.Controls.Add(_v269HardwareCards, 0, 2);
        _v269HardwareCards.Content.SizeChanged += (_, _) => SizeV269Cards(_v269HardwareCards);
        return page;
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
            var flow = _v269HardwareCards.Content;
            flow.SuspendLayout();
            try
            {
                foreach (Control old in flow.Controls.Cast<Control>().ToArray()) old.Dispose();
                foreach (var (title, body) in V269HardwareCards(snapshot))
                    flow.Controls.Add(BuildV269HardwareCard(title, body));
            }
            finally { flow.ResumeLayout(true); }
            SizeV269Cards(_v269HardwareCards);
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

    private Control BuildV269HardwareCard(string title, string text)
    {
        var card = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, Padding = new Padding(8),
            Height = 124, Margin = new Padding(2, 2, 7, 5), BackColor = PanelBackground };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, AutoEllipsis = true,
            ForeColor = Ink, AccessibleDescription = title }, 0, 0);
        card.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, AutoEllipsis = true,
            ForeColor = Muted, AccessibleDescription = text }, 0, 1);
        return card;
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
