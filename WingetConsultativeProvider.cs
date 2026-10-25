namespace GerenciadorAplicativos;

internal enum WingetQueryState { Disabled, Unavailable, Ready, Completed, Partial, Cancelled, TimedOut, Failed, Stopped }
internal enum WingetStopReason { UserDisabled, AntivirusAlert, UnexpectedWindow, DependencyRequired, UnexpectedWrite, ExcessCost, Compatibility, UnexpectedElevation, UnexpectedExit }
internal enum WingetQueryOrigin { Startup, RefreshInventory, NormalSearch, TabOpened, ExplicitLabTest }
internal sealed record WingetQueryResult(WingetQueryState State, IReadOnlyList<OfficialRelease> Findings);

// Contrato sem comandos, argumentos, caminhos de executáveis ou operações mutáveis.
// Adaptador legado de simulações. O backend COM tipado de produção é separado
// em WinGetComDiscoveryProvider, sem compartilhar permissões de execução.
internal interface IWingetReadOnlyBackend
{
    Task<WingetQueryResult> QueryAsync(IReadOnlyList<InstalledApp> snapshot, CancellationToken token);
}

internal sealed class WingetConsultativeProvider : IDisposable
{
    internal const string ProductionStatus = "WinGet COM somente leitura, sem reparo ou instalação; catálogo Microsoft HTTPS preservado";
    private readonly object gate = new();
    private readonly IWingetReadOnlyBackend? backend;
    private readonly TimeSpan timeout;
    private CancellationTokenSource? running;
    private bool enabled, stopped;
    private int epoch;

    // A produção só consegue criar o adaptador desligado e sem backend.
    internal WingetConsultativeProvider() { timeout = TimeSpan.FromSeconds(8); }
    private WingetConsultativeProvider(IWingetReadOnlyBackend fixture, TimeSpan deadline)
    { backend = fixture; timeout = deadline; }
    internal static WingetConsultativeProvider ForSimulation(IWingetReadOnlyBackend fixture, TimeSpan? deadline = null)
        => new(fixture, deadline ?? TimeSpan.FromSeconds(8));
    internal bool Enabled { get { lock (gate) return enabled && !stopped; } }
    internal bool TryOptIn(bool safetyReviewApproved)
    {
        lock (gate)
        {
            if (!safetyReviewApproved || stopped || backend is null) return false;
            enabled = true; return true;
        }
    }
    internal void Stop(WingetStopReason reason)
    {
        lock (gate)
        {
            enabled = false; stopped = true; epoch++;
            running?.Cancel(); // nunca reinstala, repara ou reinicia o GF
        }
    }
    internal async Task<WingetQueryResult> QueryAsync(IReadOnlyList<InstalledApp> snapshot,
        WingetQueryOrigin origin, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        CancellationTokenSource cancellation;
        int generation;
        lock (gate)
        {
            if (!enabled || stopped || origin != WingetQueryOrigin.ExplicitLabTest)
                return new(WingetQueryState.Disabled, []);
            if (backend is null || running is not null) return new(WingetQueryState.Unavailable, []);
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            running = cancellation; generation = epoch;
        }
        using (cancellation)
        {
            try
            {
                // Limite mesmo para fixture que ignora cancelamento. Não habilita COM real.
                Task<WingetQueryResult> work = backend!.QueryAsync(snapshot.ToArray(), cancellation.Token);
                _ = work.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                var result = await work.WaitAsync(timeout, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                lock (gate)
                {
                    if (!enabled || stopped || generation != epoch) return new(WingetQueryState.Stopped, []);
                    if (result.State is not (WingetQueryState.Completed or WingetQueryState.Partial) ||
                        result.Findings.Count > 2000 || result.Findings.Any(f =>
                            !snapshot.Any(a => ReferenceEquals(a, f.Installed))))
                    {
                        Stop(WingetStopReason.Compatibility);
                        return new(WingetQueryState.Unavailable, []);
                    }
                }
                return result;
            }
            catch (TimeoutException)
            { Stop(WingetStopReason.ExcessCost); return new(WingetQueryState.TimedOut, []); }
            catch (OperationCanceledException)
            { Stop(WingetStopReason.UserDisabled); return new(WingetQueryState.Cancelled, []); }
            catch (Exception)
            { Stop(WingetStopReason.Compatibility); return new(WingetQueryState.Failed, []); }
            finally
            {
                lock (gate) { if (ReferenceEquals(running, cancellation)) running = null; }
            }
        }
    }
    public void Dispose() => Stop(WingetStopReason.UserDisabled);
}

// Estado agregado: alias/PATH/pacote não demonstram capacidade COM ou necessidade de admin.
internal sealed record WingetContextFacts(bool PackageRegistered, bool AliasPresent, bool AliasDirectoryOnPath,
    bool Elevated, bool InventoryComplete);
internal static class WingetContextPolicy
{
    internal static string Classify(WingetContextFacts facts) => !facts.InventoryComplete ? "contexto_parcial" :
        !facts.PackageRegistered ? "pacote_nao_observado" : !facts.AliasPresent ? "alias_nao_observado" :
        !facts.AliasDirectoryOnPath ? "diretorio_fora_path" : "presenca_sem_execucao_confirmada";
}
