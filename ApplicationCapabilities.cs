namespace GerenciadorAplicativos;

internal enum ApplicationCapabilityState
{
    ReadOnly, NoCandidate, AvailableNotAuthorized, SilentEligible, SilentVerified,
    Interactive, PolicyAvailable, SafeAuthorized, Blocked, Inconclusive
}
internal sealed record ApplicationCapabilityDecision(ApplicationCapabilityState State, string Reason);

// A capability is a description of evidence, never a launch/delete permit.
// These facts are assembled from the existing engines and expire with the scan.
internal sealed record ApplicationCapabilityFacts(string Id, bool InventoryComplete = true,
    bool InventoryOnly = false, bool Identified = false, bool UpdateChecked = false,
    bool UpdateAvailable = false, bool UpdateReview = false, bool UpdateContract = false,
    bool UpdatePayloadVerified = false, bool UninstallRegistered = false,
    bool SilentUninstallContract = false, bool UninstallPayloadVerified = false,
    bool ResidualPolicy = false, bool ResidualAuthorized = false, bool ResidualUncertain = false,
    string UpdateReason = "no_verified_contract", string UninstallReason = "registered_command_requires_preflight", bool InspectionIncomplete = false, bool ExplicitUpdateOnly = false);
internal sealed record ApplicationCapabilitySnapshot(string Id, bool Identified,
    ApplicationCapabilityDecision Update, ApplicationCapabilityDecision Uninstall,
    ApplicationCapabilityDecision Residual)
{
    internal DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;
    internal IEnumerable<ApplicationCapabilityDecision> Operations => [Update, Uninstall, Residual];
    internal string Describe() => "Atualização: " + Label(Update) + "\nDesinstalação: " + Label(Uninstall) +
        "\nResíduos: " + Label(Residual);
    private static string Label(ApplicationCapabilityDecision d) => (d.State switch
    {
        ApplicationCapabilityState.ReadOnly => "somente leitura",
        ApplicationCapabilityState.NoCandidate => "sem atualização superior comprovada",
        ApplicationCapabilityState.AvailableNotAuthorized => "disponível, sem autorização de execução",
        ApplicationCapabilityState.SilentEligible => "silenciosa elegível; exige validação antes de executar",
        ApplicationCapabilityState.SilentVerified => "silenciosa verificada nesta tentativa",
        ApplicationCapabilityState.Interactive => "interativa; exige confirmação e verificação do desinstalador",
        ApplicationCapabilityState.PolicyAvailable => "política disponível; ainda sem autorização para excluir",
        ApplicationCapabilityState.SafeAuthorized => "seleção segura comprovada; exige sua confirmação e revalidação",
        ApplicationCapabilityState.Blocked => "bloqueada",
        _ => "inconclusiva"
    }) + " — " + Reason(d.Reason);
    private static string Reason(string reason) => reason switch
    {
        "inventory_only" => "entrada protegida ou somente inventário",
        "inventory_partial" => "inventário incompleto",
        "identity_missing_or_ambiguous" => "identidade ausente ou ambígua",
        "assessment_expired" => "consulta de capacidade expirou; atualizar e verificar novamente",
        "inspection_budget" => "verificação de capacidades atingiu o limite desta consulta",
        "policy_or_registered_command" => "tipo de aplicativo ou comando registrado sem comprovação",
        "msi_product_mismatch" => "identificadores MSI divergentes", "scope_unknown" => "escopo da instalação desconhecido",
        "dedicated_root_unproven" => "pasta exclusiva da instalação não comprovada",
        "binary_identity_or_version" => "identidade ou versão do executável não corresponde ao registro",
        "architecture_mismatch" => "arquitetura divergente ou não reconhecida", "engine_unrecognized" => "motor do instalador não reconhecido",
        "installation_access_denied" => "acesso negado durante a leitura da instalação", "installation_unreadable" => "instalação não pôde ser lida",
        "manifest_not_authorized" => "nenhum manifesto autenticado autoriza esta instalação e versão",
        "not_checked" => "consulta ainda não realizada", "no_candidate" => "nenhuma versão superior confirmada",
        "source_review" => "fontes ou contexto exigem revisão", "no_verified_contract" => "sem contrato técnico comprovado",
        "payload_validation_required" => "hash, assinatura e instalação serão conferidos antes do lançamento",
        "payload_verified" => "integridade, identidade e contexto conferidos",
        "registered_command_requires_preflight" => "comando registrado ainda precisa de pré-verificação",
        "no_registered_uninstaller" => "nenhum desinstalador verificável registrado",
        "no_residual_policy" => "sem política residual comprovada", "residual_preflight_required" => "remoção e propriedade precisam ser comprovadas",
        "residual_scope_inconclusive" => "parte do escopo não pôde ser conferida", "residual_selection_verified" => "somente os itens apresentados",
        _ => "evidência técnica insuficiente"
    };
}
internal static class ApplicationCapabilities
{
    internal static ApplicationCapabilitySnapshot Evaluate(ApplicationCapabilityFacts facts)
    {
        ApplicationCapabilityDecision D(ApplicationCapabilityState state, string reason) => new(state, reason);
        if (!facts.InventoryComplete || string.IsNullOrWhiteSpace(facts.Id))
        {
            var uncertain = D(ApplicationCapabilityState.Inconclusive, facts.InventoryComplete ? "identity_missing_or_ambiguous" : "inventory_partial");
            return new(facts.Id, facts.Identified, uncertain, uncertain, uncertain);
        }
        if (facts.InventoryOnly && !facts.ExplicitUpdateOnly)
        {
            var read = D(ApplicationCapabilityState.ReadOnly, "inventory_only");
            return new(facts.Id, facts.Identified, read, read, read);
        }
        if (facts.InspectionIncomplete)
        {
            var uncertain = D(ApplicationCapabilityState.Inconclusive, "inspection_budget");
            return new(facts.Id, facts.Identified, uncertain, uncertain, uncertain);
        }
        var update = !facts.UpdateChecked ? D(ApplicationCapabilityState.Inconclusive, "not_checked") :
            !facts.UpdateAvailable ? D(ApplicationCapabilityState.NoCandidate, "no_candidate") :
            facts.UpdateReview ? D(ApplicationCapabilityState.AvailableNotAuthorized, "source_review") :
            !facts.UpdateContract ? D(ApplicationCapabilityState.AvailableNotAuthorized, facts.UpdateReason) :
            facts.UpdatePayloadVerified ? D(ApplicationCapabilityState.SilentVerified, "payload_verified") :
            D(ApplicationCapabilityState.SilentEligible, "payload_validation_required");
        var uninstall = !facts.UninstallRegistered ? D(ApplicationCapabilityState.Blocked, "no_registered_uninstaller") :
            !facts.SilentUninstallContract ? D(ApplicationCapabilityState.Interactive, facts.UninstallReason) :
            facts.UninstallPayloadVerified ? D(ApplicationCapabilityState.SilentVerified, "payload_verified") :
            D(ApplicationCapabilityState.SilentEligible, "payload_validation_required");
        var residual = facts.ResidualUncertain ? D(ApplicationCapabilityState.Inconclusive, "residual_scope_inconclusive") :
            !facts.ResidualPolicy ? D(ApplicationCapabilityState.ReadOnly, "no_residual_policy") :
            facts.ResidualAuthorized ? D(ApplicationCapabilityState.SafeAuthorized, "residual_selection_verified") :
            D(ApplicationCapabilityState.PolicyAvailable, "residual_preflight_required");
        if (facts.InventoryOnly)
        {
            // Specific reviewed MSIX updater, e.g. Terminal: no generic Store
            // uninstall or residual authority is inferred from that contract.
            uninstall = D(ApplicationCapabilityState.ReadOnly, "inventory_only");
            residual = D(ApplicationCapabilityState.ReadOnly, "inventory_only");
        }
        return new(facts.Id, facts.Identified, update, uninstall, residual);
    }
}
internal sealed record ApplicationCapabilityMatrix(int Applications, int Identified, int SilentUpdateEligible,
    int SilentUninstallEligible, int ResidualPolicies, int ResidualSelections, int ConsultativeOperations,
    int InconclusiveOperations, IReadOnlyDictionary<string, int> Reasons)
{
    internal int OperationDenominator => Applications * 3;
    internal static ApplicationCapabilityMatrix From(IEnumerable<ApplicationCapabilitySnapshot> snapshots)
    {
        var apps = snapshots.ToArray();
        bool Eligible(ApplicationCapabilityDecision d) => d.State is ApplicationCapabilityState.SilentEligible or ApplicationCapabilityState.SilentVerified;
        var ops = apps.SelectMany(a => a.Operations).ToArray();
        return new(apps.Length, apps.Count(a => a.Identified), apps.Count(a => Eligible(a.Update)),
            apps.Count(a => Eligible(a.Uninstall)), apps.Count(a => a.Residual.State is ApplicationCapabilityState.PolicyAvailable or ApplicationCapabilityState.SafeAuthorized),
            apps.Count(a => a.Residual.State == ApplicationCapabilityState.SafeAuthorized),
            ops.Count(d => d.State is ApplicationCapabilityState.ReadOnly or ApplicationCapabilityState.AvailableNotAuthorized or ApplicationCapabilityState.PolicyAvailable),
            ops.Count(d => d.State == ApplicationCapabilityState.Inconclusive),
            ops.GroupBy(d => d.Reason).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal));
    }
    // Only counts and fixed reason codes may enter the diagnostic.
    internal string Audit() => $"CAPACIDADES.RESUMO aplicativos={Applications} identidades_catalogo={Identified} atualizacao_silenciosa_elegivel={SilentUpdateEligible} " +
        $"desinstalacao_silenciosa_elegivel={SilentUninstallEligible} politicas_residuais={ResidualPolicies} selecoes_residuais={ResidualSelections} " +
        $"consultivas={ConsultativeOperations} inconclusivas={InconclusiveOperations} operacoes={OperationDenominator}";
}
