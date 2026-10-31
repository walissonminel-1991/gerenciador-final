# CHAT ALTO — GF OLD original sai; MESMO L18 seleciona e observa NEW/2

Data: **2026-10-31**. **Prova experimental Windows descartável.** Não é distribuição, instalação de produção, READY, nem recuperação autorizada.

## Identidade congelada

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch única autorizada: `experimento-g5-ipc-chat-alto-20261029`.
- **Commit técnico comprovado:** `317b3650fb7c66671297d5dd741707bdb0f62a35`.
- **CI Windows:** [run 41171632398](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41171632398), job `124450625694`, `completed/success` no SHA acima.
- Release já existente: ID `429695537`, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`.
- ZIP NEW **original**: `gf-e2e-new-win-x64.zip`, asset ID `694233137`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto **original**: `GF-LAB-REAL-PACKAGE-MANIFEST.json`, asset ID `694233139`, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Apenas os três roles TUF de **TESTE** foram renovados na Draft. Sem alteração no ZIP/manifesto original e sem publicação.

## O que mudou — exclusivamente laboratório

1. `Program.cs`, somente com `#if GF_E2E_LAB`: o GF OLD original recebe a barreira `gf-e2e-selected-new-ready.json` após o mesmo L18 fixar `new/2` com fence. A própria janela OLD só solicita fechamento normal depois da barreira e permanece sujeita ao `FormClosing` existente. `FormClosed` e retorno de `Application.Run` permanecem verificáveis, sem bypass, kill ou popup forçado.
2. `GfOldDraftAcquire.cs.txt`: depois do download HTTPS e Python-TUF de TESTE, o GF OLD inicia o mesmo `GF.Updater.M3.L18Tests.exe` em modo `--real-handoff-focused`, sem lhe repassar `GH_TOKEN` nem `GITHUB_TOKEN`. O OLD aguarda apenas a barreira `new/2`; o processo L18 continua vivo após o OLD sair. Os executáveis GF nascidos do L18 não retomam automaticamente o downloader.
3. `Phase1Recovery.cs.txt`: **o mesmo `L18Coordinator.RealOwner`** recebe o PID original apenas para **adquirir imediatamente um handle de kernel** e conferir o caminho do executável OLD. Não usa PID isolado como autorização. Executa os controles existentes: `BeginCore`, `WriteRealSelector old/1`, observação OLD, `WriteRealSelector new/2`, fence antes de execução, lançamento registrado em journal, `SelectedRealPath`, pins e verificação de manifesto. Só depois do NEW/2 ter sido fixado, o owner aguarda no **handle retido** a saída de OLD com exit code 0 e os eventos WinForms de fechamento. Depois revalida `new/2`, bytes, identidade e TUF antes de chamar o observador `--recovery-observe` **já existente**, que lança NEW a partir do seletor `new/2`, testa sua UI real read-only por uma sessão delimitada e fecha o filho normalmente. A transação continua em `UnknownBlocked`.
4. `Phase1RecoveryTests.cs.txt`: modo focalizado `--real-handoff-focused` chama o mesmo `Scenario("handoff")` e o mesmo `RealOwner`/L18, seus Jobs kernel e reconciliador. Verifica `new/2`, fence, OLD/NEW/canário preservados, observações de ambos executáveis reais, fechamento gracioso, seleção e replay `UnknownBlocked`, sem READY; emite relatórios `gf-real-l18-selected-new-handoff/1` e `gf-e2e-old-owns-l18-handoff/3`. Os 29 cenários antigos seguem inalterados e continuam disponíveis.
5. `.github/workflows/gf-github-draft-e2e.yml`: o workflow **não abre mais a sessão independente NEW** da prova anterior. Verifica saída de OLD, TUF independente e depois confere o relatório da **única sessão NEW criada pelo observador selecionado do L18**; mantém negativos de ZIP adulterado/truncado.

## Resultado da CI 41171632398 — evidência dos logs

**Todas as etapas concluídas com sucesso**, incluindo isolamento do código de laboratório, build GF OLD, TUF de TESTE, ponte L18, inicialização WinForms, observação pós-OLD pelo seletor e negativos.

Marcadores reais do job `124450625694`:

- `GF_E2E_ORIGINAL_OLD_GRACEFUL_EXIT proof=FormClosed+Application.Run+ExitCode0 forced_cleanup=false ready=false`.
- `GF_E2E_OLD_EXIT_BEFORE_SAME_L18_SELECTED_NEW selector=new/2 original_old_exit=0 READY=false`.
- `GF_E2E_OLD_INITIATED_DOWNLOAD version=5.13.11.65 draft=gf-e2e-c5057bbbaf1d dotnet=true assets=5`.
- `GF_DRAFT_AUTHENTICATED`: `downloaded_archive_sha256=3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`, `extracted_files=284`, `old_files_preserved=true`, `operational_ready=false`.
- `GF_E2E_SELECTED_NEW_BY_SAME_L18_AFTER_OLD_EXIT selector=new/2 UI=FUNCTIONAL_READONLY original_old_exit=0 containment=UnknownBlocked ready=false independent_new_launch=false`.
- `GF_DRAFT_NEGATIVES`: `modified` e `interrupted` rejeitados, `downloaded_asset_executed_on_negative=false`, `rollback_authorized=false`.

O auditor Windows observou o mesmo processo NEW nascido por seleção NEW/2, com a busca read-only funcional e fechamento gracioso depois da observação. O processo OLD original encerrou antes do início dessa observação; o C# L18 não foi encerrado junto dele.

## O que a prova NÃO demonstrou

- **NEW não permaneceu aberto indefinidamente.** O observador L18 existente mantém a sessão por tempo delimitado, verifica a UI e solicita fechamento normal. Não alegar sessão permanente instalada, atualização operacional ou continuidade após reboot.
- A seleção de `new/2` ocorreu na **instalação privada de fixture** criada pelo L18 em Windows Actions, não substituiu o executável ativo de uma instalação Windows real. O binário NEW é real e autenticado por TUF de TESTE; a instalação operativa não foi trocada.
- `UnknownBlocked` continua contenção, não recuperação, READY, autorização para rollback, limpeza ou reinício automático. `HOLD` segue obrigatório.
- H1/H2/H3, quiescência independente de efeitos externos, TOCTOU em presença de mesmo usuário, prova física power loss e autenticação de produção continuam **não demonstradas**.
- A Draft privada exige token efêmero exclusivo da CI; chaves TUF públicas de TESTE **não** constituem autoridade de atualização pública. Nada de credenciais para usuários.
- O negativo `interrupted` é ZIP truncado, não queda de rede HTTP real. O item positivo customizado da lista ainda não foi demonstrado.

## Próximo objetivo EXCLUSIVO

Preparar a transição da **instalação executável REAL** no Windows descartável reutilizando o **mesmo seletor/diário/pins do L18** em vez de apenas uma cópia de fixture, com rollback/recovery **bloqueados** e provas explícitas de preservação OLD/NEW/dados pessoais-canário. Não promover para produção e não inventar nova arquitetura. Antes de uma tentativa com efeitos persistentes, escolher controles que garantam que o coordenador não encerrará nem perderá a prova do NEW selecionado. Se qualquer autoridade ficar inconclusiva, parar com HOLD; não repetir os 29 testes salvo regressão concreta.

**Restrições:** `main`, PR #65 Draft, BETA .66, Cloudflare, avaliações pessoais, releases públicos, deploys, merges, instalação em PC pessoal, rollback automático, chaves/tokens de produção: **não tocar**. Nenhum WORK foi usado. Continuar com botões rápidos e checkpoint técnico verificável.
