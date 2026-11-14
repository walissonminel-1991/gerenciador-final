# GERENCIADOR FINAL — Drivers G1.19: identidade global e histórico consultivo v1

**Data:** 14/11/2026 · PR #79 Draft · branch `drivers-pnp-problemas-consulta-20261113` · base `candidata-portatil-pessoal-pc-forte-20261101`. Roadmap 27/42; G1–G5 abertos.

## Gate G1.18 de entrada verificado
SHA `5a1eeb4c43a5a12570a07c202973f6b80d8a549d`. CIs [Windows 43630027130](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43630027130) e [integridade 43630027134](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43630027134): completed/SUCCESS, attempt 1, mesmo SHA; Windows 2022/2025 24/24 G1.9 por runner, WinForms G1.10–G1.18, 36/36 mutações, regressões visuais/gerais, segurança/privacidade, consentimento, compilação x64 e integridade.

## Contraexemplos do código G1.18
- **F1: colisão global de DeviceId no histórico.** `DriverHistory.Snapshot` agrupava DeviceId e incorporava associações divergentes no hash composto. Mesmo duas linhas idênticas eram aceitas, embora o guard G1.9 não as considere identidade única. Isso podia substituir um arquivo v1 válido por fotografia inconclusiva.
- **F2: duplicata idêntica ocultada na captura.** `DriverInventory.Correlate` descartava linhas duplicadas antes do guard, impedindo a interface de detectar que a mesma instância aparecia mais de uma vez.
- **F3: tooltip do estado não acompanhava o histórico acessível.** `DriversPlaceholder` primeiro sincronizava o texto acessível com o histórico, mas logo substituía a dica de estado somente por estado e sumários das fontes, omitindo o rodapé recém-publicado.

## Menores correções, sem alterar schema
- `DriverHistory.Snapshot`: exigir DeviceId global válido e único após normalização, inclusive entre linhas WMI associadas e sem associação; rejeitar divergência entre contagem bruta `SignedRows` e linhas associadas (quando informada), inclusive se uma correlação preexistente tiver omitido linha. Persistência v1 continua com **o mesmo hash SHA-256 por dispositivo único** e mesma estrutura, lock, atomicidade, limite e retenção. `Record` continua preservando os bytes anteriores se a evidência for inconclusiva.
- `DriverInventory.Correlate`: não eliminar duplicatas idênticas. Mantê-las consultivas e visíveis permite ao guard G1.9 e ao relatório reconhecer ambiguidade global.
- `DriversPlaceholder`: tooltips do estado e histórico passam a incluir seus textos/descrições acessíveis atuais; sem timer, motor ou ação nova.
- `TestesDrivers/Program.cs` atualiza expectativas para rejeitar duplicatas em vez de tratá-las como fotografia histórica válida. `TestesLayout/DriverG119HistoryIdentityTests.cs` exercita comparador/persistência real em diretório temporário descartável, relatório e painel WinForms real com fontes exclusivamente sintéticas. Também executa viewports 500×340, 790×510, 1200×720 e 1450×820, teclado, filtro, descrição acessível e tooltip. G1.10–G1.18 mantêm os testes de cancelamento, timeout, retorno tardio, foco, fechamento e reabertura. Não abrir DriverHistory.DefaultPath, nem chamar APIs WMI/WUA/SetupAPI nas fixtures.

**Invariantes:** CanInstall=false, CanMaintain=false, IsNewerProven=false, CompletePackageVerified=false. Nada no histórico comprova desinstalação, resíduo, versão superior, ranking, assinatura remota ou compatibilidade. WaitAsync não encerra Search COM síncrono. Nenhuma modificação em main, G5, D1, Worker, Cron, Secrets, BETA, releases ou PCs pessoais.

## Gate final
**PENDENTE DE CI NO SHA CANDIDATO.** Exigir Windows 2022/2025 24/24 G1.9 por runner, WinForms G1.10–G1.19, regressões gerais, testes visuais, mutações negativas, segurança/privacidade, consentimento, compilação Windows x64 e integridade externa `completed/SUCCESS` no MESMO SHA. GO restrito apenas após confirmação; documentar falhas e reexecuções.

**G1.20 sugerida:** auditar a temporalidade da publicação do painel versus persistência do histórico, em particular descarte da página e reentrada de consulta após gravação atrasada; conservar histórico anterior no caso de snapshot inconclusivo e sincronizar permanentemente as mensagens acessíveis, sem nova ação de manutenção.
