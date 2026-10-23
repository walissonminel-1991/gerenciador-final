using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace GerenciadorAplicativos;

// Escopo nominal EXATO, somente após a ausência comprovada da instalação.
// O usuário escolhe eliminar configurações do aplicativo em uma prévia integral.
// Não percorre Documentos/Downloads/Imagens/Músicas, MuiCache, HKCR, Windows nem
// outros perfis. Não realiza limpeza automática durante a investigação.
internal sealed record SmallAppFile(string Path, long Bytes, string Digest);
internal sealed record SmallAppKey(RegistryHive Hive, RegistryView View, string Subkey,
    string Digest, int Entries);
internal sealed record SmallAppSnapshot(string Product, IReadOnlyList<SmallAppFile> Files,
    IReadOnlyList<string> Directories, IReadOnlyList<SmallAppKey> Keys,
    bool Complete, string Reason);
internal sealed record SmallAppCleanupResult(int Files, int Directories, int Keys,
    int Failed, int Preserved, IReadOnlyList<string> Problems);

internal static class SmallAppResidualCleanup
{
    private const int MaxEntries = 800;
    private const long MaxBytes = 192L * 1024 * 1024;
    private const string Npp = "Notepad++";
    private const string Sumatra = "SumatraPDF";
#if GF_PROGRESSIVE
    internal static string[]? SyntheticFolders;
    internal static bool SyntheticNoRegistry;
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
            : p.Name.Equals(Sumatra, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ProcessesClosed(string product)
    {
        string[] names = product.Equals(Npp, StringComparison.OrdinalIgnoreCase)
            ? ["notepad++"] : ["SumatraPDF"];
        return names.All(name => Process.GetProcessesByName(name).Length == 0);
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

    private static bool ReadKeyHash(RegistryKey root, out string hash, out int entries)
    {
        hash = ""; entries = 0;
        int count = 0;
        try
        {
            var sb = new StringBuilder();
            void Visit(RegistryKey current, int depth)
            {
                if (depth > 12 || ++count > MaxEntries)
                    throw new InvalidDataException("Registro excedeu limites");
                foreach (string name in current.GetValueNames().Order(StringComparer.OrdinalIgnoreCase))
                {
                    if (++count > MaxEntries) throw new InvalidDataException("Muitos valores");
                    object? value = current.GetValue(name, null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);
                    string text = value switch
                    {
                        byte[] bytes when bytes.Length <= 1024 * 1024 => Convert.ToHexString(bytes),
                        string s => s,
                        string[] arr => string.Join('\u001f', arr),
                        int n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        null => "",
                        _ => throw new InvalidDataException("Tipo ou tamanho não suportado")
                    };
                    if (text.Length > 1024 * 1024 || sb.Length + text.Length > 4 * 1024 * 1024)
                        throw new InvalidDataException("Valor grande demais");
                    sb.Append("V").Append(name).Append(':').Append(current.GetValueKind(name))
                        .Append(':').Append(text.Length).Append(':').Append(text).Append('\n');
                }
                foreach (string name in current.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase))
                {
                    sb.Append("K").Append(name).Append('\n');
                    using var child = current.OpenSubKey(name, writable: false);
                    if (child is null) throw new IOException("Chave mudou");
                    Visit(child, depth + 1);
                }
            }
            Visit(root, 0);
            entries = count;
            hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or InvalidDataException)
        { return false; }
    }

    internal static SmallAppSnapshot Capture(string product, bool inventoryComplete,
        IReadOnlyCollection<ProgramEntry> apps, CancellationToken token = default)
    {
        if (!Supported(product) || !inventoryComplete || !AbsenceConfirmed(product, apps) ||
            !ProcessesClosed(product))
            return new(product, [], [], [], false,
                "Produto não identificado, instalação ainda presente, inventário parcial ou processo ativo.");
        var files = new List<SmallAppFile>();
        var directories = new List<string>();
        var keys = new List<SmallAppKey>();
        long bytes = 0;
        try
        {
            foreach (string root in Folders(product).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                if (!Directory.Exists(root)) continue;
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
                        using var handle = InstallerFileGuard.OpenRead(path, allowDirectoryWrites: true);
                        var identity = InstallerFileGuard.ReadIdentity(handle);
                        if (identity.Links != 1 || identity.Size > (ulong)MaxBytes ||
                            bytes > MaxBytes - (long)identity.Size)
                            return new(product, [], [], [], false, "Arquivo compartilhado ou grande demais.");
                        bytes += (long)identity.Size;
                        files.Add(new(path, (long)identity.Size,
                            Convert.ToHexString(SHA256.HashData(handle))));
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
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
        var current = await inventory();
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
            if (!check.Complete || !AbsenceConfirmed(accepted.Product, check.Apps) ||
                !ProcessesClosed(accepted.Product))
            {
                preserved += accepted.Files.Count - deleted;
                problems.Add("instalacao_alterada");
                break;
            }
            try
            {
                if (!Folders(accepted.Product).Any(root => Under(root, file.Path)) ||
                    !SafeDir(Path.GetDirectoryName(file.Path)!) ||
                    (File.GetAttributes(file.Path) & FileAttributes.ReparsePoint) != 0)
                { failed++; problems.Add("caminho_alterado"); continue; }
                using (var handle = InstallerFileGuard.OpenRead(file.Path, allowDirectoryWrites: true))
                {
                    var identity = InstallerFileGuard.ReadIdentity(handle);
                    if (identity.Links != 1 || (long)identity.Size != file.Bytes ||
                        Convert.ToHexString(SHA256.HashData(handle)) != file.Digest)
                    { failed++; problems.Add("arquivo_alterado"); continue; }
                }
                File.Delete(file.Path);
                if (File.Exists(file.Path)) { failed++; problems.Add("arquivo_permaneceu"); }
                else deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException)
            { failed++; problems.Add("arquivo_" + ex.GetType().Name); }
        }
        foreach (string dir in accepted.Directories.OrderByDescending(x => x.Length))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (Folders(accepted.Product).Any(root => Under(root, dir)) && SafeDir(dir) &&
                    !Directory.EnumerateFileSystemEntries(dir).Any())
                { Directory.Delete(dir, recursive: false); folders++; }
                else { preserved++; problems.Add("pasta_nao_vazia"); }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException)
            { failed++; problems.Add("pasta_" + ex.GetType().Name); }
        }
        if (failed == 0 && preserved == 0)
        {
            foreach (var item in accepted.Keys)
            {
                token.ThrowIfCancellationRequested();
                var check = await inventory();
                if (!check.Complete || !AbsenceConfirmed(accepted.Product, check.Apps) ||
                    !ProcessesClosed(accepted.Product))
                { preserved++; problems.Add("instalacao_alterada"); break; }
                try
                {
                    if (!Keys(accepted.Product).Any(x => x.Hive == item.Hive &&
                        x.View == item.View && x.Subkey == item.Subkey))
                    { failed++; problems.Add("registro_fora_escopo"); continue; }
                    using var root = RegistryKey.OpenBaseKey(item.Hive, item.View);
                    using var key = root.OpenSubKey(item.Subkey, writable: false);
                    if (key is null || !ReadKeyHash(key, out string digest, out int entries) ||
                        digest != item.Digest || entries != item.Entries)
                    { failed++; problems.Add("registro_alterado"); continue; }
                    key.Close();
                    root.DeleteSubKeyTree(item.Subkey, throwOnMissingSubKey: true);
                    if (root.OpenSubKey(item.Subkey) is null) keys++;
                    else { failed++; problems.Add("registro_permaneceu"); }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                    ArgumentException or System.Security.SecurityException or InvalidOperationException)
                { failed++; problems.Add("registro_" + ex.GetType().Name); }
            }
        }
        else preserved += accepted.Keys.Count;
        return new(deleted, folders, keys, failed, preserved,
            problems.Distinct(StringComparer.Ordinal).ToArray());
    }
}
