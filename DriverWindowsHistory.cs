using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

internal enum DriverInstallOutcome { Succeeded, Failed, Unknown }
internal sealed record DriverWindowsEvent(string DeviceId, string InfName, DateTime Started,
    DriverInstallOutcome Outcome);
internal sealed record DriverWindowsHistoryResult(IReadOnlyList<DriverWindowsEvent> Events,
    bool Partial, bool Unavailable, bool TailOnly, int RejectedSections)
{
    internal string Summary => Unavailable ? "Histórico Windows indisponível; preservar."
        : $"Histórico Windows: {Events.Count} vínculos explícitos" +
            (Partial ? " (leitura parcial)." : ".") +
            " Registro de instalação não comprova uso atual nem autoriza exclusão.";
}

// Somente leitura de setupapi.dev.log. Não procura nomes semelhantes, não
// reconstrói versões e não persiste IDs, nomes ou linhas do log em diagnóstico.
internal static class DriverWindowsHistory
{
    private static readonly TimeSpan RegexLimit = TimeSpan.FromMilliseconds(100);
    private static Match Match(string text, string pattern) =>
        Regex.Match(text, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexLimit);

    internal static DriverWindowsHistoryResult Parse(string text, string windows,
        CancellationToken token = default, bool tailOnly = false, int maximumEvents = 2000)
    {
        if (text.Length > 4_194_304 || maximumEvents is < 1 or > 4000)
            return new([], true, false, tailOnly, 1);
        var found = new List<DriverWindowsEvent>();
        string? device = null;
        DateTime? started = null;
        var infs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool ended = false, invalid = false, partial = tailOnly;
        int rejected = 0;
        string expectedPrefix = windows.TrimEnd('\\', '/') + "\\INF\\";
        void Reset() { device = null; started = null; infs.Clear(); ended = false; invalid = false; }
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            token.ThrowIfCancellationRequested();
            if (raw.Length > 8192) { partial = true; invalid = true; continue; }
            string line = raw.Trim();
            if (line.StartsWith(">>>", StringComparison.Ordinal) && line.Contains('['))
            {
                if (device is not null) { rejected++; partial = true; }
                Reset();
                var header = Match(line, @"^>>>\s+\[Device Install(?:\s+\([^\r\n)]{1,120}\))?\s+-\s+([^\[\]\r\n]+)\]$");
                if (header.Success)
                {
                    string id = header.Groups[1].Value;
                    if (id.Length is > 0 and <= 4000 && id == id.Trim() &&
                        id.Count(c => c == '\\') >= 2 && !id.Any(char.IsControl)) device = id;
                    else { rejected++; partial = true; }
                }
                else if (Match(line, @"^>>>\s+\[Device Install\b").Success)
                {
                    // Formato de instalação não compreendido não equivale a
                    // nenhuma instalação encontrada numa leitura completa.
                    rejected++; partial = true;
                }
                continue;
            }
            if (device is null) continue;
            if (line.StartsWith(">>>", StringComparison.Ordinal) && line.Contains("Section start", StringComparison.OrdinalIgnoreCase))
            {
                var stamp = Match(line, @"^>>>\s+(?:Section start\s+)?(\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}\.\d{3})(?:: Section start)?$");
                if (started.HasValue || !stamp.Success || !DateTime.TryParseExact(stamp.Groups[1].Value,
                    "yyyy/MM/dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) invalid = true;
                else started = DateTime.SpecifyKind(at, DateTimeKind.Unspecified); // Local do log, sem inventar fuso.
            }
            // Apenas campos explícitos de INF publicado/selecionado. Um INF
            // citado num comando, erro ou diretório de origem não é associação.
            var inf = Match(line, @"^(?:inf:\s+Published Inf Path:\s+|(?:dvi|ndv):\s+InfPath\s*=\s*|ndv:\s+Driver INF Path:\s*)(.+)$");
            if (inf.Success)
            {
                string path = inf.Groups[1].Value.Trim().Trim('\'');
                string name = path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)
                    ? path[expectedPrefix.Length..] : path;
                if (DriverPackageLinks.IsValidInf(name)) infs.Add(name);
                else invalid = true;
            }
            if (line.StartsWith("<<<", StringComparison.Ordinal) && line.Contains("Section end", StringComparison.OrdinalIgnoreCase))
            {
                var end = Match(line, @"^<<<\s+(?:Section end\s+(\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}\.\d{3})|\[(\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}\.\d{3}): Section end\])$");
                string stamp = end.Groups[1].Success ? end.Groups[1].Value : end.Groups[2].Value;
                ended = end.Success && DateTime.TryParseExact(stamp, "yyyy/MM/dd HH:mm:ss.fff",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var endAt) && started.HasValue && endAt >= started.Value;
                if (!ended) invalid = true;
            }
            var exit = Match(line, @"^<<<\s+\[Exit(?: status:\s*(SUCCESS|FAILURE(?:\(0x[0-9a-f]{8}\))?)| Status\((0x[0-9a-f]{8})\))?\]$");
            if (!exit.Success) continue;
            if (!invalid && ended && started.HasValue && infs.Count == 1)
            {
                string status = exit.Groups[1].Success ? exit.Groups[1].Value : exit.Groups[2].Value;
                var outcome = status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) || status.Equals("0x00000000", StringComparison.OrdinalIgnoreCase)
                    ? DriverInstallOutcome.Succeeded : status.Length > 0 ? DriverInstallOutcome.Failed : DriverInstallOutcome.Unknown;
                found.Add(new(device, infs.Single(), started.Value, outcome));
                if (found.Count >= maximumEvents) { partial = true; Reset(); break; }
            }
            else { rejected++; partial = true; }
            Reset();
        }
        if (device is not null) { rejected++; partial = true; }
        return new(found.Distinct().ToArray(), partial, false, tailOnly, rejected);
    }

    internal static DriverWindowsHistoryResult Capture(CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return new([], true, true, false, 0);
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return Read(Path.Combine(windows, "INF", "setupapi.dev.log"), windows, token);
    }

    internal static DriverWindowsHistoryResult Read(string path, string windows, CancellationToken token)
    {
        try
        {
            // Não seguir junções ou links para dados pessoais.
            for (FileSystemInfo? item = new FileInfo(path); item is not null;
                item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent)
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            const int limit = 4_194_304;
            long length = stream.Length;
            byte[] prefix = new byte[3];
            int bytes = stream.Read(prefix);
            Encoding encoding = bytes >= 2 && prefix[0] == 0xff && prefix[1] == 0xfe ? new UnicodeEncoding(false, false, true)
                : bytes >= 2 && prefix[0] == 0xfe && prefix[1] == 0xff ? new UnicodeEncoding(true, false, true)
                : new UTF8Encoding(false, true);
            bool tail = length > limit;
            long start = tail ? length - limit : 0;
            if (encoding is UnicodeEncoding && start % 2 != 0) start++;
            stream.Position = start;
            var buffer = new byte[checked((int)(length - start))];
            stream.ReadExactly(buffer);
            token.ThrowIfCancellationRequested();
            // Descartar a linha inicial cortada ANTES de decodificar UTF-8.
            if (tail && encoding is UTF8Encoding)
            {
                int nl = Array.IndexOf(buffer, (byte)'\n');
                if (nl < 0) return new([], true, false, true, 1);
                buffer = buffer[(nl + 1)..];
            }
            string content = encoding.GetString(buffer).TrimStart('\uFEFF');
            if (tail && encoding is UnicodeEncoding)
            {
                int nl = content.IndexOf('\n');
                content = nl < 0 ? "" : content[(nl + 1)..];
            }
            var result = Parse(content, windows, token, tail);
            return result with { Partial = result.Partial || stream.Length != length };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException)
        { return new([], true, true, false, 0); }
    }

    internal static DriverWindowsEvent[] Correlate(DriverInventoryEntry device, DriverWindowsHistoryResult history) =>
        history.Events.Where(e => e.DeviceId.Equals(device.DeviceId.Trim(), StringComparison.OrdinalIgnoreCase) &&
            e.InfName.Equals(device.InfName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Started).Take(20).ToArray();
}
