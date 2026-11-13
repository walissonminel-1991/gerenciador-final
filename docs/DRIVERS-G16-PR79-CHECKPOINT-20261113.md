# GERENCIADOR FINAL — Drivers G1.6: captura de sessão WUA sob falha

**Data:** 13/11/2026. **PR:** #79 Draft. **Branch:** `drivers-pnp-problemas-consulta-20261113`. **Base:** `candidata-portatil-pessoal-pc-forte-20261101`.
**SHA de partida:** `3ad0ac8a16d8e48d0f031729e4b466fbf07e3cdd`. **SHA técnico antes deste checkpoint:** `fc6db961cb8100c643b6c8a26353c6cf0ae6dde8`.
**Roadmap:** 27/42, inalterado. **Drivers:** G1–G5 abertos; `CanInstall=false`, `CanMaintain=false`.

## Gate inicial: G1.5 aprovado integralmente

- CI Windows [43515974410](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43515974410), no SHA congelado: **completed/SUCCESS**. Drivers Windows 2022/2025, 23 testes G1.5 em cada runner, regressões Windows gerais, compilação x64, política e consentimento **SUCCESS**; atestação exclusiva da main corretamente `skipped`.
- CI de integridade [43515974418](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43515974418), no mesmo SHA: **completed/SUCCESS**.
- PR #79 confirmada `open`, `draft=true`, sem merge.
- Não confundir com execução G1.4 [43514612971](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43514612971), que foi `CANCELLED` após novos commits, e não constitui sucesso global.

## Contraexemplo que justificou a alteração

Em G1.5, os testes COM-like atingiam `CaptureOffer`, `CaptureRelations` e `CaptureEntries`, mas **não o caminho de produção de sessão** que cria pesquisador, configura fonte, executa `Search`, lê `ResultCode`, `Updates.Count` e `Updates.Item`. Portanto, sem oferta WUA positiva nos runners, falhas nessa fronteira passavam sem cobertura adversarial específica.

A documentação do Windows Update Agent classifica `orcSucceeded=2` como sucesso e `orcSucceededWithErrors=3` como resultado possivelmente incompleto. `Search` é chamada **síncrona** e a fonte `ssWindowsUpdate=2` é usada apenas na pesquisa corrente:
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/ne-wuapi-operationresultcode
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesearcher-search
- https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-uamg/07e2bfa4-6795-4189-b007-cc50b476181a

## Implementação exata

- `DriverOfficialLookup.CaptureSession(inventory, session, token, online, releaseForTest)` é uma **única rotina** para a pesquisa e enumeração efetivamente chamadas pela produção e pelas fixtures. O `Capture` real só cria a sessão COM Windows, verifica pré-condições e delega a ela.
- Lê/escreve **somente propriedades de configuração do pesquisador**: `ClientApplicationID`, `CanAutomaticallyUpgradeService=false`, `ServerSelection=2` e `Online`; não altera configuração global de Windows Update.
- `Search(Criteria)` é somente consulta. `ResultCode` diferente de 2, resultado/coleção nulos, `Count` inválido ou acima de 512, falha de `Item`, falha de captura de oferta ou excesso de 512 entradas WUA4 completas geram resultado **parcial e nenhuma correspondência publicada**.
- Identidade `UpdateID`/revisão e metadados WUA4, categorias e supersedência continuam validados pelo **mesmo `CaptureOffer` e `Match` existentes**, sem duplicação de lógica de negócio. `SearchContext` só é atribuído após consulta íntegra.
- Liberação em `finally` de `updates`, `result`, `searcher`, `session` e liberação no submétodo de cada oferta/identidade/coleção. `releaseForTest` registra a passagem pelos `finally` em objetos artificiais; em produção, `Marshal.IsComObject` e `Marshal.FinalReleaseComObject` continuam usados.
- Cancelamento no token gera `OperationCanceledException`, não resultado artificial de zero atualizações. Objetos COM obtidos são liberados nas rotas cooperativas testadas.
- A alteração **não** cria worker, coordenador, serviço, downloader, instalador, rotina de reinício ou fallback para outra fonte.

## Novos ensaios G1.6

`TestesDrivers/G16SessionTests.cs` foi adicionado e conectado a `TestesDrivers/Program.cs`.
Objetos COM-like em memória exercitam `CaptureSession` (mesma rotina da aplicação) sem criar sessão WUA real, incluindo:
1. fluxo positivo com uma atualização sintética e fonte/critério corretos, identidade, versão instalada e flags `CanInstall=false`;
2. zero ofertas sintéticas com resultado 2, mas nunca como conclusão de estado real do hardware;
3. `ResultCode=3`, resultado nulo, tipo inválido de resultado e exceção em `Search`;
4. `CreateUpdateSearcher` negado/nulo, acesso a política sem contorno;
5. `Updates` nulo, `Count` negativo/513/tipo inválido/exceção, `Item` negado ou nulo;
6. oferta válida seguida de identidade inválida ou falha no segundo `Item`, sem publicar subconjunto;
7. cancelamento antes de configurar sessão, após `Search` e quando uma oferta já foi devolvida;
8. ordem e unicidade de chamadas aos `finally` de ofertas e objetos de sessão.

**Limite da evidência:** Esses testes não provam a liberação/vida útil de um RCW real nem o comportamento de WUA numa oferta positiva. A pesquisa síncrona `IUpdateSearcher.Search` não é interrompida automaticamente pelo simples cancelamento de `WaitAsync` do chamador: esta etapa testa **cancelamento cooperativo nos pontos de verificação** e não promete encerrar uma chamada COM bloqueada.

## CI e aceitação

O commit final, **incluindo este documento**, deve ter CI Windows e CI de integridade com **SUCCESS no mesmo SHA**. Requer jobs drivers Windows 2022/2025, regressões gerais, política/privacidade, consentimento, compilação x64 e integridade. Não usar somente o sucesso de suites especializadas e não declarar sucesso global de um workflow ainda em execução ou cancelado.

Execução técnica anterior ao checkpoint, se ainda consultável:
- Windows [43518475091](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43518475091);
- Integridade [43518475083](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43518475083).

## Limites permanentes e próximo passo

**G1 aberto.** Sem oferta WUA positiva real e sem metadados autenticados de `DriverVer`, `TargetOSVersion`, arquitetura, pacote completo, hash e ranking, nenhuma atualização pode ser considerada comprovadamente mais nova/compatível. `OfferedNumericVersion=null`, `IsNewerProven=false`, `CompletePackageVerified=false`, `CanInstall=false`, `CanMaintain=false`.

Próxima etapa sugerida **G1.7**: auditar especificamente falhas de liberação COM e o limite entre timeout de interface e `Search` síncrono em STA, com testes sintéticos de falha em `finally`, sem instalar nada e sem inventar cancelamento forçado ou rollback. Fazer apenas correção demonstravelmente necessária.

**Áreas protegidas:** `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE/FRACO, Driver Store, UAC, registros, políticas, downloads, instalação, desinstalação, rollback e reinícios permanecem intocados.
