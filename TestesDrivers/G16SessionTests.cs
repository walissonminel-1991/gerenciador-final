using System.Runtime.InteropServices;
using GerenciadorAplicativos;

// Exercita o MESMO CaptureSession do GF com objetos COM-like exclusivamente
// em memória, sem criar Microsoft.Update.Session nem acessar o sistema.
internal static class G16SessionTests
{
    internal sealed class Session
    {
        public string ClientApplicationID { get; set; } = "";
        public object? Searcher { get; set; }
        public Func<object?>? OnCreate { get; set; }
        public Action<string>? OnClientId { get; set; }
        public object? CreateUpdateSearcher() => OnCreate is null ? Searcher : OnCreate();
    }

    internal sealed class Searcher
    {
        public bool CanAutomaticallyUpgradeService { get; set; } = true;
        public int ServerSelection { get; set; } = -1;
        public bool Online { get; set; } = true;
        public string? LastCriteria { get; private set; }
        public object? Result { get; set; }
        public Func<string, object?>? OnSearch { get; set; }
        public object? Search(string criteria)
        {
            LastCriteria = criteria;
            return OnSearch is null ? Result : OnSearch(criteria);
        }
    }

    internal sealed class SearchResult
    {
        public object ResultCode { get; set; } = 2;
        public object? Updates { get; set; }
    }

    internal static (Session session, Searcher searcher, SearchResult result,
        G15CaptureTests.Collection updates, G15CaptureTests.Update offer,
        G15CaptureTests.Identity identity) Fixture()
    {
        var identity = new G15CaptureTests.Identity();
        var offer = new G15CaptureTests.Update
        {
            Identity = identity,
            SupersededCollection = new G15CaptureTests.Collection(),
            CategoriesCollection = new G15CaptureTests.Collection(new G15CaptureTests.Category()),
            EntriesCollection = new G15CaptureTests.Collection(new G15CaptureTests.Entry())
        };
        var updates = new G15CaptureTests.Collection(offer);
        var result = new SearchResult { Updates = updates };
        var searcher = new Searcher { Result = result };
        var session = new Session { Searcher = searcher };
        return (session, searcher, result, updates, offer, identity);
    }

    internal static void Run(Action<bool, string> check)
    {
        var device = new DriverInventoryEntry(
            @"PCI\VEN_1234&DEV_5678\INSTANCE", "Vídeo sintético",
            "Fornecedor A", "4.5.6.7", "oem42.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1234&DEV_5678&SUBSYS_00001234"],
            PackageEvidence = new("oem42.inf", DriverStoreState.Mapped)
        };
        var inventory = new DriverInventoryResult([device], false, "fixture G1.6");

        (DriverOfficialResult outcome, List<object> released) RunFixture(
            (Session session, Searcher searcher, SearchResult result,
             G15CaptureTests.Collection updates, G15CaptureTests.Update offer,
             G15CaptureTests.Identity identity) f, bool online = false)
        {
            var released = new List<object>();
            var outcome = DriverOfficialLookup.CaptureSession(
                inventory, f.session, CancellationToken.None, online, released.Add);
            return (outcome, released);
        }

        var valid = Fixture();
        var (good, release) = RunFixture(valid);
        check(!good.Partial && good.Matches.Count == 1 &&
            good.SearchContext is { Online: false, HostArchitecture.Length: > 0 } &&
            good.Matches.Single().Offer.UpdateId == (string)valid.identity.UpdateID &&
            !good.Matches.Single().CanInstall && !good.Matches.Single().IsNewerProven &&
            valid.session.ClientApplicationID == "GerenciadorFinal.DriverReadOnly" &&
            valid.searcher.CanAutomaticallyUpgradeService == false &&
            valid.searcher.ServerSelection == 2 && !valid.searcher.Online &&
            valid.searcher.LastCriteria == DriverOfficialLookup.Criteria,
            "G1.6: o mesmo CaptureSession real lê consulta positiva sintética sem ações de drivers");
        check(release.Count == 11 &&
            ReferenceEquals(release[release.Count - 4], valid.updates) &&
            ReferenceEquals(release[^3], valid.result) &&
            ReferenceEquals(release[^2], valid.searcher) &&
            ReferenceEquals(release[^1], valid.session) &&
            release.Distinct(ReferenceEqualityComparer.Instance).Count() == release.Count,
            "G1.6: coleções, resultado, pesquisador e sessão liberados uma vez no caminho íntegro");

        var zero = Fixture();
        zero.updates.Values.Clear();
        var (emptyResult, emptyReleased) = RunFixture(zero);
        check(!emptyResult.Partial && emptyResult.Offered == 0 &&
            emptyResult.Matches.Count == 0 && emptyReleased.Contains(zero.session),
            "G1.6: zero ofertas em consulta sintética completa não é falha COM");

        var soft = Fixture();
        soft.result.ResultCode = 3; // orcSucceededWithErrors
        var (softResult, softReleased) = RunFixture(soft);
        check(softResult.Partial && softResult.Matches.Count == 0 &&
            softReleased.Contains(soft.result) && softReleased.Contains(soft.session),
            "G1.6: Search com ResultCode 3 não publica consulta supostamente completa");

        var invalidCode = Fixture();
        invalidCode.result.ResultCode = "2";
        var (invalidResult, invalidReleased) = RunFixture(invalidCode);
        check(invalidResult.Partial && invalidResult.Matches.Count == 0 &&
            invalidReleased.Contains(invalidCode.result),
            "G1.6: ResultCode com tipo errado falha fechado");

        var exceptionSearch = Fixture();
        exceptionSearch.searcher.OnSearch = _ =>
            throw new COMException("falha sintética", unchecked((int)0x80004005));
        var (failedSearch, failedReleased) = RunFixture(exceptionSearch);
        check(failedSearch.Partial && failedSearch.Matches.Count == 0 &&
            failedReleased.SequenceEqual(new object[] { exceptionSearch.searcher, exceptionSearch.session }),
            "G1.6: exceção em Search libera pesquisador e sessão sem publicar ofertas");

        var nullSearch = Fixture();
        nullSearch.searcher.OnSearch = _ => null;
        var (nullSearchResult, nullSearchReleased) = RunFixture(nullSearch);
        check(nullSearchResult.Partial && nullSearchResult.Matches.Count == 0 &&
            nullSearchReleased.Contains(nullSearch.searcher) && nullSearchReleased.Contains(nullSearch.session),
            "G1.6: Search nulo não equivale a sucesso vazio");

        var nullSearcher = Fixture();
        nullSearcher.session.OnCreate = () => null;
        var (nullSearcherResult, nullSearcherReleased) = RunFixture(nullSearcher);
        check(nullSearcherResult.Partial && nullSearcherResult.Matches.Count == 0 &&
            nullSearcherReleased.SequenceEqual(new object[] { nullSearcher.session }),
            "G1.6: CreateUpdateSearcher nulo libera a sessão sem publicar dados");

        var createThrows = Fixture();
        createThrows.session.OnCreate = () =>
            throw new UnauthorizedAccessException("bloqueio sintético de política");
        var (createResult, createReleased) = RunFixture(createThrows);
        check(createResult.Partial && createResult.Matches.Count == 0 &&
            createReleased.SequenceEqual(new object[] { createThrows.session }),
            "G1.6: CreateUpdateSearcher negado não contorna política nem perde sessão");

        var nullUpdates = Fixture();
        nullUpdates.result.Updates = null;
        var (noCollection, nullReleased) = RunFixture(nullUpdates);
        check(noCollection.Partial && noCollection.Matches.Count == 0 &&
            !nullReleased.Contains(nullUpdates.updates) &&
            nullReleased.Contains(nullUpdates.result),
            "G1.6: Updates nulo não é interpretado como zero ofertas");

        foreach (var countProvider in new Func<object?>[]
        {
            () => -1, () => 513, () => "1",
            () => throw new COMException("Count falhou", unchecked((int)0x80004005))
        })
        {
            var fixture = Fixture();
            fixture.updates.CountOverride = countProvider;
            var (partial, released) = RunFixture(fixture);
            check(partial.Partial && partial.Matches.Count == 0 &&
                released.Contains(fixture.updates) && released.Contains(fixture.result) &&
                released.Contains(fixture.searcher) && released.Contains(fixture.session) &&
                !released.Contains(fixture.offer),
                "G1.6: Count negativo, excessivo, mal tipado ou COM falho não enumera nem publica");
        }

        var itemDenied = Fixture();
        itemDenied.updates.ItemOverride = _ =>
            throw new UnauthorizedAccessException("Item bloqueado");
        var (itemDeniedResult, itemDeniedReleases) = RunFixture(itemDenied);
        check(itemDeniedResult.Partial && itemDeniedResult.Matches.Count == 0 &&
            itemDeniedReleases.Contains(itemDenied.updates) && !itemDeniedReleases.Contains(itemDenied.offer),
            "G1.6: Updates.Item negado não publica pacote nem finge sucesso");

        var itemNull = Fixture();
        itemNull.updates.ItemOverride = _ => null;
        var (nullItemResult, nullItemReleases) = RunFixture(itemNull);
        check(nullItemResult.Partial && nullItemResult.Matches.Count == 0 &&
            nullItemReleases.Contains(itemNull.updates),
            "G1.6: Updates.Item nulo em lote completo é inconclusivo");

        var two = Fixture();
        two.updates.Values.Add(new G15CaptureTests.Update
        {
            Identity = new G15CaptureTests.Identity { UpdateID = Guid.Empty.ToString("D") }
        });
        var (twoResult, twoReleased) = RunFixture(two);
        check(twoResult.Partial && twoResult.Matches.Count == 0 &&
            twoReleased.Contains(two.offer) && twoReleased.Contains(two.updates) &&
            twoReleased.Contains(two.session),
            "G1.6: oferta válida seguida de identidade inválida rejeita publicação do lote todo");

        var secondItemFails = Fixture();
        secondItemFails.updates.Values.Add(new G15CaptureTests.Update());
        secondItemFails.updates.ItemOverride = i =>
            i == 0 ? secondItemFails.offer :
                throw new COMException("segundo Item falhou", unchecked((int)0x80004005));
        var (secondFailResult, secondReleased) = RunFixture(secondItemFails);
        check(secondFailResult.Partial && secondFailResult.Matches.Count == 0 &&
            secondReleased.Contains(secondItemFails.offer) &&
            secondReleased.Contains(secondItemFails.updates),
            "G1.6: falha de Item após oferta íntegra descarta lote inteiro");

        using var before = new CancellationTokenSource();
        before.Cancel();
        var beforeFixture = Fixture();
        var beforeReleased = new List<object>();
        bool beforeCanceled = false;
        try
        {
            DriverOfficialLookup.CaptureSession(inventory, beforeFixture.session,
                before.Token, true, beforeReleased.Add);
        }
        catch (OperationCanceledException) { beforeCanceled = true; }
        check(beforeCanceled && beforeReleased.SequenceEqual(new object[] { beforeFixture.session }),
            "G1.6: token cancelado antes da sessão libera a referência e não cria pesquisador");

        using var afterSearch = new CancellationTokenSource();
        var afterFixture = Fixture();
        afterFixture.searcher.OnSearch = _ =>
        {
            afterSearch.Cancel();
            return afterFixture.result;
        };
        var afterReleased = new List<object>();
        bool afterCanceled = false;
        try
        {
            DriverOfficialLookup.CaptureSession(inventory, afterFixture.session,
                afterSearch.Token, true, afterReleased.Add);
        }
        catch (OperationCanceledException) { afterCanceled = true; }
        check(afterCanceled &&
            afterReleased.Contains(afterFixture.result) &&
            afterReleased.Contains(afterFixture.searcher) &&
            afterReleased.Contains(afterFixture.session) &&
            !afterReleased.Contains(afterFixture.updates),
            "G1.6: cancelamento imediatamente após Search não lê Updates e libera RCWs lógicos");

        using var duringItem = new CancellationTokenSource();
        var duringFixture = Fixture();
        duringFixture.updates.ItemOverride = _ =>
        {
            duringItem.Cancel();
            return duringFixture.offer;
        };
        var duringReleased = new List<object>();
        bool duringCanceled = false;
        try
        {
            DriverOfficialLookup.CaptureSession(inventory, duringFixture.session,
                duringItem.Token, true, duringReleased.Add);
        }
        catch (OperationCanceledException) { duringCanceled = true; }
        check(duringCanceled && duringReleased.Contains(duringFixture.offer) &&
            duringReleased.Contains(duringFixture.updates) &&
            duringReleased.Contains(duringFixture.session),
            "G1.6: cancelamento ao receber oferta libera atualização e coleções sem resultado parcial");
    }
}
