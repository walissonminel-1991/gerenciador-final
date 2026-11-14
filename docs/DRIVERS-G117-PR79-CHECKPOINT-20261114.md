# GERENCIADOR FINAL — Drivers G1.17: coerência do histórico próprio e rodapé

**Data:** 14/11/2026 · **PR #79 Draft** · branch `drivers-pnp-problemas-consulta-20261113` · base `candidata-portatil-pessoal-pc-forte-20261101`. Roadmap **27/42**; G1–G5 abertos.

## 1. Gate G1.16 confirmado antes das edições
SHA congelado `1baa96f9c7159495d9a97a43b8e9ed5feebcb9e6`; CI [Windows 43616509234](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43616509234) e [integridade 43616509229](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43616509229) **completed/SUCCESS no mesmo SHA**. Windows 2022/2025 com **24/24 G1.9 por runner**; WinForms G1.10–G1.16, regressões, mutações negativas, segurança/privacidade, consentimento e compilação x64 aprovados. PR aberta/Draft, sem merge.

## 2. Contraexemplos do caminho real
- **F1 — rodapé histórico da fotografia anterior:** `DriversPlaceholder.RunRefresh` publicava `snapshot/lastResult` da nova leitura, mas mantinha `history.Text`/tooltip/descrição acessível da comparação histórica anterior até `DriverHistory.Record` concluir. A lista e o rodapé podiam referir-se a fotografias diferentes. Quando `readForTest` é sintético, gravação não é executada, então a comparação anterior podia permanecer **indefinidamente**.
- **F2 — nenhuma associação WMI confundida com arquivo inválido:** leitura WMI considerada completa com só entradas `WithoutSignedDriverAssociation=true` levava `DriverHistory.Snapshot` a lançar `InvalidDataException` e `Record` retornava mensagem genérica `"Histórico indisponível ou inválido"` apesar de o arquivo anterior estar íntegro. O caso deve distinguir **ausência de associações observadas** de corrupção do histórico.

## 3. Menor correção demonstrável
- `DriversPlaceholder.cs`: assim que a nova lista é publicada, o histórico é **rotulado como comparação em andamento**; a via sintética é explicitamente **não gravada / não comparada**. Limpa rótulo, descrição acessível e tooltip anteriores **apenas após publicação válida**. Cancelamento/timeout antes da publicação mantém histórico e lista anteriores intactos. A gravação real continua com a mesma transação e atualiza o rodapé ao terminar; não amplia ações nem cria polling.
- `DriverHistory.cs`: `Record` retorna uma mensagem específica para inventário completo **sem associações Win32_PnPSignedDriver observadas**, sem tratar como corrupção e sem gravar o v1. Mantém `Snapshot` conservador, lock, validação de schema, hash, atomicidade, controle de relógio, 20 mudanças e proteção contra consultas parciais.
- `TestesLayout/DriverHistoryPanelCoherenceTests.cs`: teste do painel WinForms real em dados sintéticos, sequência de snapshots + consulta cancelada com retorno tardio, acessibilidade/tooltip e 500×340, 790×510, 1200×720 e 1450×820; `DriverHistory.Record` em diretório temporário **exclusivo e descartável** para provar baseline, imutabilidade ante leitura parcial e ausência WMI, comparação sem falsa mudança pela ordem, `Missing` = **não observado** e recusa de relógio regressivo. NÃO utiliza `DriverHistory.DefaultPath`, WMI, WUA, SetupAPI, INF ou PC pessoal. A fixture de consulta do painel não grava histórico pessoal.
- `TestesLayout/Program.cs`: registra G1.17 no gate `--drivers-readonly`.

**Limitações:** testes temporais G1.10–G1.16 ainda cobrem diálogo modal, cancelamento, timeout, resultados tardios, fechamento e reabertura. A fixture G1.17 do painel injeta providers sintéticos; **não prova o timing de gravação real no PC**, DPI físico ou interação real com leitor de tela.

## 4. Gate final
**PENDENTE no commit candidato**: CI Windows 2022/2025 com 24/24 G1.9 por runner, WinForms G1.10–G1.17, regressões, testes visuais, mutações negativas, segurança/privacidade, consentimento, compilação Windows x64 e integridade `completed/SUCCESS` no **MESMO SHA**. Sem GO antes disso.

**Invariantes:** `CanInstall=false`, `CanMaintain=false`, `IsNewerProven=false`, `CompletePackageVerified=false`, snapshots, unicidade global DeviceId, G1.9/G1.10, privacidade, STA/COM e descarte de retornos tardios. `WaitAsync` cancela espera, não interrompe `Search` COM síncrono. Nenhuma evidência comprova versão superior ou compatibilidade de pacote remoto.

**Proibido:** download, URL de conteúdo, staging, instalação, atualização, desinstalação, rollback, elevação, política, reinício, monitoramento/timer de produção. Não tocar `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE/FRACO. PR #79 permanece Draft, roadmap 27/42, G1–G5 abertos.

**Próxima G1.18 proposta:** auditar exclusivamente semântica de falha/concorrência do histórico local e integridade da mensagem acessível, incluindo arquivo v1 inválido, alteração de relógio e transições do painel; preservar a estrutura v1 e a regra de nenhuma manutenção de drivers.
