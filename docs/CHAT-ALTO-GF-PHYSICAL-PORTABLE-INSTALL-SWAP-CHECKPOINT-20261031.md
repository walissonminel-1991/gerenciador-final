# CHAT ALTO — GF executa troca FÍSICA de instalação portátil real no Windows descartável

Data: **31/10/2026**. Missão única GERENCIADOR FINAL — autoatualização real, branch `experimento-g5-ipc-chat-alto-20261029`.

## Resultado consolidado e identidade verificável

**CI principal:** [41195636761](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41195636761), job `124521125451`, commit técnico **`7dfd784cdc38a6aea4b2a4654c2f152322754a2d`**. GitHub Actions Windows: **completed/success**. Todos os passos principais concluídos com sucesso, incluindo startup real, TUF independente, L18, comparação física de arquivos e negativos.

**Draft protegida:** Release ID `429695537`, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`, 5 assets. ZIP e manifesto NÃO foram substituídos:
- NEW `gf-e2e-new-win-x64.zip`: asset ID `694233137`, **57.695.630 bytes**, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto `GF-LAB-REAL-PACKAGE-MANIFEST.json`: asset ID `694233139`, **72.618 bytes**, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Somente `targets.json`, `snapshot.json`, `timestamp.json` de TUF **TESTE** foram renovados no Draft existente.

## O que o mesmo L18 fez

1. O GF OLD portátil **5.13.11.65**, em `RUNNER_TEMP/gf-e2e-old`, iniciou pela sua MainForm real, consultou a Draft privada com token **efêmero da CI**, baixou os 5 assets via `.NET HttpClient` e validou os 284 arquivos NEW **5.13.11.66** com Python-TUF de laboratório.
2. O próprio GF OLD chamou o **mesmo C# L18** `GF.Updater.M3.L18Tests.exe`, sem repassar token. L18 manteve os mesmos Jobs, seletor, diario transacional, pins e reconciliador, iniciou o `old/1` de teste, fixou `new/2` e registrou o fence antes de qualquer execução da nova versão.
3. O L18 reteve **handle de processo real do OLD original**, antes da barreira. O OLD pediu fechamento através do próprio WinForms; L18 esperou no handle até `ExitCode=0`, após eventos `FormClosed` e retorno de `Application.Run`. Não confundiu mero PID com identidade nem limpeza por kill com desligamento normal.
4. Sob **`GF_E2E_LIVE_INSTALL_SWAP=1`, exclusivamente em GitHub Actions Windows**, e com o processo OLD comprovadamente encerrado, o mesmo `RealOwner` do L18 comparou integralmente os conteúdos OLD/NEW com seu contexto e pins verificados, exigiu diretórios sem reparse/extras e realizou **duas renomeações de diretório**:
   - `RUNNER_TEMP/gf-e2e-old` (executável OLD original) → `RUNNER_TEMP/gf-e2e-old-preserved` (**OLD intacto**);
   - `RUNNER_TEMP/gf-e2e-authenticated-new` (NEW TUF autenticado) → `RUNNER_TEMP/gf-e2e-old` (**NEW no caminho executável original**).
5. O mesmo observador Python acionado por `RunRealObserver` do L18, após reler a seleção **`new/2`** e validar o NEW físico, iniciou o GF a partir do **caminho portátil original**, verificou a interface funcional WinForms read-only e encerrou sua sessão de teste normalmente. O programa não foi lançado por um caminho independente escolhido pelo workflow.
6. A CI verificou independentemente os bytes do diretório NEW contra a **assinatura do manifesto imutável da Draft**, confirmou todos os arquivos OLD arquivados contra inventário completo capturado **antes do startup** e conferiu o arquivo-canário em diretório privado separado. Isso não acrescentou um segundo seletor nem autoriza rollback/recovery.

## Provas observadas nos logs da CI 41195636761

- `GF_E2E_OLD_LAB_PRESTART_BASELINE files=284`: inventário específico da compilação OLD de laboratório, capturado antes de executar, usado apenas para prova de preservação, **não** como autoridade de segurança.
- `GF_E2E_ORIGINAL_OLD_GRACEFUL_EXIT proof=FormClosed+Application.Run+ExitCode0 forced_cleanup=false ready=false`.
- `GF_E2E_OLD_EXIT_BEFORE_SAME_L18_SELECTED_NEW selector=new/2 original_old_exit=0 READY=false`.
- `GF_DRAFT_AUTHENTICATED`: `extracted_files=284`, ZIP SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`, manifesto original autenticado, `operational_ready=false`.
- `GF_E2E_INSTALLED_NEW_BYTES_IN_ORIGINAL_PORTABLE_PATH verified=true`.
- `GF_E2E_OLD_BACKUP_AND_DATA_PRESERVED verified=true`.
- `GF_E2E_REAL_PORTABLE_INSTALL_UPDATED_BY_SAME_L18 selector=new/2 original_install=NEW backup=OLD data=UNCHANGED UI=FUNCTIONAL_READONLY HOLD=UnknownBlocked READY=false`.
- `GF_DRAFT_NEGATIVES`: `modified` e `interrupted` (ZIP truncado, NÃO queda HTTP ao vivo) **rejeitados**, `downloaded_asset_executed_on_negative=false`, `rollback_authorized=false`.

## Mudanças específicas nesta etapa

Commit técnico inicial `126760295612c7e7a3c098a9ba6dbd34b7148c7f`:
- `Phase1Recovery.cs.txt`: `SwapRealInstalledPackageAfterOldExit` dentro do MESMO `L18Coordinator`; somente diretórios privados sob `RUNNER_TEMP`, após seleção, fence, saída de OLD e validação de dados. Nunca restaura automaticamente.
- `Phase1RecoveryTests.cs.txt`: novo foco `--real-install-focused` reutiliza `Scenario("handoff-install")` do L18, o mesmo ReconcileReal, e exige OLD/NEW/canário preservados.
- `phase1_lab.py`: **mesmo** observador lê o seletor NEW/2 e executa o binário do caminho físico atualizado, com validação da versão instalada, conteúdo e dados; sem novo launcher independente.
- `GfOldDraftAcquire.cs.txt`: seleciona foco experimental pelo `GF_E2E_LIVE_INSTALL_SWAP=1` (compilado somente em `GF_E2E_LAB`).
- `.github/workflows/gf-github-draft-e2e.yml`: cria canário real separado, checa NEW instalado e OLD arquivado, sempre sem publicação, merge ou deploy.

**Primeira execução:** CI [41195335850](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41195335850), job `124520256482`, terminou **failure** em startup, com `gf-e2e-old-blocked/1 reason=IOException`. O diagnóstico original ocultava a subetapa: **a causa exata NÃO foi comprovada**. Nenhum SUCCESS/READY foi declarado nessa execução. A CI fez cleanup de somente seu processo de teste, sem considerar esse encerramento prova de desligamento normal.

**Correção instrumental e da prova:** commit técnico aprovado `7dfd784cdc38a6aea4b2a4654c2f152322754a2d`:
- `GfOldDraftAcquire.cs.txt`: acrescenta diagnósticos de fase em um conjunto fixo (`draft_metadata`, `asset_download`, `acquisition_report`, `tuf_verify`, `l18_selector_wait` etc.), mais `io_hresult` sem URL, stack, bearer ou token, preservando o fail-closed. Isso melhora diagnóstico, **não confirma o motivo original do IOException**.
- Workflow: o manifesto imutável da Draft corresponde ao GF OLD distribuído, mas OLD de laboratório `GF_E2E_LAB` tem bytes diferentes; portanto, a verificação independente de **preservação do OLD de laboratório** passou a comparar o diretório arquivado com um inventário calculado antes de seu startup. O NEW continua verificado contra o manifesto TUF imutável da Draft.
- CI `41195636761` executou integralmente essa configuração com SUCCESS.

## Limites e segurança (NÃO PRODUÇÃO)

- O experimento **realizou movimento físico dos diretórios de execução no runner Windows**, mas o caminho é efêmero, privado e específico de laboratório. Não atualizou uma instalação pessoal, não instalou release pública, não concluiu a atualização permanente para usuários reais e não demonstrou persistência após reboot.
- O L18 preserva **`UnknownBlocked` / `HOLD`** como contenção: não permite rollback, limpeza, reexecução automática, nem `READY` por PID, HWND, recibo local ou sucesso da UI.
- Uma falha entre as duas renomeações pode deixar o caminho original vazio; os conteúdos preservados permanecem para diagnóstico e **não há recuperação automática comprovada**. Falhas de energia, durabilidade de diretório NTFS, H1/H2/H3, TOCTOU contra mesmo usuário, efeitos externos e interrupção HTTP real continuam sem prova de segurança.
- Assinaturas TUF são **somente de TESTE** (chaves públicas); Draft exige token efêmero fornecido pela CI, nunca embutido em programa distribuído. Arquivos-canário demonstram preservação de dado INERTE de teste, não estabelecem garantias para dados pessoais reais.
- O observador mantém a sessão NEW por tempo delimitado e a fecha. Não se demonstrou sessão persistente, recuperação pós-crash ou READY.
- `main`, PR #65 Draft, BETA .66, Cloudflare, contas, credenciais persistentes, máquinas PC FORTE/FRACO, releases públicas, merge, deploy, rollback automático: **não tocar**. WORK não foi utilizado.

## Próxima missão única

Demonstrar a **persistência da versão executável selecionada após saída do coordenador** dentro do mesmo Windows descartável, sem relançamentos não autorizados, sem simular READY e sem apagar backup ou canário. Depois avaliar o caso de interrupção **entre as duas renomeações** como contenção explícita, sem tentar rollback; escolher UMA dessas provas no próximo passo, sem ampliar arquitetura. Continuar guiado por evidências e registrar checkpoint + CI, mantendo botões de avanço. 
