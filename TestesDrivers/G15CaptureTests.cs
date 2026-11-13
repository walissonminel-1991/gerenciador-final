using System.Runtime.InteropServices;
using GerenciadorAplicativos;

// Objetos COM-like de memória: a MESMA rotina de captura da aplicação é
// exercitada sem criar sessão WUA, baixar conteúdo ou tocar no driver do host.
internal static class G15CaptureTests
{
    internal sealed class Identity
    {
        public object UpdateID { get; set; } = "5819343e-5d44-44c7-a8e3-e2c3c8abe622";
        public object RevisionNumber { get; set; } = 7;
    }

    internal sealed class Collection
    {
        internal readonly List<object?> Values = [];
        public Func<object?>? CountOverride { get; set; }
        public Func<int, object?>? ItemOverride { get; set; }
        public object Count => CountOverride is null ? Values.Count : CountOverride()!;
        public object? Item(int index) =>
            ItemOverride is null ? Values[index] : ItemOverride(index);
        internal Collection(params object?[] values) { Values.AddRange(values); }
    }

    internal sealed class Category
    {
        public object CategoryID { get; set; } = "98b0feef-4e67-4cc7-b9a9-696a4674d3cb";
        public object Type { get; set; } = "Product";
        public object Name { get; set; } = "Dispositivo fictício";
    }

    internal sealed class Entry
    {
        public object DriverHardwareID { get; set; } = @"PCI\VEN_1234&DEV_5678&SUBSYS_00001234";
        public object DriverProvider { get; set; } = "Fornecedor A";
        public object DriverModel { get; set; } = "Vídeo";
        public object DriverClass { get; set; } = "Display";
        public object DriverManufacturer { get; set; } = "Fabricante A";
        public object DriverVerDate { get; set; } = new DateTime(2026, 11, 11);
        public object DeviceStatus { get; set; } = 1;
        public object DeviceProblemNumber { get; set; } = 0;
    }

    internal sealed class Update
    {
        public object Type { get; set; } = 2;
        public object IsInstalled { get; set; } = false;
        public object IsHidden { get; set; } = false;
        public object? Identity { get; set; }
        public object Title { get; set; } = "Atualização sintética";
        public object DriverHardwareID { get; set; } = @"PCI\VEN_1234&DEV_5678&SUBSYS_00001234";
        public object DriverProvider { get; set; } = "Fornecedor A";
        public object DriverModel { get; set; } = "Vídeo";
        public object DriverVerDate { get; set; } = new DateTime(2026, 11, 11);
        public object DriverClass { get; set; } = "Display";
        public object DriverManufacturer { get; set; } = "Fabricante A";
        internal Func<object?>? SupersededGetter;
        internal Func<object?>? CategoriesGetter;
        internal Func<object?>? EntriesGetter;
        internal object? SupersededCollection;
        internal object? CategoriesCollection;
        internal object? EntriesCollection;
        public object? SupersededUpdateIDs =>
            SupersededGetter is null ? SupersededCollection : SupersededGetter();
        public object? Categories =>
            CategoriesGetter is null ? CategoriesCollection : CategoriesGetter();
        public object? WindowsDriverUpdateEntries =>
            EntriesGetter is null ? EntriesCollection : EntriesGetter();
    }

    private static (Update update, Identity identity, Collection superseded,
        Collection categories, Category category, Collection entries, Entry entry) Fixture()
    {
        var identity = new Identity();
        var category = new Category();
        var entry = new Entry();
        var superseded = new Collection("9ff60e7c-bf8b-46af-854b-89e72549d385");
        var categories = new Collection(category);
        var entries = new Collection(entry);
        var update = new Update
        {
            Identity = identity,
            SupersededCollection = superseded,
            CategoriesCollection = categories,
            EntriesCollection = entries
        };
        return (update, identity, superseded, categories, category, entries, entry);
    }

    internal static void Run(Action<bool, string> check)
    {
        var good = Fixture();
        var released = new List<object>();
        var offer = DriverOfficialLookup.CaptureOffer(
            good.update, CancellationToken.None, released.Add);
        check(offer is
            {
                Revision: 7, Relations: { State: DriverOfficialRelationsState.Complete,
                    SupersededUpdateIds.Count: 1, Categories.Count: 1 },
                EntryAudit: { State: DriverOfficialEntryState.Complete, Entries.Count: 1 }
            } && offer.UpdateId == (string)good.identity.UpdateID &&
            offer.HardwareId == (string)good.entry.DriverHardwareID &&
            !offer.Relations.DriverRankProven,
            "G1.5: leitura COM-like positiva compartilha o método de produção e preserva identidade WUA");
        check(released.SequenceEqual(new object[]
            {
                good.category, good.categories, good.superseded, good.entry,
                good.entries, good.identity, good.update
            }),
            "G1.5: objetos filhos, coleções, identidade e oferta são liberados em ordem nos finally");

        var device = new DriverInventoryEntry(
            @"PCI\VEN_1234&DEV_5678\INSTANCE", "Vídeo sintético",
            "Fornecedor A", "4.5.6.7", "oem42.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1234&DEV_5678&SUBSYS_00001234"],
            PackageEvidence = new("oem42.inf", DriverStoreState.Mapped)
        };
        DriverOfficialResult Correlate(DriverOfficialOffer? candidate) =>
            candidate is null ? DriverOfficialLookup.Match(
                new([device], false, "fixture G1.5"), [], false) :
                DriverOfficialLookup.Match(new([device], false, "fixture G1.5"), [candidate], true);
        var correlated = Correlate(offer);
        check(!correlated.Partial && correlated.Matches.Count == 1 &&
            correlated.Matches.Single().MatchingWuaEntry is not null &&
            !correlated.Matches.Single().CanInstall &&
            !correlated.Matches.Single().IsNewerProven &&
            !correlated.Matches.Single().CompletePackageVerified,
            "G1.5: captura positiva sintética não promove conteúdo nem habilita instalação");

        foreach (var altered in new Action<Update>[]
        {
            u => u.Type = "Driver",
            u => u.Type = 1,
            u => u.IsInstalled = true,
            u => u.IsHidden = true,
            u => u.Identity = null,
            u => u.Identity = new Identity { UpdateID = Guid.Empty.ToString("D") },
            u => u.Identity = new Identity { RevisionNumber = 0 },
            u => u.Identity = new Identity { RevisionNumber = "sete" },
            u => u.DriverVerDate = "novembro"
        })
        {
            var f = Fixture();
            altered(f.update);
            var objects = new List<object>();
            var invalid = DriverOfficialLookup.CaptureOffer(
                f.update, CancellationToken.None, objects.Add);
            check(invalid is null && objects.Contains(f.update),
                "G1.5: atualização oculta/instalada ou identidade pai malformada é descartada e liberada");
        }

        var missing = Fixture();
        missing.update.SupersededGetter = () =>
            throw new COMException("Getter ausente", unchecked((int)0x80004002));
        var missingReleased = new List<object>();
        var missingOffer = DriverOfficialLookup.CaptureOffer(
            missing.update, CancellationToken.None, missingReleased.Add);
        check(missingOffer?.Relations?.State == DriverOfficialRelationsState.Unavailable &&
            missingOffer.EntryAudit?.State == DriverOfficialEntryState.Complete &&
            missingReleased.Contains(missing.update) && missingReleased.Contains(missing.identity) &&
            !Correlate(missingOffer).Matches.Single().CanInstall,
            "G1.5: getter WUA opcional ausente é Unavailable, não falso vazio");

        var countWrong = Fixture();
        countWrong.superseded.CountOverride = () => "2";
        var wrongReleased = new List<object>();
        var partial = DriverOfficialLookup.CaptureOffer(
            countWrong.update, CancellationToken.None, wrongReleased.Add);
        check(partial?.Relations?.State == DriverOfficialRelationsState.Inconclusive &&
            Correlate(partial) is { Partial: true, Matches.Count: 0 } &&
            wrongReleased.Contains(countWrong.superseded) &&
            wrongReleased.Contains(countWrong.update),
            "G1.5: Count de tipo errado falha fechado e libera coleção");

        var negative = Fixture();
        negative.entries.CountOverride = () => -1;
        var negativeOffer = DriverOfficialLookup.CaptureOffer(negative.update, CancellationToken.None);
        check(negativeOffer?.EntryAudit?.State == DriverOfficialEntryState.Inconclusive &&
            Correlate(negativeOffer) is { Partial: true, Matches.Count: 0 },
            "G1.5: Count negativo da coleção WUA4 não publica vínculo");

        var excessive = Fixture();
        excessive.entries.CountOverride = () => DriverOfficialLookup.MaxWuaEntriesPerOffer + 1;
        var excessiveOffer = DriverOfficialLookup.CaptureOffer(excessive.update, CancellationToken.None);
        check(excessiveOffer?.EntryAudit?.State == DriverOfficialEntryState.Inconclusive &&
            Correlate(excessiveOffer) is { Partial: true, Matches.Count: 0 },
            "G1.5: Count WUA4 acima do limite é inconclusivo sem percorrer itens");

        var itemThrows = Fixture();
        itemThrows.entries.ItemOverride = _ =>
            throw new UnauthorizedAccessException("synthetic Item denied");
        var itemReleases = new List<object>();
        var itemOffer = DriverOfficialLookup.CaptureOffer(
            itemThrows.update, CancellationToken.None, itemReleases.Add);
        check(itemOffer?.EntryAudit?.State == DriverOfficialEntryState.Inconclusive &&
            itemReleases.Contains(itemThrows.entries) && itemReleases.Contains(itemThrows.identity) &&
            itemReleases.Contains(itemThrows.update) &&
            Correlate(itemOffer) is { Partial: true, Matches.Count: 0 },
            "G1.5: Item com acesso negado não publica subconjunto e libera RCW lógico");

        var nullItem = Fixture();
        nullItem.entries.ItemOverride = _ => null;
        var nullOffer = DriverOfficialLookup.CaptureOffer(nullItem.update, CancellationToken.None);
        check(nullOffer?.EntryAudit?.State == DriverOfficialEntryState.Inconclusive &&
            Correlate(nullOffer) is { Partial: true, Matches.Count: 0 },
            "G1.5: Item nulo não cria entrada artificial");

        var corruptEntry = Fixture();
        corruptEntry.entry.DeviceProblemNumber = "invalid-integer";
        var corruptOffer = DriverOfficialLookup.CaptureOffer(corruptEntry.update, CancellationToken.None);
        check(corruptOffer?.EntryAudit?.State == DriverOfficialEntryState.Inconclusive &&
            Correlate(corruptOffer) is { Partial: true, Matches.Count: 0 },
            "G1.5: propriedade de entrada com tipo errado não autoriza vínculo");

        var wrongCategory = Fixture();
        wrongCategory.category.CategoryID = "GUID-falso";
        var categoryOffer = DriverOfficialLookup.CaptureOffer(wrongCategory.update, CancellationToken.None);
        check(categoryOffer?.Relations?.State == DriverOfficialRelationsState.Inconclusive &&
            Correlate(categoryOffer) is { Partial: true, Matches.Count: 0 },
            "G1.5: categoria inválida em getter realístico bloqueia o lote");

        var categoryThrows = Fixture();
        categoryThrows.categories.ItemOverride = _ =>
            throw new COMException("Item fail", unchecked((int)0x80004005));
        var categoryReleases = new List<object>();
        var categoryPartial = DriverOfficialLookup.CaptureOffer(
            categoryThrows.update, CancellationToken.None, categoryReleases.Add);
        check(categoryPartial?.Relations?.State == DriverOfficialRelationsState.Inconclusive &&
            categoryReleases.Contains(categoryThrows.categories) &&
            categoryReleases.Contains(categoryThrows.superseded) &&
            Correlate(categoryPartial) is { Partial: true, Matches.Count: 0 },
            "G1.5: falha COM em Category.Item libera ambas coleções e bloqueia publicação");

        using var canceled = new CancellationTokenSource();
        var cancelFixture = Fixture();
        cancelFixture.update.SupersededGetter = () =>
        {
            canceled.Cancel();
            return cancelFixture.superseded;
        };
        var cancelReleases = new List<object>();
        bool canceledObserved = false;
        try
        {
            DriverOfficialLookup.CaptureOffer(
                cancelFixture.update, canceled.Token, cancelReleases.Add);
        }
        catch (OperationCanceledException) { canceledObserved = true; }
        check(canceledObserved && cancelReleases.Contains(cancelFixture.superseded) &&
            cancelReleases.Contains(cancelFixture.identity) &&
            cancelReleases.Contains(cancelFixture.update),
            "G1.5: cancelamento após getter libera objetos e não retorna oferta parcial");

        var invalidAndGood = DriverOfficialLookup.Match(
            new([device], false, "fixture G1.5"),
            [offer!, itemOffer!], true);
        check(invalidAndGood.Partial && invalidAndGood.Matches.Count == 0,
            "G1.5: falha de uma das ofertas não publica subconjunto previamente válido");
    }

}
