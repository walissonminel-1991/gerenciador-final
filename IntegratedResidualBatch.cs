namespace GerenciadorAplicativos;

internal sealed record ResidualElevationRequest(IntegratedResidualPlan Plan, IReadOnlyList<string> TargetIds);
internal sealed record ResidualElevationResponse(string State, int Error, IReadOnlyList<IntegratedCleanupResult> Results);
internal sealed record ResidualBatchResult(IReadOnlyList<IntegratedCleanupResult> Results,
    int Offered, bool Declined, bool Cancelled, int Unavailable);

// Uma decisao de limpeza por selecao, independentemente da quantidade de apps.
// Toda I/O permanece no chamador em segundo plano; a unica dependencia visual e
// a funcao de consentimento. Mesma orquestracao na UI, nos testes e no worker.
internal static class IntegratedResidualBatch
{
    internal static async Task<ResidualBatchResult> RunAsync(IReadOnlyList<UninstallOutcome> outcomes,
        IReadOnlyDictionary<string, ResidualUninstallContext> contexts,
        Func<Task<UninstallSnapshot>> inventory,
        Func<IReadOnlyList<IntegratedResidualPlan>, CancellationToken, Task<bool>> confirm,
        Func<IReadOnlyList<ResidualElevationRequest>, CancellationToken, Task<ResidualElevationResponse?>> elevate,
        Action<string> audit, CancellationToken token = default)
    {
        var plans = new List<IntegratedResidualPlan>();
        var results = new List<IntegratedCleanupResult>();
        int unavailable = 0;
        try
        {
            token.ThrowIfCancellationRequested();
            var current = await inventory().ConfigureAwait(false);
            foreach (var outcome in outcomes)
            {
                var original = outcome.Request.Original;
                if (!contexts.TryGetValue(original.InventoryId, out var context)) continue;
                if (outcome.State != UninstallState.RegistrationAbsent)
                {
                    audit(IntegratedResidualCleanup.Audit(context.App, "capture", "skipped",
                        error: outcome.Code ?? 0, reason: "uninstall_" + outcome.State));
                    continue;
                }
                token.ThrowIfCancellationRequested();
                var plan = IntegratedResidualCleanup.Capture(context, current, token);
                audit(IntegratedResidualCleanup.Audit(context.App, "capture", plan.Complete ? "complete" : "blocked",
                    plan.Files.Count, plan.Directories.Count, plan.Keys.Count, plan.Preserved));
                foreach (var problem in plan.Problems)
                    audit(IntegratedResidualCleanup.Audit(context.App, problem.Stage, "preserved", error: problem.Error, reason: problem.Reason));
                if (!plan.Complete) { unavailable++; continue; }
                if (plan.Eligible > 0) plans.Add(plan);
                else audit(IntegratedResidualCleanup.Audit(context.App, "decision", "not_needed", reason: "no_eligible_residue"));
            }
            if (plans.Count == 0) return new(results, 0, false, false, unavailable);
            // Congelar a selecao antes de atravessar a fronteira da interface.
            var accepted = plans.ToArray();
            bool consent = await confirm(Array.AsReadOnly(accepted), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            foreach (var plan in accepted)
            {
                audit(IntegratedResidualCleanup.Audit(plan.Context.App, "decision", consent ? "yes" : "no"));
                results.Add(await IntegratedResidualCleanup.DeleteAsync(plan, consent, inventory, token, audit: audit).ConfigureAwait(false));
                if (results[^1].Cancelled) break;
            }
            if (!consent) return new(results, plans.Count, true, false, unavailable);
            var requests = accepted.Select(plan => new ResidualElevationRequest(plan,
                results.SingleOrDefault(r => r.AppId == plan.Context.App.TechnicalId)?.ElevationIds ?? []))
                .Where(r => r.TargetIds.Count > 0).ToArray();
            if (requests.Length > 0 && !token.IsCancellationRequested)
            {
                var elevated = await elevate(requests, token).ConfigureAwait(false);
                foreach (var request in requests)
                {
                    int index = results.FindIndex(r => r.AppId == request.Plan.Context.App.TechnicalId);
                    var prior = results[index];
                    var replacement = elevated?.Results.SingleOrDefault(r => r.AppId == prior.AppId);
                    var merged = prior.Items.Select(item => request.TargetIds.Contains(item.Id, StringComparer.Ordinal)
                        ? replacement?.Items.SingleOrDefault(i => i.Id == item.Id) ?? item with
                            { State = ResidualItemState.Preserved, Reason = elevated?.State ?? "uac_unavailable", Error = elevated?.Error ?? 0 }
                        : item).ToArray();
                    results[index] = prior with { Items = merged,
                        Cancelled = prior.Cancelled || replacement?.Cancelled == true,
                        Verified = prior.Problems.Count == 0 && replacement?.Problems.Count == 0 &&
                            merged.All(i => i.State is ResidualItemState.Deleted or ResidualItemState.Absent),
                        Problems = prior.Problems.Concat(replacement?.Problems ?? [new("uac", elevated?.State ?? "unavailable", elevated?.Error ?? 0)]).ToArray() };
                }
            }
            foreach (var result in results)
            {
                var plan = accepted.Single(p => p.Context.App.TechnicalId == result.AppId);
                audit(IntegratedResidualCleanup.Audit(plan.Context.App, "final", result.Verified ? "complete" : "partial",
                    result.Files, result.Directories, result.Keys, result.Preserved, result.Failed, cancelled: result.Cancelled));
            }
            return new(results, plans.Count, false, results.Any(r => r.Cancelled) || token.IsCancellationRequested, unavailable);
        }
        catch (OperationCanceledException)
        {
            foreach (var context in contexts.Values)
                audit(IntegratedResidualCleanup.Audit(context.App, "batch", "cancelled", reason: "cancelled", cancelled: true));
            return new(results, plans.Count, false, true, unavailable);
        }
        catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex))
        {
            foreach (var context in contexts.Values)
                audit(IntegratedResidualCleanup.Audit(context.App, "batch", "failed", failed: 1,
                    error: IntegratedResidualCleanup.Error(ex), reason: "unexpected_operation_failure"));
            return new(results, plans.Count, false, false, unavailable + 1);
        }
    }
}
