using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GerenciadorAplicativos;

internal sealed record DriverHistoryChange(DateTimeOffset At, int Added, int Missing, int Changed);
internal sealed record DriverHistoryState(int Schema, DateTimeOffset At,
    Dictionary<string, string> Devices, List<DriverHistoryChange> Changes);
internal sealed record DriverHistoryResult(bool Saved, bool Baseline, int Added, int Missing, int Changed, string Message, string Timeline = "");

// Historico proprio: compara observacoes, nao comprova instalacao/desinstalacao.
// Nao persiste nomes, IDs de hardware, caminhos ou saida de comandos.
internal static class DriverHistory
{
    private static readonly object Gate = new();
    private const int MaxBytes = 2_000_000;
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    // A chave do histórico é a associação identificada pelo WMI, não um
    // HardwareID para casar ofertas ou instalar pacotes. IDs opacos sem barra
    // ou com '?' são literais e só podem ser comparados como observações WMI.
    // Recusar valores vazios, espaços periféricos, controles e entradas extensas.
    private static bool ValidAssociationId(string? id) =>
        id is { Length: > 0 and <= 4000 } &&
        id == id.Trim() && !id.Any(char.IsControl);

    internal static Dictionary<string, string> Snapshot(DriverInventoryResult inventory)
    {
        // O historico v1 representa associacoes WMI assinadas, nao dispositivos
        // problematicos que nao tenham uma linha em Win32_PnPSignedDriver.
        var associated = inventory.Items.Where(x => !x.WithoutSignedDriverAssociation).ToArray();
        var identities = inventory.Items.Select(x => x.DeviceId?.Trim().ToUpperInvariant()).ToArray();
        // G1.19: nem duas linhas WMI identicas comprovam uma unica instancia.
        // A linha sem associacao tambem nao pode compartilhar DeviceId com uma
        // linha associada. A captura registra SignedRows antes da correlacao:
        // qualquer perda de linhas brutas impede a substituicao do historico.
        if (inventory.Partial || inventory.Items.Count is 0 or > 4000 ||
            associated.Length == 0 ||
            // Validar a identidade WMI bruta ANTES de Trim(): não usar o
            // predicado de HardwareID, que rejeita formas WMI legítimas.
            // A fotografia integral continua obrigatória; nenhuma linha é
            // descartada silenciosamente para evitar falsas ausências.
            inventory.Items.Any(x => !ValidAssociationId(x.DeviceId)) ||
            identities.Distinct(StringComparer.Ordinal).Count() != identities.Length ||
            (inventory.SignedRows > 0 && inventory.SignedRows != associated.Length))
            throw new InvalidDataException("Consulta ambigua ou incompleta nao substitui historico.");
        // Uma associacao por DeviceId: preservar EXATAMENTE o hash legado v1
        // para cada linha unica (o GroupBy anterior gerava este mesmo texto).
        return associated.ToDictionary(x => Hash(x.DeviceId.Trim().ToUpperInvariant()),
            x => Hash(JsonSerializer.Serialize(new[] {
                x.Provider.Trim().ToUpperInvariant(), x.Version.Trim(),
                x.InfName.Trim().ToUpperInvariant(),
                x.Signed?.ToString() ?? "?", x.Present?.ToString() ?? "?" })));
    }
    internal static DriverHistoryChange Compare(Dictionary<string, string> before,
        Dictionary<string, string> after, DateTimeOffset at) => new(at,
            after.Keys.Count(k => !before.ContainsKey(k)), before.Keys.Count(k => !after.ContainsKey(k)),
            after.Count(x => before.TryGetValue(x.Key, out var value) && value != x.Value));

    internal static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GerenciadorFinal", "Historico", "drivers-v1.json");

    internal static DriverHistoryResult Record(string path, DriverInventoryResult inventory, DateTimeOffset at,
        CancellationToken validity = default, Action? beforeReplaceForTest = null)
    {
        // G1.20: uma pagina/generacao descartada nao pode gravar uma
        // fotografia tardia. O token e reavaliado antes da troca atomica.
        if (validity.IsCancellationRequested)
            return new(false, false, 0, 0, 0, "Histórico preservado: consulta invalidada antes da gravação.");
        if (inventory.Partial || inventory.Items.Count == 0)
            return new(false, false, 0, 0, 0, "Histórico preservado: consulta parcial ou vazia.");
        // Uma fotografia PnP completa pode não conter associação obtida de
        // Win32_PnPSignedDriver. Isso não é falha do arquivo histórico nem
        // prova de inexistência de drivers: preservar sem abrir/escrever v1.
        if (!inventory.Items.Any(item => !item.WithoutSignedDriverAssociation))
            return new(false, false, 0, 0, 0,
                "Histórico preservado: nenhuma associação de driver WMI observada nesta consulta; não comprova ausência de drivers.");
        lock (Gate)
        {
            string? temporary = null;
            try
            {
                var current = Snapshot(inventory);
                if (validity.IsCancellationRequested)
                    return new(false, false, 0, 0, 0, "Histórico preservado: consulta invalidada antes da gravação.");
                string full = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                // Serializa tambem instancias diferentes do programa; conflito preserva a base.
                using var fileLock = new FileStream(full + ".lock", FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                DriverHistoryState? previous = null;
                if (File.Exists(path))
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (stream.Length > MaxBytes) throw new InvalidDataException();
                    previous = JsonSerializer.Deserialize<DriverHistoryState>(stream);
                    if (previous is null || previous.Schema != 1 || previous.Devices is null ||
                        previous.Changes is null || previous.Devices.Count is 0 or > 4000 || previous.Changes.Count > 20 ||
                        previous.Devices.Any(x => !IsHash(x.Key) || !IsHash(x.Value)) ||
                        previous.Changes.Any(x => x is null || x.Added < 0 || x.Missing < 0 || x.Changed < 0))
                        throw new InvalidDataException();
                    // A próxima fotografia deve avançar no tempo. Duas
                    // gravações com timestamp idêntico poderiam reescrever
                    // provas distintas sem ordem temporal demonstrável.
                    if (at <= previous.At)
                        return new(false, false, 0, 0, 0, "Histórico preservado: data não posterior à última consulta.");
                }
                var change = previous is null ? new DriverHistoryChange(at, 0, 0, 0) : Compare(previous.Devices, current, at);
                var changes = previous?.Changes.ToList() ?? new List<DriverHistoryChange>();
                if (previous is not null && change.Added + change.Missing + change.Changed > 0)
                    changes.Add(change);
                var next = new DriverHistoryState(1, at, current, changes.TakeLast(20).ToList());
                temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, next);
                    stream.Flush(true);
                }
                // Interleaving exclusivo das fixtures; no produto e sempre
                // nulo, sem espera, timer ou polling de producao.
                beforeReplaceForTest?.Invoke();
                if (validity.IsCancellationRequested)
                    return new(false, false, 0, 0, 0, "Histórico preservado: consulta invalidada antes da publicação.");
                File.Move(temporary, full, true);
                return new(true, previous is null, change.Added, change.Missing, change.Changed,
                    previous is null ? "Histórico local iniciado nesta consulta; sem reconstrução do passado." :
                    $"Desde a consulta anterior: {change.Added} novos, {change.Missing} não observados, {change.Changed} alterados. Não comprova remoção de pacotes.",
                    string.Join(Environment.NewLine, next.Changes.AsEnumerable().Reverse().Select(x =>
                        $"{x.At.ToLocalTime():g}: {x.Added} novos, {x.Missing} não observados, {x.Changed} alterados")));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
            {
                return new(false, false, 0, 0, 0, "Histórico indisponível ou inválido; arquivo anterior preservado. Consulta continua disponível.");
            }
            finally
            {
                if (temporary is not null) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }
    }
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
