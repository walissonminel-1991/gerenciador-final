# WORK PRÉ-BETA — checkpoint de continuidade

Data da auditoria: **27/10/2026 (America/Sao_Paulo)**.
Missão: concluir as funções essenciais antes do BETA, executando **uma etapa por sessão**.

## Estado da sessão

| Campo | Registro |
| --- | --- |
| Etapa executada | **3 — Atualizações: identidade, cobertura consultiva e estados seguros** |
| Estado | **CONCLUÍDA TECNICAMENTE, COM HOMOLOGAÇÃO U8 PENDENTE** — CI aprovada no código 5a7c048; roadmap 27/42 |
| Repositório | `walissonminel-1991/gerenciador-final` |
| Branch de continuidade | `work-pre-beta-etapa0-20261027` |
| Base imutável | `2bc9f4228fd448445fbf181652e5af93005def20` — WORK18 / PR #60 |
| Fonte da candidata .65 já existente | `e6d92e2f2aed84edb6b23e9a8e6c064a6a62621d` |
| Commit da auditoria inicial | [`1399468d22061e79b6a6f957ba472d84a1aec9ec`](https://github.com/walissonminel-1991/gerenciador-final/commit/1399468d22061e79b6a6f957ba472d84a1aec9ec) |
| PR de acompanhamento | [#64 — Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/64) |
| Ponto de retomada anterior | `b74eb879b74360ed78e57b105463fb81fa5ffe83` — recibo da Etapa 2, CI 40525913386 aprovada no código 09fff12 |
| Commit da revisão Chrome anterior | [`8e195bfa3dfaab6767d877e78a2ad8d2b538f49c`](https://github.com/walissonminel-1991/gerenciador-final/commit/8e195bfa3dfaab6767d877e78a2ad8d2b538f49c) |
| CI histórica anterior à exclusão | [40514120889](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40514120889), tentativa 2: **failure**; não reexecutar |
| Commit da exclusão solicitada | [`341c109b6b8538733b32a02909ff40c8e0a380d9`](https://github.com/walissonminel-1991/gerenciador-final/commit/341c109b6b8538733b32a02909ff40c8e0a380d9) |
| Commit de código da Etapa 3 | [`5a7c0482a759009678b73c7c0cc75727b3b872f0`](https://github.com/walissonminel-1991/gerenciador-final/commit/5a7c0482a759009678b73c7c0cc75727b3b872f0) |
| CI da Etapa 3 | [40559631389](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40559631389): **success**, tentativa 1, seis jobs obrigatórios aprovados e dois condicionais skipped |
| Commit de código da Etapa 2 | [`09fff12b0f854ab9c23886146479a7f993f936b7`](https://github.com/walissonminel-1991/gerenciador-final/commit/09fff12b0f854ab9c23886146479a7f993f936b7) |
| CI da Etapa 2 | [40525913386](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40525913386): **success**, seis jobs obrigatórios aprovados e dois condicionais skipped |
| CI anterior sem o alvo excluído | [40519539258](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40519539258): **success**, seis jobs obrigatórios aprovados e dois condicionais skipped |
| Arquivos alterados nesta etapa | `Program.cs`, `InstalledUpdateIdentity.cs`, `UpdateResultHistory.cs`, testes de atualização/interface e documentos da Etapa 3 |
| Código, versão e roadmap oficial | Arquitetura local consultiva e preservação de resultados corrigidas; versão mantida; **27/42**, U8 e demais pendências abertos |
| Próxima etapa | **4 — Desinstalação / resíduos**, somente em outra sessão; Etapa 4 não iniciada |

O commit da auditoria inicial identifica a fotografia documental anterior; o
commit de código da Etapa 1 está identificado separadamente. O HEAD final do recibo deve ser
conferido na PR ao retomar. Não interpretar referências móveis como SHAs de
build ou como autorização de distribuição.

## Retomada obrigatória

1. Abrir esta branch e ler este arquivo e a [matriz](WORK-PRE-BETA-MATRIZ.md).
2. Conferir o HEAD remoto, a PR de acompanhamento, as PRs #60/#63/#61/#62,
   seus SHAs, estado e CI. Comparar com a fotografia abaixo.
3. Preservar qualquer alteração posterior; não sobrescrever a branch por
   força. Divergências exigem uma conferência específica, sem reiniciar a auditoria.
4. Executar **somente a primeira etapa pendente**, respeitando a exclusão permanente abaixo; não repetir revisões/testes concluídos sem nova evidência.
5. Registrar implementação, testes, limitações, SHA e próxima ação nos dois
   documentos; salvar no GitHub e encerrar a sessão.

**As Etapas 0/1/2 não foram repetidas.** A sessão atual trabalha somente na
Etapa 3. Os registros anteriores abaixo são históricos.

## Conferência de retomada com referência antiga — 27/10/2026

HEAD conferido: `6d6af1b2cb0354d5d04e09daf62ccd80b9ffd43d`, PR #64 aberta e Draft.
O código `341c109b6b8538733b32a02909ff40c8e0a380d9` continua com
a CI 40519539258 concluída e aprovada. Checkpoint e matriz confirmam as
Etapas 0/1 concluídas e a Etapa 2 ainda não iniciada.

O comando recebido fazia referência ao bloqueio anterior à exclusão
permanente. Essa pendência já foi retirada do escopo por instrução do
mantenedor; não foi reaberta e nenhum site foi consultado. Não houve
revisão Chrome, repetição de testes, alteração de código ou operação nos PCs.
Esta retomada registra somente a conferência documental e encerra antes da
Etapa 2, conforme o limite solicitado. O comando atual ao final deste arquivo
apontava a Etapa 2, agora executada na sessão registrada abaixo.

## Etapa 3 — sessão atual (27/10/2026)

Base `b74eb879b74360ed78e57b105463fb81fa5ffe83`, PR #64 aberta e Draft.
SHAs das PRs #60/#61/#62/#63 reconferidos e inalterados. CI da Etapa 2:
40525913386 aprovada no código 09fff12, seis jobs success e dois condicionais
skipped; b74eb87 era recibo somente documental.

O inventário captura arquitetura do PE principal Stable explicitamente
registrado para a consulta Chrome. Exige raiz do mesmo escopo, caminho seguro,
Machine/Magic coerentes, produto, empresa e versão correspondentes. Sem prova,
conserva unknown. Não infere bitness pelo SO/RegistryView; ARM64 não vira x64.
É leitura estrutural de metadados, não autenticação do editor nem receita.

O histórico conserva também uma primeira consulta parcial. Cancelamento
posterior preserva candidato em revisão/stale, sem ação direta. Troca de
instalação invalida o cache; consulta completa substitui a evidência. Falhas
não renovam 30 minutos; até 16 observações por aplicativo. O status da tela
passa a contar falha da fonte automática, evitando falsa consulta concluída.

Antes/depois: projeção original extraída sem mudar a lógica; 41 casos
comparáveis passaram de 36 aprovações/5 falhas a 41/41. Mais um controle de
memória: **42/42 focais**, 196/196 multifontes, builds do produto e interface
sem avisos/erros em Linux antes da interrupção. O ambiente local reiniciou;
as alterações foram recuperadas desta sessão sobre o mesmo SHA-base e serão
validadas novamente na CI Windows. A integração fonte fictícia → tela real
não foi executada em Linux. Nenhum endpoint Chrome foi reconsultado.

[Relatório e roteiro futuro](WORK-PRE-BETA-ETAPA3-ATUALIZACOES.md) ·
[Evidência](evidencias/pre-beta-etapa3-atualizacoes.json).
**CI [40559631389](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40559631389) aprovada** no código
`5a7c0482a759009678b73c7c0cc75727b3b872f0`, tentativa 1. Seis jobs
obrigatórios success, dois condicionais skipped.
[Recibo por job](evidencias/pre-beta-etapa3-ci-40559631389.json).

A cópia recuperada foi validada: **410/410 atualizadores (42 novos incluídos),
196/196 multifontes, 705/705 resíduos, 36/36 mutações rejeitadas, 47 MSIX,
30 encerramento, 41 Python/políticas**. Interface: oito combinações WORK18
e integração nova aprovadas; fixture com quatro entradas passou de **0 → 2
candidatos consultivos**, dois controles sem candidato, duas consultas
fictícias, zero rede real e zero receitas executáveis. Não são dois novos
aplicativos homologados nem atualizações físicas.

Drivers 2022/2025, integridade dos demais instaladores e pacote x64
self-contained aprovados. A CI conferiu 284 hashes; não houve conferência
independente do artefato ou homologação AVG nesta sessão. Nenhum código mudou
após a CI. O recibo posterior é apenas Markdown/JSON com `[skip ci]`; seu
HEAD deve ser conferido na PR #64 e não identifica um novo build.

**U8 permanece aberto, roadmap 27/42.** Etapa 4 não iniciada; exclusão
permanente, PCs, BETA e infraestrutura preservados, sem merge/deploy/release.

## Etapa 2 — registro da sessão concluída (27/10/2026)

Retomada a partir de `a4d30a1734fc54af35bec494e65653475970c40b`.
PR #64 aberta e Draft, base WORK18 inalterada. Conferidos os SHAs das PRs
#60/#61/#62/#63 e a CI 40519539258 aprovada; nenhuma dessas branches mudou.

Correções funcionais realizadas: registros contraditórios da mesma instância
não são reduzidos ao primeiro driver; HardwareIDs divergentes sobrevivem à
correlação WMI; identidade oficial nula e data ausente/sentinela são
inconclusivas; seções Device Install não compreendidas tornam o histórico
parcial; identidade vazia não conta como dispositivo associado a um INF.
O fallback de nome PnP usa o mesmo ID normalizado da presença.

Prova antes/depois: **26 verificações novas, 6 aprovadas/20 falhas antes,
26 aprovadas/zero falhas depois**, sobre os mesmos casos sintéticos.
Regressão local de drivers: **165 verificações aprovadas**; build Release
com analisadores: **zero avisos e erros**. Esta máquina é Linux; WMI,
SetupAPI, assinatura e WUA nativos ficam para a CI Windows descartável.
Nenhum driver foi baixado, instalado, removido ou revertido.

[Relatório e limites](WORK-PRE-BETA-ETAPA2-DRIVERS.md) ·
[Evidência comparativa](evidencias/pre-beta-etapa2-drivers.json).
Código aprovado: `09fff12b0f854ab9c23886146479a7f993f936b7`. [CI 40525913386](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40525913386)
**success**, tentativa 1: seis jobs obrigatórios aprovados e dois condicionais
skipped. [Recibo por job e runner](evidencias/pre-beta-etapa2-ci-40525913386.json).

Windows 2022/2025: **174 verificações aprovadas por runner**, incluindo as
26 novas. WMI: 67 associações em cada; Store: 46/48 INFs mapeados; INFs
publicados: 666/732, enumeração completa mas consulta de associações parcial.
Histórico: zero vínculos e leitura parcial em ambos. Assinatura: 24 INFs/
catálogos verificados por runner; INF sintético sem catálogo rejeitado. WUA:
consulta completa com zero ofertas/vínculos em ambos. Isso não fecha G1/G2.
Regressões Windows: 196 multifontes, 368 atualizadores, 705 resíduos,
36 mutações rejeitadas, 47 MSIX, 30 encerramento; 41 testes Python/políticas.
Pacote x64 self-contained aprovado, 284 hashes conferidos pela CI. Isso não
comprova homologação AVG nem autoriza distribuição. Nenhum código mudou após
essa CI. O recibo final é somente Markdown/JSON, com `[skip ci]`; conferir
seu próprio SHA no HEAD da PR #64. Não atribuir um novo build a esse recibo.

Reaproveitadas as evidências E1/E2: 166/119 associações, 63/53 INFs,
19/17 compartilhados e três associações sem INF em cada PC. O detalhamento
de pacotes do FORTE era parcial; o FRACO não tem prova equivalente completa.
Não foram solicitados logs repetidos nem acessados dados dos PCs.
**D2/D4/G1/G2 permanecem abertos, roadmap 27/42.** A Etapa 3 não foi iniciada.

## Diretriz permanente — exclusão expressa do mantenedor (27/10/2026)

> Remova o VLC de qualquer teste e verificação futura, incluindo adição de botões referente ao programa.

VLC está fora do escopo de testes, consultas de versões, verificação de sites,
downloads, homologação e ações do GF. Não tentar corrigir ou reconsultar seus
espelhos. Não propor novos botões, atualizadores, desinstaladores ou limpeza
para ele. O inventário pode conservar a entrada somente leitura, sem ações.

Foram removidos o caso do verificador de catálogo/download real, os casos
de teste específicos e as regras de identidade/limpeza/auditoria histórica.
A política de exclusão também impede associação operacional e ações por
MSIX/gerenciadores externos. Não foi criado um novo teste para esse produto.

**Esta instrução explícita substitui a pendência anterior de investigar VLC.**
A exclusão é uma mudança de escopo autorizada, não um afrouxamento de HTTPS,
hash ou assinatura para os demais aplicativos. Os runs antigos continuam
registrados como falhas históricas e não se tornam aprovados retroativamente.
Não reexecutar branches antigas que ainda contenham esse alvo. A integração
futura com a BETA deve carregar esta exclusão antes de disparar testes; a
BETA privada atual e sua infraestrutura não foram modificadas.

Validação local da alteração: compilação com analisadores e zero erros/avisos,
196/196 multifontes, 54/54 ecossistemas e 18/18 políticas. A CI Windows do SHA
`341c109b6b8538733b32a02909ff40c8e0a380d9` aprovou 196/196 multifontes,
368/368 atualizadores, 705/705 resíduos/desinstalação, 36/36 mutações,
47/47 MSIX, 30/30 encerramento, integridade dos outros instaladores,
políticas, drivers dos runners 2022/2025 e empacotamento x64.
Dois casos específicos foram removidos, explicando 707 → 705; não são falhas
ignoradas. Nenhum teste novo para o produto excluído foi criado.
[Evidência final do escopo e da CI](evidencias/pre-beta-etapa1-escopo-ci-40519539258.json).

**Etapa 1 concluída tecnicamente.** Roadmap 27/42 e homologação física
continuam como registrados. A arquitetura Chrome na tela permanece pendente
da Etapa 3. Naquela sessão a Etapa 2 não foi iniciada. Não houve merge, deploy,
release pública, distribuição BETA ou operação nos PCs.

## Histórico da Etapa 1 — revisão Chrome e falha externa anterior à exclusão

As PRs #60/#61/#62/#63/#64 e seus SHAs foram reconferidos e permaneceram
iguais aos registrados na Etapa 0. A PR #63 continua Draft; seus cinco
arquivos foram revistos e incorporados na branch de continuidade como
alterações de trabalho, sem merge e sem modificar sua branch de origem.

- **Correção demonstrada:** JSON duplicado/ambíguo e versões com LF final
  ou dígitos não ASCII eram aceitos. As 37 verificações focais reproduziram
  sete falhas antes da correção; depois, 37/37 passaram.
- **Regressão local:** suíte multifontes completa 196/196, SDK 10.0.402
  em Linux. Política estática: 18/18 testes, 248 fontes examinadas.
- **Windows no SHA exato:** 196/196 multifontes, 368/368 atualizadores,
  707/707 resíduos/desinstalação, 36/36 mutações recusadas, 47/47 MSIX e
  30/30 encerramento aprovados. Políticas e drivers 2022/2025 aprovados.
- **Bloqueio de CI integral:** na tentativa 1, o job de integridade recusou
  redirect VLC para HTTP antes de segui-lo. Foi solicitada uma única repetição
  desse job; na tentativa 2, outro espelho HTTPS retornou 403. Nenhuma regra
  foi relaxada. O gate impediu o empacotamento x64, que ficou skipped.
  [Evidência por tentativa e job](evidencias/pre-beta-etapa1-ci-40514120889.json).
- **API oficial:** win/win64 responderam HTTP 200 sem redirects; paginação
  real aceita sem seguir token. O C# real também validou os dois endpoints.
- **Ganho do componente:** mesmo probe de sete entradas, 1 → 3 candidatos
  consultivos; Chrome 0 → 2 arquiteturas, controles inelegíveis 0 → 0.
  Nenhum instalador executado, nenhum ganho físico alegado.
- **Limitação descoberta:** o adaptador `LoadUpdatesAsync` obtém arquitetura
  pelos sufixos do nome, incompatíveis com a identidade exata `Google Chrome`.
  Na tela atual, esse nome chega como `unknown` e não consulta a nova fonte.
  Fornecer arquitetura local comprovada é pendência explícita da Etapa 3;
  não inferir arquitetura pelo SO nem relaxar o gate.
- **Fontes adicionais:** não foram acrescentados outros motores; o problema
  de entrada acima deve ser resolvido antes de prometer ganho de cobertura.

Arquivos: `CatalogoIdentidades.json`, `OfficialReleaseSources.cs`,
`TestesMultifontes/ChromeVersionHistoryTests.cs`,
`TestesMultifontes/ChromeVersionHistoryReviewTests.cs`, `TestesMultifontes/Program.cs`,
`scripts/measure_pre_beta_sources.py`,
`docs/EXPERIMENTO-CHROME-VERSIONHISTORY-20261027.md`,
`docs/evidencias/pre-beta-etapa1-fontes-comparacao.json`,
`docs/evidencias/pre-beta-etapa1-api.json`,
`docs/evidencias/pre-beta-etapa1-ci-40514120889.json` e os dois documentos
de continuidade. Detalhes: [revisão e limites](EXPERIMENTO-CHROME-VERSIONHISTORY-20261027.md).

Roadmap oficial, U8 e os demais checkpoints permanecem abertos conforme a
fotografia inicial. Infraestrutura privada, main, PR #40 e dados dos PCs
preservados. Nenhuma nova investigação WinGet COM.

### Estado histórico anterior à instrução do mantenedor

A revisão do componente Chrome foi concluída e seus testes passaram nos
dois ambientes. O run acima falhou antes da exclusão solicitada pelo usuário;
é evidência histórica. Não retomar a investigação desse produto nem usar
seus sites como requisito da missão. A aprovação do código atual deve vir
da CI da revisão sem esse alvo, mantendo os controles dos demais aplicativos.

## Fotografia inicial verificada do GitHub — Etapa 0

Foram listadas **29 PRs abertas**, todas Draft, no início da auditoria.
As PRs relevantes abaixo tiveram seus HEADs e execuções de CI consultados.
Nenhuma PR existente, branch de referência ou configuração remota foi alterada.

| Referência | Branch / HEAD exato | CI do HEAD consultada |
| --- | --- | --- |
| `main` | `410d65e23546976db456337c71e4705f10217d36` | Preservada; sem nova execução solicitada |
| [#40](https://github.com/walissonminel-1991/gerenciador-final/pull/40) | `piloto-diagnostico-github-v5.13.11.2` — `ef13aef49083f357f6a14f4de46f8bc976dbd830` | [40142017415](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40142017415): success |
| [#54 / Lote 1](https://github.com/walissonminel-1991/gerenciador-final/pull/54) | `work-lote1-confiabilidade-relatorios` — `7405d83f308a141d2a7f6c68cc5a5dee4a19067c` | [40214100769](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40214100769): success |
| [#55 / Lote 2](https://github.com/walissonminel-1991/gerenciador-final/pull/55) | `work-lote2-drivers-leitura` — `6a020c86ba5024b780334217bb7f38b18b23bb34` | [40219971723](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40219971723): success |
| [#56 / Lote 3](https://github.com/walissonminel-1991/gerenciador-final/pull/56) | `work-lote3-seguranca-desempenho` — `8f8d1c8fb109ba4956876b398f02dee6ce70f2e3` | [40231535067](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40231535067): success |
| [#57 / Lote 4](https://github.com/walissonminel-1991/gerenciador-final/pull/57) | `work-lote4-consolidacao` — `cb7ca5ccd986fd11c768ee410ac7c2b46296e1c1` | [40235071337](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40235071337): success |
| [#59 / integração .64](https://github.com/walissonminel-1991/gerenciador-final/pull/59) | `integracao-lotes1-4-work14-17-20261025` — `28beb9607e85d208af4c0864d768a6db7fb5296a` | [40242837652](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40242837652): success |
| [#60 / WORK18 .65](https://github.com/walissonminel-1991/gerenciador-final/pull/60) | `work18-ampliacao-atualizacoes-seguras` — `2bc9f4228fd448445fbf181652e5af93005def20` | [40253446761](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40253446761): success |
| [#63 / Chrome](https://github.com/walissonminel-1991/gerenciador-final/pull/63) | `experimento-chrome-versionhistory-20261027` — `04f59750739c6bdbaeb0bacd86ed2b734fba1271` | [40507858102](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40507858102): success; 6 jobs aprovados, 2 condicionais skipped |
| [#61 / BETA .66](https://github.com/walissonminel-1991/gerenciador-final/pull/61) | `beta-piloto-consentimento-envio-20261026` — `f64600748b45b0ddc7e39c530ac56809c26f7ea8` | [40324802334](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40324802334): success |
| [#62 / BETA endurecida](https://github.com/walissonminel-1991/gerenciador-final/pull/62) | `work-beta-retencao-issues-20261026` — `e530d724b7322963c8795b005f13ae6948a73e43` | Principal [40409850849](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40409850849): success; externa [40409850628](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40409850628), tentativa 2: **failure** |

As PRs #47/#49/#50/#51/#52/#53 permanecem abertas e Draft. A integração #59
já registra a consolidação dos Lotes 1–4 com WORK14–17. Não reaplicar todas
essas branches sobre a base .65: isso pode duplicar alterações já incorporadas.
As PRs legadas #25–#35 e #38 não foram fechadas nem reorganizadas nesta missão.

O build .65 selecionado anteriormente usa a fonte `e6d92e2f`, não o HEAD
documental `2bc9f422`; [CI de produção 40252100887](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40252100887).
Essa distinção vale para relacionar os logs físicos ao código distribuído.

## Base escolhida e dependências

A branch desta auditoria parte da WORK18 .65, sem importar a PR #63 nem a
BETA .66. Ela já contém a integração de drivers, atualizações, desinstalação,
resíduos e confiabilidade necessária às próximas etapas.

- A PR #63 também parte de `2bc9f422`. Seu delta tem **5 arquivos**:
  `CatalogoIdentidades.json`, `OfficialReleaseSources.cs`,
  `TestesMultifontes/ChromeVersionHistoryTests.cs`, `TestesMultifontes/Program.cs`
  e `docs/EXPERIMENTO-CHROME-VERSIONHISTORY-20261027.md`.
- A BETA endurecida difere da base em **62 arquivos**. Eles incluem
  `Program.cs`, projeto/manifesto, `InstallerFileGuard.cs`, UI, workflows,
  políticas de pacote, roadmap e progresso. Mudanças futuras nessas áreas
  exigem reconciliação antes da Etapa 7; quantidade de arquivos não comprova conflito.
- Os conjuntos de arquivos alterados pela PR #63 e pela BETA #62, medidos
  contra a base .65, não se sobrepõem nesta fotografia. Isso não prova
  compatibilidade comportamental nem autoriza integração automática.
- Os dois arquivos de continuidade estavam ausentes na base .65, em `main`,
  no experimento #63 e na BETA endurecida. Não havia checkpoint anterior para repetir.

## Implementação existente e evidências

O inventário inicial detalhado está na [matriz](WORK-PRE-BETA-MATRIZ.md).
Foram examinados roadmap, progresso, TESTES, relatórios WORK10–18, Lotes 1–4,
integração, auditorias WORK16/17, handoff, workflows e os pontos de código
necessários à identificação da cobertura. A revisão profunda de cada motor
continua reservada à etapa correspondente.

Já existem leitura WMI/HardwareID, correlação INF/Store/SetupAPI, consulta
WUA somente leitura, verificação criptográfica de INF; descoberta multifontes
e catálogo Microsoft autenticado; contratos MSI/WiX/NSIS/Inno; desinstalação
Squirrel condicionada a evidências; limpeza integrada com SIM/NÃO e proteção
de dados. Essas implementações não precisam ser recriadas do zero.

WinGet COM permanece apenas fonte consultiva, opcional e não bloqueante.
O log .65 registra `com_activation_failed`; não há nova evidência que justifique
reabrir sua investigação. O catálogo Microsoft por HTTPS é um mecanismo
independente; não confundir sua consulta com execução de COM/CLI.

## Testes e validações preservados — Etapa 0

- Contagem independente do JSON e das marcações do roadmap: **27/42**,
  com os mesmos **15 IDs pendentes**; sem promoção de checkpoints.
- **10 HEADs de PR relevantes** tiveram suas execuções consultadas;
  a falha externa da #62 foi registrada separadamente da CI principal.
- Na #63, o run consultado corresponde exatamente a `04f59750739c6bdbaeb0bacd86ed2b734fba1271`.
  A aprovação não comprova resposta real do endpoint Google nem ganho físico.
- Inventário de testes existente: **13 projetos de suítes**, **2 projetos de
  fixtures** e o projeto do produto. Não foram recriados testes existentes.
- Evidência anterior de produção .65: 145 verificações WORK18, 368 de
  atualização, 707 de resíduos/desinstalação, 159 multifontes, 17 catálogo,
  47 MSIX, 30 encerramento, 54 ecossistemas e 36 mutações recusadas.
  Esses totais pertencem à CI anterior, não a uma nova execução nesta sessão;
  não somar subconjuntos como verificações independentes.
- **5 arquivos físicos anteriores recuperados e lidos**: os diagnósticos
  .64/.65, o relatório local e dois históricos .65. Somente agregados e
  referências de linhas constam da matriz pública; nenhum log bruto foi anexado.
- Validação documental: escopo restrito aos dois arquivos, referências
  relativas existentes, tabelas/estados e consistência com 27/42.
- **0 novos testes de produto e 0 operações físicas executadas.** Os commits
  documentais usam `[skip ci]` para não repetir a bateria já concluída.
  Isso não contorna um teste de código: não há alteração de código ou workflow.

## Falhas, limitações e riscos abertos

1. **BETA #62 sem CI integralmente verde.** A falha externa é atribuída na PR
   à recusa de redirect HTTPS→HTTP do VLC. O estado failure foi confirmado
   na API; a causa não foi reinvestigada nem a proteção relaxada nesta sessão.
2. **Histórico dos drivers ainda parcial.** A assinatura do INF não verifica
   todo o payload. Zero ofertas WUA em runner não valida oferta positiva.
3. **Cobertura descoberta não é execução homologada.** Nos logs .64 e .65,
   zero atualizações silenciosas e zero supervisionadas elegíveis; não usar
   os 10/8 candidatos como número de atualizações executáveis.
4. **Comparação física com versões diferentes.** Hardware, Windows, versão
   do GF e rede diferem; não atribuir diferenças de tempo ao código.
5. **Metadados antigos em PROGRESSO.** `latest_real_test` ainda aponta .52
   e `latest_real_test_pc_fraco` .24; os arquivos recuperados comprovam sessões
   posteriores .64/.65. Não usar esses campos antigos para solicitar nova bateria
   completa. A atualização oficial será avaliada na Etapa 6, preservando histórico.
6. **Memória/CPU:** o relato de cerca de 60 MB em repouso não é medição
   contínua. Os logs mostram amostras de working set maiores durante consultas;
   CPU acumulada de uma etapa não é percentual em repouso.
7. **AVG:** relato de sessão sem alerta não resolve a causa do incidente nem
   homologa novas builds. P6 permanece aberto.
8. **Inicialização com Windows:** T11 do PC FRACO não foi executado segundo
   o relato fornecido. Ativar a opção ou ocultar/restaurar na bandeja não prova boot.
9. Não foram auditados valores remotos de Worker/D1/DNS/flags. Nenhuma
   afirmação de estado operacional desses serviços é feita com base no código.

## Sequência e critérios de continuidade

| Etapa | Estado após esta sessão | Próxima ação e critério de saída |
| --- | --- | --- |
| 0 — Auditoria | **CONCLUÍDA** | Referências, pendências, evidências e continuidade registradas; encerrar |
| 1 — Fontes / #63 | **CONCLUÍDA** | Revisão e exclusão solicitada aprovadas na CI 40519539258; nunca retomar verificações VLC; arquitetura na tela permanece para a Etapa 3 |
| 2 — Drivers consultivos | **CONCLUÍDA TECNICAMENTE / HOMOLOGAÇÃO PENDENTE** | CI 40525913386 aprovada; 26 casos novos, 174 verificações por runner. D2/D4/G1/G2 continuam abertos; nenhuma escrita |
| 3 — Atualizações | **CONCLUÍDA TECNICAMENTE / U8 PENDENTE** | CI 40559631389 aprovada; 42 novas, 410 atualizadores, integração consultiva 0→2; nenhum ganho físico ou receita nova; U8 aberto |
| 4 — Desinstalação / resíduos | **NÃO INICIADA** | Reusar WORK10–14/Lote 3; demonstrar propriedade, SIM/NÃO e falhas em alvos descartáveis; R4/R6/R7 reais continuam dependentes de evidências |
| 5 — Gestão de drivers | **NÃO INICIADA** | Estudo/protótipo isolado desativado, somente se viável; G3–G5 não se concluem por planejamento |
| 6 — Evidência física | **NÃO INICIADA** | Partir dos 5 arquivos já recuperados e dos relatos; avaliar cada checkpoint e pedir somente a prova realmente ausente |
| 7 — Integração / decisão | **NÃO INICIADA** | Reconciliar frentes e CI, avaliar critérios e recomendar estado técnico; sem publicar |

As etapas 4–5 usam a base técnica verificada e preservam alterações aceitas
nas sessões anteriores. A Etapa 6 pode reutilizar toda a prova já recuperada;
não depende de criar dados novos por instalação/remoção em PC pessoal.
A Etapa 7 depende dos resultados e bloqueios registrados nas anteriores.

## Limites obrigatórios para todas as próximas sessões

Não alterar, publicar ou ativar Worker `gerenciador-final-beta-private`, D1
`gf-beta-private`, flags `PILOT_ENABLED = no` / `GITHUB_EXPORT_ENABLED = no`,
URL pública desativada, domínio `gerenciador-final.eu.org` ou DNS/deSEC/EU.org.
Não executar deploy, merge em `main`, release pública, coleta ou distribuição BETA.

Sem desativar proteções, reparar WinGet, elevar automaticamente, mudar ACLs,
executar comandos de catálogos ou instalar/remover/reverter drivers. Sem
atualização real, desinstalação importante ou exclusão residual em PCs pessoais.
Usar fixtures e ambientes descartáveis; preservar documentos, credenciais,
perfis, configurações, jogos, saves e dados compartilhados.

## Próxima ação exata e comando para retomada

Em outra sessão, iniciar somente a **Etapa 4**, após conferir este checkpoint,
a matriz, HEADs e o recibo/CI da Etapa 3. Etapas 0/1/2/3 tecnicamente concluídas.
Não repetir revisões/testes sem nova evidência. Os checkpoints físicos e de
pacote continuam abertos; U8 não foi encerrado.
A exclusão permanente continua obrigatória. Não trabalhar na BETA privada.

> Continue a missão pré-BETA do Gerenciador Final pela Etapa 4. Leia o checkpoint e a matriz na branch work-pre-beta-etapa0-20261027, confira o recibo e a CI da Etapa 3, preserve a exclusão permanente do VLC, salve o resultado no GitHub e pare antes da Etapa 5.
