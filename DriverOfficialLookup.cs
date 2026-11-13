using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal sealed record DriverOfficialOffer(string UpdateId, int Revision, string Title,
    string HardwareId, string Provider, string Model, DateTime? VersionDate)
{
    // Campos informativos do IWindowsDriverUpdate; não representam versão numérica.
    internal string DriverClass { get; init; } = "";
    internal string Manufacturer { get; init; } = "";
    internal DriverOfficialEntryAudit? EntryAudit { get; init; }
    internal DriverOfficialRelationsAudit? Relations { get; init; }
}

// Apenas metadados da oferta WUA: supersedência entre UpdateIDs e categorias.
// Não há prova de classificação/ranking do pacote, versão ou elegibilidade.
internal sealed record DriverOfficialCategory(string CategoryId, string Type, string Name);
internal enum DriverOfficialRelationsState { Unavailable, Complete, Inconclusive }
internal sealed record DriverOfficialRelationsAudit(DriverOfficialRelationsState State,
    IReadOnlyList<string> SupersededUpdateIds, IReadOnlyList<DriverOfficialCategory> Categories)
{
    internal bool DriverRankProven => false;
    internal bool DriverVersionProven => false;
    internal bool PackageCompatibilityProven => false;
}

// As entradas WUA pertencem à oferta pai (UpdateID/revisão), mas não
// expõem DriverVer numérico, TargetOSVersion, assinatura ou bytes do INF.
internal sealed record DriverOfficialEntryEvidence(
    string HardwareOrCompatibleId, string Provider, string Model,
    string DriverClass, string Manufacturer, DateTime VersionDate,
    int DeviceStatus, int DeviceProblemNumber)
{
    internal string? OfferedNumericVersion => null;
    internal bool PackageCompatibilityProven => false;
}
internal enum DriverOfficialEntryState { Unavailable, Complete, Inconclusive }
internal sealed record DriverOfficialEntryAudit(DriverOfficialEntryState State,
    IReadOnlyList<DriverOfficialEntryEvidence> Entries)
{
    internal bool VerifiedRemotePackage => false;
    internal bool VerifiedTargetOsVersion => false;
    internal bool VerifiedRank => false;
}

// Prova contextual de onde ocorreu a busca. Não é um manifesto de compatibilidade
// nem vincula cada pacote remoto a uma arquitetura ou versão do Windows.
internal sealed record DriverOfficialSearchContext(
    string Source, bool Online, string HostOsVersion, string HostArchitecture,
    DateTimeOffset SearchedAtUtc)
{
    internal bool PackageOsArchitectureValidated => false;
}

// Snapshot vinculado à instância identificada no inventário corrente.
// Nunca confundir a versão instalada com a versão da oferta WUA.
internal sealed record DriverOfficialInstalledEvidence(
    string InfName, string Provider, string Version, bool? SignedReportedByWindows,
    DriverStoreState? StoreState, int? PnpProblemCode, bool WithoutSignedDriverAssociation);

internal sealed record DriverOfficialMatch(DriverOfficialOffer Offer, string DeviceId, string DeviceName)
{
    internal DriverOfficialInstalledEvidence? Installed { get; init; }
    internal DriverOfficialEntryEvidence? MatchingWuaEntry { get; init; }
    internal DriverOfficialRelationsAudit? Relations => Offer.Relations;
    internal bool ProviderMatchesInstalled => Installed is { WithoutSignedDriverAssociation: false } current &&
        current.Provider.Equals(Offer.Provider, StringComparison.OrdinalIgnoreCase);
    // INF DriverVer utiliza quatro componentes (cada um de 0 a 65534).
    // Normalização é SOMENTE da versão WMI instalada; a oferta WUA não a fornece.
    internal string? InstalledNumericVersion =>
        DriverOfficialLookup.NormalizeInstalledDriverVersion(Installed?.Version);
    internal string? OfferedNumericVersion => null;
    internal bool OfferedNumericVersionProven => false;
    internal bool TargetOsArchitectureProven => false;
    internal bool CompletePackageVerified => false;
    internal bool IsNewerProven => false;
    internal bool CanInstall => false;
    internal string Label =>
        "Oferta WUA vinculada por HardwareID exato à instância PnP. " +
        "Versão numérica da oferta, ranking, compatibilidade independente de SO/arquitetura e pacote completo não comprovados; não classificar como mais novo.";
}
internal sealed record DriverOfficialResult(IReadOnlyList<DriverOfficialMatch> Matches,
    int Offered, int Unmatched, bool Partial, string Status)
{
    internal DriverOfficialSearchContext? SearchContext { get; init; }
}

// WUA exclusivo de LEITURA: não cria downloader/installer, não aceita EULA,
// não registra serviços e não altera política de atualização do computador.
internal static class DriverOfficialLookup
{
    internal const string Criteria = "IsInstalled=0 and IsHidden=0 and Type='Driver'";
    internal const int MaxWuaEntriesPerOffer = 32;
    internal const int MaxWuaEntriesPerSearch = 512;
    internal const int MaxSupersededPerOffer = 32;
    internal const int MaxCategoriesPerOffer = 16;
    internal static bool ValidHardwareId(string? id) => DriverInventory.ValidHardwareId(id);

    internal static string? NormalizeInstalledDriverVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 47 ||
            version != version.Trim() || version.Any(c => !char.IsAsciiDigit(c) && c != '.'))
            return null;
        var components = version.Split('.');
        if (components.Length != 4 || components.Any(s => s.Length is < 1 or > 5 ||
                !ushort.TryParse(s, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var part) || part >= 65535))
            return null;
        return string.Join(".", components.Select(s => ushort.Parse(s,
            System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture)
            .ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    // Resultado completo e não mutável da busca WUA para o Windows atual.
    // Não demonstra requisitos nem compatibilidade independente do pacote.
    internal static DriverOfficialSearchContext HostContextForSuccessfulSearch(bool online) =>
        new("Windows Update", online, Environment.OSVersion.Version.ToString(),
            RuntimeInformation.OSArchitecture.ToString(), DateTimeOffset.UtcNow);

    // Campos do WUA são dados para apresentação; rejeitar controles que
    // poderiam simular linhas de diagnóstico no relatório local.
    private static bool SafeText(string? value, bool required, int maxLength) =>
        value is not null && value.Length <= maxLength && !value.Any(char.IsControl) &&
        (!required || !string.IsNullOrWhiteSpace(value));

    internal static DriverOfficialResult Match(DriverInventoryResult inventory,
        IReadOnlyList<DriverOfficialOffer> offers, bool successful)
    {
        if (!successful || inventory.Partial || inventory.Items.Count is 0 or > 4000 || offers.Count > 512)
            return new([], offers.Count, offers.Count, true,
                "Consulta ou inventário incompleto; nenhuma correspondência aprovada.");
        // Uma instância pode ter linhas WMI repetidas. Só reconciliar linhas
        // equivalentes; escolher a primeira ocultaria INF/hardware conflitante.
        var deviceGroups = inventory.Items.GroupBy(d => d.DeviceId.Trim(), StringComparer.OrdinalIgnoreCase).ToArray();
        if (deviceGroups.Any(g => g.Any(d => !SameDeviceEvidence(g.First(), d))))
            return new([], offers.Count, offers.Count, true,
                "Inventário com registros conflitantes para a mesma instância; correspondência inconclusiva. Nenhum vínculo publicado.");
        if (!RelationGraphValid(offers))
            return new([], offers.Count, offers.Count, true,
                "Relações WUA com ciclos, autossubstituição ou metadados contraditórios; nenhum vínculo aprovado.");
        var devices = deviceGroups.Select(g => g.First() with { DeviceId = g.Key }).ToArray();
        var matched = new List<DriverOfficialMatch>();
        int unmatched = 0;
        bool partial = false;
        // O mesmo ID/revisão com metadados divergentes não pode ser escolhido.
        var groups = offers.GroupBy(o => (o.UpdateId?.ToUpperInvariant(), o.Revision));
        foreach (var group in groups)
        {
            var offer = group.First();
            if (!Guid.TryParseExact(offer.UpdateId, "D", out var id) || id == Guid.Empty || offer.Revision < 1 ||
                !ValidHardwareId(offer.HardwareId) ||
                !SafeText(offer.Provider, true, 256) || !SafeText(offer.Model, true, 256) ||
                !SafeText(offer.Title, true, 2000) ||
                !SafeText(offer.DriverClass, false, 256) || !SafeText(offer.Manufacturer, false, 256) ||
                !offer.VersionDate.HasValue || offer.VersionDate.Value == DateTime.MinValue ||
                group.Distinct().Count() != 1 ||
                (offer.Relations is { State: DriverOfficialRelationsState.Inconclusive }) ||
                (offer.Relations is { State: DriverOfficialRelationsState.Complete } relations &&
                    AuditRelations(offer.UpdateId, relations.SupersededUpdateIds,
                        relations.Categories).State != DriverOfficialRelationsState.Complete) ||
                (offer.EntryAudit is { State: DriverOfficialEntryState.Inconclusive }) ||
                (offer.EntryAudit is { State: DriverOfficialEntryState.Complete } audit &&
                    (AuditEntries(audit.Entries).State != DriverOfficialEntryState.Complete ||
                     !audit.Entries.Any(e => e.HardwareOrCompatibleId.Equals(offer.HardwareId,
                         StringComparison.OrdinalIgnoreCase)))))
            { unmatched += group.Count(); partial = true; continue; }
            var exact = devices.Where(d => d.DeviceId.Length > 0 && d.Present == true &&
                d.HardwareIds.Any(h => ValidHardwareId(h) && h.Equals(offer.HardwareId, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (exact.Length == 0) { unmatched += group.Count(); continue; }
            if (exact.Any(d => !UsableInstalledEvidence(d)))
            {
                // Um vínculo incompleto não publica um subconjunto como consulta segura.
                partial = true;
                unmatched += group.Count();
                continue;
            }
            foreach (var device in exact)
                matched.Add(new(offer, device.DeviceId, device.DeviceName)
                {
                    Installed = new(device.InfName, device.Provider, device.Version, device.Signed,
                        device.PackageEvidence?.State, device.PnpProblemCode, device.WithoutSignedDriverAssociation),
                    MatchingWuaEntry = offer.EntryAudit is { State: DriverOfficialEntryState.Complete } entryAudit
                        ? entryAudit.Entries.First(e => e.HardwareOrCompatibleId.Equals(offer.HardwareId,
                            StringComparison.OrdinalIgnoreCase)) : null
                });
        }
        if (partial) return new([], offers.Count, offers.Count, true,
            "Metadados oficiais incompletos ou conflitantes; nenhuma correspondência publicada.");
        return new(matched, offers.Count, unmatched, false,
            $"Windows Update: {offers.Count} ofertas; {matched.Count} vínculos por ID exato; {unmatched} sem vínculo aprovado. Somente consulta; sem comparação numérica de versões.");
    }

    internal static DriverOfficialRelationsAudit AuditRelations(string updateId,
        IReadOnlyList<string> superseded, IReadOnlyList<DriverOfficialCategory> categories)
    {
        static DriverOfficialRelationsAudit Bad() =>
            new(DriverOfficialRelationsState.Inconclusive, [], []);
        if (!Guid.TryParseExact(updateId, "D", out var parent) || parent == Guid.Empty ||
            superseded is null || categories is null ||
            superseded.Count > MaxSupersededPerOffer || categories.Count > MaxCategoriesPerOffer)
            return Bad();
        var seenIds = new HashSet<Guid>();
        foreach (var id in superseded)
        {
            if (id is null || id.Length != 36 || !Guid.TryParseExact(id, "D", out var parsed) ||
                parsed == Guid.Empty || parsed == parent || !seenIds.Add(parsed))
                return Bad();
        }
        var seenCategories = new Dictionary<Guid, DriverOfficialCategory>();
        foreach (var cat in categories)
        {
            if (cat is null || cat.CategoryId is null || cat.CategoryId.Length != 36 ||
                !Guid.TryParseExact(cat.CategoryId, "D", out var id) || id == Guid.Empty ||
                !SafeText(cat.Type, true, 128) || !SafeText(cat.Name, true, 256))
                return Bad();
            if (seenCategories.TryGetValue(id, out var previous) &&
                (!previous.Type.Equals(cat.Type, StringComparison.OrdinalIgnoreCase) ||
                 !previous.Name.Equals(cat.Name, StringComparison.Ordinal)))
                return Bad();
            seenCategories[id] = cat;
        }
        return new(DriverOfficialRelationsState.Complete,
            seenIds.Select(id => id.ToString("D")).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            seenCategories.OrderBy(x => x.Key).Select(x => x.Value).ToArray());
    }

    // Só detecta ciclos entre UpdateIDs presentes NESTE resultado.
    // Referências externas não são declaradas ausentes nem visitadas remotamente.
    private static bool RelationGraphValid(IReadOnlyList<DriverOfficialOffer> offers)
    {
        var map = offers.Where(o => o.Relations is { State: DriverOfficialRelationsState.Complete } &&
                                   Guid.TryParseExact(o.UpdateId, "D", out _))
            .GroupBy(o => o.UpdateId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key,
                g => g.SelectMany(o => o.Relations!.SupersededUpdateIds)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        bool Visit(string id)
        {
            state[id] = 1;
            foreach (var next in map[id])
            {
                if (!map.ContainsKey(next)) continue;
                if (state.TryGetValue(next, out int s))
                {
                    if (s == 1) return false;
                    if (s == 2) continue;
                }
                if (!Visit(next)) return false;
            }
            state[id] = 2;
            return true;
        }
        return map.Keys.All(id => state.ContainsKey(id) || Visit(id));
    }

    // Consulta limitada de metadados IUpdate, sem downloads ou mutações.
    // Se qualquer propriedade estiver incompleta, não publicar subconjunto.
    private static DriverOfficialRelationsAudit CaptureRelations(
        object update, string updateId, CancellationToken token)
    {
        object? superseded = null, categories = null;
        bool started = false;
        static void Release(object? value)
        { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
        try
        {
            token.ThrowIfCancellationRequested();
            superseded = ((dynamic)update).SupersededUpdateIDs;
            started = true;
            int count = (int)((dynamic)superseded!).Count;
            if (count is < 0 or > MaxSupersededPerOffer)
                return new(DriverOfficialRelationsState.Inconclusive, [], []);
            var ids = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                ids.Add((string)((dynamic)superseded!).Item(i));
            }
            categories = ((dynamic)update).Categories;
            int catCount = (int)((dynamic)categories!).Count;
            if (catCount is < 0 or > MaxCategoriesPerOffer)
                return new(DriverOfficialRelationsState.Inconclusive, [], []);
            var list = new List<DriverOfficialCategory>(catCount);
            for (int i = 0; i < catCount; i++)
            {
                token.ThrowIfCancellationRequested();
                object? category = null;
                try
                {
                    category = ((dynamic)categories!).Item(i);
                    list.Add(new(
                        (string)((dynamic)category!).CategoryID,
                        (string)((dynamic)category!).Type,
                        (string)((dynamic)category!).Name));
                }
                finally { Release(category); }
            }
            return AuditRelations(updateId, ids, list);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is COMException or InvalidCastException or
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or UnauthorizedAccessException or
            ArgumentException or NullReferenceException or OverflowException or NotSupportedException)
        {
            return new(started ? DriverOfficialRelationsState.Inconclusive :
                DriverOfficialRelationsState.Unavailable, [], []);
        }
        finally { Release(categories); Release(superseded); }
    }

    // Rejeita entradas malformadas, chaves conflitantes e resultados truncados.
    // Nem mesmo uma coleção completa autoriza instalação/compatibilidade.
    internal static DriverOfficialEntryAudit AuditEntries(
        IReadOnlyList<DriverOfficialEntryEvidence> entries)
    {
        if (entries is null || entries.Count is < 1 or > MaxWuaEntriesPerOffer)
            return new(DriverOfficialEntryState.Inconclusive, []);
        if (entries.Any(e => e is null || !ValidHardwareId(e.HardwareOrCompatibleId) ||
                !SafeText(e.Provider, true, 256) || !SafeText(e.Model, true, 256) ||
                !SafeText(e.DriverClass, false, 256) || !SafeText(e.Manufacturer, false, 256) ||
                e.VersionDate == DateTime.MinValue || e.DeviceProblemNumber is < 0 or > 9999))
            return new(DriverOfficialEntryState.Inconclusive, []);
        var groups = entries.GroupBy(x => x.HardwareOrCompatibleId, StringComparer.OrdinalIgnoreCase);
        if (groups.Any(g => g.Distinct().Count() != 1))
            return new(DriverOfficialEntryState.Inconclusive, []);
        return new(DriverOfficialEntryState.Complete, entries.ToArray());
    }

    // Somente IWindowsDriverUpdate4.get_WindowsDriverUpdateEntries, Count, Item e
    // propriedades de leitura da entrada. Proibição de downloader/installer.
    // Getter ausente: Unavailable. Coleção iniciada e incompleta: Inconclusive.
    private static DriverOfficialEntryAudit CaptureEntries(object update, CancellationToken token)
    {
        object? collection = null;
        bool enumerating = false;
        static void Release(object? value)
        {
            if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }
        try
        {
            token.ThrowIfCancellationRequested();
            collection = ((dynamic)update).WindowsDriverUpdateEntries;
            enumerating = true;
            int count = (int)((dynamic)collection!).Count;
            if (count is < 1 or > MaxWuaEntriesPerOffer)
                return new(DriverOfficialEntryState.Inconclusive, []);
            var entries = new List<DriverOfficialEntryEvidence>(count);
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                object? item = null;
                try
                {
                    item = ((dynamic)collection!).Item(i);
                    entries.Add(new(
                        (string)((dynamic)item!).DriverHardwareID,
                        (string)((dynamic)item!).DriverProvider,
                        (string)((dynamic)item!).DriverModel,
                        (string)((dynamic)item!).DriverClass,
                        (string)((dynamic)item!).DriverManufacturer,
                        (DateTime)((dynamic)item!).DriverVerDate,
                        (int)((dynamic)item!).DeviceStatus,
                        (int)((dynamic)item!).DeviceProblemNumber));
                }
                finally { Release(item); }
            }
            return AuditEntries(entries);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is COMException or InvalidCastException or
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or UnauthorizedAccessException or
            ArgumentException or NullReferenceException or OverflowException or NotSupportedException)
        {
            return new(enumerating ? DriverOfficialEntryState.Inconclusive :
                DriverOfficialEntryState.Unavailable, []);
        }
        finally { Release(collection); }
    }

    private static bool UsableInstalledEvidence(DriverInventoryEntry device)
    {
        if (device.WithoutSignedDriverAssociation)
            // Código 28 é um relato do Windows, não prova de qual INF instalar.
            return device.PnpProblemCode == 28 && string.IsNullOrWhiteSpace(device.InfName) &&
                string.IsNullOrWhiteSpace(device.Version) &&
                string.IsNullOrWhiteSpace(device.Provider) && device.Signed is null;
        return device.InfName is not null && DriverPackageLinks.IsValidInf(device.InfName) &&
            SafeText(device.Provider, true, 256) &&
            Version.TryParse(device.Version, out _) &&
            (device.PackageEvidence is null ||
                (device.PackageEvidence.State == DriverStoreState.Mapped &&
                 device.PackageEvidence.InfName.Equals(device.InfName, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool SameDeviceEvidence(DriverInventoryEntry left, DriverInventoryEntry right) =>
        left.InfName.Equals(right.InfName, StringComparison.OrdinalIgnoreCase) &&
        left.Provider.Equals(right.Provider, StringComparison.OrdinalIgnoreCase) &&
        left.Version.Equals(right.Version, StringComparison.Ordinal) &&
        left.Signed == right.Signed && left.Present == right.Present &&
        left.PnpProblemCode == right.PnpProblemCode &&
        left.WithoutSignedDriverAssociation == right.WithoutSignedDriverAssociation &&
        left.PackageEvidence?.State == right.PackageEvidence?.State &&
        string.Equals(left.PackageEvidence?.InfName, right.PackageEvidence?.InfName, StringComparison.OrdinalIgnoreCase) &&
        left.HardwareIds.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(right.HardwareIds);

    internal static Task<DriverOfficialResult> Start(DriverInventoryResult inventory, CancellationToken token, bool online = true)
    {
        var completion = new TaskCompletionSource<DriverOfficialResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.TrySetResult(Capture(inventory, token, online)); }
            catch (OperationCanceledException) { completion.TrySetCanceled(token); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }) { IsBackground = true, Name = "GF Drivers somente leitura" };
        if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    internal static DriverOfficialResult Capture(DriverInventoryResult inventory, CancellationToken token, bool online = true)
    {
        if (!OperatingSystem.IsWindows() || inventory.Partial || inventory.Items.Count == 0)
            return Match(inventory, [], false);
        object? session = null, searcher = null, result = null, updates = null;
        static void Release(object? value)
        { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
        try
        {
            token.ThrowIfCancellationRequested();
            var type = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (type is null) return Match(inventory, [], false);
            session = Activator.CreateInstance(type)!;
            ((dynamic)session).ClientApplicationID = "GerenciadorFinal.DriverReadOnly";
            searcher = ((dynamic)session).CreateUpdateSearcher();
            // A seleção é apenas desta consulta, nunca muda a configuração do
            // Windows. Falhas/bloqueios de política são preservados, sem fallback.
            ((dynamic)searcher).CanAutomaticallyUpgradeService = false;
            ((dynamic)searcher).ServerSelection = 2; // ssWindowsUpdate, fonte Microsoft.
            ((dynamic)searcher).Online = online;
            result = ((dynamic)searcher).Search(Criteria);
            token.ThrowIfCancellationRequested();
            // orcSucceededWithErrors nunca vira consulta completa/vazia.
            if ((int)((dynamic)result).ResultCode != 2) return Match(inventory, [], false);
            updates = ((dynamic)result).Updates;
            int count = (int)((dynamic)updates).Count;
            if (count is < 0 or > 512) return Match(inventory, [], false);
            var offers = new List<DriverOfficialOffer>();
            int observedEntries = 0;
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                object? entry = null, identity = null;
                try
                {
                    entry = ((dynamic)updates).Item(i);
                    if ((int)((dynamic)entry).Type != 2 || (bool)((dynamic)entry).IsInstalled || (bool)((dynamic)entry).IsHidden)
                        return Match(inventory, offers, false);
                    identity = ((dynamic)entry).Identity;
                    string updateId = (string)((dynamic)identity).UpdateID;
                    int revision = (int)((dynamic)identity).RevisionNumber;
                    var relations = CaptureRelations(entry!, updateId, token);
                    var entryAudit = CaptureEntries(entry!, token);
                    if (entryAudit.State == DriverOfficialEntryState.Complete)
                    {
                        observedEntries += entryAudit.Entries.Count;
                        if (observedEntries > MaxWuaEntriesPerSearch)
                            return Match(inventory, offers, false);
                    }
                    offers.Add(new(updateId, revision,
                        (string)((dynamic)entry).Title, (string)((dynamic)entry).DriverHardwareID,
                        (string)((dynamic)entry).DriverProvider, (string)((dynamic)entry).DriverModel,
                        (DateTime)((dynamic)entry).DriverVerDate)
                    {
                        DriverClass = (string)((dynamic)entry).DriverClass,
                        Manufacturer = (string)((dynamic)entry).DriverManufacturer,
                        EntryAudit = entryAudit,
                        Relations = relations
                    });
                }
                finally { Release(identity); Release(entry); }
            }
            return Match(inventory, offers, true) with
            {
                SearchContext = HostContextForSuccessfulSearch(online)
            };
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or UnauthorizedAccessException or NotSupportedException)
        { return new([], 0, 0, true, $"Consulta oficial indisponível (0x{ex.HResult:X8}); não significa ausência de atualizações."); }
        finally { Release(updates); Release(result); Release(searcher); Release(session); }
    }
}
