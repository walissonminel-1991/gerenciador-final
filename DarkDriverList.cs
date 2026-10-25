using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// O mesmo cartao virtualizado da lista de Aplicativos, mas SEM acoes de
// instalacao/desinstalacao. Desenha apenas linhas visiveis mesmo com milhares
// de associacoes, evitando centenas de controles no computador fraco.
internal sealed class DarkDriverList : Control
{
    private const int RowHeight = 73;
    private readonly List<DriverInventoryEntry> _items = new();
    private readonly DarkVScrollBar _scrollbar = new();
    private readonly ToolTip _tips = new() { InitialDelay = 450, AutoPopDelay = 12000 };
    private readonly Font _titleFont = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _detailsFont = new("Segoe UI", 8.8f);
    private readonly Font _markFont = new("Segoe UI", 10.5f, FontStyle.Bold);
    private int _selectedIndex = -1;
    private int _scrollOffset;
    private int _tipIndex = -1;

    internal IReadOnlyList<DriverInventoryEntry> Items => _items;
    internal int SelectedIndex => _selectedIndex;
    internal DriverInventoryEntry? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    internal DarkDriverList()
    {
        Name = "DriverInventoryList";
        BackColor = Color.FromArgb(30, 39, 50);
        ForeColor = Color.FromArgb(237, 243, 250);
        TabStop = true;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);
        _scrollbar.Dock = DockStyle.Right;
        _scrollbar.ValueChanged += (_, _) =>
        {
            _scrollOffset = _scrollbar.Value;
            Invalidate();
        };
        Controls.Add(_scrollbar);
        Resize += (_, _) => UpdateMetrics();
    }

    internal void SetItems(IEnumerable<DriverInventoryEntry> items)
    {
        DriverInventoryEntry? selected = SelectedItem;
        _items.Clear();
        _items.AddRange(items);
        _selectedIndex = selected is null ? -1 : _items.FindIndex(item => item == selected);
        _tipIndex = -1;
        _tips.SetToolTip(this, "");
        UpdateMetrics();
    }

    internal void ScrollToTop() => _scrollbar.Value = 0;

    private void UpdateMetrics()
    {
        _scrollbar.SetMetrics(_items.Count * RowHeight, Math.Max(1, ClientSize.Height));
        _scrollOffset = _scrollbar.Value;
        Invalidate();
    }

    private void SelectIndex(int index)
    {
        _selectedIndex = index >= 0 && index < _items.Count ? index : -1;
        if (_selectedIndex >= 0)
        {
            int top = _selectedIndex * RowHeight;
            int bottom = top + RowHeight;
            if (top < _scrollOffset) _scrollbar.Value = top;
            else if (bottom > _scrollOffset + ClientSize.Height)
                _scrollbar.Value = bottom - ClientSize.Height;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (ClientSize.Height < 1) return;
        int width = Math.Max(1, ClientSize.Width - (_scrollbar.Visible ? _scrollbar.Width : 0));
        using Region previousClip = e.Graphics.Clip;
        e.Graphics.SetClip(new Rectangle(0, 0, width, ClientSize.Height));
        try
        {
            int first = Math.Max(0, _scrollOffset / RowHeight);
            int last = Math.Min(_items.Count - 1, (_scrollOffset + ClientSize.Height) / RowHeight);
            for (int index = first; index <= last; index++)
                DrawCard(e.Graphics, new Rectangle(0, index * RowHeight - _scrollOffset,
                    width, RowHeight), _items[index], index == _selectedIndex);
        }
        finally { e.Graphics.Clip = previousClip; }
    }

    private void DrawCard(Graphics canvas, Rectangle bounds, DriverInventoryEntry item, bool selected)
    {
        Rectangle card = new(bounds.Left + 4, bounds.Top + 3,
            Math.Max(1, bounds.Width - 9), bounds.Height - 6);
        canvas.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = ModernButton.RoundRect(card, 8))
        using (var background = new SolidBrush(selected
            ? Color.FromArgb(42, 66, 92) : Color.FromArgb(37, 48, 61)))
        using (var outline = new Pen(selected
            ? Color.FromArgb(111, 184, 255) : Color.FromArgb(61, 77, 93),
            selected ? 1.8f : 0.9f))
        {
            canvas.FillPath(background, path);
            canvas.DrawPath(outline, path);
        }
        if (selected)
        {
            using var accent = new SolidBrush(AppServices.Blue);
            canvas.FillRectangle(accent, card.Left + 1, card.Top + 12, 4, card.Height - 24);
        }

        Rectangle emblem = new(bounds.Left + 18, bounds.Top + 21, 31, 31);
        using (var emblemBrush = new SolidBrush(Color.FromArgb(54, 67, 81)))
            canvas.FillEllipse(emblemBrush, emblem);
        string initial = string.IsNullOrWhiteSpace(item.DeviceName)
            ? "?" : item.DeviceName.Trim()[0].ToString().ToUpperInvariant();
        TextRenderer.DrawText(canvas, initial, _markFont, emblem, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix);

        int left = bounds.Left + 62;
        bool showStatus = bounds.Width >= 570;
        int statusWidth = showStatus ? 142 : 0;
        int available = Math.Max(20, bounds.Right - left - statusWidth - 14);
        Color muted = Color.FromArgb(167, 181, 196);
        const TextFormatFlags single = TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
            TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(canvas,
            string.IsNullOrWhiteSpace(item.DeviceName) ? "Dispositivo não identificado" : item.DeviceName,
            _titleFont, new Rectangle(left, bounds.Top + 5, available, 22),
            selected ? Color.FromArgb(111, 184, 255) : ForeColor, single);
        string provider = string.IsNullOrWhiteSpace(item.Provider)
            ? "Fornecedor não informado" : item.Provider;
        string version = string.IsNullOrWhiteSpace(item.Version)
            ? "não informada" : item.Version;
        TextRenderer.DrawText(canvas, "Versão: " + version + "  ·  " + provider,
            _detailsFont, new Rectangle(left, bounds.Top + 28, available, 19),
            muted, single);
        string inf = string.IsNullOrWhiteSpace(item.InfName)
            ? "não informado" : item.InfName;
        string second = "INF: " + inf + "  ·  Assinatura (Windows): " + item.SignatureLabel;
        if (!showStatus) second += "  ·  " + item.PresenceLabel;
        TextRenderer.DrawText(canvas, second, _detailsFont,
            new Rectangle(left, bounds.Top + 46, available, 19), muted, single);

        if (showStatus)
        {
            Rectangle pill = new(bounds.Right - 138, bounds.Top + 22, 123, 26);
            Color fill = item.Present switch
            {
                true => Color.FromArgb(44, 95, 82),
                false => Color.FromArgb(103, 69, 52),
                _ => Color.FromArgb(59, 72, 88)
            };
            using var shape = ModernButton.RoundRect(pill, 7);
            using var brush = new SolidBrush(fill);
            canvas.FillPath(brush, shape);
            TextRenderer.DrawText(canvas, item.PresenceLabel, _detailsFont, pill,
                ForeColor, TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int contentWidth = ClientSize.Width - (_scrollbar.Visible ? _scrollbar.Width : 0);
        if (e.Button != MouseButtons.Left || e.X >= contentWidth || e.Y < 0) return;
        Focus();
        SelectIndex((e.Y + _scrollOffset) / RowHeight);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int contentWidth = ClientSize.Width - (_scrollbar.Visible ? _scrollbar.Width : 0);
        int index = e.X >= 0 && e.X < contentWidth && e.Y >= 0
            ? (e.Y + _scrollOffset) / RowHeight : -1;
        if (index == _tipIndex) return;
        _tipIndex = index;
        if (index < 0 || index >= _items.Count)
        {
            _tips.SetToolTip(this, "");
            return;
        }
        var item = _items[index];
        _tips.SetToolTip(this, string.Join(Environment.NewLine,
            string.IsNullOrWhiteSpace(item.DeviceName) ? "Nome nao fornecido pelo Windows" : item.DeviceName,
            "ID do dispositivo (consulta local): " + item.DeviceId,
            "Fornecedor: " + item.Provider,
            "Versão informada pelo Windows: " + item.Version,
            "INF: " + item.InfName,
            "Presença: " + item.PresenceLabel,
            item.PackageEvidence?.Label ?? "Vínculo com Driver Store não consultado",
            "INF publicado (consulta local): " + item.PackageEvidence?.PublishedPath,
            "INF no Driver Store (consulta local): " + item.PackageEvidence?.StorePath,
            "Assinatura informada pelo Windows: " + item.SignatureLabel,
            "Vínculos no histórico Windows: " + item.WindowsEvents.Count + ". Investigar mostra os detalhes.",
            "A versão é o texto relatado pelo WMI, sem correção ou comparação entre fabricantes.",
            "Dados ausentes ou versões diferentes não comprovam incompatibilidade ou obsolescência.",
            "Somente consulta. Nenhuma instalação ou remoção de drivers."));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _tipIndex = -1;
        _tips.SetToolTip(this, "");
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _scrollbar.Value -= Math.Sign(e.Delta) * RowHeight * 3;
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or
            Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int page = Math.Max(1, ClientSize.Height / RowHeight);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tips.Dispose();
            _titleFont.Dispose();
            _detailsFont.Dispose();
            _markFont.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal static class DriverListFilters
{
    // Apenas uma visao dos dados WMI. Nunca decide ausencia, obsolescencia ou
    // autorizacao de limpeza/atualizacao com base nesses filtros.
    internal static DriverInventoryEntry[] Apply(
        IEnumerable<DriverInventoryEntry> items, string filter, string query)
    {
        IEnumerable<DriverInventoryEntry> filtered = filter switch
        {
            "Presentes" => items.Where(item => item.Present == true),
            "Não confirmados" => items.Where(item => item.Present != true),
            _ => items
        };
        query = query.Trim();
        if (query.Length > 0)
            filtered = filtered.Where(item =>
                string.Join(" ", item.DeviceName, item.DeviceId, item.Provider, item.Version,
                    item.InfName, item.PresenceLabel, item.SignatureLabel)
                .Contains(query, StringComparison.CurrentCultureIgnoreCase));
        return filtered.ToArray();
    }
}
