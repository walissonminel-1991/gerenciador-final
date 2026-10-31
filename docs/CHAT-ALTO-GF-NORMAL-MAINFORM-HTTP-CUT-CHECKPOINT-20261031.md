# CHAT ALTO — GF OLD: abertura NORMAL sobrevive à desconexão HTTP do ZIP NEW

**Data:** 31/10/2026. **Estado:** prova de interface concluída em Windows GitHub Actions descartável; **somente laboratório**. **READY=false**. Sem atualização operacional de usuário.

## Identificação verificável

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental anterior: `a1ebbe4d2ddd336433f3cf128d24f4d5643183d7`.
- Base técnica já aprovada: `4766625cf320115593fa4fe6503e7a4ecd78b402`; CI [41203693345](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41203693345).
- Implementação da prova NORMAL: `7d738c7b22b1da26ae93ff72e4d2e99fcf60d65c`.
- Correção pontual de sintaxe do observador: **`6e90bc427865f2239dca45822504c9367d32380d`**.
- **CI Windows aprovada:** [41204734164](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41204734164), job **124547404145**, `completed/success`. A prova NORMAL foi executada com sucesso, depois dos controles anteriores.
- CI inicial [41204510356](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41204510356): **falhou apenas na sintaxe Python** do observador, antes do teste da janela. Não foi falha de atualização. Corrigido na revisão acima.

## Objetivo exclusivo e resultado

**COMPROVADO NO LABORATÓRIO:** na abertura normal do executável real de laboratório GF OLD `5.13.11.65`, com WinForms `MainForm.Shown`, sem argumento `--tray` e **sem chamar `ShowWindow` no observador**, o mesmo downloader `GfOldDraftAcquire.Run` / `.NET HttpClient` iniciou a transferência, recebeu resposta HTTP 200 de `127.0.0.1` para o ZIP NEW real autenticado da Draft e encontrou uma desconexão TCP real induzida pelo servidor local após **1.048.576 bytes** de `57.695.630` anunciados. Depois do erro `asset_download`, o OLD permaneceu vivo com janela própria visível e responsiva; seu inventário tinha aplicativos e a pesquisa read-only mostrou estado sem correspondência e retornou à lista.

A comprovação NÃO se baseia só em PID, HWND ou recibo gerado pelo próprio aplicativo: o observador reutilizado verificou identidade da imagem de processo, HWND pertencente à instância, desktop de entrada interativo, `message_pump_ping`, inventário não vazio e transição real de pesquisa. Nenhum botão de manutenção foi acionado.

### Evidências literais da CI aprovada

```text
GF_E2E_REAL_HTTP_CONNECTION_CUT_PASS pinned_zip=true declared=57695630 sent=1048576 received=1048576 dotnet_httpclient=true old_exit=8 L18_not_invoked=true READY=false
GF_E2E_MAINFORM_HTTP_CUT_HEALTH_PASS tcp_disconnect=REAL_LOOPBACK mainform=VISIBLE+RESPONSIVE inventory=NONZERO search=EMPTY+RESTORED GF_OLD_still_running=true NEW_executed=false L18_not_invoked=true READY=false
GF_E2E_NORMAL_MAINFORM_HTTP_CUT_HEALTH_PASS tcp_disconnect=REAL_LOOPBACK mainform=VISIBLE+RESPONSIVE inventory=NONZERO search=EMPTY+RESTORED GF_OLD_still_running=true NEW_executed=false L18_not_invoked=true READY=false
GF_E2E_NORMAL_MAINFORM_HTTP_CUT_INDEPENDENT_PASS old_files=284 new_executed=false canary_unchanged=true L18_not_invoked=true READY=false
GF_E2E_NORMAL_MAINFORM_HTTP_CUT_PASS original_mainform_shown=true visible_after_failure=true inventory_search_healthy=true received_bytes=1048576 old_bytes_unchanged=true new_executed=false READY=false
```

A CI anterior também revalidou a contenção preexistente entre renomeações: `GF_E2E_PHYSICAL_GAP_EXACTLY_BETWEEN_MOVES old_files=284 new_files=284 original_path=ABSENT`, e os seis negativos de integridade: `selector=new/2 rollback=false cleanup=false READY=false`. O OLD original e sua cópia isolada permaneceram iguais ao inventário pré-teste de **284 arquivos**, e o canário, o seletor e as evidências de ausência de execução de NEW foram conferidos independentemente.

## Separação explícita do consentimento / primeira execução

A CI compila `GerenciadorAplicativos.csproj` com `-p:GfBetaProfile=local -p:GfE2eLab=true`. O perfil `GF_BETA_LOCAL` define `BetaRuntimePolicy.AllowsCollaborativeDiagnostics=false`; o coletor fixa consentimento remoto como recusado, sem solicitar um consentimento remoto na primeira abertura. O observador NORMAL exigiu ausência prévia e posterior do arquivo `%LOCALAPPDATA%\GerenciadorFinal\Privacidade\consentimento-v1.json`, inspecionou a ausência de diálogo visível intitulado `Privacidade e diagnóstico colaborativo` pertencente ao processo e não respondeu nem escreveu consentimento.

Um diálogo de consentimento inesperado recebe erro específico `NORMAL_FIRST_RUN_CONSENT_MODAL_PRESENT_NOT_HTTP_FAILURE` ou `NORMAL_FIRST_RUN_CONSENT_MODAL_OR_PREFERENCE_CHANGED`: ele NÃO seria classificado como falha do downloader. Esta prova **não testa** o consentimento da BETA conectada/interna, que tem outra política. A observação é de um título conhecido, não auditoria universal de toda janela modal possível.

## Mudanças técnicas mínimas nesta etapa

1. `Program.cs`, sob `#if GF_E2E_LAB`: marcador separado `gf-e2e-http-normal-startup-shown.json` para a prova normal.
2. `GfOldDraftAcquire.cs.txt`: diretório e recibo isolados `gf-e2e-http-normal-download` e `gf-e2e-http-normal-failure.json`, mantendo o downloader existente e guardas de não handoff.
3. `http_cut_gui_observer.py`: opção `--normal` lança a mesma cópia OLD sem argumentos; **não** chama `ShowWindow` nesse modo, verifica janela nativamente visível, pesquisa, consentimento e preservação. A antiga prova `--tray` permanece.
4. `http_cut_normal_negative.ps1`: reusa o servidor `http_cut_server.py`, ZIP autenticado e observador Win32; copia e revalida os 284 arquivos OLD, executa a prova normal no `RUNNER_TEMP` e confere OLD, canário, seletor e ausência de NEW/retry/rollback/limpeza/READY.
5. `.github/workflows/gf-github-draft-e2e.yml`: um passo adicional após a prova anterior e inclusão do novo script no filtro da workflow. O reparo `6e90bc4` adicionou apenas concatenação explícita a uma impressão Python (sem alterar a lógica gráfica ou HTTP).

## Pacote privado e limites de segurança

- Draft **429695537**, tag `gf-e2e-c5057bbbaf1d`: `draft=true`, `published_at=null`, cinco assets ao final da CI.
- ZIP NEW: asset **694233137**, `gf-e2e-new-win-x64.zip`, 57.695.630 bytes, SHA-256 fixado `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto: asset **694233139**, `GF-LAB-REAL-PACKAGE-MANIFEST.json`, 72.618 bytes, SHA-256 fixado `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- A CI atualizou exclusivamente os **três roles TUF de TESTE** da mesma Draft, mantendo IDs do ZIP e manifesto; não publicou release.
- Token GitHub efêmero usado apenas na consulta HTTPS da Draft; o servidor de loopback não recebeu Bearer. Não houve envio remoto de diagnóstico da BETA LOCAL.

**Não comprovado:** desconexão da CDN GitHub pública, recuperação transacional após falhas, rollback seguro, durabilidade sob energia/reboot, interoperabilidade/TOCTOU entre usuários, H1/H2/H3, consentimento da edição conectada nem atualização automática completa pronta para usuário. O `OLD` deste teste é executável de laboratório com flags `GF_E2E_LAB`, em cópia isolada após experimento L18; não é a instalação do usuário. `NEW` não foi executado durante o negativo. `HOLD/UnknownBlocked` permanece contenção, sem promoção a READY.

**Preservados:** `main`, PR #65 Draft, BETA .66, Cloudflare, infraestrutura externa, PCs FORTE/FRACO e produção. Não houve merge, deploy, release pública ou uso de WORK.

## Ponto de continuidade autorizado

Esta **única prova de continuidade da interface na abertura normal está encerrada**. Não repetir provas já aprovadas sem motivo. O objetivo geral permanece atualizar o GF real com segurança; qualquer próximo passo deve escolher UMA lacuna verificável do caminho operacional, reutilizando os componentes existentes, sem inventar arquitetura paralela e sem liberar recuperação, rollback, limpeza, retry ou `READY` por inferência.
