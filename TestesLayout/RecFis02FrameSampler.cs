// REC-FIS-02-PAINT-FRAMES — infraestrutura exclusivamente de teste.
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

internal static class RecFis02FrameSampler
{
    internal sealed record Frame(int Index, long BeginTick, long EndTick, byte[] Rgb32);
    internal sealed record Result(long Frequency, Rectangle Bounds, IReadOnlyList<Frame> Frames,
        string StopReason, bool DestinationObserved);
    internal sealed record Session(Task Ready, Task<Result> Finished);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hwnd, out Rectangle bounds);
    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hwnd, ref Point point);

    internal static bool PostButtonClick(IntPtr hwnd) => PostMessageW(hwnd, 0x00F5, IntPtr.Zero, IntPtr.Zero);

    // Só consultas Win32 sobre HWND e retângulo imutáveis, sem tocar controles na worker.
    // GetClientRect devolve left/top/right/bottom, sempre left=top=0.
    internal enum GuardFailure { None, Foreground, ClientRect, ClientSize, ClientToScreen, ClientOrigin, HitTestRoot }

    // Diagnóstico somente do primeiro predicado falso. Mesmas chamadas Win32,
    // ordem e curto-circuito; não consultar GetLastError sem contrato.
    private static bool Reject(GuardFailure kind, string phase, string detail, out GuardFailure failure)
    {
        failure = kind;
        Console.WriteLine(FormattableString.Invariant(
            $"REC-FIS-02-FRAMES-GUARD phase={phase} predicate={kind} tick={Stopwatch.GetTimestamp()} thread={Environment.CurrentManagedThreadId} {detail}"));
        return false;
    }

    internal static bool OwnsDesktop(IntPtr hwnd, Rectangle bounds, string phase) =>
        OwnsDesktop(hwnd, bounds, phase, out _, out _);

    internal static bool OwnsDesktop(IntPtr hwnd, Rectangle bounds, string phase, out GuardFailure failure, out Size observedClient)
    {
        observedClient = Size.Empty;
        var foreground = GetForegroundWindow();
        if (foreground != hwnd)
            return Reject(GuardFailure.Foreground, phase, $"expected={hwnd} observed={foreground}", out failure);
        if (!GetClientRect(hwnd, out var client))
            return Reject(GuardFailure.ClientRect, phase, $"hwnd={hwnd} api=false", out failure);
        observedClient = client.Size;
        if (client.Width != bounds.Width || client.Height != bounds.Height)
            return Reject(GuardFailure.ClientSize, phase, $"hwnd={hwnd} expected={bounds.Size} observed={client.Size}", out failure);
        var origin = Point.Empty;
        if (!ClientToScreen(hwnd, ref origin))
            return Reject(GuardFailure.ClientToScreen, phase, $"hwnd={hwnd} api=false", out failure);
        if (origin != bounds.Location)
            return Reject(GuardFailure.ClientOrigin, phase, $"hwnd={hwnd} expected={bounds.Location} observed={origin}", out failure);
        Point[] points = [bounds.Location, new(bounds.Right-1, bounds.Top),
            new(bounds.Left, bounds.Bottom-1), new(bounds.Right-1, bounds.Bottom-1),
            new(bounds.Left+bounds.Width/2, bounds.Top+bounds.Height/2)];
        for (int i = 0; i < points.Length; i++)
        {
            var hit = WindowFromPoint(points[i]);
            var root = GetAncestor(hit, 2);
            if (root != hwnd)
                return Reject(GuardFailure.HitTestRoot, phase,
                    $"index={i} point={points[i]} expected={hwnd} hit={hit} root={root}", out failure);
        }
        failure = GuardFailure.None;
        return true;
    }

    internal static Bitmap StableReference(Rectangle bounds, Func<bool> guard)
    {
        var first = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        try
        {
            using var second = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            Capture(first, bounds, guard); Capture(second, bounds, guard);
            if (!Same(Rgb(first), Rgb(second))) throw new InvalidOperationException("FRAMES: referência instável");
            return first;
        }
        catch { first.Dispose(); throw; }
    }

    private static void Capture(Bitmap image, Rectangle bounds, Func<bool> guard)
    {
        if (!guard()) throw new InvalidOperationException("FRAMES: desktop/foco/geometria não pertencem à fixture");
        using var graphics = Graphics.FromImage(image);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        if (!guard()) throw new InvalidOperationException("FRAMES: desktop alterado durante a captura");
    }

    internal static Session Start(Rectangle bounds, Bitmap source, Bitmap destination, Func<bool> guard,
        CancellationToken token, int maxFrames = 48, long maxBytes = 96L * 1024 * 1024)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || source.Size != bounds.Size || destination.Size != bounds.Size)
            throw new ArgumentException("Geometria incompatível");
        int frameBytes = checked(bounds.Width * bounds.Height * 4);
        if (maxFrames < 3 || maxBytes < 3L * frameBytes) throw new ArgumentOutOfRangeException(nameof(maxFrames));
        int capacity = (int)Math.Min(maxFrames, maxBytes / frameBytes);
        byte[] origin = Rgb(source), target = Rgb(destination);
        if (Same(origin, target)) throw new ArgumentException("Controle negativo indistinguível");
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = Task.Factory.StartNew(() =>
        {
            var frames = new List<Frame>(capacity);
            try
            {
                using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                bool observedSource = false;
                for (int i = 0; i < capacity; i++)
                {
                    token.ThrowIfCancellationRequested();
                    long begin = Stopwatch.GetTimestamp();
                    Capture(bitmap, bounds, guard);
                    long end = Stopwatch.GetTimestamp();
                    byte[] pixels = Rgb(bitmap);
                    frames.Add(new Frame(i, begin, end, pixels));
                    if (!observedSource)
                    {
                        if (!Same(pixels, origin)) continue;
                        observedSource = true;
                        ready.TrySetResult(true);
                    }
                    else if (Same(pixels, target))
                        return new Result(Stopwatch.Frequency, bounds, frames, "destination-reference", true);
                }
                if (!observedSource) ready.TrySetException(new InvalidOperationException("Origem não observada"));
                return new Result(Stopwatch.Frequency, bounds, frames, "capacity-exhausted", false);
            }
            catch (Exception ex) { ready.TrySetException(ex); throw; }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        return new Session(ready.Task, finished);
    }

    private static bool Same(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);

    private static byte[] Rgb(Bitmap bitmap)
    {
        var bits = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = checked(bitmap.Width * 4);
            var output = new byte[checked(rowBytes * bitmap.Height)];
            for (int y = 0; y < bitmap.Height; y++)
                Marshal.Copy(IntPtr.Add(bits.Scan0, checked(y * bits.Stride)), output, y * rowBytes, rowBytes);
            for (int i = 3; i < output.Length; i += 4) output[i] = 0;
            return output;
        }
        finally { bitmap.UnlockBits(bits); }
    }

    // PNGs no diretório plano coberto pelo workflow existente. Metadados e
    // classificação integral no log; nenhum I/O durante a transição observada.
    internal static void Export(Result result, Bitmap source, Bitmap destination, string prefix)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(prefix)!);
        source.Save(prefix + "-source.png", ImageFormat.Png);
        destination.Save(prefix + "-reference.png", ImageFormat.Png);
        byte[] origin = Rgb(source), target = Rgb(destination);
        int changed = 0;
        bool Equal(byte[] a, byte[] b, int i) => a[i] == b[i] && a[i+1] == b[i+1] && a[i+2] == b[i+2];
        for (int i = 0; i < origin.Length; i += 4) if (!Equal(origin, target, i)) changed++;
        Console.WriteLine($"REC-FIS-02-FRAMES-REFERENCE prefix={prefix} changed={changed} frequency={result.Frequency} bounds={result.Bounds}");
        foreach (var f in result.Frames)
        {
            using var bitmap = new Bitmap(result.Bounds.Width, result.Bounds.Height, PixelFormat.Format32bppRgb);
            var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                int rowBytes = checked(bitmap.Width * 4);
                for (int y = 0; y < bitmap.Height; y++)
                    Marshal.Copy(f.Rgb32, y * rowBytes, IntPtr.Add(data.Scan0, checked(y * data.Stride)), rowBytes);
            }
            finally { bitmap.UnlockBits(data); }
            bitmap.Save(prefix + $"-frame-{f.Index:D3}.png", ImageFormat.Png);
            int a = 0, b = 0, neither = 0, outside = 0, diff = 0;
            for (int i = 0; i < origin.Length; i += 4)
            {
                bool eqA = Equal(f.Rgb32, origin, i), eqB = Equal(f.Rgb32, target, i);
                if (!eqB) diff++;
                if (Equal(origin, target, i)) { if (!eqB) outside++; }
                else if (eqA) a++;
                else if (eqB) b++;
                else neither++;
            }
            string state = Same(f.Rgb32, origin) ? "source" : Same(f.Rgb32, target) ? "destination" : "different-from-both";
            Console.WriteLine($"REC-FIS-02-FRAMES-SAMPLE prefix={prefix} index={f.Index} begin_tick={f.BeginTick} end_tick={f.EndTick} frequency={result.Frequency} state={state} source={a} destination={b} neither={neither} outside={outside} diff={diff}");
        }
        Console.WriteLine($"REC-FIS-02-FRAMES-STATUS prefix={prefix} stop={result.StopReason} destination_observed={result.DestinationObserved}");
    }
}
