using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GerenciadorAplicativos;

internal enum CleanupState { Complete, Partial, Missing, Unavailable, Cancelled, Busy, Inconclusive }

internal readonly record struct CleanupProgress(int Examined, int Removed);

internal sealed record TemporaryCleanupResult(int FilesRemoved, int DirectoriesRemoved,
    int Failed, int ReparseSkipped, int Skipped, IReadOnlyList<string> Examples)
{
    internal long BytesRemoved { get; init; }
    internal CleanupState State { get; init; }
    internal static TemporaryCleanupResult WithoutChanges(CleanupState state) => new(0, 0, 0, 0, 0, []) { State = state };

    internal string Summary => $"{FilesRemoved} arquivo(s) removido(s) · " +
        $"{Skipped + ReparseSkipped} item(ns) preservado(s) · {Failed} falha(s)/limite(s)";
}

internal readonly record struct TemporaryCleanupEstimate(long EligibleBytes, int EligibleFiles, bool Partial)
{
    internal CleanupState State { get; init; }
}

// Execução restrita à raiz temporária que o chamador já autorizou. Não integra
// a investigação de resíduos. O handle usado para conferir é o usado para excluir.
internal static class SafeTemporaryCleanup
{
    private const uint Read = 0x80000000, Delete = 0x00010000;
    private const uint DirectoryFlag = 0x10, ReparseFlag = 0x400, ProtectedFlags = 0x5;
    private const uint OpenReparse = 0x00200000, BackupSemantics = 0x02000000;
    private const int MaxDepth = 64, MaxEntries = 100_000;
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromMinutes(2);

    internal static TemporaryCleanupResult Clean(string root, Action<string>? log = null,
        CancellationToken cancellationToken = default, Action<CleanupProgress>? progress = null,
        Func<CleanupState>? checkScope = null) =>
        Scan(root, preview: false, log, cancellationToken, progress, checkScope).Cleanup;

    internal static TemporaryCleanupEstimate Estimate(string root, CancellationToken cancellationToken = default,
        Action<CleanupProgress>? progress = null, Func<CleanupState>? checkScope = null) =>
        Scan(root, preview: true, log: null, cancellationToken, progress, checkScope).Estimate;

    internal static bool IsEligibleFile(FileAttributes attributes, uint links, long written, long cutoff) =>
        (attributes & (FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.ReparsePoint)) == 0 &&
        links == 1 && written > 0 && written <= cutoff;

    private static (TemporaryCleanupResult Cleanup, TemporaryCleanupEstimate Estimate) Scan(
        string root, bool preview, Action<string>? log, CancellationToken cancellationToken,
        Action<CleanupProgress>? progress, Func<CleanupState>? checkScope)
    {
        int files = 0, directories = 0, failed = 0, links = 0, skipped = 0, entries = 0, eligibleFiles = 0;
        long eligibleBytes = 0, removedBytes = 0, lastProgress = 0;
        bool stopped = false, openedRoot = false;
        CleanupState state = CleanupState.Complete;
        var examples = new List<string>();
        var timer = Stopwatch.StartNew();
        long cutoff = DateTime.UtcNow.AddHours(-24).ToFileTimeUtc();

        void Failure(string path, string reason)
        {
            failed++;
            if (examples.Count < 5) examples.Add(Path.GetFileName(path) + ": " + reason);
        }
        bool CheckScope()
        {
            CleanupState scope = checkScope?.Invoke() ?? CleanupState.Complete;
            if (scope == CleanupState.Complete) return true;
            state = scope;
            stopped = true;
            return false;
        }
        bool WithinBudget(int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stopped) return false;
            if (depth <= MaxDepth && entries <= MaxEntries && timer.Elapsed <= MaximumDuration)
                return true;
            stopped = true;
            Failure(root, "Limite seguro da operação atingido.");
            return false;
        }
        void ProcessChecked(SafeFileHandle handle, string path, FileInformation info, bool directory)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ulong size = 0;
            if (!directory)
            {
                long written = ((long)info.Written.dwHighDateTime << 32) | (uint)info.Written.dwLowDateTime;
                long created = ((long)info.Created.dwHighDateTime << 32) | (uint)info.Created.dwLowDateTime;
                if (info.IndexHigh == 0 && info.IndexLow == 0 ||
                    created <= 0 || created > cutoff ||
                    !IsEligibleFile((FileAttributes)info.Attributes, info.Links, written, cutoff))
                { skipped++; return; }
                size = ((ulong)info.SizeHigh << 32) | info.SizeLow;
            }
            long countedBytes = preview ? eligibleBytes : removedBytes;
            if (size > (ulong)(long.MaxValue - countedBytes))
            {
                Failure(path, "Tamanho total não representável.");
                return;
            }
            if (directory && (info.Attributes & (ProtectedFlags | ReparseFlag)) != 0) { skipped++; return; }
            if (!CheckScope()) return;
            if (preview)
            {
                eligibleBytes += (long)size;
                eligibleFiles++;
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
            var disposition = new FileDisposition { DeleteFile = 1 };
            if (!SetFileInformationByHandle(handle, 4, ref disposition, 1))
            {
                int error = Marshal.GetLastWin32Error();
                if (error is 2 or 3 or 32 or 33 or 145) { skipped++; return; }
                Failure(path, new Win32Exception(error).Message);
                return;
            }
            if (directory) directories++;
            else { files++; removedBytes += (long)size; }
        }
        void Visit(string path, SafeFileHandle directory, int depth, bool keepRoot)
        {
            if (!WithinBudget(depth)) return;
            try
            {
                // A enumeração fornece o tipo esperado. A confirmação é feita pelo handle.
                foreach (var item in new DirectoryInfo(path).EnumerateFileSystemInfos())
                {
                    entries++;
                    if (!WithinBudget(depth + 1)) return;
                    long elapsed = timer.ElapsedMilliseconds;
                    if (elapsed - lastProgress >= 350)
                    {
                        lastProgress = elapsed;
                        progress?.Invoke(new(entries, files));
                    }
                    if (item.Name.StartsWith("_avg_", StringComparison.OrdinalIgnoreCase))
                    { skipped++; continue; }
                    string child = item.FullName;
                    try
                    {
                        var expected = item.Attributes;
                        if ((expected & FileAttributes.ReparsePoint) != 0) { links++; continue; }
                        if ((expected & (FileAttributes.System | FileAttributes.ReadOnly)) != 0) { skipped++; continue; }
                        bool expectedDirectory = (expected & FileAttributes.Directory) != 0;
                        using var handle = Open(child, deletable: true, exclusive: !expectedDirectory);
                        FileInformation info = Information(handle);
                        if ((info.Attributes & ReparseFlag) != 0) { links++; continue; }
                        bool isDirectory = (info.Attributes & DirectoryFlag) != 0;
                        if (isDirectory != expectedDirectory)
                        { Failure(child, "Tipo do item mudou durante a conferência."); continue; }
                        if (isDirectory)
                        {
                            if ((info.Attributes & ProtectedFlags) != 0) { skipped++; continue; }
                            Visit(child, handle, depth + 1, keepRoot: false);
                        }
                        else ProcessChecked(handle, child, info, directory: false);
                        if (stopped) return;
                    }
                    catch (Win32Exception ex) when (ex.NativeErrorCode is 2 or 3 or 32 or 33) { skipped++; }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
                    { Failure(child, ex.Message); }
                }
                if (!preview && !keepRoot && WithinBudget(depth) &&
                    !Directory.EnumerateFileSystemEntries(path).Any())
                    ProcessChecked(directory, path, Information(directory), directory: true);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
            { Failure(path, ex.Message); }
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CheckScope())
            {
                string fullRoot = Path.TrimEndingDirectorySeparator(InstallerFileGuard.ValidateLocalPath(root));
                if (fullRoot.Equals(Path.TrimEndingDirectorySeparator(Path.GetPathRoot(fullRoot)!),
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("A raiz de uma unidade não é uma pasta temporária.");
                using var held = PinRoot(fullRoot);
                if ((Information(held.Leaf).Attributes & ProtectedFlags) != 0)
                    throw new InvalidDataException("A pasta de limpeza tem atributos de proteção.");
                openedRoot = true;
                log?.Invoke("Examinando arquivos antigos sem seguir links.");
                progress?.Invoke(new(0, 0));
                Visit(fullRoot, held.Leaf, 0, keepRoot: true);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { state = CleanupState.Cancelled; }
        catch (Win32Exception ex) when (ex.NativeErrorCode is 2 or 3)
        { state = CleanupState.Missing; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or Win32Exception)
        { Failure(root, ex.Message); }

        if (state == CleanupState.Complete && failed > 0)
            state = openedRoot ? CleanupState.Partial : CleanupState.Unavailable;
        foreach (string example in examples) log?.Invoke("Detalhe: " + example);
        return (new(files, directories, failed, links, skipped, examples) { BytesRemoved = removedBytes, State = state },
            new(eligibleBytes, eligibleFiles, state is not (CleanupState.Complete or CleanupState.Missing)) { State = state });
    }

    private static SafeFileHandle Open(string path, bool deletable, bool exclusive = false)
    {
        // Não seguir nem compartilhar alteração/renomeação do componente final.
        // OPEN_EXISTING nunca cria arquivo ou diretório; DELETE não implica WRITE.
        var handle = CreateFileW(path, Read | (deletable ? Delete : 0), exclusive ? 0u : 1u,
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
