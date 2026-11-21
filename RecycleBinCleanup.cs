using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal readonly record struct RecycleBinSnapshot(int Error, long Items, long Bytes)
{
    internal bool Known => Error == 0 && Items >= 0 && Bytes >= 0 && (Items > 0 || Bytes == 0);
}

internal readonly record struct RecycleBinResult(CleanupState State, RecycleBinSnapshot Current, int Error);

internal static class RecycleBinCleanup
{
    internal static RecycleBinSnapshot Query()
    {
        // shellapi.h tem alinhamento de 1 byte em processos x86, natural em x64.
        if (IntPtr.Size == 4)
        {
            var info32 = new QueryInformation32 { Size = (uint)Marshal.SizeOf<QueryInformation32>() };
            int error32 = SHQueryRecycleBinW(null, ref info32);
            return new(error32, info32.Items, info32.Bytes);
        }
        var info = new QueryInformation { Size = (uint)Marshal.SizeOf<QueryInformation>() };
        int error = SHQueryRecycleBinW(null, ref info);
        return new(error, info.Items, info.Bytes);
    }

    internal static RecycleBinResult Empty() => Empty(Query, () => SHEmptyRecycleBinW(IntPtr.Zero, null, 0x7));

    internal static RecycleBinResult Empty(Func<RecycleBinSnapshot> query, Func<int> empty)
    {
        var before = query();
        if (!before.Known) return new(CleanupState.Unavailable, before, before.Error);
        if (before.Items == 0) return new(CleanupState.Complete, before, 0);
        int error = empty();
        var after = query();
        if (!after.Known) return new(CleanupState.Inconclusive, after, error != 0 ? error : after.Error);
        if (after.Items == 0 && error == 0) return new(CleanupState.Complete, after, 0);
        return new(CleanupState.Partial, after, error);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct QueryInformation { public uint Size; public long Bytes, Items; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct QueryInformation32 { public uint Size; public long Bytes, Items; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHQueryRecycleBinW(string? root, ref QueryInformation info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHQueryRecycleBinW(string? root, ref QueryInformation32 info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHEmptyRecycleBinW(IntPtr owner, string? root, uint flags);
}
