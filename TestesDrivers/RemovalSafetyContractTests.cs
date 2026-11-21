namespace GerenciadorAplicativos;

internal static class RemovalSafetyContractTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(!RemovalSafetyContract.CanStart(false, true, true, true, true), "sem consentimento impede executar");
        check(!RemovalSafetyContract.CanStart(true, false, true, true, true), "inventario parcial bloqueia");
        check(!RemovalSafetyContract.CanStart(true, true, false, true, true), "identidade alterada bloqueia");
        check(!RemovalSafetyContract.CanStart(true, true, true, false, true), "compartilhado bloqueia");
        check(!RemovalSafetyContract.CanStart(true, true, true, true, false), "executor nao verificado bloqueia");
        check(RemovalSafetyContract.CanStart(true, true, true, true, true), "todas as provas permitem iniciar");
        check(!RemovalSafetyContract.Confirmed(false, true, true, true), "exit != 0 nao comprova remocao");
        check(!RemovalSafetyContract.Confirmed(true, false, true, true), "leitura parcial nao comprova ausencia");
        check(!RemovalSafetyContract.Confirmed(true, true, false, true), "ausencia transitoria nao comprova");
        check(!RemovalSafetyContract.Confirmed(true, true, true, false), "titularidade nao confirmada bloqueia");
        check(RemovalSafetyContract.Confirmed(true, true, true, true), "prova completa confirma");
        check(!RemovalSafetyContract.MayInspectResidues(false), "pendente nao permite inspecao de limpeza");
        check(!RemovalSafetyContract.MayOfferCleanup(true, false, true, true), "plano incompleto nao oferece limpeza");
        check(!RemovalSafetyContract.MayOfferCleanup(true, true, false, true), "propriedade incerta preserva");
        check(!RemovalSafetyContract.MayDeleteResidues(false, true, true, true), "nao conserva residuos");
        check(!RemovalSafetyContract.MayDeleteResidues(true, true, false, true), "remocao inconclusiva nao limpa");
        check(!RemovalSafetyContract.LaterBoot(100000000000L, 100000000000L - TimeSpan.FromMinutes(10).Ticks),
            "relogio retrocedeu no mesmo boot sem autorizar revisao");
        check(RemovalSafetyContract.LaterBoot(100000000000L, 100000000000L + TimeSpan.FromMinutes(3).Ticks),
            "boot posterior so autoriza revisao consultiva");
        check(RemovalSafetyContract.AuditOutcome(false) == "no_cleanup_authorized", "diario bloqueado sem prova");
    }
}
