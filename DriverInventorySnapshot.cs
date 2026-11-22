namespace GerenciadorAplicativos;

// Limite de consistencia da fotografia consultiva: reavaliar fontes mutaveis
// antes de qualquer publicacao de UI, relatorio ou historico. Duas leituras
// iguais NAO provam atomicidade contra escritores concorrentes sem contrato.
internal static class DriverInventorySnapshot
{
    internal static DriverInventoryResult FreezeStable(DriverInventoryResult source,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var first = FreezeOnce(source);
        token.ThrowIfCancellationRequested();
        var second = FreezeOnce(source);
        if (!Equivalent(first, second) || !Coherent(second))
            throw new InvalidDataException("Fotografia de drivers mudou durante a cópia; consulta preservada.");
        token.ThrowIfCancellationRequested();
        return second;
    }

    private static DriverInventoryResult FreezeOnce(DriverInventoryResult source) =>
        source with
        {
            Items = source.Items.Select(item => item with
            {
                HardwareIds = item.HardwareIds.ToArray(),
                CompatibleIds = item.CompatibleIds.ToArray(),
                WindowsEvents = item.WindowsEvents.ToArray()
            }).ToArray(),
            OtherPnpTargetIds = source.OtherPnpTargetIds.ToArray(),
            Store = source.Store is null ? null : source.Store with
            {
                Packages = source.Store.Packages.ToArray()
            },
            PublishedPackages = source.PublishedPackages is null ? null :
                source.PublishedPackages with
                {
                    Packages = source.PublishedPackages.Packages.ToArray()
                },
            WindowsHistory = source.WindowsHistory is null ? null :
                source.WindowsHistory with
                {
                    Events = source.WindowsHistory.Events.ToArray()
                }
        };

    private static bool SameDevice(DriverInventoryEntry a, DriverInventoryEntry b) =>
        a.DeviceId == b.DeviceId && a.DeviceName == b.DeviceName &&
        a.Provider == b.Provider && a.Version == b.Version &&
        a.InfName == b.InfName && a.Signed == b.Signed &&
        a.Present == b.Present && a.PnpProblemCode == b.PnpProblemCode &&
        a.ReportedDriverClass == b.ReportedDriverClass &&
        a.WithoutSignedDriverAssociation == b.WithoutSignedDriverAssociation &&
        a.DeviceMetadata == b.DeviceMetadata &&
        a.DeviceBinding == b.DeviceBinding &&
        a.PackageEvidence == b.PackageEvidence &&
        a.HardwareIds.SequenceEqual(b.HardwareIds, StringComparer.Ordinal) &&
        a.CompatibleIds.SequenceEqual(b.CompatibleIds, StringComparer.Ordinal) &&
        a.WindowsEvents.SequenceEqual(b.WindowsEvents);

    private static bool Equivalent(DriverInventoryResult a, DriverInventoryResult b)
    {
        if (a.Partial != b.Partial || a.Status != b.Status ||
            !a.OtherPnpTargetIds.SequenceEqual(b.OtherPnpTargetIds, StringComparer.Ordinal) ||
            a.PnpEntities != b.PnpEntities || a.SignedRows != b.SignedRows ||
            a.Items.Count != b.Items.Count ||
            (a.Store is null) != (b.Store is null) ||
            (a.PublishedPackages is null) != (b.PublishedPackages is null) ||
            (a.WindowsHistory is null) != (b.WindowsHistory is null))
            return false;
        for (int i = 0; i < a.Items.Count; i++)
            if (!SameDevice(a.Items[i], b.Items[i])) return false;

        if (a.Store is { } store)
        {
            var other = b.Store!;
            if (store.Partial != other.Partial || store.WithoutInf != other.WithoutInf ||
                !store.Packages.SequenceEqual(other.Packages)) return false;
        }
        if (a.PublishedPackages is { } published)
        {
            var other = b.PublishedPackages!;
            if (published.Partial != other.Partial ||
                published.EnumerationComplete != other.EnumerationComplete ||
                !published.Packages.SequenceEqual(other.Packages)) return false;
        }
        if (a.WindowsHistory is { } history)
        {
            var other = b.WindowsHistory!;
            if (history.Partial != other.Partial ||
                history.Unavailable != other.Unavailable ||
                history.TailOnly != other.TailOnly ||
                history.RejectedSections != other.RejectedSections ||
                !history.Events.SequenceEqual(other.Events)) return false;
        }
        return true;
    }

    private static bool Coherent(DriverInventoryResult result)
    {
        // Nao bloquear a visualizacao de DeviceId ambiguo por si so:
        // G1.9/G1.19 ja bloqueiam prova de identidade e historico.
        // Verificar somente contradicoes entre evidencias explicitamente
        // observadas (nao inferir ausencia em enumeracao parcial).
        foreach (var item in result.Items)
        {
            if (item.DeviceId is null || item.InfName is null ||
                item.HardwareIds is null || item.CompatibleIds is null || item.WindowsEvents is null)
                return false;
            if (item.WindowsEvents.Any(e => e is null ||
                !string.Equals(e.DeviceId, item.DeviceId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(e.InfName, item.InfName, StringComparison.OrdinalIgnoreCase)))
                return false;
            if (item.PackageEvidence is not { } evidence) continue;
            if (!string.Equals(evidence.InfName, item.InfName, StringComparison.OrdinalIgnoreCase))
                return false;
            if (result.Store is { } store)
            {
                var matching = store.Packages.Where(e =>
                    string.Equals(e.InfName, evidence.InfName, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matching.Length > 1 ||
                    (matching.Length == 1 && matching[0] != evidence))
                    return false;
                // Nenhum match na enumeração parcial não prova ausência.
            }
            if (result.PublishedPackages is { } published)
            {
                var matching = published.Packages.Where(e =>
                    string.Equals(e.Evidence.InfName, evidence.InfName,
                        StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matching.Length > 1) return false;
                if (matching.Length == 1)
                {
                    var observed = matching[0].Evidence;
                    if (observed.State != evidence.State ||
                        (observed.State == DriverStoreState.Mapped &&
                         (!string.Equals(observed.StorePath, evidence.StorePath,
                             StringComparison.OrdinalIgnoreCase) ||
                          !string.Equals(observed.PublishedPath, evidence.PublishedPath,
                             StringComparison.OrdinalIgnoreCase))))
                        return false;
                }
            }
        }
        return true;
    }
}
