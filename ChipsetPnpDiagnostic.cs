using System.Globalization;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

internal sealed record ChipsetPnpController(string Role, string CatalogModel, string PnpStatus);

internal sealed record ChipsetPnpDiagnosis(IReadOnlyList<ChipsetPnpController> Controllers, bool Partial)
{
    internal string Summary => Partial ? "Chipset: consulta PnP parcial" : Controllers.Count == 0
        ? "Chipset: nenhum controlador do escopo identificado nos itens consultados"
        : $"Chipset: {Controllers.Count} controlador(es) identificado(s)";

    internal string Describe()
    {
        const int visibleLimit = 6;
        var lines = new List<string> { Summary, "A consulta cobre SMBus e ponte PCI–ISA." };
        foreach (var controller in Controllers.Take(visibleLimit))
        {
            string label = controller.CatalogModel.Length > 0
                ? $"{controller.Role} · {controller.CatalogModel}" : controller.Role;
            lines.Add($"{label} — {controller.PnpStatus}");
        }
        if (Controllers.Count > visibleLimit)
            lines.Add($"Mais {Controllers.Count - visibleLimit} controlador(es) no inventário.");
        if (Partial)
            lines.Add("A leitura PnP está incompleta ou há informações que o Windows não confirmou.");
        lines.Add("Confira o estado no Gerenciador de Dispositivos. Para consultar o pacote do chipset, use o modelo exato da placa ou do computador no suporte do fabricante.");
        return string.Join('\n', lines);
    }
}

internal static partial class ChipsetPnpDiagnostic
{
    internal static ChipsetPnpDiagnosis Evaluate(DriverInventoryResult inventory)
    {
        var controllers = new List<ChipsetPnpController>();
        // O inventário preserva os IDs PnP sem associação, mas sem a instância
        // correspondente não há como atribuir presença e código a esses IDs.
        bool partial = inventory.Partial || inventory.OtherPnpTargetIds.Any(id =>
            RoleForClassCode(ClassIdentifier().Match(id).Groups["class"].Value) is not null);
        var devices = inventory.Items.Where(item =>
            item.DeviceId.StartsWith(@"PCI\VEN_", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.DeviceId, StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
        {
            if (device.All(item => item.Present == false)) continue;
            var classes = device.SelectMany(item => item.HardwareIds.Concat(item.CompatibleIds))
                .Select(id => ClassIdentifier().Match(id)).Where(match => match.Success).ToArray();
            var classCodes = classes.Select(match => match.Groups["class"].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string? role = classCodes.Select(RoleForClassCode).FirstOrDefault(label => label is not null);
            if (role is null) continue;

            var item = device.First();
            if (classCodes.Length != 1 || device.Skip(1).Any() || item.Present != true || item.PnpProblemCode == 45 ||
                DriverHardwareCatalog.ReportedPciIdentity(item) is not { } pci ||
                classes.Any(match => !MatchesIdentity(match, pci)))
            {
                partial = true;
                continue;
            }

            partial |= item.PnpProblemCode is null or < 0;
            controllers.Add(new(role, DriverHardwareCatalog.Identify(item).Model, item.PnpStatusLabel));
        }
        return new(controllers, partial);
    }

    private static string? RoleForClassCode(string code) => code.ToUpperInvariant() switch
    {
        "0C05" => "SMBus",
        "0601" => "Ponte PCI–ISA",
        _ => null
    };

    private static bool MatchesIdentity(Match match, DriverPciIdentity identity)
    {
        var vendor = match.Groups["vendor"];
        var device = match.Groups["device"];
        return (!vendor.Success || vendor.Value.Equals(identity.Vendor.ToString("X4", CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase)) &&
               (!device.Success || device.Value.Equals((identity.Product & 0xffff).ToString("X4", CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase));
    }

    // HardwareID qualificado por VEN/DEV ou CompatibleID qualificado por VEN,
    // ou apenas classe, com DT opcional no formato genérico para PCI Express.
    [GeneratedRegex(@"\APCI\\(?:VEN_(?<vendor>[0-9A-F]{4})(?:&DEV_(?<device>[0-9A-F]{4}))?&CC_(?<class>[0-9A-F]{4})(?:[0-9A-F]{2})?|CC_(?<class>[0-9A-F]{4})(?:[0-9A-F]{2})?(?:&DT_[0-9A-F]{4})?)\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClassIdentifier();
}
