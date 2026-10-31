# CHAT ALTO — GF OLD em abertura normal conclui download real da Draft, sem handoff

**31/10/2026. Prova funcional concluída no Windows GitHub Actions descartável. LABORATÓRIO APENAS; READY=false.**

## Referências congeladas

- Repositório `walissonminel-1991/gerenciador-final`; única branch escrita `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental de partida: `73e842d59f67bd1aa81e862cdcdbd78e34c27f28`.
- Commit técnico desta etapa: **`c8b577e2723abe1aa0d21be5a52e315ad256d9b6`** — um commit com exatamente cinco arquivos alterados/criados.
- **CI Windows:** [41205651516](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41205651516), job **124550080017**, `completed/success`.
- CI D2.4.4 no mesmo SHA: [41205651542](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41205651542), `completed/success`.
- Prova anterior de falha HTTP com interface normal: [41204734164](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41204734164), `SUCCESS`, preservada nesta execução.

## Lacuna funcional escolhida e resultado

**Objetivo:** provar o caminho de **sucesso do download**, não outra interrupção: o GF OLD real de laboratório, versão `5.13.11.65`, aberto normalmente, sem `--tray` e sem `ShowWindow` artificial, inicia o downloader já existente por `MainForm.Shown`, obtém os **cinco assets da Draft real e privada** pelo mesmo `.NET HttpClient` e continua com janela nativamente visível e responsiva após concluir a transferência. O observador externo Win32 também constata inventário não vazio, pesquisa de nonce sem correspondência e restauração do filtro.

**APROVADO NA CI 41205651516.** O observador confirmou janela visível ANTES de concluir o download. O recibo do OLD foi somente o gatilho para verificações independentes da imagem do processo, HWND próprio, desktop interativo, message pump, transição real da pesquisa, bytes do ZIP e manifesto e TUF. Nenhum botão de manutenção foi acionado.

### Saída relevante dos logs Windows

```text
GF_E2E_GITHUB_NORMAL_OLD_COPY_PASS files=284
GF_E2E_GITHUB_NORMAL_MAINFORM_HEALTH_PASS real_draft=COMPLETE normal_gui=VISIBLE+RESPONSIVE inventory=NONZERO search=EMPTY+RESTORED OLD_running=true NEW_executed=false L18_not_invoked=true READY=false
GF_E2E_GITHUB_NORMAL_BYTES_PASS pinned_zip=true pinned_manifest=true old_files=284 canary_unchanged=true NEW_not_executed=true READY=false
GF_DRAFT_AUTHENTICATED {"downloaded_archive_sha256": "3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e", "extracted_files": 284, "lab_old_requires_l18_binding": true, "new_executable_on_disk_verified": true, "old_files_preserved": true, "operational_ready": false, "source": "github_draft_release_assets", "test_only": true, "tuf_manifest_sha256": "a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d"}
GF_E2E_GITHUB_NORMAL_MAINFORM_PASS complete_remote_draft=true TUF_verified=true normal_UI_healthy=true L18_not_invoked=true READY=false
```

Na mesma execução, os testes anteriores de falha de transporte real, continuidade de interface, corte L18 entre renomeações, HOLD e seis negativos de integridade permaneceram aprovados. A prova positiva desta etapa **NÃO executou L18**, não trocou versão nem lançou NEW.

## Alterações técnicas estritamente de laboratório

1. `Program.cs`: somente em `#if GF_E2E_LAB`, separa o recibo `gf-e2e-github-normal-startup-shown.json`.
2. `GfOldDraftAcquire.cs.txt`: a flag adicional `GF_E2E_GUI_NORMAL_SUCCESS` cria diretório/recibos separados da prova positiva; mesmas rotinas de metadata e download, URLs, guardas e hashes. Impede explicitamente handoff, fechamento voluntário de atualização, troca de instalação e teste de corte neste modo.
3. `github_normal_gui_success_observer.py`: observa a mesma interface, reutilizando `win32_process_observation.py` e a inspeção conhecida de consentimento; inicia sem `--tray` e nunca invoca `ShowWindow` no caso positivo.
4. `github_normal_gui_success.ps1`: prepara cópia isolada e byte a byte do OLD (284 arquivos), verifica download e hashes do ZIP/manifesto, executa o **verificador Python-TUF existente** e reafere OLD, canário, seletor e ausência de execução NEW.
5. `.github/workflows/gf-github-draft-e2e.yml`: um único passo após a prova normal negativa, usando somente `windows-2022` e `RUNNER_TEMP`.

Nenhuma arquitetura paralela de atualização, download ou seleção foi criada. O encerramento do processo filho observado ao final é limitado ao processo descartável de TESTE e não é rollback/limpeza/recuperação do GF.

## Pacote e controles preservados

- Release **Draft 429695537**, tag `gf-e2e-c5057bbbaf1d`: ao final da CI, `draft=true`, `published_at=null`, **cinco assets**.
- ZIP `gf-e2e-new-win-x64.zip`: ID **694233137**, **57.695.630 bytes**, SHA-256 **`3eadafed91c4090ea56c9e9be8175f8ece8acf05d9c7fda9f97db7644a33011e`**.
- Manifesto `GF-LAB-REAL-PACKAGE-MANIFEST.json`: ID **694233139**, **72.618 bytes**, SHA-256 **`a00de694e4418582e4e91231dc9f9bdd10ef541425479e1ce7e54477709f6e4d`**.
- Somente os três roles TUF de TESTE foram atualizados pela CI existente; ZIP e manifesto retiveram IDs e hashes.
- A edição compilada segue `GfBetaProfile=local`, com diagnósticos colaborativos remotos indisponíveis. A prova não responde nem grava consentimento; um diálogo inesperado é tratado como anomalia separada do atualizador. Isso **não** testa a política de consentimento BETA conectada.
- OLD original e cópia preservados em 284 arquivos por verificação independente; canário pessoal e seletor L18 sem mutação; ausência de transição concluída, NEW não executado, sem retry, rollback, limpeza ou READY.

**Fronteiras:** H1/H2/H3, TOCTOU, durabilidade física/reboot, recuperação transacional e implantação real continuam não comprovadas. Não se demonstrou atualização pronta para usuários nem handoff seguro nesta abertura normal. A validação TUF usa chaves de TESTE e a Draft privada existente; o código de laboratório não compõe o binário GF distribuído. A janela saudável não equivale a READY de atualização.

**Sem alterações:** `main`, PR #65 Draft, BETA .66, Cloudflare, PCs pessoais, instalações reais, publicação, merge, deploy, permissões ou infraestrutura de produção. **WORK não utilizado.**

## Próxima continuidade controlada

A prova de **download autêntico completo com MainForm normal saudável** está encerrada. A lacuna operacional seguinte é a coordenação da passagem para a atualização já autenticada, mas **não se deve confundir transferência e validação com autorização de fechamento do OLD, troca de arquivos, lançamento NEW ou recuperação**. Selecionar somente UM teste que conecte os componentes existentes, sem autorizar rollback, recuperação, limpeza, retry ou READY por inferência; exigir prova verificável em Windows descartável e respeitar H1/H2/H3/TOCTOU.
