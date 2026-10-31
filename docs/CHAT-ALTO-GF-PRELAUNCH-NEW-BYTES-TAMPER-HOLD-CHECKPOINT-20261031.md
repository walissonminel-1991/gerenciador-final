# CHAT ALTO — NEW instalado adulterado em 1 byte é recusado ANTES do Popen, mesmo L18, HOLD

**Data: 31/10/2026. Estado: contraprova funcional única aprovada em Windows GitHub Actions descartável. READY=false. Somente laboratório, não homologado para usuários.**

## Identidades e resultados congelados

- Repositório `walissonminel-1991/gerenciador-final`; branch EXCLUSIVA `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de partida: `2533d6dcbf4bb49ec0c26f867f8b3090d16fd492`.
- Commits técnicos sucessivos: `a29047c03d92d1ea9a7eb116d19cef13a2a0b6b1` (implantação), `e22bfb84a1a959d7e1da65d45661ddd3838efcc7` (somente conflito de nomes locais C#), **`a213ec4943d3e88d03ae99735d53fe508462f937`** (restrição da instrumentação à observação do NEW instalado). Este último é o **SHA técnico aprovado**.
- CI inicial [41213226611](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41213226611): **FAILURE** no build do coordenador antes de iniciar a prova — `CS0136` por variáveis locais `outcome` e `replay` repetidas. Não constitui evidência NEW.
- CI intermediária [41213431374](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41213431374): **FAILURE** somente no novo job de mutação; regressões anteriores passaram. O modo adversarial era aplicado equivocadamente ao observador OLD antes da seleção `NEW/2`, causando `GF_E2E_OLD_L18_SELECTED_NEW_BRIDGE_EXITED_BEFORE_ARM`. Não se atingiu a contraprova e não se reivindicou sucesso.
- **CI FINAL [41214094382](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41214094382): `completed/success`, SHA `a213ec4943d3e88d03ae99735d53fe508462f937`; todos os QUATRO jobs Windows SUCCESS.**
- O job adversarial `Modificar 1 byte do NEW instalado apos TUF e recusar CreateProcess antes do launch` é o **124576277237**, `SUCCESS`. Os demais jobs de abertura/download/negativos, instalação positiva NEW e corte após segunda renomeação preservaram `SUCCESS`.

## ÚNICA contraprova nova

**Hipótese atacada:** verificar os 284 arquivos NEW instalados e obter o seletor L18 `NEW/2` não garante, por si só, que o executável carregado mais tarde tenha exatamente os mesmos bytes. Existe uma janela entre a verificação inicial do diretório e `subprocess.Popen` no mesmo observador `phase1_lab.py`.

**Teste executado, SEM arquitetura de atualização paralela:**

1. Outro runner `windows-2022` constrói o OLD real `5.13.11.65` em modo de laboratório, abre sua janela WinForms normalmente, sem `--tray` nem `ShowWindow`, e usa o downloader original acionado por `MainForm.Shown`.
2. A mesma Draft privada e os cinco assets são baixados e autenticados via Python-TUF já existente; o L18 seleciona `NEW/2` com launch fence e aguarda o encerramento gracioso do processo OLD original por handle: `FormClosed`, retorno de `Application.Run`, `ExitCode=0`.
3. O L18 move OLD para `gf-e2e-old-preserved` e instala NEW autenticado em `gf-e2e-old`, **o caminho portátil original**, mantendo o canário pessoal.
4. No mesmo observador selecionado do L18, o Python verifica todos os arquivos do NEW instalado contra o manifesto assinado. **Somente depois dessa verificação inicial**, a instrumentação de TESTE, condicionada a `GITHUB_ACTIONS`, repositório exato e observação do NEW fisicamente instalado, acrescenta **um único byte `0xA5` ao `GerenciadorFinal.exe`** instalado. Nenhum outro arquivo é modificado.
5. Em `observed_process`, imediatamente ANTES de chegar ao `subprocess.Popen`, um novo check compara novamente o SHA-256 do executável do caminho selecionado com o hash esperado do manifesto. Essa mutação específica é recusada com `GF_EXECUTABLE_BYTES_CHANGED_BEFORE_SPAWN`. Uma sentinela de teste que seria criada se o fluxo chegasse à tentativa de Popen permaneceu AUSENTE; a observação `real-observed-new.fixture.json` permaneceu ausente.
6. O proprietário L18 retorna erro conservador — a recusa **não autoriza** executar NEW nem inferir recuperação — e o mesmo reconciliador L18 é consultado em dois processos, ambos retornando `UnknownBlocked`. Seletor `new/2`, fences, OLD preservado, instalação NEW modificada e canário são mantidos para diagnóstico; sem rollback, cleanup, retry ou READY.
7. O observador externo usa a rotina existente de verificação/extração TUF para comparar os 284 arquivos esperados com os 284 arquivos instalados. Confirma diferença em **apenas `GerenciadorFinal.exe`**, com exatamente 1 byte adicional; os outros 283 são idênticos. Reconfere também os 284 arquivos OLD em backup byte a byte.

**Log literal decisivo no job final:**

```text
GF_E2E_INSTALL_FOCUSED_NORMAL_OLD_PASS native_mainform=true responsive=true no_tray=true READY=false
GF_E2E_INSTALL_FOCUSED_OLD_CLOSE_PASS normal_gui=true NEW2_fenced=true FormClosed=true ApplicationRunReturned=true exit0=true READY=false
GF_DRAFT_AUTHENTICATED {"downloaded_archive_sha256": "3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e", "extracted_files": 284, "lab_old_requires_l18_binding": true, "new_executable_on_disk_verified": true, "old_files_preserved": true, "operational_ready": false, "source": "github_draft_release_assets", "test_only": true, "tuf_manifest_sha256": "a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d"}
GF_E2E_TAMPER_PRELAUNCH_BYTES_PASS signed_new=284 installed_new=284 difference=ONLY_EXE_PLUS_1_BYTE selected=new/2 prelaunch_sha256=REJECTED new_popen_not_reached=true observed_new_absent=true OLD_backup=284 canary=UNCHANGED reconciliations=UnknownBlocked+UnknownBlocked rollback=false recovery=false cleanup=false retry=false READY=false
GF_E2E_TAMPER_PRELAUNCH_PASS old_normal=true signed_install=true mutated_exe_rejected_before_launch=true no_new_process=true L18=UnknownBlocked replay=UnknownBlocked READY=false
```

A sentinela negativa e o caminho do código observados sustentam que `Popen` **não foi alcançado nesse cenário de laboratório**. Não reivindicar garantia global de ausência de processos externos nem de equivalência dos bytes mapeados em memória sob um adversário ativo.

## Escopo de arquivos (três commits, cinco arquivos; nenhuma produção)

1. `prototipos/autoatualizador-seguro/phase1-composed/phase1_lab.py` — verificação de hash EXE imediatamente antes de `Popen`; uma mutação de um byte de laboratório, estritamente **após** a validação completa e **somente** na observação selecionada do NEW instalado; sentinela de aproximação da chamada Popen apenas no mesmo NEW.
2. `prototipos/autoatualizador-seguro/phase1-composed/Phase1RecoveryTests.cs.txt` — o mesmo owner L18 e reconciliador aceitam recusa negativa esperada, conferem que NEW não foi observado, submetem duas reconciliações em HOLD e produzem relatório local de contenção; correção dos nomes de variáveis C#.
3. `prototipos/autoatualizador-seguro/phase1-composed/GfOldDraftAcquire.cs.txt` — escolha de modo `--real-install-tamper-focused` com flags de runner autorizadas; mantém o mesmo L18, não abre canal alternativo.
4. `prototipos/autoatualizador-seguro/phase1-composed/github_install_focused_normal.ps1` — modo `-TamperPrelaunch` e comparação TUF independente dos 284 arquivos NEW/OLD, identificando **exatamente uma alteração controlada de byte** e ausência do ponto de lançamento.
5. `.github/workflows/gf-github-draft-e2e.yml` — acrescenta **um único job adversarial dependente da instalação positiva**, em runner Windows isolado. Os três jobs anteriores continuam aprovados.

**Inalterados:** `Program.cs`, implementação de distribuição, chaves e permissões de produção, rollback, seleção operacional, workflow da BETA, `main`, PR #65 Draft, Cloudflare, PCs pessoais, H1/H2/H3 e TOCTOU. Nenhum WORK utilizado.

## Draft fixa e fronteiras

- Draft **429695537**, tag `gf-e2e-c5057bbbaf1d`: `draft=true`, `published_at=null`, cinco assets.
- ZIP NEW asset **694233137**, `gf-e2e-new-win-x64.zip`, 57.695.630 bytes, SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto original asset **694233139**, `GF-LAB-REAL-PACKAGE-MANIFEST.json`, 72.618 bytes, SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Os três metadados TUF de **TESTE** foram renovados pela workflow anterior; ZIP/manifesto conservaram IDs/tamanhos e hashes verificados por download.
- O check pré-lançamento do Python **NÃO elimina a janela TOCTOU remanescente entre o cálculo SHA-256 e `subprocess.Popen` / `CreateProcess`**. Não prova que os bytes efetivamente executados/mapeados pela CPU estejam indissociavelmente vinculados ao objeto previamente autenticado, não protege contra substituição depois da última checagem e não equivale a assinatura/trust de produção.
- Todas as evidências são limitadas ao `GfE2eLab=true` em Windows GitHub Actions descartável; jamais executar esta mutação em PCs pessoais. A alteração do executável NEW é uma simulação adversarial intencional de teste, não uma etapa de atualização normal.
- Não há prova de persistência sob perda de energia, recuperação automática segura, contenção de efeitos externos/brokered, anti-TOCTOU operacional, H1/H2/H3 resolvidos, autenticação de produção ou `READY`. Sem rollback, recuperação, cleanup, retry, merge, deploy ou publicação.

## Próxima continuidade mínima

**A contraprova de mutação ANTES do último hash está concluída e não deve ser repetida.** A lacuna residual é o intervalo **APÓS** esse último hash e **ANTES** da criação do processo e o vínculo de identidade dos bytes realmente mapeados. Escolher uma única prova independente e estreita para esse intervalo, sem promover `READY`, sem usar recuperação ou retry, sem novas arquiteturas e somente no Windows Actions descartável.
