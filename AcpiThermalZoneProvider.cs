using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// Zonas térmicas ACPI expostas pelo firmware são sensores de PLATAFORMA.
// Não são automaticamente a temperatura de CPU, chipset, VRM ou placa-mãe.
internal sealed record AcpiThermalZone(string ZoneName,
    MonitorReading<double> TemperatureC);

internal sealed record AcpiThermalZoneSnapshot(DateTimeOffset CapturedAtUtc,
    HardwareDiscoveryState State, IReadOnlyList<AcpiThermalZone> Zones, string? Limitation);

internal static class AcpiThermalZoneProvider
{
    private const string Source = "Windows WMI root\\WMI MSAcpi_ThermalZoneTemperature";
    private const int MaxZones = 64;

    // ACPI codifica temperatura em décimos de Kelvin. Rejeitar sentinelas
    // e valores implausíveis; isso NÃO demonstra que zona equivale à CPU.
    internal static MonitorReading<double> ConvertTenthsKelvin(object? raw)
    {
        if (raw is not (uint or int or ushort or short or long or ulong))
            return MonitorReading<double>.Unavailable(Source);
        try
        {
            double value = Convert.ToDouble(raw, CultureInfo.InvariantCulture) / 10d - 273.15d;
            return double.IsFinite(value) && value is >= 0 and <= 125
                ? MonitorReading<double>.Available(Math.Round(value, 2), Source)
                : MonitorReading<double>.Unavailable(Source);
        }
        catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
        { return MonitorReading<double>.Unavailable(Source); }
    }

    internal static AcpiThermalZoneSnapshot Capture(CancellationToken token)
    {
        if (!OperatingSystem.IsWindows())
            return Unavailable("Zonas térmicas ACPI disponíveis somente no Windows");
        object? locator = null, service = null, rows = null;
        var zones = new List<AcpiThermalZone>();
        try
        {
            token.ThrowIfCancellationRequested();
            Type? type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (type is null) return Unavailable("COM WMI indisponível");
            locator = Activator.CreateInstance(type);
            service = ((dynamic)locator!).ConnectServer(".", @"root\WMI");
            rows = ((dynamic)service!).ExecQuery(
                "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature",
                "WQL", 16);
            bool partial = false;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object row in (IEnumerable)rows)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (zones.Count >= MaxZones) { partial = true; break; }
                    object? Get(string key)
                    {
                        object? value = ReadWmiValueOrNull(row, key, out bool unreadable);
                        if (unreadable) partial = true;
                        return value;
                    }
                    string name = (Convert.ToString(Get("InstanceName"), CultureInfo.InvariantCulture) ?? "").Trim();
                    if (name.Length == 0 || name.Length > 256 || name.Any(char.IsControl) ||
                        !names.Add(name)) { partial = true; continue; }
                    zones.Add(new(name, ConvertTenthsKelvin(Get("CurrentTemperature"))));
                }
                finally { Release(row); }
            }
            return new(DateTimeOffset.UtcNow,
                partial ? HardwareDiscoveryState.Partial : HardwareDiscoveryState.Complete,
                zones.AsReadOnly(), partial ? "Algumas zonas ACPI não puderam ser identificadas" : null);
        }
        catch (OperationCanceledException) { return Unavailable("Consulta ACPI cancelada"); }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or
            InvalidCastException or InvalidOperationException or ArgumentException or
            System.Security.SecurityException)
        { return Unavailable("Firmware não publicou zonas ACPI consultáveis"); }
        finally { Release(rows); Release(service); Release(locator); }
    }

    // Uma propriedade WMI opcional pode retornar objeto COM nulo em máquinas
    // com zonas ACPI incompletas. Um campo ilegível é inconclusivo, não uma
    // exceção que derruba os demais provedores.
    internal static object? ReadWmiValueOrNull(object? row, string key, out bool unreadable)
    {
        unreadable = false;
        object? properties = null, property = null;
        try
        {
            if (row is null || string.IsNullOrWhiteSpace(key))
            {
                unreadable = true;
                return null;
            }
            properties = ((dynamic)row).Properties_;
            if (properties is null)
            {
                unreadable = true;
                return null;
            }
            property = ((dynamic)properties).Item(key);
            if (property is null)
            {
                unreadable = true;
                return null;
            }
            return ((dynamic)property).Value;
        }
        catch (Exception ex) when (ex is COMException or
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or NullReferenceException)
        {
            unreadable = true;
            return null;
        }
        finally { Release(property); Release(properties); }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.FinalReleaseComObject(value);
    }

    private static AcpiThermalZoneSnapshot Unavailable(string limitation) =>
        new(DateTimeOffset.UtcNow, HardwareDiscoveryState.Unavailable, [], limitation);
}
