using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private GfMonitorSessionLease? _v269ShellPresence, _v269MonitorLease;
    private GfMonitorHost? _v269MonitorHost;
    private System.Windows.Forms.Timer? _v269MonitorAttachTimer, _v269MonitorSaveTimer;
    private V269MonitorOptions? _v269MonitorOptions;
    private bool _v269MonitorReady, _v269MonitorSyncPosted, _v269MonitorStartupFailed, _v269MonitorAttachFailed;
    private long _v269MonitorRetryAt;
    private (int Scale, int Opacity)? _v269MonitorPending;
    private long _v269MonitorGeneration;
    private WddmGpuKey? _v269MonitorGpu;

    private bool V269OwnsMonitorSession() => !_v269SettingsLive || _v269MonitorLease is not null;

    private bool V269CollectionAllowed() => V269OwnsMonitorSession() &&
        !_busy && !_v269Confirming && !IsDisposed && !Disposing && !_betaClose.IsClosing && Enabled;

    private bool V269MonitorWantsCollection() => _v269MonitorHost?.WantsCollection(false, V269CollectionAllowed()) == true;

    private void PublishV269Monitor(HardwareSensorPortfolio snapshot)
    {
        if (_v269MonitorHost is not { } host || !host.Preferences().Enabled) return;
        var now = DateTimeOffset.UtcNow;
        var selected = GfMonitorSampling.SelectGpu(snapshot, _v269MonitorGpu, now);
        if (_v269MonitorGeneration == 0 || selected != _v269MonitorGpu)
        {
            _v269MonitorGpu = selected;
            _v269MonitorGeneration = host.BeginGeneration(selected);
        }
        host.Publish(_v269MonitorGeneration, snapshot, now);
    }

    // A montagem visual não cria host, timers, arquivos ou registros.
    // O teste usa exatamente a mesma montagem da janela de produção.
    internal static V269MonitorOptions MountV269MonitorOptions(V269SettingsPage view)
    {
        var options = new V269MonitorOptions();
        view.AddMonitor(options);
        return options;
    }

    private void BindV269Monitor(V269SettingsPage view)
    {
        var options = _v269MonitorOptions = MountV269MonitorOptions(view);
        _v269MonitorSaveTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _v269MonitorSaveTimer.Tick += (_, _) => FlushV269MonitorPreferences();
        Disposed += (_, _) => StopV269Monitor();
        options.PreferenceChanged += edit => ApplyV269MonitorPreferences(edit);
        options.DisplaySizeChanged += (scale, opacity) =>
        {
            if (_v269MonitorHost is null) return;
            _v269MonitorPending = (scale, opacity);
            _v269MonitorSaveTimer.Stop();
            _v269MonitorSaveTimer.Start();
        };
        options.StartupChanged += enabled =>
        {
            if (_v269MonitorHost is null || !V269CollectionAllowed()) return;
            _v269MonitorStartupFailed = false;
            try
            {
                StartupTrayRegistration.SetMonitorStartupRegistered(enabled);
                _v269MonitorStartupFailed = StartupTrayRegistration.IsMonitorStartupRegistered() != enabled;
            }
            catch (Exception ex) when (IsV269MonitorStartupError(ex)) { _v269MonitorStartupFailed = true; }
            SyncV269MonitorOptions();
        };
        view.General.SectionChanged += (_, _) => { if (view.General.MonitorSelected) SyncV269MonitorOptions(); };
        Shown += (_, _) =>
        {
            _v269MonitorReady = true;
            _v269MonitorAttachTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _v269MonitorAttachTimer.Tick += (_, _) => AttachV269Monitor();
            AttachV269Monitor();
            if (_v269MonitorHost is null)
            {
                _v269HardwareStatus.Text = "Aguardando disponibilidade dos sensores nesta sessão.";
                _v269MonitorAttachTimer.Start();
            }
            SyncV269MonitorOptions();
        };
        SyncV269MonitorOptions();
    }

    private void AcceptV269MonitorLaunch()
    {
        if (_v269MonitorHost is not { } host || !V269CollectionAllowed()) return;
        try
        {
            if (GfMonitorSessionLease.ConsumeLaunch() && !host.Preferences().Enabled)
                ApplyV269MonitorPreferences(p => p with { Enabled = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { DiagnosticTrace.Write("MONITOR_GF.INICIALIZACAO_INDISPONIVEL " + ex.GetType().Name); }
    }

    private void AttachV269Monitor()
    {
        if (!_v269MonitorReady || _v269MonitorHost is not null || IsDisposed || Disposing || _betaClose.IsClosing ||
            Environment.TickCount64 < _v269MonitorRetryAt) return;
        _v269ShellPresence ??= GfMonitorSessionLease.TryTake(GfMonitorSessionLease.Shell);
        if (_v269ShellPresence is null) return;
        _v269MonitorLease ??= GfMonitorSessionLease.TryTake(GfMonitorSessionLease.Monitor);
        if (_v269MonitorLease is null) return;
        try
        {
            var host = _v269MonitorHost = GfMonitorSession.CreateHost();
            host.PersistenceStateChanged += (_, _) => QueueV269MonitorSync();
            host.NativePresentationStateChanged += (_, _) => QueueV269MonitorSync();
            host.PlacementStateChanged += (_, _) => QueueV269MonitorSync();
            host.Configure(_v269Appearance.Theme, CanAnimateV269);
            host.Restore(DateTimeOffset.UtcNow);
            _v269MonitorAttachFailed = false;
            _v269MonitorAttachTimer?.Stop();
            if (!_v269HardwareLoaded)
                _v269HardwareStatus.Text = "Sensores disponíveis para consulta.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var failedHost = _v269MonitorHost;
            _v269MonitorHost = null;
            try { failedHost?.Dispose(); }
            catch (Exception cleanup) when (cleanup is not OutOfMemoryException)
            { DiagnosticTrace.Write("MONITOR_GF.ENCERRAMENTO_INDISPONIVEL " + cleanup.GetType().Name); }
            _v269MonitorAttachFailed = true;
            _v269MonitorRetryAt = Environment.TickCount64 + 2000;
            DiagnosticTrace.Write("MONITOR_GF.APRESENTACAO_INDISPONIVEL " + ex.GetType().Name);
        }
        SyncV269MonitorOptions();
    }

    private void FlushV269MonitorPreferences()
    {
        _v269MonitorSaveTimer?.Stop();
        if (_v269MonitorPending is not null) ApplyV269MonitorPreferences(p => p);
    }

    private GfResourceMonitorPreferences PendingV269MonitorSize(GfResourceMonitorPreferences preferences) =>
        _v269MonitorPending is { } pending ? preferences with { ScalePercent = pending.Scale, OpacityPercent = pending.Opacity } : preferences;

    private void ApplyV269MonitorPreferences(Func<GfResourceMonitorPreferences, GfResourceMonitorPreferences> edit)
    {
        _v269MonitorSaveTimer?.Stop();
        if (_v269MonitorHost is not { } host) return;
        var preferences = PendingV269MonitorSize(host.Preferences());
        _v269MonitorPending = null;
        try
        {
            host.Configure(_v269Appearance.Theme, CanAnimateV269);
            host.Apply(edit(preferences), DateTimeOffset.UtcNow);
            if ((_v269LastSnapshot ?? _v269MonitorSnapshot) is { } sample) PublishV269Monitor(sample);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            DiagnosticTrace.Write("MONITOR_GF.PREFERENCIA_FALHA " + ex.GetType().Name);
            _v269Settings?.SetMonitorNotice("Não foi possível aplicar a preferência do Monitor GF.");
            return;
        }
        SyncV269MonitorOptions();
    }

    private void QueueV269MonitorSync()
    {
        if (_v269MonitorSyncPosted || !IsHandleCreated || IsDisposed || Disposing) return;
        _v269MonitorSyncPosted = true;
        BeginInvoke((Action)(() =>
        {
            _v269MonitorSyncPosted = false;
            SyncV269MonitorOptions();
        }));
    }

    private static bool IsV269MonitorStartupError(Exception ex) => ex is IOException or UnauthorizedAccessException or
        System.Security.SecurityException or InvalidDataException or InvalidOperationException;

    private void SyncV269MonitorOptions()
    {
        if (_v269MonitorOptions is not { IsDisposed: false } options || _v269Settings is not { IsDisposed: false } view) return;
        if (_v269MonitorHost is not { } host)
        {
            options.Enabled = false;
            view.SetMonitorNotice(_v269MonitorAttachFailed ? "Apresentação do Monitor GF indisponível. Tentando novamente." :
                "Aguardando disponibilidade do Monitor GF nesta sessão.");
            return;
        }
        options.Enabled = true;
        // Observadores nativos e de persistência não substituem a edição em andamento.
        var preferences = PendingV269MonitorSize(host.Preferences());
        bool? startupRegistered;
        try { startupRegistered = StartupTrayRegistration.IsMonitorStartupRegistered(); }
        catch (Exception ex) when (IsV269MonitorStartupError(ex)) { startupRegistered = null; }
        options.LoadObserved(preferences, startupRegistered);
        string? notice = _v269MonitorStartupFailed ? "Não foi possível confirmar a entrada de inicialização do Monitor GF." :
            host.LastSaveSucceeded() == false ? "Preferência aplicada nesta sessão. Não foi possível salvar o Monitor GF." :
            host.NativePresentationAvailable() == false ? "Apresentação do Monitor GF temporariamente indisponível." :
            host.PlacementAvailable() == false ? "Área de trabalho insuficiente para exibir o Monitor GF." :
            host.EffectiveScalePercent() is int scale && scale != preferences.ScalePercent ? $"Escala efetiva na tela atual: {scale}%." :
            startupRegistered == true ? "Confira a permissão em Aplicativos de inicialização do Windows." : null;
        view.SetMonitorNotice(notice);
    }

    private void StopV269Monitor()
    {
        _v269MonitorReady = false;
        FlushV269MonitorPreferences();
        _v269MonitorSaveTimer?.Dispose();
        _v269MonitorSaveTimer = null;
        _v269MonitorAttachTimer?.Dispose();
        _v269MonitorAttachTimer = null;
        _v269MonitorHost?.Dispose();
        _v269MonitorHost = null;
        _v269MonitorLease?.Dispose();
        _v269MonitorLease = null;
        _v269ShellPresence?.Dispose();
        _v269ShellPresence = null;
    }
}
