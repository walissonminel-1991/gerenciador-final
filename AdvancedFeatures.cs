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
    private static List<Dictionary<string, string>> Wmi(string query, params string[] columns) =>
        ReadLocalWmi(query, columns) ?? [];

    // Consulta WMI local. Null significa que o provedor não está disponível.
    internal static List<Dictionary<string, string>>? ReadLocalWmi(string query, params string[] columns)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var result = new List<Dictionary<string, string>>();
        Type? type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
        if (type is null) return null;
        object? locator = null, services = null, rows = null;
        try
        {
            locator = Activator.CreateInstance(type);
            dynamic connection = locator!;
            services = connection.ConnectServer(".", @"root\cimv2");
            dynamic scope = services;
            rows = scope.ExecQuery(query);
            if (rows is null) return null;
            foreach (object item in (System.Collections.IEnumerable)rows)
            {
                try
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
                }
                finally { if (Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item); }
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

internal static class WindowsUpdateClient
{
    internal static string? VerifiedIdentity(string? rawId, int revision) =>
        WindowsUpdateSearchPolicy.VerifiedIdentity(rawId, revision);
    internal static string? CanonicalSelectedIdentity(string? value) =>
        WindowsUpdateSearchPolicy.CanonicalSelectedIdentity(value);

    // Consulta exclusivamente o registro local do agente; não chama Search,
    // Download, Install, nem altera as opções do Windows Update.
    internal static Task<string> ReadHistoryAsync() =>
        StaWorker.Run(() => ReadHistory(() => (object)CreateSession()));

    internal static string ReadHistory(Func<object> createSession)
    {
        const int limit = 50;
        dynamic searcher = ((dynamic)createSession()).CreateUpdateSearcher();
        int total = (int)searcher.GetTotalHistoryCount();
        if (total < 0) throw new InvalidDataException("Contagem inválida de histórico WUA.");
        if (total == 0) return "Nenhum evento no histórico consultado do Windows Update.";
        int requested = Math.Min(total, limit);
        dynamic entries = searcher.QueryHistory(0, requested);
        int count = (int)entries.Count;
        if (count < 0 || count > requested) throw new InvalidDataException("Coleção inválida de histórico WUA.");
        var lines = new List<string>
        {
            $"Windows Update · {count} registro(s) retornado(s) de {total} informado(s).",
            "Eventos históricos não comprovam que a atualização permanece instalada."
        };
        if (total > count)
            lines.Add("Exibição parcial dos registros mais recentes.");
        for (int i = 0; i < count; i++)
        {
            dynamic entry = entries.Item(i);
            string title = ((string?)entry.Title ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (title.Length > 240) title = title[..240] + "…";
            if (title.Length == 0) title = "Título não informado";
            DateTime date = (DateTime)entry.Date;
            string operation = (int)entry.Operation switch
            {
                1 => "Instalação", 2 => "Desinstalação", _ => "Operação não identificada"
            };
            var outcome = new WindowsUpdateNativeResult((int)entry.ResultCode, (int)entry.HResult, null);
            lines.Add($"{date:dd/MM/yyyy HH:mm} · {operation} · {outcome.Description}" +
                WindowsUpdateOperation.ErrorText(outcome.Error) + Environment.NewLine + title);
        }
        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    public static Task<WindowsUpdateScan> ScanAsync() => StaWorker.Run(() =>
    {
        dynamic session = CreateSession();
        dynamic updates = SearchPendingSoftware((object)session);
        int count = (int)updates.Count;
        var choices = new List<WinUpdateChoice>();
        for (int i = 0; i < count; i++)
        {
            dynamic entry = updates.Item(i);
            string kb = "";
            try { foreach (object number in entry.KBArticleIDs) { kb += "KB" + number + " "; } } catch { }
            string id = ReadUpdateIdentity((object)entry);
            choices.Add(new WinUpdateChoice(id,
                (string)entry.Title, kb.Trim(), (bool)entry.IsDownloaded, (bool)entry.EulaAccepted));
        }
        return new WindowsUpdateScan(WindowsUpdateSearchPolicy.ValidateOffers(choices), ReadPendingRestart());
    });

    public static Task<WindowsUpdateOperation> InstallAsync(IReadOnlySet<string> ids,
        Action<WindowsUpdatePhase> reportPhase) => StaWorker.Run(() =>
    {
        reportPhase(WindowsUpdatePhase.Revalidate);
        try
        {
            using var gate = new Mutex(false, @"Global\GerenciadorFinal.WindowsUpdate.Software");
            bool entered;
            try { entered = gate.WaitOne(0); }
            catch (AbandonedMutexException) { entered = true; }
            if (!entered) return WindowsUpdateOperation.Blocked(WindowsUpdateOutcome.Busy);

            try { return InstallSelected(ids, reportPhase, () => (object)CreateSession(), CreateCollection); }
            finally { gate.ReleaseMutex(); }
        }
        catch (Exception ex)
        {
            return WindowsUpdateOperation.Blocked(WindowsUpdateOutcome.PreflightInconclusive, ex.HResult);
        }
    });

    internal static WindowsUpdateOperation InstallSelected(IReadOnlySet<string> ids,
        Action<WindowsUpdatePhase> reportPhase, Func<object> createSession, Func<object> createCollection)
    {
        WindowsUpdateOutcome failure = WindowsUpdateOutcome.PreflightInconclusive;
        try
        {
            dynamic session = createSession();
            dynamic updates = SearchPendingSoftware((object)session);
            var matched = new List<string>();
            var entries = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var selectedUpdates = ids.Select(id => id[..36]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < (int)updates.Count; i++)
            {
                object entry = updates.Item(i);
                string id = ReadUpdateIdentity(entry);
                if (!selectedUpdates.Contains(id[..36])) continue;
                matched.Add(id);
                entries.TryAdd(id, entry);
            }
            if (!WindowsUpdateSearchPolicy.MatchesSelection(ids, matched))
                return WindowsUpdateOperation.Blocked(WindowsUpdateOutcome.SelectionChanged);

            dynamic selected = createCollection();
            var orderedEntries = ids.Order(StringComparer.Ordinal).Select(id => entries[id]).ToArray();
            foreach (dynamic entry in orderedEntries)
            {
                // Só o Windows Update pode obter o aceite de uma licença pendente.
                if (!(bool)entry.EulaAccepted)
                    return WindowsUpdateOperation.Blocked(WindowsUpdateOutcome.LicensePending);
                if (ids.Count > 1 && (int)entry.InstallationBehavior.Impact == 2) // iiRequiresExclusiveHandling
                    return WindowsUpdateOperation.Blocked(WindowsUpdateOutcome.ExclusiveUpdate);
                selected.Add(entry);
            }
            dynamic installer = session.CreateUpdateInstaller();
            if (CheckInstaller((object)installer) is { } beforeDownload) return beforeDownload;

            dynamic downloader = session.CreateUpdateDownloader();
            downloader.Updates = selected;
            reportPhase(WindowsUpdatePhase.Download);
            failure = WindowsUpdateOutcome.Download;
            var download = ReadOperationResult((object)downloader.Download(), ids.Count, isInstallation: false);
            var items = download.Items.ToArray();
            for (int i = 0; i < items.Length; i++)
            {
                if (!items[i].Succeeded) continue;
                try
                {
                    if (!(bool)((dynamic)orderedEntries[i]).IsDownloaded) items[i] = new(0, 0, null);
                }
                catch (Exception ex) { items[i] = new(-1, ex.HResult, null); }
            }
            download = download with { Items = items };
            if (!download.FullySucceeded) return download;

            failure = WindowsUpdateOutcome.PreflightInconclusive;
            installer.Updates = selected;
            if (CheckInstaller((object)installer) is { } beforeInstall) return beforeInstall;

            reportPhase(WindowsUpdatePhase.Install);
            failure = WindowsUpdateOutcome.Installation;
            return ReadOperationResult((object)installer.Install(), ids.Count, isInstallation: true);
        }
        catch (Exception ex)
        {
            return WindowsUpdateOperation.Blocked(failure, ex.HResult);
        }
    }

    private static object CreateCollection()
    {
        Type collectionType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")
            ?? throw new NotSupportedException("A coleção do Windows Update não está disponível.");
        return Activator.CreateInstance(collectionType)!;
    }

    private static WindowsUpdateOperation? CheckInstaller(object value)
    {
        dynamic installer = value;
        if ((bool)installer.RebootRequiredBeforeInstallation)
            return WindowsUpdateOperation.Blocked(WindowsUpdateOutcome.RestartPending);
        if ((bool)installer.IsBusy)
            return WindowsUpdateOperation.Blocked(WindowsUpdateOutcome.Busy);
        return null;
    }

    private static WindowsUpdateOperation ReadOperationResult(object value, int count, bool isInstallation)
    {
        dynamic result = value;
        var overall = ReadNativeResult(value, isInstallation);
        var items = new WindowsUpdateNativeResult[count];
        for (int i = 0; i < count; i++)
        {
            try { items[i] = ReadNativeResult((object)result.GetUpdateResult(i), isInstallation); }
            catch (Exception ex) { items[i] = new(-1, ex.HResult, null); }
        }
        return new(isInstallation ? WindowsUpdateOutcome.Installation : WindowsUpdateOutcome.Download, overall, items);
    }

    private static WindowsUpdateNativeResult ReadNativeResult(object value, bool isInstallation)
    {
        dynamic result = value;
        bool? reboot = null;
        if (isInstallation)
        {
            try { reboot = (bool)result.RebootRequired; }
            catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
        }
        try
        {
            int code = (int)result.ResultCode;
            return new(code is >= 0 and <= 5 ? code : -1, (int)result.HResult, reboot);
        }
        catch (Exception ex) { return new(-1, ex.HResult, reboot); }
    }

    private static bool? ReadPendingRestart()
    {
        try
        {
            Type? type = Type.GetTypeFromProgID("Microsoft.Update.SystemInfo");
            if (type is null) return null;
            dynamic info = Activator.CreateInstance(type)!;
            return (bool)info.RebootRequired;
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return null;
        }
    }

    private static object SearchPendingSoftware(object session)
    {
        dynamic searcher = ((dynamic)session).CreateUpdateSearcher();
        searcher.Online = true;
        dynamic found = searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Software'");
        WindowsUpdateSearchPolicy.RequireSuccessfulSearch((int)found.ResultCode);
        return found.Updates;
    }

    private static string ReadUpdateIdentity(object update)
    {
        dynamic identity = ((dynamic)update).Identity;
        return VerifiedIdentity((string)identity.UpdateID, (int)identity.RevisionNumber)
            ?? throw new InvalidDataException("Oferta do Windows Update sem identidade válida.");
    }

    private static dynamic CreateSession()
    {
        Type type = Type.GetTypeFromProgID("Microsoft.Update.Session")
            ?? throw new NotSupportedException("Windows Update Agent indisponível nesta versão do Windows.");
        return Activator.CreateInstance(type)!;
    }
}
