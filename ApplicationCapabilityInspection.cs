namespace GerenciadorAplicativos;

// Run off the UI thread, using the same gates as the real operations. No
// installers are downloaded or launched by this read-only assessment.
internal static class ApplicationCapabilityInspection
{
    internal static string Describe(ProgramEntry app, UpdateOperationSnapshot? operation)
    {
        var capability = app.Capabilities;
        if (capability is null) return "Capacidades ainda não verificadas.";
        if (app.UpdateRequiresReview && capability.Update.State != ApplicationCapabilityState.ReadOnly)
            capability = capability with { Update = new(ApplicationCapabilityState.AvailableNotAuthorized, "source_review") };
        if (DateTimeOffset.UtcNow - capability.ObservedAt > TimeSpan.FromMinutes(2))
        {
            ApplicationCapabilityDecision Expire(ApplicationCapabilityDecision d) => d.State is
                ApplicationCapabilityState.SilentEligible or ApplicationCapabilityState.SilentVerified or ApplicationCapabilityState.Interactive
                ? new(ApplicationCapabilityState.Inconclusive, "assessment_expired") : d;
            return (capability with { Update = Expire(capability.Update), Uninstall = Expire(capability.Uninstall) }).Describe();
        }
        if (capability.Update.State == ApplicationCapabilityState.SilentEligible &&
            operation?.Progress.Stage is UpdateStage.Installing or UpdateStage.Confirming or UpdateStage.Confirmed)
            capability = capability with { Update = new(ApplicationCapabilityState.SilentVerified, "payload_verified") };
        return capability.Describe();
    }
    internal static ApplicationCapabilitySnapshot[] Inspect(ProgramEntry[] apps, bool complete,
        bool updateChecked, CancellationToken token = default)
    {
        var result = new List<ApplicationCapabilitySnapshot>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (var app in apps)
        {
            token.ThrowIfCancellationRequested();
            bool unique = !string.IsNullOrWhiteSpace(app.InventoryId) &&
                apps.Count(a => a.InventoryId.Equals(app.InventoryId, StringComparison.OrdinalIgnoreCase)) == 1;
            var known = ApplicationIdentityCatalog.Resolve(app.Name, app.Publisher);
            bool explicitMsix = !ProtectedApplications.IsProtectedName(app.Name) && app.VerifiedMsixIdentity &&
                WindowsTerminalUpdater.Resolve(app) is not null;
            bool update = explicitMsix, silent = false, budget = clock.Elapsed > TimeSpan.FromSeconds(15);
            string updateReason = "no_verified_contract";
            if (!budget && complete && unique && !app.InventoryOnly)
            {
                try
                {
                    var recipe = VerifiedUpdaters.Resolve(app);
                    update = recipe is { Interactive: false } && VerifiedUpdaters.SingleSafeSilentInstallation(recipe, app, apps);
                    if (!update && app.IsUpdate && !app.UpdateRequiresReview)
                        updateReason = AutomaticAppInstallation.Read(app, out string detail) is null ? detail : "manifest_not_authorized";
                }
                catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex)) { }
                try
                {
                    if (app.CanUninstall)
                    {
                        var request = UninstallBatch.Select([app]).Single();
                        silent = SilentUninstallPolicy.UsesSilentMode(request);
                    }
                }
                catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex) || ex is System.Security.SecurityException)
                { /* Gates remain closed; execution repeats all checks. */ }
            }
            bool policy = IntegratedResidualPolicy.Build(ResidualAppIdentity.From(app)).Policy is not ("unsupported" or "unsupported_system_scope");
            var facts = new ApplicationCapabilityFacts(unique ? app.InventoryId : "", complete,
                app.InventoryOnly || ProtectedApplications.IsProtectedName(app.Name), known is not null,
                updateChecked, app.IsUpdate, app.UpdateRequiresReview, update,
                UninstallRegistered: app.CanUninstall, SilentUninstallContract: silent, ResidualPolicy: policy, UpdateReason: updateReason,
                InspectionIncomplete: budget, ExplicitUpdateOnly: explicitMsix);
            result.Add(ApplicationCapabilities.Evaluate(facts));
        }
        return result.ToArray();
    }
}
internal sealed partial class MainForm
{
    private async Task RefreshCapabilitiesAsync(bool updateChecked, CancellationToken token = default)
    {
        var snapshot = _installed.ToArray();
        int generation = _catalogoGeneration;
        bool complete = !_inventoryIncomplete;
        var caps = await Task.Run(() => ApplicationCapabilityInspection.Inspect(snapshot, complete, updateChecked, token), token);
        token.ThrowIfCancellationRequested();
        if (IsDisposed || generation != _catalogoGeneration || !snapshot.SequenceEqual(_installed)) return;
        for (int i = 0; i < snapshot.Length; i++) snapshot[i].Capabilities = caps[i];
        var matrix = ApplicationCapabilityMatrix.From(caps);
        DiagnosticTrace.Write(matrix.Audit());
        foreach (var reason in matrix.Reasons.OrderBy(r => r.Key, StringComparer.Ordinal))
            DiagnosticTrace.Write($"CAPACIDADES.MOTIVO motivo={reason.Key} quantidade={reason.Value} operacoes={matrix.OperationDenominator}");
        Log($"Capacidades: {matrix.SilentUpdateEligible}/{matrix.Applications} elegíveis a atualização silenciosa; " +
            $"{matrix.SilentUninstallEligible}/{matrix.Applications} com modo de desinstalação silencioso identificado; " +
            $"{matrix.ResidualPolicies}/{matrix.Applications} com política residual. Validação e consentimento continuam obrigatórios.");
    }
}
