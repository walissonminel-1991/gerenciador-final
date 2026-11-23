using GerenciadorAplicativos;

internal static class DxgiGpuIdentityProviderTests
{
    internal static void Run(bool probeNative = false)
    {
        static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException("GPU identidade: " + label); }

        var dxgi = new DxgiGpuIdentity(1, 0x1234, "Intel Arc teste",
            0x8086, 0x9A49, 0x12345678, 8_000_000_000, false);
        const string pnpId = @"PCI\VEN_8086&DEV_9A49&SUBSYS_12345678&REV_01\3&123&0&1";
        var pnp = new HardwareDeviceDescriptor(pnpId, "Intel GPU",
            "Intel", "Display", "PCI", HardwareDeviceKind.Graphics);
        Check(DxgiGpuIdentityProvider.MatchUniquePnp(dxgi, [pnp]) == pnpId,
            "identidade PnP unívoca não foi confirmada");
        Check(DxgiGpuIdentityProvider.MatchUniquePnp(dxgi,
            [pnp, pnp with { InstanceId = pnpId + "DUP" }]) is null,
            "duas instâncias PnP não podem ser escolhidas por ordem");
        Check(DxgiGpuIdentityProvider.MatchUniquePnp(dxgi,
            [pnp with { InstanceId = @"PCI\VEN_8086&DEV_9A49&SUBSYS_AAAAAAAA\A" }]) is null,
            "subsystem conflitante não pode ser associado");
        Check(DxgiGpuIdentityProvider.MatchUniquePnp(dxgi,
            [pnp with { Kind = HardwareDeviceKind.Other }]) is null,
            "classe PnP não gráfica não é prova de GPU");
        Check(DxgiGpuIdentityProvider.MatchUniquePnp(
            dxgi with { IsSoftware = true }, [pnp]) is null,
            "adaptador DXGI de software não é GPU física PnP");

        var wddm = WindowsWddmGpuProvider.Join(
            [new WddmEngineCounterRow(
                "pid_101_luid_0x00000001_0x00001234_phys_0_eng_0_engtype_3D", 42)],
            [new WddmMemoryCounterRow("luid_0x00000001_0x00001234_phys_0", 1000UL, 200UL)]);
        var dxgiSnapshot = new DxgiGpuSnapshot(DateTimeOffset.UtcNow,
            HardwareDiscoveryState.Complete, [dxgi], null);
        var inventory = new HardwareDiscoverySnapshot(DateTimeOffset.UtcNow,
            HardwareDiscoveryState.Complete, [pnp], "Windows PnP", null);
        var bound = DxgiGpuIdentityProvider.Correlate(wddm, dxgiSnapshot, inventory);
        Check(bound.Count == 1 && bound[0].DxgiDescription == "Intel Arc teste" &&
              bound[0].PnpInstanceId == pnpId &&
              bound[0].Counters.BusiestEnginePercent.Value == 42,
            "LUID do WDDM deve mapear para DXGI e PnP unívoco");
        var noLuid = DxgiGpuIdentityProvider.Correlate(wddm,
            dxgiSnapshot with { Adapters = [dxgi with { LuidLow = 0xABCD }] },
            inventory);
        Check(noLuid[0].DxgiDescription is null && noLuid[0].PnpInstanceId is null,
            "marca/modelo não se inferem quando LUID difere");
        var duplicated = DxgiGpuIdentityProvider.Correlate(wddm,
            dxgiSnapshot with { Adapters = [dxgi, dxgi] }, inventory);
        Check(duplicated[0].DxgiDescription is null && duplicated[0].PnpInstanceId is null,
            "LUID duplicado não pode associar nome por posição");
        // O mesmo LUID com phys distintos identifica engines distintas, mas
        // o DXGI GetDesc1 não atesta qual instância física é qual.
        var linked = wddm with { Adapters = [
            wddm.Adapters[0],
            wddm.Adapters[0] with { Key = new WddmGpuKey(1, 0x1234, 1) }
        ] };
        var linkedResults = DxgiGpuIdentityProvider.Correlate(linked, dxgiSnapshot, inventory);
        Check(linkedResults.Count == 2 && linkedResults.All(x =>
            x.DxgiDescription is null && x.PnpInstanceId is null &&
            x.IdentitySource.Contains("compartilhado", StringComparison.Ordinal)),
            "um mesmo LUID de duas GPUs físicas não prova identidade de cada phys");

        var partial = DxgiGpuIdentityProvider.Correlate(wddm,
            dxgiSnapshot, inventory with { State = HardwareDiscoveryState.Partial });
        Check(partial[0].DxgiDescription == "Intel Arc teste" &&
              partial[0].PnpInstanceId is null,
            "inventário PnP parcial não prova exclusividade");

        if (probeNative && OperatingSystem.IsWindows())
        {
            var observed = DxgiGpuIdentityProvider.Capture();
            Check(observed.Adapters.Count <= 32 &&
                  observed.CapturedAtUtc.Offset == TimeSpan.Zero &&
                  observed.Adapters.All(d => !string.IsNullOrWhiteSpace(d.Name)),
                "consulta nativa DXGI retornou identidade inválida");
            Console.WriteLine("DXGI sonda: " + observed.State +
                ", adaptadores=" + observed.Adapters.Count);
        }
        Console.WriteLine("OK identidade GPU: WDDM LUID → DXGI → PnP sem associação arbitrária.");
    }
}
