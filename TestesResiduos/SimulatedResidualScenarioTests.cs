using GerenciadorAplicativos;

// Fluxo 3: politica puramente em memoria. Nenhum arquivo real e aberto,
// nenhuma chave e modificada, nenhuma remocao e autorizada.
internal static class SimulatedResidualScenarioTests
{
    internal static void Run(Action<bool, string> check)
    {
        var safe = new ResidualDecisionContext(
            InventoryComplete: true, LauncherExited: true,
            AuxiliaryProcessesFinished: true, ActiveResources: false,
            ExclusiveOwnership: true, ExplicitPolicy: true,
            SpecificConsent: true, SnapshotUnchanged: true,
            PersonalOrSharedData: false, RelatedIntegrationsChecked: true,
            Reinstalled: false);
        var preview = ResidualPreview.Decide(safe);
        check(preview.SimulationEligible && !preview.CanExecute,
            "SIMULACAO RESIDUOS: todas as provas deixam apenas simular, nunca excluir");
        var blocked = new (ResidualDecisionContext State, string Reason)[]
        {
            (safe with { InventoryComplete = false }, "inventario parcial"),
            (safe with { LauncherExited = false }, "desinstalador ativo"),
            (safe with { AuxiliaryProcessesFinished = false }, "auxiliares ativos"),
            (safe with { ActiveResources = true }, "recursos em uso"),
            (safe with { ExclusiveOwnership = false }, "propriedade compartilhada ou incerta"),
            (safe with { ExplicitPolicy = false }, "politica especifica ausente"),
            (safe with { SpecificConsent = false }, "sem consentimento"),
            (safe with { SnapshotUnchanged = false }, "alvos alterados"),
            (safe with { PersonalOrSharedData = true }, "dados pessoais/salvos"),
            (safe with { RelatedIntegrationsChecked = false }, "servicos e integracoes desconhecidos"),
            (safe with { Reinstalled = true }, "reinstalacao detectada")
        };
        foreach (var item in blocked)
        {
            var result = ResidualPreview.Decide(item.State);
            check(!result.SimulationEligible && !result.CanExecute &&
                !string.IsNullOrWhiteSpace(result.Reason),
                "SIMULACAO RESIDUOS: bloquear " + item.Reason);
        }
        var app = new ProgramEntry { Name = "App Sintetico" };
        var file = new ResidualCandidate(app.Name, @"C:\GF-Fake-Readonly",
            [@"C:\GF-Fake-Readonly\arquivo.dat"], Preserved: 1);
        var denied = UninstallResiduals.Clean(app, file);
        var deniedDirect = UninstallResiduals.CleanCandidateFiles(file, ["arquivo.dat"]);
        var deniedTree = AdvancedUninstallCleaner.DeleteApprovedTree(file.Root, [file.Root]);
        check(denied.RemovedFiles == 0 && deniedDirect.RemovedFiles == 0 &&
            deniedTree.Files == 0 && deniedTree.Directories == 0,
            "SIMULACAO RESIDUOS: ate chamada direta com allowlist nao remove nada");
    }
}
