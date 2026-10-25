using System.ComponentModel;
using GerenciadorAplicativos;

internal static class Lote3UninstallTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        var lease = new Probe();
        using (var resource = new SingleLaunchResource(lease))
        {
            var claimed = resource.Claim();
            resource.Dispose();
            check(lease.Releases == 0, "L3 Dispose concorrente não solta proteção transferida ao lançamento");
            claimed!.Dispose();
            check(lease.Releases == 1, "L3 proteção liberada uma vez após o lançamento retornar");
            bool refused=false; try { resource.Claim(); } catch(InvalidOperationException) { refused=true; }
            check(refused, "L3 recurso consumido nunca autoriza segundo lançamento");
        }
        for (int i=0;i<100;i++)
        {
            var probe = new Probe();
            var gate = new SingleLaunchResource(probe);
            Parallel.Invoke(() => { try { using var claimed = gate.Claim(); } catch(InvalidOperationException) { } }, gate.Dispose);
            if (probe.Releases != 1) throw new Exception("Proteção liberada em duplicidade ou vazada.");
        }
        check(true, "L3 cem disputas entre Claim e Dispose mantêm posse única do recurso");
        var app = new ProgramEntry { Name = "GF Synthetic L3", Publisher = "GF Tests", InventoryId = "Synthetic|L3",
            Version = "1.0", MsiProductCode = "{D6F476DC-F117-4E64-B04B-517CA776D80D}",
            UninstallString = "MsiExec.exe /X{D6F476DC-F117-4E64-B04B-517CA776D80D}" };
        var request = UninstallBatch.Select([app]);
        foreach (int error in new[] { 5, 1223 })
        {
            int scans = 0, launches = 0;
            var batch = new UninstallBatch();
            var result = await batch.RunAsync(request, true,
                () => ++scans == 1 ? Task.FromResult(new UninstallSnapshot([app], true)) : throw new Win32Exception(error),
                (_, _) => { launches++; return Task.FromResult<int?>(0); }, _ => Task.CompletedTask);
            check(result.Single().State == UninstallState.Pending && launches == 1,
                "L3 desinstalador retornou e inventário falhou: preservar pendência, código " + error);
            check((await batch.RunAsync(request, true, () => Task.FromResult(new UninstallSnapshot([app], true)),
                (_, _) => { launches++; return Task.FromResult<int?>(0); }, _ => Task.CompletedTask)).Count == 0 && launches == 1,
                "L3 lote consumido não repete desinstalador após erro posterior");
        }
    }
    private sealed class Probe : IDisposable
    {
        internal int Releases;
        public void Dispose() => Interlocked.Increment(ref Releases);
    }
}
