# MISSÃO WORK13 — atualização silenciosa e confirmação

Candidata 5.13.11.58 TESTE. Validação em andamento; não é uma release estável. PR #40 permanece Draft; main preservada. Aprovada anterior: 5.13.11.57, fonte `529d85c0126aa79ae7d348f8895707f67e9d5725`, CI [40094285996](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40094285996). WORK12 e seus contratos foram examinados antes das alterações.

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

## Validação e entrega

Regressões Windows, assinatura dos instaladores oficiais (somente download, sem execução), testes adversariais e pacote serão registrados aqui após a CI. Testes novos usam cópias inertes do assembly em diretório temporário e inventários sintéticos; nenhum aplicativo pessoal é atualizado. Hooks de caminho existem somente no build de testes e a regressão da UI exige sua ausência no produto. Dois processos inertes reais comprovam timeout/cancelamento da espera sem encerramento forçado; eles terminam naturalmente sem instalar programas.

A checagem de metadados verifica versão do projeto/manifesto/README/roadmap/changelog, origem/CI/nome/hash do pacote aprovado e protege U8/R7/R4 ainda pendentes. Não cria workflow adicional. A publicação portátil depende de todos os jobs obrigatórios aprovados, incluindo a verificação Authenticode dos downloads.

## Validação física mínima pendente

PC FORTE: em uma atualização autorizada e elegível, observar preparação/download/assinatura/instalação sem porcentagem fictícia; aplicativo aberto deve solicitar consentimento, NÃO preserva processo. Após SIM, conferir versão real e resultado conservado na lista. Cancelar antes de executar deve impedir lançamento; cancelar a espera deve indicar pendência sem repetição. Não remover/atualizar VC compartilhado apenas para testar.

PC FRACO: mesma build/hash, 1280×720 e 1440×900, navegação/pesquisa durante download, cancelamento e memória; iniciar portátil na bandeja pelo Windows, restaurar e conferir resultado. Confirmar também um ciclo de desinstalação/varredura/decisão SIM/NÃO/resumo da WORK12 e recuperação pós-boot somente leitura quando houver pendência técnica real. Não criar pendência de reinício artificial. U8/R7/R4 continuam abertos; AVG, operação real dos fabricantes e desempenho físico não foram homologados.
