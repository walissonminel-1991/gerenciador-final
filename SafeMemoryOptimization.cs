using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// Notificação documentada de baixa memória. Uma falha nessa consulta
// nunca vira autorização automática para tocar na memória de outros programas.
internal sealed class WindowsLowMemorySignal : IDisposable
{
    private IntPtr _notification;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateMemoryResourceNotification(int notificationType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryMemoryResourceNotification(IntPtr notification, out int state);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    internal WindowsLowMemorySignal()
    {
        if (!OperatingSystem.IsWindows()) return;
        try { _notification = CreateMemoryResourceNotification(0); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    internal bool IsLow()
    {
        if (_notification == IntPtr.Zero || _notification == new IntPtr(-1)) return false;
        try { return QueryMemoryResourceNotification(_notification, out int state) && state != 0; }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
    }

    public void Dispose()
    {
        IntPtr handle = _notification;
        _notification = IntPtr.Zero;
        if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            CloseHandle(handle);
    }
}

// Modelo de decisão inspirado nas IDEIAS de limpeza por limite/intervalo dos
// utilitários estudados. Implementação independente, sem copiar código GPL.
// A ação autorizada é EXCLUSIVAMENTE descartar caches do próprio Gerenciador.
internal sealed class SafeMemoryOptimizationPolicy
{
    internal const int LowAvailablePercent = 15;
    internal const int ConsecutiveLowReadings = 3;
    internal static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(15);
    private int _lowReadings;
    private DateTime? _lastRun;

    internal bool AutomaticEnabled { get; set; }

    internal bool ShouldOptimize(MemoryRamSample sample, bool windowsLow, DateTime utcNow)
    {
        if (!AutomaticEnabled || sample.TotalBytes == 0)
        {
            _lowReadings = 0;
            return false;
        }

        bool pressure = windowsLow ||
            sample.AvailableBytes * 100.0 / sample.TotalBytes <= LowAvailablePercent;
        _lowReadings = pressure ? Math.Min(ConsecutiveLowReadings, _lowReadings + 1) : 0;
        return _lowReadings >= ConsecutiveLowReadings &&
            (!_lastRun.HasValue || utcNow - _lastRun.Value >= Cooldown);
    }

    internal void RecordRun(DateTime utcNow)
    {
        _lastRun = utcNow;
        _lowReadings = 0;
    }
}

// Contabilizar apenas buffers que nosso código deixa de reter.
// Não equivale a bytes de memória física recuperada pelo Windows.
internal sealed class AppCacheCleanupCounter
{
    internal long DiscardedBufferBytes { get; private set; }
    internal int Operations { get; private set; }

    internal void Register(long discardedBytes)
    {
        Operations++;
        if (discardedBytes > 0)
            DiscardedBufferBytes = discardedBytes > long.MaxValue - DiscardedBufferBytes
                ? long.MaxValue : DiscardedBufferBytes + discardedBytes;
    }
}
