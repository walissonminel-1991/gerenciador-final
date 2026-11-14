using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// G1.22: provedor inteiramente sintetico com mutacao DURANTE enumeracao.
// Persistencia apenas em pasta temporaria exclusiva; nenhum DefaultPath/WMI/WUA/INF.
internal static class DriverG122ConcurrentFreezeTests
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    // Retorna a lista inicial e muda a origem assim que a PRIMEIRA enumeracao
    // termina. A copia G1.21 observava Items antigos e Store/INF novos.
    private sealed class ChangeOnFirstEnumeration : IReadOnlyList<DriverInventoryEntry>
    {
        private readonly List<DriverInventoryEntry> items;
        private readonly Action mutate;
        private bool fired;
        internal bool Fired => fired;
        internal ChangeOnFirstEnumeration(List<DriverInventoryEntry> items, Action mutate)
        { this.items = items; this.mutate = mutate; }
        public int Count => items.Count;
        public DriverInventoryEntry this[int index] => items[index];
        public IEnumerator<DriverInventoryEntry> GetEnumerator()
        {
            foreach (var item in items) yield return item;
            if (!fired) { fired = true; mutate(); }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static void Run(MainForm owner)
    {
        string folder = Path.Combine(Path.GetTempPath(), "GF-G122-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "drivers-v1.json");
        var fieldPath = typeof(MainForm).GetField("_driverHistoryPathForTest", Private)!;
        var fieldHold = typeof(MainForm).GetField("_driverHistoryBeforeWriteForTest", Private)!;
        var fieldReplace = typeof(MainForm).GetField("_driverHistoryBeforeReplaceForTest", Private)!;
        object? priorPath = fieldPath.GetValue(owner);
        object? priorHold = fieldHold.GetValue(owner);
        object? priorReplace = fieldReplace.GetValue(owner);
        Form? form = null;
        try
        {
            var a = new DriverInventoryEntry(@"PCI\G122\A", "Dispositivo A", "Fornecedor A",
                "1.0", "oem122.inf", true, true);
            var b = new DriverInventoryEntry(@"USB\G122\B", "Dispositivo B", "Fornecedor B",
                "2.0", "oem123.inf", true, true);
            var originalPackage = new DriverStoreEvidence("oem122.inf", DriverStoreState.Mapped,
                @"C:\Windows\INF\oem122.inf",
                @"C:\Windows\System32\DriverStore\FileRepository\g122\oem122.inf");
            a = a with { PackageEvidence = originalPackage,
                HardwareIds = [@"PCI\VEN_1220&DEV_0001"] };
            var baseline = new DriverInventoryResult([a, b], false, "G1.22 baseline sintética")
                { SignedRows = 2 };
            var baselineAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            Ensure(DriverHistory.Record(path, baseline, baselineAt).Saved,
                "baseline historico v1 nao gravou");
            var stableBytes = File.ReadAllBytes(path);

            var itemSource = new List<DriverInventoryEntry> { a, b };
            var store = new List<DriverStoreEvidence> { originalPackage };
            var published = new List<DriverPublishedPackage> { new(originalPackage, 1, false) };
            var history = new List<DriverWindowsEvent> {
                new(a.DeviceId, a.InfName, new DateTime(2026, 11, 14), DriverInstallOutcome.Succeeded)
            };
            var changing = new ChangeOnFirstEnumeration(itemSource, () =>
            {
                // Interleaving entre a copia de Items e a copia de Store:
                // mesmos objetos de entrada agora descrevem tempos diferentes.
                itemSource[0] = a with { DeviceId = @"PCI\G122\FALSO",
                    Version = "99.0", InfName = "oem999.inf" };
                store[0] = originalPackage with { State = DriverStoreState.Inconsistent,
                    InfName = "oem999.inf" };
                published[0] = new(store[0], 0, true);
                history[0] = new(@"PCI\G122\FALSO", "oem999.inf",
                    new DateTime(2026, 11, 15), DriverInstallOutcome.Failed);
            });
            var incoming = new DriverInventoryResult(changing, false, "Consulta sintética")
            {
                Store = new(store, false, 0),
                PublishedPackages = new(published, false, true),
                WindowsHistory = new(history, false, false, false, 0),
                SignedRows = 2
            };

            fieldPath.SetValue(owner, path);
            fieldHold.SetValue(owner, null);
            fieldReplace.SetValue(owner, null);
            Action? read = null;
            Action<string>? filter = null;
            var factory = typeof(MainForm).GetMethod("BuildDriversPlaceholder", Private)!;
            var page = (Control)factory.Invoke(owner, [
                (Func<CancellationToken, Task<DriverInventoryResult>>)(_ => Task.FromResult(incoming)),
                (TimeSpan?)TimeSpan.FromSeconds(15),
                (Action<Action>)(callback => read = callback),
                (Action<Action<string>>)(callback => filter = callback)
            ])!;
            form = new Form { Text = "G1.22 fontes mutáveis sintéticas",
                ClientSize = new Size(900, 560), ShowInTaskbar = false };
            page.Dock = DockStyle.Fill;
            form.Controls.Add(page);
            form.Show(owner);
            Pump(50);
            read!();
            var refresh = Find<Button>(page, "DriverInventoryRefresh");
            var list = Find<DarkDriverList>(page, "DriverInventoryList");
            var status = Find<Label>(page, "DriverInventoryStatus");
            var historyStatus = Find<Label>(page, "DriverHistoryStatus");
            PumpUntil(() => changing.Fired && refresh.Text == "Consultar drivers");
            Ensure(list.Items.Count == 0,
                "fotografia rasgada foi publicada durante copia de Items/Store");
            Ensure(File.ReadAllBytes(path).SequenceEqual(stableBytes),
                "historico v1 mudou apos fotografia rasgada ou ambigua");
            Ensure(status.Text.Contains("indisponível", StringComparison.OrdinalIgnoreCase) &&
                !historyStatus.Text.Contains("Desde a consulta anterior", StringComparison.Ordinal),
                "estado acessivel da consulta rasgada apresenta sucesso");

            foreach (var size in new[] { new Size(500, 340), new Size(790, 510),
                new Size(1200, 720), new Size(1450, 820) })
            {
                page.ClientSize = size;
                filter!("Todos");
                filter!("Problemas");
                filter!("Todos");
                Pump(20);
                Ensure(list.Items.Count == 0 &&
                    File.ReadAllBytes(path).SequenceEqual(stableBytes),
                    "reentrada/refiltro publicou fotografia rasgada em " + size);
            }
            Console.WriteLine("OK G1.22: mutação durante copia Items/Store rejeitada; painel vazio, historico v1 byte a byte intacto");
        }
        finally
        {
            form?.Dispose();
            fieldPath.SetValue(owner, priorPath);
            fieldHold.SetValue(owner, priorHold);
            fieldReplace.SetValue(owner, priorReplace);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
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
    private static void Pump(int ms)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(8); }
        Application.DoEvents();
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < 5000) Pump(20);
        Ensure(condition(), "interleaving WinForms nao convergiu");
    }
    private static void Ensure(bool condition, string reason)
    {
        if (!condition) throw new Exception("G1.22: " + reason);
    }
}
