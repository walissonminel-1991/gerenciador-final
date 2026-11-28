using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GerenciadorAplicativos;

internal sealed record V269AppPreviousResult(Guid Id,string Title,string Description,bool? Complete = null);
internal sealed record V269AppDetailModel(string Name,string Publisher,
    IReadOnlyList<(string Label,string Value)> Fields,string Reasons,string IconSource="",V269MsixIconSource? MsixIcon=null,string Description="",
    IReadOnlyList<V269AppPreviousResult>? PreviousResults=null,Action<Guid>? DismissResult=null, V269DriverGlyph? DriverGlyph=null, string? CleanupGlyph=null, string? CleanupDescriptionHeading=null,
    V269RepairGlyph? RepairGlyph = null, string? RepairReport = null);

// Superfícies exclusivas do painel estruturado, derivadas da cascata final
// do HTML. Os motores e painéis de confirmação/progresso não usam estas cores.
internal static class V269AppDetailStyle
{
    internal static V269Palette Palette(V269Theme theme) => V269VisualTokens.StructuredPanel(theme);
    internal static Color Divider(V269Theme theme) => V269VisualTokens.StructuredDivider(theme);
}

// Botões de fluxo: somente a pintura varia. O botão WinForms mantém
// clique, foco, teclado, acessibilidade e o contrato original.
internal sealed class V269AppFlowButton : Button
{
    private readonly bool _primary,_dismiss;
    private bool _appStyle;
    private int _percent=100;
    private V269Theme _theme;
    internal V269AppFlowButton(bool primary,bool dismiss=false)
    {
        _primary=primary;_dismiss=dismiss;
    }
    internal void ApplyAppStyle(bool active,V269Theme theme,int percent)
    {
        _appStyle=active;_theme=theme;_percent=percent;
        FlatStyle=active?FlatStyle.Flat:FlatStyle.Standard;
        UseVisualStyleBackColor=!active;
        FlatAppearance.BorderSize=active?0:1;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,active);
        if(active)BackColor=V269AppDetailStyle.Palette(theme).RightPanel;
        else BackColor=SystemColors.Control;
        Invalidate();
    }
    private bool HasOutcomeGlyph => Name == "V269DismissFlowResult";
    internal int RequiredAppTextWidth()
    {
        int S(int n)=>V269AppTable.Scale(n,_percent,DeviceDpi);
        return TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding).Width
            + S(6)*2+(_dismiss&&!HasOutcomeGlyph?0:S(12)+S(5))+2;
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size=base.GetPreferredSize(proposedSize);
        return _appStyle?new Size(Math.Max(size.Width,RequiredAppTextWidth()),
            Math.Max(size.Height,V269AppTable.Scale(30,_percent,DeviceDpi))):size;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if(!_appStyle){base.OnPaint(e);return;}
        if(Width<3||Height<3)return;
        Color H(string s)=>V269AppTable.H(s);
        var palette=V269AppDetailStyle.Palette(_theme);
        var (surface,border,ink)=_theme switch
        {
            V269Theme.Claro=>(_primary?H("#13649e"):H("#d5e8f6"),_primary?H("#1c6798"):H("#9bbacf"),_primary?Color.White:H("#173d59")),
            V269Theme.Dark=>(_primary?H("#515961"):H("#30353a"),_primary?H("#b7c0c8"):H("#60666d"),H("#dce0e4")),
            _=>(_primary?H("#205f95"):H("#102638"),_primary?H("#6ea7cf"):H("#416783"),_primary?H("#eff8ff"):H("#c5d6e1"))
        };
        if(!Enabled)ink=palette.SecondaryText;
        int S(int n)=>V269AppTable.Scale(n,_percent,DeviceDpi);
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=ModernButton.RoundRect(new Rectangle(0,0,Width-1,Height-1),S(8));
        using var fill=new SolidBrush(surface);using var outline=new Pen(border);
        g.FillPath(fill,shape);g.DrawPath(outline,shape);
        int left=S(6);
        if(!_dismiss||HasOutcomeGlyph)
        {
            int n=S(12),y=(Height-n)/2;
            var glyphInk = _primary || HasOutcomeGlyph
                ? H(_theme == V269Theme.Claro ? "#087660" : _theme == V269Theme.Dark ? "#8acf78" : "#70c6a3") : ink;
            using var pen=new Pen(glyphInk,Math.Max(1f,DeviceDpi/96f)){StartCap=LineCap.Round,EndCap=LineCap.Round};
            if(_primary||HasOutcomeGlyph)g.DrawLines(pen,new Point[]{new(left,y+n/2),new(left+n/3,y+n-2),new(left+n,y+2)});
            else{g.DrawLine(pen,left,y+2,left+n,y+n-2);g.DrawLine(pen,left+n,y+2,left,y+n-2);}
            left+=n+S(5);
        }
        TextRenderer.DrawText(g,Text,Font,new Rectangle(left,0,Math.Max(1,Width-left-S(6)),Height),ink,
            TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|
            TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
        if(Focused&&ShowFocusCues&&Width>8&&Height>8)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-8,Height-8));
    }
}

// Estado explícito do painel: nunca inferir sucesso nem porcentagem.
internal sealed class V269AppFlowState : Control
{
    private V269Theme _theme;
    private int _percent=100;
    private Color _stateColor;
    internal V269AppFlowState()
    {
        Name="V269AppFlowState";AccessibleRole=AccessibleRole.StaticText;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        Dock=DockStyle.Top;Margin=Padding.Empty;Visible=false;
    }
    internal void ApplyState(V269PanelKind? kind,int? progress,bool? complete,V269Theme theme,int percent)
    {
        _theme=theme;_percent=percent;
        var (caption, color) = kind switch
        {
            V269PanelKind.Confirmation => ("Aguardando autorização", "#e4bb79"),
            V269PanelKind.Result when complete == true => ("Concluído", "#70c6a3"),
            V269PanelKind.Result => ("Requer atenção", "#e4bb79"),
            _ => (progress is int n ? $"Em andamento · {Math.Clamp(n,0,100)}%" : "Em andamento · progresso não informado", "#58b8e6")
        };
        _stateColor=V269AppTable.H(color);
        Text=caption;
        AccessibleName=Text;Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<3||Height<3)return;
        var palette=V269AppDetailStyle.Palette(_theme);Color H(string s)=>V269AppTable.H(s);
        var (surface,border)=_theme switch
        {
            V269Theme.Dark=>(H("#30353a"),H("#60666d")),
            V269Theme.Claro=>(H("#d5e8f6"),H("#9bbacf")),
            _=>(H("#122b40"),H("#385d7c"))
        };
        int S(int n)=>V269AppTable.Scale(n,_percent,DeviceDpi);
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=ModernButton.RoundRect(new Rectangle(0,0,Width-1,Height-1),S(8));
        using var fill=new SolidBrush(surface);using var line=new Pen(border);
        g.FillPath(fill,shape);g.DrawPath(line,shape);
        int dot=S(9);using var state=new SolidBrush(_stateColor);
        g.FillEllipse(state,S(12),(Height-dot)/2,dot,dot);
        TextRenderer.DrawText(g,Text,Font,new Rectangle(S(30),0,Math.Max(1,Width-S(36)),Height),palette.Text,
            TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
    }
}

internal sealed class V269AppFlowNote : Control
{
    private V269Theme _theme;
    private int _scale = 100;
    private bool _isResult;
    private bool _complete;
    private bool _cleanupConfirmation;

    internal V269AppFlowNote()
    {
        Name = "V269FlowNotice";
        AccessibleRole = AccessibleRole.StaticText;
        Dock = DockStyle.Bottom;
        Visible = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    internal void SetContent(bool active, V269PanelPresentation? panel, V269Theme theme, int scale, Font font,
        bool cleanupConfirmation = false)
    {
        _theme = theme;
        _scale = scale;
        _isResult = panel?.Kind == V269PanelKind.Result;
        _complete = panel?.FlowResult?.Complete == true;
        _cleanupConfirmation = active && cleanupConfirmation && panel?.Kind == V269PanelKind.Confirmation;
        Text = !active ? "" : panel?.Kind switch
        {
            V269PanelKind.Confirmation => "A confirmação autoriza somente a ação e o alvo indicados acima.",
            V269PanelKind.Progress => "É possível navegar entre abas e continuar acompanhando a operação.",
            V269PanelKind.Result => panel.Description,
            _ => ""
        };
        AccessibleName = Text;
        Font = font;
        Visible = Text.Length > 0;
        Invalidate();
    }

    private int S(int px) => V269AppTable.Scale(px, _scale, DeviceDpi);
    private int LeftPadding => S(_isResult ? 31 : 13);
    private const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl |
        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

    // O HTML reserva espaço entre o aviso e os comandos de confirmação.
    // Manter as demais operações e páginas exatamente com a métrica anterior.
    internal int BottomGap => S(_cleanupConfirmation ? 30 : 8);
    private int TopGap => S(8);
    private int VerticalPadding => S(_cleanupConfirmation ? 11 : 8);
    internal int MeasureHeight(int width) => Text.Length == 0 ? 0 :
        TextRenderer.MeasureText(Text, Font, new Size(Math.Max(1, width-LeftPadding-S(8)), 32767), Wrap).Height +
        (_cleanupConfirmation ? TopGap + BottomGap + 2 * VerticalPadding : S(32));

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Text.Length == 0 || Width < 3 || Height < S(16)) return;
        var palette = V269AppDetailStyle.Palette(_theme);
        var area = new Rectangle(0, TopGap, Width-1, Math.Max(1, Height-(_cleanupConfirmation ? TopGap+BottomGap : S(16))));
        Color accent = _isResult
            ? V269AppTable.H(_complete ? (_theme == V269Theme.Claro ? "#087660" : "#70c6a3") :
                (_theme == V269Theme.Claro ? "#966117" : "#e4bb79"))
            : palette.Accent;
        using var stripe = new SolidBrush(accent);
        if (!_isResult)
        {
            using var shape = ModernButton.RoundRect(area, S(7));
            using var fill = new SolidBrush(palette.Card);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, shape);
        }
        e.Graphics.FillRectangle(stripe, area.X, area.Y, S(2), area.Height);
        if (_isResult)
        {
            using var pen = new Pen(accent, S(2));
            int y = area.Y + S(13);
            if (_complete)
                e.Graphics.DrawLines(pen, new Point[]{new(S(9),y+S(4)),new(S(13),y+S(8)),new(S(21),y)});
            else
            {
                e.Graphics.DrawLine(pen, S(15), y, S(15), y+S(6));
                e.Graphics.DrawLine(pen, S(15), y+S(9), S(16), y+S(9));
            }
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(LeftPadding,area.Y+VerticalPadding,
            Math.Max(1,Width-LeftPadding-S(8)),Math.Max(1,area.Height-(_cleanupConfirmation ? 2*VerticalPadding : S(16)))), palette.Text, Wrap);
    }
}

internal sealed class V269AppFlowMeter : Control
{
    private int? _value;
    private bool _running;
    private int _scale = 100;
    private V269Theme _theme;
    private readonly GfDynamicProgressBar _meter=new() { Name="V269FlowChromaticBar",TabStop=false };

    internal V269AppFlowMeter()
    {
        Name = "V269FlowPercentMeter";
        AccessibleRole = AccessibleRole.ProgressBar;
        AccessibleName = "Progresso da operação";
        Dock = DockStyle.Bottom;
        Visible = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Controls.Add(_meter);
    }

    internal void SetProgress(bool running, int? value, V269Theme theme, int scale, Font font)
    {
        _running = running;
        _value = running && value is int n && n>=0 && n<=100 ? n : null;
        _theme = theme;
        _scale = scale;
        Font = font;
        AccessibleDescription = !running ? "" : _value is int p ? p + "%" : "Progresso não informado";
        Height = running ? Font.Height + S(21) : 0;
        Visible = running;
        _meter.BackColor=V269AppDetailStyle.Palette(theme).RightPanel;
        _meter.Configure(theme, FindForm() is MainForm main && main.V269CanAnimateButtons);
        var frame = GfUniversalMeter.Operation(_value,
            running ? GfMeterState.Running : GfMeterState.Unavailable, true, theme);
        _meter.Present(frame, AccessibleDescription);
        ArrangeMeter();
        Invalidate();
    }

    private int S(int px) => V269AppTable.Scale(px, _scale, DeviceDpi);
    private void ArrangeMeter()
    {
        if(_meter is null)return;
        _meter.Bounds=new Rectangle(0,S(1)+Font.Height+S(3)+S(3),Math.Max(1,Width-1),S(11));
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);ArrangeMeter();}

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!_running || Width < 3 || Height < S(20)) return;
        var graphics = e.Graphics;
        var palette = V269AppDetailStyle.Palette(_theme);
        var label = new Rectangle(0, S(1), Width, Font.Height+S(3));
        const TextFormatFlags options = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(graphics, "Progresso", Font, label, palette.SecondaryText, options);
        if (_value is int value)
            TextRenderer.DrawText(graphics, value + "%", Font, label, palette.Text, options | TextFormatFlags.Right);

    }
}

// Linha contextual baseada em gf-history-row. A ação descarta apenas
// a apresentação, e nunca um evento do motor ou do registro local.
internal sealed class V269AppHistoryRow : TableLayoutPanel
{
    private readonly Label _symbol=new(){Text="•",AutoSize=true,Margin=Padding.Empty};
    private readonly Label _heading=new(){AutoSize=true,UseMnemonic=false,Margin=Padding.Empty};
    private readonly Label _message=new(){AutoSize=true,UseMnemonic=false,Margin=Padding.Empty};
    private readonly V269AppFlowButton _dismiss=new(false,true){Name="V269AppDismissResult",Text="Dispensar",FlatStyle=FlatStyle.Flat,
        AutoSize=false,UseVisualStyleBackColor=false,Anchor=AnchorStyles.Top|AnchorStyles.Right,Margin=Padding.Empty};
    private Font? _headingFont,_messageFont;
    private Color _line;
    private int _percent=100;
    private V269Theme _theme=V269Theme.AzulGf;
    internal bool HasBottomDivider {get;}
    internal V269AppHistoryRow(V269AppPreviousResult result,Action<Guid>? dismiss,bool hasBottomDivider)
    {
        HasBottomDivider=hasBottomDivider;
        Name="V269AppPreviousResult";AccessibleName=result.Title;AccessibleDescription=result.Description;
        AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Dock=DockStyle.Top;ColumnCount=3;RowCount=2;
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,23));ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,68));
        RowStyles.Add(new RowStyle(SizeType.AutoSize));RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _heading.Text=result.Title;_message.Text=result.Description;
        _symbol.Text = result.Complete switch { true => "✓", false => "!", _ => "•" };
        _dismiss.AccessibleName="Dispensar resultado anterior de "+result.Title;
        _dismiss.Enabled=dismiss is not null;_dismiss.Click+=(_,_)=>dismiss?.Invoke(result.Id);
        Controls.Add(_symbol,0,0);SetRowSpan(_symbol,2);
        Controls.Add(_heading,1,0);Controls.Add(_message,1,1);
        Controls.Add(_dismiss,2,0);SetRowSpan(_dismiss,2);
        FontChanged+=(_,_)=>ApplyFonts();
        ApplyFonts();
    }
    private void ApplyFonts()
    {
        if(IsDisposed||Disposing)return;
        var h=_headingFont;var m=_messageFont;
        _headingFont=new Font(Font.FontFamily,Math.Max(1f,Font.SizeInPoints*11f/12f),FontStyle.Bold);
        _messageFont=new Font(Font.FontFamily,Math.Max(1f,Font.SizeInPoints*10f/12f));
        _heading.Font=_headingFont;_message.Font=_dismiss.Font=_messageFont;
        h?.Dispose();m?.Dispose();
    }
    internal void ApplyMetrics(int percent,int width)
    {
        int S(int n)=>V269AppTable.Scale(n,percent,DeviceDpi);
        _percent=percent;_dismiss.ApplyAppStyle(true,_theme,percent);
        Margin=Padding.Empty;Padding=new Padding(0,S(8),0,S(8));
        int symbol=S(23),button=Math.Max(S(65),TextRenderer.MeasureText(_dismiss.Text,_dismiss.Font).Width+S(12));
        ColumnStyles[0].Width=symbol;ColumnStyles[2].Width=button;
        MaximumSize=new Size(Math.Max(1,width),0);
        _heading.MaximumSize=_message.MaximumSize=new Size(Math.Max(1,width-symbol-button-S(7)),0);
        _message.Margin=new Padding(0,S(4),S(7),0);_heading.Margin=new Padding(0,0,S(7),0);
        _dismiss.Size=new Size(button,Math.Max(S(25),_dismiss.Font.Height+S(6)));
    }
    internal void ApplyPalette(V269Palette palette)
    {
        BackColor=palette.RightPanel;_heading.ForeColor=palette.Text;
        _symbol.ForeColor=palette.Accent;_message.ForeColor=palette.SecondaryText;_line=palette.Border;
        _theme=palette==V269AppDetailStyle.Palette(V269Theme.Dark)?V269Theme.Dark:
            palette==V269AppDetailStyle.Palette(V269Theme.Claro)?V269Theme.Claro:V269Theme.AzulGf;
        _dismiss.ApplyAppStyle(true,_theme,_percent);_dismiss.ForeColor=palette.Text;
        _dismiss.FlatAppearance.BorderColor=palette.Border;
        _dismiss.FlatAppearance.MouseOverBackColor=palette.Card;
        _dismiss.FlatAppearance.MouseDownBackColor=palette.Card;Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(!HasBottomDivider||Width<1||Height<1)return;
        using var pen=new Pen(_line);e.Graphics.DrawLine(pen,0,Height-1,Width-1,Height-1);
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);if(disposing){_headingFont?.Dispose();_messageFont?.Dispose();}
    }
}

// Histórico somente de apresentação nos estados operacionais de Aplicativos.
// A altura é limitada pelo hospedeiro; confirmação, descrição e cancelamento
// conservam os respectivos controles e contratos do motor.
internal sealed class V269AppOperationHistory : Panel
{
    private readonly Panel _viewport=new(){Margin=Padding.Empty};
    private readonly DarkVScrollBar _scroll=new(){Name="V269AppOperationHistoryScroll",
        AccessibleName="Rolagem dos resultados anteriores",Visible=false};
    private readonly WheelDeltaAccumulator _wheel=new();
    private readonly TableLayoutPanel _rows=new(){Dock=DockStyle.None,AutoSize=false,
        ColumnCount=1,Margin=Padding.Empty};
    private readonly Label _heading=new(){Text="Resultados anteriores",AutoSize=true,
        Dock=DockStyle.Top,Margin=Padding.Empty};
    private Font? _headingFont;
    private bool _measuring;
    private int _percent=100;
    private V269Theme _theme;
    internal V269AppOperationHistory(IReadOnlyList<V269AppPreviousResult> results,Action<Guid> dismiss)
    {
        Name="V269AppOperationHistory";AccessibleName="Resultados anteriores";
        Dock=DockStyle.Fill;AutoScroll=false;Margin=Padding.Empty;
        _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        _rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rows.Controls.Add(_heading,0,_rows.RowCount++);
        _heading.Paint+=(_,e)=>{
            if(_heading.Width<1)return;
            using var pen=new Pen(V269AppDetailStyle.Divider(_theme));
            e.Graphics.DrawLine(pen,0,0,_heading.Width-1,0);
        };
        for(int i=0;i<results.Count;i++)
        {
            var row=new V269AppHistoryRow(results[i],dismiss,i<results.Count-1);
            _rows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rows.Controls.Add(row,0,_rows.RowCount++);
            foreach(Control button in row.Controls.OfType<Button>())
                button.Enter+=(_,_)=>EnsureVisible(button);
            row.MouseWheel+=ScrollContent;
            foreach(Control child in row.Controls)child.MouseWheel+=ScrollContent;
        }
        _viewport.Controls.Add(_rows);Controls.Add(_viewport);Controls.Add(_scroll);
        _scroll.ValueChanged+=(_,_)=>PositionRows();
        _viewport.MouseWheel+=ScrollContent;_rows.MouseWheel+=ScrollContent;
        _heading.MouseWheel+=ScrollContent;_scroll.MouseWheel+=ScrollContent;
        SizeChanged+=(_,_)=>MeasureRows();
        FontChanged+=(_,_)=>ApplyFonts();
        DpiChangedAfterParent+=(_,_)=>MeasureRows();
        ApplyFonts();
    }
    private void ApplyFonts()
    {
        if(IsDisposed||Disposing)return;
        if(_headingFont is null||!_headingFont.FontFamily.Equals(Font.FontFamily)||
            Math.Abs(_headingFont.SizeInPoints-Font.SizeInPoints)>.01f)
        {
            var old=_headingFont;_headingFont=new Font(Font,FontStyle.Bold);
            _heading.Font=_headingFont;old?.Dispose();
        }
        MeasureRows();
    }
    internal void ApplyAppearance(V269Theme theme,int percent,Font font)
    {
        _theme=theme;_percent=percent;Font=font;
        var palette=V269AppDetailStyle.Palette(theme);
        BackColor=_viewport.BackColor=_rows.BackColor=palette.RightPanel;_heading.ForeColor=palette.Text;
        _scroll.ApplyAppScrollTheme(theme);
        foreach(var row in _rows.Controls.OfType<V269AppHistoryRow>())row.ApplyPalette(palette);
        MeasureRows();_heading.Invalidate();
    }
    private int Measure(int width)
    {
        width=Math.Max(1,width);
        int S(int n)=>V269AppTable.Scale(n,_percent,DeviceDpi);
        _heading.Padding=new Padding(0,S(12),0,S(9));
        _rows.Width=width;_heading.MaximumSize=new Size(width,0);
        foreach(var row in _rows.Controls.OfType<V269AppHistoryRow>())row.ApplyMetrics(_percent,width);
        return Math.Max(1,_rows.GetPreferredSize(new Size(width,0)).Height);
    }
    internal int PreferredHeightForWidth(int width)=>Measure(width);
    private void MeasureRows()
    {
        if(_measuring||IsDisposed||Disposing)return;_measuring=true;
        try
        {
            int width=Math.Max(1,ClientSize.Width),height=Math.Max(1,ClientSize.Height);
            int barWidth=Math.Max(12,_scroll.Width);
            int content=Measure(width);
            if(content>height){width=Math.Max(1,width-barWidth-3);content=Measure(width);}
            _viewport.Bounds=new Rectangle(0,0,width,height);
            _scroll.Bounds=new Rectangle(Math.Max(0,ClientSize.Width-barWidth),0,barWidth,height);
            _rows.Size=new Size(width,content);_rows.PerformLayout();
            _scroll.SetMetrics(content,height);PositionRows();
        }
        finally{_measuring=false;}
    }
    private void PositionRows()
    {
        _rows.Location=new Point(0,-_scroll.Value);
        _scroll.AccessibleDescription=$"Posição {_scroll.Value} de {_scroll.MaximumValue}";
    }
    internal void EnsureVisible(Control control)
    {
        if(control.IsDisposed||_scroll.MaximumValue==0)return;
        var location=_rows.PointToClient(control.PointToScreen(Point.Empty));
        if(location.Y<_scroll.Value)_scroll.Value=location.Y;
        else if(location.Y+control.Height>_scroll.Value+_viewport.Height)
            _scroll.Value=location.Y+control.Height-_viewport.Height;
    }
    private void ScrollContent(object? sender,MouseEventArgs e)
    {
        if(e is HandledMouseEventArgs {Handled:true}||_scroll.MaximumValue==0)return;
        int notches=_wheel.Consume(e.Delta);
        if(notches!=0)_scroll.Value-=notches*Math.Max(24,Font.Height*3);
        if(e is HandledMouseEventArgs handled)handled.Handled=true;
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollContent(this,e);base.OnMouseWheel(e);
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);if(disposing)_headingFont?.Dispose();
    }
}

internal sealed class V269AppDetailPanel : Panel
{
    private readonly Panel _viewport=new(){Name="V269AppDetailsViewport",AutoScroll=false,Margin=Padding.Empty};
    private readonly DarkVScrollBar _scroll=new(){Name="V269AppDetailsScroll",
        AccessibleName="Rolagem dos detalhes do aplicativo",Visible=false};
    private readonly WheelDeltaAccumulator _wheel=new();
    private readonly TableLayoutPanel _table=new(){Name="V269AppDetailsFields",Dock=DockStyle.None,AutoSize=false,
        ColumnCount=2,Margin=Padding.Empty};
    private V269Palette _palette=V269VisualTokens.ForTheme(V269Theme.AzulGf);
    private V269AppDetailModel? _model;
    private int _visibleFieldCount;
    private bool _arranging;
    private int _interfacePercent=100;
    private V269Theme _theme=V269Theme.AzulGf;
    private readonly TableLayoutPanel _identity=new(){Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
        ColumnCount=2,RowCount=2,Margin=new Padding(3,3,3,8)};
    private readonly V269AppDetailIcon _icon=new();
    private readonly Label _name=new(){AutoSize=true,UseMnemonic=false,Margin=new Padding(3,4,3,1)};
    private readonly Label _publisher=new(){AutoSize=true,UseMnemonic=false,Margin=new Padding(3,0,3,4)};
    private Font? _identityFont,_descriptionHeadingFont,_historyHeadingFont;
    internal V269AppDetailPanel()
    {
        Name="V269StructuredAppDetails";AccessibleName="Informações verificáveis e elegibilidade do aplicativo";
        Dock=DockStyle.Fill;AutoScroll=false;Margin=Padding.Empty;Visible=false;
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));
        _identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,56));
        _identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        _identity.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _identity.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _identity.Controls.Add(_icon,0,0);_identity.SetRowSpan(_icon,2);
        _identity.Controls.Add(_name,1,0);_identity.Controls.Add(_publisher,1,1);
        _table.RowCount=1;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _table.Controls.Add(_identity,0,0);_table.SetColumnSpan(_identity,2);_identity.Visible=false;
        _table.CellPaint+=(_,e)=>{
            if(_model is null||(_model.RepairGlyph.HasValue ? !HasAppFieldDivider(e.Row) : e.Row>_visibleFieldCount)||
                e.CellBounds.Width<1||e.CellBounds.Height<1)return;
            int bottom=e.CellBounds.Bottom-1;
            if(e.Row==0&&HasAppFieldDivider(e.Row))
                bottom-=Math.Min(e.CellBounds.Height-1,V269AppTable.Scale(5,100,DeviceDpi));
            using var line=new Pen(V269AppDetailStyle.Divider(_theme));
            e.Graphics.DrawLine(line,e.CellBounds.Left,bottom,e.CellBounds.Right-1,bottom);
        };
        _viewport.Controls.Add(_table);Controls.Add(_viewport);Controls.Add(_scroll);
        _scroll.ValueChanged+=(_,_)=>PositionContent();
        _table.FontChanged+=(_,_)=>FitLabels();
        SizeChanged+=(_,_)=>FitLabels();VisibleChanged+=(_,_)=>FitLabels();
        foreach(Control control in new Control[]{_viewport,_table,_identity,_icon,_name,_publisher})
            control.MouseWheel+=ScrollContent;
        _scroll.MouseWheel+=ScrollContent;
        FontChanged+=(_,_)=>RefreshIdentityFont();
        DpiChangedAfterParent+=(_,_)=>ApplyInterfaceScale(_interfacePercent);
        RefreshIdentityFont();ApplyInterfaceScale(100);ApplyPalette(V269AppDetailStyle.Palette(_theme),_theme);
    }
    internal void Present(V269AppDetailModel? model)
    {
        if(Equals(_model,model))return;
        _model=model;
        bool driver = model?.DriverGlyph is not null;
        bool cleanup = model?.CleanupGlyph is not null;
        bool repair = model?.RepairGlyph is not null;
        AccessibleName = repair ? "Detalhes da ferramenta de reparo" : cleanup ? "Detalhes da limpeza e armazenamento" : driver ? "Informações e elegibilidade do driver" :
            "Informações verificáveis e elegibilidade do aplicativo";
        _scroll.AccessibleName = repair ? "Detalhes da ferramenta de reparo" : cleanup ? "Rolagem dos detalhes da limpeza" : driver ? "Rolagem dos detalhes do driver" : "Rolagem dos detalhes do aplicativo";
        _visibleFieldCount=0;
        AccessibleDescription=model is null?"":string.Join(Environment.NewLine,
            new[]{model.Name,model.Publisher}.Concat(model.Fields.Select(field=>$"{field.Label}: {field.Value}"))
                .Concat(new[]{model.Description,model.Reasons,model.RepairReport ?? "",cleanup||repair?"Use os botões da ferramenta para iniciar ações.":"Enter ou botão direito: ações disponíveis."}));
        _scroll.Value=0;
        _table.SuspendLayout();
        try
        {
            foreach(Control child in _table.Controls.Cast<Control>().Where(c=>c!=_identity).ToArray())child.Dispose();
            _table.RowStyles.Clear();_table.RowCount=1;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _identity.Visible=model is not null;
            _name.Text=model?.Name??"";_publisher.Text=model?.Publisher??"";
            _icon.SetIdentity(model?.Name??"",model?.IconSource??"",model?.MsixIcon,model?.DriverGlyph,model?.CleanupGlyph,model?.RepairGlyph);
            if(model is null)return;
            Label Make(string value,bool strong)
            {
                var label=new Label{Text=value,AutoSize=true,
                    Margin=new Padding(0,V269AppTable.Scale(11,_interfacePercent,DeviceDpi),0,V269AppTable.Scale(11,_interfacePercent,DeviceDpi)),ForeColor=strong?_palette.Text:_palette.SecondaryText,
                    AccessibleName=value,Tag=strong,UseMnemonic=false};
                label.MouseWheel+=ScrollContent;return label;
            }
            void AddFull(string value,bool strong,string name="")
            {
                int row=_table.RowCount++;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var label=Make(value,strong);label.Name=name;
                ApplyFullMetrics(label);
                if(name=="V269AppDescriptionHeading"&&_descriptionHeadingFont is not null)
                    label.Font=_descriptionHeadingFont;
                if(name=="V269AppHistoryHeading")
                {
                    if(_historyHeadingFont is not null)label.Font=_historyHeadingFont;
                    // O divisor do HTML atravessa toda a área útil, não
                    // somente a largura natural do texto do título.
                    label.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
                }
                if(name is "V269AppDetailNote" or "V269AppHistoryHeading")
                    label.Paint+=(_,e)=>{
                        int S(int n)=>V269AppTable.Scale(n,_interfacePercent,DeviceDpi);
                        if(name=="V269AppHistoryHeading")
                        {
                            using var pen=new Pen(V269AppDetailStyle.Divider(_theme));
                            e.Graphics.DrawLine(pen,0,0,label.Width-1,0);
                        }
                        else
                        {
                            var accent=_theme switch
                            {V269Theme.Dark=>V269AppTable.H("#9ca6af"),V269Theme.Claro=>V269AppTable.H("#2577a8"),_=>V269AppTable.H("#497f9f")};
                            using var brush=new SolidBrush(accent);
                            e.Graphics.FillRectangle(brush,0,0,S(2),label.Height);
                        }
                    };
                _table.Controls.Add(label,0,row);_table.SetColumnSpan(label,2);
            }
            // A especificação HTML apresenta quatro campos. Versão e data
            // já estão na tabela; a oferta real permanece na nota quando existe.
            if (repair)
            {
                AddFull("Função", true, "V269AppDescriptionHeading");
                AddFull(model.Description, false);
            }
            foreach(var field in model.Fields.Where(field=>cleanup || driver || repair || field.Label is "Tipo" or "Origem" or "Arquitetura" or "Local"))
            {
                _visibleFieldCount++;
                int row=_table.RowCount++;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var caption=Make(field.Label,false);
                ApplyFieldMetrics(caption);
                _table.Controls.Add(caption,0,row);
                var value=Make(field.Value,true);
                ApplyFieldMetrics(value);
                value.Anchor=AnchorStyles.Top|AnchorStyles.Right;
                value.TextAlign=ContentAlignment.TopRight;
                _table.Controls.Add(value,1,row);
            }
            string note=model.Reasons;
            if (!driver && !repair)
            {
                AddFull(model.CleanupDescriptionHeading??"Descrição",true,"V269AppDescriptionHeading");
                AddFull(string.IsNullOrWhiteSpace(model.Description)?"Não informada pela fonte local.":model.Description,false);
                var offer=model.Fields.FirstOrDefault(field=>field.Label=="Oferta").Value;
                if(!string.IsNullOrWhiteSpace(offer)&&!offer.Equals("não informada",StringComparison.OrdinalIgnoreCase))
                    note=$"Oferta: {offer}. {note}";
            }
            if (repair && !string.IsNullOrWhiteSpace(model.RepairReport))
            {
                AddFull("Resultado", true);
                AddFull(model.RepairReport, false);
            }
            AddFull(note,false,"V269AppDetailNote");
            if(model.PreviousResults is {Count:>0} previous)
            {
                AddFull("Resultados anteriores",true,"V269AppHistoryHeading");
                for(int index=0;index<previous.Count;index++)
                {
                    int row=_table.RowCount++;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    var history=new V269AppHistoryRow(previous[index],model.DismissResult,index<previous.Count-1);
                    history.ApplyMetrics(_interfacePercent,Math.Max(1,ClientSize.Width));
                    history.ApplyPalette(_palette);
                    history.MouseWheel+=ScrollContent;
                    foreach(Control child in history.Controls)child.MouseWheel+=ScrollContent;
                    _table.Controls.Add(history,0,row);_table.SetColumnSpan(history,2);
                }
            }
        }
        finally{_table.ResumeLayout(true);FitLabels();}
    }
    internal bool HasAppFieldDivider(int row) => _model?.RepairGlyph is not null
        ? row == 0 || row is >= 3 && row < 3 + _visibleFieldCount
        : _model is { DriverGlyph: null } && row >= 0 && row <= _visibleFieldCount;

    private void ApplyFieldMetrics(Label label)
    {
        int vertical=V269AppTable.Scale(11,_interfacePercent,DeviceDpi);
        int gap=_model is {DriverGlyph:null}?V269AppTable.Scale(6,100,DeviceDpi):0;
        bool value=label.Tag is true;
        label.Margin=new Padding(value?gap:0,vertical,value?0:gap,vertical);
    }

    private void FitLabels()
    {
        if(_arranging||IsDisposed||Disposing)return;_arranging=true;
        try
        {
            // Mesma espessura de DarkVScrollBar.ApplyAppScrollTheme (12 px).
            // Não ler Bounds anteriores nem divergir da reaplicação do tema.
            const int barWidth=12;
            int height=Math.Max(1,ClientSize.Height);
            bool app = _model is { DriverGlyph: null };
            var labels = _table.Controls.OfType<Label>().ToArray();
            int captionWidth = app
                ? labels.Where(label => _table.GetColumnSpan(label) == 1 && _table.GetColumn(label) == 0)
                    .Select(label => TextRenderer.MeasureText(label.Text, label.Font, Size.Empty,
                        TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width + label.Margin.Horizontal)
                    .DefaultIfEmpty(0).Max()
                : 0;

            int Measure(int width)
            {
                _table.Width=width;
                _identity.MaximumSize=new Size(width,0);
                int identityText=Math.Max(20,width-(int)_identity.ColumnStyles[0].Width);
                _name.MaximumSize=_publisher.MaximumSize=new Size(identityText,0);
                // Reservar a largura dos rótulos para deixar a origem e o local
                // usarem a área que sobra, com os 12 px de intervalo do HTML.
                int leftWidth=Math.Min(captionWidth,Math.Max(1,(int)(width*.4)));
                _table.ColumnStyles[0].SizeType=app?SizeType.Absolute:SizeType.Percent;
                _table.ColumnStyles[0].Width=app?leftWidth:40;
                foreach(Label label in labels)
                {
                    int column=_table.GetColumn(label);
                    bool spansTable=_table.GetColumnSpan(label)==2;
                    int max=app
                        ? (spansTable?width:column==0?leftWidth:width-leftWidth)-label.Margin.Horizontal
                        : label.Name=="V269AppHistoryHeading"?width:
                            spansTable?width-6:(int)(width*(column==0 ? .4 : .6))-6;
                    label.MaximumSize=new Size(Math.Max(20,max),0);
                }
                foreach(var history in _table.Controls.OfType<V269AppHistoryRow>())
                    history.ApplyMetrics(_interfacePercent,width);
                return Math.Max(1,_table.GetPreferredSize(new Size(width,0)).Height);
            }
            // Primeiro medir com a largura inteira; só reservar a trilha
            // se houver excesso real. Evitar quebra de texto desnecessária.
            int width=Math.Max(1,ClientSize.Width);
            int content=Measure(width);
            if(content>height){width=Math.Max(1,width-barWidth-3);content=Measure(width);}
            _viewport.Bounds=new Rectangle(0,0,width,height);
            _scroll.Bounds=new Rectangle(Math.Max(0,ClientSize.Width-barWidth),0,barWidth,height);
            _table.Size=new Size(width,content);
            _table.PerformLayout();
            _scroll.SetMetrics(content,height);PositionContent();
        }
        finally{_arranging=false;}
    }
    private void PositionContent()
    {
        _table.Location=new Point(0,-_scroll.Value);
        if (_model is { DriverGlyph: null }) _table.Invalidate();
        _scroll.AccessibleDescription=$"Posição {_scroll.Value} de {_scroll.MaximumValue}";
    }
    private void ScrollContent(object? sender,MouseEventArgs e)
    {
        if(e is HandledMouseEventArgs {Handled:true}||_scroll.MaximumValue==0)return;
        int notches=_wheel.Consume(e.Delta);
        if(notches!=0)_scroll.Value-=notches*Math.Max(24,Font.Height*3);
        if(e is HandledMouseEventArgs handled)handled.Handled=true;
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollContent(this,e);base.OnMouseWheel(e);
    }
    private void RefreshIdentityFont()
    {
        if(IsDisposed||Disposing)return;
        if(_identityFont is not null&&_identityFont.FontFamily.Equals(Font.FontFamily)&&
            Math.Abs(_identityFont.SizeInPoints-Font.SizeInPoints*16f/12f)<.01f)return;
        var old=_identityFont;var oldDescription=_descriptionHeadingFont;var oldHistory=_historyHeadingFont;
        _identityFont=new Font(Font.FontFamily,Font.SizeInPoints*16f/12f,FontStyle.Bold);
        _descriptionHeadingFont=new Font(Font.FontFamily,Font.SizeInPoints*13f/12f,FontStyle.Bold);
        _historyHeadingFont=new Font(Font,FontStyle.Bold);
        _name.Font=_identityFont;
        foreach(var label in _table.Controls.OfType<Label>())
        {
            if(label.Name=="V269AppDescriptionHeading")label.Font=_descriptionHeadingFont;
            else if(label.Name=="V269AppHistoryHeading")label.Font=_historyHeadingFont;
        }
        old?.Dispose();oldDescription?.Dispose();oldHistory?.Dispose();FitLabels();
    }
    private void ApplyFullMetrics(Label label)
    {
        int S(int n)=>V269AppTable.Scale(n,_interfacePercent,DeviceDpi);
        label.Padding=Padding.Empty;
        label.Margin=label.Name switch
        {
            "V269AppDescriptionHeading"=>new Padding(0,S(22),0,S(8)),
            "V269AppDetailNote"=>new Padding(0,S(17),0,0),
            "V269AppHistoryHeading"=>new Padding(0,S(19),0,S(9)),
            _=>new Padding(0,S(label.Tag is true?16:4),0,S(label.Tag is true?8:7))
        };
        if(label.Name=="V269AppDetailNote")label.Padding=new Padding(S(13),S(7),0,S(7));
        if(label.Name=="V269AppHistoryHeading")label.Padding=new Padding(0,S(12),0,0);
    }
    internal void ApplyInterfaceScale(int percent)
    {
        _interfacePercent=percent;
        int D(int n)=>V269AppTable.Scale(n,100,DeviceDpi);
        int size=D(48),gap=D(10);
        _identity.Margin=new Padding(0,D(4),0,D(13)+D(5));
        _name.Margin=Padding.Empty;
        _publisher.Margin=new Padding(0,D(5),0,0);
        foreach(Label label in _table.Controls.OfType<Label>())
            if(_table.GetColumnSpan(label)==2)ApplyFullMetrics(label);
            else ApplyFieldMetrics(label);
        _icon.Size=new Size(size,size);
        _identity.ColumnStyles[0].Width=size+gap;
        FitLabels();
    }
    internal void ApplyPalette(V269Palette palette,V269Theme theme)
    {
        _theme=theme;_palette=palette;
        BackColor=_viewport.BackColor=_table.BackColor=_identity.BackColor=palette.RightPanel;
        _scroll.ApplyAppScrollTheme(theme);
        _name.ForeColor=palette.Text;_publisher.ForeColor=palette.SecondaryText;
        _icon.ApplyPalette(palette);_table.Invalidate();
        foreach(Label label in _table.Controls.OfType<Label>())
            label.ForeColor=label.Tag is true?palette.Text:palette.SecondaryText;
        foreach(var history in _table.Controls.OfType<V269AppHistoryRow>())history.ApplyPalette(palette);
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if(disposing){_identityFont?.Dispose();_descriptionHeadingFont?.Dispose();_historyHeadingFont?.Dispose();}
    }
}

// Usa o mesmo leitor de recursos locais, sem carregar imagens do protótipo,
// sem consultar rede e sem expor o caminho do arquivo ao leitor de tela.
internal sealed class V269AppDetailIcon:Control
{
    private V269AppIconCache? _cache;
    private V269Palette _palette=V269VisualTokens.ForTheme(V269Theme.AzulGf);
    private string _source="",_initial="";
    private V269MsixIconSource? _msix;
    private V269DriverGlyph? _driverGlyph;
    private string? _cleanupGlyph;
    private V269RepairGlyph? _repairGlyph;
    internal V269AppDetailIcon()
    {
        Name="V269AppDetailIcon";AccessibleRole=AccessibleRole.Graphic;TabStop=false;
        Size=new Size(48,48);Margin=new Padding(0,3,0,3);
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|
            ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;
    }
    internal void SetIdentity(string name,string source,V269MsixIconSource? msix,V269DriverGlyph? driverGlyph,string? cleanupGlyph=null,V269RepairGlyph? repairGlyph=null)
    {
        _driverGlyph=driverGlyph;_cleanupGlyph=cleanupGlyph;_repairGlyph=repairGlyph;
        _source=source;_msix=msix;_initial=string.IsNullOrWhiteSpace(name)?"?":name.Trim()[0].ToString().ToUpperInvariant();
        AccessibleName=(cleanupGlyph is not null || repairGlyph.HasValue ? "Ícone da ferramenta " : driverGlyph.HasValue ? "Ícone do driver " : "Ícone do aplicativo ")+name;Invalidate();
    }
    internal void ApplyPalette(V269Palette palette){_palette=palette;Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<3||Height<3)return;
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=ModernButton.RoundRect(new Rectangle(0,0,Width-1,Height-1),Math.Max(2,Width/6));
        using var fill=new SolidBrush(_palette.Card);using var border=new Pen(_palette.Border);
        g.FillPath(fill,shape);g.DrawPath(border,shape);
        var theme = _palette == V269AppDetailStyle.Palette(V269Theme.Claro) ? V269Theme.Claro :
            _palette == V269AppDetailStyle.Palette(V269Theme.Dark) ? V269Theme.Dark : V269Theme.AzulGf;
        if (_repairGlyph is { } repairGlyph)
        {
            int size = Math.Max(1, (int)Math.Round(Width * 26d / 48d));
            V269RepairVisuals.Draw(g, repairGlyph, new Rectangle((Width-size)/2, (Height-size)/2, size, size), theme);
            return;
        }
        if (_cleanupGlyph is { } cleanupGlyph)
        {
            int size=Math.Max(1,(int)Math.Round(Width*26d/48d));
            V269CleanupGlyph.Draw(g,cleanupGlyph,new Rectangle((Width-size)/2,(Height-size)/2,size,size),theme);
            return;
        }
        if (_driverGlyph is { } glyph)
        {
            int size = Math.Max(1, (int)Math.Round(Width * 26d / 48d));
            V269DriverVisuals.DrawGlyph(g, new Rectangle((Width-size)/2,(Height-size)/2,size,size), glyph, _palette.Accent);
            return;
        }
        var image=_source.Length==0&&_msix is null?null:(_cache??=new V269AppIconCache(this)).Get(_source,_msix);
        if(image is not null)
        {
            int inset=Math.Max(2,Width/12);
            g.DrawImage(image,new Rectangle(inset,inset,Width-2*inset,Height-2*inset));
        }
        else TextRenderer.DrawText(g,_initial,Font,ClientRectangle,_palette.Text,
            TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);
    }
    protected override void Dispose(bool disposing){if(disposing)_cache?.Dispose();base.Dispose(disposing);}
}
