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

## Reconhecimento de hardware de qualquer fabricante

`NativeHardwareDiscovery.Capture()` usa SetupAPI (`DIGCF_PRESENT | DIGCF_ALLCLASSES`) para enumerar os dispositivos PnP presentes, sem lista fixa de marcas e sem interpretar o modelo a partir de textos publicitários. Cada dispositivo tem nome reportado pelo Windows, fabricante quando disponível, classe PnP, barramento e ID de instância **somente em memória** para correlação exata futura. Classes ainda não categorizadas permanecem no inventário como `Other`, não são descartadas. Limite de segurança 4096 dispositivos; falhas, ausências de identidade ou truncamento geram `Partial` ou `Unavailable`, nunca “100% reconhecido”.

Classes contempladas: processadores, placas gráficas, unidades e controladores de armazenamento, rede, Bluetooth, áudio, USB, periféricos de entrada/jogos, monitores, baterias, sensores, câmeras, impressoras, firmware, segurança e dispositivos de sistema. Não supor que um dispositivo Bluetooth ou USB pertença à classe “áudio” só pelo barramento. Dispositivos virtuais podem estar presentes, e identificação no Windows não comprova medição de sensores físicos.

A **RAM física** continua sendo lida pelo `MemoryRamReader` existente e a **ficha estática** de CPU, BIOS, RAM, placa-mãe e GPU pelo `DetailedHardware.Capture` existente. O leitor PnP é **consultivo e sob demanda**, não deve rodar a cada 2 s. A frente da UI deverá reutilizar esse inventário e a identificação de Drivers quando já disponíveis, evitando capturas duplicadas na mesma sessão. Nenhum resultado do inventário PnP autoriza atualização de drivers, remoção, configuração, instalação nem limpeza.

**Cobertura real ≠ métricas reais:** temperatura, ventoinha e potência dependem de driver/firmware e APIs com acesso autorizado. Fabricantes/modelos desconhecidos continuam identificáveis pelo Windows mesmo quando a medição é impossível. Uma futura camada de provedores por capacidade, incluindo NVIDIA/AMD/Intel e interfaces genéricas, deverá ser homologada separadamente sem instalar driver kernel por padrão.

## Testes de reconhecimento

`TestesLayout/NativeHardwareDiscoveryTests.cs` valida categorias e barramentos com identificadores sintéticos. O argumento `--monitoramento-descoberta` faz uma enumeração **somente leitura** do próprio runner Windows, verifica cobertura e parcialidade, mas não comprova sensores de terceiros. O teste de classificação entra na suíte geral sem fazer uma enumeração PnP extra no startup dos outros testes.

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

## Evolução dos provedores de sensores — 23/11/2026

**CPU e RAM:** permanecem os contadores nativos GetSystemTimes, frequência reportada pelo Windows, GlobalMemoryStatusEx e histórico limitado por sessão. Temperatura CPU Package indisponível sem fonte confiável; não inventar usando temperatura ACPI.

**GPU NVIDIA (NvidiaNvmlSensorProvider.cs):** lê NVML local apenas de caminhos controlados pelo sistema (System32 e Program Files/NVIDIA Corporation/NVSMI), sem carga por PATH/pasta atual. Enumera até 16 GPUs e preserva nome, UUID, índice e medições por placa, sem associar GPU 0 arbitrariamente a qualquer dispositivo PnP. Funções consultivas: uso, temperatura, potência mW convertida em W, memória framebuffer em bytes e porcentagem de ventoinha. Porcentagem NVML NÃO é RPM. Em NVIDIA sem NVML ou placas AMD/Intel sem outro provedor, a leitura permanece indisponível, ainda que o dispositivo apareça no inventário PnP. O WDDM pode afetar a semântica da memória VRAM reportada.
Fontes: https://developer.nvidia.com/management-library-nvml ; https://docs.nvidia.com/deploy/nvml-api/latest/api/group__nvmlDeviceQueries.html

**Discos (WindowsStorageSensorProvider.cs):** WMI Windows Storage com duas consultas SELECT às classes MSFT_PhysicalDisk e MSFT_StorageReliabilityCounter. A temperatura, o desgaste e as horas de uso só são anexados ao dispositivo pelo mesmo DeviceId; em caso de duplicidade, ausência ou contradição, métrica indisponível e estado parcial. Wear não significa vida útil restante. Zero sem prova de suporte não é interpretado como leitura 0%. Consulta sob demanda e em thread STA, nunca no timer de 2 s. Não aciona comando ATA/NVMe nem escreve em disco.
Fonte: https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-storagereliabilitycounter

**Plataforma/placa-mãe (AcpiThermalZoneProvider.cs):** consulta as zonas de calor MSAcpi_ThermalZoneTemperature expostas pelo firmware, convertendo décimos de Kelvin para °C. A zona NÃO é automaticamente CPU, chipset nem VRM; sem vínculo explícito, temperatura desses componentes continua indisponível. Identificação estática de placa-mãe/BIOS continua pelo DetailedHardware existente. Sem instalar driver de kernel nem executar SMBus/EC.

**Carteira de provedores (HardwareSensorPortfolio.cs):** contrato consultivo que reúne CPU/RAM, PnP, GPUs NVIDIA, discos e zonas ACPI. Reutiliza inventário existente quando fornecido. Exige chamada explícita via worker STA e token de cancelamento; não modifica a nova interface, não cria novo timer, não envia dados e não faz alterações no sistema.

**Testes:** HardwareSensorProviderTests valida limites NVML/Storage/ACPI, associação estrita por DeviceId e indisponibilidade de sensores; teste sintético na suíte geral. O argumento --monitoramento-provedores permite rodar as sondas nativas consultivas em Windows, mas não é acionado automaticamente no CI de layout.

**CI histórica:** no SHA 0961bba665bfc7c667118d8980e0017ec3a9e996, integridade PASS; Windows FAIL somente no teste Drivers em Windows Server 2025 devido a TaskCanceledException em TestesDrivers/Program.cs:357, timeout de 25 s na consulta WMI. Política, BETA, regressões Windows e Drivers Server 2022 aprovados. Não foi alterada a frente Drivers para tentar mascarar a falha. Toda nova versão exige CI no HEAD exato.
## Expansão AMD / Intel / NVIDIA por contadores WDDM e DXGI

Em Windows com driver WDDM compatível, `WindowsWddmGpuProvider.Capture(token)` consulta sob demanda as classes WMI em `root\cimv2` `Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine` e `Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory`. São contadores **neutros por fabricante**, não pacotes AMD/Intel/NVIDIA. A ausência da classe no Windows ou em driver antigo resulta em `Unavailable`/`Partial`, não em leitura zero.

- Cada métrica permanece vinculada à chave `{LUID high, LUID low, índice físico}` informada na instância de contador do Windows. Identidades ilegíveis são rejeitadas e registradas como parcialidade. Contadores de GPUs distintas nunca são somados.
- `BusiestEnginePercent` calcula o maior percentual entre engines **individualizados**, somando somente contribuições de processos do mesmo engine. Nunca somar porcentagens de engines 3D/cópia/decodificação entre si nem chamar essa medida de uso total da GPU. Em uma engine cujos processos somam mais de 100%, o valor é inconclusivo.
- `DedicatedUsedBytes` e `SharedUsedBytes` são consumo do adaptador informado pelo WDDM; não são necessariamente bytes de memória exclusivos de um jogo. Leitura sem contador válido ou instância duplicada fica indisponível.
- `DxgiGpuIdentityProvider.Capture()` enumera adaptadores por `CreateDXGIFactory1` / `EnumAdapters1` / `GetDesc1` sem criar dispositivos de renderização. Preserva nome, IDs PCI e LUID da sessão; não associa pelo índice GPU 0. `Correlate()` exige LUID DXGI único idêntico ao do contador WDDM. O índice físico permanece separado porque adaptadores vinculados podem compartilhar LUID.
- O vínculo adicional do nome PnP exige **classe Display, VID/DEV/SUBSYS idênticos e exatamente uma entrada presente em inventário completo**. GPUs idênticas ou inventário parcial impedem associação PnP. Sem DXGI correspondente, o contador permanece identificável por LUID, sem nome ou modelo presumido.
- NVML continua como provedor especializado **somente NVIDIA**, com temperatura e potência quando disponíveis. O monitor neutro WDDM atende AMD/Intel/NVIDIA com suporte do sistema, mas não fornece temperatura nem RPM nas GPUs AMD/Intel sem provedores adicionais.

Testes: `TestesLayout/WindowsWddmGpuProviderTests.cs` cobre isolamento por LUID, agregação por engine, instâncias duplicadas e contadores ausentes; `DxgiGpuIdentityProviderTests.cs` cobre ligação DXGI/PnP única, recusa de modelo divergente, duas GPUs com mesmo ID e inventário parcial. Os testes entram na suíte normal em modo **sintético**. Os modos `--monitoramento-wddm` e `--monitoramento-dxgi` acionam sondas nativas explícitas fora da suíte automática.

Fontes oficiais: https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/ ; https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgifactory1-enumadapters1 ; https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1 ; https://learn.microsoft.com/en-us/windows/win32/wmisdk/monitoring-performance-data

Limites e segurança: sem instalação de drivers, DLLs ou pacotes, sem elevação, sem merge, sem mudança no layout, sem telemetria ou inicialização automática da captura WMI. Confirmação de comportamento físico em PC FORTE e PC FRACO permanece pendente.
## Checkpoint consultivo de homologação — 23/11/2026

CI do HEAD anterior `1b6e7bb0c4779a4f54fbdd6b145476f86a3185a8`: Windows `45231532797` SUCCESS (incluindo testes sintéticos WDDM, DXGI, NVML/Storage, RAM/CPU, Drivers 2022/2025, política e BETA), integridade `45231532762` SUCCESS. A compilação Windows x64 também foi aprovada. Não equivale a homologação física em GPUs AMD/Intel/NVIDIA: os runners podem não expor GPU real.

Foi encontrada e corrigida uma associação ambígua: um único LUID WDDM pode conter **mais de um índice físico**. O DXGI GetDesc1 identifica o adaptador LUID, mas não necessariamente cada `phys`. `DxgiGpuIdentityProvider.Correlate` agora preserva os contadores dos dois índices, **sem associar nome DXGI nem DeviceId PnP** a qualquer um, até haver evidência discriminante. Regressão sintética adicionada para o caso de dois `phys` no mesmo LUID.

`HardwareMonitoringDiagnosticReport.Format(portfolio)` gera relatório consultivo em português somente com contagens e estados. A versão não inclui serial, nome de usuário, DeviceId, UUID, nome de GPU, caminho local ou nome de disco. A origem separa monitoramento de CPU/RAM, DXGI por fabricante, WDDM, NVML, Storage WMI e zonas ACPI, indicando número de sensores efetivamente lidos e lacunas. Não inferir temperatura de placa-mãe, CPU/VRM nem RPM de fan %.

Modo de sondagem local explícita em ambiente Windows: `dotnet run --project TestesLayout/TestesLayout.csproj -c Release -- --monitoramento-resumo-nativo`. O executável de testes usa a thread STA existente e chama os provedores de leitura uma vez; não aparece em produção nem dispara download/instalação. O orçamento consultivo de cancelamento de 40 segundos não força interrupção de uma chamada COM bloqueada; ausência ou timeout é inconclusivo. A CI de layout geral continua com casos sintéticos para WMI/NVML e executa uma consulta DXGI nativa de baixa interferência em runner Windows, **sem exigir GPU física**. A validação PC FORTE/PC FRACO, capacidade de sensores AMD/Intel e correlação física permanecem pendentes.

Revisão de documentação oficial: `MSFT_StorageReliabilityCounter.DeviceId` corresponde ao `MSFT_PhysicalDisk.DeviceId` quando associado, mas a API também publica a associação formal `MSFT_PhysicalDiskToStorageReliabilityCounter`. O matcher continua recusando IDs físicos duplicados e telemetria duplicada; evolução futura poderá usar a associação WMI formal. Referência: https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-physicaldisktostoragereliabilitycounter
## Associação formal Storage WMI (23/11 — fase consultiva)

Fonte: Microsoft `MSFT_PhysicalDiskToStorageReliabilityCounter` documenta dois membros **REF** (`PhysicalDisk` e `StorageReliabilityCounter`) no namespace `root\Microsoft\Windows\Storage`. A classe `MSFT_StorageReliabilityCounter` admite que seu `DeviceId` também pode corresponder a `MSFT_Disk.Number`; por isso, apenas a coincidência de `DeviceId` não prova que o valor de temperatura pertence a um `MSFT_PhysicalDisk` específico.

Implementação em `StorageAssociationLinker.cs` e `WindowsStorageSensorProvider.Capture`: consultas WMI SELECT de discos, contadores e associação oficial; leituras de `SWbemObject.Path_.RelPath` para a instância; referência WMI normalizada somente para classes esperadas no mesmo namespace local; relação REF exata, unívoca, e checagem adicional de DeviceId igual. Sem relação formal comprovada, o disco físico permanece reconhecido, mas temperatura, desgaste e horas ficam como **indisponíveis**, nunca são recuperados por fallback no identificador numérico, nome ou ordem.

Rejeição explícita de referências inválidas/remotas, caminho ausente, classe distinta MSFT_Disk, referências duplicadas, mais de uma relação ao mesmo disco ou contador, contradições de DeviceId e inventário truncado. Falha/ausência da classe de associação gera estado parcial, sem liberar sensores. A coleta segue somente leitura sob demanda, no worker STA, sem SMART bruto, comando ATA/NVMe, elevação, instalação nem escrita.

Testes `TestesLayout/StorageAssociationLinkerTests.cs`: vínculo cruzado com ordem inversa, ausência de relação, classe/namespace/host remoto inválidos, identificação duplicada, contador apontado por dois discos, referência inexistente, contradição de DeviceId, caminho de WMI ausente e indisponibilidade quando a classe de associação falha. Inclusos na suíte sintética geral e no modo `--monitoramento`.

Sonda local **explicitamente solicitada**: `dotnet run --project TestesLayout/TestesLayout.csproj -c Release -- --monitoramento-storage-relacao`. A execução consulta apenas WMI Storage e informa contagens/estado, sem mostrar DeviceId, serial ou modelo. Não executar no CI geral para evitar chamada WMI lenta em runner sem armazenamento físico representativo.

**Homologação:** testes sintéticos não equivalem a prova física em SSD/HDD/SATA/NVMe, nem asseguram que todos oferecem sensores. O hardware do PC FORTE/FRACO precisa ser observado para homologar temperatura e desgaste nativos; valores ausentes continuam indisponíveis. A UI e as demais frentes permanecem intocadas.

Referências oficiais: https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-physicaldisktostoragereliabilitycounter ; https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-storagereliabilitycounter ; https://learn.microsoft.com/en-us/windows/win32/wmisdk/swbemobject-path-
## Ensaio de homologação consultiva Windows (registro histórico)

Em 23/11/2026, foi criado **temporariamente** o workflow isolado `monitoramento-consultivo-windows.yml` para a PR Draft #106, com permissões `contents: read`, checkout do SHA exato e execução em Windows Server 2022/2025, sem instalar drivers, conceder privilégios extras ou enviar identificadores. A política de segurança do projeto exige lista explícita de workflows aprovados: o teste `test_actions_fixadas_e_sem_credenciais_persistentes` reprovou a adição do workflow com `Workflow adicionado/removido sem revisar a lista explicita de CI`. O arquivo de workflow experimental foi **retirado da branch**, sem afrouxar a lista de CIs nem alterar scripts de segurança. Os logs históricos permanecem no GitHub; o monitoramento em produção não inicia essa sonda automaticamente.

O primeiro ensaio [#45239375044](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/45239375044) compilou e aprovou os contratos sintéticos e a enumeração DXGI (2 adaptadores), mas a coleta completa no Windows 2025 falhou em `AcpiThermalZoneProvider.Capture` com `Microsoft.CSharp.RuntimeBinder.RuntimeBinderException` ao ler objeto WMI ACPI nulo. A correção `ReadWmiValueOrNull` recusa objeto, coleção ou campo nulos, publica estado ACPI parcial e impede que a falta de um sensor interrompa as demais consultas. Testes de regressão reproduzem valores nulos/ausentes.

O ensaio corrigido [#45239911861](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/45239911861) terminou **SUCCESS** nos dois runners Windows Server 2022 e 2025 no SHA `1e8e6cb84669fff015fde5bfa1643fa97fa37b1d`: todos os contratos e smoke DXGI aprovados, consulta nativa completa sem exceção. Resultados agregados, sem identificadores:

- Windows 2022: CPU/MHz/uptime/RAM disponíveis; PnP completo com 64 dispositivos; DXGI 2 adaptadores; WDDM 1 engine e 1 contador de memória dedicada disponíveis, 1 LUID correspondente ao DXGI; 2 discos físicos com temperaturas/desgaste/horas indisponíveis; NVML indisponível; ACPI parcial com zero zonas válidas.
- Windows 2025: CPU/MHz/uptime/RAM disponíveis; PnP completo com 61 dispositivos; DXGI 2 adaptadores; WDDM 1 engine e 1 contador de memória dedicada disponíveis, 1 LUID correspondente ao DXGI; 2 discos físicos com temperaturas/desgaste/horas indisponíveis; NVML indisponível; ACPI parcial com zero zonas válidas.

Os runners não apresentaram GPUs físicas AMD, Intel ou NVIDIA homologáveis nem temperaturas de discos; por isso não demonstram leitura universal de sensores térmicos, RPM ou potência. A homologação física permanece dependente de autorização e de execução no PC FORTE/FRACO. O modo `--monitoramento-resumo-nativo` continua disponível **somente no projeto de testes**, acionado explicitamente. A CI geral Windows segue apenas com testes sintéticos e smoke DXGI de baixa interferência.

**Nota de segurança:** nenhuma credencial ou dado de dispositivo foi enviado a receptor externo; não alterar a lista de workflows autorizados para aprovar a PR. Somente as CIs existentes devem conceder o aceite final.
## Ampliação funcional dos motores WDDM por GPU — 23/11

Gate de partida aprovado no HEAD `15bf6d91a69ad71bce4d865cc6ff1b8c4545b450`: integridade GitHub Actions #45240497798 SUCCESS; Windows #45240497984 SUCCESS, incluindo política, Drivers 2022/2025, BETA, regressões Windows, etapa Testes layout e RAM e compilação x64. Sem novos workflows, instalação de drivers ou merge.

`WindowsWddmGpuProvider.Join` agora publica `WddmGpuAdapterSample.Engines` e `BusiestOfType` por adaptador e por **motor físico identificado**, preservando o índice e tipo reportados em cada contador WDDM e agregando somente processos do mesmo motor. A enumeração reconhece sufixos de engine como `Compute_0`, sem somar engines independentes e sem associar adaptadores por nomes. Não disponibiliza PID nem identidade privada no relatório.

O motor genérico WDDM identifica atividades separadas de 3D, COPY, COMPUTE, VIDEODECODE e VIDEOENCODE, apenas quando a classe de contador WDDM do driver retorna medições válidas. Para uma família com várias engines, `BusiestOfType` apresenta o MAIOR motor medido daquela família, não soma as porcentagens. O `BusiestEnginePercent` geral também significa maior motor e **não representa utilização física total da GPU**. Uma engine com 0% efetivamente reportado é valor disponível 0; categoria não reportada é indisponível, sem inferir inatividade.

Contadores inválidos (sem valor, fora de 0..100 ou contribuição de processos acima de 100% em um motor) ficam indisponíveis, com estado parcial, sem apagar engines distintas válidas. A disponibilidade por tipo no relatório de diagnóstico permanece agregada/sem identificadores.

Testes `WindowsWddmGpuProviderTests` e `HardwareMonitoringDiagnosticReportTests`: 3D, Copy, Compute_0, VideoDecode, VideoEncode, categoria sem dados, 0% comprovado, motor inválido em coexistência com motor válido, mais de uma engine 3D e identidade por LUID/phys. Interface de UI não modificada; sensores de temperatura/RPM/potência de GPU AMD/Intel continuam indisponíveis sem fonte oficial.

**Pendente:** CIs Windows e integridade do novo HEAD; nenhuma homologação física adicional em PC FORTE/FRACO foi executada.
## Contadores oficiais de confiabilidade de armazenamento — 23/11/2026

Gate anterior: HEAD `f127fc51c870fdafc979785995de320114d67130`, CI Windows #45244747069 SUCCESS (incluindo etapa Testes layout e RAM, política, Drivers 2022/2025, BETA e compilação Windows x64), integridade #45244747040 SUCCESS. A falha anterior de teste WDDM em `3D_suffix` foi corrigida sem alterar parser de produção nem workflows.

Expansão `StorageReliabilityCounterMapper.cs`, `WindowsStorageSensorProvider.cs` e diagnóstico sem identificadores: leitura de `ReadErrorsTotal`, `ReadErrorsUncorrected`, `WriteErrorsTotal`, `WriteErrorsUncorrected`, `ReadLatencyMax`, `WriteLatencyMax`, `FlushLatencyMax`, `StartStopCycleCount` e `LoadUnloadCycleCount` via SELECT da classe oficial `MSFT_StorageReliabilityCounter`. Não chamar `Reset`, não acessar SMART bruto, não alterar estado do disco.

**Identidade obrigatória:** essas métricas integram `StorageReliabilityRaw.Diagnostics` e são atribuídas a `StorageDeviceSensors.Diagnostics` **apenas** após o vínculo `MSFT_PhysicalDiskToStorageReliabilityCounter` via REF WMI local unívoco e DeviceId coerente. Discos sem relação REF continuam reconhecidos, mas `Diagnostics` é `null`. Duplicatas, identidades contraditórias e relação ausente não produzem métricas (fail-closed).

**Disponibilidade e semântica:** contador de erros `0` é registrado como **zero informado pelo Windows**, não prova saúde física nem que o fabricante implementou integralmente a métrica. Latência máxima em milissegundos é publicada apenas para valores positivos e até 24h (limite conservador de plausibilidade); zero, nulo, sentinela e número inválido ficam indisponíveis. Ciclos operacionais positivos válidos são publicados; zero fica indisponível sem evidência de suporte. Se erros não corrigidos excederem o total informado, somente aquele par de contadores é invalidado e o snapshot torna-se parcial. Erros e latências não equivalem a nota de saúde ou desgaste SMART.

Leitura WMI COM de campos opcionais tolera objeto/propriedade nulos ou RuntimeBinderException sem interromper a captura. O relatório agregador apresenta apenas quantidade de discos com métrica reportada, nunca DeviceId, nome, serial, valores individuais ou caminhos.

Testes `StorageReliabilityCounterMapperTests.cs` cobrem conversão UInt64/string, limites, zero reportado versus ausência, inconsistência de erros, correspondência por REF, ordenação trocada, relações ausentes ou duplicadas e recusa da identificação contraditória. A suíte existente executa os casos sintéticos sem operação administrativa; sonda Storage continua opt-in e somente leitura.

Referências oficiais: https://learn.microsoft.com/pt-br/windows-hardware/drivers/storage/msft-storagereliabilitycounter ; https://learn.microsoft.com/en-us/windows-hardware/drivers/storage/msft-physicaldisktostoragereliabilitycounter .

**Pendente:** revalidar CI Windows e integridade no novo SHA, e homologar medições de sensores físicos no PC FORTE/PC FRACO autorizado. Não atribuir a GPU AMD/Intel temperatura, potência ou RPM sem fonte oficial comprovada; CPU, GPU, RAM e ACPI seguem isolados e inalterados.
## CPU, RAM e placa-mãe por APIs WMI oficiais — checkpoint 23/11

Gate de partida na PR Draft #106, SHA `78c67221843681d46fc63648a87d12fa02462170`: Windows #45246912467 SUCCESS e integridade #45246912434 SUCCESS, incluindo compilação Windows x64, regressões, testes layout/RAM e política de segurança. Nenhuma alteração em main, G5, BETA, Drivers ou workflows.

Novo `WindowsPlatformHardwareProvider.cs`: coleta sob demanda, somente leitura, no namespace WMI local `root\cimv2`, usando classes **oficiais** `Win32_Processor`, `Win32_PhysicalMemory`, `Win32_BaseBoard`, `Win32_PerfFormattedData_PerfOS_Processor`. Identifica sockets pelo `DeviceID` WMI, módulos RAM por par `BankLabel` + `DeviceLocator`, placa-mãe pela chave `Tag`, e instâncias de uso de CPU lógica pelo nome numérico do contador (exclui `_Total`). Identidades duplicadas/ausentes, propriedades nulas, valores fora de faixa e classes indisponíveis são estados parciais/inconclusivos, não uma atribuição arbitrária ao dispositivo.

- CPU: quantidade de sockets, núcleos/lógicos **reportados pelo WMI** e `CurrentClockSpeed` em MHz reportados. Não equivale a frequência física instantânea nem a temperatura/potência do pacote da CPU. Utilização por **instância lógica** do contador Windows `PercentProcessorTime`, sem declarar que o número de instância comprova correspondência a núcleo físico ou socket.
- RAM: número de módulos SMBIOS individualizados, `Capacity` em bytes, `ConfiguredClockSpeed` reportado em MHz, código `SMBIOSMemoryType` se publicado. O código não consulta SPD privilegiado, não deduz temperatura DIMM, não presume que soma de módulos seja igual à memória utilizável pelo sistema (reserva de hardware).
- Placa-mãe: identificação `Tag`, fabricante, modelo, revisão quando publicados; não equiparar `Win32_BaseBoard` a sensor de temperatura do chipset ou VRM. Para chipset/VRM, só publicar leitura quando houver provedor dedicado com identidade inequívoca.
- GPU AMD/Intel/NVIDIA: continua no WDDM/DXGI com `LUID` e índice `phys` separados, e NVML apenas NVIDIA quando driver compatível expõe API. WDDM não oferece temperatura, potência nem RPM por si só; DXGI `QueryVideoMemoryInfo` descreve consumo e orçamento **do processo**, não VRAM total do sistema, então não é usado como métrica global de GPU.

O portfólio `HardwareSensorPortfolioReader.CaptureOnDemand` agrega essa nova fotografia depois dos sensores ACPI, sob cancelamento. O diagnóstico sem identificadores expõe somente contagens por fonte, sem DeviceID de CPU, BankLabel/DeviceLocator, Tag, nomes ou fabricante/modelo do usuário. Temperaturas CPU, memória, chipset e VRM permanecem indisponíveis. Não adiciona timer, serviço, software terceiro, rede ou instalação de driver.

`WindowsPlatformHardwareProviderTests.cs` cobre identidade única, duplicatas CPU/slot/placa/contador, unidades, valores ausentes, placeholders OEM, percentuais fora de 0–100, 0% reportado, e isolamento entre fontes WMI. Integrado à suíte `TestesLayout` e ao modo `--monitoramento` como testes sintéticos; sonda local explícita `--monitoramento-plataforma` executa a coleta nativa apenas se solicitada. Não foi executada nos PCs FORTE/FRACO.

Referências oficiais: https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-physicalmemory ; https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-baseboard ; https://learn.microsoft.com/en-us/windows/win32/wmisdk/wmi-tasks--performance-monitoring ; https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-processor .

**Gates pendentes:** CI Windows e integridade no novo HEAD; nenhuma homologação física de sensores térmicos, potência ou ventoinhas de AMD/Intel/NVIDIA, CPU, RAM, chipset ou VRM foi declarada.