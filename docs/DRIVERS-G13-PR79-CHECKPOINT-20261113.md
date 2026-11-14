# GERENCIADOR FINAL — G1.3 · DownloadContents não é prova de identidade de pacote

Data: **13/11/2026**. Repositório: `walissonminel-1991/gerenciador-final`.
PR **#79 Draft**, branch `drivers-pnp-problemas-consulta-20261113`.
**SHA inicial congelado:** `4c98d882919730c4b254acb475a14c72a82d255d`.
Roadmap **27/42**. G1–G5 **abertos**. Nenhuma homologação física.

## 1. Gate inicial verificado (mesmo SHA congelado)

- CI Windows [43495274135](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43495274135): **completed/SUCCESS** para `4c98d882...`. Jobs: drivers Windows 2022 **SUCCESS**, drivers Windows 2025 **SUCCESS**, regressões Windows **SUCCESS**, política de segurança/privacidade **SUCCESS**, consentimento antecipado BETA **SUCCESS**, compilação x64 **SUCCESS**; atestação exclusiva da main **skipped**.
- CI integridade [43495273937](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/43495273937): **completed/SUCCESS**, mesmo SHA. Integridade oficial **SUCCESS**; fonte condicional do runner **skipped**.
- PR #79 aberta em **Draft**, base `candidata-portatil-pessoal-pc-forte-20261101`, sem merge.

Essas aprovações da partida não atestam os commits G1.3. Após alteração, conferir as novas CIs do **HEAD final**.

## 2. Verificação documental das interfaces Microsoft

| Superfície | Metadado documentado | O que NÃO prova |
| --- | --- | --- |
| `IUpdate::DownloadContents` | `IUpdateDownloadContentCollection`, coleção de localizadores do conteúdo vinculado ao objeto de atualização WUA | Não contém manifesto criptográfico do pacote, bytes do INF, versão `DriverVer`, `TargetOSVersion` ou ranking |
| `IUpdateDownloadContent::DownloadUrl` | URL/localização do conteúdo em um servidor | URL, domínio, nome de arquivo e parâmetros não são autenticação independente de conteúdo, identidade de driver ou prova do pacote efetivamente selecionado |
| `IUpdateDownloadContent2::IsDeltaCompressedContent` | Booleano que distingue conteúdo comprimido/delta de arquivo completo | Não fornece hash, assinatura ou conteúdo dos arquivos |
| `IUpdate::BundledUpdates` | Atualizações filhas; a documentação observa que `DownloadContents` pode existir apenas nos filhos | Não permite concluir que o objeto pai contém lista completa de arquivos, nem provar os requisitos do INF sem conteúdo autenticado |
| `IWindowsDriverUpdate4.WindowsDriverUpdateEntries` | Entradas por HardwareID ou CompatibleID, metadados WUA consultivos (G1.2) | Não fornece hash, `DriverVer`, `TargetOSVersion` ou ranking autenticado do pacote remoto |

**Fontes oficiais** (somente leitura documental):
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdate-get_downloadcontents
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iupdatedownloadcontent
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatedownloadcontent-get_downloadurl
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iupdatedownloadcontent2
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nn-wuapi-iupdate
- https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iwindowsdriverupdate4-get_windowsdriverupdateentries
- https://learn.microsoft.com/en-us/windows-hardware/drivers/install/inf-manufacturer-section

## 3. Decisão: NO-GO objetivo para promoção da oferta a atualização segura

Nenhuma propriedade documentada acima demonstra **simultaneamente** os bytes autenticados e correspondentes ao mesmo `UpdateID`/revisão, os metadados de `DriverVer`/INF, alvo OS/arquitetura, compatibilidade de hardware, ranking e integridade de todo o pacote.

Não existe base para promover uma URL da WUA a um arquivo confiável, nem para inferir hash pelo nome, parâmetro, domínio, assinatura TLS ou pelo booleano delta. A obtenção e a validação de conteúdo real seriam outras missões, não autorizadas nesta etapa.

**Resultado:** `CompletePackageVerified=false`, `OfferedNumericVersion=null`, `OfferedNumericVersionProven=false`, `IsNewerProven=false`, `TargetOsArchitectureProven=false`, `CanInstall=false`, `CanMaintain=false`. G1 permanece aberto e não será fingida promoção a G2/G3.

Nenhuma consulta programática de `DownloadContents`, nenhuma leitura de `DownloadUrl`, nenhum armazenamento ou log de URL/token e nenhuma solicitação HTTP de conteúdo foram feitos no produto, nos testes ou no projeto. O exame foi apenas documental e por fixtures sintéticas.

## 4. Menor melhoria segura, sem criar coletor de URLs

- `DriverReadOnlyReport.cs`: exibe, quando uma busca WUA é apresentada, um aviso **IDENTIDADE DO PACOTE REMOTO: NÃO COMPROVADA**, explicando por que URL e compressão delta não demonstram versão, compatibilidade, hash nem autorização.
- `TestesDrivers/G1EvidenceTests.cs`: fixture de título sintético com URL e string `sha256` ilusória, juntamente com entrada WUA4 válida. As flags de autenticidade, versão, compatibilidade e instalação continuam negativas; o relatório exibe NO-GO sem promover a URL a evidência.
- Não foram adicionados modelos de download, componentes HTTP, rotas de distribuição, bibliotecas, serviços, instaladores ou armazenamento.

Esses testes são **negativos e sintéticos**: provam que tais campos não promovem a decisão no código, não provam comportamento diante de uma oferta positiva WUA real. Nas CIs da partida, os dois runners retornaram zero ofertas ao consultar WUA.

## 5. Validação necessária e próximo avanço consultivo

Antes de declarar o lote aprovado, conferir `Compilar Windows (sem assinatura)` com drivers **2022 e 2025**, regressões completas, política/privacidade, consentimento, compilação x64 e a CI de integridade **SUCCESS no mesmo commit final**. Atestação da main é condicional/skipped. Corrigir erros sem desativar proteções.

**Próximo passo funcional sugerido (G1.4):** avaliar exclusivamente a **precedência de ofertas WUA** via `IUpdate.SupersededUpdateIDs` e os metadados de políticas/categorias de atualização, sem comparar versões pelo título/data e sem confundir supersedência entre `UpdateID` com ranking de drivers ou autorização de instalação. Propor apenas um painel consultivo claro e testes com referências contraditórias; continuar G1 em aberto. Não retomar coleta de URLs ou criação de novo downloader sem autorização separada.

## 6. Restrições inalteradas

Somente leitura. Não houve downloads, instalação, atualização, remoção, stage, rollback, elevação, reinício, deploy, alterações D1, Worker, Cron, Secrets, BETA, G5, `main`, releases, PC FORTE ou PC FRACO. PR #79 Draft. Roadmap **27/42** permanece congelado.
