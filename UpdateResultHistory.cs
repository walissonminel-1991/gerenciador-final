namespace GerenciadorAplicativos;

// Último snapshot completo desta sessão. Falha de rede não é prova de ausência
// de atualizações. Evidência antiga fica consultiva e bloqueia receita direta.
internal sealed class UpdateResultHistory
{
    private InstalledApp[]? inventory;
    private UpdateMatch[]? complete;
    private long recorded;
    private readonly TimeProvider clock;
    internal UpdateResultHistory(TimeProvider? clock = null) => this.clock = clock ?? TimeProvider.System;
    internal void Clear() { inventory = null; complete = null; }
    internal IReadOnlyList<UpdateMatch> Apply(IReadOnlyList<InstalledApp> current,
        IReadOnlyList<UpdateMatch> fresh, bool incomplete)
    {
        if (current.Count != fresh.Count || fresh.Where((m,i) => !ReferenceEquals(m.Installed,current[i])).Any())
            throw new ArgumentException("Resultado não pertence ao snapshot consultado.");
        if (inventory is null || !inventory.SequenceEqual(current)) Clear();
        if (!incomplete)
        {
            inventory = current.ToArray(); complete = fresh.ToArray(); recorded = clock.GetTimestamp();
            return fresh;
        }
        if (complete is null || clock.GetElapsedTime(recorded) > TimeSpan.FromMinutes(30))
        { Clear(); return fresh; }
        return fresh.Select((match,i) => {
            var previous = complete[i];
            if (previous.State != MatchState.Candidate || previous.Catalog is null) return match;
            // Conserva também divergência nova. Não inventa observação recente.
            var retained = match.State == MatchState.Candidate && match.Catalog is not null ? match :
                previous with { Installed = current[i] };
            return retained with { RequiresReview = true, IsStale = true,
                Observations = previous.Observations.Concat(match.Observations).Distinct().ToArray() };
        }).ToArray();
    }
}
