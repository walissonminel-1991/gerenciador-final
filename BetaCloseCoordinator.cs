namespace GerenciadorAplicativos;

// Estado pertencente a thread da janela. Shutdown sempre prevalece sobre
// uma espera HTTP; concluir a espera nunca reabre uma janela em encerramento.
internal sealed class BetaCloseCoordinator
{
    internal bool IsClosing { get; private set; }
    internal bool IsWaiting { get; private set; }
    internal bool IsComplete { get; private set; }
    internal bool IsShutdown { get; private set; }

    internal bool TryBegin(bool cancelled, bool canSend)
    {
        if (cancelled || !canSend || IsClosing) return false;
        IsClosing = IsWaiting = true;
        return true;
    }

    internal void Shutdown()
    {
        IsShutdown = IsClosing = true;
        IsWaiting = false;
    }

    internal bool Finish()
    {
        IsWaiting = false;
        IsComplete = true;
        return !IsShutdown;
    }
}
