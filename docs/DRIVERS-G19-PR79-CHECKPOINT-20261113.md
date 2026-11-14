# GERENCIADOR FINAL — Drivers G1.9 · Coerência PnP, Driver Store e INF

**Data:** 13/11/2026. **Repositório:** `walissonminel-1991/gerenciador-final`.
**PR:** #79 Draft · **branch:** `drivers-pnp-problemas-consulta-20261113` · **base:** `candidata-portatil-pessoal-pc-forte-20261101`.
**SHA de partida:** `2c0494a14a90545827072e0705f57f2605f9967f`. **Roadmap:** 27/42, sem alterações. **G1–G5 abertos**.

## 1. Gate G1.8 revalidado antes de editar

- [CI Windows 43524004992](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43524004992): `completed/SUCCESS` no SHA inicial, inclusive drivers Windows 2022/2025 (26/26 G1.8 por runner), teste visual real do diálogo, regressões Windows, política de segurança/privacidade, consentimento e compilação Windows x64.
- [Integridade 43524004980](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43524004980): `completed/SUCCESS` no **mesmo SHA**; condicional de fontes `skipped` conforme desenho.
- PR #79 `open`, `draft=true`, head idêntico ao congelado, sem merge.

## 2. Contraexemplos verificáveis no código G1.8

**F1 — fotografia PnP não congelada:** `DriverReportPublicationGuard` guardava `selected` e `inventory` como referências. `SameEvidence(selected, selection)` pode comparar a mesma instância consigo própria após uma alteração em `HardwareIds` ou outra lista mutável. Um resultado WUA/INF antigo poderia ser considerado atual apesar da alteração do objeto subjacente. `ReferenceEquals(inventory, current)` não prova imutabilidade de suas coleções.

**F2 — identidade PnP duplicada:** `IsCurrent` exigia somente `inventory.Items.Any(...)`, e `CanPublish` usava `FirstOrDefault` ao localizar `DeviceId`. Duas linhas para a mesma instância poderiam ser tratadas como prova de vínculo inequívoco. O mecanismo de `Match` já rejeitava duplicatas conflitantes em sua própria rotina, mas a **barreira final de UI** precisava rejeitá-las independentemente.

**F3 — evidência de pacote incompleta:** `SameEvidence` comparava somente `PackageEvidence.State` e `InfName`. Alterações do `StorePath`, `PublishedPath`, `FailureStage`, `NativeError` ou divergências entre `PackageEvidence`, `Store.Packages` e `PublishedPackages.Packages` podiam passar. Isso é relevante para exibição de resultado criptográfico do **INF instalado**, mesmo que nenhuma operação de manutenção seja autorizada.

**F4 — resultado INF sem validação final do pacote:** o callback no diálogo fazia `verification = result` após verificar somente `isCurrent()`. Precisava também demonstrar a associação conservadora do pacote e rejeitar, especialmente, uma alegação de assinatura verificada sem digest, catálogo ou signatário.

## 3. Correções mínimas na PR #79

- `DriverQueryCoordinator.cs`: `DriverReportPublicationGuard` agora congela uma cópia dos campos de cada `DriverInventoryEntry`, incluindo `HardwareIds` e `WindowsEvents`, além de cópias das listas de pacotes do Store e de INFs publicados e seus estados de parcialidade/enumeração. As comparações posteriores não aceitam **mutações in-place** em listas ou no catálogo capturado, ainda que o objeto do inventário seja o mesmo.
- Uma seleção específica precisa corresponder a **exatamente uma** linha com aquele `DeviceId`. Até duplicatas idênticas não demonstram associação única do pacote. Em `CanPublish`, toda oferta WUA vinculada a uma instância exige a mesma unicidade.
- `PackageCoherent` compara os campos completos de `DriverStoreEvidence` por igualdade do registro; a lista do Store deve conter exatamente uma evidência do INF quando presente. O catálogo de INFs publicados, se observado, não pode apresentar vínculo conflitante ou duplicata; **ausência em enumeração parcial não é evidência de remoção**. A ausência numa enumeração completa é tratada como inconclusiva para aquele INF.
- `CanPublishInf` é a barreira específica do resultado local de `DriverInfVerification.Verify`: exige geração, seleção, mapeamento e evidência coerentes, token ativo e nenhum `CanInstall/CanMaintain`. Se o resultado diz `VerifiedInf`, exige SHA-256 hexadecimal de 64 caracteres, catálogo e signatário não vazios. Um resultado `Rejected`/indisponível pode continuar sendo exibido como diagnóstico, sem transformar ausência em aprovação.
- `DriverReadOnlyDialog.cs` e `DriversPlaceholder.cs`: um callback adicional passa o resultado INF pela mesma fotografia/generação antes de atribuir `verification`. Não foi adicionado outro motor ou fonte externa.
- `TestesLayout/DriverReadOnlyDialogTests.cs`: a fixture por reflexão acompanha a assinatura atualizada; **mantém** inspeções visuais, rolagem, dimensões 500×340/790×510 e botões, sem rede ou instalação.
- `TestesDrivers/G19CoherenceTests.cs` e `TestesDrivers/Program.cs`: cobrem casos positivos e negativos de geração/timeout, assinaturas incompletas, duplicatas PnP, mutação de HardwareIDs/Store/catálogo, caminhos e erros divergentes, inventário publicado parcial versus completo e associação ausente.

## 4. Provas e limitações

A evidência de G1.9 é **sintética**. A assinatura real do INF no sistema de teste continua sujeita às verificações de arquivos e catálogo já existentes em `DriverInfVerification`, que não autorizam instalação. A comparação de snapshots não substitui uma nova leitura atômica do sistema operacional, nem prova que o pacote não mudou fisicamente após o retorno da API. `Search` WUA continua síncrona; timeout de `WaitAsync` não interrompe a thread STA. Não houve oferta WUA positiva real nas execuções anteriores.

**Invariantes:** `CanInstall=false`, `CanMaintain=false`, `IsNewerProven=false`, `CompletePackageVerified=false`. Não inferir `DriverVer` da oferta, `TargetOSVersion`, ranking, versão mais recente ou permissão de atualização a partir de título, data, supersedência ou categoria. Nenhum download, endereço de conteúdo, stage, instalação, desinstalação, rollback, elevação, política ou reinício.

## 5. Gate obrigatório do SHA final G1.9

Após este checkpoint, o SHA HEAD precisa passar **CI Windows 2022/2025**, novos testes G1.9, regressões gerais Windows, teste visual do diálogo, política de segurança/privacidade, consentimento, compilação x64 e integridade externa. **Somente `completed/SUCCESS` das duas CIs no mesmo SHA** permite declarar aprovação global. Se houver falha, investigar log e corrigir o menor problema real; execuções canceladas/intermediárias não constituem prova.

## 6. Decisão e continuidade

**GO restrito ao diagnóstico somente leitura**, condicionado ao gate final; **NO-GO para manutenção/instalação e para declarar prova de pacote remoto**. PR #79 continua Draft; roadmap 27/42 e G1–G5 abertos.

**G1.10 — próximo foco proposto:** auditar a consistência entre o conteúdo renderizado e alterações de geração quando o diálogo permanece aberto após uma consulta; verificar se eventos `Activated`, troca de foco, cancelamento e reabertura podem deixar texto antigo visível ou botões indevidamente ativos. Implementar somente correções demonstráveis e testes de WinForms reais nas duas dimensões, sem novo coordenador, sem downloads e sem habilitar ações.

Não tocar `main`, G5, D1, Worker, Cron, Secrets, BETA, releases, Driver Store em produção, PC FORTE ou PC FRACO.
