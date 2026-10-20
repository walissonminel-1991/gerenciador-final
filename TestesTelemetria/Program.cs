using System.Text.Json;
using GerenciadorAplicativos;

void Verify(bool ok, string message)
{
    if (!ok) throw new InvalidOperationException("Falha: " + message);
    Console.WriteLine("OK " + message);
}

string temp = Path.Combine(Path.GetTempPath(), "gf-telemetry-tests-" + Guid.NewGuid().ToString("N"));
try
{
    var tracker = new AnonymousTelemetry(temp);
    tracker.Record("APP.FALHA_INTERFACE");
    Verify(tracker.Consent == TelemetryConsent.NotAsked &&
           !File.Exists(tracker.ConsentFile) && !File.Exists(tracker.PendingFile),
        "Sem opt-in nao cria relatorios nem preferencia implicita");
    Verify(!tracker.SaveOnNormalExit("v5.13.11.1"), "Sem consentimento nao gera relatorio");

    Verify(tracker.SetConsent(TelemetryConsent.LocalOnly), "Aceitacao local explicita");
    tracker.Record("REGISTRO_PROGRAMAS.FALHA usuario=C:\\Users\\Privado");
    tracker.Record("REGISTRO_PROGRAMAS.FALHA app=nome-privado");
    tracker.Record("APP.FALHA_INTERFACE senha=segredo");
    tracker.Record("APP.INICIALIZACAO_INICIO usuario@exemplo.com"); // nao permitido
    tracker.Record("REGISTRO_PROGRAMAS.FALHA-APENDICE"); // falso prefixo
    tracker.Record("JANELA.BANDEJA_RESTAURAR.INICIO");
    for (int i = 0; i < 120; i++)
        tracker.Record("JANELA.BANDEJA_RESTAURAR.FIM");
    Verify(tracker.SaveOnNormalExit("v5.13.11.1"), "Relatorio salvo apenas ao encerrar");
    string json = File.ReadAllText(tracker.PendingFile);
    Verify(!json.Contains("Privado") && !json.Contains("segredo") &&
           !json.Contains("nome-privado") && !json.Contains("exemplo.com") &&
           !json.Contains("C:\\") && !json.Contains("hostname", StringComparison.OrdinalIgnoreCase),
           "Dados brutos, usuario e caminhos nao entram no payload");
    using (var doc = JsonDocument.Parse(json))
    {
        var root = doc.RootElement;
        Verify(root.GetProperty("schema").GetInt32() == 1 &&
               root.GetProperty("version").GetString() == "v5.13.11.1" &&
               root.GetProperty("exit").GetString() == "normal",
               "Somente versao e estado de encerramento");
        var counts = root.GetProperty("counts");
        Verify(counts.EnumerateObject().Count() == 4 &&
               counts.GetProperty("REGISTRO_PROGRAMAS.FALHA").GetInt32() == 2 &&
               counts.GetProperty("APP.FALHA_INTERFACE").GetInt32() == 1 &&
               counts.GetProperty("JANELA.BANDEJA_RESTAURAR.INICIO").GetInt32() == 1 &&
               counts.GetProperty("JANELA.BANDEJA_RESTAURAR.FIM").GetInt32() == 100,
               "Categorias exatas e limites impedem crescimento descontrolado");
    }

    // Publicacao GitHub: apenas abre formulario no navegador, nunca executa POST.
    var link = GitHubDiagnosticShare.CreateReviewUrl(
        tracker.Preview("v5.13.11.2", normalExit: false));
    Verify(link is not null && link.Host == "github.com" &&
        link.AbsolutePath == "/walissonminel-1991/gerenciador-final/issues/new",
        "Formulario publico usa somente dominio/caminho oficial GitHub");
    var publicUrl = Uri.UnescapeDataString(link!.Query);
    Verify(publicUrl.Contains("REGISTRO_PROGRAMAS.FALHA") &&
        publicUrl.Contains("JANELA.BANDEJA_RESTAURAR.FIM") &&
        publicUrl.Contains("PÚBLICA") &&
        !publicUrl.Contains("Privado") && !publicUrl.Contains("segredo") &&
        !publicUrl.Contains("nome-privado") && !publicUrl.Contains("exemplo.com"),
        "URL de revisao exclui textos originais, caminhos e segredos simulados");
    Verify(GitHubDiagnosticShare.CreateReviewUrl(
        "{\"schema\":1,\"version\":\"v5.13.11.2\",\"exit\":\"normal\",\"counts\":{}}") is null,
        "Relatorio sem eventos nao provoca abertura de Issue");
    Verify(GitHubDiagnosticShare.CreateReviewUrl(
        "{\"schema\":1,\"version\":\"v5.13.11.2\",\"exit\":\"normal\",\"counts\":{\"MSIX.LEITURA\":1},\"secret\":\"malicioso\"}") is null,
        "Campo arbitrario inserido no JSON nao pode virar Issue");
    Verify(GitHubDiagnosticShare.CreateReviewUrl(
        "{\"schema\":1,\"version\":\"v5.13.11.2\",\"exit\":\"normal\",\"counts\":{\"C:\\\\Users\\\\Privado\":1}}") is null,
        "Chaves que parecem caminhos pessoais sao rejeitadas");
    Verify(GitHubDiagnosticShare.CreateReviewUrl(
        "{\"schema\":1,\"version\":\"v5.13.11.2\",\"exit\":\"normal\",\"counts\":{\"MSIX.LEITURA\":101}}") is null,
        "Contagem acima do limite nao pode virar Issue");

    tracker.Record("APP.FALHA_NAO_TRATADA");
    Verify(tracker.SaveOnNormalExit("v5.13.11.1") &&
           Directory.EnumerateFiles(temp, "diagnostico-pendente*").Count() == 1,
           "Encerramentos sucessivos sobrescrevem unico pendente");
    var restarted = new AnonymousTelemetry(temp);
    Verify(restarted.Consent == TelemetryConsent.LocalOnly &&
           File.Exists(restarted.PendingFile),
           "Preferencia voluntaria persiste para a proxima sessao");

    File.SetLastWriteTimeUtc(restarted.PendingFile, DateTime.UtcNow.AddDays(-8));
    _ = new AnonymousTelemetry(temp);
    Verify(!File.Exists(restarted.PendingFile), "Relatorio antigo expira apos sete dias");

    restarted.Record("APP.FALHA_INTERFACE");
    Verify(restarted.SaveOnNormalExit("v5.13.11.1"), "Pendencia criada antes da revogacao");
    Verify(restarted.SetConsent(TelemetryConsent.Declined) &&
           !File.Exists(restarted.PendingFile) &&
           !restarted.SaveOnNormalExit("v5.13.11.1"),
           "Recusa apaga pendencias e interrompe coleta");
    var refused = new AnonymousTelemetry(temp);
    Verify(refused.Consent == TelemetryConsent.Declined, "Nao repete aviso apos recusa");
    Console.WriteLine("OK: 10 cenarios de privacidade/rotacao aprovados; nenhuma rede foi usada.");
}
finally
{
    try { Directory.Delete(temp, recursive: true); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
}
