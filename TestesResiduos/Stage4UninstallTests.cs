using System.ComponentModel;
using GerenciadorAplicativos;

internal static class Stage4UninstallTests
{
    private static ProgramEntry App(string id = "A", string root = "", string version = "1.0") => new()
    {
        Name = "GF Stage4 " + id, Publisher = "GF Tests", Version = version,
        InventoryId = "CurrentUser|Registry64|GF.Stage4." + id, InstallLocation = root,
        MsiProductCode = "{00000000-0000-0000-0000-00000000040" + (id == "A" ? "1}" : "2}"),
        UninstallString = "msiexec.exe /X{00000000-0000-0000-0000-00000000040" + (id == "A" ? "1}" : "2}")
    };
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var first = App(); var second = App("B");
        var selected = UninstallBatch.Select([first, second]);
        var present = new UninstallSnapshot([first, second], true);
        int launched = 0, reads = 0;
        var missingHandle = await new UninstallBatch().RunAsync(selected, true,
            () => { reads++; return Task.FromResult(present); }, (_, _) => { launched++; return Task.FromResult<int?>(null); }, _ => Task.CompletedTask);
        check(missingHandle.Count == 1 && missingHandle[0].State == UninstallState.Pending && launched == 1 && reads == 1,
            "ETAPA4: shell sem handle deixa pendente e interrompe lote antes do segundo aplicativo");
        var store = new UninstallOperationStore();
        store.Set(first, missingHandle[0].State == UninstallState.Pending ? UninstallStage.Pending : UninstallStage.Failed);
        check(store.Find(App(version: "2.0"))?.PreventRetry == true,
            "ETAPA4: ausência de handle conserva bloqueio de repetição mesmo após recarga da linha");
        var context = new ResidualUninstallContext(ResidualAppIdentity.From(first), "unsupported", [], [], [], 0, []);
        int offers = 0, elevated = 0, inspections = 0;
        var cleanup = await IntegratedResidualBatch.RunAsync(missingHandle,
            new Dictionary<string, ResidualUninstallContext> { [first.InventoryId] = context }, () => Task.FromResult(present),
            (_, _) => { offers++; return Task.FromResult(true); }, (_, _) => { elevated++; return Task.FromResult<ResidualElevationResponse?>(null); },
            _ => { }, progress: (_, _) => inspections++);
        check(cleanup.Offered == 0 && offers == 0 && elevated == 0 && inspections == 0,
            "ETAPA4: conclusão desconhecida não inspeciona nem oferece exclusão ou elevação");
        foreach (int code in new[] { 5, 1223, 225, 226, 193 })
        {
            launched = 0;
            var outcomes = await new UninstallBatch().RunAsync(selected, true, () => Task.FromResult(present), (_, _) =>
            {
                launched++;
                return PreparedUninstaller.ObserveStartedAsync(() => Task.FromException<int?>(new Win32Exception(code)));
            }, _ => Task.CompletedTask);
            check(outcomes.Count == 1 && outcomes[0].State == UninstallState.Pending && outcomes[0].Code == code && launched == 1,
                "ETAPA4: falha Win32 " + code + " ao observar processo iniciado permanece pendente sem novo lançamento");
        }
        foreach (int code in new[] { 2, 5, 225, 226, 740, 1223 })
        {
            var outcomes = await new UninstallBatch().RunAsync([selected[0]], true, () => Task.FromResult(present),
                (_, _) => Task.FromException<int?>(new Win32Exception(code)), _ => Task.CompletedTask);
            check(outcomes.Single().State == (code == 1223 ? UninstallState.Cancelled : UninstallState.StartFailed),
                "ETAPA4: falha comprovada antes do início " + code + " conserva classificação de início/UAC");
        }
        check(await PreparedUninstaller.ObserveStartedAsync(() => Task.FromResult<int?>(0)) == 0,
            "ETAPA4: observação concluída preserva código real do fornecedor");
        var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        launched = 0;
        var stopped = await new UninstallBatch().RunAsync(selected, true, () => Task.FromResult(present),
            (_, _) => { launched++; return Task.FromResult<int?>(0); }, _ => Task.CompletedTask, cancelled.Token);
        check(stopped.Single().State == UninstallState.Cancelled && launched == 0,
            "ETAPA4: cancelamento anterior não inicia fornecedor nem lote seguinte");
        cancelled.Dispose();
        var refusal = await new UninstallBatch().RunAsync(selected, false, () => Task.FromResult(present),
            (_, _) => throw new Exception("Não autorizado"), _ => Task.CompletedTask);
        check(refusal.All(o => o.State == UninstallState.Refused),
            "ETAPA4: NÃO na desinstalação recusa todos os itens sem executar comando");

        var identity = ResidualAppIdentity.From(first);
        var plan = new IntegratedResidualPlan(new(identity, "synthetic", [], [], [], 0, []), [], [], [], 0, true, []);
        var empty = new ResidualBatchResult([], 0, false, false, 0, Inspections: [plan]);
        UninstallOutcome Outcome(UninstallState state) => new(selected[0], state, 0);
        foreach (var state in new[] { UninstallState.Blocked, UninstallState.StartFailed, UninstallState.ProcessFailed, UninstallState.StillPresent })
            check(UninstallResultSummary.Status([Outcome(state)], empty, true).Contains("não concluída"),
                "ETAPA4: resumo não anuncia conclusão para " + state);
        check(UninstallResultSummary.Status([Outcome(UninstallState.RegistrationAbsent), new(selected[1], UninstallState.StartFailed)], empty, true)
            .Contains("parcialmente"), "ETAPA4: lote misto distingue remoção confirmada e falha");
        check(UninstallResultSummary.Status([Outcome(UninstallState.Cancelled)], empty, true).Contains("cancelada"),
            "ETAPA4: cancelamento do fornecedor aparece no estado geral");
        check(UninstallResultSummary.Status([Outcome(UninstallState.Pending)], empty with { Cancelled = true }, true).Contains("pendente"),
            "ETAPA4: cancelar limpeza não oculta fornecedor ainda pendente");
        check(UninstallResultSummary.Status([Outcome(UninstallState.RegistrationAbsent)], null, true).Contains("parcial"),
            "ETAPA4: ausência de inspeção não é limpeza concluída");
        var failed = new IntegratedCleanupResult(identity.TechnicalId,
            [new("f0", "file", ResidualItemState.Failed, "sharing_violation", 32)], 0, false, false, []);
        check(UninstallResultSummary.Status([Outcome(UninstallState.RegistrationAbsent)], empty with { Results = [failed] }, true).Contains("parcial"),
            "ETAPA4: alvo não removido impede anunciar limpeza finalizada");
        check(UninstallResultSummary.Status([Outcome(UninstallState.RegistrationAbsent)], empty with { Declined = true }, true).Contains("preservados"),
            "ETAPA4: NÃO na limpeza é decisão de preservação, sem falso erro");
        check(UninstallResultSummary.Status([Outcome(UninstallState.RegistrationAbsent)], empty, true).Contains("finalizada"),
            "ETAPA4: remoção e inspeção conclusivas mantêm estado final correto");
        check(!UninstallResultSummary.Status([], null, true).Contains("finalizada"),
            "ETAPA4: resultado vazio não fabrica conclusão");
        if (OperatingSystem.IsWindows()) await NativeCleanupAsync(check);
    }

    private static async Task NativeCleanupAsync(Action<bool, string> check)
    {
        string parent = Path.Combine(Path.GetTempPath(), "GF-PreBeta-Etapa4-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(parent, "A"), other = Path.Combine(parent, "B");
        var previous = IntegratedResidualPolicy.SyntheticScope;
        Directory.CreateDirectory(root); Directory.CreateDirectory(other);
        var app = App(root: root); var neighbor = App("B", other);
        IntegratedResidualPolicy.SyntheticScope = a => a.InventoryId == app.InventoryId ? new("synthetic", [root], [], []) : new("unsupported", [], [], []);
        string cache = Path.Combine(root, "cache.tmp"), personal = Path.Combine(root, "Documents", "keep.txt"), unrelated = Path.Combine(other, "neighbor.tmp");
        Directory.CreateDirectory(Path.GetDirectoryName(personal)!);
        File.WriteAllText(cache, "SYNTHETIC CACHE"); File.WriteAllText(personal, "SYNTHETIC PERSONAL"); File.WriteAllText(unrelated, "SYNTHETIC NEIGHBOR");
        try
        {
            var prepared = IntegratedResidualCleanup.Prepare(app, new([app, neighbor], true));
            var contexts = new Dictionary<string, ResidualUninstallContext> { [app.InventoryId] = prepared };
            UninstallOutcome[] outcomes = [new(new(app, UninstallBatch.Select([app])[0].Command), UninstallState.RegistrationAbsent, 0)];
            Task<UninstallSnapshot> Inventory() => Task.FromResult(new UninstallSnapshot([neighbor], true));
            Task<ResidualElevationResponse?> NoElevation(IReadOnlyList<ResidualElevationRequest> _, CancellationToken token) =>
                throw new Exception("Fixture não deve pedir UAC");
            var no = await IntegratedResidualBatch.RunAsync(outcomes, contexts, Inventory, (_, _) => Task.FromResult(false), NoElevation, _ => { });
            check(no.Declined && File.ReadAllText(cache) == "SYNTHETIC CACHE" && File.ReadAllText(personal) == "SYNTHETIC PERSONAL",
                "ETAPA4-NATIVO: NÃO preserva bytes dos resíduos e dados pessoais descartáveis");
            var changed = await IntegratedResidualBatch.RunAsync(outcomes, contexts, Inventory,
                (_, _) => { File.WriteAllText(cache, "CHANGED CONTENT"); return Task.FromResult(true); }, NoElevation, _ => { });
            check(changed.Results.Single().Items.Any(i => i.State == ResidualItemState.Preserved) && File.Exists(cache),
                "ETAPA4-NATIVO: arquivo alterado após consentimento é preservado pela revalidação");
            using var cancellation = new CancellationTokenSource();
            var stop = await IntegratedResidualBatch.RunAsync(outcomes, contexts, Inventory,
                (_, _) => { cancellation.Cancel(); return Task.FromResult(true); }, NoElevation, _ => { }, cancellation.Token);
            check(stop.Cancelled && File.Exists(cache) && File.Exists(personal),
                "ETAPA4-NATIVO: cancelamento antes da exclusão não remove alvos");
            var yes = await IntegratedResidualBatch.RunAsync(outcomes, contexts, Inventory, (_, _) => Task.FromResult(true), NoElevation, _ => { });
            check(yes.Results.Single().Files == 1 && !File.Exists(cache) &&
                File.ReadAllText(personal) == "SYNTHETIC PERSONAL" && File.ReadAllText(unrelated) == "SYNTHETIC NEIGHBOR",
                "ETAPA4-NATIVO: SIM remove só resíduo atribuído; preserva Documents e instalação vizinha");
            File.WriteAllText(cache, "NEW SYNTHETIC CACHE");
            var overlap = App("B", root);
            var blocked = await IntegratedResidualBatch.RunAsync(outcomes, contexts,
                () => Task.FromResult(new UninstallSnapshot([overlap], true)),
                (_, _) => throw new Exception("Propriedade compartilhada não pode oferecer limpeza"), NoElevation, _ => { });
            check(blocked.Offered == 0 && File.Exists(cache) && blocked.Unavailable > 0,
                "ETAPA4-NATIVO: proprietário concorrente impede oferta e exclusão");
        }
        finally
        {
            IntegratedResidualPolicy.SyntheticScope = previous;
            Directory.Delete(parent, recursive: true);
        }
    }
}
