# GERENCIADOR FINAL — Drivers G1.16: histórico Windows e INFs publicados parciais

**Data:** 14/11/2026. **PR #79 Draft**, branch `drivers-pnp-problemas-consulta-20261113`, base `candidata-portatil-pessoal-pc-forte-20261101`. Roadmap **27/42**; G1–G5 abertos.

## Gate inicial G1.15 revalidado
Commit `cc444ced0d6c06cc0213cd0d25409a5d75235233`. [CI Windows 43612266184](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43612266184) e [integridade 43612266171](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43612266171) ambas `completed/SUCCESS` no mesmo SHA. Windows 2022/2025 **24/24 verificações G1.9 por runner**; WinForms G1.10–G1.15, regressões, mutações, segurança, privacidade, consentimento e compilação Windows x64 aprovados. A primeira tentativa Windows 2022 falhou na leitura WMI/Store nativa por `TaskCanceledException` no `WaitAsync` de **25s**, e o **mesmo SHA** passou após reexecução do job, sem alterar o timeout. PR aberta Draft, sem merge.

## Contraexemplos reproduzidos estaticamente e nas fixtures
- **F1 — histórico não consultado mas apresentado como trecho lido:** `DriverReadOnlyReport.Format` podia dizer `"Nenhum vínculo histórico explícito encontrado no trecho lido"` com `WindowsHistory=null` ou `Unavailable=true` e nenhum evento. Não havia trecho comprovadamente lido.
- **F2 — histórico parcial descrito sem limite no dispositivo:** fonte com `Partial/TailOnly/RejectedSections` não diferia da leitura completa no aviso por dispositivo sem eventos; ausência de vínculo observado não exclui instalação em trecho perdido.
- **F3 — lista de INFs parciais parece total:** `DriverPublishedAuditResult.Summary` iniciava `"INFs publicados: 0"` mesmo com coleção parcial/vazia, sugerindo 0 pacotes publicados no Windows. Os números são **somente observações da captura**, não inventário total.
- **F4 — seção detalhada não esclarecia falta ou parcialidade de enumeração:** sem `PublishedPackages` ou com coleção parcial/vazia, o detalhamento não distinguia ausência de informação de ausência de INF.

## Correção mínima
- `DriverReadOnlyReport.cs`: distingue `WindowsHistory=null`, fonte indisponível, parcial/tail/rejeições e leitura completa. Em histórico indisponível não apresenta eventos injetados como resultado observável. Em lista publicada nula/parcial, esclarece **não enumeração** e que ausência de registro não é prova de ausência de pacote.
- `DriverPublishedPackages.cs`: o resumo agora usa `"INFs publicados observados nesta consulta"`, qualifica enumeração/associação parcial e avisa que a contagem não prova o total.
- `TestesLayout/DriverReadOnlyDialogHistoryTests.cs`: teste com `DriverReadOnlyReport.Format` real e `ShowDriverReadOnlyReport` WinForms modal real, fonte histórica sintética ausente, indisponível, parcial, tail-only, trechos rejeitados; pacotes publicados sintéticos não consultados, completos, parciais e vazios; viewports **500×340** e **790×510**, acessibilidade e guard de geração obsoleta; sem acessar histórico pessoal ou motores nativos.
- `TestesLayout/Program.cs`: registra teste G1.16 no gate `--drivers-readonly`.

Não alterar `DriverHistory` persistente, `DriverQueryCoordinator`, WMI, WUA, APIs SetupAPI, `DriverInfVerification`, cancelamento, timeouts, callbacks ou motores. G1.10–G1.15 seguem exercitando cancelamento, timeout, retorno tardio e reabertura. Sem afirmar que testes sintéticos comprovam execução física real.

## Gate e decisão
**PENDENTE no commit candidato:** CI Windows 2022/2025 com **24/24 G1.9 por runner**, WinForms G1.10–G1.16, regressões gerais, mutações, visuais, segurança/privacidade, consentimento, compilação Windows x64 e integridade **completed/SUCCESS no mesmo SHA**. Sem GO global antes disso.

`CanInstall=false`, `CanMaintain=false`, `IsNewerProven=false`, `CompletePackageVerified=false`. `WaitAsync` cancela a espera, não interrompe COM síncrono. Não inferir compatibilidade remota, `DriverVer`, `TargetOSVersion`, ranking ou assinatura remota.

Sem download, URL de conteúdo, staging, instalação, atualização, desinstalação, rollback, elevação, política, reinício, monitoramento ou timer de produção. Não tocar `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE ou PC FRACO. PR #79 permanece Draft, roadmap 27/42 e G1–G5 abertos.

**G1.17 proposta:** auditar exclusivamente coerência entre o resumo do histórico local persistido e os avisos consultivos de observação parcial/ausente nos painéis, sem novas ações de manutenção; primeiro procurar contraexemplo e somente então corrigir.
