using GerenciadorAplicativos;

internal static class BundlewareReviewTests
{
    internal static void Run()
    {
        const string msi = "{00000000-0000-0000-0000-000000000987}";
        var parent = new ProgramEntry
        {
            Name = "Suite OEM sintética", Publisher = "Contoso", Version = "1.0",
            InventoryId = "LocalMachine|Registry64|BundleParent",
            InventorySource = "Registro: Computador (64 bits)",
            BundleProviderKey = "Contoso.Bundle.01",
            UninstallString = "MsiExec.exe /X" + msi,
            MsiProductCode = msi, CanUninstall = true
        };
        var child = new ProgramEntry
        {
            Name = "Componente OEM sintético", Publisher = "Contoso", Version = "1.0",
            InventoryId = "LocalMachine|Registry64|BundleChild",
            InventorySource = "Registro: Computador (64 bits)",
            BundleParentKeyName = "BundleParent",
            InventoryOnly = true, CanUninstall = false
        };
        var independent = new ProgramEntry
        {
            Name = "Aplicativo autônomo", Publisher = "Contoso",
            InventoryId = "CurrentUser|Registry64|Standalone",
            InventorySource = "Registro: Usuário atual (64 bits)"
        };
        var scan = new RegistryInventoryScan([parent, child, independent], 3, 0, 0, 0, 0) { IncludesHiddenEntries = true };
        if (BundlewareReview.Evaluate(scan with { IncludesHiddenEntries = false }, parent).CanProposeRegisteredUninstall ||
            BundlewareReview.PrepareUserSelection(scan with { IncludesHiddenEntries = false },
                [parent.InventoryId]).Eligible)
            throw new Exception("Bundleware: inventário sem componentes ocultos não pode preparar exclusão.");
        var parentReview = BundlewareReview.Evaluate(scan, parent);
        if (parentReview.State != BundleReviewState.BundleRoot ||
            parentReview.RegisteredChildren.Count != 1 ||
            parentReview.RegisteredChildren[0] != child.InventoryId)
            throw new Exception("Bundleware: grupo não preservou identidade exata do pai.");
        if (BundlewareReview.Evaluate(scan, child).State != BundleReviewState.BundleComponent ||
            BundlewareReview.Evaluate(scan, independent).State != BundleReviewState.Independent ||
            BundlewareReview.CanLaunchIndividual(child))
            throw new Exception("Bundleware: componente, pai ou independente mal classificados.");
        if (!BundlewareReview.CanLaunchIndividual(parent))
            throw new Exception("Bundleware: raiz elegível não deveria ser tratada como filho.");
        var selected = BundlewareReview.PrepareUserSelection(scan, [parent.InventoryId]);
        if (!selected.Eligible || selected.Requests.Count != 1 ||
            selected.Requests[0].Original.InventoryId != parent.InventoryId)
            throw new Exception("Bundleware: raiz não foi preparada pelo motor MSI existente: " + selected.Reason);
        if (BundlewareReview.PrepareUserSelection(scan, [child.InventoryId]).Eligible ||
            BundlewareReview.PrepareUserSelection(scan, [parent.InventoryId, child.InventoryId]).Eligible ||
            BundlewareReview.PrepareUserSelection(scan with { ReadFailures = 1 }, [parent.InventoryId]).Eligible ||
            BundlewareReview.PrepareUserSelection(scan, [parent.InventoryId, parent.InventoryId]).Eligible ||
            BundlewareReview.PrepareUserSelection(scan, ["LocalMachine|Registry64|Unknown"]).Eligible)
            throw new Exception("Bundleware: seleção ambígua, componente ou incompleta expôs remoção.");
        var orphan = new ProgramEntry
        {
            Name = child.Name, Publisher = child.Publisher, Version = child.Version,
            InventoryId = child.InventoryId,
            InventorySource = child.InventorySource,
            BundleParentKeyName = "UnregisteredParent",
            InventoryOnly = true
        };
        var orphanScan = new RegistryInventoryScan([parent, orphan], 2, 0, 0, 0, 0) { IncludesHiddenEntries = true };
        if (BundlewareReview.Evaluate(orphanScan, orphan).State != BundleReviewState.UnresolvedParent ||
            BundlewareReview.PrepareUserSelection(orphanScan, [orphan.InventoryId]).Eligible)
            throw new Exception("Bundleware: pai ausente não foi bloqueado.");
        var corrupt = new ProgramEntry
        {
            Name = parent.Name, Publisher = parent.Publisher,
            InventoryId = parent.InventoryId, InventorySource = parent.InventorySource,
            BundleMetadataReliable = false,
            BundleParentKeyName = @"..\outro", CanUninstall = true
        };
        if (BundlewareReview.CanLaunchIndividual(corrupt) ||
            BundlewareReview.Evaluate(new RegistryInventoryScan([corrupt], 1, 0, 0, 0, 0) { IncludesHiddenEntries = true },
                corrupt).CanProposeRegisteredUninstall)
            throw new Exception("Bundleware: metadado inválido não bloqueou revisão.");
        Console.WriteLine("OK: bundleware por identidade ARP e seleção de bloatware explícita; nenhuma desinstalação.");
    }
}
