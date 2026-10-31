# CHAT ALTO — GF NEW persiste íntegro e selecionado após encerramento do L18 original

**Data:** 31/10/2026. **Status da prova:** SUCCESS somente no Windows descartável, sem READY e sem recuperação autorizada.

## Identidade congelada

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch de escrita exclusiva: `experimento-g5-ipc-chat-alto-20261029`.
- **Commit técnico final comprovado:** `4eed316121331b85b3bbd5ede32b568b33c5c621`.
- **GitHub Actions Windows:** [CI 41197684863](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41197684863), job `124527062877`, `completed/success` no SHA `4eed316121331b85b3bbd5ede32b568b33c5c621`. Passaram: OLD MainForm.Shown, TUF independente, transição OLD→NEW com mesmo L18, observação pós-encerramento, negativos.
- Release existente: ID `429695537`, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`, cinco assets. ZIP/manifesto permanecem imutáveis:
  - `gf-e2e-new-win-x64.zip`: ID `694233137`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
  - `GF-LAB-REAL-PACKAGE-MANIFEST.json`: ID `694233139`, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Só os três metadados TUF `targets/snapshot/timestamp` de **TESTE** são renovados na Draft. Chaves públicas de laboratório, sem autoridade de distribuição.

## Objetivo único alcançado

Demonstrar **no mesmo Windows descartável** que, depois de o GF OLD original encerrar e o **mesmo L18** instalar NEW físico no caminho portátil original, o L18 inicial também termina e a **seleção original new/2 e os 284 arquivos executáveis NEW permanecem**. Em seguida um observador estritamente dependente desse seletor relê TUF/manifesto e comprova que a instalação NEW **funciona**.

Esta prova remove a lacuna anterior de ciclo de vida do *test harness*: antes, `L16Tests.Scenario.Dispose` e o `finally` da suíte apagavam o diretório de fixture contendo seletor/journal assim que a matriz concluía. **Nenhum segundo seletor foi criado.** Na rodada atual, sob `GF_E2E_POST_OWNER_PERSISTENCE=1` em GitHub Actions, o próprio diretório usado pelo L18 é mantido até o runner descartável terminar.

## Implementação e fluxo validado

1. `L16Tests.cs.txt`: condiciona a limpeza do **único diretório da fixture real** quando seu caminho exato foi retido por `Phase1RecoveryTests` e o ambiente é GitHub Actions do repositório autorizado, sob `GF_E2E_POST_OWNER_PERSISTENCE=1`. Caminhos normais continuam descartando as fixtures. O auxiliar L14 é encerrado normalmente. Não altera lógica de produto nem executável público.
2. `Phase1RecoveryTests.cs.txt`: `Scenario("handoff-install")` mantém o mesmo root de teste, já identificado e autenticado pelo L18. Após sucesso, gera referência `gf-e2e-postexit-selected-root/1` para o **mesmo caminho no NTFS**; o seletor e o fence continuam no diretório original, e o diário persiste. Em `--real-install-focused` evita apagar a suíte somente no teste autorizado. Os outros 29 cenários e suas regras de limpeza permanecem disponíveis.
3. `GfOldDraftAcquire.cs.txt`: antes de o OLD original fechar, o processo pai registra somente PID e instante de criação do **L18 realmente iniciado**, para diagnóstico de encerramento. Esse recibo NÃO concede autoridade de recuperação e nenhum token GitHub passa a L18/Python.
4. Workflow `gf-github-draft-e2e.yml`: após a prova prévia de troca física e comparação independente de bytes, exige o término do **processo L18 original**, confrontando PID com instante inicial (recusa PID reutilizado); relê o **mesmo** `l18-real-active.fixture.json` do root mantido; exige esquema, instalação, transação e hash do manifesto compatíveis, `selected=new` e `generation=2`. Calcula hash SHA-256 do seletor antes e depois, exigindo igualdade.
5. `phase1_lab.py`: a nova observação pós-encerramento usa exatamente o **observador `--recovery-observe` já existente**, obrigatoriamente `GF_E2E_L18_OBSERVE_INSTALLED_NEW=1`, `new/2`, TUF TESTE, manifesto, arquivo-canário, instalação física no caminho original e backup OLD preservado. Executa **uma sessão curta nova**, associada à seleção, testa UI funcional WinForms (read-only) e encerra o processo filho normalmente. Nenhum mecanismo de autoatualização, seletor, recovery ou launcher de produto novo foi criado.
6. A CI conclui com a preservação de OLD e dados, duas rejeições negativas e `READY=false`. Não move, limpa nem faz rollback de arquivos no teste pós-L18.

## Evidências verificadas dos logs do job 124527062877

- `GF_E2E_ORIGINAL_OLD_GRACEFUL_EXIT proof=FormClosed+Application.Run+ExitCode0 forced_cleanup=false ready=false`.
- `GF_E2E_INSTALLED_NEW_BYTES_IN_ORIGINAL_PORTABLE_PATH verified=true`.
- `GF_E2E_OLD_BACKUP_AND_DATA_PRESERVED verified=true`.
- `GF_E2E_ORIGINAL_L18_PROCESS_EXITED original_identity_checked=true`.
- `GF_E2E_ORIGINAL_SELECTOR_PERSISTED_AFTER_L18_EXIT selected=new/2 HOLD=true READY=false`.
- `GF_E2E_POST_L18_SELECTED_NEW_FUNCTIONAL selector=new/2 install=NEW ui=READONLY_HEALTHY independent_unselected_launch=false HOLD=true READY=false`.
- `GF_E2E_POSTEXIT_PERSISTENCE_PROVEN selector=new/2 install=5.13.11.66 UI=FUNCTIONAL_READONLY original_coordinator=EXITED old_preserved=true READY=false`.
- `GF_DRAFT_NEGATIVES`: `modified` e `interrupted` (truncamento de ZIP, **não** queda HTTP real) rejeitados, `downloaded_asset_executed_on_negative=false` e `rollback_authorized=false`.

## Histórico de tentativa nesta rodada

- [CI 41197443296](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41197443296) **failure de compilação**, não chegou à troca: `CS0103` — flag `installationSwap` fora do escopo do `finally` em `Phase1RecoveryTests.cs.txt`. Corrigido movendo **somente** a declaração para o escopo externo ao `try`, commit `9501d0a22edcd5357b152e0b61cc53958e098feb`.
- O workflow da Draft não disparou automaticamente naquele commit porque seu filtro de `on.push.paths` incluía só o downloader e o próprio YAML. O commit técnico final `4eed316121331b85b3bbd5ede32b568b33c5c621` incluiu os quatro arquivos de laboratório relevantes no filtro; CI `41197684863` testou a versão final e **passou**.
- Falha histórica [CI 41195335850](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41195335850): `IOException` cuja causa exata não foi comprovada. O diagnóstico por fase/HResult segue disponível e não autoriza caracterizar aquela falha como resolvida por causa demonstrada.

## Limites críticos — nunca afirmar produção pronta

1. **Persistência provada só dentro do MESMO runner e da MESMA sessão Windows**. O novo GF fica no caminho portátil original e a seleção `new/2` permanece após **uma saída limpa do L18**, não após reinicialização do Windows, queda de energia, falha de disco ou troca de usuário.
2. A sessão NEW que o L18 iniciou no momento da troca foi **encerrada normalmente pelo observador**. O teste pós-L18 abre **outra sessão curta**, exclusivamente pelo observador que relê `new/2`; não demonstra que a primeira sessão continuou aberta, nem funcionamento indefinido.
3. Os arquivos de estado que permanecem são uma **fixture experimental privada**. Não são autoridade independente anti-rollback/TOCTOU e não constituem mecanismo instalado de produção. A observação pós-exit é prova funcional read-only, **não** READY operacional ou autorização para execução/recovery automática.
4. `UnknownBlocked/HOLD` continua sendo contenção. Nunca usar exit code, PID, janela, hash de recibo local ou a mera persistência do seletor como autoridade de rollback, limpeza ou nova tentativa operacional.
5. H1/H2/H3, TOCTOU pelo mesmo usuário, quiescência de efeitos externos, durabilidade NTFS em falha física, autenticidade TUF de produção e interrupção HTTP real continuam não demonstrados. O token de acesso à Draft é efêmero do runner, **nunca distribuível**.

## Próximo objetivo e restrições

A próxima etapa, se autorizada, é **uma única prova de contenção** para falha/interrupção **entre as duas renomeações físicas**, preservando OLD/NEW e dados-canário e verificando que o L18 permanece bloqueado sem rollback/limpeza/retry. Não retestar tudo nem iniciar arquitetura paralela. Preparar execução somente no Windows descartável.

Preservados: `main`, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, 2FA, infraestrutura privada, releases públicos, merge/deploy, chaves/tokens de produção. WORK não utilizado. Sem limpeza automática, rollback, READY ou publicação. Manter botões de continuidade.
