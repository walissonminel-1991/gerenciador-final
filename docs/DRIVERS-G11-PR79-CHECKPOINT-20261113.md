# GERENCIADOR FINAL — G1.1 · Versão e compatibilidade de ofertas WUA

Data: **13/11/2026**, PR **#79 Draft**, branch `drivers-pnp-problemas-consulta-20261113`.
Partida congelada: **bcbb06c110cd2e9c88a7e54ef72b79fbfd766e04**.

## Primeiro gate: CIs do checkpoint anterior
- `43487446613` — **SUCCESS**, SHA congelado, Windows Server 2022/2025 (drivers), regressões, política, consentimento, build x64; atestação `main` skipped.
- `43487446400` — **SUCCESS**, integridade externa, SHA congelado.
- Esses resultados valem **somente** para o commit de partida, não para as alterações G1.1.

## Resultado da pesquisa oficial: limite identificado
A interface **IWindowsDriverUpdate** (e suas entradas **IWindowsDriverUpdateEntry**) expõe `DriverVerDate` mas **não** uma propriedade numérica de versão do driver ofertado. Versões mostradas em nomes de atualização vêm do publicador, e não são prova de comparação. As interfaces IWindowsDriverUpdate2–5 investigadas não oferecem um campo documentado de versão numérica que vincule inequivocamente UpdateID/revisão ao conteúdo `DriverVer`.

A API **SetupAPI SP_DRVINFO_DATA_V2.DriverVersion** pode representar versão de um driver na lista **local** do Windows, e `DEVPKEY_Device_DriverVersion` registra versão **instalada**, mas nenhuma das duas liga esse número, sem pacote verificável, ao UpdateID remoto. O próprio Windows decide melhores pacotes por **ranking**, depois data e versão em empates relevantes; não basta verificar número maior.

**Conclusão:** sem acesso permitido à origem íntegra e ao INF do pacote remoto, não é possível afirmar que uma oferta WUA específica tenha uma versão numérica comprovada ou esteja certificada para arquitetura/OS do usuário. Não recorrer a scraping do catálogo, parsing do título, URL derivada, instalação de teste ou downloads nesta etapa.

Fontes oficiais:
- https://learn.microsoft.com/en-us/windows/win32/wua_sdk/iwindowsdriverupdate-properties
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iwindowsdriverupdateentry
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iwindowsdriverupdate5
- https://learn.microsoft.com/en-us/windows/desktop/api/setupapi/ns-setupapi-sp_drvinfo_data_v2_w
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/accessing-device-driver-properties
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/inf-driverver-directive
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/overview-of-the-driver-selection-process
- https://support.microsoft.com/en-us/servicing/os/windows/docs/2025/10/understanding-driver-updates

## Implementação estritamente consultiva
- `DriverOfficialLookup.NormalizeInstalledDriverVersion`: aceita **somente** quatro componentes numéricos `w.x.y.z` sem espaços/controles e valores `0..65534` (limite estrito `<65535` de INF `DriverVer`). Normaliza zeros à esquerda. Isso **não** certifica versão WUA, assinatura ou o INF efetivo.
- `DriverOfficialMatch.InstalledNumericVersion` separa a versão **WMI instalada**; `OfferedNumericVersion` permanece **null** e `OfferedNumericVersionProven`, `IsNewerProven`, `TargetOsArchitectureProven`, `CanInstall` permanecem **false**.
- `DriverOfficialSearchContext`: após a busca WUA bem-sucedida, registra em memória local `HostOsVersion`, `HostArchitecture`, `Source`, `Online`, `SearchedAtUtc`. `PackageOsArchitectureValidated=false` sem exceções. Este contexto apenas identifica **onde a consulta ocorreu**, não os requisitos do pacote.
- `DriverReadOnlyReport` apresenta a distinção de forma legível. Não envia versão/arquitetura ao diagnóstico e não cria nova rota de coleta.
- `TestesDrivers/G1EvidenceTests`: amplia fixtures para limites de DriverVer, títulos enganosos, versões WMI parciais, arquitetura/OS simulados e relatório sem promoção indevida.

## Gates preservados
`CanInstall=false`, `CanMaintain=false`; nenhuma API de escrita, elevação, download, staging, remoção, rollback ou reinício. A PR #79 continua Draft; `main`, `G5` (autoatualizador), Worker/D1/Cron/Secrets, PC FORTE/FRACO e roadmap 27/42 são intocados.

## Aceitação e bloqueio restante
A etapa G1.1 só pode ser considerada **implementada tecnicamente como leitura** após as CIs do **SHA final**: compilação/regras Windows, drivers Windows 2022/2025, regressões gerais e integridade, todos SUCCESS. Resultado com WUA zero ofertas **não** comprova o comportamento com uma oferta real.

**G1 permanece ABERTO:** ainda é necessária uma oferta positiva, prova de versão numérica `DriverVer` do **mesmo** pacote com UpdateID/revisão, TargetOSVersion/arquitetura do INF, elegibilidade/ranking, integridade e origem. Para prosseguir, a próxima investigação deve considerar exclusivamente fontes oficiais e metadados disponíveis **sem tocar** nos drivers reais; se não for possível, manter INCONCLUSIVO e não iniciar G2/G3 silenciosamente.
