using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using GerenciadorAplicativos;

// G1.20: WinForms real com leitura sintetica e persistencia SOMENTE em pasta
// exclusiva. Nenhum acesso a DriverHistory.DefaultPath, WMI, WUA, INF ou SetupAPI.
internal static class DriverG120HistoryLifetimeTests
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run(MainForm owner)
    {
        var a = new DriverInventoryEntry(@"PCI\G120\A", "A sintético", "Fornecedor",
            "1.0", "oem120.inf", true, true);
        DriverInventoryResult Snap(string version) =>
            new([a with { Version = version }], false, "consulta sintética G1.20");
        var folder = Path.Combine(Path.GetTempPath(), "GF-G120-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "drivers-v1.json");
        try
        {
            // F1: duas interfaces independentes; a consulta MAIS ANTIGA
            // termina depois da nova. A hora do gravador nao pode vencer.
            var oldRead = new TaskCompletionSource<DriverInventoryResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Fixture(owner, path);
            using var oldPage = Open(owner, _ => oldRead.Task, out var oldAction,
                out _, TimeSpan.FromSeconds(30));
            oldAction();
            Pump(40);
            using var secondOwner = new MainForm(startServices: false);
            secondOwner.Show();
            Pump(40);
            Fixture(secondOwner, path);
            using var newPage = Open(secondOwner, _ => Task.FromResult(Snap("2.0")),
                out var newAction, out var newFilter, TimeSpan.FromSeconds(30));
            newAction();
            var newHistory = Find<Label>(newPage, "DriverHistoryStatus");
            PumpUntil(() => File.Exists(path) &&
                newHistory.Text.Contains("Histórico local iniciado", StringComparison.Ordinal));
            byte[] current = File.ReadAllBytes(path);
            var before = Read(path);
            Ensure(before.Schema == 1 && before.Devices.Values.Single() ==
                DriverHistory.Snapshot(Snap("2.0")).Values.Single(),
                "gravacao nova nao corresponde ao snapshot publicado");
            oldRead.TrySetResult(Snap("1.0"));
            var oldHistory = Find<Label>(oldPage, "DriverHistoryStatus");
            // Um rótulo Text pode disparar eventos WinForms antes de sua
            // AccessibleDescription correspondente ser aplicada.
            PumpUntil(() => oldHistory.Text.Contains("data não posterior", StringComparison.Ordinal) &&
                oldHistory.AccessibleDescription?.Contains(oldHistory.Text, StringComparison.Ordinal) == true);
            Ensure(File.ReadAllBytes(path).SequenceEqual(current),
                "consulta anterior sobrescreveu historico mais novo");

            // Painel, fonte acessivel, tooltip, filtro e viewports.
            var status = Find<Label>(newPage, "DriverInventoryStatus");
            var list = Find<DarkDriverList>(newPage, "DriverInventoryList");
            var tips = (ToolTip)typeof(MainForm).GetField("_toolTips", Private)!
                .GetValue(secondOwner)!;
            foreach (var size in new[] { new Size(500, 340), new Size(790, 510),
                new Size(1200, 720), new Size(1450, 820) })
            {
                newPage.ClientSize = size;
                Pump(25);
                Ensure(status.AccessibleDescription?.Contains(newHistory.Text, StringComparison.Ordinal) == true &&
                    newHistory.AccessibleDescription?.Contains(newHistory.Text, StringComparison.Ordinal) == true &&
                    string.IsNullOrEmpty(tips.GetToolTip(status)) &&
                    string.IsNullOrEmpty(tips.GetToolTip(newHistory)),
                    "histórico inacessível ou tooltip invasivo no viewport: " + size);
            }
            list.Focus();
            typeof(DarkDriverList).GetMethod("OnKeyDown", Private)!
                .Invoke(list, [new KeyEventArgs(Keys.Down)]);
            newFilter("Problemas");
            newFilter("Todos");
            Pump(40);
            Ensure(status.AccessibleDescription?.Contains(newHistory.Text, StringComparison.Ordinal) == true,
                "filtro/foco apagou a comparacao publicada");

            // F2: descarte depois de publicar, ANTES de iniciar gravacao.
            var deferred = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Fixture(owner, path, _ => deferred.Task);
            var deferredPage = Open(owner, _ => Task.FromResult(Snap("3.0")),
                out var deferredAction, out _, TimeSpan.FromSeconds(30));
            deferredAction();
            var deferredHistory = Find<Label>(deferredPage, "DriverHistoryStatus");
            PumpUntil(() => deferredHistory.Text.Contains("em andamento", StringComparison.Ordinal));
            deferredPage.Dispose();
            deferred.TrySetResult();
            Pump(110);
            Ensure(File.ReadAllBytes(path).SequenceEqual(current),
                "pagina descartada antes da escrita alterou v1");

            // F3: pagina e descartada APOS serializar o arquivo temporario,
            // enquanto a substituicao atomica ainda nao ocorreu.
            using var entered = new ManualResetEventSlim(false);
            using var resume = new ManualResetEventSlim(false);
            Fixture(owner, path, null, () =>
            {
                entered.Set();
                if (!resume.Wait(TimeSpan.FromSeconds(8)))
                    throw new Exception("G1.20: fixture nao liberou troca atomica");
            });
            var writingPage = Open(owner, _ => Task.FromResult(Snap("4.0")),
                out var writingAction, out _, TimeSpan.FromSeconds(30));
            writingAction();
            PumpUntil(() => entered.IsSet);
            Ensure(Directory.GetFiles(folder, "*.tmp").Length == 1,
                "interleaving antes de File.Move nao preparou temporario");
            writingPage.Dispose();
            resume.Set();
            PumpUntil(() => Directory.GetFiles(folder, "*.tmp").Length == 0);
            Ensure(File.ReadAllBytes(path).SequenceEqual(current),
                "pagina descartada durante gravacao substituiu v1");

            // F4: cancelamento e retorno tardio antes da publicacao.
            Fixture(owner, path);
            var pending = new TaskCompletionSource<DriverInventoryResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancelPage = Open(owner, _ => pending.Task,
                out var cancel, out _, TimeSpan.FromSeconds(30));
            cancel();
            Pump(30);
            cancel();
            pending.TrySetResult(Snap("5.0"));
            Pump(90);
            Ensure(File.ReadAllBytes(path).SequenceEqual(current),
                "resultado tardio cancelado gravou historico");

            // F5: timeout nao publica nem persiste retorno tardio.
            var late = new TaskCompletionSource<DriverInventoryResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var timeoutPage = Open(owner, _ => late.Task,
                out var timed, out _, TimeSpan.FromMilliseconds(35));
            timed();
            Pump(120);
            late.TrySetResult(Snap("6.0"));
            Pump(90);
            Ensure(File.ReadAllBytes(path).SequenceEqual(current),
                "timeout e retorno tardio substituiriam v1");

            // Regressoes existentes continuam cobrindo arquivo malformado,
            // schema desconhecido, parcial/vazio, lock, 20 entradas e hashes.
            Console.WriteLine("OK G1.20: inicio temporal, duas janelas, descarte pre/gravação, cancelamento, timeout, retorno tardio e UI acessível");
        }
        finally
        {
            Fixture(owner, null);
            Directory.Delete(folder, recursive: true);
        }
    }

    private static DriverHistoryState Read(string path) =>
        JsonSerializer.Deserialize<DriverHistoryState>(File.ReadAllText(path))!
        ?? throw new Exception("G1.20: JSON historico invalido");

    private static void Fixture(MainForm owner, string? path,
        Func<CancellationToken, Task>? beforeWrite = null, Action? beforeReplace = null)
    {
        typeof(MainForm).GetField("_driverHistoryPathForTest", Private)!.SetValue(owner, path);
        typeof(MainForm).GetField("_driverHistoryBeforeWriteForTest", Private)!.SetValue(owner, beforeWrite);
        typeof(MainForm).GetField("_driverHistoryBeforeReplaceForTest", Private)!.SetValue(owner, beforeReplace);
    }

    private static Form Open(MainForm owner,
        Func<CancellationToken, Task<DriverInventoryResult>> provider,
        out Action read, out Action<string> filter, TimeSpan timeout)
    {
        Action? action = null;
        Action<string>? setFilter = null;
        var factory = typeof(MainForm).GetMethod("BuildDriversPlaceholder", Private)!;
        var page = (Control)factory.Invoke(owner, [
            provider, (TimeSpan?)timeout,
            (Action<Action>)(callback => action = callback),
            (Action<Action<string>>)(callback => setFilter = callback)])!;
        var form = new Form { Text = "G1.20 historia sintetica",
            ClientSize = new Size(900, 560), ShowInTaskbar = false };
        page.Dock = DockStyle.Fill;
        form.Controls.Add(page);
        form.Show(owner);
        Pump(40);
        read = action ?? throw new Exception("G1.20: refresh ausente");
        filter = setFilter ?? throw new Exception("G1.20: filtro ausente");
        return form;
    }

    private static T Find<T>(Control root, string name) where T : Control =>
        Descendants(root).OfType<T>().Single(x => x.Name == name);
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Pump(int ms = 80)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < ms)
        { Application.DoEvents(); Thread.Sleep(8); }
        Application.DoEvents();
    }
    private static void PumpUntil(Func<bool> predicate, int timeoutMs = 5000)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!predicate() && watch.ElapsedMilliseconds < timeoutMs)
            Pump(20);
        Ensure(predicate(), "condicao temporal nao ocorreu");
    }
    private static void Ensure(bool valid, string message)
    {
        if (!valid) throw new Exception("G1.20: " + message);
    }
}
