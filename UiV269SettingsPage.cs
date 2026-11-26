using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Preferências locais exclusivamente de apresentação/comportamento da janela.
// Consentimento e registro de inicialização têm suas próprias fontes reais.
internal sealed record V269GeneralOptions(bool Notifications = true, bool MinimizeToTray = true);

internal static class V269GeneralSettingsStore
{
    internal static string PathForUser => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GerenciadorFinal", "geral-v269.json");

    internal static V269GeneralOptions Read(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length <= 4096)
                return JsonSerializer.Deserialize<V269GeneralOptions>(stream) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            JsonException or System.Security.SecurityException) { }
        return new();
    }

    internal static void Write(string path, V269GeneralOptions value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}

internal class V269SettingsCard : Panel
{
    protected V269AppearanceOptions Options = V269AppearanceOptions.Default;
    private readonly Dictionary<(int, FontStyle, int), Font> _fonts = new();
    protected V269SettingsCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Margin = Padding.Empty;
    }

    internal static Color H(string value) => ColorTranslator.FromHtml(value);
    protected int S(int px) => Math.Max(1, (int)Math.Round(px * Options.InterfacePercent * DeviceDpi / 9600d));
    protected Font F(int px, FontStyle style = FontStyle.Regular)
    {
        var key = (px, style, Options.TextPercent);
        if (!_fonts.TryGetValue(key, out var font))
            _fonts[key] = font = new Font("Segoe UI", px * .75f * Options.TextPercent / 100f, style);
        return font;
    }
    protected Color Ink => Options.Theme == V269Theme.Dark ? H("#dce0e4") :
        V269VisualTokens.ForTheme(Options.Theme).Text;
    protected Color Muted => Options.Theme switch
    {
        V269Theme.Dark => H("#aab4bd"), V269Theme.Claro => H("#3f627a"), _ => H("#99b1c4")
    };
    protected Color Rule => Options.Theme switch
    {
        V269Theme.Dark => H("#3c4249"), V269Theme.Claro => H("#b7d1e2"), _ => H("#1d394d")
    };
    protected Color GlyphColor(int index) => H(Options.Theme switch
    {
        V269Theme.Dark => new[] { "#85c975", "#b998f2", "#b998f2", "#65b8ef", "#65b8ef", "#e4bb67", "#c6cdd3" }[index],
        V269Theme.Claro => new[] { "#087d82", "#2b79a8", "#2b79a8", "#167bbb", "#167bbb", "#ad6f1d", "#2b79a8" }[index],
        _ => new[] { "#6bd6b2", "#69bfe0", "#69bfe0", "#75c4ed", "#75c4ed", "#ecc27c", "#69bfe0" }[index]
    });
    protected static Label TextLabel(string name, string text) => new()
    {
        Name = name, Text = text, BackColor = Color.Transparent, AutoSize = false,
        TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
    };
    protected void StyleLabel(Label label, int pixels, bool bold = false, bool muted = false)
    {
        label.Font = F(pixels, bold ? FontStyle.Bold : FontStyle.Regular);
        label.ForeColor = muted ? Muted : Ink;
    }

    internal virtual void ApplyOptions(V269AppearanceOptions options)
    {
        Options = options.Normalize();
        Invalidate(true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        if (!PaintCard || Width <= 2 || Height <= 2) return;
        var (first,last,border,angle) = V269VisualTokens.SettingsCardGradient(Options.Theme);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0,0,Width-1,Height-1);
        using var shape = V269Shell.Rounded(bounds,S(11));
        using var fill = new LinearGradientBrush(bounds,first,last,angle);
        using var line = new Pen(border);
        e.Graphics.FillPath(fill,shape);
        e.Graphics.DrawPath(line,shape);
    }

    protected virtual bool PaintCard => true;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (var font in _fonts.Values) font.Dispose();
            _fonts.Clear();
        }
    }
}

// O estado Checked é o estado efetivo, anunciado pelo objeto acessível
// nativo. Em ações externas AutoCheck=false: o controlador publica o
// resultado somente depois da confirmação/releitura, nunca por otimismo.
internal sealed class V269SettingsSwitch : CheckBox
{
    internal V269Theme Theme;
    internal V269SettingsSwitch(string name, string caption, bool autoCheck = false)
    {
        Name = name; AccessibleName = caption; AutoCheck = autoCheck;
        Text = ""; TabStop = true; Cursor = Cursors.Hand;
        BackColor = Color.Transparent; Margin = Padding.Empty;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        CheckedChanged += (_, _) => Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 2 || Height <= 2) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color H(string value) => V269SettingsCard.H(value);
        var (fill,outline) = V269VisualTokens.SettingsSwitch(Theme,Checked);
        var bounds = new Rectangle(1,1,Width-3,Height-3);
        using var shape = V269Shell.Rounded(bounds,bounds.Height/2);
        using var brush = new SolidBrush(Enabled ? fill : Color.FromArgb(180,fill));
        using var pen = new Pen(outline);
        g.FillPath(brush,shape); g.DrawPath(pen,shape);
        int n = Math.Max(1,Height - 8), x = Checked ? Width-n-4 : 4;
        using var knob = new SolidBrush(H(Checked ? (Theme == V269Theme.Dark ? "#e3e6e9" : "#ffffff") : "#d8e4ec"));
        g.FillEllipse(knob,x,(Height-n)/2,n,n);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g,ClientRectangle);
    }
}

// A barra conserva o controle TrackBar nativo e seus eventos/teclado.
// A pintura e o mapeamento do mouse usam a geometria do HTML aprovado.
internal sealed class V269SettingsRange : TrackBar
{
    private V269AppearanceOptions _appearance=V269AppearanceOptions.Default;
    private bool _dragging;
    internal V269SettingsRange()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|
            ControlStyles.SupportsTransparentBackColor|ControlStyles.ResizeRedraw,true);
        BackColor=Color.Transparent;Cursor=Cursors.Hand;TickStyle=TickStyle.None;
    }
    internal void ApplyOptions(V269AppearanceOptions options){_appearance=options.Normalize();Invalidate();}
    private int S(int px)=>Math.Max(1,(int)Math.Round(px*_appearance.InterfacePercent*DeviceDpi/9600d));
    private int Thumb=>Math.Min(S(18),Math.Max(1,Height-2));
    protected override void OnValueChanged(EventArgs e){base.OnValueChanged(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<3||Height<3)return;
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        Color H(string x)=>V269SettingsCard.H(x);
        var (accent,meter,border,knob)= V269VisualTokens.SettingsSlider(_appearance.Theme);
        int thumb=Thumb,half=thumb/2,railHeight=Math.Min(Height-2,S(7)),y=(Height-railHeight)/2;
        var rail=new Rectangle(1,y,Math.Max(1,Width-2),railHeight);
        using var path=V269Shell.Rounded(rail,railHeight/2);
        using var background=new SolidBrush(meter);using var line=new Pen(border);
        g.FillPath(background,path);
        double ratio=(Value-Minimum)/(double)Math.Max(1,Maximum-Minimum);
        int cx=half+(int)Math.Round(Math.Max(0,Width-thumb)*ratio);
        var saved=g.Save();
        try{g.SetClip(path);using var active=new SolidBrush(accent);g.FillRectangle(active,0,y,Math.Max(1,cx),railHeight);}
        finally{g.Restore(saved);}
        g.DrawPath(line,path);
        var rect=new Rectangle(cx-half,(Height-thumb)/2,Math.Max(1,thumb-1),Math.Max(1,thumb-1));
        using var ring=new SolidBrush(accent);g.FillEllipse(ring,rect);
        int inset=Math.Min(S(4),thumb/3);var inner=Rectangle.Inflate(rect,-inset,-inset);
        if(inner.Width>0&&inner.Height>0){using var fill=new SolidBrush(knob);g.FillEllipse(fill,inner);}
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle);
    }
    internal int ValueAtPosition(int x)
    {
        int t=Thumb;double f=Math.Clamp((x-t/2d)/Math.Max(1,Width-t),0d,1d);
        return Math.Clamp(Minimum+(int)Math.Round((Maximum-Minimum)*f),Minimum,Maximum);
    }
    protected override void WndProc(ref Message m)
    {
        const int LeftDown=0x0201,LeftUp=0x0202,MouseMove=0x0200,CaptureChanged=0x0215,MouseWheel=0x020A;
        if(m.Msg==MouseWheel)
        {
            // A roda pertence à página, como no protótipo. A escala muda
            // pelo arraste/clique ou pelo teclado, nunca ao rolar o formulário.
            for(Control? parent=Parent;parent is not null;parent=parent.Parent)
                if(parent is V269SettingsPage page)
                {
                    page.ScrollByWheel(unchecked((short)(m.WParam.ToInt64()>>16)));return;
                }
        }
        int x=unchecked((short)m.LParam.ToInt64());
        if(Enabled&&m.Msg==LeftDown){Focus();_dragging=true;Capture=true;Value=ValueAtPosition(x);return;}
        if(_dragging&&m.Msg==MouseMove){Value=ValueAtPosition(x);return;}
        if(_dragging&&m.Msg==LeftUp){Value=ValueAtPosition(x);_dragging=false;Capture=false;return;}
        if(m.Msg==CaptureChanged)_dragging=false;
        base.WndProc(ref m);
    }
}

// Lista suspensa nativa: mantém seleção, teclado e acessibilidade, mas
// desenha o campo fechado e a lista nos três temas, sem fundo branco no Dark.
internal sealed class V269SettingsCombo : ComboBox
{
    private V269AppearanceOptions _appearance=V269AppearanceOptions.Default;
    internal V269SettingsCombo()
    {
        DropDownStyle=ComboBoxStyle.DropDownList;DrawMode=DrawMode.OwnerDrawFixed;
        IntegralHeight=false;FlatStyle=FlatStyle.Flat;
    }
    private int S(int px)=>Math.Max(1,(int)Math.Round(px*_appearance.InterfacePercent*DeviceDpi/9600d));
    private (Color Top,Color Bottom,Color Border,Color Ink) Palette()=> V269VisualTokens.DropdownPalette(_appearance.Theme);
    internal void ApplyOptions(V269AppearanceOptions options)
    {
        _appearance=options.Normalize();var p=Palette();
        BackColor=p.Top;ForeColor=p.Ink;
        ItemHeight=Math.Max(Font.Height+2,S(31)-6);DropDownHeight=ItemHeight*3+4;
        Invalidate();
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if(e.Index<0||e.Index>=Items.Count)return;
        var p=Palette();
        Color bg=(e.State&DrawItemState.Selected)!=0?p.Border:p.Top;
        using var fill=new SolidBrush(bg);e.Graphics.FillRectangle(fill,e.Bounds);
        TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,e.Bounds,p.Ink,
            TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
    }
    // Graphics.FromHdc para WM_PRINT pode não herdar o recorte dos
    // ancestrais. Nunca pintar o seletor fora do viewport rolável.
    internal Rectangle VisibleSettingsBounds()
    {
        Rectangle visible=ClientRectangle;
        for(Control? parent=Parent;parent is not null;parent=parent.Parent)
        {
            visible.Intersect(RectangleToClient(parent.RectangleToScreen(parent.ClientRectangle)));
            if(visible.Width<=0||visible.Height<=0)return Rectangle.Empty;
            if(parent is V269SettingsPage)break;
        }
        return visible;
    }
    private void PaintClosed(Graphics g)
    {
        if(Width<3||Height<3)return;
        Rectangle visible=VisibleSettingsBounds();
        if(visible.Width<=0||visible.Height<=0)return;
        var saved=g.Save();
        try
        {
            g.SetClip(visible,CombineMode.Intersect);
            PaintClosedWithinClip(g);
        }
        finally{g.Restore(saved);}
    }
    private void PaintClosedWithinClip(Graphics g)
    {
        var p=Palette();var bounds=new Rectangle(0,0,Width-1,Height-1);
        using var fill=new LinearGradientBrush(bounds,p.Top,p.Bottom,90f);
        g.FillRectangle(fill,bounds);
        g.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=V269Shell.Rounded(bounds,S(7));using var outline=new Pen(p.Border);
        g.DrawPath(outline,shape);
        int arrowX=Width-S(10),mid=Height/2;
        using var arrow=new Pen(p.Ink,1.5f);
        g.DrawLines(arrow,new Point[]{new(arrowX-S(4),mid-S(2)),new(arrowX,mid+S(2)),new(arrowX+S(4),mid-S(2))});
        TextRenderer.DrawText(g,Text,Font,new Rectangle(S(10),0,Math.Max(1,Width-S(33)),Height),p.Ink,
            TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(3,3,Width-6,Height-6));
    }
    protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
    protected override void WndProc(ref Message m)
    {
        const int Paint=0x000F,Print=0x0317,PrintClient=0x0318,MouseWheel=0x020A;
        // A seleção não muda ao rolar a página; uma lista explicitamente
        // aberta continua com seu comportamento nativo.
        if(m.Msg==MouseWheel&&!DroppedDown)
        {
            for(Control? parent=Parent;parent is not null;parent=parent.Parent)
                if(parent is V269SettingsPage page)
                {
                    page.ScrollByWheel(unchecked((short)(m.WParam.ToInt64()>>16)));return;
                }
        }
        base.WndProc(ref m);
        if(!IsHandleCreated||IsDisposed)return;
        if(m.Msg==Paint)
        {
            using var g=Graphics.FromHwnd(Handle);PaintClosed(g);
        }
        else if((m.Msg==Print||m.Msg==PrintClient)&&m.WParam!=IntPtr.Zero)
        {
            using var g=Graphics.FromHdc(m.WParam);PaintClosed(g);
        }
    }
}

internal sealed class V269SettingsButton : Button
{
    private readonly int _glyph;
    internal V269AppearanceOptions Options=V269AppearanceOptions.Default;
    internal V269SettingsButton(string name,string caption,int glyph)
    {
        Name=name;Text=caption;AccessibleName=caption;_glyph=glyph;
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        TabStop=true;Margin=Padding.Empty;UseVisualStyleBackColor=false;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|
            ControlStyles.SupportsTransparentBackColor|ControlStyles.ResizeRedraw,true);
        BackColor=Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<=2||Height<=2)return;
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        Color H(string x)=>V269SettingsCard.H(x);
        var (background,border,ink)= V269VisualTokens.SettingsButton(Options.Theme);
        int S(int px)=>Math.Max(1,(int)Math.Round(px*Options.InterfacePercent*DeviceDpi/9600d));
        using var shape=V269Shell.Rounded(new Rectangle(0,0,Width-1,Height-1),S(8));
        using var fill=new SolidBrush(background);using var line=new Pen(border);
        g.FillPath(fill,shape);g.DrawPath(line,shape);
        int n=S(15),x=S(10);
        V269SettingsGlyph.Draw(g,new Rectangle(x,(Height-n)/2,n,n),_glyph,ink);
        TextRenderer.DrawText(g,Text,Font,new Rectangle(x+n+S(6),0,
            Math.Max(1,Width-x-n-S(12)),Height),ink,TextFormatFlags.VerticalCenter|
            TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(2,2,Width-4,Height-4));
    }
}

internal sealed class V269GeneralSettingsPanel : V269SettingsCard
{
    private readonly Label _title = TextLabel("V269GeneralTitle","Geral");
    private readonly Label[] _names;
    private readonly Label[] _descriptions;
    private readonly Control[][] _actions;
    private readonly Rectangle[] _rows = new Rectangle[6];
    private V269SettingsTab? _generalTab, _monitorTab;
    private V269MonitorOptions? _monitor;
    private bool _monitorSelected;
    internal bool MonitorSelected => _monitorSelected;
    internal event EventHandler? SectionChanged;
    internal readonly V269SettingsSwitch Administrator = new("V269Administrator","Modo Administrador");
    internal readonly V269SettingsSwitch StartWindows = new("V269StartWindows","Iniciar com o Windows");
    internal readonly V269SettingsSwitch Notifications = new("V269Notifications","Mostrar notificações");
    internal readonly V269SettingsSwitch Tray = new("V269MinimizeTray","Minimizar para a bandeja");
    internal readonly V269SettingsSwitch Collect = new("V269Collect","Coleta de dados");
    internal readonly Button Privacy = ActionButton("V269PrivacyInfo","Saiba mais");
    internal readonly Button Diagnostic = ActionButton("V269GenerateDiagnostic","Gerar diagnóstico");
    internal readonly Button History = ActionButton("V269History","Histórico de registro");

    internal V269GeneralSettingsPanel()
    {
        Name = "V269GeneralPanel"; AccessibleName = "Preferências gerais";
        string[] names = ["Modo Administrador","Iniciar com o Windows","Mostrar notificações",
            "Minimizar para a bandeja","Coleta de dados","Diagnóstico e registro"];
        string[] desc = ["Abrir uma sessão elevada somente após confirmação e UAC.",
            "Abrir o GF automaticamente ao entrar na conta.",
            "Exibir resultados de operações quando o GF estiver na bandeja.",
            "Manter o GF na Área de Notificação ao minimizar.",
            "Envio anônimo opcional, mediante consentimento.",
            "Gerar diagnóstico local ou consultar o histórico de registro."];
        _names = names.Select((x,i) => TextLabel("V269SettingTitle"+i,x)).ToArray();
        _descriptions = desc.Select((x,i) => TextLabel("V269SettingDescription"+i,x)).ToArray();
        _actions = [[Administrator],[StartWindows],[Notifications],[Tray],[Collect,Privacy],[Diagnostic,History]];
        Controls.Add(_title);
        for (int i=0;i<names.Length;i++)
        {
            Controls.Add(_names[i]); Controls.Add(_descriptions[i]);
            Controls.AddRange(_actions[i]);
            foreach(var control in _actions[i]) control.AccessibleDescription = desc[i];
        }
    }

    private static Button ActionButton(string name,string text) =>
        new V269SettingsButton(name,text,name switch {"V269PrivacyInfo"=>8,"V269GenerateDiagnostic"=>9,_=>10});

    internal void AddMonitor(V269MonitorOptions monitor)
    {
        _monitor = monitor;
        _generalTab = new V269SettingsTab("V269GeneralTab", "Geral") { Selected = true };
        _monitorTab = new V269SettingsTab("V269MonitorTab", "Monitor");
        _generalTab.Click += (_, _) => SelectMonitor(false);
        _monitorTab.Click += (_, _) => SelectMonitor(true);
        Controls.AddRange([_generalTab, _monitorTab, monitor]);
        _generalTab.TabIndex = 0;
        _monitorTab.TabIndex = 1;
        monitor.Visible = false;
        _title.Visible = false;
    }

    private void SelectMonitor(bool monitor)
    {
        if (_monitor is null || _monitorSelected == monitor) return;
        _monitorSelected = monitor;
        SuspendLayout();
        _generalTab!.Selected = !monitor;
        _monitorTab!.Selected = monitor;
        foreach (var label in _names.Concat(_descriptions)) label.Visible = !monitor;
        foreach (var action in _actions.SelectMany(row => row)) action.Visible = !monitor;
        _monitor.Visible = monitor;
        ResumeLayout(false);
        SectionChanged?.Invoke(this, EventArgs.Empty);
        Parent?.PerformLayout();
        Invalidate();
    }

    internal void SetObserved(bool? administrator, bool? startup, V269GeneralOptions general,
        TelemetryConsent consent, bool canChange, bool canCollect = true)
    {
        Administrator.Checked=administrator==true;
        Administrator.Enabled=canChange&&administrator.HasValue;
        Administrator.AccessibleDescription=administrator switch
        {
            true=>"Sessão elevada. Desativar encerra a sessão atual após confirmação.",
            false=>"Solicitar abertura elevada única, após confirmação e UAC.",
            null=>"Privilégios não verificados. Alteração bloqueada."
        };
        StartWindows.Checked=startup==true;
        StartWindows.Enabled=canChange && startup.HasValue;
        Notifications.Checked=general.Notifications; Notifications.Enabled=canChange;
        Tray.Checked=general.MinimizeToTray; Tray.Enabled=canChange;
        Collect.Checked=consent is TelemetryConsent.LocalOnly or TelemetryConsent.RemoteBeta;
        Collect.Enabled=canChange&&canCollect;
        StartWindows.AccessibleDescription=startup.HasValue
            ? "Estado do registro de inicialização deste usuário."
            : "Não foi possível consultar a inicialização. Nenhuma alteração permitida.";
        Collect.AccessibleDescription=PrivacyConsentPresentation.Explanation(consent);
    }

    internal int ArrangeCard(int width)
    {
        int left=S(14), right=Math.Max(left+1,width-S(14)), y=S(12);
        StyleLabel(_title,15,true);
        _title.Bounds=new Rectangle(left+S(33),y,Math.Max(1,right-left-S(33)),S(24));
        if (_generalTab is { } general && _monitorTab is { } tab && _monitor is { } monitor)
        {
            int x = _title.Left;
            foreach (var section in new[] { general, tab })
            {
                section.Font = _title.Font;
                section.ForeColor = Ink;
                section.Accent = V269VisualTokens.ForTheme(Options.Theme).Accent;
                int tabWidth = TextRenderer.MeasureText(section.Text, section.Font).Width + S(12);
                section.Bounds = new Rectangle(x, y, tabWidth, S(24));
                x += tabWidth + S(9);
                section.Invalidate();
            }
            if (_monitorSelected)
            {
                int contentWidth = Math.Max(1, right - left);
                monitor.ApplyOptions(Options);
                int contentHeight = monitor.ArrangeRows(contentWidth);
                monitor.Bounds = new Rectangle(left, y + S(35), contentWidth, contentHeight);
                return monitor.Bottom + S(12);
            }
        }
        y+=S(35);
        for(int i=0;i<_rows.Length;i++)
        {
            StyleLabel(_names[i],12,true); StyleLabel(_descriptions[i],10,false,true);
            int icon=S(23), copyX=left+S(41), rowRight=right-S(3);
            int actionWidth=0;
            foreach(var action in _actions[i])
            {
                action.Font=F(12);
                if(action is V269SettingsSwitch sw)
                {
                    sw.Theme=Options.Theme; action.Size=new Size(S(47),S(26)); sw.Invalidate();
                }
                else if(action is Button button)
                {
                    button.Font=F(12,FontStyle.Bold);
                    int baseline=button.Name switch {"V269PrivacyInfo"=>106,"V269GenerateDiagnostic"=>146,_=>160};
                    int w=Math.Max(S(baseline),TextRenderer.MeasureText(button.Text,button.Font,
                        Size.Empty,TextFormatFlags.NoPadding).Width+S(42));
                    action.Size=new Size(Math.Min(w,Math.Max(S(40),rowRight-copyX)),S(30));
                    if(button is V269SettingsButton visual){visual.Options=Options;visual.Invalidate();}
                }
                actionWidth+=action.Width+S(8);
            }
            actionWidth-=S(8);
            bool wrap=rowRight-copyX-actionWidth-S(15)<S(105);
            int copyWidth=Math.Max(1,(wrap?rowRight:rowRight-actionWidth-S(15))-copyX);
            int TextHeight(Label label) => Math.Max(label.Font.Height,TextRenderer.MeasureText(label.Text,label.Font,
                new Size(copyWidth,1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding).Height);
            int titleHeight=TextHeight(_names[i]), descHeight=TextHeight(_descriptions[i]);
            int copyHeight=titleHeight+S(1)+descHeight, actionsHeight=_actions[i].Max(x=>x.Height);
            bool separate=wrap && actionWidth>rowRight-copyX;
            if(separate) actionsHeight=_actions[i].Sum(x=>x.Height)+S(8)*(_actions[i].Length-1);
            bool compactDiagnostic=i==5&&width<=S(540);
            int minimumRowHeight=compactDiagnostic?S(78):S(60);
            // Em 760px, o texto WinForms ocupa 67px contra os 62px
            // do CSS. Reduzir somente o espaço livre vertical, sem cortar
            // texto nem limitar a altura em escalas maiores.
            int rowPadding=compactDiagnostic?S(11):S(16);
            int rowHeight=Math.Max(minimumRowHeight,wrap?copyHeight+actionsHeight+S(22):Math.Max(copyHeight,actionsHeight)+rowPadding);
            int textY=y+(wrap?S(8):(rowHeight-copyHeight)/2);
            _names[i].Bounds=new Rectangle(copyX,textY,copyWidth,titleHeight);
            _descriptions[i].Bounds=new Rectangle(copyX,textY+titleHeight+S(1),copyWidth,descHeight);
            int actionX=Math.Max(copyX,rowRight-actionWidth), actionY=wrap?textY+copyHeight+S(6):y+(rowHeight-actionsHeight)/2;
            foreach(var action in _actions[i])
            {
                if(separate) { action.Left=Math.Max(copyX,rowRight-action.Width); action.Top=actionY; actionY+=action.Height+S(8); }
                else { action.Location=new Point(actionX,actionY+(actionsHeight-action.Height)/2); actionX+=action.Width+S(8); }
            }
            _rows[i]=new Rectangle(left,y,right-left,rowHeight);
            y+=rowHeight;
        }
        return y+S(12);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g=e.Graphics;
        V269SettingsGlyph.Draw(g,new Rectangle(S(14),S(12),S(24),S(24)),7,H(Options.Theme switch
        {V269Theme.Dark=>"#b998f2",V269Theme.Claro=>"#7046c1",_=>"#b7a5f5"}));
        using var pen=new Pen(Rule);
        g.DrawLine(pen,S(14),S(41),Math.Max(S(14),Width-S(14)),S(41));
        if (_monitorSelected) return;
        for(int i=0;i<_rows.Length;i++)
        {
            var row=_rows[i]; if(row.Height<=0)continue;
            if(i!=_rows.Length-1)g.DrawLine(pen,row.Left,row.Bottom-1,row.Right,row.Bottom-1);
            var glyph=new Rectangle(row.X+S(3),row.Y+(row.Height-S(23))/2,S(23),S(23));
            V269SettingsGlyph.Draw(g,glyph,i,GlyphColor(i));
        }
    }
}

// Geometrias vetoriais 24x24 dos símbolos de Configurações (sem fontes
// de ícone, sem rede e sem imagens de terceiros).
internal static class V269SettingsGlyph
{
    internal static void Draw(Graphics g,Rectangle rect,int index,Color color)
    {
        if(index==5){V269HomeHeadingIcon.DrawGlyph(g,rect,V269Page.Reparo,color);return;}
        var state=g.Save();
        try
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.TranslateTransform(rect.X,rect.Y);g.ScaleTransform(rect.Width/24f,rect.Height/24f);
            using var p=new Pen(color,1.8f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
            switch(index)
            {
                case 0:
                    using(var path=new GraphicsPath())
                    {
                        path.AddBezier(12,22,12,22,20,18,20,12);path.AddLine(20,12,20,5);
                        path.AddLine(20,5,12,2);path.AddLine(12,2,4,5);path.AddLine(4,5,4,12);
                        path.AddBezier(4,12,4,18,12,22,12,22);path.CloseFigure();g.DrawPath(p,path);
                    }
                    g.DrawLines(p,new Point[]{new(9,12),new(11,14),new(15,10)});break;
                case 1:g.DrawPolygon(p,new Point[]{new(8,5),new(19,12),new(8,19)});break;
                case 2:
                    g.DrawArc(p,6,2,12,12,180,180);
                    using(var path=new GraphicsPath())
                    {
                        path.AddBezier(6,8,6,15,3,15,3,17);path.AddLine(3,17,21,17);
                        path.AddBezier(21,17,21,15,18,15,18,8);g.DrawPath(p,path);
                    }
                    g.DrawLine(p,10,21,14,21);break;
                case 3:
                    using(var shape=V269Shell.Rounded(new Rectangle(3,4,18,14),2))g.DrawPath(p,shape);
                    g.DrawLine(p,8,22,16,22);g.DrawLine(p,12,18,12,22);break;
                case 4:
                    using(var shape=V269Shell.Rounded(new Rectangle(2,5,20,14),2))g.DrawPath(p,shape);
                    g.DrawLine(p,4,15,20,15);g.DrawLine(p,16,17,18,17);break;
                case 5:break; // reutilizado antes da transformação
                case 6:
                    g.DrawLine(p,3,7,12,7);g.DrawLine(p,3,12,16,12);g.DrawLine(p,3,17,12,17);
                    g.DrawLines(p,new Point[]{new(13,6),new(18,12),new(13,18)});
                    g.DrawLine(p,20,5,20,7);g.DrawLine(p,20,17,20,19);break;
                case 7:
                    g.DrawLine(p,4,7,20,7);g.DrawLine(p,4,17,20,17);
                    g.DrawLine(p,9,4,9,10);g.DrawLine(p,15,14,15,20);break;
                case 8:g.DrawEllipse(p,2,2,20,20);g.DrawLine(p,12,10,12,17);g.DrawLine(p,12,6,12,6.3f);break;
                case 9:
                    g.DrawPolygon(p,new Point[]{new(6,2),new(14,2),new(19,7),new(19,22),new(6,22)});
                    g.DrawLines(p,new Point[]{new(14,2),new(14,8),new(19,8)});
                    g.DrawLine(p,9,12,16,12);g.DrawLine(p,9,16,16,16);break;
                case 10:
                    using(var shape=V269Shell.Rounded(new Rectangle(4,4,16,18),2))g.DrawPath(p,shape);
                    g.DrawLines(p,new Point[]{new(9,4),new(9,2),new(15,2),new(15,4)});
                    g.DrawLine(p,8,11,16,11);g.DrawLine(p,8,15,16,15);break;
            }
        }
        finally {g.Restore(state);}
    }
}

internal sealed class V269SettingsPage : Panel
{
    private const string DefaultNotice = "As preferências são locais. Operações administrativas continuam sujeitas à confirmação.";
    internal readonly V269GeneralSettingsPanel General = new();
    internal readonly V269AppearancePanel Appearance = new();
    private readonly Label _hint = new() {Name="V269SettingsHint", Text=DefaultNotice,BackColor=Color.Transparent};
    private string? _notice, _monitorNotice;
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    private int _scroll,_height,_wheel;
    private bool _arranging;
    private readonly Dictionary<int,Font> _hintFonts=new();
    private Button? _paneToggle;
    internal V269SettingsPage()
    {
        Name="V269SettingsPage";AccessibleName="Configurações gerais e aparência";
        SetStyle(ControlStyles.Selectable|ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer,true);
        BackColor=Color.Transparent; AutoScroll=false; TabStop=true;
        Controls.AddRange([General,Appearance,_hint]);
        WireFocus(this);
    }
    private void WireFocus(Control root)
    {
        foreach(Control child in root.Controls)
        {
            child.GotFocus+=(_,_)=>Reveal(child);
            WireFocus(child);
        }
    }
    internal void AddMonitor(V269MonitorOptions monitor)
    {
        General.AddMonitor(monitor);
        General.SectionChanged += (_, _) => UpdateNotice();
        WireFocus(monitor);
        foreach (var tab in General.Controls.OfType<V269SettingsTab>())
            tab.GotFocus += (_, _) => Reveal(tab);
        Arrange();
    }
    private void Reveal(Control child)
    {
        if(!IsHandleCreated || IsDisposed || !child.ContainsFocus)return;
        var r=RectangleToClient(child.RectangleToScreen(child.ClientRectangle));
        ScrollTo(_scroll+(r.Bottom>Height?r.Bottom-Height:r.Top<0?r.Top:0));
    }
    internal void AttachPaneToggle(Button button)
    {
        if(_paneToggle==button)return;
        _paneToggle=button;button.AutoSize=false;button.Dock=DockStyle.None;button.Margin=Padding.Empty;
        Controls.Add(button);button.GotFocus+=PaneFocus;Arrange();
    }
    internal void DetachPaneToggle(Button button)
    {
        if(_paneToggle!=button)return;
        button.GotFocus-=PaneFocus;Controls.Remove(button);_paneToggle=null;Arrange();
    }
    private void PaneFocus(object? sender,EventArgs e){if(sender is Control c)Reveal(c);}
    internal void SetNotice(string? message)
    {
        _notice = message;
        UpdateNotice();
    }
    internal void SetMonitorNotice(string? message)
    {
        _monitorNotice = message;
        UpdateNotice();
    }
    private void UpdateNotice()
    {
        _hint.Text = (General.MonitorSelected ? _monitorNotice : _notice) ?? DefaultNotice;
        Arrange();
    }
    internal long GeometryArrangeCount {get;private set;}
    internal int ContentHeight=>_height;
    internal int ScrollOffset=>_scroll;
    internal void ScrollTo(int offset)
    {
        int value=Math.Clamp(offset,0,Math.Max(0,_height-ClientSize.Height));
        if(value==_scroll||_arranging)return;
        // A roda altera somente a origem vertical, nunca a geometria dos
        // cartões. ArrangeCard mede textos, reposiciona controles e repinta
        // todos os cartões; isso pertence a mudanças de conteúdo/dimensões.
        int dy=_scroll-value;
        _arranging=true;
        SuspendLayout();
        try
        {
            _scroll=value;
            General.Top+=dy;
            Appearance.Top+=dy;
            _hint.Top+=dy;
            if(_paneToggle is{} toggle)toggle.Top+=dy;
        }
        finally
        {
            ResumeLayout(false);
            _arranging=false;
        }
        Invalidate();
    }
    internal void ApplyAppearance(V269AppearanceOptions options)
    {
        _options=options.Normalize(); General.ApplyOptions(_options); Appearance.ApplyOptions(_options);
        _hint.ForeColor=V269VisualTokens.ForTheme(_options.Theme).SecondaryText;
        if(!_hintFonts.TryGetValue(_options.TextPercent,out var f))
            _hintFonts[_options.TextPercent]=f=new Font("Segoe UI",7.5f*_options.TextPercent/100f);
        _hint.Font=f;
        Arrange();
    }
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);Arrange();}
    protected override void OnResize(EventArgs e){base.OnResize(e);Arrange();}
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if(e is HandledMouseEventArgs handled && handled.Handled)return;
        ScrollByWheel(e.Delta);
        if(e is HandledMouseEventArgs accepted)accepted.Handled=true;
        base.OnMouseWheel(e);
    }
    internal void ScrollByWheel(int delta)
    {
        _wheel+=delta;int steps=_wheel/120;_wheel%=120;
        if(steps!=0)ScrollTo(_scroll-steps*Math.Max(24,48*DeviceDpi/96));
    }
    protected override bool ProcessCmdKey(ref Message m,Keys key)
    {
        switch(key)
        {
            case Keys.PageDown:ScrollTo(_scroll+Math.Max(24,Height-24));return true;
            case Keys.PageUp:ScrollTo(_scroll-Math.Max(24,Height-24));return true;
            case Keys.Control|Keys.Home:ScrollTo(0);return true;
            case Keys.Control|Keys.End:ScrollTo(_height);return true;
        }
        return base.ProcessCmdKey(ref m,key);
    }
    private void Arrange()
    {
        if(_arranging||General is null||Appearance is null||Width<80)return;
        _arranging=true;
        try
        {
            int S(int n)=>Math.Max(1,(int)Math.Round(n*_options.InterfacePercent*DeviceDpi/9600d));
            int w=Math.Max(1,ClientSize.Width-S(2)),gap=S(7);
            GeometryArrangeCount++;
            int gh=General.ArrangeCard(w),ah=Appearance.ArrangeCard(w);
            int toggleWidth=_paneToggle is null?0:Math.Min(w,S(120));
            bool sameRow=_paneToggle is not null&&w>=S(330);
            int hintWidth=Math.Max(1,w-(sameRow?toggleWidth+gap:0));
            int hh=Math.Max(S(24),TextRenderer.MeasureText(_hint.Text,_hint.Font,new Size(hintWidth,1000),TextFormatFlags.WordBreak).Height);
            int footerHeight=_paneToggle is null?hh:sameRow?Math.Max(hh,S(28)):hh+gap+S(28);
            _height=gh+gap+ah+gap+footerHeight;
            _scroll=Math.Clamp(_scroll,0,Math.Max(0,_height-ClientSize.Height));
            General.Bounds=new Rectangle(0,-_scroll,w,gh);
            Appearance.Bounds=new Rectangle(0,gh+gap-_scroll,w,ah);
            int footerY=gh+gap+ah+gap-_scroll;
            _hint.Bounds=new Rectangle(0,footerY,hintWidth,hh);
            if(_paneToggle is{} toggle)
                toggle.Bounds=new Rectangle(sameRow?w-toggleWidth:0,
                    sameRow?footerY+(footerHeight-S(28))/2:footerY+hh+gap,toggleWidth,S(28));
            General.Invalidate();Appearance.Invalidate();
        }
        finally{_arranging=false;}
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if(disposing){foreach(var font in _hintFonts.Values)font.Dispose();_hintFonts.Clear();}
    }
}
