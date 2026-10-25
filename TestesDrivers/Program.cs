using GerenciadorAplicativos;

void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    Console.WriteLine("OK " + message);
}
Check(DriverInventory.ReportedDeviceName("Nome Windows", "Descricao alternativa") == "Nome Windows" &&
    DriverInventory.ReportedDeviceName("  ", " Descricao PnP ") == "Descricao PnP" &&
    DriverInventory.ReportedDeviceName(null, "") == "",
    "Descricao PnP so e fallback para nome ausente, sem inventar identidade");
var emitted = new List<string>();
TestDiagnosticDetail.SetProfile(1, line => { emitted.Add(line); return true; });
Check(TestDiagnosticDetail.Enabled && emitted.Single().Contains("PC_FORTE"),
    "Coleta de testes e voluntaria e seleciona o computador");
Check(TestDiagnosticDetail.Record("DRIVERS",
    ("associacoes", 168), ("ids_unicos", 166), ("sem_nome", 3),
    ("sem_fornecedor", 3), ("sem_versao", 1), ("versao_incomum", 2),
    ("sem_inf", 3), ("assinatura_indeterminada", 1),
    ("presenca_indeterminada", 2), ("nao_presentes", 0), ("parcial", 0)),
    "Registra apenas contagens autorizadas sem nomes ou caminhos");
Check(!TestDiagnosticDetail.TryFormat(1, "DRIVERS",
    new (string, int)[] { ("id_dispositivo", 99) }, out _) &&
    !TestDiagnosticDetail.Record("INDEVIDO", ("caminho", 1)) &&
    !TestDiagnosticDetail.Record("AMBIENTE", ("largura", -1)),
    "Contrato recusa chaves desconhecidas, categorias livres e valores invalidos");
TestDiagnosticDetail.SetProfile(0, line => { emitted.Add(line); return true; });
int countBeforeDisableCheck = emitted.Count;
Check(!TestDiagnosticDetail.Enabled &&
    !TestDiagnosticDetail.Record("AMBIENTE", ("largura", 1920), ("altura", 1080),
        ("dpi", 96), ("logicos", 8)) && emitted.Count == countBeforeDisableCheck,
    "Desligar coleta estendida impede gravacao adicional");
TestDiagnosticDetail.SetProfile(2, line => { emitted.Add(line); return true; });
int accepted = 0;
for (int i = 0; i < 170; i++)
    if (TestDiagnosticDetail.Record("AMBIENTE", ("largura", 1280),
        ("altura", 720), ("dpi", 96), ("logicos", 4))) accepted++;
Check(accepted == 156 && !TestDiagnosticDetail.Record("AMBIENTE",
    ("largura", 1280), ("altura", 720), ("dpi", 96), ("logicos", 4)),
    "Limite global de 160 observacoes nao reinicia ao alternar o perfil PC FORTE/FRACO");
TestDiagnosticDetail.SetProfile(0, line => { emitted.Add(line); return true; });
Check(TestDiagnosticDetail.TryFormat(1, "RESIDUOS",
    new (string, int)[] {
        ("grupos", 44), ("locais", 161), ("examinados", 4041),
        ("grupos_avaliados", 867), ("candidatos_gerais", 43),
        ("filtrados_ativos", 213), ("filtrados_sem_evidencia", 611),
        ("grupos_omitidos", 0), ("locais_omitidos", 0), ("fontes", 13),
        ("indisponiveis", 0), ("falhas_leitura", 2),
        ("nao_avaliados_desconhecidos", 1), ("parcial", 1)
    }, out string resumoResiduos) && resumoResiduos.Contains("grupos_avaliados=867") &&
    213 + 611 + 43 == 867,
    "Detalhamento de residuos separa grupos examinados e resultados exibidos");
Check(TestDiagnosticDetail.TryFormat(1, "INVENTARIO", new (string, int)[] {
    ("total", 174), ("sem_nome", 0), ("sem_editor", 9), ("sem_versao", 45),
    ("desinstalaveis", 140), ("so_inventario", 33),
    ("sem_desinstalacao", 34), ("sem_desinstalacao_fora_flag", 1),
    ("parcial", 0)
}, out var recordInventario) &&
    recordInventario.Contains("so_inventario=33") &&
    recordInventario.Contains("sem_desinstalacao=34"),
    "Inventario distingue flag e disponibilidade de desinstalador sem perder entradas");
Check(TestDiagnosticDetail.TryFormat(1, "FONTES", new (string, int)[] {
    ("falha_registro", 0), ("falha_appdata", 1),
    ("falha_programdata", 1), ("falha_programfiles", 0),
    ("falha_startmenu", 0), ("falha_integracoes", 0),
    ("falha_outras", 0), ("indisp_registro", 0),
    ("indisp_appdata", 0), ("indisp_outras", 0)
}, out var recordFontes) &&
    recordFontes.Contains("falha_programdata=1"),
    "Falha de fonte identificada apenas por categoria segura e nao pelo caminho real");
DriverStoreTests.Run(Check);
ReadOnlyDriverTests.Run(Check);
DiagnosticFailureTests.Run(Check);
var a = new DriverInventoryEntry("PCI\\ONE", "Video", "Vendor", "1.0", "oem1.inf", true, null);
var b = a with { DeviceId = "PCI\\TWO", DeviceName = "Outro dispositivo" };
var c = a with { DeviceId = "USB\\THREE", Signed = null };
var result = DriverInventory.Correlate(new[] { a, a, b, c }, new Dictionary<string, bool?>
    { ["pci\\one"] = true, ["PCI\\TWO"] = false });
Check(result.Count == 3, "Mesmo INF em dispositivos distintos nao e duplicata");
Check(result.Single(d => d.DeviceId == a.DeviceId).Present == true, "Correlacao nao diferencia maiusculas");
Check(result.Single(d => d.DeviceId == b.DeviceId).PresenceLabel == "Não presente", "Ausencia explicita e separada de desconhecimento");
Check(result.Single(d => d.DeviceId == c.DeviceId).Present is null && c.SignatureLabel == "Indeterminado", "Ausencia de evidencias nao inventa presenca nem assinatura");
Check(DriverInventory.Correlate(new[] { a, a with { Signed = false } }, new Dictionary<string, bool?>()).Count == 2,
    "Evidencias conflitantes de assinatura nao sao fundidas");
var named = DriverInventory.Correlate(new[] { a, b with { DeviceName = " " }, c with { DeviceName = "" } },
    new Dictionary<string, bool?> { ["PCI\\TWO"] = true },
    new Dictionary<string, string> { ["pci\\one"] = "Nome alternativo", ["pci\\two"] = "Dispositivo PnP" });
Check(named.Single(d => d.DeviceId == b.DeviceId).DeviceName == "Dispositivo PnP" &&
    named.Single(d => d.DeviceId == b.DeviceId).Present == true,
    "Nome PnP preenche nome ausente apenas para o mesmo ID sem diferenciar caixa");
Check(named.Single(d => d.DeviceId == a.DeviceId).DeviceName == a.DeviceName &&
    named.Single(d => d.DeviceId == c.DeviceId).DeviceName == "",
    "Correlacao preserva nome original e nao inventa nome para ID sem evidencia");
Check(DriverInventory.Correlate(new[] { a with { Version = "versao do fabricante" } },
    new Dictionary<string, bool?>())[0].Version == "versao do fabricante",
    "Versao bruta do WMI preservada sem converter, truncar nem inventar incompatibilidade");
// Historico usa exclusivamente um diretorio temporario isolado nestes testes.
string folder = Path.Combine(Path.GetTempPath(), "GF-driver-history-tests-" + Guid.NewGuid().ToString("N"));
string file = Path.Combine(folder, "history.json");
var now = DateTimeOffset.UtcNow;
DriverInventoryResult Complete(params DriverInventoryEntry[] items) => new(items, false, "ok");
try
{
    var first = DriverHistory.Record(file, Complete(a, b), now);
    Check(first.Saved && first.Baseline, "Primeira consulta estabelece base local");
    string baseline = File.ReadAllText(file);
    Check(!baseline.Contains(a.DeviceId) && !baseline.Contains(a.DeviceName) && !baseline.Contains(a.Provider) &&
        !baseline.Contains(a.InfName), "Arquivo nao contem nomes nem IDs brutos");
    var same = DriverHistory.Record(file, Complete(b, a, a), now.AddSeconds(1));
    Check(same.Saved && !same.Baseline && same.Added + same.Missing + same.Changed == 0,
        "Ordem e duplicatas nao criam mudancas historicas");
    var changed = DriverHistory.Record(file, Complete(a with { Version = "2.0" }, c), now.AddSeconds(2));
    Check(changed.Added == 1 && changed.Missing == 1 && changed.Changed == 1,
        "Distinguir novo, nao observado e versao alterada");
    var presence = DriverHistory.Record(file, Complete(a with { Version = "2.0", Present = false }, c), now.AddSeconds(3));
    Check(presence.Changed == 1 && presence.Missing == 0, "Nao presente e diferente de nao observado");
    string saved = File.ReadAllText(file);
    Check(!DriverHistory.Record(file, new([], true, "parcial"), now.AddSeconds(4)).Saved && File.ReadAllText(file) == saved,
        "Consulta parcial preserva base byte a byte");
    Check(!DriverHistory.Record(file, Complete(), now.AddSeconds(4)).Saved && File.ReadAllText(file) == saved,
        "Consulta vazia nao declara remocao de todos os dispositivos");
    Check(!DriverHistory.Record(file, Complete(a), now.AddDays(-1)).Saved && File.ReadAllText(file) == saved,
        "Relogio regressivo nao substitui base mais recente");
    Check(!DriverHistory.Record(file, Complete(a with { DeviceId = "" }), now.AddSeconds(4)).Saved,
        "Identidade vazia rejeitada");
    using (var busy = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        Check(!DriverHistory.Record(file, Complete(a), now.AddSeconds(5)).Saved && File.ReadAllText(file) == saved,
            "Outra instancia gravando impede sobrescrita concorrente");
    File.Delete(file + ".lock");
    File.WriteAllText(file, "{quebrado");
    Check(!DriverHistory.Record(file, Complete(a), now).Saved && File.ReadAllText(file) == "{quebrado",
        "Arquivo corrompido preservado sem reinicio silencioso");
    File.WriteAllText(file, baseline.Replace("\"Schema\":1", "\"Schema\":99"));
    Check(!DriverHistory.Record(file, Complete(a), now).Saved, "Esquema futuro nao e sobrescrito");
    File.WriteAllText(file, baseline);
    for (int i = 1; i <= 25; i++)
        Check(DriverHistory.Record(file, Complete(a with { Version = i.ToString() }), now.AddMinutes(i)).Saved,
            "Persistir rodada historica " + i);
    var state = System.Text.Json.JsonSerializer.Deserialize<DriverHistoryState>(File.ReadAllText(file))!;
    Check(DriverHistory.Record(file, Complete(a with { Version = "26" }), now.AddMinutes(26)).Timeline.Split(Environment.NewLine).Length == 20, "Historico retido disponivel para consulta na interface");
    Check(state.Changes.Count == 20, "Retencao limitada a vinte mudancas");
    Check(Directory.GetFiles(folder, "*.tmp").Length == 0, "Nenhum temporario apos gravacao");
    var before = DriverHistory.Snapshot(Complete(a));
    var after = DriverHistory.Snapshot(Complete(a with { DeviceName = "Nome traduzido", DeviceId = "pci\\one" }));
    Check(DriverHistory.Compare(before, after, now).Changed == 0, "Nome traduzido e caixa do ID nao alteram historico");
    Check(DriverHistory.Compare(before, DriverHistory.Snapshot(Complete(a, a with { Signed = false })), now).Changed == 1,
        "Assinatura conflitante preservada na comparacao");
}
finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
var links = DriverPackageLinks.Analyze(Complete(a, b, a with { InfName = "OEM1.INF", Version = "2", Signed = false },
    c with { InfName = "../oem2.inf" }, c with { InfName = "" }));
Check(links.Packages.Count == 1 && links.Packages[0].Devices == 2 && links.Packages[0].Versions == 2,
    "INF compartilhado preserva dispositivos e versoes divergentes");
Check(links.Packages[0].ConflictingSignatures && links.WithoutInf == 2,
    "Conflitos de assinatura e INF ausente/invalido ficam explicitos");
Check(DriverPackageLinks.Analyze(new([], true, "parcial")).Partial,
    "Correlacao conserva limitacao da origem");
// Prova negativa sem WMI: cancelamento nao aceita resultado atrasado.
var coordinator = new DriverQueryCoordinator();
var oldToken = coordinator.Begin(TimeSpan.FromSeconds(25));
Check(coordinator.InProgress && coordinator.CanPublish(oldToken),
    "Drivers: leitura corrente pode publicar enquanto valida");
Check(coordinator.RequestCancel() && !coordinator.CanPublish(oldToken) &&
    !coordinator.RequestCancel(), "Drivers: cancelamento impede publicacao e duplicatas");
coordinator.End(oldToken);
Check(!coordinator.InProgress, "Drivers: estado liberado apos cancelamento");
var nextToken = coordinator.Begin(TimeSpan.FromSeconds(25));
Check(coordinator.CanPublish(nextToken) && !coordinator.CanPublish(oldToken),
    "Drivers: token de leitura anterior nunca autoriza substituir nova lista");
coordinator.End(nextToken);
// Regressao para Cancel() sincronico retomando o await e restaurando
// o botao antes do manipulador Click terminar: ultima escrita deve ser
// da continuacao, nunca uma desabilitacao posterior.
var race = new DriverQueryCoordinator();
var raceToken = race.Begin(TimeSpan.FromSeconds(20));
string textButton = "Cancelar";
bool buttonEnabled = true;
using (raceToken.Register(() =>
{
    textButton = "Consultar drivers";
    buttonEnabled = true;
}))
{
    textButton = "Cancelando...";
    Check(race.RequestCancel() && textButton == "Consultar drivers" &&
        buttonEnabled, "Drivers: cancelamento reentrante nao deixa botao travado");
}
race.End(raceToken);
// CI Windows sempre exercita a ABI nativa somente leitura; fixtures continuam portáveis.
if (args.Contains("--live") || OperatingSystem.IsWindows())
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
    var live = await Task.Run(() => DriverStoreAudit.CaptureWithPackages(timeout.Token)).WaitAsync(timeout.Token);
    Console.WriteLine($"WMI quantidade={live.Items.Count} parcial={live.Partial} status={live.Status}");
    Check(live.Items.Count > 0 && !live.Partial, "Consulta WMI somente leitura concluida no runner Windows");
    foreach (var group in live.Store!.Packages.GroupBy(x => (x.State, x.FailureStage, x.NativeError)))
        Console.WriteLine($"DRIVER_STORE_CI estado={group.Key.State} etapa={group.Key.FailureStage} erro=0x{unchecked((uint)group.Key.NativeError):X8} quantidade={group.Count()}");
    Check(live.Store is not null && live.Store.Packages.Any(x => x.State == DriverStoreState.Mapped) &&
        live.Store.Packages.All(x => !x.CanMaintain),
        "SetupAPI nativa confirma ao menos um round trip INF/Driver Store no runner sem manutenção");
    Console.WriteLine($"DRIVER_STORE_CI pacotes={live.Store!.Packages.Count} mapeados={live.Store.Packages.Count(x => x.State == DriverStoreState.Mapped)} parcial={live.Store.Partial}");
}

if (OperatingSystem.IsWindows())
{
    using var nativeLimit = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var inventory = await Task.Run(() => DriverStoreAudit.CaptureWithPackages(nativeLimit.Token)).WaitAsync(nativeLimit.Token);
    Check(inventory.PublishedPackages is { Packages.Count: > 0 }, "D2: enumeração SetupAPI real no Windows descartável retorna INFs publicados");
    Check(inventory.PublishedPackages!.Packages.All(p => !p.CanMaintain), "R6: pacotes nativos nunca autorizam manutenção");
    Console.WriteLine($"D2_CI publicados={inventory.PublishedPackages.Packages.Count} enumeracao_completa={inventory.PublishedPackages.EnumerationComplete} parcial={inventory.PublishedPackages.Partial} eventos={inventory.WindowsHistory?.Events.Count} historico_parcial={inventory.WindowsHistory?.Partial} historico_indisponivel={inventory.WindowsHistory?.Unavailable}");
    var signatures = inventory.Store!.Packages.Where(p => p.State == DriverStoreState.Mapped).Take(24)
        .Select(p => DriverInfVerification.Verify(p, nativeLimit.Token)).ToArray();
    foreach (var group in signatures.GroupBy(s => (s.Trust, s.NativeError)))
        Console.WriteLine($"G2_CI estado={group.Key.Trust} erro=0x{unchecked((uint)group.Key.NativeError):X8} quantidade={group.Count()}");
    Check(signatures.Any(s => s.Trust == DriverInfTrust.VerifiedInf), "G2: assinatura criptográfica de pelo menos um INF instalado confirmada no runner");
    Check(signatures.All(s => !s.CanInstall && !s.CanMaintain), "G2: prova criptográfica real não autoriza escrita");
    string scratch = Path.Combine(Path.GetTempPath(), "GF-INF-unsigned-" + Guid.NewGuid().ToString("N") + ".inf");
    try
    {
        File.WriteAllText(scratch, "[Version]\r\nSignature=\"$Windows NT$\"\r\nClass=System\r\nClassGuid={4d36e97d-e325-11ce-bfc1-08002be10318}\r\nProvider=GF-Fixture\r\nDriverVer=10/25/2026,1.0.0.0\r\n");
        var negative = DriverInfVerification.InspectInf(scratch, nativeLimit.Token);
        Check(negative.Trust == DriverInfTrust.Rejected, "G2: INF sintético sem catálogo é recusado pela API criptográfica real");
    }
    finally { File.Delete(scratch); }
    if (args.Contains("--official-live"))
    {
        WindowsLogFormatProbe.Print();
        using var officialLimit = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        try
        {
            var official = await DriverOfficialLookup.Start(inventory, officialLimit.Token).WaitAsync(officialLimit.Token);
            Console.WriteLine($"G1_CI fonte=WindowsUpdate consulta_completa={!official.Partial} ofertas={official.Offered} vinculos={official.Matches.Count} nao_correspondidos={official.Unmatched}");
            Check(official.Matches.All(m => !m.CanInstall) && (!official.Partial || official.Matches.Count == 0),
                "G1: WUA real só emite correspondências consultivas e preserva falha da fonte");
            if (official.Partial) Console.WriteLine("G1_CI_INCONCLUSIVO fonte indisponível; consulta real não comprova compatibilidade ou ausência de ofertas.");
        }
        catch (OperationCanceledException)
        { Console.WriteLine("G1_CI_INCONCLUSIVO prazo nativo excedido; nenhuma correspondência publicada. Homologação da fonte permanece pendente."); }
    }
}
