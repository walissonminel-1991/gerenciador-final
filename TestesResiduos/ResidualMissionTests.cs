using GerenciadorAplicativos;
using Microsoft.Win32;

internal static class ResidualMissionTests
{
    internal static void Run(Action<bool, string> check)
    {
        Classification(check);
        Decisions(check);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopped = new ResidualScanBudget(cancelled.Token);
        check(!stopped.Visit() && stopped.Incomplete && stopped.Entries == 0,
            "Cancelamento impede examinar primeiro item");
        var expired = new ResidualScanBudget(timeout: TimeSpan.Zero);
        check(!expired.Visit() && expired.Incomplete, "Timeout zero encerra sem leitura");
        var capped = new ResidualScanBudget(maxEntries: 2);
        check(capped.Visit() && capped.Visit() && !capped.Visit() && capped.Entries == 2 && capped.Incomplete,
            "Orçamento global limita enumerações de fontes diferentes");
        check(!ResidualReadSafety.IsLocalAbsolute("relative/path") &&
            !ResidualReadSafety.IsLocalAbsolute(@"\\server\share\Folder") &&
            !ResidualReadSafety.IsLocalAbsolute(@"\\?\C:\Folder") &&
            !ResidualReadSafety.IsLocalAbsolute(Path.Combine(Path.GetTempPath(), "..", "Folder")),
            "Leitura recusa caminho relativo, UNC, dispositivo e traversal");
        if (OperatingSystem.IsWindows())
        {
            check(!ResidualReadSafety.IsLocalAbsolute(@"C:\Folder\file:stream"), "ADS recusado");
            check(BroadResidualDiscovery.ExecutableName("\"C:\\Apps\\LegacyEditor.exe\" --arg") == "LegacyEditor" &&
                BroadResidualDiscovery.ExecutableName(@"C:\Apps\LegacyEditor.exe --arg") == "LegacyEditor",
                "Inicialização correlaciona executável absoluto sem executar argumentos");
            check(BroadResidualDiscovery.ExecutableName("powershell -command anything") is null &&
                BroadResidualDiscovery.ExecutableName(@"\\server\share\LegacyEditor.exe") is null &&
                BroadResidualDiscovery.ExecutableName("\"C:\\Apps\\LegacyEditor.exe\"evil") is null,
                "Comandos de shell, rede e aspas ambíguas não viram identidade");
        }
        string temp = Path.Combine(Path.GetTempPath(), "GF-Mission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            string vendor = Path.Combine(temp, "Fabricante Desconhecido");
            string product = Path.Combine(vendor, "LegacyEditor");
            string third = Path.Combine(product, "NivelNaoVarrer");
            Directory.CreateDirectory(third);
            File.WriteAllText(Path.Combine(product, "playlist.m3u"), "dados ficticios");
            var found = new List<BroadResidualEvidence>();
            int entries = 0, restricted = 0;
            bool partial = false;
            BroadResidualDiscovery.InspectDirectory(temp, "Dados", "AppData", found,
                ref entries, ref restricted, ref partial, new ResidualScanBudget());
            check(found.Any(e => e.Name == "LegacyEditor" && e.Container == "Fabricante Desconhecido") &&
                found.All(e => e.Name != "NivelNaoVarrer") && partial,
                "Segundo nível encontra produto sob fabricante desconhecido informa profundidade limitada sem percorrer perfil");
            check(File.Exists(Path.Combine(product, "playlist.m3u")), "Descoberta preserva playlist");
            var many = Path.Combine(temp, "MuitosProdutos");
            Directory.CreateDirectory(many);
            for (int i = 0; i < 150; i++) Directory.CreateDirectory(Path.Combine(many, "Produto" + i));
            found.Clear(); entries = 0; restricted = 0; partial = false;
            var smallBudget = new ResidualScanBudget(maxEntries: 30);
            BroadResidualDiscovery.InspectDirectory(temp, "Dados", "AppData", found,
                ref entries, ref restricted, ref partial, smallBudget);
            check(partial && smallBudget.Entries <= 30 && entries <= 30,
                "Pastas extensas interrompem com resultado parcial e contagem limitada");
            found.Clear(); entries = 0; restricted = 0; partial = false;
            BroadResidualDiscovery.InspectDirectory(temp + "-ausente", "Dados", "AppData", found,
                ref entries, ref restricted, ref partial, new ResidualScanBudget());
            check(partial && restricted == 1 && found.Count == 0, "Raiz inacessível/ausente não equivale a ausência de resíduos");
            string linked = Path.Combine(temp, "VinculoFabricante");
            Directory.CreateSymbolicLink(linked, vendor);
            found.Clear(); entries = 0; restricted = 0; partial = false;
            BroadResidualDiscovery.InspectDirectory(linked, "Dados", "AppData", found,
                ref entries, ref restricted, ref partial, new ResidualScanBudget());
            check(partial && found.Count == 0 && !ResidualReadSafety.IsUnlinkedDirectory(Path.Combine(linked, "LegacyEditor")),
                "Link e ancestral redirecionado recusados na leitura");
            Directory.Delete(linked); // somente link sintético
        }
        finally { Directory.Delete(temp, true); }

        if (!OperatingSystem.IsWindows()) return;
        PreviewFiles(check).GetAwaiter().GetResult();
        string keyPath = @"Software\GF_Mission_" + Guid.NewGuid().ToString("N");
        try
        {
            using (var root = Registry.CurrentUser.CreateSubKey(keyPath))
            using (var product = root.CreateSubKey(@"Fabricante Exemplo\LegacyEditor"))
                product.SetValue("Preferencias", "ficticias");
            var found = new List<BroadResidualEvidence>();
            int entries = 0, restricted = 0; bool partial = false;
            BroadResidualDiscovery.InspectRegistryHive(RegistryHive.CurrentUser, RegistryView.Default,
                "HKEY_CURRENT_USER", found, ref entries, ref restricted, ref partial,
                new ResidualScanBudget(), keyPath);
            check(found.Any(e => e.Name == "LegacyEditor" && e.Container == "Fabricante Exemplo") && !partial,
                "Registro: produto em subchave de fabricante encontrado somente leitura");
            using var stillThere = Registry.CurrentUser.OpenSubKey(keyPath + @"\Fabricante Exemplo\LegacyEditor");
            check((string?)stillThere?.GetValue("Preferencias") == "ficticias", "Valores do Registro permanecem intactos");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); }
    }
    private static void Classification(Action<bool, string> check)
    {
        // Regressao PC fraco: Steam\\config e WinHance\\Config nao sao
        // o mesmo programa; pastas genericas ficam sob seu proprietario.
        check(!BroadResidualDiscovery.EligibleName("Config") &&
            !BroadResidualDiscovery.EligibleName("Logs") &&
            !BroadResidualDiscovery.EligibleName("Updates") &&
            !BroadResidualDiscovery.EligibleName("Scripts") &&
            !BroadResidualDiscovery.EligibleName("Providers") &&
            BroadResidualDiscovery.ProductFromChild("Steam", "config") == "Steam" &&
            BroadResidualDiscovery.ProductFromChild("WinHance", "Config") == "WinHance" &&
            BroadResidualDiscovery.ProductFromChild("Empresa Exemplo", "EditorReal") == "EditorReal",
            "Subpastas genericas nao se tornam candidatos de produtos distintos");
        var mixedGeneric = BroadResidualDiscovery.Classify(new[]
        {
            new BroadResidualEvidence(BroadResidualDiscovery.ProductFromChild("Steam", "config")!, @"C:\\Apps\\Steam\\config", "Dados", "AppData", "Steam"),
            new BroadResidualEvidence("Steam", @"HKCU\\Software\\Steam", "Configuração", "Registry"),
            new BroadResidualEvidence(BroadResidualDiscovery.ProductFromChild("WinHance", "Config")!, @"C:\\Apps\\WinHance\\Config", "Dados", "AppData", "WinHance"),
            new BroadResidualEvidence("WinHance", @"HKCU\\Software\\WinHance", "Configuração", "Registry")
        }, []);
        check(mixedGeneric.Count == 2 && mixedGeneric.Any(r => r.Product == "Steam") &&
            mixedGeneric.Any(r => r.Product == "WinHance") &&
            mixedGeneric.All(r => r.Findings.All(f => !f.CanRemove)),
            "Configuracoes homonimas de aplicativos distintos nunca se fundem ou habilitam exclusao");
        check(HistoricalResidualAudit.Rules.Count == 5 && HistoricalResidualAudit.Rules.Any(r => r.Id == "vlc-profile"),
            "Catálogo histórico valida nomes curtos VLC e carrega todas as cinco regras");
        var app = new ProgramEntry { Name = "Editor Antigo", MsiProductCode = "{ID-SINTETICO}" };
        check(UninstallResiduals.StillInstalled(app, new[] { new ProgramEntry
            { Name = "Nome Novo", MsiProductCode = "{ID-SINTETICO}" } }),
            "Identificador MSI preserva instalação sob nome diferente");
        var sameLocation = new ProgramEntry { Name = "Nova Versão", InstallLocation = Path.Combine(Path.GetTempPath(), "Produto") };
        check(UninstallResiduals.StillInstalled(new ProgramEntry { Name = "Antiga Versão", InstallLocation = sameLocation.InstallLocation },
            new[] { sameLocation }), "Reinstalação ou variante no mesmo local bloqueia confirmação de ausência");
        check(!ResidualReadSafety.PathsOverlap(Path.Combine(Path.GetTempPath(), "Produto"),
            Path.Combine(Path.GetTempPath(), "ProdutoOutro")), "Prefixo de pasta semelhante não equivale a ancestral compartilhado");
        var data = new ResidualAuditFinding(@"C:\Fake\Profile", "Perfil", 1, "Playlists e preferências", ResidualDisposition.PreserveData);
        var shared = data with { Disposition = ResidualDisposition.PreserveShared, Evidence = "Mais de um produto" };
        var merged = ResidualReportMerger.Merge(new[]
        {
            new ResidualAuditReport("Editor x64", false, new[] { data }, []),
            new ResidualAuditReport("Editor", false, new[] { shared, data with { Location = @"HKCU\Software\Editor" } }, [])
        });
        check(merged.Count == 1 && merged[0].Findings.Count == 2 &&
            merged[0].Findings[0].Disposition == ResidualDisposition.PreserveShared &&
            merged[0].Findings[0].Evidence.Contains("Playlists") && merged[0].Findings[0].Evidence.Contains("Mais de um"),
            "União de fontes preserva locais únicos, motivos e decisão mais restritiva");
        var conflict = ResidualReportMerger.Merge(new[]
        {
            new ResidualAuditReport("AppA", false, new[] { data }, []),
            new ResidualAuditReport("AppB", false, new[] { data }, [])
        });
        check(conflict.SelectMany(r => r.Findings).All(f => f.Disposition == ResidualDisposition.PreserveShared && !f.CanRemove),
            "Mesmo alvo ligado a produtos diferentes é compartilhado e bloqueado");
        check(ResidualReportMerger.Merge(merged, incomplete: true).All(r => r.Incomplete &&
            r.Findings.All(f => f.Disposition == ResidualDisposition.Unknown && !f.CanRemove)),
            "Inventário parcial produz estado desconhecido e proíbe exclusão em todas as fontes");
        check(!new ResidualAuditFinding("ficticio", "Teste", 1, "pontuação", ResidualDisposition.ValidatedCandidate).CanRemove,
            "Nem categoria validada nem pontuação concede capacidade de apagar");
        var media = new[]
        {
            new BroadResidualEvidence("foobar2000", @"C:\Fake\foobar2000", "Perfil", "AppData"),
            new BroadResidualEvidence("foobar2000", @"HKCU\Software\foobar2000", "Configurações", "Registry"),
            new BroadResidualEvidence("Future Games", @"C:\Fake\Future Games", "Perfil", "AppData"),
            new BroadResidualEvidence("Future Games", @"HKCU\Software\Future Games", "Chave", "Registry")
        };
        var classified = BroadResidualDiscovery.Classify(media, []);
        check(classified.Single(r => r.Product == "foobar2000").Findings.Any(f => f.Disposition == ResidualDisposition.PreserveData) &&
            classified.Single(r => r.Product == "Future Games").Findings.All(f => f.Disposition == ResidualDisposition.PreserveShared),
            "Perfil de mídia e fabricante desconhecido recebem preservação contextual");
        check(BroadResidualDiscovery.Classify(media, [], inventoryIncomplete: true)
            .SelectMany(r => r.Findings).All(f => f.Disposition == ResidualDisposition.Unknown),
            "Leitura parcial impede classificar perfis como removíveis");
        var many = Enumerable.Range(0, 5000).SelectMany(i => new[]
        {
            new BroadResidualEvidence("Produto" + i, "Local" + i, "Dados", "AppData"),
            new BroadResidualEvidence("Produto" + i, "Registro" + i, "Chave", "Registry")
        }).ToArray();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var large = BroadResidualDiscovery.Classify(many, Enumerable.Range(0, 1000)
            .Select(i => new ProgramEntry { Name = "Instalado" + i, Publisher = "Fabricante" + i }).ToArray());
        check(large.Count == 5000 && large.All(r => !r.UninstallConfirmed && r.Findings.All(f => !f.CanRemove)) &&
            clock.Elapsed < TimeSpan.FromSeconds(20),
            "Dez mil evidências e mil instalações: todos os grupos preservados e sem autorização de exclusão");
    }

    private static void Decisions(Action<bool, string> check)
    {
        var ready = new ResidualDecisionContext(true, true, true, false, true, true, true, true, RelatedIntegrationsChecked: true);
        check(ResidualPreview.Decide(ready).SimulationEligible && !ResidualPreview.Decide(ready).CanExecute,
            "Mesmo pré-condições completas habilitam apenas simulação");
        var unsafeContexts = new[]
        {
            ready with { InventoryComplete = false }, ready with { LauncherExited = false },
            ready with { AuxiliaryProcessesFinished = false }, ready with { ActiveResources = true },
            ready with { ExclusiveOwnership = false }, ready with { ExplicitPolicy = false },
            ready with { SpecificConsent = false }, ready with { SnapshotUnchanged = false },
            ready with { PersonalOrSharedData = true }, ready with { RelatedIntegrationsChecked = false }, ready with { Reinstalled = true }
        };
        check(unsafeContexts.All(c => !ResidualPreview.Decide(c).SimulationEligible && !ResidualPreview.Decide(c).CanExecute),
            "Inventário parcial, interrupção, auxiliar ativo, reinstalação, dados compartilhados ou mudança bloqueiam decisão");
        var removedRegistryBeforeExit = ready with { LauncherExited = false, AuxiliaryProcessesFinished = false };
        check(!ResidualPreview.Decide(removedRegistryBeforeExit).SimulationEligible,
            "WinMerge: registro ausente antes do término do fabricante não autoriza limpeza");
    }

    private static async Task PreviewFiles(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "GF-Preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = Path.Combine(root, "LegacyEditor.exe");
            File.WriteAllText(file, "dados ficticios A");
            var candidate = UninstallResiduals.InspectDirectory("LegacyEditor", root, new[] { "LegacyEditor.exe" })!;
            var preview = await ResidualPreview.CaptureAsync(candidate);
            check(preview.Complete && preview.Files.Count == 1 && await ResidualPreview.IsUnchangedAsync(preview),
                "Prévia Windows registra identidade, link único e SHA-256 sem exclusão");
            var written = File.GetLastWriteTimeUtc(file);
            File.WriteAllText(file, "dados ficticios B");
            File.SetLastWriteTimeUtc(file, written);
            check(!await ResidualPreview.IsUnchangedAsync(preview),
                "Conteúdo alterado com tamanho e timestamp iguais invalida prévia");
            preview = await ResidualPreview.CaptureAsync(candidate);
            File.WriteAllText(Path.Combine(root, "novo.exe"), "novo arquivo");
            check(!await ResidualPreview.IsUnchangedAsync(preview), "Arquivo surgido após prévia exige nova análise");
            File.Delete(Path.Combine(root, "novo.exe"));
            preview = await ResidualPreview.CaptureAsync(candidate);
            File.Move(file, file + ".old");
            File.WriteAllText(file, "dados ficticios B");
            File.SetLastWriteTimeUtc(file, written);
            check(!await ResidualPreview.IsUnchangedAsync(preview), "Substituição no mesmo caminho invalida identidade");
            File.Delete(file + ".old");
            using (var inUse = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                check(!(await ResidualPreview.CaptureAsync(candidate)).Complete, "Arquivo em uso ou leitura negada preservado");
            var mutablePaths = new List<string> { file };
            var copied = await ResidualPreview.CaptureAsync(candidate with { Files = mutablePaths });
            mutablePaths.Clear();
            check(copied.Candidate.Files.Count == 1 && await ResidualPreview.IsUnchangedAsync(copied),
                "Prévia copia alvos e não muda com lista original do chamador");
            check(!(await ResidualPreview.CaptureAsync(candidate with { Files = new[] { file, file } })).Complete,
                "Caminhos duplicados não forjam identidade da prévia");
            string unknown = Path.Combine(root, "playlist.m3u");
            File.WriteAllText(unknown, "playlist fictícia");
            var mixed = UninstallResiduals.InspectDirectory("LegacyEditor", root, new[] { "LegacyEditor.exe" })!;
            check(!(await ResidualPreview.CaptureAsync(mixed)).Complete &&
                !(await ResidualPreview.CaptureAsync(candidate)).Complete && File.Exists(unknown),
                "Pastas com dados misturados ou arquivo novo antes da captura permanecem desconhecidas");
            File.Delete(unknown);
            bool renameBlocked = false;
            using (var held = InstallerFileGuard.OpenRead(file, allowDirectoryWrites: true))
            {
                try { File.Move(file, file + ".rename"); } catch (IOException) { renameBlocked = true; }
            }
            check(renameBlocked && File.Exists(file), "Handle de leitura impede substituição enquanto identidade é conferida");
            string hardlink = Path.Combine(root, "SegundaReferencia.exe");
            check(CreateHardLink(hardlink, file, IntPtr.Zero), "Criar hard link apenas no fixture temporário");
            check(!(await ResidualPreview.CaptureAsync(candidate)).Complete,
                "Hard link bloqueia validação de exclusividade do arquivo");
            File.Delete(hardlink);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            check(!(await ResidualPreview.CaptureAsync(candidate, cancelled.Token)).Complete &&
                !await ResidualPreview.IsUnchangedAsync(preview, cancelled.Token), "Cancelamento invalida captura e revalidação");
            preview = await ResidualPreview.CaptureAsync(candidate);
            File.Delete(file);
            check(!await ResidualPreview.IsUnchangedAsync(preview), "Alvo ausente depois da análise não gera exclusão substituta");
        }
        finally { Directory.Delete(root, true); }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newName, string existingName, IntPtr attributes);

}
