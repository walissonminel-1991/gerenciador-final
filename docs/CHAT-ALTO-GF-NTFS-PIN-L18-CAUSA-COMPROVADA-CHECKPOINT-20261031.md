# CHAT ALTO — NTFS pin L18: causa demonstrada, teste Windows PASS, READY=false

**Data:** 2026-10-31. **Escopo:** autoatualizador real do GERENCIADOR FINAL, somente experimento Windows descartável. **Estado:** causa da rejeição original identificada; correção mínima do classificador de teste validada; **TOCTOU geral não resolvido; READY=false**.

## Base imutável e escopo

- Repositório: `walissonminel-1991/gerenciador-final`.
- Branch: `experimento-g5-ipc-chat-alto-20261029`; HEAD documental inicial: `f0503db355386de7b7b21e1b06efdcee6e925af8`.
- Erro de origem: [CI 41220121701](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41220121701), job NTFS `124594207026`: OLD normal saiu, seleção NEW e marcadores de fechamento presentes, L18 finalizou sem relatório; `GF_INSTALL_FOCUSED_HANDOFF_NOT_PROVEN`.
- Diagnósticos/correção já presentes na branch antes deste checkpoint (quatro commits descendentes do HEAD inicial): `28d2aa29324edcea0dfb1bddd7c348ae82c45586` (códigos do erro real do L18), `1138c3927c64ccd8747f38945d654910f8bf444a` (exceção do coordenador sanitizada), `0cd7acd996187f1f348e523236acbe9a91b4a0c6` (códigos separados de append/rename, hashes), `a587038a6aabfd414963d4854b0ae517f14c4588` (menor correção de classificação observada). Não houve modificação adicional de código nesta rodada.

## Causa exata do L18 sem relatório

A instrumentação no caminho real de `--real-install-focused` expôs `Rejected` e os códigos encadeados `REAL_NORMAL_OWNER_FAILED`, `REAL_OBSERVER_FAILED`, `GF_NTFS_PIN_WRITE_OR_RENAME_NOT_DENIED`. Fonte: [CI 41224134469, job 124606150927](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41224134469).

A prova de erros Win32/CRT é a [CI 41225227588, job 124609252440](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41225227588):

```text
GF_PIN_ATTACK_OBSERVATION write_denied=False write_class=PermissionError write_winerror= write_errno=13 rename_denied=True rename_class=PermissionError rename_winerror=32 rename_errno=13 pinned_unchanged=True path_unchanged=True rename_probe_exists=False READY=false
GF_PIN_FOCUSED_L18_FAILURE class=Rejected base=Rejected reason=REDACTED codes=REAL_NORMAL_OWNER_FAILED,REAL_OBSERVER_FAILED,GF_NTFS_PIN_WRITE_OR_RENAME_NOT_DENIED,GF_PHASE1_GF_NTFS_PIN_WRITE_OR_RENAME_NOT_DENIED retained=True READY=false
```

Diagnóstico: a tentativa de `exe.open("ab")` falhou com `PermissionError(errno=EACCES=13)` e `winerror=None` no Python 3.13/Windows. O classificador original só aceitava `winerror in (5,32)`; por isso sinalizou `write_denied=False`, embora o append tenha sido impedido. A tentativa de renomeação devolveu `winerror=32` e foi corretamente classificada como bloqueada. Os hashes do objeto mantido por handle e do caminho permaneceram iguais. O observador fez `require(...)`, lançou `GF_NTFS_PIN_WRITE_OR_RENAME_NOT_DENIED`; o L18 propagou o erro para o proprietário, saiu com rejeição e não gravou relatório de sucesso. O PowerShell apenas detectou a ausência desse relatório após 240 s; **não foi demonstrado crash do loader, bug NTFS geral ou execução do NEW no cenário que falhou**.

## Menor correção e garantia contra falso positivo

No commit [`a587038a6aabfd414963d4854b0ae517f14c4588`](https://github.com/walissonminel-1991/gerenciador-final/commit/a587038a6aabfd414963d4854b0ae517f14c4588), `phase1_lab.py` aceita, para o teste específico, `winerror=None` **somente** junto com `isinstance(exc, PermissionError)` e `errno==13`; preserva reconhecimento anterior de WinError 5/32. Exige controle negativo: abertura `ab` sem escrever bytes **antes** de adquirir o handle, seguida de hash idêntico. Exige também negativa de renomeação e hashes/identidade inalterados. Não modifica `CreateFileW`, `GENERIC_READ`, `FILE_SHARE_READ` exclusivo, critérios de observação ou integração de produção. O controle negativo abre o arquivo sem gravar bytes; não é medição exclusivamente de leitura de metadados.

## Prova Windows após correção

[CI 41226223387](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41226223387) no SHA `a587038a6aabfd414963d4854b0ae517f14c4588`: **SUCCESS, seis de seis jobs**, incluindo job NTFS `124612165438` **SUCCESS**. Evidência do log do job:

```text
GF_E2E_INSTALL_FOCUSED_NORMAL_OLD_PASS native_mainform=true responsive=true no_tray=true READY=false
GF_E2E_INSTALL_FOCUSED_OLD_CLOSE_PASS normal_gui=true NEW2_fenced=true FormClosed=true ApplicationRunReturned=true exit0=true READY=false
GF_E2E_NORMAL_INSTALLED_NEW_BYTES_PASS old_normal=true old_graceful_exit=true installed_new_path=true files_old=284 files_new=284 installed_equals_tuf=true new_executed_installed_path=true readonly_health=true canary_preserved=true HOLD=UnknownBlocked recovery=false rollback=false cleanup=false retry=false READY=false
GF_E2E_NTFS_PIN_INDEPENDENT_PASS open_file_object_id=STABLE read_only_share=true concurrent_append=DENIED concurrent_rename=DENIED pin_continued_through_Popen=true original_NEW_created=true functional_observation=true installed_bytes_still_authenticated=true no_recovery=true no_rollback=true no_retry=true READY=false
GF_E2E_NTFS_PIN_WINDOWS_PASS original_old_normal=true original_new_installed=true read_only_object_pin=true concurrent_write_delete_denied=true legit_NEW_executed=true HOLD=true READY=false
```

Isso é prova **limitada ao laboratório, ao handle e ao cenário executado**. Não prova bytes efetivamente mapeados pelo loader, segurança contra handles de escrita já preexistentes, outros objetos de arquivo, todas as corridas TOCTOU ou prontidão de produção. Continua `UnknownBlocked/HOLD`, sem recuperação, rollback, limpeza ou retry. Nenhuma promoção de `READY`.

## Preservação e continuidade

Preservados e não modificados: Draft privada `429695537`; ZIP `694233137`; manifesto `694233139`; `main`; PR #65 Draft; BETA .66; Cloudflare; PCs pessoais; H1/H2/H3; proteções TOCTOU; `READY=false`. Nenhum WORK, merge, deploy, publicação, recuperação, rollback, limpeza ou retry foi invocado nesta rodada.

**Checkpoint:** missão de identificar e corrigir a rejeição observada do teste NTFS concluída com CI descartável positiva. Próxima etapa somente mediante autorização explícita: avaliar a lacuna ainda aberta entre objeto pinado e bytes mapeados; não confundir aprovação do job com conclusão da Fase 1.
