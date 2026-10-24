using System.Diagnostics;
using System.Globalization;

namespace GerenciadorAplicativos;

internal enum PerformanceStage { Startup, Inventory, Updates, ResidualDiscovery, ResidualClassification, ResidualFilter, ResidualInspection }
internal sealed record PerformanceSample(PerformanceStage Stage, double DurationMs, long AllocatedBytes,
    long? WorkingBefore, long? WorkingAfter, long? PeakSession, long ManagedBefore, long ManagedAfter, double? CpuMs)
{
    internal string Format() => FormattableString.Invariant($"DESEMPENHO.LOCAL etapa={Stage} duracao_ms={DurationMs:F2} alocacoes_processo_bytes={AllocatedBytes} ram_antes_bytes={WorkingBefore} ram_depois_bytes={WorkingAfter} pico_sessao_bytes={PeakSession} gerenciada_antes_bytes={ManagedBefore} gerenciada_depois_bytes={ManagedAfter} cpu_ms={CpuMs:F2}");
}

// Métricas agregadas do próprio processo. Nunca coleta nomes, caminhos ou conteúdo.
// Alocação/CPU incluem trabalho concorrente do GF; pico é da sessão, não do intervalo.
internal static class LocalPerformanceMetrics
{
    internal static Action<PerformanceSample>? Sink { get; set; }
    private static IDisposable? startup;
    internal static IDisposable Measure(PerformanceStage stage, Action<PerformanceSample>? sink = null) => new Scope(stage, sink ?? Sink);
    internal static void BeginStartup() => startup = Measure(PerformanceStage.Startup);
    internal static void CompleteStartup() => Interlocked.Exchange(ref startup, null)?.Dispose();
    private sealed class Scope : IDisposable
    {
        private readonly PerformanceStage stage;
        private Action<PerformanceSample>? sink;
        private readonly long started, allocated, managed;
        private readonly long? working;
        private readonly double? cpu;
        internal Scope(PerformanceStage stage, Action<PerformanceSample>? sink)
        {
            this.stage = stage; this.sink = sink;
            if (sink is null) return;
            started = Stopwatch.GetTimestamp(); allocated = GC.GetTotalAllocatedBytes(false); managed = GC.GetTotalMemory(false);
            (working, _, cpu) = ReadProcess();
        }
        public void Dispose()
        {
            var target = Interlocked.Exchange(ref sink, null);
            if (target is null) return;
            var (after, peak, endCpu) = ReadProcess();
            target(new(stage, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Math.Max(0, GC.GetTotalAllocatedBytes(false) - allocated), working, after, peak,
                managed, GC.GetTotalMemory(false), cpu.HasValue && endCpu.HasValue ? Math.Max(0, endCpu.Value - cpu.Value) : null));
        }
    }
    private static (long?, long?, double?) ReadProcess()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return (process.WorkingSet64, process.PeakWorkingSet64, process.TotalProcessorTime.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        { return (null, null, null); }
    }
}
