using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Piloto real de limpeza completa, EXCLUSIVO para 7-Zip EXE x64.
// A política só conhece três pastas de propriedade direta e duas chaves próprias.
// Sem exploração do perfil pessoal, do Registro MSI, de COM, do Windows ou de atalhos genéricos.
internal sealed record SevenZipFileEvidence(string Path, long Size, string Sha256);
internal sealed record SevenZipRegistryEvidence(RegistryHive Hive, string SubKey, string Sha256, int Entries);
internal sealed record SevenZipCleanupPlan(
    IReadOnlyList<SevenZipFileEvidence> Files,
    IReadOnlyList<string> Directories,
    IReadOnlyList<SevenZipRegistryEvidence> Registry,
    long TotalBytes, bool Complete, string Reason);
internal sealed record SevenZipCleanupOutcome(
    int FilesDeleted, int DirectoriesDeleted, int KeysDeleted,
    int Failures, int Preserved, bool Reinstalled);

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

    internal static bool NoSevenZipRegistrations(IEnumerable<ProgramEntry> current) =>
        !current.Any(p => Regex.IsMatch(p.Name,
            @"^7-Zip(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    internal static bool NoSevenZipProcesses() =>
        !new[] { "7zFM", "7zG", "7z", "7za", "7zr" }
            .Any(name => Process.GetProcessesByName(name).Length != 0);

    private static IReadOnlyList<string> Roots() =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7-Zip"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "7-Zip")
    ];

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
                if (!Directory.Exists(root)) continue;
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
                        using var handle = InstallerFileGuard.OpenRead(path, allowDirectoryWrites: true);
                        var identity = InstallerFileGuard.ReadIdentity(handle);
                        if (identity.Links != 1 || identity.Size > (ulong)MaxBytes ||
                            total + (long)identity.Size > MaxBytes)
                            return new([], [], [], 0, false, "Arquivo compartilhado ou limite de tamanho.");
                        total += (long)identity.Size;
                        string digest = Convert.ToHexString(SHA256.HashData(handle)).ToLowerInvariant();
                        files.Add(new(path, (long)identity.Size, digest));
                        if (files.Count + directories.Count > MaxItems)
                            return new([], [], [], 0, false, "Limite de arquivos atingido.");
                    }
                }
            }
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
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

    private static bool TryRegistryFingerprint(RegistryKey root, out string digest, out int entries)
    {
        digest = ""; entries = 0;
        try
        {
            var builder = new StringBuilder();
            void Walk(RegistryKey node, int depth)
            {
                if (depth > 12 || ++entries > MaxItems) throw new InvalidDataException("Registro fora dos limites");
                foreach (string name in node.GetValueNames().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    if (++entries > MaxItems) throw new InvalidDataException("Muitos valores");
                    object? value = node.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    string content = value switch
                    {
                        byte[] bytes when bytes.Length < 1024 * 1024 => Convert.ToHexString(bytes),
                        string[] strings => string.Join("\u001f", strings),
                        string s => s,
                        int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        null => "",
                        _ => throw new InvalidDataException("Tipo de valor inesperado")
                    };
                    builder.Append("V:").Append(name).Append(':').Append(node.GetValueKind(name))
                        .Append(':').Append(content.Length).Append(':').Append(content).Append('\n');
                }
                foreach (string name in node.GetSubKeyNames().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    builder.Append("K:").Append(name).Append('\n');
                    using var child = node.OpenSubKey(name, writable: false);
                    if (child is null) throw new IOException("Chave mudou");
                    Walk(child, depth + 1);
                }
            }
            Walk(root, 0);
            digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
                .ToLowerInvariant();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException or InvalidDataException)
        { return false; }
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
        var before = await inventory();
        if (!before.Complete || !NoSevenZipRegistrations(before.Items) || !NoSevenZipProcesses())
            return new(0, 0, 0, 1, accepted.Files.Count, true);
        var now = Capture(previous, before.Complete, before.Items, token);
        if (!Equivalent(accepted, now))
            return new(0, 0, 0, 1, accepted.Files.Count, false);
        int deleted = 0, directories = 0, keys = 0, failed = 0, preserved = 0;
        foreach (var file in accepted.Files)
        {
            token.ThrowIfCancellationRequested();
            if (!NoSevenZipProcesses()) { preserved += accepted.Files.Count - deleted; break; }
            try
            {
                if (!Roots().Any(root => Within(root, file.Path)) || !SafeDirectory(Path.GetDirectoryName(file.Path)!) ||
                    (File.GetAttributes(file.Path) & FileAttributes.ReparsePoint) != 0)
                { failed++; continue; }
                using (var handle = InstallerFileGuard.OpenRead(file.Path, allowDirectoryWrites: true))
                {
                    var identity = InstallerFileGuard.ReadIdentity(handle);
                    if (identity.Links != 1 || (long)identity.Size != file.Size ||
                        !Convert.ToHexString(SHA256.HashData(handle)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    { failed++; continue; }
                }
                File.Delete(file.Path);
                if (!File.Exists(file.Path)) deleted++; else failed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException)
            { failed++; }
        }
        // Só diretórios VAZIOS. Se um item novo aparecer, jamais apagar recursivamente.
        foreach (string dir in accepted.Directories.OrderByDescending(x => x.Length))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (Roots().Any(root => Within(root, dir)) && SafeDirectory(dir) &&
                    !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir, recursive: false);
                    directories++;
                }
                else preserved++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or System.Security.SecurityException)
            { failed++; }
        }
        // Não apagar chaves se nenhum arquivo foi removido por falha de integridade.
        if (failed == 0 && preserved == 0)
        {
            var recheck = await inventory();
            if (recheck.Complete && NoSevenZipRegistrations(recheck.Items) && NoSevenZipProcesses())
                foreach (var key in accepted.Registry)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(key.Hive, RegistryView.Registry64);
                        using var target = baseKey.OpenSubKey(key.SubKey, writable: false);
                        if (target is null || !TryRegistryFingerprint(target, out var digest, out var entries) ||
                            entries != key.Entries || digest != key.Sha256)
                        { failed++; continue; }
                        target.Close();
                        baseKey.DeleteSubKeyTree(key.SubKey, throwOnMissingSubKey: true);
                        if (baseKey.OpenSubKey(key.SubKey) is null) keys++; else failed++;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                        ArgumentException or System.Security.SecurityException or InvalidOperationException)
                    { failed++; }
                }
        }
        else preserved += accepted.Registry.Count;
        return new(deleted, directories, keys, failed, preserved, false);
    }

    private static bool Equivalent(SevenZipCleanupPlan a, SevenZipCleanupPlan b) =>
        a.Complete && b.Complete && a.TotalBytes == b.TotalBytes &&
        a.Files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(b.Files.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)) &&
        a.Directories.ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(b.Directories) &&
        a.Registry.OrderBy(x => x.Hive).SequenceEqual(b.Registry.OrderBy(x => x.Hive));
}
