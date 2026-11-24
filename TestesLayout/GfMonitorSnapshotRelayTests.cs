using GerenciadorAplicativos;

internal static class GfMonitorSnapshotRelayTests
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Relay Monitor GF: " + message);
    }

    internal static void Run()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        GfResourceMonitorSnapshot Frame(int seconds) => new(now.AddSeconds(seconds), 20, null, 40, null, null, null);
        using var relay = new GfMonitorSnapshotRelay();
        int delivered = 0, invalidated = 0;
        void Invalidate() => invalidated++;
        relay.BeginGeneration(1);
        Require(relay.Publish(1, Frame(0), now), "coletor publica antes da abertura");
        using (relay.Attach(_ => delivered++, now, Invalidate))
        {
            Require(delivered == 1, "replay fresco ao abrir");
            Require(!relay.Publish(1, Frame(0), now), "não duplicar snapshot");
            Require(!relay.Publish(2, Frame(1), now.AddSeconds(1)), "rejeitar geração não autorizada");
            Require(relay.Publish(1, Frame(1), now.AddSeconds(1)) && delivered == 2, "entrega única");
            Require(!relay.Publish(1, Frame(-1), now), "não regredir watermark");
            bool rejected = false;
            try { using var duplicate = relay.Attach(_ => { }, now, Invalidate); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "um consumidor flutuante por host");
        }
        Require(relay.Publish(1, Frame(2), now.AddSeconds(2)) && delivered == 2, "fechado não recebe callbacks");
        using (relay.Attach(_ => delivered++, now.AddSeconds(20), Invalidate))
        {
            Require(delivered == 2 && invalidated == 1, "não reproduzir amostra vencida ao reabrir");
            relay.BeginGeneration(2);
            Require(invalidated == 2, "limpar leitura imediatamente na troca de geração");
            Require(!relay.Publish(1, Frame(20), now.AddSeconds(20)), "rejeitar coletor antigo");
            Require(relay.Publish(2, Frame(20), now.AddSeconds(20)) && delivered == 3, "retomar geração nova");
        }
        // Exceção de callback desanexa o consumidor e é propagada ao host.
        bool failed = false;
        try { using var broken = relay.Attach(_ => throw new InvalidOperationException("fixture"), now.AddSeconds(20), Invalidate); }
        catch (InvalidOperationException) { failed = true; }
        Require(failed, "não ocultar falha da interface");
        using (relay.Attach(_ => delivered++, now.AddSeconds(20), Invalidate))
            Require(delivered == 4, "falha não mantém lease ocupado");
        using (relay.Attach(_ => { }, now.AddSeconds(20), Invalidate))
        {
            bool rejected = false;
            try { using var duplicate = relay.Attach(_ => { }, now.AddSeconds(20), Invalidate); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "novo lease permanece exclusivo");
        }
        bool reentryRejected = false;
        using (relay.Attach(_ =>
        {
            try { relay.Publish(2, Frame(22), now.AddSeconds(22)); }
            catch (InvalidOperationException) { reentryRejected = true; }
        }, now.AddSeconds(20), Invalidate))
            Require(reentryRejected, "callback não pode publicar recursivamente");
        bool disposeRejected = false;
        using (relay.Attach(_ =>
        {
            try { relay.Dispose(); }
            catch (InvalidOperationException) { disposeRejected = true; }
        }, now.AddSeconds(20), Invalidate))
            Require(disposeRejected, "callback não descarta o relay em entrega");
        // Falha de entrega ao publicar, não apenas no replay inicial.
        using (relay.Attach(s =>
        {
            if (s.CapturedAtUtc == now.AddSeconds(21)) throw new InvalidOperationException("fixture publish");
        }, now.AddSeconds(20), Invalidate))
        {
            bool failedDelivery = false;
            try { relay.Publish(2, Frame(21), now.AddSeconds(21)); }
            catch (InvalidOperationException) { failedDelivery = true; }
            Require(failedDelivery, "propagar falha de entrega");
            using var replacement = relay.Attach(_ => delivered++, now.AddSeconds(21), Invalidate);
            Require(delivered == 5, "novo consumidor pode assumir após falha de entrega");
        }
        relay.BeginGeneration(3);
        Require(relay.Publish(3, Frame(22) with { GpuIdentity = "fixture A" }, now.AddSeconds(22)), "primeira identidade da geração");
        Require(!relay.Publish(3, Frame(23) with { GpuIdentity = "fixture B" }, now.AddSeconds(23)), "troca de GPU exige nova geração");
        Require(!relay.Publish(3, Frame(23) with { Generation = 2 }, now.AddSeconds(23)), "não carimbar origem conflitante");
        Require(relay.Publish(3, Frame(23), now.AddSeconds(23)), "CPU/RAM continuam com GPU ausente");
        Require(!relay.Publish(3, Frame(24) with { GpuIdentity = "fixture B" }, now.AddSeconds(24)), "ausência não apaga seleção");
        relay.BeginGeneration(4);
        Require(relay.Publish(4, Frame(24) with { GpuIdentity = "fixture B" }, now.AddSeconds(24)), "outra GPU na nova geração");
        using (relay.Attach(s => Require(s.Generation == 4, "carimbo da geração entregue"), now.AddSeconds(24), Invalidate)) { }
        relay.Dispose();
        bool disposed = false;
        try { relay.Publish(2, Frame(21), now.AddSeconds(21)); }
        catch (ObjectDisposedException) { disposed = true; }
        Require(disposed, "nenhuma entrega após descarte");
    }
}
