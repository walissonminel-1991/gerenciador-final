namespace GerenciadorAplicativos;

internal sealed record InstalledApp(string Name, string Version, string Publisher);
internal sealed record CatalogEntry(string ShortName, string ProductName, string ProductVersion, string Manufacturer);
internal enum MatchState { Candidate, EqualOrNewer, NotComparable, Unmatched, Ambiguous, DifferentMajor }
internal sealed record UpdateMatch(InstalledApp Installed, CatalogEntry? Catalog, MatchState State)
{
    public string StateLabel => State switch
    {
        MatchState.Candidate => "POSSÍVEL ATUALIZAÇÃO",
        MatchState.EqualOrNewer => "SEM VERSÃO SUPERIOR CONFIRMADA",
        MatchState.NotComparable => "VERSÃO NÃO COMPARÁVEL",
        MatchState.Ambiguous => "CORRESPONDÊNCIA AMBÍGUA",
        MatchState.DifferentMajor => "VERSÃO PRINCIPAL DIFERENTE (ANALISAR)",
        _ => "SEM CORRESPONDÊNCIA NO CATÁLOGO"
    };
}
