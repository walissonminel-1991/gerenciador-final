using System.Security.Principal;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Local\ limita os objetos à sessão do Windows. O SID separa usuários
// inclusive quando o executável é aberto na sessão com outra credencial.
internal sealed class GfMonitorSessionLease : IDisposable
{
    private static readonly Lazy<string> Prefix = new(() =>
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User ?? throw new InvalidOperationException("Usuário do Monitor GF indisponível.");
        return @"Local\GerenciadorFinal.V269.Monitor." + sid.Value + ".";
    });
    internal const string Shell = "Shell", Monitor = "Window", Autonomous = "Autonomous";
    private readonly Mutex _mutex;

    private GfMonitorSessionLease(Mutex mutex) => _mutex = mutex;

    private static bool Wait(Mutex mutex, int milliseconds)
    {
        try { return mutex.WaitOne(milliseconds); }
        catch (AbandonedMutexException) { return true; }
    }

    internal static GfMonitorSessionLease? TryTake(string role)
    {
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(false, Prefix.Value + role);
            if (!Wait(mutex, 0)) return null;
            var lease = new GfMonitorSessionLease(mutex);
            mutex = null;
            return lease;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or InvalidOperationException or
            System.ComponentModel.Win32Exception) { return null; }
        finally { mutex?.Dispose(); }
    }

    internal static bool IsPresent(string role)
    {
        // Só a janela principal cria este objeto. A consulta autônoma não
        // toma posse dele, portanto não disputa a abertura do GF.
        try
        {
            if (!Mutex.TryOpenExisting(Prefix.Value + role, out var marker)) return false;
            marker.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or InvalidOperationException or
            System.ComponentModel.Win32Exception) { return true; }
    }

    internal static EventWaitHandle RequestLaunch()
    {
        var launch = new EventWaitHandle(false, EventResetMode.AutoReset, Prefix.Value + "Launch");
        try { launch.Set(); return launch; }
        catch { launch.Dispose(); throw; }
    }

    internal static bool ConsumeLaunch()
    {
        if (!EventWaitHandle.TryOpenExisting(Prefix.Value + "Launch", out var launch)) return false;
        using (launch) return launch.WaitOne(0);
    }

    // Protege inclusive uma captura nativa que estiver terminando depois
    // do cancelamento da UI. Cada thread libera o mutex que ela adquiriu.
    internal static T Capture<T>(Func<T> read, CancellationToken token)
    {
        using var mutex = new Mutex(false, Prefix.Value + "Capture");
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (Wait(mutex, 25)) break;
        }
        try { token.ThrowIfCancellationRequested(); return read(); }
        finally { mutex.ReleaseMutex(); }
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}

internal static class GfMonitorSession
{
    internal static string PreferencesPath => Path.Combine(
        Path.GetDirectoryName(V269GeneralSettingsStore.PathForUser)!, "monitor.json");

    internal static GfMonitorHost CreateHost() => new(PreferencesPath, savePreferences: (path, value) =>
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException) { return false; }
        return GfResourceMonitorPreferencesStore.TrySave(path, value);
    });
}

internal sealed class GfMonitorStandalone : ApplicationContext
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private GfMonitorSessionLease? _lease;
    private GfMonitorHost? _host;
    private LiveHardwareMonitor? _collector;
    private CancellationTokenSource? _activeRead;
    private EventWaitHandle? _launch;
    private bool _initialLaunch = true;
    private HardwareSensorPortfolio? _snapshot;
    private long _nextCpu, _nextGpu, _generation;
    private WddmGpuKey? _selectedGpu;

    private GfMonitorStandalone()
    {
        _timer.Tick += OnTick;
        _timer.Start();
    }

    internal static int Run()
    {
        using var singleInstance = GfMonitorSessionLease.TryTake(GfMonitorSessionLease.Autonomous);
        if (singleInstance is null) return 0;
        ApplicationConfiguration.Initialize();
        using var context = new GfMonitorStandalone();
        Application.Run(context);
        return 0;
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (GfMonitorSessionLease.IsPresent(GfMonitorSessionLease.Shell))
        {
            if (_initialLaunch && _launch is null)
            {
                try { _launch = GfMonitorSessionLease.RequestLaunch(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
            }
            if (_activeRead is { } read) read.Cancel();
            else ReleaseMonitor();
            return;
        }
        if (_host is null)
        {
            _lease = GfMonitorSessionLease.TryTake(GfMonitorSessionLease.Monitor);
            if (_lease is null) return;
            try
            {
                _host = GfMonitorSession.CreateHost();
                bool launch = _initialLaunch && (_launch is null || _launch.WaitOne(0));
                _initialLaunch = false;
                _launch?.Dispose();
                _launch = null;
                var preferences = _host.Preferences();
                if (!launch && !preferences.Enabled) { ExitThread(); return; }
                _host.Configure(V269Theme.AzulGf, SystemInformation.UIEffectsEnabled && SystemInformation.IsMenuAnimationEnabled);
                if (launch && !preferences.Enabled) _host.Apply(preferences with { Enabled = true }, DateTimeOffset.UtcNow);
                else _host.Restore(DateTimeOffset.UtcNow);
                _collector = new LiveHardwareMonitor();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { ExitThread(); return; }
        }
        if (!_host.Preferences().Enabled) { ExitThread(); return; }
        long tick = Environment.TickCount64;
        if (_activeRead is not null || tick < _nextCpu || !_host.WantsCollection(false, true)) return;
        var collector = _collector!;
        using var readRequest = new CancellationTokenSource();
        _activeRead = readRequest;
        var token = readRequest.Token;
        _nextCpu = tick + 2000;
        var snapshot = _snapshot;
        try
        {
            var sample = await Task.Run(() => GfMonitorSessionLease.Capture(collector.Capture, token), token);
            token.ThrowIfCancellationRequested();
            snapshot = snapshot is null ? GfMonitorSampling.CountersOnly(sample) : snapshot with { WindowsCounters = sample };
            if (tick >= _nextGpu)
            {
                _nextGpu = tick + 8000;
                var baseline = snapshot;
                try { snapshot = await StaWorker.Run(() => GfMonitorSessionLease.Capture(
                    () => GfMonitorSampling.RefreshGpu(baseline, token), token)); }
                catch (Exception ex) when (!token.IsCancellationRequested && ex is not OutOfMemoryException)
                { snapshot = GfMonitorSampling.WithoutGpuReadings(baseline, DateTimeOffset.UtcNow); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var unavailable = GfMonitorSampling.UnavailableCounters(DateTimeOffset.UtcNow);
            snapshot = snapshot is null ? GfMonitorSampling.CountersOnly(unavailable) : snapshot with { WindowsCounters = unavailable };
        }
        finally
        {
            _activeRead = null;
            if (GfMonitorSessionLease.IsPresent(GfMonitorSessionLease.Shell)) ReleaseMonitor();
            else if (_host is { } host && snapshot is not null && !token.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;
                var gpu = GfMonitorSampling.SelectGpu(snapshot, _selectedGpu, now);
                if (_generation == 0 || gpu != _selectedGpu)
                {
                    _selectedGpu = gpu;
                    _generation = host.BeginGeneration(gpu);
                }
                _snapshot = snapshot;
                host.Publish(_generation, snapshot, now);
            }
        }
    }

    private void ReleaseMonitor()
    {
        _host?.Dispose();
        _host = null;
        _collector = null;
        _snapshot = null;
        _generation = _nextCpu = _nextGpu = 0;
        _lease?.Dispose();
        _lease = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _activeRead?.Cancel();
            _launch?.Dispose();
            ReleaseMonitor();
        }
        base.Dispose(disposing);
    }
}
