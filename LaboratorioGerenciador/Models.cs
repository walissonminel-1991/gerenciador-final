namespace GerenciadorFinal.Laboratorio;

internal sealed record InstalledApp(string Name, string Version, string Publisher);
internal sealed record CatalogEntry(string ShortName, string ProductName, string ProductVersion, string Manufacturer);
internal enum MatchState { Candidate, EqualOrNewer, NotComparable, Unmatched, Ambiguous }
internal sealed record UpdateMatch(InstalledApp Installed, CatalogEntry? Catalog, MatchState State)
{
    public string StateLabel => State switch
    {
        MatchState.Candidate => "POSSÍVEL ATUALIZAÇÃO",
        MatchState.EqualOrNewer => "SEM VERSÃO SUPERIOR CONFIRMADA",
        MatchState.NotComparable => "VERSÃO NÃO COMPARÁVEL",
        MatchState.Ambiguous => "CORRESPONDÊNCIA AMBÍGUA",
        _ => "SEM CORRESPONDÊNCIA NO CATÁLOGO"
    };
}
