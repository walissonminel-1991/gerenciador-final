using System.Globalization;

namespace GerenciadorAplicativos;

// Testes opt-in: sem identificadores, caminhos, nomes, conteudos ou envio.
internal static class TestDiagnosticDetail
{
    private static readonly Dictionary<string, string[]> Fields = new(StringComparer.Ordinal)
    {
        ["AMBIENTE"] = ["largura", "altura", "dpi", "logicos"],
        ["INVENTARIO"] = ["total", "sem_nome", "sem_editor", "sem_versao", "desinstalaveis", "so_inventario", "sem_desinstalacao", "sem_desinstalacao_fora_flag", "parcial"],
        ["MSIX"] = ["observados", "retidos", "falhas", "parcial"],
        ["ATUALIZACOES"] = ["avaliados", "candidatos", "incertos", "registro_antigo", "fontes", "ruckzuck_ok"],
        ["PACOTES"] = ["pacotes", "mapeados", "indisponiveis", "inconsistentes", "nao_consultados", "pnp", "wmi_brutos", "parcial"],
        ["DRIVERS"] = ["associacoes", "ids_unicos", "sem_nome", "sem_fornecedor", "sem_versao", "versao_incomum", "sem_inf", "assinatura_indeterminada", "presenca_indeterminada", "nao_presentes", "parcial"],
        ["RESIDUOS"] = ["grupos", "locais", "examinados", "grupos_avaliados", "candidatos_gerais", "filtrados_ativos", "filtrados_sem_evidencia", "grupos_omitidos", "locais_omitidos", "fontes", "indisponiveis", "falhas_leitura", "nao_avaliados_desconhecidos", "parcial"],
        ["ORIGEM"] = ["codigo", "entradas", "limite_entradas", "profundidade", "leitura", "links", "tempo", "cancelamento", "indisponivel"],
        ["LIMITES"] = ["entradas", "leitura", "profundidade", "links", "tempo", "cancelamento", "inventario", "fonte", "grupos", "locais"],
        ["FONTES"] = ["falha_registro", "falha_appdata", "falha_programdata", "falha_programfiles", "falha_startmenu", "falha_integracoes", "falha_outras", "indisp_registro", "indisp_appdata", "indisp_outras"],
        ["MANUTENCAO"] = ["instalados", "reconhecidos", "sem_desinstalador", "desinstalador_incerto", "pnp", "historicos_nao_observados", "seguranca_atencao", "seguranca_incerta", "parcial"]
    };
    private static readonly TestDiagnosticSession Session = new();
    internal static bool Enabled => Session.Enabled;
    internal static void SetProfile(int option, Func<string, bool> sink) => Session.SetProfile(option, sink);
    internal static bool Record(string domain, params (string Key, int Value)[] values) => Session.Record(domain, values);

    internal static bool TryFormat(int option, string domain,
        IReadOnlyList<(string Key, int Value)> values, out string line)
    {
        line = "";
        if (option is < 1 or > 2 || !Fields.TryGetValue(domain, out var fields) ||
            values.Count != fields.Length ||
            values.Any(x => x.Value is < 0 or > 100_000_000) ||
            values.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != fields.Length ||
            fields.Any(field => !values.Any(x => x.Key == field))) return false;
        line = "TESTES." + domain + " perfil=" + (option == 1 ? "PC_FORTE" : "PC_FRACO") +
            " " + string.Join(" ", values.Select(x => x.Key + "=" +
                x.Value.ToString(CultureInfo.InvariantCulture)));
        return line.Length < 600;
    }

}

// Estado isolável para testes; produção usa uma única sessão por processo.
// O teto inclui também liga/desliga e tentativas de escrita que falharam.
internal sealed class TestDiagnosticSession
{
    private readonly object gate = new();
    private Func<string, bool>? writer;
    private int profile, attempts;
    internal bool Enabled { get { lock (gate) return profile != 0 && attempts < 160; } }
    private bool Write(Func<string, bool> sink, string line)
    {
        if (attempts >= 160) return false;
        attempts++;
        try { return sink(line); }
        catch (Exception) { return false; } // diagnóstico nunca interrompe a função consultada
    }
    internal void SetProfile(int option, Func<string, bool> sink)
    {
        if (option is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(option));
        lock (gate)
        {
            if (profile != 0 && writer is not null) Write(writer, "TESTES.MODO estado=desativado");
            profile = 0; writer = null;
            if (option == 0 || attempts >= 160) return;
            string profileName = option == 1 ? "PC_FORTE" : "PC_FRACO";
            if (Write(sink, "TESTES.MODO perfil=" + profileName + " estado=ativo escopo=contagens") && attempts < 160)
            { writer = sink; profile = option; }
        }
    }
    internal bool Record(string domain, params (string Key, int Value)[] values)
    {
        lock (gate)
        {
            if (profile == 0 || writer is null || attempts >= 160 ||
                !TestDiagnosticDetail.TryFormat(profile, domain, values, out var line)) return false;
            if (Write(writer, line)) return true;
            profile = 0; writer = null;
            return false;
        }
    }
}
