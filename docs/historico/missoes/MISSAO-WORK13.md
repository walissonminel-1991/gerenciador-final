# WORK13 — descoberta e execução silenciosa

## Segunda etapa — candidata 5.13.11.59 TESTE

Prioridade solicitada: transformar a descoberta em autorização genérica verificável, preservando a .58 e os contratos WORK12. HEAD inicial confirmado `0d53b94b8c03b39ed4e643d2126cc904a200214a`; main `410d65e23546976db456337c71e4705f10217d36`, PR #40 Draft, CI anterior `40100057873` aprovada. A fonte `26bbc17ae9e07c485a91fa8b592eca7cabaad118` passou na CI `40138799763`; o pacote .59 foi gerado somente depois das regressões obrigatórias. A etapa .58 permanece registrada como histórico.

### Implementação real

O novo leitor baixa o catálogo Microsoft como MSIX de dados, exige WinVerifyTrust/editor/identidade de catálogo e abre somente seu índice SQLite, usando a DLL do Windows. Não instala a origem nem ativa WinGet COM/CLI. Nome, fabricante e ProductCode são consultados localmente; nenhuma lista do usuário é enviada. O caminho e SHA do manifesto vêm do índice autenticado. Assim, versão, URL, hash, arquitetura e motor podem mudar sem incluir uma regra por aplicativo ou release no GF.

Manifestos são lidos como YAML de dados, com limites e recusa de aliases, documentos múltiplos e ambiguidades. A seleção preserva arquitetura, escopo e tipo existentes. Suporta MSI/WiX, NSIS e Inno com parâmetros locais documentados; comandos/scripts livres não são executados. Exceções específicas já revisadas permanecem preferidas quando o hash corresponde. ZIP, EXE de mecanismo desconhecido, dependências, migrações de servidor, componentes protegidos ou identidade inconclusiva continuam sem autorização genérica.

A autoridade fica somente em memória, vinculada à instalação e emitida pela consulta autenticada. Uma lista forjada ou cópia da receita não concede execução. No clique, hash, assinaturas da instalação e do pacote, motor e produto são verificados novamente. MSI exige continuidade de UpgradeCode, ProductCode alvo, arquitetura, versão e escopo, recusando tabelas de serviços/drivers. NSIS/Inno não recebem flags presumidas de outro motor. Downloads novos rejeitam HTTP, destinos privados, credenciais em URL e mudança de conteúdo.

A lista integra os resultados do novo provedor ao histórico e à avaliação de conflitos existentes. Preparação, verificação e execução permanecem em segundo plano. Fechar aplicativo exige consentimento, caminho, sessão e usuário correspondentes; não há encerramento forçado no fluxo genérico. UAC continua apenas para escopo de máquina. Resultado zero é seguido de conferência da instalação e do binário; operação incerta não autoriza repetição por uma nova entrada de Registro.

A desinstalação ganha descoberta NSIS genérica: estrutura do desinstalador, aplicativo correspondente no diretório, metadados e assinaturas coincidentes. Ausência dessa prova preserva o modo interativo. MSI, Inno, comandos quiet registrados e políticas específicas anteriores continuam ativos. A limpeza, SIM/NÃO, direitos mínimos, dados pessoais e recuperação pós-boot da WORK12 permanecem preservados.

### Arquivos desta etapa

30 arquivos alterados desde `0d53b94b`, organizados nos commits `2388cffe` (catálogo autenticado), `4118c3d4` (descoberta/execução/testes), `41d8f92d` (versão, privacidade/licenças) `f76be625` (Inno real e consistência MSI) e `26bbc17a` (resultado após variar caixa do fabricante). Encerramento documental posterior registra a CI e o pacote; não modifica a lógica da build selecionada.

| Área | Arquivos |
| --- | --- |
| Descoberta e identidade | `PackageCatalogIndex.cs`, `AutomaticPackageManifest.cs`, `AutomaticUpdateDiscovery.cs`, `AutomaticAppInstallation.cs`, `MsiPackageEvidence.cs` |
| Assinatura, motor e processos | `TrustedFileSignature.cs`, `InstallerEngineEvidence.cs`, `AutomaticProcessPreflight.cs`, `SilentUninstallPolicy.cs` |
| Integração | `Program.cs`, `VerifiedUpdaters.cs`, `RuckZuckModels.cs`, `UpdateOperationProgress.cs` |
| Regressões | `TestesAtualizador/AutomaticDiscoveryTests.cs`, `TestesAtualizador/Program.cs`, `TestesAtualizador/TestesAtualizador.csproj`, `TestesResiduos/Work12RegressionTests.cs`, `TestesResiduos/TestesResiduos.csproj`, `TestesLayout/Work11ProgressLayoutTests.cs`, `scripts/test_work6_mutations.py`, workflow existente `validar-candidata-windows.yml` |
| Versão e documentação | `GerenciadorAplicativos.csproj`, `app.manifest`, `README.md`, `CHANGELOG.md`, `PRIVACY.md`, `docs/PROGRESSO.json`, `docs/ROADMAP.md`, `docs/THIRD-PARTY-NOTICES.md`, este relatório |

### Validação e limites

Falhas investigadas na CI `40137345146`: a primeira tentativa recebeu HTTP 403 do CDN antes da leitura do catálogo; a repetição autenticou catálogo/manifestos e validou VLC 3.0.24 com assinatura VideoLAN. ShareX 21.0.0 expôs duas causas: a detecção Inno procurava uma string antiga no overlay, mas a tabela binária v2 fica no stub/recurso; e esse instalador oficial tem diretório de certificados PE vazio. A leitura agora verifica a tabela, versão, CRC e offsets definidos no [código oficial Inno](https://github.com/jrsoftware/issrc/blob/7d30127fdb2ca8b7c0617e8d616e8625bf6ecd99/Projects/Src/Shared.Struct.pas#L422), conferindo a identidade no offset declarado. O teste real separa formato reconhecido de autorização: ShareX sem assinatura é recusado pela política do produto. Não foi removida a exigência de assinatura para aprovar a CI.

A revisão também identificou que o desinstalador Inno instalado não contém a tabela do setup. A descoberta reutiliza a evidência WORK12 do PE `Setup/Uninstall` e log adjacente; as duas operações compartilham essa verificação. Foram acrescentadas recusas para ProductCode MSI ausente, vazio ou divergente entre comando e inventário, e falha explícita se os redirects de teste esgotarem sem validar um payload. Fixtures são temporárias; nenhum desses testes executa instaladores de terceiros.

A conferência final de reassociação encontrou uma diferença de comparação: confirmação aceitava o mesmo fabricante com outra caixa, mas a busca da linha exigia texto idêntico. As duas etapas agora usam comparação sem diferença entre maiúsculas/minúsculas. Um teste nativo com PE sintético percorre a autorização, confirmação e recuperação da linha após essa mudança, evitando perder um resultado já comprovado.

Os 81 controles novos de descoberta, executados no Windows, reproduzem tentativa de forjar autoridade, copiar receita, trocar arquitetura/escopo, injetar parâmetros, usar destinos privados, YAML ambíguo e migrar MSI de família errada. As regressões Windows exercitam arquivos e banco MSI sintéticos, mudança do binário após descoberta, diretório compartilhado, pacote ausente e ausência dos hooks de teste no produto. A etapa inicial `2388cffe` passou na CI `40134383273`, incluindo autenticação real do catálogo e de manifestos de quatro fabricantes, sem executar seus instaladores. A CI aprovada desta candidata conferiu os formatos reais NSIS/Inno em downloads oficiais.

Cobertura genérica não equivale a compatibilidade universal: metadados incompletos, assinatura não confirmada, motor diferente, personalizações não documentadas e fontes divergentes podem exigir modo interativo, site ou Store. MSIX/Store genérico e servidores não recebem execução nova. Não houve atualização, desinstalação ou limpeza nos PCs pessoais. U8/R7/R4 seguem sem homologação física; roadmap 27/42. A aprovação técnica e o pacote desta etapa estão identificados abaixo.

### Resultado da validação .59

[CI Windows 40138799763 aprovada](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40138799763), fonte `26bbc17ae9e07c485a91fa8b592eca7cabaad118`. Compilação/analisadores: zero erros e zero avisos. Todas as regressões obrigatórias concluídas; fontes/drivers reais do runner ficaram no job opcional skipped. Nenhum instalador de terceiro foi executado.

| Conjunto | Resultado |
| --- | --- |
| Atualizadores Windows | 318/318, incluindo 81 controles novos de descoberta e 51 WORK13 anteriores |
| Mutações adversariais | 27/27 detectadas, incluindo lista forjada, receita copiada e mudança de arquitetura |
| Resíduos/desinstalação | 645/645; preserva WORK12 e acrescenta compartilhamento da prova Inno |
| Multifontes / catálogo | 159/159 / 17/17 |
| MSIX / fechamento seguro | 47/47 / 30/30 |
| Interface | 132 resolução/aba, 36 progresso, 44 limpeza, 20 WORK12; bandeja, descarte e hooks de teste ausentes do produto |
| Catálogo real somente leitura | MSIX Microsoft autenticado; manifestos OBS 33.0.0, VLC 3.0.24, ShareX 21.0.0 e SumatraPDF 3.6.1 conferidos pelo hash do índice |
| Binários reais somente leitura | VLC: SHA, assinatura VideoLAN e NSIS; ShareX: SHA e Inno válidos, ausência de assinatura comprovada e execução genérica recusada |
| Regressões de downloads anteriores | Oito Win32 e pacote Terminal preservados; sem executar instaladores |
| Política e metadados | 35 testes Python e 10 Node aprovados |

[Capturas geradas no Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40138799763/artifacts/12576989197) estão disponíveis. A geometria compacta e os estados passaram automaticamente. Após o retorno do ambiente local, as capturas de 1280×720 e 1440×900 foram conferidas visualmente: cartões escuros, textos/estados distintos e barras contidas, sem sobreposição. Também não houve ensaio de monitor, antivírus, atualização, desinstalação ou reboot nos PCs pessoais.

### Pacote .59 aprovado pela CI

[Portátil 5.13.11.59 TESTE Windows x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40138799763/artifacts/12578144319), arquivo `GerenciadorFinal-v5.13.11.59-26bbc17a-TESTE-win-x64.zip`; expira em **31/10/2026 às 22:37 UTC**. Fonte `26bbc17ae9e07c485a91fa8b592eca7cabaad118`; build `refs/pull/40/merge` `aaf1de4f56d9429b1e60d85bfcfbb5085884705d`; base/main `410d65e23546976db456337c71e4705f10217d36`. PR #40 permanece Draft, sem merge.

SHA-256 do ZIP, fornecido pelo upload-artifact/GitHub e coincidente na API de artefatos: `b3a0f2e244a4be8ef552610e010b864180a0b4f822588b3cdad0a21763e75399`.

A CI verificou antes do upload manifesto Win32/ativação SxS, identidade do projeto, origem, PE AMD64, self-contained e cobertura exata de **281 hashes em 282 arquivos**, **151.819.016 bytes** extraídos. ZIP: **59.169.235 bytes**. Runtimes .NET e WindowsDesktop **10.0.13**. Em relação à .58: +150.010 bytes no ZIP, +374.642 bytes extraídos e um arquivo; YamlDotNet é a nova dependência gerenciada, com licença incluída. GF sem assinatura; AVG e operação física não homologados.

**Conferência independente concluída após o retorno do ambiente Work:** ZIP baixado do artefato, SHA-256 igual ao digest do GitHub, 281 hashes internos exatos, 282 arquivos, origem e runtimes confirmados. PE AMD64 com FileVersion 5.13.11.59, ProductVersion 5.13.11.59+aaf1de4f56d9429b1e60d85bfcfbb5085884705d, manifesto `asInvoker` e diretório de certificados vazio. YamlDotNet.dll: 300.032 bytes; nenhum assembly de testes ou fixture distribuído. Não houve execução do GF ou de instalador no PC pessoal durante essa conferência.

A [CI do encerramento documental 40139511277](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40139511277) também passou, após duas tentativas de rede recusadas: HTTP 403 no VLC e redirecionamento fora da política. A repetição manteve HTTPS público, hash e assinatura obrigatórios. GETs locais somente leitura mostraram a URL oficial encaminhando para o seletor de espelhos e dois destinos HTTPS com HTTP 200; o destino depende da origem da consulta. Disponibilidade do fornecedor permanece uma limitação real: erro ou destino não autorizado bloqueia a atualização, sem degradar a segurança. Nenhum resultado de falha foi convertido em sucesso; os logs de cada tentativa continuam na CI.

README, roadmap, changelog e PROGRESSO.json selecionam este pacote aprovado; commits posteriores de documentação não mudam sua fonte de produção. Homologação física/AVG permanece pendente.

### Próximo teste físico mínimo .59

- **PC FORTE:** em uma atualização já necessária de aplicativo elegível, conferir descoberta da versão, preparação, consentimento para fechamento, instalação sem percentual fictício e confirmação da versão/resultado após renovar a lista. Se a prova falhar, o GF deve bloquear a execução e explicar o motivo. Não atualizar dependência compartilhada apenas para ensaio. O ciclo WORK12 pode ser conferido em uma desinstalação que o usuário já pretenda realizar, com decisão SIM/NÃO e resumo final.
- **PC FRACO:** usar o mesmo pacote/hash; conferir pesquisa/navegação durante consulta/download, 1280×720 e 1440×900, restauração da bandeja e opção portátil de iniciar com Windows. Não provocar reboot artificial: recuperação somente leitura deve ser observada quando existir pendência técnica real.

U8/R7/R4 permanecem abertos, progresso **27/42**. MSIX/Store genérico, servidores, motores não reconhecidos, produto/arquitetura/escopo ambíguos e assinatura ausente não ganham execução automática. Cobertura real depende das instalações encontradas; nenhum número universal de aplicativos silenciosos é alegado.

## Primeira etapa — candidata .58

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

## Pacote .58 aprovado anteriormente

[Portátil 5.13.11.58 TESTE Windows x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40099234162/artifacts/12562141990), arquivo `GerenciadorFinal-v5.13.11.58-691ed096-TESTE-win-x64.zip`; expira em 31/10/2026 às 13:48 UTC. Fonte `691ed0967c670965b329d2fd05e9a4c33e378623`; build em `refs/pull/40/merge` `02ef71406fb0e92118ccd3d919034d3b5e64074f`; base `410d65e23546976db456337c71e4705f10217d36`. PR não foi integrado à main. Commits posteriores de documentação registram este pacote e não mudam a fonte de produção aqui identificada.

SHA-256 do ZIP: `30be2c29a0f15d80b7f75fa5f79887e99d05132c87a5d76113e81328c5a04af5`.

Conferidos fora do runner: 59.019.225 bytes ZIP, 151.444.374 bytes extraídos, 281 arquivos e 280 hashes exatos; SHA do ZIP igual ao digest do artefato GitHub. EXE PE AMD64, FileVersion 5.13.11.58, manifesto `asInvoker`, runtimes .NET/WindowsDesktop 10.0.13 incluídos; nenhum assembly de testes ou fixture distribuído. Sem assinatura GF ou homologação AVG. Em relação à .57 aprovada: +7.544 bytes ZIP e +19.968 bytes extraídos, mantendo os mesmos 281 arquivos e runtimes.

## Validação física mínima pendente

PC FORTE: em uma atualização autorizada e elegível, observar preparação/download/assinatura/instalação sem porcentagem fictícia; aplicativo aberto deve solicitar consentimento, NÃO preserva processo. Após SIM, conferir versão real e resultado conservado na lista. Cancelar antes de executar deve impedir lançamento; cancelar a espera deve indicar pendência sem repetição. Não remover/atualizar VC compartilhado apenas para testar.

PC FRACO: mesma build/hash, 1280×720 e 1440×900, navegação/pesquisa durante download, cancelamento e memória; iniciar portátil na bandeja pelo Windows, restaurar e conferir resultado. Confirmar também um ciclo de desinstalação/varredura/decisão SIM/NÃO/resumo da WORK12 e recuperação pós-boot somente leitura quando houver pendência técnica real. Não criar pendência de reinício artificial. U8/R7/R4 continuam abertos; AVG, operação real dos fabricantes e desempenho físico não foram homologados.
