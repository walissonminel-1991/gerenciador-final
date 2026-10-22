namespace GerenciadorAplicativos;

internal sealed record InstalledApp(string Name, string Version, string Publisher,
    bool EligibleForUpdates = true, string Architecture = "unknown",
    string Channel = "unknown", string Scope = "unknown", string InstallType = "unknown",
    string InventoryId = "", string InstallLocation = "");
internal sealed record CatalogEntry(string ShortName, string ProductName, string ProductVersion, string Manufacturer);
internal enum MatchState { Candidate, EqualOrNewer, NotComparable, Unmatched, Ambiguous, DifferentMajor, SupersededRegistration }
internal sealed record UpdateObservation(string CanonicalId, string SourceId, string Provider,
    string Version, string Page, DateTimeOffset ObservedAt, string Architecture = "unknown",
    string Channel = "unknown", string Scope = "unknown", string InstallType = "unknown",
    string EvidenceKind = "published-version");
internal sealed record UpdateMatch(InstalledApp Installed, CatalogEntry? Catalog, MatchState State, string Provider = "RuckZuck")
{
    // Observações consultivas nunca autorizam download ou execução.
    public IReadOnlyList<UpdateObservation> Observations { get; init; } = [];
    public bool HasSourceConflict { get; init; }
    public bool RequiresReview { get; init; }
    public bool IsStale { get; init; }
    public string EvidenceLabel => IsStale ? "EVIDÊNCIA ANTERIOR; CONSULTA PARCIAL — REVALIDAR" : HasSourceConflict ? "DIVERGÊNCIA ENTRE FONTES" :
        Observations.Count > 0 ? "VERSÃO PUBLICADA EM FONTE; INSTALADOR NÃO VERIFICADO" : StateLabel;
    public string EvidenceDetails => (IsStale ? "Consulta atual incompleta: preservado último resultado completo desta sessão, por até 30 minutos; instalação direta bloqueada.\n" : "") + string.Join("\n", Observations.Select(o =>
        o.Provider + ": " + o.Version + " · " + (o.EvidenceKind switch
        {
            "windows-stable-feed" => "versão oferecida para Windows x64; assinatura não verificada",
            "community-index" => "versão no catálogo comunitário",
            "desktop-changelog" => "versão anunciada no changelog desktop",
            "incompatible-identity-or-context" => "identidade ou instalação incompatível",
            _ => "versão publicada na fonte"
        }) + (o.ObservedAt == default ? "" : " · consulta " + o.ObservedAt.ToString("u")))) +
        (Observations.Count > 0 ? "\nVersão publicada não confirma instalador compatível com esta instalação do Windows." : "");
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
