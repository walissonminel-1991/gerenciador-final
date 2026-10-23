using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GerenciadorAplicativos;

// Arquivo e ancestrais permanecem abertos; falha fechada se o Windows negar a trava.
internal static class InstallerFileGuard
{
    // Instancia de producao vive ate o encerramento do GF; testes podem liberar explicitamente.
    internal sealed class SessionProtection : IDisposable
    {
        private readonly List<FileStream> held = new();
        internal void Retain(FileStream stream)
        {
            lock (held)
            {
                if (held.Count >= 32)
                    throw new InvalidOperationException("Limite de instaladores protegidos nesta sessao. Conclua as instalacoes e reinicie o GF.");
                held.Add(stream);
            }
        }
        public void Dispose()
        {
            lock (held)
            {
                foreach (var stream in held) stream.Dispose();
                held.Clear();
            }
        }
    }

    internal static FileStream OpenRead(string path, bool allowDirectoryWrites = false)
        => OpenReadCore(path, allowDirectoryWrites, allowEmpty: false, deleteAccess: false);

    // Exclusivo de residuos. Um marcador vazio e valido; instaladores continuam
    // proibindo tamanho zero. DELETE nao altera ACL nem contorna permissoes.
    internal static FileStream OpenResidual(string path, bool deleteAccess = false)
        => OpenReadCore(path, allowDirectoryWrites: true, allowEmpty: true, deleteAccess);

    private static FileStream OpenReadCore(string path, bool allowDirectoryWrites, bool allowEmpty, bool deleteAccess)
    {
        string full = ValidateLocalPath(path);
        var parents = PinDirectory(Path.GetDirectoryName(full)!, allowDirectoryWrites);
        SafeFileHandle? handle = null;
        try
        {
            handle = CreateFileW(full, 0x80000000u | (deleteAccess ? 0x00010000u : 0u), 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
            CheckHandle(handle, directory: false);
            var stream = new PinnedStream(handle, parents);
            handle = null;
            try
            {
                if ((!allowEmpty && stream.Length == 0) || stream.Length > 300L * 1024 * 1024)
                    throw new InvalidDataException("Tamanho do pacote fora do limite.");
                return stream;
            }
            catch { stream.Dispose(); throw; }
        }
        catch { handle?.Dispose(); parents.Dispose(); throw; }
    }

    internal static void MarkResidualForDeletion(FileStream held)
    {
        byte disposition = 1; // FILE_DISPOSITION_INFO.DeleteFile (BOOLEAN)
        if (!SetFileInformationByHandle(held.SafeFileHandle, 4, ref disposition, 1))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 5) throw new UnauthorizedAccessException("O Windows negou a exclusao do residuo.");
            throw new IOException("Nao foi possivel excluir o residuo protegido.",
                unchecked((int)(0x80070000u | (uint)error)));
        }
        // O proprio objeto validado e removido ao fechar o handle. Nenhuma
        // segunda abertura por caminho permite atingir um arquivo substituto.
    }

    internal static string ValidateLocalPath(string path)
    {
        // Recusa UNC, namespaces de dispositivo, ADS e nomes relativos antes de normalizar.
        if (!OperatingSystem.IsWindows() || !Path.IsPathFullyQualified(path) ||
            path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' ||
            (path[2] != '\\' && path[2] != '/') || path.AsSpan(2).Contains(':'))
            throw new InvalidDataException("Pacote exige caminho absoluto em unidade local.");
        string full = Path.GetFullPath(path);
        if (new DriveInfo(Path.GetPathRoot(full)!).DriveType != DriveType.Fixed)
            throw new InvalidDataException("Pacote exige unidade local fixa.");
        return full;
    }

    internal static DirectoryLease PinDirectory(string directory, bool allowLeafWrites = false)
    {
        string full = ValidateLocalPath(directory);
        var chain = new Stack<string>();
        for (var current = new DirectoryInfo(full); current is not null; current = current.Parent)
            chain.Push(current.FullName);
        var lease = new DirectoryLease();
        try
        {
            while (chain.TryPop(out string? part))
            {
                // OPEN_REPARSE_POINT impede seguir o componente final; os anteriores ja estao presos.
                // Nao bloquear escrita em TEMP/perfil/raiz usados por outros aplicativos.
                // Todos os ancestrais negam DELETE; somente a pasta privada de leitura nega WRITE.
                uint sharing = chain.Count == 0 && !allowLeafWrites ? 1u : 3u;
                var handle = CreateFileW(part, 0x80000000, sharing, IntPtr.Zero, 3,
                    0x02000000 | 0x00200000, IntPtr.Zero);
                lease.Handles.Add(handle);
                lease.Paths.Add(part);
                CheckHandle(handle, directory: true);
            }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    private static void CheckHandle(SafeFileHandle handle, bool directory)
    {
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info))
            throw new IOException("Nao foi possivel proteger o caminho do pacote.",
                new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        const uint reparse = 0x400, dir = 0x10;
        if ((info.Attributes & reparse) != 0 || ((info.Attributes & dir) != 0) != directory ||
            (!directory && info.Links != 1))
            throw new InvalidDataException("Caminho redirecionado ou tipo de arquivo nao autorizado.");
    }

    // Mesma identidade por handle reutilizada pela prévia de resíduos. Não
    // altera os critérios de instalação nem autoriza exclusão de arquivo.
    internal sealed record FileIdentity(uint Volume, ulong Index, uint Links, ulong Size, long Written);
    internal static FileIdentity ReadIdentity(FileStream stream)
    {
        if (!GetFileInformationByHandle(stream.SafeFileHandle, out var info))
            throw new IOException("Identidade do arquivo indisponível.");
        return new(info.Volume, ((ulong)info.IndexHigh << 32) | info.IndexLow, info.Links,
            ((ulong)info.SizeHigh << 32) | info.SizeLow,
            ((long)info.Written.dwHighDateTime << 32) | (uint)info.Written.dwLowDateTime);
    }

    internal sealed record DirectoryIdentity(string Path, uint Volume, ulong Index);
    internal sealed class DirectoryLease : IDisposable
    {
        internal readonly List<SafeFileHandle> Handles = new();
        internal readonly List<string> Paths = new();
        internal IReadOnlyList<DirectoryIdentity> Identities()
        {
            var identities = new List<DirectoryIdentity>();
            for (int i = 0; i < Handles.Count; i++)
            {
                if (!GetFileInformationByHandle(Handles[i], out var info))
                    throw new IOException("Identidade do diretório indisponível.");
                identities.Add(new(Paths[i], info.Volume, ((ulong)info.IndexHigh << 32) | info.IndexLow));
            }
            return identities.AsReadOnly();
        }
        public void Dispose()
        {
            for (int i = Handles.Count - 1; i >= 0; i--) Handles[i].Dispose();
            Handles.Clear();
            Paths.Clear();
        }
    }

    private sealed class PinnedStream(SafeFileHandle handle, IDisposable parents)
        : FileStream(handle, FileAccess.Read)
    {
        protected override void Dispose(bool disposing)
        {
            try { base.Dispose(disposing); }
            finally { if (disposing) parents.Dispose(); }
        }
        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share,
        IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass,
        ref byte disposition, uint size);

    internal static async Task<string> DigestAsync(FileStream stream, CancellationToken token = default)
    {
        stream.Position = 0;
        string digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
        stream.Position = 0;
        return digest;
    }

    internal static async Task RequireDigestAsync(FileStream stream, string expected,
        CancellationToken token = default)
    {
        string actual = await DigestAsync(stream, token);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException("Pacote alterado desde a verificacao.");
    }
}
