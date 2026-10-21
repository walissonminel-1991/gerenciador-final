using System.Globalization;

namespace GerenciadorAplicativos;

// Testes opt-in: sem identificadores, caminhos, nomes, conteudos ou envio.
internal static class TestDiagnosticDetail
{
    private static readonly Dictionary<string, string[]> Fields = new(StringComparer.Ordinal)
    {
        ["AMBIENTE"] = ["largura", "altura", "dpi", "logicos"],
        ["INVENTARIO"] = ["total", "sem_nome", "sem_editor", "sem_versao", "desinstalaveis", "so_inventario", "parcial"],
        ["ATUALIZACOES"] = ["avaliados", "candidatos", "incertos", "registro_antigo", "fontes", "ruckzuck_ok"],
        ["DRIVERS"] = ["associacoes", "ids_unicos", "sem_nome", "sem_fornecedor", "sem_versao", "versao_incomum", "sem_inf", "assinatura_indeterminada", "presenca_indeterminada", "nao_presentes", "parcial"],
        ["RESIDUOS"] = ["grupos", "locais", "examinados", "protegidos", "insuficientes", "grupos_omitidos", "locais_omitidos", "fontes", "indisponiveis", "falhas_leitura", "nao_avaliados_desconhecidos", "parcial"],
        ["LIMITES"] = ["entradas", "leitura", "profundidade", "links", "tempo", "cancelamento", "inventario", "fonte", "grupos", "locais"],
        ["FONTES"] = ["falha_registro", "falha_appdata", "falha_outras", "indisp_registro", "indisp_appdata", "indisp_outras"],
        ["MANUTENCAO"] = ["instalados", "reconhecidos", "sem_desinstalador", "desinstalador_incerto", "pnp", "historicos_nao_observados", "seguranca_atencao", "seguranca_incerta", "parcial"]
    };
    private static readonly object Gate = new();
    private static Func<string, bool>? writer;
    private static int profile, count;
    internal static bool Enabled { get { lock (Gate) return profile != 0; } }

    // Perfis selecionados pelo usuario, nunca inferidos do hardware.
    internal static void SetProfile(int option, Func<string, bool> sink)
    {
        if (option is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(option));
        lock (Gate)
        {
            if (profile != 0) writer?.Invoke("TESTES.MODO estado=desativado");
            profile = 0;
            writer = null;
            count = 0;
            if (option == 0) return;
            string profileName = option == 1 ? "PC_FORTE" : "PC_FRACO";
            if (sink("TESTES.MODO perfil=" + profileName + " estado=ativo escopo=contagens"))
            {
                writer = sink;
                profile = option;
            }
        }
    }

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

    internal static bool Record(string domain, params (string Key, int Value)[] values)
    {
        lock (Gate)
        {
            if (profile == 0 || writer is null || count >= 160 ||
                !TryFormat(profile, domain, values, out var line) || !writer(line)) return false;
            count++;
            return true;
        }
    }
}
