# Monitor GF V2.6.9 — contrato de integração para o WORK

## Escopo e autoridade
PR Draft #114, branch ui/v269-monitor-gf-barras-dinamicas-20261124. Este documento prepara a integração futura; não é autorização para merge, release, ZIP ou execução física. O WORK deve receber o HEAD final aprovado das PRs #106, #113 e #114 e resolver conflitos por ancestralidade. Não substituir arquivos completos da outra frente. A unificação e a homologação física ficam por último, exclusivamente com o WORK.

## Componentes
- GfMonitorHost: proprietário da janela e do relay; uma instância por MainForm, na thread WinForms.
- GfMonitorSnapshotRelay / GfMonitorWindowBinding: entrega/replay, geração e descarte.
- GfResourceMonitorAdapter / GfMonitorMetricTimes: projeção sem coleta, validade individual dos sensores e identidade DXGI.
- GfThermalEvidence / GfThermalPresentation: pares nativos comprovados, aviso/máximo, histerese, ausência honesta.
- GfMonitorSettingsPanel: controle independente de Configurações; não persiste nem coleta por conta própria.
- GfDynamicProgressBar / GfUniversalMeter: apresentação reutilizável, sem autorização ou operação de manutenção.

## Sequência do host, sem segundo coletor
1. Construir GfMonitorHost com caminho local exclusivo monitor.json em diretório de configuração já existente e autorizado. O construtor somente carrega preferências, não abre janela.
2. Na thread de UI, Configure(theme, animationsEnabled) com a aparência já aplicada pelo shell. Restore(DateTimeOffset.UtcNow) somente quando o shell tiver concluído a inicialização e estiver autorizado a restaurar a preferência.
3. Usar BeginGeneration(WddmGpuKey? selected) para o produtor atual e guardar o long retornado. Seleção explícita ou identidade física inequívoca; não escolher GPU 0, nome, índice DXGI isolado ou primeiro adaptador. Sem identificação inequívoca, usar null e manter CPU/RAM.
4. Reutilizar o timer, _v269Monitor, cache e SemaphoreSlim do coletor da PR #113. Separar os gates de segurança/encerramento do requisito de visibilidade de Início. O predicado WantsCollection(homeDemand, collectionAllowed) acrescenta demanda do Monitor GF visível, sem contornar gates. Não converter o portfólio completo WMI em consulta periódica; manter coleta leve 2 s e GPU 8 s conforme já existente. Para ativação fora de Início sem cache inicial, usar a rotina única autorizada do produtor, nunca um coletor do monitor.
5. Após obter um snapshot e verificar cancelamento, geração, estado de encerramento e autorização, publicar na thread UI com Publish(generation, portfolio, DateTimeOffset.UtcNow, evidence). A UI deve publicar o portfólio composto atual, inclusive snapshots parciais de falha, não uma versão antiga que sobrescreva outro produtor. Não reatribuir CapturedAtUtc de fontes antigas. Manter a atualização de Início e o relay usando os mesmos dados; não invocar eventos de apresentação dentro da trava de coleta.
6. Em troca de topologia, GPU selecionada ou reinício do produtor, cancelar as entregas da geração anterior e chamar BeginGeneration antes de aceitar nova coleta. Geração não é identidade de usuário; não persistir nem enviar LUID.
7. No encerramento definitivo, parar/cancelar o produtor pelo mecanismo já existente e impedir novos callbacks. Dispose do host na thread UI fecha janela, libera relay e preserva preferência de habilitação. Não confundir fechamento manual do monitor (desabilita) com encerramento do shell (preserva).
8. Tratar retorno false de Apply/Save como persistência não concluída, mantendo estado em memória; comunicar sem modal/tooltip conforme interface aprovada.

## Configurações
Criar GfMonitorSettingsPanel no local aprovado em Configurações > Geral. Hidratar com LoadPreferences(host.Preferences()) e Configure(theme). No evento PreferencesChanged, usar ReadPreferences(host.Preferences()) para conservar a posição da janela e passar ao host.Apply. O WORK deve integrar esse evento ao mecanismo existente de gravação/debounce, evitando gravação em cada deslocamento do slider. Não alterar a aparência aprovada de outras configurações. Sincronizar ao reabrir a aba e após mudança de modo/fechamento manual. Não abrir janelas extras de configuração.

## Evidência térmica
A saída D3DKMT da PR #106 ainda não fornece o par TemperatureWarning e TemperatureMax; não fabricar capacidade por nome, família ou temperatura genérica. Quando o contrato real existir, construir GfThermalEvidence.FromD3dKmt apenas com consulta de capacidades bem-sucedida, mesma chave física, mesma geração, valores nativos em décimos de °C e instante de capacidades associado ao sensor. Sem par válido, omitir evidence. Não descartar uma leitura medida somente por ultrapassar máximo reportado; reconciliar com as regras de validade do provedor. A escala de referência sem limites não equivale a diagnóstico. Temperaturas CPU também dependem de sensor comprovado. Nenhuma mudança em #106 é feita por este documento.

## Adoção das barras
Em Início/Sistema, GfMeterScale.Frame(Usage, percent, theme) para percentuais realmente medidos; GfUniversalMeter.Disk(usedBytes, capacityBytes, theme) somente para volumes/armazenamento cuja capacidade é conhecida. Para Aplicativos, Drivers, Limpeza e Desempenho, Reparo e Rede, Windows Update, usar GfUniversalMeter.Operation(reportedPercent, state, inverted, theme) com progresso do motor real; null enquanto indeterminado. Não sintetizar percentuais por tempo ou converter 100% em sucesso. Estado Success exclusivamente após confirmação do backend; Failed/Cancelled devem permanecer distintos e descritos. Configure(theme, animate) e Present(frame, descrição acessível), preservando controles, layout, estados e autorizações aprovados. Não instalar, desinstalar ou reparar por causa da barra.

## Critérios de aceite na integração final
- Um único produtor, sem duplicar WMI/contadores, sem temporizador de coleta no monitor e sem novas dependências, serviços, UAC ou rede.
- CPU/RAM com atualização leve; GPU/VRAM/temperatura com suas próprias datas. Falhas, ausência de identidade, atraso, retorno antigo, troca de GPU e mudança de tema não produzem dados fictícios.
- Monitor fora de Início somente se habilitado/visível e gates permitirem. Respeitar fechamento, bandeja e cancelamento; nenhuma janela ou callback residual.
- Três temas, compacto/expandido, foco/teclado, DPI/monitores, opacidade/encaixe/posição, ativação persistente e falhas de gravação sem travar a aplicação.
- Progresso confirmado e indeterminado, estados de falha/cancelamento; barras ocultas ou estáticas sem timer ativo.
- CIs existentes em SHA exato, testes automatizados do monitor e regressões das outras frentes. A aprovação isolada não substitui teste integrado.
- Só depois da unificação autorizada: homologação física no PC FORTE e PC FRACO, com inventário e métricas reais, consumo de CPU/RAM e estabilidade durante uso prolongado/jogos. Registrar limitações sem identificadores. Não executar manutenção física ou instalação sem autorização específica.


## Correção visual obrigatória posterior à entrega inicial
O proprietário rejeitou toda a legenda cromática inferior. Preservar o Monitor GF sem legenda, sem rodapé substituto e sem espaço reservado. O modo expandido deve medir 552 x 186 px em 96 DPI; o compacto permanece 552 x 38. A lógica de cores e alertas e as descrições acessíveis de cada métrica continuam existentes, sem texto explicativo adicional na janela. O HEAD corrigido deve substituir 9da1186 nos documentos de integração somente depois da aprovação das suas CIs. Não reintroduzir a legenda de versões anteriores ou da referência histórica.
