# CHAT ALTO — GF OLD com janela NORMAL passa pela seleção NEW/2 e sai graciosamente sob L18

**Data:** 31/10/2026. **Estado:** prova funcional única aprovada em Windows GitHub Actions descartável. **LABORATÓRIO; READY=false. Não homologado.**

## Referências congeladas

- Repositório `walissonminel-1991/gerenciador-final`, branch exclusiva `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de partida `81c719ee2250706432a31d1566beec84304a803b`.
- **Commit técnico:** `0bd67d1668b94034a509474e18d1b53ee26ed7cf`. Um único arquivo alterado: `.github/workflows/gf-github-draft-e2e.yml`, com 68 adições e 1 remoção.
- **CI Windows:** [41208015676](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41208015676), job **124557002902**, `completed/success` no SHA técnico acima.
- Checkpoint anterior: `docs/CHAT-ALTO-GF-L18-UNKNOWN-DENY-OLD-CLOSE-CHECKPOINT-20261031.md`, `81c719ee2250706432a31d1566beec84304a803b`.

## UMA lacuna escolhida e comprovada

**Antes:** a única passagem existente em que o ORIGINAL OLD aguardava a barreira do **mesmo L18** `selected NEW/2` antes de se encerrar havia sido executada abrindo o GF com argumento `--tray`. As provas posteriores de interface NORMAL haviam demonstrado download, autenticação e contenção `UnknownBlocked`, mas **não** a transição positiva da janela NORMAL pelo fechamento gracioso após o fence.

**Agora:** a etapa inicial já existente `Startup real MainForm.Shown do GF OLD inicia download autonomo de Draft` da MESMA workflow iniciou o ORIGINAL `GerenciadorFinal.exe` de laboratório **sem argumentos**, em janela NORMAL. Antes da continuação L18 e antes de qualquer fechamento, um observador Python **externo ao GF** reutilizou `win32_process_observation.py` e `consent_dialogs_for_pid` para confirmar:

- janela WinForms própria **nativamente visível**, sem `ShowWindow` nem `--tray`;
- imagem exata do processo conferida pelo caminho do executável, não apenas PID/HWND;
- `message_pump_ping` real responsivo, desktop de entrada interativo, ausência de diálogo visível de consentimento BETA conectado;
- evidência isolada `gf-e2e-old-normal-preclose-win32.json` criada apenas depois da observação independente, antes do fechamento.

A mesma transferência .NET real e verificador TUF existentes obtiveram/autenticaram o pacote Draft; o L18 já existente cercou a seleção `NEW/2`, escreveu o seu fence de possível execução NEW e coordenou a saída graciosa do OLD. A workflow *reutilizou as verificações preexistentes* de `gf-e2e-selected-new-ready.json`, de `FormClosed`, retorno de `Application.Run`, código de saída `0`, e ordem do fechamento perante a observação subsequente do NEW em laboratório. Não se adicionou outro motor de atualização ou outro cenário de troca.

**Evidências literais de logs da CI aprovada:**

```text
GF_E2E_OLD_LAB_PRESTART_BASELINE files=284
GF_E2E_NORMAL_OLD_PRE_CLOSE_WIN32_PASS mainform=natively_visible+responsive original_old=true no_tray=true no_showwindow=true READY=false
GF_E2E_ORIGINAL_OLD_GRACEFUL_EXIT proof=FormClosed+Application.Run+ExitCode0 forced_cleanup=false ready=false
GF_E2E_OLD_EXIT_BEFORE_SAME_L18_SELECTED_NEW selector=new/2 original_old_exit=0 READY=false
GF_E2E_NORMAL_MAINFORM_SELECTED_CLOSE_PASS native_ui_before_close=true l18_selector=new/2 launch_fence=true FormClosed=true MessageLoopExited=true OLD_exit0=true READY=false
GF_E2E_PHYSICAL_GAP_EXACTLY_BETWEEN_MOVES old_files=284 new_files=284 original_path=ABSENT
GF_E2E_GITHUB_NORMAL_L18_DENY_CLOSE_PASS requested=true outcome=UnknownBlocked close_denied=true old_healthy_after_denial=true NEW_not_installed=true READY=false
```

Os timestamps dos logs mostraram a observação da janela NORMAL às 14:00:45 UTC e o encerramento gracioso às 14:01:14 UTC, antes da comprovação do corte físico às 14:01:25 UTC. A comparação da identidade do processo e os recibos `FormClosed` + retorno do message loop + ExitCode 0 formam a prova de ciclo de vida; **nenhum recibo, PID, HWND ou `selected-new-ready.json` isoladamente equivale a READY ou à autorização de recuperação**.

## Escopo técnico

**Único arquivo modificado:** `.github/workflows/gf-github-draft-e2e.yml`.

1. Substituiu `Start-Process -ArgumentList '--tray'` por `Start-Process` sem argumentos no **cenário positivo já existente**; não criou um segundo teste de troca.
2. Inseriu inspeção Win32 externa, limitada à instância OLD exata no `RUNNER_TEMP`, que registra a visibilidade/identidade/reatividade antes do fechamento, sem interagir com botões nem mudar estado do GF.
3. Validou esse recibo adicional em conjunto com os **mesmos** critérios anteriores de `NEW/2`, launch fence, `FormClosed`, retorno `Application.Run` e código 0. Inseriu uma única linha de diagnóstico `GF_E2E_NORMAL_MAINFORM_SELECTED_CLOSE_PASS`.
4. Não tocou em `Program.cs`, `GfOldDraftAcquire.cs.txt`, código de L18, seletor, reconciliador, algoritmo de TUF, política de consentimento nem artefatos distribuídos.

O executável foi compilado com `GfBetaProfile=local` e `GfE2eLab=true`. O código experimental não integra o binário de distribuição normal. A prova não simula aprovação do consentimento da BETA conectada e não envia telemetria externa.

## Pacote e fronteiras de segurança

- Draft **429695537** (`gf-e2e-c5057bbbaf1d`): `draft=true`, `published_at=null`, cinco assets.
- ZIP NEW: asset **694233137**, `gf-e2e-new-win-x64.zip`, **57.695.630 bytes**, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto: asset **694233139**, `GF-LAB-REAL-PACKAGE-MANIFEST.json`, **72.618 bytes**, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- A CI existente renovou apenas três metadados TUF de **TESTE**, mantendo o ZIP/manifesto fixados. Sem publicação, release, merge ou deploy.
- A execução pós-fechamento do OLD continuou o cenário já existente de **interrupção intencional entre renomeações no runner descartável**. O caminho original ficou ausente entre movimentos, enquanto OLD permaneceu em backup e NEW em staging, com retenção dos 284 arquivos de cada conjunto e canário. O L18 manteve a classificação `UnknownBlocked/HOLD` e **não autorizou rollback, recuperação, limpeza ou retry**. Essa interrupção não foi feita nos PCs pessoais.
- A prova subsequente de `UnknownBlocked` com solicitação explícita de fechamento recusada manteve-se aprovada, preservando a regra negativa anterior.

**Limites fundamentais:** esta é uma demonstração positiva da **sequência de passagem do processo OLD aberto normalmente para o L18 em um Windows descartável**, não uma prova de autorização operacional de atualização de usuários. O fence e os relatórios do mesmo ambiente não resolvem H1/H2/H3, TOCTOU, confiança de produção, durabilidade em perda de energia, efeitos externos de NEW, recuperação transacional, rollback seguro nem `READY`. **A troca física completa e lançamento do NEW instalado não foram homologados aqui**; o cenário deliberadamente terminou em corte/HOLD. Não extrapolar `NEW/2` como garantia de segurança definitiva.

**Preservados:** `main`, PR #65 Draft, BETA .66, Cloudflare, PCs FORTE/FRACO, infraestrutura de produção, permissões, autenticidade do ZIP/manifesto, H1/H2/H3, TOCTOU. Sem WORK.

## Continuidade disciplinada

Esta **prova de abertura NORMAL → fence L18 NEW/2 → encerramento gracioso OLD** está concluída. Não repetir o teste sem ganho. O gargalo operacional segue sendo demonstrar com autoridade independente e adequada — sem transformar `selected-new-ready.json` ou observações locais em `READY` — que a instalação de NEW e a recuperação eventualmente necessárias podem ocorrer com segurança. Escolher **uma única próxima lacuna** na branch experimental, exclusivamente em Windows GitHub Actions descartável, sem liberar recuperação, rollback, limpeza, retry, READY, merge, deploy ou publicação.
