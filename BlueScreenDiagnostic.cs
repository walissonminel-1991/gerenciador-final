using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace GerenciadorAplicativos;

internal enum BlueScreenEventSource { SystemErrorReporting, KernelPower, EventLog }

internal sealed record BlueScreenEvent(BlueScreenEventSource Source, DateTimeOffset RecordedAt, uint? StopCode)
{
    internal string SourceLabel => Source switch
    {
        BlueScreenEventSource.SystemErrorReporting => "WER-SystemErrorReporting, evento 1001",
        BlueScreenEventSource.KernelPower => "Kernel-Power, evento 41",
        _ => "EventLog, evento 6008"
    };
}

internal sealed record BlueScreenDiagnosis(IReadOnlyList<BlueScreenEvent> Events, bool Partial)
{
    internal string Summary => Events.Any(entry => entry.StopCode.HasValue)
        ? "Tela azul: código de parada registrado"
        : Events.Count > 0 ? "Tela azul: desligamento inesperado, causa indeterminada"
        : Partial ? "Tela azul: consulta inconclusiva" : "Tela azul: sem registros no período consultado";

    internal string Describe()
    {
        var lines = new List<string>
        {
            Summary, $"Log Sistema do Windows · últimos {BlueScreenDiagnostic.LookbackDays} dias."
        };
        var latestStop = Events.Where(entry => entry.StopCode.HasValue).MaxBy(entry => entry.RecordedAt);
        if (latestStop is not null)
            lines.Add($"Código mais recente: 0x{latestStop.StopCode:X8} · {Registered(latestStop)}.");
        var latestUnexpected = Events.Where(entry => !entry.StopCode.HasValue).MaxBy(entry => entry.RecordedAt);
        if (latestUnexpected is not null)
            lines.Add($"Registro mais recente sem código de parada: {Registered(latestUnexpected)}.");
        if (Events.Count > 0)
            lines.Add("A causa do desligamento exige investigação separada.");
        if (Partial)
            lines.Add("A consulta ao log do Windows ficou incompleta.");
        lines.Add("Confira os registros em Visualizador de Eventos → Logs do Windows → Sistema. Para investigar um código de parada, consulte a referência oficial da Microsoft.");
        return string.Join('\n', lines);
    }

    private static string Registered(BlueScreenEvent entry) =>
        $"{entry.RecordedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}" +
        $" (hora local, {entry.SourceLabel})";
}

internal static class BlueScreenDiagnostic
{
    internal const string Channel = "System";
    internal const string SystemErrorProvider = "Microsoft-Windows-WER-SystemErrorReporting";
    internal const string PowerProvider = "Microsoft-Windows-Kernel-Power";
    internal const string ShutdownProvider = "EventLog";
    internal const int LookbackDays = 14;
    internal const int RecordLimit = 128;
    private const int XmlLimit = 64 * 1024;
    private static readonly XNamespace EventNs = "http://schemas.microsoft.com/win/2004/08/events/event";

    internal static BlueScreenDiagnosis Evaluate(IEnumerable<string> records, bool partial = false)
    {
        var entries = new List<BlueScreenEvent>();
        int read = 0;
        foreach (var record in records)
        {
            if (read++ == RecordLimit) { partial = true; break; }
            if (Parse(record) is { } entry) entries.Add(entry);
            else partial = true;
        }
        return new(entries, partial);
    }

    private static BlueScreenEvent? Parse(string xml)
    {
        if (xml.Length > XmlLimit) return null;
        try
        {
            using var text = new StringReader(xml);
            using var reader = XmlReader.Create(text, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = XmlLimit
            });
            var root = XDocument.Load(reader).Root;
            if (root?.Name != EventNs + "Event") return null;
            var system = root.Element(EventNs + "System");
            if ((string?)system?.Element(EventNs + "Channel") != Channel ||
                !DateTimeOffset.TryParseExact((string?)system.Element(EventNs + "TimeCreated")?.Attribute("SystemTime"),
                    "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var stamp))
                return null;

            string? provider = (string?)system.Element(EventNs + "Provider")?.Attribute("Name");
            string? eventId = (string?)system.Element(EventNs + "EventID");
            var data = root.Element(EventNs + "EventData")?.Elements(EventNs + "Data") ?? [];
            switch (provider, eventId)
            {
                case (SystemErrorProvider, "1001"):
                    var werCode = ReadField(data, "param1");
                    if (werCode is not [var payload] || ParseWerCode(payload) is not uint code)
                        return null;
                    return new(BlueScreenEventSource.SystemErrorReporting, stamp, code);
                case (PowerProvider, "41"):
                    var powerCode = ReadField(data, "BugcheckCode");
                    if (powerCode.Length == 0)
                        return new(BlueScreenEventSource.KernelPower, stamp, null);
                    if (powerCode is not [var value] || !uint.TryParse(value, NumberStyles.None,
                            CultureInfo.InvariantCulture, out var decimalCode))
                        return null;
                    return new(BlueScreenEventSource.KernelPower, stamp, decimalCode == 0 ? null : decimalCode);
                case (ShutdownProvider, "6008"):
                    return new(BlueScreenEventSource.EventLog, stamp, null);
                default:
                    return null;
            }
        }
        catch (XmlException) { return null; }
    }

    private static string[] ReadField(IEnumerable<XElement> data, string name) =>
        data.Where(element => (string?)element.Attribute("Name") == name)
            .Select(element => element.Value.Trim()).Take(2).ToArray();

    private static uint? ParseWerCode(string payload)
    {
        var span = payload.AsSpan();
        if (!span.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return null;
        int delimiter = span.IndexOf(' ');
        var token = delimiter < 0 ? span[2..] : span[2..delimiter];
        if (delimiter >= 0)
        {
            var parameters = span[delimiter..].TrimStart();
            if (parameters.Length < 3 || parameters[0] != '(' || parameters[^1] != ')') return null;
        }
        return token.Length is >= 1 and <= 8 &&
            uint.TryParse(token, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code) &&
            code != 0 ? code : null;
    }
}
