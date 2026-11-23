using System.Globalization;

namespace GerenciadorAplicativos;

// Campos de leitura da classe oficial MSFT_StorageReliabilityCounter. O valor
// bruto só pode ser anexado ao disco depois de validar associação WMI REF
// única em StorageAssociationLinker. Não chamar método Reset.
internal sealed record StorageReliabilityCounterRaw(
    object? ReadErrorsTotal,
    object? ReadErrorsUncorrected,
    object? WriteErrorsTotal,
    object? WriteErrorsUncorrected,
    object? ReadLatencyMax,
    object? WriteLatencyMax,
    object? FlushLatencyMax,
    object? StartStopCycleCount,
    object? LoadUnloadCycleCount);

internal sealed record StorageReliabilityCounters(
    MonitorReading<ulong> ReadErrorsTotal,
    MonitorReading<ulong> ReadErrorsUncorrected,
    MonitorReading<ulong> WriteErrorsTotal,
    MonitorReading<ulong> WriteErrorsUncorrected,
    MonitorReading<ulong> ReadLatencyMaxMs,
    MonitorReading<ulong> WriteLatencyMaxMs,
    MonitorReading<ulong> FlushLatencyMaxMs,
    MonitorReading<ulong> StartStopCycles,
    MonitorReading<ulong> LoadUnloadCycles);

internal static class StorageReliabilityCounterMapper
{
    private const string Source = "Windows Storage MSFT_StorageReliabilityCounter";

    internal static ulong? ParseUnsigned(object? input)
    {
        if (input is null or DBNull or bool or double or float or decimal) return null;
        if (input is string text)
        {
            return ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture,
                out ulong parsed) && parsed != ulong.MaxValue ? parsed : null;
        }
        if (input is not (byte or ushort or uint or ulong or short or int or long))
            return null;
        try
        {
            ulong parsed = Convert.ToUInt64(input, CultureInfo.InvariantCulture);
            return parsed == ulong.MaxValue ? null : parsed;
        }
        catch (Exception ex) when (ex is OverflowException or FormatException or InvalidCastException)
        {
            return null;
        }
    }

    // Para erros, zero é o valor comunicado pelo Windows, NÃO prova ausência
    // de erros físicos quando o firmware não publica suporte ao contador.
    internal static MonitorReading<ulong> Errors(object? input, string field)
    {
        ulong? number = ParseUnsigned(input);
        return number.HasValue
            ? MonitorReading<ulong>.Available(number.Value, Source + "." + field +
                " (valor reportado, suporte do dispositivo não comprovado)")
            : MonitorReading<ulong>.Unavailable(Source + "." + field);
    }

    // Latência 0 ou sem valor pode significar ausência de suporte. Uma
    // latência máxima só é apresentada quando positiva e dentro do teto
    // conservador de plausibilidade de 24 horas (em milissegundos).
    internal static MonitorReading<ulong> Latency(object? input, string field)
    {
        ulong? number = ParseUnsigned(input);
        return number is > 0 and <= 86_400_000
            ? MonitorReading<ulong>.Available(number.Value, Source + "." + field + " (ms)")
            : MonitorReading<ulong>.Unavailable(Source + "." + field);
    }

    internal static MonitorReading<ulong> Cycles(object? input, string field)
    {
        ulong? number = ParseUnsigned(input);
        return number is > 0 and <= uint.MaxValue
            ? MonitorReading<ulong>.Available(number.Value, Source + "." + field + " (ciclos)")
            : MonitorReading<ulong>.Unavailable(Source + "." + field);
    }

    internal static StorageReliabilityCounters? Map(
        StorageReliabilityCounterRaw? raw, out bool contradictory)
    {
        contradictory = false;
        if (raw is null) return null;

        var readTotal = Errors(raw.ReadErrorsTotal, "ReadErrorsTotal");
        var readUncorrected = Errors(raw.ReadErrorsUncorrected, "ReadErrorsUncorrected");
        var writeTotal = Errors(raw.WriteErrorsTotal, "WriteErrorsTotal");
        var writeUncorrected = Errors(raw.WriteErrorsUncorrected, "WriteErrorsUncorrected");

        if (readTotal.Value is { } rt && readUncorrected.Value is { } ru && ru > rt)
        {
            contradictory = true;
            readTotal = MonitorReading<ulong>.Unavailable(Source + ".ReadErrorsTotal contraditório");
            readUncorrected = MonitorReading<ulong>.Unavailable(Source + ".ReadErrorsUncorrected contraditório");
        }
        if (writeTotal.Value is { } wt && writeUncorrected.Value is { } wu && wu > wt)
        {
            contradictory = true;
            writeTotal = MonitorReading<ulong>.Unavailable(Source + ".WriteErrorsTotal contraditório");
            writeUncorrected = MonitorReading<ulong>.Unavailable(Source + ".WriteErrorsUncorrected contraditório");
        }

        return new(readTotal, readUncorrected, writeTotal, writeUncorrected,
            Latency(raw.ReadLatencyMax, "ReadLatencyMax"),
            Latency(raw.WriteLatencyMax, "WriteLatencyMax"),
            Latency(raw.FlushLatencyMax, "FlushLatencyMax"),
            Cycles(raw.StartStopCycleCount, "StartStopCycleCount"),
            Cycles(raw.LoadUnloadCycleCount, "LoadUnloadCycleCount"));
    }
}
