# GERENCIADOR FINAL — Drivers G1.7 · Falhas de liberação COM e contenção STA

**Data:** 13/11/2026. Repositório: `walissonminel-1991/gerenciador-final`.
**PR:** #79 **Draft**, branch `drivers-pnp-problemas-consulta-20261113`.
**Base protegida:** `candidata-portatil-pessoal-pc-forte-20261101`.
**SHA de partida:** `6cc81e4496c9592b34ea69d74ecda42bb71e45d3`.
**Roadmap:** 27/42. **Drivers:** G1–G5 permanecem abertos.

## 1. Gate inicial G1.6, confirmado ANTES de editar

- [Windows 43518913883](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43518913883): `completed/SUCCESS` no SHA `6cc81e44...`; drivers Windows 2022/2025, regressões Windows, consentimento, política e compilação x64 `SUCCESS`. Job exclusivo da main `skipped` por condição.
- [Integridade 43518915741](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43518915741): `completed/SUCCESS` no mesmo SHA, instaladores oficiais sem instalação; job condicional de fontes do runner `skipped`.
- PR #79: `open`, `draft=true`, sem merge, base acima congelada.

## 2. Contraexemplos materiais, não apenas hipóteses

**A — Máscara e interrupção prematura do cleanup:** o código G1.6 executava em `finally` a sequência `ReleaseComObject(updates); ReleaseComObject(result); ReleaseComObject(searcher); ReleaseComObject(session)`. Uma exceção na primeira liberação interrompia as demais, poderia mascarar `OperationCanceledException` original e não oferecia a falha de liberação como estado de resultado inconclusivo. A mesma falha existia na liberação da identidade e da atualização, além das coleções.

**B — RCW aliasado:** quando um mesmo objeto gerenciado aparece em duas posições da árvore, `FinalReleaseComObject` duas vezes no mesmo RCW pode invalidar uma referência já liberada. Não existe prova de exclusividade global ou de identidade imutável do RCW nas interfaces WUA.

**C — Timeout do chamador não interrompe `Search`:** `IUpdateSearcher.Search` é síncrono. Quando a espera `WaitAsync` é cancelada, a thread STA da captura pode continuar ativa. O método `Start` criava uma **nova** thread por tentativa; repetidas solicitações poderiam acumular consultas concorrentes, sem garantia de cancelamento.

Referências oficiais:
- https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.finalreleasecomobject?view=net-10.0
- https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.releasecomobject?view=net-10.0
- https://learn.microsoft.com/en-us/windows/win32/wua_sdk/iupdatesearcher-methods
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesearcher-search

## 3. Correções mínimas, na mesma rotina de produção

`DriverOfficialLookup.cs`:
- `ComReleaseScope` é uma posse local da pesquisa, passada às capturas de entradas, relações e ofertas. Registra por **igualdade de referência** quais objetos já tiveram tentativa de liberação. Mesmo que um observador sintético ou o runtime gere uma exceção recuperável em `Marshal.IsComObject`/`Marshal.FinalReleaseComObject`, o scope anota `Failed` e continua tentando liberar as demais referências.
- Quando há falha de liberação, **não publica** `DriverOfficialResult` completo, `SearchContext` nem correspondências. `CaptureOffer` retorna `null` e a consulta integral fica `Partial=true, Matches.Count=0`. Liberação duplicada também é tratada como inconsistência; não executa `FinalReleaseComObject` novamente.
- Cancelamento cooperativo em `OperationCanceledException` continua sendo propagado, mesmo quando também ocorre uma falha de cleanup, sem fabricar resultado de zero ofertas.
- O comportamento anterior de leitura, validação de `UpdateID`/revisão, entradas WUA4, categorias, supersedência, limites, `Match` e proteção de dados é mantido.
- `StartWithCapture` é a menor costura interna de teste para iniciar a **mesma** pesquisa na thread STA, sob trava única `Interlocked`. Quando uma busca permanece ativa, uma tentativa concorrente recebe estado `Partial` e nenhuma nova thread é iniciada. O gate só é liberado ao **término real** da captura, antes da `TaskCompletionSource` informar conclusão.
- Essa trava é por **processo**, não por host ou usuário. **Não cancela nem encerra** `Search` bloqueado, não realiza timeout forçado, não cria worker/serviço adicional e não promete recuperação de chamada COM que nunca retorne.

`TestesDrivers/G17ReleaseTests.cs`:
- Falha injetada em cada uma das onze liberações do fluxo positivo, verificando que todas as demais tentativas ocorrem, o resultado final é parcial e não existe correspondência;
- Referências duplicadas entre coleção de categorias/entradas e entre identidade/atualização: rejeição sem tentar dupla liberação;
- Cancelamento depois de `Search` junto a falha sintética de liberação: cancelamento preservado e demais cleanup tentados;
- STA sintética com trabalho bloqueado por eventos: cancelar a espera da `Task` não cancela o trabalho; segunda pesquisa é rejeitada; após a primeira realmente terminar, nova pesquisa volta a ser permitida;
- Exceção em captura sintética: a trava STA volta a ser liberada.
- Suite conectada a `TestesDrivers/Program.cs` e usa `G16SessionTests.Fixture` já existente; nenhuma biblioteca ou arquitetura paralela adicionada.

## 4. Limites de evidência e segurança

- Testes COM-like **não comprovam** que um RCW real foi liberado nem que `FinalReleaseComObject` seja seguro para um COM de uso compartilhado. A documentação alerta que liberação forçada de RCWs compartilhados pode causar exceção ou acesso inválido; evitar afirmar mais que o provado.
- Captura WUA real continua sem oferta positiva comprovada nos runners anteriores; não há evidência autenticada da versão `DriverVer`, `TargetOSVersion`, arquitetura, pacote completo, hash ou ranking remoto.
- `CanInstall=false`, `CanMaintain=false`, `OfferedNumericVersion=null`, `IsNewerProven=false` são invariantes. **G1 não concluído; G2–G5 bloqueados.**
- Nenhum binário, URL, HTTP de pacote, instalação, remoção, staging, rollback, registro, privilégio, política global ou reinício foi tocado. Sem manipulação de PC FORTE/FRACO, `main`, G5, D1, Worker, Cron, Secrets, releases ou BETA.

## 5. Gatilhos de aceitação do commit FINAL

Exigir CI **Windows 2022/2025**, novos ensaios de liberação, regressões gerais Windows, segurança/consentimento, build x64 e **integridade** `SUCCESS` no **mesmo SHA final deste checkpoint**. Se falhar, corrigir sem retirar guards nem alterar áreas fora do escopo. Uma CI intermediária cancelada ou jobs drivers verdes isolados **não** equivalem a aprovação global.

**Decisão limitada:** GO apenas para manter o diagnóstico consultivo e validar a robustez contra as falhas sintéticas. **NO-GO** para afirmar liberação real de RCWs compartilhados, encerrar uma pesquisa WUA síncrona por timeout ou instalar drivers.

## 6. Próximo passo sugerido

**G1.8 — preservar um único resultado consultivo consistente entre busca em andamento, timeout de UI, cancelamento tardio e novo inventário PnP**. Inspecionar como UI e diagnóstico recebem `Start`, se resultados tardios poderiam sobrescrever seleção ou inventário mais recente, e demonstrar testes adversariais de geração/identidade com o mínimo de alterações. Não mudar a arquitetura de drivers, não criar downloader e não permitir instalação.
