using GerenciadorAplicativos;

internal static class BlueScreenDiagnosticTests
{
    private const string Wer = "Microsoft-Windows-WER-SystemErrorReporting";
    private const string Power = "Microsoft-Windows-Kernel-Power";
    private const string EventLog = "EventLog";
    private const string Timestamp = "2026-11-22T13:42:59.3705545Z";

    internal static void Run()
    {
        int passed = 0;
        void Check(bool condition, string expectation)
        {
            if (!condition) throw new InvalidOperationException("VF03 tela azul: " + expectation);
            passed++;
            Console.WriteLine("OK VF03 tela azul: " + expectation);
        }

        const string werPayload = """
            <Data Name="param1">0x00000116 (0x0001, 0x0002, 0x0003, 0x0004)</Data>
            <Data Name="param2">C:\Windows\Minidump\FIXTURE.dmp</Data>
            <Data Name="param3">RELATORIO-LOCAL</Data>
            """;
        string wer = Event(Wer, 1001, werPayload);
        var diagnosis = BlueScreenDiagnostic.Evaluate([wer]);
        Check(diagnosis is { Partial: false, Events: [{ StopCode: 0x116, Source: BlueScreenEventSource.SystemErrorReporting }] } &&
            diagnosis.Describe().Contains("0x00000116", StringComparison.Ordinal) &&
            diagnosis.Describe().Contains("evento 1001", StringComparison.Ordinal),
            "WER 1001 no canal System fornece um código de parada hexadecimal.");
        Check(diagnosis.Events[0].RecordedAt == DateTimeOffset.Parse(Timestamp, System.Globalization.CultureInfo.InvariantCulture) &&
            diagnosis.Describe().Contains("hora local", StringComparison.Ordinal),
            "preserva a hora do evento e indica o fuso exibido.");
        Check(!diagnosis.Describe().Contains("FIXTURE.dmp", StringComparison.Ordinal) &&
            !diagnosis.Describe().Contains("RELATORIO-LOCAL", StringComparison.Ordinal) &&
            !diagnosis.Describe().Contains("COMPUTADOR-LOCAL", StringComparison.Ordinal) &&
            !diagnosis.Describe().Contains("0x0001,", StringComparison.Ordinal),
            "o relatório omite parâmetros, caminhos de dump, nome do computador e identificador WER.");

        var explicitPower = BlueScreenDiagnostic.Evaluate([Event(Power, 41, """<Data Name="BugcheckCode">159</Data>""")]);
        Check(explicitPower is { Partial: false, Events: [{ StopCode: 159, Source: BlueScreenEventSource.KernelPower }] } &&
            explicitPower.Describe().Contains("0x0000009F", StringComparison.Ordinal),
            "Kernel-Power 41 com código decimal não zero exibe o código de parada convertido.");
        string zeroPower = Event(Power, 41,
            """<Data Name="BugcheckCode">0</Data><Data Name="PowerButtonTimestamp">123</Data>""");
        var isolatedPower = BlueScreenDiagnostic.Evaluate([zeroPower]);
        Check(isolatedPower is { Partial: false, Events: [{ StopCode: null, Source: BlueScreenEventSource.KernelPower }] } &&
            isolatedPower.Summary.Contains("causa indeterminada", StringComparison.Ordinal) &&
            !isolatedPower.Describe().Contains("0x00000000", StringComparison.Ordinal),
            "Kernel-Power 41 com código zero indica desligamento inesperado com causa indeterminada.");
        Check(BlueScreenDiagnostic.Evaluate([Event(Power, 41, "")]) is
            { Partial: false, Events: [{ StopCode: null }] },
            "Kernel-Power 41 sem campo de bugcheck continua sendo evidência de desligamento inesperado.");

        string shutdown = Event(EventLog, 6008, """<Data Name="BugcheckCode">159</Data>""",
            timestamp: "2026-11-22T14:44:00Z");
        var isolatedShutdown = BlueScreenDiagnostic.Evaluate([shutdown]);
        Check(isolatedShutdown is { Partial: false, Events: [{ StopCode: null, Source: BlueScreenEventSource.EventLog }] } &&
            isolatedShutdown.Summary.Contains("causa indeterminada", StringComparison.Ordinal),
            "EventLog 6008 isolado jamais confirma um bugcheck.");
        Check(BlueScreenDiagnostic.Evaluate([Event("Windows Error Reporting", 1001, werPayload, "Application"),
            Event("Windows Error Reporting", 1001, werPayload), Event(Wer, 1001, werPayload, "Application"),
            Event(EventLog, 1001, werPayload)]) is { Partial: true, Events.Count: 0 },
            "rejeita números de evento iguais no canal ou provedor incorreto.");

        Check(BlueScreenDiagnostic.Evaluate([
            Event(Wer, 1001, """<Data Name="param2">0x00000116</Data>"""),
            Event(Wer, 1001, """<Data Name="param1">0x00000000</Data>"""),
            Event(Wer, 1001, """<Data Name="param1">0x123456789</Data>"""),
            Event(Wer, 1001, """<Data Name="param1">0x116 texto arbitrário</Data>"""),
            Event(Wer, 1001, """<Data Name="param1">0x116</Data><Data Name="param1">0x9F</Data>"""),
            Event(Power, 41, """<Data Name="BugcheckCode">0x9F</Data>"""),
            Event(Power, 41, """<Data Name="BugcheckCode">0</Data><Data Name="BugcheckCode">159</Data>""")
            ]) is { Partial: true, Events.Count: 0 },
            "campos inválidos, conflitantes ou fora do formato do provedor tornam a leitura inconclusiva.");
        Check(BlueScreenDiagnostic.Evaluate(["<Event", new string('x', 65_537),
            """<!DOCTYPE Event [<!ENTITY x "falso">]><Event>&x;</Event>""",
            Event(Wer, 1001, werPayload, timestamp: "data inválida")]) is { Partial: true, Events.Count: 0 },
            "XML inválido, acima do limite ou com DTD não entra no diagnóstico.");

        var mixed = BlueScreenDiagnostic.Evaluate([shutdown, wer, zeroPower]);
        Check(mixed is { Partial: false, Events.Count: 3 } &&
            mixed.Describe().Contains("Código mais recente: 0x00000116", StringComparison.Ordinal) &&
            mixed.Describe().Contains("Registro mais recente sem código de parada:", StringComparison.Ordinal) &&
            mixed.Describe().Contains("evento 6008", StringComparison.Ordinal),
            "registros próximos conservam sua evidência própria e um 6008 posterior não herda o código anterior.");

        var empty = BlueScreenDiagnostic.Evaluate([]);
        var failed = BlueScreenDiagnostic.Evaluate([], partial: true);
        Check(empty.Summary.Contains("sem registros no período consultado", StringComparison.Ordinal) &&
            failed.Summary.Contains("inconclusiva", StringComparison.Ordinal) &&
            !failed.Describe().Contains("sem registros", StringComparison.Ordinal),
            "distingue ausência de registros no recorte de consulta indisponível.");
        var afterFailure = BlueScreenDiagnostic.Evaluate([wer, "<inválido"], partial: true);
        Check(afterFailure is { Partial: true, Events: [{ StopCode: 0x116 }] } &&
            afterFailure.Describe().Contains("incompleta", StringComparison.Ordinal),
            "falha após um evento válido preserva o código já obtido.");
        Check(BlueScreenDiagnostic.Evaluate(Enumerable.Repeat(wer, BlueScreenDiagnostic.RecordLimit + 1)) is
            { Partial: true, Events.Count: BlueScreenDiagnostic.RecordLimit },
            "a consulta sinaliza truncamento quando ultrapassa seu limite de registros.");

        Console.WriteLine($"VF03 tela azul: {passed} verificações aprovadas.");
    }

    private static string Event(string provider, int eventId, string data, string channel = "System",
        string timestamp = Timestamp) => $"""
        <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
          <System>
            <Provider Name="{provider}"/>
            <EventID>{eventId}</EventID>
            <TimeCreated SystemTime="{timestamp}"/>
            <Channel>{channel}</Channel>
            <Computer>COMPUTADOR-LOCAL</Computer>
          </System>
          <EventData>{data}</EventData>
        </Event>
        """;
}
