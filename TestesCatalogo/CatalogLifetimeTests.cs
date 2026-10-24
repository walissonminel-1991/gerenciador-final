using GerenciadorAplicativos;

internal static class CatalogLifetimeTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        RuckZuckCatalog.ClearCacheForTests();
        var clock = new CatalogClock();
        var fixture = new FakeHandler("\"https://ruckzuck.tools\"",
            "[{\"ShortName\":\"Fixture\",\"ProductName\":\"Fixture\",\"ProductVersion\":\"2.0\",\"Manufacturer\":\"Fixture\"}]");
        using var client = new RuckZuckCatalog(fixture, clock);
        await client.GetCatalogAsync(default);
        clock.WallClock = clock.WallClock.AddYears(-1);
        clock.Advance(TimeSpan.FromMinutes(9));
        await client.GetCatalogAsync(default);
        check(client.UsedMemoryCache && fixture.CatalogRequests == 1,
            "Cache RuckZuck: mudança no relógio civil não altera TTL monotônico");
        clock.Advance(TimeSpan.FromMinutes(1));
        await client.GetCatalogAsync(default);
        check(!client.UsedMemoryCache && fixture.CatalogRequests == 2,
            "Cache RuckZuck: expiração exata aos dez minutos exige nova consulta");
        RuckZuckCatalog.ClearCacheForTests();
        var delayed = new FakeHandler("\"https://ruckzuck.tools\"",
            "[{\"ShortName\":\"Fixture\",\"ProductName\":\"Fixture\",\"ProductVersion\":\"2.0\",\"Manufacturer\":\"Fixture\"}]", delayMs: 50);
        using var leader = new RuckZuckCatalog(delayed);
        using var follower = new RuckZuckCatalog(delayed);
        var first = leader.GetCatalogAsync(default);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
        bool cancelled = false;
        try { await follower.GetCatalogAsync(cancellation.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        await first;
        var after = await follower.GetCatalogAsync(default);
        check(cancelled && after.Count == 1 && delayed.CatalogRequests == 1 && follower.UsedMemoryCache,
            "Cancelar outro consumidor não cancela o download em andamento nem envenena o cache");
        RuckZuckCatalog.ClearCacheForTests();
    }
    private sealed class CatalogClock : TimeProvider
    {
        private long timestamp;
        internal DateTimeOffset WallClock = DateTimeOffset.Parse("2026-10-21T00:00:00Z");
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => WallClock;
        internal void Advance(TimeSpan elapsed) => timestamp += elapsed.Ticks;
    }
}
