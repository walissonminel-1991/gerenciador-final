using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace GerenciadorAplicativos;

internal sealed record V269AppPreviousResult(Guid Id,string Title,string Description);
internal sealed record V269AppDetailModel(string Name,string Publisher,
    IReadOnlyList<(string Label,string Value)> Fields,string Reasons,string IconSource="",V269MsixIconSource? MsixIcon=null,string Description="",
    IReadOnlyList<V269AppPreviousResult>? PreviousResults=null,Action<Guid>? DismissResult=null);

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

// Linha contextual baseada em gf-history-row. A ação descarta apenas
// a apresentação, e nunca um evento do motor ou do registro local.
internal sealed class V269AppHistoryRow : TableLayoutPanel
{
    private readonly Label _symbol=new(){Text="•",AutoSize=true,Margin=Padding.Empty};
    private readonly Label _heading=new(){AutoSize=true,UseMnemonic=false,Margin=Padding.Empty};
    private readonly Label _message=new(){AutoSize=true,UseMnemonic=false,Margin=Padding.Empty};
    private readonly Button _dismiss=new(){Name="V269AppDismissResult",Text="Dispensar",FlatStyle=FlatStyle.Flat,
        AutoSize=false,UseVisualStyleBackColor=false,Anchor=AnchorStyles.Top|AnchorStyles.Right,Margin=Padding.Empty};
    private Font? _headingFont,_messageFont;
    private Color _line;
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
        _symbol.ForeColor=_message.ForeColor=palette.SecondaryText;_line=palette.Border;
        _dismiss.BackColor=palette.Card;_dismiss.ForeColor=palette.Text;
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
    private readonly TableLayoutPanel _rows=new(){Dock=DockStyle.Top,AutoSize=true,
        AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Margin=Padding.Empty};
    private readonly Label _heading=new(){Text="Resultados anteriores",AutoSize=true,
        Dock=DockStyle.Top,Margin=Padding.Empty};
    private Font? _headingFont;
    private bool _measuring;
    private int _percent=100;
    private V269Theme _theme;
    internal V269AppOperationHistory(IReadOnlyList<V269AppPreviousResult> results,Action<Guid> dismiss)
    {
        Name="V269AppOperationHistory";AccessibleName="Resultados anteriores";
        Dock=DockStyle.Bottom;AutoScroll=true;Margin=Padding.Empty;
        _rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        _rows.Controls.Add(_heading,0,_rows.RowCount++);
        _heading.Paint+=(_,e)=>{
            using var pen=new Pen(V269AppDetailStyle.Divider(_theme));
            e.Graphics.DrawLine(pen,0,0,_heading.Width-1,0);
        };
        for(int i=0;i<results.Count;i++)
        {
            var row=new V269AppHistoryRow(results[i],dismiss,i<results.Count-1);
            _rows.Controls.Add(row,0,_rows.RowCount++);
        }
        Controls.Add(_rows);
        SizeChanged+=(_,_)=>MeasureRows();
        FontChanged+=(_,_)=>ApplyFonts();
        ApplyFonts();
    }
    private void ApplyFonts()
    {
        if(IsDisposed||Disposing)return;
        var old=_headingFont;_headingFont=new Font(Font,FontStyle.Bold);
        _heading.Font=_headingFont;old?.Dispose();MeasureRows();
    }
    internal void ApplyAppearance(V269Theme theme,int percent,Font font)
    {
        _theme=theme;_percent=percent;Font=font;
        var palette=V269AppDetailStyle.Palette(theme);
        BackColor=_rows.BackColor=palette.RightPanel;_heading.ForeColor=palette.Text;
        foreach(var row in _rows.Controls.OfType<V269AppHistoryRow>())row.ApplyPalette(palette);
        MeasureRows();_heading.Invalidate();
    }
    private void MeasureRows()
    {
        if(_measuring||IsDisposed||Disposing)return;_measuring=true;
        try
        {
            int S(int n)=>V269AppTable.Scale(n,_percent,DeviceDpi);
            _heading.Padding=new Padding(0,S(12),0,S(9));
            int width=Math.Max(1,ClientSize.Width-SystemInformation.VerticalScrollBarWidth);
            _rows.MaximumSize=new Size(width,0);
            _heading.MaximumSize=new Size(width,0);
            foreach(var row in _rows.Controls.OfType<V269AppHistoryRow>())row.ApplyMetrics(_percent,width);
        }
        finally{_measuring=false;}
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
            AddFull("Descrição",true,"V269AppDescriptionHeading");
            AddFull(string.IsNullOrWhiteSpace(model.Description)?"Não informada pela fonte local.":model.Description,false);
            var offer=model.Fields.FirstOrDefault(field=>field.Label=="Oferta").Value;
            string note=model.Reasons;
            if(!string.IsNullOrWhiteSpace(offer)&&!offer.Equals("não informada",StringComparison.OrdinalIgnoreCase))
                note=$"Oferta: {offer}. {note}";
            // A nota única do HTML preserva o motivo real; informações
            // completas continuam no modelo e na descrição acessível.
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
                    int max=label.Name=="V269AppHistoryHeading"?width:
                        _table.GetColumnSpan(label)==2?width-6:
                        (int)(width*(column==0 ? .4 : .6))-6;
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
        int S(int n)=>V269AppTable.Scale(n,percent,DeviceDpi);
        int size=S(48),gap=S(10);
        _identity.Margin=new Padding(0,S(4),0,S(13));
        foreach(Label label in _table.Controls.OfType<Label>())
            if(_table.GetColumnSpan(label)==2)ApplyFullMetrics(label);
            else label.Margin=new Padding(0,S(11),0,S(11));
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
