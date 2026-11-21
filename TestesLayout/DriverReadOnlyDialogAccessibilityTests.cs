using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// Janela WinForms REAL; dados e retornos inteiramente sintéticos, sem WUA/INF nativo.
internal static class DriverReadOnlyDialogAccessibilityTests
{
    private const string Title = "Drivers — detalhes e evidências";

    internal static void Run(MainForm owner)
    {
        var device = new DriverInventoryEntry(@"PCI\TEST\G111", "Dispositivo sintético G1.11",
            "Fornecedor fictício", "1.0", "oem17.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1234&DEV_5678"],
            PackageEvidence = new("oem17.inf", DriverStoreState.Mapped)
        };
        var full = new DriverInventoryResult([device], false, "completo G1.11");
        var partial = new DriverInventoryResult([device], true, "parcial G1.11");
        var empty = new DriverInventoryResult([], false, "vazio G1.11");

        Inspect(owner, full, device, true, true, "Consultas locais", "Dispositivo sintético G1.11");
        Inspect(owner, full, null, true, false, "Sem dispositivo selecionado",
            "selecione um dispositivo com INF mapeado");
        Inspect(owner, empty, null, false, false, "Inventário vazio", "Dispositivos/associações: 0");
        Inspect(owner, partial, null, false, false, "Inventário parcial", "inventário parcial: sim");
        Inspect(owner, partial, device, false, true, "Inventário parcial", "Dispositivo sintético G1.11");
        Console.WriteLine("OK G1.11: seleção, ausência, inventário vazio/parcial e elegibilidade inicial em WinForms real");

        // Callback de geração invalida texto, status acessível e botões sem outro motor.
        {
            long generation = 17;
            Action? notify = null;
            int checkedSizes = 0;
            int started = 0;
            RunModal(owner, full, device, () => generation,
                callback => notify = callback,
                _ => { started++; return Task.FromResult(
                    new DriverOfficialResult([], 0, 0, false, "sintético")); },
                _ => Task.FromResult(new DriverInfVerificationResult(DriverInfTrust.Rejected,
                    "", "", "")),
                (dialog, runOfficial, runInf) =>
                {
                    var (body, status, official, verify, close) = Controls(dialog);
                    generation++;
                    notify?.Invoke();
                    foreach (var size in new[] { new Size(500, 340), new Size(790, 510) })
                    {
                        dialog.Size = size;
                        dialog.PerformLayout();
                        Application.DoEvents();
                        (body, status, official, verify, close) = Controls(dialog);
                        if (official.Enabled || verify.Enabled || !close.Enabled ||
                            !body.RenderedText.Contains("Inventário ou seleção alterados") ||
                            body.RenderedText.Contains(device.DeviceId) ||
                            !status.Text.Contains("reabra") || status.AccessibleDescription != status.Text)
                            throw new Exception("G1.11: aviso obsoleto inacessível ou ações ativas.");
                        checkedSizes++;
                    }
                    runOfficial(); // handler real também deve respeitar o estado indisponível
                    runInf();
                    if (started != 0)
                        throw new Exception("G1.11: ação inválida iniciou consulta sintética.");
                });
            if (notify is not null || checkedSizes != 2)
                throw new Exception("G1.11: observador/viewport de invalidação incorreto.");
            Console.WriteLine("OK G1.11: geração obsoleta elimina conteúdo, preserva status acessível e impede comandos");
        }
    }

    private static void Inspect(MainForm owner, DriverInventoryResult inventory,
        DriverInventoryEntry? selected, bool expectedOfficial, bool expectedInf,
        string statusFragment, string bodyFragment)
    {
        int count = 0;
        int started = 0;
        RunModal(owner, inventory, selected, () => 1, _ => { },
            _ => { started++; return Task.FromResult(
                new DriverOfficialResult([], 0, 0, false, "sintético")); },
            _ => { started++; return Task.FromResult(new DriverInfVerificationResult(
                DriverInfTrust.Rejected, "", "", "")); },
            (dialog, runOfficial, runInf) =>
            {
                var (body, status, official, verify, close) = Controls(dialog);
                if (official.Enabled != expectedOfficial || verify.Enabled != expectedInf ||
                    !close.Enabled || !status.Text.Contains(statusFragment) ||
                    !body.RenderedText.Contains(bodyFragment))
                    throw new Exception("G1.11: estado de abertura incorreto (" + statusFragment + ").");
                if (!ReferenceEquals(dialog.CancelButton, close) ||
                    close.DialogResult != DialogResult.Cancel ||
                    !official.TabStop || !verify.TabStop || !close.TabStop ||
                    string.IsNullOrWhiteSpace(official.AccessibleName) ||
                    string.IsNullOrWhiteSpace(verify.AccessibleName) ||
                    string.IsNullOrWhiteSpace(close.AccessibleName) ||
                    status.AccessibleName != "Estado da investigação de drivers")
                    throw new Exception("G1.11: fechamento por Esc, tabulação ou nomes acessíveis ausentes.");
                // Os handlers injetados são os mesmos do Click; botões desabilitados
                // não podem chamar o provedor mesmo via acionador sintético.
                if (!expectedOfficial) runOfficial();
                if (!expectedInf) runInf();
                if (started != 0) throw new Exception("G1.11: ação desabilitada iniciou leitura.");
                foreach (var size in new[] { new Size(500, 340), new Size(790, 510) })
                {
                    dialog.Size = size;
                    dialog.PerformLayout();
                    Application.DoEvents();
                    (body, status, official, verify, close) = Controls(dialog);
                    var bounds = dialog.RectangleToScreen(dialog.ClientRectangle);
                    foreach (var button in new[] { official, verify, close })
                        if (!bounds.Contains(button.RectangleToScreen(button.ClientRectangle)) ||
                            button.Width < 75 || button.Height < 26)
                            throw new Exception("G1.11: botão fora da área visível em " + size);
                    if (!status.AutoEllipsis || status.AccessibleDescription != status.Text ||
                        string.IsNullOrWhiteSpace(status.AccessibleDescription) ||
                        !bounds.Contains(status.RectangleToScreen(status.ClientRectangle)) ||
                        body.RenderedText.Length == 0 || dialog.DeviceDpi <= 0)
                        throw new Exception("G1.11: aviso elidido sem alternativa acessível ou DPI inválido.");
                    count++;
                }
            });
        if (count != 2) throw new Exception("G1.11: viewports não inspecionados.");
    }

    private static void RunModal(MainForm owner, DriverInventoryResult inventory,
        DriverInventoryEntry? selected, Func<long> generation,
        Action<Action?> observe,
        Func<CancellationToken, Task<DriverOfficialResult>>? fakeOfficial,
        Func<CancellationToken, Task<DriverInfVerificationResult>>? fakeInf,
        Action<Form, Action, Action> inspect)
    {
        var method = typeof(MainForm).GetMethod("ShowDriverReadOnlyReport",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var guard = new DriverReportPublicationGuard(inventory, selected, generation());
        Action? officialAction = null, infAction = null;
        Exception? failure = null;
        int hits = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 90 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                var dialog = Application.OpenForms.Cast<Form>().Single(f => f.Text == Title);
                if (++hits != 1 || officialAction is null || infAction is null)
                    throw new Exception("G1.11: ações do diálogo indisponíveis.");
                inspect(dialog, officialAction, infAction);
                dialog.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
                foreach (Form form in Application.OpenForms.Cast<Form>().Where(f => f.Text == Title).ToArray())
                    form.Close();
            }
        };
        timer.Start();
        method.Invoke(owner, [inventory, selected,
            (Func<bool>)(() => guard.IsCurrent(inventory, selected, generation())),
            (Func<CancellationToken, DriverOfficialResult, bool>)((token, result) =>
                guard.CanPublish(inventory, selected, generation(), token, result)),
            (Func<CancellationToken, DriverInfVerificationResult, bool>)((token, result) =>
                guard.CanPublishInf(inventory, selected, generation(), token, result)),
            observe, fakeOfficial, fakeInf, (TimeSpan?)TimeSpan.FromSeconds(5),
            (Action<Action, Action>)((runOfficial, runInf) =>
            { officialAction = runOfficial; infAction = runInf; }), false]);
        timer.Stop();
        if (failure is not null) throw failure;
        if (hits != 1) throw new Exception("G1.11: não houve inspeção WinForms.");
    }

    private static (DarkLogView body, Label status, Button official,
        Button verify, Button close) Controls(Form dialog)
    {
        var children = Descendants(dialog).ToArray();
        return (children.OfType<DarkLogView>().Single(x => x.Name == "DriverReadOnlyReportText"),
            children.OfType<Label>().Single(x => x.Name == "DriverReadOnlyStatus"),
            children.OfType<Button>().Single(x => x.Name == "DriverOfficialReadOnly"),
            children.OfType<Button>().Single(x => x.Name == "DriverVerifyInfReadOnly"),
            children.OfType<Button>().Single(x => x.Text == "Fechar"));
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
