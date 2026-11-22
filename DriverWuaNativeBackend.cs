using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// Implementação nativa G3.2: o coordenador exige aprovação física,
 // prova concreta de backup OEM, diário e dupla revalidação.
 // Fixtures não invocam COM ou operação de hardware.
internal sealed class DriverWuaNativeBackend : IDriverUpdatePilotBackend
{
    private static int activeSta;
    private static readonly DriverUpdatePilotCheck Denied =
        new(false, false, false, false, false, false, false, false, false);

    // Um unico trabalho WUA COM por processo, STA. Cancelar o await nao
    // cancela Search/Download/Install nativos sincronicos em andamento.
    private static Task<T> StartSta<T>(Func<T> action)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromException<T>(new PlatformNotSupportedException());
        if (Interlocked.CompareExchange(ref activeSta, 1, 0) != 0)
            return Task.FromException<T>(new InvalidOperationException(
                "WUA anterior ainda ocupado; nenhuma operacao paralela."));
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            T? value = default;
            Exception? failure = null;
            try { value = action(); }
            catch (Exception ex) { failure = ex; }
            finally { Interlocked.Exchange(ref activeSta, 0); }
            // Só sinalizar o resultado APÓS liberar a exclusividade.
            if (failure is not null) done.TrySetException(failure);
            else done.TrySetResult(value!);
        }) { IsBackground = true, Name = "GF WUA autorizado G3.2" };
        try
        {
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        catch
        {
            Interlocked.Exchange(ref activeSta, 0);
            throw;
        }
        return done.Task;
    }

    public async Task<DriverUpdatePilotCheck> Revalidate(
        DriverUpdatePilotChoice choice, CancellationToken token)
    {
        try { return await StartSta(() => ReadNative(choice, token)); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return Denied; }
    }

    public async Task<DriverUpdatePilotStep> Download(
        DriverUpdatePilotChoice choice, CancellationToken token)
    {
        try
        {
            return await StartSta(() =>
            {
                token.ThrowIfCancellationRequested();
                if (!DriverUpdatePilot.AllowedClass(choice.DriverClass))
                    return new DriverUpdatePilotStep(false, false);
                using var view = NativeView.Open(choice, token);
                if (!GetCheck(view, choice, token).BeforeDownload)
                    return new DriverUpdatePilotStep(false, false);
                token.ThrowIfCancellationRequested();
                var downloader = view.Keep(((dynamic)view.Session).CreateUpdateDownloader());
                ((dynamic)downloader).ClientApplicationID = "GerenciadorFinal.DriverPilot";
                ((dynamic)downloader).IsForced = false;
                ((dynamic)downloader).Updates = view.Collection;
                var result = view.Keep(((dynamic)downloader).Download());
                var item = view.Keep(((dynamic)result).GetUpdateResult(0));
                return new DriverUpdatePilotStep(
                    (int)((dynamic)result).ResultCode == 2 &&
                    (int)((dynamic)item).ResultCode == 2 &&
                    (int)((dynamic)item).HResult == 0, false);
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(false, false);
        }
    }

    // Instalacao exige journal previamente duravel no coordenador. Nesta
    // implementacao nao existe instalacao em lote, forca, EULA automatica,
    // rollback/desinstalacao, staging manual nem reboot disparado pelo GF.
    public Task<DriverUpdatePilotStep> Install(DriverUpdatePilotChoice choice) =>
        StartSta(() =>
        {
            if (!DriverUpdatePilot.AllowedClass(choice.DriverClass))
                return new DriverUpdatePilotStep(false, false);
            using var view = NativeView.Open(choice, CancellationToken.None);
            if (!GetCheck(view, choice, CancellationToken.None).BeforeInstall)
                return new DriverUpdatePilotStep(false, false);
            ((dynamic)view.Installer).IsForced = false;
            ((dynamic)view.Installer).AllowSourcePrompts = false;
            ((dynamic)view.Installer).ClientApplicationID = "GerenciadorFinal.DriverPilot";
            var result = view.Keep(((dynamic)view.Installer).Install());
            var item = view.Keep(((dynamic)result).GetUpdateResult(0));
            bool success = (int)((dynamic)result).ResultCode == 2 &&
                (int)((dynamic)item).ResultCode == 2 &&
                (int)((dynamic)item).HResult == 0;
            bool reboot = (bool)((dynamic)result).RebootRequired ||
                (bool)((dynamic)item).RebootRequired;
            return new DriverUpdatePilotStep(success, reboot);
        });

    public Task<DriverUpdatePilotObservation> Observe(DriverUpdatePilotChoice choice) =>
        StartSta(() =>
        {
            var inventory = DriverInventory.Capture(CancellationToken.None);
            var items = inventory.Items.Where(x =>
                string.Equals(x.DeviceId, choice.DeviceId,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
            if (inventory.Partial || items.Length != 1)
                return new DriverUpdatePilotObservation(false, false, false, false);
            var item = items[0];
            bool changed = !string.Equals(item.Version, choice.InstalledVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(item.InfName, choice.InfName,
                    StringComparison.OrdinalIgnoreCase);
            return new DriverUpdatePilotObservation(true, changed,
                item.Present == true && item.Signed == true &&
                !item.WithoutSignedDriverAssociation && item.PnpProblemCode is null or 0,
                false);
        });

    private static DriverUpdatePilotCheck ReadNative(
        DriverUpdatePilotChoice choice, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!DriverUpdatePilot.AllowedClass(choice.DriverClass))
            return Denied;
        using var view = NativeView.Open(choice, token);
        return GetCheck(view, choice, token);
    }

    private static DriverUpdatePilotCheck GetCheck(NativeView view,
        DriverUpdatePilotChoice choice, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var inventory = DriverInventory.Capture(token);
        var items = inventory.Items.Where(d =>
            string.Equals(d.DeviceId, choice.DeviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
        // G3.2: a mesma oferta não pode corresponder a outra instância
        // presente, nem a dispositivo com código PnP desconhecido/problemático.
        bool uniqueHardware = DriverInventory.HasExclusiveHardwareId(inventory, choice.HardwareId);
        bool sameDevice = !inventory.Partial && items.Length == 1 && uniqueHardware &&
            items[0].Present == true && items[0].Signed == true &&
            items[0].PnpProblemCode == 0 &&
            string.Equals(items[0].ReportedDriverClass, choice.DriverClass,
                StringComparison.OrdinalIgnoreCase) &&
            !items[0].WithoutSignedDriverAssociation &&
            string.Equals(items[0].Provider, choice.Provider, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(items[0].InfName, choice.InfName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(items[0].Version, choice.InstalledVersion, StringComparison.Ordinal) &&
            items[0].HardwareIds.Contains(choice.HardwareId, StringComparer.OrdinalIgnoreCase);
        return new(
            true, view.ExactIdentity, sameDevice, view.Applicable,
            view.NoAdditionalUpdates, view.EulaAccepted,
            view.NonInteractive, view.NoPendingReboot, view.Downloaded);
    }

    private sealed class NativeView : IDisposable
    {
        private readonly List<object> owned = [];
        private readonly HashSet<object> tracked = new(ReferenceEqualityComparer.Instance);
        internal object Session { get; private set; } = null!;
        internal object Collection { get; private set; } = null!;
        internal object Installer { get; private set; } = null!;
        internal bool ExactIdentity { get; private set; }
        internal bool Applicable { get; private set; }
        internal bool NoAdditionalUpdates { get; private set; }
        internal bool EulaAccepted { get; private set; }
        internal bool NonInteractive { get; private set; }
        internal bool NoPendingReboot { get; private set; }
        internal bool Downloaded { get; private set; }

        internal object Keep(object? value)
        {
            if (value is null) throw new InvalidOperationException("WUA retornou referencia nula.");
            if (tracked.Add(value)) owned.Add(value);
            return value;
        }

        internal static NativeView Open(DriverUpdatePilotChoice choice,
            CancellationToken token)
        {
            var view = new NativeView();
            try
            {
                token.ThrowIfCancellationRequested();
                var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                var collectionType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
                if (sessionType is null || collectionType is null)
                    throw new InvalidOperationException("WUA nao registrado.");
                view.Session = view.Keep(Activator.CreateInstance(sessionType));
                ((dynamic)view.Session).ClientApplicationID = "GerenciadorFinal.DriverPilot";
                var searcher = view.Keep(((dynamic)view.Session).CreateUpdateSearcher());
                ((dynamic)searcher).CanAutomaticallyUpgradeService = false;
                ((dynamic)searcher).ServerSelection = 2; // Windows Update oficial
                ((dynamic)searcher).Online = true;
                var result = view.Keep(((dynamic)searcher).Search(DriverOfficialLookup.Criteria));
                token.ThrowIfCancellationRequested();
                if ((int)((dynamic)result).ResultCode != 2)
                    throw new InvalidOperationException("Pesquisa WUA parcial ou falha.");
                var updates = view.Keep(((dynamic)result).Updates);
                int count = (int)((dynamic)updates).Count;
                if (count is < 1 or > 512)
                    throw new InvalidOperationException("Colecao WUA vazia ou excessiva.");
                object? selected = null;
                for (int i = 0; i < count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var update = view.Keep(((dynamic)updates).Item(i));
                    var identity = view.Keep(((dynamic)update).Identity);
                    if (string.Equals((string)((dynamic)identity).UpdateID,
                            choice.UpdateId, StringComparison.OrdinalIgnoreCase) &&
                        (int)((dynamic)identity).RevisionNumber == choice.Revision)
                    {
                        if (selected is not null)
                            throw new InvalidOperationException("Oferta WUA duplicada.");
                        selected = update;
                    }
                }
                if (selected is null)
                    throw new InvalidOperationException("Oferta original nao e mais aplicavel.");
                dynamic u = selected;
                view.ExactIdentity =
                    (int)u.Type == 2 && !((bool)u.IsInstalled) && !((bool)u.IsHidden) &&
                    !((bool)u.IsBeta) &&
                    (int)u.DeploymentAction == 1 && !((bool)u.CanRequireSource) &&
                    string.Equals((string)u.DriverHardwareID, choice.HardwareId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)u.DriverProvider, choice.Provider,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)u.DriverClass, choice.DriverClass,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)u.Title, choice.Title,
                        StringComparison.Ordinal);
                // Recusar pacote com varias entradas, mesmo UpdateID correto.
                var entries = view.Keep(u.WindowsDriverUpdateEntries);
                int countEntries = (int)((dynamic)entries).Count;
                bool oneMatchingEntry = false;
                if (countEntries == 1)
                {
                    var entry = view.Keep(((dynamic)entries).Item(0));
                    oneMatchingEntry = DriverWuaF4Gates.EntryMatches(choice,
                        (string)((dynamic)entry).DriverHardwareID,
                        (string)((dynamic)entry).DriverProvider,
                        (string)((dynamic)entry).DriverClass);
                }
                view.ExactIdentity &= DriverWuaF4Gates.OneEntry(countEntries) && oneMatchingEntry;
                view.Applicable = view.ExactIdentity;
                view.EulaAccepted = (bool)u.EulaAccepted;
                view.Downloaded = (bool)u.IsDownloaded;
                var bundled = view.Keep(u.BundledUpdates);
                view.NoAdditionalUpdates = (int)((dynamic)bundled).Count == 0;
                var behavior = view.Keep(u.InstallationBehavior);
                view.NonInteractive = !((bool)((dynamic)behavior).CanRequestUserInput);
                view.Collection = view.Keep(Activator.CreateInstance(collectionType));
                _ = (int)((dynamic)view.Collection).Add(selected);
                if ((int)((dynamic)view.Collection).Count != 1)
                    throw new InvalidOperationException("WUA nao selecionou exatamente um update.");
                view.Installer = view.Keep(((dynamic)view.Session).CreateUpdateInstaller());
                ((dynamic)view.Installer).Updates = view.Collection;
                view.NoPendingReboot =
                    !((bool)((dynamic)view.Installer).IsBusy) &&
                    !((bool)((dynamic)view.Installer).RebootRequiredBeforeInstallation);
                return view;
            }
            catch
            {
                view.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            bool releaseFailed = false;
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (Marshal.IsComObject(owned[i]))
                        Marshal.FinalReleaseComObject(owned[i]);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                { releaseFailed = true; }
            }
            owned.Clear();
            tracked.Clear();
            if (releaseFailed)
                throw new InvalidOperationException(
                    "WUA: falha ao liberar objeto COM; conclusão não confiável.");
        }
    }
}
