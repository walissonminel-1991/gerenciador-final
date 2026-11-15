namespace GerenciadorAplicativos;

// Primeira fase de atualizacao WUA: coordenacao verificavel. Sem adaptador
// nativo conectado a UI ou chamadas de download/instalacao neste arquivo.
internal sealed record DriverUpdatePilotChoice(string UpdateId, int Revision,
    string DeviceId, string HardwareId, string Provider, string InfName,
    string InstalledVersion, string DriverClass, string Title);
internal sealed record DriverUpdatePilotApproval(string UpdateId, int Revision,
    string DeviceId, bool Confirmed, bool DisposableWindows, DateTimeOffset AcceptedAt);
internal enum DriverUpdatePilotState
{
    Blocked, DownloadFailed, DownloadedOnly, InstallationUncertain,
    VerificationUncertain, PendingReboot, Verified
}
internal sealed record DriverUpdatePilotResult(DriverUpdatePilotState State, string Reason);
internal sealed record DriverUpdatePilotCheck(bool OfficialSource, bool ExactUpdate,
    bool SameDevice, bool ApplicableNow, bool NoAdditionalUpdates,
    bool EulaAlreadyAccepted, bool NonInteractive, bool RebootNotPending,
    bool Downloaded)
{
    internal bool BeforeDownload => OfficialSource && ExactUpdate && SameDevice &&
        ApplicableNow && NoAdditionalUpdates && EulaAlreadyAccepted &&
        NonInteractive && RebootNotPending;
    internal bool BeforeInstall => BeforeDownload && Downloaded;
}
internal sealed record DriverUpdatePilotStep(bool Success, bool RebootRequired);
internal sealed record DriverUpdatePilotObservation(bool SameDevice,
    bool InfOrVersionChanged, bool Healthy, bool RebootRequired);
internal interface IDriverUpdatePilotBackend
{
    Task<DriverUpdatePilotCheck> Revalidate(DriverUpdatePilotChoice choice, CancellationToken token);
    Task<DriverUpdatePilotStep> Download(DriverUpdatePilotChoice choice, CancellationToken token);
    Task<DriverUpdatePilotStep> Install(DriverUpdatePilotChoice choice);
    Task<DriverUpdatePilotObservation> Observe(DriverUpdatePilotChoice choice);
}
internal interface IDriverUpdatePilotJournal
{
    bool TryBegin(DriverUpdatePilotChoice choice);
    void Mark(DriverUpdatePilotChoice choice, DriverUpdatePilotState state);
}
internal static class DriverUpdatePilot
{
    // Lote experimental delimitado: nao tocar classes de rede, display, disco,
    // armazenamento, USB, sistema, firmware ou classes desconhecidas.
    internal static bool AllowedClass(string? value) =>
        value is not null && (value.Equals("Printer", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Ports", StringComparison.OrdinalIgnoreCase));

    internal static bool TryPrepare(DriverInventoryResult inventory,
        DriverOfficialResult official, DriverOfficialMatch selected,
        out DriverUpdatePilotChoice? choice)
    {
        choice = null;
        if (inventory is null || official is null || selected is null ||
            inventory.Partial || official.Partial || inventory.Items.Count is < 1 or > 4000 ||
            official.SearchContext is not { Source: "Windows Update", Online: true } ||
            !AllowedClass(selected.Offer.DriverClass) ||
            selected.Offer.EntryAudit?.State != DriverOfficialEntryState.Complete ||
            selected.Offer.Relations?.State != DriverOfficialRelationsState.Complete ||
            selected.MatchingWuaEntry is null || selected.Installed is null ||
            !Guid.TryParseExact(selected.Offer.UpdateId, "D", out var guid) ||
            guid == Guid.Empty || selected.Offer.Revision < 1 ||
            !DriverInventory.ValidHardwareId(selected.Offer.HardwareId))
            return false;
        // A identidade precisa ser unica no inventario global e na oferta.
        var found = inventory.Items.Where(x =>
            string.Equals(x.DeviceId, selected.DeviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
        var offered = official.Matches.Where(x =>
            string.Equals(x.DeviceId, selected.DeviceId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Offer.UpdateId, selected.Offer.UpdateId, StringComparison.OrdinalIgnoreCase) &&
            x.Offer.Revision == selected.Offer.Revision).ToArray();
        if (found.Length != 1 || offered.Length != 1 || !Equals(offered[0], selected))
            return false;
        var device = found[0];
        if (device.Signed != true || device.Present != true ||
            device.WithoutSignedDriverAssociation ||
            device.PackageEvidence is { State: not DriverStoreState.Mapped } ||
            string.IsNullOrWhiteSpace(device.InfName) ||
            string.IsNullOrWhiteSpace(device.Version) ||
            string.IsNullOrWhiteSpace(device.Provider) ||
            !device.HardwareIds.Contains(selected.Offer.HardwareId, StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(device.Provider, selected.Offer.Provider, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(device.Provider, selected.Installed.Provider, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(device.InfName, selected.Installed.InfName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(device.Version, selected.Installed.Version, StringComparison.Ordinal) ||
            !string.Equals(selected.MatchingWuaEntry.HardwareOrCompatibleId,
                selected.Offer.HardwareId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(selected.MatchingWuaEntry.DriverClass, selected.Offer.DriverClass,
                StringComparison.OrdinalIgnoreCase))
            return false;
        choice = new(selected.Offer.UpdateId, selected.Offer.Revision, device.DeviceId,
            selected.Offer.HardwareId, device.Provider, device.InfName, device.Version,
            selected.Offer.DriverClass, selected.Offer.Title);
        return true;
    }

    internal static bool ConsentValid(DriverUpdatePilotChoice choice,
        DriverUpdatePilotApproval approval, DateTimeOffset now) =>
        approval.Confirmed && approval.DisposableWindows &&
        Guid.TryParseExact(choice.UpdateId, "D", out var a) &&
        Guid.TryParseExact(approval.UpdateId, "D", out var b) && a == b &&
        approval.Revision == choice.Revision &&
        string.Equals(approval.DeviceId, choice.DeviceId, StringComparison.OrdinalIgnoreCase) &&
        approval.AcceptedAt <= now && approval.AcceptedAt >= now.AddMinutes(-5);

    internal static async Task<DriverUpdatePilotResult> Execute(
        DriverUpdatePilotChoice choice, DriverUpdatePilotApproval approval,
        IDriverUpdatePilotBackend backend, IDriverUpdatePilotJournal journal,
        CancellationToken token, DateTimeOffset? nowForTest = null)
    {
        if (choice is null || approval is null || backend is null || journal is null ||
            !AllowedClass(choice.DriverClass) ||
            !ConsentValid(choice, approval, nowForTest ?? DateTimeOffset.UtcNow) ||
            token.IsCancellationRequested)
            return new(DriverUpdatePilotState.Blocked, "Sem autorizacao especifica valida.");
        // Nao confiar no snapshot que criou o botao: revalidar na fonte nativa.
        var first = await backend.Revalidate(choice, token);
        if (!first.BeforeDownload || token.IsCancellationRequested)
            return new(DriverUpdatePilotState.Blocked, "Oferta/dispositivo nao revalidados.");
        DriverUpdatePilotStep downloaded;
        try { downloaded = await backend.Download(choice, token); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(DriverUpdatePilotState.DownloadFailed, "Download WUA nao comprovado.");
        }
        if (!downloaded.Success)
            return new(DriverUpdatePilotState.DownloadFailed, "Download nao concluido.");
        if (token.IsCancellationRequested)
            return new(DriverUpdatePilotState.DownloadedOnly, "Cancelado antes de instalar.");
        var second = await backend.Revalidate(choice, token);
        if (!second.BeforeInstall || token.IsCancellationRequested)
            return new(DriverUpdatePilotState.DownloadedOnly,
                "Nova leitura mudou oferta ou dispositivo; instalacao bloqueada.");
        if (!journal.TryBegin(choice))
            return new(DriverUpdatePilotState.DownloadedOnly,
                "Intencao duravel nao persistida; instalacao bloqueada.");
        // Install pode ter produzido efeito mesmo quando retorna erro. Nenhuma
        // repeticao automatica nem rollback presumido em caso de incerteza.
        DriverUpdatePilotStep installed;
        try { installed = await backend.Install(choice); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            journal.Mark(choice, DriverUpdatePilotState.InstallationUncertain);
            return new(DriverUpdatePilotState.InstallationUncertain,
                "Estado nativo incerto; reconciliar sem repetir a instalacao.");
        }
        if (!installed.Success)
        {
            journal.Mark(choice, DriverUpdatePilotState.InstallationUncertain);
            return new(DriverUpdatePilotState.InstallationUncertain,
                "Windows nao confirmou a instalacao; sem repeticao automatica.");
        }
        if (installed.RebootRequired)
        {
            journal.Mark(choice, DriverUpdatePilotState.PendingReboot);
            return new(DriverUpdatePilotState.PendingReboot,
                "Reinicio informado pelo Windows; sem reinicio automatico.");
        }
        DriverUpdatePilotObservation observed;
        try { observed = await backend.Observe(choice); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            journal.Mark(choice, DriverUpdatePilotState.VerificationUncertain);
            return new(DriverUpdatePilotState.VerificationUncertain,
                "Verificacao pos-instalacao indisponivel.");
        }
        var state = observed.RebootRequired ? DriverUpdatePilotState.PendingReboot :
            observed.SameDevice && observed.InfOrVersionChanged && observed.Healthy
                ? DriverUpdatePilotState.Verified : DriverUpdatePilotState.VerificationUncertain;
        journal.Mark(choice, state);
        return new(state, state == DriverUpdatePilotState.Verified
            ? "Instancia PnP, versao ou INF e estado confirmados."
            : "Sem prova completa de atualizacao; nao declarar sucesso.");
    }
}
