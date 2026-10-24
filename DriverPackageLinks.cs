namespace GerenciadorAplicativos;

internal sealed record DriverPackageLink(string InfName, int Devices, int Versions, bool ConflictingSignatures);
internal sealed record DriverPackageLinkResult(IReadOnlyList<DriverPackageLink> Packages, int WithoutInf, bool Partial);

// Apenas vinculos relatados pelo WMI; nao equivale ao Driver Store completo.
internal static class DriverPackageLinks
{
    internal static bool IsValidInf(string name) => name.Length is > 4 and <= 255 &&
        char.IsAsciiLetterOrDigit(name[0]) && !name.Contains("..", StringComparison.Ordinal) &&
        name.EndsWith(".inf", StringComparison.OrdinalIgnoreCase) &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
    internal static DriverPackageLinkResult Analyze(DriverInventoryResult inventory)
    {
        var eligible = inventory.Items.Where(d => IsValidInf(d.InfName)).ToArray();
        var packages = eligible.GroupBy(d => d.InfName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DriverPackageLink(g.Key,
                g.Select(d => d.DeviceId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                g.Select(d => d.Version).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                g.Any(d => d.Signed == true) && g.Any(d => d.Signed == false)))
            .OrderBy(x => x.InfName, StringComparer.OrdinalIgnoreCase).ToArray();
        return new(packages, inventory.Items.Count - eligible.Length, inventory.Partial);
    }
}
