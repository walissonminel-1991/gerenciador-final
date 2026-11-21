using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// A geometria também serve ao hit-test: desenhar e clicar usam os mesmos retângulos.
// A lista permanece virtualizada; nenhum Button é criado por dispositivo.
internal static class DriverRowGeometry
{
    internal static int Scale(int pixels, int dpi) =>
        (int)Math.Ceiling(pixels * Math.Max(1d, dpi / 96d));

    // Igual à densidade visual de Aplicativos: uma ação por linha, à direita.
    internal static int Height(int dpi) => Scale(82, dpi);
    internal static int Height(int dpi, int width) =>
        width < Scale(440, dpi) ? Scale(114, dpi) : Height(dpi);

    internal static bool Wide(int width, int dpi) => width >= Scale(440, dpi);

    internal static Rectangle Card(Rectangle row, int dpi) =>
        new(row.Left + Scale(4, dpi), row.Top + Scale(3, dpi),
            Math.Max(1, row.Width - Scale(9, dpi)),
            Math.Max(1, row.Height - Scale(6, dpi)));

    internal static Rectangle Emblem(Rectangle row, int dpi) =>
        new(row.Left + Scale(18, dpi), row.Top + Scale(23, dpi),
            Scale(31, dpi), Scale(31, dpi));

    internal static Rectangle Text(Rectangle row, int dpi, int top, int height)
    {
        int left = row.Left + Scale(58, dpi);
        int reserved = Wide(row.Width, dpi) ? Scale(148, dpi) : Scale(13, dpi);
        return new Rectangle(left, row.Top + Scale(top, dpi),
            Math.Max(1, row.Right - left - reserved), Scale(height, dpi));
    }

    internal static Rectangle Status(Rectangle row, int dpi) =>
        new(row.Left + Scale(58, dpi), row.Top + Scale(61, dpi),
            Math.Max(1, Math.Min(Scale(140, dpi),
                row.Width - Scale(80, dpi) - (Wide(row.Width, dpi) ? Scale(140, dpi) : 0))),
            Scale(14, dpi));

    internal static Rectangle Uninstall(Rectangle row, int dpi, Font font)
    {
        int width = Math.Max(Scale(112, dpi),
            TextRenderer.MeasureText("Desinstalar", font,
                Size.Empty, TextFormatFlags.NoPadding).Width + Scale(24, dpi));
        int height = Scale(31, dpi);
        return Wide(row.Width, dpi)
            ? new Rectangle(row.Right - width - Scale(16, dpi),
                row.Top + Scale(24, dpi), width, height)
            : new Rectangle(row.Left + Scale(58, dpi),
                row.Bottom - Scale(37, dpi), width, height);
    }
}

internal enum DriverCardAction { Details, Uninstall }

internal sealed class DarkDriverList : Control
{
    private int ContentWidth =>
        Math.Max(1, ClientSize.Width - (_scrollbar.Visible ? _scrollbar.Width : 0));
    private int RowHeight => DriverRowGeometry.Height(DeviceDpi, ContentWidth);
    private readonly List<DriverInventoryEntry> _items = new();
    private HashSet<string> _offeredDevices = new(StringComparer.OrdinalIgnoreCase);
    private readonly DarkVScrollBar _scrollbar = new();
    private readonly WheelDeltaAccumulator _wheel = new();
    private readonly Font _titleFont = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _detailsFont = new("Segoe UI", 8.8f);
    private readonly Font _markFont = new("Segoe UI", 10.5f, FontStyle.Bold);
    private readonly Font _actionFont = new("Segoe UI", 9f, FontStyle.Bold);
    private readonly ContextMenuStrip _actionsMenu = new();
    private readonly ToolStripMenuItem _detailsMenu = new("Detalhes");
    private readonly ToolStripMenuItem _uninstallMenu = new("Desinstalar");
    private int _selectedIndex = -1;
    private int _scrollOffset;
    private int _tipIndex = -1;
    private DriverCardAction? _tipAction;
    private DriverInventoryEntry? _menuDevice;
    private bool _actionsCurrent;
    private Func<DriverInventoryEntry, DriverSilentUninstallDecision>? _evaluateRemoval;

    internal event EventHandler? SelectionChanged;
    internal event Action<DriverInventoryEntry, DriverCardAction>? ActionRequested;
    internal IReadOnlyList<DriverInventoryEntry> Items => _items;
    internal int SelectedIndex => _selectedIndex;
    internal DriverInventoryEntry? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;
    internal bool ActionsCurrent => _actionsCurrent;
    internal bool CanUninstall(DriverInventoryEntry item) =>
        _actionsCurrent && _evaluateRemoval?.Invoke(item).Allowed == true;
    internal string UninstallReason(DriverInventoryEntry item) =>
        _evaluateRemoval?.Invoke(item).Reason ?? "Selecione um dispositivo em inventário válido.";

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
            _tipIndex = -1;
            _tipAction = null;
            
            Invalidate();
        };
        Controls.Add(_scrollbar);
        _detailsMenu.Click += (_, _) => RequestMenuAction(DriverCardAction.Details);
        _uninstallMenu.Click += (_, _) => RequestMenuAction(DriverCardAction.Uninstall);
        _actionsMenu.Closed += (_, _) => _menuDevice = null;
        _actionsMenu.Items.AddRange([_detailsMenu, _uninstallMenu]);
        Resize += (_, _) => UpdateMetrics();
        DpiChangedAfterParent += (_, _) => UpdateMetrics();
    }

    internal void SetActionPolicy(bool current,
        Func<DriverInventoryEntry, DriverSilentUninstallDecision>? removalDecision)
    {
        _actionsCurrent = current;
        _evaluateRemoval = removalDecision;
        if (!current && _actionsMenu.Visible) _actionsMenu.Close();
        UpdateAccessibleSelection();
        Invalidate();
    }

    internal void SetItems(IEnumerable<DriverInventoryEntry> items)
    {
        DriverInventoryEntry? selected = SelectedItem;
        bool unambiguousBefore = selected is not null &&
            !string.IsNullOrWhiteSpace(selected.DeviceId) &&
            _items.Count(item => string.Equals(item.DeviceId, selected.DeviceId,
                StringComparison.OrdinalIgnoreCase)) == 1;
        _items.Clear();
        _items.AddRange(items);
        _offeredDevices.Clear();
        _selectedIndex = -1;
        if (unambiguousBefore)
        {
            int sameReference = _items.FindIndex(item => ReferenceEquals(item, selected));
            if (sameReference >= 0 && _items.Count(item =>
                string.Equals(item.DeviceId, selected!.DeviceId,
                    StringComparison.OrdinalIgnoreCase)) == 1)
                _selectedIndex = sameReference;
        }
        _tipIndex = -1;
        _tipAction = null;
        _menuDevice = null;
        if (_actionsMenu.Visible) _actionsMenu.Close();
        
        UpdateMetrics();
        UpdateAccessibleSelection();
        if (!ReferenceEquals(selected, SelectedItem))
            SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void SetOfferDevices(IEnumerable<string> deviceIds)
    {
        _offeredDevices = deviceIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        UpdateAccessibleSelection();
        Invalidate();
    }

    internal void ScrollToTop() => _scrollbar.Value = 0;

    private void UpdateAccessibleSelection()
    {
        var selected = SelectedItem;
        if (selected is null)
        {
            AccessibleDescription = $"Nenhum dispositivo selecionado; {_items.Count} associações exibidas. Use as setas para escolher.";
            return;
        }
        int sameDeviceId = _items.Count(item => string.Equals(item.DeviceId,
            selected.DeviceId, StringComparison.OrdinalIgnoreCase));
        string ambiguity = sameDeviceId > 1
            ? $"; ATENÇÃO: {sameDeviceId} linhas exibidas com o mesmo DeviceId. Associação de INF/WUA não é inequívoca"
            : "";
        AccessibleDescription = $"Selecionado {_selectedIndex + 1} de {_items.Count}: " +
            (string.IsNullOrWhiteSpace(selected.DeviceName) ? "Dispositivo não identificado" : selected.DeviceName) +
            "; ID da instância: " + selected.DeviceId +
            "; INF relatado: " + selected.InfName +
            "; classe WMI: " + selected.ReportedDriverClass +
            "; " + selected.PnpStatusLabel + ambiguity +
            (_offeredDevices.Contains(selected.DeviceId)
                ? "; oferta oficial WUA consultiva identificada, sem prova de versão superior" : "") +
            ". " + DriverPnpGuidance.Describe(selected.PnpProblemCode) +
            (_actionsCurrent
                ? "; Detalhes: consulta oficial e evidências; Desinstalar: " +
                  (CanUninstall(selected)
                    ? "disponível com confirmação e backup"
                    : "gerenciamento manual no Windows; " + UninstallReason(selected)) +
                  ". Use Enter para abrir as ações acessíveis."
                : "; ações bloqueadas até consulta válida; pressione Tab para Investigar.");
    }

    private void UpdateMetrics()
    {
        _scrollbar.SetMetrics(_items.Count * RowHeight, Math.Max(1, ClientSize.Height));
        _scrollOffset = _scrollbar.Value;
        Invalidate();
    }

    private void SelectIndex(int index)
    {
        int previous = _selectedIndex;
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
        UpdateAccessibleSelection();
        if (_selectedIndex != previous)
            SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (ClientSize.Height < 1) return;
        int width = ContentWidth;
        using Region previousClip = e.Graphics.Clip;
        e.Graphics.SetClip(new Rectangle(0, 0, width, ClientSize.Height));
        try
        {
            int first = Math.Max(0, _scrollOffset / RowHeight);
            int last = Math.Min(_items.Count - 1, (_scrollOffset + ClientSize.Height) / RowHeight);
            for (int index = first; index <= last; index++)
                DrawCard(e.Graphics, new Rectangle(0, index * RowHeight - _scrollOffset,
                    width, RowHeight), _items[index], index == _selectedIndex,
                    index == _tipIndex ? _tipAction : null);
        }
        finally { e.Graphics.Clip = previousClip; }
    }

    private void DrawCard(Graphics canvas, Rectangle bounds, DriverInventoryEntry item,
        bool selected, DriverCardAction? hover)
    {
        int dpi = DeviceDpi;
        Rectangle card = DriverRowGeometry.Card(bounds, dpi);
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
            canvas.FillRectangle(accent, card.Left + 1, card.Top + Scale(12), Scale(4), card.Height - Scale(24));
        }
        int Scale(int pixels) => DriverRowGeometry.Scale(pixels, dpi);
        Rectangle emblem = DriverRowGeometry.Emblem(bounds, dpi);
        using (var emblemBrush = new SolidBrush(Color.FromArgb(54, 67, 81)))
            canvas.FillEllipse(emblemBrush, emblem);
        string initial = string.IsNullOrWhiteSpace(item.DeviceName)
            ? "?" : item.DeviceName.Trim()[0].ToString().ToUpperInvariant();
        TextRenderer.DrawText(canvas, initial, _markFont, emblem, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        Color muted = Color.FromArgb(167, 181, 196);
        const TextFormatFlags textFlags = TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(canvas,
            string.IsNullOrWhiteSpace(item.DeviceName) ? "Dispositivo não identificado" : item.DeviceName,
            _titleFont, DriverRowGeometry.Text(bounds, dpi, 4, 19),
            selected ? Color.FromArgb(111, 184, 255) : ForeColor, textFlags);
        TextRenderer.DrawText(canvas,
            "Versão: " + (string.IsNullOrWhiteSpace(item.Version) ? "não informada" : item.Version) +
            "  ·  " + (string.IsNullOrWhiteSpace(item.Provider) ? "Fornecedor não informado" : item.Provider),
            _detailsFont, DriverRowGeometry.Text(bounds, dpi, 25, 16), muted, textFlags);
        TextRenderer.DrawText(canvas,
            "INF: " + (string.IsNullOrWhiteSpace(item.InfName) ? "não informado" : item.InfName) +
            "  ·  Assinatura (Windows): " + item.SignatureLabel,
            _detailsFont, DriverRowGeometry.Text(bounds, dpi, 43, 16), muted, textFlags);
        Rectangle pill = DriverRowGeometry.Status(bounds, dpi);
        bool offered = _offeredDevices.Contains(item.DeviceId);
        Color fill = item.HasPnpProblem ? Color.FromArgb(121, 80, 43)
            : offered ? Color.FromArgb(27, 87, 119) : item.Present switch
        {
            true => Color.FromArgb(44, 95, 82),
            false => Color.FromArgb(103, 69, 52),
            _ => Color.FromArgb(59, 72, 88)
        };
        using (var shape = ModernButton.RoundRect(pill, 7))
        using (var brush = new SolidBrush(fill))
        {
            canvas.FillPath(brush, shape);
            TextRenderer.DrawText(canvas, item.HasPnpProblem
                ? $"Problema PnP {item.PnpProblemCode}"
                : offered ? "Oferta WUA" : item.PresenceLabel,
                _detailsFont, pill, ForeColor, TextFormatFlags.NoPrefix |
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }

        Rectangle uninstall = DriverRowGeometry.Uninstall(bounds, dpi, _actionFont);
        // A ação aparece para todos. O backend OEM continua protegido:
        // casos não elegíveis seguem ao gerenciamento manual do Windows.
        DrawAction(canvas, uninstall, "Desinstalar", _actionsCurrent,
            hover == DriverCardAction.Uninstall, CanUninstall(item));
    }

    private void DrawAction(Graphics canvas, Rectangle bounds, string text,
        bool enabled, bool hovered, bool destructive)
    {
        if (!enabled)
        {
            TextRenderer.DrawText(canvas, text, _detailsFont, bounds,
                Color.FromArgb(155, 164, 173), TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            return;
        }
        Color bg = destructive ? hovered ? Color.FromArgb(192, 72, 83) : AppServices.Red
            : Color.FromArgb(47, 65, 84);
        Color fg = Color.White;
        using var shape = ModernButton.RoundRect(bounds, DriverRowGeometry.Scale(6, DeviceDpi));
        using var brush = new SolidBrush(bg);
        using var border = new Pen(enabled ? Color.FromArgb(99, 133, 163) : Color.FromArgb(68, 77, 85));
        canvas.FillPath(brush, shape);
        canvas.DrawPath(border, shape);
        TextRenderer.DrawText(canvas, text, _actionFont, bounds, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }

    private (int Index, DriverCardAction? Action) HitTest(Point position)
    {
        if (position.X < 0 || position.X >= ContentWidth || position.Y < 0)
            return (-1, null);
        int index = (position.Y + _scrollOffset) / RowHeight;
        if (index < 0 || index >= _items.Count) return (-1, null);
        var row = new Rectangle(0, index * RowHeight - _scrollOffset, ContentWidth, RowHeight);
        var uninstall = DriverRowGeometry.Uninstall(row, DeviceDpi, _actionFont);
        if (uninstall.Contains(position)) return (index, DriverCardAction.Uninstall);
        return (index, null);
    }

    private void RequestSelected(DriverCardAction action)
    {
        var item = SelectedItem;
        if (item is null || !_actionsCurrent) return;
        // O pai revalida snapshot, geração e referência antes de qualquer efeito.
        ActionRequested?.Invoke(item, action);
    }

    private void RequestMenuAction(DriverCardAction action)
    {
        // Menu aberto para dispositivo anterior não pode agir em nova seleção.
        if (_menuDevice is not null && ReferenceEquals(SelectedItem, _menuDevice))
            RequestSelected(action);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        var hit = HitTest(e.Location);
        if (hit.Index < 0) return;
        Focus();
        SelectIndex(hit.Index);
        if (hit.Action is { } action) RequestSelected(action);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        if (hit.Index == _tipIndex && hit.Action == _tipAction) return;
        _tipIndex = hit.Index;
        _tipAction = hit.Action;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _tipIndex = -1;
        _tipAction = null;
        
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int notches = _wheel.Consume(e.Delta);
        if (notches != 0)
            _scrollbar.Value = (int)Math.Clamp((long)_scrollbar.Value - (long)notches * RowHeight * 3,
                0, _scrollbar.MaximumValue);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or
            Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

    private void OpenActionsMenu(Point location)
    {
        var item = SelectedItem;
        if (item is null) return;
        _menuDevice = item;
        _detailsMenu.Enabled = _actionsCurrent;
        _uninstallMenu.Enabled = _actionsCurrent;
        _detailsMenu.ToolTipText = "";
        _uninstallMenu.ToolTipText = "";
        _actionsMenu.Show(this, location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Right) return;
        var hit = HitTest(e.Location);
        if (hit.Index >= 0)
        {
            SelectIndex(hit.Index);
            OpenActionsMenu(e.Location);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Apps ||
            e.KeyCode == Keys.F10 && e.Shift)
        {
            if (SelectedItem is not null)
            {
                int y = _selectedIndex * RowHeight - _scrollOffset +
                    DriverRowGeometry.Scale(20, DeviceDpi);
                OpenActionsMenu(new Point(DriverRowGeometry.Scale(60, DeviceDpi),
                    Math.Clamp(y, 0, Math.Max(0, Height - 1))));
            }
            e.Handled = true;
            return;
        }
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
            _actionsMenu.Dispose();
            _titleFont.Dispose();
            _detailsFont.Dispose();
            _markFont.Dispose();
            _actionFont.Dispose();
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
            "Problemas" => items.Where(item => item.HasPnpProblem),
            "Sem driver" => items.Where(item => item.WithoutSignedDriverAssociation),
            _ => items
        };
        query = query.Trim();
        if (query.Length > 0)
            filtered = filtered.Where(item =>
                string.Join(" ", item.DeviceName, item.DeviceId, item.Provider, item.Version,
                    item.InfName, item.PresenceLabel, item.SignatureLabel, item.PnpStatusLabel)
                .Contains(query, StringComparison.CurrentCultureIgnoreCase));
        return filtered.ToArray();
    }
}

// Resumo para compartilhar as contagens do PC FRACO sem dados de hardware.
// Não incluir Status WMI, consultas, IDs, nomes, fornecedor, INF ou caminhos.
internal static class DriverTriageSummary
{
    internal static string Format(DriverInventoryResult inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var items = inventory.Items;
        return string.Join(Environment.NewLine,
            "GERENCIADOR FINAL — DRIVERS — RESUMO SEM IDENTIFICADORES",
            "Somente consulta local; não comprova atualização disponível.",
            "Inventário parcial: " + (inventory.Partial ? "sim" : "não"),
            "Dispositivos/associações observados: " + items.Count,
            "Falhas PnP relatadas pelo Windows: " + items.Count(x => x.HasPnpProblem),
            "Sem linha de driver assinado associada no WMI: " +
                items.Count(x => x.WithoutSignedDriverAssociation),
            "Presença não confirmada: " + items.Count(x => x.Present != true),
            "Assinatura WMI indeterminada: " + items.Count(x => x.Signed is null),
            "As categorias podem se sobrepor. Ausência no WMI não comprova ausência de driver.",
            "Sem DeviceId, HardwareID, nomes, fornecedores, INF ou caminhos.",
            "Nenhum driver foi instalado, baixado ou removido por este resumo.");
    }
}
