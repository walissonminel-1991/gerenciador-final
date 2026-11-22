namespace GerenciadorAplicativos;

// Um lote não amplia permissões: reúne somente ações individuais explícitas
// já reconhecidas como "update" e com receita não interativa aprovada.
// Retém a identidade original para revalidar após cada recarga do inventário.
internal sealed record AppBatchUpdateCandidate(string InventoryId, string InstallationKey,
    string InstalledVersion, string RecipeId, string TargetVersion, string Name);

internal static class AppBatchUpdatePolicy
{
    private static (string Id, string Target)? ProductionRecipe(ProgramEntry app)
    {
        if (VerifiedUpdaters.GetPrimaryAction(app) != "update") return null;
        var recipe = VerifiedUpdaters.Resolve(app);
        return recipe is { Interactive: false } ? (recipe.Identifier, recipe.Version) : null;
    }

    internal static AppBatchUpdateCandidate[] Select(
        IReadOnlyList<ProgramEntry> items, bool inventoryComplete, bool catalogChecked,
        Func<ProgramEntry, (string Id, string Target)?>? recipeFor = null)
    {
        if (!inventoryComplete || !catalogChecked) return [];
        recipeFor ??= ProductionRecipe;
        var unique = items.Where(x => !string.IsNullOrWhiteSpace(x.InventoryId))
            .GroupBy(x => x.InventoryId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .Select(group => group.Single());
        var candidates = new List<AppBatchUpdateCandidate>();
        foreach (var item in unique)
        {
            if (!item.IsUpdate || item.InventoryOnly || item.UpdateRequiresReview ||
                ProtectedApplications.IsProtectedName(item.Name)) continue;
            string? key = UpdateOperationStore.InstallationKey(item);
            if (key is null) continue;
            var recipe = recipeFor(item);
            if (recipe is not { } choice || string.IsNullOrWhiteSpace(choice.Id) ||
                string.IsNullOrWhiteSpace(choice.Target) ||
                !string.Equals(item.AvailableVersion, choice.Target, StringComparison.OrdinalIgnoreCase))
                continue;
            candidates.Add(new(item.InventoryId, key, item.Version, choice.Id,
                choice.Target, item.Name));
        }
        return candidates.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal static ProgramEntry? Revalidate(
        AppBatchUpdateCandidate candidate, IReadOnlyList<ProgramEntry> current,
        bool inventoryComplete, bool catalogChecked,
        Func<ProgramEntry, (string Id, string Target)?>? recipeFor = null)
    {
        if (!inventoryComplete || !catalogChecked) return null;
        var items = current.Where(x => string.Equals(x.InventoryId, candidate.InventoryId,
            StringComparison.OrdinalIgnoreCase)).ToArray();
        if (items.Length != 1) return null;
        var rechecked = Select(items, true, true, recipeFor);
        return rechecked.Length == 1 && rechecked[0] == candidate ? items[0] : null;
    }
}

// Cada saída é classificada uma única vez, inclusive quando a identidade é
// revogada antes da primeira tentativa; a fila NUNCA retenta um alvo incerto.
internal enum AppBatchUpdateStepResult { Confirmed, Inconclusive, BlockedBeforeStart }

internal sealed record AppBatchUpdateSummary(int Confirmed, int Inconclusive, int BlockedBeforeStart,
    int NotStarted, int Planned)
{
    internal bool ConservesItems => Confirmed >= 0 && Inconclusive >= 0 &&
        BlockedBeforeStart >= 0 && NotStarted >= 0 &&
        Confirmed + Inconclusive + BlockedBeforeStart + NotStarted == Planned;

    internal string Describe(bool userStopped) =>
        $"Atualizar todos: {Confirmed} confirmado(s), {Inconclusive} inconclusivo(s), " +
        $"{BlockedBeforeStart} bloqueado(s) antes da tentativa, {NotStarted} não iniciado(s)." +
        (userStopped ? " Interrompido por escolha do usuário." : "");
}

internal static class AppBatchUpdateRun
{
    internal const int MaxCandidates = 20;

    // Executores externos nunca herdam a autorização da fila, mesmo no intervalo
    // entre duas operações quando o executor individual já liberou _busy.
    internal static bool MayExecute(bool busy, bool batchActive, bool fromApprovedBatch) =>
        !busy && (!batchActive || fromApprovedBatch);

    private static string OneLine(string value) =>
        new(value.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());

    internal static string Preview(IReadOnlyList<AppBatchUpdateCandidate> candidates, int indicatedUpdates)
    {
        if (candidates.Count is < 1 or > MaxCandidates)
            throw new InvalidOperationException("Quantidade fora do limite de consentimento.");
        string targets = string.Join(Environment.NewLine, candidates.Select((c, index) =>
            $"{index + 1}. {OneLine(c.Name)} → {OneLine(c.TargetVersion)}"));
        int outside = Math.Max(0, indicatedUpdates - candidates.Count);
        return $"Atualizar todos os elegíveis ({candidates.Count})?\n" +
            $"Atualizações indicadas que não entram neste lote: {outside} (sem autorização direta/revisão/Store).\n\n" +
            targets +
            "\n\nRevise TODOS os nomes e versões acima (a lista é rolável). " +
            "Cada alvo será revalidado, em sequência. Instaladores podem fechar programas ou solicitar UAC. " +
            "Parar lote impede somente próximos itens. Falha ou resultado não confirmado interrompe o lote; " +
            "não existe reversão automática e não há repetição automática.";
    }

    internal static async Task<AppBatchUpdateSummary> RunAsync(
        IReadOnlyList<AppBatchUpdateCandidate> candidates,
        Func<AppBatchUpdateCandidate, Task<AppBatchUpdateStepResult>> attempt,
        Func<bool> stopBeforeNext, Action<Exception>? reportFailure = null)
    {
        if (candidates.Count is < 1 or > MaxCandidates)
            throw new InvalidOperationException("Lote fora do limite permitido.");
        int confirmed = 0, inconclusive = 0, blocked = 0;
        foreach (var candidate in candidates)
        {
            if (stopBeforeNext()) break;
            AppBatchUpdateStepResult result;
            try { result = await attempt(candidate); }
            catch (Exception ex)
            {
                try { reportFailure?.Invoke(ex); } catch { }
                result = AppBatchUpdateStepResult.Inconclusive;
            }
            switch (result)
            {
                case AppBatchUpdateStepResult.Confirmed:
                    confirmed++;
                    break;
                case AppBatchUpdateStepResult.BlockedBeforeStart:
                    blocked++;
                    break;
                default:
                    inconclusive++;
                    break;
            }
            if (result != AppBatchUpdateStepResult.Confirmed) break;
        }
        var summary = new AppBatchUpdateSummary(confirmed, inconclusive, blocked,
            candidates.Count - confirmed - inconclusive - blocked, candidates.Count);
        if (!summary.ConservesItems) throw new InvalidDataException("Resumo do lote inconsistente.");
        return summary;
    }
}
