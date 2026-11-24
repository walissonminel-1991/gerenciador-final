using GerenciadorAplicativos;

public static class WindowsUpdateP0Tests
{
    private static readonly Offer First = new(new("00000000-0000-0000-0000-000000000101", 7));
    private static readonly Offer Second = new(new("00000000-0000-0000-0000-000000000102", 12));
    private static readonly HashSet<string> Selection = [First.Id, Second.Id];
    private const int Failure = unchecked((int)0x80240022);

    internal static void Run()
    {
        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception(description);
            Console.WriteLine("PASS P0-WU " + description);
        }

        WindowsUpdateOperation Execute(Session session)
        {
            var exchange = new WindowsUpdateExchange(Selection.Count, phase => session.Events.Add(phase.ToString()));
            exchange.Receive(WindowsUpdateOperation.PhasePrefix + WindowsUpdatePhase.Revalidate);
            var result = WindowsUpdateClient.InstallSelected(Selection,
                phase => exchange.Receive(WindowsUpdateOperation.PhasePrefix + phase), () => session, () => new Offers());
            exchange.Receive(result.ToWire());
            exchange.Receive("DONE|" + result.ExitCode);
            return exchange.Result;
        }

        var history = new HistorySession(55, new HistoryEntry(new DateTime(2026, 11, 24, 10, 30, 0),
            "Atualização sintética", 1, 2, 0));
        string historyText = WindowsUpdateClient.ReadHistory(() => history);
        Check(history.Requested == 50 && history.Start == 0 &&
            historyText.Contains("Exibição parcial") && historyText.Contains("Atualização sintética") &&
            historyText.Contains("Instalação · Concluída"),
            "histórico consulta somente os últimos registros locais e identifica resultado real");
        var emptyHistory = new HistorySession(0);
        Check(WindowsUpdateClient.ReadHistory(() => emptyHistory).Contains("Nenhum evento") &&
            emptyHistory.Requested == -1, "histórico vazio não chama QueryHistory");

        var success = new Session(Second, First);
        var confirmed = Execute(success);
        Check(confirmed.Outcome == WindowsUpdateOutcome.Installation && confirmed.FullySucceeded &&
            success.Events.SequenceEqual(["Revalidate", "search", "Download", "download", "Install", "install"]) &&
            success.Downloaded.SequenceEqual([First.Id, Second.Id]) && success.Installed.SequenceEqual(success.Downloaded),
            "busca oficial, download e instalação usam somente o lote confirmado, ordenado por GUID e revisão");

        foreach (var (session, expected) in new (Session, WindowsUpdateOutcome)[]
        {
            (new(First, Second with { Identity = Second.Identity with { RevisionNumber = 13 } }), WindowsUpdateOutcome.SelectionChanged),
            (new(First, Second, Second), WindowsUpdateOutcome.SelectionChanged),
            (new(First, Second with { EulaAccepted = false }), WindowsUpdateOutcome.LicensePending),
            (new(First, Second with { InstallationBehavior = new(2) }), WindowsUpdateOutcome.ExclusiveUpdate),
            (new(First, Second) { SearchCode = 3 }, WindowsUpdateOutcome.PreflightInconclusive),
            (new(First, Second) { Busy = true }, WindowsUpdateOutcome.Busy),
            (new(First, Second) { Restart = true }, WindowsUpdateOutcome.RestartPending)
        })
        {
            var result = Execute(session);
            Check(result.Outcome == expected && session.Events.SequenceEqual(["Revalidate", "search"]),
                $"revalidação bloqueia download e instalação em {expected}");
        }

        var downloadFailed = new Session(First, Second)
        {
            DownloadResult = new(2, 0, false, [new(2), new(4, Failure)])
        };
        var failed = Execute(downloadFailed);
        Check(failed.Outcome == WindowsUpdateOutcome.Download && !failed.FullySucceeded &&
            failed.Items[1].Error == Failure && !downloadFailed.Events.Contains("install"),
            "download por item com falha bloqueia instalação mesmo com sucesso geral");
        var pending = new Session(First, Second with { IsDownloaded = false });
        var pendingResult = Execute(pending);
        Check(pendingResult.Outcome == WindowsUpdateOutcome.Download && pendingResult.Items[1].Code == 0 &&
            !pending.Events.Contains("install"), "pacote ainda não baixado aparece pendente e bloqueia o lote");
        var busyAfter = new Session(First, Second) { BusyAfterDownload = true };
        Check(Execute(busyAfter).Outcome == WindowsUpdateOutcome.Busy &&
            busyAfter.Events.Contains("download") && !busyAfter.Events.Contains("install"),
            "atividade concorrente surgida durante o download bloqueia a instalação");

        var partial = new Session(First, Second)
        {
            InstallResult = new(3, Failure, false, [new(2, RebootRequired: true), new(5, Failure)])
        };
        var installed = Execute(partial);
        Check(installed.Outcome == WindowsUpdateOutcome.Installation && installed.Overall.Code == 3 &&
            installed.ExitCode != 0 && installed.RequiresRestart && installed.Items[1].Code == 5,
            "instalação parcial conserva cancelamento por item e reinício sinalizado pelo Windows");

        var interrupted = new Session(First, Second) { InterruptInstallation = true };
        var unknown = Execute(interrupted);
        Check(unknown.Outcome == WindowsUpdateOutcome.Installation && unknown.Overall.Code == -1 &&
            unknown.Overall.RebootRequired is null && unknown.ExitCode != 0,
            "exceção após início da instalação produz resultado inconclusivo");

        var exchange = new WindowsUpdateExchange(2, _ => { });
        exchange.Receive(WindowsUpdateOperation.AuthorizationDeclined);
        Check(exchange.Result.Outcome == WindowsUpdateOutcome.AuthorizationDeclined,
            "recusa UAC é distinta de falha de comunicação");

        foreach (string[] messages in new string[][]
        {
            [WindowsUpdateOperation.PhasePrefix + WindowsUpdatePhase.Revalidate, "DONE|0"],
            [WindowsUpdateOperation.PhasePrefix + WindowsUpdatePhase.Revalidate, confirmed.ToWire(), "DONE|0"],
            [WindowsUpdateOperation.PhasePrefix + WindowsUpdatePhase.Revalidate, WindowsUpdateOperation.PhasePrefix + WindowsUpdatePhase.Download,
                WindowsUpdateOperation.PhasePrefix + WindowsUpdatePhase.Install, confirmed.ToWire(), "DONE|1"],
            [WindowsUpdateOperation.PhasePrefix + WindowsUpdatePhase.Revalidate, WindowsUpdateOperation.PhasePrefix + WindowsUpdatePhase.Download,
                WindowsUpdateOperation.PhasePrefix + WindowsUpdatePhase.Install, confirmed.ToWire()]
        })
        {
            exchange = new WindowsUpdateExchange(2, _ => { });
            foreach (string message in messages) exchange.Receive(message);
            Check(exchange.Result.Outcome == WindowsUpdateOutcome.NoResult, "retorno ausente ou incoerente não vira sucesso");
        }

        var worstCase = new WindowsUpdateOperation(WindowsUpdateOutcome.Installation, new(-1, int.MinValue, null),
            Enumerable.Repeat(new WindowsUpdateNativeResult(-1, int.MinValue, null), WindowsUpdateOperation.MaxSelection).ToArray());
        string wire = worstCase.ToWire();
        Check(wire.Length <= 750 && WindowsUpdateOperation.FromWire(wire[WindowsUpdateOperation.ResultPrefix.Length..],
            WindowsUpdateOperation.MaxSelection)?.Items.Count == WindowsUpdateOperation.MaxSelection,
            "lote máximo cabe no pipe sem truncar o resultado de nenhum item");
    }

    // Contratos IDispatch simulados. Nenhum objeto COM ou serviço do Windows é criado.
    public sealed record HistoryEntry(DateTime Date, string Title, int Operation, int ResultCode, int HResult);
    public sealed class HistoryCollection(HistoryEntry[] entries)
    {
        public int Count => entries.Length;
        public HistoryEntry Item(int index) => entries[index];
    }
    public sealed class HistorySession(int total, params HistoryEntry[] entries)
    {
        public int Requested = -1;
        public int Start = -1;
        public HistorySession CreateUpdateSearcher() => this;
        public int GetTotalHistoryCount() => total;
        public HistoryCollection QueryHistory(int start, int count)
        {
            Start = start; Requested = count; return new(entries);
        }
    }

    public sealed record Identity(string UpdateID, int RevisionNumber);
    public sealed record Behavior(int Impact);
    public sealed record Offer(Identity Identity, bool EulaAccepted = true, bool IsDownloaded = true)
    {
        public string Id => Identity.UpdateID + "@" + Identity.RevisionNumber;
        public Behavior InstallationBehavior { get; init; } = new(0);
    }

    public sealed class Offers(params Offer[] entries)
    {
        private readonly List<Offer> _entries = [.. entries];
        public int Count => _entries.Count;
        public Offer Item(int index) => _entries[index];
        public void Add(Offer offer) => _entries.Add(offer);
        public string[] Ids() => _entries.Select(offer => offer.Id).ToArray();
    }

    public sealed record SearchResult(int ResultCode, Offers Updates);
    public sealed record ItemResult(int ResultCode, int HResult = 0, bool RebootRequired = false);
    public sealed record Result(int ResultCode, int HResult, bool RebootRequired, ItemResult[] Items)
    {
        public ItemResult GetUpdateResult(int index) => Items[index];
    }

    public sealed class Session(params Offer[] entries)
    {
        public readonly List<string> Events = [];
        public readonly Offers Offers = new(entries);
        public int SearchCode { get; init; } = 2;
        public bool Busy { get; init; }
        public bool BusyAfterDownload { get; init; }
        public bool Restart { get; init; }
        public bool InterruptInstallation { get; init; }
        public Result DownloadResult { get; init; } = new(2, 0, false, [new(2), new(2)]);
        public Result InstallResult { get; init; } = new(2, 0, false, [new(2), new(2)]);
        public string[] Downloaded { get; set; } = [];
        public string[] Installed { get; set; } = [];
        public Searcher CreateUpdateSearcher() => new(this);
        public Downloader CreateUpdateDownloader() => new(this);
        public Installer CreateUpdateInstaller() => new(this);
    }

    public sealed class Searcher(Session session)
    {
        public bool Online { get; set; }
        public SearchResult Search(string criteria)
        {
            if (!Online || criteria != "IsInstalled=0 and IsHidden=0 and Type='Software'")
                throw new InvalidOperationException("Busca oficial de software alterada.");
            session.Events.Add("search");
            return new(session.SearchCode, session.Offers);
        }
    }

    public sealed class Downloader(Session session)
    {
        public Offers Updates { get; set; } = new();
        public Result Download()
        {
            session.Events.Add("download");
            session.Downloaded = Updates.Ids();
            return session.DownloadResult;
        }
    }

    public sealed class Installer(Session session)
    {
        public Offers Updates { get; set; } = new();
        public bool IsBusy => session.Busy || session.BusyAfterDownload && session.Downloaded.Length > 0;
        public bool RebootRequiredBeforeInstallation => session.Restart;
        public Result Install()
        {
            session.Events.Add("install");
            session.Installed = Updates.Ids();
            if (session.InterruptInstallation) throw new IOException("WUA fixture");
            return session.InstallResult;
        }
    }
}
