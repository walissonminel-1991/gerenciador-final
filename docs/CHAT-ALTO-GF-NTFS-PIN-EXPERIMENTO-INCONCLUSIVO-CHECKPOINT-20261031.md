# CHAT ALTO — Proteção NTFS entre hash e Popen: prova INCONCLUSIVA / BLOQUEADA

**31/10/2026 — laboratório Windows GitHub Actions, READ-ONLY diagnóstico consolidado. READY=false.**

## Base verificada
- Repositório `walissonminel-1991/gerenciador-final`; branch exclusiva `experimento-g5-ipc-chat-alto-20261029`.
- HEAD documental originalmente solicitado: `fff62820ee7437a58992fbfc5916fb512d34d837`.
- Durante a inspeção, a branch já havia avançado para `4f1fb1b07b3004bde8b73f8619a92d5134d44114` (parent `ebac49635175b20e07d700ec4836e22bd00f49c0`), com uma implementação experimental `_ntfs_read_pin` no mesmo `phase1_lab.py`, executor `-PinNtfsLaunch` e workflow dedicada. Para evitar trabalho concorrente, este registro **não altera** código nem invoca outra arquitetura.
- CI relevante: [41220121701](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41220121701), `completed/failure` no SHA `4f1fb1b07b3004bde8b73f8619a92d5134d44114`.
- Job novo `Handle NTFS pinado bloqueia escrita e renomeacao e permite NEW autentico abrir`: **124594207026**, **FAILURE**. Outros cinco jobs do mesmo run, inclusive instalação positiva, recusa pré-hash, contraexemplo pós-hash e corte pós-segunda renomeação, **SUCCESS**.
- Workflows de regressão adicionais para o mesmo SHA: [41220121705](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41220121705) SUCCESS e [41220121750](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41220121750) SUCCESS.

## Mecanismo NTFS já introduzido

- `phase1_lab.py:_ntfs_read_pin`: `CreateFileW` no EXE instalado com `GENERIC_READ` e somente `FILE_SHARE_READ` (sem `FILE_SHARE_WRITE` nem `FILE_SHARE_DELETE`), seguido de leitura SHA-256 **no mesmo handle** e captura de serial de volume / file index com `GetFileInformationByHandle`.
- Durante esse handle, testa tentativa de append e tentativa de rename; ambas devem falhar e o hash/identidade do handle devem permanecer estáveis. O mesmo `observed_process` deve manter o handle até terminar o `Popen` legítimo e a observação funcional.
- Escopo guardado por `GITHUB_ACTIONS`, repositório exato, `GF_E2E_L18_OBSERVE_INSTALLED_NEW=1`, `GF_E2E_LIVE_INSTALL_SWAP=1`, ausência dos outros modos de mutação; não é componente de distribuição.
- Mesmo que aprove no futuro, este padrão não comprovará os bytes mapeados em memória e não impedirá necessariamente escritas de handles anteriores já autorizados. `FILE_SHARE_READ` não é uma garantia TOCTOU abrangente.

## Resultados efetivamente observados

No job novo, os logs comprovam apenas:
```text
GF_NTFS_PIN_IMPORT_PREFLIGHT_PASS
GF_E2E_INSTALL_FOCUSED_OLD_BASELINE files=284
GF_E2E_INSTALL_FOCUSED_NORMAL_OLD_PASS native_mainform=true responsive=true no_tray=true READY=false
GF_PIN_HANDOFF_DIAGNOSTIC old_exited=True selected=True report=False closed=True loop=True READY=false
GF_PIN_L18_PROCESS alive=False READY=false
GF_INSTALL_FOCUSED_HANDOFF_NOT_PROVEN
```

O job esperou quatro minutos após a abertura normal; o OLD saiu, os marcadores de seleção e fechamento foram gerados, porém **não houve relatório final L18**; o processo L18 já não estava ativo. Assim, o teste de handle NTFS **não passou**, e não há prova da combinação `write_denied=true`, `rename_denied=true`, objeto inalterado e `Popen` legítimo criado. O histórico inclui falha equivalente na CI [41218471981](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/41218471981).

A origem exata do término do L18 **não foi demonstrada** pelos logs de CI apresentados; não presumir conflito do loader ou garantia de compartilhamento. A ausência do relatório não pode ser transformada em `READY`, contenção corretamente provada ou êxito da abordagem NTFS.

## Conduta e menor continuidade permitida

Este checkpoint é estritamente diagnóstico. Nenhuma alteração funcional, reexecução, correção de share mode, recuperação, rollback, cleanup, retry ou promoção é autorizada por ele. A menor próxima missão é **expor o erro real do processo proprietário/observador L18** em arquivo ou log somente leitura, com limites de tamanho e sem dados privados, para separar `CreateFileW`/identidade, tentativa de escrita, `Popen`, observação de UI e falha de orquestração. Só depois ajustar exclusivamente o caminho comprovadamente defeituoso e repetir um único teste Windows descartável. Não desabilitar checks nem fazer fallback para `Popen` solto.

Preservados: Draft privada **429695537**, pacote/manifesto originais (ZIP asset **694233137**, manifesto asset **694233139**), `main`, PR #65 Draft, BETA .66, Cloudflare, computadores pessoais, H1/H2/H3, TOCTOU, `UnknownBlocked/HOLD` e `READY=false`. Sem WORK, merge, deploy ou publicação.
