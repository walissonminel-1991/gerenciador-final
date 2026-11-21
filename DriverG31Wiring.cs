namespace GerenciadorAplicativos;

// G3.1: conecta o produto aos componentes WUA e journal que ja existem na PR80.
// Nenhuma permissao de instalar e concedida pela criacao destas dependencias.
// G3.2 deve implementar e revisar as garantias para PC fisico ANTES de liberar
// qualquer instalacao pela interface normal.
internal static class DriverG31Wiring
{
    internal static IDriverUpdatePilotBackend NewProductionBackend() =>
        new DriverWuaNativeBackend();

    // Criacao diferida: nenhuma escrita no diario quando o dialogo e aberto.
    internal static Func<IDriverUpdatePilotJournal> NewProductionJournalFactory() =>
        () => new DriverUpdatePilotFileJournal(DriverUpdatePilotFileJournal.SuggestedPath);

    internal static bool IsNativeBackend(IDriverUpdatePilotBackend? backend) =>
        backend is DriverWuaNativeBackend;

    // Somente fixtures isoladas podem executar o piloto completo na G3.1.
    // Esta barreira nao depende de checkbox, variavel de ambiente ou alegacao
    // de VM e impede que a UI normal instale drivers no PC fisico.
    internal static bool CanExecutePilot(IDriverUpdatePilotBackend? backend,
        Func<IDriverUpdatePilotJournal>? journalFactory) =>
        backend is not null && journalFactory is not null && !IsNativeBackend(backend);
}

// Uma consulta WUA COM sincrona pode continuar apos Cancel/WaitAsync
// e tambem apos a janela que iniciou a espera ser fechada. Rastrear a Task
// REAL por PROCESSO, nao por Form, para uma segunda janela nao iniciar
// outra consulta enquanto o primeiro COM continua ocupado.
internal sealed class DriverG31ReadGate
{
    private static readonly object ProcessSync = new();
    private static Task<DriverUpdatePilotCheck>? processTask;

    internal bool HasPendingWork
    {
        get { lock (ProcessSync) return processTask is { IsCompleted: false }; }
    }

    internal Task<DriverUpdatePilotCheck>? LatestTask
    {
        get { lock (ProcessSync) return processTask; }
    }

    // A conclusao de uma consulta anterior nunca autoriza publicar estado
    // quando outra janela iniciou uma Task COM mais recente.
    internal bool IsLatestCompleted(Task<DriverUpdatePilotCheck> observed)
    {
        lock (ProcessSync)
            return ReferenceEquals(processTask, observed) && observed.IsCompleted;
    }

    internal bool TryStart(Func<Task<DriverUpdatePilotCheck>> operation,
        out Task<DriverUpdatePilotCheck>? task)
    {
        task = null;
        // Atomicidade entre duas janelas. A operacao retorna uma Task de
        // espera; nenhuma chamada COM e feita dentro deste objeto de controle.
        lock (ProcessSync)
        {
            if (processTask is { IsCompleted: false }) return false;
            var started = operation() ?? throw new InvalidOperationException(
                "G3.1: backend WUA retornou tarefa nula.");
            processTask = started;
            task = started;
            return true;
        }
    }
}
