namespace GerenciadorAplicativos;

// Primeira fase de atualizacao WUA: coordenacao verificavel. Sem adaptador
// nativo conectado a UI ou chamadas de download/instalacao neste arquivo.
internal sealed record DriverUpdatePilotChoice(string UpdateId, int Revision,
    string DeviceId, string HardwareId, string Provider, string InfName,
    string InstalledVersion, string DriverClass, string Title);
internal sealed record DriverUpdatePilotApproval(string UpdateId, int Revision,
    string DeviceId, bool Confirmed, bool DisposableWindows, DateTimeOffset AcceptedAt)
{
    // G3.2: via física separada, condicionada também à prova de backup OEM.
    internal bool PhysicalWindowsApproved { get; init; }
}
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
            selected.Kind != DriverOfficialMatchKind.PrimaryHardwareId ||
            !AllowedClass(selected.Offer.DriverClass) ||
            selected.Offer.EntryAudit is not { State: DriverOfficialEntryState.Complete, Entries.Count: 1 } ||
            selected.Offer.Relations?.State != DriverOfficialRelationsState.Complete ||
            selected.MatchingWuaEntry is null || selected.Installed is null ||
            selected.Offer.EntryAudit.Entries[0] != selected.MatchingWuaEntry ||
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
        // O mesmo identificador também pode atingir outra instância por CompatibleID.
        if (!DriverInventory.HasExclusiveHardwareId(inventory, selected.Offer.HardwareId))
            return false;
        if (device.Signed != true || device.Present != true ||
            device.BindingStatus == DriverBindingStatus.Conflicting ||
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
        approval.Confirmed && (approval.DisposableWindows != approval.PhysicalWindowsApproved) &&
        Guid.TryParseExact(choice.UpdateId, "D", out var a) &&
        Guid.TryParseExact(approval.UpdateId, "D", out var b) && a == b &&
        approval.Revision == choice.Revision &&
        string.Equals(approval.DeviceId, choice.DeviceId, StringComparison.OrdinalIgnoreCase) &&
        approval.AcceptedAt <= now && approval.AcceptedAt >= now.AddMinutes(-5);

    private static void Notify(Action<string>? progress, string message)
    {
        // O observador não concede autoridade e pode ter sido descartado.
        try { progress?.Invoke(message); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
    }

    private static bool Current(Func<bool>? check)
    {
        try { return check?.Invoke() ?? true; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    private static bool MarkSafely(IDriverUpdatePilotJournal journal,
        DriverUpdatePilotChoice choice, DriverUpdatePilotState state)
    {
        try { journal.Mark(choice, state); return true; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    internal static async Task<DriverUpdatePilotResult> Execute(
        DriverUpdatePilotChoice choice, DriverUpdatePilotApproval approval,
        IDriverUpdatePilotBackend backend, IDriverUpdatePilotJournal journal,
        CancellationToken token, DateTimeOffset? nowForTest = null,
        Func<bool>? stillCurrent = null, Action<string>? progress = null,
        DriverG32RecoveryTicket? recovery = null)
    {
        if (choice is null || approval is null || backend is null || journal is null ||
            !AllowedClass(choice.DriverClass) ||
            !ConsentValid(choice, approval, nowForTest ?? DateTimeOffset.UtcNow) ||
            token.IsCancellationRequested || !Current(stillCurrent))
            return new(DriverUpdatePilotState.Blocked, "Autorizacao, consulta ou origem inválida.");
        // Não há caminho nativo por mera mudança de checkbox, COM ou teste.
        // Exigir prova G3.2 real antes mesmo da primeira consulta WUA.
        if (backend is DriverWuaNativeBackend)
        {
            if (!DriverG32RecoveryTicket.HasUpdateOperation ||
                !approval.PhysicalWindowsApproved || approval.DisposableWindows ||
                recovery is null || !recovery.Matches(choice) ||
                journal is not DriverUpdatePilotFileJournal)
                return new(DriverUpdatePilotState.Blocked,
                    "G3.2: prova de backup, diário ou autorização física ausente.");
        }
        else if (approval.PhysicalWindowsApproved)
            return new(DriverUpdatePilotState.Blocked,
                "Autorização física não pode ser executada por backend simulado.");
        Notify(progress, "Revalidando oferta e dispositivo no Windows Update…");
        DriverUpdatePilotCheck first;
        try { first = await backend.Revalidate(choice, token); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(DriverUpdatePilotState.Blocked,
                "Revalidação indisponível; nenhuma operação de atualização.");
        }
        if (!first.BeforeDownload || token.IsCancellationRequested || !Current(stillCurrent))
            return new(DriverUpdatePilotState.Blocked,
                "Oferta, identidade ou janela revogadas antes do download.");

        Notify(progress, "Baixando pacote oficial selecionado (sem instalar)…");
        DriverUpdatePilotStep downloaded;
        try { downloaded = await backend.Download(choice, token); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(DriverUpdatePilotState.DownloadFailed, "Download não comprovado.");
        }
        if (!downloaded.Success)
            return new(DriverUpdatePilotState.DownloadFailed, "Download não concluído.");
        if (token.IsCancellationRequested || !Current(stillCurrent))
            return new(DriverUpdatePilotState.DownloadedOnly,
                "Cancelado ou revogado após download; instalação não iniciada.");

        Notify(progress, "Revalidando novamente antes de qualquer instalação…");
        DriverUpdatePilotCheck second;
        try { second = await backend.Revalidate(choice, token); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(DriverUpdatePilotState.DownloadedOnly,
                "Segunda revalidação indisponível; instalação não iniciada.");
        }
        if (!second.BeforeInstall || token.IsCancellationRequested || !Current(stillCurrent))
            return new(DriverUpdatePilotState.DownloadedOnly,
                "Oferta ou instância mudou; instalação bloqueada.");
        if (backend is DriverWuaNativeBackend &&
            (recovery is null || !await recovery.RevalidateBeforeInstallAsync(
                choice, () => Current(stillCurrent), token) ||
             token.IsCancellationRequested || !Current(stillCurrent)))
            return new(DriverUpdatePilotState.DownloadedOnly,
                "Backup, assinatura, pacote ou seleção não foram reconfirmados; instalação bloqueada.");
        Notify(progress, "Registrando intenção durável antes da instalação…");
        bool journalReady;
        try { journalReady = journal.TryBegin(choice); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { journalReady = false; }
        if (!journalReady)
            return new(DriverUpdatePilotState.DownloadedOnly,
                "Registro durável indisponível; instalação bloqueada.");
        if (token.IsCancellationRequested || !Current(stillCurrent))
        {
            MarkSafely(journal, choice, DriverUpdatePilotState.DownloadedOnly);
            return new(DriverUpdatePilotState.DownloadedOnly,
                "Cancelado após registrar intenção, antes de instalar; conciliação necessária.");
        }

        // Não cancelar a chamada síncrona de Install: erro/timeout pode
        // esconder efeitos em andamento. Só reconciliar, nunca repetir às cegas.
        Notify(progress, "Instalando atualização selecionada; não desligar o Windows…");
        DriverUpdatePilotStep installed;
        try { installed = await backend.Install(choice); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            MarkSafely(journal, choice, DriverUpdatePilotState.InstallationUncertain);
            return new(DriverUpdatePilotState.InstallationUncertain,
                "Instalação pode ter produzido efeito; conciliar sem repetir.");
        }
        if (!installed.Success)
        {
            MarkSafely(journal, choice, DriverUpdatePilotState.InstallationUncertain);
            return new(DriverUpdatePilotState.InstallationUncertain,
                "Windows não confirmou instalação; nenhuma repetição automática.");
        }
        if (installed.RebootRequired)
        {
            MarkSafely(journal, choice, DriverUpdatePilotState.PendingReboot);
            return new(DriverUpdatePilotState.PendingReboot,
                "Reinício necessário; GF não reiniciou o computador.");
        }
        Notify(progress, "Conferindo o mesmo dispositivo no inventário PnP…");
        DriverUpdatePilotObservation observed;
        try { observed = await backend.Observe(choice); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            MarkSafely(journal, choice, DriverUpdatePilotState.VerificationUncertain);
            return new(DriverUpdatePilotState.VerificationUncertain,
                "Instalação nativa terminou, mas verificação PnP ficou inconclusiva.");
        }
        var state = observed.RebootRequired ? DriverUpdatePilotState.PendingReboot :
            observed.SameDevice && observed.InfOrVersionChanged && observed.Healthy
                ? DriverUpdatePilotState.Verified : DriverUpdatePilotState.VerificationUncertain;
        if (!MarkSafely(journal, choice, state))
            return new(DriverUpdatePilotState.VerificationUncertain,
                "Falha ao persistir conclusão; resultado requer conciliação.");
        Notify(progress, "Resultado da atualização registrado: " + state);
        return new(state, state == DriverUpdatePilotState.Verified
            ? "Instalação concluída e mesma instância PnP verificada."
            : "Resultado inconclusivo ou reinício pendente; não declarar sucesso.");
    }
}
