using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Componentes de apresentação: nenhuma elegibilidade é calculada pelo desenho.
internal sealed class V269SystemUpdateRow : Panel
{
    private readonly Label _name, _info;
    internal Button? Command { get; }
    private V269AppearanceOptions _options;
    private Font? _nameFont, _infoFont;

    internal V269SystemUpdateRow(string name,string info,string? command,V269AppearanceOptions options)
    {
        Name="V269SystemUpdateRow"; AccessibleName=name; AccessibleDescription=info;
        BackColor=Color.Transparent; Margin=Padding.Empty;
        SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        _name=new Label{Text=name,UseMnemonic=false,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleLeft};
        _info=new Label{Text=info,UseMnemonic=false,BackColor=Color.Transparent,TextAlign=ContentAlignment.TopLeft};
        _name.Click+=(_,_)=>OnClick(EventArgs.Empty);_info.Click+=(_,_)=>OnClick(EventArgs.Empty);
        Controls.Add(_name);Controls.Add(_info);
        if(command is not null)
        {
            Command=new V269CleanupButton(command,"download");Controls.Add(Command);
        }
        ApplyOptions(options);
    }
    private int S(int n)=>V269AppTable.Scale(n,_options.InterfacePercent,DeviceDpi);
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        _options=options.Normalize();
        float size=9f*_options.TextPercent/100f;
        if(_nameFont is null||Math.Abs(_nameFont.Size-size)>.01f)
        {
            var a=_nameFont; var b=_infoFont;
            _nameFont=new Font("Segoe UI",size,FontStyle.Bold);
            _infoFont=new Font("Segoe UI",size);
            _name.Font=_nameFont;_info.Font=_infoFont;a?.Dispose();b?.Dispose();
        }
        var p=V269VisualTokens.ForTheme(_options.Theme);
        _name.ForeColor=p.Text;_info.ForeColor=p.SecondaryText;
        if(Command is V269CleanupButton button)button.ApplyOptions(_options);
        Fit();Invalidate();
    }
    internal void Fit()
    {
        if(_name is null||_info is null||Width<1)return;
        int pad=S(4),gap=S(7),bh=S(30);
        int bw=Command is null?0:Math.Min(Math.Max(1,Width/2),Math.Max(S(89),TextRenderer.MeasureText(Command.Text,Command.Font).Width+S(26)));
        bool stacked=Command is not null&&Width<S(260);
        int w=Math.Max(1,Width-pad*2-(stacked||Command is null?0:bw+gap));
        int h1=TextRenderer.MeasureText(_name.Text,_name.Font,new Size(w,1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
        int h2=TextRenderer.MeasureText(_info.Text,_info.Font,new Size(w,1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
        _name.Bounds=new Rectangle(pad,pad,w,h1);
        _info.Bounds=new Rectangle(pad,pad+h1,w,h2);
        int height=Math.Max(S(42),h1+h2+pad*2);
        if(stacked)height+=bh+gap;
        if(Height!=height)Height=height;
        if(Command is not null)Command.Bounds=new Rectangle(Math.Max(0,Width-bw-pad),stacked?Height-bh-pad:(Height-bh)/2,bw,bh);
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);Fit();}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen=new Pen(V269VisualTokens.ForTheme(_options.Theme).Border);
        e.Graphics.DrawLine(pen,0,Height-1,Math.Max(0,Width-1),Height-1);
    }
    protected override void Dispose(bool disposing){if(disposing){_nameFont?.Dispose();_infoFont?.Dispose();}base.Dispose(disposing);}
}

internal sealed class V269StartupCard : Panel
{
    private readonly Label _title,_hint;
    private readonly string _glyph;
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    private bool _selected;
    private Font? _titleFont,_hintFont;
    internal Button Command {get;}
    internal StartupItem? Item {get;}
    internal bool? GfEnabled {get;}
    internal V269StartupCard(string name,string hint,string glyph,string command,bool allowed,StartupItem? item,bool? gfEnabled)
    {
        Item=item;GfEnabled=gfEnabled;_glyph=glyph;
        Name=item is null?"GFStartupTrayCard":"StartupEntryCard";AccessibleName=name;AccessibleRole=AccessibleRole.Grouping;
        TabStop=true;SetStyle(ControlStyles.Selectable|ControlStyles.SupportsTransparentBackColor|
            ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        BackColor=Color.Transparent;
        _title=new Label{Text=name,UseMnemonic=false,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleLeft};
        _hint=new Label{Text=hint,UseMnemonic=false,BackColor=Color.Transparent,TextAlign=ContentAlignment.MiddleLeft};
        _title.Click+=(_,_)=>OnClick(EventArgs.Empty);_hint.Click+=(_,_)=>OnClick(EventArgs.Empty);
        Command=new V269CleanupButton(command,allowed?"power":"lock")
        {Enabled=allowed,AccessibleName=command+": "+name,Margin=Padding.Empty};
        Controls.Add(_title);Controls.Add(_hint);Controls.Add(Command);
        Size=new Size(240,114);ApplyOptions(_options);
    }
    private int S(int n)=>V269AppTable.Scale(n,_options.InterfacePercent,DeviceDpi);
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        _options=options.Normalize();
        float size=9.75f*_options.TextPercent/100f;
        if(_titleFont is null||Math.Abs(_titleFont.Size-size)>.01f)
        {
            var a=_titleFont;var b=_hintFont;
            _titleFont=new Font("Segoe UI",size,FontStyle.Bold);_hintFont=new Font("Segoe UI",size*.87f);
            _title.Font=_titleFont;_hint.Font=_hintFont;a?.Dispose();b?.Dispose();
        }
        var p=V269VisualTokens.ForTheme(_options.Theme);
        _title.ForeColor=p.Text;_hint.ForeColor=p.SecondaryText;
        ((V269CleanupButton)Command).ApplyOptions(_options);Arrange();Invalidate(true);
    }
    internal void SetSelected(bool selected){if(_selected!=selected){_selected=selected;Invalidate(true);}}
    private int TitleHeight(int width)=>Math.Max(S(25),TextRenderer.MeasureText(_title.Text,_title.Font,
        new Size(Math.Max(1,width-S(55)),1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height);
    internal int HeightForWidth(int width)=>Math.Max(S(114),S(30)+TitleHeight(width)+_hint.Font.Height+Math.Max(S(30),Command.Font.Height+S(8)));
    private void Arrange()
    {
        if(_title is null||_hint is null||Command is null)return;
        int pad=S(12),left=pad+S(29),h=TitleHeight(Width),bh=Math.Max(S(30),Command.Font.Height+S(8));
        _title.Bounds=new Rectangle(left,pad,Math.Max(1,Width-left-pad),h);
        _hint.Bounds=new Rectangle(pad,pad+h+S(3),Math.Max(1,Width-pad*2),_hint.Font.Height+S(3));
        int bw=Math.Min(Math.Max(1,Width-2*pad),TextRenderer.MeasureText(Command.Text,Command.Font).Width+S(30));
        Command.Bounds=new Rectangle(Math.Max(0,Width-pad-bw),Math.Max(pad,Height-pad-bh),bw,bh);
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);Arrange();}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)Focus();base.OnMouseDown(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(Width<3||Height<3)return;
        var (top,bottom,border)=_options.Theme switch
        {
            V269Theme.Dark=>_selected?("#50565d","#41464c","#b4bdc6"):("#33373b","#2b2f33","#555c63"),
            V269Theme.Claro=>_selected?("#c2e0f5","#c2e0f5","#4a9cc9"):("#e2f2fd","#e2f2fd","#8ebad6"),
            _=>_selected?("#113048","#113048","#5e95bb"):("#0d2437","#0b2031","#30516b")
        };
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        var rect=new Rectangle(0,0,Width-1,Height-1);
        using var path=ModernButton.RoundRect(rect,S(9));
        using var fill=new LinearGradientBrush(rect,V269AppTable.H(top),V269AppTable.H(bottom),LinearGradientMode.Vertical);
        using var line=new Pen(V269AppTable.H(border));
        g.FillPath(fill,path);g.DrawPath(line,path);
        V269CleanupGlyph.Draw(g,_glyph,new Rectangle(S(12),S(14),S(21),S(21)),_options.Theme);
        if(Focused&&ShowFocusCues&&Width>8&&Height>8)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-8,Height-8));
    }
    protected override void Dispose(bool disposing){if(disposing){_titleFont?.Dispose();_hintFont?.Dispose();}base.Dispose(disposing);}
}

// Usa Button real e mantém o nome e comportamento acessíveis existentes.
internal sealed class V269SystemSwitch : Button
{
    internal bool? State {get;private set;}
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    internal V269SystemSwitch(string name,string caption)
    {
        Name=name;AccessibleName=caption;Text=caption;BackColor=Color.Transparent;FlatStyle=FlatStyle.Flat;
        FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.SupportsTransparentBackColor|
            ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
    }
    internal void ApplyOptions(V269AppearanceOptions options){_options=options.Normalize();Invalidate();}
    internal void SetState(bool? state){State=state;AccessibleDescription=state is true?"Ativada":state is false?"Desativada":"Indisponível";Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<6||Height<6)return;
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        var p=V269VisualTokens.ForTheme(_options.Theme);
        var rect=new Rectangle(1,1,Width-3,Height-3);
        using var shape=V269Shell.Rounded(rect,rect.Height/2);
        Color fill=State==true?p.Accent:p.Border;
        using var b=new SolidBrush(fill);using var line=new Pen(p.SecondaryText);
        g.FillPath(b,shape);g.DrawPath(line,shape);
        int n=Math.Max(1,Height-8),x=State==true?Width-n-4:4;
        using var knob=new SolidBrush(State is null? p.SecondaryText:Color.White);
        g.FillEllipse(knob,x,(Height-n)/2,n,n);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle);
    }
}
