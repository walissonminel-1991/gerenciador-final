using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal enum V269DriverGlyph
{
    Chip, Monitor, Audio, Bluetooth, Ethernet, WiFi, Storage, Usb, Headset, Gamepad, Plug, Shield
}

internal static class V269DriverVisuals
{
    internal static string Category(DriverInventoryEntry item) =>
        DriverHardwareSupport.Label(DriverHardwareSupport.CategoryOf(item));

    internal static V269DriverGlyph Glyph(DriverInventoryEntry item)
    {
        var name = DriverHardwareCatalog.DisplayName(item);
        string pnp = item.ReportedPnpClass.ToUpperInvariant();
        if (pnp == "BLUETOOTH" || name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
            return V269DriverGlyph.Bluetooth;
        if (pnp == "MONITOR") return V269DriverGlyph.Monitor;
        if (pnp == "USB") return V269DriverGlyph.Usb;
        if (pnp == "SECURITYDEVICES") return V269DriverGlyph.Shield;
        return DriverHardwareSupport.CategoryOf(item) switch
        {
            DriverHardwareCategory.Graphics => V269DriverGlyph.Monitor,
            DriverHardwareCategory.Audio => name.Contains("Headset", StringComparison.OrdinalIgnoreCase)
                ? V269DriverGlyph.Headset : V269DriverGlyph.Audio,
            DriverHardwareCategory.Network => name.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("WLAN", StringComparison.OrdinalIgnoreCase)
                    ? V269DriverGlyph.WiFi : V269DriverGlyph.Ethernet,
            DriverHardwareCategory.Storage => V269DriverGlyph.Storage,
            DriverHardwareCategory.GameInput => V269DriverGlyph.Gamepad,
            DriverHardwareCategory.Peripherals => V269DriverGlyph.Plug,
            DriverHardwareCategory.Firmware => V269DriverGlyph.Shield,
            _ => V269DriverGlyph.Chip
        };
    }

    internal static string Enumerator(DriverInventoryEntry item)
    {
        int separator = item.DeviceId.IndexOf('\\');
        // O prefixo do ID é o enumerador PnP. PCI não revela geração ou largura do link.
        return separator > 0 ? item.DeviceId[..separator] + " (PnP)" : "Não informado";
    }

    internal static void DrawGlyph(Graphics graphics, Rectangle bounds, V269DriverGlyph glyph, Color ink)
    {
        if (glyph is V269DriverGlyph.Chip or V269DriverGlyph.Monitor)
        {
            V269HomeHeadingIcon.DrawGlyph(graphics, bounds,
                glyph == V269DriverGlyph.Chip ? V269Page.Drivers : V269Page.Sistema, ink);
            return;
        }

        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
            using var pen = new Pen(ink, 2.1f)
            {
                StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
            };
            void Line(params PointF[] points) => graphics.DrawLines(pen, points);
            void Box(int x, int y, int width, int height, int radius)
            {
                using var path = V269Shell.Rounded(new Rectangle(x, y, width, height), radius);
                graphics.DrawPath(pen, path);
            }

            switch (glyph)
            {
                case V269DriverGlyph.Bluetooth:
                    Line(new(7, 7.5f), new(17, 16), new(12, 21), new(12, 3), new(17, 8), new(7, 16.5f));
                    break;
                case V269DriverGlyph.Ethernet:
                    Box(4, 3, 16, 10, 2);
                    for (int x = 7; x <= 15; x += 4) Line(new(x, 7), new(x + 2, 7));
                    Box(5, 17, 14, 4, 1);
                    for (int x = 8; x <= 16; x += 2) Line(new(x, 13), new(x, 17));
                    break;
                case V269DriverGlyph.WiFi:
                    graphics.DrawArc(pen, -4, 4, 32, 32, 232, 76);
                    graphics.DrawArc(pen, 1, 9, 22, 22, 231, 78);
                    graphics.DrawArc(pen, 6, 14, 12, 12, 228, 84);
                    Line(new(12, 20), new(12, 20.1f));
                    break;
                case V269DriverGlyph.Storage:
                    Box(2, 5, 20, 14, 2);
                    Line(new(4, 15), new(20, 15));
                    Line(new(16, 17), new(18, 17));
                    break;
                case V269DriverGlyph.Usb:
                    Line(new(12, 22), new(12, 3), new(9, 6));
                    Line(new(12, 3), new(15, 6));
                    Line(new(12, 12), new(7, 12), new(7, 9), new(4, 9), new(4, 15), new(7, 15), new(7, 12));
                    Line(new(12, 17), new(17, 17), new(17, 14), new(20, 14), new(20, 20), new(17, 20), new(17, 17));
                    break;
                case V269DriverGlyph.Audio:
                    Line(new(4, 9), new(4, 15), new(8, 15), new(13, 19), new(13, 5), new(8, 9), new(4, 9));
                    graphics.DrawArc(pen, 10, 6, 12, 12, -42, 84);
                    graphics.DrawArc(pen, 3, 2, 20, 20, -45, 90);
                    break;
                case V269DriverGlyph.Headset:
                    graphics.DrawArc(pen, 3, 2, 18, 18, 180, 180);
                    Box(3, 12, 4, 8, 2);
                    Box(17, 12, 4, 8, 2);
                    graphics.DrawBezier(pen, 17, 20, 16, 22, 14, 23, 10, 23);
                    break;
                case V269DriverGlyph.Gamepad:
                    using (var path = new GraphicsPath())
                    {
                        path.AddLine(7, 7, 17, 7);
                        path.AddBezier(17, 7, 20, 7, 21.5f, 9, 22, 12);
                        path.AddLine(22, 12, 23, 17);
                        path.AddBezier(23, 17, 23.4f, 19, 21.5f, 20.5f, 19.9f, 19.4f);
                        path.AddLines([new PointF(19.9f, 19.4f), new(16, 17), new(8, 17), new(4.1f, 19.4f)]);
                        path.AddBezier(4.1f, 19.4f, 2.5f, 20.5f, .6f, 19, 1, 17);
                        path.AddLine(1, 17, 2, 12);
                        path.AddBezier(2, 12, 2.5f, 9, 4, 7, 7, 7);
                        path.CloseFigure();
                        graphics.DrawPath(pen, path);
                    }
                    Line(new(7, 10), new(7, 15));
                    Line(new(4.5f, 12.5f), new(9.5f, 12.5f));
                    Line(new(16.5f, 11.5f), new(16.6f, 11.5f));
                    Line(new(19, 13.5f), new(19.1f, 13.5f));
                    break;
                case V269DriverGlyph.Shield:
                    graphics.DrawBezier(pen, 12, 22, 12, 22, 20, 18, 20, 12);
                    Line(new(20, 12), new(20, 5), new(12, 2), new(4, 5), new(4, 12));
                    graphics.DrawBezier(pen, 4, 12, 4, 18, 12, 22, 12, 22);
                    Line(new(9, 12), new(11, 14), new(15, 10));
                    break;
                case V269DriverGlyph.Plug:
                    Line(new(8, 3), new(8, 8));
                    Line(new(16, 3), new(16, 8));
                    Line(new(6, 12), new(6, 8), new(18, 8), new(18, 12));
                    graphics.DrawArc(pen, 6, 6, 12, 12, 0, 180);
                    Line(new(12, 18), new(12, 22));
                    break;
            }
        }
        finally { graphics.Restore(state); }
    }
}

internal sealed partial class DarkDriverList
{
    private readonly DarkVScrollBar _horizontal = new()
    {
        Name = "V269DriversHorizontalScroll", AccessibleName = "Rolagem horizontal de drivers", Visible = false
    };
    private readonly Dictionary<string, string> _offerVersions = new(StringComparer.OrdinalIgnoreCase);
    private V269AppearanceOptions _tableOptions = V269AppearanceOptions.Default;
    private bool _compactTable, _updatingTableMetrics;
    private int _dragIndex = -1;
    private int _dropIndex = -1;
    private Point _dragOrigin;

    private string _emptyMessage = "";
    internal string EmptyMessage
    {
        get => _emptyMessage;
        set
        {
            if (_emptyMessage == value) return;
            _emptyMessage = value;
            UpdateAccessibleSelection();
            Invalidate();
        }
    }

    internal bool TableMode { get; private set; }
    internal int TableHeaderHeight => TableMode ? Scale(42) : 0;
    internal int TableRowHeight => Scale(52);
    internal int RowsHeight => Math.Max(1, ClientSize.Height - TableHeaderHeight -
        (TableMode && _horizontal.Visible ? _horizontal.Height : 0));
    private int MinimumWidth => V269AppTable.MinimumWidth(_tableOptions.InterfacePercent, DeviceDpi, _compactTable, drivers: true);
    private int Scale(int pixels) => V269AppTable.Scale(pixels, _tableOptions.InterfacePercent, DeviceDpi);

    internal event Action<int, int>? ReorderRequested;

    internal void UseV269Table()
    {
        TableMode = true;
        _scrollbar.Name = "V269DriversVerticalScroll";
        _scrollbar.AccessibleName = "Rolagem vertical de drivers";
        _scrollbar.Dock = DockStyle.None;
        _horizontal.SetAppHorizontalAxis();
        _horizontal.ValueChanged += (_, _) => Invalidate();
        Controls.Add(_horizontal);
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "Lista de drivers";
    }

    internal void ApplyTableAppearance(V269AppearanceOptions options, int windowWidth)
    {
        _tableOptions = options.Normalize();
        _compactTable = V269AppTable.IsCompact(windowWidth);
        BackColor = V269AppTable.EmptySurface(options.Theme);
        _scrollbar.ApplyAppScrollTheme(options.Theme);
        _horizontal.ApplyAppScrollTheme(options.Theme);
        UpdateMetrics();
    }

    internal void SetOfferVersions(IEnumerable<DriverOfficialMatch> matches)
    {
        _offerVersions.Clear();
        foreach (var group in matches.Where(m => m.OfferedNumericVersionProven &&
            !string.IsNullOrWhiteSpace(m.OfferedNumericVersion))
            .GroupBy(m => m.DeviceId, StringComparer.OrdinalIgnoreCase))
        {
            var versions = group.Select(m => m.OfferedNumericVersion!).Distinct(StringComparer.Ordinal).ToArray();
            if (versions.Length == 1) _offerVersions.Add(group.Key, versions[0]);
        }
        Invalidate();
    }

    internal string? OfferVersion(DriverInventoryEntry item) => _offerVersions.GetValueOrDefault(item.DeviceId);

    private Rectangle TableRow(int index) => new(-_horizontal.Value,
        TableHeaderHeight + index * RowHeight - _scrollOffset, Math.Max(ContentWidth, MinimumWidth), RowHeight);

    private Rectangle TableAction(Rectangle row, bool remove) =>
        V269AppTable.Action(row, _tableOptions.InterfacePercent, DeviceDpi, remove, _compactTable, drivers: true);

    private Rectangle TableGrip(Rectangle row) =>
        V269AppTable.Grip(row, _tableOptions.InterfacePercent, DeviceDpi);

    private void DrawTable(Graphics graphics)
    {
        var saved = graphics.Save();
        try
        {
            int tableWidth = Math.Max(ContentWidth, MinimumWidth);
            int y = TableHeaderHeight;
            graphics.SetClip(new Rectangle(0, 0, ContentWidth, y), CombineMode.Intersect);
            using var font = new Font("Segoe UI", 9f * _tableOptions.TextPercent / 100f);
            using var bold = new Font(font, FontStyle.Bold);
            using var small = new Font("Segoe UI", 8.25f * _tableOptions.TextPercent / 100f);
            V269AppTable.DrawHeader(graphics, new Rectangle(-_horizontal.Value, 0, tableWidth, y),
                _tableOptions, font, DeviceDpi, _compactTable, drivers: true);
            graphics.Restore(saved);
            saved = graphics.Save();
            graphics.SetClip(new Rectangle(0, y, ContentWidth, RowsHeight), CombineMode.Intersect);
            if (_items.Count == 0)
            {
                int padding = Scale(28);
                TextRenderer.DrawText(graphics, EmptyMessage, font,
                    new Rectangle(padding, y + padding, Math.Max(1, ContentWidth - padding * 2), Math.Max(1, RowsHeight - padding)),
                    V269AppTable.StateInk(_tableOptions.Theme, false, false),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                    TextFormatFlags.PreserveGraphicsClipping);
                return;
            }
            int last = Math.Min(_items.Count - 1, (_scrollOffset + RowsHeight - 1) / RowHeight);
            for (int index = _scrollOffset / RowHeight; index <= last; index++)
                DrawTableRow(graphics, index, font, bold, small);
        }
        finally { graphics.Restore(saved); }
    }

    private void DrawTableRow(Graphics graphics, int index, Font font, Font bold, Font small)
    {
        var item = _items[index];
        var row = TableRow(index);
        bool selected = index == _selectedIndex;
        var theme = _tableOptions.Theme;
        V269AppTable.DrawRowBackground(graphics, row, theme, selected, index % 2 != 0,
            _tableOptions.InterfacePercent, DeviceDpi);
        var cells = V269AppTable.Columns(row, _tableOptions.InterfacePercent, DeviceDpi, _compactTable, drivers: true);
        int padding = V269AppTable.CellPadding(_tableOptions.InterfacePercent, DeviceDpi, _compactTable);
        var colors = V269AppTable.Colors(theme);
        Color ink = V269AppTable.RowInk(theme, selected);
        Color muted = selected ? V269AppTable.H(theme switch
        {
            V269Theme.Dark => "#b8c2cb", V269Theme.Claro => "#456781", _ => "#b7d0e3"
        }) : V269AppTable.StateInk(theme, false, false);
        void Text(string value, Rectangle bounds, Font face, Color color)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            TextRenderer.DrawText(graphics, value, face, bounds, color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
        }
        Rectangle Inset(int column)
        {
            var area = cells[column];
            area.Inflate(-padding, 0);
            return area;
        }
        var grip = TableGrip(row);
        Text("⋮⋮", grip, bold, muted);
        int emblemSize = Scale(30);
        var emblem = new Rectangle(grip.Right + Scale(3), row.Y + (row.Height - emblemSize) / 2, emblemSize, emblemSize);
        using (var path = ModernButton.RoundRect(emblem, Scale(8)))
        using (var fill = new SolidBrush(selected ? colors.Selected : colors.Head))
        using (var outline = new Pen(colors.Border))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.FillPath(fill, path);
            graphics.DrawPath(outline, path);
        }
        int n = Scale(17);
        V269DriverVisuals.DrawGlyph(graphics, new Rectangle(emblem.X + (emblem.Width - n) / 2,
            emblem.Y + (emblem.Height - n) / 2, n, n), V269DriverVisuals.Glyph(item), ink);
        int x = emblem.Right + Scale(9);
        int width = cells[0].Right - x - padding;
        int textTop = row.Y + (row.Height - bold.Height - small.Height) / 2;
        Text(DriverHardwareCatalog.DisplayName(item), new Rectangle(x, textTop, width, bold.Height), bold, ink);
        string category = V269DriverVisuals.Category(item);
        string provider = string.IsNullOrWhiteSpace(item.Provider) ? "Não informado" : item.Provider;
        Text(category + " · " + provider, new Rectangle(x, textTop + bold.Height, width, small.Height), small, muted);
        Text(string.IsNullOrWhiteSpace(item.Version) ? "—" : item.Version, Inset(1), font, ink);
        string? version = OfferVersion(item);
        Text(version ?? "—", Inset(2), font, version is null ? muted : ink);
        V269AppTable.DrawAction(graphics, TableAction(row, false), theme, false, CanUpdate(item),
            _tipIndex == index && _tipAction == DriverCardAction.Update, false);
        V269AppTable.DrawAction(graphics, TableAction(row, true), theme, true, CanUninstall(item),
            _tipIndex == index && _tipAction == DriverCardAction.Uninstall, false);
        if (selected && Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(row, -2, -2));
        if (index == _dropIndex)
        {
            using var line = new Pen(colors.Rail, Scale(2));
            int y = index < _dragIndex ? row.Top + 1 : row.Bottom - 1;
            graphics.DrawLine(line, row.Left, y, row.Right, y);
        }
    }

    private void EndTableDrag()
    {
        _dragIndex = _dropIndex = -1;
        if (Capture) Capture = false;
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture && _dragIndex >= 0) EndTableDrag();
    }
}
