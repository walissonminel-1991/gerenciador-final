using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Novos viewports são independentes de DarkLogView. O controle negativo
// histórico do Lote 4 continua podendo substituir apenas a classe de log,
// compilar e detectar a sobreposição genuína, sem apagar estes tipos.
// Reutiliza a barra temática e a rolagem de linhas do editor nativo,
// sem substituir o TextBox acessível, seleção, cópia ou teclado.
internal sealed class V269TextScrollHost : Panel
{
    private readonly TextBox _editor;
    private readonly DarkVScrollBar _scroll = new() { Name = "V269TextScroll", Visible = false, AccessibleName = "Rolagem do texto" };
    private bool _ownScroll = true, _arranging, _scheduled;
    private V269Theme _theme = V269Theme.AzulGf;

    internal V269TextScrollHost(TextBox editor)
    {
        _editor = editor;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        _editor.Dock = DockStyle.None;
        _editor.ScrollBars = ScrollBars.None;
        Controls.Add(_editor);
        Controls.Add(_scroll);
        _scroll.ApplyAppScrollTheme(_theme);
        _scroll.ValueChanged += (_, _) => { if (!_arranging) ScrollTo(_scroll.Value); };
        _editor.TextChanged += (_, _) => Reflow();
        _editor.FontChanged += (_, _) => Reflow();
        _editor.HandleCreated += (_, _) => Reflow();
        _editor.MouseWheel += (_, _) => SchedulePosition();
        _editor.KeyUp += (_, _) => SchedulePosition();
        _editor.MouseUp += (_, _) => SchedulePosition();
        SizeChanged += (_, _) => Reflow();
        VisibleChanged += (_, _) => Reflow();
    }

    internal void Configure(V269Theme theme, bool ownScroll = true)
    {
        if (_theme != theme)
        {
            _theme = theme;
            _scroll.ApplyAppScrollTheme(theme);
        }
        if (_ownScroll != ownScroll)
        {
            _ownScroll = ownScroll;
            Reflow();
        }
    }

    private const int EM_GETLINECOUNT = 0x00BA, EM_GETFIRSTVISIBLELINE = 0x00CE, EM_LINESCROLL = 0x00B6;
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp);
    private int Query(int msg) => SendMessage(_editor.Handle, msg, IntPtr.Zero, IntPtr.Zero).ToInt32();
    private void ScrollTo(int line)
    {
        if (!_editor.IsHandleCreated) return;
        int delta = line - Query(EM_GETFIRSTVISIBLELINE);
        if (delta != 0) SendMessage(_editor.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
    }
    internal void Reflow()
    {
        if (_arranging || IsDisposed || Disposing || !_editor.IsHandleCreated || !Visible) return;
        _arranging = true;
        try
        {
            int width = Math.Max(1, ClientSize.Width), height = Math.Max(1, ClientSize.Height);
            int first = Query(EM_GETFIRSTVISIBLELINE);
            // Medir primeiro sem reservar barra: nunca sustentar overflow
            // artificial causado pela própria largura da barra.
            _editor.Bounds = new Rectangle(0,0,width,height);
            int page = Math.Max(1, (_editor.ClientSize.Height - 2) / Math.Max(1,_editor.Font.Height));
            int lines = Query(EM_GETLINECOUNT);
            bool overflow = _ownScroll && lines > page;
            if (overflow)
            {
                int scrollbarWidth = _scroll.Width;
                _editor.Width = Math.Max(1,width-scrollbarWidth);
                _scroll.Bounds = new Rectangle(width-scrollbarWidth,0,scrollbarWidth,height);
                lines = Query(EM_GETLINECOUNT);
            }
            _scroll.SetMetrics(overflow ? lines : 0,page);
            ScrollTo(_ownScroll ? Math.Clamp(first,0,_scroll.MaximumValue) : 0);
            _scroll.Value = Query(EM_GETFIRSTVISIBLELINE);
        }
        finally { _arranging = false; }
    }
    private void SchedulePosition()
    {
        if (_scheduled || !IsHandleCreated || IsDisposed || !_ownScroll) return;
        _scheduled = true;
        try
        {
            BeginInvoke((Action)(() =>
            {
                _scheduled = false;
                if (IsDisposed || !_editor.IsHandleCreated) return;
                _arranging = true;
                try { _scroll.Value = Query(EM_GETFIRSTVISIBLELINE); }
                finally { _arranging = false; }
            }));
        }
        catch (InvalidOperationException) { _scheduled = false; }
    }
}

// Viewport de revisão WUA: conteúdo de altura mínima e controles
// originais, com rolagem externa somente se a janela não comportar tudo.
internal sealed class V269ReviewScrollHost : Panel
{
    private readonly Panel _viewport = new() { Dock = DockStyle.Fill, AutoScroll = false };
    private readonly DarkVScrollBar _scroll = new() { Name = "V269ReviewScroll", Dock = DockStyle.Right, Visible = false, AccessibleName = "Rolagem da revisão" };
    private readonly Control _content;
    private readonly int _minimumHeight;
    private readonly WheelDeltaAccumulator _wheel = new();
    private bool _arranging;

    internal V269ReviewScrollHost(Control content, int minimumHeight)
    {
        _content = content;
        _minimumHeight = minimumHeight;
        Dock = DockStyle.Fill;
        AutoScroll = false;
        _content.Dock = DockStyle.None;
        _viewport.Controls.Add(_content);
        Controls.Add(_viewport);
        Controls.Add(_scroll);
        _scroll.BringToFront();
        _scroll.ApplyAppScrollTheme(V269Theme.AzulGf);
        _scroll.ValueChanged += (_, _) => _content.Top = -_scroll.Value;
        SizeChanged += (_, _) => Reflow();
        DpiChangedAfterParent += (_, _) => Reflow();
        VisibleChanged += (_, _) => Reflow();
        _viewport.SizeChanged += (_, _) => Reflow();
        void Track(Control control)
        {
            // TextBox tem seu próprio teclado/cópia e roda nativa de linhas.
            // Não propagar seu movimento para o viewport de autorização.
            if (control is not (TextBoxBase or ListBox or V269TextScrollHost or DarkVScrollBar))
                control.MouseWheel += (_, e) =>
                {
                    int notches = _wheel.Consume(e.Delta);
                    if (notches != 0) _scroll.Value = (int)Math.Clamp((long)_scroll.Value-85L*notches,0,_scroll.MaximumValue);
                };
            if (control is Button or CheckBox or ListBox or TextBoxBase)
                control.Enter += (_, _) =>
                {
                    if (!control.Visible || !control.IsHandleCreated) return;
                    var bounds = _content.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                    if (bounds.Top < _scroll.Value) _scroll.Value = bounds.Top;
                    else if (bounds.Bottom > _scroll.Value + _viewport.ClientSize.Height)
                        _scroll.Value = bounds.Bottom - _viewport.ClientSize.Height;
                };
            foreach (Control child in control.Controls) Track(child);
            control.ControlAdded += (_, e) => { if (e.Control is { } child) Track(child); };
        }
        Track(_viewport);
        Reflow();
    }

    internal void Configure(V269Theme theme) { _scroll.ApplyAppScrollTheme(theme); Reflow(); }

    private void Reflow()
    {
        if (_arranging || IsDisposed || Disposing) return;
        _arranging = true;
        try
        {
            int height = Math.Max(1, ClientSize.Height);
            int required = Math.Max(height,(int)Math.Ceiling(_minimumHeight * DeviceDpi / 96d));
            _scroll.SetMetrics(required,height);
            int width = Math.Max(1,ClientSize.Width - (required > height ? _scroll.Width : 0));
            _content.Bounds = new Rectangle(0,-_scroll.Value,width,required);
        }
        finally { _arranging = false; }
    }
}

internal static class V269ScrollThemeExtensions
{
    internal static void ApplyScrollTheme(this DarkLogView view,V269Theme theme)
    {
        void Apply(Control control)
        {
            if (control is DarkVScrollBar bar) bar.ApplyAppScrollTheme(theme);
            foreach (Control child in control.Controls) Apply(child);
        }
        Apply(view);
        view.PerformLayout();
    }
}
