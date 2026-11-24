namespace GerenciadorAplicativos;

// Rascunho: o host proprietário publica o snapshot JÁ coletado na thread
// de UI. Não inicia hardware, timer, tarefa, serviço, fila ou thread.
// Existe no máximo um consumidor flutuante; a aba Início conserva seu motor.
internal sealed class GfMonitorSnapshotRelay : IDisposable
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private long _generation;
    private long _lease;
    private string? _selectedGpuIdentity;
    private DateTimeOffset? _watermark;
    private GfResourceMonitorSnapshot? _latest;
    private Action<GfResourceMonitorSnapshot>? _consumer;
    private Action? _invalidate;
    private bool _delivering;
    private bool _disposed;

    private void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("Relay Monitor GF pertence à thread do host.");
    }

    private void CheckLive()
    {
        CheckThread();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_delivering) throw new InvalidOperationException("Entrega reentrante do Monitor GF.");
    }

    internal void BeginGeneration(long generation)
    {
        CheckLive();
        if (generation <= _generation) throw new ArgumentOutOfRangeException(nameof(generation));
        _generation = generation; _watermark = null; _latest = null; _selectedGpuIdentity = null;
        // O host limpa filtros, watermark e leitura visível da geração antiga
        // imediatamente; não aguardar o timer de frescor de dez segundos.
        if (_invalidate is { } invalidate) Deliver(invalidate);
    }

    internal IDisposable Attach(Action<GfResourceMonitorSnapshot> consumer, DateTimeOffset now, Action invalidate)
    {
        CheckLive(); ArgumentNullException.ThrowIfNull(consumer); ArgumentNullException.ThrowIfNull(invalidate);
        if (_consumer is not null) throw new InvalidOperationException("Monitor GF já possui consumidor.");
        long id = ++_lease;
        _consumer = consumer; _invalidate = invalidate;
        try
        {
            if (_latest is { } snapshot && GfResourceMonitorAdapter.IsFresh(snapshot.CapturedAtUtc, now))
                Deliver(() => consumer(snapshot));
            else { _latest = null; Deliver(invalidate); }
        }
        catch { Detach(id); throw; }
        return new Subscription(this, id);
    }

    internal bool Publish(long generation, GfResourceMonitorSnapshot snapshot, DateTimeOffset now)
    {
        CheckLive(); ArgumentNullException.ThrowIfNull(snapshot);
        if (generation <= 0 || generation != _generation ||
            !GfResourceMonitorAdapter.IsFresh(snapshot.CapturedAtUtc, now) ||
            (_watermark is DateTimeOffset accepted && snapshot.CapturedAtUtc <= accepted)) return false;
        if (snapshot.Generation != 0 && snapshot.Generation != generation) return false;
        // A seleção de outra GPU exige nova geração; a ausência temporária
        // de identidade não autoriza reatribuir a amostra a outro adaptador.
        if (!string.IsNullOrWhiteSpace(snapshot.GpuIdentity))
        {
            if (_selectedGpuIdentity is not null && !string.Equals(_selectedGpuIdentity, snapshot.GpuIdentity, StringComparison.Ordinal)) return false;
            _selectedGpuIdentity = snapshot.GpuIdentity;
        }
        snapshot = snapshot with { Generation = generation };
        _watermark = snapshot.CapturedAtUtc;
        _latest = snapshot;
        // Não engolir exceções da UI: o host deve tratar falhas na fronteira
        // de apresentação. O watermark impede reentregar a mesma amostra.
        if (_consumer is { } consumer) Deliver(() => consumer(snapshot));
        return true;
    }

    private void Deliver(Action callback)
    {
        long lease = _lease;
        _delivering = true;
        try { callback(); }
        catch { Detach(lease); throw; }
        finally { _delivering = false; }
    }

    private void Detach(long lease)
    {
        CheckThread();
        if (_lease == lease) { _consumer = null; _invalidate = null; }
    }

    public void Dispose()
    {
        CheckThread();
        if (_disposed) return;
        if (_delivering) throw new InvalidOperationException("Descarte reentrante do relay Monitor GF.");
        _disposed = true;
        _consumer = null; _invalidate = null; _latest = null; _watermark = null;
    }

    private sealed class Subscription(GfMonitorSnapshotRelay relay, long lease) : IDisposable
    {
        private GfMonitorSnapshotRelay? _relay = relay;
        public void Dispose()
        {
            if (_relay is not { } owner) return;
            owner.Detach(lease);
            _relay = null;
        }
    }
}
