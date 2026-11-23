using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Security;

namespace GerenciadorAplicativos;

internal static class BlueScreenEventLog
{
    internal static BlueScreenDiagnosis Read()
    {
        if (!OperatingSystem.IsWindows()) return new([], Partial: true);

        var records = new List<string>();
        var clock = Stopwatch.StartNew();
        long windowMilliseconds = BlueScreenDiagnostic.LookbackDays * 86_400_000L;
        var filter = FormattableString.Invariant($"""
            *[System[TimeCreated[timediff(@SystemTime) >= 0 and timediff(@SystemTime) <= {windowMilliseconds}]
            and ((Provider[@Name='{BlueScreenDiagnostic.SystemErrorProvider}'] and EventID=1001)
            or (Provider[@Name='{BlueScreenDiagnostic.PowerProvider}'] and EventID=41)
            or (Provider[@Name='{BlueScreenDiagnostic.ShutdownProvider}'] and EventID=6008))]]
            """);
        try
        {
            using var reader = new EventLogReader(new EventLogQuery(BlueScreenDiagnostic.Channel, PathType.LogName, filter)
            { ReverseDirection = true, TolerateQueryErrors = false });
            while (records.Count <= BlueScreenDiagnostic.RecordLimit && clock.Elapsed < TimeSpan.FromSeconds(7))
            {
                using var entry = reader.ReadEvent(TimeSpan.FromSeconds(2));
                if (entry is null) return BlueScreenDiagnostic.Evaluate(records);
                records.Add(entry.ToXml());
            }
        }
        catch (Exception error) when (error is EventLogException or UnauthorizedAccessException or SecurityException or TimeoutException)
        {
            // Preservar evidências anteriores a uma falha sem expor o XML ou mensagens da API.
        }
        return BlueScreenDiagnostic.Evaluate(records, partial: true);
    }
}
