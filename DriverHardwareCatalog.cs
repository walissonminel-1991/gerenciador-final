using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

internal sealed record DriverHardwareIdentity(string Vendor, string Model, string Subsystem, bool UsbInterface)
{
    internal static readonly DriverHardwareIdentity Unknown = new("", "", "", false);
}

// Metadados consultivos para a tela. Os IDs, nomes WMI e vínculos de INF
// do inventário continuam sendo a evidência usada pelo Windows e pelo WUA.
internal static partial class DriverHardwareCatalog
{
    internal const string UnknownName = "Dispositivo não identificado";

    private static readonly Lazy<Catalog> Pci = new(() => Catalog.Load("pci", true));
    private static readonly Lazy<Catalog> Usb = new(() => Catalog.Load("usb", false));
    private static readonly ConditionalWeakTable<DriverInventoryEntry, DriverHardwareIdentity> Identities = new();
    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dispositivo desconhecido", UnknownName, "Outro dispositivo",
        "Dispositivo PCI", "Dispositivo de sistema básico", "Controlador de rede", "Controlador Ethernet",
        "Controlador de barramento SM", "Controlador de comunicações PCI simples",
        "Controlador de áudio multimídia", "Dispositivo de áudio de alta definição",
        "Controlador de vídeo", "Controlador de vídeo (compatível com VGA)",
        "Adaptador de Vídeo Básico da Microsoft", "Dispositivo USB", "Dispositivo USB composto",
        "Dispositivo de entrada USB", "Unknown device", "Other device", "PCI Device",
        "Base System Device", "Network Controller", "Ethernet Controller",
        "SM Bus Controller", "PCI Simple Communications Controller",
        "Multimedia Audio Controller", "High Definition Audio Device", "Video Controller",
        "Video Controller (VGA Compatible)", "Microsoft Basic Display Adapter",
        "USB Device", "USB Composite Device", "USB Input Device"
    };

    internal static DriverHardwareIdentity Identify(DriverInventoryEntry item) =>
        Identities.GetValue(item, Resolve);

    internal static string DisplayName(DriverInventoryEntry item)
    {
        string name = item.DeviceName.Trim();
        if (name.Length > 0 && !GenericNames.Contains(name)) return name;
        var identity = Identify(item);
        string model = identity.Subsystem.Length > 0 ? identity.Subsystem : identity.Model;
        if (model.Length > 0)
            return identity.UsbInterface
                ? (name.Length > 0 ? name : "Interface USB") + " · " + model
                : model;
        return name.Length > 0 ? name : UnknownName;
    }

    private static DriverHardwareIdentity Resolve(DriverInventoryEntry item)
    {
        // O segmento anterior ao identificador de instância também contém
        // VEN/DEV ou VID/PID. Serve quando a lista HardwareID está vazia.
        int prefix = item.DeviceId.IndexOf('\\');
        int separator = prefix < 0 ? -1 : item.DeviceId.IndexOf('\\', prefix + 1);
        string device = separator < 0 ? item.DeviceId : item.DeviceId[..separator];
        bool? isPci = null;
        uint product = 0;
        uint? subsystem = null;
        bool conflictingSubsystem = false, usbInterface = false;
        foreach (string id in item.HardwareIds.Prepend(device))
        {
            if (id.Length > 256) continue;
            var match = PciId().Match(id);
            bool pci = match.Success;
            if (!pci) match = UsbId().Match(id);
            if (!match.Success || !Hex(match.Groups[1].ValueSpan, 4, out uint vendor) ||
                vendor is 0 or 0xffff || !Hex(match.Groups[2].ValueSpan, 4, out uint model))
                continue;

            uint key = (vendor << 16) | model;
            if (isPci.HasValue && (isPci.Value != pci || product != key))
                return DriverHardwareIdentity.Unknown;
            isPci = pci;
            product = key;
            foreach (var qualifier in id.Split('&').Skip(2))
            {
                if (pci && qualifier.StartsWith("SUBSYS_", StringComparison.OrdinalIgnoreCase) &&
                    Hex(qualifier.AsSpan(7), 8, out uint value) && value != 0)
                {
                    conflictingSubsystem |= subsystem.HasValue && subsystem.Value != value;
                    subsystem = value;
                }
                if (!pci && qualifier.StartsWith("MI_", StringComparison.OrdinalIgnoreCase) &&
                    Hex(qualifier.AsSpan(3), 2, out _))
                    usbInterface = true;
            }
        }
        if (!isPci.HasValue) return DriverHardwareIdentity.Unknown;

        var catalog = isPci.Value ? Pci.Value : Usb.Value;
        return new(catalog.Vendors.GetValueOrDefault(product >> 16, ""),
            catalog.Products.GetValueOrDefault(product, ""),
            !conflictingSubsystem && subsystem is uint sub
                ? catalog.Subsystems.GetValueOrDefault((product, sub), "") : "", usbInterface);
    }

    [GeneratedRegex(@"\APCI\\VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})(?:&[0-9A-Z_]+)*\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PciId();

    [GeneratedRegex(@"\AUSB\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})(?:&[0-9A-Z_]+)*\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UsbId();

    private static bool Hex(ReadOnlySpan<char> value, int length, out uint result)
    {
        result = 0;
        return value.Length == length &&
            uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);
    }

    private sealed class Catalog
    {
        internal Dictionary<uint, string> Vendors { get; } = new();
        internal Dictionary<uint, string> Products { get; } = new();
        internal Dictionary<(uint Product, uint Subsystem), string> Subsystems { get; } = new();

        internal static Catalog Load(string bus, bool pci)
        {
            var catalog = new Catalog();
            using var resource = typeof(DriverHardwareCatalog).Assembly.GetManifestResourceStream($"GerenciadorFinal.DriverIds.{bus}");
            if (resource is null) return catalog;
            using var compressed = new GZipStream(resource, CompressionMode.Decompress);
            using var reader = new StreamReader(compressed);
            uint? vendor = null, product = null;
            while (reader.ReadLine() is { } line && !line.StartsWith("C ", StringComparison.Ordinal))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                int offset = 0;
                while (offset < line.Length && line[offset] == '\t') offset++;
                int delimiter = line.IndexOf("  ", offset, StringComparison.Ordinal);
                if (delimiter < 0) continue;
                var code = line.AsSpan(offset, delimiter - offset);
                string name = line[(delimiter + 2)..].Trim();
                if (name.Length == 0) continue;
                switch (offset)
                {
                    case 0 when Hex(code, 4, out uint vendorId):
                        vendor = vendorId;
                        product = null;
                        catalog.Vendors.Add(vendorId, name);
                        break;
                    case 1 when vendor is uint current && Hex(code, 4, out uint productId):
                        uint productKey = (current << 16) | productId;
                        product = productKey;
                        catalog.Products.Add(productKey, name);
                        break;
                    case 2 when pci && product is uint parent && code.Length == 9 && code[4] == ' ' &&
                        Hex(code[..4], 4, out uint subVendor) && Hex(code[5..], 4, out uint subDevice):
                        // Em SUBSYS do Windows, o dispositivo vem antes do fornecedor.
                        catalog.Subsystems.Add((parent, (subDevice << 16) | subVendor), name);
                        break;
                }
            }
            return catalog;
        }
    }
}
