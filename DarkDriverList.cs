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
        int reserved = Wide(row.Width, dpi) ? Scale(row.Width >= Scale(700, dpi) ? 280 : 148, dpi) : Scale(13, dpi);
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
                row.Top + Scale(row.Width >= Scale(700, dpi) ? 24 : 43, dpi), width, height)
            : new Rectangle(row.Right - width - Scale(16, dpi),
                row.Bottom - Scale(37, dpi), width, height);
    }

    internal static Rectangle Update(Rectangle row, int dpi, Font font)
    {
        int width = Math.Max(Scale(112, dpi),
            TextRenderer.MeasureText("Atualizar", font,
                Size.Empty, TextFormatFlags.NoPadding).Width + Scale(24, dpi));
        if (!Wide(row.Width, dpi))
            return new Rectangle(row.Left + Scale(58, dpi),
                row.Bottom - Scale(37, dpi), width, Scale(31, dpi));
        if (row.Width < Scale(700, dpi))
            return new Rectangle(row.Right - width - Scale(16, dpi),
                row.Top + Scale(6, dpi), width, Scale(31, dpi));
        int uninstallLeft = Uninstall(row, dpi, font).Left;
        return new Rectangle(uninstallLeft - width - Scale(10, dpi),
            row.Top + Scale(24, dpi), width, Scale(31, dpi));
    }
}

internal enum DriverCardAction { Details, Update, Uninstall }

internal sealed partial class DarkDriverList : Control
{
    private int ContentWidth =>
        Math.Max(1, ClientSize.Width - (_scrollbar.Visible ? _scrollbar.Width : 0));
    private int RowHeight => TableMode ? TableRowHeight : DriverRowGeometry.Height(DeviceDpi, ContentWidth);
    private readonly List<DriverInventoryEntry> _items = new();
    private HashSet<string> _offeredDevices = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _eligibleUpdateDevices = new(StringComparer.OrdinalIgnoreCase);
    private readonly DarkVScrollBar _scrollbar = new();
    private readonly WheelDeltaAccumulator _wheel = new();
    private readonly PixelWheelDeltaAccumulator _verticalWheel = new(), _horizontalWheel = new();
    private readonly Font _titleFont = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _detailsFont = new("Segoe UI", 8.8f);
    private readonly Font _markFont = new("Segoe UI", 10.5f, FontStyle.Bold);
    private readonly Font _actionFont = new("Segoe UI", 9f, FontStyle.Bold);
    private readonly ContextMenuStrip _actionsMenu = new();
    private readonly ToolStripMenuItem _detailsMenu = new("Detalhes");
    private readonly ToolStripMenuItem _updateMenu = new("Atualizar");
    private readonly ToolStripMenuItem _uninstallMenu = new("Desinstalar");
    private readonly ToolStripMenuItem _summaryMenu = new("Copiar resumo sem identificadores") { Visible = false };
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
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool CanCopySummary { get; set; }
    internal event Action? CopySummaryRequested;
    internal bool HasConsultativeOffer(DriverInventoryEntry item) => _offeredDevices.Contains(item.DeviceId);
    internal bool CanUpdate(DriverInventoryEntry item) =>
        _actionsCurrent && _eligibleUpdateDevices.Contains(item.DeviceId);
    internal bool CanUninstall(DriverInventoryEntry item) =>
        _actionsCurrent && _evaluateRemoval?.Invoke(item).Allowed == true;
    // A rota manual abre o Gerenciador de Dispositivos com consentimento.
    // Nunca prometer desinstalação direta quando o gate OEM a proíbe.
    internal static string RemovalActionLabel(bool directEligible) =>
        directEligible ? "Desinstalar" : "Gerenciar";
    internal string UninstallReason(DriverInventoryEntry item) =>
        _evaluateRemoval?.Invoke(item).Reason ?? "Selecione um dispositivo em inventário válido.";

    internal V269Theme Theme = V269Theme.AzulGf;

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
        _updateMenu.Click += (_, _) => RequestMenuAction(DriverCardAction.Update);
        _uninstallMenu.Click += (_, _) => RequestMenuAction(DriverCardAction.Uninstall);
        _actionsMenu.Closed += (_, e) =>
        {
            // O WinForms fecha o menu antes de entregar Click ao item.
            // A seleção é consumida uma única vez em RequestMenuAction.
            if (e.CloseReason != ToolStripDropDownCloseReason.ItemClicked)
                _menuDevice = null;
        };
        _actionsMenu.ShowItemToolTips = false;
        _summaryMenu.Click += (_, _) => CopySummaryRequested?.Invoke();
        _actionsMenu.Items.AddRange([_detailsMenu, _updateMenu, _uninstallMenu, _summaryMenu]);
        Resize += (_, _) => UpdateMetrics();
        VisibleChanged += (_, _) => { if (TableMode) UpdateMetrics(); };
        DpiChangedAfterParent += (_, _) => UpdateMetrics();
    }

    internal void SetActionPolicy(bool current,
        Func<DriverInventoryEntry, DriverSilentUninstallDecision>? removalDecision)
    {
        _actionsCurrent = current;
        _evaluateRemoval = removalDecision;
        if (!current)
        {
            _menuDevice = null;
            if (_actionsMenu.Visible) _actionsMenu.Close();
        }
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
        _offerVersions.Clear();
        _eligibleUpdateDevices.Clear();
        _selectedIndex = -1;
        if (unambiguousBefore)
        {
            int sameReference = _items.FindIndex(item => ReferenceEquals(item, selected));
            if (sameReference >= 0 && _items.Count(item =>
                string.Equals(item.DeviceId, selected!.DeviceId,
                    StringComparison.OrdinalIgnoreCase)) == 1)
                _selectedIndex = sameReference;
        }
        if (_dragIndex >= 0) EndTableDrag();
        if (TableMode && _selectedIndex < 0 && _items.Count > 0) _selectedIndex = 0;
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

    internal void SetEligibleUpdateDevices(IEnumerable<string> deviceIds)
    {
        _eligibleUpdateDevices = deviceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
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
            AccessibleDescription = (TableMode && EmptyMessage.Length > 0 ? EmptyMessage + " " : "") + $"Nenhum dispositivo selecionado; {_items.Count} associações exibidas. Use as setas para escolher.";
            return;
        }
        int sameDeviceId = _items.Count(item => string.Equals(item.DeviceId,
            selected.DeviceId, StringComparison.OrdinalIgnoreCase));
        string ambiguity = sameDeviceId > 1
            ? $"; ATENÇÃO: {sameDeviceId} linhas exibidas com o mesmo DeviceId. Associação de INF/WUA não é inequívoca"
            : "";
        AccessibleDescription = $"Selecionado {_selectedIndex + 1} de {_items.Count}: " +
            DriverHardwareCatalog.DisplayName(selected) +
            "; ID da instância: " + selected.DeviceId +
            "; INF relatado: " + selected.InfName +
            "; classe WMI: " + selected.ReportedDriverClass +
            "; " + selected.PresenceLabel + "; " + selected.PnpStatusLabel + ambiguity +
            (_eligibleUpdateDevices.Contains(selected.DeviceId)
                ? "; ação Atualizar habilitada para revisão de uma oferta WUA exata; instalação exige revalidação, backup e consentimento"
                : _offeredDevices.Contains(selected.DeviceId)
                    ? "; oferta WUA apenas consultiva, sem ação de atualização elegível" : "") +
            ". " + DriverPnpGuidance.Describe(selected.PnpProblemCode) +
            (_actionsCurrent
                ? "; Detalhes: consulta oficial e evidências; " +
                  (CanUninstall(selected)
                    ? "Desinstalar: disponível com confirmação e backup"
                    : "Gerenciar: encaminha ao Gerenciador de Dispositivos do Windows, " +
                      "após confirmação, sem remoção automática pelo GF; " +
                      UninstallReason(selected)) +
                  ". Use Enter para abrir as ações acessíveis."
                : "; ações bloqueadas até consulta válida.");
    }

    private void UpdateMetrics()
    {
        if (_updatingTableMetrics) return;
        _updatingTableMetrics = true;
        try
        {
            if (TableMode)
                V269AppTable.FitScrollBars(this, _scrollbar, _horizontal, MinimumWidth, TableHeaderHeight, _items.Count * RowHeight);
            else _scrollbar.SetMetrics(_items.Count * RowHeight, Math.Max(1, ClientSize.Height));
        }
        finally { _updatingTableMetrics = false; }
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
            else if (bottom > _scrollOffset + RowsHeight)
                _scrollbar.Value = bottom - RowsHeight;
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
        if (TableMode)
        {
            DrawTable(e.Graphics);
            return;
        }
        int width = ContentWidth;
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

    private void DrawCard(Graphics canvas, Rectangle bounds, DriverInventoryEntry item,
        bool selected)
    {
        int dpi = DeviceDpi;
        Rectangle card = DriverRowGeometry.Card(bounds, dpi);
        canvas.SmoothingMode = SmoothingMode.AntiAlias;
        var palette = V269VisualTokens.ForTheme(Theme);
        using (var path = ModernButton.RoundRect(card, 8))
        using (var background = new SolidBrush(selected
            ? palette.Selection : palette.Card))
        using (var outline = new Pen(selected
            ? palette.Accent : palette.Border,
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
        string deviceName = DriverHardwareCatalog.DisplayName(item);
        string initial = string.IsNullOrWhiteSpace(item.DeviceName) && deviceName == DriverHardwareCatalog.UnknownName
            ? "?" : deviceName[0].ToString().ToUpperInvariant();
        TextRenderer.DrawText(canvas, initial, _markFont, emblem, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        Color muted = selected ? Color.White : palette.SecondaryText;
        const TextFormatFlags textFlags = TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(canvas, deviceName,
            _titleFont, DriverRowGeometry.Text(bounds, dpi, 4, 19),
            selected ? Color.White : palette.Text, textFlags);
        TextRenderer.DrawText(canvas,
            "Versão: " + (string.IsNullOrWhiteSpace(item.Version) ? "não informada" : item.Version) +
            "  ·  " + (string.IsNullOrWhiteSpace(item.Provider) ? "Fornecedor não informado" : item.Provider),
            _detailsFont, DriverRowGeometry.Text(bounds, dpi, 25, 16), muted, textFlags);
        TextRenderer.DrawText(canvas,
            "INF: " + (string.IsNullOrWhiteSpace(item.InfName) ? "não informado" : item.InfName) +
            "  ·  Assinatura (Windows): " + item.SignatureLabel,
            _detailsFont, DriverRowGeometry.Text(bounds, dpi, 43, 16), muted, textFlags);
        // Estado não é um botão: ponto discreto, sem pílula ou área de clique.
        // Presente/Indeterminado ficam na acessibilidade; só alertas ganham texto.
        Rectangle status = DriverRowGeometry.Status(bounds, dpi);
        bool offered = _offeredDevices.Contains(item.DeviceId);
        bool eligible = CanUpdate(item);
        Color dot = item.HasPnpProblem ? Color.FromArgb(231, 173, 103)
            : eligible ? Color.FromArgb(133, 221, 176)
            : offered ? Color.FromArgb(114, 196, 255)
            : item.Present == true ? Color.FromArgb(111, 166, 148)
            : Color.FromArgb(123, 135, 149);
        using (var brush = new SolidBrush(dot))
            canvas.FillEllipse(brush, status.Left + Scale(1), status.Top + Scale(4),
                Scale(6), Scale(6));
        string statusText = item.HasPnpProblem ? $"Atenção PnP {item.PnpProblemCode}"
            : eligible ? "Oferta WUA para revisão"
            : offered ? "Oferta WUA consultiva" : "";
        if (statusText.Length > 0)
            TextRenderer.DrawText(canvas, statusText, _detailsFont,
                new Rectangle(status.Left + Scale(12), status.Top,
                    Math.Max(1, status.Width - Scale(12)), status.Height),
                dot, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        DrawAction(canvas, DriverRowGeometry.Update(bounds, dpi, _actionFont),
            "Atualizar", V269ActionKind.Atualizar, eligible);
        DrawAction(canvas, DriverRowGeometry.Uninstall(bounds, dpi, _actionFont),
            "Remover", V269ActionKind.Remover, CanUninstall(item));
    }

    private void DrawAction(Graphics canvas, Rectangle bounds, string text, V269ActionKind kind, bool allowed)
    {
        using var shape = ModernButton.RoundRect(bounds, DriverRowGeometry.Scale(6, DeviceDpi));
        using var brush = new SolidBrush(V269VisualTokens.ActionBackground(Theme, kind, allowed));
        using var border = new Pen(V269VisualTokens.ActionBorder(Theme, kind, allowed));
        var color = Theme == V269Theme.Claro
            ? allowed ? Color.White : V269VisualTokens.ForTheme(Theme).SecondaryText
            : V269VisualTokens.ActionColor(Theme, kind, allowed);
        canvas.FillPath(brush, shape);
        canvas.DrawPath(border, shape);
        TextRenderer.DrawText(canvas, text, _actionFont, bounds, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }

    private (int Index, DriverCardAction? Action) HitTest(Point position)
    {
        if (position.X < 0 || position.X >= ContentWidth || position.Y < TableHeaderHeight ||
            position.Y >= TableHeaderHeight + RowsHeight)
            return (-1, null);
        int index = (position.Y - TableHeaderHeight + _scrollOffset) / RowHeight;
        if (index < 0 || index >= _items.Count) return (-1, null);
        var row = TableMode ? TableRow(index) : new Rectangle(0, index * RowHeight - _scrollOffset, ContentWidth, RowHeight);
        var uninstall = TableMode ? TableAction(row, true) : DriverRowGeometry.Uninstall(row, DeviceDpi, _actionFont);
        if (CanUninstall(_items[index]) && uninstall.Contains(position)) return (index, DriverCardAction.Uninstall);
        var update = TableMode ? TableAction(row, false) : DriverRowGeometry.Update(row, DeviceDpi, _actionFont);
        if (CanUpdate(_items[index]) && update.Contains(position))
            return (index, DriverCardAction.Update);
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
        var item = _menuDevice;
        _menuDevice = null;
        if (item is not null && ReferenceEquals(SelectedItem, item) &&
            (action != DriverCardAction.Update || CanUpdate(item)))
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
        else if (TableMode && TableGrip(TableRow(hit.Index)).Contains(e.Location))
        {
            _dragIndex = hit.Index;
            _dragOrigin = e.Location;
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        if (_dragIndex >= 0)
        {
            var dragSize = SystemInformation.DragSize;
            var deadZone = new Rectangle(_dragOrigin.X - dragSize.Width / 2,
                _dragOrigin.Y - dragSize.Height / 2, dragSize.Width, dragSize.Height);
            _dropIndex = deadZone.Contains(e.Location) || hit.Index == _dragIndex ? -1 : hit.Index;
            Invalidate();
        }
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
        if (e is HandledMouseEventArgs { Handled: true }) return;
        if (TableMode)
        {
            bool horizontal = (ModifierKeys & Keys.Shift) != 0 && _horizontal.MaximumValue > 0;
            var bar = horizontal ? _horizontal : _scrollbar;
            var wheel = horizontal ? _horizontalWheel : _verticalWheel;
            bar.Value = wheel.Move(bar.Value, bar.MaximumValue, e.Delta, horizontal ? Scale(52) : RowHeight * 3);
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
            return;
        }
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
        bool canSummarize = TableMode && CanCopySummary;
        if (item is null && !canSummarize) return;
        _menuDevice = item;
        _detailsMenu.Visible = _updateMenu.Visible = _uninstallMenu.Visible = item is not null;
        _detailsMenu.Enabled = _actionsCurrent;
        _updateMenu.Enabled = item is not null && CanUpdate(item);
        _uninstallMenu.Enabled = _actionsCurrent;
        _summaryMenu.Visible = canSummarize;
        _uninstallMenu.Text = item is not null && CanUninstall(item) ? "Desinstalar" : "Gerenciar no Windows";
        _actionsMenu.Show(this, location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _dragIndex >= 0)
        {
            int from = _dragIndex;
            int to = _dropIndex;
            EndTableDrag();
            if (to >= 0) ReorderRequested?.Invoke(from, to);
            return;
        }
        if (e.Button != MouseButtons.Right) return;
        var hit = HitTest(e.Location);
        if (hit.Index >= 0)
        {
            SelectIndex(hit.Index);
            OpenActionsMenu(e.Location);
        }
        else if (_items.Count == 0 && TableMode && CanCopySummary) OpenActionsMenu(e.Location);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (TableMode && e.Alt && !e.Control && !e.Shift &&
            (e.KeyCode is Keys.Up or Keys.Down) && _selectedIndex >= 0)
        {
            int nextIndex = _selectedIndex + (e.KeyCode == Keys.Up ? -1 : 1);
            if (nextIndex >= 0 && nextIndex < _items.Count)
            {
                ReorderRequested?.Invoke(_selectedIndex, nextIndex);
                SelectIndex(nextIndex);
            }
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode is Keys.Enter or Keys.Apps ||
            e.KeyCode == Keys.F10 && e.Shift)
        {
            if (SelectedItem is not null || TableMode && CanCopySummary)
            {
                int y = TableHeaderHeight + Math.Max(0, _selectedIndex) * RowHeight - _scrollOffset +
                    DriverRowGeometry.Scale(20, DeviceDpi);
                OpenActionsMenu(new Point(DriverRowGeometry.Scale(60, DeviceDpi),
                    Math.Clamp(y, 0, Math.Max(0, Height - 1))));
            }
            e.Handled = true;
            return;
        }
        int page = Math.Max(1, RowsHeight / RowHeight);
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
            if (!TableMode) _horizontal.Dispose();
            _titleFont.Dispose();
            _detailsFont.Dispose();
            _markFont.Dispose();
            _actionFont.Dispose();
        }
        base.Dispose(disposing);
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
        var present = items.Where(x => x.Present == true).ToArray();
        int genericWindows = present.Count(x => DriverHardwareCatalog.IsGenericName(x.DeviceName));
        int genericGf = present.Count(x =>
            DriverHardwareCatalog.IsGenericName(DriverHardwareCatalog.DisplayName(x)));
        return string.Join(Environment.NewLine,
            "GERENCIADOR FINAL — DRIVERS — RESUMO SEM IDENTIFICADORES",
            "Somente consulta local; não comprova atualização disponível.",
            "Inventário parcial: " + (inventory.Partial ? "sim" : "não"),
            "Dispositivos/associações observados: " + items.Count,
            $"Associações presentes: {present.Length}; nomes genéricos no Windows: {genericWindows}; no GF: {genericGf}.",
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

// Uma oferta consultiva não basta para mostrar Atualizar. A ação só abre
// o fluxo G3.2 existente, que revalida e exige backup/consentimento no clique.
// A versão superior não é inferida de título ou data da oferta.
internal static class DriverUpdateEligibility
{
    internal static IReadOnlySet<string> Find(
        DriverInventoryResult inventory, DriverOfficialResult official)
    {
        var eligible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (inventory.Partial || official.Partial ||
            official.SearchContext is not { Source: "Windows Update", Online: true })
            return eligible;
        foreach (var group in official.Matches.Where(
            match => match.Kind == DriverOfficialMatchKind.PrimaryHardwareId).GroupBy(
            match => match.DeviceId, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(group.Key) || group.Count() != 1) continue;
            var match = group.Single();
            if (!DriverUpdatePilot.TryPrepare(inventory, official, match,
                out var choice) || choice is null)
                continue;
            var device = inventory.Items.SingleOrDefault(item =>
                string.Equals(item.DeviceId, group.Key,
                    StringComparison.OrdinalIgnoreCase));
            if (device is not null && DriverG32RecoveryTicket.CanOffer(
                inventory, device, choice))
                eligible.Add(group.Key);
        }
        return eligible;
    }
}
