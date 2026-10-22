using System.Diagnostics;
using System.Runtime.InteropServices;

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

    internal static bool IsCandidate(int pid, int ownPid, int session, int ownSession,
        string name, string path, string windowsFolder, long bytes, bool hasWindow)
    {
        if (pid <= 4 || pid == ownPid || session != ownSession ||
            hasWindow || bytes < MinimumWorkingSet ||
            string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
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
        if (!OperatingSystem.IsWindows()) return false;
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
                if (!EmptyWorkingSet(handle))
                { result = "O Windows não conseguiu reduzir a memória desse aplicativo."; return false; }
            }
            finally { CloseHandle(handle); }
            result = "O Windows reduziu a memória física ocupada por " + target.Name +
                ". A memória pode ser recarregada quando o aplicativo voltar a trabalhar.";
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or
            System.ComponentModel.Win32Exception or UnauthorizedAccessException or NotSupportedException)
        { return false; }
    }
}
