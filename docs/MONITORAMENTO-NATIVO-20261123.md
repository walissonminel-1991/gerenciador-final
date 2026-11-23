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
## CI isolada de homologação consultiva (somente leitura)

Workflow isolado `.github/workflows/monitoramento-consultivo-windows.yml`, limitado à PR Draft #106 e a arquivos exclusivos desta frente, com `permissions: contents: read` e checkout do SHA exato. Runners Windows Server 2022/2025 com .NET 10.0.402, sem segredos, drivers, privilégios extras, download de sensores, execução administrativa ou upload de artefatos.

Em cada runner compila apenas o projeto de testes e executa: 1) contratos sintéticos `--monitoramento`; 2) smoke nativo DXGI `--monitoramento-dxgi` (nenhuma GPU física exigida); 3) uma rodada `--monitoramento-resumo-nativo` (PnP, CPU, RAM, NVML, WDDM, DXGI, Storage e ACPI), imprimindo **somente resumo sem identificadores**. Ausência de fonte/driver deve ser explicitamente inconclusiva; não significa falha na máquina nem ausência de dispositivo. O runner sem GPUs físicas não prova funcionamento de temperatura, energia ou ventoinhas AMD/Intel/NVIDIA. Não usar logs da CI como autorização para manutenção.

Essa CI é complementar às regressões Windows anteriores: não altera os arquivos dos workflows de Drivers, de release, de integridade nem a interface. A homologação física efetiva permanece reservada às máquinas Windows autorizadas, respeitando AVG e leitura somente consultiva.
## Falha real reproduzida no CI consultivo e correção ACPI

Em 23/11/2026, a primeira execução do novo workflow monitoramento consultivo #45239375044 compilou e aprovou os contratos sintéticos e o smoke DXGI no runner Windows Server 2025 (2 adaptadores DXGI). Ao executar `--monitoramento-resumo-nativo`, falhou com `Microsoft.CSharp.RuntimeBinder.RuntimeBinderException: Cannot perform runtime binding on a null reference`, em `AcpiThermalZoneProvider.Capture` / `Get("InstanceName")`, porque o provedor WMI ACPI devolveu um objeto de propriedade/coleção nulo. A falha é do monitoramento ACPI, não dos Drivers, do Windows Update nem da interface.

Correção em `AcpiThermalZoneProvider.ReadWmiValueOrNull`: testa objeto/coleção/propriedade nulos antes da chamada dinâmica, trata erros WMI/RuntimeBinder como campo ilegível, marca o snapshot ACPI parcial e não inventa temperatura. `TestesLayout/HardwareSensorProviderTests` reproduz entrada e coleção nulas, e a propriedade ausente com ExpandoObject sintético, exigindo estado inconclusivo sem exceção. Sem alteração de sensor ou instalação de driver. Gates: CI Windows, integridade e workflow consultivo no **novo HEAD**; a correção não foi considerada aprovada antes da execução.