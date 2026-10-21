using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GerenciadorAplicativos;

internal sealed record TemporaryCleanupResult(int FilesRemoved, int DirectoriesRemoved,
    int Failed, int ReparseSkipped, int Skipped, IReadOnlyList<string> Examples);

// Execução restrita à raiz temporária que o chamador já autorizou. Não integra
// a investigação de resíduos. O handle usado para conferir é o usado para excluir.
internal static class SafeTemporaryCleanup
{
    private const uint Read = 0x80000000, Delete = 0x00010000;
    private const uint DirectoryFlag = 0x10, ReparseFlag = 0x400, ReadOnlyFlag = 1;
    private const uint OpenReparse = 0x00200000, BackupSemantics = 0x02000000;
    private const int MaxDepth = 64, MaxEntries = 100_000;
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(2);

    internal static TemporaryCleanupResult Clean(string root, Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int files = 0, directories = 0, failed = 0, links = 0, skipped = 0, entries = 0;
        bool stopped = false;
        var examples = new List<string>();
        var timer = Stopwatch.StartNew();
        DateTime cutoff = DateTime.UtcNow.AddHours(-24);

        void Failure(string path, string reason)
        {
            failed++;
            if (examples.Count < 5) examples.Add(Path.GetFileName(path) + ": " + reason);
        }
        bool WithinBudget(int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stopped) return false;
            if (depth <= MaxDepth && entries <= MaxEntries && timer.Elapsed <= MaximumDuration)
                return true;
            stopped = true;
            Failure(root, "Limite seguro da consulta atingido; limpeza parcial.");
            return false;
        }
        void DeleteChecked(SafeFileHandle handle, string path, FileInformation info, bool directory)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((info.Attributes & ReadOnlyFlag) != 0 || (!directory && info.Links != 1))
            { skipped++; return; }
            if (!directory)
            {
                long written = ((long)info.Written.dwHighDateTime << 32) |
                    (uint)info.Written.dwLowDateTime;
                DateTime lastWrite;
                try { lastWrite = DateTime.FromFileTimeUtc(written); }
                catch (ArgumentOutOfRangeException) { skipped++; return; }
                if (written <= 0 || lastWrite > cutoff) { skipped++; return; }
            }
            var disposition = new FileDisposition { DeleteFile = 1 };
            if (!SetFileInformationByHandle(handle, 4, ref disposition, 1))
            {
                int error = Marshal.GetLastWin32Error();
                // Partilha, ACL, arquivo em uso ou pasta preenchida durante a consulta:
                // preservar. Nunca tentar novamente por pathname ou elevar privilégio.
                if (error is 2 or 3 or 5 or 32 or 33 or 145) { skipped++; return; }
                Failure(path, new Win32Exception(error).Message);
                return;
            }
            if (directory) directories++; else files++;
        }
        void Visit(string path, SafeFileHandle directory, int depth, bool keepRoot)
        {
            if (!WithinBudget(depth)) return;
            try
            {
                // Todos os ancestrais e o diretório enumerado permanecem presos.
                // Os compartilhamentos não impedem a criação normal de arquivos filhos.
                foreach (string child in Directory.EnumerateFileSystemEntries(path))
                {
                    entries++;
                    if (!WithinBudget(depth + 1)) return;
                    if (Path.GetFileName(child).StartsWith("_avg_", StringComparison.OrdinalIgnoreCase))
                    { skipped++; continue; }
                    try
                    {
                        using var handle = Open(child, deletable: true);
                        FileInformation info = Information(handle);
                        if ((info.Attributes & ReparseFlag) != 0) { links++; continue; }
                        bool isDirectory = (info.Attributes & DirectoryFlag) != 0;
                        if (isDirectory) Visit(child, handle, depth + 1, keepRoot: false);
                        else DeleteChecked(handle, child, info, directory: false);
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode is 2 or 3 or 5 or 32 or 33)
                    { skipped++; }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
                    { Failure(child, ex.Message); }
                }
                if (!keepRoot && !stopped)
                {
                    // Uma criação concorrente torna a pasta não vazia: o próprio Windows
                    // recusa FileDispositionInfo. Nenhuma exclusão recursiva é solicitada.
                    if (!Directory.EnumerateFileSystemEntries(path).Any())
                        DeleteChecked(directory, path, Information(directory), directory: true);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
            { Failure(path, ex.Message); }
        }

        try
        {
            string fullRoot = Path.TrimEndingDirectorySeparator(InstallerFileGuard.ValidateLocalPath(root));
            if (fullRoot.Equals(Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullRoot)!),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A raiz de uma unidade não é uma pasta temporária.");
            using var held = PinRoot(fullRoot);
            log?.Invoke("Pasta temporária protegida; examinando arquivos antigos sem seguir links.");
            Visit(fullRoot, held.Leaf, 0, keepRoot: true);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or Win32Exception)
        { Failure(root, ex.Message); }

        log?.Invoke($"Limpeza: {files} arquivo(s) e {directories} pasta(s) removidos; " +
            $"{skipped} itens recentes, protegidos, vinculados ou em uso preservados; " +
            $"{links} links redirecionados ignorados; {failed} falhas/limites.");
        foreach (string example in examples) log?.Invoke("Detalhe: " + example);
        return new(files, directories, failed, links, skipped, examples);
    }

    private static SafeFileHandle Open(string path, bool deletable)
    {
        // Não seguir nem compartilhar alteração/renomeação do componente final.
        // OPEN_EXISTING nunca cria arquivo ou diretório; DELETE não implica WRITE.
        var handle = CreateFileW(path, Read | (deletable ? Delete : 0), 1,
            IntPtr.Zero, 3, BackupSemantics | OpenReparse, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error);
        }
        return handle;
    }

    private static FileInformation Information(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return info;
    }

    private static DirectoryLease PinRoot(string root)
    {
        var chain = new Stack<string>();
        for (var current = new DirectoryInfo(root); current is not null; current = current.Parent)
        {
            if (chain.Count >= 128) throw new InvalidDataException("Caminho temporário excessivamente profundo.");
            chain.Push(current.FullName);
        }
        var lease = new DirectoryLease();
        try
        {
            while (chain.TryPop(out string? path))
            {
                var handle = Open(path, deletable: false);
                lease.Handles.Add(handle);
                var info = Information(handle);
                if ((info.Attributes & ReparseFlag) != 0 || (info.Attributes & DirectoryFlag) == 0)
                    throw new InvalidDataException("Raiz ou ancestral redirecionado; nenhuma limpeza iniciada.");
            }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    private sealed class DirectoryLease : IDisposable
    {
        internal readonly List<SafeFileHandle> Handles = new();
        internal SafeFileHandle Leaf => Handles[^1];
        public void Dispose()
        {
            for (int i = Handles.Count - 1; i >= 0; i--) Handles[i].Dispose();
            Handles.Clear();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDisposition { public byte DeleteFile; } // BOOLEAN nativo: um byte.
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
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass,
        ref FileDisposition disposition, uint size);
}
