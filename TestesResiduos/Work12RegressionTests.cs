using System.Security.AccessControl;
using System.Security.Principal;
using GerenciadorAplicativos;
using Microsoft.Win32;

// Somente alvos com GUID criados pelo proprio teste; nunca aplicativos reais.
internal static class Work12RegressionTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("WORK12 requires Windows");
        CatalogRegistryScope(check);
        string id = "GF-W12-" + Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), id);
        string keyPath = @"Software\" + id;
        Directory.CreateDirectory(root);
        using var testHive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        var app = new ProgramEntry { Name = id, Publisher = "GF Synthetic", InventoryId = id,
            InstallLocation = root, UninstallString = "\"" + Path.Combine(root, "uninstall.exe") + "\"" };
        var address = new ResidualKeyLocation(RegistryHive.CurrentUser, RegistryView.Registry64, keyPath);
        IntegratedResidualPolicy.SyntheticScope = _ => new("synthetic", [root], [address], []);
        Task<UninstallSnapshot> Empty() => Task.FromResult(new UninstallSnapshot([], true));
        var removed = new UninstallOutcome(new(app, new(Path.Combine(root, "uninstall.exe"), "", null)), UninstallState.RegistrationAbsent, 0);
        try
        {
            // A chave nao existia antes, mas o desinstalador deixou configuracao nela.
            var context = IntegratedResidualCleanup.Prepare(app, new([app], true));
            using (var key = testHive.CreateSubKey(keyPath)) key.SetValue("InstallPath", root);
            var plan = IntegratedResidualCleanup.Capture(context, new([], true));
            check(plan.Keys.Count == 1, "W12 chave da politica criada pelo desinstalador precisa ser encontrada");
            testHive.DeleteSubKeyTree(keyPath, false);

            // Reproduzir .56 com ZERO alvos, inclusive sem pasta vazia elegivel.
            Directory.Delete(root);
            // Erro 5 no preflight nunca equivale a ausencia.
            context = context with { Preserved = 1, Problems = [new("prepare", "registry_unreadable", 5)] };
            var audit = new List<string>();
            var batch = await IntegratedResidualBatch.RunAsync([removed],
                new Dictionary<string, ResidualUninstallContext> { [id] = context }, Empty,
                (_, _) => Task.FromResult(false), (_, _) => Task.FromResult<ResidualElevationResponse?>(null), audit.Add);
            check(batch.Offered == 0 && batch.Inspections!.Single().Eligible == 0 && batch.Unavailable > 0 &&
                !audit.Any(x => x.Contains("no_eligible_residue")),
                "W12 erro 5 sem alvo elegivel deve terminar inconclusivo");

            // Consultar valores nao exige notificacoes nem permissoes de escrita.
            using (var key = testHive.CreateSubKey(keyPath)) key.SetValue("Marker", "SYNTHETIC");
            using (var acl = testHive.OpenSubKey(keyPath, RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.ChangePermissions | RegistryRights.ReadPermissions))
            {
                var original = acl!.GetAccessControl();
                var denied = acl.GetAccessControl();
                denied.AddAccessRule(new RegistryAccessRule(WindowsIdentity.GetCurrent().User!, RegistryRights.Notify, AccessControlType.Deny));
                acl.SetAccessControl(denied);
                bool readable = false;
                try
                {
                    using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                    using var key = ResidualRegistryGuard.Open(hive, keyPath, RegistryView.Registry64);
                    readable = key is not null && ResidualRegistryGuard.Fingerprint(key, RegistryView.Registry64, out _, out _);
                }
                catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex)) { }
                finally
                {
                    var restore = new RegistrySecurity();
                    restore.SetSecurityDescriptorBinaryForm(original.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                    acl.SetAccessControl(restore);
                }
                check(readable, "W12 inspecao nao deve exigir KEY_NOTIFY negado");
            }
            testHive.DeleteSubKeyTree(keyPath, false);

            // Sem receita e sem InstallLocation: comando registrado corrobora a pasta.
            IntegratedResidualPolicy.SyntheticScope = null;
            string dedicated = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), id);
            var unknown = new ProgramEntry { Name = id, Publisher = app.Publisher, InventoryId = id,
                UninstallString = "\"" + Path.Combine(dedicated, "uninstall.exe") + "\"" };
            check(IntegratedResidualPolicy.Build(ResidualAppIdentity.From(unknown)).Roots.Contains(dedicated, StringComparer.OrdinalIgnoreCase),
                "W12 pasta dedicada corroborada pelo comando nao depende de InstallLocation preenchido");
        }
        finally
        {
            IntegratedResidualPolicy.SyntheticScope = null;
            testHive.DeleteSubKeyTree(keyPath, false);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        await IntegratedCycle(check);
        await UninstallerStates(check);
        await SilentEngines(check);
        GenericOwnership(check);
        await ActualWaitCancellation(check);
    }

    // Reproduz a discrepância do PC FORTE: um catálogo explicitamente por usuário
    // não pode ampliar a busca para HKLM32/64 só porque o app foi instalado para a máquina.
    // Nenhuma chave real é aberta, alterada ou excluída neste teste.
    private static void CatalogRegistryScope(Action<bool, string> check)
    {
        foreach (var sample in new[]
        {
            (Name: "SumatraPDF", Publisher: "Krzysztof Kowalczyk", Policy: "sumatrapdf", Key: @"Software\SumatraPDF"),
            (Name: "Notepad++ (64-bit x64)", Publisher: "Notepad++ Team", Policy: "notepadpp", Key: @"Software\Notepad++")
        })
        {
            var app = new ProgramEntry
            {
                Name = sample.Name, Publisher = sample.Publisher,
                InventoryId = "LocalMachine|Registry64|" + sample.Policy
            };
            var scope = IntegratedResidualPolicy.Build(ResidualAppIdentity.From(app));
            check(scope.Policy == sample.Policy && scope.Keys.Count == 1 &&
                scope.Keys.All(k => k.Hive == RegistryHive.CurrentUser &&
                    k.View == RegistryView.Registry64 && k.Subkey == sample.Key),
                "W12 catalogo per-user jamais promove chave HKLM por instalacao machine: " + sample.Policy);
        }
        check(UninstallRuleCatalog.Rules.Count > 0 &&
            UninstallRuleCatalog.Rules.Where(rule => rule.RegistryKeys.Count > 0)
                .All(rule => rule.RegistryKeys.All(key => key.StartsWith(@"Software\", StringComparison.Ordinal))),
            "W12 contratos atuais de exclusividade por usuario permanecem estritos");
    }

    private static async Task IntegratedCycle(Action<bool, string> check)
    {
        string id = "GF-W12-Cycle-" + Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), id), keyPath = @"Software\" + id;
        Directory.CreateDirectory(root);
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        var app = new ProgramEntry { Name = id, Publisher = "GF Synthetic", InventoryId = id, InstallLocation = root,
            UninstallString = "\"" + Path.Combine(root, "uninstall.exe") + "\"" };
        var address = new ResidualKeyLocation(RegistryHive.CurrentUser, RegistryView.Registry64, keyPath);
        var removed = new UninstallOutcome(new(app, new(Path.Combine(root, "uninstall.exe"), "", null)), UninstallState.RegistrationAbsent, 0);
        Task<UninstallSnapshot> Empty() => Task.FromResult(new UninstallSnapshot([], true));
        Task<ResidualElevationResponse?> NoElevation(IReadOnlyList<ResidualElevationRequest> _, CancellationToken __) => Task.FromResult<ResidualElevationResponse?>(null);
        ResidualUninstallContext Prepare() => IntegratedResidualCleanup.Prepare(app, new([app], true));
        Dictionary<string, ResidualUninstallContext> Context(ResidualUninstallContext context) => new() { [id] = context };
        IntegratedResidualPolicy.SyntheticScope = _ => new("synthetic", [root], [address], []);
        try
        {
            var context = Prepare(); Directory.Delete(root);
            var empty = await IntegratedResidualBatch.RunAsync([removed], Context(context), Empty,
                (_, _) => throw new Exception("Nao deve pedir limpeza sem residuos"), NoElevation, _ => { });
            check(empty.Inspections!.Single().Assessment == ResidualAssessment.None && empty.Unavailable == 0 &&
                UninstallResultSummary.Describe([removed], empty).Contains("Nenhum resíduo encontrado nos locais verificados"),
                "W12 ausencia comprovada tem resumo e dispensa SIM/NAO");

            Directory.CreateDirectory(root);
            string config = Path.Combine(root, "settings.cfg"), personal = Path.Combine(root, "documento.pdf");
            File.WriteAllText(config, "SYNTHETIC"); File.WriteAllText(personal, "PERSONAL SYNTHETIC");
            context = Prepare(); int prompts = 0;
            var declined = await IntegratedResidualBatch.RunAsync([removed], Context(context), Empty,
                (_, _) => { prompts++; return Task.FromResult(false); }, NoElevation, _ => { });
            check(prompts == 1 && declined.Declined && File.ReadAllText(config) == "SYNTHETIC" && File.Exists(personal) &&
                UninstallResultSummary.Describe([removed], declined).Contains("Você escolheu NÃO"),
                "W12 NAO mantem conteudo e resultado visivel");

            var phases = new List<string>(); prompts = 0;
            var cleaned = await IntegratedResidualBatch.RunAsync([removed], Context(context), Empty,
                (_, _) => { prompts++; check(File.Exists(config), "W12 nenhum arquivo excluido antes do SIM"); return Task.FromResult(true); },
                NoElevation, _ => { }, progress: (_, phase) => phases.Add(phase));
            string summary = UninstallResultSummary.Describe([removed], cleaned);
            check(prompts == 1 && cleaned.Results.Single().Files == 1 && !File.Exists(config) && File.ReadAllText(personal) == "PERSONAL SYNTHETIC" &&
                summary.Contains("Itens preservados: 1") && summary.Contains("Arquivos pessoais"),
                "W12 SIM remove residuo real e explica arquivo pessoal preservado");
            check(phases.SequenceEqual(new[] { "inspecting", "awaiting_cleanup", "cleaning" }),
                "W12 ciclo emite etapas reais de verificacao decisao e limpeza");
            var kept = await IntegratedResidualBatch.RunAsync([removed], Context(Prepare()), Empty,
                (_, _) => throw new Exception("Dados pessoais nao devem ser oferecidos"), NoElevation, _ => { });
            check(kept.Inspections!.Single().Assessment == ResidualAssessment.Preserved &&
                UninstallResultSummary.Describe([removed], kept).Contains("preservados por segurança"),
                "W12 residuos preservados nao sao classificados como ausencia");

            File.WriteAllText(config, "SYNTHETIC");
            using (var key = hive.CreateSubKey(keyPath)) key.SetValue("Marker", "SYNTHETIC");
            context = Prepare();
            using (var acl = hive.OpenSubKey(keyPath, RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.ChangePermissions | RegistryRights.ReadPermissions))
            {
                var original = acl!.GetAccessControl(); var denied = acl.GetAccessControl();
                denied.AddAccessRule(new RegistryAccessRule(WindowsIdentity.GetCurrent().User!, RegistryRights.QueryValues, AccessControlType.Deny));
                acl.SetAccessControl(denied);
                try
                {
                    var partial = await IntegratedResidualBatch.RunAsync([removed], Context(context), Empty,
                        (_, _) => Task.FromResult(true), NoElevation, _ => { });
                    summary = UninstallResultSummary.Describe([removed], partial);
                    check(partial.Unavailable == 1 && partial.Results.Single().Files == 1 && !File.Exists(config) &&
                        summary.Contains("erro 5") && summary.Contains("não significa ausência"),
                        "W12 erro 5 preserva chave e permite limpar somente outro alvo comprovado");
                    check(acl.GetAccessControl().GetAccessRules(true, false, typeof(SecurityIdentifier)).OfType<RegistryAccessRule>()
                            .Any(r => r.AccessControlType == AccessControlType.Deny && (r.RegistryRights & RegistryRights.QueryValues) != 0),
                        "W12 producao nao altera ACL nem remove negacao de acesso");
                    check(RebootResidualReview.Select([removed], partial).Length == 0,
                        "W12 erro 5 nao solicita reinicio");
                    var onlyDenied = await IntegratedResidualBatch.RunAsync([removed], Context(context), Empty,
                        (_, _) => throw new Exception("Sem elegiveis nao pode pedir limpeza"), NoElevation, _ => { });
                    check(onlyDenied.Unavailable == 1 && onlyDenied.Offered == 0 &&
                        onlyDenied.Inspections!.Single().Eligible == 0 &&
                        UninstallResultSummary.Describe([removed], onlyDenied).Contains("não significa ausência"),
                        "W12 ACL real erro 5 com zero elegiveis informa inconclusao sem pergunta vazia");
                }
                finally
                {
                    var restore = new RegistrySecurity();
                    restore.SetSecurityDescriptorBinaryForm(original.GetSecurityDescriptorBinaryForm(), AccessControlSections.Access);
                    acl.SetAccessControl(restore);
                }
            }
            hive.DeleteSubKeyTree(keyPath, false);

            File.WriteAllText(config, "LOCKED SYNTHETIC"); context = Prepare();
            using (var held = new FileStream(config, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var locked = await IntegratedResidualBatch.RunAsync([removed], Context(context), Empty,
                    (_, _) => Task.FromResult(false), NoElevation, _ => { });
                check(locked.Inspections!.Single().Problems.Any(p => p.Error is 32 or 33) &&
                    RebootResidualReview.Select([removed], locked).Any(i => i.Targets.Any(t => t.Address == root)),
                    "W12 bloqueio real 32/33 permite somente revisao passiva apos reinicio");
            }
            context = Prepare(); using var cancel = new CancellationTokenSource();
            var cancelled = await IntegratedResidualBatch.RunAsync([removed], Context(context), Empty,
                (_, _) => { cancel.Cancel(); return Task.FromResult(true); }, NoElevation, _ => { }, cancel.Token);
            check(cancelled.Cancelled && File.ReadAllText(config) == "LOCKED SYNTHETIC",
                "W12 cancelamento na decisao impede qualquer exclusao");

            var personalPlan = IntegratedResidualCleanup.Capture(context, new([new ProgramEntry {
                Name = "Outro", InventoryId = "other", InstallLocation = root }], true));
            check(personalPlan.Eligible == 0 && File.Exists(config),
                "W12 outra instalacao compartilhando pasta impede limpeza");

            // Pasta criada depois: deve aparecer como preservada, sem receber identidade inventada.
            File.Delete(config); File.Delete(personal); Directory.Delete(root);
            context = Prepare(); Directory.CreateDirectory(root); File.WriteAllText(config, "LATE");
            var late = IntegratedResidualCleanup.Capture(context, new([], true));
            check(late.Files.Count == 0 && late.Problems.Any(p => p.Reason == "root_without_preuninstall_identity"),
                "W12 pasta nova existe e e reportada sem exclusao nao comprovada");
        }
        finally
        {
            IntegratedResidualPolicy.SyntheticScope = null;
            hive.DeleteSubKeyTree(keyPath, false);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task UninstallerStates(Action<bool, string> check)
    {
        string code = "{00000000-0000-0000-0000-000000001212}";
        var app = new ProgramEntry { Name = "GF Synthetic MSI", InventoryId = "GF-W12-MSI", MsiProductCode = code, UninstallString = "msiexec.exe /X" + code };
        var requests = UninstallBatch.Select([app]); var phases = new List<string>();
        bool removed = false;
        Task<UninstallSnapshot> Inventory() => Task.FromResult(new UninstallSnapshot(removed ? [] : [app], true));
        var launcher = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = new UninstallBatch().RunAsync(requests, true, Inventory, (_, _) => launcher.Task, _ => Task.CompletedTask,
            progress: (_, phase) => phases.Add(phase));
        check(!task.IsCompleted && phases.Contains("working"), "W12 desinstalador demorado informa trabalho antes de concluir");
        removed = true; launcher.SetResult(0);
        var outcomes = await task;
        check(outcomes.Single().State == UninstallState.RegistrationAbsent && phases.SequenceEqual(new[] { "preparing", "working", "confirming" }),
            "W12 conclusao exige saida e janela completa de ausencia no inventario");
        removed = false;
        var timed = await new UninstallBatch().RunAsync(requests, true, Inventory,
            (_, _) => Task.FromException<int?>(new TimeoutException()), _ => Task.CompletedTask);
        check(timed.Single().State == UninstallState.Pending && UninstallResultSummary.Describe(timed, null).Contains("Não repita"),
            "W12 timeout fica pendente e impede sugerir repeticao");
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); int starts = 0;
        var cancelled = await new UninstallBatch().RunAsync(requests, true, Inventory,
            (_, _) => { starts++; return Task.FromResult<int?>(0); }, _ => Task.CompletedTask, cancel.Token);
        check(starts == 0 && cancelled.Single().State == UninstallState.Cancelled, "W12 cancelamento anterior impede iniciar fornecedor");
        using var after = new CancellationTokenSource();
        var pending = await new UninstallBatch().RunAsync(requests, true, Inventory,
            (_, _) => { after.Cancel(); return Task.FromCanceled<int?>(after.Token); }, _ => Task.CompletedTask, after.Token);
        check(pending.Single().State == UninstallState.Pending, "W12 cancelar espera nao promete cancelar processo do fornecedor");
        var unknown = await new UninstallBatch().RunAsync(requests, true, Inventory, (_, _) => Task.FromResult<int?>(null), _ => Task.CompletedTask);
        check(unknown.Single().State == UninstallState.StartFailed, "W12 resposta nula nao comprova desinstalacao");
        var store = new UninstallOperationStore();
        foreach (var stage in Enum.GetValues<UninstallStage>())
        {
            store.Set(app, stage);
            check(!store.Find(app)!.Text.Contains('%'), "W12 sem porcentagem ficticia em " + stage);
        }
        store.Set(app, UninstallStage.Pending);
        check(store.Find(app)!.PreventRetry && store.Find(new ProgramEntry { Name = app.Name, InventoryId = "different" }) is null,
            "W12 operacao pendente protege identidade sem remapear pelo nome");
    }

    private static async Task SilentEngines(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "GF-W12-Silent-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string fixture = Path.Combine(AppContext.BaseDirectory, "SyntheticSilent", "GF-SilentFixture.exe");
        string exe = Path.Combine(root, "unins000.exe"), dat = Path.ChangeExtension(exe, ".dat");
        try
        {
            File.Copy(fixture, exe);
            check(System.Diagnostics.FileVersionInfo.GetVersionInfo(exe).FileDescription == "Setup/Uninstall",
                "W12 fixture PE contem metadado real do motor Inno");
            var app = new ProgramEntry { Name = "GF Synthetic Inno", InventoryId = "GF-W12-Inno", Publisher = "GF Synthetic", UninstallString = "\"" + exe + "\"" };
            var request = UninstallBatch.Select([app]).Single();
            check(request.Command.Arguments.Length == 0, "W12 nome unins000 sem log de motor nao autoriza flags");
            File.WriteAllBytes(dat, System.Text.Encoding.ASCII.GetBytes("Inno Setup Uninstall Log (b)".PadRight(64, '\0')));
            request = UninstallBatch.Select([app]).Single();
            check(InstallerEngineEvidence.InstalledKind(request.Command with { Arguments = "" }) == "inno",
                "W12 descoberta de atualizacao reconhece Inno instalado pelo log sem confundir setup e uninstaller");
            check(request.Command.Arguments == "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART" && SilentUninstallPolicy.UsesSilentMode(request),
                "W12 Inno comprovado usa parametros documentados e impede reboot automatico");
            File.WriteAllText(dat, "FAKE HEADER".PadRight(64));
            check(!InstallerEngineEvidence.IsVerifiedInnoUninstaller(request.Command with { Arguments = "" }),
                "W12 descoberta de atualizacao recusa log Inno adulterado");
            check(UninstallBatch.Select([app]).Single().Command.Arguments.Length == 0, "W12 log Inno adulterado recua para interacao");
            string other = Path.Combine(root, "other.exe"); File.Copy(fixture, other);
            var wrong = new ProgramEntry { Name = app.Name, InventoryId = app.InventoryId, UninstallString = app.UninstallString,
                QuietUninstallString = "\"" + other + "\" /S" };
            check(!SilentUninstallPolicy.MatchesAuthorized(wrong, new(other, "/S", null)),
                "W12 fronteira de execucao rejeita quiet de outro EXE mesmo se construido diretamente");
            var quiet = new ProgramEntry { Name = app.Name, InventoryId = app.InventoryId, UninstallString = app.UninstallString,
                QuietUninstallString = "\"" + exe + "\" /S /KEEP-SETTINGS" };
            request = UninstallBatch.Select([quiet]).Single();
            check(request.Command.Arguments == "/S /KEEP-SETTINGS" && SilentUninstallPolicy.UsesSilentMode(request),
                "W12 comando quiet registrado mantem exatamente suas opcoes");
            var differentProduct = new ProgramEntry { Name = "GF Synthetic shared engine", InventoryId = "GF-W12-Product-A",
                UninstallString = "\"" + other + "\" /product:A", QuietUninstallString = "\"" + other + "\" /product:B /quiet" };
            check(UninstallBatch.Select([differentProduct]).Single().Command.Arguments == "/product:A" &&
                !SilentUninstallPolicy.MatchesAuthorized(differentProduct, new(other, "/product:B /quiet", null)),
                "W12 quiet do mesmo EXE nao pode trocar identificador de produto");
            var alias = new ProgramEntry { Name = "GF Synthetic alias", InventoryId = "GF-W12-Alias", UninstallString = app.UninstallString };
            int launches = 0;
            var shared = await new UninstallBatch().RunAsync(UninstallBatch.Select([quiet]), true,
                () => Task.FromResult(new UninstallSnapshot([quiet, alias], true)),
                (_, _) => { launches++; return Task.FromResult<int?>(0); }, _ => Task.CompletedTask);
            check(launches == 0 && shared.Single().State == UninstallState.Blocked,
                "W12 quiet nao mascara EXE compartilhado por dois registros sem identificador");
            var unlocker = new ProgramEntry { Name = "Unlocker 1.9.2", InventoryId = "GF-W12-Unlocker", UninstallString = "\"" + other + "\"" };
            request = UninstallBatch.Select([unlocker]).Single();
            check(request.Command.Arguments.Length == 0 && SilentUninstallPolicy.DescribeMode(request).Contains("interativo"),
                "W12 Unlocker sem contrato de motor informa interacao sem improvisar flags");

            string sumatraRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SumatraPDF");
            // CI descartavel. Jamais tocar em uma instalacao que ja exista.
            if (Directory.Exists(sumatraRoot)) throw new Exception("Fixture Sumatra exige local isolado vazio");
            Directory.CreateDirectory(sumatraRoot);
            try
            {
                string sumatraExe = Path.Combine(sumatraRoot, "SumatraPDF.exe"); File.Copy(fixture, sumatraExe);
                var sumatra = new ProgramEntry { Name = "SumatraPDF", Publisher = "Krzysztof Kowalczyk", Version = "3.6.1", InventoryId = "GF-W12-Sumatra",
                    InstallLocation = sumatraRoot, UninstallString = "\"" + sumatraExe + "\" -uninstall" };
                request = UninstallBatch.Select([sumatra]).Single();
                check(request.Command.Arguments == "-uninstall -silent" && SilentUninstallPolicy.UsesSilentMode(request),
                    "W12 Sumatra 3.6.1 usa contrato conferido no fonte oficial");
            }
            finally { Directory.Delete(sumatraRoot, true); }
        }
        finally { Directory.Delete(root, true); }
    }

    private static void GenericOwnership(Action<bool, string> check)
    {
        string id = "GF-W12-Ownership-" + Guid.NewGuid().ToString("N");
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", id);
        string keyPath = @"Software\" + id;
        Directory.CreateDirectory(root);
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        var app = new ProgramEntry { Name = id, Publisher = "GF Synthetic", InventoryId = id,
            UninstallString = "\"" + Path.Combine(root, "uninstall.exe") + "\"" };
        IntegratedResidualPolicy.SyntheticScope = null;
        try
        {
            File.WriteAllText(Path.Combine(root, "library.dll"), "SYNTHETIC");
            using (var key = hive.CreateSubKey(keyPath)) key.SetValue("InstallPath", @"C:\Windows");
            var context = IntegratedResidualCleanup.Prepare(app, new([app], true));
            var plan = IntegratedResidualCleanup.Capture(context, new([], true));
            check(context.Policy == "registered-install" && plan.Files.Count == 1 && plan.Keys.Count == 0 && plan.Preserved > 0,
                "W12 sem receita encontra arquivo dedicado mas nao adota chave apenas pelo nome");
            using (var key = hive.OpenSubKey(keyPath, true)) key!.SetValue("InstallPath", root);
            context = IntegratedResidualCleanup.Prepare(app, new([app], true));
            plan = IntegratedResidualCleanup.Capture(context, new([], true));
            check(plan.Keys.Count == 1 && plan.Files.Count == 1, "W12 chave generica exige caminho de instalacao corroborante antes e depois");
            using (var key = hive.OpenSubKey(keyPath, true)) key!.SetValue("InstallPath", @"C:\Windows");
            var changed = IntegratedResidualCleanup.Capture(context, new([], true));
            check(changed.Keys.Count == 0, "W12 chave que muda de proprietario durante remocao e preservada");
            using (var key = hive.OpenSubKey(keyPath, true)) key!.SetValue("InstallPath", root);
            var other = new ProgramEntry { Name = "Outro produto", InventoryId = "other", InstallLocation = root };
            var shared = IntegratedResidualCleanup.Capture(context, new([other], true));
            check(shared.Eligible == 0, "W12 pasta compartilhada impede tambem adotar chave generica");
            check(!IntegratedResidualPolicy.EligibleFile(Path.Combine(root, "credentials.ini")) &&
                !IntegratedResidualPolicy.EligibleFile(Path.Combine(root, "passwords.cfg")) &&
                IntegratedResidualPolicy.PersonalDirectory(Path.Combine(root, "wallets")),
                "W12 arquivos de credenciais e pastas de carteiras sao preservados");
        }
        finally { hive.DeleteSubKeyTree(keyPath, false); Directory.Delete(root, true); }
    }
    private static async Task ActualWaitCancellation(Action<bool, string> check)
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "SyntheticSilent", "GF-SilentFixture.exe");
        var app = new ProgramEntry { Name = "GF Synthetic Process", InventoryId = "GF-W12-Process" };
        var request = new UninstallRequest(app, new(fixture, "--gf-delay", null));
        var start = new System.Diagnostics.ProcessStartInfo { FileName = fixture, Arguments = "--gf-delay", UseShellExecute = false, CreateNoWindow = true };
        using (var cancel = new CancellationTokenSource())
        using (var prepared = new PreparedUninstaller(request, start, null))
        {
            var task = prepared.LaunchAsync(cancel.Token); cancel.Cancel();
            bool cancelled = false;
            try { await task; } catch (OperationCanceledException) { cancelled = true; }
            check(cancelled, "W12 espera de PE real distingue cancelar de timeout sem matar o fornecedor");
        }
        using (var prepared = new PreparedUninstaller(request, start, null, waitLimit: TimeSpan.FromMilliseconds(100)))
        {
            bool timed = false;
            try { await prepared.LaunchAsync(default); } catch (TimeoutException) { timed = true; }
            check(timed, "W12 limite real de espera retorna indeterminado sem terminar processo");
        }
        await Task.Delay(1200); // apenas o PE sintetico inerte termina naturalmente
    }

}
