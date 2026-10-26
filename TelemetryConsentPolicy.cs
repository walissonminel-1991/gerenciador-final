using System.Security.Cryptography;
using System.Text;

namespace GerenciadorAplicativos;

// Aviso identico para todos. Versao/hash nao identificam pessoas ou maquinas.
internal static class TelemetryConsentPolicy
{
    internal const string Version = "gf-beta-20261026-r3";
    internal const string RemoteNotice =
        "BETA .66 — participação opcional. Aviso gf-beta-20261026-r3.\n\n" +
        "Finalidade: descobrir frequências de falhas técnicas e melhorar a estabilidade e a compatibilidade do GF. " +
        "Com seu aceite específico, ao sair o programa tenta enviar por HTTPS somente a versão do GF, " +
        "encerramento normal ou incompleto e contagens de 14 códigos técnicos autorizados. " +
        "Não envia inventário, nomes de programas, logs brutos, arquivos, caminhos, credenciais ou ID do computador.\n\n" +
        "Cloudflare recebe a conexão, processa IP e metadados para entrega e proteção e agrega as contagens no D1. " +
        "O IP não é salvo pelo GF no D1. GitHub recebe sínteses diárias em repositório privado, acessível somente " +
        "ao responsável e administradores autorizados. Esses provedores podem tratar dados fora do Brasil.\n\n" +
        "Há no máximo um resumo pendente neste PC, válido por até 7 dias e substituível por um resumo novo. " +
        "Sem conexão, em falha ou durante desligamento, ele pode aguardar a próxima sessão autorizada. " +
        "Agregados D1 e sínteses GitHub vencem no sétimo dia contado do dia UTC de recebimento; " +
        "a manutenção seguinte tenta a exclusão definitiva. Falhas podem atrasá-la; obrigações de exclusão " +
        "permanecem até confirmação ou revisão. Backups, segurança, notificações e cópias dos provedores " +
        "têm retenções próprias, descritas no aviso completo.\n\n" +
        "Contagens pequenas são omitidas ou arredondadas no GitHub. Não medem participantes e não garantem " +
        "anonimato em grupos pequenos. Recusar mantém todas as funções. Revogue em Bandeja > Privacidade e diagnóstico. " +
        "Revogar interrompe novas coletas/envios, cancela a tentativa e tenta apagar a pendência. " +
        "Não desfaz uma requisição já recebida. Sem identificadores, não é possível localizar uma contribuição " +
        "individual dentro dos agregados; eles seguem a retenção informada.\n\n" +
        "Responsável proposto: Walisson Minel. Contato privado para informações, acesso, correção e demais direitos: " +
        "PENDENTE DE APROVAÇÃO. Aviso completo: AVISO-PRIVACIDADE-BETA.md, junto ao programa. " +
        "A participação está bloqueada até aprovação do responsável, contato, transferências, retenções e endpoint. ";
    internal static string NoticeHash { get; } = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(RemoteNotice))).ToLowerInvariant();
}
