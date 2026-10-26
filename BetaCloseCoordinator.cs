namespace GerenciadorAplicativos;

// Estado pertencente a thread da janela. Shutdown sempre prevalece sobre
// uma espera HTTP; concluir a espera nunca reabre uma janela em encerramento.
internal sealed class BetaCloseCoordinator
{
    private bool _sendActive;
    private bool _suppressFollowup;
    internal bool IsClosing { get; private set; }
    internal bool IsWaiting { get; private set; }
    internal bool IsComplete { get; private set; }
    internal bool IsShutdown { get; private set; }

    internal bool TryBegin(bool cancelled, bool canSend)
    {
        if (cancelled || !canSend || IsClosing) return false;
        _sendActive = IsClosing = IsWaiting = true;
        return true;
    }

    internal void Shutdown()
    {
        IsShutdown = IsClosing = true;
        IsWaiting = false;
        _suppressFollowup = false;
    }

    internal bool Finish()
    {
        _sendActive = false;
        IsWaiting = false;
        if (_suppressFollowup)
        {
            _suppressFollowup = IsClosing = IsComplete = false;
            return false;
        }
        IsComplete = true;
        return !IsShutdown;
    }

    internal bool CancelShutdown()
    {
        if (!IsShutdown) return false;
        IsShutdown = IsComplete = false;
        // Esperar somente o cancelamento da tentativa antiga. Sua continuacao
        // nao pode fechar a janela depois de o Windows desistir de desligar.
        _suppressFollowup = _sendActive;
        IsClosing = IsWaiting = _sendActive;
        return true;
    }
}
