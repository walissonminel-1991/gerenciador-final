# Pré-BETA — Etapa 2: drivers somente leitura

27/10/2026. Branch `work-pre-beta-etapa0-20261027`, Draft PR #64.
Base de retomada: `a4d30a1734fc54af35bec494e65653475970c40b`.
**Estado: concluída tecnicamente, com homologação física/pacote pendente. Etapa 3 não iniciada.**

## Resultado funcional

A consulta oficial agora recusa uma instância com registros contraditórios
em vez de escolher a primeira linha WMI. Compara INF, fornecedor, versão
bruta, presença, relato de assinatura e conjunto de HardwareIDs. O nome de
exibição não decide identidade. HardwareIDs divergentes não são descartados
pela correlação anterior. Instâncias distintas do mesmo hardware continuam
separadas; caixa, ordem de IDs e duplicatas equivalentes não criam conflito.

UpdateID nulo retorna metadado inconclusivo; data oficial ausente ou
DateTime.MinValue impede publicação do lote. A data é metadado, nunca um
critério de versão mais nova. Uma oferta incompleta não permite publicar
as demais como se a consulta estivesse aprovada. Nome PnP e presença usam
o mesmo ID normalizado. No histórico Windows, seções Device Install não
compreendidas contam como rejeitadas/parciais; outras operações não são
tratadas como instalações. Um ID vazio não conta como dispositivo associado
a um INF publicado: a enumeração pode estar completa e a associação parcial.

## Validação e reprodução

Com SDK 10.0.402, executar:

```sh
dotnet run --project TestesDrivers/TestesDrivers.csproj -c Release -- --etapa2
dotnet run --project TestesDrivers/TestesDrivers.csproj -c Release
dotnet build GerenciadorAplicativos.csproj -c Release -p:EnableWindowsTargeting=true -p:RunAnalyzers=true
```

A primeira execução usou os novos testes e o código de produção intacto do
SHA-base: **26 casos, 6 aprovados, 20 falhas**, saída 1. Após as correções,
com os mesmos testes: **26 aprovados, zero falhas**, saída 0. Regressão de
drivers local: **165 aprovações**. Build com analisadores: **zero avisos e
erros**. Os testes só usam IDs/logs sintéticos e diretórios temporários.
Linux não executou as APIs nativas; a prova nativa da CI está abaixo.
[Evidência](evidencias/pre-beta-etapa2-drivers.json).

## O que cada evidência permite afirmar

| Evidência | Conclusão permitida | O que continua inconclusivo |
| --- | --- | --- |
| Oferta WUA com identidade/revisão válidas e HardwareID local exato, presente e sem conflito | Oferta oficial vinculada para consulta; SO/arquitetura relatados pelo serviço | Compatibilidade independente completa, número de versão, payload e autorização de instalar |
| Oferta sem vínculo ou resultado parcial | Oferta não aprovada para o dispositivo; falha não equivale a zero atualizações | Não inferir por título, nome parecido, fabricante ou prefixo do ID |
| INF publicado ↔ Store por SetupAPI | Vínculo nativo do INF observado | Pacote sem associação não é órfão/lixo; versão antiga não autoriza remover |
| Seção SetupAPI com instância, INF explícito e terminadores válidos | Registro da operação e seu resultado, na hora local do log | Uso atual, versão instalada ou instalação efetiva de todos os componentes; publicação de INF não é essa prova |
| SHA-256 igual de INF publicado/armazenado e SetupVerifyInfFileW | Integridade do INF comparado e confiança informada para seu catálogo/signatário | Todos os arquivos do pacote, origem remota, compatibilidade completa e revogação independente |

A [documentação WUA](https://learn.microsoft.com/en-us/windows/win32/wua_sdk/iwindowsdriverupdate-properties)
expõe HardwareID ou CompatibleID e data; o GF continua exigindo igualdade
com HardwareIDs locais e não amplia automaticamente para IDs compatíveis.
Os formatos de [cabeçalho](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/format-of-a-text-log-section-header)
e [rodapé](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/format-of-a-text-log-section-footer)
foram consultados. A semântica de [SetupVerifyInfFile/Authenticode](https://learn.microsoft.com/pt-br/windows-hardware/drivers/install/using-setupapi-to-verify-driver-authenticode-signatures)
foi revista; o tratamento de editor confiável/não estabelecido foi preservado.
Não se acrescentou verificação de payload por meio de download ou execução.

## Evidências físicas e pendências

Reutilizados E1/E2 da matriz: FORTE .64 tem 166 associações, 63 INFs,
19 compartilhados e 3 sem INF; FRACO .65 tem 119, 53, 17 e 3. O FORTE
relatava pacotes parciais apesar de inventário WMI completo; o FRACO não
possui detalhamento equivalente. As primeiras bases do histórico próprio
do GF não comprovam histórico SetupAPI completo. WindowsUpdate geral=0
nesses logs não é prova de consulta de drivers com zero ofertas.

D2: falta cobertura/interpretação suficiente de histórico Windows real.
D4: falta interpretação humana completa em ambos os PCs, não apenas contagens.
G1: falta oferta positiva real com versão e compatibilidade completas.
G2: falta prova de origem, todos os arquivos/assinatura e compatibilidade.
**Roadmap oficial preservado em 27/42, sem novo checkpoint fechado.**

CanInstall/CanMaintain continuam false. Nenhuma instalação, remoção,
reversão, elevação, alteração de ACL ou chamada de downloader/installer foi
adicionada. Sem dados brutos dos PCs, merge, release, deploy ou operação na
BETA/Worker/D1/DNS/flags. VLC permanece permanentemente excluído de testes,
consultas, verificações e ações; nenhum botão ou caso novo para ele foi criado.
Etapas 0/1 não foram repetidas; Etapa 3 não iniciada.

## Recibo final da CI

Código `09fff12b0f854ab9c23886146479a7f993f936b7`, [CI 40525913386](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40525913386),
tentativa 1, **success**: seis jobs obrigatórios aprovados e dois condicionais
skipped. [Evidência por job](evidencias/pre-beta-etapa2-ci-40525913386.json).
174 verificações por runner Windows 2022/2025, incluindo as 26 novas.

| Medida nativa | Windows 2022 | Windows 2025 |
| --- | --- | --- |
| Associações WMI / parcial | 67 / não | 67 / não |
| INFs Store mapeados / parcial | 46 / sim | 48 / sim |
| INFs publicados / enumeração completa / consulta parcial | 666 / sim / sim | 732 / sim / sim |
| Vínculos históricos / parcial | 0 / sim | 0 / sim |
| INF/catálogo verificado | 24 | 24 |
| WUA completa / ofertas / vínculos | sim / 0 / 0 | sim / 0 / 0 |

INF sintético sem catálogo rejeitado em ambos. O WUA não produziu oferta
positiva; o histórico continua parcial e a prova criptográfica se limita ao
INF/catálogo. D2/D4/G1/G2 não foram fechados.
Regressões: 196 multifontes, 368 atualizadores, 705 resíduos, 36 mutações
rejeitadas, 47 MSIX, 30 encerramento e 41 Python/políticas. Pacote x64
self-contained validado pela CI com 284 hashes; sem homologação AVG ou
conferência independente do artefato nesta sessão.

Após a CI, somente este relatório, checkpoint, matriz e evidências Markdown/JSON
foram atualizados no recibo `[skip ci]`. Nenhum código mudou. Conferir o SHA
desse recibo na PR #64 ao retomar; ele não identifica um novo build.
A próxima sessão pode executar somente a Etapa 3, preservando as pendências.
