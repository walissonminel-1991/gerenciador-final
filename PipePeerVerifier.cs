using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GerenciadorAplicativos;

internal static class PipePeerVerifier
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

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
