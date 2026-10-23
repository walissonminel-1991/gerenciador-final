using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Piloto real de limpeza completa, EXCLUSIVO para 7-Zip EXE x64.
// A política só conhece três pastas de propriedade direta e duas chaves próprias.
// Sem exploração do perfil pessoal, do Registro MSI, de COM, do Windows ou de atalhos genéricos.
internal sealed record SevenZipFileEvidence(string Path, long Size, string Sha256, InstallerFileGuard.FileIdentity Identity);
internal sealed record SevenZipRegistryEvidence(RegistryHive Hive, string SubKey, string Sha256, int Entries);
internal sealed record SevenZipCleanupPlan(
    IReadOnlyList<SevenZipFileEvidence> Files,
    IReadOnlyList<string> Directories,
    IReadOnlyList<SevenZipRegistryEvidence> Registry,
    long TotalBytes, bool Complete, string Reason);
internal sealed record SevenZipCleanupOutcome(
    int FilesDeleted, int DirectoriesDeleted, int KeysDeleted,
    int Failures, int Preserved, bool Reinstalled)
{
    // Códigos técnicos fixos, sem caminhos, nomes de usuário ou conteúdo.
    internal IReadOnlyList<string> Problems { get; init; } = [];
}

internal static class SevenZipCompleteCleanup
{
    private const int MaxItems = 600;
    private const long MaxBytes = 256L * 1024 * 1024;
    private const string OwnKey = @"SOFTWARE\7-Zip";

    internal static bool Eligible(ProgramEntry previous)
    {
        if (previous.InventoryOnly ||
            !Regex.IsMatch(previous.Name,
                @"^7-Zip\s+\d+\.\d{1,2}\s+\(x64\)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            !previous.Publisher.Equals("Igor Pavlov", StringComparison.OrdinalIgnoreCase) ||
            !previous.InventoryId.Equals("LocalMachine|Registry64|7-Zip", StringComparison.OrdinalIgnoreCase))
            return false;
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip");
        return previous.InstallLocation.TrimEnd('\\').Equals(dir, StringComparison.OrdinalIgnoreCase) &&
            UninstallCommand.TryParse(previous.UninstallString, out var command) &&
            command.ProductCode is null && command.Arguments.Length == 0 &&
            command.FileName.Equals(Path.Combine(dir, "Uninstall.exe"), StringComparison.OrdinalIgnoreCase);
    }

#if GF_PROGRESSIVE
    // Disponivel apenas no assembly de testes. Jamais compila no aplicativo publicado.
    internal static string[]? SyntheticRoots;
#endif

    // Recuperação explícita do piloto PC FORTE, após a remoção da entrada ARP.
    // NUNCA é tratada como prova de instalação ou como autorização de limpeza.
    // A captura ainda exige inventário completo, ausência de variantes,
    // prévia de todos os caminhos e confirmação do usuário.
    internal static ProgramEntry RecoveryIdentity()
    {
        string root = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFiles), "7-Zip");
        return new ProgramEntry
        {
            Name = "7-Zip 26.05 (x64)", Publisher = "Igor Pavlov",
            Version = "26.05", InventoryId = "LocalMachine|Registry64|7-Zip",
            InstallLocation = root,
            UninstallString = "\"" + Path.Combine(root, "Uninstall.exe") + "\""
        };
    }

    internal static bool NoSevenZipRegistrations(IEnumerable<ProgramEntry> current) =>
        !current.Any(p => Regex.IsMatch(p.Name,
            @"^7-Zip(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    internal static bool NoSevenZipProcesses() =>
        !new[] { "7zFM", "7zG", "7z", "7za", "7zr" }
            .Any(name => Process.GetProcessesByName(name).Length != 0);

    private static IReadOnlyList<string> Roots()
    {
#if GF_PROGRESSIVE
        if (SyntheticRoots is not null) return SyntheticRoots;
#endif
        return
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7-Zip"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "7-Zip")
        ];
    }

    internal static SevenZipCleanupPlan Capture(ProgramEntry previous, bool inventoryComplete,
        IEnumerable<ProgramEntry> current, CancellationToken cancel = default)
    {
        if (!Eligible(previous) || !inventoryComplete || !NoSevenZipRegistrations(current) ||
            !NoSevenZipProcesses())
            return new([], [], [], 0, false,
                "Instalação ainda registrada, inventário parcial, programa aberto ou origem não comprovada.");
        var files = new List<SevenZipFileEvidence>();
        var directories = new List<string>();
        var registry = new List<SevenZipRegistryEvidence>();
        long total = 0;
        try
        {
            foreach (var root in Roots())
            {
                cancel.ThrowIfCancellationRequested();
                try { _ = File.GetAttributes(root); }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { continue; }
                // Inclui pais para impedir junções e redirecionamentos.
                if (!SafeDirectory(root))
                    return new([], [], [], 0, false, "Diretório com link ou caminho não verificável.");
                var pending = new Stack<string>();
                pending.Push(root);
                while (pending.Count > 0)
                {
                    cancel.ThrowIfCancellationRequested();
                    var dir = pending.Pop();
                    if (!SafeDirectory(dir) || !Within(root, dir))
                        return new([], [], [], 0, false, "Caminho ou link alterado.");
                    directories.Add(dir);
                    if (directories.Count + files.Count > MaxItems)
                        return new([], [], [], 0, false, "Limite de objetos atingido; nada autorizado.");
                    foreach (var path in Directory.EnumerateFileSystemEntries(dir))
                    {
                        cancel.ThrowIfCancellationRequested();
                        if (!Within(root, path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                            return new([], [], [], 0, false, "Link ou objeto fora da pasta exclusiva.");
                        if (Directory.Exists(path))
                        {
                            pending.Push(path);
                            continue;
                        }
                        if (!File.Exists(path)) return new([], [], [], 0, false, "Objeto de tipo desconhecido.");
                        using var handle = InstallerFileGuard.OpenResidual(path);
                        var identity = InstallerFileGuard.ReadIdentity(handle);
                        if (identity.Links != 1 || identity.Size > (ulong)MaxBytes ||
                            total + (long)identity.Size > MaxBytes)
                            return new([], [], [], 0, false, "Arquivo compartilhado ou limite de tamanho.");
                        total += (long)identity.Size;
                        string digest = Convert.ToHexString(SHA256.HashData(handle)).ToLowerInvariant();
                        files.Add(new(path, (long)identity.Size, digest, identity));
                        if (files.Count + directories.Count > MaxItems)
                            return new([], [], [], 0, false, "Limite de arquivos atingido.");
                    }
                }
            }
#if GF_PROGRESSIVE
            if (SyntheticRoots is null)
#endif
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                cancel.ThrowIfCancellationRequested();
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(OwnKey, writable: false);
                if (key is null) continue;
                if (!TryRegistryFingerprint(key, out var fingerprint, out int entries))
                    return new([], [], [], 0, false, "Registro muito amplo ou não verificável.");
                registry.Add(new(hive, OwnKey, fingerprint, entries));
            }
            return new(files.AsReadOnly(), directories.AsReadOnly(), registry.AsReadOnly(), total,
                true, "Prévia capturada; requer autorização específica e revalidação completa.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException or InvalidOperationException)
        { return new([], [], [], 0, false, "Fonte indisponível: " + ex.GetType().Name); }
    }

    private static bool Within(string root, string path)
    {
        string prefix = Path.GetFullPath(root).TrimEnd('\\') + "\\";
        string normalized = Path.GetFullPath(path);
        return normalized.Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SafeDirectory(string dir)
    {
        var item = new DirectoryInfo(dir);
        for (DirectoryInfo? current = item; current is not null; current = current.Parent)
        {
            if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0)
                return false;
        }
        return true;
    }

    private static bool TryRegistryFingerprint(RegistryKey root, out string digest, out int entries) =>
        RegistryTreeFingerprint.TryCapture(root, MaxItems, out digest, out entries, maxBytes: 8 * 1024 * 1024);

    // Estado da interface produzido SOMENTE a partir da última conferência,
    // nunca a partir de uma lista antiga que pode conter itens já excluídos.
    // O relatório por si só não autoriza qualquer exclusão.
    internal static ResidualAuditReport? RecoveryReport(SevenZipCleanupPlan plan)
    {
        if (!plan.Complete ||
            plan.Files.Count + plan.Directories.Count + plan.Registry.Count == 0)
            return null;
        var findings = plan.Files.Select(file => new ResidualAuditFinding(
            file.Path, "Arquivo próprio — conferir limpeza", 1,
            "Vínculo com a pasta exata do aplicativo; autorização separada necessária.",
            ResidualDisposition.Review, ResidualSource.Installation))
            .Concat(plan.Directories.Select(path => new ResidualAuditFinding(
                path, "Pasta própria — conferir limpeza", 1,
                "Diretório exato; só será removido se ficar vazio.",
                ResidualDisposition.Review, ResidualSource.Installation)))
            .Concat(plan.Registry.Select(key => new ResidualAuditFinding(
                (key.Hive == RegistryHive.LocalMachine
                    ? @"HKEY_LOCAL_MACHINE\\" : @"HKEY_CURRENT_USER\\") + key.SubKey,
                "Configuração própria do 7-Zip", key.Entries,
                "Árvore específica verificada por resumo criptográfico; não é cadastro MSI.",
                ResidualDisposition.Review, ResidualSource.Registry)))
            .ToArray();
        return new ResidualAuditReport(RecoveryIdentity().Name, true, findings,
            Array.Empty<string>(), Historical: true);
    }

    internal static string Describe(SevenZipCleanupPlan plan)
    {
        if (!plan.Complete) return "Limpeza bloqueada: " + plan.Reason;
        var paths = plan.Directories.OrderBy(x => x.Length).ToArray();
        return "O 7-Zip EXE x64 foi removido e nenhuma outra edição foi registrada.\n\n" +
            "A limpeza apagará definitivamente configurações e arquivos EXCLUSIVOS nas seguintes pastas:\n" +
            string.Join("\n", paths.Take(12)) +
            (paths.Length > 12 ? "\n... (" + (paths.Length - 12) + " subpastas adicionais)" : "") +
            "\n\nArquivos: " + plan.Files.Count + " · Bytes: " + plan.TotalBytes +
            "\nChaves próprias do Registro: " + string.Join(", ", plan.Registry.Select(x =>
                (x.Hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU") + @"\Software\7-Zip")) +
            "\n\nARQUIVOS EXATOS NA PRÉVIA:\n" +
            string.Join("\n", plan.Files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Path + " (" + x.Size + " bytes)")) +
            "\n\nNÃO remove Downloads, Documentos, Imagens, Músicas, dados em outros discos " +
            "ou o cadastro interno do Windows Installer.\n" +
            "Falha de permissões ou mudança desde a prévia preservará os itens restantes.";
    }

    internal static async Task<SevenZipCleanupOutcome> ExecuteAsync(
        ProgramEntry previous, SevenZipCleanupPlan accepted,
        Func<Task<(bool Complete, IReadOnlyCollection<ProgramEntry> Items)>> inventory,
        CancellationToken token = default)
    {
        if (!accepted.Complete) return new(0, 0, 0, 1, 0, false);
        token.ThrowIfCancellationRequested();
        var before = await inventory();
        token.ThrowIfCancellationRequested();
        if (!before.Complete || !NoSevenZipRegistrations(before.Items) || !NoSevenZipProcesses())
            return new(0, 0, 0, 1, accepted.Files.Count, true);
        var now = Capture(previous, before.Complete, before.Items, token);
        if (!Equivalent(accepted, now))
            return new(0, 0, 0, 1, accepted.Files.Count, false);
        int deleted = 0, directories = 0, keys = 0, failed = 0, preserved = 0;
        var problems = new List<string>();
        foreach (var file in accepted.Files)
        {
            token.ThrowIfCancellationRequested();
            // A reinstalação pode ocorrer DURANTE a limpeza. Conferir novamente
            // antes de cada arquivo é deliberadamente mais lento, porém bloqueia
            // exclusão de uma nova instalação iniciada depois da prévia.
            var gate = await inventory();
            token.ThrowIfCancellationRequested();
            if (!gate.Complete || !NoSevenZipRegistrations(gate.Items) || !NoSevenZipProcesses())
            {
                preserved += accepted.Files.Count - deleted + accepted.Directories.Count + accepted.Registry.Count;
                return new(deleted, directories, keys, failed, preserved, true)
                    { Problems = ["instalacao_alterada"] };
            }
            string stage = "verificar_caminho";
            try
            {
                if (!Roots().Any(root => Within(root, file.Path)) || !SafeDirectory(Path.GetDirectoryName(file.Path)!) ||
                    (File.GetAttributes(file.Path) & FileAttributes.ReparsePoint) != 0)
                { failed++; problems.Add("arquivo_caminho_invalido"); continue; }
                stage = "abrir_verificacao";
                using (var handle = InstallerFileGuard.OpenResidual(file.Path, deleteAccess: true))
                {
                    stage = "verificar_conteudo";
                    var identity = InstallerFileGuard.ReadIdentity(handle);
                    if (identity != file.Identity || identity.Links != 1 || (long)identity.Size != file.Size ||
                        !Convert.ToHexString(SHA256.HashData(handle)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    { failed++; problems.Add("arquivo_conteudo_alterado"); continue; }
                    stage = "excluir";
                    token.ThrowIfCancellationRequested();
                    InstallerFileGuard.MarkResidualForDeletion(handle);
                }
                stage = "confirmar_exclusao";
                if (!File.Exists(file.Path)) deleted++;
                else { failed++; problems.Add("arquivo_permaneceu"); }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException)
            { failed++; problems.Add(ProblemCode("arquivo_" + stage, ex)); }
        }
        // Só diretórios VAZIOS. Se um item novo aparecer, jamais apagar recursivamente.
        foreach (string dir in accepted.Directories.OrderByDescending(x => x.Length))
        {
            token.ThrowIfCancellationRequested();
            var directoryGate = await inventory();
            token.ThrowIfCancellationRequested();
            if (!directoryGate.Complete || !NoSevenZipRegistrations(directoryGate.Items) || !NoSevenZipProcesses())
                return new(deleted, directories, keys, failed, preserved + accepted.Directories.Count - directories +
                    accepted.Registry.Count, true) { Problems = ["instalacao_alterada"] };
            try
            {
                using var parentLease = InstallerFileGuard.PinDirectory(Path.GetDirectoryName(dir)!, allowLeafWrites: true);
                if (Roots().Any(root => Within(root, dir)) && SafeDirectory(dir) &&
                    !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir, recursive: false);
                    directories++;
                }
                else { preserved++; problems.Add("pasta_nao_vazia_ou_alterada"); }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException)
            { failed++; problems.Add(ProblemCode("pasta", ex)); }
        }
        // Não apagar chaves se nenhum arquivo foi removido por falha de integridade.
        if (failed == 0 && preserved == 0)
        {
            var recheck = await inventory();
            token.ThrowIfCancellationRequested();
            if (recheck.Complete && NoSevenZipRegistrations(recheck.Items) && NoSevenZipProcesses())
                foreach (var key in accepted.Registry)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(key.Hive, RegistryView.Registry64);
                        int separator = key.SubKey.LastIndexOf('\\');
                        if (separator <= 0 || separator == key.SubKey.Length - 1)
                        { failed++; problems.Add("registro_subchave_invalida"); continue; }
                        using var parent = baseKey.OpenSubKey(key.SubKey[..separator], writable: true);
                        if (parent is null) { failed++; problems.Add("registro_pai_ausente"); continue; }
                        string leaf = key.SubKey[(separator + 1)..];
                        using var target = parent.OpenSubKey(leaf, writable: false);
                        if (target is null || !TryRegistryFingerprint(target, out var digest, out var entries) ||
                            entries != key.Entries || digest != key.Sha256)
                        { failed++; problems.Add("registro_conteudo_alterado"); continue; }
                        target.Close();
                        token.ThrowIfCancellationRequested();
                        parent.DeleteSubKeyTree(leaf, throwOnMissingSubKey: true);
                        using var remaining = parent.OpenSubKey(leaf);
                        if (remaining is null) keys++;
                        else { failed++; problems.Add("registro_permaneceu"); }
                    }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                        ArgumentException or System.Security.SecurityException or InvalidOperationException)
                    { failed++; problems.Add(ProblemCode("registro", ex)); }
                }
        }
        else
        {
            preserved += accepted.Registry.Count;
            if (accepted.Registry.Count > 0) problems.Add("registro_preservado_apos_falha_anterior");
        }
        return new(deleted, directories, keys, failed, preserved, false)
            { Problems = problems.Distinct(StringComparer.Ordinal).ToArray() };
    }

    private static string ProblemCode(string stage, Exception exception)
    {
        // O HResult faz parte do tipo, nao do caminho nem do usuario.
        // Guardar apenas codigos de erro Win32 conhecidos; outros viram codigo generico.
        int code = exception.HResult & 0xffff;
        string error = exception switch
        {
            UnauthorizedAccessException or System.Security.SecurityException => "sem_permissao",
            IOException when code is 32 or 33 => "em_uso",
            IOException when code is 5 => "sem_permissao",
            IOException => "erro_entrada_saida",
            _ => "caminho_invalido"
        };
        string win32 = code is 2 or 3 or 5 or 19 or 32 or 33 or 80 or 87 or 183
            ? "_win32_" + code.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "";
        return stage + "_" + error + win32;
    }

    private static bool Equivalent(SevenZipCleanupPlan a, SevenZipCleanupPlan b) =>
        a.Complete && b.Complete && a.TotalBytes == b.TotalBytes &&
        a.Files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(b.Files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)) &&
        a.Directories.ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(b.Directories) &&
        a.Registry.OrderBy(x => x.Hive).SequenceEqual(b.Registry.OrderBy(x => x.Hive));
}
