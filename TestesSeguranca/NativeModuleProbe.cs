using System.Runtime.InteropServices;
using System.Text;

internal static class NativeModuleProbe
{
    [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileName(IntPtr module, StringBuilder path, int capacity);

    internal static string PathOf(IntPtr module)
    {
        var path = new StringBuilder(32768);
        uint length = GetModuleFileName(module, path, path.Capacity);
        if (length == 0 || length >= path.Capacity)
            throw new IOException("Nao foi possivel identificar a DLL carregada.");
        return path.ToString();
    }
}
