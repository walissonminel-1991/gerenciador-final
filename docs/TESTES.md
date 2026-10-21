# Roteiro de testes — v5.13.11.34 TESTE

## Bateria de correção Jellyfin/Plex no PC FORTE (.34, CI aprovada)

Build oficial de testes [v5.13.11.34 (artefato GitHub Actions)](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39694660841/artifacts/12390097297).
Código aprovado: `9c411fba3177c111888d5fc7a62b4bd0b3211511`.
[Regressões Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39694660843)
(**130/130 motores multifontes**) e
[segurança/privacidade](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39694660928)
aprovadas. Uma revisão inicial da .34 falhou o teste canônico de Plex
e não deve ser testada; o pacote acima vem da revisão corrigida.
Confira BUILD-INFO e SHA256SUMS no artefato portátil, sem mesclar pastas.
Retenção GitHub indicada até 28/10/2026. Executável sem assinatura,
sem aprovação real AVG e sem elevação/WinGet real.

Capturas da .33 mostraram Jellyfin Media Player separado do Jellyfin
Server 12.1.0, Plex player 1.115.0 separado do Plex Media Server
1.43.4.10903 (x64), GameInput fora de Todos (somente inventário) e
divergências Proton 1.43.0 changelog/1.42.0 Windows. A .33 registrou
cancelamento de consulta em cerca de 0,6 s e encerramento normal.

Na .34, verificar **somente leitura**: Jellyfin Server reconhecido
com versão no nome, Plex Media Server reconhecido com sufixo (x64),
sem sugerir atualização a Jellyfin Media Player nem ao Plex player.
Versão mais nova não é prova de compatibilidade de instalador.
A UI deve mostrar número de candidatas e quantas exigem revisão.
Não alterar GameInput, Proton, MSIX, WinGet e políticas.
Preservar AVG e comparar com .33 após passar a CI, se for seguro.
PC FRACO adiado pelo usuário; não assumir testes nesse ambiente.

## Histórico das sessões anteriores

## Primeira bateria real .33 — PC FORTE (21/10/2026)

Evidências enviadas em sessão privada: seis capturas e diagnóstico local da .33;
**não** anexar arquivo pessoal bruto, nomes de pastas de usuário nem paths do teste
ao GitHub. Diagnóstico apresenta encerramento normal, sem erro fatal observado,
sem uso de executor WinGet COM/CLI ou instalação, sem alteração de drivers
e sem exclusão de resíduos. AVG não exibiu alerta nas capturas; aprovação
antivírus universal **não** comprovada.

**Estabilidade:** 174 aplicativos (140 com desinstalador/34 sem), 27 MSIX,
inventário completo; WinGet contexto `pacote=1 alias=1 path=0 elevado=0 parcial=0`.
Alias/PATH não comprovam capacidade da API nem exigência de administrador.
178 associações WMI/63 INFs, 3 associações sem INF válido, 176 PnP,
nenhuma diferença entre leituras. Resíduos 45 grupos/164 locais,
44 candidatos gerais, 212 grupos protegidos, 611 sem evidência,
3.925 entradas na descoberta geral, cobertura parcial por limite de entradas,
profundidade, links e leitura em AppData/ProgramData. Nenhuma remoção.

**Atualizações:** .32 tinha 3 candidatas; a .33 observou **6** em 174
instalações; 13/13 fontes HTTPS reconhecidas e OK, 631 entradas no RuckZuck,
103 sem correspondência de catálogo, 31 inventário apenas e 4 incertos.
Tempo da etapa de busca medido: 5.726s na abertura, 1.775s após
renovação (cache/fatores de rede não isolados). Oito fontes do projeto
antes, treze agora; não existe executor WinGet real.
Um resultado com **divergência entre fontes**; `IsUpdate` ainda conta
`UpdateRequiresReview`, logo a UI inclui o caso conflitante nas seis
candidatas; `VerifiedUpdaters.Resolve` bloqueia receita direta
e `GetPrimaryAction` apresenta somente Store/site/orientação.

**Códigos consultivos novos (somente números, definidos no código):**
- `fonte=1 etapa=9`, `fonte=2 etapa=9`, `fonte=4 etapa=9`:
  novas candidatas reconhecidas por fontes HTTPS, conservando ações
  de site/orientação e sem autorizar instalador genérico.
- `fonte=3 etapa=0`: identidade canônica não reconhecida no
  snapshot; **não comprova ausência do produto**.
- `fonte=5 etapa=2`: entrada reconhecida mas inelegível para
  atualização por proteção de inventário; não liberar automaticamente.
- `fonte=6 etapa=3`: condição de nome/editor/canal/identidade
  não satisfeita; causa específica ainda não comprovada sem
  verificação local das propriedades originais.
- `fontes=7/8/9 etapa=7`: conflito entre proveniências do mesmo
  produto; manter versão de feed Windows e outras observações
  discriminadas, sem instalação direta.

**Pendências objetivas:** desambiguar visualmente
`candidatas totais` de `candidatas que exigem revisão`; apresentar
na própria UI o motivo seguro de inelegibilidade sem divulgar dados
privados; conferir identidade exata do candidato fonte 3 e a
condição estrita fonte 6, sem enfraquecer proteções ou instalar
WinGet. Ainda não há teste físico da .33 no PC FRACO,
medição completa de AVG/CPU durante várias sessões nem
instalação verificada; U6 e demais checkpoints de validação
continuam abertos, roadmap **26/42 (61,9%)**.


## Próxima bateria .33 — somente consultas

1. PC FORTE: extrair pacote .33 validado em pasta nova e conferir BUILD-INFO/SHA256SUMS.
   Manter AVG ativo. Abrir normalmente, registrar startup/RAM/CPU/processos/arquivos novos.
   A versão não inclui executor WinGet e não pede UAC para pesquisar.
2. Escolher Teste PC FORTE em Limpeza e desempenho. Inspeção de contexto apenas de leitura:
   App Installer/alias/PATH do usuário atual; não executa WinGet nem instala pré-requisitos.
3. Verificar atualizações; observar barra por fontes concluídas, Cancelar consulta,
   falha isolada e estado parcial. Repetir; mudar lista; confirmar que não há flash ou reinício.
4. Apontar para cada candidata: conferir as versões/origens na dica. Proton prioriza feed Windows e pode mostrar
   divergência com changelog; FanControl aceita versão inteira; Jellyfin de versão principal diferente
   requer revisão. Novos produtos oferecem consulta/site/orientação, nunca instalador genérico.
5. Registrar somente contagens e códigos COBERTURA_ATUALIZACAO, especialmente fonte 5
   (GameInput); não compartilhar inventário bruto. Etapa 0 significa identidade não reconhecida,
   não ausência comprovada de instalação. Legenda completa em [MISSÃO WORK 5](MISSAO-WORK5.md).
6. Sem rede: inventário utilizável, fontes indisponíveis explícitas e nenhuma instalação.
   Fechar/reabrir, minimizar/restaurar e comparar fontes/RAM/CPU com baseline .32.
7. Se houver alerta, janela inesperada, UAC, dependência ou fechamento: parar, guardar evento
   e hash. Não criar exceções nem tentar reparar WinGet. A consulta real COM/CLI está bloqueada.
8. Somente depois, repetir consultas no PC FRACO, 1440×900; incluir inicialização/MSIX,
   DPI real, cancelamento e bandeja. Comparar com .24 sem presumir que ausências são remoções.

Não atualizar/desinstalar aplicativos, não limpar resíduos/Lixeira nem alterar drivers nesta bateria.
UI automatizada usa o runner; não substitui monitores/DPI/AVG reais. Zero checkpoints novos.

## Comparação presencial com WinGet — PC FORTE, 21/10/2026

Consulta **somente leitura** `winget upgrade` em PowerShell elevado mostrou
**9 atualizações candidatas**, enquanto o GF .32 mostrou **3** na mesma
máquina. Duas versões-alvo coincidiram; a terceira divergiu:
GF detectou uma revisão recém-anunciada pelo fornecedor, enquanto o
manifesto comunitário WinGet ainda mostrava a revisão anterior.
O changelog oficial recebeu a revisão mais nova em 21/10/2026 às
16h UTC, conforme commit
[`9ebebbc`](https://github.com/ProtonMail/WebClients/commit/9ebebbc88e8b3b9652d4b5bbfebfd8e2d3538966).
Isso valida a detecção por fonte oficial, **não** a disponibilidade
comprovada do instalador Windows nem a segurança de instalação.

Das **6 candidatas adicionais** do WinGet, cinco produtos não têm
receita correspondente nas fontes oficiais fixas atuais do GF;
um produto de sistema tem receita consultiva, mas a razão exata de
não se tornar candidato deve ser apurada com vínculo de
nome/editor/versão/eligibilidade e proteção de mudança de versão
principal. Não atribuir causa exata com base só em totais agregados.
RuckZuck e fontes fixas têm escopo menor e só aceitam correspondências
conservadoras de identidade. Sem execução de atualização, download,
alteração do Windows ou ampliação automática de privilégios.

A execução de `winget` no PowerShell não elevado falhou por comando não
encontrado, mas funcionou no terminal elevado. Possível divergência
de App Execution Alias/PATH/registro do App Installer por contexto; **não
é prova de que o WinGet exige administrador**. Não reinstalar
pré-requisitos apenas por este diagnóstico.

A Lixeira nativa continuava mostrando dois itens da pasta/arquivo
de teste da versão .32 após o retorno
`0x8000FFFF` observado em `SHEmptyRecycleBin`. Isto comprova que
a Lixeira **não ficou vazia**; não comprova se houve exclusão parcial
anterior. Não repetir esvaziamento, não apagar `$Recycle.Bin`
manualmente e não inspecionar conteúdo privado.

**U6:** comparação real foi obtida e os limites arquiteturais
ficaram explícitos. Permanecem pendentes a classificação individual
das seis diferenças e a verificação de origem/canal de instaladores.
Checkpoint continua aberto; roadmap **26/42**. O próximo passo é
melhorar a classificação consultiva e seus testes sem habilitar
instalações automáticas.


## Validação presencial .32 — PC FORTE, sessão 21/10/2026

**Aprovado nesta sessão:** 174 aplicativos/27 MSIX sem falhas, 140 entradas
com ação, três candidatas de atualização, oito fontes sem falha,
178 associações de drivers, 63 INFs mapeados, nenhuma alteração no
histórico de drivers (nas leituras sucessivas), 176 dispositivos PnP
consultados, encerramento normal e sem eventos de exclusão de drivers.

**Resíduos:** 45 grupos/164 locais e 44 candidatos gerais após 867 grupos
examinados. 212 protegidos, 611 sem evidência suficiente, 3.925 entradas
nas origens gerais, cobertura parcial de AppData e ProgramData e limites
de profundidade, entradas e redirecionamentos. A inspeção de dados
da Aquiris foi preservada; nenhuma exclusão de resíduos realizada.

**Auditoria de limpezas comprovada no PC FORTE:**
- `LIMPEZA.INICIO tipo=temporarios_usuario` e `LIMPEZA.RESUMO`
  com zero arquivos/pastas removidos, 30 itens preservados, zero links e falhas.
- `LIMPEZA.PROCESSO_RESULTADO tipo=temporarios_windows codigo=0`
  — resultado do worker sem contagem individual de arquivos.
- `LIMPEZA.PROCESSO_RESULTADO tipo=limpeza_disco_windows codigo=0`
  — não implica bytes recuperados.
- `RAM.CACHE_INTERNO modo=manual bytes_descartados=0`.
- Os eventos estruturados não contêm caminhos nem nomes de arquivos.

**Ocorrência aberta, não resolvida:** a interface mostrou
`O Windows retornou código -2147418113` após a chamada para esvaziar
a Lixeira, equivalente ao HRESULT `0x8000FFFF` (`E_UNEXPECTED`)
de `SHEmptyRecycleBin` no código atual. É operação separada
de `cleanmgr`. Não foi registrada no arquivo local de diagnóstico
da .32; resultado final da Lixeira não comprovado. **Não repetir
a exclusão nem apagar a pasta do sistema da Lixeira para investigar.**
Inspeção manual da Lixeira do Windows sem exclusão, seguida de
diagnóstico específico de estado e erro em versão futura.
Fonte oficial: documentação Microsoft de SHEmptyRecycleBinW/HRESULT.

**Continuidade:** confrontar 7-Zip/Outlook/Proton Pass com fontes oficiais
e WinGet (somente listagem), validar os três dispositivos sem nome
sem instalar drivers e preparar programa de teste não essencial para
atualização supervisionada, com backup e critério de confirmação.
Nenhum checkpoint adicional foi fechado: **26/42**.

## Build da .32 e CI

Artefato portátil não assinado [v5.13.11.32 TESTE](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39680829661/artifacts/12384491029),
código `ea8530d`. Confira BUILD-INFO e SHA256SUMS; retenção informada
até 28/10/2026. Aprovados: [compilação](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39680829661),
[regressões Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39680829530)
e [política de privacidade](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39680829538).
A primeira tentativa de compilação falhou por colisão de nomes locais,
corrigida na revisão aprovada. CI não substitui execução nos PCs reais.

## Resultados da segunda bateria PC FORTE (.31) e objetivo da .32

A .31 confirmou 174 programas, 27 MSIX, 3 possíveis atualizações,
178 associações WMI, 63/63 INFs consultados no Driver Store,
45 grupos e 164 locais de resíduos **parciais**. A UI informou
47 arquivos/pastas removidos dos temporários, 17 preservados e
zero falhas, e retornou código 0 de Limpeza de Disco; esses eventos
não constaram no diagnóstico persistente. A .32 acrescenta trilha
local agregada e estrita, sem mudar os critérios de limpeza.

Não repetir limpeza só para testar logging. A validação de formato fica
na CI e pode ser verificada quando houver outra limpeza genuinamente
necessária. Nunca enviar dumps da Steam, paths sensíveis ou arquivos
pessoais; Steam/dumps permanece somente leitura e protegido.
O filtro Todos do GF exibe aplicativos com ação; Inventário mostra
também entradas sem ação. Visualizar no Driver tab a distinção entre
presença indeterminada e assinatura informada pelo Windows.

## Histórico da bateria .31


21/10/2026 · PR #40 Draft · Roadmap **26/42 (61,9%)**.
Código `de6ee62` aprovado nas três Actions. [Pacote escolhido e hashes](OPERACAO-FAXINA-FINAL.md#candidata-aprovada-e-pacote-conferido). Este arquivo é
um procedimento pendente de campo, não um relatório de testes presenciais.
Resultados .30 do PC FORTE e matriz de achados: [revisão consolidada](OPERACAO-FAXINA-FINAL.md).
Testes anteriores permanecem no CHANGELOG, documentos históricos e histórico Git.

## Preparar os dois computadores

PC FORTE: 2560×1440, DPI real 120/125%. PC FRACO: 1440×900, escala a confirmar.
Começar no PC FORTE, refinar resíduos e repetir a bateria consultiva no PC FRACO.

1. Baixar o ZIP x64 da execução aprovada indicada no PR. Extrair a pasta inteira;
   runtime incluído, sem instalar SDK ou misturar arquivos de outras candidatas.
2. Conferir `BUILD-INFO.txt` e `SHA256SUMS.txt`, versão .31, SHA fonte e merge
   sintético. Registrar qual artefato foi usado. Hash não é assinatura digital.
3. Manter AVG/Defender/SmartScreen ativos. Se bloquearem, parar e registrar
   horário, etapa, código/nome da detecção e hash. Nenhuma exceção ou desativação.
4. Não executar otimização, limpeza, reparo, instalação, desinstalação ou ações em
   lote nesta primeira bateria. Nenhuma operação de driver faz parte desta build.
5. Ativar manualmente `Teste PC FORTE` ou `Teste PC FRACO` em Limpeza e desempenho,
   **antes de repetir** consultas. Padrão desligado, seleção só nesta sessão.

## Bateria A — somente leitura, nos dois PCs

| Ordem / checkpoints | Ação objetiva | Evidência mínima e critério |
| --- | --- | --- |
| 1 · I7 | Atualizar lista duas vezes na mesma sessão; pesquisar MSIX no filtro Inventário | `REGISTRO_PROGRAMAS.LIDO`, `MSIX.LEITURA`, `JOGOS.LEITURA`, `TESTES.INVENTARIO` e `TESTES.MSIX`; total, observados/retidos, falhas, fonte completa/parcial. PC FORTE .30 tinha 174/27 MSIX; variação legítima deve ser explicada, não forçada |
| 2 · I7/MSIX | Se 0x80040154 ocorrer naturalmente, atualizar lista uma vez; não reparar Windows | Falha permanece PARCIAL; após leitura completa anterior nesta sessão, entradas antigas ficam retidas/sem receita e com presença não confirmada. Sem base nesta sessão não haverá retenção inventada de 19 apps da execução passada. Recuperação completa remove a marca antiga e substitui a fotografia |
| 3 · U6 | Verificar atualizações; repetir; cancelar uma consulta e repetir | `TESTES.ATUALIZACOES`, `MULTIFONTES.COBERTURA`, tempos, contagem de fontes ok e rótulos cache/HTTPS no painel. Nenhuma instalação. A soma das categorias deve igualar o total avaliado; candidatos sem receita mantêm site/Store como alternativa |
| 4 · U6 | Se WinGet já existe, no mesmo PC/horário consultar `winget upgrade` sem outros argumentos; comparar com nova consulta GF | Somente listagem, sem `--all`, sem ID de pacote, sem instalar/reparar WinGet. Planilha manual privada por caso A/B: versões instalada/GF/WinGet, canal, arquitetura, escopo e editor conferido localmente. Explicar identidade ausente, catálogo sem entrada, versão incerta, preview/ESR, registro antigo e principal divergente; não tratar mais candidatos como mais segurança |
| 5 · R6 | Investigar resíduos uma vez e consultar Resumo | `RESIDUOS_GERAIS.CONSULTA_FIM`, `TESTES.RESIDUOS`, `TESTES.LIMITES`, `TESTES.FONTES`, `TESTES.ORIGEM`. Separar grupos gerais classificados, preservados, insuficientes e candidatos; grupos/locais reunidos são outro agregado. Flags/ACL/links devem permanecer PARCIAL |
| 6 · R6 | Conferir fabricante de jogo, save/perfil, cache, software portátil e exemplo homônimo, se aparecerem; usar Inspecionar local voluntariamente | Capturas recortadas da decisão, filtros/origens, paginação, motivo e foco; mascarar caminhos/IDs. Não abrir conteúdo nem apagar. Finalidade pelo nome não prova exclusividade; pasta sem ARP pode ser instalação ativa. Cancelar inspeção preserva fotografia anterior |
| 7 · D2/D4 | Consultar drivers duas vezes sem conectar/desconectar dispositivos | `TESTES.DRIVERS`, `DRIVERS.VINCULOS`, `TESTES.PACOTES`, `DRIVERS.HISTORICO`: WMI bruto/associações/IDs, PnP consultados, INFs/compartilhados, mapeados/indisponíveis/inconsistentes/não consultados. Nada é removível por estar ausente ou não mapeado |
| 8 · D4 | Conferir localmente rede, vídeo, storage e um INF compartilhado com o Windows, se disponíveis | Nome/fornecedor/versão/presença/INF e caminho Store em tooltip, apenas no PC. Registrar resultado agregado e divergências por caso, sem exportar IDs. `IsSigned` WMI não é validação criptográfica. Não tentar explicar 168→177 sem histórico real correlacionado |
| 9 · V5 | Abrir modal de resíduos com lista extensa e detalhes longos; buscar, filtrar origem/classificação, paginar e cancelar; Tab/Shift+Tab/Esc/fechar; testar bandeja | Capturas gerais recortadas nos DPI reais; controles visíveis, janela no monitor correto, rolagem dark, foco recuperado e fechamento sem travar. Modal diante de botões não significa controles cortados. `Otimizar` deve continuar em uma linha |
| 10 · P5/P6 | Três sessões da mesma build, condições comparáveis; 2 min em repouso e bandeja/restauração | `DESEMPENHO.LOCAL`: Startup, Inventory, Updates, ResidualDiscovery/Classification/Inspection; anotar tempo até UI, até inventário e até consultas concluídas separadamente; CPU/RAM em 30/60/120 s. Não atribuir rede ao desenho da UI nem comparar MB decimal com MiB |
| 11 · Privacidade | Desativar perfil e repetir uma consulta; alternar perfil não reinicia teto | Sem novas linhas de observação `TESTES.*`; linha MODO de desligamento somente se ainda houver orçamento. Diagnóstico básico existente permanece. Máximo total de 160 tentativas, incluindo modo/falhas; ausência de linha após teto não significa zero resultado. Nenhum envio/arquivo novo |
| 12 · I7/R6/P6 | Diagnóstico completo; fechar normalmente e reabrir | `TESTES.MANUTENCAO` e `APP.ENCERRAMENTO_NORMAL`. Conferir PARCIAL persistente por fonte; não transformar histórico PnP em lixo. Desaparecer um alerta numa sessão não conclui compatibilidade AVG |

As consultas não devem provocar novas instalações nem mudar permissões. Falha
ou cancelamento preserva dados e resultados anteriores; não insistir em loops.
Não provocar erro COM, remover componentes do Windows, criar ACL negada no perfil
real nem instalar antivírus para simular um caso.

### Campos novos para comparar a .31

`TESTES.MSIX`: `observados`, `retidos`, `falhas`, `parcial`.
`TESTES.PACOTES`: `pacotes`, `mapeados`, `indisponiveis`, `inconsistentes`,
`nao_consultados`, `pnp`, `wmi_brutos`, `parcial`. Vínculos confirmados não validam
história, compatibilidade, hash, catálogo ou assinatura de drivers.

`TESTES.ORIGEM`: código fixo, entradas e flags de limite de entradas,
profundidade, leitura, links, tempo, cancelamento e indisponibilidade. As contagens
representam observações debitadas no orçamento da descoberta, não arquivos únicos
ou espaço recuperável. Um mesmo local pode ser observado por fontes distintas.

| Código | Origem fixa |
| --- | --- |
| 1 | AppData do usuário atual |
| 2 | ProgramData |
| 3 | Program Files |
| 4 | Atalhos do menu Iniciar |
| 5 | Processos em execução |
| 6 | App Paths |
| 7 | Inicialização/serviços |
| 8 / 9 | Registro HKCU, visões 64 / 32 bits |
| 10 / 11 | Registro HKLM, visões 64 / 32 bits |
| 0 | Categoria não identificada; sem caminho no log |

## Entrega privada das evidências de campo

- Um `GerenciadorFinal-Diagnostico-v5.13.11.31.log` por PC/sessão relevante,
  identificado manualmente no texto da conversa. Não publicar log bruto no GitHub.
- Resolução, escala, Windows/arquitetura, versão/hash do artefato e ordem executada.
  O programa não infere PC FORTE/FRACO automaticamente.
- Capturas recortadas: inventário completo/parcial; resumo de resíduos e um
  exemplo preservado; tabela/estado de drivers; modal visível no DPI real.
  Mascarar usuário, caminhos, IDs, contas e itens pessoais antes de compartilhar.
- Para AVG: texto/código da detecção, instante e hash; somente captura recortada
  do aviso se necessário. Não enviar binário ativo para criar exceção.
- Comparação WinGet por casos equivalentes, no mesmo PC e em horário próximo;
  se não estiver disponível, marcar pendente sem instalar pré-requisitos.

I7, R7, U8, P5, V5 e D4 exigem os dois computadores. R6 exige resolver casos
históricos concretos; U6 exige explicar divergências reais de fontes. D2 ainda
exige histórico confiável do Windows e cobertura além das associações WMI.

## Bateria B — aplicativos, separada e supervisionada

Somente depois de A e do refinamento de resíduos: escolher **um** aplicativo não
essencial sem dados sensíveis, salvar backup e definir uma rota de recuperação
pelo instalador oficial verificado. Ponto de restauração não substitui backup.

1. Registrar versão/identidade/escopo/arquitetura e obter consentimento individual.
2. Testar uma atualização com receita validada; conferir hash/assinatura,
   código de saída, versão final e atualização automática da lista. Se depender
   de site/Store, usar a alternativa e validar a nova leitura; nenhum sucesso presumido.
3. Em etapa separada, testar o desinstalador oficial, observando as escolhas de
   preservação do próprio instalador. Reconsultar inventário e resíduos; não usar
   limpeza heurística externa nem liberar o botão de exclusão do GF.
4. Se houver falha, parar essa operação, registrar resultado e seguir a rota de
   recuperação prevista. Não testar lotes nem aplicativos essenciais do Windows.

Repetir somente os cenários autorizados no PC FRACO após a validação no PC FORTE.
Não houve execução remota destas operações nesta missão.

## Ambiente descartável e manutenção do Windows

Drivers G1–G5 continuam bloqueados. Instalação, remoção, rollback, export/backup e
recuperação de falha de drivers só poderão ser ensaiados em VM/sistema descartável
após desenho/validação de origem, compatibilidade e assinatura. Não usar
`pnputil /delete-driver` ou equivalente nos PCs pessoais. Saves, credenciais,
WinSxS, Driver Store, pacotes compartilhados e pontos de restauração são preservados.

Testes de instalador GF, atalhos e operações administrativas existentes (rede,
DISM/SFC, limpeza, energia/restauração) são outra bateria em máquina isolada,
após backup e consentimento específico. Não criar defeitos para acionar botões.
Assinatura Authenticode, atestação de procedência e SHA-256 são evidências distintas;
a candidata desta missão permanece experimental e sem assinatura.

