using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// G1.23: prova de limite ABA sem versao cooperativa + revogacao WinForms.
// Somente fontes sinteticas e historico v1 em diretorio temporario proprio.
internal static class DriverG123AbaRevocationTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private sealed class HookedList<T> : IReadOnlyList<T>
    {
        private readonly List<T> items;
        private readonly Action<int>? afterEnumeration;
        private int enumerations;
        internal int Enumerations => enumerations;
        internal HookedList(List<T> items, Action<int>? afterEnumeration)
        { this.items = items; this.afterEnumeration = afterEnumeration; }
        public int Count => items.Count;
        public T this[int index] => items[index];
        public IEnumerator<T> GetEnumerator()
        {
            foreach (T item in items) yield return item;
            afterEnumeration?.Invoke(++enumerations);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static void Run(MainForm owner)
    {
        var dir = Path.Combine(Path.GetTempPath(), "GF-G123-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "drivers-v1.json");
        var fieldPath = typeof(MainForm).GetField("_driverHistoryPathForTest", Private)!;
        var fieldBefore = typeof(MainForm).GetField("_driverHistoryBeforeWriteForTest", Private)!;
        var fieldReplace = typeof(MainForm).GetField("_driverHistoryBeforeReplaceForTest", Private)!;
        var savedPath = fieldPath.GetValue(owner);
        var savedBefore = fieldBefore.GetValue(owner);
        var savedReplace = fieldReplace.GetValue(owner);
        try
        {
            var evidence = new DriverStoreEvidence("oem123.inf", DriverStoreState.Mapped,
                @"C:\Windows\INF\oem123.inf",
                @"C:\Windows\System32\DriverStore\FileRepository\g123\oem123.inf");
            var eventA = new DriverWindowsEvent(@"PCI\G123\A", "oem123.inf",
                new DateTime(2026, 11, 14), DriverInstallOutcome.Succeeded);
            var a = new DriverInventoryEntry(@"PCI\G123\A", "Dispositivo G123 A",
                "Fornecedor A", "2.0", "oem123.inf", true, true)
            {
                PackageEvidence = evidence,
                HardwareIds = [@"PCI\VEN_1230&DEV_0001"],
                WindowsEvents = [eventA]
            };
            var b = new DriverInventoryEntry(@"USB\G123\B", "Dispositivo G123 B",
                "Fornecedor B", "1.0", "oem124.inf", true, true);
            DriverInventoryResult Complete(DriverInventoryEntry first) =>
                new([first, b], false, "consulta G1.23 sintetica")
                {
                    Store = new([evidence], false, 0),
                    PublishedPackages = new([new(evidence, 1, false)], false, true),
                    WindowsHistory = new([eventA], false, false, false, 0),
                    SignedRows = 2
                };
            var baseSnapshot = Complete(a);

            // ABA A->B->A: ambos os resultados observaveis sao iguais.
            // Nenhuma duplicacao de leitura pode revelar a transicao oculta.
            int abaChanges = 0;
            var source = new List<DriverInventoryEntry> { a, b };
            var aba = new HookedList<DriverInventoryEntry>(source, n =>
            {
                if (n != 1) return;
                source[0] = a with { Version = "99.0",
                    DeviceId = @"PCI\G123\ABA", InfName = "oem999.inf" };
                abaChanges++;
                source[0] = a;
                abaChanges++;
            });
            var acceptedAba = DriverInventorySnapshot.FreezeStable(
                baseSnapshot with { Items = aba }, CancellationToken.None);
            Ensure(aba.Enumerations == 2 && abaChanges == 2 &&
                acceptedAba.Items[0].DeviceId == a.DeviceId &&
                acceptedAba.Items[0].Version == a.Version,
                "transicao ABA indetectavel não foi exercitada");

            var store = new List<DriverStoreEvidence> { evidence };
            int storeAbaChanges = 0;
            var abaStore = new HookedList<DriverStoreEvidence>(store, n =>
            {
                if (n != 1) return;
                store[0] = evidence with { State = DriverStoreState.Inconsistent };
                storeAbaChanges++;
                store[0] = evidence;
                storeAbaChanges++;
            });
            var acceptedStore = DriverInventorySnapshot.FreezeStable(
                baseSnapshot with { Store = new(abaStore, false, 0) }, CancellationToken.None);
            Ensure(abaStore.Enumerations == 2 && storeAbaChanges == 2 &&
                acceptedStore.Store!.Packages[0] == evidence,
                "ABA de Store não atravesou as duas leituras");

            // Alteracao apos a ULTIMA enumeracao: a fotografia retornada e
            // estavel, mas nao existe prova de que ainda descreva a fonte.
            var liveItems = new List<DriverInventoryEntry> { a, b };
            var liveHistory = new List<DriverWindowsEvent> { eventA };
            int lastReadMutations = 0;
            var afterSecond = new HookedList<DriverWindowsEvent>(liveHistory, n =>
            {
                if (n != 2) return;
                liveItems[0] = a with { Version = "99.0" };
                liveHistory[0] = eventA with { Outcome = DriverInstallOutcome.Failed };
                lastReadMutations++;
            });
            var returned = DriverInventorySnapshot.FreezeStable(baseSnapshot with
            {
                Items = liveItems,
                WindowsHistory = new(afterSecond, false, false, false, 0)
            }, CancellationToken.None);
            Ensure(afterSecond.Enumerations == 2 && lastReadMutations == 1 &&
                liveItems[0].Version == "99.0" &&
                returned.Items[0].Version == "2.0" &&
                returned.WindowsHistory!.Events.Single().Outcome == DriverInstallOutcome.Succeeded,
                "limite apos ultima enumeracao não reproduzido");

            // Excecoes e invalidacoes nas duas passagens nao devem ser
            // convertidas em uma fotografia aceita.
            for (int failureAt = 1; failureAt <= 2; failureAt++)
            {
                int at = failureAt;
                var throwing = new HookedList<DriverInventoryEntry>(
                    new() { a, b }, n =>
                    {
                        if (n == at) throw new InvalidOperationException(
                            "enumeracao sintetica invalidada");
                    });
                MustThrow<InvalidOperationException>(() =>
                    DriverInventorySnapshot.FreezeStable(
                        baseSnapshot with { Items = throwing }, CancellationToken.None),
                    "falha na enumeracao " + at);
            }
            for (int cancelAt = 1; cancelAt <= 2; cancelAt++)
            {
                int at = cancelAt;
                using var sourceCancel = new CancellationTokenSource();
                var cancellation = new HookedList<DriverInventoryEntry>(
                    new() { a, b }, n => { if (n == at) sourceCancel.Cancel(); });
                MustThrow<OperationCanceledException>(() =>
                    DriverInventorySnapshot.FreezeStable(
                        baseSnapshot with { Items = cancellation }, sourceCancel.Token),
                    "cancelamento durante copia " + at);
            }
            // Dados duplicados permanecem VISIVEIS como ambiguidade;
            // histórico v1 G1.19 deve rejeitar.
            var ambiguous = DriverInventorySnapshot.FreezeStable(
                Complete(a) with { Items = new[] { a, a with { Version = "3.0" } } },
                CancellationToken.None);
            MustThrow<InvalidDataException>(() => DriverHistory.Snapshot(ambiguous),
                "DeviceId ambiguo não bloqueado no historico");

            // Demonstracao em arquivo TEMPORARIO do limite de autoridade:
            // a dupla leitura aceita ABA e Record nao possui versao nativa.
            var earlier = Complete(a with { Version = "1.0" });
            Ensure(DriverHistory.Record(path, earlier, DateTimeOffset.UtcNow.AddMinutes(-15)).Saved,
                "baseline local sintetico nao salvo");
            byte[] initial = File.ReadAllBytes(path);
            Ensure(DriverHistory.Record(path, acceptedAba, DateTimeOffset.UtcNow.AddMinutes(-14)).Saved &&
                !File.ReadAllBytes(path).SequenceEqual(initial),
                "historico de teste nao demonstrou limite de ABA aceito");
            // A afirmacao nao e que a versao 2.0 seja falsa; e que a
            // transicao ABA nao foi detectada por nenhuma das duas copias.
            Console.WriteLine("LIMITE G1.23: ABA A-B-A e mudanca apos segunda leitura invisiveis sem versao cooperativa; apenas arquivo temporario");

            Fixture(owner, path);
            var revocationTriggered = 0;
            int calls = 0;
            Action? refreshAction = null;
            TextBox? searchBox = null;
            Action<string>? filterAction = null;
            var interrupted = new HookedList<DriverInventoryEntry>(
                new() { a, b }, n =>
                {
                    if (n != 1) return;
                    revocationTriggered++;
                    // Eventos REAIS de TextChanged e filtro reentram enquanto
                    // FreezeStable ainda está enumerando a fonte sintetica.
                    searchBox!.Text = "Dispositivo";
                    filterAction!("Todos");
                    refreshAction!(); // pede cancelamento da espera atual
                });
            DriverInventoryResult interruptedInventory =
                Complete(a) with { Items = interrupted };

            using (var page = Open(owner, token =>
            {
                calls++;
                return Task.FromResult(calls == 1 ? Complete(a) :
                    calls == 2 ? interruptedInventory :
                    Complete(a with { Version = "3.0" }));
            }, out refreshAction, out filterAction, TimeSpan.FromSeconds(20)))
            {
                var list = Find<DarkDriverList>(page, "DriverInventoryList");
                var refresh = Find<Button>(page, "DriverInventoryRefresh");
                var investigate = Find<Button>(page, "DriverReadOnlyInvestigation");
                var status = Find<Label>(page, "DriverInventoryStatus");
                var historyStatus = Find<Label>(page, "DriverHistoryStatus");
                var title = Find<Label>(page, "DriverInventoryTitle");
                var tooltip = (ToolTip)typeof(MainForm).GetField("_toolTips", Private)!
                    .GetValue(owner)!;
                searchBox = Find<TextBox>(page, "DriverInventorySearch");
                refreshAction!();
                PumpUntil(() => historyStatus.Text.Contains("Desde a consulta anterior", StringComparison.Ordinal)
                    && refresh.Text == "Consultar drivers");
                byte[] approvedHistory = File.ReadAllBytes(path);
                Ensure(list.Items.Count == 2 && investigate.Enabled,
                    "primeira fotografia não foi publicada");
                refreshAction!();
                PumpUntil(() => revocationTriggered == 1 &&
                    refresh.Text == "Consultar drivers");
                Ensure(calls == 2 && list.Items.Count == 2 &&
                    list.Items[0].DeviceId == a.DeviceId &&
                    File.ReadAllBytes(path).SequenceEqual(approvedHistory),
                    "cancelamento reentrante publicou inventario ou alterou v1");
                Ensure(!investigate.Enabled,
                    "consulta revogada voltou a habilitar Investigar como se fosse atual");
                Ensure(investigate.AccessibleDescription?.Contains("anterior", StringComparison.OrdinalIgnoreCase) == true,
                    "botao acessivel não identificou a fotografia anterior revogada");

                for (int i = 0; i < 4; i++)
                {
                    var sizes = new[] { new Size(500, 340), new Size(790, 510),
                        new Size(1200, 720), new Size(1450, 820) };
                    page.ClientSize = sizes[i];
                    filterAction!("Presentes");
                    filterAction!("Todos");
                    Pump(20);
                    Ensure(list.Items.Count == 2 && !investigate.Enabled &&
                        File.ReadAllBytes(path).SequenceEqual(approvedHistory),
                        "filtros reabilitaram consulta revogada " + sizes[i]);
                    Ensure(status.AccessibleDescription?.Contains(status.Text, StringComparison.Ordinal) == true &&
                        tooltip.GetToolTip(status).Contains(status.Text, StringComparison.Ordinal),
                        "descricao acessivel/tooltip sem estado consistente");
                }

                // Nova consulta VALIDA deve reabilitar o dialogo novamente.
                searchBox.Text = "";
                refreshAction!();
                PumpUntil(() => calls == 3 && refresh.Text == "Consultar drivers" &&
                    historyStatus.Text.Contains("Desde a consulta anterior", StringComparison.Ordinal) &&
                    File.ReadAllBytes(path).SequenceEqual(approvedHistory) == false);
                Ensure(investigate.Enabled && list.Items.Count == 2 &&
                    list.Items[0].Version == "3.0",
                    "nova consulta valida nao reabilitou a investigacao");
                list.Focus();
                typeof(DarkDriverList).GetMethod("OnKeyDown", Private)!
                    .Invoke(list, [new KeyEventArgs(Keys.Down)]);
                Ensure(list.SelectedItem?.DeviceId == a.DeviceId &&
                    list.AccessibleDescription?.Contains(a.DeviceId, StringComparison.Ordinal) == true,
                    "selecao/teclado incoerentes apos reabertura valida");
                var report = DriverReadOnlyReport.Format(Complete(a with { Version = "3.0" }),
                    list.SelectedItem);
                Ensure(report.Contains("Versão instalada (WMI): 3.0", StringComparison.Ordinal) &&
                    !report.Contains("99.0", StringComparison.Ordinal),
                    "formatador publicou evidencia sintética revogada");
            }

            // Timeout e retorno tardio nao podem substituir o v1 validado.
            byte[] finalHistory = File.ReadAllBytes(path);
            var pending = new TaskCompletionSource<DriverInventoryResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (var latePage = Open(owner, _ => pending.Task, out var lateAction,
                out _, TimeSpan.FromMilliseconds(35)))
            {
                lateAction();
                Pump(130);
                pending.TrySetResult(Complete(a with { Version = "42.0" }));
                Pump(90);
                Ensure(File.ReadAllBytes(path).SequenceEqual(finalHistory),
                    "timeout/retorno tardio sobrescreveram v1");
            }
            // Descarte e resultado tardio com outro provider sintetico.
            var abandoned = new TaskCompletionSource<DriverInventoryResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var discarded = Open(owner, _ => abandoned.Task, out var disposeAction,
                out _, TimeSpan.FromSeconds(10));
            disposeAction();
            discarded.Dispose();
            abandoned.TrySetResult(Complete(a with { Version = "55.0" }));
            Pump(90);
            Ensure(File.ReadAllBytes(path).SequenceEqual(finalHistory),
                "descarte/retorno tardio sobrescreveram v1");
            Console.WriteLine("OK G1.23: WinForms revogacao reentrante, filtros, teclado, acessibilidade, novo inventario valido, timeout/descarte, v1 preservado");
        }
        finally
        {
            Fixture(owner, savedPath as string, savedBefore as Func<CancellationToken, Task>,
                savedReplace as Action);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    private static void MustThrow<T>(Action action, string reason) where T : Exception
    {
        bool thrown = false;
        try { action(); }
        catch (T) { thrown = true; }
        Ensure(thrown, reason);
    }

    private static void Fixture(MainForm owner, string? path,
        Func<CancellationToken, Task>? beforeWrite = null,
        Action? beforeReplace = null)
    {
        typeof(MainForm).GetField("_driverHistoryPathForTest", Private)!.SetValue(owner, path);
        typeof(MainForm).GetField("_driverHistoryBeforeWriteForTest", Private)!.SetValue(owner, beforeWrite);
        typeof(MainForm).GetField("_driverHistoryBeforeReplaceForTest", Private)!.SetValue(owner, beforeReplace);
    }

    private static Form Open(MainForm owner,
        Func<CancellationToken, Task<DriverInventoryResult>> readForTest,
        out Action? read, out Action<string>? filter, TimeSpan timeout)
    {
        Action? action = null;
        Action<string>? changeFilter = null;
        var method = typeof(MainForm).GetMethod("BuildDriversPlaceholder", Private)!;
        var page = (Control)method.Invoke(owner, [
            readForTest, (TimeSpan?)timeout,
            (Action<Action>)(callback => action = callback),
            (Action<Action<string>>)(callback => changeFilter = callback)
        ])!;
        var form = new Form { Text = "G1.23 fotografia sintetica", ClientSize = new Size(900, 560),
            ShowInTaskbar = false };
        page.Dock = DockStyle.Fill;
        form.Controls.Add(page);
        form.Show(owner);
        Pump(40);
        read = action ?? throw new Exception("G1.23: acao de consulta ausente");
        filter = changeFilter ?? throw new Exception("G1.23: filtro ausente");
        return form;
    }

    private static T Find<T>(Control control, string name) where T : Control =>
        Descendants(control).OfType<T>().Single(x => x.Name == name);
    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Pump(int milliseconds = 70)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        { Application.DoEvents(); Thread.Sleep(8); }
        Application.DoEvents();
    }
    private static void PumpUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < timeoutMs) Pump(20);
        Ensure(condition(), "interleaving WinForms nao convergiu");
    }
    private static void Ensure(bool value, string reason)
    {
        if (!value) throw new Exception("G1.23: " + reason);
    }
}
