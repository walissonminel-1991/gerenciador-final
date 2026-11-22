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
