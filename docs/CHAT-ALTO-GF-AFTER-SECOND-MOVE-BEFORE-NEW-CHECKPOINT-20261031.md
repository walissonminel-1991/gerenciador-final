# CHAT ALTO — Corte após SEGUNDA renomeação, ANTES da execução NEW, com contenção L18

**Data: 31/10/2026. Estado: prova adversarial única aprovada em Windows GitHub Actions descartável. LABORATÓRIO, READY=false.**

## Identidade e execução verificável

- Repositório: `walissonminel-1991/gerenciador-final`.
- Única branch alterada: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental inicial: `3600cd10eb38bf8da2ed575b83c728cac7104ef5`.
- Commit técnico inicial `8d1012f897e05118cb9280e047ebfcfafa2c8b7e`.
- Correção estrita de despacho L18 `d01cc41faed2836075b257f7f96c35a7c433d162` (**SHA técnico final**).
- CI inicial [41210753256](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41210753256): primeira e segunda provas SUCCESS, terceira FAILURE **antes de armar NEW/2**. Não interpretar como prova nova aprovada.
- **CI final [41211427784](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41211427784): `completed/success`; TRÊS jobs SUCCESS** no SHA `d01cc41faed2836075b257f7f96c35a7c433d162`.
- Job regressão entre renomeações: **124567068128**, SUCCESS.
- Job instalação real positiva original: **124567871008**, SUCCESS.
- **Job adversarial novo:** **124568395822**, SUCCESS.

## UMA lacuna nova concluída

O corte físico anterior ocorria ENTRE `Directory.Move(live,backup)` e `Directory.Move(incoming,live)`, deixando o caminho original ausente. A instalação positiva posterior comprovou o NEW instalado e executado. **A lacuna inédita** era a falha do proprietário L18 imediatamente **DEPOIS** de `Directory.Move(incoming,live)`, mas **ANTES** do recibo de instalação e do lançamento/observação funcional do NEW.

No mesmo método `L18Coordinator.SwapRealInstalledPackageAfterOldExit`, foi inserida uma guarda de **laboratório descartável** para interromper o proprietário no ponto exato:

1. O GF OLD `5.13.11.65` é aberto com `MainForm.Shown` NORMAL, sem `--tray`, sem `ShowWindow` e sem forçar encerramento para obter prova positiva.
2. O OLD autentica a Draft privada real; o TUF existente verifica o pacote `5.13.11.66`; o **mesmo** L18 cerca `NEW/2`, retém handle do OLD e observa seu encerramento gracioso (`FormClosed` + `Application.Run` retornado + `ExitCode=0`).
3. Após a primeira renomeação, o OLD original já está em `gf-e2e-old-preserved`. A segunda renomeação coloca NEW autêntico em `gf-e2e-old` — **caminho portátil original**.
4. O owner publica o marcador `after-second-install-move-before-new-observe` via o `RealCut` EXISTENTE (arquivo gravado e fechado); o helper existente, usando o **mesmo LabProcessJob**, interrompe **somente** o owner sob handle retido, com `ExitCode=91`. Não há observador NEW, execução NEW instalada, recibo de instalação bem-sucedida, retry ou retomada.
5. O mesmo reconciliador L18 é acionado **duas vezes**, retorna `UnknownBlocked` / `REAL_NEW_EFFECTS_NOT_INDEPENDENTLY_BOUNDED`, conserva o seletor `new/2` e os fences de possíveis efeitos, sem mudar pastas, reiniciar processos NEW, recuperar OLD ou limpar.
6. O observador externo à execução confirma conjuntos OLD e NEW de **284 arquivos cada** por comprimento e SHA-256 e confere o NEW instalado contra extração TUF independente do ZIP original. Também preserva canário, hashes fixos do ZIP e manifesto da Draft, manifesto transacional do L18 com instalação/transação/seletor vinculados, e ausência de `real-observed-new.fixture.json`.

O cenário é **separado em um terceiro runner `windows-2022` novo**, dependente do job de instalação positiva, que por sua vez depende da prova anterior de corte entre renomeações. Não combina seletores ou diretórios de provas diferentes; não altera a prova de instalação positiva nem repete desnecessariamente a implementação de L18.

## Evidência literal da CI final

```text
GF_E2E_INSTALL_FOCUSED_OLD_BASELINE files=284
GF_E2E_INSTALL_FOCUSED_NORMAL_OLD_PASS native_mainform=true responsive=true no_tray=true READY=false
GF_E2E_INSTALL_FOCUSED_OLD_CLOSE_PASS normal_gui=true NEW2_fenced=true FormClosed=true ApplicationRunReturned=true exit0=true READY=false
GF_DRAFT_AUTHENTICATED {"downloaded_archive_sha256": "3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e", "extracted_files": 284, "lab_old_requires_l18_binding": true, "new_executable_on_disk_verified": true, "old_files_preserved": true, "operational_ready": false, "source": "github_draft_release_assets", "test_only": true, "tuf_manifest_sha256": "a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d"}
GF_E2E_AFTER_SECOND_MOVE_CUT_BYTES_PASS OLD_backup=284 NEW_installed_original_path=284 NEW_bytes_equal_TUF=true second_rename_complete=true new_observer_absent=true installed_receipt_absent=true selector=new/2 replay=UnknownBlocked canary=UNCHANGED rollback=false recovery=false cleanup=false retry=false READY=false
GF_E2E_AFTER_SECOND_MOVE_CUT_PASS old_normal=true new_installed=true new_not_started=true owner_killed=91 L18=UnknownBlocked replay=UnknownBlocked HOLD=true READY=false
```

## Falha inicial localizada e corrigida

- Na primeira CI `41210753256`, ambos os jobs anteriores passaram. O terceiro ficou bloqueado em `l18_selector_wait` com `GF_E2E_OLD_L18_SELECTED_NEW_BRIDGE_EXITED_BEFORE_ARM`: o processo L18 havia encerrado antes de armar o seletor, e OLD conservadoramente recusou prosseguir. **Nenhuma segunda renomeação foi reivindicada.**
- Ao inspecionar o código real, identificou-se que `Phase1RecoveryTests.Entry` aceitava o modo externo `--real-install-after-second-cut-focused`, mas **não admitia** o nome interno `handoff-install-after-second-cut` em sua entrada `--real-owner` com seis argumentos e PID original.
- A correção `d01cc41faed2836075b257f7f96c35a7c433d162` ampliou **somente** o despacho `--real-owner` de laboratório para o nome já restrito. Não modificou a comparação de identidade real do OLD, os fences, o seletor, a proteção de arquivos ou a reconciliação. A CI final `41211427784` comprovou três jobs SUCCESS.

## Arquivos e escopo congelados

Comparação do HEAD documental de partida `3600cd10eb38bf8da2ed575b83c728cac7104ef5` até o SHA técnico final: **dois commits, cinco arquivos**:

1. `prototipos/autoatualizador-seguro/phase1-composed/Phase1Recovery.cs.txt`: ponto de corte precisamente após segunda renomeação, antes de qualquer recibo/observação, limitado a `GITHUB_ACTIONS` e ao repositório; sem retomada ou rollback.
2. `prototipos/autoatualizador-seguro/phase1-composed/Phase1RecoveryTests.cs.txt`: extensão do mesmo helper `PhysicalInstallBetweenMovesCut`, mesmo owner Job, reconciliador, seletor e verificação NTFS, com checagens condicionais do layout físico; correção do despacho do PID.
3. `prototipos/autoatualizador-seguro/phase1-composed/GfOldDraftAcquire.cs.txt`: seleciona o comando do mesmo L18 para modo pós-segunda-renomeação somente se as flags do runner autorizado estiverem presentes.
4. `prototipos/autoatualizador-seguro/phase1-composed/github_install_focused_normal.ps1`: modo `-AfterSecondCut`; mesmo OLD real e setup, mas pós-corte valida preservação/ausência de NEW observado em vez de exigir execução funcional; observador TUF independente continua obrigatório.
5. `.github/workflows/gf-github-draft-e2e.yml`: acrescenta terceiro job Windows descartável dependente, sem modificar a natureza das duas provas antigas.

**Não alterados:** `Program.cs`, fluxo de produção, manifesto/ZIP da Draft, chaves, `main`, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, configuração de consentimento, H1/H2/H3 e TOCTOU. Nenhum WORK gasto. Sem merge, deploy ou publicação.

## Draft e invariantes preservados

- Draft existente **429695537**, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`, cinco assets.
- ZIP NEW **694233137**, `gf-e2e-new-win-x64.zip`, tamanho 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto original **694233139**, `GF-LAB-REAL-PACKAGE-MANIFEST.json`, tamanho 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- A CI renovou apenas os três metadados TUF **de TESTE** da Draft, não os bytes dos dois assets imutáveis.
- Resultado observado **não** é recuperação segura de uma falha elétrica real; `RealCut` mais `LabProcessJob` é um corte de processo controlado. O estado `UnknownBlocked` significa **contenção**, não READY, não sucesso operacional e não garantia de reativação do programa após falhas inesperadas.
- H1/H2/H3, TOCTOU, persistência de diretório em perda de energia, confiança de produção, efeitos externos/brokered do NEW e autoridade independente para recuperação **permanecem inconclusivos**. Não executar recuperação, rollback, limpeza, retry, promoção, merge, release ou deploy.

## Próxima continuidade recomendada

O ponto **após segunda renomeação e antes de lançamento/observação NEW** está coberto e aprovado. **Não repetir** este corte nem os anteriores sem novo ganho. A próxima lacuna deve ser escolhida de forma mínima e funcional, preferencialmente um contraexemplo de *vínculo de bytes realmente executados* ou *não repetição de instalação* após interrupção, mantendo HOLD e as mesmas restrições. Não abrir uma arquitetura paralela, não autorizar recuperação/rollback/cleanup/retry/READY; continuar no Windows Actions descartável com um único passo novo e checkpoint verificável.
