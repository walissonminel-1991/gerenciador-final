using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal static class StaWorker
{
    public static Task<T> Run<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.TrySetResult(action()); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }) { IsBackground = true, Name = "Gerenciador - consulta Windows" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}

internal static class DetailedHardware
{
    // WMI padrão do Windows; nenhum programa de terceiros é instalado.
    private static List<Dictionary<string, string>> Wmi(string query, params string[] columns)
    {
        var result = new List<Dictionary<string, string>>();
        Type? type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
        if (type is null) return result;
        object? locator = null, services = null, rows = null;
        try
        {
            locator = Activator.CreateInstance(type);
            dynamic connection = locator!;
            services = connection.ConnectServer(".", @"root\cimv2");
            dynamic scope = services;
            rows = scope.ExecQuery(query);
            foreach (object item in (System.Collections.IEnumerable)rows)
            {
                dynamic entry = item;
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string col in columns)
                {
                    try
                    {
                        object? val = entry.Properties_.Item(col).Value;
                        dict[col] = val is null ? "" : Convert.ToString(val, CultureInfo.CurrentCulture) ?? "";
                    }
                    catch { dict[col] = ""; }
                }
                result.Add(dict);
                if (result.Count >= 16) break;
                Marshal.FinalReleaseComObject(item);
            }
        }
        finally
        {
            if (rows is not null && Marshal.IsComObject(rows)) Marshal.FinalReleaseComObject(rows);
            if (services is not null && Marshal.IsComObject(services)) Marshal.FinalReleaseComObject(services);
            if (locator is not null && Marshal.IsComObject(locator)) Marshal.FinalReleaseComObject(locator);
        }
        return result;
    }

    private static string Get(Dictionary<string, string> row, string key, string fallback = "Não informado")
        => row.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

    private static string Capacity(string raw)
        => ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong bytes)
            ? (bytes / 1073741824d).ToString("N1", CultureInfo.CurrentCulture) + " GB"
            : "Não informado";

    public static SystemInfoSnapshot Capture()
    {
        var info = IntegratedSystemReader.Computer();
        var cards = new List<SystemInfoEntry>(info.Entries);
        var warnings = new List<string>();
        void Safely(string category, Action append)
        {
            try { append(); }
            catch (Exception ex) { warnings.Add(category + ": " + ex.Message); }
        }
        Safely("CPU", () =>
        {
            foreach (var cpu in Wmi("SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize, SocketDesignation FROM Win32_Processor",
                "Name", "Manufacturer", "NumberOfCores", "NumberOfLogicalProcessors", "MaxClockSpeed", "L2CacheSize", "L3CacheSize", "SocketDesignation"))
            {
                cards.Add(new SystemInfoEntry("CPU · " + Get(cpu, "Name"),
                    $"Fabricante: {Get(cpu, "Manufacturer")} | Núcleos: {Get(cpu, "NumberOfCores")} | Threads: {Get(cpu, "NumberOfLogicalProcessors")}\n" +
                    $"Clock máximo informado: {Get(cpu, "MaxClockSpeed")} MHz | L2: {Get(cpu, "L2CacheSize")} KB | L3: {Get(cpu, "L3CacheSize")} KB | Socket: {Get(cpu, "SocketDesignation")}"));
            }
        });
        Safely("GPU", () =>
        {
            foreach (var gpu in Wmi("SELECT Name, VideoProcessor, DriverVersion, AdapterRAM, CurrentHorizontalResolution, CurrentVerticalResolution, AdapterCompatibility FROM Win32_VideoController",
                "Name", "VideoProcessor", "DriverVersion", "AdapterRAM", "CurrentHorizontalResolution", "CurrentVerticalResolution", "AdapterCompatibility"))
            {
                string resolution = Get(gpu, "CurrentHorizontalResolution", "?") + " × " + Get(gpu, "CurrentVerticalResolution", "?");
                cards.Add(new SystemInfoEntry("GPU · " + Get(gpu, "Name"),
                    $"Fabricante: {Get(gpu, "AdapterCompatibility")} | Processador gráfico: {Get(gpu, "VideoProcessor")}\n" +
                    $"Driver: {Get(gpu, "DriverVersion")} | Resolução: {resolution} | VRAM WMI: {Capacity(Get(gpu, "AdapterRAM", ""))} (pode ser imprecisa)"));
            }
        });
        Safely("Placa-mãe", () =>
        {
            foreach (var board in Wmi("SELECT Manufacturer, Product, Version FROM Win32_BaseBoard", "Manufacturer", "Product", "Version"))
                cards.Add(new SystemInfoEntry("Placa-mãe", $"{Get(board, "Manufacturer")} · {Get(board, "Product")} · revisão {Get(board, "Version")}"));
        });
        Safely("BIOS", () =>
        {
            foreach (var bios in Wmi("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", "Manufacturer", "SMBIOSBIOSVersion", "ReleaseDate"))
                cards.Add(new SystemInfoEntry("BIOS / UEFI", $"{Get(bios, "Manufacturer")} | Versão: {Get(bios, "SMBIOSBIOSVersion")} | Data WMI: {Get(bios, "ReleaseDate")}"));
        });
        Safely("Memória RAM", () =>
        {
            foreach (var ram in Wmi("SELECT Manufacturer, PartNumber, Capacity, ConfiguredClockSpeed, Speed, DeviceLocator, SMBIOSMemoryType FROM Win32_PhysicalMemory",
                "Manufacturer", "PartNumber", "Capacity", "ConfiguredClockSpeed", "Speed", "DeviceLocator", "SMBIOSMemoryType"))
                cards.Add(new SystemInfoEntry("RAM · " + Get(ram, "DeviceLocator"),
                    $"{Capacity(Get(ram, "Capacity", ""))} | {Get(ram, "Manufacturer")} | {Get(ram, "PartNumber")}\n" +
                    $"Clock configurado: {Get(ram, "ConfiguredClockSpeed")} MT/s | Nominal: {Get(ram, "Speed")} MT/s | Tipo SMBIOS: {Get(ram, "SMBIOSMemoryType")}"));
        });
        Safely("Unidades físicas", () =>
        {
            foreach (var disk in Wmi("SELECT Model, InterfaceType, Size, MediaType FROM Win32_DiskDrive", "Model", "InterfaceType", "Size", "MediaType"))
                cards.Add(new SystemInfoEntry("Disco · " + Get(disk, "Model"),
                    $"Capacidade: {Capacity(Get(disk, "Size", ""))} | Interface WMI: {Get(disk, "InterfaceType")} | Tipo: {Get(disk, "MediaType")}"));
        });
        string summary = "Detalhes de CPU, GPU, placa-mãe, BIOS, módulos RAM e discos. Dados reportados pelo Windows/WMI; sem temperaturas ou sensores de ventoinha.";
        if (warnings.Count > 0) summary += " Algumas consultas não foram possíveis: " + string.Join("; ", warnings.Take(2));
        return new SystemInfoSnapshot("Este computador · hardware detalhado", summary, cards);
    }
}

internal sealed record StartupItem(string Name, string Details, string Kind, string Locator, bool Enabled, bool Editable);

internal static class StartupManager
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string DisabledKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunDisabledByGerenciador";

    public static IReadOnlyList<StartupItem> Read()
    {
        var list = new List<StartupItem>();
        using (var current = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
        {
            void Load(string keyPath, bool enabled)
            {
                using var key = current.OpenSubKey(keyPath);
                if (key is null) return;
                foreach (string name in key.GetValueNames())
                {
                    try
                    {
                        object? raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (raw is null) continue;
                        list.Add(new StartupItem(name, "Meu usuário · Registro Run · " + raw,
                            "registry", name, enabled, true));
                    }
                    catch { }
                }
            }
            Load(RunKey, true);
            Load(DisabledKey, false);
        }
        string user = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (Directory.Exists(user))
        {
            foreach (string path in Directory.EnumerateFiles(user))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                bool disabled = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                string sourceExt = disabled ? Path.GetExtension(path[..^9]).ToLowerInvariant() : ext;
                if (sourceExt is not (".lnk" or ".url" or ".exe" or ".bat" or ".cmd")) continue;
                list.Add(new StartupItem(Path.GetFileNameWithoutExtension(disabled ? path[..^9] : path),
                    "Meu usuário · Pasta Inicializar · " + path, "file", path, !disabled, true));
            }
        }
        void Machine(RegistryView view, string label)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = root.OpenSubKey(RunKey);
                if (key is null) return;
                foreach (string name in key.GetValueNames())
                    list.Add(new StartupItem(name, label + " · " + Convert.ToString(key.GetValue(name)),
                        "machine", name, true, false));
            }
            catch { }
        }
        Machine(Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32, "Todos os usuários · Registro 64 bits · somente leitura");
        if (Environment.Is64BitOperatingSystem) Machine(RegistryView.Registry32, "Todos os usuários · Registro 32 bits · somente leitura");
        try
        {
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
            foreach (string path in Directory.EnumerateFiles(common))
                list.Add(new StartupItem(Path.GetFileName(path), "Todos os usuários · Pasta Inicializar · somente leitura", "machine", path, true, false));
        }
        catch { }
        return list.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static void Toggle(StartupItem item)
    {
        if (!item.Editable) throw new InvalidOperationException("Esta entrada pertence a todos os usuários e não pode ser modificada nesta versão.");
        if (item.Kind == "file")
        {
            string target = item.Enabled ? item.Locator + ".disabled" : item.Locator[..^9];
            if (!File.Exists(item.Locator)) throw new FileNotFoundException("O arquivo de inicialização não existe mais.");
            if (File.Exists(target)) throw new IOException("Já existe um arquivo no local de destino. Não foi feita alteração.");
            File.Move(item.Locator, target);
            return;
        }
        if (item.Kind != "registry") throw new NotSupportedException();
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        string origin = item.Enabled ? RunKey : DisabledKey;
        string destination = item.Enabled ? DisabledKey : RunKey;
        using var source = root.OpenSubKey(origin, writable: true)
            ?? throw new IOException("Entrada de inicialização não está mais disponível.");
        object? value = source.GetValue(item.Locator, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null) throw new IOException("A entrada foi removida por outro programa.");
        RegistryValueKind kind = source.GetValueKind(item.Locator);
        using var dest = root.CreateSubKey(destination, writable: true)
            ?? throw new UnauthorizedAccessException("Sem permissão para atualizar a inicialização.");
        if (dest.GetValueNames().Contains(item.Locator, StringComparer.OrdinalIgnoreCase))
            throw new IOException("Já existe uma entrada com o mesmo nome no destino.");
        dest.SetValue(item.Locator, value, kind);
        // Depois da cópia confirmada, remove a origem; em falha, a cópia continua recuperável.
        source.DeleteValue(item.Locator, throwOnMissingValue: true);
        if (!item.Enabled && source.ValueCount == 0) root.DeleteSubKey(DisabledKey, throwOnMissingSubKey: false);
    }
}

internal sealed record WinUpdateChoice(string Id, string Title, string Kb, bool Downloaded, bool EulaAccepted);
internal static class WindowsUpdateClient
{
    public static Task<IReadOnlyList<WinUpdateChoice>> ScanAsync() => StaWorker.Run<IReadOnlyList<WinUpdateChoice>>(() =>
    {
        dynamic session = CreateSession();
        dynamic searcher = session.CreateUpdateSearcher();
        searcher.Online = true;
        dynamic found = searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Software'");
        int count = (int)found.Updates.Count;
        var choices = new List<WinUpdateChoice>();
        for (int i = 0; i < count; i++)
        {
            dynamic entry = found.Updates.Item(i);
            string kb = "";
            try { foreach (object number in entry.KBArticleIDs) { kb += "KB" + number + " "; } } catch { }
            string rawId = (string)entry.Identity.UpdateID;
            string id = Guid.TryParse(rawId, out Guid parsed) ? parsed.ToString("D") : rawId;
            choices.Add(new WinUpdateChoice(id,
                (string)entry.Title, kb.Trim(), (bool)entry.IsDownloaded, (bool)entry.EulaAccepted));
        }
        return choices;
    });

    public static Task<int> InstallAsync(IReadOnlySet<string> ids, Action<string> log) => StaWorker.Run(() =>
    {
        if (ids.Count == 0) return 2;
        dynamic session = CreateSession();
        dynamic searcher = session.CreateUpdateSearcher();
        searcher.Online = true;
        log("Verificando novamente as atualizações selecionadas antes de instalar...");
        dynamic found = searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Software'");
        Type collectionType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")
            ?? throw new NotSupportedException("A coleção do Windows Update não está disponível.");
        dynamic selected = Activator.CreateInstance(collectionType)!;
        for (int i = 0; i < (int)found.Updates.Count; i++)
        {
            dynamic entry = found.Updates.Item(i);
            string rawId = (string)entry.Identity.UpdateID;
            string id = Guid.TryParse(rawId, out Guid parsed) ? parsed.ToString("D") : rawId;
            if (!ids.Contains(id)) continue;
            if (!(bool)entry.EulaAccepted)
            {
                // O programa não aceita contratos adicionais silenciosamente.
                log("Ignorada (licença ainda não aceita): " + (string)entry.Title);
                continue;
            }
            selected.Add(entry);
            log("Selecionada: " + (string)entry.Title);
        }
        if ((int)selected.Count == 0) { log("Nenhuma atualização elegível para instalação."); return 2; }
        log("Baixando " + (int)selected.Count + " atualizações pelo serviço oficial do Windows...");
        dynamic downloader = session.CreateUpdateDownloader();
        downloader.Updates = selected;
        dynamic downloadResult = downloader.Download();
        log("Resultado do download: " + Convert.ToString(downloadResult.ResultCode));
        log("Instalando atualizações baixadas. O Windows poderá solicitar etapas adicionais...");
        dynamic installer = session.CreateUpdateInstaller();
        installer.Updates = selected;
        dynamic result = installer.Install();
        int status = (int)result.ResultCode;
        log("Resultado da instalação: " + status + " (2=sucesso, 3=parcial, 4=falha, 5=cancelado)");
        if ((bool)result.RebootRequired) log("REINICIALIZAÇÃO NECESSÁRIA: salve o trabalho e reinicie quando puder. Nenhum reinício foi solicitado pelo aplicativo.");
        return status == 2 ? 0 : 1;
    });

    private static dynamic CreateSession()
    {
        Type type = Type.GetTypeFromProgID("Microsoft.Update.Session")
            ?? throw new NotSupportedException("Windows Update Agent indisponível nesta versão do Windows.");
        return Activator.CreateInstance(type)!;
    }
}

internal sealed class ProcessHistorySampler
{
    private readonly Dictionary<int, (string Name, TimeSpan Cpu, DateTime At, Queue<double> CpuSamples, long Ram)> _history = new();
    private int _samples;

    public SystemInfoSnapshot TakeSample()
    {
        var now = DateTime.UtcNow;
        var seen = new HashSet<int>();
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    int pid = process.Id;
                    seen.Add(pid);
                    string name = process.ProcessName;
                    TimeSpan cpuTime = process.TotalProcessorTime;
                    long memory = process.WorkingSet64;
                    if (!_history.TryGetValue(pid, out var previous) || previous.Cpu > cpuTime || previous.Name != name)
                    {
                        _history[pid] = (name, cpuTime, now, new Queue<double>(), memory);
                        continue;
                    }
                    double elapsed = Math.Max(0.1, (now - previous.At).TotalSeconds);
                    double used = Math.Clamp((cpuTime - previous.Cpu).TotalSeconds / (elapsed * Math.Max(1, Environment.ProcessorCount)) * 100, 0, 100);
                    previous.CpuSamples.Enqueue(used);
                    if (previous.CpuSamples.Count > 36) previous.CpuSamples.Dequeue();
                    _history[pid] = (name, cpuTime, now, previous.CpuSamples, memory);
                }
                catch { }
            }
        }
        foreach (int oldPid in _history.Keys.Where(k => !seen.Contains(k)).ToArray()) _history.Remove(oldPid);
        _samples++;
        var ordered = _history.Select(k => (Pid: k.Key, k.Value.Name, k.Value.Ram, Samples: k.Value.CpuSamples.ToArray()))
            .Where(x => x.Samples.Length > 0)
            .OrderByDescending(x => Math.Max(x.Samples.Max(), x.Samples.Average() * 1.5))
            .ThenByDescending(x => x.Ram).Take(14).ToList();
        var cards = ordered.Select(x =>
        {
            string bars = string.Concat(x.Samples.TakeLast(26).Select(v => "▁▂▃▄▅▆▇█"[Math.Clamp((int)Math.Round(v / 12.5), 0, 7)]));
            return new SystemInfoEntry(x.Name + " · PID " + x.Pid,
                $"CPU média: {x.Samples.Average():N1}% · Pico: {x.Samples.Max():N1}% · RAM: {x.Ram / 1048576d:N0} MB\nHistórico CPU: {bars}");
        }).ToArray();
        return new SystemInfoSnapshot("Processos · histórico em memória",
            $"{_samples} coleta(s); até 36 amostras por processo (aprox. 3 minutos). CPU em % do total de processadores lógicos. Histórico descartado ao fechar o programa.",
            cards.Length > 0 ? cards : new[] { new SystemInfoEntry("Aguardando dados", "A primeira medição estabelece a referência. Aguarde alguns segundos para ver CPU e memória.") });
    }
}
