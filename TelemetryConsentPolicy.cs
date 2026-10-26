using System.Security.Cryptography;
using System.Text;

namespace GerenciadorAplicativos;

// Aviso identico para todos. Versao/hash nao identificam pessoas ou maquinas.
internal static class TelemetryConsentPolicy
{
    internal const string Version = "gf-beta-20261026-r2";
    internal const string RemoteNotice =
        "VERSÃO BETA — participação opcional.\n\n" +
        "Finalidade: identificar frequências de falhas técnicas para corrigir o GF. " +
        "Com autorização, ao fechar normalmente o programa pode enviar por HTTPS " +
        "apenas a versão do GF, o tipo de encerramento e contagens de 14 códigos permitidos. " +
        "Não envia logs brutos, lista de programas, arquivos, caminhos, credenciais nem ID do PC.\n\n" +
        "O provedor recebe o IP para entregar a requisição e limitar abusos; não gravamos o IP no D1. " +
        "Resumos ficam até 7 dias no PC; agregados no receptor e no GitHub PRIVADO vencem no sétimo dia " +
        "contado do dia da coleta. A exclusão é tentada na próxima manutenção e pode atrasar em falhas. " +
        "Pendências de exclusão são conservadas até confirmação ou revisão do responsável. " +
        "Contagens pequenas são omitidas ou arredondadas; isso não comprova quantas pessoas participaram " +
        "nem impede inferências sobre grupos pequenos.\n\n" +
        "Recusar não restringe funções. Revogue em Bandeja > Privacidade e diagnóstico. " +
        "A revogação impede novos envios e cancela a tentativa em andamento, mas uma requisição " +
        "já recebida pelo servidor não pode ser desfeita no computador.\n\n" +
        "Esta BETA ainda não foi liberada. Responsável, contato para exercer direitos, destinatários " +
        "e política completa precisam ser publicados e aprovados antes da participação.";
    internal static string NoticeHash { get; } = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(RemoteNotice))).ToLowerInvariant();
}
