namespace GerenciadorAplicativos;

// Amostragem adaptativa: preserva coleta em segundo plano e na bandeja,
// mas evita enumerar todos os processos a cada poucos segundos no PC FRACO.
// Sem consulta WMI, hooks globais, monitor de arquivos ou servico adicional.
internal static class ProcessSamplingPolicy
{
    internal const int ForegroundIntervalMs = 2_000;
    internal const int BackgroundIntervalMs = 12_000;
    internal const int TrayIntervalMs = 20_000;

    // Coleta detalhada de nomes/PIDs/handles apenas quando o usuario
    // efetivamente esta visualizando a aba Processos. Em segundo plano,
    // os contadores globais de CPU, memoria e rede permanecem ativos.
    internal static bool ShouldReadProcessDetails(bool processTabVisible, bool inTray) =>
        processTabVisible && !inTray;

    internal static int IntervalMs(bool processTabVisible, bool inTray)
    {
        if (inTray) return TrayIntervalMs;
        return processTabVisible ? ForegroundIntervalMs : BackgroundIntervalMs;
    }
}
