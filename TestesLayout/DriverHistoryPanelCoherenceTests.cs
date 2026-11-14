using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// G1.17: painel WinForms real + histórico v1 num diretório descartável.
// Provedores de inventário sintéticos: sem WMI, WUA, INF, SetupAPI,
// consulta ao Windows ou escrita em histórico pessoal do runner.
internal static class DriverHistoryPanelCoherenceTests
{
    internal static void Run(MainForm owner)
    {
        var a = new DriverInventoryEntry(@"PCI\G117\A", "Dispositivo A sintético",
            "Fornecedor exemplo", "1.0", "oem17.inf", true, true);
        var b = new DriverInventoryEntry(@"PCI\G117\B", "Dispositivo B sintético",
            "Fornecedor exemplo", "2.0", "oem18.inf", true, true);
        var unassociated = new DriverInventoryEntry(@"PCI\G117\NOASSOC",
            "Dispositivo sem associação sintético", "", "", "", null, null)
        { WithoutSignedDriverAssociation = true };
        var first = new DriverInventoryResult([a, b], false, "fotografia 1");
        var partial = new DriverInventoryResult([a], true, "fotografia parcial");
        var completeUnassociated = new DriverInventoryResult([unassociated],
            false, "nenhuma associação WMI");

        // Persistência real ocorre SOMENTE sob diretório novo e isolado
        // do runner. Nunca consultar DriverHistory.DefaultPath.
        var root = Path.Combine(Path.GetTempPath(),
            "GF-G117-TESTES-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "drivers-v1.json");
        try
        {
            var now = new DateTimeOffset(2026, 11, 14, 14, 0, 0, TimeSpan.Zero);
            var baseline = DriverHistory.Record(path, first, now);
            if (!baseline.Saved || !baseline.Baseline || !File.Exists(path))
                throw new Exception("G1.17: baseline histórico sintético não foi gravado.");
            byte[] before = File.ReadAllBytes(path);
            if (System.Text.Encoding.UTF8.GetString(before).Contains(a.DeviceId,
                StringComparison.OrdinalIgnoreCase))
                throw new Exception("G1.17: identidade PnP foi persistida sem hash.");
            var noWmi = DriverHistory.Record(path, completeUnassociated,
                now.AddSeconds(1));
            if (noWmi.Saved || !noWmi.Message.Contains(
                    "nenhuma associação de driver WMI observada", StringComparison.Ordinal) ||
                !File.ReadAllBytes(path).SequenceEqual(before))
                throw new Exception("G1.17: zero associações WMI simulou corrupção ou alterou baseline.");
            var skipped = DriverHistory.Record(path, partial, now.AddSeconds(2));
            if (skipped.Saved || !skipped.Message.Contains("parcial", StringComparison.Ordinal) ||
                !File.ReadAllBytes(path).SequenceEqual(before))
                throw new Exception("G1.17: snapshot parcial alterou o histórico anterior.");
            var reordered = DriverHistory.Record(path,
                new([b, a], false, "mesmos dispositivos, ordem diferente"),
                now.AddSeconds(3));
            if (!reordered.Saved || reordered.Added != 0 ||
                reordered.Missing != 0 || reordered.Changed != 0)
                throw new Exception("G1.17: ordem das linhas alterou comparação imutável.");
            var changed = DriverHistory.Record(path,
                new([a with { Version = "3.0" }], false, "nova observação"),
                now.AddSeconds(4));
            if (!changed.Saved || changed.Missing != 1 || changed.Changed != 1 ||
                changed.Added != 0 || !changed.Message.Contains("não observados"))
                throw new Exception("G1.17: ausência WMI foi classificada indevidamente.");
            var last = File.ReadAllBytes(path);
            if (DriverHistory.Record(path, first, now.AddDays(-1)).Saved ||
                !File.ReadAllBytes(path).SequenceEqual(last))
                throw new Exception("G1.17: relógio regressivo substituiu o histórico.");
        }
        finally
        {
            // Apagar SOMENTE o diretório temporário criado por esta fixture.
            Directory.Delete(root, recursive: true);
        }

        // Testar a MESMA ação de consulta do painel, mas sem abrir o
        // histórico pessoal ou consultar o WMI.
        int calls = 0;
        var pending = new TaskCompletionSource<DriverInventoryResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        DriverInventoryResult Read(int index) => index switch
        {
            1 => first,
            3 => partial,
            _ => completeUnassociated
        };
        Action? refreshAction = null;
        var method = typeof(MainForm).GetMethod("BuildDriversPlaceholder",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var page = (Control)method.Invoke(owner, [
            (Func<CancellationToken, Task<DriverInventoryResult>>)(_ =>
            {
                calls++;
                return calls == 2 ? pending.Task : Task.FromResult(Read(calls));
            }),
            (TimeSpan?)TimeSpan.FromSeconds(3),
            (Action<Action>)(callback => refreshAction = callback),
            (Action<Action<string>>)(_ => { })
        ])!;
        using var form = new Form { Text = "Drivers G1.17 sintético",
            ClientSize = new Size(900, 560), ShowInTaskbar = false };
        page.Dock = DockStyle.Fill;
        form.Controls.Add(page);
        form.Show(owner);
        Pump();
        var historyLabel = Find<Label>(page, "DriverHistoryStatus");
        var status = Find<Label>(page, "DriverInventoryStatus");
        var list = Find<DarkDriverList>(page, "DriverInventoryList");
        var refresh = refreshAction ?? throw new Exception("G1.17: handler do painel indisponível.");

        refresh();
        Pump();
        if (calls != 1 || list.Items.Count != 2 ||
            !historyLabel.Text.Contains("Histórico sintético desta consulta",
                StringComparison.Ordinal) ||
            !status.AccessibleDescription!.Contains(historyLabel.Text, StringComparison.Ordinal))
            throw new Exception("G1.17: primeira fotografia não atualizou histórico acessível.");

        // Simular comparação anterior já apresentada no painel.
        const string earlier = "Desde a consulta anterior: 2 não observados (fotografia antiga).";
        historyLabel.Text = earlier;
        historyLabel.AccessibleDescription = earlier;
        refresh(); // segunda leitura mantém fotografia anterior até concluir
        Pump(25);
        if (calls != 2 || !historyLabel.Text.Equals(earlier, StringComparison.Ordinal))
            throw new Exception("G1.17: leitura pendente substituiu histórico válido anterior.");
        refresh(); // cancelar a espera, sem concluir trabalho nativo
        Pump(60);
        pending.TrySetResult(new([a], false, "resultado tardio ignorado"));
        Pump();
        if (list.Items.Count != 2 || !historyLabel.Text.Equals(earlier, StringComparison.Ordinal))
            throw new Exception("G1.17: cancelamento publicou inventário ou histórico tardio.");

        refresh();
        Pump();
        if (calls != 3 || list.Items.Count != 1 ||
            !historyLabel.Text.Contains("Histórico sintético desta consulta", StringComparison.Ordinal) ||
            historyLabel.Text.Contains("não observados") ||
            !status.AccessibleDescription!.Contains(historyLabel.Text, StringComparison.Ordinal) ||
            status.AccessibleDescription.Contains(earlier, StringComparison.Ordinal))
            throw new Exception("G1.17: nova fotografia exibiu comparação da fotografia anterior.");

        foreach (var size in new[] { new Size(500, 340), new Size(790, 510),
            new Size(1200, 720), new Size(1450, 820) })
        {
            page.ClientSize = size;
            Pump(35);
            if (!status.AccessibleDescription!.Contains(historyLabel.Text,
                    StringComparison.Ordinal))
                throw new Exception("G1.17: viewport ocultou histórico acessível.");
        }

        refresh();
        Pump();
        if (calls != 4 || list.Items.Count != 1 ||
            !historyLabel.Text.Contains("Histórico sintético desta consulta") ||
            !status.AccessibleDescription!.Contains(historyLabel.Text))
            throw new Exception("G1.17: dispositivo sem associação deixou rastro de histórico antigo.");

        Console.WriteLine("OK G1.17: histórico v1 preservado, ausência WMI, snapshot parcial, cancelamento e rodapé WinForms coerente");
    }

    private static void Pump(int milliseconds = 80)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        Application.DoEvents();
    }

    private static T Find<T>(Control root, string name) where T : Control =>
        Descendants(root).OfType<T>().Single(x => x.Name == name);
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var d in Descendants(c)) yield return d;
        }
    }
}
