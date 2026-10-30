# GF — Fase 1 — contrato de no-launch e ensaio de NEW fora do coordenador

**2026-10-30 · Ensaio Windows descartável; nenhuma integração operacional.**

## 1. Estado confirmado

- Repositório `walissonminel-1991/gerenciador-final`, única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD de partida `5057e42ce9aec3f0732127611b8c74ab91917bd5`.
- Última CI técnica aprovada antes do novo cenário: [41073513779](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41073513779), job `124123487779`: 20/20 `PASS_CONTAINMENT`, zero recuperações, READY=false.
- Último commit técnico com reconciliador alterado: `f250be5a32a9706551051da1b0b082f57b383d15`.
- Nova instrumentação de teste, sem alteração no reconciliador nem no GF distribuído: `c38db307dae6973b46aa44e339c335549e54fdf3`.
- CI disparada para novo cenário: [41078357355](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41078357355), job `124140520370`. **Status: em execução ao preparar este documento.** Não afirmar conclusão ou contagem 21/21 sem ler relatório.
- PR #65 confirmada open/Draft/unmerged antes da escrita.

## 2. Dois contratos de lançamento incompatíveis quando não declarados

### CONTRATO ESTREITO: L18-controlled launch

`L18_NO_LAUNCH`: não há lançamento **por aquele coordenador L18 específico**, com transação/manifesto/instalação vinculados, desde o ponto anterior ao `MAY_LAUNCH` até o cut observado. O próprio C# pode controlar as chamadas que efetua e as testemunhas do seu processo, mas uma história local same-user é restaurável. A ausência de lançamento pelo coordenador **não** significa ausência de execução do pacote por outros meios, nem autoriza automaticamente OLD.

### CONTRATO AMPLO: any GF NEW execution with potentially relevant effects

`GLOBAL_NO_NEW_EFFECT`: nenhuma execução de imagens/hosts capazes de produzir os efeitos da versão candidata NEW ocorreu (inclusive por `CreateProcess` do próprio usuário, cópia, outro launcher, broker, serviço, tarefa ou outro contexto) e nenhuma classe de efeito permanece ativa. Esta proposição exigiria enforcement do SO que impedisse lançamento não registrado e prova separada de encerramento de efeitos.

**A CI atual só demonstra contenção conservadora de estados ambíguos.** Sem H1 anti-rollback de ledger, H2 despacho obrigatório e H3 quiescência de efeitos, o contrato global é **não demonstrado**. Não usar `L18_NO_LAUNCH` para inferir `GLOBAL_NO_NEW_EFFECT`.

## 3. Novo teste adversarial: `out-of-band-new-with-old-selected`

**Objetivo:** mostrar em Windows descartável que NEW real pode ser iniciado de fora do `RealOwner` quando a transação histórica do coordenador permanece `OLD/1`. O teste não mexe em ACLs do sistema, política de execução, UAC, serviços, tarefas, usuários nem produção.

1. Prepara duas versões reais de GF e os mesmos dados autenticados do cenário L18; pins NTFS e TUF são mantidos.
2. Inicia owner L18 e o interrompe no cut `after-intent`, com `old/1`, antes de qualquer lançamento de NEW pelo coordenador, sem `NewLaunchFence`.
3. Um processo Python **iniciado diretamente pelo supervisor de testes**, fora dos Jobs C# owner e recovery, executa o GF NEW real via `subprocess.Popen` e observador Win32 já existente. Seleção `old/1` é checada, não alterada. A origem da execução é distinta do coordenador; o modo adicional é *somente laboratório*.
4. Exige evidência específica de processo GF NEW criado/terminado, identidade do executável, resposta Win32 e exercício funcional de inventário somente leitura; nunca trata isso como READY ou quiescência.
5. Exige que seletor `old/1`, diário L18 sem `Launched` de NEW, fence ausente e Jobs C# vazios permaneçam como antes.
6. Reabre o **mesmo** `ReconcileReal` em um novo processo C#, exige `UnknownBlocked` pelo motivo `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY` e replay bloqueado; não relança OLD.
7. O workflow exige 21/21 `PASS_CONTAINMENT`, zero recuperações e `out_of_band_new_execution_observed=true` / `out_of_band_no_launch_claim_rejected=true`.

**O que confirma, se passar:** o contrato restrito de launch pelo coordenador não equivale a autorização global de ausência de execução. O caminho atual permanece conservador sem precisar inserir um helper privilegiado.

**O que NÃO confirma:** adversário administrador no host, capacidades de outras contas, elevação, política de execução do sistema, outros brokers ou I/O externos, cold recovery, perda real de energia ou READY. O teste usa uma versão GF descartável e um processo de supervisão do próprio runner, não autentica bytes carregados na memória como uma política de kernel.

## 4. Limites e próximos critérios

- Se a CI falhar por erro de compilação/harness, corrigir estritamente a fixture e repetir, sem classificar como proteção.
- Se passar, não restabelecer `OldRecoveredNoReady`. Não inventar uma autoridade de lançamento.
- Se a execução direta for bloqueada por política do runner, registrar a precondição concreta e tratar H2 como **não demonstrada**, não como garantida em todos os Windows.
- Antes de propor um helper elevado, provar que a execução direta/alternativa não pode ocorrer no escopo reivindicado; caso contrário o ledger protegido de outra identidade apenas prova o que ele registrou.

**Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8.** Não alterar main, PR #65 Draft, BETA .66, infraestrutura, credenciais, telemetria ou PCs pessoais. Sem WORK, merge, release, deploy ou integração operacional. Todas as mudanças técnicas desta rodada são fixtures de laboratório e validação de CI.


## 5. Resultado Windows definitivo (posterior ao preparo)

**CI [41078357355](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41078357355): `completed/success`**, job `124140520370`, commit técnico de laboratório `c38db307dae6973b46aa44e339c335549e54fdf3`, período do run `2026-10-30T15:11:16Z` a `15:21:13Z` (atualização final da API).

O log bruto `GF_PHASE1_RECOVERY_CASE` contém **21 casos `passed`**, todos classificados **`PASS_CONTAINMENT`**. O `GF_PHASE1_RECOVERY_REPORT` declara `expected_cases=21`, `failed=0`, `operational_ready=false`, `phase1_complete=false`, `operational_gates_closed=0`, `real_power_loss_tested=false`, `supervisor_loss_recovery_proven=false`, `new_effects_quiescence_proven=false`. **0 casos `PASS_RECOVERY_LAB_ONLY`.**

### Caso novo: evidência vinculada, não inferência

`out-of-band-new-with-old-selected`:
- `status=passed`, `classification=PASS_CONTAINMENT`;
- `out_of_band_new_execution_observed=true`, `out_of_band_no_launch_claim_rejected=true`;
- `outcome=UnknownBlocked`, `replay=UnknownBlocked`;
- `block_reason=REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`;
- `old_and_canary_preserved=true`, `manifest_and_object_identities_rechecked=true`;
- `unsafe_recovery_after_real_new=false`, `operational_ready=false`.

A fixture exigiu que NEW fosse **realmente lançada pelo observador Python fora de ambos os Jobs do coordenador** e observada funcionalmente, com o seletor `OLD/1`, sem fence NEW, sem observação OLD e sem journal de lançamento do `RealOwner`. Um reconciliador C# novo e replay permaneceram bloqueados.

O caso histórico `same-user-full-state-rollback-after-new` continuou `passed/UnknownBlocked/replay UnknownBlocked`, confirmando ausência de regressão no limite já validado. Não houve alteração em `Phase1Recovery.cs.txt`.

**Interpretação segura:** ficou empiricamente refutado no runner descartável que o estado `OLD/1`/sem fence equivale a `nenhuma execução de NEW fora do L18`. Isso não prova que todo modo alternativo de execução ou efeito foi investigado; não é uma prova de atualização/desinstalação real, efeitos duráveis, reboot ou quiescência.

**Decisão:** manter a barreira `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`. Qualquer hipótese de recuperação futura precisa especificar formalmente o escopo de `no-launch` e comprovar enforcement não contornável nesse escopo. Não criar um auxiliar elevado somente para proteger um ledger que outras execuções podem contornar.

**Preservações conferidas:** nova escrita técnica limitada a `Phase1RecoveryTests.cs.txt`, `phase1_lab.py` e workflow de laboratório; GF distribuído, `main`, PR #65 Draft, BETA .66, infra, telemetria e PCs pessoais intocados. Fase 1 **ABERTA**, READY=false, G1–G8 BLOCKED 0/8.
