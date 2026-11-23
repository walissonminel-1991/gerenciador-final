using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// Contrato de disco físico do Windows Storage Management. NÃO equivale à
// temperatura da placa-mãe nem a SMART bruto lido por porta privilegiada.
internal sealed record StorageDeviceSensors(
    string DeviceId,
    string Name,
    MonitorReading<double> TemperatureC,
    MonitorReading<double> WearPercent,
    MonitorReading<ulong> PowerOnHours);

internal sealed record StorageSensorsSnapshot(
    DateTimeOffset CapturedAtUtc,
    HardwareDiscoveryState State,
    IReadOnlyList<StorageDeviceSensors> Disks,
    string? Limitation);

internal sealed record StorageIdentity(string DeviceId, string FriendlyName);
internal sealed record StorageReliabilityRaw(string DeviceId, object? Temperature,
    object? Wear, object? PowerOnHours);

internal static class WindowsStorageSensorProvider
{
    private const string Source = "Windows Storage MSFT_StorageReliabilityCounter";
    private const int MaxRows = 256;

    internal static MonitorReading<double> Temperature(object? input)
    {
        // WMI retorna UInt8 nullable. Zero não é temperatura comprovada.
        uint? value = ParseNumber(input);
        return value is > 0 and <= 125
            ? MonitorReading<double>.Available(value.Value, Source + ".Temperature")
            : MonitorReading<double>.Unavailable(Source + ".Temperature");
    }

    internal static MonitorReading<double> Wear(object? input)
    {
        // Campo Wear mede desgaste relatado pelo disco; NÃO é saúde restante.
        // Zero pode ser valor-padrão WMI de hardware sem contador, então
        // manter inconclusivo em vez de afirmar "0% de desgaste".
        uint? value = ParseNumber(input);
        return value is > 0 and <= 100
            ? MonitorReading<double>.Available(value.Value, Source + ".Wear")
            : MonitorReading<double>.Unavailable(Source + ".Wear");
    }

    internal static MonitorReading<ulong> Hours(object? input)
    {
        uint? value = ParseNumber(input);
        return value is > 0 and <= 65_535
            ? MonitorReading<ulong>.Available(value.Value, Source + ".PowerOnHours")
            : MonitorReading<ulong>.Unavailable(Source + ".PowerOnHours");
    }

    private static uint? ParseNumber(object? input)
    {
        if (input is null or DBNull) return null;
        if (input is not (byte or ushort or uint or int or long or ulong))
            return null;
        try { return Convert.ToUInt32(input, CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
        { return null; }
    }

    // Juntar SOMENTE pelo mesmo DeviceId. Duplicatas impedem atribuir
    // a temperatura de um disco ao outro; nenhuma heurística por nome/ordem.
    internal static StorageSensorsSnapshot Join(
        IEnumerable<StorageIdentity> physical,
        IEnumerable<StorageReliabilityRaw> reliability,
        bool sourcePartial = false)
    {
        var physicalRows = physical.Take(MaxRows + 1).ToArray();
        var sensorRows = reliability.Take(MaxRows + 1).ToArray();
        bool partial = sourcePartial || physicalRows.Length > MaxRows || sensorRows.Length > MaxRows;
        var groups = sensorRows.Where(x => !string.IsNullOrWhiteSpace(x.DeviceId))
            .GroupBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
        var duplicatePhysicalIds = physicalRows
            .Where(x => !string.IsNullOrWhiteSpace(x.DeviceId))
            .GroupBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (duplicatePhysicalIds.Count > 0) partial = true;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var devices = new List<StorageDeviceSensors>();
        foreach (var disk in physicalRows.Take(MaxRows))
        {
            if (string.IsNullOrWhiteSpace(disk.DeviceId) ||
                duplicatePhysicalIds.Contains(disk.DeviceId) || !ids.Add(disk.DeviceId))
            {
                partial = true; continue;
            }
            string name = string.IsNullOrWhiteSpace(disk.FriendlyName)
                ? "Disco físico (nome indisponível)" : disk.FriendlyName;
            if (groups.TryGetValue(disk.DeviceId, out var rows) && rows.Length == 1)
            {
                var r = rows[0];
                devices.Add(new(disk.DeviceId, name, Temperature(r.Temperature),
                    Wear(r.Wear), Hours(r.PowerOnHours)));
            }
            else
            {
                if (rows is { Length: > 1 }) partial = true;
                devices.Add(new(disk.DeviceId, name,
                    Temperature(null), Wear(null), Hours(null)));
            }
        }
        if (groups.Keys.Any(id => !ids.Contains(id))) partial = true;
        return new(DateTimeOffset.UtcNow,
            partial ? HardwareDiscoveryState.Partial : HardwareDiscoveryState.Complete,
            devices.AsReadOnly(),
            partial ? "Armazenamento parcial ou identidade contraditória" : null);
    }

    // Somente leitura via COM WMI já usado por DriverInventory, mas namespace
    // independente root\Microsoft\Windows\Storage. Chamar via STA e apenas
    // sob demanda, fora do timer de 2 s e fora da thread da interface.
    internal static StorageSensorsSnapshot Capture(CancellationToken token)
    {
        if (!OperatingSystem.IsWindows())
            return Unavailable("Storage WMI disponível apenas no Windows");

        object? locator = null, service = null;
        try
        {
            token.ThrowIfCancellationRequested();
            Type? type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (type is null) return Unavailable("Provedor COM WMI indisponível");
            locator = Activator.CreateInstance(type);
            service = ((dynamic)locator!).ConnectServer(".", @"root\Microsoft\Windows\Storage");

            var physical = new List<StorageReferencedPhysical>();
            var reliability = new List<StorageReferencedReliability>();
            var associations = new List<StorageAssociationReference>();
            var clock = Stopwatch.StartNew();
            bool partial = false;

            void Query(string sql, Action<Func<string, object?>, string?> read)
            {
                object? result = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    result = ((dynamic)service!).ExecQuery(sql, "WQL", 16);
                    int scanned = 0;
                    foreach (object row in (IEnumerable)result)
                    {
                        try
                        {
                            if (++scanned > MaxRows || clock.Elapsed > TimeSpan.FromSeconds(12))
                            { partial = true; break; }
                            token.ThrowIfCancellationRequested();
                            object? Get(string key)
                            {
                                object? properties = null, property = null;
                                try
                                {
                                    properties = ((dynamic)row).Properties_;
                                    property = ((dynamic)properties).Item(key);
                                    return ((dynamic)property).Value;
                                }
                                catch (COMException) { partial = true; return null; }
                                finally { Release(property); Release(properties); }
                            }
                            // RelPath da PRÓPRIA instância WMI é comparado às
                            // referências da associação, sem parsing de DeviceId.
                            string? path = ReadRelPath(row);
                            if (path is null) partial = true;
                            read(Get, path);
                        }
                        finally { Release(row); }
                    }
                }
                finally { Release(result); }
            }

            Query("SELECT DeviceId, FriendlyName FROM MSFT_PhysicalDisk", (get, path) =>
            {
                string id = Convert.ToString(get("DeviceId"), CultureInfo.InvariantCulture)?.Trim() ?? "";
                if (id.Length == 0) { partial = true; return; }
                physical.Add(new(new StorageIdentity(id,
                    Convert.ToString(get("FriendlyName"), CultureInfo.InvariantCulture) ?? ""), path));
            });
            Query("SELECT DeviceId, Temperature, Wear, PowerOnHours FROM MSFT_StorageReliabilityCounter",
                (get, path) =>
            {
                string id = Convert.ToString(get("DeviceId"), CultureInfo.InvariantCulture)?.Trim() ?? "";
                if (id.Length == 0) { partial = true; return; }
                reliability.Add(new(new StorageReliabilityRaw(id, get("Temperature"),
                    get("Wear"), get("PowerOnHours")), path));
            });
            // REF é a evidência oficial de associação, mais forte que DeviceId
            // coincidente. Falha na classe de associação NÃO libera fallback.
            try
            {
                Query("SELECT PhysicalDisk, StorageReliabilityCounter FROM " +
                    "MSFT_PhysicalDiskToStorageReliabilityCounter", (get, _) =>
                {
                    associations.Add(new(
                        Convert.ToString(get("PhysicalDisk"), CultureInfo.InvariantCulture),
                        Convert.ToString(get("StorageReliabilityCounter"), CultureInfo.InvariantCulture)));
                });
            }
            catch (COMException) { partial = true; }
            return StorageAssociationLinker.Join(physical, reliability, associations, partial);
        }
        catch (OperationCanceledException) { return Unavailable("Consulta Storage cancelada"); }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or
            InvalidCastException or InvalidOperationException or ArgumentException or
            System.Security.SecurityException)
        {
            return Unavailable("Provedor Storage/WMI indisponível ou acesso negado");
        }
        finally { Release(service); Release(locator); }
    }

    private static string? ReadRelPath(object row)
    {
        object? path = null;
        try
        {
            path = ((dynamic)row).Path_;
            string? value = Convert.ToString(((dynamic)path!).RelPath, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch { return null; } // Sem Path_ válido, sem relação confiável.
        finally { Release(path); }
    }

    private static void Release(object? obj)
    {
        if (obj is not null && Marshal.IsComObject(obj))
            Marshal.FinalReleaseComObject(obj);
    }

    private static StorageSensorsSnapshot Unavailable(string reason) =>
        new(DateTimeOffset.UtcNow, HardwareDiscoveryState.Unavailable,
            [], reason);
}
