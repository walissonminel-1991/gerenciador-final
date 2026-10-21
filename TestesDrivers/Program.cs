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
Check(accepted == 159 && !TestDiagnosticDetail.Record("AMBIENTE",
    ("largura", 1280), ("altura", 720), ("dpi", 96), ("logicos", 4)),
    "Limite global de 160 observacoes nao reinicia ao alternar o perfil PC FORTE/FRACO");
TestDiagnosticDetail.SetProfile(0, line => { emitted.Add(line); return true; });
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
if (args.Contains("--live"))
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
    var live = await Task.Run(() => DriverInventory.Capture(timeout.Token)).WaitAsync(timeout.Token);
    Console.WriteLine($"WMI quantidade={live.Items.Count} parcial={live.Partial} status={live.Status}");
    Check(live.Items.Count > 0 && !live.Partial, "Consulta WMI somente leitura concluida no runner Windows");
}
