# Histórico de versões — Gerenciador Final

## v5.13.11.32 — auditoria local estruturada de limpeza (TESTE; CI aprovada)

Na segunda bateria presencial da .31, capturas mostraram limpeza real de 47 arquivos/pastas,
17 itens preservados, zero falhas, seguida de cleanmgr do Windows com código 0.
Os eventos só estavam visíveis no painel e não no arquivo de diagnóstico.
A nova versão grava exclusivamente categoria fixa de ação, início, cancelamento,
resumo agregado de temporários e código de saída das limpezas elevadas.
Não grava conteúdo do pipe, nomes de arquivos, caminhos, mensagens de exceção
ou detalhes de usuário; código 0 não significa quantidade de bytes liberados.
O mecanismo de remoção não foi alterado. Testes de campos inválidos incluídos.
CI aprovada em 21/10/2026: compilação Windows x64, regressões (incluindo testes de auditoria), 132 combinações de layout e políticas de segurança. Teste presencial da .32 ainda pendente; roadmap 26/42.


## v5.13.11.31 — MISSÃO WORK 4 (TESTE; CI aprovada)

- MSIX conserva apenas em memória a última leitura completa da sessão quando uma fonte falha; itens retidos têm presença não confirmada e não recebem receitas. Recuperação completa substitui a fotografia; não corrige por suposição a causa de 0x80040154.
- Fusão de atualizações recusa resultados de outra linha/snapshot e revalida somente inventário. Cache de dez minutos usa relógio monotônico, com origem cache/HTTPS observável e regressões de TTL/cancelamento.
- Resíduos registram orçamento e motivos por origem; corrigida contagem além do teto de 128 filhos e de processos já consultados. Ancestrais presos durante enumeração, sem mais raízes, profundidade ou orçamento. Modal contido no monitor do proprietário após escala DPI.
- Consulta de drivers acrescenta vínculo INF publicado ↔ Driver Store por SetupAPI e conferência de volta ao INF exato. Múltiplos dispositivos, presença desconhecida, assinatura WMI, limites e pacotes não consultados são preservados. Não enumera o Store inteiro, não reconstrói histórico Windows e não verifica assinatura criptográfica.
- Diagnóstico opt-in limita todas as tentativas a 160, inclusive troca de perfil e falhas de gravação; sink indisponível não interrompe consultas. Campos novos somente numéricos, sem envio ou persistência adicional.
- Roteiro, matriz P0–P3 e checkpoints consolidados nos documentos existentes. Nenhum checkpoint novo concluído: **26/42**. R4 e G1–G5 bloqueados; main e PR Draft preservados. CI final aprovada: 360/360 resíduos, 41/41 MSIX, 95/95 instaladores, 69/69 multifontes; pacote e 273 hashes conferidos. A integração SetupAPI e um stub antigo reprovaram em rodadas anteriores, foram corrigidos sem remover assertivas. [Evidências e pacote](docs/OPERACAO-FAXINA-FINAL.md#candidata-aprovada-e-pacote-conferido).


## v5.13.11.30 — comparação de drivers e clareza diagnóstica (TESTE; CI aprovada)

No PC FORTE a .29 mostrou 177 associações de drivers (antes 168), 63 pacotes INF (antes 60) e 9 IDs novos no histórico local, sem entradas ausentes ou alteradas. A segunda leitura foi estável e a consulta PnP passou de 166 para 175; não atribuir a atualização, falha nem remoção sem evidência por ID. O inventário permaneceu em 174 (27 MSIX), com 33 entradas marcadas como inventário somente e 34 sem desinstalação possível. O modo de testes separa essas contagens e discrimina o tipo de fonte com falha de resíduos por rótulos fixos, sem gravar caminhos ou novos identificadores. Não altera varreduras, limites de segurança nem permissões. O botão de otimização recebe rótulo visual compacto mantendo o nome acessível. A investigação de resíduos segue parcial, 867 grupos examinados, 43 candidatos gerais/44 grupos unificados e 161 locais; nenhuma exclusão. Regressões Windows aprovadas (351/351 resíduos, 30/30 MSIX, 132 cenários e verificações das novas métricas), além de build e políticas. Teste presencial da .30 ainda pendente. Checkpoints 26/42.


## v5.13.11.29 — diagnóstico de resíduos e layout para DPI (TESTE)

Bateria presencial da .28 no PC FORTE confirmou 174 aplicativos, 27 MSIX, três possíveis atualizações (7-Zip, Outlook/Store, Proton Pass), 168 associações de drivers e fotografia de drivers estável. Resíduos: 867 grupos avaliados na descoberta geral, 213 preservados por vínculo ativo, 611 sem indícios independentes, 43 candidatos gerais, 44 grupos/161 locais após união com auditorias específicas. Cobertura parcial; nenhuma exclusão.

A .29 deixa explícitos `grupos_avaliados` e `candidatos_gerais`, e renomeia as contagens filtradas no modo voluntário de testes para não confundir os 867 avaliados com os 44 exibidos. No PC FORTE a seleção do modo foi posterior à primeira leitura: `TESTES.INVENTARIO` ausente não significa zero. Linhas de controles de Limpeza e desempenho recebem espaço adicional proporcional ao DPI para reduzir cortes visuais em monitores de 125%+; aprovação presencial ainda pendente. A regressão garante área de conteúdo, não apenas altura nominal. Não foram alterados orçamentos de varredura, remoção de resíduos, ações com drivers, instaladores ou permissões. CI aprovada: 351/351 resíduos, 30/30 MSIX, 132 resoluções/abas e testes de métricas. Roadmap 26/42.


## v5.13.11.28 — coleta comparativa local PC FORTE / PC FRACO (TESTE)

Escolha temporária e explícita em Limpeza e desempenho: Desligado (padrão), PC FORTE ou PC FRACO. Somente após ativar, acrescenta métricas numéricas de inventário, atualizações, drivers, resíduos e diagnóstico completo às consultas que o usuário já solicita. Esquema fechado de categorias e campos; sem IDs de dispositivo, nomes, caminhos, arquivos, dados pessoais, uploads ou novas varreduras. Limite de 160 observações extras por sessão e desativação imediata. Não muda ações de atualização, limpeza, drivers, permissões ou os 26/42 checkpoints. Regressões Windows aprovadas, incluindo 351/351 resíduos, 30/30 MSIX, 132 cenários de layout e testes da coleta opt-in; testes presenciais da .28 ainda pendentes. Limite acumulado por sessão, mesmo após alternar modo, aprovado em teste dedicado. Falhas anteriores de fixture e de layout em 1280×720 foram corrigidas e revalidadas.


## v5.13.11.27 — melhoria conservadora da identificação de drivers (TESTE)

No PC FORTE, a .26 observou inventário estável de 174 entradas, 27 aplicativos MSIX, duas possíveis atualizações, 168 associações de drivers e resíduos parciais em 44 grupos/161 locais. A .27 usa a descrição PnP do MESMO dispositivo quando falta o nome, exibe ID local em tooltip e permite pesquisa por esse ID sem incluí-lo em diagnóstico; versões e assinaturas brutas preservadas. Testes específicos cobrem o fallback e a pesquisa. Nenhum motor de atualização, driver, remoção, rollback, worker, autorização de resíduos ou privilégio foi ampliado. CI Windows da .27 aprovada (351/351 resíduos, 30/30 MSIX, testes de drivers incluindo novo fallback; builds e políticas aprovadas). Teste presencial da .27 realizado no PC FORTE, sem manutenção real de drivers ou resíduos; checkpoints **26/42**.

## v5.13.11.26 — incorporação conservadora (TESTE)

A inspeção sob demanda explica por que preservar favoritos, sessões, credenciais, perfis, playlists, jogos salvos, backups e fontes de instalação. São pistas por nomes, sem ler conteúdo nem comprovar propriedade. Cache não implica abandono ou espaço recuperável. Subpastas genéricas desses dados permanecem associadas ao pai, evitando falsos produtos e fusões entre fabricantes. Recusas de segurança `InvalidDataException` são tratadas como consulta parcial na sessão e interface. Implementação própria após analisar Dism++/WinScript e documentação oficial; nenhum código, catálogo ou workflow externo copiado. Licença MIT, raízes, profundidade, worker, motor de atualizações e exclusão bloqueada preservados. [Procedência, validação e roteiro](docs/OPERACAO-FAXINA-FINAL.md#incorporação-conservadora--v5131126-teste). Roadmap **26/42**; testes nos PCs, MSIX e AVG continuam pendentes.

## v5.13.11.25 — revisão da faxina (TESTE)

Preservação de inventário MSIX observado mesmo em falhas tardias, fase/apartment no diagnóstico e propagação de cobertura parcial ao diagnóstico completo. Nomes de drivers ausentes podem usar o nome PnP do mesmo ID, sem reinterpretar versões. Limpeza temporária e logs passam a conferir a identidade efetiva; consentimento e pendência recusam redirecionamento. Corrigido o contrato GUID/revisão do Windows Update entre consulta, UI e worker, sem instalações no teste. Correções da ABI de restauração, do vínculo MSI/GameInput com o ZIP autenticado e da resolução de desinstaladores do Windows. Certificado extraído do assinante validado pelo WinTrust, sem API obsoleta. Contagens e falhas de fontes de resíduos corrigidas; três arquivos e métodos órfãos retirados. Scripts derivam versão do projeto/PE, CI valida referências e oferece testes externos opcionais. [Relatório único, validações e limites](docs/OPERACAO-FAXINA-FINAL.md). Roadmap mantém **26/42**, PR Draft, resíduos somente leitura, sem release estável. A causa do erro MSIX do PC fraco e a compatibilidade AVG continuam pendentes.

## Manutenção de repositório em 21/10/2026 (sem versão nova de executável)

Removidos da árvore de desenvolvimento workflows versionados antigos e guias de teste v5.12 obsoletos. Mantidos três fluxos CI oficiais, suíte de segurança ajustada à lista explícita, atualização do README e progresso funcional coerente em 26/42. Commits anteriores e resultados históricos permanecem consultáveis.

## v5.13.11.24 — fontes parcialmente lidas e inventário MSIX (TESTE)

Separada falha pontual de leitura de fonte inteiramente indisponível na auditoria de resíduos; painel mais claro e logs com contadores por origem. Testes Windows 327/327 e MSIX 24/24 aprovados; a execução real posterior no PC fraco mostrou erro MSIX 0x80040154 persistente após retry, com inventário parcial de 55 entradas. Nenhuma remoção habilitada.

## v5.13.11.23 — motivos de parcialidade explícitos (TESTE)

Registro sanitizado de falhas e limites na descoberta de resíduos; interface e inventário MSIX preservados. A leitura real MSIX da .23 enumerou 123 pacotes e 19 apps sem retry. Sem exclusão.


## v5.13.11.22 — inventário visível sem ações inseguras (TESTE)

- PC fraco confirmou a leitura MSIX: **123 pacotes**, **19 aplicativos**, **74 entradas** no total, falhas **0**. O retry novo não precisou atuar (`repeticao=False`), então não é possível concluir que ele resolverá uma futura falha persistente.
- Busca por `MSIX` parecia vazia porque a lista exibia somente aplicativos com atualização/desinstalação, e o filtro **Possíveis** estava ativo. Não era ausência de pacotes.
- Novo filtro **Inventário** na aba Aplicativos: mostra também Store/MSIX e jogos já identificados, sem exigir ação autorizada. Busca por origem `MSIX` revela esses aplicativos; dados sem ação têm identificação **Somente leitura** não clicável. A aba padrão **Todos** continua orientada a ações.
- Testes de interface para filtro adicional em resoluções variadas e teste de política confirmando que entradas `InventoryOnly` nunca são desinstaláveis só por aparecerem.
- Terceira bateria agregada: [PC fraco v5.13.11.21](docs/TESTE-PC-FRACO-2026-10-21-v5.13.11.21.md). Nenhum log bruto publicado.
- Permanecem análise parcial de resíduos, nenhuma exclusão autorizada, transmissão de diagnóstico remoto desabilitada e PR Draft. Roadmap **26/42**.


## v5.13.11.21 — proteção do inventário MSIX após terceira bateria no PC fraco (TESTE)

- As capturas reais confirmaram o conserto dos filtros por origem/classificação da v5.13.11.20, sem permitir exclusão de resíduos.
- Diagnosticado `COMException(0x80040154)` ao ativar a API WinRT de inventário MSIX. A leitura caiu de 19 aplicativos MSIX no teste anterior para zero; a lista geral mostrou 55 em vez de 74 entradas. **Não indica desinstalação de aplicativos.**
- MSIX tenta mais uma vez somente nesse HRESULT, sem PowerShell, processos auxiliares, elevação nem leitura de outros perfis. Se a fonte continuar indisponível, o GF anuncia **Inventário PARCIAL** de forma persistente no cabeçalho e no registro; não transforma a falha em consulta completa.
- Casos automáticos para recuperação, falha persistente, acesso negado, texto de interface e propagação da parcialidade para proteção de resíduos.
- [Terceira bateria anonimizada](docs/TESTE-PC-FRACO-2026-10-21-v5.13.11.20.md). Nenhum arquivo de diagnóstico bruto publicado.
- Limpeza de resíduos, instalação/desinstalação e manutenção de drivers não foram liberadas como decorrência desta alteração. Roadmap permanece 26/42.


## v5.13.11.20 — filtros por origem e classificação coerentes (TESTE)

- Segunda bateria real PC fraco 1440×900 identificou que o filtro por origem mostrava todos os locais do grupo, inclusive de outras fontes. Corrigida a interseção entre **origem, classificação e pesquisa** sobre os locais individuais, sem modificar a fotografia original nem permitir exclusões.
- Total de locais passa a indicar apenas locais correspondentes ao filtro; busca por conteúdo de um local não deve corresponder falsamente a outro local do mesmo grupo.
- Listas vazias distinguem "Nenhum candidato revisável" em uma análise parcial de "Nenhum grupo/local corresponde aos filtros", sem sugerir ausência total de resíduos.
- Casos de regressão adicionados ao motor e UI Windows para origem e classificação com dados comuns, perfil do usuário e Registro.
- [Relatório agregado do PC fraco v5.13.11.19](docs/TESTE-PC-FRACO-2026-10-21-v5.13.11.19.md). Excluídos os arquivos brutos e dados pessoais. Exclusão de resíduos e envio remoto continuam desativados.


## v5.13.11.19 — correções após testes reais no PC fraco (TESTE)

- Evidência real em monitor 1440×900 revelou agrupamento incorreto de subpastas genéricas como `Config`, `Logs`, `Updates`, `Scripts` e `Providers`; subpastas homônimas de fabricantes distintos podiam parecer um único aplicativo. A descoberta agora conserva esses locais associados ao nome do contêiner pai, sem os classificar como produtos independentes nem conceder autorização de exclusão.
- Abrange enumeração de subpastas em perfis/instalações e de subchaves de Registro; testes com dois fabricantes fictícios verificam que nenhuma pasta `Config` vira grupo próprio.
- Janela de investigação diferencia lista de grupos vazia de inspeção em nível sem itens; mantém o contexto e a proteção de dados.
- Métricas agregadas e observações estão no [relatório de teste real](docs/TESTE-PC-FRACO-2026-10-21-v5.13.11.18.md), sem anexar diagnósticos brutos ou histórico de IDs de hardware.
- Não foram habilitadas limpeza, atualizações, drivers, remoção de resíduos nem envio de dados. Nova bateria real deverá confirmar agrupamentos e desempenho; progresso funcional continua 26/42.


## v5.13.11.18 — confiabilidade da cobertura e próxima bateria no PC fraco (TESTE)

- Revisão independente da Work 3 detectou inconsistência de cobertura: relatórios de origem marcados como `Incomplete=true`, mas sem `Issues` agregados, podiam aparecer na investigação progressiva como análise normal. A fotografia agora atribui `ReportIncomplete` e informa **relatório de origem incompleto** sem inventar o motivo.
- Se uma origem tiver falha sinalizada apenas em `report.Coverage.Issues`, a consulta agora propaga essa flag para a cobertura geral **sem somar novamente as contagens**. Uma quantidade desconhecida permanece desconhecida.
- Listas de fontes consultadas/indisponíveis são copiadas ao construir a fotografia para que mutações posteriores do produtor não alterem um resultado já apresentado.
- Testes de resíduos cobrem origem parcialmente classificada, informação de falha presente só na linha, contagens não duplicadas, relatório sem cobertura opcional e mutação de listas. Teste WinForms verifica a advertência visível também em uma revisão sem locais.
- Nenhuma alteração no motor de atualização, exclusão de resíduos, desinstalação, drivers ou telemetria. Exclusão real de resíduos continua bloqueada; medições do PC fraco ainda não foram realizadas.
- Esta candidata **não** conclui as 16 etapas funcionais restantes do roadmap; progresso permanece 26/42.


## v5.13.11.17 — investigação progressiva e preparação do PC fraco (TESTE)

- Todos os grupos/locais classificados disponíveis, eliminando os cortes de 100 grupos/sete locais; descoberta continua limitada e explicitamente parcial quando necessário.
- Revisão com cartões virtualizados, busca global, filtros de classificação/origem, páginas de grupos/locais, detalhes e inspeção somente leitura sob demanda.
- Correlação por caminhos usados, aliases e fabricantes ambíguos; instalações a conferir e dados compartilhados permanecem preservados.
- Cache de uma consulta, cancelamento, limites de enumeração/concorrência, liberação de listas ao fechar e resultado pendente na bandeja sem janela inesperada.
- Métricas agregadas locais de duração/CPU/memória/alocações e benchmark reproduzível contra a .16; maior cobertura também tem custo de memória. Sem promessa de ganho no PC real.
- Revisão avançada preserva falhas/limites; decisão exige conferência de integrações e recusa reinstalação. Cinco entradas de exclusão continuam bloqueadas.
- 310/310 verificações de resíduos na consolidação anterior à troca de versão; investigação testada nas oito resoluções de 800×600 a 4K, além da matriz geral de 132 combinações. Falhas de espera de animação e descarte duplo foram corrigidas, sem reduzir asserções.

[Relatório completo](docs/MISSAO-WORK3-v5.13.11.17.md) · [benchmarks e limites](docs/BENCHMARKS-WORK3.md) · [checklist PC fraco](docs/CHECKLIST-PC-FRACO-v5.13.11.17.md) · [roteiro de histórico extenso](docs/TESTES-RESIDUOS-v5.13.11.17.md). Validações finais, commit de integração, ZIP e SHA-256 registrados na PR #40 após conferência. Pacote de testes sem assinatura; sem merge/release estável, envio remoto ou exclusão de resíduos. Roadmap funcional mantido em 26/42: hardware real e R4 continuam pendentes.

## v5.13.11.16 — integridade e segurança da análise de resíduos (TESTE)

- União deixa de descartar relatórios após os primeiros 500. Classificação contabiliza grupos/locais fora da exibição, cancelamentos, tempo, profundidade, falhas e evidências não avaliadas. Análise parcial permanece visível mesmo sem achados.
- Cobertura acompanha as fontes e se mantém correta ao unir resumos aninhados, sem duplicar contagens. Evidências, tipos e motivos são preservados; aliases da mesma visão do Registro reconciliados sem misturar arquiteturas.
- Preservação geral de perfis, preferências, ProgramData e diretórios compartilhados entre produtos; instalações portáteis, Netease e Driver Booster exigem conferência. Comparação indexada do inventário/processos e cancelamento cooperativo também na classificação.
- Prévia recusa conteúdo misturado, alvos duplicados ou alterados; copia os alvos aceitos e possui timeout na captura e conferência. Hash/identidade não concedem autorização para apagar.
- Cinco pontos internos de exclusão permanecem bloqueados e cobertos por testes, incluindo fixtures Windows de ACL negada, junction, hard links e mudança de arquivo. Não foram alteradas receitas de atualização oficial nem habilitado envio remoto.
- 38 workflows históricos reforçados: Actions em SHA oficial e checkout sem persistência de credenciais. Nomes únicos dos três checks atuais; documentação de proteção administrativa da main ainda pendente do proprietário.

[Relatório, limites e continuidade](docs/MISSAO-WORK2-v5.13.11.16.md) · [Roteiro nos PCs](docs/TESTES-RESIDUOS-v5.13.11.16.md). Resultados finais de CI, origem/commit e integridade do ZIP vinculados na PR #40; pacote experimental sem assinatura. Roadmap funcional permanece 26/42, com exclusão suspensa.

## v5.13.11.15 — descoberta, preservação e prévia de resíduos (TESTE)

- Suspensa exclusão de resíduos de executáveis, perfis e chaves do Registro: reenumeração ampliava o consentimento e não protegia conteúdo/identidade até a remoção. Bloqueio também nos métodos internos, sem opção oculta para reativar nesta build.
- Descoberta limitada a dois níveis para produto sob fabricante, HKCU/HKLM 32/64, pistas de Run/serviços Win32 e perfis históricos de VLC/foobar2000. Sem execução de comandos, leitura de outros perfis, tarefas/drivers ou varredura de discos.
- Decisão por item: revisar, preservar dados, preservar compartilhado ou estado desconhecido. União de evidências sem perder locais/motivos; proteção por MSI, identidade de inventário e caminho ainda utilizado.
- Prévia pós-desinstalação com identidade por handle, recusa de hard links e comparação SHA-256 dos executáveis reconhecidos; botão **Conferir novamente**, sem botão de apagar. Comparação não é prova de exclusividade nem rastreamento completo de subprocessos.
- Testes negativos adicionais, fontes limitadas/canceláveis, classificação com milhares de evidências e regressão da interface. Correção do validador histórico que rejeitava o nome curto VLC.
- Consentimento continua apenas local; sem envio remoto, merge na main, release estável ou nova funcionalidade de drivers. Desinstaladores oficiais continuam exigindo confirmação e têm sua própria política de dados.

Validação e limites: [missão de resíduos](docs/MISSAO-RESIDUOS-v5.13.11.15.md). Consultar os três workflows da revisão exata antes de usar o artefato; testes reais de desempenho, AVG, ACL/junctions e manutenção destrutiva continuam pendentes.


## v5.13.11.14 — resíduos WinMerge após teste real (TESTE)

- **Notepad++ 8.9.8.1:** o teste real confirmou integridade do instalador, pré-verificações, execução e versão instalada.
- **WinMerge x64:** a entrada de desinstalação desapareceu e o inventário caiu de 175 para 174 aplicativos; identificada uma pasta de instalação remanescente, sem remoção automática.
- Auditoria somente leitura agora reconhece `HKCU\Software\Thingamahoochie\WinMerge`, chave documentada pelo fabricante, e as visualizações HKLM da mesma subchave, quando acessíveis.
- **Resíduos de aplicativos** também pode consultar a família WinMerge historicamente, sem exigir reinstalação ou apagar preferências; protege variantes ainda instaladas. Nenhum achado significa autorização de exclusão.
- A lista de programas é atualizada antes do relatório de revisão após desinstalação. A mensagem diferencia **ausência do registro** de **término integral do desinstalador do fabricante**, pois a captura mostrou uma janela nativa ainda aberta.
- Testes verificam a identificação exata do WinMerge, fabricantes homônimos, o bloqueio com instalação ativa e a natureza não destrutiva do relatório histórico.
- As 11 famílias de perfis de fabricante/jogos e os candidatos Driver Booster/Netease continuam protegidos, só para revisão.
- [Relatório da simulação](docs/SIMULACAO-MANUTENCAO-WINMERGE-2026-10-20.md).


## v5.13.11.13 — pequenos ajustes de legibilidade após teste real (TESTE)

- Capturas da v5.13.11.12 confirmaram **três cartões por linha** na subaba Inicialização e o indicador persistente **Coleta local ativa** no cabeçalho, sem solicitar novamente consentimento já concedido.
- O botão da área de memória agora mostra **Abrir tarefas**, com nome acessível e dica completa **Abrir Gerenciador de Tarefas do Windows**, corrigindo corte do texto na janela proporcional.
- O filtro da lista DRIVERS agora mostra **Outros**, com explicação acessível de que inclui associações não presentes ou indeterminadas; nenhuma é automaticamente considerada obsoleta.
- A validação Windows passa a exigir os rótulos curtos e seus textos acessíveis. Sem mudança de execução de atualizações, drivers, limpeza, desinstalação, redes, energia ou telemetria.


## v5.13.11.12 — inicialização compacta e indicador de privacidade (TESTE)

- A aba **Inicialização** (dentro de Inicialização e sistema) distribui cartões em **três colunas quando a largura permite**, duas em janelas intermediárias e uma nas estreitas, sem alterar as grades de Windows Update e outras subabas.
- Cartões de inicialização mais baixos e compactos: títulos e descrições com dicas integrais/acessibilidade; confirmação e ações de ativação/desativação preservadas.
- **Consentimento local não reaparece automaticamente após trocar o ZIP**, porque o aceite/recusa anterior fica persistido fora da pasta do programa, evitando solicitação repetida. A janela principal agora mostra continuamente **Coleta local ativa**, **Coleta desativada** ou **Configurar privacidade**, com botão para revisar a escolha.
- O botão reflete imediatamente aceitação ou revogação; a coleta continua somente local, sem transmissão, arquivo enviado ou novo consentimento remoto.
- Testes de layout verificam transições 1/2/3 colunas, margens mínimas, estado persistente visível e acessibilidade.


## v5.13.11.11 — lista de drivers no mesmo padrão dos aplicativos (TESTE)

- Removida a tabela técnica com colunas da aba DRIVERS; exibida lista de cartões escuros com nome em destaque, informações complementares, ícone, seleção e rolagem escura, seguindo a aba Aplicativos.
- Cada cartão exibe versão, fornecedor, INF associado, assinatura informada pelo Windows e status de presença (presente, não presente ou indeterminado), sem confundir ausência com driver obsoleto.
- Campo de pesquisa por dispositivo, fabricante, versão ou INF; filtros Todos, Presentes e Não confirmados (inclui não presente e indeterminado), sem instalação, atualização ou remoção.
- Renderização virtualizada dos cartões: apenas linhas visíveis são desenhadas, evitando centenas de controles no PC de baixo desempenho.
- Mantidos captura WMI, correlação de pacotes, histórico local e registros de diagnóstico anteriores, com lista integralmente somente leitura.
- Testes Windows validam lista, filtros com 184 entradas fictícias e layout na matriz de resoluções; em 800×600, pesquisa e consulta permanecem visíveis, os filtros passam a um seletor compacto e o rodapé se reduz para preservar um cartão completo, sem perder o estado acessível.


## v5.13.11.10 — tabela de drivers responsiva (TESTE)

- Capturas reais da v5.13.11.8 mostraram a tabela de drivers utilizando somente parte da largura da aba e truncando os dados.
- As seis colunas agora preenchem proporcionalmente a largura disponível, com larguras mínimas para preservar a usabilidade e permitir rolagem horizontal em telas estreitas.
- Regressões de layout conferem a configuração da tabela na matriz de resoluções do projeto.
- Diagnóstico, limpeza, atualização, desinstalação e reparo **não tiveram seus mecanismos de execução alterados** nesta revisão.
- Observações de campo e limitações: [teste real de 20/10/2026](docs/TESTE-REAL-v5.13.11.8-2026-10-20.md).


## v5.13.11.9 — legibilidade do diagnóstico colaborativo (TESTE)

- Corrigidas as quebras de parágrafo do componente `DarkLogView.SetText` em Windows: converte novas linhas para CRLF sem duplicar `\r` e sem alterar o registro ao vivo.
- Janela de privacidade apresenta texto introdutório mais curto, fonte Segoe UI no relatório e explicações com acentos em português.
- Mensagem da simulação agora informa `SIMULAÇÃO CONCLUÍDA — NADA FOI ENVIADO`, evitando confundir simulação com aprovação de consentimento.
- Testes Windows exigem separação visual real dos parágrafos antes e depois do clique, além de isolamento da simulação, sem rede, arquivos ou autorização.
- Nenhum envio de diagnósticos foi ativado; recurso continua apenas local e voluntário.


## v5.13.11.8 — simulação de contribuição sem envio (TESTE)

- Janela de privacidade usa linguagem simples: mostra claramente quando a coleta local está ativa ou desativada, sem obrigar a interpretar JSON.
- Novo botão **Simular sem enviar**, sem exigir consentimento ou conta: cria uma demonstração fictícia estritamente em memória, validada pelo esquema de dados permitido.
- Simulação não lê informações reais do computador, não grava arquivo, não comunica com rede e não altera consentimento. Testes automatizados validam esses limites e a presença do controle na interface.
- A coleta continua apenas local e voluntária; envio remoto, autenticação de participantes, manutenção por IA e publicação automática permanecem desativados.
- Definidos critérios para uma futura contribuição simples, sem upload de arquivos e sem exigir GitHub do usuário final. O receptor de homologação é protegido por segredo exclusivo do servidor, desligado por padrão e testado apenas com tráfego fictício. Nenhuma transmissão foi ativada no GF. Veja docs/EXPERIENCIA-CONTRIBUICAO-SIMPLES.md e docs/INFRAESTRUTURA-ENVIO-SEGURO.md.


## v5.13.11.7 — bloqueio de envio manual e verificações adicionais (TESTE)

- Removida a publicação manual de diagnóstico para Issues públicas; GitHub Actions não aceita mais eventos de Issues como entrada. Coleta permanece estritamente local e consentida.
- Validador fechado de JSON técnico; nenhuma categoria, campo ou anexo arbitrário é admitido; proteção contra contagens ambíguas e timestamps futuros.
- Prototipo de recepção remota desativado por padrão e sem possibilidade de gravação nesta candidata.
- Atualização automática exige receita fixa com SHA-256 e assinatura; Visual C++ 2013 sem hash passa a oferecer apenas página oficial.
- Windows Update valida GUID e revisão selecionada antes da instalação; sem autorização de aceite de EULA ou reinício automático.
- Testes adicionais de privacidade, bloqueios de instalador e políticas de Actions; workflows legados remanescentes fixados por SHA.
- **Ainda é candidata de testes**: não promete ausência absoluta de riscos nem aprovação por AVG. Detalhes em docs/SEGURANCA-v5.13.11.7.md.

## v5.13.11.6 — caminhos protegidos e defesa adicional (TESTE)

- Ancestrais abertos da raiz ao destino, sem seguir reparse points nem compartilhar exclusao/renomeacao; recusa UNC, dispositivos e ADS.
- Arquivo e ancestrais de instaladores permanecem protegidos durante a sessao, mesmo apos retorno do processo inicial. Limite de 32 pacotes; temporarios retidos nao sao apagados durante a sessao.
- Pasta de trabalho do instalador no sistema; arquivos/pastas inesperados junto do pacote bloqueiam lancamento. Isso nao controla DLLs carregadas internamente por instaladores externos.
- Escrita de privacidade e downloads com diretórios presos; auditoria NuGet bloqueante e configuracao Dependabot semanal.
- Testes de renomeacao de ancestral, symlink de pasta, namespaces, retencao e arquivos vizinhos. Ver docs/SEGURANCA-v5.13.11.6.md.

## v5.13.11.5 — protecao de instaladores, pipe e arquivos locais (TESTE)

- Receita integral aprovada e comprovante em memoria vinculam o instalador ao download desta sessao.
- SHA-256 e assinatura sao revalidados antes da execucao; arquivo permanece sem compartilhamento de escrita/exclusao enquanto o processo iniciado e aguardado. Terminal recebe trava equivalente durante deployment.
- P/Invokes do GF usam somente a pasta de sistema para carregar DLLs nativas.
- Pipe administrativo verifica PID informado pelo Windows e limita espera da autenticacao a cinco segundos.
- Privacidade usa temporarios exclusivos com CreateNew; recusa pastas redirecionadas e limita leitura pelo arquivo aberto.
- Testes de adulteracao, receitas forjadas, PID incorreto e temporarios preexistentes. Limites documentados em docs/SEGURANCA-v5.13.11.5.md.


## v5.13.11.4 — auditoria de segurança, diagnóstico e consistência (TESTE)

- Comandos internos Windows e Gerenciador de Tarefas usam caminhos absolutos da pasta de sistema, com lista fechada; evita procurar executáveis homônimos no diretório portátil/PATH.
- Limpeza avançada só pode excluir pastas/chaves simultaneamente válidas na reconsulta e presentes na confirmação original.
- Diagnóstico expirado não pode ser compartilhado quando sua exclusão é bloqueada pelo Windows; sessão vazia preserva pendência anterior.
- Reaceitar coleta já ativa preserva contagens; consentimento inválido/ambíguo e versão com quebra de linha são recusados.
- Roadmap reconciliado: a checklist publicada contém 27/42 checkpoints (64,3%), diferente do resumo histórico 30/45. Evidências de monitoramento atualizadas para o painel RAM.
- Regressões para comandos, escopo autorizado, consentimento, retomada e expiração; relatório em docs/AUDITORIA-v5.13.11.4.md.


## v5.13.11.3 — retomada e revogação do diagnóstico (TESTE)

- Revisão no GitHub usa o resumo da sessão atual ou, sem eventos novos, o relatório local anterior ainda válido, preservando sua versão original.
- Pendências são revalidadas por tamanho, conteúdo permitido e prazo de sete dias também ao abrir a revisão.
- Revogar interrompe a coleta imediatamente nesta sessão mesmo quando o disco impede gravar a preferência; falhas de gravação/exclusão são informadas sem afirmar sucesso.
- Aviso antes de abrir o navegador explica que a URL já transmite o resumo ao GitHub; criar a Issue pública ainda exige confirmação no site.
- Regressões para retomada, prioridade da sessão atual, adulteração, expiração durante a execução e falhas de disco.
- Publicação continua manual no GitHub; testes reais/AVG e automação por IA continuam pendentes.


## v5.13.11.2 TESTE — piloto colaborativo GitHub-only
- Menu da bandeja permite revisar dados técnicos agregados e abrir um formulário de Issue no repositório público, com autenticação pelo próprio navegador do usuário.
- Dupla confirmação humana: aviso de publicidade no GF e envio final no GitHub. Nenhum token, conta vinculada automaticamente, upload ou transmissão invisível é adicionado.
- Filtro estrito de schema, versão, categorias e contagens antes de montar URL; recusa de dados arbitrários/relatórios vazios; testes offline dedicados.
- Mantém a coleta local/consentimento anterior. GitHub App e envio automático ao encerrar ainda não implementados.


## v5.13.11.1 TESTE — privacidade e diagnostico colaborativo local
- Consentimento explícito na primeira abertura, recusável e revogável pelo menu da bandeja; sem caixa pré-selecionada.
- Resumo estrito com versão do GF, encerramento e contagens de 14 categorias técnicas, sem logs brutos, nomes, caminhos, inventário ou IDs persistentes.
- Somente um relatório pendente local, substituído ao encerrar, com expiração de 7 dias e limpeza ao revogar; não utiliza a rede.
- Código de referência para recepção HTTPS/Cloudflare D1 com TTL, ainda **não implantado**. Transmissão automática futura dependerá de novo consentimento e revisão de privacidade.
- Testes próprios de opt-in, recusas, limites, sanitização, rotação e retenção.


## v5.13.11.0 CANDIDATA — novas fontes e consolidação para GitHub
- Quatro novas consultas oficiais somente leitura: ShareX, HandBrake, Rufus e qBittorrent. URLs fixas GitHub Releases; nomes e editores conferidos em catálogo interno antes da chamada. Nenhuma instalação automática habilitada pelas novas fontes.
- Regressões com fontes simuladas: candidatos, versões oficiais, falso editor, nomes parecidos, cache, pré-lançamento, falha de provedor e fusão sem duplicar linhas.
- Bateria abrangente Windows para comparar motores de versões, instaladores, drivers, resíduos, RAM, inventário MSIX e compatibilidade de interface.
- Projeto e manifesto atualizados para v5.13.11.0; README e identificação do pacote atualizados; nome do ZIP com versão e commit, BUILD-INFO e SHA256SUMS verificado em CI.
- Consolidado o desenvolvimento das séries v5.13.10.19 a v5.13.10.24: histórico de drivers, resíduos específicos, tema escuro, painel de RAM, otimização de caches próprios, instrumentação de bandeja/erros MSIX e validação automatizada.
- Estado: CANDIDATA. Validação em PC FRACO e segundo PC, assinaturas digitais e avaliação de alerta AVG ainda pendentes. Sem afirmação de segurança final, estabilidade pública ou cobertura universal.


Este arquivo resume as alterações mais recentes do projeto. As versões anteriores a 5.12 são etapas históricas do desenvolvimento e não representam versões publicadas no GitHub.

## 5.13.10.18 TESTE — consolidacao e inventario consultivo de drivers
- Diagnostico de desinstalacao considera ambas as alternativas registradas; caminho absoluto ausente nao herda confianca do nome msiexec.exe.
- Leitura incompleta do Registro bloqueia os fluxos que precisam confirmar ausencia antes da limpeza; auditoria contextual informa limitacoes do inventario.
- Confirmacao posterior exige a identidade original da instalacao; Sumatra confere tambem arquitetura PE e escopo. Mudanca de identidade permanece indeterminada, sem afirmar sucesso.
- Diagnostico de cobertura registra apenas contagens por motivo, sem nomes de aplicativos; nao presume equivalencia com WinGet.
- DRIVERS recebe tabela consultiva de associacoes dispositivo/INF/versao/fornecedor, presenca e assinatura informada pelo WMI, sob demanda e em segundo plano. Nao enumera todo Driver Store, nao instala, remove ou reverte drivers.
- Consulta de drivers tem limite de espera na interface e impede consultas sobrepostas enquanto WMI termina. Identificadores completos de dispositivos nao sao gravados no log.
- Consulta WMI real validada no runner; enumeracao COM corrigida apos falha reproduzida.
- Aba DRIVERS preserva altura da tabela e permite rolagem em janelas pequenas; captura 800x600 revisada.
- Nove grupos de regressao, 22 cenarios de layout e ativacao do pacote aprovados. Validacao nos computadores do usuario continua pendente.

## 5.13.10.17 TESTE — consolidacao antes dos testes nos computadores
- Verificacao de caminhos de desinstalacao movida para o mesmo trabalho em segundo plano do inventario, antes de entregar o snapshot a interface.
- Resumo acessivel e dica do contador distinguem inventario completo, itens com acao, sem acao, desinstalaveis e atualizacoes, sem duplicar itens com duas acoes.
- Confirmacao posterior de instalacao exige editor reconhecido e recusa entradas somente leitura; nome e versao isolados nao confirmam sucesso.
- Novos casos de regressao para contadores, fabricante divergente, arquitetura Visual C++, receita desconhecida e versao antiga.
- Roadmap de consolidacao com checkpoints verificaveis; validacao nos dois computadores permanece separada.
- Nao comprova resolucao do AVG, ganho de desempenho real ou cobertura universal. Correlacao de arquitetura/escopo na confirmacao posterior de todos os instaladores permanece pendente.

## 5.13.10.16 TESTE — PC FRACO e procedencia verificavel
- A amostragem do monitor usa 2 s somente na aba Processos, 12 s nas demais abas e 20 s ao minimizar para a bandeja; a coleta continua na bandeja.
- A primeira coleta de processos aguarda o intervalo do timer, sem competir imediatamente com o inventario inicial; ao abrir Processos ou restaurar a janela, atualiza sob demanda.
- Consulta de atualizacoes de aplicativos precede a consulta do Windows Update, com pausa curta; ambas continuam somente leitura e sem instalacao automatica.
- O artefato inclui hashes SHA-256 para o executavel e seus arquivos principais; facilita confirmar a integridade, mas nao substitui assinatura digital ou avaliacao do AVG.
- Nenhuma protecao e desativada, nenhuma exclusao antivirus criada e nenhuma deteccao e contornada. Ganhos de desempenho devem ser verificados no PC FRACO.

## 5.13.10.15 TESTE — monitor de processos semelhante ao Gerenciador de Tarefas
- Substitui cartoes de CPU por tabela de processos com CPU instantanea, RAM, PID e entrada/saida em KB/s; colunas ordenaveis e filtro por nome/PID.
- CPU de processos usa diferenca de TotalProcessorTime normalizada pela quantidade de processadores logicos; CPU total usa GetSystemTimes; RAM total usa GlobalMemoryStatusEx; trafego total de rede usa contadores de adaptadores ativos.
- GetProcessIoCounters informa bytes de leitura/escrita acumulados e suas taxas. Esse numero nao e disco fisico: nao inferir consumo de disco nem rede por processo.
- Primeira amostra e acesso negado exibem valor indisponivel em vez de inventar zero. Processos que terminam nao interrompem o monitor.
- Atualiza a cada 2 segundos somente quando a aba Processos esta visivel; em segundo plano e na bandeja passa para 5 segundos; nenhuma operacao pode encerrar processos.
- Tabela WinForms com pintura dupla e atualizacao incremental de celulas (sem apagar todas as linhas quando a ordem nao muda), com filtro preservado e scroll restaurado.
- Botao Tarefas do Windows abre Task Manager nativo para informacoes que nao sao medidas pelo Gerenciador Final.
- Ampliados testes de layout e testes de amostras CPU/E/S, memoria, PID, ordenacao, filtro, refresh sem remontagem e dados sinteticos. Preservadas regras de seguranca e residuos.
- Nenhum codigo ou elemento grafico proprietario do Task Manager foi copiado.

## 5.13.10.12 TESTE — descricoes da ultima aba e Atualizar lista na area correta
- A aba Inicializacao e sistema volta a exibir as quatro descricoes completas, em texto legivel abaixo dos botoes (Processos, Inicializacao, Windows Update e Este computador), com aviso acessivel e tooltip.
- Amplia a altura reservada aos comandos para evitar que suas descricoes fiquem ocultas por falta de espaco.
- Corrige seis verificacoes com indice de aba antigo no painel integrado: depois de incluir DRIVERS, o Sistema passou a ser a sexta aba (indice 5), evitando deixar de exibir resultados atualizados de Processos e Windows Update.
- A opcao Atualizar lista sai da faixa de pesquisa e passa a integrar os cinco comandos de Aplicativos, antes de Verificar atualizacoes.
- Em janelas estreitas, os cinco comandos sao distribuidos em duas linhas proporcionais (tres em cima e dois embaixo), sem abreviar nomes.
- O campo de pesquisa mantem apenas Pesquisar, filtro textual e Limpar; Atualizar lista permanece uma leitura local, enquanto Verificar atualizacoes realiza consulta online.
- Testes automatizados cobrem as descricoes de cada comando nas 22 resolucoes da matriz, visibilidade das legendas, posicao de Atualizar lista, distribuicao responsiva dos cinco comandos e ausencia de indices antigos da ultima aba.
- Nao modifica acoes de drivers, politicas de limpeza, inventario ou o tratamento conservador de residuos de GOG.com, McAfee e Microvirt (somente leitura).
- Versao ainda sujeita a avaliacao presencial no PC FRACO.

## 5.13.10.11 TESTE — sexta aba DRIVERS e navegacao uniforme
- Insere a aba DRIVERS imediatamente depois de Aplicativos; e uma area apenas visual com Inventario, Atualizacao/instalacao e Historico/reversao como funcionalidades futuras.
- Nenhum driver e consultado, instalado, removido ou alterado pela aba provisoria. Nenhuma funcao de manutencao existente foi modificada.
- As seis abas distribuem-se uniformemente em toda a largura disponivel, com margens laterais simetricas de ate 12 pixels e pequenas separacoes iguais entre botoes.
- Titulos completos sao desenhados em uma linha sempre que possivel; em janelas estreitas, usam quebra de linha natural para preservar o texto.
- Ajusta os indices internos de Limpeza, Reparo, Planos de energia e Sistema para evitar carregamento da pagina errada apos inserir DRIVERS.
- A pagina provisoria possui tres cartoes informativos, reorganizados em uma, duas ou tres colunas conforme a largura disponivel. Sem botoes que prometam funcionalidades inexistentes.
- Aumenta a bateria de testes de layout para 22 cenarios x 6 abas = 132 combinacoes, verificando margens simetricas, largura uniforme, hit-test de todas as abas e ausencia de acao na pagina DRIVERS.
- Testa 12 ciclos de bandeja sem recriar janela, 150 linhas de Registro em um lote e sete escalas simuladas de DPI alem dos testes funcionais e de seguranca ja existentes.
- Mantem o limite de ate 60% da area util como teto, podendo ocupar menos quando adequado. A aparencia final ainda requer conferencias no Windows real e no PC FRACO.

## 5.13.10.10 TESTE — densidade visual consistente em 1920x1080 e 2560x1440
- Preserva a janela proporcional e o teto de 60% da area util nas 21 resolucoes.
- Alinha a configuracao DPI do executavel e dos testes no modo PerMonitorV2; trocar de monitor ou escala 100/125/150% deve manter textos com escala nativa do Windows.
- Centraliza as cinco abas com o mesmo espacamento dos dois lados, incluindo a logica de acerto de clique e navegacao por teclado.
- Limpeza distribui oito ferramentas em no maximo quatro colunas, e Reparo distribui cinco ferramentas em no maximo tres colunas; os cartoes aproveitam melhor a largura sem esticar botoes.
- Discos locais podem usar tres colunas quando ha espaco; altura do painel de limpeza acompanha os discos detectados em vez de preencher o resto da janela com uma caixa vazia.
- Diagnostico de rede passa a ter altura baseada no conteudo, deixando a area sem informacoes no fundo neutro.
- Alteracoes de resolucao do Windows recalculam o tamanho proporcional da janela, com 170 ms de consolidacao de eventos para evitar redesenhos repetidos; o redimensionamento manual continua sujeito ao limite.
- Acrescenta testes de distribuicao de cartoes em Full HD e 1440p, preservando rolagem, bandeja e protecao de dados.
- Nenhum mecanismo de busca, desinstalacao, limpeza ou driver foi alterado nesta revisao visual.

## 5.13.10.9 TESTE — proporcao universal de ate 60% da area da tela
- Substitui a janela inicial fixa de 875x745 por dimensoes proporcionais a area util real do monitor, independentemente da resolucao (inclusive 800x600 e 8K).
- A abertura utiliza cerca de 57,4% da area util (70% da largura e 82% da altura); o limite de redimensionamento usa no maximo 59,76% (72% da largura e 83% da altura).
- O conteudo interno agora acompanha todo o espaco da janela, sem o teto de 875x745 escalonado por DPI que mantinha a interface pequena em 2560x1440.
- Mantem os tamanhos de fonte controlados pelo DPI do Windows: a janela cresce sem multiplicar artificialmente textos e botoes.
- Ao trocar de monitor ou DPI, limita novamente o tamanho e reposiciona a janela dentro da area util, sem salvar configuracoes no disco.
- Nao permite maximizar. Continua com minimizar para bandeja, menu Abrir/Sair e operacoes de leitura em andamento.
- Testes de 21 resolucoes + 1440x900 e de monitores 125% verificam limite de area, minimo de aproveitamento de 55% e centragem.
- A interface visual final continua sujeita a capturas e validacao nos computadores de laboratorio e PC FRACO.
- Nenhuma alteracao em limpeza, atualizacao, catalogo, instaladores, drivers ou registros historicos.

## 5.13.10.8 TESTE — pintura leve e Registro estavel para computadores antigos
- Remove WS_EX_COMPOSITED aplicado indiscriminadamente a toda a janela; o estilo pode gerar custo alto de repintura em GPUs integradas e maquinas sem placa dedicada.
- Usa buffering somente na janela principal, mantendo a estrutura nativa e a transicao oculta/visivel pela bandeja.
- Prepara o layout antes de Show() ao restaurar a bandeja, evitando exibir uma arvore suspensa que precisaria ser remontada.
- Na abertura, executa o inventario sem animar o progresso ou alternar estados de dezenas de controles; operacoes solicitadas pelo usuario continuam com progresso e bloqueios.
- Agrupa mensagens consecutivas do Registro em lotes de 85 ms, sem descartar linhas; a fila existe apenas em memoria.
- Recalcula barra do Registro so quando ha mudanca de metricas, limitando invalidacoes em cascata.
- Teste no Windows verifica composicao global desativada, 70 linhas preservadas em um unico lote, 21 resolucoes e 6 transicoes para a bandeja.
- Nao altera a classificacao conservadora de residuos, os limites de exclusao nem a deteccao de drivers. Os registros historicos permanecem somente leitura.
- A aprovacao depende de observacao no PC de laboratorio, alem de verificacao posterior no PC FRACO.

## 5.13.10.7 TESTE — pintura unica, botoes proporcionais e sinais de programa ativo
- A janela utiliza WS_EX_COMPOSITED para compor todos os controles antes de apresenta-los; a restauracao evita reorganizacoes intermediarias enquanto esta oculta.
- Nomes das cinco abas passam a ser desenhados em linha unica com tipografia ajustada somente quando necessario, sem reticencias.
- Reduz alturas de abas e dos cartoes de ferramentas, libera largura na pesquisa, evita esticar botoes de acoes ate preencher todo o espaco.
- Remove a quebra manual no nome Limpeza de Disco automatica; tipografia dos botoes se ajusta ao espaco, sem truncar o titulo.
- Os contadores da aba Aplicativos usam os nomes completos quando couberem, eliminando as abreviacoes 'Apps' e 'Atual.' na abertura normal.
- A revisao historica cruza tambem nomes de processos em execucao, inclusive programas portateis como MPC-HC, sem concluir remocao pela ausencia de processo.
- O reconhecimento adicional nao apaga dados e nao confunde executaveis inativos, saves ou pastas remanescentes com prova de desinstalacao.
- Testes verificam estilo nativo de composicao, seis restauracoes com HWND estavel, botoes sem quebras, matriz de resolucoes e verificacoes de residuos somente leitura.
- GOG.com, McAfee e Microvirt foram informados como desinstalados no computador de teste; MPC-HC permanece ativo segundo o usuario. A ferramenta continua exigindo evidencias locais e nao transfere esse estado individual para outros computadores.
- A avaliacao de fluidez real no computador permanece necessaria.

## 5.13.10.6 TESTE — controle de rolagem e informacoes sem cortes
- Corrige rolagem fantasma provocada pela margem tecnica de 3 pixels no componente de pagina.
- Os cartoes de limpeza, reparo e discos deixam de ter barras internas ao serem incorporados numa pagina; se o conteudo for maior, apenas a pagina rola.
- A altura do painel de discos e calculada conforme a quantidade de unidades, atualizada quando a leitura termina, sem comprimir cartoes.
- Reparo e energia ocupam menos altura e evitam a barra externa quando a pagina cabe na area visivel.
- Botoes de disco e margens do painel de limpeza foram reduzidos sem remover funcionalidades; os limites de seguranca permanecem os mesmos.
- Os quatro atalhos da aba Inicializacao e sistema usam legendas curtas que cabem; as explicacoes completas ficam em dicas de acessibilidade.
- Resumo de aplicativos mostra sempre quantidade e atualizacoes, sem cortar a legenda apos o primeiro numero.
- Testes automatizados em 21 resolucoes e na referencia 1440x900 checam se existe rolagem duplicada ou fantasma.
- Bandeja, ausencia de maximizar, limite de 60 por cento e diagnostico local permanecem inalterados.
- GOG.com, McAfee, Microvirt e MPC-HC permanecem classificados de modo conservador e so leitura, sem exclusao automatica.
- Aprovacao final depende da verificacao visual real, especialmente ao alternar de aba e revisar varias unidades de disco.

## 5.13.10.5 TESTE — bandeja fluida, mais informacao por tela e diagnostico
- Nao alternar ShowInTaskbar ao minimizar/restaurar: Hide e Show preservam o mesmo HWND e evitam repintura por recriacao.
- Suspende reorganizacoes de paginas e barras de rolagem durante transicoes de bandeja; amostragem de processos e consultas continuam ativas.
- Registra eventos de visibilidade e criacao/destruicao de janela no diagnostico ja existente, para diferenciar repaint de novo processo.
- Interface mais compacta: margens menores, cabecalho e comandos baixos, abas estreitas em duas linhas e log preservado.
- Linhas de aplicativos com 67 px, texto com quebra em vez de reticencias, dicas com identidade/versao integrais.
- Ferramentas com cartoes de 244x124 px e ajuste para 3 colunas quando houver largura; botoes aceitam multiplas linhas.
- Processos, inicializacao, atualizacoes do Windows e planos de energia usam cartoes menos altos e espacos internos menores.
- Reduz alturas minimas de paginas, conservando rolagem quando necessaria para nao esconder botoes.
- Testes de seis ciclos de minimizacao/restauracao sem mudar o HWND, alem da matriz de resolucoes e testes de seguranca anteriores.
- GOG.com, McAfee, Microvirt e MPC-HC permanecem historicos e somente leitura; nenhuma exclusao automaticamente autorizada.
- A ausencia de duplicacao de APP.INICIALIZACAO no log anterior nao confirma a causa do desaparecimento, motivo pelo qual foram adicionados marcadores de ciclo de janela.
- Validacao visual no PC real e testes de longos nomes/monitores continuam necessarios.

## 5.13.10.4 TESTE — janela discreta e bandeja do Windows
- Em monitores a partir de 1366x768 (area util correspondente), a janela fica abaixo de 60% da area util; resolucoes menores mantem excecao para usabilidade.
- Tamanho inicial compacto de 875x745 ou menor quando necessario, sem aumentar fontes ou botoes conforme a resolucao.
- Removida a funcao e o botao de maximizar. Barra superior escura personalizada, com somente minimizar e fechar, incluindo dicas e realce ao passar o mouse.
- Minimizar oculta o programa da area de trabalho/barra de tarefas e mostra o icone de bandeja sem interromper tarefas, consultas nem a coleta periodica de CPU/RAM.
- Menu de bandeja Abrir e Sair; duplo clique restaura. Fechar encerra o aplicativo, diferentemente de Minimizar.
- Quando Sair e solicitado durante operacao protegida, solicita cancelamento seguro quando disponivel e espera a operacao terminar antes de encerrar.
- Nenhum arquivo de configuracao ou residuo novo e persistido por esses controles.
- Testes de layout e bandeja automatizados no Windows; validacao visual real continua necessaria.

## 5.13.10.3 TESTE — janela inicial compacta como a referencia 1440x900
- Substitui o tamanho inicial de 1250x980 pela dimensao de referencia 875x745: em monitores grandes, a janela deixa espaco livre ao redor.
- Abre centralizada dentro da area util do monitor; em telas pequenas limita ao espaco disponivel, sem perder navegacao, rolamento ou o registro.
- Mantem fontes, botoes e proporcoes visuais do layout atual sem ampliar a interface com a resolucao fisica (4K, 5K, 8K).
- Mantem suporte a redimensionamento manual, troca de monitor e escalonamento DPI existente.
- Acrescenta validacao explicita da referencia 1440x900, alem das 21 resolucoes anteriores.
- Ajusta os testes de regressao que ainda exigiam indevidamente a abertura em dimensoes grandes.
- Esta e uma compilacao de teste; a avaliacao visual no Windows real continua necessaria.

## 5.13.10.2 TESTE — consistencia de interface e registro
- Corrige o registro excessivamente baixo e reserva espaco real para diagnosticos em qualquer altura util.
- Usa largura e altura preferidas limitadas em telas 4K/8K; nao dimensiona fontes ou botoes de acordo com a resolucao nominal.
- Quebra os nomes das abas em duas linhas quando necessario, em vez de truncar o nome das funcionalidades.
- Ajusta o resumo da aba Aplicativos ao espaco, preservando a informacao completa em uma dica acessivel.
- Botoes de acoes de aplicativos permitem duas linhas para nao ocultar seus titulos.
- Testes com 21 resolucoes e simulacoes geometricas para 100%, 125%, 150% e 200% de DPI.
- A auditoria historica permanece informativa e somente leitura. GOG.com, McAfee, Microvirt e MPC-HC nao sao candidatos a exclusao automatica.
- Continuam necessarios testes visuais no equipamento real, principalmente 800x600 e DPI alto.

## 5.13.10.1 TESTE — resolucoes, DPI e multiplos monitores
- Matriz de verificacao automatizada com as 21 resolucoes fornecidas: 16:9, ultrawide, 4:3 e 16:10, incluindo 800x600 e 8K.
- A janela inicial permanece inteiramente dentro da area util de cada monitor; o tamanho minimo deixa de impor 970x690 em monitores maiores.
- Reposicionamento e reducao da janela ao trocar para monitores menores, alterar resolucao ou mudar DPI, sem armazenar preferencias no disco.
- Cabecalho e registro cedem espaco para a area de trabalho em alturas reduzidas.
- Abas com largura adaptavel, altura escalada por DPI e dicas mostrando o titulo completo quando o texto precisa ser encurtado.
- Cartoes de ferramentas ajustam sua largura ao espaco disponivel, sem criar barra horizontal nativa.
- Testes de geometria, navegacao e foco nao executam manutencao do sistema; as resolucoes altas nao geram capturas gigantes.
- A aprovacao final depende dos testes de CI no Windows e de validacao visual em equipamentos reais, incluindo 125%, 150%, 200% e mais de um monitor.

## 5.13.3 TESTE — precisão no inventário de resíduos
- Filtra namespaces compartilhados do Registro e componentes do Windows que estavam aparecendo como resíduos.
- A auditoria geral só apresenta candidatos com evidências de dados de aplicativo cruzadas com outra fonte independente; um único caminho, atalho ou vista do Registro não basta.
- Mantém candidatos como não confirmados, sem qualquer exclusão automática, e preserva a varredura histórica específica.
- Testes com casos extraídos do diagnóstico v5.13.2, incluindo Netease, dotnet, Classes, Windows Defender e K-Lite.
- Esta versão ainda exige validação da CI e testes no Windows real antes de aprovação.

## 5.12.3 — Instalador e distribuição
- Preparação de instalador Inno Setup para Windows x64 com consentimento UAC.
- Aplicativo .NET autocontido: não exige SDK no computador do usuário final.
- Atalhos no Menu Iniciar e opção de atalho na área de trabalho.
- Integração do ícone GF e preparação para futura assinatura digital.
- Sem criação automática de exceções no antivírus ou regras de firewall.

## 5.12.2 — Reparo de rede e interface
- O botão Reparar rede depende dos resultados do diagnóstico e solicita confirmação antes de agir.
- A redefinição completa da rede permanece separada do reparo específico.
- Ajuste da largura do botão Atualizar discos.

## 5.12.1 — Diagnóstico de rede
- Diagnóstico consultivo de adaptador, gateway, DNS e conectividade TCP/443.
- Resultados mostrados na aba Reparo e rede e registrados no log.
- Cartão Criar ponto de restauração com confirmação.
- Remoção de atalhos redundantes na interface.

## 5.12 — Reparação do Windows
- Fluxo de reparo com DISM RestoreHealth e depois SFC Scannow, com confirmação e UAC.
- Registro dos resultados e códigos de saída das verificações.

## Histórico anterior
As versões 5.4 a 5.11 introduziram progressivamente a interface escura, listas de aplicativos, ferramentas de limpeza, diagnóstico e gerenciamento do sistema. A documentação atual e o código-fonte representam o estado funcional da versão 5.12.3.

> **Observação:** estas são notas de desenvolvimento; compilações e testes de compatibilidade devem ser feitos no Windows antes de publicar binários para usuários finais.
