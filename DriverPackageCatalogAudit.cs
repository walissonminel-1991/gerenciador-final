using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace GerenciadorAplicativos;

internal enum DriverPackageCatalogState { Verified, Incomplete, Rejected, Unavailable }

internal sealed record DriverPackageCatalogResult(DriverPackageCatalogState State, int FilesObserved, int FilesCovered)
{
    // Evidência exclusivamente consultiva. Nunca conceder autoridade de escrita.
    internal bool CanInstall => false;
    internal bool CanMaintain => false;
    internal bool Complete => State == DriverPackageCatalogState.Verified;
}

// Completa a prova de INF assinada já existente: exige que CADA arquivo da
// pasta instalada esteja vinculado ao MESMO catálogo que o SetupAPI confiou.
// Não substitui DriverInfVerification, não aprova origem externa/compatibilidade.
internal static class DriverPackageCatalogAudit
{
    private const int MaxFiles = 256;
    private const int MaxEntries = 512;
    private const int MaxDepth = 4;
    private const long MaxFileBytes = 256L * 1024 * 1024;
    private const long MaxPackageBytes = 1024L * 1024 * 1024;
    private const int MaxMatchingCatalogs = 32;

    internal static DriverPackageCatalogResult Verify(DriverStoreEvidence mapping, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return new(DriverPackageCatalogState.Unavailable, 0, 0);
        token.ThrowIfCancellationRequested();

        // O vínculo nativo, a igualdade do INF publicado e a confiança no
        // catálogo são checados pelo motor EXISTENTE antes de olhar o payload.
        var inf = DriverInfVerification.Verify(mapping, token);
        if (inf.Trust == DriverInfTrust.Unavailable)
            return new(DriverPackageCatalogState.Unavailable, 0, 0);
        if (inf.Trust != DriverInfTrust.VerifiedInf || string.IsNullOrWhiteSpace(inf.Catalog))
            return new(DriverPackageCatalogState.Rejected, 0, 0);

        try
        {
            string root = Path.GetDirectoryName(mapping.StorePath) ?? "";
            if (root.Length == 0 || !Directory.Exists(root) || !Ordinary(root))
                return new(DriverPackageCatalogState.Incomplete, 0, 0);

            // A lista é exaustiva dentro de uma única pasta de pacote. Recusar
            // links, profundidade excessiva e limites, sem ignorar arquivos.
            var files = new List<string>();
            var pending = new Stack<(string Directory, int Depth)>();
            pending.Push((root, 0));
            int visited = 0;
            long totalBytes = 0;
            while (pending.Count != 0)
            {
                token.ThrowIfCancellationRequested();
                var (dir, depth) = pending.Pop();
                if (!Ordinary(dir))
                    return new(DriverPackageCatalogState.Incomplete, files.Count, 0);
                foreach (string entry in Directory.EnumerateFileSystemEntries(dir))
                {
                    token.ThrowIfCancellationRequested();
                    if (++visited > MaxEntries)
                        return new(DriverPackageCatalogState.Incomplete, files.Count, 0);
                    var attrs = File.GetAttributes(entry);
                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                        return new(DriverPackageCatalogState.Incomplete, files.Count, 0);
                    if ((attrs & FileAttributes.Directory) != 0)
                    {
                        if (depth >= MaxDepth)
                            return new(DriverPackageCatalogState.Incomplete, files.Count, 0);
                        pending.Push((entry, depth + 1));
                    }
                    else
                    {
                        long length = new FileInfo(entry).Length;
                        if (length > MaxFileBytes || checked(totalBytes + length) > MaxPackageBytes ||
                            files.Count >= MaxFiles)
                            return new(DriverPackageCatalogState.Incomplete, files.Count, 0);
                        totalBytes += length;
                        files.Add(entry);
                    }
                }
            }

            if (files.Count == 0 || !files.Any(f => Path.GetFullPath(f).Equals(
                    Path.GetFullPath(mapping.StorePath), StringComparison.OrdinalIgnoreCase)))
                return new(DriverPackageCatalogState.Rejected, files.Count, 0);

            // O catálogo retornado pelo SetupAPI já passou pela política de
            // assinatura do Windows. O conteúdo da cópia .cat no pacote,
            // quando existe, também deve ser idêntico a esse catálogo.
            byte[] trustedCatHash;
            using (var catalog = new FileStream(inf.Catalog, FileMode.Open, FileAccess.Read, FileShare.Read))
                trustedCatHash = SHA256.HashData(catalog);

            Guid driverPolicy = new("F750E6C3-38EE-11D1-85E5-00C04FC295EE");
            if (!CryptCATAdminAcquireContext2(out IntPtr admin, ref driverPolicy, "SHA256", IntPtr.Zero, 0))
                return new(DriverPackageCatalogState.Unavailable, files.Count, 0);
            try
            {
                int covered = 0;
                int localCatalogs = 0;
                var actualTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    token.ThrowIfCancellationRequested();
                    if (!Ordinary(file))
                        return new(DriverPackageCatalogState.Incomplete, files.Count, covered);
                    if (file.EndsWith(".cat", StringComparison.OrdinalIgnoreCase))
                    {
                        using var localCat = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(localCat), trustedCatHash))
                            return new(DriverPackageCatalogState.Rejected, files.Count, covered);
                        localCatalogs++;
                    }
                    else
                    {
                        if (!CatalogContainsFile(admin, file, inf.Catalog, token, out string tag))
                            return new(DriverPackageCatalogState.Rejected, files.Count, covered);
                        // Duplicados iguais impedem provar multiplicidade de nomes
                        // apenas pelos hashes presentes no catalogo.
                        if (!actualTags.Add(tag))
                            return new(DriverPackageCatalogState.Incomplete, files.Count, covered);
                    }
                    covered++;
                }
                if (localCatalogs != 1 || actualTags.Count == 0)
                    return new(DriverPackageCatalogState.Incomplete, files.Count, covered);
                var declared = ReadCatalogMemberTags(inf.Catalog, token);
                if (declared is null || !declared.SetEquals(actualTags))
                    return new(DriverPackageCatalogState.Incomplete, files.Count, covered);
                return new(DriverPackageCatalogState.Verified, files.Count, covered);
            }
            finally { CryptCATAdminReleaseContext(admin, 0); }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException or
            CryptographicException or OverflowException or DllNotFoundException or
            EntryPointNotFoundException)
        { return new(DriverPackageCatalogState.Unavailable, 0, 0); }
    }

    // Ponto de teste independente: exige membro do catálogo EXATO, não
    // assinatura de outro pacote ou igualdade de nomes comerciais.
    internal static bool IsCatalogMember(string file, string trustedCatalog, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return false;
        token.ThrowIfCancellationRequested();
        try
        {
            if (!Ordinary(file) || !Ordinary(trustedCatalog)) return false;
            Guid policy = new("F750E6C3-38EE-11D1-85E5-00C04FC295EE");
            if (!CryptCATAdminAcquireContext2(out IntPtr admin, ref policy, "SHA256", IntPtr.Zero, 0))
                return false;
            try { return CatalogContainsFile(admin, file, trustedCatalog, token, out _); }
            finally { CryptCATAdminReleaseContext(admin, 0); }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException or
            CryptographicException or DllNotFoundException or EntryPointNotFoundException)
        { return false; }
    }

    private static bool CatalogContainsFile(IntPtr admin, string file, string catalog, CancellationToken token,
        out string memberHash)
    {
        memberHash = "";
        token.ThrowIfCancellationRequested();
        using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaxFileBytes) return false;
        // SHA-256 do membro conforme SIP do Windows (não confundir com
        // SHA-256 bruto de um binário PE, que tem campos excluídos).
        uint size = 32;
        byte[] digest = new byte[32];
        if (!CryptCATAdminCalcHashFromFileHandle2(admin, input.SafeFileHandle, ref size, digest, 0)
            || size != 32) return false;

        IntPtr current = CryptCATAdminEnumCatalogFromHashFirst(admin, digest, size, 0, IntPtr.Zero);
        try
        {
            for (int i = 0; i < MaxMatchingCatalogs && current != IntPtr.Zero; i++)
            {
                token.ThrowIfCancellationRequested();
                var info = new CatalogInfo
                {
                    Size = (uint)Marshal.SizeOf<CatalogInfo>(),
                    CatalogFile = ""
                };
                if (CryptCATCatalogInfoFromContext(current, ref info, 0) &&
                    Path.GetFullPath(info.CatalogFile).Equals(Path.GetFullPath(catalog),
                        StringComparison.OrdinalIgnoreCase))
                {
                    memberHash = Convert.ToHexString(digest);
                    return true;
                }
                // A API recebe o handle anterior e avança a enumeração.
                IntPtr previous = current;
                current = CryptCATAdminEnumCatalogFromHashNext(admin, digest, size, 0, ref previous);
            }
            return false;
        }
        finally
        {
            if (current != IntPtr.Zero) CryptCATAdminReleaseCatalogContext(admin, current, 0);
        }
    }

    // Sem lista assinada COMPLETA nao ha sucesso: um arquivo ausente nao
    // aparece na enumeracao do diretorio e seria invisivel a uma simples
    // validacao dos arquivos presentes. Requer conjunto exato de tags SHA256.
    private static HashSet<string>? ReadCatalogMemberTags(string catalog, CancellationToken token)
    {
        IntPtr handle = CryptCATOpen(catalog, 0, IntPtr.Zero, 0x200, 0);
        if (handle == new IntPtr(-1))
            handle = CryptCATOpen(catalog, 0, IntPtr.Zero, 0x100, 0);
        if (handle == new IntPtr(-1) || handle == IntPtr.Zero) return null;
        try
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IntPtr member = IntPtr.Zero;
            for (int i = 0; i < MaxFiles; i++)
            {
                token.ThrowIfCancellationRequested();
                member = CryptCATEnumerateMember(handle, member);
                if (member == IntPtr.Zero) return tags.Count > 0 ? tags : null;
                var header = Marshal.PtrToStructure<CatalogMemberHeader>(member);
                string? tag = Marshal.PtrToStringUni(header.ReferenceTag);
                // A estrutura e os algoritmos nao sao interpretados por
                // semelhanca: catalogo sem tags SHA256 exatas e INCONCLUSIVO.
                if (tag is null || tag.Length != 64 || !tag.All(Uri.IsHexDigit) ||
                    !tags.Add(tag)) return null;
            }
            return null; // catalogo truncado pela cota nunca e "completo"
        }
        finally { CryptCATClose(handle); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CatalogMemberHeader
    {
        internal uint Size;
        internal IntPtr ReferenceTag;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CryptCATOpen(string file, uint flags, IntPtr provider,
        uint catalogVersion, uint encodingType);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr CryptCATEnumerateMember(IntPtr catalog, IntPtr previous);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATClose(IntPtr catalog);

    private static bool Ordinary(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.Contains('\0') || path.Split('\\', '/').Any(p => p is "." or ".."))
            return false;
        for (FileSystemInfo? item = (File.GetAttributes(path) & FileAttributes.Directory) != 0
                ? new DirectoryInfo(path) : new FileInfo(path);
            item is not null;
            item = item is FileInfo f ? f.Directory : ((DirectoryInfo)item).Parent)
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                return false;
        return true;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CatalogInfo
    {
        internal uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        internal string CatalogFile;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminAcquireContext2(out IntPtr handle,
        ref Guid subsystem, string algorithm, IntPtr strongHashPolicy, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminCalcHashFromFileHandle2(IntPtr admin,
        SafeFileHandle file, ref uint length, [Out] byte[] hash, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", EntryPoint = "CryptCATAdminEnumCatalogFromHash", ExactSpelling = true)]
    private static extern IntPtr CryptCATAdminEnumCatalogFromHashFirst(IntPtr admin,
        byte[] hash, uint length, uint flags, IntPtr previous);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", EntryPoint = "CryptCATAdminEnumCatalogFromHash", ExactSpelling = true)]
    private static extern IntPtr CryptCATAdminEnumCatalogFromHashNext(IntPtr admin,
        byte[] hash, uint length, uint flags, ref IntPtr previous);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATCatalogInfoFromContext(IntPtr context,
        ref CatalogInfo info, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminReleaseCatalogContext(IntPtr admin,
        IntPtr context, uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("wintrust.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminReleaseContext(IntPtr admin, uint flags);
}
