# CHAT ALTO — GF OLD aberto normalmente autentica Draft e invoca o L18 existente sem fechar a interface

**Data: 31/10/2026. Situação: prova específica aprovada em Windows GitHub Actions descartável. SOMENTE LABORATÓRIO; READY=false.**

## Referências verificáveis

- Repositório: `walissonminel-1991/gerenciador-final`.
- Única branch modificada: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de entrada: `aa4d379f923661b83bd7858184c11085cb26836b`.
- **Commit técnico aprovado:** `938f28fac4d9a8f3c1fab1ae2b94ac84b3947d23`; comparação com o HEAD anterior: **um commit, cinco arquivos alterados**, sem arquitetura substituta.
- **CI Windows completa:** [41206593898](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41206593898); job **124552810095**; `completed/success` em 31/10/2026.
- Regressão independente D2.4.4: [41206593768](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41206593768), `completed/success`.
- Prova positiva anterior, apenas download e janela: [41205651516](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41205651516), `completed/success`. A nova prova adiciona **chamada ao L18 a partir da mesma abertura normal**, e não apenas repetição do download.

## Objetivo exclusivo e resultado

**Comprovado:** o GF OLD real experimental `5.13.11.65`, iniciado com a janela normal WinForms `MainForm.Shown` sem `--tray` ou `ShowWindow` forçado, baixou os cinco assets autênticos da Draft privada da versão `5.13.11.66` pelo mesmo `.NET HttpClient`, iniciou no próprio OLD o verificador Python-TUF já existente e, após confirmação TUF, iniciou o **mesmo executável C# `GF.Updater.M3.L18Tests.exe`** pelo seu comando `--real-normal-focused`, sem iniciar os modos de troca de instalação, fechamento automático ou seleção NEW de produção.

O L18 executou seu cenário real/focado de laboratório, comprovou as invariantes de contenção do próprio cenário, e retornou `UnknownBlocked` (inclusive em replay) e `READY=false`. Um observador Win32 externo, reutilizando funções já existentes, acompanhou **o mesmo processo OLD** vivo, janela própria nativamente visível e message pump responsivo DURANTE a espera pelo L18, com inventário não vazio, busca sem correspondências e restauração normal depois da conclusão. Sem intervenção do observador para exibir a janela, sem consentimento modal inesperado sob a política BETA LOCAL.

**Evidências literais extraídas dos logs do job Windows:**

```text
GF_E2E_GITHUB_NORMAL_L18_HEALTH_PASS real_draft=COMPLETE normal_gui=VISIBLE+RESPONSIVE inventory=NONZERO search=EMPTY+RESTORED OLD_running=true L18=UnknownBlocked installed_NEW_not_executed=true READY=false
GF_DRAFT_AUTHENTICATED {"downloaded_archive_sha256": "3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e", "extracted_files": 284, "lab_old_requires_l18_binding": true, "new_executable_on_disk_verified": true, "old_files_preserved": true, "operational_ready": false, "source": "github_draft_release_assets", "test_only": true, "tuf_manifest_sha256": "a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d"}
GF_E2E_GITHUB_NORMAL_L18_PASS normal_ui=true authenticated_zip=true existing_l18=true outcome=UnknownBlocked old_open=true rollback=false cleanup=false READY=false
```

No mesmo job, as provas anteriores de `MainForm.Shown`, download completo com autenticação, interrupção TCP após 1 MiB, sobrevivência da interface e contenção L18 após corte físico entre renomeações continuaram aprovadas. Este conjunto não é critério de `READY`.

## Alterações técnicas mínimas

1. `Program.cs`: adiciona somente um marcador de evidência separado `gf-e2e-github-normal-l18-startup-shown.json` dentro de `#if GF_E2E_LAB`; o binário distribuído não recebe esse fluxo.
2. `GfOldDraftAcquire.cs.txt`: novo modo negativo quanto a promoção `GF_E2E_GUI_NORMAL_L18_PROOF`, que exige `GITHUB_ACTIONS`, branch de laboratório e `GF_E2E_HANDOFF_L18=1` mas exige `GF_E2E_SELF_GRACEFUL_CLOSE`, `GF_E2E_SELECTED_NEW_CONTINUATION`, `GF_E2E_SWAP_CUT_AFTER_OLD_MOVE` e `GF_E2E_LIVE_INSTALL_SWAP` **todos desativados**. Usa a **mesma** implementação de download, TUF e `HandoffAuthenticatedDownloadToL18Async` já existente, com caminhos de recibos e diretórios distintos. A decisão de `UnknownBlocked` é a mesma do cenário existente `--real-normal-focused`.
3. `github_normal_gui_success_observer.py`: opção `--l18` estende o observador Win32 anterior. Confirma a janela do processo OLD ainda responsiva durante a espera pelo relatório L18 e revalida `UnknownBlocked`, ausência de `READY` e inexistência de autorização de recuperação.
4. `github_normal_gui_success.ps1`: reutilizado com `-L18`; cria cópia de 284 arquivos OLD em `RUNNER_TEMP`, pinagem de bytes ZIP/manifesto, verificação independente TUF, conferência de canário e seletor e ausência de instalação nova concluída.
5. `.github/workflows/gf-github-draft-e2e.yml`: uma etapa Windows adicional; as anteriores permanecem inalteradas.

Não há novo updater, nova implementação de seleção, novo downloader, fonte remota, serviço de atualização ou autoridade para recuperação. O processo observado é encerrado **somente após a prova**, no ambiente de laboratório, como encerramento do processo filho descartável, e não como ação do mecanismo de atualização.

## Escopo e proteção de pacote

- Draft **429695537**, tag `gf-e2e-c5057bbbaf1d`: `draft=true`, `published_at=null`, cinco assets; nenhum release público.
- ZIP NEW `gf-e2e-new-win-x64.zip`: asset **694233137**, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto `GF-LAB-REAL-PACKAGE-MANIFEST.json`: asset **694233139**, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- A workflow existente renovou **somente três roles TUF de TESTE**, mantendo ZIP e manifesto fixados.
- OLD original e cópias de laboratório, canário de dados e seletor L18 previamente registrado preservados. A nova prova não trocou a instalação nem lançou um NEW instalado como atualização: atenção, o cenário `--real-normal-focused` pode exercitar processos NEW **dentro das próprias fixtures de laboratório**, o que não equivale a executar a nova versão instalada no computador de um usuário.
- Consentimento: edição compilada `GfBetaProfile=local` sem envio remoto e sem pedido de autorização de telemetria conectada. Não foi produzido ou modificado consentimento; isto NÃO valida o fluxo de consentimento da BETA conectada.

**Não comprovados:** passagem autorizada da UI para uma troca de instalação, instalação física completa do pacote NEW, recuperação, rollback seguro, TOCTOU sob mesmo usuário, H1/H2/H3, estabilidade em reboot/queda física, confiança de produção ou prontidão de atualização. `UnknownBlocked` é contenção, **não** sinal de sucesso operacional. Nenhum mecanismo de rollback, limpeza, retry ou `READY` foi habilitado por esta prova.

**Não tocados:** `main`, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, produção, permissões, releases públicas. Sem merge, deploy, publicação ou uso de WORK.

## Continuidade recomendada

**Esta etapa de prova L18 a partir de MainForm normal está ENCERRADA.** Para avançar rumo à autoatualização utilizável, selecionar apenas o menor próximo contrato funcional de passagem da janela ao coordenador já existente, com decisão explícita de não fechar ou trocar quando houver contenção; não repetir os mesmos cenários sem ganho, nem introduzir autoridade nova ou liberar recuperação/rollback/limpeza/retry/READY por inferência. Qualquer prova adicional deve ser realizada no Windows descartável e acompanhada de um novo checkpoint.
