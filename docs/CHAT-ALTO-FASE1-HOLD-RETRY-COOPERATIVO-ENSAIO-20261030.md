# GERENCIADOR FINAL — Fase 1 — HOLD conservador: segunda tentativa cooperativa após UNKNOWN

**Data:** 30/10/2026. **Natureza:** ensaio funcional experimental, exclusivamente Windows descartável de CI. Este checkpoint é registrado enquanto a CI ainda está executando; **nenhum resultado da nova execução é antecipado**.

## 1. Identidade e escopo

- Repositório: `walissonminel-1991/gerenciador-final`; única branch de escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD de partida: `074d21e26b3c4fef6c88677cec97e50d3c943556`.
- Commit técnico: `75d8df112373a907a8b19634c51ecb3d9d6f881d`.
- Modificações técnicas: `prototipos/autoatualizador-seguro/phase1-composed/Phase1RecoveryTests.cs.txt` e `.github/workflows/gf-autoatualizador-fase1-real-lab.yml`. **NÃO** foram alterados `Phase1Recovery.cs.txt`, o C# L18 de produção experimental, TUF, B+A, NTFS/TOCTOU, seletor ou política de recuperação.
- CI anterior aprovada: [41098083325](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41098083325), job `124208946182`, 26/26 `PASS_CONTAINMENT`, zero recuperações.
- CI desta mudança: [41109061522](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41109061522), `head_sha=75d8df112373a907a8b19634c51ecb3d9d6f881d`; **verificação final pendente quando esta seção foi escrita**.
- PR #65 conferida aberta/Draft/sem merge. `main`, BETA .66, Cloudflare/D1, telemetria, credenciais e computadores pessoais fora do escopo.

## 2. Diferença material frente às 26 contenções anteriores

O teste `retry-after-unknown-retains-both` avalia **uma segunda invocação do proprietário RealOwner** após uma primeira execução funcional OLD/NEW real e reconciliador separado retornar `UnknownBlocked`. Casos anteriores avaliavam *replay do reconciliador*, não esse veto ao início de uma nova tentativa cooperativa.

Sequência implementada no teste C#, sem novo coordinator:

1. Preparar raiz marcada em Windows Actions, duas versões reais do GF e TUF de laboratório.
2. Iniciar `RealOwner(normal)`; exigir sucesso e recibos de observação funcional real de **OLD e NEW**, `created`, `terminated` e encerramento gracioso do processo observado.
3. Reconciliar em novo processo; exigir `UnknownBlocked` com `REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`, Jobs inativos.
4. Capturar SHA256 do seletor, fences OLD/NEW, relatórios de observação e sequência de arquivos do diário L18; abrir pins dos arquivos OLD/NEW e canário e conferir o namespace real.
5. Executar **outro processo** `--real-owner ... normal` na **mesma raiz e transação**; exigir saída não zero com o motivo `L20_BEGIN_BLOCKED` do `BeginCore` já existente, antes de observação funcional.
6. Conferir que nenhum dos arquivos anteriores foi alterado: seletor, journal, fences, relatórios OLD/NEW, identidade NTFS e bytes de todos os arquivos fixados, além do conjunto de caminhos dos dois pacotes e do canário.
7. Reconciliar novamente em processo independente; exigir `UnknownBlocked`, sem promoção, sem nova seleção de OLD/NEW nem alteração de journal.

O segundo processo **pode registrar um diagnóstico próprio de erro**, fora da lista de objetos invariantes. Isso não confere autoridade; o teste não exige uma raiz literalmente sem I/O.

A CI foi atualizada para exigir exatamente **27** casos `PASS_CONTAINMENT` e uma linha de evidência estruturada com campos `cooperating_retry_rejected_before_launch`, `second_owner_rejection=L20_BEGIN_BLOCKED`, `old_and_new_bytes_preserved`, `selector_unchanged`, `fences_and_observations_unchanged`, `journal_unchanged`, `unknown_replay_blocked`, e os indicadores negativos `same_user_rollback_resistant=false`, `external_launch_blocked=false`, `independent_recovery_authority=false`, `operational_ready=false`. Os 26 casos existentes continuam na mesma matriz.

## 3. Hipótese, limitações e decisão

**Hipótese `P-HOLD-1` em escopo restrito:** quando o histórico local permanece íntegro, `BeginCore` não aceita uma tentativa nova sobre a mesma raiz após uma atualização ambígua. O novo ensaio só pode comprovar essa propriedade depois de efetivamente passar no Windows.

**Não é H1**: o mesmo usuário ainda pode restaurar estado mutável (incluindo journal e seletor). **Não é H2**: NEW pode executar fora do coordenador. **Não é H3**: brokers e efeitos delegados podem continuar após o processo. Não há reboot, power loss, perda do supervisor, proteção anti-rollback de hardware, quiescência universal ou recuperação automática. A política segue `UnknownBlocked` e `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY`.

Se o caso falhar, manter a Fase 1 bloqueada e tratar erro como **falha de prova**, sem relaxar asserções para obter verde e sem declarar 27/27. Um runner `SUCCESS` somente confirma as asserções implementadas, não autoriza atualização do produto.

**Estado obrigatório:** Fase 1 ABERTA, `READY=false`, G1–G8 `BLOCKED 0/8`, zero recuperações automáticas autorizadas. Sem WORK, merge, release, deploy, helper/serviço/tarefa privilegiada, ACL/política Windows, TPM, backend ou integração operacional.

## 4. Evidência final da CI

**Primeira execução e diagnóstico da falha (comprovados):** [run 41109061522](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41109061522), job `124246352419`, tentativa 1, `completed/failure`, head técnico `75d8df112373a907a8b19634c51ecb3d9d6f881d`. A compilação e o teste C# produziram **27/27 `PASS_CONTAINMENT`, 0 falhas, 0 recuperações** no relatório, incluindo `retry-after-unknown-retains-both` com `L20_BEGIN_BLOCKED` e preservação de OLD/NEW, seletor, journal, fences e canário. **A CI global NÃO PASSOU**: a asserção herdada exigia `old_and_canary_preserved`, `reconciler_new_process`, `manifest_and_object_identities_rechecked` em **todas** as linhas de resultado; o caso novo omitiu esses três campos. O log bruto reportou `GF_PHASE1_CRASH_FALSE_READY_OR_INCOMPLETE_EVIDENCE`. Auditoria programática do JSON confirmou que **somente o novo caso** violou esse contrato de esquema; nenhum teste C# falhou.

**Correção de relatório (não de segurança):** commit `c4c79d61c31a2af83f732983a0bc7a40cdf3c2c6` adicionou os três campos `true` ao resultado do caso **depois** de verificar seus controles já executados. Não alterou o coordenador, os 27 procedimentos de execução, os demais casos ou a asserção global do workflow. CI de validação do commit corrigido: [41110783790](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41110783790). **Resultado ainda pendente no momento desta edição**; não declarar 27/27 de CI global até conferir sua conclusão.


**Validação final comprovada (CI corrigida):** [run 41110783790](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41110783790), job `124252340866`, tentativa 1, `completed/success`, head SHA técnico `c4c79d61c31a2af83f732983a0bc7a40cdf3c2c6`.

O log bruto confirmou **27 registros individuais, 27 nomes únicos, 27 `PASS_CONTAINMENT`, zero falhas, zero recuperações**, relatório final `expected_cases=27`, `failed=0`, `operational_ready=false`, `phase1_complete=false`, `operational_gates_closed=0`, `new_effects_quiescence_proven=false`, `supervisor_loss_recovery_proven=false` e `real_power_loss_tested=false`. Todas as linhas possuem agora os três campos globais de integridade; zero violações ou `##[error]` no job.

O caso `retry-after-unknown-retains-both` foi confirmado `passed/PASS_CONTAINMENT`: `cooperating_retry_rejected_before_launch=true`, `second_owner_rejection=L20_BEGIN_BLOCKED`, `real_gf_old_and_new_observed_before_retry=true`, `old_and_new_bytes_preserved=true`, `selector_unchanged=true`, `fences_and_observations_unchanged=true`, `journal_unchanged=true`, `unknown_replay_blocked=true`, `outcome=UnknownBlocked`, `replay=UnknownBlocked`, `block_reason=REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`. Campos de limite confirmados `same_user_rollback_resistant=false`, `external_launch_blocked=false`, `independent_recovery_authority=false`, `unsafe_recovery_after_real_new=false`, `operational_ready=false`.

**Classificação:** `PASS_CONTAINMENT` exclusivamente para **não repetir o lançamento cooperativo numa raiz com histórico íntegro**. Não prova recuperação transacional, anti-rollback H1, despacho universal H2, quiescência H3, durabilidade após reboot/power loss ou proteção contra execução NEW fora do coordenador. O bloqueio `REAL_NO_INDEPENDENT_NO_LAUNCH_AUTHORITY` e `UnknownBlocked` permanece inalterado. **Fase 1 ABERTA; READY=false; G1–G8 BLOCKED 0/8.**
