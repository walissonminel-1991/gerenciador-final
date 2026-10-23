using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;

namespace GerenciadorAplicativos;

// Escopo nominal EXATO, somente após a ausência comprovada da instalação.
// O usuário escolhe eliminar configurações do aplicativo em uma prévia integral.
// Não percorre Documentos/Downloads/Imagens/Músicas, MuiCache, HKCR, Windows nem
// outros perfis. Não realiza limpeza automática durante a investigação.
internal sealed record SmallAppFile(string Path, long Bytes, string Digest, InstallerFileGuard.FileIdentity Identity);
internal sealed record SmallAppKey(RegistryHive Hive, RegistryView View, string Subkey,
    string Digest, int Entries);
internal sealed record SmallAppSnapshot(string Product, IReadOnlyList<SmallAppFile> Files,
    IReadOnlyList<string> Directories, IReadOnlyList<SmallAppKey> Keys,
    bool Complete, string Reason);
internal sealed record SmallAppCleanupResult(int Files, int Directories, int Keys,
    int Failed, int Preserved, IReadOnlyList<string> Problems);

internal static class SmallAppResidualCleanup
{
    // Orientacao legivel para falha de permissao; nunca solicita elevacao,
    // altera ACL ou transforma um item preservado em sucesso.
    internal static string ExplainAccessFailure(IReadOnlyList<string> problems) =>
        problems.Any(p => p.StartsWith("registro_abrir_pai_SecurityException_", StringComparison.Ordinal) ||
                          p.StartsWith("registro_abrir_pai_UnauthorizedAccessException_", StringComparison.Ordinal))
            ? "O Windows negou permissao de escrita na chave-pai do Registro nesta sessao. " +
              "A chave foi preservada. O GF nao modifica permissoes nem solicita " +
              "privilegios de administrador automaticamente."
            : "";
    // Consulta somente a possibilidade de abrir a chave-pai para escrita,
    // sem escrever nem modificar permissões. Usar antes de oferecer a
    // confirmação quando o único resíduo específico é o Registro.
    // Não substitui revalidação de identidade/conteúdo na exclusão.
    internal static string? RegistryWritePreflight(SmallAppSnapshot snapshot,
        Func<SmallAppKey, bool>? canOpenParent = null)
    {
        if (!snapshot.Complete || !Supported(snapshot.Product) ||
            snapshot.Files.Count != 0 || snapshot.Directories.Count != 0 ||
            snapshot.Keys.Count == 0)
            return null;

        foreach (var key in snapshot.Keys)
        {
            bool writable = false;
            try
            {
                if (canOpenParent is not null)
                    writable = canOpenParent(key);
                else
                {
                    int separator = key.Subkey.LastIndexOf('\\');
                    if (separator <= 0 || separator == key.Subkey.Length - 1)
                        return "Chave residual com caminho inválido. Nenhuma exclusão tentada.";
                    using var root = RegistryKey.OpenBaseKey(key.Hive, key.View);
                    using var parent = root.OpenSubKey(key.Subkey[..separator], writable: true);
                    writable = parent is not null;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException or InvalidOperationException)
            {
                writable = false;
            }
            if (!writable)
            {
                string location = key.Hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
                string view = key.View == RegistryView.Registry32 ? "32 bits" : "64 bits";
                return $"Foi encontrada {snapshot.Keys.Count} chave(s) exclusiva(s) de Registro. " +
                    $"O Windows não concedeu acesso de escrita à chave-pai em {location} ({view}) " +
                    "nesta sessão. Não serão repetidas tentativas de exclusão sem essa permissão. " +
                    "Os resíduos permanecem preservados; o GF não eleva privilégios nem altera ACLs. " +
                    "A consulta não comprova que o aplicativo está instalado.";
            }
        }
        return null;
    }

    private const int MaxEntries = 800;
    private const long MaxBytes = 192L * 1024 * 1024;
    private const string Npp = "Notepad++";
    private const string Sumatra = "SumatraPDF";
#if GF_PROGRESSIVE
    internal static string[]? SyntheticFolders;
    internal static bool SyntheticNoRegistry;
    internal static Action<string>? SyntheticBeforeFileDelete;
    internal static (RegistryHive Hive, RegistryView View, string Subkey)[]? SyntheticRegistryKeys;
#endif

    internal static bool Supported(string? product) =>
        product is not null && (product.Equals(Npp, StringComparison.OrdinalIgnoreCase) ||
                                product.Equals(Sumatra, StringComparison.OrdinalIgnoreCase));

    private static string[] Folders(string product)
    {
#if GF_PROGRESSIVE
        if (SyntheticFolders is not null) return SyntheticFolders;
#endif
        if (product.Equals(Npp, StringComparison.OrdinalIgnoreCase))
            return
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Npp),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Npp),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Npp),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Npp)
            ];
        return
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Sumatra),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Sumatra),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Sumatra),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Sumatra)
        ];
    }

    private static (RegistryHive Hive, RegistryView View, string Subkey)[] Keys(string product)
    {
#if GF_PROGRESSIVE
        if (SyntheticNoRegistry) return [];
        if (SyntheticRegistryKeys is not null) return SyntheticRegistryKeys;
#endif
        if (product.Equals(Npp, StringComparison.OrdinalIgnoreCase))
            return
            [
                (RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Notepad++"),
                (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Notepad++"),
                (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Notepad++")
            ];
        return
        [
            (RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\SumatraPDF"),
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\SumatraPDF"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\SumatraPDF")
        ];
    }

    internal static bool AbsenceConfirmed(string product, IReadOnlyCollection<ProgramEntry> apps)
    {
        // Um perfil existente jamais autoriza desinstalar o próprio aplicativo.
        return !apps.Any(p => product.Equals(Npp, StringComparison.OrdinalIgnoreCase)
            ? p.Name.StartsWith(Npp, StringComparison.OrdinalIgnoreCase)
            : p.Name.Equals(Sumatra, StringComparison.OrdinalIgnoreCase) ||
              p.Name.StartsWith("SumatraPDF ", StringComparison.OrdinalIgnoreCase));
    }

    private static bool UnregisteredExecutableFound(string product)
    {
#if GF_PROGRESSIVE
        if (SyntheticFolders is not null) return false;
#endif
        string binary = product.Equals(Npp, StringComparison.OrdinalIgnoreCase)
            ? "notepad++.exe" : "SumatraPDF.exe";
        return Folders(product).Any(folder => File.Exists(Path.Combine(folder, binary)));
    }

    private static bool ProcessesClosed(string product)
    {
        string[] names = product.Equals(Npp, StringComparison.OrdinalIgnoreCase)
            ? ["notepad++"] : ["SumatraPDF"];
        try
        {
            foreach (string name in names)
            {
                var processes = Process.GetProcessesByName(name);
                try { if (processes.Length != 0) return false; }
                finally { foreach (var process in processes) process.Dispose(); }
            }
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or
            InvalidOperationException or UnauthorizedAccessException) { return false; }
    }

    private static bool Under(string root, string path)
    {
        string first = Path.GetFullPath(root).TrimEnd('\\');
        string absolute = Path.GetFullPath(path);
        return absolute.Equals(first, StringComparison.OrdinalIgnoreCase) ||
            absolute.StartsWith(first + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SafeDir(string dir)
    {
        for (DirectoryInfo? item = new(dir); item is not null; item = item.Parent)
            if (!item.Exists || (item.Attributes & FileAttributes.ReparsePoint) != 0)
                return false;
        return true;
    }

    private static bool ReadKeyHash(RegistryKey root, out string hash, out int entries) =>
        RegistryTreeFingerprint.TryCapture(root, MaxEntries, out hash, out entries);

    internal static SmallAppSnapshot Capture(string product, bool inventoryComplete,
        IReadOnlyCollection<ProgramEntry> apps, CancellationToken token = default)
    {
        if (!Supported(product) || !inventoryComplete || !AbsenceConfirmed(product, apps) ||
            !ProcessesClosed(product) || UnregisteredExecutableFound(product))
            return new(product, [], [], [], false,
                "Produto não identificado, instalação/EXE ainda presente, inventário parcial ou processo ativo.");
        var files = new List<SmallAppFile>();
        var directories = new List<string>();
        var keys = new List<SmallAppKey>();
        long bytes = 0;
        try
        {
            foreach (string root in Folders(product).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                if (!ResidualPersonalDataBoundary.IsOutside(root))
                    return new(product, [], [], [], false, "Pasta pessoal sobreposta ou limites pessoais nao verificaveis.");
                try { _ = File.GetAttributes(root); }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { continue; }
                if (!SafeDir(root))
                    return new(product, [], [], [], false, "Diretório redirecionado ou inacessível.");
                var pending = new Stack<string>();
                pending.Push(root);
                while (pending.TryPop(out string? dir))
                {
                    token.ThrowIfCancellationRequested();
                    if (!Under(root, dir) || !SafeDir(dir))
                        return new(product, [], [], [], false, "Pasta mudou ou contém link.");
                    directories.Add(dir);
                    if (directories.Count + files.Count > MaxEntries)
                        return new(product, [], [], [], false, "Limite de pastas atingido.");
                    foreach (string path in Directory.EnumerateFileSystemEntries(dir))
                    {
                        token.ThrowIfCancellationRequested();
                        if (!Under(root, path) ||
                            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                            return new(product, [], [], [], false, "Link ou caminho não autorizado.");
                        if (Directory.Exists(path)) { pending.Push(path); continue; }
                        if (!File.Exists(path))
                            return new(product, [], [], [], false, "Objeto desconhecido na pasta.");
                        using var handle = InstallerFileGuard.OpenResidual(path);
                        var identity = InstallerFileGuard.ReadIdentity(handle);
                        if (identity.Links != 1 || identity.Size > (ulong)MaxBytes ||
                            bytes > MaxBytes - (long)identity.Size)
                            return new(product, [], [], [], false, "Arquivo compartilhado ou grande demais.");
                        bytes += (long)identity.Size;
                        files.Add(new(path, (long)identity.Size,
                            Convert.ToHexString(SHA256.HashData(handle)), identity));
                        if (directories.Count + files.Count > MaxEntries)
                            return new(product, [], [], [], false, "Limite de arquivos atingido.");
                    }
                }
            }
            foreach (var k in Keys(product))
            {
                token.ThrowIfCancellationRequested();
                using var hive = RegistryKey.OpenBaseKey(k.Hive, k.View);
                using var key = hive.OpenSubKey(k.Subkey, writable: false);
                if (key is null) continue;
                if (!ReadKeyHash(key, out string digest, out int entryCount))
                    return new(product, [], [], [], false, "Registro não verificável.");
                keys.Add(new(k.Hive, k.View, k.Subkey, digest, entryCount));
            }
            return new(product, files.AsReadOnly(), directories.AsReadOnly(), keys.AsReadOnly(),
                true, "Prévia completa dos locais exclusivos. Exclusão somente após confirmação.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        { return new(product, [], [], [], false, "Consulta interrompida: " + ex.GetType().Name); }
    }

    private static bool Equivalent(SmallAppSnapshot a, SmallAppSnapshot b) =>
        a.Complete && b.Complete &&
        a.Product.Equals(b.Product, StringComparison.OrdinalIgnoreCase) &&
        a.Files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(b.Files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)) &&
        a.Directories.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(b.Directories) &&
        a.Keys.OrderBy(x => x.Hive).ThenBy(x => x.View).SequenceEqual(
            b.Keys.OrderBy(x => x.Hive).ThenBy(x => x.View));

    internal static ResidualAuditReport? Report(SmallAppSnapshot snapshot)
    {
        if (!snapshot.Complete ||
            snapshot.Files.Count + snapshot.Directories.Count + snapshot.Keys.Count == 0)
            return null;
        var findings = snapshot.Files.Select(file => new ResidualAuditFinding(
                file.Path, "Arquivo próprio — conferir limpeza", 1,
                "Alvo específico com integridade validada. Remoção exige autorização separada.",
                ResidualDisposition.Review, ResidualSource.Installation))
            .Concat(snapshot.Directories.Select(dir => new ResidualAuditFinding(
                dir, "Pasta própria — conferir limpeza", 1,
                "Pasta dedicada ao produto; pastas não vazias não são excluídas recursivamente sem prévia.",
                ResidualDisposition.Review, ResidualSource.UserData)))
            .Concat(snapshot.Keys.Select(key => new ResidualAuditFinding(
                (key.Hive == RegistryHive.CurrentUser ? @"HKEY_CURRENT_USER\" : @"HKEY_LOCAL_MACHINE\") +
                (key.View == RegistryView.Registry32 ? "(32 bits)\\" : "(64 bits)\\") + key.Subkey,
                "Configuração própria — conferir limpeza", key.Entries,
                "Chave própria exclusiva; conteúdo resumido por SHA-256, sem remover áreas compartilhadas.",
                ResidualDisposition.Review, ResidualSource.Registry)))
            .ToArray();
        return new ResidualAuditReport(snapshot.Product, true, findings,
            Array.Empty<string>(), Historical: true);
    }

    internal static string Describe(SmallAppSnapshot snapshot)
    {
        if (!snapshot.Complete) return "Limpeza bloqueada: " + snapshot.Reason;
        return "APLICATIVO: " + snapshot.Product +
            "\nApenas os locais específicos abaixo serão removidos mediante autorização.\n" +
            "\nPASTAS:\n" + string.Join("\n", snapshot.Directories.Order(StringComparer.OrdinalIgnoreCase)) +
            "\n\nARQUIVOS:\n" + string.Join("\n", snapshot.Files
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Path + " (" + x.Bytes + " bytes)")) +
            "\n\nCHAVES DO REGISTRO:\n" + string.Join("\n", snapshot.Keys.Select(k =>
                (k.Hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM") +
                (k.View == RegistryView.Registry32 ? " (32 bits)\\" : " (64 bits)\\") + k.Subkey)) +
            "\n\nNão inclui arquivos pessoais em Documentos, Downloads, Imagens ou Músicas, " +
            "nem MuiCache, outros produtos, HKCR ou componentes compartilhados do Windows.";
    }

    internal static async Task<SmallAppCleanupResult> DeleteAsync(
        SmallAppSnapshot accepted,
        Func<Task<(bool Complete, IReadOnlyCollection<ProgramEntry> Apps)>> inventory,
        CancellationToken token = default)
    {
        if (!accepted.Complete || !Supported(accepted.Product))
            return new(0, 0, 0, 1, 0, ["previa_invalida"]);
        token.ThrowIfCancellationRequested();
        var current = await inventory();
        token.ThrowIfCancellationRequested();
        if (!current.Complete || !AbsenceConfirmed(accepted.Product, current.Apps) ||
            !ProcessesClosed(accepted.Product))
            return new(0, 0, 0, 1, accepted.Files.Count, ["produto_presente"]);
        var latest = Capture(accepted.Product, current.Complete, current.Apps, token);
        if (!Equivalent(accepted, latest))
            return new(0, 0, 0, 1, accepted.Files.Count, ["previa_alterada"]);
        int deleted = 0, folders = 0, keys = 0, failed = 0, preserved = 0;
        var problems = new List<string>();
        foreach (var file in accepted.Files)
        {
            token.ThrowIfCancellationRequested();
            var check = await inventory();
            token.ThrowIfCancellationRequested();
            if (!check.Complete || !AbsenceConfirmed(accepted.Product, check.Apps) ||
                !ProcessesClosed(accepted.Product))
            {
                preserved += accepted.Files.Count - deleted + accepted.Directories.Count + accepted.Keys.Count;
                problems.Add("instalacao_alterada");
                return new(deleted, folders, keys, failed, preserved, problems);
            }
            try
            {
                if (!Folders(accepted.Product).Any(root => Under(root, file.Path)) ||
                    !ResidualPersonalDataBoundary.IsOutside(file.Path) ||
                    !SafeDir(Path.GetDirectoryName(file.Path)!) ||
                    (File.GetAttributes(file.Path) & FileAttributes.ReparsePoint) != 0)
                { failed++; problems.Add("caminho_alterado"); continue; }
                using (var handle = InstallerFileGuard.OpenResidual(file.Path, deleteAccess: true))
                {
                    var identity = InstallerFileGuard.ReadIdentity(handle);
                    if (identity != file.Identity || identity.Links != 1 || (long)identity.Size != file.Bytes ||
                        Convert.ToHexString(SHA256.HashData(handle)) != file.Digest)
                    { failed++; problems.Add("arquivo_alterado"); continue; }
#if GF_PROGRESSIVE
                    SyntheticBeforeFileDelete?.Invoke(file.Path);
#endif
                    token.ThrowIfCancellationRequested();
                    InstallerFileGuard.MarkResidualForDeletion(handle);
                }
                if (File.Exists(file.Path)) { failed++; problems.Add("arquivo_permaneceu"); }
                else deleted++;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException)
            { failed++; problems.Add("arquivo_" + ex.GetType().Name); }
        }
        foreach (string dir in accepted.Directories.OrderByDescending(x => x.Length))
        {
            token.ThrowIfCancellationRequested();
            var check = await inventory();
            token.ThrowIfCancellationRequested();
            if (!check.Complete || !AbsenceConfirmed(accepted.Product, check.Apps) ||
                !ProcessesClosed(accepted.Product) || UnregisteredExecutableFound(accepted.Product))
                return new(deleted, folders, keys, failed, preserved + accepted.Directories.Count - folders +
                    accepted.Keys.Count, ["instalacao_alterada"]);
            try
            {
                using var parentLease = InstallerFileGuard.PinDirectory(Path.GetDirectoryName(dir)!, allowLeafWrites: true);
                if (Folders(accepted.Product).Any(root => Under(root, dir)) &&
                    ResidualPersonalDataBoundary.IsOutside(dir) && SafeDir(dir) &&
                    !Directory.EnumerateFileSystemEntries(dir).Any())
                { Directory.Delete(dir, recursive: false); folders++; }
                else { preserved++; problems.Add("pasta_nao_vazia"); }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException)
            { failed++; problems.Add("pasta_" + ex.GetType().Name); }
        }
        if (failed == 0 && preserved == 0)
        {
            foreach (var item in accepted.Keys)
            {
                token.ThrowIfCancellationRequested();
                var check = await inventory();
                token.ThrowIfCancellationRequested();
                if (!check.Complete || !AbsenceConfirmed(accepted.Product, check.Apps) ||
                    !ProcessesClosed(accepted.Product))
                { preserved++; problems.Add("instalacao_alterada"); break; }
                string phase = "validar";
                try
                {
                    if (!Keys(accepted.Product).Any(x => x.Hive == item.Hive &&
                        x.View == item.View && x.Subkey == item.Subkey))
                    { failed++; problems.Add("registro_fora_escopo"); continue; }
                    // Excluir somente a subchave autorizada, abrindo a chave-pai
                    // na visao 32/64 bits correta. Nunca excluir da raiz HKLM.
                    int separator = item.Subkey.LastIndexOf('\\');
                    if (separator <= 0 || separator == item.Subkey.Length - 1)
                    { failed++; problems.Add("registro_subchave_invalida"); continue; }
                    string parentPath = item.Subkey[..separator];
                    string leaf = item.Subkey[(separator + 1)..];
                    phase = "abrir_pai";
                    using var root = RegistryKey.OpenBaseKey(item.Hive, item.View);
                    using var parent = root.OpenSubKey(parentPath, writable: true);
                    if (parent is null)
                    { failed++; problems.Add("registro_pai_ausente"); continue; }
                    phase = "revalidar";
                    using var key = parent.OpenSubKey(leaf, writable: false);
                    if (key is null || !ReadKeyHash(key, out string digest, out int entries) ||
                        digest != item.Digest || entries != item.Entries)
                    { failed++; problems.Add("registro_alterado"); continue; }
                    key.Close();
                    phase = "excluir";
                    token.ThrowIfCancellationRequested();
                    parent.DeleteSubKeyTree(leaf, throwOnMissingSubKey: true);
                    phase = "conferir";
                    using var remaining = parent.OpenSubKey(leaf, writable: false);
                    if (remaining is null) keys++;
                    else { failed++; problems.Add("registro_permaneceu"); }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                    ArgumentException or System.Security.SecurityException or InvalidOperationException)
                {
                    failed++;
                    problems.Add("registro_" + phase + "_" + ex.GetType().Name +
                        "_hr" + ((uint)ex.HResult).ToString("X8"));
                }
            }
        }
        else preserved += accepted.Keys.Count;
        return new(deleted, folders, keys, failed, preserved,
            problems.Distinct(StringComparer.Ordinal).ToArray());
    }
}
