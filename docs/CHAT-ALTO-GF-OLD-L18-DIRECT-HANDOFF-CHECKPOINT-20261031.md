# CHAT ALTO — GF OLD inicia TUF e C# L18 diretamente (handoff experimental)

Data de referência: 31/10/2026. **Missão única:** autoatualização real GF OLD `5.13.11.65` para NEW `5.13.11.66`, no Windows descartável, sem READY operacional.

## Evidência anterior FECHADA — CI 41168060868 SUCCESS

- [Run GitHub Actions 41168060868](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41168060868), job `124439824746`, commit técnico `791e7a167bc27c63b4895e3496001e8488bd2dae`.
- `MainForm.Shown` da interface real do OLD (inicializado com `--tray`) acionou o downloader `.NET HttpClient`, que consultou e baixou os cinco assets da Draft.
- O Python-TUF **de TESTE** validou os 284 arquivos do pacote NEW e sua versão.
- O C# L18 da CI executou OLD/NEW reais: 29/29 casos `PASS_CONTAINMENT`, `failed=0`; caso `normal` com `UnknownBlocked`, `old_and_canary_preserved=true`, `manifest_and_object_identities_rechecked=true`.
- Negativos `modified` e `interrupted` (ZIP truncado, não interrupção HTTP real) rejeitados, sem execução de NEW adulterado; `rollback_authorized=false`.
- `operational_ready=false`, `phase1_complete=false`, observação positiva item-específico da GUI ainda não demonstrada.
- Esta CI anterior executava o L18 via workflow; **não** demonstrava chamada do L18 pelo processo GF OLD.

## Mudança técnica NOVA — commit 2a345e98a07b672c6922d70b43aedf47ac3357b1

- Branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`; SHA técnico `2a345e98a07b672c6922d70b43aedf47ac3357b1`.
- Alterou apenas `prototipos/autoatualizador-seguro/phase1-composed/GfOldDraftAcquire.cs.txt` e `.github/workflows/gf-github-draft-e2e.yml`.
- A inicialização experimental `MainForm.Shown` usa a mesma `GfOldDraftAcquire.Run` com `GF_E2E_HANDOFF_L18=1` **somente** quando compilada com `GfE2eLab=true`, sob `GITHUB_ACTIONS=true`, repositório exato, dados fixos e `RUNNER_TEMP`.
- Depois de baixar ZIP/manifesto/roles, **o GF OLD** passa a executar `python.exe github_draft_package.py verify` com `--lab-old-variant`, verificando TUF e NEW **antes de qualquer execução**. Se falhar: sem chamada ao L18.
- Com TUF aprovado, **o GF OLD** inicia por `ProcessStartInfo.ArgumentList` o mesmo `GF.Updater.M3.L18Tests.exe --real-suite`, usando os mesmos caminhos/fixtures/âncora, assinados e pré-preparados pela CI, e a matriz existente de 29 casos; não cria outra máquina de estado, seletor, reconciliador ou rollback.
- Os processos filhos recebem `GF_E2E_STARTUP_DRAFT_PROBE=0`, `GF_E2E_HANDOFF_L18=0` e **não** recebem `GH_TOKEN` / `GITHUB_TOKEN`, evitando recursive download e exposição de credencial.
- O GF OLD valida o relatório `29/29 PASS_CONTAINMENT`, `UnknownBlocked`, `operational_ready=false`, dados-canário e identidades; somente então emite `gf-e2e-old-l18-handoff-report.json`.
- A CI agora **prepara** fonte Stage8 pinada, bridge e fixtures antes da inicialização normal do OLD; espera recibo da chamada do L18 **iniciada pelo OLD**, verifica novamente TUF em pasta independente, valida relatório e mantém negativos.
- Limite importante: a CI ainda fornece toolchain Python-TUF e bridge de laboratório, portanto isto **não** é updater de produção ou independência total do executor. Mesmo o L18 continua operando em instalação de fixture/seletores de laboratório, não substitui uma troca transacional pronta no GF instalado.

## Estado da NOVA CI no checkpoint

- [CI 41168904327](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41168904327), job `124442392769`, SHA `2a345e98a07b672c6922d70b43aedf47ac3357b1`.
- Compilação GF OLD de laboratório, isolamento da compilação normal e renovação dos metadados TUF de TESTE: **SUCCESS**.
- Preparação do bridge C# L18 e fixtures previamente à inicialização: **SUCCESS**.
- `Startup real MainForm.Shown do GF OLD inicia download autonomo de Draft`: **IN_PROGRESS** no último estado observado. É nesse passo que OLD já começa a invocar Python-TUF e L18, por isso pode levar o tempo da matriz de 29 casos.
- Ainda **não** declarar esse novo handoff validado até CI `completed/success` e prova em logs; negativos e checagem independente de TUF vêm depois.

## Draft protegida

- ID `429695537`, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`.
- ZIP ORIGINAL asset ID `694233137`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto original asset ID `694233139`, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Apenas três roles TUF de TESTE podem ser renovados na Draft. **Não** publicar a Release, não trocar ZIP/manifesto, nem criar nova Draft, deploy ou distribuir.

## Restrições

Manter `main`, PR #65 Draft, BETA .66, Cloudflare, credenciais permanentes e PCs pessoais **intocados**; proibição de merge, release público, deploy, instalação em computador pessoal, login/tokens distribuídos ou promoção de `READY`. H1/H2/H3 e TOCTOU same-user não comprovados; `HOLD` / `UnknownBlocked` é só contenção. Chaves TUF de laboratório públicas não equivalem a autoridade de produção. Nenhuma segunda arquitetura.

## Próxima ação exata

1. Conferir resultado final e logs da **CI 41168904327**, job `124442392769`, inclusive etapa do startup/handoff, autenticação TUF independente, 29 casos, negativos, OLD/NEW/canário e `UnknownBlocked`.
2. Se falhar: corrigir apenas o erro demonstrado. Se passar: registrar resultado, propor **próxima prova única** de transição sob comando do OLD em Windows descartável, sem ampliar arquitetura, sem WORK até ponto decisivo.
3. Manter botões para `Continuar missão` e `Abrir CI`.

Nenhum WORK utilizado nesta rodada.
