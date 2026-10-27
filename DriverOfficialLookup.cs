using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal sealed record DriverOfficialOffer(string UpdateId, int Revision, string Title,
    string HardwareId, string Provider, string Model, DateTime? VersionDate);
internal sealed record DriverOfficialMatch(DriverOfficialOffer Offer, string DeviceId, string DeviceName)
{
    internal bool CanInstall => false;
    internal string Label => "Oferta do Windows Update com ID de hardware exato; compatibilidade de SO/arquitetura relatada pelo serviço. Pacote não verificado.";
}
internal sealed record DriverOfficialResult(IReadOnlyList<DriverOfficialMatch> Matches,
    int Offered, int Unmatched, bool Partial, string Status);

// WUA exclusivo de LEITURA: não cria downloader/installer, não aceita EULA,
// não registra serviços e não altera política de atualização do computador.
internal static class DriverOfficialLookup
{
    internal const string Criteria = "IsInstalled=0 and IsHidden=0 and Type='Driver'";
    internal static bool ValidHardwareId(string? id) => DriverInventory.ValidHardwareId(id);

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
                !ValidHardwareId(offer.HardwareId) || string.IsNullOrWhiteSpace(offer.Provider) ||
                string.IsNullOrWhiteSpace(offer.Model) || string.IsNullOrWhiteSpace(offer.Title) ||
                offer.Title.Length > 2000 || !offer.VersionDate.HasValue || offer.VersionDate.Value == DateTime.MinValue ||
                group.Distinct().Count() != 1)
            { unmatched += group.Count(); partial = true; continue; }
            var exact = devices.Where(d => d.DeviceId.Length > 0 && d.Present == true &&
                d.HardwareIds.Any(h => ValidHardwareId(h) && h.Equals(offer.HardwareId, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (exact.Length == 0) { unmatched += group.Count(); continue; }
            foreach (var device in exact) matched.Add(new(offer, device.DeviceId, device.DeviceName));
        }
        if (partial) return new([], offers.Count, offers.Count, true,
            "Metadados oficiais incompletos ou conflitantes; nenhuma correspondência publicada.");
        return new(matched, offers.Count, unmatched, false,
            $"Windows Update: {offers.Count} ofertas; {matched.Count} vínculos por ID exato; {unmatched} sem vínculo aprovado. Somente consulta; sem comparação numérica de versões.");
    }

    private static bool SameDeviceEvidence(DriverInventoryEntry left, DriverInventoryEntry right) =>
        left.InfName.Equals(right.InfName, StringComparison.OrdinalIgnoreCase) &&
        left.Provider.Equals(right.Provider, StringComparison.OrdinalIgnoreCase) &&
        left.Version.Equals(right.Version, StringComparison.Ordinal) &&
        left.Signed == right.Signed && left.Present == right.Present &&
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
                    offers.Add(new((string)((dynamic)identity).UpdateID, (int)((dynamic)identity).RevisionNumber,
                        (string)((dynamic)entry).Title, (string)((dynamic)entry).DriverHardwareID,
                        (string)((dynamic)entry).DriverProvider, (string)((dynamic)entry).DriverModel,
                        (DateTime)((dynamic)entry).DriverVerDate));
                }
                finally { Release(identity); Release(entry); }
            }
            return Match(inventory, offers, true);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or UnauthorizedAccessException or NotSupportedException)
        { return new([], 0, 0, true, $"Consulta oficial indisponível (0x{ex.HResult:X8}); não significa ausência de atualizações."); }
        finally { Release(updates); Release(result); Release(searcher); Release(session); }
    }
}
