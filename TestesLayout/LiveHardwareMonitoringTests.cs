using GerenciadorAplicativos;

internal static class LiveHardwareMonitoringTests
{
    internal static void Run()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Monitoramento: " + message);
        }

        var earlier = new CpuSystemTimes(100, 200, 100);
        var later = new CpuSystemTimes(150, 330, 170);
        Require(CpuUsageMath.Between(null, later) is null,
            "primeira amostra não pode inventar porcentagem de CPU");
        Require(Math.Abs((CpuUsageMath.Between(earlier, later) ?? -1) - 75d) < 0.00001,
            "cálculo de uso da CPU confundiu tempo ocioso com tempo de kernel");
        Require(CpuUsageMath.Between(earlier, earlier) is null,
            "intervalo sem diferença de contadores não é porcentagem válida");
        Require(CpuUsageMath.Between(later, earlier) is null,
            "recuo de contadores não pode ser tratado como wrap válido");
        Require(CpuUsageMath.Between(earlier, new CpuSystemTimes(500, 250, 120)) is null,
            "tempo ocioso superior ao total precisa ser inconclusivo");
        Require(CpuUsageMath.Between(
            new CpuSystemTimes(0, ulong.MaxValue - 2, 0),
            new CpuSystemTimes(0, ulong.MaxValue, 30)) is not null,
            "soma de diferenças válida no limite uint64 foi descartada");
        Require(CpuUsageMath.Between(
            new CpuSystemTimes(0, 0, 0),
            new CpuSystemTimes(0, ulong.MaxValue, 2)) is null,
            "overflow na soma de tempos precisa ser inconclusivo");

        var missing = LiveHardwareSensors.Unavailable;
        Require(missing.CpuTemperatureC.Availability == MonitorAvailability.Unavailable &&
                missing.CpuTemperatureC.Value is null &&
                missing.CpuPowerWatts.Value is null &&
                missing.CpuFanRpm.Value is null &&
                missing.GpuUsagePercent.Value is null &&
                missing.GpuTemperatureC.Value is null &&
                missing.GpuPowerWatts.Value is null &&
                missing.GpuFanRpm.Value is null &&
                missing.GpuVramUsedBytes.Value is null &&
                missing.ChipsetTemperatureC.Value is null &&
                missing.VrmTemperatureC.Value is null,
            "nenhum sensor sem fonte pode receber número sintético");

        var monitor = new LiveHardwareMonitor();
        Require(monitor.RecommendedIntervalMs(true, false) == ProcessSamplingPolicy.ForegroundIntervalMs &&
                monitor.RecommendedIntervalMs(false, false) == ProcessSamplingPolicy.BackgroundIntervalMs &&
                monitor.RecommendedIntervalMs(true, true) == ProcessSamplingPolicy.TrayIntervalMs,
            "cadência deve ser reutilizada, sem novo timer concorrente");

        if (OperatingSystem.IsWindows())
        {
            var first = monitor.Capture();
            Require(first.CpuUsagePercent.Availability != MonitorAvailability.Available,
                "a primeira amostra de CPU não pode declarar carga instantânea");
            Require(first.CapturedAtUtc.Offset == TimeSpan.Zero, "data da amostra não está em UTC");
            Require(first.Memory is null ||
                    first.Memory.TotalBytes >= first.Memory.AvailableBytes,
                "RAM disponível supera o total físico");
            Require(first.SystemUptime.Value is null || first.SystemUptime.Value.Value >= TimeSpan.Zero,
                "tempo de atividade negativo");
            Thread.Sleep(60);
            var second = monitor.Capture();
            double? load = second.CpuUsagePercent.Value;
            Require(!load.HasValue || load.Value is >= 0 and <= 100,
                "uso da CPU deve estar no intervalo 0-100");
            double? mhz = second.CpuFrequencyMhz.Value;
            Require(!mhz.HasValue || mhz.Value is >= 100 and <= 20_000,
                "frequência informada pelo Windows está fora da faixa validada");
            var memoryAlreadyRead = new MemoryRamSample(DateTime.UtcNow, 1_048_576, 524_288);
            var shared = monitor.Capture(memoryAlreadyRead);
            Require(ReferenceEquals(shared.Memory, memoryAlreadyRead),
                "a aba Sistema deve poder reutilizar a amostra RAM sem nova coleta");
            Require(monitor.History.Count == 3 && !ReferenceEquals(monitor.History, monitor.History),
                "histórico precisa ser limitado à sessão e exposto por cópia");
        }

        Console.WriteLine("OK monitoramento: contadores CPU, estados inconclusivos, cadência, RAM e uptime.");
    }
}
