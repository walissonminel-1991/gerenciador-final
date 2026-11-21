using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// G1.16: report e diálogo WinForms REAL; todas as evidências e provedores
// são sintéticos. Não lê setupapi.dev.log, WUA, WMI nem arquivos pessoais.
internal static class DriverReadOnlyDialogHistoryTests
{
    private const string Title = "Drivers — investigar sem alterações";

    internal static void Run(MainForm owner)
    {
        var device = new DriverInventoryEntry(@"PCI\G116\A", "Dispositivo sintético G1.16",
            "Fornecedor exemplo", "1.0", "oem16.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1616&DEV_0016"],
            PackageEvidence = new("oem16.inf", DriverStoreState.Mapped)
        };
        var published = new DriverPublishedPackage(
            new("oem16.inf", DriverStoreState.Mapped), 0, false);
        var noHistory = new DriverInventoryResult([device], false, "fotografia sem fonte");
        var unavailable = noHistory with
        {
            WindowsHistory = new DriverWindowsHistoryResult([], true, true, false, 0)
        };
        var partial = noHistory with
        {
            WindowsHistory = new DriverWindowsHistoryResult([], true, false, true, 2),
            PublishedPackages = new DriverPublishedAuditResult([published], true, false)
        };
        var emptyPartial = noHistory with
        {
            WindowsHistory = new DriverWindowsHistoryResult([], true, false, false, 1),
            PublishedPackages = new DriverPublishedAuditResult([], true, false)
        };
        var complete = noHistory with
        {
            WindowsHistory = new DriverWindowsHistoryResult([], false, false, false, 0),
            PublishedPackages = new DriverPublishedAuditResult([published], false, true)
        };

        CheckReport(noHistory,
            ["Histórico deste dispositivo não consultado",
             "Enumeração de INFs publicados não consultada"],
            ["Nenhum vínculo histórico explícito encontrado no trecho lido",
             "Log Windows"]);
        CheckReport(unavailable,
            ["Histórico deste dispositivo indisponível", "Histórico Windows indisponível"],
            ["Nenhum vínculo histórico explícito encontrado no trecho lido",
             "Log Windows"]);
        CheckReport(partial,
            ["Histórico Windows incompleto", "trecho parcial consultado",
             "Relação de INFs publicados parcial ou inconclusiva",
             "oem16.inf: Sem dispositivo observado nesta consulta; preservar"],
            ["Nenhum vínculo histórico explícito encontrado no trecho lido"]);
        CheckReport(emptyPartial,
            ["Nenhum INF retornado na parcela consultada",
             "Histórico Windows incompleto", "trecho parcial consultado"], []);
        CheckReport(complete,
            ["Nenhum vínculo histórico explícito encontrado no trecho lido",
             "INFs publicados observados nesta consulta: 1"], 
            ["Relação de INFs publicados parcial ou inconclusiva"]);

        // A origem indisponível não deve promover eventos injetados na linha.
        var stale = device with
        {
            WindowsEvents = [new(device.DeviceId, device.InfName,
                new DateTime(2026, 11, 14, 12, 30, 0), DriverInstallOutcome.Succeeded)]
        };
        CheckReport(unavailable with { Items = [stale] },
            ["Histórico deste dispositivo indisponível"], ["Log Windows", "sucesso registrado"]);

        // Sem gravar arquivo: o mecanismo histórico existente recusa
        // snapshot parcial. Não alterar o histórico v1 de usuário.
        try
        {
            _ = DriverHistory.Snapshot(partial with { Partial = true });
            throw new Exception("G1.16: inventário parcial não bloqueou snapshot histórico.");
        }
        catch (InvalidDataException) { }

        Inspect(owner, partial, device, "Relação de INFs publicados parcial",
            "Histórico Windows incompleto");
        Inspect(owner, unavailable, device, "Histórico deste dispositivo indisponível",
            "Enumeração de INFs publicados não consultada");

        // Revalidar a prova imutável: uma geração antiga não autoriza
        // publicação mesmo que o relatório consultivo ainda seja legível.
        var guard = new DriverReportPublicationGuard(partial, device, 16);
        var rejected = new DriverInfVerificationResult(DriverInfTrust.Rejected);
        if (!guard.IsCurrent(partial, device, 16) ||
            guard.CanPublishInf(partial, device, 17, CancellationToken.None, rejected))
            throw new Exception("G1.16: geração obsoleta autorizou evidência INF.");
        Console.WriteLine("OK G1.16: histórico indisponível/parcial, INFs publicados parciais, modal acessível e geração obsoleta");
    }

    private static void CheckReport(DriverInventoryResult inventory,
        string[] required, string[] forbidden)
    {
        string report = DriverReadOnlyReport.Format(inventory, inventory.Items[0]);
        foreach (var phrase in required)
            if (!report.Contains(phrase, StringComparison.Ordinal))
                throw new Exception("G1.16: aviso consultivo ausente: " + phrase);
        foreach (var phrase in forbidden)
            if (report.Contains(phrase, StringComparison.Ordinal))
                throw new Exception("G1.16: relatório promove informação inconclusiva: " + phrase);
    }

    private static void Inspect(MainForm owner, DriverInventoryResult inventory,
        DriverInventoryEntry selected, string requiredReport, string requiredSecond)
    {
        var guard = new DriverReportPublicationGuard(inventory, selected, 16);
        if (!guard.IsCurrent(inventory, selected, 16))
            throw new Exception("G1.16: fixture não possui guard válido.");
        Action? observed = null, startOfficial = null, startInf = null;
        Exception? failure = null;
        int checks = 0, starts = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 95 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                var modal = Application.OpenForms.Cast<Form>().Single(f => f.Text == Title);
                var controls = Descendants(modal).ToArray();
                var body = controls.OfType<DarkLogView>().Single(x => x.Name == "DriverReadOnlyReportText");
                var status = controls.OfType<Label>().Single(x => x.Name == "DriverReadOnlyStatus");
                var official = controls.OfType<Button>().Single(x => x.Name == "DriverOfficialReadOnly");
                var verify = controls.OfType<Button>().Single(x => x.Name == "DriverVerifyInfReadOnly");
                var close = controls.OfType<Button>().Single(x => x.Text == "Fechar");
                if (startOfficial is null || startInf is null || !close.Enabled ||
                    !official.Enabled || !verify.Enabled ||
                    !body.RenderedText.Contains(requiredReport, StringComparison.Ordinal) ||
                    !body.RenderedText.Contains(requiredSecond, StringComparison.Ordinal) ||
                    status.AccessibleDescription != status.Text)
                    throw new Exception("G1.16: modal com histórico/INFs parciais inconsistente.");
                foreach (var size in new[] { new Size(500, 340), new Size(790, 510) })
                {
                    modal.Size = size;
                    modal.PerformLayout();
                    Application.DoEvents();
                    if (status.AccessibleDescription != status.Text ||
                        !modal.RectangleToScreen(modal.ClientRectangle).Contains(
                            close.RectangleToScreen(close.ClientRectangle)))
                        throw new Exception("G1.16: viewport e descrição acessível divergentes.");
                    checks++;
                }
                // Nenhuma chamada nativa. Os testes temporais G1.10 preservados
                // cobrem handlers, cancelamento, timeout, retorno tardio e reabertura.
                if (starts != 0)
                    throw new Exception("G1.16: fixture disparou motor externo.");
                modal.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
                foreach (var x in Application.OpenForms.Cast<Form>()
                    .Where(x => x.Text == Title).ToArray()) x.Close();
            }
        };
        timer.Start();
        var method = typeof(MainForm).GetMethod("ShowDriverReadOnlyReport",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(owner, [inventory, selected,
            (Func<bool>)(() => guard.IsCurrent(inventory, selected, 16)),
            (Func<CancellationToken, DriverOfficialResult, bool>)((token, result) =>
                guard.CanPublish(inventory, selected, 16, token, result)),
            (Func<CancellationToken, DriverInfVerificationResult, bool>)((token, result) =>
                guard.CanPublishInf(inventory, selected, 16, token, result)),
            (Action<Action?>)(next => observed = next),
            (Func<CancellationToken, Task<DriverOfficialResult>>)(_ =>
            { starts++; return Task.FromResult(new DriverOfficialResult([], 0, 0, true, "synthetic")); }),
            (Func<CancellationToken, Task<DriverInfVerificationResult>>)(_ =>
            { starts++; return Task.FromResult(new DriverInfVerificationResult(DriverInfTrust.Rejected)); }),
            (TimeSpan?)TimeSpan.FromSeconds(3),
            (Action<Action, Action>)((official, inf) =>
            { startOfficial = official; startInf = inf; }), false]);
        timer.Stop();
        if (failure is not null) throw failure;
        if (checks != 2 || observed is not null)
            throw new Exception("G1.16: modal não mediu viewports ou reteve observador.");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var desc in Descendants(child)) yield return desc;
        }
    }
}
