using System.Text.Json;

namespace GerenciadorAplicativos;

internal enum DriverG32OperationKind { Update, Export, Stage, Uninstall }

// G3.2: uma autorizacao concreta depende de backup OEM verificavel.
// A construcao privada impede promover um checkbox ou uma fixture a prova.
internal sealed class DriverG32RecoveryTicket
{
    private static int active;
    private static int activeKind;
    private static long generation;
    private readonly long leaseGeneration;
    private readonly DriverUpdatePilotChoice choice;
    private readonly DriverInventoryEntry before;
    private readonly DriverStoreEvidence package;
    private readonly DriverInfVerificationResult trust;
    private readonly string folder;
    private readonly string digest;

    private DriverG32RecoveryTicket(DriverUpdatePilotChoice choice,
        DriverInventoryEntry before, DriverStoreEvidence package,
        DriverInfVerificationResult trust, string folder, string digest, long leaseGeneration)
    {
        this.leaseGeneration = leaseGeneration;
        this.choice = choice;
        this.before = before;
        this.package = package;
        this.trust = trust;
        this.folder = folder;
        this.digest = digest;
    }

    // Recusar diretórios remotos/redirecionados ANTES de criar o backup.
    // O caminho completo e os ancestrais são conferidos novamente depois.
    private static bool OrdinaryLocalPath(string target)
    {
        try
        {
            string full = Path.GetFullPath(target);
            if (!Path.IsPathFullyQualified(full) ||
                full.StartsWith(@"\\", StringComparison.Ordinal) ||
                full.StartsWith("//", StringComparison.Ordinal)) return false;
            for (DirectoryInfo? directory = new DirectoryInfo(full);
                directory is not null; directory = directory.Parent)
                if (directory.Exists &&
                    directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    return false;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    internal string BackupFolder => folder;

    internal static bool HasActiveOperation => Volatile.Read(ref active) != 0;
    internal static bool HasUpdateOperation =>
        HasActiveOperation && Volatile.Read(ref activeKind) == (int)DriverG32OperationKind.Update + 1;

    internal static bool TryBeginOperation(DriverG32OperationKind kind = DriverG32OperationKind.Update)
    {
        if (!Enum.IsDefined(kind) || Interlocked.CompareExchange(ref active, 1, 0) != 0)
            return false;
        Volatile.Write(ref activeKind, (int)kind + 1);
        Interlocked.Increment(ref generation);
        return true;
    }

    internal static void EndOperation()
    {
        Volatile.Write(ref activeKind, 0);
        Interlocked.Exchange(ref active, 0);
    }

    internal bool Matches(DriverUpdatePilotChoice candidate) =>
        HasUpdateOperation && leaseGeneration == Volatile.Read(ref generation) &&
        Equals(choice, candidate) && !string.IsNullOrWhiteSpace(digest);

    internal static bool CanOffer(DriverInventoryResult? inventory,
        DriverInventoryEntry? selected, DriverUpdatePilotChoice? candidate)
    {
        if (inventory is null || selected is null || candidate is null ||
            inventory.Partial || !DriverUpdatePilot.AllowedClass(candidate.DriverClass) ||
            selected.PnpProblemCode != 0 ||
            !string.Equals(selected.DeviceId, candidate.DeviceId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(selected.InfName, candidate.InfName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(selected.Provider, candidate.Provider, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(selected.Version, candidate.InstalledVersion, StringComparison.Ordinal) ||
            !string.Equals(selected.ReportedDriverClass, candidate.DriverClass,
                StringComparison.OrdinalIgnoreCase) ||
            !selected.HardwareIds.Contains(candidate.HardwareId, StringComparer.OrdinalIgnoreCase))
            return false;
        // Reutiliza a politica ja aprovada de OEM unico, nao critico,
        // nao Microsoft, presente, assinado e mapeado no Driver Store.
        return DriverSilentUninstall.Evaluate(inventory, selected).Allowed;
    }

    private static DriverInventoryEntry? Resolve(DriverInventoryResult inventory,
        DriverUpdatePilotChoice candidate)
    {
        var matches = inventory.Items.Where(x =>
            string.Equals(x.DeviceId, candidate.DeviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool Same(DriverInventoryEntry original, DriverInventoryEntry current) =>
        string.Equals(original.DeviceId, current.DeviceId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(original.InfName, current.InfName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(original.Provider, current.Provider, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(original.Version, current.Version, StringComparison.Ordinal) &&
        string.Equals(original.ReportedDriverClass, current.ReportedDriverClass,
            StringComparison.OrdinalIgnoreCase) &&
        original.PnpProblemCode == current.PnpProblemCode &&
        original.HardwareIds.ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(current.HardwareIds);

    internal static async Task<(DriverG32RecoveryTicket? Ticket, string Reason)> PrepareAsync(
        DriverInventoryResult snapshot, DriverInventoryEntry selected,
        DriverUpdatePilotChoice candidate, Func<bool> stillCurrent, CancellationToken token)
    {
        if (!HasUpdateOperation)
            return (null, "Nenhuma sessão de atualização WUA exclusiva está ativa.");
        long capturedLease = Volatile.Read(ref generation);
        if (!OperatingSystem.IsWindows() || !DriverSilentUninstall.IsElevated())
            return (null, "Execute o GF como administrador; nenhuma atualização iniciada.");
        if (!CanOffer(snapshot, selected, candidate) || !stillCurrent() ||
            token.IsCancellationRequested)
            return (null, "Dispositivo, pacote ou seleção não elegíveis; nada alterado.");
        if (!DriverUpdatePilotFileJournal.MayBeginReadOnly(
                DriverUpdatePilotFileJournal.SuggestedPath))
            return (null, "Diário WUA anterior pendente, inválido ou indisponível; concilie antes.");
        // A remoção anterior do mesmo INF também pode ter estado incerto.
        string backups = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "GerenciadorFinal", "Drivers", "Backups");
        if (!OrdinaryLocalPath(backups))
            return (null, "Pasta de backup remota/redirecionada; operação bloqueada.");
        string pendingRemoval = Path.Combine(backups,
            "pendente-" + candidate.InfName.ToLowerInvariant() + ".json");
        if (File.Exists(pendingRemoval))
            return (null, "Existe remoção anterior pendente deste INF. Não atualizar.");

        string? destination = null;
        try
        {
            var fresh = await Task.Run(() =>
                DriverStoreAudit.CaptureWithPackages(token), token);
            var device = Resolve(fresh, candidate);
            if (device is null || !Same(selected, device) ||
                !CanOffer(fresh, device, candidate) || !stillCurrent() ||
                token.IsCancellationRequested)
                return (null, "Fotografia PnP ou vínculo OEM mudou; nenhuma operação WUA.");
            var mapped = device.PackageEvidence!;
            var verification = await Task.Run(() =>
                DriverInfVerification.Verify(mapped, token), token);
            if (verification.Trust != DriverInfTrust.VerifiedInf ||
                string.IsNullOrWhiteSpace(verification.Sha256))
                return (null, "Assinatura do INF original não confirmada; atualização bloqueada.");
            if (!stillCurrent() || token.IsCancellationRequested)
                return (null, "Seleção revogada antes do backup; nada alterado.");

            destination = Path.Combine(backups, "wua-" +
                DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" +
                Guid.NewGuid().ToString("N"));
            if (!OrdinaryLocalPath(destination))
                return (null, "Destino de backup redirecionado; nenhuma pasta criada.");
            Directory.CreateDirectory(destination);
            if (!OrdinaryLocalPath(destination) ||
                !DriverSilentUninstall.SafeBackupLocation(backups, destination))
                return (null, "Local do backup não confiável; não baixar driver.");
            int exit = await DriverSilentUninstall.RunPnPUtil(
                ["/export-driver", candidate.InfName, destination],
                TimeSpan.FromMinutes(2));
            if (exit != 0 ||
                !DriverSilentUninstall.SafeBackupLocation(backups, destination) ||
                !DriverSilentUninstall.VerifyBackup(destination,
                    Path.GetFileName(mapped.StorePath), verification.Sha256, out string digest) ||
                string.IsNullOrWhiteSpace(digest))
                return (null, "Exportação OEM incompleta ou adulterada; não baixar driver.");
            // Recibo durável: recuperação manual, nunca rollback automático.
            byte[] receipt = JsonSerializer.SerializeToUtf8Bytes(new
            {
                Candidate = candidate, BackupSha256 = digest,
                InfSha256 = verification.Sha256, VerifiedAtUtc = DateTimeOffset.UtcNow,
                Recovery = "Backup OEM; restauração exclusivamente supervisionada."
            });
            // Recibo fora da pasta exportada: o hash do pacote permanece imutável.
            string receiptPath = destination + ".json";
            using (var stream = new FileStream(receiptPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(receipt);
                stream.Flush(flushToDisk: true);
            }
            if (!stillCurrent() || token.IsCancellationRequested ||
                !HasUpdateOperation || capturedLease != Volatile.Read(ref generation))
                return (null, "Sessão WUA ou seleção revogada após exportação; backup preservado sem download.");
            return (new DriverG32RecoveryTicket(candidate, device,
                mapped, verification, destination, digest, capturedLease),
                "Backup OEM exportado, assinatura e hash verificados.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return (null, "Falha de preparação do backup (" +
                ex.GetType().Name + "); nenhuma instalação solicitada.");
        }
    }

    internal async Task<bool> RevalidateBeforeInstallAsync(
        DriverUpdatePilotChoice candidate, Func<bool> stillCurrent, CancellationToken token)
    {
        if (!Matches(candidate) || !stillCurrent() || token.IsCancellationRequested ||
            !DriverUpdatePilotFileJournal.MayBeginReadOnly(
                DriverUpdatePilotFileJournal.SuggestedPath))
            return false;
        try
        {
            string backups = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "GerenciadorFinal", "Drivers", "Backups");
            if (!OrdinaryLocalPath(folder) ||
                !DriverSilentUninstall.SafeBackupLocation(backups, folder) ||
                !DriverSilentUninstall.VerifyBackup(folder,
                    Path.GetFileName(package.StorePath), trust.Sha256, out string currentDigest) ||
                !string.Equals(currentDigest, digest, StringComparison.Ordinal))
                return false;
            var fresh = await Task.Run(() =>
                DriverStoreAudit.CaptureWithPackages(token), token);
            var current = Resolve(fresh, candidate);
            if (current is null || !Same(before, current) ||
                !CanOffer(fresh, current, candidate))
                return false;
            var checkedInf = await Task.Run(() =>
                DriverInfVerification.Verify(current.PackageEvidence!, token), token);
            return DriverSilentUninstall.MatchesBackupSource(package, trust,
                current.PackageEvidence!, checkedInf) &&
                !token.IsCancellationRequested && stillCurrent();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }
}
