using GerenciadorAplicativos;

internal static class WindowsWddmGpuProviderTests
{
    internal static void Run(bool probeNative = false)
    {
        static void Check(bool condition, string reason)
        { if (!condition) throw new InvalidOperationException("WDDM GPU: " + reason); }

        const string gpuA = "luid_0x00000001_0x00001234_phys_0";
        const string gpuB = "luid_0x00000002_0x00005678_phys_0";
        const string engine3D = "pid_101_" + gpuA + "_eng_0_engtype_3D";
        const string engineCopy = "pid_201_" + gpuA + "_eng_1_engtype_Copy";
        const string another3D = "pid_202_" + gpuA + "_eng_0_engtype_3D";
        Check(WindowsWddmGpuProvider.TryEngineKey(engine3D, out var key, out var engine) &&
              key == new WddmGpuKey(1, 0x1234, 0) && engine == "0:3D",
            "identidade de engine por LUID/phys e número não foi preservada");
        Check(WindowsWddmGpuProvider.TryMemoryKey(gpuB, out var keyB) &&
              keyB == new WddmGpuKey(2, 0x5678, 0), "identidade de GPU B divergente");
        foreach (string invalid in new[]
        {
            "GPU Engine", "pid_X_" + gpuA + "_eng_0_engtype_3D",
            "pid_1_luid_0x01_0x02_phys_64_eng_0_engtype_3D",
            "pid_1_luid_0x01_0x02_phys_0_eng_-1_engtype_3D",
            "pid_1_luid_0x01_0x02_phys_0_eng_0_engtype_3D_suffix"
        })
            Check(!WindowsWddmGpuProvider.TryEngineKey(invalid, out _, out _),
                "nome inválido aceito: " + invalid);
        Check(!WindowsWddmGpuProvider.TryMemoryKey("luid_0x01_0x02_phys_64", out _) &&
              !WindowsWddmGpuProvider.TryMemoryKey("luid_0x01_0x02", out _),
            "memória sem phys válido foi aceita");
        Check(WindowsWddmGpuProvider.Counter((ulong)42) == 42 &&
              WindowsWddmGpuProvider.Counter("42") == 42 &&
              WindowsWddmGpuProvider.Counter(0) == 0 &&
              WindowsWddmGpuProvider.Counter(null) is null &&
              WindowsWddmGpuProvider.Counter(-1) is null &&
              WindowsWddmGpuProvider.Counter("42.9") is null &&
              WindowsWddmGpuProvider.Counter(double.NaN) is null,
              "leitura de contador não foi validada");

        var aggregated = WindowsWddmGpuProvider.Join(
            [
                new(engine3D, 12UL),
                new(another3D, 18UL),
                new(engineCopy, 6UL),
                new("pid_303_" + gpuB + "_eng_0_engtype_3D", 71UL)
            ],
            [
                new(gpuB, 1_234_000UL, 5_000UL),
                new(gpuA, 2_000_000UL, 100_000UL)
            ]);
        Check(aggregated.State == HardwareDiscoveryState.Complete &&
              aggregated.Adapters.Count == 2 &&
              aggregated.Adapters[0].Key == key &&
              aggregated.Adapters[0].BusiestEnginePercent.Value == 30 &&
              aggregated.Adapters[0].DedicatedUsedBytes.Value == 2_000_000 &&
              aggregated.Adapters[1].BusiestEnginePercent.Value == 71 &&
              aggregated.Adapters[1].DedicatedUsedBytes.Value == 1_234_000,
            "GPU por LUID confundiu adaptadores ou somou engines diferentes");

        var incomplete = WindowsWddmGpuProvider.Join(
            [new(engine3D, 99), new(another3D, 90), new(engineCopy, 16)],
            [new(gpuA, 60, 70), new(gpuA, 80, 90)]);
        Check(incomplete.State == HardwareDiscoveryState.Partial &&
              incomplete.Adapters.Count == 1 &&
              incomplete.Adapters[0].BusiestEnginePercent.Value == 16 &&
              incomplete.Adapters[0].DedicatedUsedBytes.Value is null &&
              incomplete.Adapters[0].SharedUsedBytes.Value is null,
            "engine >100 ou memória duplicada deveria ser inconclusiva");
        var empty = WindowsWddmGpuProvider.Join([], []);
        Check(empty.State == HardwareDiscoveryState.Unavailable &&
              empty.Adapters.Count == 0 &&
              !string.IsNullOrEmpty(empty.Limitation),
            "sem contador WDDM, não inventar GPU ociosa");

        if (probeNative && OperatingSystem.IsWindows())
        {
            var actual = WindowsWddmGpuProvider.Capture(CancellationToken.None);
            Check(actual.CapturedAtUtc.Offset == TimeSpan.Zero &&
                  actual.Adapters.Count <= 4096 &&
                  actual.Adapters.All(a => a.BusiestEnginePercent.Value is null ||
                      a.BusiestEnginePercent.Value is >= 0 and <= 100),
                "captura WDDM violou estado/limites");
            Console.WriteLine($"WDDM probe estado={actual.State} adaptadores={actual.Adapters.Count}");
        }
        Console.WriteLine("OK WDDM GPU: AMD/Intel/NVIDIA por LUID e utilização por engine.");
    }
}
