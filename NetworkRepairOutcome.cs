namespace GerenciadorAplicativos;

// Recibo do worker autenticado: codigo zero significa apenas comando
// terminado sem erro, nunca rede reparada, DNS resolvido ou reboot concluido.
internal enum NetworkRepairWorkerState { NotConfirmed, CommandReturnedZero, CommandReturnedError }

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
}
