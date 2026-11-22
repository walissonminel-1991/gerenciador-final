using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

internal sealed record DriverHardwareIdentity(string Vendor, string Model, string Subsystem, bool UsbInterface)
{
    internal static readonly DriverHardwareIdentity Unknown = new("", "", "", false);
    internal bool FromUsbParent { get; init; }
}

// Metadados consultivos para a tela. Os IDs, nomes WMI e vínculos de INF
// do inventário continuam sendo a evidência usada pelo Windows e pelo WUA.
internal static partial class DriverHardwareCatalog
{
    internal const string UnknownName = "Dispositivo não identificado";

    private static readonly Lazy<Catalog> Pci = new(() => Catalog.Load("pci", true));
    private static readonly Lazy<Catalog> Usb = new(() => Catalog.Load("usb", false));
    private static readonly Lazy<Catalog> HdAudio = new(() => Catalog.Load("hda", false, compressed: false));
    private enum HardwareBus { Pci, Usb, HdAudio }

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
        "USB Device", "USB Composite Device", "USB Input Device",
        "USB Printing Support", "Generic USB Printer", "Suporte de impressão USB", "Impressora USB genérica",
        "High Definition Audio", "HD Audio", "Dispositivo de áudio HD",
        "USB Audio Device", "Dispositivo de áudio USB",
        "HID-compliant device", "Dispositivo compatível com HID"
    };

    internal static bool IsGenericName(string name) =>
        string.IsNullOrWhiteSpace(name) || GenericNames.Contains(name.Trim());

    internal static DriverHardwareIdentity Identify(DriverInventoryEntry item) =>
        Identities.GetValue(item, Resolve);

    internal static uint? ReportedPciProduct(DriverInventoryEntry item)
    {
        static uint? Product(string id)
        {
            if (id.Length > 256) return null;
            var match = PciId().Match(id);
            return match.Success && Hex(match.Groups[1].ValueSpan, 4, out uint vendor) &&
                vendor is not (0 or 0xffff) && Hex(match.Groups[2].ValueSpan, 4, out uint device)
                    ? (vendor << 16) | device : null;
        }
        uint? instance = Product(DriverDeviceMetadata.HardwarePart(item.DeviceId));
        var reported = item.HardwareIds
            .Where(id => id.StartsWith(@"PCI\VEN_", StringComparison.OrdinalIgnoreCase)).ToArray();
        return instance.HasValue && reported.Length > 0 && reported.All(id => Product(id) == instance)
            ? instance : null;
    }

    internal static string DisplayName(DriverInventoryEntry item)
    {
        string name = item.DeviceName.Trim();
        if (!IsGenericName(name)) return name;
        if (item.DeviceMetadata is { BusDescription: var description } && !IsGenericName(description))
            return description;
        var identity = Identify(item);
        string model = identity.Subsystem.Length > 0 ? identity.Subsystem : identity.Model;
        if (!IsGenericName(model))
            return identity.UsbInterface && !identity.FromUsbParent
                ? (name.Length > 0 ? name : "Interface USB") + " · " + model
                : model;
        return name.Length > 0 ? name : UnknownName;
    }

    private static DriverHardwareIdentity Resolve(DriverInventoryEntry item)
    {
        if (DriverDeviceMetadata.IsUsbPrinter(item.DeviceId))
        {
            if (item.DeviceMetadata is not { UsbParentHardwareId: { Length: > 0 } parent } ||
                !UsbId().IsMatch(parent))
                return DriverHardwareIdentity.Unknown;
            return ResolveIds([parent]) with { FromUsbParent = true };
        }
        // O segmento anterior ao identificador de instância também serve
        // quando o Windows não informou uma lista HardwareID.
        return ResolveIds(item.HardwareIds.Prepend(DriverDeviceMetadata.HardwarePart(item.DeviceId)));
    }

    private static DriverHardwareIdentity ResolveIds(IEnumerable<string> ids)
    {
        HardwareBus? bus = null;
        uint product = 0;
        uint? subsystem = null;
        bool conflictingSubsystem = false, usbInterface = false;
        foreach (string id in ids)
        {
            int separator = id.IndexOf('\\');
            if (separator <= 0 || id.Length > 256) continue;
            var (candidateBus, match) = id[..separator].ToUpperInvariant() switch
            {
                "PCI" => (HardwareBus.Pci, PciId().Match(id)),
                "USB" => (HardwareBus.Usb, UsbId().Match(id)),
                "HDAUDIO" => (HardwareBus.HdAudio, HdAudioId().Match(id)),
                _ => (default, Match.Empty)
            };
            if (!match.Success || !Hex(match.Groups[1].ValueSpan, 4, out uint vendor) ||
                vendor is 0 or 0xffff || !Hex(match.Groups[2].ValueSpan, 4, out uint model))
                continue;

            uint key = (vendor << 16) | model;
            if (bus.HasValue && (bus.Value != candidateBus || product != key))
                return DriverHardwareIdentity.Unknown;
            bus = candidateBus;
            product = key;
            foreach (var qualifier in id.Split('&').Skip(2))
            {
                if (bus == HardwareBus.Pci && qualifier.StartsWith("SUBSYS_", StringComparison.OrdinalIgnoreCase) &&
                    Hex(qualifier.AsSpan(7), 8, out uint value) && value != 0)
                {
                    conflictingSubsystem |= subsystem.HasValue && subsystem.Value != value;
                    subsystem = value;
                }
                if (bus == HardwareBus.Usb && qualifier.StartsWith("MI_", StringComparison.OrdinalIgnoreCase) &&
                    Hex(qualifier.AsSpan(3), 2, out _))
                    usbInterface = true;
            }
        }
        var catalog = bus switch
        {
            HardwareBus.Pci => Pci.Value,
            HardwareBus.Usb => Usb.Value,
            HardwareBus.HdAudio => HdAudio.Value,
            _ => null
        };
        if (catalog is null) return DriverHardwareIdentity.Unknown;
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

    [GeneratedRegex(@"\AHDAUDIO\\FUNC_01&VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})(?:&[0-9A-Z_]+)*\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HdAudioId();

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

        internal static Catalog Load(string bus, bool pci, bool compressed = true)
        {
            var catalog = new Catalog();
            using var resource = typeof(DriverHardwareCatalog).Assembly.GetManifestResourceStream($"GerenciadorFinal.DriverIds.{bus}");
            if (resource is null) return catalog;
            using var stream = compressed ? new GZipStream(resource, CompressionMode.Decompress) : resource;
            using var reader = new StreamReader(stream);
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
