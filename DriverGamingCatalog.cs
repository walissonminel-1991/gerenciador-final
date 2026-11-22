using System.Runtime.InteropServices;
using System.Text;

namespace GerenciadorAplicativos;

internal sealed record DriverGamingReference(string Channel, string Version, Uri Page,
    Version? ComparableVersion = null);

internal sealed record DriverGamingInfo(string Model, Uri Support, Version? InstalledComparable,
    IReadOnlyList<DriverGamingReference> References);

// Peças de catálogo com revisão explícita. A página oficial é aberta a pedido.
// Estas referências não passam ao WUA, ao motor de instalação ou ao diagnóstico remoto.
internal static class DriverGamingCatalog
{
    internal const string ReviewedOn = "22/11/2026";
    private static readonly Lazy<bool> Windows11Desktop = new(DetectWindows11X64);
    internal static bool Windows11X64 => Windows11Desktop.Value;

    private static readonly Uri Amd9070 = new("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-9000-series/amd-radeon-rx-9070.html");
    private static readonly Uri Amd9070Xt = new("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-9000-series/amd-radeon-rx-9070-xt.html");
    private static readonly Uri IntelSupport = new("https://www.intel.com/content/www/us/en/download/785597/intel-arc-graphics-windows.html");
    private static readonly DriverGamingReference[] NvidiaReferences =
    [
        new("Game Ready", "621.24", new("https://www.nvidia.com/pt-br/drivers/details/285415/"), new(621, 24)),
        new("Studio", "621.14", new("https://www.nvidia.com/pt-br/drivers/details/284061/"), new(621, 14))
    ];
    private static readonly DriverGamingReference[] AmdReferences =
    [
        new("Adrenalin Edition", "26.11.2", new("https://www.amd.com/en/resources/support-articles/release-notes/RN-RAD-WIN-26-11-2.html"))
    ];
    private static readonly DriverGamingReference[] IntelReferences =
    [
        new("Arc Graphics WHQL com suporte Game On", "32.0.101.9175", new("https://downloadmirror.intel.com/932449/ReleaseNotes_101.9175_WHQL.pdf"),
            new(32, 0, 101, 9175))
    ];

    private static bool DetectWindows11X64()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ||
            RuntimeInformation.OSArchitecture != Architecture.X64)
            return false;
        var version = new OsVersionInfo { Size = (uint)Marshal.SizeOf<OsVersionInfo>(), ServicePack = "" };
        return RtlGetVersion(ref version) == 0 && version.Major == 10 && version.Minor == 0 &&
            version.Build >= 22000 && version.ProductType == 1; // VER_NT_WORKSTATION
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OsVersionInfo
    {
        internal uint Size, Major, Minor, Build, Platform;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string ServicePack;
        internal ushort ServicePackMajor, ServicePackMinor, SuiteMask;
        internal byte ProductType, Reserved;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int RtlGetVersion(ref OsVersionInfo version);

    internal static DriverGamingInfo? Find(DriverInventoryResult inventory,
        DriverInventoryEntry? selected, bool windows11X64)
    {
        if (inventory.Partial || selected is not { Present: true } ||
            DriverHardwareSupport.CategoryOf(selected) != DriverHardwareCategory.Graphics ||
            inventory.Items.Count(item => item.DeviceId.Equals(selected.DeviceId, StringComparison.OrdinalIgnoreCase)) != 1 ||
            !inventory.Items.Contains(selected))
            return null;

        Version? installed = null;
        if (selected.BindingStatus != DriverBindingStatus.Conflicting && !selected.WithoutSignedDriverAssociation)
            Version.TryParse(DriverOfficialLookup.NormalizeInstalledDriverVersion(selected.Version), out installed);
        bool NvidiaProvider() => selected.Provider.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
            selected.Provider.Equals("NVIDIA Corporation", StringComparison.OrdinalIgnoreCase);
        bool IntelProvider() => selected.Provider.Equals("Intel Corporation", StringComparison.OrdinalIgnoreCase);

        return DriverHardwareCatalog.ReportedPciProduct(selected) switch
        {
            // NVIDIA Supported GPU Products: 10DE:2D04.
            0x10de2d04 => new("NVIDIA GeForce RTX 5060 Ti", DriverHardwareSupport.NvidiaDrivers,
                NvidiaProvider() && installed is { Build: >= 0, Revision: >= 0 and <= 9999 }
                    ? new Version((installed.Build % 10) * 100 + installed.Revision / 100, installed.Revision % 100)
                    : null, windows11X64 ? NvidiaReferences : []),
            // Intel compute-runtime names these desktop Arc products explicitly.
            0x8086e209 or 0x8086e20b => new("Intel Arc B580", IntelSupport,
                IntelProvider() ? installed : null, windows11X64 ? IntelReferences : []),
            0x8086e20c => new("Intel Arc B570", IntelSupport,
                IntelProvider() ? installed : null, windows11X64 ? IntelReferences : []),
            // A AMD documenta compartilhamento deste DeviceID por variantes Navi 48.
            0x10027550 => AmdNavi48(selected, windows11X64),
            _ => null
        };
    }

    private static DriverGamingInfo AmdNavi48(DriverInventoryEntry selected, bool windows11X64)
    {
        if (selected.BindingStatus == DriverBindingStatus.Confirmed &&
            selected.Provider.Equals("Advanced Micro Devices, Inc.", StringComparison.OrdinalIgnoreCase))
        {
            string name = selected.DeviceName.Trim();
            if (name.Equals("AMD Radeon RX 9070 XT", StringComparison.OrdinalIgnoreCase))
                return new("AMD Radeon RX 9070 XT (nome PnP)", Amd9070Xt, null,
                    windows11X64 ? AmdReferences : []);
            if (name.Equals("AMD Radeon RX 9070", StringComparison.OrdinalIgnoreCase))
                return new("AMD Radeon RX 9070 (nome PnP)", Amd9070, null,
                    windows11X64 ? AmdReferences : []);
        }
        return new("AMD Navi 48 (modelo comercial a confirmar)", DriverHardwareSupport.AmdDrivers, null, []);
    }

    internal static void AppendReport(StringBuilder text, DriverGamingInfo info)
    {
        text.AppendLine("DRIVER GRÁFICO — FONTES OFICIAIS");
        text.AppendLine(info.Model);
        if (info.InstalledComparable is { } installed)
        {
            string version = installed.Build < 0 ? $"{installed.Major}.{installed.Minor:00}" : installed.ToString();
            text.AppendLine($"Versão instalada no formato comparável do fabricante: {version}.");
        }
        text.AppendLine("Canal do driver instalado indeterminado pelo inventário.");
        if (info.References.Count == 0)
        {
            text.AppendLine("Consulte o fabricante para confirmar a versão aplicável ao modelo e ao Windows.");
            return;
        }
        text.AppendLine($"Referências oficiais para Windows 11 x64 revisadas em {ReviewedOn}. Consulte o fabricante para ver a oferta atual.");
        foreach (var reference in info.References)
        {
            string comparison = info.InstalledComparable is { } local && reference.ComparableVersion is { } official
                ? local.CompareTo(official) switch
                {
                    < 0 => " (referência numericamente superior à versão instalada)",
                    > 0 => " (referência numericamente inferior à versão instalada)",
                    _ => " (mesma versão numérica)"
                }
                : "";
            text.AppendLine($"{reference.Channel} {reference.Version}{comparison}.");
        }
    }
}
