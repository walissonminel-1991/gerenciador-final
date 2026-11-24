using GerenciadorAplicativos;

internal static class GfThermalPresentationTests
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Apresentação térmica GF: " + message);
    }

    internal static void Run()
    {
        DateTimeOffset t = DateTimeOffset.UtcNow;
        var key = new WddmGpuKey(1, 2, 0);
        var other = new WddmGpuKey(1, 2, 1);
        var view = new GfThermalPresentation();
        const long generation = 1;
        var theme = V269Theme.AzulGf;
        GfResourceMonitorSnapshot Sample(double? v, int seconds, WddmGpuKey? capsKey = null, long g = generation)
        {
            DateTimeOffset at = t.AddSeconds(seconds);
            var selected = capsKey ?? key;
            var caps = GfThermalEvidence.FromD3dKmt(selected, g, selected, g, true, 700, 800, at);
            return new(at, null, null, null, null, null, v, GpuIdentity: key.ToString(),
                GpuThermalEvidence: caps, Generation: g);
        }
        GfMeterFrame Present(double? raw, double? smoothed, int seconds)
        {
            var s = Sample(raw, seconds); view.Update(s, s.CapturedAtUtc);
            return view.Frame(s, smoothed, theme, s.CapturedAtUtc);
        }
        var low = Present(48, 48, 0);
        Require(low.ScalePosition is < 75 && !low.Immediate, "leitura abaixo do aviso");
        var warning = Present(70, 50, 1);
        Require(warning.ScalePosition == 75 && warning.Immediate && warning.Fraction == 0.875,
            "aviso real não espera EMA");
        var max = Present(81, 60, 2);
        Require(max.ScalePosition == 100 && max.Immediate && max.Fraction == 1, "máximo real ou acima sem atraso");
        Require(Present(60, 60, 3).ScalePosition == 100 && Present(60, 60, 6).ScalePosition == 75,
            "cor respeita recuperação em duas etapas");
        Require(Present(60, 60, 7).ScalePosition == 75 && Present(60, 60, 10).ScalePosition == 60,
            "cor retorna à escala contínua após recuperação");
        var mismatch = Sample(75, 11, other);
        Require(view.Update(mismatch, mismatch.CapturedAtUtc) == GfThermalAlert.Unknown &&
            !view.Frame(mismatch, 75, theme, mismatch.CapturedAtUtc).Immediate, "limite de outra GPU não classifica alerta");
        var fresh = Sample(80, 12);
        view.Update(fresh, fresh.CapturedAtUtc);
        view.Expire(t.AddSeconds(23));
        Require(!view.Frame(fresh, 80, theme, t.AddSeconds(23)).Immediate, "capacidade expirada não permanece crítica");
        var missing = Sample(null, 24);
        Require(view.Update(missing, missing.CapturedAtUtc) == GfThermalAlert.Unknown &&
            view.Frame(missing, null, theme, missing.CapturedAtUtc).Fraction is null, "ausência não vira zero");
    }
}
