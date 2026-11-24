using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GerenciadorAplicativos;

internal sealed record V269AppDetailModel(string Name,string Publisher,
    IReadOnlyList<(string Label,string Value)> Fields,string Reasons,string IconSource="",V269MsixIconSource? MsixIcon=null,string Description="");

// Superfícies exclusivas do painel estruturado, derivadas da cascata final
// do HTML. Os motores e painéis de confirmação/progresso não usam estas cores.
internal static class V269AppDetailStyle
{
    internal static V269Palette Palette(V269Theme theme)
    {
        var p=V269VisualTokens.ForTheme(theme);
        Color H(string s)=>V269AppTable.H(s);
        return theme switch
        {
            V269Theme.Dark=>p with {RightPanel=H("#2a2e32"),Border=H("#60666d"),
                Text=H("#dce0e4"),SecondaryText=H("#aab4bd"),Card=H("#30353a")},
            V269Theme.Claro=>p with {RightPanel=H("#eaf4fc"),Border=H("#9bbacf")},
            _=>p with {RightPanel=H("#0b2032"),Border=H("#25445f"),Card=H("#11283b")}
        };
    }
    internal static Color Divider(V269Theme theme)=>V269AppTable.H(theme switch
    {V269Theme.Dark=>"#3c4249",V269Theme.Claro=>"#b7d1e2",_=>"#1c354b"});
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
    private Font? _identityFont;
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
            if(_model is null||e.Row>_visibleFieldCount)return;
            using var line=new Pen(V269AppDetailStyle.Divider(_theme));
            e.Graphics.DrawLine(line,e.CellBounds.Left,e.CellBounds.Bottom-1,e.CellBounds.Right-1,e.CellBounds.Bottom-1);
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
        _visibleFieldCount=0;
        AccessibleDescription=model is null?"":string.Join(Environment.NewLine,
            new[]{model.Name,model.Publisher}.Concat(model.Fields.Select(field=>$"{field.Label}: {field.Value}"))
                .Concat(new[]{model.Description,model.Reasons,"Enter ou botão direito: ações disponíveis."}));
        _scroll.Value=0;
        _table.SuspendLayout();
        try
        {
            foreach(Control child in _table.Controls.Cast<Control>().Where(c=>c!=_identity).ToArray())child.Dispose();
            _table.RowStyles.Clear();_table.RowCount=1;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _identity.Visible=model is not null;
            _name.Text=model?.Name??"";_publisher.Text=model?.Publisher??"";
            _icon.SetIdentity(model?.Name??"",model?.IconSource??"",model?.MsixIcon);
            if(model is null)return;
            Label Make(string value,bool strong)
            {
                var label=new Label{Text=value,AutoSize=true,
                    Margin=new Padding(3,3,3,3),ForeColor=strong?_palette.Text:_palette.SecondaryText,
                    AccessibleName=value,Tag=strong,UseMnemonic=false};
                label.MouseWheel+=ScrollContent;return label;
            }
            void AddFull(string value,bool strong)
            {
                int row=_table.RowCount++;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var label=Make(value,strong);_table.Controls.Add(label,0,row);_table.SetColumnSpan(label,2);
            }
            // A especificação HTML apresenta quatro campos. Versão e data
            // já estão na tabela; a oferta real permanece na nota quando existe.
            foreach(var field in model.Fields.Where(field=>field.Label is "Tipo" or "Origem" or "Arquitetura" or "Local"))
            {
                _visibleFieldCount++;
                int row=_table.RowCount++;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _table.Controls.Add(Make(field.Label,false),0,row);
                var value=Make(field.Value,true);
                value.Anchor=AnchorStyles.Top|AnchorStyles.Right;
                value.TextAlign=ContentAlignment.TopRight;
                _table.Controls.Add(value,1,row);
            }
            AddFull("Descrição",true);
            AddFull(string.IsNullOrWhiteSpace(model.Description)?"Não informada pela fonte local.":model.Description,false);
            var offer=model.Fields.FirstOrDefault(field=>field.Label=="Oferta").Value;
            string note=model.Reasons;
            if(!string.IsNullOrWhiteSpace(offer)&&!offer.Equals("não informada",StringComparison.OrdinalIgnoreCase))
                note=$"Oferta: {offer}. {note}";
            // A nota única do HTML preserva o motivo real; informações
            // completas continuam no modelo e na descrição acessível.
            AddFull(note,false);
        }
        finally{_table.ResumeLayout(true);FitLabels();}
    }
    private void FitLabels()
    {
        if(_arranging||IsDisposed||Disposing)return;_arranging=true;
        try
        {
            int barWidth=Math.Max(12,_scroll.Width);
            int height=Math.Max(1,ClientSize.Height);
            int Measure(int width)
            {
                _table.Width=width;
                _identity.MaximumSize=new Size(width,0);
                int identityText=Math.Max(20,width-(int)_identity.ColumnStyles[0].Width-12);
                _name.MaximumSize=_publisher.MaximumSize=new Size(identityText,0);
                foreach(Label label in _table.Controls.OfType<Label>())
                {
                    int column=_table.GetColumn(label);
                    int max=_table.GetColumnSpan(label)==2?width-6:
                        (int)(width*(column==0 ? .4 : .6))-6;
                    label.MaximumSize=new Size(Math.Max(20,max),0);
                }
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
            Math.Abs(_identityFont.SizeInPoints-Font.SizeInPoints)<.01f)return;
        var old=_identityFont;_identityFont=new Font(Font,FontStyle.Bold);
        _name.Font=_identityFont;old?.Dispose();FitLabels();
    }
    internal void ApplyInterfaceScale(int percent)
    {
        _interfacePercent=percent;
        int size=V269AppTable.Scale(48,percent,DeviceDpi),gap=V269AppTable.Scale(8,percent,DeviceDpi);
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
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if(disposing)_identityFont?.Dispose();
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
    internal V269AppDetailIcon()
    {
        Name="V269AppDetailIcon";AccessibleRole=AccessibleRole.Graphic;TabStop=false;
        Size=new Size(48,48);Margin=new Padding(0,3,0,3);
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|
            ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;
    }
    internal void SetIdentity(string name,string source,V269MsixIconSource? msix)
    {
        _source=source;_msix=msix;_initial=string.IsNullOrWhiteSpace(name)?"?":name.Trim()[0].ToString().ToUpperInvariant();
        AccessibleName="Ícone do aplicativo "+name;Invalidate();
    }
    internal void ApplyPalette(V269Palette palette){_palette=palette;Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<3||Height<3)return;
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=ModernButton.RoundRect(new Rectangle(0,0,Width-1,Height-1),Math.Max(2,Width/6));
        using var fill=new SolidBrush(_palette.Card);using var border=new Pen(_palette.Border);
        g.FillPath(fill,shape);g.DrawPath(border,shape);
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
