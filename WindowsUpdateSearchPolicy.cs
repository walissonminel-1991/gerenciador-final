using System.Globalization;

namespace GerenciadorAplicativos;

internal sealed record WinUpdateChoice(string Id, string Title, string Kb, bool Downloaded, bool EulaAccepted);

internal static class WindowsUpdateSearchPolicy
{
    // WUA OperationResultCode: 2=Succeeded; 3=SucceededWithErrors (incompleto).
    internal static void RequireSuccessfulSearch(int resultCode)
    {
        if (resultCode != 2)
            throw new InvalidDataException("Consulta do Windows Update inconclusiva; resultado=" + resultCode);
    }

    internal static string? VerifiedIdentity(string? rawId, int revision) =>
        Guid.TryParse(rawId, out Guid guid) && guid != Guid.Empty && revision > 0
            ? guid.ToString("D") + "@" + revision.ToString(CultureInfo.InvariantCulture) : null;

    internal static string? CanonicalSelectedIdentity(string? value)
    {
        if (value is null || value.Length is < 38 or > 47 || value[36] != '@' ||
            !Guid.TryParseExact(value[..36], "D", out Guid guid) ||
            !int.TryParse(value.AsSpan(37), NumberStyles.None, CultureInfo.InvariantCulture, out int revision))
            return null;
        string? canonical = VerifiedIdentity(guid.ToString("D"), revision);
        return canonical is not null && value.AsSpan(37).SequenceEqual(canonical.AsSpan(37)) ? canonical : null;
    }

    internal static IReadOnlyList<WinUpdateChoice> ValidateOffers(IEnumerable<WinUpdateChoice> offers)
    {
        var found = new Dictionary<string, WinUpdateChoice>(StringComparer.OrdinalIgnoreCase);
        foreach (var offer in offers)
        {
            string id = CanonicalSelectedIdentity(offer.Id)
                ?? throw new InvalidDataException("Oferta do Windows Update sem identidade válida.");
            string product = id[..36];
            if (found.TryGetValue(product, out var previous) && previous != offer)
                throw new InvalidDataException("Ofertas contraditórias ou revisões diferentes da mesma atualização; consulte novamente.");
            found[product] = offer;
        }
        return found.Values.ToArray();
    }
}
