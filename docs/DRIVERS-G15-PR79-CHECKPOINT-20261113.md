# GERENCIADOR FINAL — Drivers G1.5 · Auditoria de captura WUA COM-like

**Data:** 13/11/2026. Repositório `walissonminel-1991/gerenciador-final`.
**PR:** #79 Draft, branch `drivers-pnp-problemas-consulta-20261113`.
**SHA inicial congelado:** `798ea3d42a0d5e7a26943e72c39603c6dac1c04e`.
**Roadmap:** 27/42; G1–G5 permanecem **abertos**.

## 1. Estado real da CI G1.4 ao retomar

- [Windows 43514612971](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43514612971), SHA `798ea3d4...`: drivers Windows 2022/2025, política e consentimento **SUCCESS**, com 25 verificações G1.4 aprovadas nos dois runners.
- **Resultado GLOBAL: CANCELLED.** A regressão geral ainda executava quando os novos commits na mesma branch cancelaram o workflow. Não declarar SUCCESS global e não usar execuções intermediárias canceladas como aprovação.
- [Integridade 43514612984](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43514612984), mesmo SHA: **SUCCESS**.
- PR #79 permanece **Draft**, base `candidata-portatil-pessoal-pc-forte-20261101`, sem merge.

## 2. Lacuna de teste comprovada

G1.4 tinha fixtures para validar `AuditEntries`, `AuditRelations` e `Match`, porém não havia exercitado diretamente o caminho COM-like de propriedades/getters, `Count`, `Item`, `Identity`, revisão, liberação de RCWs e cancelamento. Os runners anteriores registraram WUA com **zero ofertas reais**.

## 3. Menor alteração de implementação

- `DriverOfficialLookup.CaptureOffer(object, CancellationToken, Action<object>? releaseForTest)` é a **mesma leitura de oferta** utilizada pelo caminho real `Capture` e pelas fixtures COM-like. Faz validação de `Type`, `IsInstalled`, `IsHidden`, `Identity.UpdateID`/revisão, lê `SupersededUpdateIDs`, `Categories`, `WindowsDriverUpdateEntries` e os metadados WUA já existentes. Se o pai falhar, retorna `null` e o lote completo permanece inconclusivo; cancelamento propaga `OperationCanceledException` sem ofertar resultados parciais.
- `CaptureRelations` e `CaptureEntries` tornaram-se `internal` com observador **opcional somente para testes**. Sem observador, continua a liberação COM real por `Marshal.IsComObject` + `Marshal.FinalReleaseComObject`. Com observador, testes conseguem registrar em memória cada liberação em `finally`. **Isto não prova um COM RCW real no Windows; só prova que os caminhos de `finally` são acionados em fixtures.**
- O próprio objeto de atualização e sua identidade são liberados no `finally` do novo método, inclusive em erro e cancelamento. As coleções e seus itens continuam liberados dentro dos métodos correspondentes.
- O loop real WUA chama `CaptureOffer` e preserva os mesmos limites anteriores de 512 ofertas e 512 entradas WUA4 completas acumuladas.
- As ações de instalação, manutenção, download, elevação, remoção e rollback **não são chamadas nem habilitadas**.

## 4. Ensaios sintéticos adicionados

`TestesDrivers/G15CaptureTests.cs`, chamado por `TestesDrivers/Program.cs`, contém objetos públicos internos COM-like com as mesmas propriedades de leitura (`Identity`, `RevisionNumber`, `SupersededUpdateIDs`, `Categories`, `WindowsDriverUpdateEntries`, `Count` e `Item`).

Cobre: captura íntegra, ordem de liberação observada, atualização não-driver/instalada/oculta, identidade nula/inválida, revisão inválida, data tipada incorretamente, getter COM indisponível, `Count` não inteiro/negativo/excessivo, `Item` negado/nulo, entrada com tipo incorreto, categoria inválida, falha COM de `Category.Item`, cancelamento após getter e lote com oferta válida seguida de oferta inconsistente. Toda falha parcial preserva `Matches.Count=0` e as flags `CanInstall=false`, `IsNewerProven=false`, `CompletePackageVerified=false`.

Esses testes não criam sessão WUA, não acessam HTTP nem conteúdo de atualização, não instalam drivers e não usam PCs pessoais.

## 5. Verificação de escopo e aprovação exigida

Para o **SHA final G1.5**, exigir:
- Drivers Windows Server **2022 e 2025** com a nova suíte passando;
- Regressões Windows gerais **SUCCESS** e compilação x64 **SUCCESS**;
- Política de segurança/privacidade, consentimento e integridade externa **SUCCESS**;
- Nenhum job obrigatório ignorado ou falho;
- PR Draft, nenhuma alteração em `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE/FRACO;
- `CanInstall/CanMaintain=false`.

Uma CI iniciada ou um teste de driver verde isolado não equivale a aprovação global. A primeira passagem deve conferir o resultado do próprio SHA, e não de commits anteriores.

## 6. Limitações e próximo avanço

Mesmo com CI totalmente verde, **nenhuma oferta WUA positiva real foi capturada**. Os ensaios não demonstram que os getters COM reais forneçam dados completos em hardware de usuário nem que um pacote remoto corresponda a `DriverVer`, `TargetOSVersion`, arquitetura, assinatura, hash completo e ranking.

**G1 permanece aberto.** Próximo passo sugerido **G1.6**: auditoria estática da arquitetura de liberação/timeout e do contrato de falha integral de `Capture` versus `Match`, com testes artificiais de interrupção em cada estágio (incluindo `Search`, `Updates.Count` e `Updates.Item`) sem acessar WUA real. Só desenvolver se houver contraexemplo verificável; não abrir outro coordenador nem repetir pesquisa de URLs.
