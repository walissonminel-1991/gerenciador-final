using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed record SystemInfoEntry(string Title, string Description);
internal sealed record SystemInfoSnapshot(string Heading, string Summary, IReadOnlyList<SystemInfoEntry> Entries);

// Consultas deliberadamente somente de leitura. Nenhum processo, chave de
// inicialização, serviço ou configuração do Windows é alterado aqui.
internal static class IntegratedSystemReader
{
    public static SystemInfoSnapshot Processes()
    {
        var results = new List<(string Name, int Pid, long Bytes)>();
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    results.Add((process.ProcessName, process.Id, process.WorkingSet64));
                }
                catch (InvalidOperationException) { /* Processo encerrou durante a consulta. */ }
                catch (System.ComponentModel.Win32Exception) { /* Acesso limitado ao processo. */ }
                catch (Exception) { /* Processo indisponível. */ }
            }
        }
        var ordered = results.OrderByDescending(p => p.Bytes).ThenBy(p => p.Name,
            StringComparer.CurrentCultureIgnoreCase).ToList();
        var entries = ordered.Take(160).Select(p => new SystemInfoEntry(
            p.Name + "  ·  PID " + p.Pid.ToString(CultureInfo.CurrentCulture),
            "Memória física aproximada: " + (p.Bytes / 1048576d).ToString("N1", CultureInfo.CurrentCulture) + " MB"))
            .ToArray();
        string summary = $"{ordered.Count} processos acessíveis identificados. Exibindo os {entries.Length} com maior uso de memória — fotografia do momento.";
        return new SystemInfoSnapshot("Processos em execução", summary, entries);
    }

    public static SystemInfoSnapshot StartupEntries()
    {
        var items = new List<SystemInfoEntry>();
        const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        void ReadRegistry(RegistryHive hive, RegistryView view, string scope)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var run = root.OpenSubKey(runKey, writable: false);
                if (run is null) return;
                foreach (string name in run.GetValueNames())
                {
                    try
                    {
                        object? raw = run.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        string command = raw?.ToString()?.Trim() ?? "";
                        if (command.Length == 0) continue;
                        items.Add(new SystemInfoEntry(name, scope + "  ·  " + command));
                    }
                    catch { /* Valor inacessível ou removido entre consultas. */ }
                }
            }
            catch { /* Algumas visões do Registro podem estar indisponíveis. */ }
        }

        ReadRegistry(RegistryHive.CurrentUser, RegistryView.Default, "Meu usuário — Registro Run");
        if (Environment.Is64BitOperatingSystem)
        {
            ReadRegistry(RegistryHive.LocalMachine, RegistryView.Registry64, "Todos os usuários — 64 bits");
            ReadRegistry(RegistryHive.LocalMachine, RegistryView.Registry32, "Todos os usuários — 32 bits");
        }
        else ReadRegistry(RegistryHive.LocalMachine, RegistryView.Registry32, "Todos os usuários — Registro Run");

        void ReadFolder(Environment.SpecialFolder folder, string scope)
        {
            try
            {
                string path = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
                foreach (string file in Directory.EnumerateFiles(path))
                    items.Add(new SystemInfoEntry(Path.GetFileNameWithoutExtension(file), scope + "  ·  " + file));
            }
            catch { /* Acesso limitado à pasta Inicializar. */ }
        }
        ReadFolder(Environment.SpecialFolder.Startup, "Pasta Inicializar do usuário");
        ReadFolder(Environment.SpecialFolder.CommonStartup, "Pasta Inicializar de todos os usuários");

        var result = items.GroupBy(x => x.Title + "\u001f" + x.Description,
            StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (result.Length == 0)
            return new SystemInfoSnapshot("Aplicativos de inicialização",
                "Não foram encontradas entradas nos locais consultados.",
                new[] { new SystemInfoEntry("Consulta limitada", "Alguns aplicativos utilizam serviços ou tarefas agendadas e não aparecem nesta lista.") });
        return new SystemInfoSnapshot("Aplicativos de inicialização",
            $"{result.Length} entradas encontradas. Consulta ao Registro Run e pastas Inicializar; nenhuma configuração será modificada.", result);
    }

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

    public static SystemInfoSnapshot WindowsUpdate()
    {
        var items = new List<SystemInfoEntry>();
        string service = QueryServiceState("wuauserv");
        items.Add(new SystemInfoEntry("Serviço Windows Update", service));
        string startMode = "Não disponível";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\wuauserv");
            object? raw = key?.GetValue("Start");
            if (raw is not null)
            {
                int mode = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                startMode = mode switch
                {
                    2 => "Automático", 3 => "Manual (sob demanda)", 4 => "Desativado", 0 => "Inicialização do sistema", 1 => "Inicialização precoce", _ => "Tipo " + mode
                };
            }
        }
        catch { }
        items.Add(new SystemInfoEntry("Modo de inicialização do serviço", startMode));
        string lastSuccess = "Não disponível no histórico local consultado";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Install");
            string value = Convert.ToString(key?.GetValue("LastSuccessTime")) ?? "";
            if (value.Length > 0) lastSuccess = value;
        }
        catch { }
        items.Add(new SystemInfoEntry("Última instalação bem-sucedida registrada", lastSuccess));
        items.Add(new SystemInfoEntry("O que esta consulta faz", "Mostra apenas dados locais. Não pesquisa atualizações na Internet e não instala pacotes."));
        items.Add(new SystemInfoEntry("Importante", "O serviço pode estar parado quando não está verificando atualizações. Isso, por si só, não significa falha."));
        return new SystemInfoSnapshot("Windows Update — estado local",
            "Informações locais do serviço e do Registro do Windows. Nenhuma alteração ou busca online foi realizada.", items);
    }

    private static string QueryServiceState(string serviceName)
    {
        try
        {
            IntPtr manager = OpenSCManager(null, null, 0x0001);
            if (manager == IntPtr.Zero) return "Consulta indisponível";
            try
            {
                IntPtr handle = OpenService(manager, serviceName, 0x0004);
                if (handle == IntPtr.Zero) return "Serviço indisponível ou acesso negado";
                try
                {
                    if (!QueryServiceStatus(handle, out var status)) return "Estado não disponível";
                    return status.CurrentState switch
                    {
                        1 => "Parado", 2 => "Iniciando", 3 => "Parando", 4 => "Em execução",
                        5 => "Retomando", 6 => "Pausando", 7 => "Pausado", _ => "Estado não identificado"
                    };
                }
                finally { CloseServiceHandle(handle); }
            }
            finally { CloseServiceHandle(manager); }
        }
        catch { return "Consulta indisponível"; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint ServiceType, CurrentState, ControlsAccepted, Win32ExitCode,
            ServiceSpecificExitCode, CheckPoint, WaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr manager, string serviceName, uint desiredAccess);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatus(IntPtr service, out ServiceStatus status);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr service);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}

