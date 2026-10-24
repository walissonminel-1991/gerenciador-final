using GerenciadorAplicativos;

internal static class OfficialLifetimeTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        OfficialReleaseSources.ClearCacheForTests();
        var firefox = new InstalledApp("Mozilla Firefox", "139.0", "Mozilla Corporation");
        var clock = new OfficialClock();
        var fixture = new Fixture();
        using var source = new OfficialReleaseSources(fixture, clock: clock);
        var first = await source.QueryAsync([firefox], default);
        var cached = await source.QueryAsync([firefox], default);
        check(!first.Diagnostics.Single().UsedMemoryCache && cached.Diagnostics.Single().UsedMemoryCache && fixture.Requests == 1,
            "Fontes oficiais discriminam HTTPS e cache sem presumir causa de latência");
        clock.WallClock = clock.WallClock.AddYears(1);
        clock.Advance(TimeSpan.FromMinutes(9));
        await source.QueryAsync([firefox], default);
        check(fixture.Requests == 1, "Cache oficial: relógio civil adiantado não altera prazo monotônico");
        clock.Advance(TimeSpan.FromMinutes(1));
        var expired = await source.QueryAsync([firefox], default);
        check(fixture.Requests == 2 && !expired.Diagnostics.Single().UsedMemoryCache,
            "Cache oficial: TTL expira sem sleeps ou dependência de rede real");
        var inventoryOnly = firefox with { EligibleForUpdates = false };
        var forcedReadOnly = MultiSourceMatcher.Merge([inventoryOnly],
            [new(inventoryOnly, new("Firefox", "Firefox", "999.0", "Mozilla"), MatchState.Candidate)],
            [new(inventoryOnly, "999.0", "Mozilla", "https://www.mozilla.org/firefox/")]);
        check(forcedReadOnly.Single().State == MatchState.Unmatched && forcedReadOnly.Single().Catalog is null,
            "Fusão revalida somente inventário mesmo se ambas as fontes devolverem candidato indevido");
        var one = firefox with { };
        var two = firefox with { };
        bool swappedRejected = false;
        try { MultiSourceMatcher.Merge([one, two], [new(two, null, MatchState.Unmatched), new(one, null, MatchState.Unmatched)], []); }
        catch (InvalidDataException) { swappedRejected = true; }
        check(swappedRejected, "Entradas iguais por valor não permitem trocar identidades e posições de instalação");
        OfficialReleaseSources.ClearCacheForTests();
    }
    private sealed class OfficialClock : TimeProvider
    {
        private long timestamp;
        internal DateTimeOffset WallClock = DateTimeOffset.Parse("2026-10-21T00:00:00Z");
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => WallClock;
        internal void Advance(TimeSpan elapsed) => timestamp += elapsed.Ticks;
    }
}
