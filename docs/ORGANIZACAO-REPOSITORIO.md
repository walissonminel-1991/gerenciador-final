# Organização do repositório — GERENCIADOR FINAL

**Etapa 8 — BLOQUEADA para pacote/entrega executável privada (28/10/2026).**
[Preparação e impedimento](WORK-INTEGRACAO-ETAPA8-PREPARACAO.md) ·
[guia/roteiro futuro PC FORTE](WORK-INTEGRACAO-ETAPA9-PC-FORTE-ROTEIRO.md) ·
[recibo](evidencias/integracao-etapa8-preparacao.json).
Fonte 6df95fdd776644cd4b248b5cea61108974ca9ce9 e CIs 40675784213/40675784155
reconferidas success, tentativa 1; onze diferenças documentais posteriores,
sem alteração funcional ou de arquivos embarcados. Perfil local e proteções
preservados. Somente três manifestos disponíveis; ZIP executável, SHA-256 do
pacote e link privado inexistentes. Alternativa privada documentada, sem
provisionamento ou contorno de gates. **Etapa 9 não iniciada; todos os testes
físicos NÃO EXECUTADO.** Roadmap 27/42; conectada/pública bloqueada.

## Estado e entradas

28/10/2026. Branch `work-integracao-final-20261027`, PR #65 aberta/Draft.
**Etapa 7 concluída — 28/10/2026:** [correções e regressões](WORK-INTEGRACAO-ETAPA7-CORRECOES.md),
[matriz de defeitos](WORK-INTEGRACAO-DEFEITOS.md) e [recibo](evidencias/integracao-etapa7-correcoes.json).
GF6-001/GF6-002 (ALTOS locais), GF6-003/GF6-004 (BAIXOS) e GF7-001
(regressão de CI, MÉDIO) corrigidos no escopo demonstrado.
Fonte **`6df95fdd776644cd4b248b5cea61108974ca9ce9`**, [Windows 40675784213](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784213)
e [integridade externa 40675784155](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40675784155) aprovadas,
tentativa 1. Regra local compilada: zero consultas automáticas e diagnóstico
remoto desativado inclusive com consentimento herdado/erro/encerramento.
**BETA LOCAL não preparada; conectada/pública bloqueada.** Próxima sessão
somente Etapa 8; não iniciada. Roadmap **27/42**, sem homologação física/AVG.

| Necessidade | Entrada canônica |
| --- | --- |
| Estado e retomada | [Checkpoint](WORK-INTEGRACAO-CHECKPOINT.md) e [matriz](WORK-INTEGRACAO-MATRIZ.md) |
| Regras das Etapas 5–9 | [Adendo BETA LOCAL](WORK-INTEGRACAO-ADENDO-BETA-LOCAL.md) |
| Fonte/reconciliação | [Etapa 1](WORK-INTEGRACAO-ETAPA1-RECONCILIACAO.md) e [Etapa 2](WORK-INTEGRACAO-ETAPA2-CODIGO.md) |
| CI e limites da candidata | [Etapa 3](WORK-INTEGRACAO-ETAPA3-VALIDACAO.md) e [avaliação 4](WORK-INTEGRACAO-ETAPA4-AVALIACAO.md) |
| Organização e propostas pendentes | [Etapa 5](WORK-INTEGRACAO-ETAPA5-ORGANIZACAO.md) e [inventário por SHA](evidencias/integracao-etapa5-organizacao.json) |
| Auditoria histórica | [Etapa 6/14 eixos](WORK-INTEGRACAO-ETAPA6-AUDITORIA.md), [matriz de defeitos](WORK-INTEGRACAO-DEFEITOS.md) e [recibo](evidencias/integracao-etapa6-auditoria.json) |
| Correções, regressões e CI própria | [Etapa 7](WORK-INTEGRACAO-ETAPA7-CORRECOES.md) e [recibo](evidencias/integracao-etapa7-correcoes.json) |
| Preparação/entrega local e roteiro condicionado | [Etapa 8](WORK-INTEGRACAO-ETAPA8-PREPARACAO.md), [recibo](evidencias/integracao-etapa8-preparacao.json) e [PC FORTE](WORK-INTEGRACAO-ETAPA9-PC-FORTE-ROTEIRO.md) |
| Progresso sem promoção artificial | [ROADMAP](ROADMAP.md) e [PROGRESSO.json](PROGRESSO.json), 27/42 |
| Testes e CI | [TESTES](TESTES.md) e [Actions atuais/históricas](ACTIONS-ATUAIS-E-HISTORICAS.md) |
| Futuro teste físico manual | [Plano mínimo da avaliação 4](WORK-INTEGRACAO-ETAPA4-AVALIACAO.md#7-testes-físicos-mínimos-futuros--pc-forte), reaproveitando [E1–E5](WORK-PRE-BETA-ETAPA6-EVIDENCIAS-FISICAS.md); sem execução agora |
| AVG, privacidade e distribuição | [AVG](AVG-DESENVOLVIMENTO-SEGURO.md), [PRIVACY](../PRIVACY.md), [contrato de publicação](PUBLICACAO.md) e [homologação conectada separada](BETA-66-HOMOLOGACAO-CONECTADA.md) |
| Missões anteriores | [Pré-BETA encerrada](WORK-PRE-BETA-ETAPA7-DECISAO.md), [WORK18](MISSAO-WORK18.md), [integração .64](MISSAO-INTEGRACAO.md), [WORK9 parte 1](MISSAO-WORK9-PARTE1.md) e [parte 2](MISSAO-WORK9-PARTE2.md) |

Este documento permanece o índice de organização; não criar um segundo índice
por build. README apresenta o produto; checkpoint/matriz governam a missão.
Os recibos 1–4 são históricos imutados, acessíveis pelos respectivos relatórios.
PR #40, main e branches de origem preservadas. Regras de VLC, WinGet COM,
drivers desativados, AVG e flags no permanecem; nenhuma aprovação decorre da organização.

## Inventário e referências

Inventário da fonte de entrada da Etapa 5:

| Categoria | Uso efetivo e decisão |
| --- | --- |
| C# na raiz | 142 fontes; curinga do SDK e exclusões explícitas. Dois pilotos de limpeza excluídos do produto são vinculados às regressões. Não inferir código morto pelo nome. |
| Três Catalogo*.json | Recursos incorporados com nomes lógicos diferentes: identidade, desinstalação e auditoria histórica. |
| Assets/ | Ícone e prévia usados pelo projeto/README; dois binários esperados. Nenhum EXE/DLL/ZIP/log de build na árvore. |
| Testes*/ | 13 suítes de console, duas fixtures TestesResiduos e TestesManifesto em PowerShell. TestesRede é opcional externo. As 92 fontes de teste são excluídas do produto; GF_PROGRESSIVE é hook sintético. |
| scripts/ | Guardrails, comparações/mutações, verificação do portátil e ferramentas de desenvolvimento. Presença não autoriza compilação conectada ou envio. |
| prototipos/ | 29 arquivos em dois receptores/protótipos com testes próprios; fonte de laboratório, sem ativação operacional. Não são duplicatas intercambiáveis. |
| Scripts .bat/.cmd/.iss | Build de desenvolvimento, portátil, instalador opcional e assinatura têm finalidades diferentes; preservados, sem execução nesta etapa. |
| .github/workflows/ | Quatro arquivos na candidata, dois chamadores de PR e dois reutilizáveis/dispatch. Lista explícita do guardrail coincide com a árvore; main conserva seus dois workflows. |
| Dependências declaradas | YamlDotNet 18.1.0 e Microsoft.WindowsPackageManager.ComInterop 1.29.380, além dos packs Microsoft; termos/avisos em [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md). Nenhuma mudança de licença/dependência. |
| Evidências históricas | Missões, segurança, benchmarks, testes físicos agregados, [arquivo PC FRACO](TESTES-HISTORICOS-PC-FRACO.md) e recibos preservados para auditoria. |

Na entrada: 437 blobs, 4.632.177 bytes; nenhum blob duplicado ou colisão de
caixa do Windows. Conferidos 91 Markdown/279 destinos relativos de arquivo e
157 referências explícitas Compile/ProjectReference/EmbeddedResource/Import,
sem destino ausente. Não é auditoria funcional, de todas as âncoras, de sites
externos ou de segredos. Nenhuma remoção de fonte foi demonstrada como segura.

## Branches, PRs, Issues e artefatos

O [inventário da Etapa 5](evidencias/integracao-etapa5-organizacao.json) fixa
101 branches (83 ancestrais, 17 divergentes e a candidata), 31 PRs abertas
Draft e três Issues abertas. Inclui 31 PRs encerradas de contexto e metadados
de 36 artefatos em cinco runs; não é catálogo global do armazenamento GitHub.
Ancestralidade não significa merge da PR nem autorização para apagar a branch.

As Issues #37/#39 são planos históricos; #48 é evidência sanitizada de
cobertura considerada na auditoria 6. A fonte atual sustenta mitigação técnica,
sem encerrar o caso físico. Nenhuma foi fechada/editada.
Dois nomes de branch apontam ao mesmo tip; D01 e D02 no relatório são apenas
propostas dependentes de autorização específica. Não excluir refs, fechar
PRs/Issues, apagar artefatos ou modificar main por aparência de antiguidade.

O upload BETA atual conserva manifestos/evidências e não entrega executável.
Retenção de sete dias; metadados não substituem arquivo privado dos bytes
quando necessário e autorizado. Nenhum pacote foi baixado ou preparado aqui.

## Faxina e preservação

A WORK9 consolidou nove relatórios/planos antigos do PC FRACO em um arquivo com
âncoras, conteúdo preservado, hash original e link ao checkpoint anterior.
Atualizou as referências e separou roteiros históricos do vigente. Os números e
commits estão no relatório da Parte 2. Nenhum histórico Git foi reescrito.

A remoção anterior de 40 workflows e dez guias v5.12 permanece registrada em
[Actions](ACTIONS-ATUAIS-E-HISTORICAS.md). Não foi repetida nem contabilizada como
trabalho desta missão. Runs, artefatos remotos, branches, tags e releases foram
preservados; remover arquivos da árvore não reduz objetos Git históricos.

Saídas bin/obj, Distribuicao, capturas, caches, logs e ZIPs locais ficam ignorados.
Não criar workflow nem LEIA-PRIMEIRO por versão. Novos fluxos precisam entrar na
lista explícita do guardrail após revisão. Evidências devem ser agregadas e sem
dados pessoais; preservar decisões únicas antes de consolidar documentos.
