using Microsoft.Win32;
using System.Globalization;
using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal sealed record SystemInfoEntry(string Title, string Description);
internal sealed record SystemInfoSnapshot(string Heading, string Summary, IReadOnlyList<SystemInfoEntry> Entries);

// Consultas deliberadamente somente de leitura. Nenhum processo, chave de
// inicialização, serviço ou configuração do Windows é alterado aqui.
internal static class IntegratedSystemReader
{
    public static SystemInfoSnapshot Computer()
    {
        string windowsName = "Windows";
        string version = Environment.OSVersion.Version.ToString();
        string cpuName = "Não informado";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is not null)
            {
                string product = Convert.ToString(key.GetValue("ProductName")) ?? "Windows";
                if (Environment.OSVersion.Version.Build >= 22000 && product.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
                    product = "Windows 11" + product["Windows 10".Length..];
                windowsName = product;
                string displayVersion = Convert.ToString(key.GetValue("DisplayVersion")) ?? "";
                if (!string.IsNullOrWhiteSpace(displayVersion)) version = displayVersion + " (compilação " + Environment.OSVersion.Version.Build + ")";
            }
        }
        catch { }
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            cpuName = (Convert.ToString(key?.GetValue("ProcessorNameString")) ?? cpuName).Trim();
        }
        catch { }
        string memory = "Não disponível";
        try
        {
            var info = new MemoryStatus();
            info.Length = (uint)Marshal.SizeOf<MemoryStatus>();
            if (GlobalMemoryStatusEx(ref info))
                memory = (info.TotalPhysical / 1073741824d).ToString("N1", CultureInfo.CurrentCulture) + " GB";
        }
        catch { }
        string bootMode;
        try
        {
            bootMode = GetSystemMetrics(67) switch
            {
                0 => "Inicialização normal",
                1 => "Modo de segurança",
                2 => "Modo de segurança com rede",
                _ => "Modo não identificado"
            };
        }
        catch { bootMode = "Não disponível"; }
        var uptime = TimeSpan.FromMilliseconds(Math.Max(0, Environment.TickCount64));
        var entries = new[]
        {
            new SystemInfoEntry("Windows", windowsName + "  ·  " + version),
            new SystemInfoEntry("Processador", cpuName),
            new SystemInfoEntry("Memória física instalada (aprox.)", memory),
            new SystemInfoEntry("Arquitetura", RuntimeInformation.OSArchitecture.ToString() + "  ·  " + Environment.ProcessorCount + " processadores lógicos"),
            new SystemInfoEntry("Nome do computador", Environment.MachineName),
            new SystemInfoEntry("Tempo ligado desde a última inicialização", $"{(int)uptime.TotalDays} dia(s), {uptime.Hours} h e {uptime.Minutes} min"),
            new SystemInfoEntry("Modo de inicialização atual", bootMode)
        };
        return new SystemInfoSnapshot("Informações deste computador",
            "Resumo de consulta local. Não substitui os diagnósticos avançados de hardware.", entries);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}

