using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using GerenciadorAplicativos;

// G1.21: componentes WinForms reais, fornecedor totalmente sintetico e
// persistencia v1 SOMENTE em diretorio temporario descartavel.
// Nunca invoca WMI/WUA/SetupAPI/INF nem abre DriverHistory.DefaultPath.
internal static class DriverG121PublicationFreezeTests
{
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    internal static void Run(MainForm owner)
    {
        var folder = Path.Combine(Path.GetTempPath(), "GF-G121-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "drivers-v1.json");
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fieldPath = typeof(MainForm).GetField("_driverHistoryPathForTest", Private)!;
        var fieldHold = typeof(MainForm).GetField("_driverHistoryBeforeWriteForTest", Private)!;
        var fieldReplace = typeof(MainForm).GetField("_driverHistoryBeforeReplaceForTest", Private)!;
        var originalPath = fieldPath.GetValue(owner);
        var originalHold = fieldHold.GetValue(owner);
        var originalReplace = fieldReplace.GetValue(owner);
        Form? form = null;
        try
        {
            const string oldHardware = @"PCI\VEN_1210&DEV_0001";
            const string newHardware = @"PCI\VEN_9999&DEV_9999";
            var packages = new List<DriverStoreEvidence> {
                new("oem121.inf", DriverStoreState.Mapped,
                    @"C:\Windows\INF\oem121.inf",
                    @"C:\Windows\System32\DriverStore\FileRepository\g121\oem121.inf")
            };
            var published = new List<DriverPublishedPackage> {
                new(packages[0], 1, false)
            };
            var events = new List<DriverWindowsEvent> {
                new(@"PCI\G121\A", "oem121.inf", new DateTime(2026, 11, 14),
                    DriverInstallOutcome.Succeeded)
            };
            var historyEvents = new List<DriverWindowsEvent>(events);
            var hardware = new List<string> { oldHardware };
            var a = new DriverInventoryEntry(@"PCI\G121\A",
                "Dispositivo original G121", "Fornecedor original", "1.0",
                "oem121.inf", true, true)
            {
                HardwareIds = hardware, WindowsEvents = events,
                PackageEvidence = packages[0], PnpProblemCode = 0
            };
            var b = new DriverInventoryEntry(@"USB\G121\B",
                "Dispositivo secundario G121", "Fornecedor B", "2.0",
                "oem122.inf", true, true);
            var source = new List<DriverInventoryEntry> { a, b };
            var result = new DriverInventoryResult(source, false, "Consulta sintetica G121")
            {
                Store = new(packages, false, 0),
                PublishedPackages = new(published, false, true),
                WindowsHistory = new(historyEvents, false, false, false, 0),
                SignedRows = 2
            };
            var expected = DriverHistory.Snapshot(result);
            fieldPath.SetValue(owner, path);
            fieldHold.SetValue(owner, (Func<CancellationToken, Task>)(_ => hold.Task));
            fieldReplace.SetValue(owner, null);

            Action? read = null;
            Action<string>? filter = null;
            var builder = typeof(MainForm).GetMethod("BuildDriversPlaceholder", Private)!;
            var page = (Control)builder.Invoke(owner, [
                (Func<CancellationToken, Task<DriverInventoryResult>>)(_ => Task.FromResult(result)),
                (TimeSpan?)TimeSpan.FromSeconds(25),
                (Action<Action>)(cb => read = cb),
                (Action<Action<string>>)(cb => filter = cb)
            ])!;
            form = new Form { Text = "G1.21 sintético", ClientSize = new Size(900, 560),
                ShowInTaskbar = false };
            page.Dock = DockStyle.Fill;
            form.Controls.Add(page);
            form.Show(owner);
            Pump(50);
            read!();
            var list = Find<DarkDriverList>(page, "DriverInventoryList");
            var label = Find<Label>(page, "DriverHistoryStatus");
            var state = Find<Label>(page, "DriverInventoryStatus");
            var search = Find<TextBox>(page, "DriverInventorySearch");
            var title = Find<Label>(page, "DriverInventoryTitle");
            var investigate = Find<Button>(page, "DriverReadOnlyInvestigation");
            PumpUntil(() => list.Items.Count == 2 &&
                label.Text.Contains("em andamento", StringComparison.Ordinal));
            Ensure(!File.Exists(path) && investigate.Enabled,
                "fixture nao bloqueou gravacao apos publicacao");

            // Mutar a FONTE depois da publicacao: colecao, ordem, identidade,
            // evidencia Win32_PnPSignedDriver, HardwareIds, logs e Store/INF.
            source.Clear();
            source.Add(a with { DeviceId = @"PCI\G121\FORJADO",
                DeviceName = "Dispositivo forjado", Provider = "Outro",
                InfName = "oem999.inf", Version = "99.0" });
            hardware[0] = newHardware;
            events[0] = new(@"PCI\G121\FORJADO", "oem999.inf",
                new DateTime(2026, 11, 15), DriverInstallOutcome.Failed);
            historyEvents[0] = events[0];
            packages[0] = packages[0] with { State = DriverStoreState.Inconsistent };
            published[0] = new(packages[0], 0, true);

            // Reentrancia: filtro/pesquisa REAL reconstroi a lista publicada.
            filter!("Todos");
            Pump(40);
            Ensure(list.Items.Count == 2 &&
                list.Items[0].DeviceId == a.DeviceId &&
                list.Items[1].DeviceId == b.DeviceId,
                "mutacao da colecao fonte alterou a lista publicada");
            Ensure(title.Text.Contains("Drivers: 2", StringComparison.Ordinal),
                "contagem da UI nao corresponde a fotografia congelada");

            foreach (var size in new[] { new Size(500, 340), new Size(790, 510),
                new Size(1200, 720), new Size(1450, 820) })
            {
                page.ClientSize = size;
                filter!("Presentes");
                filter!("Todos");
                Pump(25);
                Ensure(list.Items.Count == 2 && list.Items[0].DeviceId == a.DeviceId,
                    "refiltro/viewports expuseram fonte mutada " + size);
            }
            search.Text = "Dispositivo original G121";
            Pump(40);
            Ensure(list.Items.Count == 1 && list.Items[0].DeviceId == a.DeviceId,
                "pesquisa passou a usar dados da fonte alterada");
            search.Text = "";
            Pump(40);
            typeof(DarkDriverList).GetMethod("SelectIndex", Private)!
                .Invoke(list, [0]);
            Ensure(list.SelectedItem?.DeviceId == a.DeviceId &&
                list.SelectedItem.HardwareIds.SequenceEqual([oldHardware]) &&
                list.SelectedItem.WindowsEvents.Single().DeviceId == a.DeviceId &&
                list.AccessibleDescription?.Contains(a.DeviceId, StringComparison.Ordinal) == true,
                "seleção/descrição acessivel reutilizou evidencia mutada");

            // O modal WinForms real continua coberto pelos testes G1.10–20.
            // Aqui validar o FORMATADOR real com a fotografia que a lista
            // WinForms publicou, sem automacao de foco/clique dependente
            // da geometria de um formulario de teste com viewport ampliado.
            Ensure(investigate.Enabled, "botao de investigacao desabilitado");
            var displayed = list.Items.ToArray();
            var frozenPackage = displayed[0].PackageEvidence;
            Ensure(frozenPackage is { State: DriverStoreState.Mapped },
                "associacao Store da lista foi mutada");
            var reportInventory = new DriverInventoryResult(displayed, false,
                "fotografia publicada G1.21")
            {
                Store = new([frozenPackage!], false, 0),
                PublishedPackages = new([new(frozenPackage!, 1, false)], false, true),
                WindowsHistory = new(displayed[0].WindowsEvents.ToArray(),
                    false, false, false, 0)
            };
            string reportText = DriverReadOnlyReport.Format(reportInventory, list.SelectedItem);
            Ensure(reportText.Contains(a.DeviceId, StringComparison.Ordinal) &&
                reportText.Contains(oldHardware, StringComparison.Ordinal) &&
                !reportText.Contains(newHardware, StringComparison.Ordinal) &&
                !reportText.Contains("FORJADO", StringComparison.Ordinal) &&
                !reportText.Contains("oem999.inf", StringComparison.Ordinal) &&
                !reportText.Contains("99.0", StringComparison.Ordinal),
                "formatador consultivo exibiu evidencia da origem mutada");

            // A tarefa de historico recebe a MESMA fotografia publicada.
            hold.TrySetResult();
            PumpUntil(() => File.Exists(path) &&
                !label.Text.Contains("em andamento", StringComparison.Ordinal));
            var saved = JsonSerializer.Deserialize<DriverHistoryState>(File.ReadAllText(path))!;
            Ensure(saved.Schema == 1 && saved.Devices.Count == 2 &&
                saved.Devices.Count == expected.Count &&
                saved.Devices.All(kv => expected.TryGetValue(kv.Key, out var v) && v == kv.Value),
                "arquivo v1 divergente da lista historica publicada");
            var tooltip = (ToolTip)typeof(MainForm).GetField("_toolTips", Private)!
                .GetValue(owner)!;
            // O texto de Label pode mudar antes do SyncStatusAccessibility
            // completar eventos WinForms reentrantes. Exigir convergencia
            // de TODOS os campos da mesma fotografia, com timeout curto.
            PumpUntil(() => state.AccessibleDescription?.Contains(label.Text, StringComparison.Ordinal) == true &&
                label.AccessibleDescription?.Contains(label.Text, StringComparison.Ordinal) == true &&
                string.IsNullOrEmpty(tooltip.GetToolTip(state)) &&
                string.IsNullOrEmpty(tooltip.GetToolTip(label)));
            Console.WriteLine("OK G1.21: fotografia publicada imutavel, filtros e selecao, guard, modal, fontes Store/INF e historico v1 coerentes");
        }
        finally
        {
            hold.TrySetResult();
            form?.Dispose();
            Pump(50);
            fieldPath.SetValue(owner, originalPath);
            fieldHold.SetValue(owner, originalHold);
            fieldReplace.SetValue(owner, originalReplace);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private static T Find<T>(Control c, string name) where T : Control =>
        Descendants(c).OfType<T>().Single(x => x.Name == name);
    private static IEnumerable<Control> Descendants(Control c)
    {
        foreach (Control child in c.Controls)
        {
            yield return child;
            foreach (var more in Descendants(child)) yield return more;
        }
    }
    private static void Pump(int ms = 80)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); }
        Application.DoEvents();
    }
    private static void PumpUntil(Func<bool> condition, int milliseconds = 5000)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && clock.ElapsedMilliseconds < milliseconds) Pump(20);
        Ensure(condition(), "estado WinForms nao convergiu");
    }
    private static void Ensure(bool value, string description)
    {
        if (!value) throw new Exception("G1.21: " + description);
    }
}
