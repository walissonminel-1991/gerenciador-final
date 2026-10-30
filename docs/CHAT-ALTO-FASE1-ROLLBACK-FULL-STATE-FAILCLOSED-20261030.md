# Gerenciador Final — Fase 1 — rollback same-user e contenção fail-closed

**2026-10-30 — checkpoint de engenharia em branch experimental. A Fase 1 permanece ABERTA; READY=false; G1–G8 BLOCKED 0/8.**

## Baseline confirmada

- Repositório `walissonminel-1991/gerenciador-final`; única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental inicial `c946a4da8af3e36e28b23e1572a8a4d1f022a60b`; CI técnica anterior 41064412191 SUCCESS, 19/19.
- CI intermediária [41069783718](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41069783718) FAILED ao compilar: `CS0122: L18Coordinator.RealSelector(string) inaccessible`, sem executar a suíte adversarial.
- Correção restrita ao teste, sem ampliar API do coordenador: commit `08b37626ef2f04fdc58bcd0c4ca0d15cd9f0446e`.

## Falha de autoridade reproduzida no Windows descartável

- [Run 41071494214](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41071494214), job `124116532619`, **completed/failure**. A compilação C# passou, assim como os 19 cenários originais.
- No vigésimo `same-user-full-state-rollback-after-new`, o owner criou snapshot de seletor `old/1` e prefixo do diário antes da seleção de NEW; o teste observou `real-observed-new.fixture.json` com processo real NEW criado, encerrado e saída graciosa limitada; restaurou seletor e histórico pré-lançamento e eliminou fence. O reconciliador C# novo aceitou `OldRecoveredNoReady`.
- A evidência para a aceitação é o *local exato* da falha: `GF_PHASE1_RECOVERY_FAIL same-user-full-state-rollback-after-new ... REAL_FINAL_SELECTOR_INCONSISTENT`. Antes desse assert, o teste exige sucesso de `outcome == OldRecoveredNoReady`, seletor `old/1` e relatório de observação de NEW. A expectativa final ainda exigia `old/3`, portanto o erro atesta diferença entre snapshot e expectativa da fixture, **não** bloqueio do ataque.
- Não reportar 20/20 PASS nesta CI: houve **19 casos aprovados + 1 falha depois da aceitação insegura**. O relatório `failed=1`. Esta execução demonstra contraexemplo de autoridade, não READY, recuperação segura, power loss, reboot nem quiescência de brokers.

## Alteração conservadora implementada, aguardando CI de validação

Commit técnico `f250be5a32a9706551051da1b0b082f57b383d15`:
- `ReconcileReal` conserva as barreiras anteriores para NEW selecionada, fence, journal, NTFS, TUF e Job Objects. Para histórico local aparentemente `old/1`, bloqueia **antes de restaurar/reobservar OLD** com `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`. Mesmo um estado prefixo válido same-user pode ter sido restaurado depois de NEW executar.
- Os 3 cenários `intent`, `recover-before-replace`, `recover-after-replace` deixam de autorizar recuperação; passam a validar recusa conservadora. Os nomes históricos permanecem mas **não executam mais cortes de crash do reconciliador**, não devem contar como prova de recuperação.
- O vigésimo exige `UnknownBlocked` na adulteração completa com `old/1` e NEW previamente observada, preservação do seletor e ausência de observação OLD, além de replay bloqueado. A suíte exige 20 `PASS_CONTAINMENT`, 0 `PASS_RECOVERY_LAB_ONLY` e ausência de `OldRecoveredNoReady`.
- A aprovação de uma nova CI verifica somente contenção no Windows descartável; sem autoridade de lançamento independente, recuperação automática não é autorizada.

**CI disparada para validar correção:** [41073513779](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41073513779), job `124123487779`. **Estado ainda em execução na escrita deste checkpoint; não afirmar resultado final até leitura dos logs.**

## Limites e proteções

Nenhum código GF distribuído, `main`, PR #65 Draft, BETA .66, infraestrutura, credenciais, telemetria ou PC pessoal alterado. Sem WORK, merge, release, deploy ou integração operacional. A Fase 1 permanece ABERTA; G1–G8 BLOCKED 0/8; READY=false. O fail-closed evita a promoção local da história, mas **não fornece** autoridade independente, demonstração de power-loss, garantia de I/O quiescente ou recuperação fria.

## Handoff

1. Conferir conclusão da CI 41073513779, examinar `GF_PHASE1_RECOVERY_REPORT` completo e os 20 casos, especialmente `same-user-full-state-rollback-after-new`.
2. Se falha, diagnosticar assert e corrigir somente o que for sustentado pela evidência, sem aceitar `OldRecoveredNoReady`.
3. Se sucesso, registrar `run/job/commit` e manter Fase 1 aberta; não promover READY ou G1–G8.

---

## Validação definitiva Windows — resultado posterior à seção de preparação

**CI [41073513779](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41073513779): completed/success.** Job **124123487779**, commit **`f250be5a32a9706551051da1b0b082f57b383d15`**. O resultado foi conferido pelo endpoint do GitHub Actions e pelo log bruto `GF_PHASE1_RECOVERY_CASE`/`GF_PHASE1_RECOVERY_REPORT` do job.

- **20 de 20 casos `status=passed`, todos `PASS_CONTAINMENT`; zero `PASS_RECOVERY_LAB_ONLY`; `failed=0`.** Isso comprova apenas a contenção implementada em laboratório descartável, não recuperação transacional.
- `same-user-full-state-rollback-after-new`: `outcome=UnknownBlocked`, `replay=UnknownBlocked`, `full_state_rollback_blocked=true`, `unsafe_recovery_after_real_new=false`, motivo `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`. A expectativa do workflow rejeita qualquer `OldRecoveredNoReady` e exige o 20º caso de bloqueio.
- `intent`, `recover-before-replace` e `recover-after-replace`: todos `PASS_CONTAINMENT`, `UnknownBlocked`, motivo `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`. Os dois nomes `recover-*` **não injetam mais crash de recuperação** e não devem ser contados como recuperação testada.
- `selected` e `joint-fence-journal-erasure`: `UnknownBlocked` por `REAL_NEW_SELECTED_NO_INDEPENDENT_NO_LAUNCH_PROOF`; as contenções anteriores mantidas.
- Relatório final declara expressamente `operational_ready=false`, `phase1_complete=false`, `operational_gates_closed=0`, `real_power_loss_tested=false`, `new_effects_quiescence_proven=false`. O resultado **não** prova cold recovery, perda de supervisor, READY, TUF de produção nem quiescência de brokers.

**Decisão final desta rodada:** a contraprova de aceitação insegura está preservada na CI 41071494214 (FAILURE após o reconciliador aceitar `OldRecoveredNoReady`), e a correção fail-closed está validada na CI 41073513779 (SUCCESS, 20/20 contenções). Como ainda não existe autoridade de lançamento anti-rollback independente do mesmo usuário, **nenhuma recuperação automática é autorizada**. Fase 1 **ABERTA**, READY=false, G1–G8 **BLOCKED 0/8**.
