namespace GerenciadorAplicativos;

// Recibo do worker autenticado: codigo zero significa apenas comando
// terminado sem erro, nunca rede reparada, DNS resolvido ou reboot concluido.
internal enum NetworkRepairWorkerState { NotConfirmed, CommandReturnedZero, CommandReturnedError }

// Uma única resolução do nome de teste não comprova a correção de toda
// a rede, mas permite comparar a evidência antes/depois do flush DNS.
internal enum NetworkDnsVerification { NotEvaluated, TestRecovered, TestStillFailing, Inconclusive }

internal static class NetworkRepairOutcome
{
    internal static NetworkRepairWorkerState FromWire(string? raw)
    {
        if (raw is null || !raw.StartsWith("DONE|", StringComparison.Ordinal))
            return NetworkRepairWorkerState.NotConfirmed;
        string code = raw["DONE|".Length..];
        if (code == "0") return NetworkRepairWorkerState.CommandReturnedZero;
        return code.Length is >= 1 and <= 10 && code.All(char.IsAsciiDigit) &&
            int.TryParse(code, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int exit) && exit > 0
            ? NetworkRepairWorkerState.CommandReturnedError :
            NetworkRepairWorkerState.NotConfirmed;
    }
    internal static NetworkDnsVerification ReviewDnsAfterFlush(
        NetworkScan? before, NetworkScan? after, NetworkRepairWorkerState command)
    {
        if (command != NetworkRepairWorkerState.CommandReturnedZero)
            return NetworkDnsVerification.NotEvaluated;
        if (before is null || after is null)
            return NetworkDnsVerification.Inconclusive;
        var prior = before.Checks.Where(x =>
            x.Name == "DNS").ToArray();
        var current = after.Checks.Where(x =>
            x.Name == "DNS").ToArray();
        if (prior.Length != 1 || current.Length != 1 ||
            prior[0].State != "Atenção")
            return NetworkDnsVerification.Inconclusive;
        return current[0].State switch
        {
            "OK" => NetworkDnsVerification.TestRecovered,
            "Atenção" => NetworkDnsVerification.TestStillFailing,
            _ => NetworkDnsVerification.Inconclusive
        };
    }
}
