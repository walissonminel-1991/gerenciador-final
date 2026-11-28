using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GerenciadorAplicativos;

internal sealed record PowerPlanSnapshot(Guid? ActiveGuid, IReadOnlyDictionary<string, Guid> Available, string? Error)
{
    public Guid? For(string key) => Available.TryGetValue(key, out var id) ? id : null;
}

// Planos oficiais do Windows. Em equipamentos com Modern Standby ou restrições
// de firmware, algumas opções podem não estar disponíveis ou não ser aceitas.
internal static class PowerPlanService
{
    private static readonly Guid Balanced = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid Saver = Guid.Parse("a1841308-3541-4fab-bc81-f71556f20b4a");
    private static readonly Guid Ultimate = Guid.Parse("e9a42b02-d5df-448d-aa00-03f14749eb61");
    private static readonly Regex GuidPattern = new(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.Compiled);

    private static string Normalize(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        foreach (char ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                result.Append(char.ToLowerInvariant(ch));
        return result.ToString();
    }

    private static Guid? FirstGuid(string text)
    {
        Match match = GuidPattern.Match(text);
        return match.Success && Guid.TryParse(match.Value, out Guid id) ? id : null;
    }

    public static async Task<PowerPlanSnapshot> ReadAsync()
    {
        try
        {
            var schemes = await AppServices.RunCommandAsync("powercfg.exe", new[] { "/list" });
            if (schemes.ExitCode != 0)
                return new(null, new Dictionary<string, Guid>(), "O Windows não conseguiu listar os planos de energia.");
            var active = await AppServices.RunCommandAsync("powercfg.exe", new[] { "/getactivescheme" });
            Guid? activeGuid = active.ExitCode == 0 ? FirstGuid(active.StandardOutput) : null;
            var result = ParsePlans(schemes.StandardOutput);
            return new(activeGuid, result, active.ExitCode == 0 ? null : "Não foi possível identificar o plano ativo.");
        }
        catch (Exception ex)
        {
            return new(null, new Dictionary<string, Guid>(), "Consulta de energia: " + ex.Message);
        }
    }

    private static Dictionary<string, Guid> ParsePlans(string output)
    {
        var results = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in output.Split('\n'))
        {
            Match matched = GuidPattern.Match(line);
            if (!matched.Success || !Guid.TryParse(matched.Value, out var id)) continue;
            string label = Normalize(line[(matched.Index + matched.Length)..]);
            // GUIDs oficiais são preferidos aos nomes locais e cópias.
            if (id == Balanced) results["balanced"] = id;
            else if (id == Saver) results["saver"] = id;
            else if (id == Ultimate) results["ultimate"] = id;
            else if ((label.Contains("gerenciador final - desempenho maximo") ||
                      label.Contains("ultimate performance") || label.Contains("desempenho maximo")) &&
                     !results.ContainsKey("ultimate")) results["ultimate"] = id;
            else if ((label.Contains("economia de energia") || label.Contains("power saver") ||
                      label.Contains("economia de bateria")) && !results.ContainsKey("saver"))
                results["saver"] = id;
            else if ((label.Contains("equilibrado") || label.Contains("balanced")) &&
                     !results.ContainsKey("balanced")) results["balanced"] = id;
        }
        return results;
    }

    public static bool? HibernateEnabled()
    {
        try
        {
            object? val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", null);
            return val is int i ? i != 0 : null;
        }
        catch { return null; }
    }

    // Reversão somente para um esquema que o Windows lista como existente.
    public static async Task<int> RestorePreviousAsync(Guid previous, Action<string> log)
    {
        var schemes = await AppServices.RunCommandAsync("powercfg.exe", new[] { "/list" });
        if (schemes.ExitCode != 0 || !GuidPattern.Matches(schemes.StandardOutput)
            .Cast<Match>().Any(m => Guid.TryParse(m.Value, out Guid id) && id == previous))
        {
            log("O plano anterior não está mais disponível. Nenhuma alteração realizada.");
            return 1;
        }
        var set = await AppServices.RunCommandAsync("powercfg.exe", new[] { "/setactive", previous.ToString("D") }, log);
        if (set.ExitCode != 0) return set.ExitCode;
        var check = await AppServices.RunCommandAsync("powercfg.exe", new[] { "/getactivescheme" });
        if (check.ExitCode != 0 || FirstGuid(check.StandardOutput) != previous)
        {
            log("Não foi possível confirmar a restauração do plano anterior.");
            return 1;
        }
        log("Plano de energia anterior restaurado e confirmado.");
        return 0;
    }

    public static async Task<int> ApplyAsync(string key, Action<string> log)
    {
        Guid template = key switch
        {
            "balanced" => Balanced,
            "saver" => Saver,
            "ultimate" => Ultimate,
            _ => Guid.Empty
        };
        if (template == Guid.Empty) return 2;
        var snapshot = await ReadAsync();
        if (snapshot.Error is not null)
        {
            log(snapshot.Error);
            return 1;
        }
        Guid? target = snapshot.For(key);
        Guid? created = null;
        async Task<CommandResult> Cmd(params string[] arguments)
            => await AppServices.RunCommandAsync("powercfg.exe", arguments,
                line => { if (!string.IsNullOrWhiteSpace(line)) log(line); });
        if (target is null)
        {
            log("Plano indisponível na lista. Tentando disponibilizar o esquema oficial sem modificar outros planos...");
            var duplicate = await Cmd("/duplicatescheme", template.ToString("D"));
            if (duplicate.ExitCode != 0)
            {
                log("O Windows não permitiu criar este plano (código " + duplicate.ExitCode + ").");
                log("Alguns dispositivos e configurações de energia não aceitam esse esquema.");
                return duplicate.ExitCode;
            }
            target = FirstGuid(duplicate.StandardOutput);
            if (target is null)
            {
                log("Não foi possível identificar o GUID do plano criado; nenhuma ativação foi realizada.");
                return 1;
            }
            created = target;
            if (key == "ultimate")
            {
                // Nome fixo para reconhecer a cópia na próxima execução e não gerar duplicatas.
                var name = await Cmd("/changename", target.Value.ToString("D"), "Gerenciador Final - Desempenho Maximo",
                    "Plano oficial Ultimate Performance; pode aumentar o consumo de energia.");
                if (name.ExitCode != 0) log("Aviso: o Windows não conseguiu atribuir um nome à cópia do plano.");
            }
        }
        log("Solicitando ativação do plano " + key + "...");
        var result = await Cmd("/setactive", target.Value.ToString("D"));
        if (result.ExitCode != 0)
        {
            log("O Windows recusou a ativação do plano. Código " + result.ExitCode + ".");
            if (created is not null)
            {
                var cleanup = await Cmd("/delete", created.Value.ToString("D"));
                if (cleanup.ExitCode == 0) log("Cópia não utilizada removida.");
            }
            return result.ExitCode;
        }
        var verify = await Cmd("/getactivescheme");
        if (verify.ExitCode != 0 || FirstGuid(verify.StandardOutput) != target)
        {
            log("Não foi possível confirmar que o Windows adotou o plano solicitado.");
            return 1;
        }
        log("Plano ativado e confirmado pelo Windows.");
        return 0;
    }
}

internal sealed partial class MainForm
{
    private readonly Dictionary<string, Button> _powerButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Label> _powerBadges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Label _currentPowerLabel = new();
    private readonly Label _hibernateStateLabel = new();
    private Button? _hibernateToggle;
    private bool _refreshingEnergy;
    private Guid? _activePowerGuid;
    private bool? _hibernateEnabled;
    private Guid? _previousPowerGuid;
    private Button? _restorePreviousPowerButton;

    private Control BuildEnergyPanel()
    {
        var root=new V269CleanupSection
        {
            Name="V269EnergySection",Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,
            BackColor=Color.Transparent,Padding=new Padding(12),Margin=new Padding(0,0,0,7)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,37));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,108));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
        var heading=new V269ModuleHeading("Planos de Energia","bolt"){Dock=DockStyle.Fill};
        heading.Click+=(_,_)=>SetSystemView("energy");
        _currentPowerLabel.Text="Aguardando consulta ao Windows";
        root.Controls.Add(heading,0,0);
        var grid = new TableLayoutPanel {Name="V269PowerPlans",Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty,BackColor=Color.Transparent};
        for(int i=0;i<3;i++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        grid.Controls.Add(PowerCard("balanced","Padrão","Equilíbrio no uso diário.",AppServices.Blue),0,0);
        grid.Controls.Add(PowerCard("saver","Economia","Menor consumo de energia.",AppServices.Green),1,0);
        grid.Controls.Add(PowerCard("ultimate","Desempenho máximo","Prioriza desempenho.",AppServices.Orange),2,0);
        root.Controls.Add(grid,0,1);
        var secondary=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,BackColor=Color.Transparent,Margin=Padding.Empty};
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,83));
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,142));
        secondary.Controls.Add(new Label{Text="Hibernação",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Ink},0,0);
        _hibernateToggle=new V269SystemSwitch("V269Hibernation","Hibernação"){Dock=DockStyle.None,Anchor=AnchorStyles.Left,Size=new Size(43,24),Enabled=false,Margin=Padding.Empty};
        _hibernateToggle.Click+=async (_,_)=>{SetSystemView("energy");await ToggleHibernateAsync();};
        _actionControls.Add(_hibernateToggle);secondary.Controls.Add(_hibernateToggle,1,0);
        _hibernateStateLabel.Text="Aguardando consulta";_hibernateStateLabel.Dock=DockStyle.Fill;_hibernateStateLabel.TextAlign=ContentAlignment.MiddleLeft;_hibernateStateLabel.ForeColor=Muted;
        secondary.Controls.Add(_hibernateStateLabel,2,0);
        _restorePreviousPowerButton=new V269CleanupButton("Restaurar plano","refresh"){Dock=DockStyle.Fill,Margin=new Padding(1,5,0,5),Enabled=false,AccessibleName="Restaurar plano anterior"};
        _restorePreviousPowerButton.Click+=async (_,_)=>{SetSystemView("energy");await RestorePreviousPowerAsync();};
        _actionControls.Add(_restorePreviousPowerButton);secondary.Controls.Add(_restorePreviousPowerButton,3,0);
        root.Controls.Add(secondary,0,2);
        bool? compact=null;
        void Fit()
        {
            if(root.Width<=0)return;
            int S(int n)=>V269AppTable.Scale(n,_v269Appearance.InterfacePercent,root.DeviceDpi);
            bool small=root.ClientSize.Width-root.Padding.Horizontal<S(415);
            if(compact!=small)
            {
                compact=small;secondary.SuspendLayout();
                try
                {
                    secondary.SetCellPosition(_restorePreviousPowerButton,new TableLayoutPanelCellPosition(small?0:3,small?1:0));
                    secondary.SetColumnSpan(_restorePreviousPowerButton,small?4:1);
                    secondary.ColumnStyles[3].Width=small?0:S(142);
                    secondary.RowCount=small?2:1;secondary.RowStyles.Clear();
                    for(int i=0;i<secondary.RowCount;i++)secondary.RowStyles.Add(new RowStyle(SizeType.Percent,100f/secondary.RowCount));
                }
                finally{secondary.ResumeLayout(true);}
            }
            int columns=root.ClientSize.Width-root.Padding.Horizontal<S(325)?1:3;
            if(grid.ColumnCount!=columns)
            {
                grid.SuspendLayout();
                var cards=grid.Controls.Cast<Control>().ToArray();grid.ColumnCount=columns;grid.RowCount=3/columns;
                grid.ColumnStyles.Clear();grid.RowStyles.Clear();
                for(int i=0;i<columns;i++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/columns));
                for(int i=0;i<grid.RowCount;i++)grid.RowStyles.Add(new RowStyle(SizeType.Percent,100f/grid.RowCount));
                for(int i=0;i<cards.Length;i++)grid.SetCellPosition(cards[i],new TableLayoutPanelCellPosition(i%columns,i/columns));
                grid.ResumeLayout(true);
            }
            root.RowStyles[0].Height=S(34);
            root.RowStyles[1].Height=(3/columns)*S(root.Width<S(535)?126:108);
            root.RowStyles[2].Height=S(small?76:42);
        }
        root.SizeChanged+=(_,_)=>Fit();root.Tag=(Action)Fit;Fit();
        return root;
    }

    private Control PowerCard(string key,string title,string description,Color accent)
    {
        var card=new V269CleanupSection{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1,BackColor=Color.Transparent,
            Padding=new Padding(8,6,8,5),Margin=new Padding(0,1,6,3),AccessibleName=title,TabStop=true,IsPlanCard=true};
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute,24));card.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute,17));card.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
        var name=new V269ModuleHeading(title,key=="balanced"?"monitor":key=="saver"?"cloud":"bolt",9.75f){Dock=DockStyle.Fill};
        var hint=new Label{Text=description,Dock=DockStyle.Fill,ForeColor=Muted,Font=new Font("Segoe UI",8.5f)};
        card.Controls.Add(name,0,0);card.Controls.Add(hint,0,1);
        name.Click+=(_,_)=>SetSystemView("energy");hint.Click+=(_,_)=>SetSystemView("energy");card.Click+=(_,_)=>SetSystemView("energy");
        var badge=new Label{Text="Aguardando consulta",Dock=DockStyle.Fill,ForeColor=Muted,Font=new Font("Segoe UI",8f),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
        _powerBadges[key]=badge;card.Controls.Add(badge,0,2);
        var button=new V269CleanupButton("Ativar","check"){Dock=DockStyle.None,Anchor=AnchorStyles.Right|AnchorStyles.Bottom,
            Size=new Size(87,28),Enabled=false,Margin=Padding.Empty,AccessibleName="Ativar plano "+title};
        button.Click+=async (_,_)=>{SetSystemView("energy");await ChoosePowerPlanAsync(key,title);};
        _actionControls.Add(button);_powerButtons[key]=button;card.Controls.Add(button,0,3);
        card.SizeChanged+=(_,_)=>{button.Width=Math.Min(96,Math.Max(1,card.ClientSize.Width-card.Padding.Horizontal));};
        return card;
    }

    private async Task RefreshEnergyInfoAsync()
    {
        if (_refreshingEnergy || IsDisposed) return;
        _refreshingEnergy = true;
        try
        {
            var snapshot = await PowerPlanService.ReadAsync();
            if (IsDisposed) return;
            ApplyV269PowerSnapshot(snapshot,PowerPlanService.HibernateEnabled());
        }
        catch (Exception ex)
        {
            _currentPowerLabel.Text = "Erro na consulta de energia: " + ex.Message;
            Log("Energia: " + ex.Message);
        }
        finally { _refreshingEnergy = false; if(_nav.SelectedPage==V269Page.Sistema) PresentV269SystemDetails(); }
    }

    private void ApplyV269PowerSnapshot(PowerPlanSnapshot snapshot,bool? hibernate)
    {
            _activePowerGuid = snapshot.ActiveGuid;
            _hibernateEnabled = hibernate;
            string? activeKey = snapshot.Available.FirstOrDefault(p => p.Value == _activePowerGuid).Key;
            _currentPowerLabel.Text = snapshot.Error ?? (activeKey switch
            {
                "balanced" => "Plano ativo: Padrão (Equilibrado)",
                "saver" => "Plano ativo: Economia de energia",
                "ultimate" => "Plano ativo: Desempenho Máximo (Ultimate Performance)",
                _ when _activePowerGuid is not null => "Plano ativo: Personalizado ou não identificado entre os três padrões",
                _ => "Não foi possível identificar o plano ativo"
            });
            foreach (string key in _powerButtons.Keys)
            {
                bool selected = snapshot.ActiveGuid is not null && snapshot.For(key) == snapshot.ActiveGuid;
                _powerBadges[key].Text = snapshot.Error is not null ? "Indisponível" : selected ? "" : snapshot.For(key) is not null ? "" : "Pode exigir criação";
                if(_powerButtons[key].Parent is V269CleanupSection card)card.SetPlanActive(selected);
                _powerBadges[key].ForeColor = selected ? V269AppDetailStyle.Palette(_v269Appearance.Theme).Text : Muted;
                _powerButtons[key].Text = selected ? "Ativo" : "Ativar";
                _powerButtons[key].Enabled = !_busy && !selected && snapshot.Error is null;
            }
            if (_restorePreviousPowerButton is not null)
                _restorePreviousPowerButton.Enabled = !_busy && _previousPowerGuid is not null &&
                    snapshot.ActiveGuid != _previousPowerGuid;
            if (_hibernateToggle is not null)
            {
                if(_hibernateToggle is V269SystemSwitch toggle)toggle.SetState(_hibernateEnabled);
                _hibernateToggle.Text = _hibernateEnabled switch
                {
                    true => "Desativar",
                    false => "Ativar",
                    _ => "Indisponível"
                };
                _hibernateToggle.Enabled = !_busy && _hibernateEnabled is not null;
                _hibernateStateLabel.Text = _hibernateEnabled switch
                {
                    true => "Ativada",
                    false => "Desativada",
                    _ => "Estado não disponível neste computador. Nenhuma alteração será feita."
                };
            }
    }

    private async Task ChoosePowerPlanAsync(string key, string title)
    {
        if (_busy) return;
        string notice = key == "ultimate"
            ? "O plano Ultimate Performance pode consumir mais energia, aumentar calor e ruído e não garante FPS superior.\n\n"
            : "";
        if (!await ConfirmV269Async("Confirmar plano de energia", notice + "Ativar o plano " + title + " no Windows?\n\n" +
            "O Windows pode solicitar permissão administrativa. A alteração pode ser revertida a qualquer momento."))
            return;
        Guid? before = (await PowerPlanService.ReadAsync()).ActiveGuid;
        await ExecuteAsync("Alterando plano de energia...", async () =>
        {
            await RunElevatedAsync("power-plan-" + key);
            var after = await PowerPlanService.ReadAsync();
            if (before is not null && after.ActiveGuid is not null && after.ActiveGuid != before)
            {
                _previousPowerGuid = before;
                Log("Plano anterior guardado somente na memória desta sessão para reversão.");
            }
            bool verified=after.ActiveGuid is Guid active && after.For(key)==active;
            PresentV269ModuleResult(V269Page.Sistema,"Ativar "+title,verified?"Plano ativo confirmado pelo Windows.":"Não foi possível confirmar a ativação do plano.",verified);
        });
        await RefreshEnergyInfoAsync();
    }

    private async Task RestorePreviousPowerAsync()
    {
        if (_busy || _previousPowerGuid is not Guid previous) return;
        if (!await ConfirmV269Async("Restaurar plano", "Restaurar o plano de energia anterior desta sessão?\n\n" +
            "A alteração poderá solicitar autorização do Windows.")) return;
        await ExecuteAsync("Restaurando plano anterior...", async () =>
        {
            await RunElevatedAsync("power-plan-restore:" + previous.ToString("D"));
            var now = await PowerPlanService.ReadAsync();
            if (now.ActiveGuid == previous)
            {
                _previousPowerGuid = null;
                Log("Plano anterior restaurado; reversão da sessão concluída.");
            }
            else Log("Restauração não confirmada; consulte o registro da operação.");
            PresentV269ModuleResult(V269Page.Sistema,"Restaurar plano anterior",now.ActiveGuid==previous?"Plano anterior confirmado.":"Restauração não confirmada.",now.ActiveGuid==previous);
        });
        await RefreshEnergyInfoAsync();
    }

    private async Task ToggleHibernateAsync()
    {
        if (_busy || _hibernateEnabled is null) return;
        bool enable = !_hibernateEnabled.Value;
        string warning = enable
            ? "Ativar a hibernação? O Windows poderá reservar espaço no disco para o arquivo hiberfil.sys."
            : "Desativar a hibernação? Isso pode impedir o uso da Inicialização Rápida e remover o arquivo hiberfil.sys.";
        if (!await ConfirmV269Async("Confirmar hibernação", warning)) return;
        await ExecuteAsync(enable ? "Ativando hibernação..." : "Desativando hibernação...", async () =>
        {
            await RunElevatedAsync(enable ? "hibernate-on" : "hibernate-off");
            await RefreshEnergyInfoAsync();
            bool verified=_hibernateEnabled==enable;
            PresentV269ModuleResult(V269Page.Sistema,"Hibernação",verified?"Estado confirmado pelo Windows.":"Alteração não confirmada.",verified);
        });
        await RefreshEnergyInfoAsync();
    }
}
