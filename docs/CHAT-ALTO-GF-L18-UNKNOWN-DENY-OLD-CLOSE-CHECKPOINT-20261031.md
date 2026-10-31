# CHAT ALTO — L18 UnknownBlocked nega fechamento do OLD real com interface normal

**Data:** 31/10/2026. **Estado:** prova funcional de laboratório aprovada no Windows GitHub Actions descartável. **READY=false.**

## Identidade e cadeia verificável

- Repositório: `walissonminel-1991/gerenciador-final`; branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de partida: `3248f9c0cce988b86714da5b1bfcfe01e57a53a8`.
- **Commit técnico:** `e8bc4cd785aa5d6829921eab65ce36d54cfde2b8`.
- **CI principal:** [41207291058](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41207291058) — job **124554867768**, `completed/success`.
- CI D2.4.4 no mesmo commit: [41207290980](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41207290980), `success`.
- Prova anterior de download autêntico + chamada L18 com janela saudável: [41206593898](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41206593898), `success`.

## ÚNICA lacuna escolhida: decisão de fechamento

**Problema observado no código antes da intervenção:** após o retorno do L18, o programa experimental aceitava `GF_E2E_SELF_GRACEFUL_CLOSE=1` juntamente com a **mera existência** de `gf-e2e-old-l18-handoff-report.json` para chamar `afterVerifiedLabHandoff`. Um relatório do cenário `--real-normal-focused` podia conter `UnknownBlocked`, que é contenção e NÃO autoriza fechar OLD; ainda havia a possibilidade de o arquivo ter sido criado por etapa anterior do mesmo runner. Esse era um contraexemplo de contrato de fechamento, não um defeito comprovado do produto distribuído.

**Correção funcional:** o caminho de fechamento automático do OLD no laboratório passa a considerar somente a continuação `GF_E2E_SELECTED_NEW_CONTINUATION=1`, cuja barreira de seleção NEW já é validada no caminho L18 existente antes de retornar. A presença de relatório genérico (incluindo relatório `UnknownBlocked` ou herdado de outro passo) deixa de autorizar callback de fechamento. Não foi aberta autoridade nova para recuperação, rollback, limpeza, retry ou READY.

**Prova específica:** o GF OLD `5.13.11.65`, aberto normalmente por `MainForm.Shown`, sem `--tray` e sem `ShowWindow` forçado, baixa e autentica os cinco assets da Draft, chama o **mesmo** `GF.Updater.M3.L18Tests.exe --real-normal-focused`, recebe `UnknownBlocked` e `READY=false`. Em seguida, a prova deixa `GF_E2E_SELF_GRACEFUL_CLOSE=1` deliberadamente, com `GF_E2E_GUI_NORMAL_L18_DENY_CLOSE=1`, e exige recusa explícita de chamada ao callback. O observador Win32 externo aguarda o resultado e confirma, por 12 ciclos adicionais de observação, que **a mesma janela OLD continua nativamente visível e com message pump responsivo**; também valida inventário não vazio e pesquisa que retorna à lista. Recibo local isolado nunca substitui a observação Win32 real.

### Evidências literais dos logs Windows

```text
GF_E2E_GITHUB_NORMAL_L18_HEALTH_PASS real_draft=COMPLETE normal_gui=VISIBLE+RESPONSIVE inventory=NONZERO search=EMPTY+RESTORED OLD_running=true L18=UnknownBlocked close_request=DENIED installed_NEW_not_executed=true READY=false
GF_E2E_GITHUB_NORMAL_BYTES_PASS pinned_zip=true pinned_manifest=true old_files=284 canary_unchanged=true NEW_not_executed=true READY=false
GF_DRAFT_AUTHENTICATED {"downloaded_archive_sha256": "3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e", "extracted_files": 284, "lab_old_requires_l18_binding": true, "new_executable_on_disk_verified": true, "old_files_preserved": true, "operational_ready": false, "source": "github_draft_release_assets", "test_only": true, "tuf_manifest_sha256": "a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d"}
GF_E2E_GITHUB_NORMAL_L18_DENY_CLOSE_PASS requested=true outcome=UnknownBlocked close_denied=true old_healthy_after_denial=true NEW_not_installed=true READY=false
```

Todos os passos anteriores da CI principal permaneceram aprovados, inclusive autenticação do download, falha TCP de 1 MiB, preservação do OLD, canário e seletor, contenção entre renomeações e negativos de integridade. Isso não autoriza inferir prontidão de atualização.

## Arquivos e escopo exatos

Comparação com `3248f9c0cce988b86714da5b1bfcfe01e57a53a8`: **um commit, quatro arquivos**:

1. `prototipos/autoatualizador-seguro/phase1-composed/GfOldDraftAcquire.cs.txt` — recusa explícita do callback sob `UnknownBlocked`, com verificação de relatório local correspondente ao mesmo modo experimental, mantendo somente a continuação NEW já cercada como possível candidato a fechar OLD; o modo de prova é restrito a `GITHUB_ACTIONS`.
2. `prototipos/autoatualizador-seguro/phase1-composed/github_normal_gui_success_observer.py` — reutilização do observador Win32 com `--l18 --deny-close`; verifica o processo OLD durante e depois da recusa por múltiplas medições.
3. `prototipos/autoatualizador-seguro/phase1-composed/github_normal_gui_success.ps1` — `-L18 -DenyClose` exige recibo negativo, saúde da interface e integridade de bytes, canário e seletor; não abre segunda implementação de atualizador.
4. `.github/workflows/gf-github-draft-e2e.yml` — alterou **somente os argumentos** do último passo já existente para executar a nova prova. Não adicionou outro job repetitivo.

Nenhuma mudança em `Program.cs` neste commit. O comportamento continua sob `#if GF_E2E_LAB`; o GF distribuído não compila o `GfOldDraftAcquire.cs.txt`.

## Pacote e limites preservados

- Draft existente: **429695537**, tag `gf-e2e-c5057bbbaf1d`; `draft=true`, `published_at=null`, cinco assets.
- ZIP: `gf-e2e-new-win-x64.zip`, asset **694233137**, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto: `GF-LAB-REAL-PACKAGE-MANIFEST.json`, asset **694233139**, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- A CI renovou somente os três metadados TUF de **TESTE** da Draft, não substituiu o ZIP/manifesto e não publicou release.
- OLD e seus 284 arquivos, canário e seletor L18 da inspeção independente permaneceram preservados. NEW não foi instalado nesta prova. Os cenários internos L18 podem exercitar processos de fixture, o que não constitui execução de NEW instalado no usuário.
- Perfil do OLD: `GfBetaProfile=local`, sem envio remoto de diagnósticos. Não foi simulada aceitação de consentimento conectado nem modificado consentimento.
- O encerramento pelo observador ao **fim da prova** é restrito ao seu processo de TESTE descartável e não constitui mecanismo de atualização, rollback ou recuperação.
- `main`, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, produção, H1/H2/H3 e TOCTOU não foram alterados. Sem merge, deploy, publicação, WORK, novas chaves ou permissões.

**Limites não resolvidos:** confiança de produção, passagem para fechamento seguro de OLD quando NEW estiver efetivamente pronto, atualização instalada, recuperação transacional, persistência sob falha física, H1/H2/H3, TOCTOU e eventual rollback. O resultado atual **não aprova o fechamento do OLD nem o lançamento NEW para atualização real**.

## Próxima continuidade

A prova da **recusa de fechar OLD por `UnknownBlocked`** está concluída e não requer repetição. O próximo avanço útil é provar, dentro dos controles existentes, uma condição positiva e independente que autorize o início da passagem a NEW sem confundir `selected-new-ready.json`, PID/HWND, relatórios locais ou `UnknownBlocked` com autoridade de recuperação ou READY. Escolher **somente uma lacuna**, no Windows GitHub Actions descartável. Não autorizar recuperação, rollback, limpeza, retry ou READY; não substituir arquitetura nem afetar o produto distribuído.
