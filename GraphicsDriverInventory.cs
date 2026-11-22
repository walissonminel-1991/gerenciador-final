using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// VF02 fase 1: fotografia derivada EXCLUSIVAMENTE do inventário PnP/WMI já lido.
// Não faz rede, não consulta software NVIDIA/AMD/Intel, não instala nem cria
// elegibilidade para DriverUpdatePilot (Display continua proibido).
internal sealed record GraphicsDriverSnapshot(
    string DeviceId, string Manufacturer, string Model, string ModelSource,
    string InstalledWindowsVersion, string VersionSource, string DriverProvider,
    string InfName, string InstalledChannel, string Observation);

internal static class GraphicsDriverInventory
{
    internal const string UnknownChannel = "não identificado";

    private static readonly Regex PciDevice = new(
        @"\APCI\\VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})(?:&[0-9A-Z_]+)*\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static string Manufacturer(string vendorId) => vendorId.ToUpperInvariant() switch
    {
        "10DE" => "NVIDIA",
        "1002" => "AMD",
        "8086" => "Intel",
        _ => ""
    };

    // Conversão documentada pelo fabricante: últimos 5 dígitos da versão
    // do Gerenciador de Dispositivos = número NVIDIA xxx.xx.
    // https://www.nvidia.com/en-gb/drivers/drivers-faq/
    // Só converter WMI confirmado com provedor NVIDIA: nunca infere canal,
    // oferta remota, compatibilidade de pacote ou autorização de Display.
    internal static string? NvidiaPublicVersion(GraphicsDriverSnapshot? gpu)
    {
        if (gpu is null || gpu.Manufacturer != "NVIDIA") return null;
        string provider = gpu.DriverProvider.Trim();
        if (!provider.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase) &&
            !provider.Equals("NVIDIA Corporation", StringComparison.OrdinalIgnoreCase)) return null;
        string[] parts = gpu.InstalledWindowsVersion.Split('.');
        if (parts.Length != 4 ||
            (parts[0] != "31" && parts[0] != "32") ||
            parts[1] != "0" ||
            parts[2].Length != 2 || parts[2][0] != '1' ||
            parts[2][1] < '5' || parts[2][1] > '9' ||
            parts[3].Length != 4 ||
            !parts[3].All(c => c >= '0' && c <= '9')) return null;
        string digits = $"{parts[2][1]}{parts[3]}";
        return $"{digits[..3]}.{digits[3..]}";
    }

    internal static GraphicsDriverSnapshot[] Find(DriverInventoryResult inventory)
    {
        var result = new List<GraphicsDriverSnapshot>();
        foreach (var group in inventory.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.DeviceId))
            .GroupBy(item => item.DeviceId.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            DriverInventoryEntry row = group.First();
            var device = PciDevice.Match(DriverDeviceMetadata.HardwarePart(row.DeviceId));
            if (!device.Success) continue;
            string vendorId = device.Groups[1].Value.ToUpperInvariant();
            string productId = device.Groups[2].Value.ToUpperInvariant();
            string manufacturer = Manufacturer(vendorId);
            if (manufacturer.Length == 0) continue;

            // IDs divergentes da MESMA instância não autorizam atribuir fabricante
            // nem modelo. IDs compatíveis sem DEV não entram na comparação.
            bool conflicting = row.HardwareIds.Any(id =>
            {
                var match = PciDevice.Match(id);
                return match.Success &&
                    (!match.Groups[1].Value.Equals(vendorId, StringComparison.OrdinalIgnoreCase) ||
                     !match.Groups[2].Value.Equals(productId, StringComparison.OrdinalIgnoreCase));
            });
            if (conflicting) continue;

            bool displayClass = row.ReportedDriverClass.Equals("Display", StringComparison.OrdinalIgnoreCase);
            DriverHardwareIdentity identity = DriverHardwareCatalog.Identify(row);
            bool knownRtx5060Ti = vendorId == "10DE" && productId == "2D04" &&
                identity.Model.Contains("GeForce RTX 5060 Ti", StringComparison.OrdinalIgnoreCase);
            // PCI Intel/NVIDIA/AMD também pode representar áudio, USB, rede etc.
            // Sem classe Display, somente um modelo PCI gráfico explicitamente
            // reconhecido no catálogo pode aparecer, e sua versão não é aceita.
            if (!displayClass && (!knownRtx5060Ti ||
                !string.IsNullOrWhiteSpace(row.ReportedDriverClass))) continue;

            string model, modelSource;
            if (knownRtx5060Ti)
            {
                model = identity.Model;
                modelSource = "catálogo PCI (VEN_10DE/DEV_2D04)";
            }
            else if (DriverHardwareCatalog.ReadableReportedName(row.DeviceName))
            {
                model = row.DeviceName.Trim();
                modelSource = "Win32_PnPSignedDriver.DeviceName";
            }
            else if (row.DeviceMetadata is { } info &&
                     DriverHardwareCatalog.ReadableReportedName(info.FriendlyName))
            {
                model = info.FriendlyName.Trim();
                modelSource = "DEVPKEY_Device_FriendlyName do mesmo dispositivo";
            }
            else
            {
                model = "GPU " + manufacturer + " (modelo não identificado)";
                modelSource = "fabricante PCI e classe Display; modelo não comprovado";
            }

            bool unique = group.Count() == 1;
            string version = row.Version.Trim();
            bool hasWmiVersion = unique && !inventory.Partial && displayClass &&
                !row.WithoutSignedDriverAssociation &&
                version.Length is > 0 and <= 48 && Version.TryParse(version, out _);
            string observedVersion = hasWmiVersion ? version : "não confirmada";
            string versionSource = hasWmiVersion
                ? "Win32_PnPSignedDriver.DriverVersion (versão Windows; não número Game Ready/Studio)"
                : "Win32_PnPSignedDriver: associação/versão insuficiente ou ambígua";
            string observation = !unique ? "múltiplas associações para o mesmo DeviceId" :
                inventory.Partial ? "inventário parcial" :
                !displayClass ? "classe Display não confirmada" :
                !hasWmiVersion ? "versão Windows não confirmada" :
                "inventário local somente leitura; não prova atualização disponível";
            result.Add(new(row.DeviceId, manufacturer, model, modelSource,
                observedVersion, versionSource, row.Provider, row.InfName,
                UnknownChannel, observation));
        }
        return result.OrderBy(x => x.Manufacturer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Model, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static string CompactSummary(DriverInventoryResult inventory)
    {
        var graphics = Find(inventory);
        if (graphics.Length == 0) return "";
        var first = graphics[0];
        string extra = graphics.Length > 1 ? $" · +{graphics.Length - 1} GPU(s)" : "";
        return $"GPU: {first.Model} · Driver Windows: {first.InstalledWindowsVersion} · " +
            $"Canal: {first.InstalledChannel}{extra} (ver Detalhes)";
    }
}
