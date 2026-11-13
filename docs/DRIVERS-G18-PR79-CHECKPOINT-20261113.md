# GERENCIADOR FINAL — Drivers G1.8 · Coerência temporal do inventário, seleção e WUA

**Data:** 13/11/2026. **PR:** #79 Draft, `drivers-pnp-problemas-consulta-20261113`. **Base:** `candidata-portatil-pessoal-pc-forte-20261101`.
**SHA congelado de partida:** `42f8b748801b5eb35dd1e8cab6169977715e614c`.
**Roadmap:** 27/42. **Drivers:** G1–G5 permanecem abertos. `CanInstall=false`, `CanMaintain=false`.

## 1. Gate anterior — G1.7, SHA congelado

- [CI Windows 43521604233](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43521604233): **completed/SUCCESS** no SHA `42f8b748...`; drivers Windows 2022 e 2025, testes G1.7 (17/17 por runner), regressões Windows gerais, segurança/privacidade, consentimento e compilação x64 **SUCCESS**. Atestação restrita à main `skipped` por condição.
- [Integridade externa 43521604171](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43521604171): **completed/SUCCESS** no mesmo SHA. Fontes externas condicionais `skipped`.
- PR #79: `open`, `draft=true`, na base indicada; nenhuma fusão em `main`.

## 2. Falha de coerência encontrada no código real

`DriversPlaceholder.cs` publica `lastResult` e o `SelectedItem` da lista após um inventário WMI/SetupAPI. Ao clicar **Investigar**, `DriverReadOnlyDialog.cs` recebe os dois como argumentos e inicia duas tarefas possíveis: `DriverOfficialLookup.Start(inventory, token)` com `WaitAsync(token)`, e `DriverInfVerification.Verify(package, token)`.

Antes de G1.8, após `await` de cada tarefa, o diálogo exigia apenas `token.ThrowIfCancellationRequested()` e `!dialog.IsDisposed`. **Não havia revalidação do inventário/instância/INF ao publicar**. Se a fotografia da página mudasse em meio ao trabalho assíncrono, uma resposta WUA ou verificação INF de geração antiga poderia ser exibida como pertinente à seleção atual. Esse é um contraexemplo verificável à barreira de publicação, não uma afirmação de ocorrência no PC pessoal. A janela modal limita a interação normal com a página pai, mas não constitui prova de invariância de estado durante callbacks assíncronos.

A seleção é uma **instância PnP**, não um índice ou nome localizado. Mesmo HardwareID pode ser compartilhado por mais de uma instância; o INF e a versão instalada também podem mudar em nova fotografia. UpdateID/revisão não fornecem `DriverVer` remoto, compatibilidade nem rank.

## 3. Menor barreira implementada — sem coordenador paralelo

**`DriverQueryCoordinator.cs`**: `DriverReportPublicationGuard` armazena a referência da fotografia, a seleção e a geração. `IsCurrent` requer **mesma referência de inventário**, mesma geração e mesma evidência de instância PnP (DeviceId, HardwareIDs, INF, versão, fornecedor, assinatura informada, presença, problema PnP, estado/nome do INF associado e tipo de vínculo). A seleção deve existir na própria fotografia, não apenas conservar texto igual em memória.

`CanPublish` exige token não cancelado, fotografia e seleção atuais, e rejeita resultado parcial contendo vínculos, UpdateID GUID vazio/inválido, revisão não positiva, dispositivo/HardwareID ausente da fotografia, metadados instalados (INF, versão, fornecedor, assinatura, problema, estado do pacote) divergentes, ou alegações de `CanInstall`, pacote completo e versão mais nova. Resultado parcial **sem** vínculos pode ser exibido como diagnóstico inconclusivo, nunca como instalação autorizada.

**`DriversPlaceholder.cs`**: `inventoryGeneration` aumenta ao iniciar uma nova consulta de inventário, **antes** de aguardar WMI/SetupAPI, inclusive se a leitura vier a falhar ou cancelar. O clique em Investigar captura geração, fotografia e seleção e fornece ao diálogo callbacks somente de verificação de atualidade; não altera motores de driver nem fonte WUA.

**`DriverReadOnlyDialog.cs`**: `Run` verifica a atualidade antes de iniciar; a callback do WUA só atribui `official` se `canPublishOfficial(token, result)` for verdadeiro. A callback de verificação INF também verifica `isCurrent()` após o await. Se a fotografia divergir, resultados anteriores não são publicados, o texto detalhado é substituído por aviso de reabertura da investigação e o status distingue descarte por geração/seleção de descarte por inconsistência. O mecanismo de cancelamento e a contenção STA G1.7 permanecem intactos.

## 4. Evidências sintéticas G1.8

`TestesDrivers/G18PublicationTests.cs` é executado na suíte já existente `TestesDrivers/Program.cs`. Não cria sessão WUA nem consulta dispositivos reais. Exercita:
- Fotografia e seleção equivalentes na geração correta: resultado consultivo permitido, sempre `CanInstall=false`;
- Identidade de **objeto de inventário** nova com mesmo texto, geração anterior e posterior, resultado fora de ordem: rejeitados;
- Instância DeviceId, INF, versão local, fornecedor, assinatura informada, presença, código de problema, falta de associação, HardwareIDs, estado/nome do INF alterados: rejeitados;
- Seleção removida ou substituída por `null`, dispositivo ausente da fotografia: rejeitados;
- Token cancelado/timeout de UI depois de pesquisa: rejeita publicação;
- Oferta com UpdateID inválido/vazio, revisão zero, HardwareID ou instância divergentes; snapshot instalado do match com INF ou versão falsa: rejeitados;
- Lote parcial com vínculos: rejeitado; parcial sem vínculos: pode apresentar apenas diagnóstico inconclusivo;
- Nova geração com outro UpdateID/revisão válido: aceita seu próprio resultado e mantém `IsNewerProven=false`.

Esses ensaios validam a lógica pura de decisão usada pela UI; **não constituem homologação visual ou resposta WUA real positiva**. Os runners anteriores encontraram zero ofertas.

## 5. CI obrigatória no SHA FINAL deste checkpoint

Antes de aprovar a etapa, exigir `completed/SUCCESS` de:
- Driver tests em **Windows 2022** e **Windows 2025**;
- Regressões Windows gerais, política de segurança/privacidade, consentimento, compilação x64;
- Integridade externa no **mesmo SHA**;
- Nenhuma falha ou job obrigatório ignorado; atestação apenas na main e fonte condicional podem ser `skipped`.

Uma CI iniciada ou aprovação de uma etapa G1.7 anterior não atesta G1.8. Não registrar `SUCCESS` final até conferir as execuções do commit deste arquivo.

## 6. Limites e próxima etapa sugerida

**GO limitado:** manter consulta e relatório como funções de **diagnóstico somente leitura**, desde que CI final seja aprovada. **NO-GO:** habilitar atualização/manutenção de drivers, afirmar validade de pacote WUA, cancelar `Search` síncrono de forma forçada ou certificar liberação real de RCW compartilhado.

`CanInstall=false`, `CanMaintain=false`, `CompletePackageVerified=false`, `IsNewerProven=false`. G1–G5 abertos, roadmap 27/42 inalterado.

**Próximo lote G1.9:** auditar adversarialmente a atualização da *própria visualização do diálogo* e a proteção contra publicação de resultados de verificação INF depois de mudança no estado PnP/Driver Store; avaliar se `DriverReportPublicationGuard` rejeita inconsistências de evidências adicionais (incluindo duplicatas de DeviceId e mudanças em estados de pacotes) sem enfraquecer consultas legítimas. Evitar ampliar indefinidamente a arquitetura sem prova funcional real.

**Áreas não tocadas:** `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, Driver Store, políticas Windows, instalação/remoção/atualização de drivers, downloads, UAC, reinício, PC FORTE e PC FRACO.
