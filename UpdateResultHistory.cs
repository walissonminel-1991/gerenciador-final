namespace GerenciadorAplicativos;

// Último snapshot publicado nesta sessão, inclusive uma primeira consulta
// parcial. Preservá-lo não promove consulta parcial a completa nem cria
// autoridade: evidência recuperada fica em revisão e bloqueia receita direta.
internal sealed class UpdateResultHistory
{
    private InstalledApp[]? inventory;
    private UpdateMatch[]? published;
    private long recorded;
    private readonly TimeProvider clock;
    internal UpdateResultHistory(TimeProvider? clock = null) => this.clock = clock ?? TimeProvider.System;
    internal void Clear() { inventory = null; published = null; }
    internal IReadOnlyList<UpdateMatch> Apply(IReadOnlyList<InstalledApp> current,
        IReadOnlyList<UpdateMatch> fresh, bool incomplete)
    {
        if (current.Count != fresh.Count || fresh.Where((m,i) => !ReferenceEquals(m.Installed,current[i])).Any())
            throw new ArgumentException("Resultado não pertence ao snapshot consultado.");
        if (inventory is null || !inventory.SequenceEqual(current)) Clear();
        if (!incomplete)
        {
            inventory = current.ToArray(); published = fresh.ToArray(); recorded = clock.GetTimestamp();
            return fresh;
        }
        if (published is null || clock.GetElapsedTime(recorded) > TimeSpan.FromMinutes(30))
        {
            Clear();
            if (fresh.Any(m => m.State == MatchState.Candidate && m.Catalog is not null))
            { inventory = current.ToArray(); published = fresh.ToArray(); recorded = clock.GetTimestamp(); }
            return fresh;
        }
        var retainedResults = fresh.Select((match,i) => {
            var previous = published[i];
            if (previous.State != MatchState.Candidate || previous.Catalog is null) return match;
            // Conserva também divergência nova. Não inventa observação recente.
            var retained = match.State == MatchState.Candidate && match.Catalog is not null ? match :
                previous with { Installed = current[i] };
            return retained with { RequiresReview = true, IsStale = true,
                Observations = match.Observations.Concat(previous.Observations).Distinct().Take(16).ToArray() };
        }).ToArray();
        // Atualizar o que a tela publicou, sem renovar o prazo de evidência
        // recuperada por falhas ou cancelamentos sucessivos.
        published = retainedResults;
        return retainedResults;
    }
}
