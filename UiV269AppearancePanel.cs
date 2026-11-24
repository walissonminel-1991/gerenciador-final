using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

/// <summary>Preferências visuais locais, sem telemetria ou alterações no Windows.</summary>
internal readonly record struct V269AppearanceOptions(
    V269Theme Theme, int InterfacePercent, int TextPercent, bool AnimationsEnabled)
{
    internal static V269AppearanceOptions Default => new(V269Theme.AzulGf, 100, 100, true);

    internal V269AppearanceOptions Normalize() => new(
        Enum.IsDefined(Theme) ? Theme : V269Theme.AzulGf,
        V269VisualTokens.InterfacePercent(InterfacePercent),
        V269VisualTokens.TextPercent(TextPercent), AnimationsEnabled);

    internal bool CanAnimate(bool reducedMotion) => AnimationsEnabled && !reducedMotion;
}

internal static class V269AppearanceSettingsStore
{
    internal static V269AppearanceOptions Read(string path)
    {
        try
        {
            using var stream=File.OpenRead(path);
            if(stream.Length>4096)return V269AppearanceOptions.Default;
            using var document=JsonDocument.Parse(stream);
            var root=document.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||
               !root.TryGetProperty("Theme",out var theme)||!theme.TryGetInt32(out int t)||
               !root.TryGetProperty("InterfacePercent",out var ui)||!ui.TryGetInt32(out int u)||
               !root.TryGetProperty("TextPercent",out var text)||!text.TryGetInt32(out int p)||
               !root.TryGetProperty("AnimationsEnabled",out var animations)||
               animations.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return V269AppearanceOptions.Default;
            return new V269AppearanceOptions((V269Theme)t,u,p,animations.GetBoolean()).Normalize();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or
            InvalidOperationException or System.Security.SecurityException)
        {return V269AppearanceOptions.Default;}
    }

    internal static void Write(string path,V269AppearanceOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temporary,JsonSerializer.Serialize(options.Normalize()));
            File.Move(temporary,path,overwrite:true);
        }
        finally{File.Delete(temporary);}
    }
}

/// <summary>Controles acessíveis de aparência V2.6.9, sem operações de manutenção.</summary>
internal sealed class V269AppearancePanel : V269SettingsCard
{
    private readonly Label _title=TextLabel("V269AppearanceTitle","Aparência");
    private readonly Label _themeLabel=TextLabel("V269ThemeLabel","Tema");
    private readonly Label _uiLabel=TextLabel("V269UiScaleLabel","Tamanho da interface");
    private readonly Label _textLabel=TextLabel("V269TextScaleLabel","Tamanho do texto");
    private readonly Label _uiValue=TextLabel("V269UiPercent","100%");
    private readonly Label _textValue=TextLabel("V269TextPercent","100%");
    private readonly Label _uiMin=TextLabel("V269UiMinimum","90%"),_uiMax=TextLabel("V269UiMaximum","110%");
    private readonly Label _textMin=TextLabel("V269TextMinimum","90%"),_textMax=TextLabel("V269TextMaximum","115%");
    private readonly Label _motionTitle=TextLabel("V269MotionTitle","Animações da interface");
    private readonly Label _motionDescription=TextLabel("V269MotionDescription","Transições discretas em todas as abas, sem afetar operações.");
    private readonly ComboBox _theme;
    private readonly TrackBar _uiScale,_textScale;
    private readonly V269SettingsSwitch _animations=new("V269Animations","Animações da interface",true);
    private bool _updating;
    private Rectangle _motionRow;
    internal event EventHandler? AppearanceChanged;

    internal V269AppearancePanel()
    {
        Name="V269AppearancePanel";AccessibleName="Aparência da interface";
        _theme=new V269SettingsCombo{Name="V269ThemeChoice",DropDownStyle=ComboBoxStyle.DropDownList,
            TabStop=true,AccessibleName="Tema da interface",IntegralHeight=false,FlatStyle=FlatStyle.Flat};
        _theme.Items.AddRange(["Azul GF (padrão)","Dark — Preto e grafite","Claro — azul suave"]);
        TrackBar Scale(string name,string caption,int maximum)=>new V269SettingsRange{Name=name,AccessibleName=caption,
            Minimum=90,Maximum=maximum,Value=100,SmallChange=1,LargeChange=5,TickStyle=TickStyle.None,
            AutoSize=false,TabStop=true};
        _uiScale=Scale("V269UiScale","Tamanho da interface",110);
        _textScale=Scale("V269TextScale","Tamanho dos textos",115);
        foreach(var label in new[]{_uiValue,_textValue,_uiMax,_textMax})label.TextAlign=ContentAlignment.MiddleRight;
        Controls.AddRange([_title,_themeLabel,_theme,_uiLabel,_uiValue,_uiScale,_uiMin,_uiMax,
            _textLabel,_textValue,_textScale,_textMin,_textMax,_motionTitle,_motionDescription,_animations]);
        SetOptions(V269AppearanceOptions.Default);
        _theme.SelectedIndexChanged+=(_,_)=>Changed();
        _uiScale.ValueChanged+=(_,_)=>Changed();
        _textScale.ValueChanged+=(_,_)=>Changed();
        _animations.CheckedChanged+=(_,_)=>Changed();
    }

    internal V269AppearanceOptions GetOptions()=>new((V269Theme)Math.Max(0,_theme.SelectedIndex),_uiScale.Value,_textScale.Value,_animations.Checked);
    internal void SetOptions(V269AppearanceOptions options)
    {
        options=options.Normalize();_updating=true;
        try
        {
            _theme.SelectedIndex=(int)options.Theme;_uiScale.Value=options.InterfacePercent;
            _textScale.Value=options.TextPercent;_animations.Checked=options.AnimationsEnabled;
            RefreshLabels();ApplyOptions(options);
        }
        finally{_updating=false;}
    }
    internal override void ApplyOptions(V269AppearanceOptions options)
    {
        base.ApplyOptions(options);
        _theme.Font=F(14);
        ((V269SettingsCombo)_theme).ApplyOptions(Options);
        ((V269SettingsRange)_uiScale).ApplyOptions(Options);
        ((V269SettingsRange)_textScale).ApplyOptions(Options);
        _animations.Theme=Options.Theme;
        if(Width>=80)ArrangeCard(Width);
    }

    internal int ArrangeCard(int width)
    {
        int left=S(14), right=Math.Max(left+1,width-S(14)), inner=right-left;
        StyleLabel(_title,15,true);_title.Bounds=new Rectangle(left+S(33),S(12),Math.Max(1,inner-S(33)),S(24));
        int gap=S(7),cols=inner>=S(620)?3:inner>=S(260)?2:1,cell=Math.Max(1,(inner-gap*(cols-1))/cols);
        int y=S(47),fieldHeight=S(64);
        Rectangle Field(int i)=>new(left+(i%cols)*(cell+gap),y+(i/cols)*(fieldHeight+gap),i%cols==cols-1?inner-(i%cols)*(cell+gap):cell,fieldHeight);
        foreach(var label in new[]{_themeLabel,_uiLabel,_textLabel})StyleLabel(label,11,false,true);
        foreach(var label in new[]{_uiValue,_textValue})StyleLabel(label,11,true);
        foreach(var label in new[]{_uiMin,_uiMax,_textMin,_textMax})StyleLabel(label,10,false,true);
        var theme=Field(0);_themeLabel.Bounds=new Rectangle(theme.X,theme.Y,theme.Width,S(14));
        _theme.Bounds=new Rectangle(theme.X,theme.Y+S(14),theme.Width,S(31));
        void Range(int i,Label label,Label value,TrackBar bar,Label min,Label max)
        {
            var field=Field(i);int valueWidth=Math.Min(S(43),field.Width/2);
            label.Bounds=new Rectangle(field.X,field.Y,Math.Max(1,field.Width-valueWidth),S(18));
            value.Bounds=new Rectangle(field.Right-valueWidth,field.Y,valueWidth,S(18));
            bar.Bounds=new Rectangle(field.X,field.Y+S(21),field.Width,S(26));
            min.Bounds=new Rectangle(field.X,field.Y+S(49),field.Width/2,S(15));
            max.Bounds=new Rectangle(field.X+field.Width/2,field.Y+S(49),field.Width-field.Width/2,S(15));
        }
        Range(1,_uiLabel,_uiValue,_uiScale,_uiMin,_uiMax);Range(2,_textLabel,_textValue,_textScale,_textMin,_textMax);
        y+=((3+cols-1)/cols)*(fieldHeight+gap)-gap+S(4);
        int copyX=left+S(41),copyW=Math.Max(1,right-S(3)-S(47)-S(15)-copyX);
        StyleLabel(_motionTitle,12,true);StyleLabel(_motionDescription,10,false,true);
        int th=Math.Max(_motionTitle.Font.Height,TextRenderer.MeasureText(_motionTitle.Text,_motionTitle.Font,new Size(copyW,1000),TextFormatFlags.WordBreak).Height);
        int dh=Math.Max(_motionDescription.Font.Height,TextRenderer.MeasureText(_motionDescription.Text,_motionDescription.Font,new Size(copyW,1000),TextFormatFlags.WordBreak).Height);
        int rowHeight=Math.Max(S(60),th+dh+S(17)),copyY=y+(rowHeight-th-dh-S(1))/2;
        _motionTitle.Bounds=new Rectangle(copyX,copyY,copyW,th);
        _motionDescription.Bounds=new Rectangle(copyX,copyY+th+S(1),copyW,dh);
        _animations.Bounds=new Rectangle(right-S(3)-S(47),y+(rowHeight-S(26))/2,S(47),S(26));
        _motionRow=new Rectangle(left,y,inner,rowHeight);
        return y+rowHeight+S(12);
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);if(_theme is not null&&Width>=80)ArrangeCard(Width);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        V269SettingsGlyph.Draw(e.Graphics,new Rectangle(S(14),S(12),S(24),S(24)),3,H(Options.Theme switch
        {V269Theme.Dark=>"#65b8ef",V269Theme.Claro=>"#167bbb",_=>"#75c4ed"}));
        using var pen=new Pen(Rule);e.Graphics.DrawLine(pen,S(14),S(41),Math.Max(S(14),Width-S(14)),S(41));
        if(_motionRow.Height>0)V269SettingsGlyph.Draw(e.Graphics,new Rectangle(_motionRow.X+S(3),_motionRow.Y+(_motionRow.Height-S(23))/2,S(23),S(23)),6,GlyphColor(6));
    }
    private void RefreshLabels()
    {
        _uiValue.Text=_uiScale.Value+"%";_textValue.Text=_textScale.Value+"%";
        _uiScale.AccessibleDescription=_uiValue.Text;_textScale.AccessibleDescription=_textValue.Text;
    }
    private void Changed()
    {
        if(_updating)return;RefreshLabels();ApplyOptions(GetOptions());AppearanceChanged?.Invoke(this,EventArgs.Empty);
    }
}
