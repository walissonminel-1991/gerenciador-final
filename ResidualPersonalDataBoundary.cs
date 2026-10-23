using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// Pastas especiais podem ser movidas para dentro de uma pasta de aplicativo.
// Nao basta excluir nomes literais como "Documents": consultar os locais atuais.
internal static class ResidualPersonalDataBoundary
{
    internal static bool IsOutside(string path)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            string root = Path.GetFullPath(path).TrimEnd('\\', '/');
            foreach (var folder in new[] { Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic,
                Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.DesktopDirectory,
                Environment.SpecialFolder.CommonDocuments, Environment.SpecialFolder.CommonPictures,
                Environment.SpecialFolder.CommonMusic, Environment.SpecialFolder.CommonVideos,
                Environment.SpecialFolder.CommonDesktopDirectory })
            {
                string personal = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
                if (string.IsNullOrWhiteSpace(personal) || Overlaps(root, personal)) return false;
            }
            var downloadsId = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (SHGetKnownFolderPath(ref downloadsId, 0x4000, IntPtr.Zero, out buffer) != 0 || buffer == IntPtr.Zero)
                    return false;
                string? downloads = Marshal.PtrToStringUni(buffer);
                return !string.IsNullOrWhiteSpace(downloads) && !Overlaps(root, downloads);
            }
            finally { if (buffer != IntPtr.Zero) Marshal.FreeCoTaskMem(buffer); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException or DllNotFoundException or EntryPointNotFoundException)
        { return false; }
    }

    private static bool Overlaps(string root, string other)
    {
        string personal = Path.GetFullPath(other).TrimEnd('\\', '/');
        return root.Equals(personal, StringComparison.OrdinalIgnoreCase) ||
            root.StartsWith(personal + "\\", StringComparison.OrdinalIgnoreCase) ||
            personal.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SHGetKnownFolderPath(ref Guid folder, uint flags, IntPtr token, out IntPtr path);
}
