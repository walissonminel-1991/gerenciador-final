using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class GfMonitorPreferencesAndProgressTests
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Preferências/progresso Monitor GF: " + message);
    }

    internal static void Run()
    {
        var dir = Directory.CreateTempSubdirectory("gf-monitor-config-fixture-");
        try
        {
            string path = Path.Combine(dir.FullName, "monitor.json");
            File.WriteAllText(path, "{\"Version\":1,\"Enabled\":true}");
            Require(GfResourceMonitorPreferencesStore.Load(path) == GfResourceMonitorPreferences.Default,
                "configuração incompleta não pode ativar o monitor");
            var selected = GfResourceMonitorPreferences.Default with { Enabled = true, X = -120, Y = 30, OpacityPercent = 85 };
            Require(GfResourceMonitorPreferencesStore.TrySave(path, selected) &&
                GfResourceMonitorPreferencesStore.Load(path) == selected, "preferência completa preservada");
            string valid = File.ReadAllText(path);
            File.WriteAllText(path, "{\"Version\":1,\"Enabled\":true,\"Expanded\":false,\"AlwaysOnTop\":false,\"OpacityPercent\":85,\"SnapToEdges\":true,\"X\":-120,\"Y\":30}");
            Require(GfResourceMonitorPreferencesStore.Load(path) == selected, "migração estrita v1 para v2 com fixação segura");
            var customized = selected with { Fixed = false, ScalePercent = 135, MonitorTheme = V269Theme.Claro, AlwaysOnTop = true };
            Require(GfResourceMonitorPreferencesStore.TrySave(path,customized) &&
                GfResourceMonitorPreferencesStore.Load(path) == customized, "tema, escala, fixação e sobreposição persistem");
            File.WriteAllText(path,valid.Replace("\"Fixed\":true","\"Fixed\":null"));
            Require(GfResourceMonitorPreferencesStore.Load(path) == GfResourceMonitorPreferences.Default,"fixação inválida rejeitada");
            File.WriteAllText(path, valid.Insert(1, "\"Enabled\":false,"));
            Require(GfResourceMonitorPreferencesStore.Load(path) == GfResourceMonitorPreferences.Default,
                "chave duplicada rejeitada");
            File.WriteAllText(path, valid.Replace("\"OpacityPercent\":85", "\"OpacityPercent\":\"85\""));
            Require(GfResourceMonitorPreferencesStore.Load(path) == GfResourceMonitorPreferences.Default,
                "tipo inválido não ativa o monitor");
        }
        finally { dir.Delete(recursive: true); }

        foreach (var theme in Enum.GetValues<V269Theme>())
        {
            var downloading = GfUniversalMeter.Update(new(UpdateStage.Downloading, 100, 100), theme);
            Require(downloading.Fraction == 1 && downloading.State == GfMeterState.Running &&
                downloading.Color == GfMeterScale.Colors(theme).Blue, "100% dos bytes não confirma instalação");
            Require(GfUniversalMeter.Update(new(UpdateStage.Confirmed), theme).State == GfMeterState.Success,
                "sucesso somente após confirmação");
            foreach (var stage in new[] { UpdateStage.Pending, UpdateStage.Blocked, UpdateStage.Failed,
                UpdateStage.Cancelled, UpdateStage.AuthorizationDeclined })
                Require(GfUniversalMeter.Update(new(stage), theme).Fraction is null,
                    "estado terminal não confirmado sem progresso fictício: " + stage);
            Require(GfUniversalMeter.Uninstall(UninstallStage.Cleaning, theme).State == GfMeterState.Indeterminate,
                "limpeza de resíduos não informa percentual inventado");
            Require(GfUniversalMeter.Uninstall(UninstallStage.Finished, theme).State == GfMeterState.Success,
                "desinstalação finalizada tem estado explícito");
            foreach (var stage in new[] { UninstallStage.Partial, UninstallStage.Pending, UninstallStage.Failed, UninstallStage.Cancelled })
                Require(GfUniversalMeter.Uninstall(stage, theme).Fraction is null,
                    "desinstalação incompleta nunca equivale a 100%: " + stage);
        }

        using var host = new Form();
        using var bar = new GfDynamicProgressBar { Width = 180, Height = 10 };
        host.Controls.Add(bar); host.Show(); Application.DoEvents();
        var frame = GfMeterScale.Frame(GfMeterKind.Progress, null, V269Theme.AzulGf, state: GfMeterState.Indeterminate);
        bar.Configure(V269Theme.AzulGf, true);
        bar.Present(frame, "fixture");
        Require(bar.IsAnimationRunning(), "indeterminado visível anima");
        bar.Configure(V269Theme.AzulGf, false);
        Require(!bar.IsAnimationRunning(), "desativar animação para o timer");
        bar.Configure(V269Theme.AzulGf, true);
        bar.Present(frame, "fixture");
        Require(bar.IsAnimationRunning(), "reapresentar frame idêntico não congela animação");
        bar.Configure(V269Theme.Dark, true);
        Require(bar.IsAnimationRunning(), "troca de tema mantém indeterminado");
        bar.Hide(); Require(!bar.IsAnimationRunning(), "barra oculta não anima");
        bar.Show(); Require(bar.IsAnimationRunning(), "barra visível restaura animação");
        bar.Present(GfMeterScale.Frame(GfMeterKind.Progress, null, V269Theme.Dark, state: GfMeterState.Success), "concluído");
        Require(!bar.IsAnimationRunning(), "conclusão confirmada encerra timer");
        host.Close();
    }
}
