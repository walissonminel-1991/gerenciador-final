using System.Text;
using System.Text.Json;
using GerenciadorAplicativos;

// G1.18: contrato v1 real, EXCLUSIVAMENTE em pasta de teste aleatória.
// O suite --drivers-readonly também exercita WinForms reais em G1.10–G1.17.
// Não consulta WMI/WUA/INF/SetupAPI, não lê DriverHistory.DefaultPath.
internal static class DriverHistoryRecoveryTests
{
    internal static void Run()
    {
        var device = new DriverInventoryEntry(@"PCI\G118\A", "Nome privado sintético G1.18",
            "Fornecedor sintético G1.18", "1.0", "oem118.inf", true, true);
        DriverInventoryResult Snap(string version) =>
            new([device with { Version = version }], false, "fotografia WMI sintética");
        var t0 = new DateTimeOffset(2026, 11, 14, 18, 0, 0, TimeSpan.Zero);
        var folder = Path.Combine(Path.GetTempPath(),
            "GF-G118-ISOLADO-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "drivers-v1.json");
        try
        {
            var baseline = DriverHistory.Record(path, Snap("1.0"), t0);
            Ensure(baseline.Saved && baseline.Baseline, "baseline íntegra não criada");
            byte[] original = File.ReadAllBytes(path);
            Ensure(original.Length is > 0 and < 2_000_000,
                "limite de tamanho v1 inesperado");
            string originalText = Encoding.UTF8.GetString(original);
            Ensure(!originalText.Contains(device.DeviceId, StringComparison.OrdinalIgnoreCase) &&
                   !originalText.Contains(device.DeviceName, StringComparison.OrdinalIgnoreCase) &&
                   !originalText.Contains(device.Provider, StringComparison.OrdinalIgnoreCase) &&
                   !originalText.Contains(device.InfName, StringComparison.OrdinalIgnoreCase),
                "identificador pessoal bruto exposto no arquivo de hashes");

            var equal = DriverHistory.Record(path, Snap("2.0"), t0);
            Ensure(!equal.Saved && equal.Message.Contains("não posterior", StringComparison.Ordinal) &&
                   File.ReadAllBytes(path).SequenceEqual(original),
                "timestamp igual aceitou fotografia conflitante");
            Ensure(!DriverHistory.Record(path, Snap("2.0"), t0.AddTicks(-1)).Saved &&
                   File.ReadAllBytes(path).SequenceEqual(original),
                "relógio regressivo substituiu histórico");

            // Provar serialização intraprocesso quando duas chamadas chegam
            // simultaneamente com MESMO instante, mas evidência diferente.
            var moment = t0.AddMinutes(1);
            var first = Task.Run(() => DriverHistory.Record(path, Snap("2.0"), moment));
            var second = Task.Run(() => DriverHistory.Record(path, Snap("3.0"), moment));
            Task.WaitAll(first, second);
            Ensure(first.Result.Saved != second.Result.Saved,
                "duas escritas com mesmo instante foram ambas aceitas");
            var current = ReadState(path);
            Ensure(current.Schema == 1 && current.At == moment &&
                   current.Devices.Count == 1,
                "sequência concorrente corrompeu o schema ou o instante");

            // Lock no estilo de segunda instância: falha segura, sem substituir
            // a base nem criar temporários.
            byte[] beforeBusy = File.ReadAllBytes(path);
            using (var held = new FileStream(path + ".lock", FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None))
            {
                var blocked = DriverHistory.Record(path, Snap("4.0"), moment.AddSeconds(1));
                Ensure(!blocked.Saved && File.ReadAllBytes(path).SequenceEqual(beforeBusy),
                    "conflito de lock alterou histórico v1");
                Ensure(Directory.GetFiles(folder, "*.tmp").Length == 0,
                    "conflito criou arquivo temporário");
            }

            // JSON inválido, schema desconhecido e arquivo grande: não
            // regenerar silenciosamente a base corrompida do usuário.
            File.WriteAllText(path, "{\"Schema\":1,\"At\":", Encoding.UTF8);
            byte[] malformed = File.ReadAllBytes(path);
            Ensure(!DriverHistory.Record(path, Snap("5.0"), moment.AddMinutes(2)).Saved &&
                   File.ReadAllBytes(path).SequenceEqual(malformed),
                "arquivo JSON truncado foi substituído");
            File.WriteAllText(path, JsonSerializer.Serialize(current with { Schema = 99 }));
            byte[] future = File.ReadAllBytes(path);
            Ensure(!DriverHistory.Record(path, Snap("5.0"), moment.AddMinutes(2)).Saved &&
                   File.ReadAllBytes(path).SequenceEqual(future),
                "schema futuro foi substituído");
            File.WriteAllBytes(path, new byte[2_000_001]);
            Ensure(!DriverHistory.Record(path, Snap("5.0"), moment.AddMinutes(2)).Saved &&
                   new FileInfo(path).Length == 2_000_001,
                "arquivo maior que limite foi sobrescrito");

            // Simular interrupção após criar temporário (não é recovery
            // automático): arquivo v1 íntegro deve ser fonte única da verdade.
            File.WriteAllBytes(path, beforeBusy);
            string orphan = path + ".interrompido.tmp";
            File.WriteAllText(orphan, "{\"Schema\":1,\"Devices\":{}", Encoding.UTF8);
            var resumed = DriverHistory.Record(path, Snap("6.0"),
                moment.AddMinutes(3));
            Ensure(resumed.Saved && !resumed.Baseline &&
                   ReadState(path).Schema == 1 && File.Exists(orphan) &&
                   File.ReadAllText(orphan).Contains("interrompido") == false,
                "temporário órfão interferiu na gravação atômica");
            Ensure(Directory.GetFiles(folder, "*.tmp").Length == 1,
                "gravação deixou novo temporário não previsto");

            var valid = File.ReadAllBytes(path);
            Ensure(!DriverHistory.Record(path, new([], true, "parcial sintético"),
                    moment.AddMinutes(4)).Saved &&
                   File.ReadAllBytes(path).SequenceEqual(valid),
                "snapshot parcial substituiu v1");
            Ensure(!DriverHistory.Record(path, new([], false, "vazio sintético"),
                    moment.AddMinutes(4)).Saved &&
                   File.ReadAllBytes(path).SequenceEqual(valid),
                "snapshot vazio substituiu v1");
            Ensure(!DriverHistory.Record(path, new([
                    device with { WithoutSignedDriverAssociation = true }],
                    false, "sem associação WMI sintética"),
                    moment.AddMinutes(4)).Saved &&
                   File.ReadAllBytes(path).SequenceEqual(valid),
                "ausência de associação WMI substituiu v1");

            // A retenção de 20 mudanças segue sendo responsabilidade do
            // contrato v1: verificar que a alteração temporal não a afetou.
            for (int i = 1; i <= 22; i++)
                Ensure(DriverHistory.Record(path, Snap("R" + i),
                    moment.AddMinutes(i + 4)).Saved,
                    "retenção: rodada " + i + " falhou");
            Ensure(ReadState(path).Changes.Count == 20 &&
                   Directory.GetFiles(folder, "*.tmp").Length == 1,
                "retenção v1 ou limpeza de temporários regrediu");
            Console.WriteLine("OK G1.18: timestamp igual bloqueado, concorrência, lock, arquivo inválido, schema, tamanho, temporários e retenção v1");
        }
        finally
        {
            // Somente o diretório criado nesta fixture. Nenhum caminho pessoal.
            Directory.Delete(folder, recursive: true);
        }
    }

    private static DriverHistoryState ReadState(string path) =>
        JsonSerializer.Deserialize<DriverHistoryState>(File.ReadAllText(path))
        ?? throw new Exception("G1.18: histórico v1 não pôde ser lido");

    private static void Ensure(bool yes, string reason)
    {
        if (!yes) throw new Exception("G1.18: " + reason);
    }
}
