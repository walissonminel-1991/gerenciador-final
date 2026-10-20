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
if (args.Contains("--live"))
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
    var live = await Task.Run(() => DriverInventory.Capture(timeout.Token)).WaitAsync(timeout.Token);
    Console.WriteLine($"WMI quantidade={live.Items.Count} parcial={live.Partial} status={live.Status}");
    Check(live.Items.Count > 0 && !live.Partial, "Consulta WMI somente leitura concluida no runner Windows");
}
