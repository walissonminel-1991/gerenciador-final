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
    internal static Dictionary<string, string> Snapshot(DriverInventoryResult inventory)
    {
        if (inventory.Partial || inventory.Items.Count == 0 || inventory.Items.Count > 4000 ||
            inventory.Items.Any(x => string.IsNullOrWhiteSpace(x.DeviceId)))
            throw new InvalidDataException("Consulta incompleta nao substitui historico.");
        return inventory.Items.GroupBy(x => x.DeviceId.Trim().ToUpperInvariant())
            .ToDictionary(g => Hash(g.Key), g => Hash(string.Join("\n", g.Select(x =>
                JsonSerializer.Serialize(new[] { x.Provider.Trim().ToUpperInvariant(), x.Version.Trim(),
                    x.InfName.Trim().ToUpperInvariant(), x.Signed?.ToString() ?? "?", x.Present?.ToString() ?? "?" }))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))));
    }
    internal static DriverHistoryChange Compare(Dictionary<string, string> before,
        Dictionary<string, string> after, DateTimeOffset at) => new(at,
            after.Keys.Count(k => !before.ContainsKey(k)), before.Keys.Count(k => !after.ContainsKey(k)),
            after.Count(x => before.TryGetValue(x.Key, out var value) && value != x.Value));

    internal static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GerenciadorFinal", "Historico", "drivers-v1.json");

    internal static DriverHistoryResult Record(string path, DriverInventoryResult inventory, DateTimeOffset at)
    {
        if (inventory.Partial || inventory.Items.Count == 0)
            return new(false, false, 0, 0, 0, "Histórico preservado: consulta parcial ou vazia.");
        lock (Gate)
        {
            string? temporary = null;
            try
            {
                var current = Snapshot(inventory);
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
                    if (at < previous.At)
                        return new(false, false, 0, 0, 0, "Histórico preservado: relógio anterior à última consulta.");
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
