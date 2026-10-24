namespace GerenciadorAplicativos;

internal sealed record ResidualElevationRequest(IntegratedResidualPlan Plan, IReadOnlyList<string> TargetIds);
internal sealed record ResidualElevationResponse(string State, int Error, IReadOnlyList<IntegratedCleanupResult> Results);
internal sealed record ResidualBatchResult(IReadOnlyList<IntegratedCleanupResult> Results,
    int Offered, bool Declined, bool Cancelled, int Unavailable,
    IReadOnlyList<IntegratedResidualPlan>? RebootPlans = null,
    IReadOnlyList<IntegratedResidualPlan>? Inspections = null);

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
        Action<string> audit, CancellationToken token = default,
        Action<ResidualAppIdentity, string>? progress = null)
    {
        var plans = new List<IntegratedResidualPlan>();
        var inspected = new List<IntegratedResidualPlan>();
        var results = new List<IntegratedCleanupResult>();
        int unavailable = 0;
        try
        {
            token.ThrowIfCancellationRequested();
            var current = await inventory().ConfigureAwait(false);
            foreach (var skipped in contexts.Values.Where(context => !outcomes.Any(o =>
                o.Request.Original.InventoryId.Equals(context.App.InventoryId, StringComparison.OrdinalIgnoreCase))))
                audit(IntegratedResidualCleanup.Audit(skipped.App, "uninstall", "skipped", reason: "not_attempted_after_pending_or_cancel"));
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
                progress?.Invoke(context.App, "inspecting");
                var plan = IntegratedResidualCleanup.Capture(context, current, token);
                inspected.Add(plan);
                audit(IntegratedResidualCleanup.Audit(context.App, "capture", plan.Conclusive ? "complete" : "inconclusive",
                    plan.Files.Count, plan.Directories.Count, plan.Keys.Count, plan.Preserved));
                foreach (var problem in plan.Problems)
                    audit(IntegratedResidualCleanup.Audit(context.App, problem.Stage, "preserved", error: problem.Error, reason: problem.Reason));
                if (!plan.Conclusive) unavailable++;
                if (!plan.Complete) continue;
                if (plan.Eligible > 0) plans.Add(plan);
                else audit(IntegratedResidualCleanup.Audit(context.App, "decision", plan.Assessment.ToString(),
                    kept: plan.Preserved, reason: plan.Assessment switch
                    {
                        ResidualAssessment.None => "no_residue_in_verified_scope",
                        ResidualAssessment.Preserved => "preserved_for_safety",
                        _ => "verification_inconclusive"
                    }));
            }
            if (plans.Count == 0) return new(results, 0, false, false, unavailable,
                inspected.Where(p => p.Problems.Any(e => e.Error is 32 or 33)).ToArray(), inspected.ToArray());
            // Congelar a selecao antes de atravessar a fronteira da interface.
            var accepted = plans.ToArray();
            token.ThrowIfCancellationRequested();
            foreach (var plan in accepted) progress?.Invoke(plan.Context.App, "awaiting_cleanup");
            bool consent = await confirm(Array.AsReadOnly(accepted), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            foreach (var plan in accepted)
            {
                audit(IntegratedResidualCleanup.Audit(plan.Context.App, "decision", consent ? "yes" : "no"));
                progress?.Invoke(plan.Context.App, consent ? "cleaning" : "preserved");
                results.Add(await IntegratedResidualCleanup.DeleteAsync(plan, consent, inventory, token, audit: audit).ConfigureAwait(false));
                if (results[^1].Cancelled) break;
            }
            if (results.Any(r => r.Cancelled) || token.IsCancellationRequested)
            {
                foreach (var remaining in accepted.Where(p => !results.Any(r => r.AppId == p.Context.App.TechnicalId)))
                {
                    var pending = remaining.Files.Select(f => new ResidualItemResult(f.Id, "file", ResidualItemState.Cancelled, "cancelled"))
                        .Concat(remaining.Keys.Select(k => new ResidualItemResult(k.Id, "registry", ResidualItemState.Cancelled, "cancelled")))
                        .Concat(remaining.Directories.Select(d => new ResidualItemResult(d.Id, "directory", ResidualItemState.Cancelled, "cancelled"))).ToArray();
                    results.Add(new(remaining.Context.App.TechnicalId, pending, remaining.Preserved, true, false, [new("batch", "cancelled")]));
                    audit(IntegratedResidualCleanup.Audit(remaining.Context.App, "batch", "cancelled", kept: pending.Length, cancelled: true));
                }
                return new(results, plans.Count, false, true, unavailable, Inspections: inspected.ToArray());
            }
            if (!consent) return new(results, plans.Count, true, false, unavailable, Inspections: inspected.ToArray());
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
            // Apenas falha de compartilhamento (32/33) indica possivel
            // beneficio de reinicializacao. Acesso negado 5 nao indica isso.
            var rebootPlans = inspected.Where(plan => plan.Problems.Any(p => p.Error is 32 or 33) || results.Any(r =>
                r.AppId == plan.Context.App.TechnicalId && !r.Verified &&
                r.Items.Any(RebootResidualReview.IsRetryable))).ToArray();
            return new(results, plans.Count, false, results.Any(r => r.Cancelled) || token.IsCancellationRequested,
                unavailable, rebootPlans, inspected.ToArray());
        }
        catch (OperationCanceledException)
        {
            foreach (var context in contexts.Values)
                audit(IntegratedResidualCleanup.Audit(context.App, "batch", "cancelled", reason: "cancelled", cancelled: true));
            return new(results, plans.Count, false, true, unavailable, Inspections: inspected.ToArray());
        }
        catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex))
        {
            foreach (var context in contexts.Values)
                audit(IntegratedResidualCleanup.Audit(context.App, "batch", "failed", failed: 1,
                    error: IntegratedResidualCleanup.Error(ex), reason: "unexpected_operation_failure"));
            return new(results, plans.Count, false, false, unavailable + 1, Inspections: inspected.ToArray());
        }
    }
}
