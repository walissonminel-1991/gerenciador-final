# CHAT ALTO — Interface real OLD continua saudável após desconexão HTTP do ZIP NEW

**31/10/2026. Prova em Windows GitHub Actions descartável. SOMENTE LABORATÓRIO.**

## Referências de evidência

- Repositório: `walissonminel-1991/gerenciador-final`, branch exclusiva `experimento-g5-ipc-chat-alto-20261029`.
- **SHA técnico aprovado:** `4766625cf320115593fa4fe6503e7a4ecd78b402`.
- **CI Windows:** [41203693345](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41203693345), job `124544386693`, `completed/success`.
- Draft GitHub Release `429695537`, tag `gf-e2e-c5057bbbaf1d`: permaneceu `draft=true`, `published_at=null`, cinco assets.
- ZIP NEW `gf-e2e-new-win-x64.zip`: asset ID `694233137`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto `GF-LAB-REAL-PACKAGE-MANIFEST.json`: asset ID `694233139`, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Apenas os três roles TUF de TESTE foram renovados durante a CI. ZIP, manifesto, publicação, branch main, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais e produção não foram tocados.

## Objetivo exclusivo cumprido

Comprovar que o **GF OLD real 5.13.11.65**, com WinForms `MainForm.Shown` e seu `GfOldDraftAcquire.Run` original em `.NET HttpClient`, **continua com janela viva, visível, responsiva e inventário/pesquisa funcionais** depois de desconexão TCP real durante a transferência do ZIP NEW 5.13.11.66.

Essa prova é distinta da CI anterior `41202775129`, que comprovou o transporte, preservação dos 284 arquivos OLD e código de saída 8 somente pelo entrypoint CLI `--gf-e2e-draft-acquire`. Agora o downloader é iniciado de modo assíncrono a partir da **janela real** do mesmo executável. A janela não encerra porque não houve handoff validado ao L18.

O ZIP fonte é um download verdadeiro da mesma Draft (57.695.630 bytes), com SHA-256 original verificado. O servidor Python existente em `127.0.0.1` respondeu HTTP 200, anunciou `Content-Length: 57695630` e desligou a conexão **depois de enviar 1.048.576 bytes**. O `HttpClient` do GF detectou a conexão incompleta na fase `asset_download`. O token efêmero GitHub só foi utilizado na consulta HTTPS de metadados da Draft; não foi enviado ao loopback nem embutido no executável.

## Alterações exclusivas do teste experimental

1. `Program.cs`: sob `#if GF_E2E_LAB`, a `MainForm.Shown` usa o arquivo de prova separado `gf-e2e-http-ui-startup-shown.json` se `GF_E2E_HTTP_UI_NEGATIVE=1`. Não muda o código compilado para usuários nem as regras de consentimento/bandeja normais.
2. `prototipos/autoatualizador-seguro/phase1-composed/GfOldDraftAcquire.cs.txt`: quando `GF_E2E_HTTP_CUT_NEGATIVE=1` e `GF_E2E_HTTP_UI_NEGATIVE=1`, o mesmo `FetchAsset` usa diretório e relatório de falha separados (`gf-e2e-http-ui-download`, `gf-e2e-http-ui-failure.json`). Proteções existentes bloqueiam handoff, fechamento automático, lançamento de NEW, recovery ou READY.
3. `prototipos/autoatualizador-seguro/phase1-composed/http_cut_gui_observer.py`: utiliza **as funções Win32 preexistentes** `own_main_windows`, `process_image_path`, `message_pump_ping`, `read_only_search_round_trip` e `desktop_observation` do arquivo `win32_process_observation.py`. Lança a cópia exata do OLD com `--tray` e `MainForm.Shown`. Depois de constatar os relatórios de conexão e erro, retém o processo, verifica o caminho/hash da imagem e um HWND da mesma identidade; exibe SOMENTE essa janela via `ShowWindow` no runner; comprova mensagem respondida, inventário com mais de zero aplicativos, busca de nonce sem correspondência e retorno à busca limpa, e verifica que a janela ainda permanece visível e responsiva ao final. Faz encerramento restrito ao próprio processo de laboratório, sem associá-lo a recuperação do produto.
4. `prototipos/autoatualizador-seguro/phase1-composed/http_cut_gui_negative.ps1`: cria cópia isolada dos **284 arquivos OLD** a partir do backup do teste anterior, com comparação SHA-256 de todos os arquivos antes de iniciar a instância GUI. Reutiliza o mesmo servidor de falha HTTP `http_cut_server.py` e o ZIP previamente autenticado, somente em `RUNNER_TEMP`, e exige recibos WinForms/HTTP/Win32 específicos. Depois da sessão faz verificação INDEPENDENTE de todos os 284 arquivos no OLD original e na cópia, canário de dados, seletor original `new/2`, ausência de NEW observado e ausência de instalação concluída.
5. Workflow `gf-github-draft-e2e.yml`: adiciona uma única etapa após a prova CLI e monitora os novos arquivos. Todas as etapas antigas de TUF, corte entre renomeações, HOLD, mensagens, seis negativos de integridade e falha HTTP CLI continuam aprovadas.

## Evidências dos logs da CI 41203693345

```
GF_E2E_PHYSICAL_GAP_EXACTLY_BETWEEN_MOVES old_files=284 new_files=284 original_path=ABSENT
GF_E2E_GUIDANCE_NEGATIVES_INDEPENDENT_PASS old_modified=BLOCK old_missing=BLOCK new_modified=BLOCK new_missing=BLOCK canary_modified=BLOCK canary_missing=BLOCK false_preservation_claims=0 original_bytes_unchanged=true selector=new/2 rollback=false cleanup=false READY=false
GF_E2E_REAL_HTTP_CONNECTION_CUT_PASS pinned_zip=true declared=57695630 sent=1048576 received=1048576 dotnet_httpclient=true old_exit=8 L18_not_invoked=true READY=false
GF_E2E_MAINFORM_HTTP_CUT_HEALTH_PASS tcp_disconnect=REAL_LOOPBACK mainform=VISIBLE+RESPONSIVE inventory=NONZERO search=EMPTY+RESTORED GF_OLD_still_running=true NEW_executed=false L18_not_invoked=true READY=false
GF_E2E_MAINFORM_HTTP_CUT_INDEPENDENT_PASS old_files=284 new_executed=false canary_unchanged=true L18_not_invoked=true READY=false
GF_E2E_MAINFORM_HTTP_CUT_PASS original_mainform_shown=true visible_after_failure=true inventory_search_healthy=true received_bytes=1048576 old_bytes_unchanged=true new_executed=false READY=false
```

**Critérios observados:** a falha de rede aconteceu de verdade durante o corpo HTTP, no loopback; o processo do GF OLD continuou vivo DEPOIS do relatório de erro; sua janela e imagem pertenciam à instância observada; Win32 confirmou a janela visível e com message pump, inventário não vazio, pesquisa que produziu o estado real sem correspondências e filtro restaurado, tudo sem clicar em manutenção; OLD/NEW de laboratório, dados e seletor não sofreram mutação indevida e não se concedeu READY nem recuperação automática.

## Histórico de CI nesta rodada

- `ca5cad4343d5ccf313c9a2a76b8393f25b2bf018`, [CI 41203506723](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41203506723): falha **na compilação**, `CS0103`, variável `guiNegative` fora do escopo do `catch`. Não chegou ao teste HTTP.
- **`4766625cf320115593fa4fe6503e7a4ecd78b402`**, [CI 41203693345](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41203693345): moveu a variável de seleção negativa para escopo visível no `catch`, sem mudar a semântica do `HttpClient`. Toda a CI aprovou, incluindo o novo teste de UI.

## Limites importantes

- A janela começou com `--tray` (suprime o consentimento modal na primeira abertura) e depois foi exibida por `ShowWindow` no Windows descartável. **Não** demonstramos uma primeira execução visual normal, sem intervenção do observador. O usuário não executou GF no PC pessoal.
- A quebra TCP foi REAL como transporte, mas induzida pelo servidor de loopback: **não** é prova de interrupção na CDN GitHub nem de indisponibilidade real da internet.
- O GF OLD do teste de GUI é uma **cópia byte a byte do OLD preservado** após a experiência de corte físico na CI, sob um diretório de teste novo. Não é atualização/instalação comercial/usuário; o caminho original permanece ausente pela etapa anterior.
- A busca de inventário confirma um contrato funcional de pesquisa read-only. Não demonstra manutenção de atualização/desinstalação, completa integridade de todas as operações GF, assinatura pública, H1/H2/H3, TOCTOU sob mesmo usuário, durabilidade sob queda física/reboot ou recuperação segura.
- `HOLD/UnknownBlocked` mantém contenção sem rollback, limpeza, retry ou READY. Chaves TUF continuam de TESTE, e o token da Draft só existe no runner.

## Próxima decisão

A prova da janela saudável após falha HTTP está encerrada. Não criar outra arquitetura, nem aumentar uma lista de auditorias sem objetivo funcional. O próximo trabalho, **se aprovado**, deve voltar ao objetivo operacional único de autoatualização de GF: definir a menor evidência pendente para levar de protótipo isolado a caminho de atualização utilizável no Windows real, SEM autorizar produção até que os controles independentes e recuperação tenham prova suficiente. No próximo passo, escolher apenas UMA alteração/teste que reduza essa lacuna, mantendo a mesma branch, L18 e Draft privada.

**Restrições mantidas:** nenhuma alteração em `main`, PR #65, BETA, Cloudflare, PCs pessoais; nenhum merge, deploy, release pública, rollback automático, limpeza ou WORK.
