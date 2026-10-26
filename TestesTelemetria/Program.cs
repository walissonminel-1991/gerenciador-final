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
    Verify(!Directory.Exists(temp) && tracker.Consent == TelemetryConsent.NotAsked,
        "Simulacao parte de um computador sem preferencia registrada");
    Verify(tracker.ExplainLocalCollection("v5.13.11.8").Contains("COLETA DESATIVADA"),
        "Explicacao em linguagem simples mostra coleta desativada");
    string offlineExample = AnonymousTelemetry.SimulateContributionWithoutSending("v5.13.11.8");
    Verify(offlineExample.Contains("SIMULAÇÃO CONCLUÍDA") &&
           offlineExample.Contains("INVENTADOS") &&
           offlineExample.Contains("Nenhum arquivo foi criado") &&
           !Directory.Exists(temp) &&
           tracker.Consent == TelemetryConsent.NotAsked,
        "Simulacao offline nao coleta, nao cria arquivos e nao altera consentimento");
    bool invalidVersionBlocked = false;
    try { AnonymousTelemetry.SimulateContributionWithoutSending("../program.exe"); }
    catch (ArgumentException) { invalidVersionBlocked = true; }
    Verify(invalidVersionBlocked, "Simulacao recusa versao arbitraria");
    tracker.Record("APP.FALHA_INTERFACE");
    Verify(tracker.Consent == TelemetryConsent.NotAsked &&
           !File.Exists(tracker.ConsentFile) && !File.Exists(tracker.PendingFile),
        "Sem opt-in nao cria relatorios nem preferencia implicita");
    Verify(!tracker.SaveOnNormalExit("v5.13.11.1"), "Sem consentimento nao gera relatorio");

    Verify(tracker.SetConsent(TelemetryConsent.LocalOnly), "Aceitacao local explicita");
    Verify(tracker.ExplainLocalCollection("v5.13.11.8").Contains("COLETA LOCAL ATIVA"),
        "Participacao aceita e exibida sem linguagem de programacao");
    tracker.Record("DESEMPENHO.LOCAL etapa=ResidualFilter duracao_ms=12.34 ram_depois_bytes=120000000");
    Verify(!tracker.Preview("v5.13.11.17", false).Contains("DESEMPENHO") &&
           !tracker.Preview("v5.13.11.17", false).Contains("120000000"),
        "Métricas locais não ampliam o esquema de contribuição voluntária");
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
    Verify(!DiagnosticPayloadPolicy.IsValidReport(trailingNewline),
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
            string foreignConsent = Path.Combine(realFolder, "consentimento-v1.json");
            string foreignPending = Path.Combine(realFolder, "diagnostico-pendente-v1.json");
            File.WriteAllText(foreignConsent, "{\"schema\":1,\"state\":2}");
            File.WriteAllText(foreignPending, validReport);
            File.SetLastWriteTimeUtc(foreignPending, DateTime.UtcNow.AddDays(-8));
            var linkedCollector = new AnonymousTelemetry(linkedFolder);
            Verify(linkedCollector.Consent == TelemetryConsent.NotAsked &&
                File.Exists(foreignPending) && File.ReadAllText(foreignPending) == validReport,
                "Construtor não importa consentimento nem expira relatório através de pasta redirecionada");
            Verify(!linkedCollector.SetConsent(TelemetryConsent.LocalOnly) &&
                File.ReadAllText(foreignConsent) == "{\"schema\":1,\"state\":2}" &&
                !linkedCollector.DeletePending() && File.Exists(foreignPending),
                "Pasta de privacidade redirecionada não recebe gravações ou exclusões");
            Directory.Delete(linkedFolder);

            string leafFolder = Path.Combine(temp, "linked-leaves");
            Directory.CreateDirectory(leafFolder);
            string leafConsent = Path.Combine(leafFolder, "consentimento-v1.json");
            File.CreateSymbolicLink(leafConsent, foreignConsent);
            Verify(new AnonymousTelemetry(leafFolder).Consent == TelemetryConsent.NotAsked,
                "Link na folha não pode importar consentimento de outro arquivo");
            File.Delete(leafConsent);
            var leafCollector = new AnonymousTelemetry(leafFolder);
            Verify(leafCollector.SetConsent(TelemetryConsent.LocalOnly), "Prepara revisão de folha redirecionada");
            File.SetLastWriteTimeUtc(foreignPending, DateTime.UtcNow);
            File.CreateSymbolicLink(leafCollector.PendingFile, foreignPending);
            Verify(leafCollector.ReviewableReport("v5.13.11.4") is null &&
                leafCollector.DeletePending() && File.ReadAllText(foreignPending) == validReport,
                "Revisão rejeita link de arquivo e remoção apaga só a entrada local");
        }
        else Console.WriteLine("SKIP: link de diretorio indisponivel; nao e teste aprovado.");

        string logFolder = Path.Combine(temp, "protected-log");
        Directory.CreateDirectory(logFolder);
        string logPath = Path.Combine(logFolder, "GerenciadorFinal-Diagnostico-" + DiagnosticTrace.VersionLabel + ".log");
        string outsideLog = Path.Combine(temp, "sentinela-fora-do-log.txt");
        File.WriteAllText(outsideLog, "sentinela-não-truncar");
        if (!TelemetryNativeFixtures.CreateHardLink(logPath, outsideLog))
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        Verify(DiagnosticTrace.StartInFolders([logFolder]) &&
            DiagnosticTrace.Write("TESTE.PRIMEIRA_LINHA") &&
            File.ReadAllText(outsideLog) == "sentinela-não-truncar",
            "Log pré-criado como hard link não trunca nem modifica arquivo externo");
        Verify(File.ReadAllText(logPath).Contains("TESTE.PRIMEIRA_LINHA"),
            "Log protegido continua acessível a leitores comuns durante a sessão");
        bool logReplacementBlocked = false;
        try { File.Delete(logPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logReplacementBlocked = true; }
        Verify(logReplacementBlocked && DiagnosticTrace.Write("TESTE.APÓS_TROCA_RECUSADA"),
            "Handle de sessão impede substituir a folha entre duas gravações");
        bool parentReplacementBlocked = false;
        try { Directory.Move(logFolder, logFolder + "-movido"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { parentReplacementBlocked = true; }
        Verify(parentReplacementBlocked, "Ancestrais do log não podem ser substituídos durante a sessão");
        var writes = new bool[16];
        Parallel.For(0, writes.Length, i => writes[i] = DiagnosticTrace.Write("TESTE.CONCORRENTE." + i));
        string[] logLines = File.ReadAllLines(logPath);
        Verify(writes.All(ok => ok) && Enumerable.Range(0, writes.Length).All(i =>
            logLines.Count(line => line.EndsWith("TESTE.CONCORRENTE." + i, StringComparison.Ordinal)) == 1),
            "Gravações concorrentes mantêm cada ocorrência completa exatamente uma vez");
        DiagnosticTrace.Stop();
        Verify(!DiagnosticTrace.Write("TESTE.APÓS_FECHAR") &&
            DiagnosticTrace.StartInFolders([logFolder]) && DiagnosticTrace.Write("TESTE.REABERTURA"),
            "Encerrar libera handles e uma nova sessão reinicia o log de forma segura");
        DiagnosticTrace.Stop();
        if (linkedCreated)
        {
            File.Delete(logPath);
            File.CreateSymbolicLink(logPath, outsideLog);
            Verify(DiagnosticTrace.StartInFolders([logFolder]) && DiagnosticTrace.Write("TESTE.LINK") &&
                File.ReadAllText(outsideLog) == "sentinela-não-truncar",
                "Log pré-criado como link simbólico não modifica seu destino");
            DiagnosticTrace.Stop();
            string redirectedLog = Path.Combine(temp, "redirected-log");
            Directory.CreateSymbolicLink(redirectedLog, logFolder);
            string fallbackLog = Path.Combine(temp, "fallback-log");
            string protectedContents = File.ReadAllText(logPath);
            Verify(DiagnosticTrace.StartInFolders([redirectedLog, fallbackLog]) &&
                Path.GetDirectoryName(DiagnosticTrace.LogFilePath) == fallbackLog &&
                File.ReadAllText(logPath) == protectedContents,
                "Pasta redirecionada é recusada sem tocar seus arquivos e há fallback explícito");
            DiagnosticTrace.Stop();
            Directory.Delete(redirectedLog);
        }
    }
    var beta = new AnonymousTelemetry(Path.Combine(temp, "beta-simulado"));
    Verify(beta.Consent == TelemetryConsent.NotAsked && beta.PendingForRemote() is null,
        "Sem opt-in remoto nenhuma pendencia pode ser transmitida");
    Verify(beta.SetConsent(TelemetryConsent.LocalOnly) && beta.PendingForRemote() is null,
        "Consentimento local antigo nao equivale a consentimento remoto");
    beta.Record("APP.FALHA_INTERFACE usuario=segredo"); // elimina parametros
    Verify(beta.SaveOnNormalExit("v5.13.11.66") && beta.PendingForRemote() is null &&
        File.Exists(beta.PendingFile),
        "Coleta local permanece estritamente local");
    Verify(beta.SetConsent(TelemetryConsent.RemoteBeta) &&
        !File.Exists(beta.PendingFile) && beta.PendingForRemote() is null,
        "Opt-in remoto descarta pendencia criada com autorizacao apenas local");
    beta.Record("MSIX.LEITURA caminho=C:/Privado");
    Verify(beta.SaveOnNormalExit("v5.13.11.66") &&
        DiagnosticPayloadPolicy.IsValidReport(beta.PendingForRemote()) &&
        !beta.PendingForRemote()!.Contains("Privado", StringComparison.Ordinal) &&
        !beta.PendingForRemote()!.Contains("APP.FALHA_INTERFACE", StringComparison.Ordinal),
        "Apenas eventos posteriores ao opt-in remoto podem ser enviados");
    Verify(beta.SetConsent(TelemetryConsent.Declined) &&
        beta.PendingForRemote() is null && !File.Exists(beta.PendingFile),
        "Revogacao interrompe leitura de pendencia e remove arquivo");
    // O runner net10.0 valida o gravador sem carregar HTTP/WinForms do produto.
    // A ausencia de endpoint e o aceite bloqueado sao testados no TestesLayout
    // (que referencia o executavel real) e pela politica de CI.
    Console.WriteLine("OK: privacidade, retomada, expiracao e falhas de disco; nenhuma rede foi usada.");
}
finally
{
    DiagnosticTrace.Stop();
    try { Directory.Delete(temp, recursive: true); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
}

internal static class TelemetryNativeFixtures
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW",
        CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CreateHardLinkNative(string link, string existing, IntPtr attributes);
    internal static bool CreateHardLink(string link, string existing) => CreateHardLinkNative(link, existing, IntPtr.Zero);
}
