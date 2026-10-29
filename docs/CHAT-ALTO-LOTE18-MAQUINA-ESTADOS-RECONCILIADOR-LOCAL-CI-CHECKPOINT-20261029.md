# GERENCIADOR FINAL — CHAT ALTO — LOTE 18 — CHECKPOINT TÉCNICO
**Data:** 2026-10-29
**Status:** implementado e validado SOMENTE COMO PROTÓTIPO INERTE; **G1–G8 BLOCKED (0/8)**.

## Baseline e proteção
- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch: `experimento-g5-ipc-chat-alto-20261029`.
- Baseline L17 verificada: `a37a12d2e79e9c413e52f638e2943544ab8db136`.
- Commit técnico validado: `4ac782a2871e818b6b09c94505362211eb24d5c7`.
- `main` preservada: `410d65e23546976db456337c71e4705f10217d36`.
- PR #65 permanece Draft, aberta, sem merge: `ee73a850f6d7454ed9e81ce0f0dd35c5b8b88fcc`.
- BETA .66, Cloudflare, Worker/D1, domínio, consentimento, credenciais e PCs pessoais: **sem ações ou mudanças**.
- Nenhum WORK utilizado, merge, release, deploy, helper/serviço instalado, chave operacional, execução de GF ou autorização de gate.

## Componentes novos isolados
1. `prototipos/autoatualizador-seguro/m3-l18-reconciler/L18Coordinator.cs.txt`: máquina de fases persistentes e reconciliador de observação local. Proíbe sucesso a partir somente de `terminal.json`; verifica conteúdo Stage11/Stage3 TUF de teste via L15, ZIP e staged exatos sob WindowsFileGuard, identidade NTFS do staged, seletor real, registro de exit code **observado pelo próprio componente** e ausência das pendências conhecidas. O estado de êxito é exclusivamente `LocalConsistentObserved`, nunca READY do GF.
2. `.../L18Tests.cs.txt`: suíte de processos filhos Windows e casos de adulteração, timeout, morte/reentrada e canários pessoais fictícios.
3. `.../L18Reconciler.csproj`: .NET 10, runner isolado com fontes compartilhadas L16/L15/Stage11 de fixture, sem compilar no aplicativo principal.
4. `.../README.md`: modelo de confiança, estados, garantias e riscos expressamente não oferecidos.
5. `.github/workflows/autoatualizador-m3-l18-reconciler-inerte.yml`: CI Windows 2022 sem credenciais persistentes, TUF de laboratório fixado, compilação SDK 10.0.402, checagem de resultados e ausência de vazamento para a compilação do GF.

Nenhum arquivo dos Lotes 15–17 nem código operacional GF foi modificado; somente cinco arquivos novos da frente L18. Os testes L16/L17 anteriores continuam como baseline de regressão. A CI L18 recompila/reutiliza componentes compartilhados para cenários afetados, sem exigir repetição integral de testes históricos não alterados.

## Máquina de estados efetiva
A persistência do L18 utiliza sequência de eventos `FileMode.CreateNew`, `FileOptions.WriteThrough` e `Flush(true)`, com conferência do formato, sequência, tx, digest do alvo assinado de teste, digest da baseline OLD e identidade NTFS staged. Inclui fases:
- `old-verified`, `authorizing`, `preparing`, `staged-verified`, `intent-durable`,
- `switching`, `owner-launched`, `owner-exited`, `switched-unconfirmed`,
- `reconciling`, `old-observed`, `new-observed`, `failed-known`, `timeout` e `unknown`.
`NO_TRANSACTION` é representado pela ausência de eventos, que deve ser analisada, não convertida em sucesso automaticamente.

**Limite deliberado:** o staged foi construído pelo preparador inerte L16 antes de L18 começar; `preparing` no L18 é apenas pré-checagem e classificação daquela fixture pré-existente, **não uma instalação de pacote real**. O PID persistido como diagnóstico é não autoritativo. Handles e journaling locais não garantem resistência contra escritor de mesmo usuário ou power loss.

`Busy` vem do lease ocupado, não é fase terminal. O próprio coordenador dispara o proprietário de laboratório, observa seu exit code e grava `timeout` + incerteza se WaitAsync vencer; jamais trata expiração como término de I/O. A recuperação tem lease exclusivo, confronta fatos locais e exige `owner-exited` previamente persistido para reconhecer NEW. Se o coordenador cai antes desse registro, permanece UNKNOWN, mesmo que o proprietário tenha realmente acabado.

Uma nova avaliação após `failed-known` e OLD consistente produz `OldVerified` sem reexecutar escrita. Registros locais podem ser reescritos por um invasor que controla todo o domínio; esse limite não está resolvido.

## Evidência objetiva — CI Windows definitiva
**Execução final:** https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40960143651
**Commit testado:** `4ac782a2871e818b6b09c94505362211eb24d5c7`.
**Conclusão:** `success`; compilação **0 Warning(s), 0 Error(s)**; `GF_L18_FINAL_REPORT` passou **7/7**, falhou **0/7**; sete linhas `PASS L18-` e zero `FAIL L18-`.

Cobertura:
- L18-01: NEW honesto e observação local repetida idempotente; canário separado preservado.
- L18-02: terminal local fabricado após morte sem `owner-exited` não autoriza NEW.
- L18-03: recibo contraditório, inclusive depois de observação prévia válida, bloqueia reconhecimento.
- L18-04: TIMEOUT persistido **pelo coordenador**, BUSY com dono ativo, conclusão tardia não remove UNKNOWN.
- L18-05: kill durante `RECONCILING`, seguido por reentrada de novo processo e repetição idempotente.
- L18-06: falha conhecida antes de seleção, seguida de reclassificação verificável para OLD sem nova troca.
- L18-07: symlink/reparse point em nome do diário aponta para canário fora do sandbox; pré-checagem recusa sem alterar o arquivo externo.

Evidências das compilações preliminares também aprovadas:
- https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40959248035 — 7/7 no commit inicial.
- https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40959614548 — 7/7 após fortalecer canário com symlink.
O resultado FINAL é a execução `40960143651`, do commit técnico atual.

## Matriz demonstrado / hipótese / pendente
**Demonstrado em fixture:** reconciliação por TUF test-only + observação NTFS + processo encerrado + recibo auxiliar, sem sucesso só por recibo; BUSY/TIMEOUT/UNKNOWN dentro de coordenador; morte durante recuperação e reentrada; prevenção controlada de reparse externo; preservação de OLD, canários e idempotência nos cenários ensaiados.

**Premissas:** proprietário e coordenador são componentes de teste cooperativos; `owner-exited` e os demais eventos representam observações locais sem autoridade criptográfica independente. A revalidação física usa NTFS do GitHub Windows runner. Não há prova geral de que todo I/O kernel foi concluído após a saída do processo.

**Pendências que não podem ser promovidas a READY:**
- G1: frescor independente/antirrollback após restauração integral, relógio e máquina; risco não aceito formalmente.
- G2: qualificação do perfil TUF e manutenção D6-02; interop não integralmente homologada.
- G3/G4: pacote GF verdadeiro assinado/autorizado, validação de PE e política operacional.
- G5/G6: IPC, GFs em execução, ciclo real, READY e late-load.
- G7: custódia e bootstrap de chaves reais.
- G8: timeout global e I/O bloqueante kernel/driver, power loss, reparse hostil e NTFS físico em ambiente não descartável.
- TOCTOU universal no seletor/scratch da fronteira L16 e atacante de mesmo usuário continuam riscos não fechados.

A implementação não traz autoridade independente, cadastro, TPM, serviço privilegiado, testemunha remota nem promessas de proteção de dados pessoais do PC real.

## Critério final
**LOTE 18 CONCLUÍDO COMO EXPERIMENTO INERTE (7/7 CI WINDOWS), NÃO COMO HOMOLOGAÇÃO OPERACIONAL.**
**G1–G8 permanecem BLOCKED (0/8); nenhum aceite de risco foi presumido.**
