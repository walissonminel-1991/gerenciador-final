using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using GerenciadorAplicativos;

internal static class GfMonitorSettingsTests
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Configurações Monitor GF: " + message);
    }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control next in All(child)) yield return next;
        }
    }

    internal static void Run()
    {
        using var panel = new GfMonitorSettingsPanel();
        int changes = 0;
        panel.PreferencesChanged += (_, _) => changes++;
        var defaults = GfResourceMonitorPreferences.Default;
        var preferences = defaults with { Enabled = true, Expanded = true, AlwaysOnTop = true, OpacityPercent = 75,
            Fixed = false, ScalePercent = 125, MonitorTheme = V269Theme.Claro, X = -30, Y = 20 };
        panel.LoadPreferences(preferences);
        Require(changes == 0 && panel.ReadPreferences(preferences) == preferences, "hidratação sem efeitos colaterais");
        var enable = (CheckBox)panel.Controls.Find("GfMonitorEnabled", true).Single();
        var fix = (CheckBox)panel.Controls.Find("GfMonitorFixed", true).Single();
        var overlay = (CheckBox)panel.Controls.Find("GfMonitorOverlay", true).Single();
        var startup = (CheckBox)panel.Controls.Find("GfMonitorStartup", true).Single();
        var opacity = (TrackBar)panel.Controls.Find("GfMonitorTransparency", true).Single();
        var scale = (TrackBar)panel.Controls.Find("GfMonitorScale", true).Single();
        var themeChoice = (ComboBox)panel.Controls.Find("GfMonitorTheme", true).Single();
        var inputs = All(panel).Where(c => c is CheckBox or TrackBar or ComboBox).ToArray();
        Require(inputs.Length == 7 && inputs.All(c => c.TabStop), "exatamente sete controles navegáveis");
        Require(!startup.Enabled && !startup.AutoCheck, "startup sem confirmação não é ação real");
        panel.LoadStartupState(true,true);
        Require(startup.Checked && startup.Enabled && !panel.RequestedStartupState(), "estado Windows observado");
        panel.LoadStartupState(false,true);
        Require(!startup.Checked && panel.RequestedStartupState() && changes == 0, "observação não grava preferências");
        enable.Checked = false; opacity.Value = 10; fix.Checked = true; scale.Value = 140; themeChoice.SelectedIndex = 2;
        var result = panel.ReadPreferences(preferences with { X = -100, Y = 50 });
        Require(changes == 5 && !result.Enabled && result.OpacityPercent == 90 && result.Fixed &&
            result.ScalePercent == 140 && result.MonitorTheme == V269Theme.Dark && result.AlwaysOnTop &&
            result.Expanded && result.X == -100 && result.Y == 50, "preferências independentes conservam posição e modo");
        overlay.Checked = false;
        Require(panel.ReadPreferences(result).Fixed && !panel.ReadPreferences(result).AlwaysOnTop, "fixar não equivale a sobrepor");
        foreach (V269Theme theme in Enum.GetValues<V269Theme>())
        {
            panel.Configure(theme);
            Require(panel.BackColor == V269VisualTokens.ForTheme(theme).Card &&
                enable.ForeColor == V269VisualTokens.ForTheme(theme).Text, "tokens dos três temas");
        }
        Require(changes == 6 && panel.ReadPreferences(result).MonitorTheme == V269Theme.Dark,
            "tema da página não altera tema do monitor");
        VerifyShellSettings(preferences);
        VerifySessionOwnership();
        Console.WriteLine("OK Monitor GF: sete configurações, tema independente e inicialização observada.");
    }

    private static void VerifyShellSettings(GfResourceMonitorPreferences preferences)
    {
        using var view = new V269SettingsPage { Dock = DockStyle.Fill };
        using var form = new Form { ClientSize = new System.Drawing.Size(760, 596), ShowInTaskbar = false };
        var options = new V269MonitorOptions();
        view.AddMonitor(options);
        form.Controls.Add(view);
        form.Show();
        var general = view.Controls.Find("V269GeneralTab", true).OfType<V269SettingsTab>().Single();
        var monitor = view.Controls.Find("V269MonitorTab", true).OfType<V269SettingsTab>().Single();
        Require(general.Selected && !monitor.Selected && !options.Visible && view.General.Tray.Visible,
            "Geral é selecionada ao abrir Configurações");
        int changes = 0;
        bool? startupRequested = null;
        var edited = preferences;
        options.PreferenceChanged += change => { ++changes; edited = change(edited); };
        options.DisplaySizeChanged += (scale, opacity) =>
        { ++changes; edited = edited with { ScalePercent = scale, OpacityPercent = opacity }; };
        options.StartupChanged += enabled => startupRequested = enabled;
        options.LoadObserved(preferences, null);
        monitor.AccessibilityObject.DoDefaultAction();
        Require(monitor.Selected && !general.Selected && options.Visible && !view.General.Tray.Visible &&
            view.Appearance.Visible && view.Appearance.Top > view.General.Bottom,
            "Monitor substitui somente o conteúdo do cartão Geral");
        Require(All(options).Count(c => c is CheckBox or TrackBar or ComboBox) == 7 && changes == 0,
            "sete opções no shell, sem efeitos durante hidratação");
        var startup = options.Controls.Find("V269MonitorStartup", true).OfType<CheckBox>().Single();
        Require(!startup.Enabled, "inicialização desconhecida é indisponível");
        options.LoadObserved(preferences, true);
        Require(startup.Checked && startup.Enabled && startup.AccessibleDescription?.Contains("depende") == true &&
            options.Controls.Find("V269MonitorLabel3", true).Single().Text.Contains("registrado"),
            "registro observado é apresentado separado da execução efetiva no Windows");
        options.LoadObserved(preferences, false);
        typeof(CheckBox).GetMethod("OnClick", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic)!.Invoke(startup, [EventArgs.Empty]);
        Require(startupRequested == true && !startup.Checked && changes == 0,
            "solicitação de startup só muda após leitura observada");
        options.Controls.Find("V269MonitorScale", true).OfType<TrackBar>().Single().Value = 130;
        options.Controls.Find("V269MonitorTransparency", true).OfType<TrackBar>().Single().Value = 20;
        Require(changes == 2 && edited.ScalePercent == 130 && edited.OpacityPercent == 80 &&
            edited.X == preferences.X && edited.Y == preferences.Y && edited.Expanded == preferences.Expanded,
            "edições de escala preservam modo e posição atuais");
        foreach (var theme in Enum.GetValues<V269Theme>())
        {
            view.ApplyAppearance(new V269AppearanceOptions(theme, 100, 100, true));
            Require(monitor.Accent == V269VisualTokens.ForTheme(theme).Accent && options.Width > 0,
                "seção selecionada reutiliza os tokens dos três temas");
        }
        general.AccessibilityObject.DoDefaultAction();
        Require(general.Selected && !options.Visible && view.General.Tray.Visible && view.Appearance.Visible,
            "retornar a Geral preserva os controles originais");
    }

    // Janela principal nativa, sem serviços, com dados sintéticos.
    // Executado fora de WORK18, no contrato BETA LOCAL já existente.
    internal static void RunMainFormVisual()
    {
        using var form = new MainForm(startServices: false);
        form.Show();
        Application.DoEvents();
        var shell = form.Controls.Find("V269Shell", true).OfType<V269Shell>().Single();
        shell.SelectPage(V269Page.Configuracoes);
        var page = (V269SettingsPage)shell.PageContent(V269Page.Configuracoes)!;
        var appearance = page.Appearance;
        var options = MainForm.MountV269MonitorOptions(page);
        var general = page.Controls.Find("V269GeneralTab", true).OfType<V269SettingsTab>().Single();
        var monitor = page.Controls.Find("V269MonitorTab", true).OfType<V269SettingsTab>().Single();
        var surface = form.Controls.OfType<TableLayoutPanel>().Single();
        var nativeSize = surface.Size;
        var themeChoice = appearance.Controls.Find("V269ThemeChoice", true).OfType<ComboBox>().Single();
        var inputs = All(options).Where(c => c is CheckBox or TrackBar or ComboBox).ToArray();
        Require(inputs.Length == 7 && inputs.All(c => c.TabStop), "sete opções da janela principal");
        var fixture = GfResourceMonitorPreferences.Default with
        { Enabled = true, ScalePercent = 125, OpacityPercent = 75, MonitorTheme = V269Theme.Claro };
        options.LoadObserved(fixture, null);
        var startup = options.Controls.Find("V269MonitorStartup", true).OfType<CheckBox>().Single();
        Require(!startup.Enabled, "startup desconhecido não é habilitado");
        options.LoadObserved(fixture, false);
        Directory.CreateDirectory("LayoutCaptures");
        // Resolução da superfície em pixels, sem mudar a resolução física do runner.
        foreach (var size in new[] { new System.Drawing.Size(1440, 900), new System.Drawing.Size(2560, 1440) })
        foreach (var theme in Enum.GetValues<V269Theme>())
        {
            surface.Size = size;
            themeChoice.SelectedIndex = (int)theme;
            page.ApplyAppearance(new V269AppearanceOptions(theme, 100, 100, true));
            general.AccessibilityObject.DoDefaultAction();
            Require(general.Selected && !options.Visible && page.General.Tray.Visible &&
                ReferenceEquals(appearance, page.Appearance), "Geral e Aparência preservados");
            monitor.AccessibilityObject.DoDefaultAction();
            page.ScrollTo(0);
            surface.PerformLayout(); page.PerformLayout(); Application.DoEvents();
            Require(surface.Size == size && monitor.Selected && !general.Selected && options.Visible &&
                !page.General.Tray.Visible && appearance.Visible && appearance.Top > page.General.Bottom &&
                !shell.RightPane.Visible, "geometria da janela, seleção e Aparência preservadas");
            var rows = Enumerable.Range(0, 7).Select(i =>
                options.Controls.Find("V269MonitorLabel" + i, true).OfType<Label>().Single()).ToArray();
            Require(rows.All(label => label.Visible && options.ClientRectangle.Contains(label.Bounds)) &&
                inputs.All(input => input.Visible && input.Enabled && options.ClientRectangle.Contains(input.Bounds)),
                "sete linhas sem recorte");
            for (int i = 0; i < rows.Length; i++)
            {
                var label = rows[i];
                var input = inputs[i];
                var required = TextRenderer.MeasureText(label.Text, label.Font,
                    new System.Drawing.Size(label.Width, 1000), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                Require(required.Height <= label.Height && label.Right <= input.Left &&
                    input.Top >= label.Top && input.Bottom <= label.Bottom &&
                    Math.Abs((input.Top + input.Bottom) - (label.Top + label.Bottom)) <= 1 &&
                    (i == 0 || rows[i-1].Bottom <= label.Top),
                    $"alinhamento e texto integral: {label.Text}; tema={theme}; superfície={size}; " +
                    $"rótulo={label.Bounds}; controle={input.Bounds}; texto={required}");
                var backgrounds = theme switch
                {
                    V269Theme.Dark => new[] { "#303438", "#282c30" },
                    V269Theme.Claro => new[] { "#eef8ff", "#dceefa" },
                    _ => new[] { "#0d3047", "#0b2237" }
                };
                Require(backgrounds.All(hex => Contrast(label.ForeColor,
                    System.Drawing.ColorTranslator.FromHtml(hex)) >= 4.5), "contraste do texto: " + label.Text);
                Require(input.Focus(), "foco por teclado: " + input.AccessibleName);
                Application.DoEvents();
                Require(LayoutTests.FullyVisible(input), "foco revela opção: " + input.AccessibleName);
            }
            Require(Contrast(themeChoice.ForeColor, themeChoice.BackColor) >= 4.5,
                "contraste do seletor Aparência");
            var monitorTheme = options.Controls.Find("V269MonitorTheme", true).OfType<ComboBox>().Single();
            Require(monitorTheme.SelectedIndex == 1 && monitorTheme.Items.Count == 3 &&
                Contrast(monitorTheme.ForeColor, monitorTheme.BackColor) >= 4.5, "tema independente e contraste");
            Require(!Application.OpenForms.OfType<GfResourceMonitorForm>().Any(), "nenhum Monitor real iniciado");
            monitor.Focus();
            page.ScrollTo(0); Application.DoEvents();
            using var capture = new System.Drawing.Bitmap(size.Width, size.Height);
            surface.DrawToBitmap(capture, new System.Drawing.Rectangle(System.Drawing.Point.Empty, size));
            capture.Save($"LayoutCaptures/v269-configuracoes-monitor-SINTETICO-{theme}-{size.Width}x{size.Height}.png");
            Console.WriteLine($"PASS Monitor Configurações {theme} {size.Width}x{size.Height}: sete opções, contraste, alinhamento, foco e Aparência.");
        }
        surface.Size=nativeSize;surface.PerformLayout();Application.DoEvents();
        VerifyNativeThemeSelection(form,shell,page,options,monitor,themeChoice,fixture);
        general.AccessibilityObject.DoDefaultAction();
        Require(!options.Visible && page.General.Tray.Visible && appearance.Visible, "retorno a Geral");
        form.Close();

        static double Contrast(System.Drawing.Color a, System.Drawing.Color b)
        {
            static double L(System.Drawing.Color c)
            {
                static double C(byte x) { double v = x / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
                return .2126 * C(c.R) + .7152 * C(c.G) + .0722 * C(c.B);
            }
            double x = L(a), y = L(b);
            return (Math.Max(x,y) + .05) / (Math.Min(x,y) + .05);
        }
    }

    // Percurso nativo de ponteiro em janela descartável, sem serviços,
    // preferências reais, Registro ou coleta. O hit-test antecede cada clique.
    private static void VerifyNativeThemeSelection(MainForm form,V269Shell shell,V269SettingsPage page,
        V269MonitorOptions options,V269SettingsTab monitor,ComboBox global,GfResourceMonitorPreferences fixture)
    {
        var local=options.Controls.Find("V269MonitorTheme",true).OfType<ComboBox>().Single();
        var observed=fixture;int edits=0;
        void Edit(Func<GfResourceMonitorPreferences,GfResourceMonitorPreferences> change)
        { observed=change(observed);edits++; }
        options.PreferenceChanged+=Edit;
        bool oldTop=form.TopMost;Point oldCursor=Cursor.Position;
        try
        {
            form.TopMost=true;form.Activate();
            global.SelectedIndex=(int)V269Theme.AzulGf;
            options.LoadObserved(fixture,false);monitor.PerformClick();page.ScrollTo(0);
            Application.DoEvents();
            Require(shell.Theme==V269Theme.AzulGf&&local.SelectedIndex==1&&edits==0,
                "hidratação antes da prova por ponteiro");
            ChooseNativeItem(local,2);
            Require(local.SelectedIndex==2&&observed.MonitorTheme==V269Theme.Dark&&edits==1&&
                global.SelectedIndex==(int)V269Theme.AzulGf&&shell.Theme==V269Theme.AzulGf,
                "clique no Dark do Monitor não modifica o GF");
            ChooseNativeItem(global,(int)V269Theme.Claro);
            Require(shell.Theme==V269Theme.Claro&&global.SelectedIndex==(int)V269Theme.Claro&&
                local.SelectedIndex==2&&observed.MonitorTheme==V269Theme.Dark&&edits==1,
                "clique em Claro do GF não modifica Monitor");
            ChooseNativeItem(local,0);
            Require(local.SelectedIndex==0&&observed.MonitorTheme==V269Theme.AzulGf&&edits==2&&
                shell.Theme==V269Theme.Claro,"retorno ao Clássico do Monitor é independente");
            Console.WriteLine("PASS REC-FIS-MON-INPUT: hit-test nativo, três seleções reais de lista e isolamento recíproco.");
        }
        finally
        {
            local.DroppedDown=false;global.DroppedDown=false;
            options.PreferenceChanged-=Edit;
            Cursor.Position=oldCursor;form.TopMost=oldTop;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ComboInfo
    {
        public int Size;public NativeRect Item,Button;public int State;
        public IntPtr Combo,Edit,List;
    }
    [DllImport("user32.dll")] private static extern bool GetComboBoxInfo(IntPtr combo,ref ComboInfo info);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent,IntPtr child);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd,ref Point point);
    [DllImport("user32.dll",EntryPoint="SendMessageW")]
    private static extern IntPtr GetListItemRect(IntPtr hwnd,uint message,IntPtr index,ref NativeRect rect);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
    private static void ChooseNativeItem(ComboBox combo,int index)
    {
        Require(combo.Focus(),"foco revela seletor "+combo.Name);Application.DoEvents();
        Require(LayoutTests.FullyVisible(combo),"seletor nativo totalmente visível "+combo.Name);
        var info=new ComboInfo{Size=Marshal.SizeOf<ComboInfo>()};
        Require(GetComboBoxInfo(combo.Handle,ref info),"geometria nativa "+combo.Name);
        var arrow=combo.PointToScreen(new Point((info.Button.Left+info.Button.Right)/2,
            (info.Button.Top+info.Button.Bottom)/2));
        void Click(Point point,IntPtr target)
        {
            var hit=WindowFromPoint(point);
            Require(hit==target||IsChild(target,hit),"hit-test não pertence ao controle esperado "+combo.Name);
            Cursor.Position=point;
            mouse_event(0x0002,0,0,0,UIntPtr.Zero);mouse_event(0x0004,0,0,0,UIntPtr.Zero);
        }
        void PumpUntil(Func<bool> condition,string message)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            do {Application.DoEvents();if(condition())return;System.Threading.Thread.Sleep(10);}
            while(watch.ElapsedMilliseconds<1000);
            Require(condition(),message);
        }
        Click(arrow,combo.Handle);
        PumpUntil(()=>combo.DroppedDown,"clique deve abrir lista "+combo.Name);
        Require(GetComboBoxInfo(combo.Handle,ref info)&&info.List!=IntPtr.Zero,"HWND da lista "+combo.Name);
        var rect=new NativeRect();
        Require(GetListItemRect(info.List,0x0198,(IntPtr)index,ref rect)!=new IntPtr(-1),"retângulo do item");
        var item=new Point((rect.Left+rect.Right)/2,(rect.Top+rect.Bottom)/2);
        Require(ClientToScreen(info.List,ref item),"coordenada do item");
        Click(item,info.List);
        PumpUntil(()=>!combo.DroppedDown&&combo.SelectedIndex==index,"clique deve escolher o item real "+combo.Name);
    }

    private static void VerifySessionOwnership()
    {
        const string path = @"C:\GF Teste\GerenciadorFinal.exe";
        Require(StartupTrayRegistration.MonitorCommand(path) == $"\"{path}\" --monitor" &&
            StartupTrayRegistration.Command(path) == $"\"{path}\" --tray" &&
            StartupTrayRegistration.MonitorValueName != StartupTrayRegistration.ValueName,
            "comandos do mesmo executável possuem entradas independentes");
        string role = "Fixture." + Guid.NewGuid().ToString("N");
        Require(!GfMonitorSessionLease.IsPresent(role), "consultar presença não cria o objeto da sessão");
        bool TryClaim()
        {
            using var lease = GfMonitorSessionLease.TryTake(role);
            return lease is not null;
        }
        using (var owner = GfMonitorSessionLease.TryTake(role))
        {
            Require(owner is not null && GfMonitorSessionLease.IsPresent(role) &&
                !Task.Run(TryClaim).GetAwaiter().GetResult(),
                "segunda thread não assume uma sessão ocupada");
        }
        Require(!GfMonitorSessionLease.IsPresent(role), "consulta não mantém uma sessão já encerrada");
        Require(Task.Run(TryClaim).GetAwaiter().GetResult(), "sessão pode ser retomada após o proprietário sair");
    }
}
