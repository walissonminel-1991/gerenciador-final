# GERENCIADOR FINAL — Monitoramento nativo · 23/11/2026

## Objetivo e isolamento

Preparar o motor de monitoramento para os cartões **Início** e **Sistema** do novo layout, sem alterar a implementação visual que está sendo desenvolvida em outra frente. Esta branch nasce do HEAD `72669bf1494315d90c1192fb2465b0287544d68e` da PR #101. Não modifica `main`, a política G2, Drivers, a distribuição, telemetria, a infraestrutura ou G5.

## Implementado no backend

- `LiveHardwareMonitor.Capture()` fornece uma amostra somente leitura, com instante UTC, uso global de CPU, frequência reportada pelo Windows, memória física e tempo desde a inicialização.
- **CPU %:** `GetSystemTimes`, calculada com diferenças entre leituras e subtração do tempo ocioso do total (kernel inclui idle). A primeira leitura é `WarmingUp`, nunca inventa 0%.
- **CPU MHz:** `CallNtPowerInformation(ProcessorInformation=11)`, média dos valores `CurrentMhz` válidos. Trata-se da frequência **reportada pela política/driver de energia**, não de clock físico instantâneo nem de turbo comprovado.
- **RAM:** reutiliza `MemoryRamReader.Capture()` (`GlobalMemoryStatusEx`) e seu contrato de total, disponível e uso, sem manipular a RAM nem esvaziar caches.
- **Tempo de atividade:** `GetTickCount64`; não se deve confundir com o relógio local ou o horário do último desligamento (Inicialização Rápida pode manter o kernel).
- **Histórico:** até 90 amostras em memória volátil por instância, copiadas antes de serem expostas. Sem nomes de processos, PIDs ou gravação de amostras em disco.
- **Cadência:** reaproveita `ProcessSamplingPolicy.IntervalMs` (2 s visível, 12 s em segundo plano e 20 s na bandeja). O novo motor **não cria outro timer**.

## Sensores que permanecem indisponíveis

Os campos de temperatura, potência e ventoinhas de CPU/GPU/placa-mãe; uso e VRAM em uso da GPU; temperaturas de chipset e VRM estão previstos no contrato `LiveHardwareSensors`, mas permanecem **nulos com estado Unavailable**. A identificação estática de GPU, memória, placa-mãe e BIOS já possui um caminho independente em `DetailedHardware.Capture`; não duplicar esse inventário.

**Nunca derivar temperatura, potência, RPM ou uso da GPU de nomes, versões de driver, valores publicitários ou leituras de outra máquina.** Não introduzir novo driver kernel, ferramenta externa, serviço, privilégios administrativos, pacote de terceiros ou rede sem revisão específica.

## Contrato para a frente de interface

Instanciar um `LiveHardwareMonitor` por sessão de janela; utilizar o timer já existente ou um orquestrador único. Exibir `MonitorReading<T>.Value` apenas se `Availability == Available`. Em `WarmingUp`, exibir “Medindo…”; em `Unavailable`, exibir “Indisponível”. Não indicar que `CpuFrequencyMhz` é frequência turbo instantânea.

A memória da aba Sistema pode continuar usando a própria série `MemoryRamHistory`. Para evitar duas leituras redundantes de RAM, a integração poderá chamar `Capture(amostraRamExistente)`; `Capture()` permanece disponível para quando o motor coletar sozinho.

A interface visual não foi alterada nesta branch. Nenhum valor do protótipo apresentado em imagens foi usado como leitura real.

## Validação

`TestesLayout/LiveHardwareMonitoringTests.cs` cobre deltas e overflow de CPU, aquecimento, sensores nulos, reutilização da cadência e invariantes de leituras Windows; executa pelo argumento `--monitoramento` e também na suíte geral do projeto. Compilação e homologação física dependem da CI e de execução autorizada nos PCs.

## Referências oficiais

- [GetSystemTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes)
- [GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex)
- [CallNtPowerInformation](https://learn.microsoft.com/en-us/windows/win32/api/powerbase/nf-powerbase-callntpowerinformation)
- [PROCESSOR_POWER_INFORMATION](https://learn.microsoft.com/en-us/windows/win32/power/processor-power-information-str)
- [GetTickCount64](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64)
