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
        var executable = UninstallBatch.Select([parent]).Single();
        var initialSnapshot = new UninstallSnapshot([parent, child, independent], true);
        if (!BundlewareReview.RelationsStillSafe(executable, initialSnapshot, initialSnapshot) ||
            BundlewareReview.RelationsStillSafe(executable, initialSnapshot,
                new UninstallSnapshot([parent, independent], true)) ||
            BundlewareReview.RelationsStillSafe(executable, initialSnapshot,
                new UninstallSnapshot([parent, child, independent], false)))
            throw new Exception("Bundleware: ligação ao executor padrão não preserva o relacionamento completo.");
        var brokenArpIdentity = new ProgramEntry
        {
            Name = "ARP com chave de identidade malformada",
            InventoryId = "LocalMachine|Registry64|Other|Extra",
            InventorySource = "Registro: Computador (64 bits)"
        };
        if (BundlewareReview.RelationsStillSafe(
                new UninstallRequest(brokenArpIdentity, executable.Command),
                initialSnapshot, initialSnapshot))
            throw new Exception("Bundleware: identidade ARP malformada ignorou guarda.");
        var impact = BundlewareReview.ExplainPotentialBundleImpact(parent, initialSnapshot);
        if (!impact.Contains("1 componente(s)", StringComparison.Ordinal) ||
            !impact.Contains("pode afetá-los", StringComparison.Ordinal) ||
            BundlewareReview.ExplainPotentialBundleImpact(independent, initialSnapshot) != "" ||
            !BundlewareReview.ExplainPotentialBundleImpact(parent,
                new UninstallSnapshot(initialSnapshot.Items, false))
                .Contains("não puderam ser conferidas", StringComparison.Ordinal))
            throw new Exception("Bundleware: aviso de impacto do pacote incorreto ou exagerado.");
        var removedParent = new UninstallOutcome(executable, UninstallState.RegistrationAbsent);
        var childSurvived = BundlewareReview.ObserveAfterUninstall(removedParent,
            initialSnapshot, new UninstallSnapshot([child, independent], true));
        var allArpGone = BundlewareReview.ObserveAfterUninstall(removedParent,
            initialSnapshot, new UninstallSnapshot([independent], true));
        var parentReturned = BundlewareReview.ObserveAfterUninstall(removedParent,
            initialSnapshot, new UninstallSnapshot([parent, child, independent], true));
        var uncertainAfter = BundlewareReview.ObserveAfterUninstall(removedParent,
            initialSnapshot, new UninstallSnapshot([independent], false));
        var incompleteUninstall = BundlewareReview.ObserveAfterUninstall(
            new UninstallOutcome(executable, UninstallState.Pending), initialSnapshot,
            new UninstallSnapshot([independent], true));
        if (childSurvived.State != BundlePostRemovalState.ComponentsStillRegistered ||
            childSurvived.RelatedBefore != 1 || childSurvived.RelatedRemaining != 1 ||
            !childSurvived.MustShowPartialResult ||
            allArpGone.State != BundlePostRemovalState.RegistrationsAbsent ||
            allArpGone.ConfirmsCleanUninstall || allArpGone.MayRemoveRemainingComponents ||
            allArpGone.MustShowPartialResult ||
            parentReturned.State != BundlePostRemovalState.ParentStillRegistered ||
            !parentReturned.MustShowPartialResult ||
            uncertainAfter.State != BundlePostRemovalState.Inconclusive ||
            !uncertainAfter.MustShowPartialResult ||
            incompleteUninstall.State != BundlePostRemovalState.PendingObservation ||
            !incompleteUninstall.MustShowPartialResult)
            throw new Exception("Bundleware: conciliação posterior de registros ARP promoveu conclusão ou perdeu dependências.");
        var independentRequest = new UninstallRequest(independent, executable.Command);
        var independentOutcome = new UninstallOutcome(independentRequest,
            UninstallState.RegistrationAbsent);
        if (BundlewareReview.ObserveAfterUninstall(independentOutcome, initialSnapshot,
                new UninstallSnapshot([parent, child], true)).State !=
                BundlePostRemovalState.NotApplicable)
            throw new Exception("Bundleware: aplicativo independente ganhou falsa pendência de componentes.");
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
        // Prova funcional do encaminhamento ao executor já existente:
        // delegates totalmente sintéticos não chamam MSI nem alteram Windows.
        int dispatched = 0;
        bool launchObserved = false;
        Task<UninstallSnapshot> FullScan() => Task.FromResult(
            new UninstallSnapshot(launchObserved ? [] : [parent, child, independent], true));
        Task<int?> FakeLaunch(UninstallRequest request, CancellationToken _) 
        {
            dispatched++;
            launchObserved = true;
            return Task.FromResult<int?>(0);
        }
        Task<RegistryInventoryScan> FreshArp() => Task.FromResult(scan);
        var denied = BundlewareReview.RunConfirmedSelectionAsync(
            scan, [parent.InventoryId], false, FreshArp, FullScan,
            FakeLaunch, _ => Task.CompletedTask).GetAwaiter().GetResult();
        if (denied.SentToExecutor || dispatched != 0)
            throw new Exception("Bundleware: nenhuma remoção pode iniciar sem aceite.");
        var changedChild = new ProgramEntry
        {
            Name = child.Name, Publisher = child.Publisher, Version = child.Version,
            InventoryId = child.InventoryId, InventorySource = child.InventorySource,
            BundleParentKeyName = "OutroPacote", InventoryOnly = true
        };
        var changed = scan with { Items = [parent, changedChild, independent] };
        if (BundlewareReview.RelationsStillSafe(executable, initialSnapshot,
                new UninstallSnapshot([parent, changedChild, independent], true)))
            throw new Exception("Bundleware: mudança da relação no executor padrão não foi bloqueada.");

        var stale = BundlewareReview.RunConfirmedSelectionAsync(
            scan, [parent.InventoryId], true, () => Task.FromResult(changed),
            FullScan, FakeLaunch, _ => Task.CompletedTask).GetAwaiter().GetResult();
        if (stale.SentToExecutor || dispatched != 0)
            throw new Exception("Bundleware: dependência alterada foi aceita indevidamente.");
        var atLaunchChanged = BundlewareReview.RunConfirmedSelectionAsync(
            scan, [parent.InventoryId], true, FreshArp,
            () => Task.FromResult(new UninstallSnapshot(
                [parent, changedChild, independent], true)),
            FakeLaunch, _ => Task.CompletedTask).GetAwaiter().GetResult();
        if (!atLaunchChanged.SentToExecutor || dispatched != 0 ||
            atLaunchChanged.Outcomes.Count != 1 ||
            atLaunchChanged.Outcomes[0].State != UninstallState.Blocked ||
            atLaunchChanged.Outcomes[0].DiagnosticReason != "bundle_relationship_changed")
            throw new Exception("Bundleware: alteração no último snapshot não foi recusada antes do uninstaller.");
        var routed = BundlewareReview.RunConfirmedSelectionAsync(
            scan, [parent.InventoryId], true, FreshArp, FullScan, FakeLaunch,
            _ => Task.CompletedTask).GetAwaiter().GetResult();
        if (!routed.SentToExecutor || dispatched != 1 ||
            routed.Outcomes.Count != 1 ||
            routed.Outcomes[0].State != UninstallState.RegistrationAbsent ||
            routed.PostRemovalReviews.Count != 1 ||
            routed.PostRemovalReviews[0].State != BundlePostRemovalState.RegistrationsAbsent ||
            routed.PostRemovalReviews[0].ConfirmsCleanUninstall)
            throw new Exception("Bundleware: executor e conciliação ARP não confirmaram apenas ausência de registros.");
        launchObserved = false;
        Task<int?> UncertainLaunch(UninstallRequest _, CancellationToken __) =>
            Task.FromException<int?>(new Exception("Falha sintética após iniciar executor"));
        var uncertain = BundlewareReview.RunConfirmedSelectionAsync(
            scan, [parent.InventoryId], true, FreshArp, FullScan,
            UncertainLaunch, _ => Task.CompletedTask).GetAwaiter().GetResult();
        if (!uncertain.SentToExecutor ||
            !uncertain.Reason.Contains("incerto", StringComparison.OrdinalIgnoreCase) ||
            uncertain.Reason.Contains("nenhuma remoção iniciada", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Bundleware: falha após handoff foi tratada como nenhuma remoção.");
        // Metadados quebrados de aplicativo não relacionado não interrompem
        // a desinstalação registrada de outra suite; filhos ligados falham.
        var unrelatedBroken = new ProgramEntry
        {
            Name = "Terceiro com metadados inválidos",
            InventoryId = "CurrentUser|Registry64|OtherBad",
            InventorySource = "Registro: Usuário atual (64 bits)",
            BundleMetadataReliable = false, BundleParentKeyName = @"..\\other",
            InventoryOnly = true
        };
        var withUnrelated = scan with { Items = [parent, child, independent, unrelatedBroken] };
        if (!BundlewareReview.RelationsStillSafe(executable, initialSnapshot,
                new UninstallSnapshot([parent, child, independent, unrelatedBroken], true)) ||
            BundlewareReview.Evaluate(withUnrelated, parent).State != BundleReviewState.BundleRoot)
            throw new Exception("Bundleware: metadados de terceiro afetaram pacote sem relação.");
        var linkedBroken = new ProgramEntry
        {
            Name = child.Name, InventoryId = child.InventoryId,
            InventorySource = child.InventorySource,
            BundleParentKeyName = "BundleParent", BundleMetadataReliable = false,
            InventoryOnly = true
        };
        if (BundlewareReview.RelationsStillSafe(executable, initialSnapshot,
                new UninstallSnapshot([parent, linkedBroken, independent], true)) ||
            BundlewareReview.Evaluate(scan with { Items = [parent, linkedBroken, independent] },
                parent).CanProposeRegisteredUninstall)
            throw new Exception("Bundleware: filho vinculado com metadados invalidos passou na barreira.");
        Console.WriteLine("OK: bundleware por identidade ARP e seleção de bloatware explícita; nenhuma desinstalação.");
    }
}
