using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// Diálogo WinForms REAL com inventários totalmente sintéticos.
// Sem chamadas WUA, SetupAPI, COM, WMI ou verificação de INF nativa.
internal static class DriverReadOnlyDialogIdentityTests
{
    private const string Title = "Drivers — detalhes e evidências";

    internal static void Run(MainForm owner)
    {
        var device = new DriverInventoryEntry(@"PCI\G114\A", "Dispositivo sintético A",
            "Fornecedor A", "1.0", "oem17.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1111&DEV_0001"],
            PackageEvidence = new("oem17.inf", DriverStoreState.Mapped)
        };
        var different = device with { InfName = "oem18.inf", DeviceName = "Mesmo DeviceId, outro INF",
            PackageEvidence = new("oem18.inf", DriverStoreState.Mapped) };
        var other = new DriverInventoryEntry(@"PCI\G114\B", "Dispositivo sintético B",
            "Fornecedor B", "2.0", "oem22.inf", true, true);

        var conflict = new DriverInventoryResult([device, different], false,
            "instância duplicada, INF conflitante");
        var identical = new DriverInventoryResult([device, device], false,
            "instância duplicada, evidência idêntica");
        var full = new DriverInventoryResult([device, other], false, "único");
        var partial = new DriverInventoryResult([device, different], true, "parcial");
        // Refiltro por nome mostra apenas uma das duas linhas, mas a guarda
        // deve usar o inventário completo e não essa lista visível.
        using (var visibleList = new DarkDriverList())
        {
            visibleList.SetItems(DriverListFilters.Apply(conflict.Items, "Todos", "Dispositivo sintético A"));
            if (visibleList.Items.Count != 1) throw new Exception("G1.14: filtro ocultou quantidade inesperada");
            typeof(DarkDriverList).GetMethod("OnKeyDown",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(
                    visibleList, [new KeyEventArgs(Keys.Down)]);
            if (!ReferenceEquals(visibleList.SelectedItem, device))
                throw new Exception("G1.14: seleção filtrada sintética incorreta.");
            Inspect(owner, conflict, visibleList.SelectedItem, "DeviceId duplicado",
                "já ambígua na abertura", true, true, expectedInf: false);
        }

        Inspect(owner, identical, device, "DeviceId duplicado",
            "já ambígua na abertura", true, true, expectedInf: false);
        Inspect(owner, partial, device, "DeviceId duplicado",
            "já ambígua na abertura", true, true, expectedInf: false);
        Inspect(owner, conflict, null, "ATENÇÃO: DeviceId",
            "DeviceId duplicado no inventário completo", false, true, expectedInf: false);
        Inspect(owner, full, device, "Dispositivo sintético A",
            "Consultas locais", false, false, expectedInf: true);
        Inspect(owner, full, null, "Selecione um dispositivo",
            "Sem dispositivo selecionado", false, false, expectedInf: false);

        // Com seleção única e outra identidade duplicada, bloquear WUA
        // sem impedir a mera leitura do relatório e INF desta seleção.
        var unrelated = new DriverInventoryResult([other, device, different], false,
            "único selecionado com outro DeviceId duplicado");
        Inspect(owner, unrelated, other, "Dispositivo sintético B",
            "DeviceId duplicado no inventário completo", false, true, expectedInf: false);
        Console.WriteLine("OK G1.14: duplicatas visíveis/ocultas, seleção, diálogo e INF/WUA conservadores");
    }

    private static void Inspect(MainForm owner, DriverInventoryResult inventory,
        DriverInventoryEntry? selected, string bodyFragment, string statusFragment,
        bool initialInvalid, bool expectOfficialDisabled, bool? expectedInf = null)
    {
        int starts = 0;
        int checks = 0;
        var guard = new DriverReportPublicationGuard(inventory, selected, 10);
        if (guard.IsCurrent(inventory, selected, 10) == initialInvalid)
            throw new Exception("G1.14: pré-condição de identidade/guard divergente.");
        Action? runOfficial = null, runInf = null;
        Action? invalidation = null;
        Exception? failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 90 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                var modal = Application.OpenForms.Cast<Form>().Single(f => f.Text == Title);
                var controls = Descendants(modal).ToArray();
                var report = controls.OfType<DarkLogView>().Single(x => x.Name == "DriverReadOnlyReportText");
                var status = controls.OfType<Label>().Single(x => x.Name == "DriverReadOnlyStatus");
                var official = controls.OfType<Button>().Single(x => x.Name == "DriverOfficialReadOnly");
                var inf = controls.OfType<Button>().Single(x => x.Name == "DriverVerifyInfReadOnly");
                var close = controls.OfType<Button>().Single(x => x.Text == "Fechar");
                if (runOfficial is null || runInf is null ||
                    !report.RenderedText.Contains(bodyFragment, StringComparison.Ordinal) ||
                    !status.Text.Contains(statusFragment, StringComparison.Ordinal) ||
                    status.AccessibleDescription != status.Text || !close.Enabled ||
                    official.Enabled == expectOfficialDisabled)
                    throw new Exception("G1.14: estado visual/acessível divergente: " +
                        status.Text + " | " + report.RenderedText.Substring(0, Math.Min(150, report.RenderedText.Length)));
                if (expectedInf.HasValue && inf.Enabled != expectedInf.Value)
                    throw new Exception("G1.14: elegibilidade INF incorreta.");
                if (inventory.Items.GroupBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase)
                        .Any(g => g.Count() > 1) && selected is null &&
                    (!report.RenderedText.Contains("Consulta ao Windows Update indisponível",
                        StringComparison.Ordinal) ||
                     report.RenderedText.Contains("Consulta ao Windows Update ainda não solicitada",
                        StringComparison.Ordinal)))
                    throw new Exception("G1.14: relatório oferece WUA mesmo com identidade global repetida.");
                if (initialInvalid && (official.Enabled || inf.Enabled ||
                    !report.RenderedText.Contains("nenhum resultado", StringComparison.OrdinalIgnoreCase)))
                    throw new Exception("G1.14: fonte inicialmente ambígua ainda autoriza ações.");

                foreach (var size in new[] { new Size(500, 340), new Size(790, 510) })
                {
                    modal.Size = size;
                    modal.PerformLayout();
                    Application.DoEvents();
                    if (official.Enabled == expectOfficialDisabled ||
                        status.AccessibleDescription != status.Text ||
                        !modal.RectangleToScreen(modal.ClientRectangle).Contains(
                            close.RectangleToScreen(close.ClientRectangle)))
                        throw new Exception("G1.14: regressão de viewport ou acessibilidade.");
                    checks++;
                }

                // Para os casos bloqueados, o callback de fixture invoca
                // os MESMOS handlers da UI, sem efetuar consultas reais.
                if (initialInvalid || expectOfficialDisabled)
                    runOfficial();
                if (initialInvalid || expectedInf is false)
                    runInf();
                if (starts != 0)
                    throw new Exception("G1.14: provedor sintético acionado apesar de bloqueio.");
                modal.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
                foreach (Form form in Application.OpenForms.Cast<Form>()
                    .Where(f => f.Text == Title).ToArray()) form.Close();
            }
        };
        var method = typeof(MainForm).GetMethod("ShowDriverReadOnlyReport",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        timer.Start();
        method.Invoke(owner, [inventory, selected,
            (Func<bool>)(() => guard.IsCurrent(inventory, selected, 10)),
            (Func<CancellationToken, DriverOfficialResult, bool>)((token, result) =>
                guard.CanPublish(inventory, selected, 10, token, result)),
            (Func<CancellationToken, DriverInfVerificationResult, bool>)((token, result) =>
                guard.CanPublishInf(inventory, selected, 10, token, result)),
            (Action<Action?>)(action => invalidation = action),
            (Func<CancellationToken, Task<DriverOfficialResult>>)(_ =>
            {
                starts++;
                return Task.FromResult(new DriverOfficialResult([], 0, 0, false, "SINTÉTICO WUA"));
            }),
            (Func<CancellationToken, Task<DriverInfVerificationResult>>)(_ =>
            {
                starts++;
                return Task.FromResult(new DriverInfVerificationResult(DriverInfTrust.Rejected));
            }),
            (TimeSpan?)TimeSpan.FromSeconds(3),
            (Action<Action, Action>)((a, b) => { runOfficial = a; runInf = b; }), false]);
        timer.Stop();
        if (failure is not null) throw failure;
        if (checks != 2 || invalidation is not null)
            throw new Exception("G1.14: viewports ou assinatura de invalidação pendentes.");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }
}
