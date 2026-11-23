namespace GerenciadorAplicativos;

internal sealed record UsbPnpObservation(string DeviceId, bool? Present, int? ProblemCode);

internal sealed record UsbPnpDiagnosis(int Present, int Disabled, int MissingDriver,
    int StoppedWithProblem, int OtherProblems, bool Partial)
{
    internal string Summary => Disabled + MissingDriver + StoppedWithProblem + OtherProblems > 0
        ? "O Windows informou ocorrências PnP em registros USB presentes."
        : Partial ? "A consulta PnP de USB ficou inconclusiva."
        : Present == 0 ? "A consulta PnP não confirmou registros USB presentes."
        : "O Windows não informou códigos de problema nos registros USB presentes consultados.";

    internal string Describe()
    {
        var lines = new List<string> { "Diagnóstico de USB", "", Summary };
        if (!Partial || Present > 0) lines.Add($"Registros PnP USB com presença confirmada: {Present}.");
        if (Disabled > 0) lines.Add($"Desativados (código 22): {Disabled}.");
        if (MissingDriver > 0) lines.Add($"Driver não instalado (código 28): {MissingDriver}.");
        if (StoppedWithProblem > 0) lines.Add($"Interrompidos após problema relatado (código 43): {StoppedWithProblem}.");
        if (OtherProblems > 0) lines.Add($"Com outros códigos PnP: {OtherProblems}.");
        if (Partial) lines.Add("A leitura foi parcial. Alguns registros podem estar sem presença ou estado confirmado.");
        lines.Add("");
        lines.Add("Confira o acessório e o código no Gerenciador de Dispositivos. Se ele não aparecer, teste outra porta ou outro cabo de dados e repita a consulta.");
        return string.Join(Environment.NewLine, lines);
    }
}

internal static class UsbPnpDiagnostic
{
    internal static bool IsUsbInstance(string id) =>
        id.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith(@"USBSTOR\", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith(@"USBPRINT\", StringComparison.OrdinalIgnoreCase);

    internal static UsbPnpDiagnosis Evaluate(IEnumerable<UsbPnpObservation> observations, bool partial)
    {
        int present = 0, disabled = 0, missingDriver = 0, stopped = 0, other = 0;
        var instances = observations.Where(row => IsUsbInstance(row.DeviceId))
            .GroupBy(row => row.DeviceId, StringComparer.OrdinalIgnoreCase);
        foreach (var instance in instances)
        {
            if (instance.All(row => row.Present == false)) continue;
            var first = instance.First();
            // Duplicatas idênticas contam uma vez. Uma discordância de presença
            // ou de código não serve para atribuir um problema à instância.
            if (instance.Any(row => row.Present != true || row.ProblemCode == 45))
            {
                partial = true;
                continue;
            }
            present++;
            if (first.ProblemCode is null or < 0 || instance.Any(row => row.ProblemCode != first.ProblemCode))
            {
                partial = true;
                continue;
            }
            switch (first.ProblemCode)
            {
                case 0: break;
                case 22: disabled++; break;
                case 28: missingDriver++; break;
                case 43: stopped++; break;
                default: other++; break;
            }
        }
        return new(present, disabled, missingDriver, stopped, other, partial);
    }
}
