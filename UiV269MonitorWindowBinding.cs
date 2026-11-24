namespace GerenciadorAplicativos;

// Une um formulário já criado pelo host ao relay de snapshots existente.
// Nenhuma abertura de janela, coleta, elevação ou persistência implícita.
// O host mantém a propriedade da janela e do relay.
internal sealed class GfMonitorWindowBinding : IDisposable
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly GfResourceMonitorForm _form;
    private IDisposable? _subscription;
    private bool _disposed;

    internal GfMonitorWindowBinding(GfResourceMonitorForm form, GfMonitorSnapshotRelay relay, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(relay);
        if (form.IsDisposed || form.Disposing || !form.IsHandleCreated || form.InvokeRequired)
            throw new InvalidOperationException("Monitor GF requer uma janela viva na thread do host.");
        _form = form;
        _form.FormClosed += OnClosed;
        _form.Disposed += OnDisposed;
        try
        {
            _subscription = relay.Attach(form.Present, now, form.ResetForSnapshotGeneration);
            // O replay é síncrono; uma janela descartada durante ele não pode
            // deixar uma assinatura presa no relay.
            if (_disposed) { _subscription.Dispose(); _subscription = null; }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void OnClosed(object? sender, System.Windows.Forms.FormClosedEventArgs e) => Dispose();
    private void OnDisposed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("Binding Monitor GF pertence à thread do host.");
        if (_disposed) return;
        _disposed = true;
        _form.FormClosed -= OnClosed;
        _form.Disposed -= OnDisposed;
        _subscription?.Dispose();
        _subscription = null;
    }
}
