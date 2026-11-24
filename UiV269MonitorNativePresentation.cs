using System.Runtime.InteropServices;
using System.Text;

namespace GerenciadorAplicativos;

// Somente HWND próprio e consulta da janela foreground. Sem captura de input,
// injeção, reparenting de Explorer, hook ou consulta de hardware.
internal static class GfMonitorNativePresentation
{
    internal const int Layered = 0x80000, Transparent = 0x20, NoActivate = 0x08000000;
    private const int ExStyle = -20;

    [DllImport("user32.dll", ExactSpelling=true)]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", ExactSpelling=true)]
    private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", ExactSpelling=true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, ExactSpelling=true)]
    private static extern int GetClassNameW(IntPtr hwnd, StringBuilder name, int count);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW", ExactSpelling=true, SetLastError=true)]
    private static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongW", ExactSpelling=true, SetLastError=true)]
    private static extern int SetWindowLong(IntPtr hwnd,int index,int value);
    [DllImport("user32.dll", ExactSpelling=true, SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd,uint key,byte alpha,uint flags);
    [DllImport("user32.dll", ExactSpelling=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLayeredWindowAttributes(IntPtr hwnd,out uint key,out byte alpha,out uint flags);

    internal static bool IsDesktopForeground()
    {
        IntPtr shell = GetShellWindow(), foreground = GetForegroundWindow();
        if (shell == IntPtr.Zero || foreground == IntPtr.Zero) return false;
        if (shell == foreground) return true;
        if (GetWindowThreadProcessId(shell,out uint shellPid)==0 ||
            GetWindowThreadProcessId(foreground,out uint foregroundPid)==0 ||
            shellPid==0 || shellPid!=foregroundPid) return false;
        var name = new StringBuilder(64);
        return GetClassNameW(foreground,name,name.Capacity)>0 &&
            name.ToString() is "Progman" or "WorkerW";
    }

    internal static bool ShouldShow(bool enabled,bool overlay,bool desktop,bool nativeReady) =>
        enabled && nativeReady && (overlay || desktop);

    internal static int Styles(IntPtr hwnd) => GetWindowLong(hwnd,ExStyle);
    internal static bool Apply(IntPtr hwnd,bool fixedPosition,int opacityPercent)
    {
        if (hwnd==IntPtr.Zero) return false;
        int old=Styles(hwnd);
        int desired=(old | Layered | NoActivate);
        desired=fixedPosition ? desired | Transparent : desired & ~Transparent;
        if(old!=desired)
        {
            Marshal.SetLastPInvokeError(0);
            int previous=SetWindowLong(hwnd,ExStyle,desired);
            if(previous==0 && Marshal.GetLastPInvokeError()!=0)return false;
        }
        int observed=Styles(hwnd);
        if((observed & (Layered|NoActivate|Transparent))!=(desired & (Layered|NoActivate|Transparent)))return false;
        byte alpha=(byte)Math.Round(Math.Clamp(opacityPercent,40,100)*255d/100);
        return SetLayeredWindowAttributes(hwnd,0,alpha,2) &&
            GetLayeredWindowAttributes(hwnd,out _,out byte actual,out uint flags) && actual==alpha && (flags&2)!=0;
    }
}
