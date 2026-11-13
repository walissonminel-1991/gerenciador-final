namespace GerenciadorAplicativos;

// Uma leitura cancelada nunca pode substituir a ultima fotografia publicada.
internal sealed class DriverQueryCoordinator
{
    private CancellationTokenSource? _current;
    internal bool InProgress => _current is not null;
    internal CancellationToken Begin(TimeSpan timeout)
    {
        if (_current is not null) throw new InvalidOperationException("Consulta já em andamento.");
        _current = new CancellationTokenSource(timeout);
        return _current.Token;
    }
    internal bool RequestCancel()
    {
        if (_current is null || _current.IsCancellationRequested) return false;
        _current.Cancel();
        return true;
    }
    internal bool CanPublish(CancellationToken token) =>
        _current is not null && _current.Token == token && !token.IsCancellationRequested;
    internal void End(CancellationToken token)
    {
        if (_current is null || _current.Token != token) return;
        _current.Dispose();
        _current = null;
    }
}
 
// Uma consulta externa usa uma fotografia de inventário PnP e de pacotes.
// Guardar cópias das evidências, não somente referências que podem mudar
// durante a leitura COM/INF. Isto não é autorização de manutenção.
internal sealed class DriverReportPublicationGuard
{
    private readonly DriverInventoryResult inventory;
    private readonly DriverInventoryEntry? selected;
    private readonly DriverInventoryEntry[] frozenItems;
    private readonly DriverStoreEvidence[]? frozenStore;
    private readonly DriverPublishedPackage[]? frozenPublished;
    private readonly bool? frozenStorePartial;
    private readonly int? frozenWithoutInf;
    private readonly bool? frozenPublishedPartial;
    private readonly bool? frozenEnumerationComplete;
    private readonly long generation;

    internal DriverReportPublicationGuard(DriverInventoryResult inventory,
        DriverInventoryEntry? selected, long generation)
    {
        this.inventory = inventory;
        this.selected = selected is null ? null : Freeze(selected);
        this.generation = generation;
        frozenItems = inventory.Items.Select(Freeze).ToArray();
        frozenStore = inventory.Store?.Packages.ToArray();
        frozenPublished = inventory.PublishedPackages?.Packages.ToArray();
        frozenStorePartial = inventory.Store?.Partial;
        frozenWithoutInf = inventory.Store?.WithoutInf;
        frozenPublishedPartial = inventory.PublishedPackages?.Partial;
        frozenEnumerationComplete = inventory.PublishedPackages?.EnumerationComplete;
    }

    private static DriverInventoryEntry Freeze(DriverInventoryEntry item) =>
        item with
        {
            HardwareIds = item.HardwareIds.ToArray(),
            WindowsEvents = item.WindowsEvents.ToArray()
        };

    private static bool SameEvidence(DriverInventoryEntry? a, DriverInventoryEntry? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return string.Equals(a.DeviceId, b.DeviceId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.InfName, b.InfName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.Version, b.Version, StringComparison.Ordinal) &&
            string.Equals(a.Provider, b.Provider, StringComparison.OrdinalIgnoreCase) &&
            a.Signed == b.Signed && a.Present == b.Present &&
            a.PnpProblemCode == b.PnpProblemCode &&
            a.WithoutSignedDriverAssociation == b.WithoutSignedDriverAssociation &&
            a.PackageEvidence == b.PackageEvidence &&
            a.HardwareIds.Count == b.HardwareIds.Count &&
            a.HardwareIds.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(b.HardwareIds) &&
            a.WindowsEvents.SequenceEqual(b.WindowsEvents);
    }

    private bool SamePackageSnapshots()
    {
        if (inventory.Store?.Partial != frozenStorePartial ||
            inventory.Store?.WithoutInf != frozenWithoutInf ||
            inventory.PublishedPackages?.Partial != frozenPublishedPartial ||
            inventory.PublishedPackages?.EnumerationComplete != frozenEnumerationComplete)
            return false;
        if ((frozenStore is null) != (inventory.Store is null) ||
            (frozenPublished is null) != (inventory.PublishedPackages is null))
            return false;
        return (frozenStore is null ||
                inventory.Store!.Packages.SequenceEqual(frozenStore)) &&
            (frozenPublished is null ||
                inventory.PublishedPackages!.Packages.SequenceEqual(frozenPublished));
    }

    private bool SelectedUnique()
    {
        if (selected is null) return true;
        var group = frozenItems.Where(d =>
            string.Equals(d.DeviceId, selected.DeviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
        // Duas linhas WMI para a mesma instância podem trazer INF distinto;
        // até duplicata idêntica não prova associação unívoca do pacote.
        return !string.IsNullOrWhiteSpace(selected.DeviceId) &&
            group.Length == 1 && SameEvidence(group[0], selected);
    }

    // Quando uma fonte de pacote realmente observou o mesmo INF, diferenças
    // em caminho, estado, estágio ou erro são contradições. Coleções parciais
    // não provam a AUSÊNCIA de um INF que não foi enumerado.
    private bool PackageCoherent(DriverInventoryEntry device)
    {
        var package = device.PackageEvidence;
        if (package is null) return true;
        if (!string.Equals(package.InfName, device.InfName, StringComparison.OrdinalIgnoreCase))
            return false;
        if (inventory.Store is { } store)
        {
            var matches = store.Packages.Where(x =>
                string.Equals(x.InfName, device.InfName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1 || matches[0] != package) return false;
        }
        if (inventory.PublishedPackages is { } published)
        {
            var matches = published.Packages.Where(x =>
                string.Equals(x.Evidence.InfName, device.InfName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1) return false;
            if (matches.Length == 1)
            {
                var observed = matches[0].Evidence;
                if (observed.State != package.State ||
                    !string.Equals(observed.InfName, package.InfName,
                        StringComparison.OrdinalIgnoreCase))
                    return false;
                if (observed.State == DriverStoreState.Mapped &&
                    (!string.Equals(observed.StorePath, package.StorePath,
                        StringComparison.OrdinalIgnoreCase) ||
                     !string.Equals(observed.PublishedPath, package.PublishedPath,
                        StringComparison.OrdinalIgnoreCase)))
                    return false;
            }
            if (matches.Length == 0 && published.EnumerationComplete && !published.Partial)
                return false;
        }
        return true;
    }

    internal bool IsCurrent(DriverInventoryResult? current, DriverInventoryEntry? selection,
        long currentGeneration)
    {
        if (!ReferenceEquals(inventory, current) || currentGeneration != generation ||
            !SameEvidence(selected, selection) || !SamePackageSnapshots() ||
            inventory.Items.Count != frozenItems.Length || !SelectedUnique())
            return false;
        for (int i = 0; i < frozenItems.Length; i++)
            if (!SameEvidence(frozenItems[i], inventory.Items[i])) return false;
        return selected is null || PackageCoherent(selected);
    }

    internal bool CanPublish(DriverInventoryResult? current, DriverInventoryEntry? selection,
        long currentGeneration, CancellationToken token, DriverOfficialResult result)
    {
        if (token.IsCancellationRequested || !IsCurrent(current, selection, currentGeneration) ||
            result is null || (result.Partial && result.Matches.Count != 0))
            return false;
        foreach (var match in result.Matches)
        {
            if (!Guid.TryParseExact(match.Offer.UpdateId, "D", out var id) ||
                id == Guid.Empty || match.Offer.Revision < 1 ||
                match.CanInstall || match.IsNewerProven || match.CompletePackageVerified)
                return false;
            var matches = frozenItems.Where(d =>
                string.Equals(d.DeviceId, match.DeviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1) return false;
            var device = matches[0];
            if (!PackageCoherent(device) ||
                !device.HardwareIds.Contains(match.Offer.HardwareId, StringComparer.OrdinalIgnoreCase) ||
                match.Installed is null ||
                !string.Equals(device.InfName, match.Installed.InfName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(device.Version, match.Installed.Version, StringComparison.Ordinal) ||
                !string.Equals(device.Provider, match.Installed.Provider, StringComparison.OrdinalIgnoreCase) ||
                device.Signed != match.Installed.SignedReportedByWindows ||
                device.PnpProblemCode != match.Installed.PnpProblemCode ||
                device.WithoutSignedDriverAssociation != match.Installed.WithoutSignedDriverAssociation ||
                device.PackageEvidence?.State != match.Installed.StoreState)
                return false;
        }
        return true;
    }

    internal bool CanPublishInf(DriverInventoryResult? current, DriverInventoryEntry? selection,
        long currentGeneration, CancellationToken token, DriverInfVerificationResult result)
    {
        if (token.IsCancellationRequested || !IsCurrent(current, selection, currentGeneration) ||
            selected?.PackageEvidence?.State != DriverStoreState.Mapped ||
            !PackageCoherent(selected) || result is null ||
            result.CanInstall || result.CanMaintain)
            return false;
        if (result.Trust == DriverInfTrust.VerifiedInf)
        {
            // Uma assinatura 'verificada' sem a prova mínima retornada pelo
            // próprio verificador nunca deve ser exibida como comprovada.
            if (result.Sha256.Length != 64 ||
                result.Sha256.Any(c => !Uri.IsHexDigit(c)) ||
                string.IsNullOrWhiteSpace(result.Catalog) ||
                string.IsNullOrWhiteSpace(result.Signer))
                return false;
        }
        return true;
    }
}
