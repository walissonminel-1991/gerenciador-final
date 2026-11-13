using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

// Teste WinForms REAL com resultados de WUA/INF exclusivamente sintéticos.
// Nenhum clique deste ensaio acessa o Windows Update ou o Driver Store real.
internal static class DriverReadOnlyDialogTemporalTests
{
    private const string Title = "Drivers — investigar sem alterações";
    internal static void Run(MainForm owner)
    {
        var device = new DriverInventoryEntry(@"PCI\TEST\FIXTURE", "Dispositivo de teste G1.10",
            "Fornecedor de teste", "1.0", "oem17.inf", true, true)
        {
            HardwareIds = [@"PCI\VEN_1234&DEV_5678"],
            PackageEvidence = new("oem17.inf", DriverStoreState.Mapped),
            WindowsEvents = [new(@"PCI\TEST\FIXTURE", "oem17.inf",
                new DateTime(2026, 10, 25, 12, 34, 56), DriverInstallOutcome.Failed)]
        };
        var inventory = new DriverInventoryResult([device], false, "fotografia G1.10");
        // Seleção e troca de filtro emitem invalidação sem polling.
        using (var list = new DarkDriverList())
        {
            int changes = 0;
            list.SelectionChanged += (_, _) => changes++;
            list.SetItems([device]);
            typeof(DarkDriverList).GetMethod("SelectIndex",
                BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(list, [0]);
            list.SetItems([]);
            if (changes != 2)
                throw new Exception("G1.10: seleção/filtro não notificaram invalidação visual.");
        }
        var method = typeof(MainForm).GetMethod("ShowDriverReadOnlyReport",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        DriverOfficialResult fake = FakeOfficial(device);

        // Caso 1: consulta em curso, atualização de geração notificada pela
        // página e resultado WUA tardio; texto sensível desaparece nos dois DPIs.
        {
            long generation = 1;
            int launched = 0;
            Action? notify = null;
            var read = new TaskCompletionSource<DriverOfficialResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int stage = 0;
            RunModal(owner, method, inventory, device, () => generation,
                callback => notify = callback,
                _ => { launched++; return read.Task; }, null, TimeSpan.FromSeconds(30),
                dialog =>
                {
                    var (body, status, official, verify, close) = Controls(dialog);
                    if (stage++ == 0)
                    {
                        if (!official.Enabled || !verify.Enabled ||
                            !body.RenderedText.Contains("falha registrada"))
                            throw new Exception("G1.10: diálogo válido não mostra investigação inicial.");
                        ClickEnabled(official, () => launched == 1);
                        if (launched != 1)
                            throw new Exception($"G1.10: evento Click WUA sintético inconsistente; launched={launched}, enabled={official.Enabled}, status={status.Text}, canSelect={official.CanSelect}.");
                        generation = 2;
                        notify?.Invoke();
                        AssertObsolete(dialog, device);
                        read.TrySetResult(fake);
                        return false;
                    }
                    AssertObsolete(dialog, device);
                    // Um botão desarmado não pode iniciar nova consulta.
                    official.PerformClick();
                    verify.PerformClick();
                    if (launched != 1 || !close.Enabled || notify is null)
                        throw new Exception("G1.10: invalidação não preservou somente Fechar.");
                    return true;
                });
            if (notify is not null || launched != 1)
                throw new Exception("G1.10: callback de geração ficou registrado após fechar.");
            Console.WriteLine("OK G1.10: geração alterada oculta relatório e desarma botões, inclusive após WUA sintético tardio");
        }

        // Caso 2: WaitAsync expira, mas o trabalho sintético continua. Clique
        // subsequente não reinicia a busca, e o retorno posterior não é publicado.
        {
            long generation = 3;
            int launched = 0;
            var read = new TaskCompletionSource<DriverOfficialResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int stage = 0;
            RunModal(owner, method, inventory, device, () => generation, _ => { },
                _ => { launched++; return read.Task; }, null,
                TimeSpan.FromMilliseconds(35),
                dialog =>
                {
                    var (body, status, official, verify, close) = Controls(dialog);
                    if (stage++ == 0)
                    {
                        ClickEnabled(official, () => launched == 1);
                        if (launched != 1)
                            throw new Exception("G1.10: tarefa WUA sintética não iniciou.");
                        return false;
                    }
                    if (stage == 2)
                    {
                        if (!status.Text.Contains("prazo esgotado", StringComparison.OrdinalIgnoreCase) &&
                            !status.Text.Contains("cancelada", StringComparison.OrdinalIgnoreCase))
                            throw new Exception("G1.10: timeout do chamador não foi comunicado.");
                        official.PerformClick();
                        if (launched != 1 ||
                            !status.Text.Contains("ainda encerrando", StringComparison.OrdinalIgnoreCase))
                            throw new Exception("G1.10: nova busca iniciada com tarefa anterior pendente.");
                        read.TrySetResult(fake);
                        return false;
                    }
                    if (body.RenderedText.Contains("MARCADOR-WUA-ANTIGO") ||
                        !body.RenderedText.Contains("falha registrada") ||
                        !close.Enabled)
                        throw new Exception("G1.10: WUA tardio após timeout sobrescreveu relatório.");
                    return true;
                });
            if (launched != 1)
                throw new Exception("G1.10: consulta anterior gerou trabalho adicional.");
            Console.WriteLine("OK G1.10: timeout WaitAsync não publica retorno tardio nem duplica pesquisa");
        }

        // Caso 3: a janela perde/recupera foco sem notificação da página.
        // OnActivated real deve descartar prova antiga, inclusive INF pendente.
        {
            long generation = 5;
            var read = new TaskCompletionSource<DriverInfVerificationResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            int launched = 0;
            RunModal(owner, method, inventory, device, () => generation, _ => { },
                null, _ => { launched++; return read.Task; },
                TimeSpan.FromSeconds(25),
                dialog =>
                {
                    var (body, status, official, verify, close) = Controls(dialog);
                    ClickEnabled(verify, () => launched == 1);
                    if (launched != 1)
                        throw new Exception("G1.10: tarefa INF sintética não iniciou.");
                    generation = 6;
                    var activate = typeof(Form).GetMethod("OnActivated",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? throw new Exception("G1.10: evento Activated indisponível.");
                    activate.Invoke(dialog, [EventArgs.Empty]);
                    AssertObsolete(dialog, device);
                    return true; // fechar ainda com verificação INF pendente
                });
            read.TrySetResult(new DriverInfVerificationResult(DriverInfTrust.VerifiedInf,
                new string('A', 64), @"C:\TESTE\catalogo.cat", "Fornecedor de teste"));
            Application.DoEvents();
            if (launched != 1)
                throw new Exception("G1.10: fechamento disparou nova verificação INF.");
            Console.WriteLine("OK G1.10: foco e fechamento invalidam prova INF sem publicar tarefa tardia");
        }

        // Caso 4: abrir uma nova janela com nova geração deve restaurar
        // exclusivamente os dados válidos, sem carregar a obsolescência anterior.
        {
            long generation = 7;
            RunModal(owner, method, inventory, device, () => generation, _ => { },
                null, null, TimeSpan.FromSeconds(30),
                dialog =>
                {
                    var (body, status, official, verify, close) = Controls(dialog);
                    if (!official.Enabled || !verify.Enabled || !close.Enabled ||
                        !body.RenderedText.Contains("falha registrada") ||
                        body.RenderedText.Contains("Feche esta janela e abra uma nova investigação"))
                        throw new Exception("G1.10: janela reaberta herdou invalidação antiga.");
                    return true;
                });
            Console.WriteLine("OK G1.10: reabertura nova restabelece relatório somente leitura válido");
        }
    }

    // PerformClick pode ser suprimido pelo foco do runner WinForms mesmo
    // com Enabled=true. O fallback invoca o mesmo OnClick/evento registrado
    // no controle real; não chama o motor nem contorna verificações do GF.
    private static void ClickEnabled(Button button, Func<bool> observed)
    {
        if (!button.Enabled) throw new Exception("G1.10: botão deveria estar habilitado.");
        button.PerformClick();
        if (observed()) return;
        typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(button, [EventArgs.Empty]);
    }

    private static DriverOfficialResult FakeOfficial(DriverInventoryEntry device)
    {
        var offer = new DriverOfficialOffer("5819343e-5d44-44c7-a8e3-e2c3c8abe622",
            7, "MARCADOR-WUA-ANTIGO", device.HardwareIds[0], device.Provider,
            device.DeviceName, new DateTime(2026, 11, 13));
        var match = new DriverOfficialMatch(offer, device.DeviceId, device.DeviceName)
        {
            Installed = new(device.InfName, device.Provider, device.Version, device.Signed,
                device.PackageEvidence?.State, device.PnpProblemCode,
                device.WithoutSignedDriverAssociation)
        };
        return new([match], 1, 0, false, "MARCADOR-WUA-ANTIGO");
    }

    private static void RunModal(MainForm owner, MethodInfo method,
        DriverInventoryResult inventory, DriverInventoryEntry device,
        Func<long> generation, Action<Action?> observe,
        Func<CancellationToken, Task<DriverOfficialResult>>? officialRead,
        Func<CancellationToken, Task<DriverInfVerificationResult>>? infRead,
        TimeSpan timeout, Func<Form, bool> tick)
    {
        const long capturedGeneration = 1;
        // A geração inicial de cada cenário é a que foi informada pelo teste.
        long origin = generation();
        var guard = new DriverReportPublicationGuard(inventory, device, origin);
        Exception? failure = null;
        int calls = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 150 };
        timer.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.Cast<Form>()
                .SingleOrDefault(f => f.Text == Title);
            if (dialog is null) { failure = new Exception("G1.10: diálogo WinForms não encontrado."); timer.Stop(); return; }
            try
            {
                if (++calls > 8) throw new Exception("G1.10: cenário WinForms não terminou.");
                if (tick(dialog)) { timer.Stop(); dialog.Close(); }
            }
            catch (Exception ex)
            {
                failure = ex;
                timer.Stop();
                dialog.Close();
            }
        };
        timer.Start();
        method.Invoke(owner, [inventory, device,
            (Func<bool>)(() => guard.IsCurrent(inventory, device, generation())),
            (Func<CancellationToken, DriverOfficialResult, bool>)((token, result) =>
                guard.CanPublish(inventory, device, generation(), token, result)),
            (Func<CancellationToken, DriverInfVerificationResult, bool>)((token, result) =>
                guard.CanPublishInf(inventory, device, generation(), token, result)),
            (Action<Action?>)observe, officialRead, infRead, (TimeSpan?)timeout]);
        timer.Stop();
        if (failure is not null) throw failure;
        if (calls == 0) throw new Exception("G1.10: nenhum evento UI executado.");
    }

    private static (DarkLogView body, Label status, Button official,
        Button verify, Button close) Controls(Form dialog)
    {
        var nodes = Descendants(dialog).ToArray();
        var body = nodes.OfType<DarkLogView>().Single(x => x.Name == "DriverReadOnlyReportText");
        var buttons = nodes.OfType<Button>().ToArray();
        var official = buttons.Single(x => x.Name == "DriverOfficialReadOnly");
        var verify = buttons.Single(x => x.Name == "DriverVerifyInfReadOnly");
        var close = buttons.Single(x => x.Text == "Fechar");
        var status = nodes.OfType<Label>().Single(x =>
            x.Text.Contains("Consult", StringComparison.OrdinalIgnoreCase) ||
            x.Text.Contains("reabra", StringComparison.OrdinalIgnoreCase) ||
            x.Text.Contains("cancel", StringComparison.OrdinalIgnoreCase) ||
            x.Text.Contains("encerrando", StringComparison.OrdinalIgnoreCase) ||
            x.Text.Contains("resultado", StringComparison.OrdinalIgnoreCase));
        return (body, status, official, verify, close);
    }

    private static void AssertObsolete(Form dialog, DriverInventoryEntry device)
    {
        foreach (var size in new[] { new Size(500, 340), new Size(790, 510) })
        {
            dialog.Size = size;
            dialog.PerformLayout();
            var (body, status, official, verify, close) = Controls(dialog);
            if (!body.RenderedText.Contains("Inventário ou seleção alterados") ||
                body.RenderedText.Contains(device.DeviceId) ||
                body.RenderedText.Contains("falha registrada") ||
                body.RenderedText.Contains("MARCADOR-WUA-ANTIGO") ||
                official.Enabled || verify.Enabled || !close.Enabled ||
                !status.Text.Contains("reabra", StringComparison.OrdinalIgnoreCase))
                throw new Exception("G1.10: evidência obsoleta visível ou botões ativos em " +
                    size.Width + "x" + size.Height);
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
