# GERENCIADOR FINAL — Drivers G1.18: timestamp e recuperação segura do histórico v1

**Data:** 14/11/2026. **PR #79 aberta/Draft/sem merge**, branch `drivers-pnp-problemas-consulta-20261113`, base `candidata-portatil-pessoal-pc-forte-20261101`. Roadmap **27/42**; G1–G5 abertos.

## Gate de entrada G1.17 comprovado
SHA `b82b39bf6ccd99c23bb5cef70b493cce42611551`. [Windows 43618902056](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43618902056) e [integridade 43618902079](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43618902079): **completed/SUCCESS** no mesmo SHA. Windows 2022/2025 **24/24 verificações G1.9 por runner**; G1.10–G1.17 WinForms, regressões gerais, mutações negativas, testes visuais, política de segurança/privacidade, consentimento, compilação Windows x64 e integridade aprovados. PR Draft, sem merge.

## Contraexemplo F1 — duas fotografias com timestamp idêntico
`DriverHistory.Record` usava `if (at < previous.At)`, portanto duas gravações de conteúdo divergente, porém com `DateTimeOffset` **igual**, eram aceitas sequencialmente, substituindo a associação anterior com a mesma marca temporal. O v1 descreve uma linha do tempo; não é seguro aceitar ordem não comprovada. **Menor correção:** exigir `at > previous.At`, falhando fechado em igualdade (`at <= previous.At`) com mensagem “Histórico preservado: data não posterior à última consulta”. Não altera layout/serialização nem adota um novo sistema de versionamento.

## Conjunto adversarial real do v1, sem mexer em dados do usuário
`TestesLayout/DriverHistoryRecoveryTests.cs` usa somente pasta temporária aleatória e descartável; **nunca** `DriverHistory.DefaultPath`. Prova:
- igualdade e regressão do relógio rejeitadas **byte a byte**, sem substituir o arquivo anterior;
- duas chamadas concorrentes com mesmo timestamp: exatamente uma pode salvar, pelo `Gate` e nova monotonicidade;
- bloqueio de outra instância via `.lock`: falha segura, bytes intactos, nenhum temporário criado;
- JSON truncado, schema 99 e arquivo maior que **2 MB**: recusa sem reconstrução silenciosa;
- temporário órfão sintético por interrupção: não substitui arquivo v1 válido; o histórico usa apenas transação própria e não promete recuperação automática de órfão;
- snapshot parcial, vazio e sem associação WMI: base anterior preservada;
- retenção de no máximo **20 mudanças**, ausência de dados PnP/WMI brutos no JSON v1 e limpeza de temporários criados pela própria gravação.

`TestesLayout/Program.cs` registra G1.18 no gate `--drivers-readonly`. Os testes WinForms G1.10–G1.17 continuam validando painel real, diálogos, cancelamento, timeout, retorno tardio, foco, filtros e acessibilidade em viewports 500×340, 790×510, 1200×720 e 1450×820. A fixture G1.18 de persistência é isolada do painel para não abrir nenhum histórico pessoal. Não afirma homologação física, DPI alternativo ou leitor de tela externo.

## Arquivos e invariantes
Somente **quatro arquivos** foram alterados em relação ao SHA G1.17: `DriverHistory.cs` (comparador e mensagem), `TestesLayout/DriverHistoryRecoveryTests.cs` (novo teste sintético), `TestesLayout/Program.cs` (registro) e este checkpoint. **Nenhuma mudança** em `DriversPlaceholder.cs`, formatação acessível, WMI, WUA, INF, SetupAPI, G1.9/G1.10, download, instalação, histórico v1/schema, atomicidade, SHA-256, lock ou retenção.

`CanInstall=false`, `CanMaintain=false`, `IsNewerProven=false` e `CompletePackageVerified=false`. `WaitAsync` cancela a espera, **não encerra `Search` COM síncrono**. Nenhuma observação permite inferir desinstalação, resíduo, atualização, versão superior ou compatibilidade de pacote remoto.

## Gate final
**PENDENTE:** Windows 2022/2025 (24/24 G1.9 por runner), testes WinForms G1.10–G1.18, regressões gerais, controles negativos de mutação, visuais, segurança/privacidade, consentimento, compilação x64 e integridade **completed/SUCCESS no mesmo SHA**. GO restrito somente depois de comprovação integral. Se o gate falhar, manter NO-GO e registrar a falha.

Nenhum download, URL de conteúdo, staging, instalação, atualização, desinstalação, rollback, elevação, mudança de política, reinício, timer/polling ou coordenador paralelo. Não tocar `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE ou PC FRACO. Preservar PR #79 Draft, roadmap 27/42, G1–G5 abertos.

**Próxima G1.19 sugerida:** auditar exclusivamente limites de observação do histórico v1 diante de duplicidades DeviceId e associações WMI conflitantes, e a coerência das mensagens consultivas sobre mudanças versus instalação/desinstalação, sem habilitar manutenção.
