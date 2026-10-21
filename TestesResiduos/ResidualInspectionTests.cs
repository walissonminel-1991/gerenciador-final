using GerenciadorAplicativos;
using Microsoft.Win32;

internal static class ResidualInspectionTests
{
    internal static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "GF-Inspect-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "Editor Antigo");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "playlist.m3u"), "dados fictícios");
            Directory.CreateDirectory(Path.Combine(folder, "Componentes", "Perfil", "NivelExtra"));
            var report = new ResidualAuditReport("Editor Antigo", false, [new(folder, "Perfil", 1, "preservar")], []);
            var session = new ResidualInspectionSession(report, [root]);
            var result = session.Inspect(folder);
            check(result.Entries.Count == 2 && !result.Coverage.Incomplete && !result.CanExecute &&
                result.Entries.All(e => e.Disposition == ResidualDisposition.PreserveData), "Inspeção sob demanda enumera somente metadados locais e preserva dados");
            check(session.Inspect(root).Coverage.Incomplete && session.Inspect(folder + "Outro").Coverage.Incomplete,
                "Inspeção recusa locais digitados, raiz e prefixos fora da fotografia");
            var second = session.Inspect(result.Entries.Single(e => e.Container).Location);
            check(second.Entries.Count == 1 && session.Inspect(second.Entries.Single().Location).Coverage.Issues.HasFlag(ResidualScanIssue.DepthLimit),
                "Navegação descoberta tem limite de profundidade global por sessão");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            check(session.Inspect(folder, cancelled.Token).Coverage.Issues.HasFlag(ResidualScanIssue.Cancelled), "Inspeção cancelada não vira leitura concluída");
            check(session.Inspect(folder, timeout: TimeSpan.Zero).Coverage.Issues.HasFlag(ResidualScanIssue.Timeout), "Inspeção com tempo esgotado preserva parcialidade");
            check(session.Inspect(folder, maximumEntries: 1).Entries.Count == 1 && session.Inspect(folder, maximumEntries: 1).Coverage.Incomplete,
                "Limite sob demanda não é confundido com inspeção completa");
            check(session.Inspect(Path.Combine(folder, "playlist.m3u")).Coverage.Incomplete,
                "Arquivos encontrados não viram alvos executáveis de novas operações");
            string linked = Path.Combine(folder, "Vinculo");
            Directory.CreateSymbolicLink(linked, Path.Combine(folder, "Componentes"));
            var linkedResult = session.Inspect(folder);
            check(linkedResult.Coverage.Issues.HasFlag(ResidualScanIssue.LinkSkipped) && linkedResult.Entries.All(e => e.Name != "Vinculo"),
                "Inspeção não segue links nem os inclui como contêineres navegáveis");
            Directory.Delete(linked);
            var protectedTarget = new ResidualAuditReport("Fictício", false,
                [new(Path.GetPathRoot(root)!, "Raiz protegida", 1, "fora do escopo")], []);
            check(new ResidualInspectionSession(protectedTarget, [root]).Inspect(Path.GetPathRoot(root)!).Coverage.Incomplete,
                "Inspeção recusa raiz protegida mesmo quando recebida em relatório sintético");
            check(File.ReadAllText(Path.Combine(folder, "playlist.m3u")) == "dados fictícios", "Playlist de fixture permanece intacta após inspeções");
        }
        finally { Directory.Delete(root, true); }
        PreservationReasons(check);
        if (!OperatingSystem.IsWindows()) return;
        string key = @"Software\GF_Inspection_" + Guid.NewGuid().ToString("N");
        try
        {
            using (var created = Registry.CurrentUser.CreateSubKey(key))
            using (var child = created.CreateSubKey("Perfil")) child.SetValue("Privado", "CONTEUDO_FICTICIO_NAO_LER");
            var report = new ResidualAuditReport("Fictício", false,
                [new(@"HKEY_CURRENT_USER\" + key, "Preferências", 1, "preservar")], []);
            var registrySession = new ResidualInspectionSession(report);
            var result = registrySession.Inspect(report.Findings.Single().Location);
            check(result.Entries.Count == 1 && result.Entries[0].Name == "Perfil" && !result.Coverage.Incomplete &&
                !result.Explanation.Contains("CONTEUDO_FICTICIO"), "Inspeção nativa do Registro enumera somente nomes de subchaves fictícias");
            using var preserved = Registry.CurrentUser.OpenSubKey(key + @"\Perfil");
            check((string?)preserved?.GetValue("Privado") == "CONTEUDO_FICTICIO_NAO_LER", "Valor de fixture do Registro permanece intacto após inspeção");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(key, false); }
    }
    private static void PreservationReasons(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "GF-Preserve-" + Guid.NewGuid().ToString("N"));
        string outside = root + "-outside";
        var names = new (string Name, bool Container, ResidualPreservationKind Kind)[]
        {
            ("places.sqlite", false, ResidualPreservationKind.BrowserHistoryAndBookmarks),
            ("favicons.sqlite", false, ResidualPreservationKind.BrowserHistoryAndBookmarks),
            ("Bookmarks", false, ResidualPreservationKind.BrowserHistoryAndBookmarks),
            ("History", false, ResidualPreservationKind.BrowserHistoryAndBookmarks),
            ("cookies.sqlite", false, ResidualPreservationKind.BrowserCookies),
            ("Cookies", false, ResidualPreservationKind.BrowserCookies),
            ("logins.json", false, ResidualPreservationKind.BrowserCredentials),
            ("key4.db", false, ResidualPreservationKind.BrowserCredentials),
            ("Login Data", false, ResidualPreservationKind.BrowserCredentials),
            ("sessionstore.jsonlz4", false, ResidualPreservationKind.BrowserSession),
            ("sessionstore-backups", true, ResidualPreservationKind.BrowserSession),
            ("bookmarkbackups", true, ResidualPreservationKind.BrowserHistoryAndBookmarks),
            ("Profiles", true, ResidualPreservationKind.BrowserProfile),
            ("User Data", true, ResidualPreservationKind.BrowserProfile),
            ("cache2", true, ResidualPreservationKind.Cache),
            ("Cache", true, ResidualPreservationKind.Cache),
            ("Backup", true, ResidualPreservationKind.Backup),
            ("Backups", true, ResidualPreservationKind.Backup),
            ("MobileSync", true, ResidualPreservationKind.Backup),
            ("Saved Games", true, ResidualPreservationKind.SavedGames),
            ("Savegames", true, ResidualPreservationKind.SavedGames),
            ("Saves", true, ResidualPreservationKind.SavedGames),
            ("favoritos.m3u", false, ResidualPreservationKind.Playlist),
            ("favoritos.m3u8", false, ResidualPreservationKind.Playlist),
            ("favoritos.pls", false, ResidualPreservationKind.Playlist),
            ("favoritos.fpl", false, ResidualPreservationKind.Playlist),
            ("favoritos.xspf", false, ResidualPreservationKind.Playlist),
            ("reparo.msi", false, ResidualPreservationKind.Installer),
            ("reparo.msp", false, ResidualPreservationKind.Installer),
            ("desconhecido.bak", false, ResidualPreservationKind.Unknown),
            ("places.sqlite.copy", false, ResidualPreservationKind.Unknown),
            ("Cookies.txt", false, ResidualPreservationKind.Unknown),
            ("sessionstore-unrelated", true, ResidualPreservationKind.Unknown),
            ("CacheHelper", true, ResidualPreservationKind.Unknown)
        };
        string[] profiles =
        [
            Path.Combine(root, "Perfis alternativos", "perfil-ficticio.default-release"),
            Path.Combine(root, "Packages", "Mozilla.Firefox_Fixture", "LocalCache", "Roaming",
                "Mozilla", "Firefox", "Profiles", "outro-perfil-ficticio")
        ];
        var originalFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            foreach (string profile in profiles)
            {
                Directory.CreateDirectory(profile);
                foreach (var sample in names)
                {
                    string target = Path.Combine(profile, sample.Name);
                    if (sample.Container) Directory.CreateDirectory(target);
                    else
                    {
                        byte[] content = System.Text.Encoding.UTF8.GetBytes("CONTEUDO_SINTETICO_NAO_EXIBIR:" + sample.Name);
                        File.WriteAllBytes(target, content); originalFiles.Add(target, content);
                    }
                }
                // Dados pessoais ao lado de caches e conteúdo dentro de backups/saves
                // continuam protegidos; a consulta não precisa abrir nenhum arquivo.
                foreach (string subfolder in new[] { "cache2", "Backup", "Saves", "bookmarkbackups", "sessionstore-backups" })
                {
                    string file = Path.Combine(profile, subfolder, "fixture.bin");
                    byte[] content = [0, 4, 8, 16, 255];
                    File.WriteAllBytes(file, content); originalFiles.Add(file, content);
                }
            }
            var report = new ResidualAuditReport("Perfil sintético", false,
                profiles.Select(p => new ResidualAuditFinding(p, "Perfil", 1, "preservar")).ToArray(), []);
            var session = new ResidualInspectionSession(report, [root]);
            ResidualInspectionResult[] results;
            using (var lockedPersonalFile = new FileStream(Path.Combine(profiles[0], "places.sqlite"),
                FileMode.Open, FileAccess.Read, FileShare.None))
                results = profiles.Select(p => session.Inspect(p)).ToArray();
            check(results.All(r => !r.Coverage.Incomplete && r.Entries.Count == names.Length && !r.CanExecute),
                "Perfis alternativo e MSIX sintéticos são consultados por metadados mesmo com base pessoal bloqueada para leitura");
            check(results.All(r => names.All(expected => r.Entries.Any(e => e.Name == expected.Name &&
                    e.Container == expected.Container && e.PreservationKind == expected.Kind))),
                "Inspeção distingue bases pessoais, sessões, perfis, backups, saves, playlists, instaladores e caches");
            check(results.SelectMany(r => r.Entries).All(e => e.Disposition == ResidualDisposition.PreserveData &&
                    !string.IsNullOrWhiteSpace(e.PreservationExplanation)),
                "Todo motivo, inclusive cache e desconhecido, continua uma decisão de preservação");
            check(results.SelectMany(r => r.Entries).Where(e => e.PreservationKind == ResidualPreservationKind.Unknown)
                    .All(e => e.Disposition == ResidualDisposition.PreserveData) &&
                results.SelectMany(r => r.Entries).All(e => !e.PreservationExplanation.Contains("CONTEUDO_SINTETICO")),
                "Nomes parecidos não comprovam categoria nem expõem conteúdo de arquivos");
            check(results.All(r => r.Entries.Where(e => !e.Container).All(e => !e.CanInspect)),
                "Arquivo pessoal identificado permanece não navegável e não vira operação adicional");
            var nested = session.Inspect(results[0].Entries.Single(e => e.Name == "cache2").Location);
            check(nested.Entries.Single().Name == "fixture.bin" &&
                nested.Entries.Single().PreservationKind == ResidualPreservationKind.Unknown &&
                nested.Entries.Single().Disposition == ResidualDisposition.PreserveData,
                "Cache não transfere a seus filhos uma hipótese de conteúdo descartável");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var partial = session.Inspect(profiles[0], maximumEntries: 1);
            check(partial.Coverage.Incomplete && partial.Entries.Count == 1 &&
                partial.Entries.All(e => e.Disposition == ResidualDisposition.PreserveData) &&
                session.Inspect(profiles[1], cancelled.Token).Coverage.Issues.HasFlag(ResidualScanIssue.Cancelled),
                "Motivos de preservação não removem limites nem transformam cancelamento em inspeção completa");

            Directory.CreateDirectory(outside);
            string sentinel = Path.Combine(outside, "NAO_ENUMERAR_FORA_DO_ESCOPO.txt");
            File.WriteAllText(sentinel, "sentinela fictícia");
            string link = Path.Combine(profiles[0], "PerfilRedirecionado");
            Directory.CreateSymbolicLink(link, outside);
            try
            {
                var linked = session.Inspect(profiles[0]);
                check(linked.Coverage.Issues.HasFlag(ResidualScanIssue.LinkSkipped) &&
                    linked.Entries.All(e => e.Name != "PerfilRedirecionado" && e.Name != Path.GetFileName(sentinel)) &&
                    session.Inspect(sentinel).Coverage.Incomplete,
                    "Perfil redirecionado é recusado e não inclui metadados do alvo externo na fotografia");
            }
            finally { Directory.Delete(link); }
            check(originalFiles.All(f => File.ReadAllBytes(f.Key).SequenceEqual(f.Value)) &&
                File.ReadAllText(sentinel) == "sentinela fictícia",
                "Bases de navegador, backups, saves, playlists, fontes MSI e alvo externo permanecem byte a byte intactos");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(outside)) Directory.Delete(outside, true);
        }
        check(!Directory.Exists(root) && !Directory.Exists(outside),
            "Fixtures de preservação e alvo externo são removidos ao terminar o teste");
    }

}
