using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private readonly Label _cleanupPreview = new();
    private readonly Label _networkSummary = new();
    private readonly Label _networkDetails = new();
    private Button? _repairNetworkButton;
    private NetworkScan? _lastNetworkScan;

    private Control BuildNetworkResultPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3, ColumnCount = 1,
            BackColor = PanelBackground,
            Margin = new Padding(15, 2, 15, 4),
            Padding = new Padding(16, 8, 16, 8)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = "DIAGNÓSTICO DE REDE", Dock = DockStyle.Fill,
            ForeColor = Ink, Font = new Font("Segoe UI", 10.2F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _networkSummary.Text = "Clique em Analisar rede para executar os testes. Nenhuma alteração será feita.";
        _networkSummary.ForeColor = Muted;
        _networkSummary.Dock = DockStyle.Fill;
        _networkSummary.TextAlign = ContentAlignment.MiddleLeft;
        _networkDetails.Dock = DockStyle.Fill;
        _networkDetails.ForeColor = Muted;
        _networkDetails.Font = new Font("Segoe UI", 8.8F);
        _networkDetails.TextAlign = ContentAlignment.TopLeft;
        _networkDetails.Padding = new Padding(0, 4, 0, 0);
        _networkDetails.AutoEllipsis = false;
        panel.Controls.Add(_networkSummary, 0, 1);
        panel.Controls.Add(_networkDetails, 0, 2);
        return panel;
    }

    private async Task RunNetworkDiagnosticAsync()
    {
        if (_busy) return;
        await ExecuteAsync("Verificando conexão, gateway, DNS e acesso HTTPS...", async () =>
        {
            var result = await NetworkDiagnostics.AnalyzeAsync();
            _lastNetworkScan = result;
            bool actionable = GetSuggestedNetworkRepair(result) is not null;
            _networkSummary.ForeColor = result.HasWarnings ? Color.FromArgb(244, 183, 102) : Color.FromArgb(127, 217, 166);
            _networkSummary.Text = !result.HasWarnings
                ? "Rede sem falhas nos testes disponíveis. Não há reparo indicado."
                : actionable
                    ? "Foram encontrados alertas. O botão Reparar rede pode aplicar uma correção específica."
                    : "Há alertas, mas nenhuma correção automática segura foi identificada. Confira os detalhes.";
            var lines = result.Checks.Select(check => $"{check.Name}: {check.Detail}").ToArray();
            _networkDetails.Text = string.Join(Environment.NewLine, lines.Take(5)) +
                (lines.Length > 5 ? $"{Environment.NewLine}Mais {lines.Length - 5} resultado(s) no registro." : "");
            _relayoutRepair?.Invoke();
            foreach (var check in result.Checks)
            {
                Log($"Rede [{check.State}]: {check.Name} — {check.Detail}");
            }
            Log("Diagnóstico de rede somente consultivo. Falha isolada pode resultar de firewall, proxy ou bloqueio de ICMP.");
            Log(actionable ? "Uma correção orientada está disponível mediante confirmação." : "Nenhuma correção automática indicada nesta análise.");
            Status(result.HasWarnings ? "Rede: consulte os alertas do diagnóstico" : "Diagnóstico de rede concluído");
        });
    }

    // Não confunde gateway/ICMP bloqueado com problema que precisa ser corrigido.
    // Só sugere correções quando a interface e o gateway foram identificados.
    private static string? GetSuggestedNetworkRepair(NetworkScan? scan)
    {
        if (scan is null) return null;
        bool localReady = scan.Checks.Any(c => c.Name == "Adaptador" && c.State == "OK") &&
                          scan.Checks.Any(c => c.Name == "Gateway" && c.State == "OK");
        if (!localReady) return null;
        if (scan.Checks.Any(c => c.Name == "DNS" && c.State == "Atenção"))
            return "flush-dns";
        // Somente quando DNS passou e a conectividade TCP/443 falhou; pode ser
        // proxy/firewall e, portanto, a ação requer confirmação explícita.
        if (scan.Checks.Any(c => c.Name == "DNS" && c.State == "OK") &&
            scan.Checks.Any(c => c.Name == "Conexão HTTPS" && c.State == "Atenção"))
            return "network-repair-winsock";
        return null;
    }

    private async Task RepairNetworkAfterDiagnosisAsync()
    {
        if (_busy) return;
        string? task = GetSuggestedNetworkRepair(_lastNetworkScan);
        if (task is null)
        {
            MessageBox.Show(this,
                "Execute Analisar rede primeiro. Se a análise estiver normal ou não indicar uma correção segura, " +
                "nenhuma configuração será alterada. Verifique os alertas antes de usar a redefinição avançada.",
                "Reparar rede", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool dnsRepair = task == "flush-dns";
        string confirmation = dnsRepair
            ? "O teste detectou uma falha na resolução DNS. Deseja limpar apenas o cache DNS do Windows? " +
              "Isso não altera os servidores DNS configurados. O teste será repetido depois da operação."
            : "O DNS respondeu, mas a conexão TCP/443 ao endereço de teste falhou. Isso pode ocorrer por proxy, firewall " +
              "ou bloqueio do servidor, e não comprova um defeito no Windows.\n\n" +
              "Deseja tentar redefinir APENAS o catálogo Winsock? A rede pode ser interrompida e será necessário " +
              "reiniciar o Windows para completar a alteração. Nenhum reinício automático será feito.";
        if (MessageBox.Show(this, confirmation, "Confirmar reparo orientado de rede",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            Log("Reparo de rede orientado cancelado pelo usuário.");
            return;
        }

        Log("Reparo orientado selecionado pelo diagnóstico: " + (dnsRepair ? "cache DNS" : "Winsock") + ".");
        await ExecuteAsync(dnsRepair ? "Limpando cache DNS..." : "Redefinindo catálogo Winsock...",
            () => RunElevatedAsync(task));

        if (dnsRepair)
        {
            // Reexecutar os testes: não declarar sucesso apenas porque o comando terminou.
            await RunNetworkDiagnosticAsync();
        }
        else
        {
            _lastNetworkScan = null;
            _networkSummary.ForeColor = Muted;
            _networkSummary.Text = "Redefinição Winsock solicitada. Reinicie o Windows e execute Analisar rede novamente.";
            _networkDetails.Text = "O resultado depende da reinicialização. Nenhuma mudança no TCP/IP completo foi aplicada.";
            if (_repairNetworkButton is not null) _repairNetworkButton.Enabled = false;
            Log("A redefinição Winsock pode requerer reinicialização. Refaça a análise depois de reiniciar.");
        }
    }

    private async Task ShowCleanupPreviewAsync()
    {
        if (_busy) return;
        await ExecuteAsync("Estimando arquivos elegíveis sem apagar...", async () =>
        {
            var categories = await SafetyDiagnostics.EstimateCleanupAsync();
            var messages = categories.Select(c => $"{c.Category}: {c.EligibleBytes / 1048576d:N1} MiB / {c.EligibleFiles} arquivo(s)" +
                (c.Partial ? " (estimativa parcial)" : "")).ToArray();
            _cleanupPreview.Text = "Prévia: " + string.Join("  •  ", messages);
            foreach (string item in messages) Log("Prévia de limpeza: " + item);
            Status("Prévia concluída — nada foi removido");
        });
    }
}
