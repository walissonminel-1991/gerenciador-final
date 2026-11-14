using System.Runtime.InteropServices;
using GerenciadorAplicativos;

// G1.7: todas as referências são objetos sintéticos; nenhuma sessão WUA real
// é criada. A callback simula exceções em Marshal.FinalReleaseComObject.
internal static class G17ReleaseTests
{
    internal static void Run(Action<bool, string> check)
    {
        var device = new DriverInventoryEntry(
            @"PCI\VEN_1234&DEV_5678\INSTANCE", "Vídeo sintético",
            "Fornecedor A", "4.5.6.7", "oem42.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1234&DEV_5678&SUBSYS_00001234"],
            PackageEvidence = new("oem42.inf", DriverStoreState.Mapped)
        };
        var inventory = new DriverInventoryResult([device], false, "fixture G1.7");

        for (int failedRelease = 0; failedRelease < 11; failedRelease++)
        {
            var fixture = G16SessionTests.Fixture();
            var attempted = new List<object>();
            var position = failedRelease;
            var outcome = DriverOfficialLookup.CaptureSession(
                inventory, fixture.session, CancellationToken.None, false, value =>
                {
                    attempted.Add(value);
                    if (attempted.Count == position + 1)
                        throw new COMException("liberação artificial falhou", unchecked((int)0x80004005));
                });
            check(outcome.Partial && outcome.Matches.Count == 0 &&
                outcome.SearchContext is null && !outcome.Status.Contains("0 ofertas;", StringComparison.Ordinal) &&
                attempted.Count == 11 &&
                attempted.Distinct(ReferenceEqualityComparer.Instance).Count() == 11 &&
                ReferenceEquals(attempted[^4], fixture.updates) &&
                ReferenceEquals(attempted[^3], fixture.result) &&
                ReferenceEquals(attempted[^2], fixture.searcher) &&
                ReferenceEquals(attempted[^1], fixture.session),
                "G1.7: falha em liberação " + (failedRelease + 1) +
                " de 11 não omite demais finally nem publica oferta completa");
        }

        var nested = G16SessionTests.Fixture();
        nested.offer.EntriesCollection = nested.offer.CategoriesCollection; // alias real de referência
        var nestedReleases = new List<object>();
        var nestedResult = DriverOfficialLookup.CaptureSession(
            inventory, nested.session, CancellationToken.None, true, nestedReleases.Add);
        check(nestedResult.Partial && nestedResult.Matches.Count == 0 &&
            nestedReleases.Count(x => ReferenceEquals(x, nested.offer.CategoriesCollection)) == 1 &&
            nestedReleases.Count(x => ReferenceEquals(x, nested.session)) == 1,
            "G1.7: alias de coleção COM-like não recebe FinalRelease duas vezes");

        var duplicateIdentity = G16SessionTests.Fixture();
        duplicateIdentity.offer.Identity = duplicateIdentity.offer;
        var duplicateReleases = new List<object>();
        var duplicateResult = DriverOfficialLookup.CaptureSession(
            inventory, duplicateIdentity.session, CancellationToken.None, false,
            duplicateReleases.Add);
        check(duplicateResult.Partial && duplicateResult.Matches.Count == 0 &&
            duplicateReleases.Count(x => ReferenceEquals(x, duplicateIdentity.offer)) == 1 &&
            duplicateReleases.Contains(duplicateIdentity.session),
            "G1.7: identidade e atualização aliasadas bloqueiam publicação e dupla liberação");

        using var canceled = new CancellationTokenSource();
        var canceledFixture = G16SessionTests.Fixture();
        canceledFixture.searcher.OnSearch = _ =>
        {
            canceled.Cancel();
            return canceledFixture.result;
        };
        var canceledReleased = new List<object>();
        bool cancellationPropagated = false;
        try
        {
            DriverOfficialLookup.CaptureSession(inventory, canceledFixture.session,
                canceled.Token, false, obj =>
                {
                    canceledReleased.Add(obj);
                    if (ReferenceEquals(obj, canceledFixture.result))
                        throw new InvalidOperationException("falha de liberação simulada");
                });
        }
        catch (OperationCanceledException) { cancellationPropagated = true; }
        check(cancellationPropagated &&
            canceledReleased.Contains(canceledFixture.result) &&
            canceledReleased.Contains(canceledFixture.searcher) &&
            canceledReleased.Contains(canceledFixture.session),
            "G1.7: falha de cleanup não mascara cancelamento cooperativo e demais liberações");

        // WaitAsync cancela apenas a espera do consumidor: Search síncrono
        // continua na STA até retornar, sem abortar Thread nem recriar sessões.
        using var entered = new ManualResetEventSlim(false);
        using var allowFinish = new ManualResetEventSlim(false);
        var captureRuns = 0;
        var apartment = ApartmentState.Unknown;
        DriverOfficialResult CaptureBlocked(DriverInventoryResult inv, CancellationToken token, bool online)
        {
            Interlocked.Increment(ref captureRuns);
            apartment = Thread.CurrentThread.GetApartmentState();
            entered.Set();
            if (!allowFinish.Wait(TimeSpan.FromSeconds(10)))
                throw new InvalidOperationException("timeout do ensaio sintético");
            return new([], 0, 0, false, "pesquisa fictícia concluída");
        }

        Task<DriverOfficialResult>? first = null;
        bool callerTimedOut = false;
        bool blockedSecond = false;
        bool workerFinished = false;
        bool availableAgain = false;
        try
        {
            first = DriverOfficialLookup.StartWithCapture(
                inventory, CancellationToken.None, false, CaptureBlocked);
            check(entered.Wait(TimeSpan.FromSeconds(4)),
                "G1.7: captura sintética entrou em thread de consulta");
            using var caller = new CancellationTokenSource();
            var onlyAwait = first.WaitAsync(caller.Token);
            caller.Cancel();
            try { onlyAwait.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { callerTimedOut = true; }

            var second = DriverOfficialLookup.StartWithCapture(inventory,
                CancellationToken.None, false,
                (_, _, _) => throw new InvalidOperationException(
                    "segunda pesquisa não deve começar com STA ocupada"));
            var busy = second.GetAwaiter().GetResult();
            blockedSecond = !first.IsCompleted && busy.Partial &&
                busy.Matches.Count == 0 &&
                busy.Status.Contains("ainda em execução", StringComparison.Ordinal) &&
                captureRuns == 1;
        }
        finally
        {
            allowFinish.Set(); // nunca abandonar thread do ensaio artificial
        }
        if (first is not null)
        {
            var done = first.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            workerFinished = !done.Partial && captureRuns == 1;
        }
        var again = DriverOfficialLookup.StartWithCapture(
            inventory, CancellationToken.None, false,
            (_, _, _) => new([], 0, 0, false, "segunda pesquisa fictícia terminou"))
            .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        availableAgain = !again.Partial && captureRuns == 1;
        check(callerTimedOut && blockedSecond && workerFinished && availableAgain &&
            (!OperatingSystem.IsWindows() || apartment == ApartmentState.STA),
            "G1.7: WaitAsync cancela só chamador; único STA continua, barra duplicata e libera vaga após finalizar");

        var thrownCapture = DriverOfficialLookup.StartWithCapture(
            inventory, CancellationToken.None, false,
            (_, _, _) => throw new InvalidOperationException("falha de captura sintética"));
        bool faultObserved = false;
        try { thrownCapture.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); }
        catch (InvalidOperationException) { faultObserved = true; }
        var afterFault = DriverOfficialLookup.StartWithCapture(
            inventory, CancellationToken.None, false,
            (_, _, _) => new([], 0, 0, true, "resultado inconclusivo esperado"))
            .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        check(faultObserved && afterFault.Partial,
            "G1.7: falha do worker não mantém permanentemente gate STA armado");
    }
}
