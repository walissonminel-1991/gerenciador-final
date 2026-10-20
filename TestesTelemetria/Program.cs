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
    Verify(tracker.SetConsent(TelemetryConsent.LocalOnly) &&
        JsonDocument.Parse(tracker.Preview("v5.13.11.4", false)).RootElement
            .GetProperty("counts").GetProperty("REGISTRO_PROGRAMAS.FALHA").GetInt32() == 2,
        "Reconfirmar consentimento ativo preserva ocorrencias ja coletadas");
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

    // Nenhum link, formulario, anexo ou envio a GitHub: somente politica de JSON local.
    string validReport = tracker.Preview("v5.13.11.6", normalExit: false);
    Verify(DiagnosticPayloadPolicy.IsValidReport(validReport),
        "Apenas contagens tecnicas com esquema fechado sao elegiveis");
    Verify(!DiagnosticPayloadPolicy.IsValidReport(
        "{\"schema\":1,\"version\":\"v5.13.11.6\",\"exit\":\"normal\",\"counts\":{}}"),
        "Nao aceitar relatorio sem ocorrencias");
    Verify(!DiagnosticPayloadPolicy.IsValidReport(
        "{\"schema\":1,\"version\":\"v5.13.11.6\",\"exit\":\"normal\",\"counts\":{\"MSIX.LEITURA\":1},\"filename\":\"malware.exe\"}"),
        "Proibir metadados de arquivos e campos inesperados");
    Verify(!DiagnosticPayloadPolicy.IsValidReport(
        "{\"schema\":1,\"version\":\"v5.13.11.6\",\"exit\":\"normal\",\"counts\":{\"MSIX.LEITURA\":1,\"MSIX.LEITURA\":2}}"),
        "Proibir chaves repetidas mesmo com JSON sintaticamente valido");
    Verify(!DiagnosticPayloadPolicy.IsValidReport(
        "{\"schema\":1,\"version\":\"v5.13.11.6\",\"exit\":\"normal\",\"counts\":{\"MSIX.LEITURA\":101}}"),
        "Recusar contagem maior que o limite");
    Verify(!DiagnosticPayloadPolicy.IsValidReport(
        "{\"schema\":1,\"version\":\"v5.13.11.6\",\"exit\":\"normal\",\"counts\":{\"C:\\\\Users\\\\Privado\":1}}"),
        "Recusar chaves que contenham caminhos ou nomes arbitrarios");

    tracker.Record("APP.FALHA_NAO_TRATADA");
    Verify(tracker.SaveOnNormalExit("v5.13.11.1") &&
           Directory.EnumerateFiles(temp, "diagnostico-pendente*").Count() == 1,
           "Encerramentos sucessivos sobrescrevem unico pendente");
    var restarted = new AnonymousTelemetry(temp);
    Verify(restarted.Consent == TelemetryConsent.LocalOnly &&
           File.Exists(restarted.PendingFile),
           "Preferencia voluntaria persiste para a proxima sessao");

    Verify(restarted.ReviewableReport("v5.13.11.3") == File.ReadAllText(restarted.PendingFile),
        "Reabertura permite revisar pendencia anterior com sua versao original");
    string previousReport = File.ReadAllText(restarted.PendingFile);
    Verify(!restarted.SaveOnNormalExit("v5.13.11.4") &&
        File.ReadAllText(restarted.PendingFile) == previousReport,
        "Encerrar sessao vazia preserva pendencia util anterior");
    restarted.Record("DESINSTALACAO.FALHA");
    using (var current = JsonDocument.Parse(restarted.ReviewableReport("v5.13.11.3")!))
        Verify(current.RootElement.GetProperty("version").GetString() == "v5.13.11.3" &&
            current.RootElement.GetProperty("counts").EnumerateObject().Count() == 1,
            "Sessao atual tem prioridade sem misturar contagens de sessoes diferentes");
    var pendingOnly = new AnonymousTelemetry(temp);
    File.WriteAllText(pendingOnly.PendingFile, "{invalid}");
    Verify(pendingOnly.ReviewableReport("v5.13.11.3") is null,
        "Pendencia adulterada nao chega ao formulario");
    File.WriteAllText(pendingOnly.PendingFile, new string('x', 4097));
    Verify(pendingOnly.ReviewableReport("v5.13.11.3") is null,
        "Pendencia acima do limite e rejeitada");
    tracker.SaveOnNormalExit("v5.13.11.1");

    File.SetLastWriteTimeUtc(restarted.PendingFile, DateTime.UtcNow.AddDays(-8));
    Verify(pendingOnly.ReviewableReport("v5.13.11.3") is null,
        "Expiracao tambem e verificada ao revisar, sem reiniciar o aplicativo");
    Verify(!File.Exists(restarted.PendingFile), "Relatorio antigo expira apos sete dias");

    restarted.Record("APP.FALHA_INTERFACE");
    Verify(restarted.SaveOnNormalExit("v5.13.11.1"), "Pendencia criada antes da revogacao");
    Verify(restarted.SetConsent(TelemetryConsent.Declined) &&
           !File.Exists(restarted.PendingFile) &&
           !restarted.SaveOnNormalExit("v5.13.11.1"),
           "Recusa apaga pendencias e interrompe coleta");
    var refused = new AnonymousTelemetry(temp);
    Verify(refused.Consent == TelemetryConsent.Declined, "Nao repete aviso apos recusa");
    Verify(refused.ReviewableReport("v5.13.11.3") is null,
        "Recusa impede revisao e compartilhamento de pendencias");

    // Um diretorio no lugar do arquivo simula falha de persistencia sem depender de ACL/root.
    var blocked = new AnonymousTelemetry(Path.Combine(temp, "blocked-consent"));
    Verify(blocked.SetConsent(TelemetryConsent.LocalOnly), "Prepara revogacao com disco bloqueado");
    blocked.Record("APP.FALHA_INTERFACE");
    blocked.SaveOnNormalExit("v5.13.11.3");
    File.Delete(blocked.ConsentFile);
    Directory.CreateDirectory(blocked.ConsentFile);
    Verify(!blocked.SetConsent(TelemetryConsent.Declined) &&
        blocked.Consent == TelemetryConsent.Declined && !File.Exists(blocked.PendingFile),
        "Falha ao persistir recusa ainda interrompe coleta e apaga pendencia");
    blocked.Record("APP.FALHA_INTERFACE");
    Verify(!blocked.SaveOnNormalExit("v5.13.11.3") &&
        blocked.ReviewableReport("v5.13.11.3") is null,
        "Revogacao com erro impede nova coleta, salvamento e compartilhamento");
    Verify(!blocked.SetConsent(TelemetryConsent.LocalOnly) &&
        blocked.Consent == TelemetryConsent.Declined,
        "Falha ao aceitar nao reativa coleta");

    var lockedReport = new AnonymousTelemetry(Path.Combine(temp, "blocked-report"));
    lockedReport.SetConsent(TelemetryConsent.LocalOnly);
    Directory.CreateDirectory(lockedReport.PendingFile);
    Verify(!lockedReport.SetConsent(TelemetryConsent.Declined) &&
        lockedReport.Consent == TelemetryConsent.Declined,
        "Falha de exclusao e informada sem manter coleta ativa");
    string invalidConsentFolder = Path.Combine(temp, "invalid-consent");
    Directory.CreateDirectory(invalidConsentFolder);
    foreach (string invalid in new[]
    {
        "[]", "null", "{\"schema\":1,\"state\":2,\"extra\":true}",
        "{\"schema\":1,\"state\":1,\"state\":2}",
        "{\"schema\":1,\"schema\":1,\"state\":2}",
        "{\"schema\":1,\"state\":99999999999999999}",
        "{\"schema\":1,\"state\":\"2\"}", "{\"schema\":2,\"state\":2}"
    })
    {
        File.WriteAllText(Path.Combine(invalidConsentFolder, "consentimento-v1.json"), invalid);
        var invalidTracker = new AnonymousTelemetry(invalidConsentFolder);
        Verify(invalidTracker.Consent == TelemetryConsent.NotAsked &&
            !invalidTracker.SaveOnNormalExit("v5.13.11.4"),
            "Consentimento ambiguo ou adulterado nao ativa coleta: " + invalid);
    }
    string trailingNewline = JsonSerializer.Serialize(new
    {
        schema = 1, version = "v5.13.11.4\n", exit = "normal",
        counts = new Dictionary<string, int> { ["MSIX.LEITURA"] = 1 }
    });
    Verify(GitHubDiagnosticShare.CreateReviewUrl(trailingNewline) is null,
        "Versao com quebra de linha final e rejeitada");

    if (OperatingSystem.IsWindows())
    {
        var expiring = new AnonymousTelemetry(Path.Combine(temp, "locked-expiry"));
        expiring.SetConsent(TelemetryConsent.LocalOnly);
        expiring.Record("APP.FALHA_INTERFACE");
        expiring.SaveOnNormalExit("v5.13.11.4");
        var expiredReader = new AnonymousTelemetry(Path.Combine(temp, "locked-expiry"));
        File.SetLastWriteTimeUtc(expiring.PendingFile, DateTime.UtcNow.AddDays(-8));
        using (var readLock = new FileStream(expiring.PendingFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            Verify(expiredReader.ReviewableReport("v5.13.11.4") is null && File.Exists(expiring.PendingFile),
                "Relatorio expirado nao pode ser revisado mesmo quando o Windows impede sua exclusao");
        Verify(expiredReader.ReviewableReport("v5.13.11.4") is null && !File.Exists(expiring.PendingFile),
            "Relatorio expirado e apagado quando o bloqueio termina");
    }
    var uniqueTemp = new AnonymousTelemetry(Path.Combine(temp, "unique-temp"));
    Directory.CreateDirectory(Path.GetDirectoryName(uniqueTemp.ConsentFile)!);
    string predictableTemp = uniqueTemp.ConsentFile + ".tmp";
    File.WriteAllText(predictableTemp, "sentinela-nao-modificar");
    Verify(uniqueTemp.SetConsent(TelemetryConsent.LocalOnly) &&
        File.ReadAllText(predictableTemp) == "sentinela-nao-modificar",
        "Gravacao atomica nao trunca nem segue temporario previsivel preexistente");
    Verify(!Directory.EnumerateFiles(Path.GetDirectoryName(uniqueTemp.ConsentFile)!, "*.tmp")
        .Any(p => p != predictableTemp),
        "Temporarios exclusivos sao removidos apos gravacao");

    if (OperatingSystem.IsWindows())
    {
        string realFolder = Path.Combine(temp, "real-storage");
        string linkedFolder = Path.Combine(temp, "linked-storage");
        Directory.CreateDirectory(realFolder);
        bool linkedCreated = false;
        try { Directory.CreateSymbolicLink(linkedFolder, realFolder); linkedCreated = true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (linkedCreated)
        {
            var linkedCollector = new AnonymousTelemetry(linkedFolder);
            Verify(!linkedCollector.SetConsent(TelemetryConsent.LocalOnly) &&
                !File.Exists(Path.Combine(realFolder, "consentimento-v1.json")),
                "Pasta de privacidade redirecionada nao recebe gravacoes");
            Directory.Delete(linkedFolder);
        }
        else Console.WriteLine("SKIP: link de diretorio indisponivel; nao e teste aprovado.");
    }
    Console.WriteLine("OK: privacidade, retomada, expiracao e falhas de disco; nenhuma rede foi usada.");
}
finally
{
    try { Directory.Delete(temp, recursive: true); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
}
