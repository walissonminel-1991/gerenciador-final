using GerenciadorAplicativos;
using Microsoft.Win32;

internal static class ResidualMissionTests
{
    internal static void Run(Action<bool, string> check)
    {
        Classification(check);
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
                found.All(e => e.Name != "NivelNaoVarrer") && !partial,
                "Segundo nível encontra produto sob fabricante desconhecido sem percorrer perfil");
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
        check(large.Count == 100 && large.All(r => !r.UninstallConfirmed && r.Findings.All(f => !f.CanRemove)) &&
            clock.Elapsed < TimeSpan.FromSeconds(20),
            "Dez mil evidências e mil instalações: saída limitada e sem autorização de exclusão");
    }

}
