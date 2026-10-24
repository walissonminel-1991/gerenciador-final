namespace GerenciadorAplicativos;

// Uma leitura cancelada nunca pode substituir a ultima fotografia publicada.
internal sealed class DriverQueryCoordinator
{
    private CancellationTokenSource? _current;
    internal bool InProgress => _current is not null;
    internal CancellationToken Begin(TimeSpan timeout)
    {
        if (_current is not null) throw new InvalidOperationException("Consulta já em andamento.");
        _current = new CancellationTokenSource(timeout);
        return _current.Token;
    }
    internal bool RequestCancel()
    {
        if (_current is null || _current.IsCancellationRequested) return false;
        _current.Cancel();
        return true;
    }
    internal bool CanPublish(CancellationToken token) =>
        _current is not null && _current.Token == token && !token.IsCancellationRequested;
    internal void End(CancellationToken token)
    {
        if (_current is null || _current.Token != token) return;
        _current.Dispose();
        _current = null;
    }
}
