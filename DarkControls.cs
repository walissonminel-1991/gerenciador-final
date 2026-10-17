using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        _contentSize = Math.Max(0, contentSize);
        _pageSize = Math.Max(1, pageSize);
        bool needed = _contentSize > _pageSize;
        Visible = needed;
        ChangeValue(_value);
        Invalidate();
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
        int size = Math.Clamp((int)Math.Round(available * (_pageSize / (double)_contentSize)), 25, available);
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
    public static Rectangle Checkbox(Rectangle bounds) => new(bounds.Left + 17, bounds.Top + 28, 20, 20);
    public static Rectangle UpdateButton(Rectangle bounds) => new(bounds.Right - 234, bounds.Top + 20, 105, 36);
    public static Rectangle UninstallButton(Rectangle bounds) => new(bounds.Right - 124, bounds.Top + 20, 112, 36);
}

internal sealed class DarkProgramList : Control
{
    private readonly List<ProgramEntry> _items = new();
    private readonly DarkVScrollBar _scrollbar = new();
    private int _selectedIndex = -1;
    private int _scrollOffset;
    private int _itemHeight = 72;

    public event EventHandler? SelectedIndexChanged;
    public event DrawItemEventHandler? DrawItem;
    public event Action<ProgramEntry, string>? ItemActionClicked;
    public event Action<ProgramEntry>? ItemCheckboxClicked;
    private bool _multiSelectMode;
    private bool _actionsEnabled = true;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MultiSelectMode { get => _multiSelectMode; set { _multiSelectMode = value; Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ActionsEnabled { get => _actionsEnabled; set { _actionsEnabled = value; Invalidate(); } }
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
    }

    public void ScrollToTop() => _scrollbar.Value = 0;

    public void SetItems(IEnumerable<ProgramEntry> entries)
    {
        ProgramEntry? prior = SelectedItem;
        _items.Clear();
        _items.AddRange(entries);
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int contentWidth = ClientSize.Width - (_scrollbar.Visible ? _scrollbar.Width : 0);
        if (e.Button != MouseButtons.Left || e.X >= contentWidth || !_actionsEnabled) return;
        int index = (e.Y + _scrollOffset) / _itemHeight;
        if (index < 0 || index >= _items.Count) return;
        var item = _items[index];
        var bounds = new Rectangle(0, index * _itemHeight - _scrollOffset, contentWidth, _itemHeight);
        Focus();
        if (_multiSelectMode)
        {
            SelectIndex(index);
            ItemCheckboxClicked?.Invoke(item);
            return;
        }
        if (AppRowGeometry.UninstallButton(bounds).Contains(e.Location))
        {
            SelectIndex(index);
            ItemActionClicked?.Invoke(item, "uninstall");
            return;
        }
        if (item.IsUpdate && AppRowGeometry.UpdateButton(bounds).Contains(e.Location))
        {
            SelectIndex(index);
            ItemActionClicked?.Invoke(item, "update-not-validated");
            return;
        }
        SelectIndex(index);
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
            ItemCheckboxClicked?.Invoke(current);
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
    private bool _syncing;

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
        _editor.TextChanged += (_, _) => ScheduleSync();
        _editor.MouseWheel += (_, _) => ScheduleSync();
        _editor.KeyUp += (_, _) => ScheduleSync();
        Resize += (_, _) => ScheduleSync();
    }

    public void AppendText(string text)
    {
        _editor.AppendText(text);
        _editor.SelectionStart = _editor.TextLength;
        _editor.ScrollToCaret();
        SyncMetrics();
    }

    private void ScheduleSync()
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(new Action(SyncMetrics)); } catch (InvalidOperationException) { }
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
}

// FlowLayoutPanel sem AutoScroll: as barras nativas nunca ficam visiveis.
// Usado nos cartoes das ferramentas e no painel de unidades.
internal sealed class DarkScrollableFlow : UserControl
{
    private readonly Panel _viewport;
    private readonly FlowLayoutPanel _flow;
    private readonly DarkVScrollBar _scroll;
    private bool _arranging;

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
        _scroll.ValueChanged += (_, _) => _flow.Top = -_scroll.Value;
        _viewport.Resize += (_, _) => Reflow();
        _flow.ControlAdded += (_, _) => Reflow();
        _flow.ControlRemoved += (_, _) => Reflow();
        _viewport.MouseWheel += (_, e) => _scroll.Value -= Math.Sign(e.Delta) * 85;
        _flow.MouseWheel += (_, e) => _scroll.Value -= Math.Sign(e.Delta) * 85;
        Resize += (_, _) => Reflow();
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
            int usable = Math.Max(1, width - _flow.Padding.Horizontal);
            int used = 0, currentRowHeight = 0;
            int totalHeight = _flow.Padding.Vertical;
            foreach (Control card in _flow.Controls)
            {
                if (!card.Visible) continue;
                int w = card.Width + card.Margin.Horizontal;
                int h = card.Height + card.Margin.Vertical;
                if (used > 0 && used + w > usable)
                {
                    totalHeight += currentRowHeight;
                    used = 0;
                    currentRowHeight = 0;
                }
                used += w;
                currentRowHeight = Math.Max(currentRowHeight, h);
            }
            totalHeight += currentRowHeight + 3;
            _flow.Height = Math.Max(totalHeight, height);
            _scroll.SetMetrics(totalHeight, height);
            _flow.Top = -_scroll.Value;
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
    private const int StripHeight = 42;

    public event EventHandler? SelectedIndexChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex => _selectedIndex;

    public DarkTabHost(int tabWidth = 176)
    {
        BackColor = Color.FromArgb(21, 27, 35);
        _strip = new TabStrip(this) { Height = StripHeight, TabWidth = tabWidth };
        _content = new Panel { BackColor = BackColor, Padding = Padding.Empty };
        Controls.Add(_content);
        Controls.Add(_strip);
        Resize += (_, _) => LayoutHost();
        FontChanged += (_, _) => LayoutHost();
        LayoutHost();
        _strip.BringToFront();
    }

    private void LayoutHost()
    {
        if (IsDisposed) return;
        _strip.Bounds = new Rectangle(0, 0, Math.Max(0, ClientSize.Width), Math.Min(StripHeight, Math.Max(0, ClientSize.Height)));
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
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TabWidth { get; set; } = 176;
        public TabStrip(DarkTabHost host)
        {
            _host = host;
            BackColor = Color.FromArgb(21, 27, 35);
            ForeColor = Color.FromArgb(237, 243, 250);
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        private int TabAreaWidth => Math.Max(80, Math.Min(TabWidth, (Width - 2) / Math.Max(1, _host._pages.Count)));

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int w = TabAreaWidth;
            for (int i = 0; i < _host._pages.Count; i++)
            {
                int left = i * w + 3;
                var bounds = new Rectangle(left, 3, Math.Max(10, w - 6), Height - 9);
                bool active = _host.SelectedIndex == i;
                using var path = ModernButton.RoundRect(bounds, 8);
                using var brush = new SolidBrush(active ? Color.FromArgb(43, 66, 89) : Color.FromArgb(33, 42, 53));
                e.Graphics.FillPath(brush, path);
                if (active)
                {
                    using var accent = new SolidBrush(Color.FromArgb(59, 155, 231));
                    e.Graphics.FillRectangle(accent, bounds.X + 12, bounds.Bottom - 4, bounds.Width - 24, 3);
                }
                var labelColor = active ? Color.FromArgb(170, 216, 255) : Color.FromArgb(164, 179, 193);
                TextRenderer.DrawText(e.Graphics, _host._pages[i].Title, Font, bounds, labelColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            _host.SelectTab(e.X / TabAreaWidth);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys k = keyData & Keys.KeyCode;
            return k is Keys.Left or Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) _host.SelectTab((_host.SelectedIndex - 1 + _host._pages.Count) % _host._pages.Count);
            else if (e.KeyCode == Keys.Right) _host.SelectTab((_host.SelectedIndex + 1) % _host._pages.Count);
            else { base.OnKeyDown(e); return; }
            e.Handled = true;
        }
    }
}
