using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Host de UI optativo. O proprietário do coletor chama Publish na thread
// WinForms, após liberar sua trava de coleta. Nenhuma chamada a Capture,
// timer de hardware, tarefa, serviço, WMI ou integração implícita ao shell.
internal sealed class GfMonitorHost : IDisposable
{
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly GfMonitorSnapshotRelay _relay = new();
    private readonly string _preferencesPath;
    private GfResourceMonitorPreferences _preferences;
    private GfResourceMonitorForm? _window;
    private GfMonitorWindowBinding? _binding;
    private long _generation;
    private WddmGpuKey? _selected;
    private DateTimeOffset? _cpuAt, _wddmAt, _dxgiAt, _thermalAt;
    private GfResourceMonitorSnapshot? _lastProjected;
     private bool _animate = true, _disposed, _closingForHost, _applying;

    internal GfMonitorHost(string preferencesPath)
    {
        _preferencesPath = Path.GetFullPath(preferencesPath);
        _preferences = GfResourceMonitorPreferencesStore.Load(_preferencesPath);
    }

    private void Check()
    {
        if (Environment.CurrentManagedThreadId != _thread)
            throw new InvalidOperationException("Host Monitor GF exige thread de UI.");
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    internal GfResourceMonitorPreferences Preferences()
    {
        Check();
        return _window is { IsDisposed: false } window ? window.CapturePreferences(_preferences.Enabled) : _preferences;
    }

    // O shell continua responsável por seus gates de segurança/encerramento.
    // O monitor apenas acrescenta demanda; não abre consulta por conta própria.
    internal bool WantsCollection(bool homeDemand, bool collectionAllowed)
    {
        Check();
        return collectionAllowed && (homeDemand || (_preferences.Enabled && _window is { Visible: true, IsDisposed: false }));
    }

    internal long BeginGeneration(WddmGpuKey? selected)
    {
        Check();
        _selected = selected;
        _cpuAt = _wddmAt = _dxgiAt = _thermalAt = null;
        _lastProjected = null;
        _relay.BeginGeneration(++_generation);
        return _generation;
    }

    // O produtor entrega todos os dados da geração selecionada. Rejeitar
    // regressões de fonte, mas permitir reutilização do cache ainda fresco.
    internal bool Publish(long generation, HardwareSensorPortfolio portfolio, DateTimeOffset now,
        GfThermalEvidence? evidence = null)
    {
        Check(); ArgumentNullException.ThrowIfNull(portfolio);
        if (generation <= 0 || generation != _generation) return false;
        var c = portfolio.WindowsCounters.CapturedAtUtc;
        var w = portfolio.WindowsGpuCounters?.CapturedAtUtc;
        var d = portfolio.DxgiAdapters?.CapturedAtUtc;
        var t = portfolio.D3dKmtGpuSensors?.CapturedAtUtc;
        static bool Older(DateTimeOffset? next, DateTimeOffset? previous) =>
            next is DateTimeOffset n && previous is DateTimeOffset p && n < p;
        // Não registrar no watermark de uma fonte uma data futura inválida:
        // isso bloquearia amostras legítimas posteriores. Uma fonte ausente
        // ou antiga continua podendo ser invalidada no adaptador.
        bool Future(DateTimeOffset? at) => at is DateTimeOffset value && value > now.AddSeconds(5);
        if (Future(c) || Future(w) || Future(d) || Future(t) ||
            Older(c, _cpuAt) || Older(w, _wddmAt) || Older(d, _dxgiAt) || Older(t, _thermalAt)) return false;
        var s = GfResourceMonitorAdapter.FromPortfolio(portfolio, _selected, now);
        if (evidence is not null && _selected is WddmGpuKey key && s.GpuTemperature is not null &&
            s.MetricTimes?.GpuTemperature is DateTimeOffset measured && evidence.Matches(key, generation, measured, now))
            s = s with { GpuThermalEvidence = evidence };
        // A hora de entrega, sozinha, não constitui nova medição. Comparar
        // valores, instantes de origem, identidade e evidência; invalidar uma
        // fonte que venceu ainda produz uma entrega distinta.
        if (_lastProjected is { } last && (s with { CapturedAtUtc = last.CapturedAtUtc }) == last)
            return false;
        if (!_relay.Publish(generation, s, now)) return false;
        _lastProjected = s;
        _cpuAt = c; _wddmAt = w ?? _wddmAt; _dxgiAt = d ?? _dxgiAt; _thermalAt = t ?? _thermalAt;
        return true;
    }

    internal void Configure(V269Theme theme, bool animate)
    {
        // Compatibilidade com o shell: o parâmetro de tema global não
        // substitui a escolha exclusiva do monitor.
        Check(); _animate = animate;
        _window?.Configure(_preferences.MonitorTheme, animate);
    }

    // Restaurar só sob chamada explícita do shell (não no construtor).
    internal void Restore(DateTimeOffset now)
    {
        Check();
        if (_preferences.Enabled) Open(now);
    }

    internal bool Apply(GfResourceMonitorPreferences preferences, DateTimeOffset now)
    {
        Check();
        _preferences = preferences.Normalize();
        if (_preferences.Enabled)
        {
            if (_window is null) Open(now);
            else
            {
                _applying = true;
                try { _window.ApplyPreferences(_preferences); }
                finally { _applying = false; }
            }
        }
        else CloseWindow();
        return Save();
    }

    internal bool Save()
    {
        Check();
        _preferences = Preferences();
        return GfResourceMonitorPreferencesStore.TrySave(_preferencesPath, _preferences);
    }

    private void Open(DateTimeOffset now)
    {
        if (_window is { IsDisposed: false }) return;
        var window = new GfResourceMonitorForm();
        _window = window;
        try
        {
            window.Configure(_preferences.MonitorTheme, _animate);
            window.ApplyPreferences(_preferences);
            window.FormClosed += OnClosed;
            window.ModeChanged += OnModeChanged;
            window.Show();
            _binding = new GfMonitorWindowBinding(window, _relay, now);
        }
        catch
        {
            window.FormClosed -= OnClosed; window.ModeChanged -= OnModeChanged;
            _binding?.Dispose(); _binding = null; _window = null;
            window.Dispose();
            throw;
        }
    }

    private void OnModeChanged(object? sender, EventArgs e)
    {
        if (!_applying && !_closingForHost && !_disposed) Save();
    }

    private void OnClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is not GfResourceMonitorForm window || !ReferenceEquals(_window, window)) return;
        var captured = window.CapturePreferences(_closingForHost && _preferences.Enabled);
        _preferences = _closingForHost ? _preferences with { X = captured.X, Y = captured.Y } : captured;
        window.FormClosed -= OnClosed; window.ModeChanged -= OnModeChanged;
        _binding?.Dispose(); _binding = null; _window = null;
        if (!_closingForHost && !_disposed) Save();
    }

    private void CloseWindow()
    {
        if (_window is not { } window) return;
        _closingForHost = true;
        try { window.Close(); }
        finally
        {
            _closingForHost = false;
            if (ReferenceEquals(_window, window))
            {
                window.FormClosed -= OnClosed; window.ModeChanged -= OnModeChanged;
                _binding?.Dispose(); _binding = null; _window = null;
                window.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _thread)
            throw new InvalidOperationException("Host Monitor GF exige thread de UI.");
        if (_disposed) return;
        Save(); // conserva a preferência habilitada para a próxima execução
        CloseWindow();
        _relay.Dispose();
        _disposed = true;
    }
}
