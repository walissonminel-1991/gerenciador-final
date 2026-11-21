using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// G1.19: codigo real de Snapshot/Record/relatorio/painel; entradas sinteticas.
// Persistencia EXCLUSIVA em pasta temporaria nova, nunca DefaultPath.
internal static class DriverG119HistoryIdentityTests
{
    internal static void Run(MainForm owner)
    {
        var a = new DriverInventoryEntry(@"PCI\G119\A", "Dispositivo A sintético",
            "Fornecedor A", "1.0", "oem119.inf", true, true);
        var b = a with { DeviceId = @"PCI\G119\B", DeviceName = "Dispositivo B sintético" };
        var noAssoc = a with { WithoutSignedDriverAssociation = true, InfName = "",
            Provider = "", Version = "", Signed = null };
        DriverInventoryResult Complete(params DriverInventoryEntry[] entries) =>
            new(entries, false, "inventário sintético G1.19");
        var start = new DateTimeOffset(2026, 11, 14, 18, 0, 0, TimeSpan.Zero);
        var directory = Path.Combine(Path.GetTempPath(), "GF-G119-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "drivers-v1.json");
        try
        {
            var baseline = DriverHistory.Record(path, Complete(a, b), start);
            Ensure(baseline.Saved && baseline.Baseline, "baseline não estabelecida");
            byte[] original = File.ReadAllBytes(path);
            var rejected = new (string Name, DriverInventoryResult Inventory)[]
            {
                ("DeviceId identico", Complete(a, a)),
                ("INF divergente", Complete(a, a with { InfName = "oem999.inf" })),
                ("versao divergente", Complete(a, a with { Version = "9.0" })),
                ("fornecedor divergente", Complete(a, a with { Provider = "Outro fornecedor" })),
                ("assinatura divergente", Complete(a, a with { Signed = false })),
                ("caixa divergente", Complete(a, a with { DeviceId = @"pci\g119\a" })),
                ("espacos equivalentes", Complete(a, a with { DeviceId = " " + a.DeviceId + " " })),
                ("associado e sem WMI", Complete(a, noAssoc)),
                ("identidade vazia", Complete(a with { DeviceId = "" })),
                ("identidade sem barra", Complete(a with { DeviceId = "G119" })),
                ("identidade com controle", Complete(a with { DeviceId = @"PCI\G119\A" + "\n" })),
                ("identidade wildcard", Complete(a with { DeviceId = @"PCI\G119\*" })),
                ("snapshot parcial", new([a], true, "parcial sintético")),
                ("snapshot vazio", Complete()),
                ("contagem WMI bruta discordante", Complete(a) with { SignedRows = 2 })
            };
            for (int i = 0; i < rejected.Length; i++)
            {
                var attempt = DriverHistory.Record(path, rejected[i].Inventory, start.AddSeconds(i + 1));
                Ensure(!attempt.Saved && File.ReadAllBytes(path).SequenceEqual(original),
                    "rejeição byte a byte: " + rejected[i].Name);
            }
            var same = DriverHistory.Record(path, Complete(b, a), start.AddMinutes(2));
            Ensure(same.Saved && !same.Baseline && same.Added == 0 &&
                same.Missing == 0 && same.Changed == 0,
                "reordenacao inequívoca criou alteração");
            var changed = DriverHistory.Record(path,
                Complete(a with { Version = "2.0" }, b), start.AddMinutes(3));
            Ensure(changed.Saved && changed.Changed == 1 && changed.Missing == 0 &&
                changed.Added == 0 && changed.Message.Contains("não comprova remoção", StringComparison.OrdinalIgnoreCase),
                "alteracao real não foi diferenciada de remoção");
            byte[] after = File.ReadAllBytes(path);
            Ensure(!DriverHistory.Record(path, Complete(a, b), start).Saved &&
                File.ReadAllBytes(path).SequenceEqual(after),
                "retorno temporal obsoleto substituiu v1");
        }
        finally { Directory.Delete(directory, recursive: true); }

        var ambiguous = Complete(a, a with { InfName = "oem777.inf" });
        string report = DriverReadOnlyReport.Format(ambiguous, a);
        Ensure(report.Contains("DeviceId ausente ou duplicado", StringComparison.Ordinal) &&
            report.Contains("Consulta ao Windows Update indisponível", StringComparison.Ordinal) &&
            report.Contains("não comprova", StringComparison.OrdinalIgnoreCase),
            "relatorio promoveu duplicata a vínculo oficial");
        var guard = new DriverReportPublicationGuard(ambiguous, a, 12);
        Ensure(!guard.IsCurrent(ambiguous, a, 12),
            "guard G1.9 aceitou DeviceId duplicado");

        var correlated = DriverInventory.Correlate([a, a],
            new Dictionary<string, bool?>());
        Ensure(correlated.Count == 2, "captura eliminou duplicatas idênticas antes do guard");

        Action? refresh = null;
        Action<string>? filter = null;
        var build = typeof(MainForm).GetMethod("BuildDriversPlaceholder",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var page = (Control)build.Invoke(owner, [
            (Func<CancellationToken, Task<DriverInventoryResult>>)(_ =>
                Task.FromResult(ambiguous)),
            (TimeSpan?)TimeSpan.FromSeconds(3),
            (Action<Action>)(callback => refresh = callback),
            (Action<Action<string>>)(callback => filter = callback)])!;
        using var form = new Form { Text = "G1.19 sintético",
            ClientSize = new Size(900, 560), ShowInTaskbar = false };
        page.Dock = DockStyle.Fill;
        form.Controls.Add(page);
        form.Show(owner);
        Pump();
        refresh!();
        Pump();
        var status = Find<Label>(page, "DriverInventoryStatus");
        var history = Find<Label>(page, "DriverHistoryStatus");
        var list = Find<DarkDriverList>(page, "DriverInventoryList");
        var search = Find<TextBox>(page, "DriverInventorySearch");
        Ensure(list.Items.Count == 2 &&
            history.Text.Contains("não gravado nem comparado", StringComparison.Ordinal) &&
            status.AccessibleDescription?.Contains(history.Text, StringComparison.Ordinal) == true &&
            history.AccessibleDescription == history.Text,
            "painel publicado anuncia fotografia historica antiga");
        var tooltipField = typeof(MainForm).GetField("_toolTips",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var tips = tooltipField?.GetValue(owner) as ToolTip
            ?? throw new Exception("G1.19: tooltips do painel não encontrados");
        Ensure(tips.GetToolTip(status).Contains(history.Text, StringComparison.Ordinal) &&
            tips.GetToolTip(history).Contains(history.Text, StringComparison.Ordinal),
            "tooltip diverge do texto e descrição acessivel");

        foreach (var size in new[] { new Size(500, 340), new Size(790, 510),
            new Size(1200, 720), new Size(1450, 820) })
        {
            page.ClientSize = size;
            Pump(40);
            Ensure(status.AccessibleDescription?.Contains(history.Text, StringComparison.Ordinal) == true &&
                tips.GetToolTip(status).Contains(history.Text, StringComparison.Ordinal),
                "viewport perdeu histórico acessivel em " + size);
        }
        list.Focus();
        typeof(DarkDriverList).GetMethod("OnKeyDown",
            BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(list, [new KeyEventArgs(Keys.Down)]);
        Ensure(list.AccessibleDescription?.Contains("Associação de INF/WUA não é inequívoca", StringComparison.Ordinal) == true,
            "teclado/descricao nao sinalizam DeviceId duplicado");
        filter!("Problemas");
        Pump(40);
        filter!("Todos");
        search.Text = "Dispositivo A";
        Pump();
        Ensure(status.AccessibleDescription?.Contains(history.Text, StringComparison.Ordinal) == true,
            "filtro/pesquisa apagou historico do snapshot publicado");
        Console.WriteLine("OK G1.19: duplicatas WMI, identidade global, v1 intacto, relatorio, WinForms, tooltip e acessibilidade");
    }

    private static void Ensure(bool ok, string context)
    {
        if (!ok) throw new Exception("G1.19: " + context);
    }
    private static void Pump(int milliseconds = 90)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < milliseconds)
        { Application.DoEvents(); Thread.Sleep(10); }
        Application.DoEvents();
    }
    private static T Find<T>(Control root, string name) where T : Control =>
        Descendants(root).OfType<T>().Single(c => c.Name == name);
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control control in root.Controls)
        {
            yield return control;
            foreach (var child in Descendants(control)) yield return child;
        }
    }
}
