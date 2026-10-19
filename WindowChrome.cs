using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Janela discreta e fixa, SEM maximizar/tela cheia. Os dois botoes disponiveis
// sao Minimizar (para bandeja) e Fechar (encerramento real). Nenhum formulario
// auxiliar, persistencia em disco ou consulta extra e criada por este modulo.
internal sealed partial class MainForm
{
    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _trayMenu;
    private Icon? _windowIcon;
    private Panel? _caption;
    private bool _hiddenInTray;

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
        Resize += (_, _) =>
        {
            if (!IsDisposed && WindowState == FormWindowState.Minimized)
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
        if (IsDisposed || _hiddenInTray || _trayIcon is null) return;
        _hiddenInTray = true;
        _trayIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
    }

    internal void RestoreFromTray()
    {
        if (IsDisposed) return;
        _hiddenInTray = false;
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Show();
        if (_trayIcon is not null) _trayIcon.Visible = false;
        Activate();
        BringToFront();
    }

    private void RequestTrayExit()
    {
        if (IsDisposed) return;
        // Preserva politica existente: cancelar primeiro downloads/consultas
        // em andamento; nao interromper uma instalacao no meio.
        if (_busy) RestoreFromTray();
        Close();
    }

    // Bloqueia mesmo uma tentativa por atalhos do Windows/WM_SYSCOMMAND.
    protected override void WndProc(ref Message m)
    {
        const int WM_SYSCOMMAND = 0x0112;
        const long SC_MAXIMIZE = 0xF030;
        if (m.Msg == WM_SYSCOMMAND &&
            (m.WParam.ToInt64() & 0xFFF0) == SC_MAXIMIZE)
            return;
        base.WndProc(ref m);
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
