using System.Drawing;

namespace GerenciadorAplicativos;

/// <summary>
/// Identidades dos temas aprovados no protótipo V2.6.9.
/// Não contém lógica de manutenção ou de elegibilidade de operações.
/// </summary>
internal enum V269Theme
{
    AzulGf,
    Dark,
    Claro
}

internal enum V269ActionKind
{
    Atualizar,
    Remover
}

/// <summary>Tokens nativos de aparência. Não derivar autorização da cor.</summary>
internal sealed record V269Palette(
    Color Background,
    Color Sidebar,
    Color Content,
    Color RightPanel,
    Color Card,
    Color Border,
    Color Text,
    Color SecondaryText,
    Color Selection,
    Color Accent,
    Color DisabledAction,
    Color EnabledUpdate,
    Color EnabledRemove);

internal static class V269VisualTokens
{
    internal const int MinimumInterfacePercent = 90;
    internal const int MaximumInterfacePercent = 110;
    internal const int MinimumTextPercent = 90;
    internal const int MaximumTextPercent = 115;

    private static Color Hex(string html) => ColorTranslator.FromHtml(html);

    // Paleta derivada da V2.6.9: azul GF vivo, mas sem saturar o texto.
    private static readonly V269Palette Azul = new(
        Hex("#06111e"), Hex("#091a2a"), Hex("#091d30"), Hex("#0e2334"),
        Hex("#0b2033"), Hex("#25445f"), Hex("#e4edf6"), Hex("#9ab0c2"),
        Hex("#155fa6"), Hex("#2d9cdb"), Hex("#abcfe6"),
        Hex("#49ce81"), Hex("#f36c82"));

    // Dark deliberadamente neutro: preto/grafite, texto suavizado.
    private static readonly V269Palette Grafite = new(
        Hex("#0e0f11"), Hex("#1b1e21"), Hex("#202327"), Hex("#2a2e32"),
        Hex("#2b2e33"), Hex("#4d535a"), Hex("#d2d7dc"), Hex("#a7b0b8"),
        Hex("#525962"), Hex("#c7cfd6"), Hex("#9aa4ac"),
        Hex("#54cb8b"), Hex("#fa788d"));

    // Claro: fundo de gelo, texto escuro e ação desabilitada azul muito suave.
    private static readonly V269Palette Claro = new(
        Hex("#dbeaf6"), Hex("#d5eaf9"), Hex("#e4f3ff"), Hex("#eaf4fc"),
        Hex("#eef8ff"), Hex("#8db1ca"), Hex("#17374c"), Hex("#3f627a"),
        Hex("#2a7cb7"), Hex("#2374ab"), Hex("#8fb9d5"),
        Hex("#278f5d"), Hex("#e25971"));

    internal static V269Palette ForTheme(V269Theme theme) => theme switch
    {
        V269Theme.Dark => Grafite,
        V269Theme.Claro => Claro,
        _ => Azul
    };

    // Preenchimentos semânticos opacos. Azul, vermelho e verde vêm
    // dos pixels da referência marcada pelo proprietário (25/11).
    // Amarelo proposto para a escala; não é cor de texto.
    internal static (Color Blue, Color Green, Color Yellow, Color Red) MeterFillColors =>
        (Color.FromArgb(0,162,232), Color.FromArgb(34,177,76),
         Color.FromArgb(255,223,0), Color.FromArgb(237,28,36));

    // Cores de leitura independentes dos preenchimentos. O azul Dark
    // tem luminância corrigida para a superfície final #2b2e33.
    internal static (Color Blue, Color Green, Color Yellow, Color Red) MeterInkColors(V269Theme theme) =>
        theme == V269Theme.Claro
            ? (Color.FromArgb(21,105,177), Color.FromArgb(27,117,68), Color.FromArgb(146,100,8), Color.FromArgb(180,44,60))
            : (theme == V269Theme.Dark ? Color.FromArgb(54,163,224) : Color.FromArgb(45,156,219),
                Color.FromArgb(68,193,123), Color.FromArgb(232,196,75),
                theme == V269Theme.Dark ? Color.FromArgb(242,116,126) : Color.FromArgb(231,96,106));

    internal static double ContrastRatio(Color a, Color b)
    {
        static double Linear(byte value)
        {
            double x = value / 255d;
            return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4);
        }
        static double Luminance(Color c) => .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);
    }

    // Tokens de superfície/estado específicos da última cascata HTML.
    // Escopo preservado: tabela não redefine a paleta de outras páginas.
    internal static Color InventorySurface(V269Theme theme) => Hex(theme switch
    { V269Theme.Dark => "#24272b", V269Theme.Claro => "#eaf4fb", _ => "#081b2b" });
    internal static (Color Track,Color Thumb,Color Hot) InventoryScroll(V269Theme theme) => theme switch
    {
        V269Theme.Dark => (Hex("#24272b"),Hex("#686f77"),Hex("#9ca6af")),
        V269Theme.Claro => (Hex("#eaf4fc"),Hex("#8db1ca"),Hex("#5f97bf")),
        _ => (Hex("#0a1f30"),Hex("#3a5c79"),Hex("#6598bf"))
    };
    internal static (Color Head,Color Row,Color Alternate,Color Selected,Color Ink,Color Border,Color Rail) InventoryPalette(V269Theme theme) => theme switch
    {
        V269Theme.Dark => (Hex("#34383d"),Hex("#282b2f"),Hex("#2d3136"),Hex("#50575f"),Hex("#cbd1d6"),Hex("#51575e"),Hex("#c8d0d7")),
        V269Theme.Claro => (Hex("#bcdcf3"),Hex("#eaf4fb"),Hex("#e4f2fd"),Hex("#c3dff3"),Hex("#20435f"),Hex("#9bbacf"),Hex("#2374ab")),
        _ => (Hex("#17364a"),Hex("#091d2d"),Hex("#0c273a"),Hex("#1c4666"),Hex("#e4edf6"),Hex("#25445f"),Hex("#7bc2ff"))
    };
    internal static V269Palette StructuredPanel(V269Theme theme)
    {
        var p = ForTheme(theme);
        return theme switch
        {
            V269Theme.Dark => p with { RightPanel=Hex("#2a2e32"),Border=Hex("#60666d"),Text=Hex("#dce0e4"),SecondaryText=Hex("#aab4bd"),Card=Hex("#30353a") },
            V269Theme.Claro => p with { RightPanel=Hex("#eaf4fc"),Border=Hex("#9bbacf") },
            _ => p with { RightPanel=Hex("#0b2032"),Border=Hex("#25445f"),Card=Hex("#11283b") }
        };
    }
    internal static Color StructuredDivider(V269Theme theme) => Hex(theme switch
    { V269Theme.Dark => "#3c4249", V269Theme.Claro => "#b7d1e2", _ => "#1c354b" });

    internal static Color InventoryHeaderInk(V269Theme theme) => Hex(theme switch
    { V269Theme.Dark => "#cdd3d9", V269Theme.Claro => "#143e60", _ => "#bfd2df" });
    internal static Color InventoryHeaderDivider(V269Theme theme) => Hex(theme switch
    { V269Theme.Dark => "#656b72", V269Theme.Claro => "#b7d1e2", _ => "#355067" });
    internal static Color InventoryHeaderTop(V269Theme theme) =>
        theme == V269Theme.Dark ? Hex("#3b4045") : InventoryPalette(theme).Head;
    internal static Color InventoryRowInk(V269Theme theme,bool selected) => !selected ? InventoryPalette(theme).Ink : Hex(theme switch
    { V269Theme.Dark => "#e0e4e7", V269Theme.Claro => "#20435f", _ => "#eef7ff" });
    internal static Color InventoryStateInk(V269Theme theme,bool selected,bool available) =>
        selected || available ? InventoryRowInk(theme,selected) : ForTheme(theme).SecondaryText;
    internal static (Color First,Color Last,Color Border) InventorySelection(V269Theme theme) => theme switch
    {
        V269Theme.Dark => (Hex("#5a626b"),Hex("#484e56"),Hex("#aeb9c3")),
        V269Theme.Claro => (Hex("#dcebf7"),Hex("#d1e3f1"),Hex("#78a9cb")),
        _ => (Hex("#1a456d"),Hex("#143555"),Hex("#5c97c3"))
    };
    internal static Color InventoryRowDivider(V269Theme theme) => Hex(theme switch
    { V269Theme.Dark => "#3d4349", V269Theme.Claro => "#b7d1e2", _ => "#1a3345" });
    internal static Color InventorySelectionRail(V269Theme theme) =>
        theme == V269Theme.Claro ? Hex("#7bc2ff") : InventoryPalette(theme).Rail;

    // A referência final preserva gradientes por função. As transparências
    // da caixa de ícone são decorativas e não são aplicadas ao glifo/texto.
    internal static (Color First,Color Last,Color Border) NavigationSelection(V269Theme theme) => theme switch
    {
        V269Theme.Dark => (Hex("#666d74"),Hex("#50575e"),Hex("#b6c0c8")),
        V269Theme.Claro => (Hex("#6f97bc"),Hex("#5a7da1"),Hex("#7aa7d0")),
        _ => (Hex("#246aae"),Hex("#1b4f91"),Hex("#6ea9d1"))
    };
    internal static Color NavigationSelectionRail(V269Theme theme) =>
        theme == V269Theme.Dark ? Hex("#c8d0d7") : Color.White;
    internal static Color NavigationPaintInk(V269Theme theme,bool selected) =>
        theme == V269Theme.Dark ? Hex(selected ? "#e3e6e9" : "#c6cdd3") :
        selected ? Color.White : ForTheme(theme).Text;
    internal static (Color Top,Color Bottom,Color Outline) NavigationIconSurface(V269Theme theme,bool selected) => theme switch
    {
        V269Theme.Claro => (Color.FromArgb(191,Color.White),Color.FromArgb(166,Hex("#d6e8f5")),Color.FromArgb(92,Hex("#578db9"))),
        V269Theme.Dark when selected => (Color.FromArgb(43,Color.White),Color.FromArgb(12,Color.White),Color.FromArgb(107,Hex("#d9e0e7"))),
        V269Theme.Dark => (Hex("#383d42"),Hex("#292c31"),Hex("#626a72")),
        _ when selected => (Color.FromArgb(41,Color.White),Color.FromArgb(18,Color.White),Color.FromArgb(71,Color.White)),
        _ => (Color.FromArgb(15,Color.White),Color.FromArgb(5,Color.White),Color.FromArgb(71,Hex("#7091b0")))
    };
    private static readonly Color[] DarkNavigationGlyphs = [Hex("#65b8ef"),Hex("#b998f2"),Hex("#ee9d68"),Hex("#55cbb5"),Hex("#e4bb67"),Hex("#65b8ef"),Hex("#e4bb67"),Hex("#b998f2")];
    private static readonly Color[] LightNavigationGlyphs = [Hex("#167bbb"),Hex("#7046c1"),Hex("#bc592b"),Hex("#087d82"),Hex("#ad6f1d"),Hex("#167bbb"),Hex("#ad6f1d"),Hex("#7046c1")];
    private static readonly Color[] BlueNavigationGlyphs = [Hex("#75c4ed"),Hex("#b7a5f5"),Hex("#e8a784"),Hex("#6bd6b2"),Hex("#ecc27c"),Hex("#75c4ed"),Hex("#ecc27c"),Hex("#b7a5f5")];
    internal static Color NavigationGlyph(V269Theme theme,V269Page page,bool selected) =>
        selected && theme != V269Theme.Dark ? Color.White :
        (theme == V269Theme.Dark ? DarkNavigationGlyphs : theme == V269Theme.Claro ? LightNavigationGlyphs : BlueNavigationGlyphs)[(int)page];
    internal static Color WorkspaceBase(V269Theme theme) => Hex(theme switch
    { V269Theme.Dark => "#151719", V269Theme.Claro => "#deebf7", _ => "#081827" });
    internal static (Color First,Color Last,Color Border,float Angle) WorkspaceGradient(V269Theme theme,bool sidebar) => (theme,sidebar) switch
    {
        (V269Theme.Dark,true) => (Hex("#1e2024"),Hex("#17191c"),Hex("#4a5057"),90f),
        (V269Theme.Dark,false) => (Hex("#25282b"),Hex("#1c1e21"),Hex("#4d535a"),75f),
        (V269Theme.Claro,true) => (Hex("#d5eaf9"),Hex("#c6e0f2"),Hex("#8db1ca"),90f),
        (V269Theme.Claro,false) => (Hex("#e4f3ff"),Hex("#d7e9f8"),Hex("#8db1ca"),65f),
        (_,true) => (Hex("#081c31"),Hex("#0b2539"),Hex("#25445f"),90f),
        _ => (Hex("#0a2539"),Hex("#071a2b"),Hex("#25445f"),55f)
    };

    internal static (Color Back,Color Ink,Color Border) InventoryActionColors(V269Theme theme,bool remove,bool allowed)
    {
        var (back,ink,border)=(theme,remove,allowed) switch
        {
            (V269Theme.Claro,false,true)=>("#278b55","#ffffff","#2da269"),
            (V269Theme.Claro,false,false)=>("#f0f8fd","#5f97bf","#9ebed6"),
            (V269Theme.Claro,true,true)=>("#cf465e","#ffffff","#df6075"),
            (V269Theme.Claro,true,false)=>("#e5edf3","#9ba8b4","#b5c4d0"),
            (V269Theme.Dark,false,true)=>("#276e49","#e2fff1","#61c891"),
            (V269Theme.Dark,false,false)=>("#383e44","#bac3ca","#68727b"),
            (V269Theme.Dark,true,true)=>("#772d39","#ffe4e8","#f3798e"),
            (V269Theme.Dark,true,false)=>("#373c42","#929ca5","#626a73"),
            (_,false,true)=>("#1d633e","#caffdf","#46b977"),
            (_,false,false)=>("#173249","#bfd9ec","#4b6981"),
            (_,true,true)=>("#722734","#ffd9df","#f66b84"),
            _=>("#263743","#8997a3","#435766")
        };
        return (Hex(back),Hex(ink),Hex(border));
    }

    internal static (Color First,Color Last,Color Border,float Angle) SettingsCardGradient(V269Theme theme) => theme switch
        {
            V269Theme.Dark => (Hex("#303438"), Hex("#282c30"), Hex("#565c62"), 65f),
            V269Theme.Claro => (Hex("#eef8ff"), Hex("#dceefa"), Hex("#9bbacf"), 70f),
            _ => (Hex("#0d3047"), Hex("#0b2237"), Hex("#25445f"), 65f)
        };

    internal static (Color Fill,Color Outline) SettingsSwitch(V269Theme theme,bool isChecked) => theme switch
        {
            V269Theme.Dark => (Hex(isChecked ? "#858f98" : "#494f55"), Hex(isChecked ? "#c0c9d0" : "#7d858d")),
            V269Theme.Claro => (Hex(isChecked ? "#237ab9" : "#a5b3bf"), Hex(isChecked ? "#146394" : "#647d91")),
            _ => (Hex(isChecked ? "#2a7cbc" : "#2c4458"), Hex(isChecked ? "#6db0da" : "#567387"))
        };

    internal static (Color Accent,Color Meter,Color Border,Color Knob) SettingsSlider(V269Theme theme) => theme switch
        {
            V269Theme.Dark=>(Hex("#c7cfd6"),Hex("#46505b"),Hex("#565c62"),Hex("#2b2f34")),
            V269Theme.Claro=>(Hex("#2374ab"),Hex("#c3dced"),Hex("#9bbacf"),Hex("#f5fbff")),
            _=>(Hex("#2d9cdb"),Hex("#28445d"),Hex("#25445f"),Hex("#0c2439"))
        };

    internal static (Color Top,Color Bottom,Color Border,Color Ink) DropdownPalette(V269Theme theme) => theme switch
    {
        V269Theme.Dark=>(Hex("#383d43"),Hex("#30353a"),Hex("#737c85"),Hex("#d1d6db")),
        V269Theme.Claro=>(Hex("#eaf5fe"),Hex("#eaf5fe"),Hex("#8eafc7"),Hex("#17374c")),
        _=>(Hex("#0d2233"),Hex("#0b1d2b"),Hex("#3d5a70"),Hex("#e1edf7"))
    };

    internal static (Color Background,Color Border,Color Ink) SettingsButton(V269Theme theme) => theme switch
        {
            V269Theme.Dark=>(Hex("#353a40"),Hex("#737c85"),Hex("#d1d6db")),
            V269Theme.Claro=>(Hex("#e2f0fb"),Hex("#8eafc7"),Hex("#173d59")),
            _=>(Hex("#102b41"),Hex("#3d5a70"),Hex("#c5d6e1"))
        };

    internal static (string Top,string Bottom,string Border,string Ink) CleanupButton(V269Theme theme,bool primary) => theme switch
        {
            V269Theme.Claro => primary ? ("#2a8ac7","#13649e","#1c6798","#ffffff") : ("#d8eaf7","#d8eaf7","#89abc3","#173d59"),
            V269Theme.Dark => primary ? ("#6b747e","#515961","#b7c0c8","#e4e7ea") : ("#3a3f45","#3a3f45","#68717a","#d1d6db"),
            _ => primary ? ("#205f95","#205f95","#6ea7cf","#eff8ff") : ("#142f44","#142f44","#416783","#dfedf7")
        };

    internal static (string Top,string Bottom,string Border) ToolSection(V269Theme theme) => theme switch
        {
            V269Theme.Claro=>("#eef8ff","#dceefa","#8db1ca"),
            V269Theme.Dark=>("#303438","#282c30","#565c62"),
            _=>("#0d3047","#0b2237","#25445f")
        };

    internal static (string Top,string Bottom,string Border) ToolCard(V269Theme theme,bool selected) => theme switch
        {
            V269Theme.Dark => selected ? ("#50565d", "#41464c", "#b4bdc6") : ("#33373b", "#2b2f33", "#555c63"),
            V269Theme.Claro => selected ? ("#c2e0f5", "#c2e0f5", "#4a9cc9") : ("#e2f2fd", "#e2f2fd", "#8ebad6"),
            _ => selected ? ("#113048", "#113048", "#5e95bb") : ("#0d2437", "#0b2031", "#30516b")
        };

    internal static (string Top,string Bottom,string Border,string Track) DiskCard(V269Theme theme) => theme switch
        {
            V269Theme.Claro=>("#edf5fb","#edf5fb","#8db1ca","#c6ddef"),
            V269Theme.Dark=>("#33373b","#2b2f33","#555c63","#393e43"),
            _=>("#0b2134","#0b2134","#25445f","#1d394e")
        };

    internal static (string Top,string Bottom,string Border) DiskSelected(V269Theme theme) => theme switch
        {V269Theme.Dark=>("#50565d","#41464c","#b4bdc6"),V269Theme.Claro=>("#c2e0f5","#c2e0f5","#4a9cc9"),_=>("#0f2c45","#0f2c45","#478ab4")};

    internal static (Color Surface,Color Border,Color Ink) InventorySearch(V269Theme theme) => theme switch
    {
        V269Theme.Claro=>(Hex("#eaf5fe"),Hex("#8eafc7"),Hex("#17374c")),
        V269Theme.Dark=>(Hex("#353a40"),Hex("#737c85"),Hex("#d1d6db")),
        _=>(Hex("#102638"),Hex("#416783"),Hex("#dfedf7"))
    };

    internal static (Color Surface,Color Border,Color Ink) InventoryToolbar(V269Theme theme,bool primary) => theme switch
        {
            V269Theme.Claro=>(primary?Hex("#2579b4"):Hex("#e2f0fb"),Hex("#8eafc7"),primary?Color.White:Hex("#173d59")),
            V269Theme.Dark=>(primary?Hex("#515961"):Hex("#3a3f45"),primary?Hex("#b7c0c8"):Hex("#68717a"),Hex("#dce0e4")),
            _=>(primary?Hex("#205f95"):Hex("#102638"),primary?Hex("#6ea7cf"):Hex("#416783"),Hex("#dfedf7"))
        };

    internal static (Color Card,Color Border,Color Soft,Color Ink,Color Muted,Color Photo,Color TitleInk,Color NameInk,Color PhotoBorder) HomeCardPalette(V269Theme theme) => theme switch
        {
            V269Theme.Dark => (Hex("#303438"), Hex("#565c62"), Hex("#3c4249"),
                Hex("#ced4d9"), Hex("#aab4bd"), Hex("#33383d"), Hex("#dce0e4"),
                Hex("#ced4d9"), Hex("#575e65")),
            V269Theme.Claro => (Hex("#eaf4fc"), Hex("#9bbacf"), Hex("#b7d1e2"),
                Hex("#17374c"), Hex("#3f627a"), Hex("#dcecf9"), Hex("#17374c"),
                Hex("#123b5d"), Hex("#b7d1e2")),
            _ => (Hex("#0a2135"), Hex("#25445f"), Hex("#1c354b"), Hex("#e4edf6"),
                Hex("#9ab0c2"), Hex("#10283e"), Hex("#e4edf6"), Hex("#e4edf6"), Hex("#1c354b"))
        };

    internal static Color DriverGlyphInk(V269DriverGlyph glyph, V269Theme theme)
    {
        bool light = theme == V269Theme.Claro;
        string color = glyph switch
        {
            V269DriverGlyph.Audio or V269DriverGlyph.Headset or V269DriverGlyph.Chip =>
                light ? "#ac4f24" : "#f5a273",
            V269DriverGlyph.Plug or V269DriverGlyph.Gamepad =>
                light ? "#3f752b" : "#95ca74",
            V269DriverGlyph.Shield => light ? "#007882" : "#63c9b7",
            _ => light ? "#126da0" : "#72c4ed"
        };
        return Hex(color);
    }

    internal static Color RepairWrenchInk(V269Theme theme) => Hex(theme switch
    { V269Theme.Claro => "#ad690d", V269Theme.Dark => "#e7bd5f", _ => "#f2c477" });

    internal static int InterfacePercent(int percent) =>
        Math.Clamp(percent, MinimumInterfacePercent, MaximumInterfacePercent);

    internal static int TextPercent(int percent) =>
        Math.Clamp(percent, MinimumTextPercent, MaximumTextPercent);

    internal static int Scale(int logicalPixels, int interfacePercent) =>
        (int)Math.Round(logicalPixels * InterfacePercent(interfacePercent) / 100.0,
            MidpointRounding.AwayFromZero);

    // Superfícies dos botões de ação na V2.6.9. Estado sem permissão é
    // neutro, e não há terceiro sinal visual para consulta/ausência/bloqueio.
    internal static Color ActionBackground(V269Theme theme, V269ActionKind kind, bool allowed) =>
        theme switch
        {
            V269Theme.Dark => Hex(!allowed ? "#2f343a" :
                kind == V269ActionKind.Atualizar ? "#24684a" : "#5f2731"),
            V269Theme.Claro => Hex(!allowed ? "#eaf4fb" :
                kind == V269ActionKind.Atualizar ? "#237d4c" : "#cf5368"),
            _ => Hex(!allowed ? "#132c40" :
                kind == V269ActionKind.Atualizar ? "#164f35" : "#491d27")
        };

    internal static Color ActionBorder(V269Theme theme, V269ActionKind kind, bool allowed) =>
        theme switch
        {
            V269Theme.Dark => Hex(!allowed ? "#707880" :
                kind == V269ActionKind.Atualizar ? "#57bf8c" : "#ef788c"),
            V269Theme.Claro => Hex(!allowed ? "#9fc1d8" :
                kind == V269ActionKind.Atualizar ? "#3cb070" : "#d9546a"),
            _ => Hex(!allowed ? "#537ea0" :
                kind == V269ActionKind.Atualizar ? "#4fc37f" : "#ff6f89")
        };

    internal static Color ActionColor(V269Theme theme, V269ActionKind action, bool allowed)
    {
        V269Palette palette = ForTheme(theme);
        if (!allowed) return palette.DisabledAction;
        return action == V269ActionKind.Atualizar
            ? palette.EnabledUpdate : palette.EnabledRemove;
    }
}
