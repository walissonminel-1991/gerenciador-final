using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// Diálogo WinForms REAL com inventários, Store e publicados sintéticos.
// Nenhuma leitura WUA, WMI, SetupAPI, INF real, arquivos ou histórico do usuário.
internal static class DriverReadOnlyDialogEvidenceTests
{
    private const string ModalTitle = "Drivers — investigar sem alterações";

    internal static void Run(MainForm owner)
    {
        var mapped = new DriverStoreEvidence("oem31.inf", DriverStoreState.Mapped,
            @"C:\Windows\INF\oem31.inf",
            @"C:\Windows\System32\DriverStore\FileRepository\sintetico\oem31.inf");
        var device = new DriverInventoryEntry(@"PCI\G115\A", "Dispositivo G1.15",
            "Fornecedor sintético", "1.0", "oem31.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1111&DEV_0015"], PackageEvidence = mapped
        };
        DriverInventoryResult Snapshot(DriverInventoryEntry item, bool partial = false,
            DriverStoreAuditResult? store = null, DriverPublishedAuditResult? published = null) =>
            new([item], partial, "inventário G1.15 sintético")
            {
                Store = store, PublishedPackages = published
            };

        // Boa evidência: permite verificação local consultiva, mesmo se a
        // enumeração geral dos INFs publicados foi apenas parcial.
        var complete = Snapshot(device,
            store: new([mapped], false, 0),
            published: new([new(mapped, 1, false)], false, true));
        Inspect(owner, complete, device, true, true, "Ainda não solicitada", "Consultas locais");
        var partial = Snapshot(device, partial: true,
            store: new([mapped], true, 0),
            published: new([], true, false));
        Inspect(owner, partial, device, false, true,
            "Ainda não solicitada", "Inventário parcial",
            "Consulta ao Windows Update indisponível: inventário parcial");

        var evidenceCases = new[]
        {
            (Device: device with { PackageEvidence = (DriverStoreEvidence?)null },
                Fragment: "vínculo com Driver Store não informado"),
            (Device: device with { PackageEvidence = mapped with { State = DriverStoreState.NotConsulted } },
                Fragment: "Driver Store não consultado"),
            (Device: device with { PackageEvidence = mapped with { State = DriverStoreState.Unavailable } },
                Fragment: "consulta do vínculo Driver Store não disponível"),
            (Device: device with { PackageEvidence = mapped with { State = DriverStoreState.Inconsistent } },
                Fragment: "vínculo do INF com Driver Store inconsistente"),
            (Device: device with { WithoutSignedDriverAssociation = true },
                Fragment: "nenhuma associação de driver assinado observada")
        };
        foreach (var (item, fragment) in evidenceCases)
        {
            Inspect(owner, Snapshot(item), item, true, false, fragment, "Consultas locais");
            var forged = new DriverInfVerificationResult(DriverInfTrust.VerifiedInf,
                new string('A', 64), "catalogo.cat", "Fornecedor sintético");
            string formatted = DriverReadOnlyReport.Format(Snapshot(item), item, null, forged);
            if (formatted.Contains("Assinatura do INF confirmada pelo Windows", StringComparison.Ordinal) ||
                !formatted.Contains(fragment, StringComparison.Ordinal))
                throw new Exception("G1.15: resultado sintético injetado exibido em evidência indisponível.");
        }

        // Pacote Mapped com Store ou publicado contraditório: guard inválido
        // JÁ NA ABERTURA. A janela não inicia nenhuma consulta.
        var badStore = Snapshot(device,
            store: new([mapped with { StorePath = @"C:\Fake\outro.inf" }], false, 0),
            published: new([new(mapped, 1, false)], false, true));
        Inspect(owner, badStore, device, false, false, null,
            "Evidência local já incoerente", expectedInvalid: true);
        var completeAbsent = Snapshot(device,
            store: new([mapped], false, 0),
            published: new([], false, true));
        Inspect(owner, completeAbsent, device, false, false, null,
            "Evidência local já incoerente", expectedInvalid: true);
        var wrongInf = Snapshot(device with { InfName = "oem32.inf" });
        Inspect(owner, wrongInf, wrongInf.Items[0], false, false, null,
            "Evidência local já incoerente", expectedInvalid: true);

        var empty = new DriverInventoryResult([], false, "vazio sintético");
        Inspect(owner, empty, null, false, false, "Indisponível: selecione um dispositivo",
            "Inventário vazio", "Consulta ao Windows Update indisponível: inventário vazio");
        Inspect(owner, complete, null, true, false,
            "Indisponível: selecione um dispositivo", "Sem dispositivo selecionado");

        Console.WriteLine("OK G1.15: Store Mapped/NotConsulted/Unavailable/Inconsistent, parcial/vazio, guard e acessibilidade WinForms");
    }

    private static void Inspect(MainForm owner, DriverInventoryResult inventory,
        DriverInventoryEntry? selection, bool expectOfficial, bool expectInf,
        string? reportInfFragment, string statusFragment,
        string? reportWuaFragment = null, bool expectedInvalid = false)
    {
        var guard = new DriverReportPublicationGuard(inventory, selection, 15);
        if (guard.IsCurrent(inventory, selection, 15) == expectedInvalid)
            throw new Exception("G1.15: pré-condição do guard não reproduzida: " + statusFragment);
        int starts = 0, measured = 0;
        Exception? failure = null;
        Action? officialAction = null, infAction = null, observer = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 85 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                var dialog = Application.OpenForms.Cast<Form>().Single(f => f.Text == ModalTitle);
                var children = Descendants(dialog).ToArray();
                var body = children.OfType<DarkLogView>().Single(x => x.Name == "DriverReadOnlyReportText");
                var state = children.OfType<Label>().Single(x => x.Name == "DriverReadOnlyStatus");
                var official = children.OfType<Button>().Single(x => x.Name == "DriverOfficialReadOnly");
                var inf = children.OfType<Button>().Single(x => x.Name == "DriverVerifyInfReadOnly");
                var close = children.OfType<Button>().Single(x => x.Text == "Fechar");
                if (official.Enabled != expectOfficial || inf.Enabled != expectInf ||
                    !state.Text.Contains(statusFragment, StringComparison.Ordinal) ||
                    state.AccessibleDescription != state.Text || !close.Enabled ||
                    !ReferenceEquals(dialog.CancelButton, close) ||
                    officialAction is null || infAction is null)
                    throw new Exception("G1.15: primeiro estado incoerente: " + state.Text +
                        "; official=" + official.Enabled + "; INF=" + inf.Enabled);
                if (!expectInf && !expectedInvalid &&
                    inf.AccessibleDescription?.Contains(DriverReadOnlyReport.InfAvailability(selection),
                        StringComparison.Ordinal) != true)
                    throw new Exception("G1.15: justificativa do botão INF inacessível.");
                if (expectedInvalid && (!body.RenderedText.Contains("nenhuma consulta",
                    StringComparison.OrdinalIgnoreCase) || !inf.AccessibleDescription!.Contains("incoerente")))
                    throw new Exception("G1.15: prova incoerente foi apresentada.");
                if (!expectedInvalid && reportInfFragment is not null &&
                    !body.RenderedText.Contains(reportInfFragment, StringComparison.Ordinal))
                    throw new Exception("G1.15: relatório INF divergente: " + reportInfFragment);
                if (!expectedInvalid && reportWuaFragment is not null &&
                    !body.RenderedText.Contains(reportWuaFragment, StringComparison.Ordinal))
                    throw new Exception("G1.15: relatório WUA divergente: " + reportWuaFragment);

                foreach (var size in new[] { new Size(500, 340), new Size(790, 510) })
                {
                    dialog.Size = size;
                    dialog.PerformLayout();
                    Application.DoEvents();
                    if (state.AccessibleDescription != state.Text ||
                        !dialog.RectangleToScreen(dialog.ClientRectangle).Contains(
                            close.RectangleToScreen(close.ClientRectangle)))
                        throw new Exception("G1.15: viewport/estado acessível divergente.");
                    measured++;
                }

                // Callbacks de fixture executam os handlers reais, sem rede.
                if (!expectOfficial) officialAction();
                if (!expectInf) infAction();
                if (starts != 0)
                    throw new Exception("G1.15: handler bloqueado iniciou a fonte.");
                dialog.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
                foreach (var open in Application.OpenForms.Cast<Form>()
                    .Where(f => f.Text == ModalTitle).ToArray()) open.Close();
            }
        };
        var method = typeof(MainForm).GetMethod("ShowDriverReadOnlyReport",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        timer.Start();
        method.Invoke(owner, [inventory, selection,
            (Func<bool>)(() => guard.IsCurrent(inventory, selection, 15)),
            (Func<CancellationToken, DriverOfficialResult, bool>)((token, result) =>
                guard.CanPublish(inventory, selection, 15, token, result)),
            (Func<CancellationToken, DriverInfVerificationResult, bool>)((token, result) =>
                guard.CanPublishInf(inventory, selection, 15, token, result)),
            (Action<Action?>)(callback => observer = callback),
            (Func<CancellationToken, Task<DriverOfficialResult>>)(_ =>
            {
                starts++;
                return Task.FromResult(new DriverOfficialResult([], 0, 0, false, "Sintético WUA"));
            }),
            (Func<CancellationToken, Task<DriverInfVerificationResult>>)(_ =>
            {
                starts++;
                return Task.FromResult(new DriverInfVerificationResult(DriverInfTrust.Rejected));
            }),
            (TimeSpan?)TimeSpan.FromSeconds(3),
            (Action<Action, Action>)((a, b) => { officialAction = a; infAction = b; })]);
        timer.Stop();
        if (failure is not null) throw failure;
        if (observer is not null || measured != 2)
            throw new Exception("G1.15: modal deixou observador registrado ou não mediu os viewports.");
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
