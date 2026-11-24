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
        if (Width <= 2 || Height <= 2) return;
        var (first,last,border,angle) = Options.Theme switch
        {
            V269Theme.Dark => (H("#303438"), H("#282c30"), H("#565c62"), 65f),
            V269Theme.Claro => (H("#eef8ff"), H("#dceefa"), H("#9bbacf"), 70f),
            _ => (H("#0d3047"), H("#0b2237"), H("#25445f"), 65f)
        };
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0,0,Width-1,Height-1);
        using var shape = V269Shell.Rounded(bounds,S(11));
        using var fill = new LinearGradientBrush(bounds,first,last,angle);
        using var line = new Pen(border);
        e.Graphics.FillPath(fill,shape);
        e.Graphics.DrawPath(line,shape);
    }

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
        var (fill,outline) = Theme switch
        {
            V269Theme.Dark => (H(Checked ? "#858f98" : "#494f55"), H(Checked ? "#c0c9d0" : "#7d858d")),
            V269Theme.Claro => (H(Checked ? "#237ab9" : "#a5b3bf"), H(Checked ? "#146394" : "#647d91")),
            _ => (H(Checked ? "#2a7cbc" : "#2c4458"), H(Checked ? "#6db0da" : "#567387"))
        };
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

internal sealed class V269GeneralSettingsPanel : V269SettingsCard
{
    private readonly Label _title = TextLabel("V269GeneralTitle","Geral");
    private readonly Label[] _names;
    private readonly Label[] _descriptions;
    private readonly Control[][] _actions;
    private readonly Rectangle[] _rows = new Rectangle[6];
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
        string[] desc = ["Privilégios da sessão; operações elevadas exigem confirmação.",
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

    private static Button ActionButton(string name,string text)
    {
        var button = new Button { Name=name, Text=text, AccessibleName=text, FlatStyle=FlatStyle.Flat,
            TabStop=true, Margin=Padding.Empty, UseVisualStyleBackColor=false };
        button.FlatAppearance.BorderSize=1;
        return button;
    }

    internal void SetObserved(bool administrator, bool? startup, V269GeneralOptions general,
        TelemetryConsent consent, bool canChange)
    {
        Administrator.Checked=administrator;
        // O GF não reabre todo o processo elevado: as operações continuam
        // nos workers administrativos autenticados. O clique explica o modo.
        Administrator.Enabled=canChange;
        StartWindows.Checked=startup==true;
        StartWindows.Enabled=canChange && startup.HasValue;
        Notifications.Checked=general.Notifications; Notifications.Enabled=canChange;
        Tray.Checked=general.MinimizeToTray; Tray.Enabled=canChange;
        Collect.Checked=consent is TelemetryConsent.LocalOnly or TelemetryConsent.RemoteBeta;
        Collect.Enabled=canChange;
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
                    int w=TextRenderer.MeasureText(button.Text,button.Font).Width+S(37);
                    action.Size=new Size(Math.Max(S(94),w),S(30));
                    button.BackColor=Options.Theme switch {V269Theme.Dark=>H("#353a40"),V269Theme.Claro=>H("#e2f0fb"),_=>H("#102b41")};
                    button.ForeColor=Ink;
                    button.FlatAppearance.BorderColor=Options.Theme switch {V269Theme.Dark=>H("#737c85"),V269Theme.Claro=>H("#8eafc7"),_=>H("#3d5a70")};
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
            int rowHeight=Math.Max(S(60),wrap?copyHeight+actionsHeight+S(22):Math.Max(copyHeight,actionsHeight)+S(16));
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
        V269HomeHeadingIcon.DrawGlyph(g,new Rectangle(S(14),S(12),S(24),S(24)),V269Page.Configuracoes,GlyphColor(6));
        using var pen=new Pen(Rule);
        g.DrawLine(pen,S(14),S(41),Math.Max(S(14),Width-S(14)),S(41));
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
        var state=g.Save();
        try
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.TranslateTransform(rect.X,rect.Y);g.ScaleTransform(rect.Width/24f,rect.Height/24f);
            using var p=new Pen(color,1.8f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
            switch(index)
            {
                case 0:
                    g.DrawPolygon(p,[new Point(12,2),new Point(21,6),new Point(19,15),new Point(12,22),new Point(5,15),new Point(3,6)]);
                    g.DrawLines(p,[new Point(8,12),new Point(11,15),new Point(16,9)]);break;
                case 1: g.DrawPolygon(p,[new Point(6,3),new Point(21,12),new Point(6,21)]);break;
                case 2:
                    using(var path=new GraphicsPath()){
                        path.AddBezier(5,17,7,15,6,8,8,6);path.AddBezier(8,6,10,2,14,2,16,6);
                        path.AddBezier(16,6,18,8,17,15,19,17);path.AddLine(19,17,5,17);g.DrawPath(p,path);
                    }g.DrawArc(p,9,17,6,5,0,180);break;
                case 3: g.DrawRectangle(p,3,4,18,14);g.DrawLine(p,12,18,12,22);g.DrawLine(p,7,22,17,22);break;
                case 4: g.DrawEllipse(p,3,3,18,7);g.DrawLine(p,3,7,3,18);g.DrawLine(p,21,7,21,18);g.DrawArc(p,3,8,18,7,0,180);g.DrawArc(p,3,15,18,7,0,180);break;
                default: g.DrawLine(p,4,20,15,9);g.DrawArc(p,11,2,11,11,40,270);g.DrawLine(p,4,20,7,22);break;
            }
        }
        finally {g.Restore(state);}
    }
}

internal sealed class V269SettingsPage : Panel
{
    internal readonly V269GeneralSettingsPanel General = new();
    internal readonly V269AppearancePanel Appearance = new();
    private readonly Label _hint = new() {Name="V269SettingsHint", Text="As preferências são locais. Operações administrativas continuam sujeitas à confirmação.",BackColor=Color.Transparent};
    private V269AppearanceOptions _options=V269AppearanceOptions.Default;
    private int _scroll,_height,_wheel;
    private bool _arranging;
    private Font? _hintFont;
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
    private void Reveal(Control child)
    {
        if(!IsHandleCreated || IsDisposed || !child.ContainsFocus)return;
        var r=RectangleToClient(child.RectangleToScreen(child.ClientRectangle));
        ScrollTo(_scroll+(r.Bottom>Height?r.Bottom-Height:r.Top<0?r.Top:0));
    }
    internal int ContentHeight=>_height;
    internal int ScrollOffset=>_scroll;
    internal void ScrollTo(int offset)
    {
        int value=Math.Clamp(offset,0,Math.Max(0,_height-ClientSize.Height));
        if(value==_scroll)return;
        _scroll=value;Arrange();
    }
    internal void ApplyAppearance(V269AppearanceOptions options)
    {
        _options=options.Normalize(); General.ApplyOptions(_options); Appearance.ApplyOptions(_options);
        _hint.ForeColor=V269VisualTokens.ForTheme(_options.Theme).SecondaryText;
        Font f=new("Segoe UI",7.5f*_options.TextPercent/100f);
        var old=_hintFont;_hint.Font=_hintFont=f;old?.Dispose();
        Arrange();
    }
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);Arrange();}
    protected override void OnResize(EventArgs e){base.OnResize(e);Arrange();}
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if(e is HandledMouseEventArgs handled && handled.Handled)return;
        _wheel+=e.Delta;int steps=_wheel/120;_wheel%=120;
        if(steps!=0)ScrollTo(_scroll-steps*Math.Max(24,48*DeviceDpi/96));
        if(e is HandledMouseEventArgs accepted)accepted.Handled=true;
        base.OnMouseWheel(e);
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
            int gh=General.ArrangeCard(w),ah=Appearance.ArrangeCard(w);
            int hh=Math.Max(S(24),TextRenderer.MeasureText(_hint.Text,_hint.Font,new Size(w,1000),TextFormatFlags.WordBreak).Height);
            _height=gh+gap+ah+gap+hh;
            _scroll=Math.Clamp(_scroll,0,Math.Max(0,_height-ClientSize.Height));
            General.Bounds=new Rectangle(0,-_scroll,w,gh);
            Appearance.Bounds=new Rectangle(0,gh+gap-_scroll,w,ah);
            _hint.Bounds=new Rectangle(0,gh+gap+ah+gap-_scroll,w,hh);
            General.Invalidate();Appearance.Invalidate();
        }
        finally{_arranging=false;}
    }
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)_hintFont?.Dispose();}
}
