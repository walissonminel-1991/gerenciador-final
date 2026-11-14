# GERENCIADOR FINAL — G1.2 · Auditoria das entradas WUA4 e limites da SetupAPI

Data: **2026-11-13**. Repositório `walissonminel-1991/gerenciador-final`.
PR **#79 Draft**, branch `drivers-pnp-problemas-consulta-20261113`.
Base congelada: `b3e639c5f4bb488198376d46581a98da9ff0980f`.
Roadmap **27/42**; gestão de drivers G1–G5 **aberta**.

## Gate inicial confirmado

As duas CIs associadas EXATAMENTE à base congelada foram consultadas:

| CI | SHA | Resultado |
| --- | --- | --- |
| Windows [43491276857](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43491276857) | b3e639c5 | SUCCESS — drivers Windows 2022/2025, regressões Windows, política, consentimento e compilação x64; atestar main skipped |
| Integridade [43491276843](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43491276843) | b3e639c5 | SUCCESS — integridade de instaladores; job condicional skipped |

Essas CIs **não validam** commits subsequentes G1.2. Uma CI da nova ponta é obrigatória.

## Pesquisa independente nas APIs oficiais Microsoft

1. `IWindowsDriverUpdate4::get_WindowsDriverUpdateEntries` expõe **coleção somente leitura de entradas pertencentes à mesma atualização**, com `Count` e `Item`. Cada `IWindowsDriverUpdateEntry` expõe `DriverHardwareID` (HardwareID **ou CompatibleID**), `DriverClass`, `DriverManufacturer`, `DriverModel`, `DriverProvider`, `DriverVerDate`, `DeviceProblemNumber` e `DeviceStatus`. **Não possui DriverVer numérico**, `TargetOSVersion`, arquitetura do INF, hash dos arquivos ou ranking de pacotes.
2. A identidade **UpdateID/revisão** vem do `IUpdateIdentity` da oferta pai, não de cada entrada. O identificador de uma entrada **não** é o ID da instância PnP; uma correspondência HardwareID exata é vínculo consultivo, não identificação de arquivo/INF ou dependentes.
3. `SP_DRVINFO_DATA_V2.DriverVersion/DriverDate` é dado da lista de drivers **local** da SetupAPI. `DEVPKEY_Device_DriverVersion` é dado instalado; `SP_DRVINSTALL_PARAMS.Rank` depende de uma lista local. Nenhum desses campos **autentica** o conteúdo remoto nem liga o driver local à oferta WUA pai sem outra prova de origem.
4. A seleção de um pacote pelo Windows depende das seções de modelos **INF** e sua decoração `TargetOSVersion`, arquitetura, HardwareIDs/CompatibleIDs, ranking, prioridade/categoria Windows Update e critérios de escolha da versão. O Windows pode preferir um pacote **mais antigo**. Registrar o host ou número WMI não comprova o destino remoto.

Documentação oficial consultada:
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iwindowsdriverupdate4-get_windowsdriverupdateentries
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iwindowsdriverupdateentry
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iwindowsdriverupdateentrycollection
- https://learn.microsoft.com/en-us/windows/win32/api/setupapi/ns-setupapi-sp_drvinfo_data_v2_a
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/accessing-device-driver-properties
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/inf-manufacturer-section
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/overview-of-the-driver-selection-process
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/how-windows-selects-a-driver-for-a-device

## Implementação restrita na PR #79

`DriverOfficialLookup.cs`:
- Modelo `DriverOfficialEntryEvidence` guarda **somente campos WUA4 documentados**; nenhum atributo é chamado de prova de versão/pacote/arquitetura/ranking.
- `CaptureEntries` usa getter WUA4 `WindowsDriverUpdateEntries`, `Count`, `Item` e propriedades **somente leitura**; libera objetos COM e respeita cancelamento.
- Teto **32 entradas por oferta** e **512 entradas completas na busca**; falha de getter = `Unavailable` e falha após iniciar coleção/contagem inválida = `Inconclusive`.
- `AuditEntries` valida IDs, campos de texto, data, código de problema e divergências entre registros do mesmo identificador. Coleção vazia, excessiva, contraditória ou malformada **não** gera correspondência.
- `DriverOfficialOffer.EntryAudit` preserva o resultado da coleção sob o mesmo **UpdateID/revisão da oferta pai**. Quando completa, a oferta só publica vínculo consultivo se contiver exatamente o HardwareID que já vinculava o inventário local. O registro é copiado como `MatchingWuaEntry`; outro ID, conflito ou coleção parcial resulta em **INCONCLUSIVO**. Getter indisponível preserva a consulta original, mas nunca finge entrada completa.
- `DriverReadOnlyReport.cs` informa status `Complete/Unavailable/Inconclusive`, contagem, dados da entrada selecionada e aviso explícito de ausência de `DriverVer`, `TargetOSVersion`, ranking e bytes.
- `TestesDrivers/G1EvidenceTests.cs` inclui fixtures sintéticas de múltiplas entradas, entrada sem vínculo, indisponibilidade, limites, contradições, controles de texto, duplicatas e relatório. **Não** executa instaladores ou drivers.
- `CanInstall`, `CanMaintain`, `IsNewerProven`, `TargetOsArchitectureProven` continuam **false**. `OfferedNumericVersion=null` permanece constante.

Nenhum pacote foi baixado, instalado, removido, stageado ou revertido. Nenhum teste tocou em PCs pessoais.

## Critérios de validação e NO-GO

Exigir CI Windows **2022 e 2025**, testes drivers, regressões gerais, políticas/consentimento, compilação x64 e integridade **SUCCESS no mesmo HEAD final**. Em caso de falha, corrigir sem desativar guards nem alterar outras áreas.

**LIMITAÇÃO DO TESTE NATIVO:** nas execuções anteriores a busca WUA retornou **zero ofertas**; assim a propriedade WUA4 não foi chamada com oferta positiva real. A compilação e fixtures comprovam apenas contratos sintéticos, **não** sucesso real do COM WUA4. Registrar isso mesmo com CI verde.

**G1 aberto / G2–G5 bloqueados:** falta provar `DriverVer` e `TargetOSVersion` do **mesmo pacote** remoto, origem, integridade dos bytes, vínculo autorizado com UpdateID/revisão, compatibilidade/ranking e elegibilidade real. Sem prova, nunca usar data/título/versão local para dizer que a oferta é mais nova; operações permanecem desativadas. D2/D4 físicos ainda abertos.

## Restrições preservadas

PR #79 Draft, sem novas PRs, merges, releases ou mudanças em `main`, D1, Worker, Cron, Secrets, BETA, G5, instalações reais, ACLs, UAC ou reinício. Roadmap oficial **27/42** inalterado. Próxima etapa só após conferir o SHA e resultado da CI; não declarar G1 concluído.