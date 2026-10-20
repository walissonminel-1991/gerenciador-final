namespace GerenciadorAplicativos;

internal sealed record InstalledApp(string Name, string Version, string Publisher,
    bool EligibleForUpdates = true);
internal sealed record CatalogEntry(string ShortName, string ProductName, string ProductVersion, string Manufacturer);
internal enum MatchState { Candidate, EqualOrNewer, NotComparable, Unmatched, Ambiguous, DifferentMajor, SupersededRegistration }
internal sealed record UpdateMatch(InstalledApp Installed, CatalogEntry? Catalog, MatchState State, string Provider = "RuckZuck")
{
    public string DiagnosticReason => !Installed.EligibleForUpdates
        ? "SOMENTE INVENTÁRIO; SEM IDENTIDADE AUTORIZADA PARA ATUALIZAÇÃO"
        : StateLabel;

    public string StateLabel => State switch
    {
        MatchState.Candidate => "POSSÍVEL ATUALIZAÇÃO",
        MatchState.SupersededRegistration => "REGISTRO ANTIGO; VERSÃO MAIS RECENTE TAMBÉM INSTALADA",
        MatchState.EqualOrNewer => "SEM VERSÃO SUPERIOR CONFIRMADA",
        MatchState.NotComparable => "VERSÃO NÃO COMPARÁVEL",
        MatchState.Ambiguous => "CORRESPONDÊNCIA AMBÍGUA",
        MatchState.DifferentMajor => "VERSÃO PRINCIPAL DIFERENTE (ANALISAR)",
        _ => "SEM CORRESPONDÊNCIA NO CATÁLOGO"
    };
}
