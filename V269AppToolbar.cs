using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GerenciadorAplicativos;

// Campo nativo sempre visível: preserva teclado, seleção, IME e acessibilidade.
internal sealed class V269AppSearchEditor:TextBox
{
    internal Color CueInk=SystemColors.GrayText;
    internal bool ShowsCue=>TextLength==0&&!Focused&&PlaceholderText.Length>0;
    protected override void WndProc(ref Message m)
    {
        const int WM_PRINTCLIENT=0x0318;
        base.WndProc(ref m);
        // DrawToBitmap pede WM_PRINTCLIENT ao HWND. O Windows nem sempre
        // desenha o cue banner nesse caminho, embora o mostre na tela.
        // Desenhar apenas a dica (nunca modificar o texto real do controle).
        if(m.Msg==WM_PRINTCLIENT&&m.WParam!=IntPtr.Zero&&ShowsCue&&ClientSize.Width>1&&ClientSize.Height>1)
        {
            using var g=Graphics.FromHdc(m.WParam);
            using var fill=new SolidBrush(BackColor);g.FillRectangle(fill,ClientRectangle);
            TextRenderer.DrawText(g,PlaceholderText,Font,ClientRectangle,CueInk,
                TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|
                TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        }
    }
}
internal sealed class V269AppSearchBox : Panel
{
    private readonly V269AppSearchEditor _editor;
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    private Font? _font;
    internal V269AppSearchBox(V269AppSearchEditor editor)
    {
        Name="V269AppsSearchFrame";_editor=editor;
        Dock=DockStyle.Fill;Margin=new Padding(0,1,9,1);
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|
            ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;_editor.BorderStyle=BorderStyle.None;
        _editor.Dock=DockStyle.None;Controls.Add(_editor);
        Resize+=(_,_)=>ArrangeEditor();
        _editor.GotFocus+=(_,_)=>Invalidate();
        _editor.LostFocus+=(_,_)=>Invalidate();
    }
    private int S(int x)=>V269AppTable.Scale(x,_options.InterfacePercent,DeviceDpi);
    private (Color Surface,Color Border,Color Ink) Colors()=>_options.Theme switch
    {
        V269Theme.Claro=>(V269AppTable.H("#eaf5fe"),V269AppTable.H("#8eafc7"),V269AppTable.H("#17374c")),
        V269Theme.Dark=>(V269AppTable.H("#353a40"),V269AppTable.H("#737c85"),V269AppTable.H("#d1d6db")),
        _=>(V269AppTable.H("#102638"),V269AppTable.H("#416783"),V269AppTable.H("#dfedf7"))
    };
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        _options=options.Normalize();var palette=Colors();
        float size=9f*_options.TextPercent/100f;
        if(_font is null||Math.Abs(_font.Size-size)>.01f)
        {var old=_font;_font=new Font("Segoe UI",size);_editor.Font=_font;old?.Dispose();}
        _editor.BackColor=palette.Surface;_editor.ForeColor=palette.Ink;
        _editor.CueInk=V269AppTable.H(_options.Theme switch
        {V269Theme.Claro=>"#4c6475",V269Theme.Dark=>"#9fa8b0",_=>"#9ab0c2"});
        ArrangeEditor();_editor.Invalidate();Invalidate();
    }
    private void ArrangeEditor()
    {
        int x=S(31);_editor.Bounds=new Rectangle(x,Math.Max(1,(Height-_editor.PreferredHeight)/2),
            Math.Max(1,Width-x-S(9)),_editor.PreferredHeight);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<3||Height<3)return;
        var c=Colors();e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=ModernButton.RoundRect(new Rectangle(0,0,Width-1,Height-1),S(8));
        using var fill=new SolidBrush(c.Surface);using var border=new Pen(_editor.Focused?V269VisualTokens.ForTheme(_options.Theme).Accent:c.Border);
        e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(border,shape);
        int n=S(11),x=S(11),y=(Height-n)/2;
        using var ink=new Pen(c.Ink,1.7f){EndCap=LineCap.Round};
        e.Graphics.DrawEllipse(ink,x,y,n,n);e.Graphics.DrawLine(ink,x+n-1,y+n-1,x+n+S(4),y+n+S(4));
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left)_editor.Focus();}
    protected override void Dispose(bool disposing){if(disposing)_font?.Dispose();base.Dispose(disposing);}
}
internal sealed class V269AppToolbarButton:Button
{
    private readonly bool _primary;
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    private Font? _font;private bool _hover,_pressed,_filterSelected;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool FilterSelected {get=>_filterSelected;set{if(_filterSelected!=value){_filterSelected=value;Invalidate();}}}
    internal V269AppToolbarButton(string caption,bool primary=false)
    {
        _primary=primary;Text=caption;AccessibleName=caption;Dock=DockStyle.Fill;FlatStyle=FlatStyle.Flat;
        FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|
            ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;
    }
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        _options=options.Normalize();
        float size=9f*_options.TextPercent/100f;
        if(_font is null||Math.Abs(_font.Size-size)>.01f)
        {var old=_font;_font=new Font("Segoe UI",size,FontStyle.Bold);Font=_font;old?.Dispose();}
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<3||Height<3)return;var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        Color H(string s)=>V269AppTable.H(s);
        var (surface,border,ink)=_options.Theme switch
        {
            V269Theme.Claro=>(_primary?H("#2579b4"):H("#e2f0fb"),H("#8eafc7"),_primary?Color.White:H("#173d59")),
            V269Theme.Dark=>(_primary?H("#515961"):H("#3a3f45"),_primary?H("#b7c0c8"):H("#68717a"),H("#dce0e4")),
            _=>(_primary?H("#205f95"):H("#102638"),_primary?H("#6ea7cf"):H("#416783"),H("#dfedf7"))
        };
        if(_filterSelected)
        {
            var p=V269VisualTokens.ForTheme(_options.Theme);
            surface=p.Selection;border=p.Accent;ink=_options.Theme==V269Theme.Claro?H("#20435f"):p.Text;
        }
        if(!Enabled)ink=V269VisualTokens.ForTheme(_options.Theme).SecondaryText;
        else if(_hover)surface=ControlPaint.Light(surface,_pressed ? .03f : .15f);
        int S(int x)=>V269AppTable.Scale(x,_options.InterfacePercent,DeviceDpi);
        using var shape=ModernButton.RoundRect(new Rectangle(0,0,Width-1,Height-1),S(8));
        using var fill=new SolidBrush(surface);using var line=new Pen(border);g.FillPath(fill,shape);g.DrawPath(line,shape);
        int left=S(_primary?8:4);
        if(_primary&&Width>S(70)&&Text=="Verificar")
        {
            int n=S(15),y=(Height-n)/2;
            using var pen=new Pen(ink,1.7f){StartCap=LineCap.Round,EndCap=LineCap.Round};
            g.DrawArc(pen,left,y,n,n,-35,300);g.DrawLines(pen,new Point[]{new(left+n,y),new(left+n,y+S(6)),new(left+n-S(6),y+S(6))});
            left+=n+S(4);
        }
        TextRenderer.DrawText(g,Text,Font,new Rectangle(left,0,Math.Max(1,Width-left-S(_primary?6:4)),Height),ink,
            TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-6,Height-6));
    }
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);_hover=true;Invalidate();}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);_hover=_pressed=false;Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);_pressed=true;Invalidate();}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);_pressed=false;Invalidate();}
    protected override void Dispose(bool disposing){if(disposing)_font?.Dispose();base.Dispose(disposing);}
}

// Seletor exclusivo de Aplicativos: o HWND continua sendo ComboBox nativo
// para teclado, lista aberta e acessibilidade. Em WM_PRINT desenhamos uma
// única vez o seletor fechado; o desenho padrão sobrepunha texto e seta branca.
internal sealed class V269AppSortCombo:ComboBox
{
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    internal V269AppSortCombo()
    {
        DropDownStyle=ComboBoxStyle.DropDownList;DrawMode=DrawMode.OwnerDrawFixed;
        IntegralHeight=false;FlatStyle=FlatStyle.Flat;
    }
    private int S(int n)=>V269AppTable.Scale(n,_options.InterfacePercent,DeviceDpi);
    private (Color Top,Color Bottom,Color Border,Color Ink) Colors()=>_options.Theme switch
    {
        V269Theme.Claro=>(V269AppTable.H("#eaf5fe"),V269AppTable.H("#eaf5fe"),V269AppTable.H("#8eafc7"),V269AppTable.H("#17374c")),
        V269Theme.Dark=>(V269AppTable.H("#383d43"),V269AppTable.H("#30353a"),V269AppTable.H("#737c85"),V269AppTable.H("#d1d6db")),
        _=>(V269AppTable.H("#0d2233"),V269AppTable.H("#0b1d2b"),V269AppTable.H("#3d5a70"),V269AppTable.H("#e1edf7"))
    };
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        _options=options.Normalize();var c=Colors();
        BackColor=c.Top;ForeColor=c.Ink;
        ItemHeight=Math.Max(Font.Height+2,S(42)-6);DropDownHeight=ItemHeight*5+4;
        Invalidate();
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if(e.Index<0||e.Index>=Items.Count)return;
        var c=Colors();
        var palette=V269VisualTokens.ForTheme(_options.Theme);
        using var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?palette.Selection:c.Top);
        e.Graphics.FillRectangle(fill,e.Bounds);
        var text=e.Bounds;text.Inflate(-S(5),0);
        TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,text,c.Ink,
            TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
        if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
    }
    private void PaintClosed(Graphics g)
    {
        if(Width<3||Height<3)return;
        var saved=g.Save();
        try
        {
            g.SetClip(ClientRectangle,CombineMode.Intersect);
            var c=Colors();var bounds=new Rectangle(0,0,Width-1,Height-1);
            using var fill=new LinearGradientBrush(bounds,c.Top,c.Bottom,LinearGradientMode.Vertical);
            g.FillRectangle(fill,ClientRectangle);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using var shape=ModernButton.RoundRect(bounds,S(8));
            using var outline=new Pen(Focused?V269VisualTokens.ForTheme(_options.Theme).Accent:c.Border);
            g.DrawPath(outline,shape);
            int x=Width-S(11),y=Height/2;
            using var arrow=new Pen(c.Ink,1.5f);
            g.DrawLines(arrow,new Point[]{new(x-S(4),y-S(2)),new(x,y+S(2)),new(x+S(4),y-S(2))});
            TextRenderer.DrawText(g,Text,Font,new Rectangle(S(10),0,Math.Max(1,Width-S(32)),Height),c.Ink,
                TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|
                TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.PreserveGraphicsClipping);
            if(Focused&&ShowFocusCues&&Width>6&&Height>6)
                ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-6,Height-6));
        }
        finally{g.Restore(saved);}
    }
    protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
    protected override void WndProc(ref Message m)
    {
        const int Paint=0x000f,Print=0x0317,PrintClient=0x0318;
        if((m.Msg is Print or PrintClient)&&m.WParam!=IntPtr.Zero&&IsHandleCreated&&!IsDisposed)
        {
            using var g=Graphics.FromHdc(m.WParam);PaintClosed(g);
            m.Result=IntPtr.Zero;return;
        }
        base.WndProc(ref m);
        if(m.Msg==Paint&&IsHandleCreated&&!IsDisposed)
        {
            using var g=Graphics.FromHwnd(Handle);PaintClosed(g);
        }
    }
}
