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

    internal static FileStream OpenRead(string path)
    {
        string full = ValidateLocalPath(path);
        var parents = PinDirectory(Path.GetDirectoryName(full)!);
        SafeFileHandle? handle = null;
        try
        {
            handle = CreateFileW(full, 0x80000000, 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
            CheckHandle(handle, directory: false);
            var stream = new PinnedStream(handle, parents);
            handle = null;
            try
            {
                if (stream.Length <= 0 || stream.Length > 300L * 1024 * 1024)
                    throw new InvalidDataException("Tamanho do pacote fora do limite.");
                return stream;
            }
            catch { stream.Dispose(); throw; }
        }
        catch { handle?.Dispose(); parents.Dispose(); throw; }
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

    internal static IDisposable PinDirectory(string directory)
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
                var handle = CreateFileW(part, 0x80000000, 1, IntPtr.Zero, 3,
                    0x02000000 | 0x00200000, IntPtr.Zero);
                lease.Handles.Add(handle);
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
        if ((info.Attributes & reparse) != 0 || ((info.Attributes & dir) != 0) != directory)
            throw new InvalidDataException("Caminho redirecionado ou tipo de arquivo nao autorizado.");
    }

    private sealed class DirectoryLease : IDisposable
    {
        internal readonly List<SafeFileHandle> Handles = new();
        public void Dispose()
        {
            for (int i = Handles.Count - 1; i >= 0; i--) Handles[i].Dispose();
            Handles.Clear();
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
