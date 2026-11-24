using System.Drawing;
using System.Windows.Forms;
namespace GerenciadorAplicativos;

internal sealed record V269AppDetailModel(string Name,string Publisher,
    IReadOnlyList<(string Label,string Value)> Fields,string Reasons);

internal sealed class V269AppDetailPanel : Panel
{
    private readonly TableLayoutPanel _table=new(){Dock=DockStyle.Top,AutoSize=true,
        AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,Margin=Padding.Empty};
    private V269Palette _palette=V269VisualTokens.ForTheme(V269Theme.AzulGf);
    private V269AppDetailModel? _model;
    private bool _arranging;
    internal V269AppDetailPanel()
    {
        Name="V269StructuredAppDetails";AccessibleName="Informações verificáveis e elegibilidade do aplicativo";
        Dock=DockStyle.Fill;AutoScroll=true;Margin=Padding.Empty;Visible=false;
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));
        Controls.Add(_table);SizeChanged+=(_,_)=>FitLabels();
    }
    internal void Present(V269AppDetailModel? model)
    {
        if(Equals(_model,model))return;
        _model=model;
        _table.SuspendLayout();
        try
        {
            while(_table.Controls.Count>0)_table.Controls[0].Dispose();
            _table.RowStyles.Clear();_table.RowCount=0;
            if(model is null)return;
            Label Make(string value,bool strong)=>new(){Text=value,AutoSize=true,
                Margin=new Padding(3,5,3,5),ForeColor=strong?_palette.Text:_palette.SecondaryText,
                AccessibleName=value,Tag=strong,UseMnemonic=false};
            void AddFull(string value,bool strong)
            {
                int row=_table.RowCount++;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var label=Make(value,strong);_table.Controls.Add(label,0,row);_table.SetColumnSpan(label,2);
            }
            AddFull(model.Name,true);AddFull(model.Publisher,false);
            foreach(var field in model.Fields)
            {
                int row=_table.RowCount++;_table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _table.Controls.Add(Make(field.Label,false),0,row);
                _table.Controls.Add(Make(field.Value,true),1,row);
            }
            AddFull("Elegibilidade e evidência",true);
            AddFull(model.Reasons,false);
            AddFull("Enter ou botão direito: ações disponíveis.",false);
        }
        finally{_table.ResumeLayout(true);FitLabels();}
    }
    private void FitLabels()
    {
        if(_arranging||IsDisposed)return;_arranging=true;
        try
        {
            int width=Math.Max(60,ClientSize.Width-SystemInformation.VerticalScrollBarWidth-6);
            _table.Width=width;
            foreach(Label label in _table.Controls.OfType<Label>())
            {
                int column=_table.GetColumn(label);
                int max=_table.GetColumnSpan(label)==2?width-6:
                    (int)(width*(column==0 ? .4 : .6))-6;
                label.MaximumSize=new Size(Math.Max(20,max),0);
            }
        }
        finally{_arranging=false;}
    }
    internal void ApplyPalette(V269Palette palette)
    {
        _palette=palette;BackColor=_table.BackColor=palette.RightPanel;
        foreach(Label label in _table.Controls.OfType<Label>())
            label.ForeColor=label.Tag is true?palette.Text:palette.SecondaryText;
    }
}
