# WORK PRÉ-BETA — matriz inicial de cobertura e evidências

**Etapas 0–4 técnicas, estudo da Etapa 5 e consolidação da Etapa 6 concluídos em 27/10/2026. Homologações parciais; 27/42. Gestão de drivers desativada, exclusão permanente preservada; Etapa 7 não iniciada.** Base: WORK18 .65 TESTE,
`2bc9f4228fd448445fbf181652e5af93005def20`.
Branch: `work-pre-beta-etapa0-20261027`.
Continuidade: [checkpoint](WORK-PRE-BETA-CHECKPOINT.md).

**Diretriz permanente do mantenedor:** VLC excluído de testes, verificações,
consultas e ações/botões. Não investigar ou reconsultar seus sites. Somente
inventário sem ações, com políticas específicas removidas. A falha externa
anterior permanece histórica e deixa de ser requisito da missão.

Na matriz inicial, **Antes/Depois descreve o conhecimento verificado pela auditoria da Etapa 0**.
Os resultados e limites das Etapas 1–6 estão nas seções posteriores. Não houve ampliação
de permissões ou nova homologação física.
O estado oficial foi conferido em [ROADMAP](ROADMAP.md) e
[PROGRESSO](PROGRESSO.json): **27/42 (64,3%)**; 15 pendentes.

## Cobertura funcional inicial

| Funcionalidade | Antes | Depois | Evidência | Limitação | Roadmap |
| --- | --- | --- | --- | --- | --- |
| Inventário | Win32/MSIX/jogos, deduplicação, leitura parcial e histórico já implementados | Confirmada a existência na base .65 e recuperadas sessões físicas de ambos os PCs | `Program.cs`, `InventoryReconciler.cs`, `LocalMsixInventory.cs`; E1/E2/E3/E4/E5 abaixo | Contagens não comprovam cobertura integral de todos os programas; versões físicas diferentes | I1–I6 concluídos anteriormente; I7 aberto |
| Drivers — inventário | WMI, PnP, HardwareID e INF já disponíveis | Confirmadas 166/119 associações nos logs .64/.65; nenhum driver modificado | `DriverInventory.cs`, `DriverPackageLinks.cs`, `DriverHistory.cs`; E1/E2 | Associação observada não equivale a dispositivo único, compatibilidade ou driver atualizável | D1/D3 já concluídos; D4 aberto |
| Drivers — investigação | Lote 2: INF publicado ↔ Store ↔ dispositivo e log Windows | Implementação existente localizada; confirmação de limites nativos e físicos registrada | `DriverPublishedPackages.cs`, `DriverStoreAudit.cs`, `DriverWindowsHistory.cs`; [Lote 2](MISSAO-LOTE2.md) | Histórico parcial, pacotes sem associação não são descartáveis; logs dos PCs não comprovam investigação completa | D2 aberto |
| Drivers — versões oficiais | Consulta WUA por ID de hardware exato | Confirmado contrato somente leitura, sem downloader/installer; CI nativa existente | `DriverOfficialLookup.cs`, `TestesDrivers/ReadOnlyDriverTests.cs`; CI de drivers na [#63](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40507858102) | Oferta não é compatibilidade independente completa; não há versão numérica inferida do título; oferta positiva real pendente | G1 aberto |
| Drivers — assinatura/integridade | Igualdade SHA-256 de INF e SetupVerifyInfFileW | Evidência anterior de INF/catálogo separada da validação de todo o pacote | `DriverInfVerification.cs`, `TestesDrivers/DriverStoreTests.cs`; Lote 2 | 24/24 INFs assinados por runner inicial não verificam origem/payload/SO/arquitetura completos | G2 aberto |
| Drivers — gestão | Instalação/reversão não disponibilizadas | Estado preservado; estudo da Etapa 5 concluído, sem executor ou ativação | `DriverOfficialMatch.CanInstall` e `DriverInfVerificationResult.CanInstall` retornam false | Nenhuma escrita autorizada; não marcar protótipo como recurso homologado | G3/G4/G5 abertos |
| Atualizações — fontes | RuckZuck, fontes oficiais, catálogo Microsoft autenticado e leitores de ecossistemas | Arquitetura existente mapeada; 10/8 candidatos físicos recuperados; #63 permanece separada | `OfficialReleaseSources.cs`, `RuckZuckCatalog.cs`, `PackageCatalogIndex.cs`, `AutomaticUpdateDiscovery.cs`; E1/E2 | Catálogo/versão descoberta não autoriza execução; indisponibilidade externa afeta resultados | U1/U2/U6 já concluídos |
| Atualizações — Chrome | Experimento consultivo em PR #63; documentação ainda dizia CI pendente | CI do HEAD exato confirmada: 6 jobs obrigatórios aprovados e 2 condicionais skipped; revisão completa fica para Etapa 1 | [#63](https://github.com/walissonminel-1991/gerenciador-final/pull/63), [CI 40507858102](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40507858102) | API real e ganho físico não demonstrados nesta auditoria; não integrado; x86/x64 Stable apenas conforme proposta | Nenhum checkpoint novo |
| Atualizações — execução | Contratos MSI/WiX/NSIS/Inno, hash, assinatura, identidade e estado final | Escopo WORK18 confirmado; zero elegíveis silenciosos/supervisionados nas sessões físicas lidas | `AutomaticPackageManifest.cs`, `MsiPackageEvidence.cs`, `VerifiedUpdaters.cs`; [WORK18](MISSAO-WORK18.md), E1/E2 | Fixtures selecionam contratos; payload e homologação final são provas diferentes; sem atualização nesta sessão | U3/U4/U5/U7 já concluídos; U8 aberto |
| Desinstalações | MSI, quiet registrado, NSIS/Inno comprovados, regras específicas e Squirrel condicionado | Fluxos e regressões existentes localizados; 130/54 entradas com desinstalador na fotografia principal | `RegisteredUninstaller.cs`, `SilentUninstallPolicy.cs`, `UninstallRuleCatalog.cs`, `SquirrelUninstallEvidence.cs`; E1/E2 | 61/40 elegíveis silenciosos são pré-classificação sujeita a preflight, não remoções executadas | R7 aberto |
| Resíduos integrados | Captura anterior, ausência confirmada, classificação, SIM/NÃO e revalidação já implementados | Proteções e provas sintéticas anteriores inventariadas; nenhum vestígio real excluído | `IntegratedResidualCleanup.cs`, `IntegratedResidualBatch.cs`, `IntegratedResidualPolicy.cs`, `ResidualPersonalDataBoundary.cs`; [WORK10](MISSAO-WORK10.md), [WORK12](MISSAO-WORK12.md), [WORK14](MISSAO-WORK14-MOTORES.md) | Não cobre todo vestígio de todo app; preserva propriedade incerta, perfis e dados pessoais; homologação física falta | R1/R2/R3/R5 já concluídos; R4/R6/R7 abertos |
| Confiabilidade | Histórico por códigos/contagens, motivos e comparação de snapshots completos | Relatório e dois históricos do PC FRACO recuperados sem mudanças de inventário observadas | `LocalReliabilityHistory.cs`, testes do Lote 1; E3/E4/E5 | Ausência em snapshot não comprova desinstalação; históricos não substituem validação de execução | Apoia I7/R7/U8, sem concluí-los |
| Desempenho/estabilidade | Métricas de etapa, cache limitado e bandeja implementados | Tempos e working set dos logs recuperados; encerramento normal em ambas as sessões | `LocalPerformanceMetrics.cs`, E1/E2; [Lote 3](MISSAO-LOTE3.md) | Sem série de CPU/RAM em repouso e sem diagnóstico conclusivo do AVG | P1–P4 já concluídos; P5/P6 abertos |
| Interface | Layout/DPI sintético e navegação sob regressão | Evidência de bandeja .65 recuperada; relatos visuais separados de prova de boot | `TestesLayout`, E1/E2 e relato da missão | Sem nova inspeção visual física; T11 do PC FRACO não executado | V1–V4 já concluídos; V5 aberto |
| BETA privada .66 | Cliente, retenção e receptor em PRs separadas | Estado das PRs/CI conferido; uma falha externa registrada | [#61](https://github.com/walissonminel-1991/gerenciador-final/pull/61), [#62](https://github.com/walissonminel-1991/gerenciador-final/pull/62) | Nenhuma homologação conectada ou autorização de distribuição inferida; infraestrutura fora de escopo | Dependência da decisão futura; nenhum ganho no roadmap |

## Fontes físicas recuperadas

Os cinco arquivos abaixo foram lidos na íntegra nesta sessão. São evidências
privadas fornecidas anteriormente pelo mantenedor; não foram copiados para
o repositório público. As referências E1–E5 preservam nome não pessoal,
versão/data e linhas úteis para conferência no arquivo original.

| Fonte | Arquivo / sessão | Evidência localizada |
| --- | --- | --- |
| E1 | `GerenciadorFinal-Diagnostico-v5.13.11.64.log`, PC FORTE, 25/10/2026, 20:09–20:14 (-03:00), 153 linhas | Startup L8; inventário/MSIX L14–L15; capacidades L38; multifontes L62; drivers L140–L144; encerramento L153 |
| E2 | `GerenciadorFinal-Diagnostico-v5.13.11.65.log`, PC FRACO, 27/10/2026, 09:15–09:20 (-03:00), 127 linhas | Startup L8; inventário/MSIX L14–L15; capacidades L36; multifontes L55–L56 e L106–L107; drivers L108–L110; bandeja L113–L119; encerramento L127 |
| E3 | `GF-Relatorio-Local(1).txt`, PC FRACO, 27/10/2026, 14 linhas | Inventário 74 completo; restrições agregadas; base e comparação sem diferenças |
| E4 | Primeiro histórico local da sessão anterior .65, 27/10/2026, 3 linhas | Base 11:56:18 UTC e comparação 11:59:45 UTC: 74, zero mudanças; identificador do nome omitido |
| E5 | Segundo histórico local .65, 27/10/2026, 3 linhas | Base 12:15:04 UTC e comparação 12:16:00 UTC: 74, zero mudanças; identificador do nome omitido |

Os horários completos são apenas os da sessão técnica. Não publicar UUIDs de
histórico, IDs de dispositivos, seriais, nomes de aplicativos privados, caminhos
de usuário, comandos, credenciais ou arquivos brutos. Os logs usados contêm
principalmente códigos e contagens, mas essa característica não dispensa revisão.

## Fotografia física comparativa

**Versões, hardware, Windows e condições de consulta diferem.** Não calcular
ganho de desempenho ou cobertura do código pela diferença entre os PCs.
MSIX é parcela do inventário principal consolidado, não um total adicional
a somar a 169/74 (`Program.cs`, `LoadInstalledAsync`).

| Indicador | PC FORTE / E1 / .64 | PC FRACO / E2 / .65 | Interpretação e limite |
| --- | --- | --- | --- |
| Inventário principal consolidado | 169; 0 erros de Registro | 74; 0 erros de Registro | Não comprova cobertura integral dos softwares existentes |
| MSIX observados | 27; status ok; 0 falhas | 19; status ok; 0 falhas | Evidência atual supera a antiga falha MSIX .24; não é diagnóstico de todas as versões |
| Entradas com desinstalador disponível | 130 | 54 | Não significa 130/54 remoções homologadas |
| Candidatos multifontes | 10 | 8 | Apenas descobertas; não atualizações executadas |
| Fontes de produto OK / consultadas, primeiro ciclo | 13/13 | 4/4 | Denominador conforme produtos consultados; RuckZuck 631 entradas em ambos |
| Incertos informados pelo resumo multifontes | 4 | 7 | No resumo de capacidades há também categorias de ambiguidade/revisão; não somar como novas instalações |
| Atualização silenciosa elegível / supervisionada | 0 / 0 | 0 / 0 | Valor observado antes de qualquer execução, não prova de impossibilidade universal |
| Desinstalação silenciosa elegível | 61 | 40 | Exige preflight, consentimento e comprovação posterior |
| Políticas residuais aplicáveis / seleções residuais | 14 / 0 | 6 / 0 | Política aplicável não comprova resíduos encontrados ou removidos |
| Drivers / INFs correlacionados | 166 / 63 | 119 / 53 | Associações e pacotes não são o mesmo denominador |
| INFs compartilhados / entradas sem INF | 19 / 3 | 17 / 3 | Não implica driver obsoleto ou exclusão segura |
| Histórico próprio de drivers | Primeira base; 0 diferenças | Primeira base; 0 diferenças | `base=False` não demonstra histórico de instalações Windows completo |
| Cobertura detalhada de pacotes/PnP | `TESTES.PACOTES parcial=1`; 164 PnP / 166 WMI | Sem evento equivalente de pacote detalhado; diagnóstico com 117 PnP | `DRIVERS.INVENTARIO parcial=False` não elimina a parcialidade de outra etapa |
| Startup interno | 376,16 ms; CPU acumulada 359,38 ms | 667,98 ms; CPU acumulada 593,75 ms | Não mede todo o tempo percebido de abertura nem CPU percentual |
| Inventário inicial / seguinte | 621,03 / 479,74 ms | 1.335,91 / 953,82 ms | Amostras de etapa; sem série estatística comparável |
| Consulta de atualizações, primeiro / segundo ciclo | 7.422,66 / 8.755,21 ms | 12.358,66 / 7.197,53 ms | Rede/cache/fontes diferentes; sem inferência de regressão |
| Working set após startup | 65.613.824 bytes | 61.018.112 bytes | Amostra após Startup, não medição contínua de repouso |
| Pico de working set registrado até o diagnóstico final | 250.249.216 bytes | 177.324.032 bytes | Pico observado nos eventos, não perfil de memória completo |
| Diagnóstico amplo: `instalados` | 267 | 122 | Escopo amplo diferente do inventário principal; não apresentar como crescimento de apps |
| Windows Update geral | 1 oferta | 0 ofertas | Não é o resultado específico da consulta de drivers WUA |
| Bandeja / início com Windows | Opção ativada no log | Ocultar e restaurar registrados | Nenhum dos eventos comprova inicialização após boot/login |
| Encerramento | Normal | Normal | Estabilidade observada nessas sessões, sem diagnóstico conclusivo do AVG |

Relatos adicionais fornecidos na missão: PC FRACO com cerca de 60 MB em
repouso, 11/12 testes sem falhas e **T11 não executado**. Esses relatos ficam
separados das medições de etapa dos logs. A linha de marcação T01–T12 não
constitui contagem de verificações automatizadas nem prova de boot.

## Matriz dos 15 checkpoints pendentes

Todos os itens abaixo permanecem **abertos no roadmap oficial**. Esta tabela
explicita o que a evidência atual resolve e o que ainda falta, sem criar novos
critérios arbitrários ou solicitar repetição integral de uma bateria.

| Checkpoint | Critério vigente / evidência aproveitável | Falta objetiva | Etapa responsável |
| --- | --- | --- | --- |
| I7 | Cobertura conferida nos dois PCs; E1/E2 têm leituras completas e MSIX ok | Conferência da cobertura com referência local de igual escopo e tratamento de ausências/duplicidades; não concluir por contagem isolada | 6 |
| R4 | Limpeza integrada, confirmação, revalidação, exclusões sintéticas verificadas | Homologação física/cobertura específica e comprovação dos efeitos/preservação; nenhuma exclusão pessoal autorizada | 4, depois avaliação em 6 |
| R6 | Casos históricos ambíguos; preservação já coberta por fixtures | Evidência concreta de propriedade/dependentes/uso que resolva amostras reais; sem publicar nomes privados | 4 e 6 |
| R7 | Desinstalação e preservação nos PCs | Evidência antes/depois da operação, estado final e preservação; os logs E1/E2 não registram esse ciclo | 4 e 6 |
| U8 | Instalação/versão final nos PCs; relato histórico .52 de atualização 7-Zip preservado em PROGRESSO | Completar homologação específica restante; candidatos ou retorno zero não comprovam nova versão; sem operação real nesta missão | 3 e 6 |
| P5 | Tempo de abertura, CPU e RAM nos PCs; E1/E2 recuperam várias amostras | Avaliar suficiência do Startup interno, período/estado das medições e CPU/RAM em repouso/uso; não tratar relato de 60 MB como série completa | 6 |
| P6 | Diagnosticar AVG e estabilidade; sessões E1/E2 encerraram normalmente | Causa/resolução verificável do incidente e evidência AVG aplicável ao pacote exato; não repetir alerta nem desativar proteção | 6 e 7 |
| V5 | Legibilidade/fluidez nos dois PCs; relatos e CI de layout já existem | Relacionar capturas/aprovação física à versão/resolução e aos pontos remanescentes; não substituir por DPI simulado | 6 |
| D2 | Hardware/INF/Store/SetupAPI implementados e CI nativa existente | Cobertura e interpretação do histórico Windows real, incluindo resultados parciais/pacotes não associados | 2 e 6 |
| D4 | Inventário 166/119 e vínculos agregados disponíveis | Conferência humana da interpretação de dispositivos, INFs e histórico em ambos os PCs, não apenas presença na lista | 2 e 6 |
| G1 | WUA oficial por hardware exato; fixtures positivas/negativas | Oferta positiva real com versão e compatibilidade completas verificáveis; zero ofertas em runner não basta | 2 |
| G2 | SHA-256 e assinatura INF/catálogo já verificados em runners | Todo o payload, assinatura, origem, SO/arquitetura e compatibilidade; não limitar a prova ao INF | 2 |
| G3 | Instalar e confirmar dispositivo/versão | Funcionalidade não homologada; fora das permissões de escrita desta missão | 5, somente estudo/protótipo desativado |
| G4 | Reversão suportada e recuperação | Projeto/testes seguros sem operações reais; planejamento não encerra checkpoint | 5 |
| G5 | Fluxo controlado nos PCs | Depende de G1–G4 e autorização específica futura para ações reais; não substituível por fixtures | 5 e 7 |

## Regressões já disponíveis e provas que não devem ser repetidas

| Área | Suítes / provas existentes | Aproveitamento nesta missão |
| --- | --- | --- |
| Identidade/motores/fontes | `TestesMotor`, `TestesMultifontes`, `TestesCatalogo`, `TestesEcossistema`; comparações WORK14/16/17/18 | Rodar novamente apenas quando a etapa modificar comportamento ou houver defeito/regressão demonstrável |
| Atualizações | `TestesAtualizador`: autoridade, MSI, preflight, cancelamento, falhas, estado pendente e confirmação | Fixtures na Etapa 3; homologação física tratada separadamente |
| Desinstalação/resíduos | `TestesResiduos` e fixtures inertes: SIM/NÃO, propriedade, links, arquivo ocupado, permissões, estados e preservação | Reusar cenários WORK10–14/Lote 3; não reproduzir exclusão em PC pessoal |
| Drivers | `TestesDrivers`, testes de diálogo e jobs Windows 2022/2025 | Consulta, assinatura nativa e associações; não comprovam hardware dos PCs |
| Interface/confiabilidade | `TestesLayout`, `TestesMsix`, `TestesFechamento`, `TestesTelemetria`, `TestesSeguranca`, `TestesRede` | Evidência existente preservada; testes de rede/telemetria não autorizam transmissão de dados reais |
| Proteções | `scripts/test_politicas_seguranca.py`, `scripts/test_work6_mutations.py`, verificações de pacote | Não remover barreiras para ampliar cobertura ou aprovar CI |

A WORK18 mediu **5 → 13 cenários elegíveis antes do payload** em 28 casos
controlados (4 novos Atualizar e 4 Acompanhar; 15 negativos). Isso não são
oito aplicativos reais adicionais nem oito payloads autorizados. A medida
permanece no [relatório](MISSAO-WORK18.md) e na
[evidência anterior](evidencias/work18-ci-40252100887.json).

## Riscos e decisão de sequência

PR #63: CI aprovada, endpoint real e revisão completa pendentes; não promover
Chrome de consulta a execução. PR #62: CI principal aprovada e integridade
externa falhou; sem alegar prontidão integral da BETA. A infraestrutura privada
não foi acessada nem alterada.

Esta seção preserva a fotografia inicial da Etapa 0. O estado atual das
Etapas 0/1 é concluído; o estado atual da continuidade está ao final.
A continuidade atual está na seção da Etapa 6 ao final; Etapa 7 não iniciada. O roteiro físico mínimo final pertence
à Etapa 6 e deve partir das evidências acima, incluindo a pendência específica
T11; não solicitar uma nova bateria completa por falta de consolidação.

## Etapa 1 — fonte Chrome revista; regressões aprovadas, CI integral bloqueada

| Funcionalidade | Antes | Depois | Evidência | Limitação | Roadmap |
| --- | --- | --- | --- | --- | --- |
| Consulta oficial Chrome | Proposta #63 separada; API ao vivo não validada | Cinco arquivos revistos incorporados na branch #64, sem merge; GETs win/win64 e parser C# reais aprovados | [Revisão](EXPERIMENTO-CHROME-VERSIONHISTORY-20261027.md), modo explícito `--chrome-live` | Somente consulta; sem prova de rollout, payload ou arquitetura local no adaptador da tela | Nenhum novo checkpoint |
| Parser Chrome | Aceitava propriedades JSON duplicadas, LF final e dígitos não ASCII | Recusa ambiguidades e exige versão ASCII completa com quatro componentes | 7 falhas reproduzidas antes; 37/37 focais locais; 196/196 multifontes em Linux e Windows | A CI anterior falhou; a exclusão permanente foi aplicada posteriormente | Proteções preservadas; U8 aberto |
| Isolamento/cache | Contratos existentes e teste entre duas arquiteturas Chrome | Confirmada preservação de Mozilla/GitHub frente a 429/500/302/JSON inválido/duplicado/corpo excessivo/timeout; concorrência deduplicada e cancelamento explícito | Suíte multifontes 196/196 local e Windows; fixtures sem rede | Não comprova disponibilidade permanente dos serviços externos | U1/U2 já concluídos anteriormente |
| Cobertura consultiva controlada | 1 resultado/candidato; 0 Chrome, em 7 entradas | 3 resultados/candidatos; 2 arquiteturas Chrome da mesma família; zero falsos candidatos nos 4 controles | [Comparação reproduzível](evidencias/pre-beta-etapa1-fontes-comparacao.json), mesmo probe nos dois snapshots | Amostra sintética, não novos aplicativos homologados ou ganho físico | 27/42 mantidos |
| Arquitetura Chrome na tela | Nome exato `Google Chrome`, arquitetura obtida apenas por sufixos de nome | Limitação de entrada identificada e documentada; gate conservador mantido | `Program.cs`, `LoadUpdatesAsync`; identidade exata e testes de arquitetura desconhecida | Na tela atual, esse nome produz `unknown` e não consulta a nova fonte; fornecer prova local na Etapa 3 | Pendência de cobertura/identidade; sem encerrar U8 |

Sem nova leitura/repetição dos testes físicos da Etapa 0. Sem alteração da
BETA .66, drivers, permissões ou comandos de execução.

CI [40514120889](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40514120889),
fonte `8e195bfa3dfaab6767d877e78a2ad8d2b538f49c`: regressões/políticas/drivers
aprovados, integridade externa **failure**. Tentativa 1 recusou HTTP no VLC;
a única repetição recebeu 403 de outro espelho HTTPS. O pacote x64 foi
bloqueado pelo gate e ficou skipped. [Resultados por job/tentativa](evidencias/pre-beta-etapa1-ci-40514120889.json).

O bloqueio anterior foi retirado do escopo por instrução explícita do mantenedor.
Não repetir verificações desse produto. A revisão Chrome está concluída e não
deve ser repetida. A CI da alteração de exclusão está aprovada e registrada no checkpoint;
a arquitetura local para a tela permanece pendente na futura Etapa 3.

| Funcionalidade | Antes | Depois | Evidência | Limitação | Roadmap |
| --- | --- | --- | --- | --- | --- |
| Escopo de aplicativos | Caso externo e regras específicas consumiam a validação | Alvo removido dos testes/catálogos e excluído das consultas, limpeza e ações; nenhum novo botão | Alteração solicitada pelo mantenedor; verificador real e casos ativos revistos | Registros antigos preservados como histórico; BETA privada inalterada | 27/42, sem novo checkpoint |

**CI final [40519539258](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40519539258)
aprovada**, fonte `341c109b6b8538733b32a02909ff40c8e0a380d9`: seis jobs
obrigatórios success, dois condicionais skipped. Regressões: 196 multifontes,
368 atualizadores, 705 resíduos/desinstalação, 36 mutações recusadas, 47 MSIX
e 30 encerramento. A redução 707 → 705 corresponde aos dois casos removidos.
[Evidência](evidencias/pre-beta-etapa1-escopo-ci-40519539258.json).
**Etapa 1 concluída; próxima: Etapa 2, não iniciada nesta sessão.**

## Conferência de retomada — 27/10/2026

A referência à antiga pendência externa foi confrontada com o HEAD
`6d6af1b2cb0354d5d04e09daf62ccd80b9ffd43d`, a PR #64 e a CI 40519539258 aprovada.
Etapas 0/1 continuam concluídas; a exclusão permanente foi preservada.
Nenhum site ou teste foi reconsultado/reexecutado, nenhum código foi alterado
e a Etapa 2 não foi iniciada. Roadmap permanece 27/42.

## Etapa 2 — drivers consultivos (27/10/2026)

**CONCLUÍDA TECNICAMENTE, COM PENDÊNCIAS DE HOMOLOGAÇÃO.** Base desta sessão: `a4d30a1734fc54af35bec494e65653475970c40b`.
[Relatório](WORK-PRE-BETA-ETAPA2-DRIVERS.md) e
[comparação reproduzida](evidencias/pre-beta-etapa2-drivers.json).

| Ponto | Antes | Depois | Evidência / limite | Roadmap |
| --- | --- | --- | --- | --- |
| Instância repetida / oferta oficial | Primeiro registro escondia divergências de INF, versão, fornecedor, presença, assinatura ou hardware | Resultado inconclusivo, sem vínculos; independe da ordem das linhas | Nove cenários nas duas ordens; não compara versões numericamente | Apoia D2/G1, sem fechar |
| Correlação WMI | HardwareIDs divergentes podiam desaparecer na deduplicação; nome PnP consultado por ID sem normalizar | Evidência divergente preservada; fallback pelo mesmo ID normalizado | Fixtures encadeiam correlação → busca oficial | D2/D4 abertos |
| Metadados oficiais | UpdateID nulo podia lançar exceção; data ausente/sentinela passava | Inconclusivo, sem publicar subconjunto de ofertas | GUID/revisão e identidade exata continuam obrigatórios; título não vira versão | G1 aberto |
| Histórico Windows | Cabeçalho Device Install malformado podia ser ignorado como leitura completa | Seção rejeitada e cobertura parcial, conservando vínculos válidos | Três formatos malformados, mistura de seções e controle de outra operação | D2 aberto |
| INF publicado | ID vazio podia contar como dispositivo | Zero dispositivos identificáveis, associação desconhecida/parcial; preservar | Enumeração e correlação têm estados separados; pacote sem associação continua protegido | D2/R6 abertos |
| Controles positivos | Instâncias distintas e duplicatas equivalentes | Preservadas; dois dispositivos do mesmo hardware recebem dois vínculos consultivos | Sem instalação/manutenção; 6 controles já aprovavam antes | Nenhum checkpoint adicional |
| Prova local | 6/26 aprovadas, 20 falhas | 26/26 aprovadas; suíte de drivers 165 aprovadas; build sem avisos/erros | Mesmo SDK 10.0.402 e casos sintéticos; Linux sem ABI Windows | Não substitui prova física |
| Prova física reaproveitada | FORTE 166/63, FRACO 119/53 associações/INFs | Nenhuma nova operação ou coleta; limites anteriores conservados | Pacotes/histórico incompletos; sem oferta positiva real compatível ou payload completo | D2/D4/G1/G2 abertos; 27/42 |

**CI [40525913386](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40525913386) aprovada**,
fonte `09fff12b0f854ab9c23886146479a7f993f936b7`, tentativa 1: seis jobs
obrigatórios success e dois condicionais skipped.
[Recibo](evidencias/pre-beta-etapa2-ci-40525913386.json).

| Evidência nativa | Windows 2022 | Windows 2025 | Limite preservado |
| --- | --- | --- | --- |
| Verificações / novas | 174 / 26 | 174 / 26 | Runner descartável, não os PCs |
| WMI / INFs Store mapeados | 67 / 46 | 67 / 48 | Store parcial em ambos |
| INFs publicados | 666 | 732 | Enumeração completa; associação/consulta parcial |
| Vínculos do histórico Windows | 0 | 0 | Leitura parcial; não equivale a nenhuma instalação anterior |
| INF/catálogo verificado | 24 | 24 | Payload completo e origem não comprovados; negativo sem catálogo rejeitado |
| Consulta oficial WUA | Completa, 0 ofertas | Completa, 0 ofertas | Não há prova positiva real para G1 |

Regressão obrigatória: 196 multifontes, 368 atualizadores, 705 resíduos,
36 mutações rejeitadas, 47 MSIX, 30 encerramento e 41 Python/políticas.
Pacote x64 self-contained aprovado, 284 hashes pela CI. O recibo posterior
altera somente Markdown/JSON, `[skip ci]`; o código acima é o SHA validado.
A versão do produto não mudou. Próxima sessão: somente Etapa 3; nesta sessão
ela não foi iniciada. Nenhum merge/deploy ou acesso à infraestrutura BETA.
Exclusão permanente inalterada, sem teste ou botão novo para o produto excluído.

## Etapa 3 — atualizações (27/10/2026)

**CONCLUÍDA TECNICAMENTE; U8 ABERTO.** Base `b74eb879b74360ed78e57b105463fb81fa5ffe83`.
CI da Etapa 2 conferida: 40525913386 aprovada no código 09fff12.
[Relatório/roteiro](WORK-PRE-BETA-ETAPA3-ATUALIZACOES.md) ·
[Evidência](evidencias/pre-beta-etapa3-atualizacoes.json).

| Ponto | Antes | Depois | Prova e limite | Roadmap |
| --- | --- | --- | --- | --- |
| Arquitetura na consulta | Sufixo do nome; identidade exata chegava unknown | PE principal registrado, produto/empresa/versão coerentes, raiz do mesmo escopo | Não usa SO/RegistryView; metadados não autenticam o pacote | U8 aberto |
| Prova local inválida | Captura específica ausente | Caminho inseguro/edição/escopo/PE/metadados conflitantes conservam unknown | Sem procurar outro EXE ou usar setup.exe; ARM64 não vira x64 | Nenhuma nova receita |
| Primeira consulta parcial → cancelamento | Candidato podia desaparecer | Último candidato preservado em revisão/stale | Mudança de registro invalida; consulta completa substitui; 30 min sem renovação por falhas | U8 aberto |
| Memória | Retenção sucessiva não era necessária no desenho anterior | Até 16 observações por app, mais recentes primeiro | 100 rodadas sintéticas | Sem dados remotos |
| Estado da fonte | Falha automática podia ficar fora do resumo | Contagem exibida inclui a falha já tratada como parcial | Sem liberar execução por status | Clareza |
| Fluxo autorizado | Gates de payload e tentativa única existentes | Preservados; bloqueio, recusa UAC/assinatura, falha, confirmação, reinício, cancelamento e timeout simulados | Fixtures inertes não validam instalação real por retorno zero | U8 aberto |
| Comparação | 36/41 aprovadas, 5 falhas | 41/41 comparáveis + 1 controle de memória = 42/42 | Projeção antiga extraída sem alteração de lógica; SDK 10.0.402 | 27/42 mantidos |
| Evidência física | E1/E2: 10/8 candidatos, 0/0 elegíveis silenciosos/supervisionados | Reutilizada, com roteiro futuro preparado | Sem nova coleta/operação pessoal | Homologação real pendente |

**CI [40559631389](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40559631389) aprovada**,
fonte `5a7c0482a759009678b73c7c0cc75727b3b872f0`, tentativa 1: seis jobs
obrigatórios success, dois condicionais skipped.
[Recibo](evidencias/pre-beta-etapa3-ci-40559631389.json).

| Prova final | Resultado | Limite |
| --- | --- | --- |
| Atualizações / controles novos | 410/410, incluindo 42 novos | Fixtures/validações não substituem instalação nos PCs |
| Adaptador → fonte fictícia → tela real | 0→2 candidatos em 4 entradas; 2 controles sem candidato; 2 consultas fictícias; 0 receitas e 0 rede real | Duas arquiteturas da mesma família, não novos apps homologados |
| Multifontes / resíduos / mutações | 196 / 705 / 36 rejeitadas | Regressão obrigatória, não execução em dados pessoais |
| MSIX / encerramento / Python-políticas | 47 / 30 / 41 | Sem alteração de permissão ou proteção |
| UI WORK18 / pacote | 8 combinações; x64 self-contained e 284 hashes | Sem homologação AVG ou conferência independente do artefato nesta sessão |

A CI validou o código recuperado após reinício do ambiente. Nenhum código
mudou depois dela; recibo apenas Markdown/JSON, `[skip ci]`. Versão mantida,
roadmap 27/42 e U8 aberto. Sem nova API Chrome ou investigação COM. Etapa 4
não iniciada. BETA, PCs, main e exclusão permanente preservados.

## Etapa 4 — desinstalação e resíduos (27/10/2026)

**CONCLUÍDA TECNICAMENTE; R4/R6/R7 ABERTOS.** Base `9a01aee2d14685efa8b28df63fb406d14fd32625`.
CI da Etapa 3 conferida: 40559631389 aprovada no código 5a7c048.
[Relatório](WORK-PRE-BETA-ETAPA4-DESINSTALACAO.md) ·
[Evidência](evidencias/pre-beta-etapa4-desinstalacao.json).

| Ponto | Antes | Depois | Evidência / limite | Roadmap |
| --- | --- | --- | --- | --- |
| Shell sem handle | StartFailed; lote podia avançar e botão permitir nova tentativa | Pending, lote interrompido, repetição bloqueada e nenhuma limpeza | Resultado nulo não comprova início recusado; callbacks inertes | R7 aberto |
| Erro de acompanhamento | Win32 podia parecer falha inicial/UAC recusado | Exceção tipada após início mantém Pending e código nativo | Cinco códigos; falhas comprovadas pré-início preservadas | R7 aberto |
| Resumo geral | Podia anunciar finalizada com falha ou limpeza não verificada | Falha, misto, pendente, cancelado, parcial e NÃO distintos | Helper usado pela tela; nenhuma autoridade nova para excluir | R4/R7 abertos |
| Propriedade/consentimento | Guardas WORK10–14 existentes | Preservadas; cinco cenários nativos preparados | Só GUID TEMP: NÃO, mutação, cancelar, SIM preservando dados/vizinho, dono concorrente | R4/R6/R7 não fechados por fixture |
| Comparação focal | 11/29 aprovadas, 18 falhas | 29/29 aprovadas no Linux | Mesmos casos, extração prévia sem alterar lógica | 27/42 mantidos |
| Regressão local | Controles existentes disponíveis | 41 controles do fluxo e build/analisadores aprovados | Suíte completa bloqueada em asserção RAM preexistente no Linux; validar Windows | Sem alterar RAM para aprovar |
| Física | E1/E2 sem ciclo completo de remoção/preservação | Evidência anterior reutilizada; nenhuma operação pessoal | Ainda falta antes/depois real e propriedade de casos históricos | R4/R6/R7 abertos |

**CI [40565220517](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40565220517) aprovada**,
fonte `386e7cd5263266a0478e60139e59b036f1d69e31`, tentativa 1: seis jobs
obrigatórios success, dois condicionais skipped.
[Recibo](evidencias/pre-beta-etapa4-ci-40565220517.json).

| Prova final Windows | Resultado | Limite |
| --- | --- | --- |
| Resíduos / controles novos | 739/739, incluindo 29 focais e cinco nativos | Não são desinstalações físicas nos PCs |
| NÃO / alteração / cancelar | Bytes preservados; mutação bloqueada; cancelamento sem exclusão | Diretório TEMP com GUID, somente dados do teste |
| SIM / dono concorrente | Só cache atribuído removido, Documents/vizinho intactos; concorrente impede oferta | Propriedade real dos casos históricos privados não resolvida |
| Atualizadores / multifontes / mutações | 410 / 196 / 36 rejeitadas | Etapas anteriores e proteções preservadas |
| MSIX / encerramento / Python-políticas | 47 / 30 / 41 | Sem alteração de RAM para aprovar sua asserção no Windows |
| Interface / drivers / pacote | Aprovados; x64 self-contained com 284 hashes | Sem homologação AVG ou conferência independente do ZIP |

O código acima é o SHA validado; recibo posterior somente Markdown/JSON,
`[skip ci]`. Versão e roadmap 27/42 mantidos, R4/R6/R7 abertos. Nenhuma nova
verificação/teste/botão para o produto excluído. Etapa 5 não iniciada;
nenhum merge, deploy, release, driver, reinício ou acesso à BETA privada.

## Etapa 5 — estudo da gestão de drivers (27/10/2026)

**ESTUDO CONCLUÍDO; GESTÃO DESATIVADA; G1–G5 ABERTOS.** Base
`466092ce237eba098ba83612c6b7774ec8226e5e`. CI da Etapa 4 reconferida:
40565220517, código 386e7cd, tentativa 1, seis jobs obrigatórios success e
dois condicionais skipped. SHAs/CI das PRs #60/#61/#62/#63 preservados.
[Estudo](WORK-PRE-BETA-ETAPA5-GESTAO-DRIVERS.md) ·
[Evidência](evidencias/pre-beta-etapa5-estudo-drivers.json).

| Ponto | Antes | Depois nesta etapa | Evidência / limite | Roadmap |
| --- | --- | --- | --- | --- |
| Entradas para gestão | Inventário, oferta oficial e INF/catálogo consultivos | Lacunas de identidade atual, pacote inteiro, criticidade, dependentes e recuperação mapeadas | Leitura dos contratos existentes, sem coletar dados dos PCs | G1/G2 abertos |
| API de instalação | Nenhum executor de drivers | Comparação oficial: staging já escreve; DiInstallDriver tem escopo múltiplo e TRUE pode ser apenas staging; DiInstallDevice requer seleção explícita | Seis fontes Microsoft; alternativa por dispositivo ainda depende de delimitar efeitos do pacote | G3 aberto |
| Reversão | Nenhum executor nem garantia de suporte | Backup obrigatório; DiRollbackDriver pode remover o pacote substituído; desinstalar pacote não é fallback de recuperação | Documentação Microsoft; nenhuma API invocada ou prova real de suporte | G4 aberto |
| Contrato futuro | Sem desenho específico da transação | Identidade/payload, escopo, consentimento, revalidação, intenção durável, confirmação e reconciliação definidos | 12 casos de revisão esperados, **não executados**; não são testes aprovados | G3/G4/G5 abertos |
| Produto | CanInstall/CanMaintain false; somente consulta | Mesmo comportamento e mesmos blobs de código, testes, workflows e versão | Só quatro arquivos Markdown/JSON; zero executor/botão/flag novo | Nenhum checkpoint fechado |
| Validação | CI da Etapa 4 já aprovada | Conferência da CI e validação documental de JSON, referências, IDs e escopo Git | Zero testes de produto novos ou repetidos; nenhuma nova build/CI | 27/42 mantidos |

O novo commit é documental, `[skip ci]`; conferir seu SHA no HEAD da PR #64,
sem atribuir a ele a build 386e7cd. As evidências físicas E1–E5 não foram
reconsolidadas nesta etapa. Nenhum driver baixado/instalado/removido/revertido,
elevação, alteração de ACL ou reinício. Exclusão permanente, proteções, PCs,
BETA/Worker/D1/DNS/flags, main e versão preservados. Sem merge/release/deploy.
**Parada antes da Etapa 6**, reservada a outra sessão, reutilizando as provas
anteriores sem repetir a bateria completa. Quinze checkpoints continuam abertos.

## Etapa 6 — consolidação física (27/10/2026)

**CONSOLIDAÇÃO CONCLUÍDA; HOMOLOGAÇÕES PARCIAIS; 27/42.** Base
`3b6061c33df54cd2b5689a1cabacb866e9a28ff4`. E1–E5 recuperadas e relidas;
zero novo teste ou operação física. E3/E5 são os mesmos eventos; E3/E4/E5 não
registram versão e a associação à .65/FRACO é contextual. Logs .64/.65 sem
SHA/hash do artefato não homologam os commits pré-BETA posteriores.
[Relatório, 15 avaliações e roteiro focal](WORK-PRE-BETA-ETAPA6-EVIDENCIAS-FISICAS.md) ·
[Evidência](evidencias/pre-beta-etapa6-fisica.json).

| Funcionalidade | Antes no registro oficial | Depois nesta etapa | Evidência / limite | Roadmap |
| --- | --- | --- | --- | --- |
| Últimos testes físicos | PROGRESSO apontava .52/FORTE e .24/FRACO | .65/FRACO como sessão mais recente e .64/FORTE separada; registros antigos preservados | E1–E5, sem apagar atualização histórica 7-Zip .52 nem vincular SHA inexistente | Nenhum done mudou |
| Inventário | Critério ainda sem referência equivalente | 169/74 incluindo 27/19 MSIX ok, contagens repetidas e histórico 74 estável consolidados | Não somar MSIX nem confundir escopo amplo 267/122; falta conferência humana da cobertura | I7 parcial/aberto |
| Desempenho | Texto oficial ainda dizia não medir os PCs | Startup 376,16/667,98 ms; três amostras Inventory e duas Updates por sessão; CPU por etapa e RAM documentadas | Working set após Startup 62,57/58,19 MiB; falta repouso comparável; hardware/Windows/versão diferem | P5 parcial/aberto |
| Estabilidade / AVG | Incidente sem causa/resolução comprovadas | Encerramentos normais E1/E2 reaproveitados | Relato sem alerta não homologa hash/AVG nem explica o incidente | P6 parcial/aberto |
| Interface | Aprovação física não vinculada | FORTE 2560×1440/DPI 120 e bandeja FRACO registrados; relato 11/12 separado | Falta captura/aprovação vinculada ao artefato/DPI; T11 não executado | V5 aberto |
| Drivers | Associações existentes, interpretação não homologada | 166/119 associações, 63/53 INFs, 19/17 compartilhados e três sem INF em cada; atributos inconclusivos explicitados | 63/63 mapeados no FORTE com parcial=1; FRACO sem equivalente detalhado; histórico próprio não é SetupAPI completo | D2/D4 abertos; G1–G5 abertos/desativados |
| Remoção / atualização | Fixtures e relato histórico sem ciclo restante completo | Ausência de nova prova operacional avaliada separadamente | E3–E5 sem mudanças não comprovam desinstalação; 10/8 candidatos não comprovam instalação | R4/R6/R7/U8 abertos |
| Roteiro remanescente | Baterias históricas extensas ainda referenciadas | Quatro blocos focais: cobertura/interpretação, repouso, visual/T11 e prova AVG | Planejamento, sem execução; operações exigem contexto/autorização específica futura | 15 pendências mantidas |

CI 40565220517 da Etapa 4 reconferida no código 386e7cd: seis jobs obrigatórios
success e dois condicionais skipped, tentativa 1. Etapa 5 foi documental, sem
CI nova. Sete Markdown/JSON nesta etapa; validação documental e de metadados
aprovada, sem código/testes/workflows alterados nem nova build. Recibo
`[skip ci]`; conferir SHA no HEAD da PR #64. Campos de candidata/artefato e
históricos preservados. Nenhuma nova verificação/teste/botão do alvo excluído,
operação em PC, driver, elevação/ACL, reinício, WinGet, merge/release/deploy ou
acesso à BETA/infraestrutura. **Parada antes da Etapa 7.**
