using System.Globalization;

namespace GerenciadorAplicativos;

internal static class DriverListFilters
{
    private const CompareOptions SearchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    // Os filtros só mudam o que aparece na lista.
    internal static DriverInventoryEntry[] Apply(
        IEnumerable<DriverInventoryEntry> items, string filter, string query,
        IReadOnlySet<string>? prioritized = null)
    {
        IEnumerable<DriverInventoryEntry> filtered = filter switch
        {
            "Presentes" => items.Where(item => item.Present == true),
            "Não confirmados" => items.Where(item => item.Present != true),
            "Problemas" => items.Where(item => item.HasPnpProblem),
            "Sem driver" => items.Where(item => item.WithoutSignedDriverAssociation),
            _ => items
        };
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        DriverInventoryEntry[] view;
        if (terms.Length == 0) view = filtered.ToArray();
        else
        {
            string phrase = string.Join(" ", terms);
            var comparison = CultureInfo.CurrentCulture.CompareInfo;
            view = filtered.Select(item => (Item: item, Rank: SearchRank(item, phrase, terms, comparison)))
                .Where(match => match.Rank > 0)
                .OrderByDescending(match => match.Rank)
                .Select(match => match.Item).ToArray();
        }
        // Uma oferta WUA validada apenas altera a ordenação visual.
        // Sem candidatos, conservar rigorosamente a ordem original (G1.12).
        // Com pesquisa, conservar o ranking relativo dentro de cada grupo.
        if (prioritized is null || !view.Any(item => prioritized.Contains(item.DeviceId)))
            return view;
        return view.OrderByDescending(item => prioritized.Contains(item.DeviceId)).ToArray();
    }

    private static int SearchRank(DriverInventoryEntry item, string phrase, string[] terms, CompareInfo comparison)
    {
        if (item.DeviceId.Equals(phrase, StringComparison.OrdinalIgnoreCase) ||
            item.HardwareIds.Contains(phrase, StringComparer.OrdinalIgnoreCase))
            return 4;

        var identity = DriverHardwareCatalog.Identify(item);
        var names = new[] { item.DeviceName, item.DeviceMetadata?.FriendlyName ?? "",
            item.DeviceMetadata?.BusDescription ?? "", item.DeviceMetadata?.DeviceDescription ?? "",
            identity.Model, identity.Subsystem, DriverHardwareCatalog.DisplayName(item),
            DriverHardwareSupport.Label(DriverHardwareSupport.CategoryOf(item)) };
        if (names.Any(name => comparison.Compare(name, phrase, SearchOptions) == 0))
            return 3;
        if (names.Any(name => comparison.IndexOf(name, phrase, SearchOptions) >= 0))
            return 2;

        string searchable = string.Join(" ", string.Join(" ", names), item.DeviceId, item.Provider,
            item.Version, item.InfName, item.ReportedDriverClass, item.PresenceLabel, item.SignatureLabel,
            item.PnpStatusLabel, identity.Vendor, item.DeviceMetadata?.UsbParentHardwareId,
            string.Join(" ", item.HardwareIds));
        return terms.All(term => ContainsTerm(comparison, searchable, term,
            wholeCharacter: terms.Length > 1 && term.Length == 1 && char.IsLetterOrDigit(term[0]))) ? 1 : 0;
    }

    private static bool ContainsTerm(CompareInfo comparison, string searchable, string term, bool wholeCharacter)
    {
        int match = comparison.IndexOf(searchable, term, SearchOptions);
        if (!wholeCharacter) return match >= 0;

        // Em "Dispositivo A" ou "RTX 5060 Ti 8", um único caractere precisa
        // ser um termo próprio, sem corresponder ao meio de outra palavra.
        while (match >= 0 && match < searchable.Length)
        {
            int after = match + 1;
            if ((match == 0 || !char.IsLetterOrDigit(searchable[match - 1])) &&
                (after == searchable.Length || !char.IsLetterOrDigit(searchable[after])))
                return true;
            match = comparison.IndexOf(searchable, term, after, SearchOptions);
        }
        return false;
    }
}
