# GERENCIADOR FINAL — G1 · Correlação consultiva WUA ↔ dispositivo PnP

**13/11/2026.** Escopo exclusivo: etapa G1 (consulta de ofertas de drivers, sem escrita).
PR #79 Draft, branch `drivers-pnp-problemas-consulta-20261113`, derivada da PR #78
`0325c5617b10e25c36a6d7396fceab9ee9282eeb`.
Roadmap **27/42** e gestão de drivers **G1–G5 abertos**; nenhuma homologação física.

## Evidência anterior congelada
- SHA `f866e9da92d0461becc3eb769100aa996ea692b5`.
- CI Windows [43483493139](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43483493139): SUCCESS (drivers Windows Server 2022/2025, regressões Windows, segurança, consentimento e build x64; atestação main corretamente skipped).
- CI integridade [43483493151](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43483493151): SUCCESS.
- Aprovação anterior não equivale à validação dos commits G1 posteriores.

## Implementação G1 deste lote
- `DriverOfficialOffer` preserva UpdateID, revisão e HardwareID exato; agora também captura classe e fabricante informados por `IWindowsDriverUpdate`.
- `DriverOfficialMatch.Installed` vincula à **mesma instância PnP** os atributos observados do driver **atual**: INF, fornecedor, versão bruta WMI, assinatura relatada, vínculo Store (quando consultado) e código PnP.
- Correspondência exige hardware exato, presença relatada, ausência de conflito entre linhas da instância e evidência de driver instalado coerente. Dispositivo sem linha de driver só é correlacionável de modo consultivo com PnP **28** e nenhuma evidência inventada de INF/versão.
- INF atual ausente/inválido, versão atual não numérica, fornecedor vazio, Store inconsistente ou retornando outro INF e PnP conflitante tornam o lote inconclusivo, sem publicar subconjunto como aprovado.
- Campos de texto WUA com controles ou limites ultrapassados são recusados; UpdateID/revisão e metadados duplicados contraditórios continuam falhando fechado.
- Relatório somente local distingue versão instalada da oferta, expõe classe/fabricante/identidade WUA e declara explicitamente que **não** foram comprovados versão numérica da oferta, ranking, compatibilidade independente de SO/arquitetura ou integridade do pacote completo.
- `CanInstall/CanMaintain` permanecem falsos; **nenhum** download, staging, instalação, desinstalação, rollback, elevação, reinício ou alteração de política.

## Fundamentação e limites
O `IWindowsDriverUpdate` usado fornece DriverHardwareID, DriverProvider, DriverModel, DriverClass, DriverManufacturer e DriverVerDate, **não** um campo de versão numérica do driver oferecido. `DriverVerDate` e `Title` não são autoridade de atualização. A seleção real do Windows depende de ranking (assinatura, recursos e correspondência do hardware), datas e versões sob condições documentadas. Uma consulta WUA para a máquina atual não é validação independente do sistema operacional, arquitetura, dependências, INF/payload ou recuperação.

Referências Microsoft:
- https://learn.microsoft.com/en-us/windows/win32/wua_sdk/iwindowsdriverupdate-properties
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/how-windows-ranks-driver-packages
- https://support.microsoft.com/pt-br/servicing/os/windows/docs/2025/10/understanding-driver-updates

## Ensaios e critérios de aceitação
Fixtures sintéticas em `TestesDrivers/G1EvidenceTests.cs` abrangem correlação íntegra, versão instalada, fornecedor divergente, dispositivo PnP 28, PnP 22 não elegível a esse vínculo, Store inconsistente, UpdateID/revisão contraditórios, classe divergente, nome de oferta com controles e múltiplas alternativas sem seleção automática. `TestesDrivers/Program.cs` chama a nova suíte; layout anterior da PR #79 permanece inalterado.

Para declarar validação técnica:
1. Confirmar HEAD, diff e seis arquivos/código da PR #79 sem mutadores de drivers.
2. Exigir CI Windows 2022 e 2025, regressões Windows, política/privacidade, build e integridade **todos verdes no SHA final**.
3. Se algum job falhar, registrar e corrigir **sem** remover guards e reexecutar para SHA fixado.
4. Nunca usar ausência de oferta no runner para afirmar que a busca completa de versões foi homologada.

## Pendente, sem liberação
- **G1:** ainda falta oferta positiva real com versão numérica comprovada, ranking e compatibilidade integral de SO/arquitetura, além de correlação com os dados do pacote real.
- **G2:** origem, hashes e assinatura de todos os bytes, dependências e compatibilidade específica de cada arquivo.
- **G3–G5:** executor por dispositivo, revalidação, confirmação pós-instalação, recuperação aplicável e homologação física. Não ativar nenhum deles neste lote.
- **D2/D4:** cobertura do histórico e interpretação física dos PCs ainda abertas.

### Restrições
Manter PR #79 **Draft** e a PR #78 como base. Preservar `main`, G5 (autoatualizador), Worker, D1, Cron, Secrets, BETA, releases e máquinas pessoais. **Somente consulta.** Não promover G1/G2 no roadmap nem publicar pacote. A continuação deverá partir do resultado de CI do SHA final, jamais presumir sucesso.
