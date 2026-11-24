using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace GerenciadorAplicativos;

// Somente HWND próprio e consulta da janela foreground. Sem captura de input,
// injeção, reparenting de Explorer, hook ou consulta de hardware.
internal static class GfMonitorNativePresentation
{
    internal const int Layered = 0x80000, Transparent = 0x20, NoActivate = 0x08000000;
    private const int ExStyle = -20;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { internal int Left, Top, Right, Bottom; }
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool EnumWindowCallback(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll", ExactSpelling=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", ExactSpelling=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", ExactSpelling=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll", ExactSpelling=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("dwmapi.dll", ExactSpelling=true)]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out uint value, uint size);
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

    // A região do widget, e não o foreground global, determina a demanda.
    // Ignorar somente desktop do mesmo shell, nossa janela, janelas invisíveis,
    // minimizadas e cloaked. Qualquer janela comum sobreposta bloqueia a área.
    // Usar GetWindowRect no mesmo contexto DPI de WinForms (bordas invisíveis
    // também bloqueiam conservadoramente); nenhum dado de janela é persistido.
    internal static bool BlocksDesktopArea(Rectangle area, Rectangle bounds,
        bool visible, bool minimized, bool cloaked, bool desktopWindow, bool self) =>
        visible && !minimized && !cloaked && !desktopWindow && !self &&
        bounds.Width > 0 && bounds.Height > 0 && area.IntersectsWith(bounds);

    internal static bool IsDesktopAreaAvailable(IntPtr ownWindow, Rectangle area)
    {
        if (ownWindow == IntPtr.Zero || area.Width <= 0 || area.Height <= 0) return false;
        IntPtr shell = GetShellWindow();
        if (shell == IntPtr.Zero || GetWindowThreadProcessId(shell, out uint shellPid) == 0 || shellPid == 0)
            return false;
        bool blocked = false;
        var name = new StringBuilder(64);
        bool completed = EnumWindows((hwnd, _) =>
        {
            if (hwnd == ownWindow || hwnd == shell || !IsWindowVisible(hwnd) || IsIconic(hwnd))
                return true;
            // DWMWA_CLOAKED=14: janelas em outra área de trabalho virtual
            // ou ocultadas pelo compositor não ocupam a região visível.
            if (DwmGetWindowAttribute(hwnd, 14, out uint cloak, sizeof(uint)) == 0 && cloak != 0)
                return true;
            if (!GetWindowRect(hwnd, out var rect))
            {
                blocked = true; return false; // observação incompleta: não mostrar
            }
            var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (!BlocksDesktopArea(area,bounds,true,false,false,false,false)) return true;
            if (GetWindowThreadProcessId(hwnd,out uint pid)!=0 && pid==shellPid)
            {
                name.Clear();
                if(GetClassNameW(hwnd,name,name.Capacity)>0 && name.ToString() is "Progman" or "WorkerW")
                    return true;
            }
            blocked = true; return false;
        },IntPtr.Zero);
        // Explorer pode ter reiniciado durante a enumeração.
        return completed && !blocked && GetShellWindow()==shell;
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
