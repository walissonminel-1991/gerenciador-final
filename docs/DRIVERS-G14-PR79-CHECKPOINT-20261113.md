# GERENCIADOR FINAL — G1.4 · Relações WUA de supersedência e categorias

**Data:** 13/11/2026. **PR:** #79 Draft. **Branch:** `drivers-pnp-problemas-consulta-20261113`.
**SHA de partida:** `74f2479be2305af96f782aaf434f3a77d93d25dd`.
**Roadmap:** 27/42, inalterado. **Drivers:** G1–G5 continuam abertos.

## 1. Gate inicial confirmado
As duas CIs terminaram integralmente para o SHA de partida:
- Windows [43502471109](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43502471109): **SUCCESS** — Drivers Windows 2022/2025, regressões Windows, política/privacidade, consentimento, compilação x64; atestação exclusiva main ignorada corretamente.
- Integridade [43502471029](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43502471029): **SUCCESS** — integridade oficial; job condicional runner ignorado.

Aprovações do SHA anterior não equivalem à aprovação deste lote G1.4.

## 2. Metadados oficialmente documentados

- `IUpdate::SupersededUpdateIDs` devolve `IStringCollection` com **identificadores independentes da revisão** de atualizações que a oferta diz substituir. É uma relação sobre **atualizações**, não uma operação de manutenção ou comparação de `DriverVer`.
- `IUpdate::Categories` retorna `ICategoryCollection`; `ICategory.CategoryID`, `Type` e `Name` descrevem as categorias às quais a oferta pertence. O **nome é localizado** e não é identidade nem ranking.
- `UpdateID` (identidade do pai) e `RevisionNumber` continuam vinculados à oferta WUA original. A propriedade de supersedência contém apenas **UpdateIDs**, sem a revisão da atualização substituída.
- Uma relação não comprova que duas ofertas correspondam ao mesmo dispositivo, tenham conteúdo autenticado, sejam ambas retornadas pela busca atual, forneçam `DriverVer`, `TargetOSVersion`, classificação de pacote, estado de instalação, elegibilidade de instalação ou rank PnP.
- As buscas `IsInstalled=0 and IsHidden=0 and Type='Driver'` **não** constituem catálogo completo de atualizações supersedidas. Um ID referenciado ausente da resposta permanece **não observado nesta consulta**, não inexistente.
- O Windows pode favorecer pacote de versão aparentemente menor por ranking, assinatura, correspondência específica, classe/prioridade e política. Nenhuma dessas regras é substituída por supersedência.

**Referências Microsoft:**
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdate-get_supersededupdateids
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdate-get_categories
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-icategory
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-icategory-get_categoryid
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-icategory-get_name
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-istringcollection
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/overview-of-the-driver-selection-process

## 3. Implementação na PR #79, somente leitura

`DriverOfficialLookup.cs`:
- `DriverOfficialOffer.Relations` contém `DriverOfficialRelationsAudit` (estados `Unavailable`, `Complete`, `Inconclusive`) e cópia de UpdateIDs/categorias validadas do mesmo objeto WUA da oferta.
- `CaptureRelations`: acessa apenas os getters `SupersededUpdateIDs` e `Categories`, e `Count/Item` e `CategoryID/Type/Name`. Sem `IUpdateDownloader`, URLs ou métodos de escrita; libera objetos COM e respeita cancelamento.
- Limites: máximo **32 GUIDs** substituídos e **16 categorias** por oferta. Falhas, dados conflitantes, GUID inválido/vazio, autossubstituição, duplicatas de ID, controles de texto, categorias contraditórias e truncamento resultam em **INCONCLUSIVO**, sem publicar subconjunto como completo.
- A ausência do getter é `Unavailable` e não vira prova de ausência de supersedência.
- `RelationGraphValid` detecta ciclos entre UpdateIDs das ofertas presentes no lote completo (`A→B→A`, `A→B→C→A`). Referências a UpdateIDs não retornados não são consultadas nem consideradas falhas.
- `DriverOfficialMatch.Relations` é consulta/relato do objeto da oferta; `DriverRankProven`, `DriverVersionProven`, `PackageCompatibilityProven`, `CanInstall`, `CanMaintain`, `IsNewerProven` permanecem falsos.
- `DriverReadOnlyReport.cs`: apresenta no máximo oito IDs de supersedência e oito categorias por correspondência e ressalta que não demonstram ranking/versão/compatibilidade.
- `TestesDrivers/G1EvidenceTests.cs`: testes inteiramente sintéticos para GUIDs válidos/inválidos, duplicatas, inconsistências, autossubstituição, categoria localizada e contraditória, limite de entradas, ciclos 2/3 nós, getter indisponível, coleção incompleta, ID ausente na consulta e nenhuma autoridade de instalação.

**Importante:** nada em G1.4 marca G1 como concluído ou habilita ações. Nenhum binário foi baixado, instalado, removido, executado ou armazenado. Nenhum componente G5/D1 foi alterado.

## 4. CI, testes e limites da evidência

Para o novo SHA final, exigir **SUCCESS** de drivers Windows **2022/2025**, regressões gerais Windows, políticas, consentimento, compilação x64 e integridade externa. Não aceitar somente jobs especializados nem apresentar uma CI anterior como prova.

Teste WUA real anterior retornou **zero ofertas** em ambos runners. Assim, a leitura COM de `SupersededUpdateIDs` e `Categories` **não** está homologada diante de oferta positiva no Windows; os fixtures validam somente parsing e detecção adversarial dos metadados sintéticos.

**G1 ainda aberto / NO-GO de instalação:** faltam `DriverVer` e `TargetOSVersion` do **mesmo pacote** remoto vinculado ao UpdateID/revisão, origem, integridade completa e ranking elegível no dispositivo real. Não inferir por título, data, supersedência ou classificação. `CanInstall=false`, `CanMaintain=false`.

## 5. Próximo avanço focado

**G1.5 — auditoria de robustez WUA positiva sem hardware pessoal.** Examinar testabilidade da leitura COM em entradas sintéticas com interface semelhante à WUA, inclusive exceções de getters, contagens malformadas, coleções divergentes e cancelamento, sem usar/instalar driver ou chamar serviço remoto. Provar que a consulta continua fail-closed e que nenhuma coleção parcial libera correspondências. Se preciso, introduzir a menor costura de teste sem arquitetura paralela. Evitar aumentar pesquisas de propriedades incapazes de provar o pacote. Toda alteração deve ficar na mesma PR e CI Windows.

## 6. Restrições preservadas

PR #79 Draft, base `candidata-portatil-pessoal-pc-forte-20261101`. Não tocar `main`, G5 autoatualizador, D1, Worker, Cron, Secrets, BETA, releases, PC FORTE/FRACO. Sem download de drivers, staging, instalação, remoção, restauração, UAC, novos serviços, reinício ou alteração de políticas. Roadmap **27/42** inalterado.
