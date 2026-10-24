# MISSÃO WORK13 — atualização silenciosa e confirmação

Candidata 5.13.11.58 TESTE com CI aprovada e pacote conferido. Homologação física pendente. PR #40 permanece Draft; main preservada. Aprovada anterior: 5.13.11.57, fonte `529d85c0126aa79ae7d348f8895707f67e9d5725`, CI [40094285996](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40094285996). WORK12 e seus contratos foram examinados antes das alterações.

## Causas comprovadas

A [CI de reprodução 40096161682](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40096161682), commit `6e8db268`, falhou nas seis asserções introduzidas antes da implementação: OBS ainda interativo, VC2013 sem SHA autorizado, confirmação OBS aceitando outra pasta e versão de Registro sem binário, cancelamento no callback final ainda executando e comparação de publisher MSIX por prefixo.

O OBS usava argumentos vazios apesar de seu instalador NSIS conhecido. VC2013 tinha URLs e parâmetros Microsoft, mas faltavam hashes fixos; a recusa anterior era correta. A confirmação genérica por Registro não comprovava a versão no disco. Consulta MSIX síncrona e validação de cadeia podiam deter a interface. Uma migração legítima de GUID do bundle VC exigia comprovação própria para conservar o resultado sem associar instalações por nome aproximado.

PROGRESSO.json anunciava .53 enquanto README/roadmap anunciavam .57. O primeiro título do changelog ainda era .49 e agrupava indevidamente alterações históricas com WORK12. Agora candidata em validação e última build aprovada têm campos distintos, conferidos automaticamente junto ao manifesto/projeto.

## Implementação

- OBS 32.2.2: NSIS `/S`; Notepad++ 8.9.8.1: pacote x86 próprio além do x64 existente; VC2013 12.0.40664.0: x64/x86 com SHA fixo, `/install /quiet /norestart`. Nenhum argumento de catálogo remoto é executado.
- Contrato EXE: família exclusiva, escopo de máquina, caminho/uninstaller canônicos, ausência de MSI, arquitetura PE e versão anterior no arquivo. Confirmação exige Registro e binário posterior compatíveis e mesma identidade/pasta/editor. Visão do Registro não é usada isoladamente para inferir arquitetura.
- VC2013: preserva família/arquitetura e escopo; confirma também DLL `msvcr120.dll`. Admite GUID ARP novo somente com família única e DLL confirmada. Reassociação do resultado exige novamente esse contrato e estado confirmado. Uma entrada com novo GUID não pode iniciar outra atualização da mesma família/escopo enquanto existir uma tentativa ativa, pendente ou já confirmada; pendência nunca é convertida em sucesso por essa associação.
- Download: limite, contagem integral, SHA e WinVerifyTrust/editor, seguidos da identidade/versionamento PE. A arquitetura do stub NSIS/Burn pode ser x86 em pacotes x64; arquitetura instalada é corroborada no executável/DLL e a do payload é vinculada ao hash da receita.
- Interface: assinatura e lançamento fora da UI; progresso individual existente, instalação indeterminada, botão existente para cancelar espera, prazo de consulta MSIX e no máximo uma consulta nativa em andamento. Cancelar depois de executar não mata processo, não afirma reversão e não repete automaticamente. Consulta posterior tolera até quatro observações separadas por dois segundos. A ordem das etapas corresponde à validação real; o botão de cancelar espera é restaurado assim que a espera acaba, antes da recarga do catálogo.
- MSIX: publisher DN exato, namespace/identidade únicos, pacote x64 e entradas sem duplicação; hash fixo mantido e confiança obrigatória no deployment nativo Windows.
- Fechamento com consentimento preservado; Notepad++ x86 adicionado aos caminhos conhecidos. OBS somente em caminho canônico. Processos reabertos bloqueiam lançamento. Nenhum encerramento de dependências/serviços ou mudança de ACL.

## Fontes e cobertura preservada

Contratos públicos: [NSIS /S](https://nsis.sourceforge.io/Docs/Chapter3.html), [Notepad++ deployment](https://npp-user-manual.org/docs/command-prompt/), [VC2013 oficial](https://support.microsoft.com/help/4032938), [manifesto OBS](https://github.com/microsoft/winget-pkgs/blob/master/manifests/o/OBSProject/OBSStudio/32.2.2/OBSProject.OBSStudio.installer.yaml), [manifesto Notepad++](https://github.com/microsoft/winget-pkgs/blob/master/manifests/n/Notepad%2B%2B/Notepad%2B%2B/8.9.8.1/Notepad%2B%2B.Notepad%2B%2B.installer.yaml).

| Pacote | SHA-256 fixado |
| --- | --- |
| VC2013 x64 | `a4bba7701e355ae29c403431f871a537897c363e215cafe706615e270984f17c` |
| VC2013 x86 | `53b605d1100ab0a88b867447bbf9274b5938125024ba01f5105a9e178a3dcdbd` |
| Notepad++ x86 | `ee848aa54df153921678b343857a83861da81ce0c9dbb217b6f55a8780e60f5e` |

Adobe continua na página oficial de atualização, sem confundir Acrobat/Reader/edições. Plex Server agora abre downloads do servidor e Jellyfin versionado conserva sua página Windows; não são autorizados instaladores de serviço sem prova de assinatura/arquitetura/migração de dados. A API Plex foi conferida: releases x86 e x64 têm versões diferentes; versão global não autoriza trocar arquitetura. Proton com fontes divergentes exige revisão. VLC não recebe nova receita: a fonte oficial anuncia 3.0.24 e o manifesto inicialmente investigado era 3.0.23.

WinGet COM/CLI permanece desativado. O provedor consultivo isolado já testa comandos sintéticos, timeout/cancelamento, inelegibilidade, conflito e desativação em todos os caminhos. Não há backend de execução com contrato completo de identidade, instalação, licença/consentimento, origem e pós-verificação. MSI/Inno genéricos e comandos RuckZuck não ganham autorização automática; parâmetros documentados sozinhos não comprovam o produto a instalar.

## Arquivos alterados

| Área | Arquivos |
| --- | --- |
| Autorização e confirmação | `VerifiedUpdaters.cs`, `UpdateInstallationContract.cs`, `UpdateLaunchGate.cs`, `WindowsTerminalUpdater.cs` |
| Interface e processos | `Program.cs`, `UpdateOperationProgress.cs`, `UpdateProcessCoordinator.cs`, `UpdateBlockingPreflight.cs`, `InstallerLockScanner.cs` |
| Regressões | `TestesAtualizador/Program.cs`, `Work13UpdateTests.cs`, `SimulatedUpdateScenarioTests.cs`, `TestesAtualizador.csproj`; `TestesLayout/Work11ProgressLayoutTests.cs`; `scripts/test_work6_mutations.py` |
| Versão e documentação | `GerenciadorAplicativos.csproj`, `app.manifest`, `README.md`, `CHANGELOG.md`, `docs/ROADMAP.md`, `docs/PROGRESSO.json`, este relatório |
| Consistência e CI | `scripts/verificar_pacote.py`, `scripts/test_pacote.py`, workflows existentes `validar-candidata-windows.yml` e `triagem-diagnostico-github.yml` |

Não há novos workflows. As lógicas de desinstalação, política residual, consentimento, recuperação pós-boot e inicialização permanecem preservadas; suas regressões continuam obrigatórias. O ganho é de quatro para oito receitas Win32 silenciosas locais (contando arquiteturas, sem multiplicar escopos): três novos pacotes autorizados e OBS convertido a silencioso. Isso não mede cobertura universal nem o número de aplicativos elegíveis em um PC.

## Validação e entrega

Fonte de produção: `691ed0967c670965b329d2fd05e9a4c33e378623`. [CI 40099234162](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40099234162): regressões Windows, downloads verificados, política e compilação aprovados; pacote aprovado também na conferência independente. Compilação/analisadores sem erros ou avisos.

| Conjunto | Resultado |
| --- | --- |
| Atualizadores determinísticos Windows | 237/237, incluindo 51 controles WORK13; a leitura adicional do bundle oficial resulta em 238/238 |
| Mutações adversariais | 24/24 rejeitadas; três novas, além das proteções WORK12 preservadas |
| Resíduos/desinstalação | 643/643, incluindo reproduções reais WORK12 em alvos sintéticos |
| Multifontes / catálogo | 159/159 / 17/17 |
| MSIX / fechamento seguro | 47/47 / 30/30 |
| UI | 132 combinações resolução/aba, 36 de progresso, 44 de limpeza e 20 WORK12; bandeja, cancelamento, descarte e DPI simulado 100/125/150/200% |
| Downloads Windows | Oito receitas Win32: sete com SHA, WinVerifyTrust/editor e identidade; 7-Zip conserva exceção já existente de hash fixo. Terminal: SHA, estrutura e publisher exatos; confiança final obrigatória no deployment Windows, não executado |
| Fontes públicas reais | RuckZuck 631 entradas e 20/20 fontes oficiais disponíveis, em GETs somente leitura no ambiente Linux Work; não é validação nos PCs pessoais |
| Política | 34 testes Python na CI do código e 10 Node; encerramento amplia para 35 Python, aprovados localmente |

Capturas 1280×720 e 1440×900 [geradas no Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40099234162/artifacts/12561699630) foram conferidas visualmente: cartões escuros, estados distintos e barras dentro dos limites, sem sobreposição. Simulação de geometria/DPI não é homologação de monitor físico. A consulta de resultado em cache fez 100.000 leituras em 38 ms/608 bytes no runner; não mede desempenho dos PCs.

Testes novos usam cópias inertes do assembly em diretório temporário e inventários sintéticos. Dois processos inertes reais comprovam timeout/cancelamento da espera sem encerramento forçado e terminam naturalmente sem instalar programas. Hooks de caminho existem somente no build de testes; a regressão da UI exige sua ausência no produto. Nenhum software pessoal foi instalado, atualizado ou removido; nenhum reinício físico foi executado.

A primeira rodada completa encontrou uma expectativa histórica que recusava qualquer receita x86 do Notepad++. A asserção agora exige identificador, URL e hash próprios de x86, e os controles PE comprovam recusa ao trocar arquitetura. O mutante antigo de cancelamento passou a ser equivalente após a segunda barreira: foi renovado para remover ambas; outro mutante remove apenas a barreira final e é detectado pelo callback de cancelamento. Nenhuma proteção foi removida para aprovar testes. CIs interrompidas por novos commits não são contadas como aprovação final. O job opcional de fontes/drivers reais Windows permaneceu skipped; fontes HTTP foram verificadas separadamente, sem alegar teste de hardware Windows.

A checagem de metadados verifica versão do projeto/manifesto/README/roadmap/changelog, origem/CI/nome/hash do pacote aprovado e protege U8/R7/R4 ainda pendentes. Não cria workflow adicional. A publicação portátil depende de todos os jobs obrigatórios aprovados, incluindo a verificação Authenticode dos downloads.

## Pacote aprovado selecionado

[Portátil 5.13.11.58 TESTE Windows x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40099234162/artifacts/12562141990), arquivo `GerenciadorFinal-v5.13.11.58-691ed096-TESTE-win-x64.zip`; expira em 31/10/2026 às 13:48 UTC. Fonte `691ed0967c670965b329d2fd05e9a4c33e378623`; build em `refs/pull/40/merge` `02ef71406fb0e92118ccd3d919034d3b5e64074f`; base `410d65e23546976db456337c71e4705f10217d36`. PR não foi integrado à main. Commits posteriores de documentação registram este pacote e não mudam a fonte de produção aqui identificada.

SHA-256 do ZIP: `30be2c29a0f15d80b7f75fa5f79887e99d05132c87a5d76113e81328c5a04af5`.

Conferidos fora do runner: 59.019.225 bytes ZIP, 151.444.374 bytes extraídos, 281 arquivos e 280 hashes exatos; SHA do ZIP igual ao digest do artefato GitHub. EXE PE AMD64, FileVersion 5.13.11.58, manifesto `asInvoker`, runtimes .NET/WindowsDesktop 10.0.13 incluídos; nenhum assembly de testes ou fixture distribuído. Sem assinatura GF ou homologação AVG. Em relação à .57 aprovada: +7.544 bytes ZIP e +19.968 bytes extraídos, mantendo os mesmos 281 arquivos e runtimes.

## Validação física mínima pendente

PC FORTE: em uma atualização autorizada e elegível, observar preparação/download/assinatura/instalação sem porcentagem fictícia; aplicativo aberto deve solicitar consentimento, NÃO preserva processo. Após SIM, conferir versão real e resultado conservado na lista. Cancelar antes de executar deve impedir lançamento; cancelar a espera deve indicar pendência sem repetição. Não remover/atualizar VC compartilhado apenas para testar.

PC FRACO: mesma build/hash, 1280×720 e 1440×900, navegação/pesquisa durante download, cancelamento e memória; iniciar portátil na bandeja pelo Windows, restaurar e conferir resultado. Confirmar também um ciclo de desinstalação/varredura/decisão SIM/NÃO/resumo da WORK12 e recuperação pós-boot somente leitura quando houver pendência técnica real. Não criar pendência de reinício artificial. U8/R7/R4 continuam abertos; AVG, operação real dos fabricantes e desempenho físico não foram homologados.
