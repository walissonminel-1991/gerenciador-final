# WORK PRÉ-BETA — matriz inicial de cobertura e evidências

**Etapa 0 concluída em 27/10/2026.** Base: WORK18 .65 TESTE,
`2bc9f4228fd448445fbf181652e5af93005def20`.
Branch: `work-pre-beta-etapa0-20261027`.
Continuidade: [checkpoint](WORK-PRE-BETA-CHECKPOINT.md).

Nesta etapa, **Antes/Depois descreve o conhecimento verificado pela auditoria**.
Não houve alteração funcional, ampliação de permissões ou nova homologação.
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
| Drivers — gestão | Instalação/reversão não disponibilizadas | Mantido o estado; somente planejamento na futura Etapa 5 | `DriverOfficialMatch.CanInstall` e `DriverInfVerificationResult.CanInstall` retornam false | Nenhuma escrita autorizada; não marcar protótipo como recurso homologado | G3/G4/G5 abertos |
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

**Próxima sessão: Etapa 1, somente fontes de atualização e PR #63.** Não
repetir a auditoria inicial; verificar apenas mudanças desde os SHAs registrados.
Etapas 2–7 continuam não iniciadas. O roteiro físico mínimo final pertence
à Etapa 6 e deve partir das evidências acima, incluindo a pendência específica
T11; não solicitar uma nova bateria completa por falta de consolidação.
