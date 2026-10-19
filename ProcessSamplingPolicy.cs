namespace GerenciadorAplicativos;

// Amostragem adaptativa: preserva coleta em segundo plano e na bandeja,
// mas evita enumerar todos os processos a cada poucos segundos no PC FRACO.
// Sem consulta WMI, hooks globais, monitor de arquivos ou servico adicional.
internal static class ProcessSamplingPolicy
{
    internal const int ForegroundIntervalMs = 2_000;
    internal const int BackgroundIntervalMs = 12_000;
    internal const int TrayIntervalMs = 20_000;

    internal static int IntervalMs(bool processTabVisible, bool inTray)
    {
        if (inTray) return TrayIntervalMs;
        return processTabVisible ? ForegroundIntervalMs : BackgroundIntervalMs;
    }
}
