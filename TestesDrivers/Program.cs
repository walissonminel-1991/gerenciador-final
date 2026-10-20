using GerenciadorAplicativos;

void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    Console.WriteLine("OK " + message);
}
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
Check(System.Runtime.InteropServices.Marshal.SizeOf<DriverStoreInventory.NativePackage>() == (IntPtr.Size == 8 ? 100 : 72),
    "Layout nativo DISM corresponde ao tamanho documentado dos campos");
Check(System.Runtime.InteropServices.Marshal.OffsetOf<DriverStoreInventory.NativePackage>("CatalogFile").ToInt32() == (IntPtr.Size == 8 ? 20 : 12) &&
    System.Runtime.InteropServices.Marshal.OffsetOf<DriverStoreInventory.NativePackage>("ProviderName").ToInt32() == (IntPtr.Size == 8 ? 60 : 36),
    "Offsets do ABI DISM usam pack 1, sem alinhamento padrao de ponteiros");
var pkg = new DriverStoreEntry("OEM1.INF", "Vendor", "1.0", "Display", false, true, 0);
var store = new DriverStoreResult(new[] { pkg, pkg with { PublishedName = "oem99.inf" } }, false, "ok");
var joined = DriverStoreInventory.Correlate(store, Complete(a, a, b));
Check(joined[0].ObservedDevices == 2 && joined[0].Package.BootCritical,
    "Correlacao INF preserva compartilhamento e protecao de boot");
Check(joined[1].LinkLabel == "Sem vínculo observado", "Sem vinculo nao e classificacao de lixo");
Check(DriverStoreInventory.Correlate(store, null).All(x => x.ObservedDevices is null && x.EvidencePartial),
    "Consulta de dispositivos ausente nao inventa zero vinculos");
Check(DriverStoreInventory.Correlate(store, new([], true, "parcial")).All(x => x.EvidencePartial),
    "Correlacao conserva incerteza da leitura parcial");
Check(DriverStoreInventory.Correlate(store, Complete(a with { Version = "1.0.0.0" }))[0].VersionConflicts == 0,
    "Versoes equivalentes nao criam divergencia artificial");
Check(DriverStoreInventory.Correlate(store, Complete(a with { Version = "2.0" }))[0].VersionConflicts == 1,
    "Mesmo INF com versao divergente nao e confirmacao de versao ativa");
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    Check(DriverStoreInventory.Capture(cancelled.Token).Partial, "Cancelamento anterior a consulta DISM nao abre sessao nativa");
}
if (args.Contains("--live"))
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
    var live = await Task.Run(() => DriverInventory.Capture(timeout.Token)).WaitAsync(timeout.Token);
    Console.WriteLine($"WMI quantidade={live.Items.Count} parcial={live.Partial} status={live.Status}");
    Check(live.Items.Count > 0 && !live.Partial, "Consulta WMI somente leitura concluida no runner Windows");
    using var storeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    var packages = await Task.Run(() => DriverStoreInventory.Capture(storeTimeout.Token)).WaitAsync(storeTimeout.Token);
    Console.WriteLine($"DISM quantidade={packages.Items.Count} parcial={packages.Partial} status={packages.Status}");
    Check(packages.Items.Count > 0 && !packages.Partial, "Consulta DISM real somente leitura concluida no runner");
    Check(packages.Items.All(p => p.PublishedName.EndsWith(".inf", StringComparison.OrdinalIgnoreCase)),
        "Nomes publicados do retorno nativo possuem formato INF");
}
