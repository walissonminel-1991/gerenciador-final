using GerenciadorAplicativos;
using System.Diagnostics;
using System.Text.Json;

internal static class ResidualBenchmark
{
    internal static void Run(bool comparable = false)
    {
        _ = BroadResidualDiscovery.Classify([], []); // aquecimento idêntico nos dois commits
        foreach (var (apps, evidenceCount) in new[] { (500, 5000), (1000, 10000), (1000, 20000), (1000, 50000) })
        {
            var inventory = Enumerable.Range(0, apps).Select(i => new ProgramEntry { Name = "Instalado" + i, Publisher = "Fabricante" + i }).ToArray();
            var evidence = Enumerable.Range(0, evidenceCount).Select(i => new BroadResidualEvidence("Produto" + (i / 2),
                "Local" + i, "Metadados fictícios", i % 2 == 0 ? "AppData" : "Registry")).ToArray();
            if (comparable)
                for (int warmup = 0; warmup < 5; warmup++)
                    _ = BroadResidualDiscovery.Classify(evidence, inventory);
            var samples = new List<object>();
            for (int iteration = 0; iteration < (comparable ? 7 : 3); iteration++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long beforeHeap = GC.GetTotalMemory(true), beforeAllocation = GC.GetTotalAllocatedBytes(true);
                using var process = Process.GetCurrentProcess();
                double cpu = process.TotalProcessorTime.TotalMilliseconds;
                long workingBefore = process.WorkingSet64;
                var clock = Stopwatch.StartNew();
                var result = BroadResidualDiscovery.Classify(evidence, inventory);
                double elapsed = clock.Elapsed.TotalMilliseconds;
                process.Refresh();
                double cpuElapsed = process.TotalProcessorTime.TotalMilliseconds - cpu;
                long allocation = GC.GetTotalAllocatedBytes(true) - beforeAllocation;
                long afterHeap = GC.GetTotalMemory(true);
                process.Refresh();
                string signature = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result.Select(r => new
                    { r.Product, Findings = r.Findings.Select(f => new { f.Location, f.Kind, f.Disposition, f.Sources, f.Evidence, f.Items, f.BasisDisposition }) })))));
                samples.Add(new { iteration, elapsed_ms = elapsed, signature, allocated_bytes = allocation,
                    managed_before_bytes = beforeHeap, managed_after_bytes = afterHeap, ram_before_bytes = workingBefore,
                    ram_after_bytes = process.WorkingSet64, peak_process_bytes = process.PeakWorkingSet64,
                    cpu_ms = cpuElapsed, returned = result.Count,
                    found = result.Coverage.CandidatesFound, unexamined = result.Coverage.CandidatesNotEvaluated });
#if GF_PROGRESSIVE
                var indexClock = Stopwatch.StartNew();
                var index = new ResidualResultIndex(result);
                for (int page = 0; page < index.Query().PageCount; page++) _ = index.Query(page: page);
                Console.WriteLine(JsonSerializer.Serialize(new { kind = "navigation", apps, evidenceCount, iteration,
                    elapsed_ms = indexClock.Elapsed.TotalMilliseconds, available = index.Count, filter_passes = index.FilterPasses }));
#endif
                GC.KeepAlive(result);
            }
            Console.WriteLine(JsonSerializer.Serialize(new { kind = "classification", apps, evidenceCount,
                runtime = Environment.Version.ToString(), processor_count = Environment.ProcessorCount, samples }));
        }
    }
}
