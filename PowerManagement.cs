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
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1,
            Padding = new Padding(8, 6, 8, 6), Margin = Padding.Empty,
            BackColor = AppServices.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 203));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
            BackColor = AppServices.Background };
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(new Label { Text = "PLANOS DE ENERGIA", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = Ink,
            TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _currentPowerLabel.Text = "Consultando o plano ativo do Windows...";
        _currentPowerLabel.Dock = DockStyle.Fill;
        _currentPowerLabel.ForeColor = Muted;
        _currentPowerLabel.TextAlign = ContentAlignment.MiddleLeft;
        header.Controls.Add(_currentPowerLabel, 0, 1);
        root.Controls.Add(header, 0, 0);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            BackColor = AppServices.Background, Margin = Padding.Empty };
        for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 / 3f));
        grid.Controls.Add(PowerCard("balanced", "PADRÃO", "Equilibrado: ajusta o consumo conforme a atividade. Recomendado para uso diário.", AppServices.Blue), 0, 0);
        grid.Controls.Add(PowerCard("saver", "ECONOMIA", "Prioriza economia de energia. Pode limitar parte do desempenho.", AppServices.Green), 1, 0);
        grid.Controls.Add(PowerCard("ultimate", "DESEMPENHO MÁXIMO", "Plano oficial Ultimate Performance. Pode aumentar consumo, calor e ruído. Sem garantia de ganho em jogos.", AppServices.Orange), 2, 0);
        root.Controls.Add(grid, 0, 1);

        var hibernate = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 2,
            BackColor = PanelBackground, Padding = new Padding(11, 7, 11, 7), Margin = new Padding(3, 3, 3, 0) };
        hibernate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        hibernate.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        hibernate.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        hibernate.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        hibernate.Controls.Add(new Label { Text = "HIBERNAÇÃO", Dock = DockStyle.Fill, ForeColor = Ink,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _hibernateStateLabel.Text = "Consultando o estado...";
        _hibernateStateLabel.Dock = DockStyle.Fill;
        _hibernateStateLabel.ForeColor = Muted;
        _hibernateStateLabel.TextAlign = ContentAlignment.MiddleLeft;
        hibernate.Controls.Add(_hibernateStateLabel, 0, 1);
        _hibernateToggle = CreateSecondaryButton("Consultar...");
        _hibernateToggle.Dock = DockStyle.Fill;
        _hibernateToggle.Margin = new Padding(5, 5, 0, 5);
        _hibernateToggle.Enabled = false;
        _hibernateToggle.Click += async (_, _) => await ToggleHibernateAsync();
        _actionControls.Add(_hibernateToggle);
        hibernate.Controls.Add(_hibernateToggle, 1, 0);
        hibernate.SetRowSpan(_hibernateToggle, 2);
        root.Controls.Add(hibernate, 0, 2);
        var restoreRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2,
            Margin = new Padding(5, 2, 5, 0), BackColor = AppServices.Background };
        restoreRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        restoreRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 238));
        restoreRow.Controls.Add(new Label { Text = "Reversão disponível nesta sessão, após uma alteração feita pelo Gerenciador.",
            Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _restorePreviousPowerButton = CreateSecondaryButton("Restaurar plano anterior");
        _restorePreviousPowerButton.Dock = DockStyle.Fill;
        _restorePreviousPowerButton.Margin = new Padding(4, 5, 0, 5);
        _restorePreviousPowerButton.Enabled = false;
        _restorePreviousPowerButton.Click += async (_, _) => await RestorePreviousPowerAsync();
        _actionControls.Add(_restorePreviousPowerButton);
        restoreRow.Controls.Add(_restorePreviousPowerButton, 1, 0);
        root.Controls.Add(restoreRow, 0, 3);
        return root;
    }

    private Control PowerCard(string key, string title, string description, Color accent)
    {
        var card = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1,
            BackColor = PanelBackground, Padding = new Padding(9, 6, 9, 5), Margin = new Padding(3, 3, 3, 4) };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        card.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = Ink,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        card.Controls.Add(new Label { Text = description, Dock = DockStyle.Fill, ForeColor = Muted,
            Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.TopLeft }, 0, 1);
        var badge = new Label { Text = "Consultando...", Dock = DockStyle.Fill, ForeColor = Muted,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        _powerBadges[key] = badge;
        card.Controls.Add(badge, 0, 2);
        var button = CreateButton("Ativar", accent, Color.White);
        button.Dock = DockStyle.Fill;
        button.Enabled = false;
        button.Margin = Padding.Empty;
        button.Click += async (_, _) => await ChoosePowerPlanAsync(key, title);
        _actionControls.Add(button);
        _powerButtons[key] = button;
        card.Controls.Add(button, 0, 3);
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
            _activePowerGuid = snapshot.ActiveGuid;
            _hibernateEnabled = PowerPlanService.HibernateEnabled();
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
                _powerBadges[key].Text = selected ? "● ATIVO" : snapshot.For(key) is not null ? "Disponível" : "Pode exigir criação do plano";
                _powerBadges[key].ForeColor = selected ? Color.FromArgb(117, 221, 167) : Muted;
                _powerButtons[key].Text = selected ? "Plano ativo" : "Ativar";
                _powerButtons[key].Enabled = !_busy && !selected && snapshot.Error is null;
            }
            if (_restorePreviousPowerButton is not null)
                _restorePreviousPowerButton.Enabled = !_busy && _previousPowerGuid is not null &&
                    snapshot.ActiveGuid != _previousPowerGuid;
            if (_hibernateToggle is not null)
            {
                _hibernateToggle.Text = _hibernateEnabled switch
                {
                    true => "Desativar",
                    false => "Ativar",
                    _ => "Indisponível"
                };
                _hibernateToggle.Enabled = !_busy && _hibernateEnabled is not null;
                _hibernateStateLabel.Text = _hibernateEnabled switch
                {
                    true => "Configuração ativada. Desativar pode afetar a Inicialização Rápida.",
                    false => "Configuração desativada. Ativar requer espaço para o arquivo de hibernação.",
                    _ => "Estado não disponível neste computador. Nenhuma alteração será feita."
                };
            }
        }
        catch (Exception ex)
        {
            _currentPowerLabel.Text = "Erro na consulta de energia: " + ex.Message;
            Log("Energia: " + ex.Message);
        }
        finally { _refreshingEnergy = false; }
    }

    private async Task ChoosePowerPlanAsync(string key, string title)
    {
        if (_busy) return;
        string notice = key == "ultimate"
            ? "O plano Ultimate Performance pode consumir mais energia, aumentar calor e ruído e não garante FPS superior.\n\n"
            : "";
        if (MessageBox.Show(this, notice + "Ativar o plano " + title + " no Windows?\n\n" +
            "O Windows pode solicitar permissão administrativa. A alteração pode ser revertida a qualquer momento.",
            "Confirmar plano de energia", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
        });
        await RefreshEnergyInfoAsync();
    }

    private async Task RestorePreviousPowerAsync()
    {
        if (_busy || _previousPowerGuid is not Guid previous) return;
        if (MessageBox.Show(this, "Restaurar o plano de energia anterior desta sessão?\n\n" +
            "A alteração poderá solicitar autorização do Windows.", "Restaurar plano",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
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
        if (MessageBox.Show(this, warning, "Confirmar hibernação",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        await ExecuteAsync(enable ? "Ativando hibernação..." : "Desativando hibernação...", async () =>
        {
            await RunElevatedAsync(enable ? "hibernate-on" : "hibernate-off");
            await RefreshEnergyInfoAsync();
        });
        await RefreshEnergyInfoAsync();
    }
}
