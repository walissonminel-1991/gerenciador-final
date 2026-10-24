namespace GerenciadorAplicativos;

// Fotografia apenas em memória, por processo/usuário corrente. Uma fonte parcial
// não confirma ausência nem substitui a última leitura completa. Entradas antigas
// são visíveis e protetoras, mas perdem qualquer permissão de receita.
internal sealed class LocalMsixSnapshot
{
    private readonly object gate = new();
    private LocalMsixApp[] lastComplete = [];

    internal LocalMsixResult Read(Func<LocalMsixResult> capture)
    {
        // Serializar captura + aplicação: uma leitura lenta não pode publicar um
        // snapshot anterior depois de uma consulta mais recente.
        lock (gate)
        {
            var result = capture();
            bool complete = result.Status == "ok" && result.Failures == 0;
            if (complete)
            {
                lastComplete = result.Items.Select(x => x with { Stale = false }).ToArray();
                return result with { Items = lastComplete.ToArray() };
            }
            var current = result.Items.Select(x => x with { RecipeEligible = false }).ToList();
            var observed = current.Select(x => x.PackageName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            current.AddRange(lastComplete.Where(x => !observed.Contains(x.PackageName))
                .Select(x => x with { Stale = true, RecipeEligible = false }));
            return result with { Items = current.ToArray() };
        }
    }
}
