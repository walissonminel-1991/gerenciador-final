namespace GerenciadorAplicativos;

// Uma remoção de INF não prova a titularidade de arquivos, serviços ou Registro.
// Nao usar o limpador de aplicativos para DriverStore, System32 ou chaves PnP.
internal static class DriverResidualReviewPolicy
{
    internal static bool MayInspect(DriverSilentUninstallState result) =>
        RemovalSafetyContract.MayInspectResidues(result == DriverSilentUninstallState.Verified);

    internal static bool MayDeleteFromWindows(bool hasIndependentExclusiveOwnershipProof) =>
        false; // Sem um mapa tipado e verificavel de titularidade, preservar sempre.
}
