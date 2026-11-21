namespace GerenciadorAplicativos;

// R7 e G3 compartilham SOMENTE provas, transicoes e autorizacao.
// Nenhuma operacao de arquivo/Registro/PnP ocorre nesta classe.
internal static class RemovalSafetyContract
{
    internal static bool CanStart(bool consent, bool inventoryComplete,
        bool stableIdentity, bool uniqueOwner, bool executorVerified) =>
        consent && inventoryComplete && stableIdentity && uniqueOwner && executorVerified;

    // Sucesso do processo sozinho nao comprova remocao. O chamador define
    // qual evidencia independente constitui ausencia estavel (ARP vs PnP+INF).
    internal static bool Confirmed(bool commandSucceeded, bool inventoryComplete,
        bool stableAbsence, bool ownershipRechecked) =>
        commandSucceeded && inventoryComplete && stableAbsence && ownershipRechecked;

    internal static bool MayInspectResidues(bool confirmedRemoval) => confirmedRemoval;

    internal static bool MayOfferCleanup(bool confirmedRemoval, bool planComplete,
        bool uniquelyOwned, bool hasEligibleTargets) =>
        confirmedRemoval && planComplete && uniquelyOwned && hasEligibleTargets;

    internal static bool MayDeleteResidues(bool explicitConsent, bool validFrozenPlan,
        bool confirmedRemoval, bool ownershipRechecked) =>
        explicitConsent && validFrozenPlan && confirmedRemoval && ownershipRechecked;

    internal static bool LaterBoot(long storedUtcTicks, long currentUtcTicks) =>
        storedUtcTicks > 0 && currentUtcTicks > storedUtcTicks &&
        currentUtcTicks - storedUtcTicks > TimeSpan.FromMinutes(2).Ticks;

    // Codigo estavel sem identificadores pessoais no diario. O caller conserva
    // seu armazenamento: R7 usa DiagnosticTrace; G3 usa registro/marker OEM.
    internal static string AuditOutcome(bool verified) =>
        verified ? "removal_confirmed" : "no_cleanup_authorized";
}
