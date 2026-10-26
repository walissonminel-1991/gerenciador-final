using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Janela discreta e fixa, SEM maximizar/tela cheia. Os dois botoes disponiveis
// sao Minimizar (para bandeja) e Fechar (encerramento real). Nenhum formulario
// auxiliar, persistencia em disco ou consulta extra e criada por este modulo.
internal sealed partial class MainForm
{
    // Nao usar WS_EX_COMPOSITED em toda a arvore de controles: o estilo
    // pode aumentar o tempo de composicao, particularmente no PC FRACO.
    // Os paineis que precisam de buffering usam pintura local.
    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _trayMenu;
    private Icon? _windowIcon;
    private Panel? _caption;
    private bool _hiddenInTray;
    private bool _trayTransitioning;
    private bool _diagnoseWindowLifecycle;
    private IntPtr _trayHandleBeforeHide;
    internal bool IsTrayTransitioning => _trayTransitioning;
    private bool _exitWhenIdle;

    internal bool IsInTray => _hiddenInTray;
    internal bool HasActiveTrayIcon => _trayIcon?.Visible == true;
    internal bool HasCustomCaption => _caption is { IsDisposed: false };

    private void InitializeWindowChrome()
    {
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = true;
        ShowInTaskbar = true;

        try { _windowIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch (Exception ex) when (ex is ArgumentException or IOException or System.ComponentModel.Win32Exception)
        { _windowIcon = null; }
        Icon = _windowIcon ?? SystemIcons.Application;

        _trayMenu = new ContextMenuStrip
        {
            BackColor = Color.FromArgb(30, 39, 50),
            ForeColor = Color.FromArgb(237, 243, 250),
            ShowImageMargin = false,
            ShowCheckMargin = false
        };
        _trayMenu.Items.Add(new ToolStripMenuItem("Abrir", null, (_, _) => RestoreFromTray())
        { Name = "TrayOpen" });
        _trayMenu.Items.Add(new ToolStripMenuItem(
            "Privacidade e diagnóstico", null, (_, _) =>
            {
                RestoreFromTray();
                OfferAnonymousDiagnosticConsent();
            }) { Name = "TrayPrivacy" });
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(new ToolStripMenuItem("Sair", null, (_, _) => RequestTrayExit())
        { Name = "TrayExit" });

        _trayIcon = new NotifyIcon
        {
            Icon = Icon,
            Text = "Gerenciador Final — em execução",
            Visible = false,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        _caption = new Panel
        {
            Height = 31, Dock = DockStyle.Top, Margin = Padding.Empty,
            BackColor = Color.FromArgb(30, 30, 30)
        };
        var title = new Label
        {
            Text = Text, AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(232, 232, 232),
            Font = new Font("Segoe UI", 9F),
            BackColor = Color.Transparent,
            AutoSize = false
        };
        var minimize = CaptionButton("—", "Minimizar para a bandeja do sistema", false,
            (_, _) => MinimizeToTray());
        var close = CaptionButton("×", "Fechar o Gerenciador Final", true,
            (_, _) => Close());
        _caption.Controls.Add(title);
        _caption.Controls.Add(minimize);
        _caption.Controls.Add(close);
        void ArrangeCaption()
        {
            if (_caption.IsDisposed) return;
            int h = _caption.ClientSize.Height;
            int buttonWidth = Math.Max(46, (int)Math.Round(46d * DeviceDpi / 96d));
            close.Bounds = new Rectangle(Math.Max(0, _caption.Width - buttonWidth), 0, buttonWidth, h);
            minimize.Bounds = new Rectangle(Math.Max(0, close.Left - buttonWidth), 0, buttonWidth, h);
            title.Bounds = new Rectangle(34, 0, Math.Max(1, minimize.Left - 42), h);
        }
        _caption.SizeChanged += (_, _) => ArrangeCaption();
        _caption.Paint += (_, e) =>
        {
            if (_windowIcon is not null)
                e.Graphics.DrawIcon(_windowIcon, new Rectangle(10, 7, 17, 17));
            else
                e.Graphics.DrawIcon(SystemIcons.Application, new Rectangle(10, 7, 17, 17));
            using var pen = new Pen(Color.FromArgb(58, 66, 77));
            e.Graphics.DrawLine(pen, 0, _caption.Height - 1,
                Math.Max(0, _caption.Width - 1), _caption.Height - 1);
        };
        _caption.MouseDown += MoveFromCaption;
        title.MouseDown += MoveFromCaption;
        Controls.Add(_caption);
        _caption.BringToFront();
        ArrangeCaption();

        // Programas continuam coletando dados enquanto a janela esta oculta.
        // Sair realmente fecha o processo e libera o icone da bandeja.
        FormClosed += (_, _) =>
        {
            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            _trayMenu?.Dispose();
            _trayMenu = null;
            _windowIcon?.Dispose();
            _windowIcon = null;
        };
        VisibleChanged += (_, _) =>
        {
            if (!_diagnoseWindowLifecycle || _trayTransitioning || IsDisposed) return;
            DiagnosticTrace.Write("JANELA.VISIBILIDADE visivel=" + Visible +
                " estado=" + WindowState + " bandeja=" + _hiddenInTray);
        };
        // Nunca alternar ShowInTaskbar apos o Form criar Handle. O WinForms
        // pode destruir e recriar o HWND ao trocar essa propriedade e repintar
        // centenas de controles em cascata, parecendo fechar e abrir sozinho.
        Resize += (_, _) =>
        {
            if (!IsDisposed && IsHandleCreated && !_trayTransitioning &&
                WindowState == FormWindowState.Minimized)
                BeginInvoke((Action)MinimizeToTray);
        };
    }

    private Button CaptionButton(string symbol, string hint, bool isClose,
        EventHandler action)
    {
        var button = new Button
        {
            Text = symbol,
            AccessibleName = hint,
            TabStop = true,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(230, 235, 241),
            BackColor = Color.FromArgb(30, 30, 30),
            Font = new Font("Segoe UI", isClose ? 13F : 11F),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = Padding.Empty,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = isClose
            ? Color.FromArgb(186, 38, 47) : Color.FromArgb(57, 66, 77);
        button.FlatAppearance.MouseDownBackColor = isClose
            ? Color.FromArgb(142, 25, 33) : Color.FromArgb(73, 87, 104);
        button.Click += action;
        _toolTips.SetToolTip(button, hint);
        return button;
    }

    private void MoveFromCaption(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || IsDisposed) return;
        WindowCaptionMover.ReleaseCapture();
        WindowCaptionMover.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
    }

    internal void MinimizeToTray()
    {
        if (IsDisposed || _trayTransitioning || _hiddenInTray || _trayIcon is null) return;
        _trayTransitioning = true;
        DiagnosticTrace.Write("JANELA.BANDEJA_OCULTAR.INICIO");
        try
        {
            _trayHandleBeforeHide = IsHandleCreated ? Handle : IntPtr.Zero;
            _hiddenInTray = true;
            // Hide retira a janela da barra de tarefas naturalmente.
            // NUNCA alterar ShowInTaskbar aqui: evita RecreateHandle().
            Hide();
            DiagnosticTrace.Write("JANELA.BANDEJA_OCULTAR.APÓS_HIDE");
            _trayIcon.Visible = true;
            UpdateProcessSamplingCadence();
            DiagnosticTrace.Write("JANELA.BANDEJA_OCULTAR.FIM");
        }
        finally { _trayTransitioning = false; }
    }

    internal void RestoreFromTray()
    {
        if (IsDisposed || _trayTransitioning || (!_hiddenInTray && Visible)) return;
        _trayTransitioning = true;
        DiagnosticTrace.Write("JANELA.BANDEJA_RESTAURAR.INICIO");
        try
        {
            _hiddenInTray = false;
            // Preparar o layout com a janela ainda oculta. Nao invocar
            // Show() enquanto SuspendLayout esta ativo: ao retomar, a arvore
            // poderia redesenhar cada area em momentos diferentes.
            SuspendLayout();
            try
            {
                if (WindowState != FormWindowState.Normal)
                    WindowState = FormWindowState.Normal;
            }
            finally { ResumeLayout(true); }
            DiagnosticTrace.Write("JANELA.BANDEJA_RESTAURAR.ANTES_SHOW");
            Show();
            DiagnosticTrace.Write("JANELA.BANDEJA_RESTAURAR.APOS_SHOW");
            UpdateProcessSamplingCadence();
            if (_trayIcon is not null) _trayIcon.Visible = false;
            if (_trayHandleBeforeHide != IntPtr.Zero && Handle != _trayHandleBeforeHide)
                DiagnosticTrace.Write("JANELA.HANDLE_ALTERADO apos restaurar bandeja");
        }
        finally { _trayTransitioning = false; }
        Activate();
        DiagnosticTrace.Write("JANELA.BANDEJA_RESTAURAR.FIM");
        ResumeUninstallCleanupConfirmation();
        // Recolher RAM global, sem enumeracao de PIDs na restauracao.
        if (_nav.SelectedIndex == 5 && _activeSystemView == "memory")
        {
            DiagnosticTrace.Write("JANELA.BANDEJA_RESTAURAR.RAM_ATUALIZAR");
            _ = RefreshMemoryRamAsync();
        }
    }

    private void RequestTrayExit()
    {
        if (IsDisposed || _betaClose.IsClosing) return;
        if (_busy)
        {
            // Sair significa ENCERRAR, mas so depois que a operacao atual
            // terminar ou cancelar de forma segura. Nenhum processo e morto.
            _exitWhenIdle = true;
            _consultaCatalogoCts?.Cancel();
            _installerDownloadCts?.Cancel();
            if (_trayIcon is not null)
                _trayIcon.Text = "Gerenciador Final — encerrando após operação";
            return;
        }
        Close();
    }

    private void ExitAfterCurrentOperation()
    {
        if (!_exitWhenIdle || IsDisposed || _busy) return;
        _exitWhenIdle = false;
        if (IsHandleCreated) BeginInvoke((Action)Close);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_diagnoseWindowLifecycle)
            DiagnosticTrace.Write("JANELA.HANDLE_DESTRUIDO bandeja=" +
                _hiddenInTray + " fechando=" + Disposing);
        base.OnHandleDestroyed(e);
    }

    private void ResumeAfterCancelledShutdown()
    {
        if (IsDisposed || Disposing) return;
        _memoryTimer.Start();
        if (!_busy && !_betaClose.IsClosing) _programList.ActionsEnabled = true;
    }

    // Bloqueia mesmo uma tentativa por atalhos do Windows/WM_SYSCOMMAND.
    protected override void WndProc(ref Message m)
    {
        const int WM_SYSCOMMAND = 0x0112;
        const long SC_MAXIMIZE = 0xF030;
        if (m.Msg == WM_SYSCOMMAND &&
            (m.WParam.ToInt64() & 0xFFF0) == SC_MAXIMIZE)
            return;
        const int WM_ENDSESSION = 0x0016;
        bool shutdownCancelled = m.Msg == WM_ENDSESSION && m.WParam == IntPtr.Zero;
        base.WndProc(ref m);
        if (shutdownCancelled && _betaClose.CancelShutdown()) ResumeAfterCancelledShutdown();
    }
}

internal static class WindowCaptionMover
{
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr SendMessage(IntPtr hWnd, uint msg,
        IntPtr wParam, IntPtr lParam);
}
