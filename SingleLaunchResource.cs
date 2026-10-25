namespace GerenciadorAplicativos;

// Transfere a posse da proteção ao único lançamento. Dispose concorrente do
// preparador não pode soltar o arquivo enquanto Process.Start ainda o usa.
// Não valida comandos nem confere autorização de executar.
internal sealed class SingleLaunchResource(IDisposable? resource) : IDisposable
{
    private readonly object gate = new();
    private IDisposable? held = resource;
    private bool consumed;

    internal IDisposable? Claim()
    {
        lock (gate)
        {
            if (consumed) throw new InvalidOperationException("Tentativa já consumida.");
            consumed = true;
            var transferred = held;
            held = null;
            return transferred;
        }
    }

    public void Dispose()
    {
        IDisposable? released;
        lock (gate)
        {
            consumed = true;
            released = held;
            held = null;
        }
        released?.Dispose();
    }
}
