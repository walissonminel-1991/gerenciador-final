namespace GerenciadorAplicativos;

/// <summary>
/// Vincula SOMENTE aparência entre controles WinForms independentes da V2.6.9.
/// Não registra preferências, não altera a MainForm e não aciona motores reais.
/// A integração com a aplicação completa permanece exclusiva do WORK.
/// </summary>
internal sealed class V269AppearanceBinder : IDisposable
{
    private readonly V269Shell _shell;
    private readonly V269AppearancePanel _panel;
    private bool _disposed;

    internal V269AppearanceBinder(V269Shell shell, V269AppearancePanel panel)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(panel);
        if (shell.IsDisposed) throw new ObjectDisposedException(nameof(shell));
        if (panel.IsDisposed) throw new ObjectDisposedException(nameof(panel));
        _shell = shell;
        _panel = panel;
        _panel.AppearanceChanged += OnAppearanceChanged;
        Sync();
    }

    internal void SetOptions(V269AppearanceOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // SetOptions não emite eventos; a atualização deve ocorrer uma vez.
        _panel.SetOptions(options);
        Sync();
    }

    private void OnAppearanceChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        if (_disposed || _shell.IsDisposed || _panel.IsDisposed) return;
        V269AppearanceOptions options = _panel.GetOptions().Normalize();
        _shell.ApplyAppearance(options.Theme, options.InterfacePercent, options.TextPercent);
        // AnimationsEnabled é somente preferência: o WORK poderá aplicar a
        // redução de movimento às views reais sem animações fictícias aqui.
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _panel.AppearanceChanged -= OnAppearanceChanged;
    }
}
