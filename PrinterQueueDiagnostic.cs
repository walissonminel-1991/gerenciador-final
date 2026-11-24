using System.Globalization;

namespace GerenciadorAplicativos;

internal enum PrinterQueueState { Offline, PausedOrStopped, Attention, NoOfflineIndication, NoDefault, Inconclusive }

internal sealed record PrinterQueueDiagnosis(PrinterQueueState State)
{
    internal string Summary => State switch
    {
        PrinterQueueState.Offline => "O Windows informa que a impressora padrão está offline.",
        PrinterQueueState.PausedOrStopped => "O Windows informa que a fila padrão está pausada ou interrompida.",
        PrinterQueueState.Attention => "O Windows informa que a impressora padrão requer atenção.",
        PrinterQueueState.NoOfflineIndication => "O estado da fila padrão informado pelo Windows não indica offline.",
        PrinterQueueState.NoDefault => "A consulta do Windows não retornou uma impressora padrão.",
        _ => "A consulta do estado da impressora padrão ficou inconclusiva."
    };

    internal string Guidance => State switch
    {
        PrinterQueueState.Offline => "Confira se a impressora está ligada e se a conexão USB ou de rede está disponível. Abra Impressoras e scanners nas Configurações para conferir a fila.",
        PrinterQueueState.PausedOrStopped => "Abra a fila da impressora padrão em Impressoras e scanners e confira por que ela está pausada ou interrompida.",
        PrinterQueueState.Attention => "Confira os avisos da impressora e da fila em Impressoras e scanners.",
        PrinterQueueState.NoOfflineIndication => "Se a impressão ainda falhar, confira a conexão e a fila em Impressoras e scanners. A consulta lê o estado informado pelo Windows; a impressão não foi testada.",
        PrinterQueueState.NoDefault => "Abra Impressoras e scanners nas Configurações para conferir a instalação e a seleção da impressora padrão.",
        _ => "Confira a impressora em Impressoras e scanners e repita o diagnóstico."
    };

    internal string Describe() => $"Diagnóstico da impressora padrão\n\n{Summary}\n\n{Guidance}";
}

internal static class PrinterQueueDiagnostic
{
    internal static PrinterQueueDiagnosis Evaluate(IReadOnlyList<Dictionary<string, string>>? queues)
    {
        if (queues is null or { Count: > 1 }) return new(PrinterQueueState.Inconclusive);
        if (queues.Count == 0) return new(PrinterQueueState.NoDefault);

        var queue = queues[0];
        ushort? status = ReadStatus(queue, "PrinterStatus");
        ushort? extended = ReadStatus(queue, "ExtendedPrinterStatus");
        ushort? detectedError = ReadStatus(queue, "DetectedErrorState");
        // O estado de erro usa uma tabela própria. Offline corresponde a 9 nesse campo.
        if (status == 7 || extended == 7 || detectedError == 9) return new(PrinterQueueState.Offline);
        if (status == 6 || extended is 6 or 8) return new(PrinterQueueState.PausedOrStopped);
        if (extended is 9 or 11 or 16 or 18) return new(PrinterQueueState.Attention);
        if (status is >= 3 and <= 5 || extended is >= 3 and <= 5 or 10 or >= 12 and <= 15 or 17)
            return new(PrinterQueueState.NoOfflineIndication);
        return new(PrinterQueueState.Inconclusive);
    }

    private static ushort? ReadStatus(Dictionary<string, string> queue, string name) =>
        ushort.TryParse(queue.GetValueOrDefault(name), NumberStyles.None, CultureInfo.InvariantCulture, out ushort code)
            ? code : null;
}
