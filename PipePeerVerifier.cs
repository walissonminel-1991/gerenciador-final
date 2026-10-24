using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Security.Principal;

namespace GerenciadorAplicativos;

internal static class PipePeerVerifier
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    // O worker que recebe um plano destrutivo tambem autentica o servidor pelo
    // PID do kernel e pelo mesmo executavel. Token/nomes do pipe nao bastam.
    internal static bool IsSameApplicationServer(NamedPipeClientStream client, out string reason)
    {
        reason = "uac_peer_rejected";
        try
        {
            if (!OperatingSystem.IsWindows() || !GetNamedPipeServerProcessId(client.SafePipeHandle, out uint id) ||
                id == (uint)Environment.ProcessId) return false;
            using var parent = Process.GetProcessById(checked((int)id));
            if (!OpenProcessToken(parent.SafeHandle, 8, out var token)) return false;
            using (token)
            using (var owner = new WindowsIdentity(token.DangerousGetHandle()))
            using (var current = WindowsIdentity.GetCurrent())
                if (!Equals(owner.User, current.User)) { reason = "uac_account_changed"; return false; }
            string? path = parent.MainModule?.FileName;
            string? self = Environment.ProcessPath;
            // No host dotnet.exe, a igualdade de host nao autentica o assembly.
            return self is not null && !Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
                path is not null && Path.GetFullPath(path).Equals(Path.GetFullPath(self), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            System.ComponentModel.Win32Exception or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        { return false; }
    }

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

    internal static bool IsExpectedClient(NamedPipeServerStream server, int expectedProcessId)
    {
        if (!OperatingSystem.IsWindows() || expectedProcessId <= 0 || !server.IsConnected)
            return false;
        try
        {
            return GetNamedPipeClientProcessId(server.SafePipeHandle, out uint actual) &&
                actual == (uint)expectedProcessId;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        { return false; }
    }
}
