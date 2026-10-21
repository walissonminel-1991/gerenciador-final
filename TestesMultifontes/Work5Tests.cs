using System.Diagnostics;
using System.Net;
using GerenciadorAplicativos;

internal static class Work5Tests
{
    internal static async Task Run(Action<bool, string> check)
    {
        // Versões instaladas INVENTADAS; não é reprodução do inventário privado dos PCs.
        InstalledApp[] apps = [new("7-Zip 25.00 (x64)", "25.00", "Igor Pavlov"),
            new("Outlook for Windows", "1.2026.720.100", "Microsoft Corporation"),
            new("Proton Pass", "1.41.0", "Proton AG"),
            new("EA app", "13.800.0.6000", "Electronic Arts"),
            new("FanControl", "283", "Remi Mercier Software Inc"),
            new("Jellyfin Server", "12.1", "The Jellyfin Project"),
            new("K-Lite Mega Codec Pack 20.0.0", "20.0.0", "KLCP"),
            new("Microsoft GameInput", "3.5.270.0", "Microsoft Corporation"),
            new("Plex Media Server", "1.43.4.10903", "Plex, Inc.")];
        OfficialReleaseSources.ClearCacheForTests();
        var fixture = new NineFixture();
        using var source = new OfficialReleaseSources(fixture);
        var progress = new Recorder();
        var timer = Stopwatch.StartNew();
        var scan = await source.QueryAsync(apps, default, progress: progress);
        long cold = timer.ElapsedMilliseconds;
        var results = MultiSourceMatcher.Merge(apps, null, scan.Findings);
        check(results.Count == 9 && results.All(r => r.State == MatchState.Candidate),
            "Nove identidades sintéticas consultadas; duas fontes de Proton não duplicam a instalação");
        check(scan.Findings.Count == 10 && progress.Values.Count == 10 &&
            progress.Values.All(d => d.Status == "ok"), "Progresso conta conclusões reais de cada fonte HTTPS");
        check(results[2].Catalog?.ProductVersion == "1.43.0" && results[2].HasSourceConflict &&
            results[2].RequiresReview && results[2].Observations.Count == 2 &&
            results[2].EvidenceDetails.Contains("1.42.0") && results[2].EvidenceDetails.Contains("1.43.0"),
            "Proton mantém changelog 1.43.0 e índice 1.42.0, bloqueando decisão de instalação");
        check(results[4].Catalog?.ProductVersion == "284" && Matching.CompareVersions("V283", "284") < 0 &&
            Matching.CompareVersions("284", "284.0") == 0 && Matching.CompareVersions("284beta", "285") is null,
            "FanControl aceita revisão inteira e rejeita sufixo beta sem parsing tabular");
        var reverse = MultiSourceMatcher.Merge(apps, null, scan.Findings.Reverse().Concat(scan.Findings).ToArray());
        check(results.Zip(reverse).All(pair => pair.First.Catalog?.ProductVersion == pair.Second.Catalog?.ProductVersion &&
            pair.First.EvidenceDetails == pair.Second.EvidenceDetails),
            "Deduplicação e explicação independem de ordem e repetição das fontes");
        timer.Restart();
        await source.QueryAsync(apps, default);
        check(fixture.Requests == 10, "Consulta repetida usa cache de versões públicas, sem armazenar inventário");
        Console.WriteLine($"WORK5_FIXTURE consulta_fria_ms={cold} repetida_ms={timer.ElapsedMilliseconds}");
        var changed = apps.Select(a => a with { }).ToArray();
        check(MultiSourceMatcher.Merge(changed, null, scan.Findings).All(m => m.State == MatchState.Unmatched),
            "Observações antigas não passam a outro snapshot mesmo com registros iguais");
        var fresh = await source.QueryAsync(changed, default);
        check(MultiSourceMatcher.Merge(changed, null, fresh.Findings).Count(m => m.State == MatchState.Candidate) == 9 && fixture.Requests == 10,
            "Novo inventário reconstrói associações; cache reutiliza somente versão pública");

        var wrong = new[] { apps[3] with { Publisher = "Outro" }, apps[4] with { Name = "FanControl Pro" },
            apps[5] with { Name = "Jellyfin Media Player" }, apps[6] with { Name = "K-Lite Standard Codec Pack" },
            apps[8] with { Name = "Plex" }, apps[4] with { Channel = "preview" },
            apps[5] with { EligibleForUpdates = false } };
        var unsafeScan = await source.QueryAsync(wrong, default);
        check(!unsafeScan.Findings.Any(f => wrong.Take(5).Any(a => ReferenceEquals(a, f.Installed))),
            "Fabricante, edição Mega, servidor e nomes genéricos não concedem correspondência");
        check(MultiSourceMatcher.Merge(wrong, null, unsafeScan.Findings).All(m => m.State == MatchState.Unmatched),
            "Canal preview e somente inventário não viram candidata estável");
        foreach (var dimension in new[] { "x86", "arm64", "scope", "type", "channel" })
        {
            var app = apps[4] with { Architecture = dimension is "x86" or "arm64" ? dimension : "unknown",
                Scope = dimension == "scope" ? "user" : "unknown",
                Channel = dimension == "channel" ? "preview" : "unknown",
                InstallType = dimension == "type" ? "portable" : "unknown" };
            var release = new OfficialRelease(app, "284", "fixture", "", Architecture: "x64",
                Scope: "machine", Channel: "stable", InstallType: "msix");
            var blocked = MultiSourceMatcher.Merge([app], null, [release]).Single();
            check(blocked.State == MatchState.Unmatched && blocked.RequiresReview,
                "Dimensão incompatível isolada preservada para revisão: " + dimension);
        }
        var badIdentity = new OfficialRelease(apps[4], "284", "fixture", "", CanonicalId: "ea-desktop");
        check(MultiSourceMatcher.Merge([apps[4]], null, [badIdentity]).Single().State == MatchState.Unmatched,
            "Mesmo nome e versão não superam ID canônico de outro produto");
        var oldServer = apps[5] with { Version = "10.11.0" };
        var serverScan = await source.QueryAsync([oldServer], default);
        check(MultiSourceMatcher.Merge([oldServer], null, serverScan.Findings).Single() is
            { State: MatchState.DifferentMajor, RequiresReview: true }, "Migração principal Jellyfin requer revisão, sem receita automática");
        var twins = new[] { apps[4] with { Architecture = "x86", Scope = "user" }, apps[4] with { Architecture = "arm64", Scope = "machine" } };
        var twinScan = await source.QueryAsync(twins, default);
        check(MultiSourceMatcher.Merge(twins, null, twinScan.Findings).Count == 2,
            "Instalações lado a lado permanecem distintas mesmo com identidade canônica comum");
        check(OfficialReleaseSources.CoverageStage(apps[7], "gameinput-winget", scan, results[7]) == 9 &&
            OfficialReleaseSources.CoverageStage(apps[7] with { EligibleForUpdates = false }, "gameinput-winget", scan, results[7]) == 2 &&
            OfficialReleaseSources.CoverageStage(apps[7] with { Publisher = "Outro" }, "gameinput-winget", scan, results[7]) == 3,
            "GameInput discrimina candidato, inelegibilidade e identidade sem inferir causa por total");
        check(OfficialReleaseSources.CoverageStage(apps[7], "gameinput-winget", new([], []), results[7]) == 4 &&
            OfficialReleaseSources.CoverageStage(apps[7], "gameinput-winget", new([], [new("gameinput-winget", "falha", 0)]), results[7]) == 5,
            "Cobertura diferencia fonte não consultada de fonte indisponível");

        OfficialReleaseSources.ClearCacheForTests();
        using (var partial = new OfficialReleaseSources(new NineFixture(failEa: true)))
        {
            var partialScan = await partial.QueryAsync(apps, default);
            check(partialScan.Findings.Count == 9 && partialScan.Diagnostics.Count(d => d.Status != "ok") == 1,
                "Falha isolada EA preserva as outras nove consultas e declara resultado parcial");
        }
        OfficialReleaseSources.ClearCacheForTests();
        using (var offline = new OfficialReleaseSources(new NineFixture(offline: true)))
        {
            var offlineScan = await offline.QueryAsync(apps, default);
            check(offlineScan.Findings.Count == 0 && offlineScan.Diagnostics.All(d => d.Status != "ok"),
                "Offline simulado retorna indisponibilidade explícita sem afetar inventário");
        }
        var clock = new CacheClock();
        var timedFixture = new NineFixture();
        OfficialReleaseSources.ClearCacheForTests();
        using (var timed = new OfficialReleaseSources(timedFixture, clock: clock))
        {
            timer.Restart();
            var first = await timed.QueryAsync([apps[4]], default);
            long firstMs = timer.ElapsedMilliseconds;
            clock.Advance(TimeSpan.FromMinutes(9));
            var warm = await timed.QueryAsync([apps[4]], default);
            check(timedFixture.Requests == 1 && warm.Findings[0].ObservedAt == first.Findings[0].ObservedAt,
                "Cache preserva instante original da observação, sem fingir consulta nova");
            clock.Advance(TimeSpan.FromMinutes(1));
            timer.Restart();
            var expired = await timed.QueryAsync([apps[4]], default);
            check(timedFixture.Requests == 2 && !expired.Diagnostics[0].UsedMemoryCache &&
                expired.Findings[0].ObservedAt > first.Findings[0].ObservedAt,
                "TTL dez minutos expira exatamente e renova procedência");
            Console.WriteLine($"WORK5_FIXTURE TTL_fria_ms={firstMs} expirada_ms={timer.ElapsedMilliseconds}");
            using var isolated = new OfficialReleaseSources(new NineFixture(offline: true), clock: clock);
            check((await isolated.QueryAsync([apps[4]], default)).Findings.Count == 0,
                "Transportes diferentes não herdam sucesso de cache de outra fonte simulada");
        }
        await AdapterTests(apps, check);
        foreach (var facts in new[] { new WingetContextFacts(false, false, false, false, true),
            new(true, false, true, false, true), new(true, true, false, false, true), new(true, true, true, true, true),
            new(true, true, true, false, false) })
            check(WingetContextPolicy.Classify(facts).Length > 0, "Contexto agregado classificado sem executar nem elevar");
    }

    private static async Task AdapterTests(InstalledApp[] apps, Action<bool, string> check)
    {
        using var production = new WingetConsultativeProvider();
        check(!production.Enabled && !production.TryOptIn(true), "Produção não ativa backend não validado nem com opt-in");
        foreach (var origin in Enum.GetValues<WingetQueryOrigin>())
            check((await production.QueryAsync(apps, origin, default)).State == WingetQueryState.Disabled,
                "WinGet desligado não executa em " + origin);
        var backend = new FakeBackend((_, _) => Task.FromResult(new WingetQueryResult(WingetQueryState.Completed, [])));
        using (var lab = WingetConsultativeProvider.ForSimulation(backend))
        {
            check(!lab.TryOptIn(false) && lab.TryOptIn(true), "Somente sessão de laboratório revisada aceita opt-in explícito");
            foreach (var origin in Enum.GetValues<WingetQueryOrigin>().Where(o => o != WingetQueryOrigin.ExplicitLabTest))
                await lab.QueryAsync(apps, origin, default);
            check(backend.Calls == 0, "Mesmo sessão opt-in não consulta ao iniciar, atualizar lista, pesquisar ou trocar aba");
            check((await lab.QueryAsync(apps, WingetQueryOrigin.ExplicitLabTest, default)).State == WingetQueryState.Completed && backend.Calls == 1,
                "Ação consultiva explícita chama apenas o contrato simulado");
            lab.Stop(WingetStopReason.AntivirusAlert);
            check(!lab.Enabled && !lab.TryOptIn(true), "Alerta bloqueia sessão sem reiniciar, reinstalar ou insistir");
        }
        foreach (var failure in new[] { "ausente", "por_usuario", "alias_quebrado", "so_elevado", "versao_antiga", "COM", "politica", "catalogo", "truncada" })
        {
            var fake = new FakeBackend((_, _) => failure == "COM" ? throw new InvalidOperationException("fixture") :
                Task.FromResult(new WingetQueryResult(WingetQueryState.Unavailable, [])));
            using var lab = WingetConsultativeProvider.ForSimulation(fake); lab.TryOptIn(true);
            check((await lab.QueryAsync(apps, WingetQueryOrigin.ExplicitLabTest, default)).Findings.Count == 0 && !lab.Enabled,
                "Falha simulada desativa provedor e preserva HTTPS: " + failure);
        }
        var waiting = new TaskCompletionSource<WingetQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var slow = WingetConsultativeProvider.ForSimulation(new FakeBackend((_, _) => waiting.Task), TimeSpan.FromMilliseconds(40)))
        {
            slow.TryOptIn(true);
            check((await slow.QueryAsync(apps, WingetQueryOrigin.ExplicitLabTest, default)).State == WingetQueryState.TimedOut && !slow.Enabled,
                "Timeout limita espera mesmo quando fixture ignora token; nenhum backend real é autorizado");
        }
        waiting.SetResult(new(WingetQueryState.Completed, []));
        using (var cancellable = WingetConsultativeProvider.ForSimulation(new FakeBackend(async (_, t) =>
        { await Task.Delay(Timeout.Infinite, t); return new(WingetQueryState.Completed, []); })))
        {
            cancellable.TryOptIn(true);
            var pending = cancellable.QueryAsync(apps, WingetQueryOrigin.ExplicitLabTest, default);
            cancellable.Stop(WingetStopReason.UserDisabled);
            check((await pending).State == WingetQueryState.Cancelled && !cancellable.Enabled,
                "Desativar sessão cancela consulta em andamento e descarta resultados tardios");
        }
        var foreign = new OfficialRelease(apps[0] with { }, "26.05", "fixture", "");
        using (var bad = WingetConsultativeProvider.ForSimulation(new FakeBackend((_, _) =>
            Task.FromResult(new WingetQueryResult(WingetQueryState.Completed, [foreign])))))
        {
            bad.TryOptIn(true);
            check((await bad.QueryAsync(apps, WingetQueryOrigin.ExplicitLabTest, default)).State == WingetQueryState.Unavailable,
                "Contrato rejeita observação associada a outro snapshot/sessão");
        }
    }
    private sealed class CacheClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero).AddTicks(timestamp);
        internal void Advance(TimeSpan span) => timestamp += span.Ticks;
    }
    private sealed class Recorder : IProgress<ProviderDiagnostic>
    {
        internal readonly System.Collections.Concurrent.ConcurrentBag<ProviderDiagnostic> Values = [];
        public void Report(ProviderDiagnostic value) => Values.Add(value);
    }
    private sealed class FakeBackend(Func<IReadOnlyList<InstalledApp>, CancellationToken, Task<WingetQueryResult>> query) : IWingetReadOnlyBackend
    {
        internal int Calls;
        public Task<WingetQueryResult> QueryAsync(IReadOnlyList<InstalledApp> apps, CancellationToken token)
        { Calls++; return query(apps, token); }
    }
    private sealed class NineFixture(bool failEa = false, bool offline = false) : HttpMessageHandler
    {
        internal int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Requests);
            if (offline) throw new HttpRequestException("offline fixture");
            string path = request.RequestUri!.AbsolutePath;
            if (failEa && path.EndsWith("EADesktop")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            string body = path.Contains("CHANGELOG") ? "### Version 1.43.0\n- fixture\n" :
                path.EndsWith("releases/latest") ? "{\"tag_name\":\"" +
                    (path.Contains("FanControl") ? "V284" : path.Contains("jellyfin") ? "v12.2" : "26.05") + "\",\"draft\":false,\"prerelease\":false}" :
                "[{\"type\":\"dir\",\"name\":\"" + (path.EndsWith("Outlook") ? "1.2026.812.100" :
                    path.EndsWith("ProtonPass") ? "1.42.0" : path.EndsWith("EADesktop") ? "13.813.0.6326" :
                    path.EndsWith("Mega") ? "20.1.0" : path.EndsWith("GameInput") ? "3.5.283" : "1.43.5.11029") + "\"}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
