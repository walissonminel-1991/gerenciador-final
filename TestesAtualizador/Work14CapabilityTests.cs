using GerenciadorAplicativos;

internal static class Work14CapabilityTests
{
    internal static void Run(Action<bool, string> check)
    {
        var facts = new ApplicationCapabilityFacts("fixture", UpdateChecked: true, UpdateAvailable: true,
            UninstallRegistered: true, ResidualPolicy: true, Identified: true);
        var consult = ApplicationCapabilities.Evaluate(facts);
        check(consult.Update.State == ApplicationCapabilityState.AvailableNotAuthorized &&
            consult.Uninstall.State == ApplicationCapabilityState.Interactive && consult.Residual.State == ApplicationCapabilityState.PolicyAvailable,
            "W14 nome ou politica identificada nao concede execucao ou exclusao");
        var eligible = ApplicationCapabilities.Evaluate(facts with { UpdateContract = true, SilentUninstallContract = true });
        check(eligible.Update.State == ApplicationCapabilityState.SilentEligible && eligible.Uninstall.State == ApplicationCapabilityState.SilentEligible,
            "W14 contrato elegivel mantem validacao do payload pendente");
        var verified = ApplicationCapabilities.Evaluate(facts with { UpdateContract = true, UpdatePayloadVerified = true,
            SilentUninstallContract = true, UninstallPayloadVerified = true, ResidualAuthorized = true });
        check(verified.Update.State == ApplicationCapabilityState.SilentVerified && verified.Uninstall.State == ApplicationCapabilityState.SilentVerified &&
            verified.Residual.State == ApplicationCapabilityState.SafeAuthorized, "W14 provas das tres operacoes sao independentes");
        var forged = ApplicationCapabilities.Evaluate(facts with { UpdatePayloadVerified = true, UninstallPayloadVerified = true,
            ResidualPolicy = false, ResidualAuthorized = true });
        check(forged.Update.State == ApplicationCapabilityState.AvailableNotAuthorized && forged.Uninstall.State == ApplicationCapabilityState.Interactive &&
            forged.Residual.State == ApplicationCapabilityState.ReadOnly, "W14 prova final isolada nao substitui contrato ou politica");
        foreach (var invalid in new[] { facts with { InventoryComplete = false }, facts with { Id = "" } })
            check(ApplicationCapabilities.Evaluate(invalid).Operations.All(d => d.State == ApplicationCapabilityState.Inconclusive),
                "W14 inventario parcial ou identidade ausente nunca mostra capacidade executavel");
        check(ApplicationCapabilities.Evaluate(facts with { InventoryOnly = true, UpdateContract = true,
            SilentUninstallContract = true, ResidualAuthorized = true }).Operations.All(d => d.State == ApplicationCapabilityState.ReadOnly),
            "W14 MSIX jogos e componentes protegidos conservam somente leitura");
        check(ApplicationCapabilities.Evaluate(facts with { UpdateReview = true, UpdateContract = true, UpdatePayloadVerified = true })
            .Update.State == ApplicationCapabilityState.AvailableNotAuthorized, "W14 conflito de fonte prevalece sobre contrato de instalacao");
        check(ApplicationCapabilities.Evaluate(facts with { ResidualUncertain = true, ResidualAuthorized = true })
            .Residual.State == ApplicationCapabilityState.Inconclusive, "W14 escopo inacessivel nao vira limpeza conclusiva");
        var specific = ApplicationCapabilities.Evaluate(facts with { InventoryOnly = true, ExplicitUpdateOnly = true, UpdateContract = true });
        check(specific.Update.State == ApplicationCapabilityState.SilentEligible &&
            specific.Uninstall.State == ApplicationCapabilityState.ReadOnly && specific.Residual.State == ApplicationCapabilityState.ReadOnly,
            "W14 atualizador MSIX especifico nao autoriza desinstalacao ou limpeza generica da Store");
        foreach (string reason in new[] { "version_not_comparable", "catalog_identity_ambiguous", "major_version_review" })
        {
            var uncertain = ApplicationCapabilities.Evaluate(facts with { UpdateAvailable = false, UpdateUncertainty = reason });
            check(uncertain.Update.State == ApplicationCapabilityState.Inconclusive && uncertain.Update.Reason == reason &&
                uncertain.Uninstall.State == ApplicationCapabilityState.Interactive,
                "W14 incerteza de atualizacao nao vira ausencia nem contamina desinstalacao: " + reason);
        }
        var unmatched = ApplicationCapabilities.Evaluate(facts with { UpdateAvailable = false, UpdateReason = "catalog_unmatched" });
        check(unmatched.Update.Reason == "catalog_unmatched", "W14 sem correspondencia no catalogo nao equivale a versao atualizada");
        var matrix = ApplicationCapabilityMatrix.From([consult, eligible, verified, forged]);
        check(matrix.Applications == 4 && matrix.OperationDenominator == 12 && matrix.SilentUpdateEligible == 2 &&
            matrix.SilentUninstallEligible == 2 && matrix.ResidualPolicies == 3 && matrix.ResidualSelections == 1 &&
            matrix.Reasons.Values.Sum() == 12, "W14 matriz conserva denominadores por aplicativo e por operacao");
        check(!matrix.Audit().Contains("fixture") && !matrix.Audit().Contains("PRIVATE"), "W14 metrica publica nao inclui identidade local");
    }
}
