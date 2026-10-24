using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GerenciadorAplicativos;

// Consulta SOMENTE LEITURA ao Restart Manager nativo do Windows.
// Nunca usa RmShutdown/RmRestart: fechar processos requer consentimento separado.
internal sealed record LockedProcess(int ProcessId, string Name, bool IsSystem);
internal sealed record LockInspection(bool Success, IReadOnlyList<LockedProcess> Processes, string Detail);

internal static class InstallerLockScanner
{
    private const int ErrorMoreData = 234;
    private const int MaxResources = 160;

    internal static string[] ObsResources()
    {
        // A primeira prova usa DLLs e executaveis no diretorio FIXO de OBS.
        // Nenhum arquivo de perfil de usuario, TEMP ou diretório de terceiros.
        string[] roots =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio")
        };
        foreach (string root in roots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                var paths = new List<string>();
                var folders = new[]
                {
                    Path.Combine(root, "obs-plugins", "64bit"),
                    Path.Combine(root, "bin", "64bit"),
                    root
                };
                foreach (string folder in folders)
                {
                    if (!Directory.Exists(folder)) continue;
                    foreach (string file in Directory.EnumerateFiles(folder, "*",
                        new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true,
                            AttributesToSkip = FileAttributes.ReparsePoint
                        }))
                    {
                        if (!file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                            !file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                            !file.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)) continue;
                        if (paths.Count >= MaxResources) break;
                        paths.Add(file);
                    }
                    if (paths.Count >= MaxResources) break;
                }
                return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
        }
        return [];
    }

    internal static string[] NotepadResources()
    {
        string[] roots = [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Notepad++"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Notepad++")];
        try
        {
            return roots.Where(Directory.Exists).SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                .Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Take(MaxResources).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    internal static string[] SumatraResources()
    {
        string[] roots =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SumatraPDF"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SumatraPDF"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SumatraPDF")
        };
        var files = new List<string>();
        foreach (string root in roots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                files.AddRange(Directory.EnumerateFiles(root, "*",
                    new EnumerationOptions { RecurseSubdirectories = true,
                        IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint })
                    .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                                   path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    .Take(MaxResources - files.Count));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            if (files.Count >= MaxResources) break;
        }
        return files.ToArray();
    }

    internal static LockInspection Inspect(IEnumerable<string> resources)
    {
        string[] files = resources
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxResources)
            .ToArray();
        if (files.Length == 0) return new(false, [], "Nenhum arquivo instalado disponível para análise.");

        var key = new StringBuilder(64);
        int rc = RmStartSession(out uint session, 0, key);
        if (rc != 0) return new(false, [], "Restart Manager indisponível (" + rc + ").");
        try
        {
            // Batch reduz o risco de ERROR_SEM_TIMEOUT de uma única chamada longa.
            foreach (string[] batch in files.Chunk(40))
            {
                rc = RmRegisterResources(session, (uint)batch.Length, batch, 0, IntPtr.Zero, 0, IntPtr.Zero);
                if (rc != 0)
                    return new(false, [], "Falha ao verificar bloqueios (" + rc + ").");
            }
            uint needed = 0, count = 0, reboot = 0;
            rc = RmGetList(session, out needed, ref count, null, out reboot);
            if (rc == 0 && needed == 0)
                return reboot == 0
                    ? new(true, [], "Sem bloqueios conhecidos.")
                    : new(false, [], "Reinicialização do Windows pode ser necessária.");
            if (rc != ErrorMoreData && rc != 0)
                return new(false, [], "Falha ao consultar arquivos em uso (" + rc + ").");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (needed > 512) return new(false, [], "Limite seguro de processos excedido.");
                count = Math.Max(needed, 1);
                var entries = new RmProcessInfo[(int)count];
                rc = RmGetList(session, out needed, ref count, entries, out reboot);
                if (rc == ErrorMoreData) continue;
                if (rc != 0) return new(false, [], "Falha ao consultar processos (" + rc + ").");
                var found = entries.Take((int)count)
                    .Where(p => p.Process.ProcessId > 0)
                    .GroupBy(p => p.Process.ProcessId)
                    .Select(g =>
                    {
                        var p = g.First();
                        string display = p.AppName?.Trim() ?? "";
                        try
                        {
                            using var process = Process.GetProcessById(p.Process.ProcessId);
                            display = process.ProcessName;
                        }
                        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                            or Win32Exception) { }
                        bool system = p.ApplicationType is 4 or 5 or 1000 || p.SessionId == uint.MaxValue;
                        return new LockedProcess(p.Process.ProcessId, display, system);
                    })
                    .ToArray();
                // RM_REBOOT_REASON_DETECTED_SELF (0x10) é esperado nos testes
                // quando o próprio processo segura o arquivo. Retornar o PID,
                // sem autorizar seu fechamento; o preflight vai bloqueá-lo.
                bool selfOnly = reboot == 0x10 &&
                    found.Length > 0 && found.All(p => p.ProcessId == Environment.ProcessId);
                return reboot == 0 || selfOnly
                    ? new(true, found, selfOnly ? "Recurso utilizado pelo próprio processo." : "Consulta concluída.")
                    : new(false, found, "Windows requer intervenção/reinício (motivo " + reboot + ").");
            }
            return new(false, [], "Lista de bloqueios mudou durante a consulta.");
        }
        finally { RmEndSession(session); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public int ProcessId;
        public uint StartTimeLow;
        public uint StartTimeHigh;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint SessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, EntryPoint = "RmStartSession")]
    private static extern int RmStartSession(out uint handle, int flags, StringBuilder sessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, EntryPoint = "RmRegisterResources")]
    private static extern int RmRegisterResources(uint handle, uint fileCount,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] string[] filenames,
        uint appCount, IntPtr applications, uint serviceCount, IntPtr services);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, EntryPoint = "RmGetList")]
    private static extern int RmGetList(uint handle, out uint needed, ref uint count,
        [In, Out] RmProcessInfo[]? processes, out uint rebootReasons);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, EntryPoint = "RmEndSession")]
    private static extern int RmEndSession(uint handle);
}
