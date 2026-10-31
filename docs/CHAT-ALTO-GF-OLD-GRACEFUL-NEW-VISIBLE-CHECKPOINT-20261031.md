# CHAT ALTO — GF OLD original encerra normalmente; NEW autenticado abre janela real

Data: 2026-10-31. **Somente Windows descartável GitHub Actions, test-only, nenhuma promoção operacional.**

## Identidade e estado

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch exclusiva: `experimento-g5-ipc-chat-alto-20261029`.
- Commit técnico comprovado: `07ccde3fa88a1f9e5a4c7c122fb785dfdb3ddfee`.
- CI real: [run 41170741828](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41170741828), job `124447976113`: **completed/success** no SHA `07ccde3fa88a1f9e5a4c7c122fb785dfdb3ddfee`.
- Release existente: ID `429695537`, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`.
- NEW ZIP original: asset ID `694233137`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto original: asset ID `694233139`, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Apenas metadados TUF de TESTE `targets/snapshot/timestamp` foram renovados. O ZIP e o manifesto permaneceram inalterados.

## Alterações no commit técnico

1. `Program.cs`: somente sob `#if GF_E2E_LAB`, o evento de startup `MainForm.Shown` chama `GfOldDraftAcquire.Run` com um callback para solicitar `mainWindow.Close()` **somente após** o relatório do mesmo L18 ter sido concluído e conferido. Um timer limitado evita solicitar fechamento enquanto `_busy`; nunca ignora `FormClosing`, `_betaClose` ou cancela manutenção ativa.
2. `Program.cs` registra exclusivamente no runner o evento do `FormClosed` original e, depois que `Application.Run(mainWindow)` retorna, o marcador da saída do laço de mensagens. A CI exige, adicionalmente, **o handle da instância original** encerrado com `ExitCode=0`. Nenhuma prova baseia-se apenas em recibo local.
3. `GfOldDraftAcquire.cs.txt`: recebe callback opcional, invoca-o somente depois de TUF de TESTE aprovado, execução do L18 focado concluída e verificações `PASS_CONTAINMENT`, dados-canário e `UnknownBlocked`. Credenciais efêmeras da Draft não são passadas aos processos Python/L18.
4. `.github/workflows/gf-github-draft-e2e.yml`: proíbe classificar cleanup forçado como sucesso; a execução precisa encerrar o GF OLD por sua própria janela. Depois lança uma **sessão independente** do executável NEW extraído e autenticado e reutiliza `win32_process_observation.py` para observar visibilidade, propriedade da janela, resposta do laço de mensagens e pesquisa funcional de ida/volta, antes de encerrá-la normalmente. Não cria outro seletor nem outro autoatualizador.

## Evidências verificadas diretamente nos logs

- `GF_E2E_ORIGINAL_OLD_GRACEFUL_EXIT proof=FormClosed+Application.Run+ExitCode0 forced_cleanup=false ready=false`
- `GF_E2E_GF_OLD_MAINFORM_SHOWN_TUF_AND_EXISTING_L18_COMPLETE focused_normal=PASS_CONTAINMENT ready=false original_old_exit=0`
- `GF_DRAFT_AUTHENTICATED`: NEW autentica `284` arquivos, SHA-256 do ZIP e manifesto conferidos, `old_files_preserved=true`, `operational_ready=false`.
- `GF_E2E_OLD_OWNED_PYTHON_TUF_AND_L18 old=5.13.11.65 new=5.13.11.66 focused_normal=PASS_CONTAINMENT ready=false`.
- `GF_E2E_NEW_VISIBLE_UI_OBSERVER`: `independent_new_session_after_old_exit=true`, `new_graceful_exit=true`, `new_hwnd_owned_by_launched_process=true`, `new_image_sha_from_signed_manifest=true`, `new_mainform_visible=true`, `new_ui_search_roundtrip=true`, `positive_visible_ui_proved=true`, **`single_live_l18_selector_session_proved=false`**, `operational_ready=false`.
- `GF_DRAFT_NEGATIVES`: `modified` e `interrupted` (ZIP truncado) rejeitados, `downloaded_asset_executed_on_negative=false`, `rollback_authorized=false`.

## Exatamente o que esta CI demonstrou

1. GF OLD original `5.13.11.65` aberto pela interface inicia busca e download do GitHub Draft, autentica NEW com Python-TUF de TESTE e chama o MESMO coordenador C# L18.
2. L18 executa seu cenário normal em instalação de fixture com seleção `new/2`, observações limitadas de GF OLD/NEW, diário/Job e dados-canário preservados, terminando em `UnknownBlocked` e sem `READY`.
3. Depois que o relatório L18 é conferido, **o próprio GF OLD original** solicita fechamento pela janela WinForms, produz `FormClosed`, retorna de `Application.Run` e sai com `ExitCode=0`. A CI não precisou matar essa instância.
4. Em seguida, um NEW com bytes autenticados é lançado independentemente do seletor L18, apresenta uma janela WinForms visível, completa busca funcional read-only e encerra normalmente. É a primeira prova desse conjunto de características do NEW **depois** do OLD original ter saído.

## Limites: NÃO equivale a autoatualização pronta

- O NEW visível pós-OLD **não é o mesmo processo controlado pelo seletor ativo do L18**. É uma segunda sessão do mesmo binário já autenticado, usada como verificação independente da interface. `single_live_l18_selector_session_proved=false`. Não alegar que o L18 deixou NEW ativo, persistido e utilizável continuamente no lugar da instalação OLD.
- O próprio L18 ainda seleciona OLD/NEW em diretórios privados de fixture, não em uma instalação operacional GF, e a função normal conclui em `UnknownBlocked`, não em `READY`.
- Os metadados TUF usam chaves públicas de laboratório; Draft requer token efêmero da CI, nunca distribuível ao usuário.
- O teste de interrupção anterior usa ZIP truncado, não corte de transferência HTTP ao vivo. Item específico no controle customizado ainda não é prova positiva.
- Falta prova robusta de quiescência de efeitos externos/recuperação após crash físico, H1/H2/H3, proteção contra rollback malicioso por mesmo usuário ou TOCTOU. O relatório local não fornece uma autoridade externa.

## Próximo objetivo único e restrições

**Próxima prova:** utilizar a seleção `new/2` do **mesmo L18** para iniciar e observar a sessão NEW final de modo contínuo após a saída original de OLD, sem iniciar outro NEW independente da seleção e sem promover READY. Só avançar se isso puder ser demonstrado na VM descartável, com bytes verificados, OLD/NEW/canário preservados e contenção `UnknownBlocked`. Não repetir matrizes anteriores sem regressão específica.

Preservar `main`, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, credenciais e tudo de produção; não merge/deploy/publicação/release, não WORK desnecessário. `HOLD` não autoriza rollback, exclusão, limpeza nem repetições automáticas. Usar botões de continuidade e CI.

Documentação acrescentada sem alterar o código técnico após a CI aprovada.
