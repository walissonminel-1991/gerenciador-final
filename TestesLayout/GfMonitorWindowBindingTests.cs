using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class GfMonitorWindowBindingTests
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Binding Monitor GF: " + message);
    }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in All(child)) yield return descendant;
        }
    }

    internal static void Run()
    {
        using var relay = new GfMonitorSnapshotRelay();
        using var form = new GfResourceMonitorForm();
        form.Configure(V269Theme.AzulGf, false);
        form.Show();
        Application.DoEvents();
        var now = DateTimeOffset.UtcNow;
        GfResourceMonitorSnapshot Sample(DateTimeOffset at, int cpu) => new(at, cpu, null, 40, null, null, null);
        string Cpu() => All(form).OfType<Label>().Single(x => x.Name == "MonitorCompactMetric0").Text;
        relay.BeginGeneration(1);
        Require(relay.Publish(1, Sample(now, 91), now), "publicação antes de abrir binding");
        using var binding = new GfMonitorWindowBinding(form, relay, now);
        Require(Cpu() == "CPU 91%", "replay aparece no formulário real");
        relay.BeginGeneration(2);
        Require(Cpu() == "CPU —", "troca de geração limpa imediatamente o formulário");
        Require(!relay.Publish(1, Sample(now.AddSeconds(1), 99), now.AddSeconds(1)), "coletor antigo não reaparece");
        var earlier = now.AddSeconds(-1);
        Require(relay.Publish(2, Sample(earlier, 12), now) && Cpu() == "CPU 12%", "novo watermark permite timestamp menor após reset");
        Require(!relay.Publish(2, Sample(earlier, 99), now) && Cpu() == "CPU 12%", "duplicata não altera a leitura");
        form.Close();
        int delivered = 0;
        using var next = relay.Attach(_ => delivered++, now, () => { });
        Require(delivered == 1, "fechamento liberou o lease para outro consumidor");
        binding.Dispose();
        Require(relay.Publish(2, Sample(now, 37), now) && delivered == 2, "descarte antigo não invalida novo lease");
        VerifyThermalBinding();
    }

    private static void VerifyThermalBinding()
    {
        using var relay = new GfMonitorSnapshotRelay();
        using var form = new GfResourceMonitorForm();
        form.Configure(V269Theme.AzulGf, false);
        form.Show(); Application.DoEvents();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var key = new WddmGpuKey(1, 2, 0);
        GfResourceMonitorSnapshot Sample(double value, int seconds, long generation)
        {
            var at = now.AddSeconds(seconds);
            return new(at, null, null, null, null, null, value, GpuIdentity: key.ToString(),
                GpuThermalEvidence: GfThermalEvidence.FromD3dKmt(key, generation, key, generation, true, 700, 800, at));
        }
        Label Temperature() => All(form).OfType<Label>().Single(l => l.Name == "MonitorCompactMetric5");
        relay.BeginGeneration(1);
        using var binding = new GfMonitorWindowBinding(form, relay, now);
        Require(relay.Publish(1, Sample(48, -3, 1), now), "amostra fria entregue");
        Require(relay.Publish(1, Sample(70, -2, 1), now) && Temperature().Text == "GPU 70°C" &&
            Temperature().ForeColor == GfMeterScale.Colors(V269Theme.AzulGf).Yellow,
            "aviso comprovado na janela real, sem atraso da EMA");
        Require(relay.Publish(1, Sample(81, -1, 1), now) && Temperature().ForeColor == GfMeterScale.Colors(V269Theme.AzulGf).Red,
            "máximo comprovado na janela real");
        relay.BeginGeneration(2);
        Require(Temperature().Text == "GPU —", "geração limpa também estado térmico");
        Require(relay.Publish(2, Sample(48, -3, 2), now) && Temperature().ForeColor != GfMeterScale.Colors(V269Theme.AzulGf).Red,
            "perfil novo não herda alerta anterior");
        form.Close();
    }
}
