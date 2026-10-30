# Gerenciador Final — Fase 1 — ensaio de rollback conjunto após NEW

**2026-10-30 — checkpoint técnico de preparação, ainda SEM RESULTADO Windows verificado**

## Âncora e escopo

- Branch única: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD de partida confirmado: `585d6ff5cfdb3270f3caefad5dfc2b1ea8095d4e`.
- Baseline técnica: `5fd3a583caf4f62c58d382e5fc31e314b44cdc1d`.
- Baseline de CI previamente comprovada: [run 41064412191](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41064412191), job 124091955138, sucesso 19/19: 3 recuperações limitadas e 16 contenções. **Esta CI não executou o vigésimo cenário.**
- Commits desta rodada: `a064bfa6aa35a1e3393b0df29ef6217d266860ae` (snapshot pré-NEW), `695998e788ba61afb0e0779432edde2e7ea2b8e9` (ensaio de adversário same-user), `6db4a68ea83cb45ffc031c24617c4bf790b9540b` (validação de relatório no workflow).
- Únicos arquivos técnicos modificados: `phase1-composed/Phase1Recovery.cs.txt`, `Phase1RecoveryTests.cs.txt` e `.github/workflows/gf-autoatualizador-fase1-real-lab.yml`. Nenhum código do GF distribuído foi modificado.

## Hipótese e execução especificada

Foi adicionado o cenário `same-user-full-state-rollback-after-new` à matriz da suíte Windows, passando de 19 para **20** casos:

1. O mesmo `RealOwner` C# L18 cria snapshot apenas **de teste** dos bytes válidos do seletor `old/1` e do prefixo do diário, imediatamente antes de selecionar NEW.
2. Prossegue pelo seletor real NEW, grava `NewLaunchFence`, registra `Launched`, executa a candidata **GF NEW real no runner descartável** usando o observador existente, valida criação/terminação/saída graciosa limitada, e registra `Exited`.
3. O supervisor de teste interrompe o owner, remove a cauda do diário e fence de NEW e repõe os bytes do seletor `old/1` e do diário pré-NEW. Pacotes TUF, hashes e objetos NTFS dos slots são mantidos.
4. Novo processo reconciliador usa o **mesmo** `ReconcileReal` e os Job Objects ainda retidos pelo supervisor. O teste observa se `OldRecoveredNoReady` é aceito embora NEW tenha executado.
5. Em caso de aceitação insegura, o resultado é marcado `COUNTEREXAMPLE_REPRODUCED_LAB_ONLY`, com `unsafe_recovery_after_real_new=true`. **Esta classificação não é `PASS_RECOVERY_LAB_ONLY` nem `PASS_CONTAINMENT`.** O workflow exige 3 recuperações benignas, 16 contenções e 1 contraexemplo adversarial com evidência de falha.

**Importante:** o assert desta primeira versão do ensaio **espera detectar o comportamento vulnerável**; portanto, eventual CI verde demonstrará reprodução do **defeito de autoridade**, e nunca segurança do reconciliador. Se o ensaio falhar, inspecionar a causa; não afirmar que a propriedade de segurança foi comprovada por ausência de reprodução.

## Status de verificação e decisão

A API disponível permitiu confirmar que os três commits e os três arquivos foram publicados na branch, mas não permitiu obter uma nova execução `push` do GitHub Actions: a consulta de workflow pelo commit vinculada ao conector cobre somente alguns eventos de PR, e retornou vazio. **Não há número de nova CI, job, sucesso nem evidência Windows verificados nesta entrega**. Não inventar números nem promover o resultado a reprodução empírica.

Nenhuma correção funcional de `ReconcileReal` foi feita até obter o resultado do cenário. O código experimental ainda pode aceitar recuperação em presença de histórico `old/1` localmente restaurado. Este é precisamente o risco sob investigação. A solução **não** será inferir `no-launch` de `old/1`, hashes do pacote, Job vazio ou diário same-user. Se o teste reproduzir, avaliar como medida mínima o bloqueio conservador de TODA recuperação OLD/1 que dependa de um histórico local reversível sob o modelo de adulteração same-user; tal medida reclassificaria os 3 `PASS_RECOVERY_LAB_ONLY` como evidência benigna sem autorização de rollback hostil. Exigir novo ensaio Windows com os mesmos cortes antes de afirmar correção.

Fase 1 **ABERTA**, `READY=false`, G1–G8 **BLOCKED 0/8**. Sem power-loss, reboot, recuperação fria, quiescência de brokers externos ou READY operacional provados. Sem WORK, merge, release, deploy, mudanças em `main`, PR #65 Draft, BETA .66, infraestrutura, credenciais, telemetria ou PCs pessoais.

## Handoff seguro

1. Confirmar branch HEAD, localizar o workflow `gf-autoatualizador-fase1-real-lab.yml` em Actions e conferir a execução de `6db4a68ea83cb45ffc031c24617c4bf790b9540b` (ou indicar explicitamente que não foi disparada).
2. Capturar run/job/log `GF_PHASE1_RECOVERY_CASE` / `GF_PHASE1_RECOVERY_REPORT` do cenário `same-user-full-state-rollback-after-new`.
3. Caso confirmado `unsafe_recovery_after_real_new=true`, registrar falha e executar patch **fail-closed** com novo teste que exija `UnknownBlocked`, preservando o registro de contraprova em CI histórica. Caso não confirmado, investigar bloqueio de execução/teste sem declarar segurança.
4. Nenhum resultado do cenário autoriza encerrar Fase 1, promover READY, liberar G1–G8 ou alterar o produto.
