namespace GerenciadorAplicativos;

// Prévia em memória, sem persistência/telemetria e sem qualquer executor.
// Usa o guard de arquivos já existente, não um segundo motor de descoberta.
internal sealed record ResidualFileState(string Path, InstallerFileGuard.FileIdentity Identity, string Digest);
internal sealed record ResidualPreviewState(ResidualCandidate Candidate,
    IReadOnlyList<ResidualFileState> Files, bool Complete);
internal sealed record ResidualDecisionContext(bool InventoryComplete, bool LauncherExited,
    bool AuxiliaryProcessesFinished, bool ActiveResources, bool ExclusiveOwnership,
    bool ExplicitPolicy, bool SpecificConsent, bool SnapshotUnchanged,
    bool PersonalOrSharedData = false, bool RelatedIntegrationsChecked = false, bool Reinstalled = false);
internal sealed record ResidualDecision(bool SimulationEligible, string Reason)
{
    internal bool CanExecute => false;
}

internal static class ResidualPreview
{
    private const long MaxFileBytes = 32L * 1024 * 1024;
    private const long MaxTotalBytes = 64L * 1024 * 1024;

    internal static ResidualDecision Decide(ResidualDecisionContext context)
    {
        if (context.Reinstalled) return new(false, "Aplicativo reinstalado ou versão paralela identificada: preservar.");
        if (!context.RelatedIntegrationsChecked) return new(false, "Serviços, tarefas e integrações relacionadas ainda não conferidos integralmente.");
        if (context.PersonalOrSharedData) return new(false, "Dados pessoais ou compartilhados: preservar.");
        if (!context.InventoryComplete) return new(false, "Inventário parcial: estado desconhecido, preservar.");
        if (!context.LauncherExited || !context.AuxiliaryProcessesFinished)
            return new(false, "Término do desinstalador e de seus auxiliares não confirmado.");
        if (context.ActiveResources) return new(false, "Recursos ainda utilizados: preservar.");
        if (!context.ExclusiveOwnership || !context.ExplicitPolicy)
            return new(false, "Vínculo exclusivo e política de remoção não comprovados.");
        if (!context.SpecificConsent) return new(false, "Sem autorização específica dos alvos exibidos.");
        if (!context.SnapshotUnchanged) return new(false, "Estado alterado ou ilegível desde a prévia: preservar.");
        return new(true, "Pré-condições de simulação atendidas. Exclusão real desativada nesta candidata.");
    }

    internal static async Task<ResidualPreviewState> CaptureAsync(ResidualCandidate candidate,
        CancellationToken token = default)
    {
        // Não reter listas mutáveis do chamador como autorização de conteúdo.
        candidate = candidate with { Files = Array.AsReadOnly(candidate.Files.ToArray()) };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        token = deadline.Token;
        var files = new List<ResidualFileState>();
        bool complete = candidate.Files.Count is > 0 and <= 16 && candidate.Preserved == 0 &&
            candidate.Files.Distinct(StringComparer.OrdinalIgnoreCase).Count() == candidate.Files.Count &&
            candidate.Files.All(ResidualReadSafety.IsLocalAbsolute);
        long total = 0;
        if (!complete || !ResidualReadSafety.IsUnlinkedDirectory(candidate.Root))
            return new(candidate, files, false);
        if (!SameTargets(candidate)) return new(candidate, files, false);
        foreach (string path in candidate.Files)
        {
            if (token.IsCancellationRequested) { complete = false; break; }
            try
            {
                if (!string.Equals(Path.GetDirectoryName(path), candidate.Root, StringComparison.OrdinalIgnoreCase))
                { complete = false; continue; }
                using var stream = InstallerFileGuard.OpenRead(path, allowDirectoryWrites: true);
                var identity = InstallerFileGuard.ReadIdentity(stream);
                if (identity.Links != 1 || identity.Size > (ulong)MaxFileBytes ||
                    total + (long)identity.Size > MaxTotalBytes)
                { complete = false; continue; }
                total += (long)identity.Size;
                string digest = await InstallerFileGuard.DigestAsync(stream, token).ConfigureAwait(false);
                if (identity != InstallerFileGuard.ReadIdentity(stream)) { complete = false; continue; }
                files.Add(new(path, identity, digest));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or ArgumentException or NotSupportedException or OperationCanceledException)
            { complete = false; }
        }
        return new(candidate, files.AsReadOnly(), complete && !token.IsCancellationRequested &&
            files.Count == candidate.Files.Count && SameTargets(candidate));
    }

    private static bool SameTargets(ResidualCandidate candidate)
    {
        var current = UninstallResiduals.InspectDirectory(candidate.Product, candidate.Root,
            candidate.Files.Select(Path.GetFileName).OfType<string>().ToArray());
        return current is not null && current.Preserved == 0 &&
            current.Files.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(candidate.Files);
    }

    internal static async Task<bool> IsUnchangedAsync(ResidualPreviewState accepted,
        CancellationToken token = default)
    {
        if (!accepted.Complete || token.IsCancellationRequested || accepted.Candidate.Preserved != 0 ||
            accepted.Files.Count != accepted.Candidate.Files.Count) return false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        token = deadline.Token;
        // Só os alvos originalmente exibidos. Nunca acrescentar arquivos novos.
        var refreshed = UninstallResiduals.InspectDirectory(accepted.Candidate.Product,
            accepted.Candidate.Root, accepted.Candidate.Files.Select(Path.GetFileName).OfType<string>().ToArray());
        if (refreshed is null || refreshed.Preserved != accepted.Candidate.Preserved ||
            !refreshed.Files.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(accepted.Candidate.Files)) return false;
        var current = await CaptureAsync(refreshed, token).ConfigureAwait(false);
        return !token.IsCancellationRequested && current.Complete && accepted.Files.Count == current.Files.Count &&
            accepted.Files.All(a => current.Files.Any(c =>
                string.Equals(a.Path, c.Path, StringComparison.OrdinalIgnoreCase) &&
                a.Identity == c.Identity && a.Digest == c.Digest));
    }

    internal static ResidualAuditReport Describe(string product, IEnumerable<ResidualPreviewState> states,
        bool unchanged = true)
    {
        var decision = Decide(new(true, true, false, false, false, true, false, unchanged));
        var snapshots = states.ToArray();
        bool partial = !unchanged || snapshots.Any(s => !s.Complete);
        var findings = snapshots.SelectMany(state => state.Candidate.Files.Select(path =>
            new ResidualAuditFinding(path, "Prévia de arquivo — exclusão bloqueada", 1,
                !unchanged ? "Arquivo ou conjunto de alvos mudou desde a prévia. Preservar e repetir análise." :
                state.Complete ? "Identidade e conteúdo registrados somente nesta revisão. " + decision.Reason :
                "Não foi possível validar identidade, link único e conteúdo dentro dos limites. Preservar.",
                ResidualDisposition.Unknown))).ToArray();
        return new(product, false, findings, [], Incomplete: partial,
            Coverage: partial ? new ResidualCoverage(ResidualScanIssue.ReadFailure, SourcesUnavailable: ["Preview"],
                CandidatesNotEvaluated: null) : null);
    }
}
