using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Backup OEM independente da atualização WUA. A restauração só adiciona
// UM INF verificado ao Driver Store; nunca instala no dispositivo ou reinicia.
// Nenhum método é invocado automaticamente ao abrir o aplicativo.
internal sealed record DriverBackupReceipt(int Schema, string PublishedInf, string OriginalInf,
    string InfSha256, string PackageSha256, string DriverClass, DateTimeOffset CreatedAtUtc);
internal sealed record DriverBackupOutcome(bool Saved, string Message, string? Folder = null);
internal sealed record DriverRestoreReview(bool Verified, bool CanStage, string Message,
    string? InfPath = null, DriverBackupReceipt? Receipt = null);
internal sealed record DriverRestoreOutcome(bool Attempted, bool RequiresReconciliation, string Message);
internal enum DriverRestoreReconcileState { NoRequest, Inconclusive, MatchingInfObserved }
internal sealed record DriverRestoreReconciliation(DriverRestoreReconcileState State,
    string Message, string? PublishedInf = null)
{
    // Nunca confirma vinculação PnP, instalação num dispositivo ou rollback.
    internal bool DeviceRestored => false;
    internal bool MayClearPendingAutomatically => false;
}

internal enum DriverBackupCatalogState { Valid, ProtectedClass, PendingReconciliation, Inconclusive }
internal sealed record DriverBackupCatalogItem(string Folder, DriverBackupCatalogState State,
    string Message, string PublishedInf = "", string DriverClass = "",
    DateTimeOffset CreatedAtUtc = default)
{
    // A listagem é somente informativa, nunca uma permissão de execução.
    internal bool CanExecute => false;
}
internal sealed record DriverBackupCatalogSnapshot(
    IReadOnlyList<DriverBackupCatalogItem> Items, bool Partial, int Inspected)
{
    internal int VerifiedCount => Items.Count(item => item.State is
        DriverBackupCatalogState.Valid or DriverBackupCatalogState.ProtectedClass or
        DriverBackupCatalogState.PendingReconciliation);
}

// Visão consultiva agregada para a futura camada de detalhes do GF.
// O cofre OEM e o diário WUA são fontes independentes: backup disponível
// não prova atualização instalada, e journal Verified não autoriza restauração.
internal sealed record DriverMaintenanceReadOnlyOverview(
    DriverWuaJournalSnapshot UpdateJournal,
    DriverBackupCatalogSnapshot Backups,
    bool OtherDriverOperationActive)
{
    internal bool NeedsSupervisedReview =>
        UpdateJournal.RequiresSupervisedReview ||
        Backups.Partial ||
        Backups.Items.Any(x => x.State is DriverBackupCatalogState.Inconclusive
            or DriverBackupCatalogState.PendingReconciliation) ||
        OtherDriverOperationActive;
    internal int PendingRecoveries => Backups.Items.Count(x =>
        x.State == DriverBackupCatalogState.PendingReconciliation);
    internal bool GrantsMaintenancePermission => false;
}

internal static class DriverRecoveryVault
{
    private static int active;

    [DllImport("setupapi.dll", EntryPoint = "SetupDiGetINFClassW",
        CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetINFClass(string infName, out Guid classGuid,
        StringBuilder className, int classNameSize, out int requiredSize);

    // Confirmar a classe real do INF assinado: o rótulo do recibo não
    // concede permissão para restaurar pacotes de classes críticas.
    internal static string ReadInfClass(string path)
    {
        if (!OperatingSystem.IsWindows()) return "";
        try
        {
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path) ||
                File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                return "";
            var buffer = new StringBuilder(64);
            return SetupDiGetINFClass(path, out _, buffer, buffer.Capacity, out _)
                ? DriverInventory.NormalizeDriverClass(buffer.ToString()) : "";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return ""; }
    }

    private static readonly Regex PublishedInf = new(@"^oem[0-9]{1,7}\.inf$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex InfFile = new(@"^[a-zA-Z0-9_.-]{1,180}\.inf$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static string Root => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "GerenciadorFinal", "Drivers", "Backups");

    private static bool Hash64(string? value) => value is { Length: 64 } &&
        value.All(Uri.IsHexDigit);

    private static bool ExistingParentsAreOrdinary(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            if (!Path.IsPathFullyQualified(full) || full.StartsWith(@"\\", StringComparison.Ordinal) ||
                full.StartsWith("//", StringComparison.Ordinal)) return false;
            for (DirectoryInfo? dir = new(full); dir is not null; dir = dir.Parent)
                if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    private static bool SafeChild(string root, string folder)
    {
        try
        {
            if (!Directory.Exists(root) || !Directory.Exists(folder) ||
                !ExistingParentsAreOrdinary(folder)) return false;
            string expected = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            string actual = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
            return string.Equals(Path.GetDirectoryName(actual), expected,
                    StringComparison.OrdinalIgnoreCase) &&
                DriverSilentUninstall.SafeBackupLocation(root, folder);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    internal static bool CanExport(DriverInventoryResult? inventory, DriverInventoryEntry? selected)
    {
        if (inventory is null || selected is null || inventory.Partial ||
            inventory.Items.Count is < 1 or > 4000 ||
            string.IsNullOrWhiteSpace(selected.DeviceId) ||
            selected.Signed != true || selected.WithoutSignedDriverAssociation ||
            !PublishedInf.IsMatch(selected.InfName ?? "") ||
            selected.PackageEvidence is not { State: DriverStoreState.Mapped } package ||
            !string.Equals(package.InfName, selected.InfName, StringComparison.OrdinalIgnoreCase) ||
            !InfFile.IsMatch(Path.GetFileName(package.StorePath)))
            return false;
        return inventory.Items.Count(item => string.Equals(item.DeviceId,
            selected.DeviceId, StringComparison.OrdinalIgnoreCase)) == 1 &&
            Equals(inventory.Items.Single(item => string.Equals(item.DeviceId,
                selected.DeviceId, StringComparison.OrdinalIgnoreCase)), selected);
    }

    internal static async Task<DriverBackupOutcome> ExportAsync(
        DriverInventoryResult snapshot, DriverInventoryEntry selected, bool confirmed,
        Func<bool> stillCurrent, CancellationToken token)
    {
        if (!confirmed || !CanExport(snapshot, selected) || token.IsCancellationRequested ||
            !stillCurrent() || !OperatingSystem.IsWindows() || !DriverSilentUninstall.IsElevated())
            return new(false, "Backup não autorizado ou inventário incompleto; nenhuma exportação.");
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
            return new(false, "Outra operação de recuperação ainda está ativa.");
        if (!DriverG32RecoveryTicket.TryBeginOperation(DriverG32OperationKind.Export))
        {
            Interlocked.Exchange(ref active, 0);
            return new(false, "Atualização WUA ou recuperação já está em andamento.");
        }
        string? folder = null;
        try
        {
            // Revalidar a identidade antes de exportar; nunca confiar só no cartão.
            var fresh = await Task.Run(() => DriverStoreAudit.CaptureWithPackages(token), token);
            var rows = fresh.Items.Where(item => string.Equals(item.DeviceId,
                selected.DeviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (rows.Length != 1 || !CanExport(fresh, rows[0]) ||
                !string.Equals(rows[0].InfName, selected.InfName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(rows[0].Version, selected.Version, StringComparison.Ordinal) ||
                !string.Equals(rows[0].Provider, selected.Provider, StringComparison.OrdinalIgnoreCase) ||
                !stillCurrent() || token.IsCancellationRequested)
                return new(false, "Vínculo OEM mudou na reconsulta; backup não iniciado.");
            var mapped = rows[0].PackageEvidence!;
            var signature = await Task.Run(() => DriverInfVerification.Verify(mapped, token), token);
            if (signature.Trust != DriverInfTrust.VerifiedInf || !Hash64(signature.Sha256))
                return new(false, "Assinatura/INF original não verificado pelo Windows.");
            if (!ExistingParentsAreOrdinary(Root) || !stillCurrent() || token.IsCancellationRequested)
                return new(false, "Destino redirecionado ou seleção revogada; nada exportado.");
            Directory.CreateDirectory(Root);
            folder = Path.Combine(Root, "manual-" + DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ") +
                "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            if (!SafeChild(Root, folder))
                return new(false, "Pasta de backup não confiável.", folder);
            int exit = await DriverSilentUninstall.RunPnPUtil(
                ["/export-driver", selected.InfName, folder], TimeSpan.FromMinutes(2));
            string original = Path.GetFileName(mapped.StorePath);
            if (exit != 0 || !SafeChild(Root, folder) ||
                !DriverSilentUninstall.VerifyBackup(folder, original, signature.Sha256,
                    out string packageHash) || token.IsCancellationRequested || !stillCurrent())
                return new(false, "Exportação não comprovada; arquivos preservados para inspeção.", folder);
            var receipt = new DriverBackupReceipt(1, selected.InfName, original,
                signature.Sha256, packageHash, selected.ReportedDriverClass, DateTimeOffset.UtcNow);
            // Recibo separado: não altera o hash do conjunto exportado.
            string receiptPath = folder + ".backup.json";
            await using (var output = new FileStream(receiptPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(receipt);
                await output.WriteAsync(bytes, CancellationToken.None);
                output.Flush(flushToDisk: true);
            }
            return new(true, "Backup OEM exportado e conferido; nenhum driver foi modificado.", folder);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(false, "Backup inconclusivo (" + ex.GetType().Name +
                "); não remover arquivos nem repetir automaticamente.", folder);
        }
        finally { DriverG32RecoveryTicket.EndOperation(); Interlocked.Exchange(ref active, 0); }
    }

    private static string? FindSingleInf(string folder, string infName)
    {
        var queue = new Queue<string>();
        queue.Enqueue(folder);
        string? found = null;
        int entries = 0;
        while (queue.Count != 0)
        {
            string directory = queue.Dequeue();
            if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint)) return null;
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                if (++entries > 512) return null;
                var attrs = File.GetAttributes(path);
                if (attrs.HasFlag(FileAttributes.ReparsePoint)) return null;
                if (attrs.HasFlag(FileAttributes.Directory)) queue.Enqueue(path);
                else if (Path.GetFileName(path).Equals(infName, StringComparison.OrdinalIgnoreCase))
                {
                    if (found is not null) return null;
                    found = path;
                }
            }
        }
        return found;
    }

    // Sem COM, PnPUtil, elevação ou alteração do sistema. O consumo desta
    // fotografia nunca substitui as verificações de instante de execução.
    internal static DriverMaintenanceReadOnlyOverview CaptureOverviewReadOnly(
        int maximumBackups = 128) =>
        CaptureOverviewReadOnlyForTest(DriverUpdatePilotFileJournal.SuggestedPath,
            Root, DriverG32RecoveryTicket.HasActiveOperation, maximumBackups);

    internal static DriverMaintenanceReadOnlyOverview CaptureOverviewReadOnlyForTest(
        string journalPath, string backupRoot, bool driverOperationActive,
        int maximumBackups = 128) =>
        new(DriverUpdatePilotFileJournal.InspectReadOnly(journalPath),
            BrowseBackupsForTest(backupRoot, maximumBackups), driverOperationActive);

    // Catálogo consultivo: não enumera Driver Store, não cria pastas,
    // não remove arquivos e nunca oculta recibos pendentes de recuperação.
    internal static DriverBackupCatalogSnapshot BrowseBackups(int maximum = 128) =>
        BrowseBackupsForTest(Root, maximum);

    internal static DriverBackupCatalogSnapshot BrowseBackupsForTest(string root, int maximum = 128)
    {
        var items = new List<DriverBackupCatalogItem>();
        int inspected = 0;
        bool partial = false;
        try
        {
            if (maximum is < 1 or > 512)
                return new([], true, 0);
            if (!Directory.Exists(root))
                return new([], !ExistingParentsAreOrdinary(root), 0);
            if (!ExistingParentsAreOrdinary(root))
                return new([], true, 0);
            var children = Directory.EnumerateDirectories(root, "manual-*",
                SearchOption.TopDirectoryOnly).Take(maximum + 1).ToArray();
            if (children.Length > maximum) partial = true;
            foreach (var folder in children.Take(maximum))
            {
                inspected++;
                try
                {
                    if (!SafeChild(root, folder))
                    {
                        items.Add(new(folder, DriverBackupCatalogState.Inconclusive,
                            "Local não confiável; não usar esta cópia."));
                        partial = true;
                        continue;
                    }
                    var review = ReviewForTest(root, folder);
                    if (!review.Verified || review.Receipt is null)
                    {
                        items.Add(new(folder, DriverBackupCatalogState.Inconclusive,
                            review.Message));
                        partial = true;
                        continue;
                    }
                    string marker = folder + ".restore-pending.json";
                    bool markerExists = File.Exists(marker);
                    bool markerValid = !markerExists ||
                        PendingMarkerMatches(marker, review.Receipt);
                    var state = markerExists && !markerValid
                        ? DriverBackupCatalogState.Inconclusive :
                        markerExists ? DriverBackupCatalogState.PendingReconciliation :
                        review.CanStage ? DriverBackupCatalogState.Valid :
                        DriverBackupCatalogState.ProtectedClass;
                    items.Add(new(folder, state,
                        markerExists && !markerValid
                            ? "Marcador de recuperação inválido ou redirecionado; preservar para revisão."
                            : markerExists ? "Recuperação pendente de conciliação; não repetir."
                            : review.Message,
                        review.Receipt.PublishedInf, review.Receipt.DriverClass,
                        review.Receipt.CreatedAtUtc));
                    if (markerExists && !markerValid) partial = true;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    items.Add(new(folder, DriverBackupCatalogState.Inconclusive,
                        "Não foi possível ler o backup (" + ex.GetType().Name + ")."));
                    partial = true;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            partial = true;
        }
        return new(items.OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Folder, StringComparer.OrdinalIgnoreCase).ToArray(),
            partial, inspected);
    }

    // Pode ser executado sem privilégios. Não abre processo nem altera o Windows.
    internal static DriverRestoreReview Review(string folder) => ReviewForTest(Root, folder);

    internal static DriverRestoreReview ReviewForTest(string root, string folder)
    {
        try
        {
            if (!SafeChild(root, folder)) return new(false, false, "Pasta fora do cofre local ou redirecionada.");
            string path = folder + ".backup.json";
            if (!File.Exists(path) || new FileInfo(path).Length is < 2 or > 16384 ||
                File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                return new(false, false, "Recibo de backup ausente ou inválido.");
            var receipt = JsonSerializer.Deserialize<DriverBackupReceipt>(File.ReadAllText(path));
            if (receipt is null || receipt.Schema != 1 ||
                !PublishedInf.IsMatch(receipt.PublishedInf ?? "") ||
                !InfFile.IsMatch(receipt.OriginalInf ?? "") ||
                !Hash64(receipt.InfSha256) || !Hash64(receipt.PackageSha256) ||
                receipt.CreatedAtUtc == default || receipt.CreatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(5))
                return new(false, false, "Recibo inconsistente.");
            string? inf = FindSingleInf(folder, receipt.OriginalInf);
            if (inf is null || !DriverSilentUninstall.VerifyBackup(folder,
                    receipt.OriginalInf, receipt.InfSha256, out string hash) ||
                !hash.Equals(receipt.PackageSha256, StringComparison.OrdinalIgnoreCase))
                return new(false, false, "Backup alterado, incompleto ou não verificável.");
            bool stageAllowed = DriverUpdatePilot.AllowedClass(receipt.DriverClass);
            return new(true, stageAllowed,
                stageAllowed ? "Cópia conferida; apenas inclusão controlada no Driver Store é possível." :
                "Cópia conferida; classe protegida ou indeterminada, restauração automática bloqueada.",
                inf, receipt);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(false, false, "Revisão inconclusiva (" + ex.GetType().Name + ").");
        }
    }

    // Etapa 1 de recuperação: somente /add-driver INF EXATO, nunca /install.
    // Exige decisão humana e marca durável antes da operação nativa.
    internal static async Task<DriverRestoreOutcome> StageAsync(
        string folder, bool confirmed, Func<bool> stillCurrent, CancellationToken token)
    {
        if (!confirmed || !OperatingSystem.IsWindows() || !DriverSilentUninstall.IsElevated() ||
            token.IsCancellationRequested || !stillCurrent())
            return new(false, false, "Restauração não autorizada; nada solicitado.");
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
            return new(false, false, "Outra recuperação em andamento.");
        if (!DriverG32RecoveryTicket.TryBeginOperation(DriverG32OperationKind.Stage))
        {
            Interlocked.Exchange(ref active, 0);
            return new(false, false, "Atualização WUA ou recuperação já está em andamento.");
        }
        bool markerWritten = false;
        try
        {
            var review = Review(folder);
            if (!review.Verified || !review.CanStage || review.InfPath is null || review.Receipt is null)
                return new(false, false, review.Message);
            string marker = folder + ".restore-pending.json";
            if (File.Exists(marker) || !stillCurrent() || token.IsCancellationRequested)
                return new(false, false, "Recuperação pendente ou seleção revogada; não repetir.");
            var signature = await Task.Run(() =>
                DriverInfVerification.InspectInf(review.InfPath, token), token);
            string actualClass = ReadInfClass(review.InfPath);
            if (signature.Trust != DriverInfTrust.VerifiedInf ||
                !string.Equals(signature.Sha256, review.Receipt.InfSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !DriverUpdatePilot.AllowedClass(actualClass) ||
                !string.Equals(actualClass, review.Receipt.DriverClass, StringComparison.OrdinalIgnoreCase) ||
                !stillCurrent() || token.IsCancellationRequested || !Review(folder).CanStage)
                return new(false, false, "INF exportado não passou na reconfirmação de assinatura e hash.");
            await using (var output = new FileStream(marker, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    Schema = 1, Action = "StageOnly", AtUtc = DateTimeOffset.UtcNow,
                    BackupHash = review.Receipt.PackageSha256, State = "PENDING_RECONCILIATION"
                });
                await output.WriteAsync(bytes, CancellationToken.None);
                output.Flush(flushToDisk: true);
            }
            markerWritten = true;
            // Não há /install, /subdirs, /reboot, wildcard, exclusão ou repetição.
            int exit = await DriverSilentUninstall.RunPnPUtil(
                ["/add-driver", review.InfPath], TimeSpan.FromMinutes(2));
            return new(true, true, exit == 0
                ? "PnPUtil retornou sem erro; inclusão no Store ainda requer verificação independente. Nenhum dispositivo foi instalado pelo GF."
                : "PnPUtil retornou erro; estado do Store inconclusivo, não repetir automaticamente.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(markerWritten, markerWritten,
                "Recuperação " + (markerWritten ? "potencialmente iniciada" : "não iniciada") +
                " (" + ex.GetType().Name + "); conciliar antes de repetir.");
        }
        finally { DriverG32RecoveryTicket.EndOperation(); Interlocked.Exchange(ref active, 0); }
    }

    private sealed record StagePendingReceipt(int Schema, string Action,
        DateTimeOffset AtUtc, string BackupHash, string State);

    // Usado pelo catálogo e conciliação: um arquivo de nome esperado não
    // comprova sozinho uma solicitação legítima de staging.
    private static bool PendingMarkerMatches(string marker, DriverBackupReceipt receipt)
    {
        try
        {
            if (!File.Exists(marker)) return false;
            var info = new FileInfo(marker);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                info.Length is <= 2 or > 4096)
                return false;
            var pending = JsonSerializer.Deserialize<StagePendingReceipt>(File.ReadAllBytes(marker));
            return pending is not null && pending.Schema == 1 &&
                pending.Action == "StageOnly" &&
                pending.State == "PENDING_RECONCILIATION" &&
                string.Equals(pending.BackupHash, receipt.PackageSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                pending.AtUtc != default &&
                pending.AtUtc <= DateTimeOffset.UtcNow.AddMinutes(5) &&
                pending.AtUtc >= receipt.CreatedAtUtc;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    // Esta observação é opcional e não bloqueia o lançamento do aplicativo.
    // Um INF já presente ANTES do comando também pode coincidir; portanto
    // jamais deduzir que o GF o adicionou, instalou ou restaurou um dispositivo.
    internal static DriverRestoreReconciliation ReconcileReadOnly(
        string folder, CancellationToken token)
    {
        if (token.IsCancellationRequested || !OperatingSystem.IsWindows())
            return new(DriverRestoreReconcileState.Inconclusive,
                "Consulta indisponível; não alterar o diário de recuperação.");
        try
        {
            var inventory = DriverInventory.Capture(token);
            var published = DriverPublishedPackages.Capture(inventory, token,
                maximumPackages: 4096, timeout: TimeSpan.FromSeconds(20));
            return ReconcileWithEvidence(Root, folder, published,
                entry => DriverInfVerification.Verify(entry, token), ReadInfClass);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(DriverRestoreReconcileState.Inconclusive,
                "Consulta ao Driver Store indisponível (" + ex.GetType().Name + ").");
        }
    }

    // A fonte e os delegates também servem aos testes isolados: produção usa
    // SetupAPI e assinatura do Windows; fixtures nunca alteram o Driver Store.
    internal static DriverRestoreReconciliation ReconcileWithEvidence(
        string root, string folder, DriverPublishedAuditResult published,
        Func<DriverStoreEvidence, DriverInfVerificationResult> verify,
        Func<string, string> infClass)
    {
        var inconclusive = new DriverRestoreReconciliation(
            DriverRestoreReconcileState.Inconclusive,
            "Recuperação inconclusiva; preservar o diário e não repetir staging.");
        try
        {
            var review = ReviewForTest(root, folder);
            if (!review.Verified || !review.CanStage || review.Receipt is null ||
                !SafeChild(root, folder))
                return inconclusive;
            string marker = folder + ".restore-pending.json";
            if (!File.Exists(marker))
                return new(DriverRestoreReconcileState.NoRequest,
                    "Backup íntegro, mas nenhuma recuperação foi solicitada.");
            if (!PendingMarkerMatches(marker, review.Receipt))
                return inconclusive;
            if (published.Partial || !published.EnumerationComplete ||
                published.Packages.Count is < 1 or > 4096 ||
                published.Packages.Any(p => p.Evidence.State != DriverStoreState.Mapped))
                return inconclusive;
            // A observação só aceita UM único INF assinado de mesma classe e
            // hash. A existência não prova que o GF efetuou a operação.
            string? observed = null;
            foreach (var package in published.Packages)
            {
                var evidence = package.Evidence;
                // O arquivo original exportado é a identidade de pacote.
                // Evita verificar milhares de INFs publicados sem relação
                // com esta recuperação e mantém a checagem do hash exato.
                if (!string.Equals(Path.GetFileName(evidence.StorePath),
                    review.Receipt.OriginalInf, StringComparison.OrdinalIgnoreCase))
                    continue;
                var signature = verify(evidence);
                if (signature.Trust != DriverInfTrust.VerifiedInf ||
                    !string.Equals(signature.Sha256, review.Receipt.InfSha256,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!DriverUpdatePilot.AllowedClass(infClass(evidence.StorePath)) ||
                    !string.Equals(infClass(evidence.StorePath), review.Receipt.DriverClass,
                        StringComparison.OrdinalIgnoreCase))
                    return inconclusive;
                if (observed is not null) return inconclusive;
                observed = evidence.InfName;
            }
            return observed is null ? inconclusive : new(
                DriverRestoreReconcileState.MatchingInfObserved,
                "INF assinado correspondente observado no Driver Store. A origem da inclusão e a vinculação PnP não estão comprovadas; não repetir nem liberar diário automaticamente.",
                observed);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return inconclusive; }
    }
}
