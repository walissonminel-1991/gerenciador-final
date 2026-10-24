using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GerenciadorAplicativos;

// Reducao de conjunto de trabalho por API suportada do Windows.
// Nao e "limpeza de toda a RAM", nao apaga dados e pode gerar novas leituras de disco.
// Escolhe apenas um processo local em segundo plano; automatico reusa a MESMA identidade autorizada.
internal sealed record MemoryTrimTarget(int Pid, DateTime StartedUtc, string Name, string Executable, long WorkingSet);

internal static class SelectiveMemoryTrim
{
    private const uint QueryLimited = 0x1000;
    private const uint SetQuota = 0x0100;
    private const long MinimumWorkingSet = 128L * 1024 * 1024;
    private static int trimming;
    private static readonly string[] Excluded = [
        "av", "antivirus", "defender", "security", "vpn", "antitrack", "driver",
        "service", "system", "svchost", "lsass", "csrss", "wininit", "winlogon",
        "explorer", "dwm", "sihost", "searchhost", "taskhost", "runtimebroker",
        "smartscreen", "msmpeng", "memory compression"
    ];

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "K32EmptyWorkingSet")]
    private static extern bool EmptyWorkingSet(IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessTimes(IntPtr process, out long created, out long exited,
        out long kernel, out long user);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetProcessId(IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags,
        StringBuilder executable, ref uint size);

    internal static bool SameAuthorizedIdentity(MemoryTrimTarget target, int pid, DateTime startedUtc, string path) =>
        target.Pid == pid && target.StartedUtc == startedUtc &&
        !string.IsNullOrWhiteSpace(path) && path.Equals(target.Executable, StringComparison.OrdinalIgnoreCase);

    internal static bool TryReadHandleIdentity(IntPtr handle, out int pid, out DateTime startedUtc, out string path)
    {
        pid = 0; startedUtc = default; path = "";
        if (handle == IntPtr.Zero || !OperatingSystem.IsWindows() ||
            !GetProcessTimes(handle, out long created, out _, out _, out _) ||
            !GetExitCodeProcess(handle, out uint exit) || exit != 259) return false;
        uint id = GetProcessId(handle);
        var buffer = new StringBuilder(32768); uint size = (uint)buffer.Capacity;
        if (id is 0 or > int.MaxValue || !QueryFullProcessImageNameW(handle, 0, buffer, ref size)) return false;
        pid = (int)id; startedUtc = DateTime.FromFileTimeUtc(created); path = buffer.ToString();
        return path.Length > 0;
    }

    internal static bool IsCandidate(int pid, int ownPid, int session, int ownSession,
        string name, string path, string windowsFolder, long bytes, bool hasWindow)
    {
        if (pid <= 4 || pid == ownPid || session != ownSession ||
            hasWindow || bytes < MinimumWorkingSet ||
            string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            string.IsNullOrWhiteSpace(windowsFolder) ||
            !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return false;
        if (Excluded.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            return false;
        string windows = Path.GetFullPath(windowsFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string executable = Path.GetFullPath(path);
        return !executable.StartsWith(windows, StringComparison.OrdinalIgnoreCase);
    }

    internal static MemoryTrimTarget? ChooseCandidate()
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var current = Process.GetCurrentProcess();
        int ownPid = current.Id, session = current.SessionId;
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows)) return null;
        var candidates = new List<MemoryTrimTarget>();
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    var modulePath = p.MainModule?.FileName ?? "";
                    long bytes = p.WorkingSet64;
                    if (!IsCandidate(p.Id, ownPid, p.SessionId, session, p.ProcessName,
                        modulePath, windows, bytes, p.MainWindowHandle != IntPtr.Zero))
                        continue;
                    candidates.Add(new(p.Id, p.StartTime.ToUniversalTime(), p.ProcessName, modulePath, bytes));
                }
                catch (Exception ex) when (ex is InvalidOperationException or
                    System.ComponentModel.Win32Exception or UnauthorizedAccessException or
                    NotSupportedException) { /* Falha de leitura: nunca autorizar. */ }
            }
        }
        return candidates.OrderByDescending(x=>x.WorkingSet).FirstOrDefault();
    }

    internal static bool TryTrimAuthorized(MemoryTrimTarget target, out string result)
    {
        result = "O processo mudou ou a operação não está disponível.";
        if (!OperatingSystem.IsWindows() || Interlocked.CompareExchange(ref trimming, 1, 0) != 0) return false;
        try
        {
            using var current = Process.GetCurrentProcess();
            using var p = Process.GetProcessById(target.Pid);
            string path = p.MainModule?.FileName ?? "";
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (p.StartTime.ToUniversalTime() != target.StartedUtc ||
                !path.Equals(target.Executable, StringComparison.OrdinalIgnoreCase) ||
                !IsCandidate(p.Id, current.Id, p.SessionId, current.SessionId,
                    p.ProcessName, path, windows, p.WorkingSet64, p.MainWindowHandle != IntPtr.Zero))
                return false;
            IntPtr handle = OpenProcess(QueryLimited | SetQuota, false, p.Id);
            if (handle == IntPtr.Zero) { result = "O Windows não permitiu ajustar a memória desse aplicativo."; return false; }
            try
            {
                // Identidade obtida do MESMO handle entregue a EmptyWorkingSet.
                // Reutilizar um PID entre as duas consultas nao transfere consentimento.
                if (!TryReadHandleIdentity(handle, out int pid, out DateTime startedUtc, out string executable) ||
                    !SameAuthorizedIdentity(target, pid, startedUtc, executable)) return false;
                if (!EmptyWorkingSet(handle))
                { result = "O Windows não conseguiu reduzir a memória desse aplicativo."; return false; }
            }
            finally { CloseHandle(handle); }
            result = "O Windows reduziu a memória física ocupada por " + target.Name +
                ". A memória pode ser recarregada quando o aplicativo voltar a trabalhar.";
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or
            System.ComponentModel.Win32Exception or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        { return false; }
        finally { Volatile.Write(ref trimming, 0); }
    }
}
