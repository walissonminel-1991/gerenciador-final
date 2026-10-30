# GERENCIADOR FINAL — Fase 1 — contrato fixo e decisão de arquitetura

**2026-10-30. Resultado: NO_GO_FOR_POSITIVE_SUCCESS_UNDER_UNCHANGED_CONTRACT.** Decisão de engenharia, não nova funcionalidade. Sem licença para reclassificar HOLD como recuperação, remover guards ou declarar READY. A decisão deste checkpoint congela os requisitos vigentes, sem alterá-los.

## 1. Âncora auditável

Repositório `walissonminel-1991/gerenciador-final`, branch única de escrita `experimento-g5-ipc-chat-alto-20261029`. HEAD inicial `5d1847614fd200027ef642a27ead25ba7e9e81fc`. Código L18 `c4c79d61c31a2af83f732983a0bc7a40cdf3c2c6`; CI [41110783790](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41110783790), job 124252340866, **SUCCESS, 27/27 PASS_CONTAINMENT, zero recuperações**. LOW inerte `4499fef1d8e44085406d7e3ffecadaade5f545b0`; CI [41114014768](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41114014768), job 124263420669 SUCCESS, não executou GF real. PR #65 Draft, HEAD `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`, sem merge.

Leitura dirigida: `docs/CHAT-ALTO-AUTOATUALIZADOR-FASE1-GF-REAL-PARCIAL-CHECKPOINT-20261029.md` §§1,5,6 e limites R1–R6; `docs/CHAT-ALTO-FASE1-HOLD-RETRY-COOPERATIVO-ENSAIO-20261030.md`; `docs/CHAT-ALTO-FASE1-H1-CANDIDATOS-AUTORIDADE-DECISAO-NOGO-20261030.md`; `docs/CHAT-ALTO-FASE1-CHC01-PROTOCOLO-INSTRUMENTACAO-GATES-20261030.md`; `Phase1Recovery.cs.txt` e `L18Coordinator.cs.txt`.

## 2. Separação das quatro fases

| Fase | Entrega verificável | Limite |
|---|---|---|
| **1 — laboratório experimental** | Ciclo positivo de OLD/NEW reais: autenticidade TUF de teste, seleção única C# L18, execução/saúde relevante verificável, falhas e recuperação conservadora **no escopo prometido** | Contenção 27/27 e observação parcial de interface não demonstram ciclo de atualização positivo recuperável. Não exige por antecipação chaves de produção |
| **2 — qualificação de segurança** | G1–G8, confiança operacional, revisão adversarial e qualificação da candidata | Não sana retroativamente ausência de autoridade da Fase 1 nem permite enfraquecer seus testes |
| **3 — integração** | Incorporar o mecanismo homologável ao programa distribuível, sem seletor duplicado | `.cs.txt` e Python de CI não são código instalado no GF |
| **4 — homologação real** | Compatibilidade, funcionamento e falhas no Windows real em cenários autorizados | Crash de processo no runner não substitui reboot ou queda de energia |

## 3. Matriz curta dos critérios atuais

| Critério | Evidência atual | Lacuna | Dependência | Refutação |
|---|---|---|---|---|
| **Pacotes/identidade** | TUF teste autentica pacotes OLD/NEW, seleções e hashes/pins NTFS | Imagem carregada e janela TOCTOU geral ainda não atestadas | Vincular efetivamente bytes executados e identidade do objeto até launch | Trocar arquivo/namespace entre verificação e execução e executar bytes não autorizados |
| **Saúde/READY positivo** | GF real responde a campo de pesquisa com nonce e encerra por WM_CLOSE | Verificação de UI não atesta saúde relevante de motores nem READY independente | Contrato mínimo de funções efetivas e instrumento externo falsificável | NEW falha em função essencial enquanto nonce/WM_CLOSE passa e é marcada READY |
| **Crash pré-lançamento** | L18, fences, diário, BeginCore e bloqueios; 27 contenções | História `old/1` é restaurável mesmo após NEW executada, inclusive como prefixo válido | H1 história durável independente e H2 domínio obrigatório de launch | NEW executa; usuário restaura todo estado same-user pré-NEW; reconciliador autoriza OLD |
| **Pós-NEW/retorno OLD** | Mesmo coordenador observa NEW, retém OLD e NEW; Job/fence preservados | Efeitos delegados e I/O posterior não delimitados | H3 conclusão independente dos efeitos e H2 | Broker externo escreve depois de GF sair; retorno OLD foi autorizado por Job vazio |
| **Crash/restart/reboot** | Novos processos e replays bloqueados, retry `L20_BEGIN_BLOCKED` com estado íntegro | Não há cold recovery após perda do supervisor nem teste de reboot/power-loss; nunca inferir a partir de kills | H1/H2/H3 conforme o corte; infraestrutura de prova apropriada | Perder supervisor/witness e ainda autorizar recuperação baseada em estado local reapresentado |

Essa matriz reorganiza critérios já vigentes, não cria G9 nem transfere exigência de uma fase à seguinte.

## 4. Comportamento implementado: OLD, NEW, seletor e saúde

- **PREPARE:** `RealOwner` revalida manifesto e pins e grava diário + seletor `old/1`; `prepare-only` é terminal, sem execução funcional e sem retomada `prepare-only → normal`. Não é preparação sem escrita.
- **NORMAL:** o owner escreve fence OLD antes de observar OLD, seleciona `new/2`, grava fence NEW antes de observar NEW. Pesquisa WinForms e encerramento gracioso verificam somente uma função limitada. A execução normal registra `Unknown`, mantém `new/2` histórico e preserva OLD/NEW. Não há resultado de **sucesso de atualização** nem READY.
- **Falha ainda na preparação com owner original vivo:** cancelamento da preparação não é recuperação de crash; não executar OLD/NEW para obter recibo.
- **Após crash com histórico aparentemente pré-NEW:** mesmo `old/1` não certifica ausência de execução externa nem rollback same-user; `ReconcileReal` preserva `Require(false,"REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY")` e `UnknownBlocked`.
- **Após possível NEW:** `new/2` e fence permanecem; não selecionar ou relançar OLD, não limpar canário ou pacotes, não converter health visual em quiescência. Um retry com histórico íntegro é barrado em `BeginCore` (`L20_BEGIN_BLOCKED`).
- **Após reboot/power-loss:** não houve prova e não há autorização de retorno automático. HOLD pode preservar material para diagnóstico, não é recuperação.

**Adversário U:** qualquer processo com mesmo principal/privilégios que GF pode restaurar arquivos, bytes, pais/namespaces, journal, seletor e fences acessíveis a U e lançar NEW fora de L18. Não pressupor comprometimento do kernel; reavaliar quando GF é iniciado como administrador. Recursos observados/protegidos: arquivos pinados, seletor/diário validados, processos criados pelo Job, canário e pedaço de interface do GF. Não cobertos: brokers, serviços, I/O delegado, rede/COM/registro completos, outras execuções NEW, imagem carregada e falha física.

## 5. UMA arquitetura candidata dentro do envelope — avaliação de viabilidade

**`PORTABLE_B+A_ONE_WAY_WITH_FAIL_CLOSED/0`**: manter as duas versões separadas, C# L18 como único seletor, TUF/pins/fences; condicionar eventual decisão NEW a prova positiva de saúde relevante **a definir**, preservar OLD para diagnóstico, bloquear absolutamente rollback/retry automáticos quando história de execução for ambígua, inclusive após crash/reboot. Zero serviço, UAC ou backend; internet eventual apenas para baixar atualização.

**Resultado da checagem conjunta:** **NÃO atende ao contrato atual se este exigir recuperação automática após crash/reboot.** O mecanismo não cria H1/H2/H3, não confere READY e HOLD não é recuperação. Não pode ser implementado como substituto de garantia sem mudança expressa do contrato. Mesmo se aprovado, exige trabalho funcional novo: health mínimo de valor real e decisão positiva testável, resultados fail-closed, manual de recuperação sem ações destrutivas, testes positivos/negativos e depois qualificação. Não afirmar que preservar OLD impede danos de NEW.

## 6. Duas alternativas, consequências e decisão mínima

| Opção | Portabilidade / privilégio | Custo / offline | Entrega que ainda falta |
|---|---|---|---|
| **B — recomendada condicionalmente:** aceitar formalmente **sem recuperação automática em histórias ambíguas**, inclusive após crash/reboot; preservar OLD/NEW e bloquear até diagnóstico | Preserva executável portátil e usuário comum, sem serviço/ACL/UAC | Sem infraestrutura paga; offline para uso, rede eventual para obter release | Revisar contrato Fase 1 de forma expressa, definir evidência de sucesso/saúde mínima, implementar fluxo positivo e testes de HOLD/recuperação manual. Não confundir com prova de H1/H2/H3 |
| **A — manter recuperação automática forte:** exigir nova autoridade protegida de outra identidade, além de despacho obrigatório e prova de quiescência | Compromete portabilidade estrita; provável instalação privilegiada, ACL/serviço/política, autorização nova | Possivelmente sem hospedagem paga e com uso offline, mas manutenção/compatibilidade caras | H1/H2/H3, reboot/power-loss, atestação de saúde. Serviço sozinho não garante H2/H3 e não existe implementação pronta |

**Decisão essencial para o mantenedor:** autorizar explicitamente B (revisar a promessa de recuperação automática **sem** transformar HOLD em recuperação, e aceitar um contrato verificável de READY experimental limitado distinto do operacional), ou preservar A, exigindo primeiro autorização para estudar nova fronteira privilegiada. Não inferir consentimento a partir desta documentação.

## 7. Execução e encerramento desta rodada

**Nenhum código alterado; nenhuma CI disparada; nenhum WORK gasto.** Os contraexemplos que refutam os atalhos já foram executados (rollback full-state, launch fora do coordenador, broker tardio). Se dois resultados de uma nova fixture mantiverem `UnknownBlocked`, não há justificativa de engenharia para repetir a suíte.

**Status: Fase 1 ABERTA, READY=false, G1–G8 BLOCKED 0/8, 0 recuperações autorizadas.** `main`, PR #65 Draft, BETA .66, GF distribuído, TUF/B+A/NTFS/TOCTOU, infra/Cloudflare/D1, telemetria, segredos e PCs preservados. Sem merge, release, deploy, TPM, helper/serviço, alteração ACL/política Windows, backend ou integração operacional.

**Única próxima ação:** recolher do mantenedor a decisão expressa B versus A; somente depois escolher um incremento funcional e seu teste de refutação.
