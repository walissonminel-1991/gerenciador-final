using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using GerenciadorAplicativos;

// Somente callbacks, handlers HTTP e arquivos temporarios da fixture.
// Nao inicia inventario, WUA, instalador, Shutdown do SO ou receptor real.
internal static class LocalBetaRegressionTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return Task.FromResult(response(request)); }
    }

    internal static void Run()
    {
        int checks = 0, failed = 0;
        void Check(bool ok, string label)
        {
            checks++;
            if (!ok) failed++;
            Console.WriteLine((ok ? "PASS " : "FAIL ") + "GF7_LOCAL " + label);
        }
        var prior = AnonymousTelemetryContext.Current;
        string folder = Path.Combine(Path.GetTempPath(), "gf7-local-fixture-" + Guid.NewGuid().ToString("N"));
        try
        {
            AnonymousTelemetryContext.Current = null;
            int automatic = 0;
            using (var form = new MainForm(startServices: false, startupOnlineProbe: () => automatic++))
            {
                form.Show(); Application.DoEvents();
                typeof(MainForm).GetMethod("BeginStartupChecks", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                Check(automatic == 0, "GF6-001 abertura nao chama HTTP/WUA; observado=" + automatic);
                form.Close(); Application.DoEvents();
            }

            Directory.CreateDirectory(folder);
            string choice = JsonSerializer.Serialize(new { schema = 2, state = 3,
                notice_version = TelemetryConsentPolicy.Version, notice_sha256 = TelemetryConsentPolicy.NoticeHash });
            string pending = JsonSerializer.Serialize(new { schema = 1, version = "v5.13.11.66", exit = "normal",
                counts = new Dictionary<string, int> { ["MSIX.LEITURA"] = 1 } });
            File.WriteAllText(Path.Combine(folder, "consentimento-v1.json"), choice);
            File.WriteAllText(Path.Combine(folder, "diagnostico-pendente-v1.json"), pending);
            var recorder = new AnonymousTelemetry(folder);
            AnonymousTelemetryContext.Current = recorder;
            Check(!recorder.IsRemoteConsentCurrent(), "GF6-002 escolha remota herdada nao ativa coleta");
            recorder.Record("APP.FALHA_INTERFACE");
            Check(!recorder.SaveOnNormalExit("v5.13.11.66"), "GF6-002 saida normal nao grava pendencia remota");
            Check(!recorder.SaveOnExit("v5.13.11.66", normalExit: false), "GF6-002 falha nao grava pendencia remota");
            int sends = 0;
            using (var client = new HttpClient(new Handler(_ => { sends++; return new(HttpStatusCode.Accepted); })))
            {
                bool sent = Task.Run(() => BetaTelemetryTransport.TrySendAsync(recorder,
                    new Uri("https://receiver.invalid/ingest"), client)).GetAwaiter().GetResult();
                Check(!sent && sends == 0, "GF6-002 transporte ficticio nao e chamado; observado=" + sends);
            }
            Check(recorder.PendingForRemote() is null, "GF6-002 pendencia antiga nao e elegivel a envio");
            using (var closing = new MainForm(startServices: false))
            { closing.Show(); Application.DoEvents(); closing.Close(); Application.DoEvents(); }
            Check(File.Exists(recorder.PendingFile) && File.ReadAllText(recorder.PendingFile) == pending &&
                File.ReadAllText(recorder.ConsentFile) == choice, "GF6-002 fechamento preserva arquivos herdados sem regravar");
            Check(!new AnonymousTelemetry(folder).IsRemoteConsentCurrent(), "GF6-002 reabertura permanece sem coleta remota");
            Check(recorder.SetConsent(TelemetryConsent.Declined) && !File.Exists(recorder.PendingFile),
                "GF6-002 revogacao explicita continua disponivel");
            Check(!recorder.SetConsent(TelemetryConsent.RemoteBeta) && !recorder.SetConsent(TelemetryConsent.LocalOnly),
                "GF6-002 esta edicao nao pode ativar o gravador colaborativo");
            Check(!BetaTelemetryUploader.IsConfigured, "GF6-002 uploader real permanece desconfigurado");

            var agents = new List<string>();
            using var source = new RuckZuckCatalog(new Handler(request =>
            {
                if (request.RequestUri?.Host != "ruckzuck.tools" || request.Method != HttpMethod.Get)
                    throw new Exception("GF7_LOCAL requisicao fora da fixture");
                agents.Add(request.Headers.UserAgent.ToString());
                string body = request.RequestUri.AbsolutePath.EndsWith("/geturl", StringComparison.Ordinal)
                    ? "\"https://ruckzuck.tools\""
                    : "[{\"ShortName\":\"GF7Fixture\",\"ProductName\":\"GF7Fixture\",\"ProductVersion\":\"2.0\",\"Manufacturer\":\"Fixture\"}]";
                return new(HttpStatusCode.OK) { Content = new StringContent(body) };
            }));
            _ = Task.Run(() => source.GetCatalogAsync(CancellationToken.None, forceRefresh: true)).GetAwaiter().GetResult();
            string expected = "GerenciadorFinal/" + typeof(MainForm).Assembly.GetName().Version;
            Check(agents.Count == 2 && agents.All(a => a == expected), "GF6-004 User-Agent acompanha assembly; observado=" + string.Join(",", agents));
            Console.WriteLine($"GF7_LOCAL_RESULT checks={checks} failed={failed} rede_real=0 drivers_alterados=0");
            if (failed != 0) throw new Exception("GF7_LOCAL_REGRESSION_FAILED=" + failed);
        }
        finally
        {
            AnonymousTelemetryContext.Current = prior;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
