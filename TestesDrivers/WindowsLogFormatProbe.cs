using System.Text.RegularExpressions;

// Somente o executável de testes. Emite formatos de campos do runner,
// nunca dispositivos, caminhos, nomes de produtos, comandos ou linhas brutas.
internal static class WindowsLogFormatProbe
{
    internal static void Print()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF", "setupapi.dev.log");
        var templates = new Dictionary<string, int>();
        try
        {
            foreach (string raw in File.ReadLines(path).Take(100_000))
            {
                string line = raw.Trim();
                string? template = null;
                var header = Regex.Match(line, @"^>>>\s+\[Device Install(?:\s+\(([A-Za-z ]+)\))?\s+-\s+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                if (header.Success) template = "Device Install (" + header.Groups[1].Value + ") - <ID>";
                else if (line.StartsWith(">>>", StringComparison.Ordinal) && line.Contains("Section start", StringComparison.Ordinal))
                    template = Regex.Replace(line, @"\d", "0");
                else if (line.StartsWith("<<<", StringComparison.Ordinal) &&
                    Regex.IsMatch(line, @"^<<<[ A-Za-z0-9\[\]():/.\-]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                    template = Regex.Replace(line, @"\d", "0");
                else
                {
                    var field = Regex.Match(line, @"^((?:inf|dvi|ndv):\s+(?:InfPath|Inf Path|Driver INF Path|InfFile|Inf File|Published Inf Path|Selected Driver|Opened INF)\s*[:=\-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                    if (field.Success) template = field.Groups[1].Value + " <value>";
                }
                if (template is not null) templates[template] = templates.GetValueOrDefault(template) + 1;
            }
            foreach (var pair in templates.OrderByDescending(x => x.Value).Take(25))
                Console.WriteLine($"D2_LOG_FORMATO quantidade={pair.Value} formato={pair.Key}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Console.WriteLine("D2_LOG_FORMATO indisponivel=" + ex.GetType().Name); }
    }
}
