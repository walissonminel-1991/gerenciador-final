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
 
// Uma consulta externa usa uma fotografia imutável da UI. O retorno só pode
// ser exibido na geração/seleção que o solicitou, mesmo após timeout ou
// encerramento tardio do worker STA. Não concede autoridade para instalar.
internal sealed class DriverReportPublicationGuard
{
    private readonly DriverInventoryResult inventory;
    private readonly DriverInventoryEntry? selected;
    private readonly long generation;
    internal DriverReportPublicationGuard(DriverInventoryResult inventory,
        DriverInventoryEntry? selected, long generation)
    {
        this.inventory = inventory;
        this.selected = selected;
        this.generation = generation;
    }

    // Identidade de dispositivo + evidência instalada; nomes de exibição e
    // índice na lista não constituem identidade.
    private static bool SameEvidence(DriverInventoryEntry? a, DriverInventoryEntry? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return string.Equals(a.DeviceId, b.DeviceId, StringComparison.OrdinalIgnoreCase) &&
            a.InfName.Equals(b.InfName, StringComparison.OrdinalIgnoreCase) &&
            a.Version.Equals(b.Version, StringComparison.Ordinal) &&
            a.Provider.Equals(b.Provider, StringComparison.OrdinalIgnoreCase) &&
            a.Signed == b.Signed && a.Present == b.Present &&
            a.PnpProblemCode == b.PnpProblemCode &&
            a.WithoutSignedDriverAssociation == b.WithoutSignedDriverAssociation &&
            a.PackageEvidence?.State == b.PackageEvidence?.State &&
            string.Equals(a.PackageEvidence?.InfName, b.PackageEvidence?.InfName,
                StringComparison.OrdinalIgnoreCase) &&
            a.HardwareIds.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(b.HardwareIds);
    }

    internal bool IsCurrent(DriverInventoryResult? current, DriverInventoryEntry? selection,
        long currentGeneration) =>
        ReferenceEquals(inventory, current) && currentGeneration == generation &&
        SameEvidence(selected, selection) &&
        (selected is null || inventory.Items.Any(x => SameEvidence(selected, x)));

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
            var device = inventory.Items.FirstOrDefault(d =>
                string.Equals(d.DeviceId, match.DeviceId, StringComparison.OrdinalIgnoreCase) &&
                d.HardwareIds.Contains(match.Offer.HardwareId, StringComparer.OrdinalIgnoreCase));
            if (device is null || match.Installed is null ||
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
}
