# Integração final — Etapa 5: organização segura do GitHub

27/10/2026, America/Sao_Paulo. Repositório `walissonminel-1991/gerenciador-final`.
Branch `work-integracao-final-20261027`, [PR #65 Draft](https://github.com/walissonminel-1991/gerenciador-final/pull/65).

**ETAPA 5 CONCLUÍDA no escopo organizacional descrito. Etapa 6 não iniciada.**
BETA LOCAL não preparada; BETA conectada/pública bloqueada. Nenhuma exclusão,
fechamento, merge, build, distribuição ou mudança de produto nesta sessão.

[Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) · [Matriz](WORK-INTEGRACAO-MATRIZ.md) ·
[Índice de organização](ORGANIZACAO-REPOSITORIO.md) ·
[Recibo/inventário](evidencias/integracao-etapa5-organizacao.json) ·
[Avaliação anterior](WORK-INTEGRACAO-ETAPA4-AVALIACAO.md) ·
[Adendo BETA LOCAL](WORK-INTEGRACAO-ADENDO-BETA-LOCAL.md).

## 1. Entrada, fontes e alcance

HEAD de entrada conferido: **`d815d1a5348b3f24c29e5c133a45b081cb7f6958`**,
árvore `f36abd8191bed637148ff6e533aa7578bfbd68af`; PR aberta/Draft, base #60
`2bc9f4228fd448445fbf181652e5af93005def20`. Checkpoint, matriz, avaliação 4,
recibo 4 e adendo relidos. Os oito caminhos diferentes da fonte validada
`6b5b0e52f3a576c1c59eb20ca66ed8c61bfb48d8` são somente Markdown/JSON.

Inventariadas todas as 101 branches (duas páginas), 31 PRs abertas e três
Issues abertas; também 31 PRs encerradas para contexto. As coleções couberam
em suas páginas, exceto branches com paginação explícita. Conferidos os 437
blobs da árvore e 120 arquivos textuais por SHA Git: 91 Markdown, projetos,
configurações, guardrails e metadados de continuidade. Não há AGENTS.md na árvore.

Workflows foram auditados pela fonte versionada e pelas execuções; a main foi
consultada apenas para confirmar seus dois arquivos de workflow. Examinadas
100 execuções recentes das 2.046 retornadas como total e os metadados de 36
artefatos em cinco runs relevantes. **Não é inventário global de todos os
artefatos/execuções**, nem nova verificação de seus bytes. Nenhum binário foi
baixado ou executado. Estado operacional de Worker/D1/DNS/Secrets não auditado.

## 2. Branches e PRs — classificação sem descarte

Foram comparados os 100 SHAs distintos das 101 branches contra a entrada
da candidata. O recibo contém nome, SHA completo, relação de ancestralidade,
ahead/behind, base comum e PRs relacionadas de cada branch.

| Grupo | Quantidade / evidência | Decisão organizacional |
| --- | --- | --- |
| Candidata atual | Uma branch, PR #65 | Única linha de continuidade desta missão; manter Draft |
| Tips ancestrais da candidata | 83 branches; ahead=0 na comparação | Código alcançável no histórico; preservar refs. Isso não significa PR mesclada ou identidade de comportamento atual |
| Tips divergentes | 17 branches | Preservar commits próprios; não inferir redundância ou perda de utilidade pela idade |
| Linhas v5.12 | 34 nomes com essa versão | Marcos históricos, sem necessidade de reexecutar seus testes |
| Dois nomes no mesmo tip | `teste-diagnostico-precisao-v5.13.6` e `teste-inventario-residuos-v5.13.7`, SHA `dd89e28eab2aa193accbf5e85dc566aeef631c72` | Alias identificado; remoção apenas como proposta D01 |

As 31 PRs abertas são Draft: #65 atual, 21 com tip ancestral e nove
divergentes. Nenhuma foi fechada, retargetada ou declarada mesclada.

| PRs / linha | Situação e evidência preservada |
| --- | --- |
| #65 | Candidata atual; avaliação 1–4 e organização 5 documentadas |
| #64 / #60 / #59 / #54–#57 | Tips ancestrais; checkpoints, decisões e CIs históricos preservados |
| #61 / #62 | Divergentes no grafo; #61 já está na #62, cujo delta foi conciliado na Etapa 2. Não reimportar nem tratar como commits mesclados |
| #63 | Experimento divergente; revisão corrigida preservada pela #64 e integração. Não substituir a revisão atual pelo experimento |
| #47 / #49–#53 | Frente divergente de auditoria/WORK14–17; histórico da integração anterior explica a incorporação. Não descartar os commits próprios |
| #40 | Linha experimental original, ancestral e Draft; preservação obrigatória |
| #24–#35 e #38 | Outras PRs abertas com tips ancestrais; decisões e escopos históricos mantidos |
| #58 encerrada | Mesmo head/SHA da #53, base diferente; duplicação histórica de proposta, não uma PR aberta a fechar agora |
| #41 / #42 encerradas e merged | Limpeza/segurança incorporadas na main segundo metadados das PRs; tips originais divergem da candidata. Squash/integração não autorizam apagar refs |
| #43–#46 encerradas | Evidências e tentativas físicas anteriores; preservar. Em #44 o tip atual da branch difere do head encerrado, distinção registrada no recibo |

As demais divergências incluem `laboratorio-provedores-v0.1`, duas linhas
`teste-consolidacao` e validações físicas. Não foram investigadas como novas
fontes de produto, reintegradas ou usadas para reabrir escopos excluídos.

## 3. Issues e histórico a preservar

| Issue aberta | Organização e continuidade |
| --- | --- |
| [#48](https://github.com/walissonminel-1991/gerenciador-final/issues/48) | Registro sanitizado R4/R7 de cobertura inconclusiva e leitura HKLM. Manter aberto como evidência; a Etapa 6 deverá conferir aplicabilidade à candidata, sem solicitar repetição destrutiva ou presumir correção |
| [#39](https://github.com/walissonminel-1991/gerenciador-final/issues/39) | Histórico do desenho de diagnóstico/recebimento. Referenciar junto aos gates atuais de privacidade; não autoriza envio, formulário público ou implantação |
| [#37](https://github.com/walissonminel-1991/gerenciador-final/issues/37) | Checklist histórico da .0 e AVG/distribuição. Preservar; não usar como aprovação da .66 ou ordem para Release |

Nenhuma mudança de estado, comentário, rótulo ou edição das Issues. O recibo
publica somente número, título, URL, estado e encaminhamento organizacional;
não copia corpos, logs pessoais ou diagnósticos brutos. Históricos físicos,
segurança, decisões, benchmarks e recibos das etapas anteriores permanecem.

## 4. Workflows e arquivos de teste

| Arquivo na candidata | Papel atual |
| --- | --- |
| `build-windows.yml` | Chamador de regressões/política; build x64 depende de ambos; BETA conserva manifestos, sem upload de executável |
| `validar-candidata-windows.yml` | Reutilizável/dispatch: regressões, consentimento antecipado e drivers somente leitura 2022/2025; gate `--official-live` preservado |
| `triagem-diagnostico-github.yml` | Reutilizável/dispatch: guardrails e receptores/SQLite sintéticos; sem triagem automática de conteúdo de Issues |
| `integridade-externa.yml` | CI separada: integridade dos instaladores sem instalar; consultas externas adicionais condicionais |

A lista explícita de `scripts/test_politicas_seguranca.py` já exige esses
quatro arquivos; não foi alterada. A main continua com dois workflows; os
registros de três da WORK9/PR #40 foram identificados como históricos nos guias.
Não confundir quatro arquivos com quatro runs de PR: dois são reutilizáveis.
Atestação permanece condicionada à main/não BETA; não é assinatura Authenticode.

13 suítes de console, das quais TestesRede é opcional externo; duas fixtures
em TestesResiduos e TestesManifesto em PowerShell. As 92 fontes C# de teste
permanecem excluídas do produto; 157 referências explícitas de projetos/fontes/
recursos/imports resolvem na árvore. Curingas/SDK/expressões MSBuild não foram
contados como referências explícitas. Não remover fixtures, projetos, hooks
condicionais ou testes de segurança por parecerem repetidos; os modos focados
e integrais têm finalidades próprias. Nenhuma suíte foi executada nesta etapa.

## 5. Documentação e árvore integrada

Achados organizacionais corrigidos, sem produzir nova implementação:

| ID | Referência desatualizada | Correção segura |
| --- | --- | --- |
| S01 | README apresenta #61/.65 como entrada e afirma implantação remota não comprovada | Entrada #65, CI própria e estado local/conectado; .65 explicitamente histórica; remoto não auditado |
| S02 | Índice de organização aponta WORK9/#40 como estado corrente | Índice único para checkpoint, matriz, adendo e relatórios 1–5; históricos seguem acessíveis |
| S03 | Organização menciona três workflows, 12 suítes e só packs Microsoft | Inventário atual com quatro workflows, 13 suítes + duas fixtures e dependências explícitas do projeto/avisos existentes |
| S04 | Guia Actions mistura estado corrente e decisões de 21–22/10 | Seção atual PR #65 e arquivo histórico claramente separados; contrato BETA/duas CIs explicitados |
| S05 | TESTES abre procedimento da .65 sem entrada da integração | Entrada .66 com CI/relatórios, inclusão documental de TestesEcossistema e distinção de TestesRede opcional; roteiros anteriores preservados |
| S06 | Continuidade/roadmap encerram antes da organização | Estado 5, evidências e próxima sessão 6 registrados; somente bloco integration_final muda no PROGRESSO |

No inventário de entrada: 437 blobs, 4.632.177 bytes; nenhum blob byte a byte
duplicado, colisão de nomes por caixa do Windows, bin/obj/Distribuicao,
EXE/DLL/ZIP/PDB/log de build ou pacote/credencial com extensão rastreada pela
conferência. Isso não constitui varredura geral de segredos ou auditoria
funcional. Há dois assets binários esperados (ícone e prévia).

Os 142 C# da raiz usam a organização do projeto; dois pilotos de limpeza
são explicitamente excluídos e vinculados às regressões. Os três catálogos
possuem recursos lógicos diferentes. Os 29 arquivos de dois protótipos têm
papéis/testes distintos e não são infraestrutura ativa. Scripts de compilação,
portátil, instalador e assinatura têm finalidades diferentes; presença não
autoriza executá-los. Nenhum arquivo foi considerado removível apenas pelo nome.

91 Markdown e 279 destinos relativos de arquivo conferidos contra a árvore,
sem destino ausente. A conferência não validou todos os fragmentos/âncoras ou
sites externos; não houve consulta a fontes de aplicativo. As correções são de
estado e navegação, sem inventar links quebrados ou multiplicar índices.

## 6. Artefatos, retenção e CI

| Run examinado | Artefatos | Papel |
| --- | --- | --- |
| 40604593820 | 9 | Evidências/manifestos da candidata integrada, sem pacote executável |
| 40604593709 | 0 | Integridade externa da mesma fonte |
| 40565220517 | 9 | Pré-BETA #64; inclui pacote .65 histórico |
| 40409850849 | 9 | BETA #62 histórica; manifestos/evidências |
| 40252100887 | 9 | WORK18 .65 histórica; pacote anterior e evidências |

Total observado: 36 artefatos, 127.837.710 bytes de tamanho informado, nenhum
marcado expired no instante da consulta. Não representa consumo total da conta.
Sem download, exclusão, mudança de retenção, reupload ou novo artefato.
Releases e tags: zero observadas; nenhuma criada ou removida.

O manifesto da integração ID **12779928854**, fonte `6b5b0e52`, permanece com
digest `ebe688f662593689c0ba8b0f4d43a33828d8b048d8386eed2be8a09043aa6754`
e expiração informada em **03/11/2026**. A retenção configurada dos uploads é
sete dias. Metadados/digest não preservam os bytes depois da expiração; os
recibos anteriores registram o escopo verificado. Se o mantenedor precisar
conservar artefatos além da retenção, planejar arquivo privado autorizado;
não reenviar executáveis, abrir Release ou contornar o contrato nesta etapa.

Reconferidas principal [40604593820](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40604593820)
e integridade [40604593709](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40604593709):
completed/success, tentativa 1, SHA exato **`6b5b0e52f3a576c1c59eb20ca66ed8c61bfb48d8`**;
sete jobs obrigatórios success e dois condicionais skipped. Aprovação não
transferida das PRs antigas. Commits documentais usam `[skip ci]`, sem nova build.

Limite do conector: os endpoints de enumeração global de workflows/artefatos
não são expostos pelo fetch. Foram usados arquivos/guardrail versionados,
coleção de runs e ferramenta de artefatos por run. Estado enabled/disabled no
servidor e artefatos fora dos cinco runs não são afirmados. A consulta de refs
Git confirmou ausência de tags; não houve necessidade de navegador/fallback.

## 7. Propostas que dependem de autorização específica

**Nenhuma foi executada; não há pedido de aprovação pendente nesta sessão.**

| ID | Proposta opcional | Alvo exato e condição antes de qualquer ação |
| --- | --- | --- |
| D01 | Remover um alias de branch, se o mantenedor não precisar dele | Somente `teste-inventario-residuos-v5.13.7`, tip `dd89e28eab2aa193accbf5e85dc566aeef631c72`; preservar o alias `teste-diagnostico-precisao-v5.13.6`, conferir refs/PRs/dependências novamente e obter autorização que nomeie a branch. Não é necessária para continuar |
| D02 | Encerrar propostas sobrepostas após decisão explícita do mantenedor sobre a candidata | Somente PRs #61, #62, #63 e #64, individualmente autorizadas e com recibos/decisões acessíveis. #65 segue Draft e não substitui revisão. Não executar automaticamente nem apagar suas branches/históricos |

Não é proposta apagar as demais 100 refs, commits, tags, Releases, documentos,
testes, runs ou artefatos. PR #40 e main continuam preservadas. PRs ancestrais
podem continuar abertas como trilha de revisão; o catálogo elimina a ambiguidade
sem mudar seus estados. #58 já estava encerrada, portanto não entra em D02.

## 8. Verificações, pendências e parada

Alterados somente README, guias de organização/Actions/TESTES, checkpoint,
matriz, PROGRESSO, ROADMAP, este relatório e recibo JSON: **dez Markdown/JSON**.
Verificações documentais aprovadas: JSONs, preservação dos 42 estados e
históricos, 92 Markdown/324 destinos relativos de arquivo e dois fragmentos
novos. Blocos de comandos de TESTES e arquivo histórico de Actions preservados.
O contrato existente de metadados também passou: candidata .66 pending e
aprovação .65 histórica, distinta da CI própria da integração. Conferir os
blobs no GitHub antes do fechamento. Código, testes, workflows, versão, aviso,
políticas e históricos não mudam. As 101 refs da entrada serão reconferidas;
somente a branch de continuidade pode avançar.

Roadmap **27/42**, os mesmos 42 IDs/done; candidate/beta e históricos intactos.
VLC permanentemente somente inventário, sem novos testes específicos, ações,
consultas ou verificações. WinGet COM opcional/não bloqueante/consultivo, sem
reinvestigação. Gestão de drivers indisponível. AVG e proteções preservados,
sem assinatura/homologação presumida. `PILOT_ENABLED=no` e
`GITHUB_EXPORT_ENABLED=no` mantidos. Nenhuma transmissão, infraestrutura remota,
operação em PC, merge, release, deploy, distribuição ou preparação de pacote.

Permanecem WUA 2022 inconclusiva, 2025 sem ofertas, CS0649 não bloqueante,
AVG/físico/conectada e requisitos do perfil BETA LOCAL registrados na Etapa 4.
A auditoria geral da **Etapa 6 ainda não foi executada**. O próximo trabalho
deve produzir a matriz de defeitos nos 14 eixos do adendo; incluir a conferência
do achado #48 e dos gatilhos/consentimento antigo do futuro perfil local.
Não é autorização para corrigir antes de demonstrar defeito ou preparar EXE.

**Próxima sessão: somente Etapa 6. Salvar e parar antes da Etapa 7.**
Organização 5 não homologa a BETA LOCAL e não desbloqueia a conectada/pública.
