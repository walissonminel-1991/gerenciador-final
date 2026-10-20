using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Todas as barras sao desenhadas pelo proprio aplicativo. Nenhum tema global
// do Windows, politica de seguranca ou componente externo e alterado.
internal sealed class DarkVScrollBar : Control
{
    private int _contentSize;
    private int _pageSize = 1;
    private int _value;
    private bool _dragging;
    private int _dragY;
    private int _dragValue;
    private bool _hot;
    private static readonly Color Track = Color.FromArgb(29, 38, 49);
    private static readonly Color Thumb = Color.FromArgb(87, 110, 134);
    private static readonly Color ThumbHot = Color.FromArgb(117, 157, 196);

    public event EventHandler? ValueChanged;

    public DarkVScrollBar()
    {
        Width = 14;
        MinimumSize = new Size(12, 25);
        BackColor = Track;
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set => ChangeValue(value);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MaximumValue => Math.Max(0, _contentSize - _pageSize);

    public void SetMetrics(int contentSize, int pageSize)
    {
        int nextContent = Math.Max(0, contentSize);
        int nextPage = Math.Max(1, pageSize);
        bool needed = nextContent > nextPage;
        // Nao redesenhar barra se a metrica e a visibilidade nao mudaram.
        // Evita cintilacao na area de registro a cada evento de texto.
        if (_contentSize == nextContent && _pageSize == nextPage && Visible == needed)
            return;
        _contentSize = nextContent;
        _pageSize = nextPage;
        if (Visible != needed) Visible = needed;
        ChangeValue(_value);
        if (needed) Invalidate();
    }

    private void ChangeValue(int next)
    {
        int normalized = Math.Clamp(next, 0, MaximumValue);
        if (_value == normalized) return;
        _value = normalized;
        Invalidate();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private Rectangle ThumbBounds()
    {
        const int margin = 3;
        int available = Math.Max(1, Height - margin * 2);
        if (_contentSize <= 0 || _contentSize <= _pageSize)
            return new Rectangle(margin, margin, Math.Max(2, Width - margin * 2), available);
        int size = Math.Clamp((int)Math.Round(available * (_pageSize / (double)_contentSize)), Math.Min(25, available), available);
        int travel = available - size;
        int pos = MaximumValue == 0 ? 0 : (int)Math.Round(travel * (_value / (double)MaximumValue));
        return new Rectangle(margin, margin + pos, Math.Max(2, Width - margin * 2), size);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Track);
        if (!Visible || MaximumValue == 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = ThumbBounds();
        using var path = ModernButton.RoundRect(rect, 5);
        using var brush = new SolidBrush(_hot || _dragging ? ThumbHot : Thumb);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || MaximumValue == 0) return;
        Focus();
        Rectangle thumb = ThumbBounds();
        if (thumb.Contains(e.Location))
        {
            _dragging = true;
            _dragY = e.Y;
            _dragValue = _value;
            Capture = true;
        }
        else
        {
            Value += e.Y < thumb.Top ? -_pageSize : _pageSize;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            Rectangle thumb = ThumbBounds();
            int travel = Math.Max(1, Height - 6 - thumb.Height);
            Value = _dragValue + (int)Math.Round((e.Y - _dragY) * (MaximumValue / (double)travel));
        }
        else
        {
            bool hot = ThumbBounds().Contains(e.Location);
            if (hot != _hot) { _hot = hot; Invalidate(); }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_dragging) { _hot = false; Invalidate(); }
    }

    protected override bool IsInputKey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        return key is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up: Value -= 35; break;
            case Keys.Down: Value += 35; break;
            case Keys.PageUp: Value -= _pageSize; break;
            case Keys.PageDown: Value += _pageSize; break;
            case Keys.Home: Value = 0; break;
            case Keys.End: Value = MaximumValue; break;
            default: base.OnKeyDown(e); return;
        }
        e.Handled = true;
    }
}

internal static class AppRowGeometry
{
    public static Rectangle Checkbox(Rectangle bounds) => new(bounds.Left + 17, bounds.Top + 23, 18, 18);
    public static Rectangle UpdateButton(Rectangle bounds) => new(bounds.Right - 318, bounds.Top + 16, 112, 33);
    public static Rectangle SiteButton(Rectangle bounds) => new(bounds.Right - 200, bounds.Top + 16, 80, 33);
    public static Rectangle UninstallButton(Rectangle bounds) => new(bounds.Right - 115, bounds.Top + 16, 103, 33);
}

internal sealed class DarkProgramList : Control
{
    private readonly List<ProgramEntry> _items = new();
    private readonly ToolTip _itemTips = new() { InitialDelay = 450, AutoPopDelay = 12000 };
    private int _tipRow = -1;
    private readonly DarkVScrollBar _scrollbar = new();
    private int _selectedIndex = -1;
    private int _scrollOffset;
    private int _itemHeight = 72;
    // Pressão visual de 160 ms antes de abrir instaladores/navegador.
    // Uma só animação leve por clique, sem formularios extras, sons ou arquivos.
    private readonly System.Windows.Forms.Timer _feedbackTimer = new() { Interval = 16 };
    private ProgramEntry? _hoverItem;
    private string _hoverAction = "";
    private ProgramEntry? _downItem;
    private string _downAction = "";
    private ProgramEntry? _pulseItem;
    private string _pulseAction = "";
    private long _pulseStart;
    private float _pulseProgress;

    public event EventHandler? SelectedIndexChanged;
    public event DrawItemEventHandler? DrawItem;
    public event Action<ProgramEntry, string>? ItemActionClicked;
    public event Action<ProgramEntry>? ItemCheckboxClicked;
    private bool _multiSelectMode;
    private bool _actionsEnabled = true;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MultiSelectMode
    {
        get => _multiSelectMode;
        set { _multiSelectMode = value; ResetActionVisuals(); Invalidate(); }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ActionsEnabled
    {
        get => _actionsEnabled;
        set { _actionsEnabled = value; if (!value) ResetActionVisuals(); Invalidate(); }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<ProgramEntry> Items => _items;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ProgramEntry? SelectedItem => _selectedIndex < 0 || _selectedIndex >= _items.Count ? null : _items[_selectedIndex];
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ItemHeight { get => _itemHeight; set { _itemHeight = Math.Max(24, value); UpdateMetrics(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SelectIndex(value);
    }

    public DarkProgramList()
    {
        BackColor = Color.FromArgb(30, 39, 50);
        ForeColor = Color.FromArgb(237, 243, 250);
        Font = new Font("Segoe UI", 9.5F);
        TabStop = true;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);
        _scrollbar.Dock = DockStyle.Right;
        _scrollbar.ValueChanged += (_, _) => { _scrollOffset = _scrollbar.Value; Invalidate(); };
        Controls.Add(_scrollbar);
        Resize += (_, _) => UpdateMetrics();
        _feedbackTimer.Tick += (_, _) => AdvanceButtonAnimation();
    }

    private void ResetActionVisuals()
    {
        _feedbackTimer.Stop();
        _hoverItem = _downItem = _pulseItem = null;
        _hoverAction = _downAction = _pulseAction = "";
        _pulseProgress = 0;
        Cursor = Cursors.Default;
    }

    // Estado da interação usado exclusivamente para desenhar os botões da lista.
    internal (bool Hovered, bool Pressed, float Pulse) ActionVisual(ProgramEntry item, string action)
    {
        bool hover = _actionsEnabled && ReferenceEquals(item, _hoverItem) && _hoverAction == action;
        bool down = ReferenceEquals(item, _downItem) && _downAction == action && hover;
        bool pulse = ReferenceEquals(item, _pulseItem) && _pulseAction == action;
        return (hover, down || (pulse && _pulseProgress < 0.75F), pulse ? _pulseProgress : 0F);
    }

    private (ProgramEntry? Item, string Action) HitAction(Point point)
    {
        int contentWidth = ClientSize.Width - (_scrollbar.Visible ? _scrollbar.Width : 0);
        if (!_actionsEnabled || _multiSelectMode || point.X < 0 || point.Y < 0 ||
            point.X >= contentWidth || point.Y >= ClientSize.Height || _itemHeight <= 0)
            return (null, "");
        int index = (point.Y + _scrollOffset) / _itemHeight;
        if (index < 0 || index >= _items.Count) return (null, "");
        var item = _items[index];
        var bounds = new Rectangle(0, index * _itemHeight - _scrollOffset, contentWidth, _itemHeight);
        if (item.CanUninstall && AppRowGeometry.UninstallButton(bounds).Contains(point)) return (item, "uninstall");
        if (item.IsUpdate && AppRowGeometry.UpdateButton(bounds).Contains(point))
        {
            string? primaryAction = VerifiedUpdaters.GetPrimaryAction(item);
            if (primaryAction is not null) return (item, primaryAction);
        }
        // O botao secundario Site so aparece quando Atualizar e seguro e direto.
        if (item.IsUpdate &&
            (VerifiedUpdaters.Resolve(item) is not null || WindowsTerminalUpdater.Resolve(item) is not null) &&
            VerifiedUpdaters.GetOfficialPage(item) is not null &&
            AppRowGeometry.SiteButton(bounds).Contains(point)) return (item, "site");
        return (null, "");
    }

    private void AdvanceButtonAnimation()
    {
        if (_pulseItem is null) { _feedbackTimer.Stop(); return; }
        _pulseProgress = Math.Clamp((float)((Stopwatch.GetTimestamp() - _pulseStart) * 1000.0 /
            Stopwatch.Frequency / 160.0), 0F, 1F);
        Invalidate();
        if (_pulseProgress < 1F) return;
        var item = _pulseItem;
        var action = _pulseAction;
        _feedbackTimer.Stop();
        _pulseItem = null;
        _pulseAction = "";
        _pulseProgress = 0;
        Invalidate();
        if (!IsDisposed && _actionsEnabled && !_multiSelectMode && _items.Contains(item))
            ItemActionClicked?.Invoke(item, action);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _feedbackTimer.Stop(); _feedbackTimer.Dispose(); _itemTips.Dispose(); }
        base.Dispose(disposing);
    }

    public void ScrollToTop() => _scrollbar.Value = 0;

    public void SetItems(IEnumerable<ProgramEntry> entries)
    {
        ResetActionVisuals();
        ProgramEntry? prior = SelectedItem;
        _items.Clear();
        _items.AddRange(entries);
        _tipRow = -1;
        int oldSelected = _selectedIndex;
        _selectedIndex = prior is null ? -1 : _items.FindIndex(x => ReferenceEquals(x, prior));
        UpdateMetrics();
        if (oldSelected != _selectedIndex) SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void UpdateMetrics()
    {
        _scrollbar.SetMetrics(_items.Count * _itemHeight, Math.Max(1, ClientSize.Height));
        _scrollOffset = _scrollbar.Value;
        Invalidate();
    }

    private void SelectIndex(int index)
    {
        int next = index >= 0 && index < _items.Count ? index : -1;
        if (_selectedIndex == next) return;
        _selectedIndex = next;
        if (next >= 0)
        {
            int top = next * _itemHeight;
            int bottom = top + _itemHeight;
            if (top < _scrollOffset) _scrollbar.Value = top;
            else if (bottom > _scrollOffset + ClientSize.Height)
                _scrollbar.Value = bottom - ClientSize.Height;
        }
        Invalidate();
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        int contentWidth = Math.Max(1, ClientSize.Width - (_scrollbar.Visible ? _scrollbar.Width : 0));
        var original = e.Graphics.Clip;
        e.Graphics.SetClip(new Rectangle(0, 0, contentWidth, ClientSize.Height));
        try
        {
            if (_itemHeight <= 0 || ClientSize.Height <= 0) return;
            int first = Math.Max(0, _scrollOffset / _itemHeight);
            int last = Math.Min(_items.Count - 1, (_scrollOffset + ClientSize.Height) / _itemHeight);
            for (int i = first; i <= last; i++)
            {
                var rect = new Rectangle(0, i * _itemHeight - _scrollOffset, contentWidth, _itemHeight);
                DrawItem?.Invoke(this, new DrawItemEventArgs(e.Graphics, Font, rect, i,
                    i == _selectedIndex ? DrawItemState.Selected : DrawItemState.Default));
            }
        }
        finally { e.Graphics.Clip = original; original.Dispose(); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = _itemHeight > 0 ? (e.Y + _scrollOffset) / _itemHeight : -1;
        if (index != _tipRow)
        {
            _tipRow = index;
            string text = index >= 0 && index < _items.Count
                ? _items[index].Name + "\nVersão atual: " + _items[index].Version +
                  "\nDisponível: " + _items[index].AvailableVersion +
                  "\nFonte: " + _items[index].Source
                : "";
            _itemTips.SetToolTip(this, text);
        }
        var (item, action) = HitAction(e.Location);
        if (!ReferenceEquals(item, _hoverItem) || _hoverAction != action)
        {
            _hoverItem = item;
            _hoverAction = action;
            Cursor = item is null ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverItem = null;
        _hoverAction = "";
        _tipRow = -1;
        _itemTips.SetToolTip(this, "");
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int contentWidth = ClientSize.Width - (_scrollbar.Visible ? _scrollbar.Width : 0);
        if (e.Button != MouseButtons.Left || e.X >= contentWidth || !_actionsEnabled) return;
        int index = (e.Y + _scrollOffset) / _itemHeight;
        if (index < 0 || index >= _items.Count) return;
        var item = _items[index];
        Focus();
        if (_multiSelectMode)
        {
            SelectIndex(index);
            if (item.CanUninstall) ItemCheckboxClicked?.Invoke(item);
            return;
        }
        var (hit, action) = HitAction(e.Location);
        _downItem = hit;
        _downAction = action;
        _hoverItem = hit;
        _hoverAction = action;
        _feedbackTimer.Stop();
        _pulseItem = null;
        _pulseAction = "";
        _pulseProgress = 0;
        SelectIndex(index);
        Invalidate(); // Mostra o botão pressionado ANTES de executar a ação.
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        var (hit, action) = HitAction(e.Location);
        bool click = hit is not null && ReferenceEquals(hit, _downItem) && action == _downAction;
        _downItem = null;
        _downAction = "";
        if (click)
        {
            // Feedback perceptível mesmo para a ação que abre o navegador instantaneamente.
            _pulseItem = hit;
            _pulseAction = action;
            _pulseStart = Stopwatch.GetTimestamp();
            _pulseProgress = 0;
            _feedbackTimer.Start();
        }
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _scrollbar.Value -= Math.Sign(e.Delta) * _itemHeight * 3;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        return key is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_multiSelectMode && _actionsEnabled && e.KeyCode == Keys.Space && SelectedItem is ProgramEntry current)
        {
            if (current.CanUninstall) ItemCheckboxClicked?.Invoke(current);
            e.Handled = true;
            return;
        }
        int page = Math.Max(1, ClientSize.Height / _itemHeight);
        int next = _selectedIndex;
        switch (e.KeyCode)
        {
            case Keys.Up: next = Math.Max(0, next - 1); break;
            case Keys.Down: next = Math.Min(_items.Count - 1, next + 1); break;
            case Keys.Home: next = 0; break;
            case Keys.End: next = _items.Count - 1; break;
            case Keys.PageUp: next = Math.Max(0, next - page); break;
            case Keys.PageDown: next = Math.Min(_items.Count - 1, Math.Max(0, next) + page); break;
            default: base.OnKeyDown(e); return;
        }
        SelectIndex(next);
        e.Handled = true;
    }
}

// TextBox sem barras nativas: preserva selecao de texto, Ctrl+C e Ctrl+A.
// A barra propria manipula apenas a primeira linha visivel do editor.
internal sealed class DarkLogView : UserControl
{
    private const int EM_GETLINECOUNT = 0x00BA;
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
    private const int EM_LINESCROLL = 0x00B6;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly TextBox _editor = new();
    private readonly DarkVScrollBar _scroll = new();
    private readonly System.Windows.Forms.Timer _renderTimer = new() { Interval = 85 };
    private readonly System.Text.StringBuilder _pending = new();
    private bool _syncing;
    private bool _syncScheduled;
    internal int BatchFlushCount { get; private set; }
    internal string RenderedText
    {
        get { FlushPending(); return _editor.Text; }
    }

    public DarkLogView()
    {
        BackColor = Color.FromArgb(17, 24, 32);
        var border = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(61, 73, 86), Padding = new Padding(1) };
        Controls.Add(border);
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
        border.Controls.Add(inner);
        _editor.Multiline = true;
        _editor.ReadOnly = true;
        _editor.ScrollBars = ScrollBars.None;
        _editor.WordWrap = true;
        _editor.BorderStyle = BorderStyle.None;
        _editor.BackColor = BackColor;
        _editor.ForeColor = Color.FromArgb(237, 243, 250);
        _editor.Font = new Font("Consolas", 8.5F);
        _editor.Dock = DockStyle.Fill;
        _editor.Margin = Padding.Empty;
        _scroll.Dock = DockStyle.Right;
        _scroll.Width = 14;
        _scroll.ValueChanged += (_, _) => ScrollTo(_scroll.Value);
        // Ordem de Dock importa: barra e editor permanecem lado a lado.
        inner.Controls.Add(_editor);
        inner.Controls.Add(_scroll);
        _scroll.BringToFront();
        _editor.Resize += (_, _) => ScheduleSync();
        _editor.HandleCreated += (_, _) => ScheduleSync();
        _editor.TextChanged += (_, _) => ScheduleSync();
        _editor.MouseWheel += (_, _) => ScheduleSync();
        _editor.KeyUp += (_, _) => ScheduleSync();
        Resize += (_, _) => ScheduleSync();
        _renderTimer.Tick += (_, _) => FlushPending();
    }

    // Para relatórios estáticos: apresenta o texto desde a primeira linha,
    // sem o temporizador de lotes usado pelo registro em tempo real.
    public void SetText(string value)
    {
        _renderTimer.Stop();
        _pending.Clear();
        _editor.Text = value ?? "";
        _editor.SelectionStart = 0;
        _editor.SelectionLength = 0;
        _editor.ScrollToCaret();
        SyncMetrics();
    }

    // Mensagens consecutivas chegam a cada poucos milisseundos no arranque.
    // Apresenta-las juntas evita varios ScrollToCaret e dezenas de repinturas.
    // A fila existe so em memoria; nao interfere no arquivo DiagnosticTrace.
    public void AppendText(string text)
    {
        if (IsDisposed || string.IsNullOrEmpty(text)) return;
        _pending.Append(text);
        if (!_renderTimer.Enabled) _renderTimer.Start();
    }

    internal void FlushPending()
    {
        _renderTimer.Stop();
        if (IsDisposed || _editor.IsDisposed || _pending.Length == 0) return;
        string batch = _pending.ToString();
        _pending.Clear();
        BatchFlushCount++;
        _editor.AppendText(batch);
        _editor.SelectionStart = _editor.TextLength;
        _editor.ScrollToCaret();
        SyncMetrics();
    }

    private void ScheduleSync()
    {
        if (IsDisposed || !IsHandleCreated || _syncScheduled) return;
        _syncScheduled = true;
        try
        {
            BeginInvoke((Action)(() =>
            {
                _syncScheduled = false;
                if (!IsDisposed && !_renderTimer.Enabled) SyncMetrics();
            }));
        }
        catch (InvalidOperationException) { _syncScheduled = false; }
    }

    private void SyncMetrics()
    {
        if (IsDisposed || !_editor.IsHandleCreated || _syncing) return;
        _syncing = true;
        try
        {
            int lines = SendMessage(_editor.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
            int lineHeight = Math.Max(1, _editor.Font.Height + 1);
            int page = Math.Max(1, _editor.ClientSize.Height / lineHeight);
            _scroll.SetMetrics(lines, page);
            _scroll.Value = SendMessage(_editor.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
        }
        finally { _syncing = false; }
    }

    private void ScrollTo(int target)
    {
        if (_syncing || !_editor.IsHandleCreated) return;
        int current = SendMessage(_editor.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
        int delta = target - current;
        if (delta != 0) SendMessage(_editor.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderTimer.Stop();
            _renderTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}

// FlowLayoutPanel sem AutoScroll: as barras nativas nunca ficam visiveis.
// Usado nos cartoes das ferramentas e no painel de unidades.
internal sealed class DarkScrollableFlow : UserControl
{
    private readonly Panel _viewport;
    private readonly FlowLayoutPanel _flow;
    private readonly DarkVScrollBar _scroll;
    private bool _arranging;

    // Um unico scroll pertence a pagina externa. Componentes embutidos
    // mostram todo o conteudo, em vez de instalar barras independentes.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool EmbeddedInPage { get; set; }

    // Evita o scroll fantasma de 3 px da folga utilizada pelos cartoes.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool IsPageViewport { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool HasVerticalScrollbar => _scroll.Visible;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FlowLayoutPanel Content => _flow;

    public DarkScrollableFlow()
    {
        BackColor = Color.FromArgb(21, 27, 35);
        _viewport = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
        _scroll = new DarkVScrollBar { Dock = DockStyle.Right };
        _flow = new FlowLayoutPanel
        {
            WrapContents = true, AutoScroll = false, FlowDirection = FlowDirection.LeftToRight,
            BackColor = BackColor, Padding = new Padding(12), Margin = Padding.Empty
        };
        _viewport.Controls.Add(_flow);
        Controls.Add(_viewport);
        Controls.Add(_scroll);
        _scroll.BringToFront();
        _scroll.ValueChanged += (_, _) =>
        {
            if (!EmbeddedInPage) _flow.Top = -_scroll.Value;
        };
        _viewport.Resize += (_, _) => Reflow();
        _flow.ControlAdded += (_, e) => { if (e.Control is not null) TrackFocus(e.Control); Reflow(); };
        _flow.ControlRemoved += (_, _) => Reflow();
        _viewport.MouseWheel += (_, e) => _scroll.Value -= Math.Sign(e.Delta) * 85;
        _flow.MouseWheel += (_, e) => _scroll.Value -= Math.Sign(e.Delta) * 85;
        Resize += (_, _) => Reflow();
        VisibleChanged += (_, _) =>
        {
            if (FindForm() is MainForm form && form.IsTrayTransitioning) return;
            Reflow();
        };
        DpiChangedAfterParent += (_, _) => Reflow();
    }

    private void TrackFocus(Control control)
    {
        control.Enter += (_, _) =>
        {
            if (!control.Visible || !control.IsHandleCreated) return;
            var rect = _flow.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
            if (rect.Top < _scroll.Value) _scroll.Value = rect.Top;
            else if (rect.Bottom > _scroll.Value + _viewport.Height)
                _scroll.Value = rect.Bottom - _viewport.Height;
        };
        control.ControlAdded += (_, e) => { if (e.Control is not null) TrackFocus(e.Control); };
        foreach (Control child in control.Controls) TrackFocus(child);
    }

    internal int MeasureContentHeight(int width)
    {
        int usable = Math.Max(1, width - _flow.Padding.Horizontal);
        int used = 0, rowHeight = 0, total = _flow.Padding.Vertical;
        foreach (Control card in _flow.Controls)
        {
            // Visible herda o estado da aba; medir mesmo antes de ela ser aberta.
            int w = card.Width + card.Margin.Horizontal;
            int h = card.Height + card.Margin.Vertical;
            if (used > 0 && used + w > usable)
            {
                total += rowHeight;
                used = 0;
                rowHeight = 0;
            }
            used += w;
            rowHeight = Math.Max(rowHeight, h);
        }
        return total + rowHeight + 3;
    }

    public void Reflow()
    {
        if (_arranging || IsDisposed) return;
        int width = _viewport.ClientSize.Width;
        int height = _viewport.ClientSize.Height;
        if (width <= 0 || height <= 0) return;
        _arranging = true;
        try
        {
            _flow.Width = width;
            int measured = MeasureContentHeight(width);
            int totalHeight = IsPageViewport ? Math.Max(0, measured - 3) : measured;
            _flow.Height = Math.Max(totalHeight, height);
            if (EmbeddedInPage)
            {
                _scroll.SetMetrics(0, height);
                _scroll.Visible = false;
                _flow.Top = 0;
            }
            else
            {
                _scroll.SetMetrics(totalHeight, height);
                _flow.Top = -_scroll.Value;
            }
            _flow.Left = 0;
            _flow.PerformLayout();
        }
        finally { _arranging = false; }
    }
}

// Abas 100% desenhadas no tema escuro, sem o fundo claro da TabControl nativa.
internal sealed class DarkTabHost : UserControl
{
    private readonly List<(string Title, Control Page)> _pages = new();
    private readonly TabStrip _strip;
    private readonly Panel _content;
    private int _selectedIndex = -1;
    private const int StripHeight = 35;

    public event EventHandler? SelectedIndexChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex => _selectedIndex;
    internal int TabCount => _pages.Count;
    internal string[] TabTitles => _pages.Select(item => item.Title).ToArray();
    internal Rectangle[] NavigationBounds => Enumerable.Range(0, _pages.Count)
        .Select(index => _strip.GetButtonBounds(index)).ToArray();
    internal int NavigationHitTest(Point point) => _strip.HitTestForLayout(point);

    public DarkTabHost()
    {
        BackColor = Color.FromArgb(21, 27, 35);
        _strip = new TabStrip(this) { Height = StripHeight };
        _content = new Panel { BackColor = BackColor, Padding = Padding.Empty };
        Controls.Add(_content);
        Controls.Add(_strip);
        Resize += (_, _) => LayoutHost();
        FontChanged += (_, _) => LayoutHost();
        DpiChangedAfterParent += (_, _) => LayoutHost();
        LayoutHost();
        _strip.BringToFront();
    }

    private void LayoutHost()
    {
        if (IsDisposed) return;
        // Altura proporcional ao DPI; todas as abas permanecem acessiveis.
        int eachTab = ClientSize.Width / Math.Max(1, _pages.Count);
        // Seis abas igualmente distribuídas. Quando necessário, títulos
        // longos podem ocupar duas linhas, sem serem abreviados.
        double scale = Math.Max(1, DeviceDpi / 96d);
        int height = eachTab < 145 * scale ? 44 : 35;
        int scaledStrip = (int)Math.Ceiling(height * Math.Max(1, DeviceDpi) / 96d);
        _strip.Bounds = new Rectangle(0, 0, Math.Max(0, ClientSize.Width), Math.Min(scaledStrip, Math.Max(0, ClientSize.Height)));
        int contentTop = _strip.Bottom;
        _content.Bounds = new Rectangle(0, contentTop, Math.Max(0, ClientSize.Width), Math.Max(0, ClientSize.Height - contentTop));
        _content.PerformLayout();
        foreach (var (_, page) in _pages)
        {
            page.Bounds = new Rectangle(0, 0, Math.Max(0, _content.ClientSize.Width), Math.Max(0, _content.ClientSize.Height));
        }
    }

    public void AddTab(string title, Control page)
    {
        page.Dock = DockStyle.Fill;
        page.Margin = Padding.Empty;
        _pages.Add((title, page));
        _content.Controls.Add(page);
        LayoutHost();
        if (_selectedIndex < 0) SelectTab(0);
        else page.Visible = false;
        _strip.Invalidate();
    }

    public void SelectTab(int index)
    {
        if (index < 0 || index >= _pages.Count || _selectedIndex == index) return;
        _selectedIndex = index;
        for (int i = 0; i < _pages.Count; i++) _pages[i].Page.Visible = i == index;
        _pages[index].Page.BringToFront();
        LayoutHost();
        _strip.Invalidate();
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class TabStrip : Control
    {
        private readonly DarkTabHost _host;
        private readonly System.Windows.Forms.Timer _feedbackTimer = new() { Interval = 16 };
        private readonly ToolTip _tabTips = new() { InitialDelay = 250, AutoPopDelay = 7000 };
        private int _hotTab = -1;
        private int _pressedTab = -1;
        private int _pulseTab = -1;
        private long _pulseStart;
        private float _pulseProgress = 1F;
        private const double PulseMs = 145;
        public TabStrip(DarkTabHost host)
        {
            _host = host;
            BackColor = Color.FromArgb(21, 27, 35);
            ForeColor = Color.FromArgb(237, 243, 250);
            Font = new Font("Segoe UI", 8.8F, FontStyle.Bold);
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            _feedbackTimer.Tick += (_, _) =>
            {
                _pulseProgress = Math.Clamp((float)((Stopwatch.GetTimestamp() - _pulseStart) *
                    1000.0 / Stopwatch.Frequency / PulseMs), 0F, 1F);
                Invalidate();
                if (_pulseProgress < 1F) return;
                _feedbackTimer.Stop();
                int next = _pulseTab;
                _pulseTab = -1;
                if (!IsDisposed && next >= 0) _host.SelectTab(next);
                Invalidate();
            };
        }

        private int HitTab(Point point)
        {
            if (point.X < 0 || point.Y < 0 || point.Y >= Height) return -1;
            int relative = point.X - TabOffset;
            if (relative < 0 || relative >= TabAreaWidth * _host._pages.Count) return -1;
            int index = relative / TabAreaWidth;
            return index >= 0 && index < _host._pages.Count ? index : -1;
        }

        private void StartTabFeedback(int index)
        {
            if (index < 0 || index >= _host._pages.Count) return;
            _pulseTab = index;
            _pulseProgress = 0F;
            _pulseStart = Stopwatch.GetTimestamp();
            _feedbackTimer.Start();
            Invalidate();
        }

        // Seis abas dividem a largura disponivel igualmente. A margem lateral
        // e limitada a 12 pixels, evitando corredores vazios em telas grandes.
        private int TabEdgeMargin => Math.Clamp(Width / 160, 3, 12);
        private int TabAreaWidth => Math.Max(1,
            (Width - 2 * TabEdgeMargin) / Math.Max(1, _host._pages.Count));
        private int TabOffset => Math.Max(0,
            (Width - TabAreaWidth * _host._pages.Count) / 2);
        internal Rectangle GetButtonBounds(int index)
        {
            if (index < 0 || index >= _host._pages.Count) return Rectangle.Empty;
            int w = TabAreaWidth;
            return new Rectangle(TabOffset + index * w + 3, 3,
                Math.Max(1, w - 6), Math.Max(1, Height - 9));
        }
        internal int HitTestForLayout(Point point) => HitTab(point);

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int w = TabAreaWidth;
            for (int i = 0; i < _host._pages.Count; i++)
            {
                var bounds = GetButtonBounds(i);
                bool active = _host.SelectedIndex == i;
                bool down = i == _pressedTab || (i == _pulseTab && _pulseProgress < 0.65F);
                float glow = i == _pulseTab ? MathF.Sin(_pulseProgress * MathF.PI) : 0F;
                if (down) { bounds.Offset(0, 2); bounds.Height -= 2; }
                Color baseFill = active ? Color.FromArgb(43, 66, 89) : Color.FromArgb(33, 42, 53);
                if (down) baseFill = Color.FromArgb(25, 35, 46);
                else if (i == _hotTab) baseFill = Color.FromArgb(52, 69, 86);
                if (glow > 0)
                    baseFill = Color.FromArgb(
                        Math.Min(255, baseFill.R + (int)(9 * glow)),
                        Math.Min(255, baseFill.G + (int)(18 * glow)),
                        Math.Min(255, baseFill.B + (int)(23 * glow)));
                using var path = ModernButton.RoundRect(bounds, 8);
                using var brush = new SolidBrush(baseFill);
                e.Graphics.FillPath(brush, path);
                if (i == _hotTab || i == _pulseTab)
                {
                    using var outline = new Pen(Color.FromArgb(90 + (int)(50 * glow), 91, 157, 213), 1F);
                    e.Graphics.DrawPath(outline, path);
                }
                // A própria cor da aba ativa indica a seleção; sem sublinhado redundante.
                var labelColor = active ? Color.FromArgb(170, 216, 255) : Color.FromArgb(164, 179, 193);
                string caption = _host._pages[i].Title;
                // A versao anterior forçava duas linhas mesmo com largura
                // suficiente. Priorizar o titulo INTEGRAL em uma linha;
                // reduzir apenas a fonte da aba, nunca usar reticencias.
                Font chosen = Font;
                Font? fitted = null;
                try
                {
                    int available = Math.Max(12, bounds.Width - 7);
                    while (TextRenderer.MeasureText(e.Graphics, caption, chosen,
                        new Size(int.MaxValue, 32), TextFormatFlags.NoPadding |
                        TextFormatFlags.NoPrefix).Width > available &&
                        chosen.SizeInPoints > 7.1F)
                    {
                        float nextSize = Math.Max(7.0F, chosen.SizeInPoints - 0.35F);
                        fitted?.Dispose();
                        fitted = new Font(Font.FontFamily, nextSize, FontStyle.Bold);
                        chosen = fitted;
                    }
                    bool wrap = TextRenderer.MeasureText(e.Graphics, caption, chosen,
                        new Size(int.MaxValue, 32),
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width > available;
                    TextRenderer.DrawText(e.Graphics, caption, chosen, bounds, labelColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        (wrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine) |
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
                finally { fitted?.Dispose(); }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int tab = HitTab(e.Location);
            if (_hotTab == tab) return;
            _hotTab = tab;
            Cursor = tab >= 0 ? Cursors.Hand : Cursors.Default;
            _tabTips.SetToolTip(this, tab >= 0 ? _host._pages[tab].Title : string.Empty);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hotTab = -1;
            _pressedTab = -1;
            _tabTips.SetToolTip(this, string.Empty);
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            _pressedTab = HitTab(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            int clicked = HitTab(e.Location);
            bool valid = _pressedTab >= 0 && clicked == _pressedTab;
            _pressedTab = -1;
            if (valid) StartTabFeedback(clicked);
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            return k is Keys.Left or Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_host._pages.Count == 0) return;
            if (e.KeyCode == Keys.Left)
                StartTabFeedback((_host.SelectedIndex - 1 + _host._pages.Count) % _host._pages.Count);
            else if (e.KeyCode == Keys.Right)
                StartTabFeedback((_host.SelectedIndex + 1) % _host._pages.Count);
            else { base.OnKeyDown(e); return; }
            e.Handled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _feedbackTimer.Stop();
                _feedbackTimer.Dispose();
                _tabTips.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
