# CHAT ALTO — Contraexemplo TOCTOU: NEW muda APÓS último SHA-256 e Popen cria o processo; L18 permanece em HOLD

**Data: 31/10/2026. Estado: uma contraprova adversarial nova concluída em GitHub Actions Windows descartável, cinco jobs SUCCESS. TESTE APENAS. READY=false.**

## Identificação e evidências verificáveis

- Repositório: `walissonminel-1991/gerenciador-final`.
- Única branch alterada: `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental anterior: `3d5aef25ce6211c5a9cba4a4fc5e0f24a4528f6c`.
- **Único commit técnico desta rodada:** `6107b4c835a6cb36b54e986f66bff117f83bc2c6`.
- **CI Windows: [41216710645](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41216710645)**, `completed/success`, HEAD técnico `6107b4c835a6cb36b54e986f66bff117f83bc2c6`, **todos os cinco jobs SUCCESS**.
- Job adversarial novo `NEW original executa apos um byte alterado ENTRE hash e Popen, manter HOLD`, ID **124584029558**, `SUCCESS`.
- Demais quatro jobs: `gf-old-initiated-draft-real` **124582646440**, `gf-normal-install-focused` **124583483518**, `gf-installed-new-prelaunch-tamper` **124584029563**, `gf-after-second-move-cut-focused` **124584029590**: **todos SUCCESS**. Regressoes mantidas.

## UMA contraprova, e o limite exato da conclusão

**Pergunta:** o SHA-256 correto do executável imediatamente antes da chamada `subprocess.Popen` garante que o executável ainda corresponda ao manifesto autenticado quando o Windows criar o processo?

**Resposta experimental: NÃO.** O caminho de observação original é vulnerável à janela de tempo entre o último hash de arquivo e a chamada de criação do processo.

O mesmo GF OLD `5.13.11.65` abre NORMAL, com `MainForm.Shown`, sem `--tray` e sem `ShowWindow` forçado. Seu downloader existente baixa a Draft privada real. O Python-TUF já implementado valida o NEW `5.13.11.66`, e a ponte C# L18 existente mantém seletor `NEW/2`, fences e handle do OLD original. Depois do `FormClosed`, retorno de `Application.Run` e `ExitCode=0` do OLD, o L18 coloca os 284 arquivos do NEW autenticado no **caminho portátil ORIGINAL** e preserva os 284 arquivos OLD no backup.

**ÚNICO desvio adversarial, sem outra arquitetura:**

1. No mesmo `phase1_lab.py:observed_process` e **após** `file_hash(exe)==expected_exe_sha`, com a guarda de laboratório restrita a `GITHUB_ACTIONS=true`, repositório exato e a variável herdada `GF_E2E_L18_OBSERVE_INSTALLED_NEW=1`, é acrescentado **um byte `0xA5`** ao `GerenciadorFinal.exe` do NEW instalado. O arquivo alterado é sincronizado e comprovado com hash divergente **antes** de tentar iniciar o processo.
2. O fluxo **não chama outro lançador**: alcança o `subprocess.Popen([str(exe),"--tray"])` original, que **retorna um processo Windows realmente criado**. O observador `observe_real_gf` existente confirma, por consulta Win32 associada ao processo retornado, que o caminho da imagem corresponde ao executável instalado (`actual_image_path_matches=true`).
3. A avaliação **pós-criação** do SHA-256 do arquivo instalado falha (`image_on_disk_sha_matches_manifest=false`). A observação funcional é rejeitada com `RECOVERY_FUNCTIONAL_OBSERVATION_UNPROVEN`, sem gerar sucesso/READY. O processo de teste é encerrado pelo **handle próprio do Popen**, conforme a contenção de testes existente; não se apaga nem se reinstala o GF.
4. O L18 bloqueia e duas execuções do mesmo reconciliador retornam `UnknownBlocked`, sem alterar seletor `new/2`, sem rollback, recuperação, limpeza de arquivos ou retry; `READY=false`.
5. A auditoria externa ao owner reextrai o ZIP pelo **verificador TUF existente** e compara por tamanho e SHA-256: **284 arquivos NEW** em ambos os conjuntos, **283 idênticos** e **somente o EXE instalado com um byte extra**. Reconfere também os 284 OLD preservados, canário pessoal, manifesto transacional, seletor/fences e hashes imutáveis da Draft.

A evidência de processo criado não depende **somente** de recibo local ou PID: combina retorno de `Popen` (handle do processo filho), consulta Win32 do caminho de imagem e comparação independente de bytes em disco. **Ainda assim não prova os bytes de imagem mapeados na memória nem estabelece autoridade operacional independente.**

## Trechos literais dos logs CI 41216710645, job 124584029558

```text
GF_E2E_INSTALL_FOCUSED_NORMAL_OLD_PASS native_mainform=true responsive=true no_tray=true READY=false
GF_E2E_INSTALL_FOCUSED_OLD_CLOSE_PASS normal_gui=true NEW2_fenced=true FormClosed=true ApplicationRunReturned=true exit0=true READY=false
GF_DRAFT_AUTHENTICATED {"downloaded_archive_sha256": "3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e", "extracted_files": 284, "lab_old_requires_l18_binding": true, "new_executable_on_disk_verified": true, "old_files_preserved": true, "operational_ready": false, "source": "github_draft_release_assets", "test_only": true, "tuf_manifest_sha256": "a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d"}
GF_E2E_POSTHASH_COUNTEREXAMPLE_BYTES_PASS last_hash=PASSED mutation=EXE_PLUS_1_BYTE posthash_popen=CREATED win32_image_path=VERIFIED postcreation_hash=DIFFERENT rejected_after_launch=true installed_new=284 old_backup=284 canary=UNCHANGED outcome=UnknownBlocked replay=UnknownBlocked rollback=false recovery=false cleanup=false retry=false READY=false
GF_E2E_POSTHASH_COUNTEREXAMPLE_PASS normal_old=true signed_install=true hash_was_original_then_mutated=true NEW_process_created=true postlaunch_sha_rejected=true L18=UnknownBlocked replay=UnknownBlocked READY=false
```

**Interpretação rigorosa:** o teste confirma que o Win32 permitiu criar um processo a partir do caminho instalado depois que o arquivo mudou após a checagem. O byte extra pode ser um overlay PE e não se reivindica que a CPU executou esse byte, nem que a memória do processo tinha código malicioso, nem explorabilidade geral/prod. A validação pós-criação falha corretamente, mas ocorre **tarde demais para impedir os efeitos que já poderiam ter ocorrido depois da criação**. A resposta conservadora `UnknownBlocked` não equivale a um rollback seguro, READY ou conclusão do objetivo final de autoatualização para o público.

## Arquivos exatos do único commit técnico

O compare `3d5aef25ce6211c5a9cba4a4fc5e0f24a4528f6c` → `6107b4c835a6cb36b54e986f66bff117f83bc2c6` mostra **cinco arquivos experimentais e um único commit**, sem produção:

1. `prototipos/autoatualizador-seguro/phase1-composed/phase1_lab.py`: acrescenta só a mutação de TESTE entre o hash e o `Popen` original, valida processo criado e identidade de imagem observada via Win32, sem conceder sucesso.
2. `prototipos/autoatualizador-seguro/phase1-composed/Phase1RecoveryTests.cs.txt`: ramo adversarial negativo no **mesmo L18** requer processo criado, observação de hash divergente e duas reconciliações `UnknownBlocked`, com OLD/NEW/canário preservados; não autoriza retry/rollback.
3. `prototipos/autoatualizador-seguro/phase1-composed/GfOldDraftAcquire.cs.txt`: seleciona modo `--real-install-posthash-focused` somente com flags limitadas à CI e à instalação original; não cria outro canal.
4. `prototipos/autoatualizador-seguro/phase1-composed/github_install_focused_normal.ps1`: usa `-TamperPosthash`, reextrai NEW pelo TUF existente, compara todos os 284 NEW e 284 OLD e confere Win32/processo/manifesto/sentinela e dados pessoais.
5. `.github/workflows/gf-github-draft-e2e.yml`: **um** job novo dependente da instalação positiva em runner Windows limpo. Os quatro jobs anteriores continuam aprovados.

**Nenhuma alteração** em `main`, `Program.cs`, código distribuído, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, H1/H2/H3, permissões, chaves, deploy, workflow de publicação ou mecanismo de recuperação. Nenhum WORK consumido.

## Draft e dados preservados

- Draft privada **429695537**, tag `gf-e2e-c5057bbbaf1d`, `draft=true`, `published_at=null`, cinco assets.
- ZIP **694233137** `gf-e2e-new-win-x64.zip` — 57.695.630 bytes; SHA-256 `3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`.
- Manifesto original **694233139** `GF-LAB-REAL-PACKAGE-MANIFEST.json` — 72.618 bytes; SHA-256 `a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`.
- Metadados **timestamp/snapshot/targets de TESTE** renovados pela workflow antiga; ZIP e manifesto originais retiveram ID, tamanho e hashes.
- Não houve recuperação, rollback de atualização, limpeza de arquivos pessoais, retry, READY, merge, deploy ou publicação. A finalização do único **processo filho de TESTE** pelo seu handle, quando a verificação posterior falha, não constitui recuperação da atualização.

## Próximo passo estreito; NÃO reproduzir os cenários já aprovados

**Bloqueador demonstrado:** usar somente hash prévio e `Popen` por pathname deixa uma janela TOCTOU real, mesmo que a detecção posterior falhe fechada. A próxima prova funcional mínima deve avaliar o **objeto de arquivo NTFS efetivamente mantido/pinado** no intervalo hash → criação de processo, por exemplo travar substituição/escrita com handles de compartilhamento restrito durante a verificação e a chamada de lançamento, e demonstrar em Windows descartável que uma mutação concorrente é recusada enquanto o processo legítimo ainda é iniciado. Inspecionar restrições do loader Windows e identidade do objeto; não confundir nome/caminho/PID ou bloqueio em um ensaio com garantia completa de bytes executados.

Não prometer proteção TOCTOU total, prova em falha elétrica, persistência segura, `READY`, rollback, recuperação, limpeza ou autoatualizador público. Preservar a arquitetura B+A, L18, TUF TESTE, executável portátil original e o modo de contenção `UnknownBlocked/HOLD`.
