using System.Diagnostics;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Aplicável a TODO inventário (sem fornecedor/modelo codificado). Os controles
// aparecem para qualquer seleção; a política explica por que uma operação
// concreta pode ser bloqueada. Nunca remover INF do Windows/Microsoft, pacote
// compartilhado ou classe crítica. Não utilizar /force nem /reboot.
internal sealed record DriverSilentUninstallDecision(bool Allowed, string Reason);
internal enum DriverSilentUninstallState
{
    Blocked, BackupFailed, InstallationUncertain, VerificationUncertain, Verified
}
internal sealed record DriverSilentUninstallOutcome(DriverSilentUninstallState State,
    string Message, string? BackupPath = null)
{
    internal bool RequiresInventoryRefresh => State is DriverSilentUninstallState.Verified or
        DriverSilentUninstallState.InstallationUncertain or DriverSilentUninstallState.VerificationUncertain;
}

internal static class DriverSilentUninstall
{
    private static int active;
    // Classes que podem derrubar o SO ou serviços essenciais nunca entram
    // no piloto de remoção. Outros dispositivos são avaliados individualmente.
    private static readonly HashSet<string> CriticalClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Computer", "Processor", "Firmware", "DiskDrive", "HDC",
        "SCSIAdapter", "Storage", "Volume", "VolumeSnapshot", "Display",
        "Net", "NetClient", "NetService", "NetTrans", "USB", "USBDevice",
        "HIDClass", "Keyboard", "Mouse", "Bluetooth", "Battery", "SecurityDevices",
        "SoftwareComponent", "Extension", "Monitor", "Biometric", "FSFilter", "Unknown"
    };
    private static readonly Regex OemInf = new(@"^oem[0-9]{1,7}\.inf$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static DriverSilentUninstallDecision Evaluate(
        DriverInventoryResult? inventory, DriverInventoryEntry? selected)
    {
        if (inventory is null || selected is null || inventory.Partial ||
            inventory.Items.Count is < 1 or > 4000)
            return new(false, "Inventário inexistente ou parcial; consulte novamente.");
        if (string.IsNullOrWhiteSpace(selected.DeviceId) ||
            selected.DeviceId.Length > 1024 || selected.DeviceId.Any(char.IsControl) ||
            inventory.Items.Count(d => string.Equals(d.DeviceId, selected.DeviceId,
                StringComparison.OrdinalIgnoreCase)) != 1)
            return new(false, "Instância PnP duplicada ou inválida.");
        var current = inventory.Items.Single(d => string.Equals(d.DeviceId,
            selected.DeviceId, StringComparison.OrdinalIgnoreCase));
        if (!Equals(current, selected) || current.Present != true || current.Signed != true ||
            current.WithoutSignedDriverAssociation || current.PnpProblemCode != 0)
            return new(false, "Presença, estado PnP ou associação assinada do dispositivo não confirmados.");
        if (!OemInf.IsMatch(current.InfName ?? ""))
            return new(false, "Apenas pacotes OEM publicados (oemNN.inf); INFs do Windows são protegidos.");
        if (current.PackageEvidence is not { State: DriverStoreState.Mapped } mapped ||
            !string.Equals(mapped.InfName, current.InfName, StringComparison.OrdinalIgnoreCase))
            return new(false, "Vínculo INF/Driver Store não comprovado.");
        if (string.IsNullOrWhiteSpace(current.Provider) ||
            current.Provider.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(current.ReportedDriverClass) ||
            CriticalClasses.Contains(current.ReportedDriverClass))
            return new(false, "Driver do sistema, classe crítica ou classe não identificada.");
        // MEDIA pode incluir áudio da placa-mãe: só permitir dispositivos
        // enumerados na raiz, sem desativar placa de som física.
        if (current.ReportedDriverClass.Equals("MEDIA", StringComparison.OrdinalIgnoreCase) &&
            !current.DeviceId.StartsWith(@"ROOT\", StringComparison.OrdinalIgnoreCase))
            return new(false, "MEDIA físico protegido; dispositivo não é virtual ROOT.");
        if (inventory.Items.Count(d => !d.WithoutSignedDriverAssociation &&
            string.Equals(d.InfName, current.InfName, StringComparison.OrdinalIgnoreCase)) != 1)
            return new(false, "Pacote INF compartilhado ou duplicado: exclusão conjunta bloqueada.");
        if (inventory.Store is not { Packages: { } packages } ||
            packages.Count(p => string.Equals(p.InfName, current.InfName,
                StringComparison.OrdinalIgnoreCase) && p.State == DriverStoreState.Mapped) != 1)
            return new(false, "Pacote não confirmado de forma única no Driver Store.");
        return new(true, "Pacote OEM individual não crítico; backup e reconfirmação obrigatórios.");
    }

    internal static async Task<DriverSilentUninstallOutcome> Execute(
        DriverInventoryResult snapshot, DriverInventoryEntry selected, bool consent,
        Func<bool> selectionStillCurrent, Action<string>? progress = null)
    {
        var gate = Evaluate(snapshot, selected);
        if (!RemovalSafetyContract.CanStart(consent, !snapshot.Partial,
            selectionStillCurrent(), gate.Allowed, gate.Allowed))
            return new(DriverSilentUninstallState.Blocked, gate.Allowed
                ? "Seleção foi alterada; nenhuma ação executada." : gate.Reason);
        if (!OperatingSystem.IsWindows() || !IsElevated())
            return new(DriverSilentUninstallState.Blocked,
                "Execute o GF como administrador para remover drivers. Nenhum comando foi iniciado.");
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
            return new(DriverSilentUninstallState.Blocked,
                "Outra remoção está ativa; operação paralela bloqueada.");
        string? backupPath = null;
        bool removalAttempted = false;
        try
        {
            progress?.Invoke("Revalidando instância PnP e pacote OEM no Windows...");
            var fresh = await Task.Run(() => DriverStoreAudit.CaptureWithPackages(CancellationToken.None));
            var rows = fresh.Items.Where(d => string.Equals(d.DeviceId,
                selected.DeviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (rows.Length != 1 || !SameIdentity(selected, rows[0]) ||
                !Evaluate(fresh, rows[0]).Allowed || !selectionStillCurrent())
                return new(DriverSilentUninstallState.Blocked,
                    "Identidade ou associação mudou na reconsulta; nada removido.");

            // WMI IsSigned apenas relata uma propriedade; antes de executar
            // operações exige assinatura do INF/catálogo verificada no Windows.
            var infTrust = await Task.Run(() => DriverInfVerification.Verify(
                rows[0].PackageEvidence!, CancellationToken.None));
            if (infTrust.Trust != DriverInfTrust.VerifiedInf)
                return new(DriverSilentUninstallState.Blocked,
                    "Assinatura do pacote não foi verificada pelo Windows; nenhum driver removido.");

            // Exportação é a etapa de recuperação prévia. Nenhuma remoção ocorre
            // enquanto não existir cópia em armazenamento persistente, com hash.
            string backups = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "GerenciadorFinal", "Drivers", "Backups");
            string pendingPath = Path.Combine(backups,
                "pendente-" + selected.InfName.ToLowerInvariant() + ".json");
            if (!IsMissing(pendingPath))
                return new(DriverSilentUninstallState.Blocked,
                    "Existe operação pendente deste INF. Confira o estado antes de repetir.");
            backupPath = Path.Combine(backups,
                DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupPath);
            if (!SafeBackupLocation(backups, backupPath))
                return new(DriverSilentUninstallState.BackupFailed,
                    "Diretório do backup contém redirecionamento ou não é confiável; remoção bloqueada.");
            progress?.Invoke("Exportando pacote original para recuperação...");
            var exported = await RunPnPUtil(["/export-driver", selected.InfName, backupPath], TimeSpan.FromMinutes(2));
            var package = rows[0].PackageEvidence!;
            string digest;
            if (exported != 0 || !SafeBackupLocation(backups, backupPath) ||
                !VerifyBackup(backupPath, Path.GetFileName(package.StorePath), infTrust.Sha256, out digest))
                return new(DriverSilentUninstallState.BackupFailed,
                    "Backup OEM não confirmado; remoção não iniciada.", backupPath);
            if (!selectionStillCurrent())
                return new(DriverSilentUninstallState.Blocked,
                    "Seleção alterada após o backup; nenhum driver removido.", backupPath);

            // Uma segunda fotografia verifica identidade, elegibilidade e
            // compartilhamento imediatamente antes da ação irreversível.
            var finalSnapshot = await Task.Run(() =>
                DriverStoreAudit.CaptureWithPackages(CancellationToken.None));
            var lastRows = finalSnapshot.Items.Where(d => string.Equals(d.DeviceId,
                selected.DeviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (lastRows.Length != 1 || !SameIdentity(selected, lastRows[0]) ||
                !Evaluate(finalSnapshot, lastRows[0]).Allowed)
            {
                return new(DriverSilentUninstallState.Blocked,
                    "Revalidação final mudou; pacote preservado. Nenhuma remoção iniciada.",
                    backupPath);
            }
            var finalPackage = lastRows[0].PackageEvidence!;
            var finalTrust = await Task.Run(() => DriverInfVerification.Verify(finalPackage, CancellationToken.None));
            if (!MatchesBackupSource(package, infTrust, finalPackage, finalTrust) || !selectionStillCurrent())
                return new(DriverSilentUninstallState.Blocked,
                    "Pacote ou seleção mudou após o backup; nenhum driver removido.", backupPath);

            string record = Path.Combine(backupPath, "operacao-gf.json");
            await WriteRecord(record, selected, "INTENCAO_ANTES_DA_REMOCAO", digest);
            // Protege inclusive contra uma segunda instância do GF e contra
            // reexecução após queda do Windows. A presença deste marcador
            // bloqueia nova remoção do MESMO pacote até conciliação manual.
            try
            {
                using var pending = new FileStream(pendingPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
                byte[] marker = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    selected.DeviceId, selected.InfName, BackupPath = backupPath,
                    BackupSha256 = digest, At = DateTimeOffset.UtcNow
                });
                pending.Write(marker);
                pending.Flush(flushToDisk: true);
            }
            catch (IOException)
            {
                return new(DriverSilentUninstallState.Blocked,
                    "Existe operação pendente deste INF. Não repetir sem conciliação.", backupPath);
            }
            // Depois deste ponto, um timeout/erro não significa que não houve
            // efeito. Nunca repetir às cegas; conciliar manualmente pelo INF.
            progress?.Invoke("Removendo silenciosamente pacote OEM específico...");
            if (!selectionStillCurrent())
            {
                await WriteRecord(record, selected, "SELECAO_REVOGADA", digest);
                File.Delete(pendingPath);
                return new(DriverSilentUninstallState.Blocked,
                    "Seleção revogada; nenhum driver removido.", backupPath);
            }
            int exit;
            removalAttempted = true;
            try { exit = await RunPnPUtil(["/delete-driver", selected.InfName, "/uninstall"], TimeSpan.FromMinutes(3)); }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                await WriteRecord(record, selected, "RESULTADO_INCERTO", digest);
                return new(DriverSilentUninstallState.InstallationUncertain,
                    "PnPUtil não retornou resultado confiável. Não repetir automaticamente.", backupPath);
            }
            await WriteRecord(record, selected, "PNPUTIL_SAIU_" + exit, digest);
            progress?.Invoke("Conferindo pacote e vínculo após a desinstalação...");
            var after = await Task.Run(() => DriverInventory.Capture(CancellationToken.None));
            bool verified = RemovalConfirmed(exit, after, finalPackage);
            await WriteRecord(record, selected, verified ? "VERIFICADO" : "VERIFICACAO_PENDENTE", digest);
            if (verified)
            {
                try { File.Delete(pendingPath); }
                catch (IOException)
                {
                    return new(DriverSilentUninstallState.VerificationUncertain,
                        "Remoção confirmada, mas trava persistente não pôde ser liberada; não repetir.", backupPath);
                }
                catch (UnauthorizedAccessException)
                {
                    return new(DriverSilentUninstallState.VerificationUncertain,
                        "Remoção confirmada, mas trava persistente não pôde ser liberada; não repetir.", backupPath);
                }
            }
            return verified
                ? new(DriverSilentUninstallState.Verified,
                    "Pacote OEM removido. Nenhum vínculo PnP ou INF do pacote foi encontrado.", backupPath)
                : new(DriverSilentUninstallState.VerificationUncertain,
                    "Remoção ainda não comprovada. Pode ser necessário reiniciar; não repetir.", backupPath);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return removalAttempted
                ? new(DriverSilentUninstallState.InstallationUncertain,
                    "Falha após solicitar remoção: " + e.GetType().Name + ". Confira o estado antes de repetir.", backupPath)
                : new(DriverSilentUninstallState.Blocked,
                    "Falha de preparação: " + e.GetType().Name + ". Nenhuma remoção solicitada.", backupPath);
        }
        finally { Interlocked.Exchange(ref active, 0); }
    }

    private static bool SameIdentity(DriverInventoryEntry a, DriverInventoryEntry b) =>
        string.Equals(a.DeviceId, b.DeviceId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.InfName, b.InfName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Provider, b.Provider, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Version, b.Version, StringComparison.Ordinal) &&
        string.Equals(a.ReportedDriverClass, b.ReportedDriverClass, StringComparison.OrdinalIgnoreCase);

    internal static bool MatchesBackupSource(DriverStoreEvidence originalPackage,
        DriverInfVerificationResult original, DriverStoreEvidence currentPackage,
        DriverInfVerificationResult current) =>
        original.Trust == DriverInfTrust.VerifiedInf && current.Trust == DriverInfTrust.VerifiedInf &&
        original.Sha256.Length != 0 && original.Sha256 == current.Sha256 &&
        string.Equals(original.Catalog, current.Catalog, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(original.Signer, current.Signer, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(originalPackage.StorePath, currentPackage.StorePath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(originalPackage.PublishedPath, currentPackage.PublishedPath, StringComparison.OrdinalIgnoreCase);

    internal static bool RemovalConfirmed(int exit, DriverInventoryResult after, DriverStoreEvidence package)
    {
        if (exit != 0 || after.Partial || after.Items.Count == 0) return false;
        bool assigned = after.Items.Any(d =>
            string.Equals(d.InfName, package.InfName, StringComparison.OrdinalIgnoreCase));
        return RemovalSafetyContract.Confirmed(true, true,
            !assigned && IsMissing(package.PublishedPath), IsMissing(package.StorePath));
    }

    private static bool IsMissing(string path)
    {
        try { _ = File.GetAttributes(path); return false; }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
    }

    // Segurança do local persistente, inclusive quando o perfil contiver
    // junções/reparse points: não gravar backups privilegiados através de links.
    private static bool SafeBackupLocation(string backups, string destination)
    {
        try
        {
            var root = Path.GetFullPath(backups).TrimEnd(Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(destination);
            if (!full.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)) return false;
            for (DirectoryInfo? directory = new DirectoryInfo(destination);
                directory is not null; directory = directory.Parent)
            {
                if (!directory.Exists ||
                    directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException) { return false; }
    }

    internal static bool VerifyBackup(string folder, string infName, string infDigest, out string digest)
    {
        digest = "";
        try
        {
            // Não usar SearchOption.AllDirectories: ela pode percorrer uma
            // junção antes de detectarmos um redirecionamento.
            var files = new List<string>();
            var directories = new Queue<string>();
            directories.Enqueue(folder);
            int countDirectories = 0;
            while (directories.Count != 0)
            {
                if (++countDirectories > 256) return false;
                string directory = directories.Dequeue();
                var dirInfo = new DirectoryInfo(directory);
                if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
                foreach (string entry in Directory.GetFileSystemEntries(directory))
                {
                    var attrs = File.GetAttributes(entry);
                    if (attrs.HasFlag(FileAttributes.ReparsePoint)) return false;
                    if (attrs.HasFlag(FileAttributes.Directory)) directories.Enqueue(entry);
                    else files.Add(entry);
                    if (files.Count > 256 || directories.Count > 256) return false;
                }
            }
            int matchingInfs = 0;
            long total = 0;
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (string file in files.Order(StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(file);
                total += info.Length;
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                    info.Length <= 0 || info.Length > 200_000_000 || total > 200_000_000)
                    return false;
                for (DirectoryInfo? parent = info.Directory; parent is not null &&
                    parent.FullName.StartsWith(Path.GetFullPath(folder),
                        StringComparison.OrdinalIgnoreCase); parent = parent.Parent)
                    if (parent.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
                string relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
                if (relative.StartsWith("../", StringComparison.Ordinal) ||
                    Path.IsPathRooted(relative)) return false;
                byte[] name = System.Text.Encoding.UTF8.GetBytes(relative + "\n" + info.Length + "\n");
                sha.AppendData(name);
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] fileDigest = SHA256.HashData(stream);
                if (Path.GetFileName(file).Equals(infName, StringComparison.OrdinalIgnoreCase))
                {
                    if (++matchingInfs != 1 || Convert.ToHexString(fileDigest) != infDigest) return false;
                }
                sha.AppendData(fileDigest);
            }
            if (matchingInfs != 1) return false;
            digest = Convert.ToHexString(sha.GetHashAndReset());
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException) { return false; }
    }

    private static async Task WriteRecord(string path, DriverInventoryEntry selected,
        string phase, string backupDigest)
    {
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(new
        {
            selected.DeviceId, selected.InfName, selected.Provider,
            selected.ReportedDriverClass, Domain = "driver", Phase = phase,
            RemovalContract = RemovalSafetyContract.AuditOutcome(phase == "VERIFICADO"),
            BackupSha256 = backupDigest, TimestampUtc = DateTimeOffset.UtcNow
        });
        string temporary = path + ".tmp";
        await using (var f = new FileStream(temporary, FileMode.Create,
            FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await f.WriteAsync(content);
            f.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, overwrite: true);
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task<int> RunPnPUtil(IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        string exe = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.Windows), "System32", "pnputil.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("PnPUtil do Windows indisponível.");
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = false, RedirectStandardError = false
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var child = Process.Start(info) ?? throw new IOException("PnPUtil não iniciou.");
        using var cancel = new CancellationTokenSource(timeout);
        try { await child.WaitForExitAsync(cancel.Token); }
        catch (OperationCanceledException)
        {
            // NÃO matar instalação/remoção em andamento: resultado incerto e
            // marcador durável impedem repetição. O processo pode continuar.
            throw new TimeoutException("PnPUtil excedeu o prazo; resultado indeterminado.");
        }
        return child.ExitCode;
    }
}
