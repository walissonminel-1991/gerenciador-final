namespace GerenciadorAplicativos;

// Preferencia persiste entre builds. Uma versao nova nao deve forcar consentimento
// repetido, mas o estado deve ficar visivel e revisavel em todas as aberturas.
internal static class PrivacyConsentPresentation
{
    internal static string ButtonLabel(TelemetryConsent state) => state switch
    {
        TelemetryConsent.RemoteBeta => "Envio BETA autorizado",
        TelemetryConsent.LocalOnly => "Coleta local ativa",
        TelemetryConsent.Declined => "Coleta desativada",
        _ => "Configurar privacidade"
    };

    internal static string Explanation(TelemetryConsent state) => state switch
    {
        TelemetryConsent.RemoteBeta =>
            "Você autorizou o resumo e envio remoto da BETA sob o aviso atual. " +
            "Revogue em Bandeja > Privacidade e diagnóstico.",
        TelemetryConsent.LocalOnly =>
            "Você autorizou somente contagens técnicas locais em uma versão anterior. " +
            "Nenhum diagnóstico é enviado pela internet. Clique para revisar ou desativar.",
        TelemetryConsent.Declined =>
            "Você recusou ou desativou a coleta. Nenhum diagnóstico colaborativo será registrado. " +
            "Clique para revisar sua escolha.",
        _ =>
            "Você ainda não escolheu participar. A coleta colaborativa está desativada. " +
            "Clique para conhecer as opções."
    };
}
