namespace GerenciadorAplicativos;

// Recibo do worker autenticado: codigo zero significa apenas comando
// terminado sem erro, nunca rede reparada, DNS resolvido ou reboot concluido.
internal enum NetworkRepairWorkerState { NotConfirmed, CommandReturnedZero, CommandReturnedError }

// Duas resoluções de nomes independentes são observadas antes e depois.
// Nem mesmo recuperação de ambas prova causalidade do flush ou rede íntegra.
internal enum NetworkDnsVerification
{
    NotEvaluated, TestRecovered, PartiallyRecovered, TestStillFailing, Inconclusive
}

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
        var prior = before.Checks.Where(x => x.Name == "DNS").ToArray();
        var previousAlternate = before.Checks.Where(x =>
            x.Name == "DNS alternativo").ToArray();
        var current = after.Checks.Where(x => x.Name == "DNS").ToArray();
        var currentAlternate = after.Checks.Where(x =>
            x.Name == "DNS alternativo").ToArray();
        if (prior.Length != 1 || previousAlternate.Length != 1 ||
            current.Length != 1 || currentAlternate.Length != 1 ||
            prior[0].State != "Atenção" ||
            previousAlternate[0].State != "Atenção")
            return NetworkDnsVerification.Inconclusive;
        bool goodFirst = current[0].State == "OK";
        bool goodSecond = currentAlternate[0].State == "OK";
        bool badFirst = current[0].State == "Atenção";
        bool badSecond = currentAlternate[0].State == "Atenção";
        if (goodFirst && goodSecond) return NetworkDnsVerification.TestRecovered;
        if (badFirst && badSecond) return NetworkDnsVerification.TestStillFailing;
        if ((goodFirst && badSecond) || (badFirst && goodSecond))
            return NetworkDnsVerification.PartiallyRecovered;
        return NetworkDnsVerification.Inconclusive;
    }
}
