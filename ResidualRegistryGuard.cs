using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace GerenciadorAplicativos;

// Mesmo fingerprint usado pelas limpezas anteriores. As aberturas deste fluxo
// nao seguem links e a remocao inteira participa de uma transacao do Registro.
// Sem suporte a transacoes, o alvo e preservado; nunca ha fallback por caminho.
internal sealed class ResidualRegistryReadException(string step, int depth, Exception cause)
    : IOException("Registry inspection failed at " + step, cause)
{
    internal string Step { get; } = step;
    internal int Depth { get; } = depth;
}
internal sealed record ResidualRegistryFailure(string Step, int Depth, int Error);

internal static class ResidualRegistryGuard
{
    // Inspecao: KEY_QUERY_VALUE | KEY_ENUMERATE_SUB_KEYS. Nao exigir
    // READ_CONTROL/KEY_NOTIFY para ler; negacao continua sendo erro 5.
    private const int Read = 0x00009, Write = 0x20006, DeleteRight = 0x10000;

    internal static RegistryKey? Open(RegistryKey parent, string child, RegistryView view,
        SafeFileHandle? transaction = null, bool writable = false)
    {
        var parts = child.Split('\\');
        if (parts.Any(p => p.Length == 0 || p is "." or "..")) throw new InvalidDataException("registry_path");
        RegistryKey? current = null;
        try
        {
            for (int i = 0; i < parts.Length; i++)
            {
                RegistryKey? next;
                try { next = OpenSingle(current ?? parent, parts[i], view, transaction, writable && i == parts.Length - 1); }
                catch (Exception ex) when (IntegratedError(ex))
                { throw new ResidualRegistryReadException(i == parts.Length - 1 ? "open_leaf" : "open_parent", i, ex); }
                current?.Dispose(); current = next;
                if (current is null) return null;
                try
                {
                    if (current.GetValueNames().Contains("SymbolicLinkValue", StringComparer.OrdinalIgnoreCase))
                        throw new InvalidDataException("registry_link");
                }
                catch (Exception ex) when (IntegratedError(ex))
                { throw new ResidualRegistryReadException("link_check", i, ex); }
            }
            var result = current; current = null; return result;
        }
        finally { current?.Dispose(); }
    }

    private static RegistryKey? OpenSingle(RegistryKey parent, string child, RegistryView view,
        SafeFileHandle? transaction, bool writable)
    {
        int rights = Read | (int)view | (writable ? Write | DeleteRight : 0);
        // OpenBaseKey marks predefined roots writable in .NET. Its .Handle
        // getter reopens the ROOT with KEY_READ | KEY_WRITE. That incidental
        // write request fails for a standard user before our read-only P/Invoke.
        // Use a borrowed predefined HKEY; never request or close a root handle.
        using var predefined = Predefined(parent);
        var parentHandle = predefined ?? parent.Handle;
        int error = transaction is null
            ? RegOpenKeyExW(parentHandle, child, 8, rights, out var handle)
            : RegOpenKeyTransactedW(parentHandle, child, 8, rights, out handle, transaction, IntPtr.Zero);
        if (error == 2) { handle?.Dispose(); return null; }
        if (error != 0) { handle?.Dispose(); throw new Win32Exception(error); }
        return RegistryKey.FromHandle(handle, view);
    }

    private static SafeRegistryHandle? Predefined(RegistryKey key) => key.Name switch
    {
        "HKEY_CURRENT_USER" => new SafeRegistryHandle((IntPtr)(int)RegistryHive.CurrentUser, ownsHandle: false),
        "HKEY_LOCAL_MACHINE" => new SafeRegistryHandle((IntPtr)(int)RegistryHive.LocalMachine, ownsHandle: false),
        _ => null
    };
    private static bool IntegratedError(Exception ex) => ex is IOException or UnauthorizedAccessException or
        System.Security.SecurityException or InvalidOperationException or ArgumentException or Win32Exception;
    internal static ResidualRegistryFailure Failure(Exception ex, string fallback = "fingerprint", int depth = 0)
    {
        if (ex is ResidualRegistryReadException read)
            return Failure(read.InnerException!, read.Step, read.Depth);
        int error = ex is Win32Exception win ? win.NativeErrorCode :
            ((uint)ex.HResult & 0xFFFF0000u) == 0x80070000u ? ex.HResult & 0xffff : 0;
        return new(fallback, depth, error);
    }
    internal static bool Fingerprint(RegistryKey key, RegistryView view, out string digest, out int entries,
        SafeFileHandle? transaction = null, bool reserve = false) =>
        Fingerprint(key, view, out digest, out entries, out _, transaction, reserve);

    internal static bool Fingerprint(RegistryKey key, RegistryView view, out string digest, out int entries,
        out ResidualRegistryFailure? failure, SafeFileHandle? transaction = null, bool reserve = false)
    {
        ResidualRegistryFailure? observed = null;
        bool complete = RegistryTreeFingerprint.TryCapture(key, 3000, out digest, out entries,
            openChild: (parent, name) => Open(parent, name, view, transaction, writable: reserve),
            beforeKey: reserve ? Reserve : null, throwOnFailure: reserve,
            onFailure: (step, depth, ex) => observed = ex is ResidualRegistryReadException read
                ? new("open_child", depth + read.Depth, Failure(ex).Error) : Failure(ex, step, depth));
        failure = observed;
        return complete;
    }

    // Uma abertura transacionada somente para leitura NAO impede que uma
    // escrita externa posterior seja incorporada antes da primeira escrita.
    // Reservar cada chave por uma escrita/remoção privada na transacao adquire
    // a barreira de escrita antes do hash. O marcador nunca fica visivel fora
    // da transacao e nao altera o conteudo usado no fingerprint.
    private static void Reserve(RegistryKey key)
    {
        string marker;
        do { marker = "__GF_CLEANUP_" + Guid.NewGuid().ToString("N"); }
        while (key.GetValueNames().Contains(marker, StringComparer.OrdinalIgnoreCase));
        int error = RegSetValueExW(key.Handle, marker, 0, 4, [0, 0, 0, 0], 4);
        if (error != 0) throw new Win32Exception(error);
        error = RegDeleteValueW(key.Handle, marker);
        if (error != 0) throw new Win32Exception(error);
    }

    internal static bool Delete(ResidualKeyEvidence item, CancellationToken token)
    {
        using var transaction = CreateTransaction(IntPtr.Zero, IntPtr.Zero, 0, 0, 0, 10000, null);
        if (transaction.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            using var hive = RegistryKey.OpenBaseKey(item.Hive, item.View);
            using var key = Open(hive, item.Subkey, item.View, transaction, writable: true);
            if (key is null) return false;
            if (!Fingerprint(key, item.View, out string digest, out int entries, transaction, reserve: true) ||
                digest != item.Digest || entries != item.Entries)
                throw new InvalidDataException("registry_changed");
#if GF_PROGRESSIVE
            BeforeCommit?.Invoke(item);
#endif
            token.ThrowIfCancellationRequested();
            // RegDeleteTree no handle transacionado remove o conteudo. A chave
            // raiz exige RegDeleteKeyTransacted, nunca Registry.DeleteSubKeyTree.
            int error = RegDeleteTreeW(key.Handle, null);
            if (error != 0) throw new Win32Exception(error);
            using var rootHandle = Predefined(hive)!;
            error = RegDeleteKeyTransactedW(rootHandle, item.Subkey, (int)item.View, 0, transaction, IntPtr.Zero);
            if (error != 0) throw new Win32Exception(error);
            token.ThrowIfCancellationRequested();
            if (!CommitTransaction(transaction)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return true;
        }
        finally { RollbackTransaction(transaction); } // inocuo apos commit; fecha tambem em cancelamento
    }

#if GF_PROGRESSIVE
    internal static Action<ResidualKeyEvidence>? BeforeCommit;
#endif

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegOpenKeyExW(SafeRegistryHandle key, string subkey, int options,
        int access, out SafeRegistryHandle result);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegOpenKeyTransactedW(SafeRegistryHandle key, string subkey, int options,
        int access, out SafeRegistryHandle result, SafeFileHandle transaction, IntPtr extended);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegDeleteTreeW(SafeRegistryHandle key, string? subkey);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegSetValueExW(SafeRegistryHandle key, string name, int reserved, int type, byte[] data, int bytes);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegDeleteValueW(SafeRegistryHandle key, string name);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegDeleteKeyTransactedW(SafeRegistryHandle key, string subkey,
        int view, int reserved, SafeFileHandle transaction, IntPtr extended);
    [DllImport("ktmw32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateTransaction(IntPtr security, IntPtr id, int options,
        int isolation, int flags, int timeout, string? description);
    [DllImport("ktmw32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CommitTransaction(SafeFileHandle transaction);
    [DllImport("ktmw32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RollbackTransaction(SafeFileHandle transaction);
}

// Quatro notificacoes recursivas substituem um inventario completo por arquivo.
// Qualquer mudanca invalida a fotografia e exige nova consulta antes de excluir.
internal sealed class ResidualRegistrationMonitor : IDisposable
{
    private readonly List<(RegistryKey Key, EventWaitHandle Event)> watches = [];
    internal ResidualRegistrationMonitor()
    {
        try
        {
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                // Observar o pai tambem detecta a criacao de Uninstall ausente.
                var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall") ??
                    root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion");
                if (key is null) throw new IOException("inventory_watch_unavailable");
                var signal = new EventWaitHandle(false, EventResetMode.ManualReset);
                watches.Add((key, signal));
                Arm(key, signal);
            }
        }
        catch { Dispose(); throw; }
    }
    internal bool Changed => watches.Any(w => w.Event.WaitOne(0));
    internal void Rearm()
    {
        foreach (var w in watches)
            if (w.Event.WaitOne(0)) { w.Event.Reset(); Arm(w.Key, w.Event); }
    }
    private static void Arm(RegistryKey key, EventWaitHandle signal)
    {
        int error = RegNotifyChangeKeyValue(key.Handle, true, 0x1000000F, signal.SafeWaitHandle, true);
        if (error != 0) throw new Win32Exception(error);
    }
    public void Dispose()
    {
        foreach (var w in watches) { w.Key.Dispose(); w.Event.Dispose(); }
        watches.Clear();
    }
    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern int RegNotifyChangeKeyValue(SafeRegistryHandle key,
        [MarshalAs(UnmanagedType.Bool)] bool subtree, int filter, SafeWaitHandle signal,
        [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}
