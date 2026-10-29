namespace GerenciadorAplicativos;

// Politica da edicao compilada, sem preferencia ou variavel de ambiente que a amplie.
internal static class BetaRuntimePolicy
{
    internal static bool IsLocalBeta
    {
        get
        {
#if GF_BETA_LOCAL
            return true;
#else
            return false;
#endif
        }
    }
    internal static bool AllowsAutomaticOnlineChecks => !IsLocalBeta;
    internal static bool AllowsCollaborativeDiagnostics => !IsLocalBeta;
    internal const string LocalNotice =
        "BETA LOCAL TESTE — NÃO HOMOLOGADA.\n\n" +
        "Coleta e envio de diagnóstico remoto estão desativados nesta edição, " +
        "inclusive durante falhas e encerramento. Uma escolha antiga não os ativa.\n\n" +
        "Histórico e relatórios técnicos locais continuam disponíveis. " +
        "As consultas de versões devem ser iniciadas por você.\n\n" +
        "Você pode desativar uma preferência antiga ou simular com dados inventados, sem enviar.";
}
