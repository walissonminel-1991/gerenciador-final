using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Somente apresentação. Nenhum estado visual autoriza uma operação.
internal enum GfMeterKind { Usage, Temperature, Progress, InvertedProgress }
internal enum GfMeterState { Unavailable, Running, Indeterminate, Success, Failed, Cancelled }

internal readonly record struct GfMeterFrame(double? Fraction, Color Color, GfMeterState State, bool Immediate = false,
    GfMeterKind? ScaleKind = null, double? ScalePosition = null);

internal static class GfMeterScale
{
    internal static (Color Blue, Color Green, Color Yellow, Color Red) Colors(V269Theme theme) =>
        theme == V269Theme.Claro
            ? (Color.FromArgb(21, 105, 177), Color.FromArgb(27, 117, 68), Color.FromArgb(146, 100, 8), Color.FromArgb(180, 44, 60))
            : (Color.FromArgb(45, 156, 219), Color.FromArgb(68, 193, 123), Color.FromArgb(232, 196, 75), theme == V269Theme.Dark ? Color.FromArgb(242, 116, 126) : Color.FromArgb(231, 96, 106));

    internal static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        int C(int x, int y) => (int)Math.Round(x + (y - x) * t);
        return Color.FromArgb(C(a.R, b.R), C(a.G, b.G), C(a.B, b.B));
    }

    internal static Color UsageColor(double percent, V269Theme theme)
    {
        var (blue, green, yellow, red) = Colors(theme);
        return percent <= 40 ? blue : percent <= 60 ? Mix(blue, green, (percent - 40) / 20)
            : percent <= 75 ? Mix(green, yellow, (percent - 60) / 15)
            : Mix(yellow, red, (percent - 75) / 25);
    }

    internal static Color ScaleColor(GfMeterKind kind, double position, V269Theme theme)
    {
        position = Math.Clamp(position, 0, 100);
        var (blue, _, yellow, red) = Colors(theme);
        return kind switch
        {
            GfMeterKind.Progress => blue,
            GfMeterKind.InvertedProgress => position <= 35 ? Mix(red, yellow, position / 35) :
                Mix(yellow, blue, (position - 35) / 65),
            _ => UsageColor(position, theme)
        };
    }

    // Limites térmicos pertencem ao mesmo componente e ao mesmo tipo de sensor.
    // Valores genéricos são apenas referência cromática, nunca diagnóstico.
    internal static bool ValidThermalLimit(double? limit) =>
        limit is double l && double.IsFinite(l) && l >= 70 && l <= 125;

    internal static double ThermalIndex(double celsius, double? limit)
    {
        double green = ValidThermalLimit(limit) ? limit!.Value - 25 : 60;
        double yellow = ValidThermalLimit(limit) ? limit!.Value - 10 : 78;
        double red = ValidThermalLimit(limit) ? limit!.Value : 95;
        return celsius <= 40 ? 40 : celsius <= green ? 40 + 20 * (celsius - 40) / (green - 40)
            : celsius <= yellow ? 60 + 15 * (celsius - green) / (yellow - green)
            : Math.Min(100, 75 + 25 * (celsius - yellow) / (red - yellow));
    }

    internal static bool Valid(double? value, GfMeterKind kind) =>
        value is double v && double.IsFinite(v) &&
        (kind == GfMeterKind.Temperature ? v >= -40 && v <= 150 : v >= 0 && v <= 100);

    internal static GfMeterFrame Frame(GfMeterKind kind, double? value, V269Theme theme,
        double? limit = null, GfMeterState state = GfMeterState.Running)
    {
        var (blue, green, yellow, red) = Colors(theme);
        Color neutral = V269VisualTokens.ForTheme(theme).SecondaryText;
        if (state == GfMeterState.Failed) return new(null, red, state, true);
        if (state == GfMeterState.Cancelled) return new(null, neutral, state, true);
        bool progress = kind is GfMeterKind.Progress or GfMeterKind.InvertedProgress;
        if (state == GfMeterState.Success && progress) return new(1, green, state, true);
        if (state == GfMeterState.Indeterminate && progress) return new(null, blue, state);
        if (state != GfMeterState.Running || !Valid(value, kind))
            return new(null, neutral, GfMeterState.Unavailable, true);
        double v = value!.Value;
        if (kind == GfMeterKind.Temperature)
        {
            bool known = ValidThermalLimit(limit);
            double index = ThermalIndex(v, limit);
            return new(Math.Clamp(v / (known ? limit!.Value : 95), 0, 1),
                ScaleColor(kind, index, theme), state, known && v >= limit!.Value, kind, index);
        }
        if (kind == GfMeterKind.Usage) return new(v / 100, UsageColor(v, theme), state, ScaleKind: kind, ScalePosition: v);
        // A opção invertida é explicitamente visual. Verde fica reservado ao
        // sucesso confirmado; 100% recebido de um provedor não prova conclusão.
        return new(v / 100, ScaleColor(kind, v, theme), state, ScaleKind: kind, ScalePosition: v);
    }
}

// EMA temporal; preserva número bruto separado e reinicia em ausência, troca
// de identidade ou lacuna. Limite térmico comprovado é atendido imediatamente.
internal sealed class GfMeterSmoother
{
    private double? _value;
    private DateTimeOffset? _at;
    internal void Reset() { _value = null; _at = null; }
    internal double? Update(double? raw, DateTimeOffset at, GfMeterKind kind,
        double seconds, double? thermalLimit = null)
    {
        if (!GfMeterScale.Valid(raw, kind)) { Reset(); return null; }
        double v = raw!.Value;
        if (_at is DateTimeOffset old && at <= old) return _value;
        double elapsed = _at is DateTimeOffset last ? (at - last).TotalSeconds : 0;
        bool critical = kind == GfMeterKind.Temperature && GfMeterScale.ValidThermalLimit(thermalLimit) && v >= thermalLimit!.Value;
        _value = _value is null || elapsed > 10 || critical || seconds <= 0
            ? v : _value.Value + (v - _value.Value) * (1 - Math.Exp(-elapsed / seconds));
        _at = at;
        return _value;
    }
}

// Controle nativo independente de motores. Timer exclusivamente visual,
// somente enquanto uma transição/indicador indeterminado está visível.
internal sealed class GfDynamicProgressBar : Control
{
    private GfMeterFrame _target = new(null, Color.Gray, GfMeterState.Unavailable);
    private double _shown, _start;
    private Color _shownColor = Color.Gray;
    private double? _shownPosition, _startPosition;
    private GfMeterKind? _shownKind;
    private long _since;
    private readonly System.Windows.Forms.Timer _animation;
    private V269Theme _theme;
    private bool _animate = true;

    internal GfDynamicProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 8;
        TabStop = false;
        AccessibleRole = AccessibleRole.ProgressBar;
        _animation = new() { Interval = 33 };
        _animation.Tick += (_, _) => Advance();
    }

    internal void Configure(V269Theme theme, bool animate)
    {
        bool changed = _theme != theme;
        _theme = theme; _animate = animate;
        if (!animate) Snap();
        if (changed)
        {
            _animation.Stop();
            _shownKind = null; // o próximo frame é reapresentado no novo tema
        }
        Invalidate();
    }

    internal void Present(GfMeterFrame frame, string description)
    {
        AccessibleDescription = description;
        if (_target == frame && _shownKind == frame.ScaleKind) return;
        _target = frame;
        if (!_animate || !Visible || frame.Immediate || frame.Fraction is null ||
            frame.ScaleKind is null || frame.ScaleKind != _shownKind ||
            frame.ScalePosition is null || _shownPosition is null)
        {
            Snap();
            if (_animate && Visible && frame.State == GfMeterState.Indeterminate) _animation.Start();
            return;
        }
        _start = _shown; _startPosition = _shownPosition; _since = Environment.TickCount64;
        _animation.Start(); Invalidate();
    }

    private void Snap()
    {
        _animation.Stop();
        _shown = _target.Fraction ?? 0; _shownColor = _target.Color;
        _shownPosition = _target.ScalePosition; _shownKind = _target.ScaleKind;
        Invalidate();
    }

    private void Advance()
    {
        if (!Visible || IsDisposed) { Snap(); return; }
        if (_target.State == GfMeterState.Indeterminate) { Invalidate(); return; }
        double t = Math.Clamp((Environment.TickCount64 - _since) / 250d, 0, 1);
        _shown = _start + ((_target.Fraction ?? 0) - _start) * t;
        if (_startPosition is double start && _target.ScalePosition is double target && _target.ScaleKind is GfMeterKind kind)
        {
            _shownPosition = start + (target - start) * t;
            _shownColor = GfMeterScale.ScaleColor(kind, _shownPosition.Value, _theme);
        }
        else { Snap(); return; }
        if (t >= 1) { Snap(); return; }
        Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (IsDisposed || Disposing || _animation is null) return;
        if (!Visible) Snap();
        else if (_animate && _target.State == GfMeterState.Indeterminate) _animation.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 2 || Height < 2) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var p = V269VisualTokens.ForTheme(_theme);
        using var track = new SolidBrush(p.Border);
        using var fill = new SolidBrush(_shownColor);
        using var outline = Capsule(new RectangleF(0, 0, Width, Height));
        e.Graphics.FillPath(track, outline);
        var saved = e.Graphics.Save();
        e.Graphics.SetClip(outline);
        if (_target.State == GfMeterState.Indeterminate)
        {
            int w = Math.Max(1, Width / 4);
            int x = _animate ? (int)((Environment.TickCount64 % 1200) * (Width - w) / 1200) : (Width - w) / 2;
            using var segment = Capsule(new RectangleF(x, 0, w, Height));
            e.Graphics.FillPath(fill, segment);
        }
        else if (_target.Fraction.HasValue && _shown > 0)
        {
            float w = (float)(Width * Math.Clamp(_shown, 0, 1));
            using var segment = Capsule(new RectangleF(0, 0, w, Height));
            e.Graphics.FillPath(fill, segment);
        }
        e.Graphics.Restore(saved);
    }

    private static GraphicsPath Capsule(RectangleF bounds)
    {
        var path = new GraphicsPath();
        float d = Math.Min(bounds.Height, bounds.Width);
        if (d <= 0) return path;
        if (bounds.Width < bounds.Height)
        {
            path.AddEllipse(bounds);
            return path;
        }
        path.AddArc(bounds.Left, bounds.Top, d, d, 90, 180);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 180);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animation.Dispose();
        base.Dispose(disposing);
    }
}
