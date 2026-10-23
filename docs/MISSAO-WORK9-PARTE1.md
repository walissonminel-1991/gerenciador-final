# MISSÃO WORK 9 — Parte 1: correções, segurança e desempenho

Data: 23/10/2026. Branch `piloto-diagnostico-github-v5.13.11.2`, PR #40 Draft.
Base conferida antes das alterações: `d065f9c6e3f1bf025aa7df256089be8e215cae39`.
Versão preservada: **5.13.11.48 TESTE**. Roadmap preservado: **27/42**.

<!-- WORK9_VALIDATION -->
Validação final em andamento. Este checkpoint não autoriza distribuição estável.

## Arquitetura reconstruída e revisão

| Área | Implementação e fluxo real |
| --- | --- |
| Interface | `MainForm`, em `Program.cs` e arquivos parciais. Controles escuros próprios, listas virtualizadas, tarefas de leitura em segundo plano e estado global `_busy` para operações explícitas. |
| Inventário | ARP HKCU/HKLM 32/64 → `InventoryReconciler`; MSIX por `LocalMsixInventory`/`LocalMsixSnapshot`; jogos por `GameLibraryAudit`. A consulta visual e a consulta de proteção agora têm políticas distintas de inclusão de entradas ocultas. |
| Atualizações | `RuckZuckCatalog`, `OfficialReleaseSources`, `WingetConsultativeProvider` e `Matching` produzem evidências consultivas. Somente receitas locais exatas de `VerifiedUpdaters`/`WindowsTerminalUpdater` podem preparar instalação. Download → SHA-256/assinatura → pré-fechamento → revalidação → lançamento único → confirmação posterior. Store permanece uma alternativa explícita; WinGet COM/CLI continua desativado. |
| Desinstalação | `UninstallBatch` vincula identidade e comando, `SilentUninstallPolicy` limita o silêncio, `RegisteredUninstaller` mantém a preparação protegida e a pós-conferência decide entre ausência de registro e conclusão pendente. MSIX protegido não recebe um executor genérico novo. |
| Resíduos | `BroadResidualDiscovery`/auditoria histórica/budgets classificam hipóteses somente para leitura. `SmallAppResidualCleanup` e `SevenZipCompleteCleanup` têm prévias específicas, consentimento separado e revalidação; `InstallerFileGuard` protege objetos e `RegistryTreeFingerprint` resume a árvore. Resultado geral nunca concede autorização de exclusão. |
| Sistema e diagnóstico | Windows Update usa trabalhador STA e catálogo compartilhado. Inicialização/hardware usam consultas com identidade da visita. RAM mantém cadências 2/12/20 segundos. Diagnóstico continua local/opt-in e agregado; protótipos de recepção não foram implantados. |

Dependências: .NET 10, WinForms, APIs Win32/WinRT e bibliotecas do framework. O projeto principal não ganhou `PackageReference`, serviço residente ou requisito de SDK para o usuário. O pacote continua x64 self-contained, sem extração automática da aplicação a partir de um executável único.

A revisão encontrou acoplamento entre inventário visual e decisões de segurança, duas implementações duplicadas de fingerprint do Registro e dependência excessiva do nome da tela como estado assíncrono. Os três pontos foram corrigidos. `Program.cs` continua extenso e concentra UI, inventário e coordenação; uma decomposição maior exige revisão própria. Não foi feita uma reorganização ampla de fontes, documentos ou funcionalidades nesta parte.

## Defeitos reproduzidos e corrigidos

Gravidade descreve o impacto possível, sem atribuição de CVE. As asserções repetidas em quatro combinações de Registro não são quatro correções diferentes.

| Caso | Gravidade e causa | Correção e evidência |
| --- | --- | --- |
| U1 | Média: cancelamento ocorrido enquanto `confirm()` aguardava ainda produzia `Confirmed`. | Nova conferência do token após a confirmação; resultado passa a indeterminado. `W9-U1`, `UpdateLaunchGate.cs`, `556919ff`. |
| U2 | Alta: a identidade pré-lançamento não comparava `MsiProductCode`; uma transição EXE/MSI podia passar pela revalidação. | Comparação do ProductCode junto aos demais campos originais. Controle positivo da mesma instalação preservado. `W9-U2`, `VerifiedUpdaters.cs`, `556919ff`. |
| U3 | Alta: Notepad++ podia ser confirmado somente pelo registro/versionamento mesmo após troca de pasta ou aparecimento de segunda instalação. | Conferência de família única, contexto original e versão do arquivo. Sumatra também exige versão PE verificável; seu antigo teste positivo com um cabeçalho PE sem recurso de versão foi corrigido. `W9-U3/U6`, `556919ff` e `7b7959ff`. |
| U4 | Alta: o pré-lançamento Sumatra aceitava qualquer arquitetura detectável, sem compará-la à receita já baixada. | Confere arquitetura e escopo com a receita, usando leitura protegida. Um binário vazio ou recusado retorna não comprovado. `W9-U4/U6`, `556919ff` e `7b7959ff`. |
| U5 | Alta: comando MSI com campo ProductCode vazio podia ser tratado como instalação Sumatra EXE. | Exige comando registrado analisável e sem identidade MSI antes do EXE silencioso. `W9-U5`, `556919ff`. |
| UN1 | Média: cancelamento na última leitura pós-desinstalação ainda podia confirmar ausência de registro. | Revalida token imediatamente após cada leitura assíncrona. A operação iniciada permanece pendente. `W9-UN`, `UninstallBatch.cs`, `7b534add`. |
| R1 | Média: arquivos de configuração com zero bytes eram recusados como instaladores inválidos, interrompendo a prévia. | Abertura residual própria admite tamanho zero; a política de instaladores permanece estrita. Recusas de integridade são tratadas como prévia não verificável. `W9-R1`, `InstallerFileGuard.cs`, `d1445f14`. |
| R2 | Alta: o arquivo e seus pais eram liberados entre a conferência do hash e `File.Delete(path)`. Um substituto podia ser apagado. | Remoção pelo mesmo handle validado, mantendo os ancestrais presos. Snapshot inclui volume/índice/identidade do arquivo. Troca adversarial é bloqueada; dados novos não são apagados. `W9-R2`, `d1445f14`. |
| R3 | Alta: cancelamento durante o inventário anterior à exclusão não era observado antes de apagar o arquivo. Diretórios também não recebiam nova consulta de presença. | Token após cada espera, revalidação por diretório e interrupção da limpeza se a instalação reaparecer. `W9-R3`, `SmallAppResidualCleanup.cs`/`SevenZipCompleteCleanup.cs`, `d1445f14`. |
| R4 | Alta: árvore com irmãos A/B e árvore A/B aninhada tinham a mesma representação; arrays REG_MULTI_SZ distintos também podiam colidir antes do SHA-256. | Formato com contagens, tipos e comprimentos, preservando fronteiras e unidades UTF-16. Rotina única, incremental e limitada. `W9-R4`, `RegistryTreeFingerprint.cs`, `d1445f14`. |
| UI1 | Média: voltar à mesma tela tornava novamente válido um resultado antigo, pois só o nome da tela era conferido. | Geração da visita invalidada ao trocar tela ou aba principal; startup, alternância e hardware conferem a visita antes de aplicar resultado. Finalização evita controles descartados. `W9-UI1/UI2`, `c69eba25`. |
| I1 | Alta: registros ocultos por `SystemComponent` eram omitidos também do inventário que confirmava ausência. | Consulta de segurança inclui registros ocultos, de atualização e nomes extensos, mantendo-os somente leitura. Caminhos/comandos continuam como evidência para proteção, sem conceder execução. Lista visual preservada. `W9-I`, `e62d106a`. |
| R6 | Alta: conhecer uma raiz de aplicativo não impedia sobreposição com uma pasta pessoal configurada. | Limite explícito para pastas especiais atuais, inclusive Downloads; recusa sobreposição e falha de consulta. Aplicado à captura e à revalidação dos alvos dos dois pilotos. `W9-R6`, `ResidualPersonalDataBoundary.cs`. |

Reforços adicionais, sem contabilizá-los como defeitos separados: liberação de objetos `Process`, leitura de raízes sem confundir permissão negada com ausência, cancelamento antes da mutação, proteção do pai ao remover diretório vazio e pós-conferência do Registro sem perder handles. Não houve alteração de ACL, tomada de propriedade ou exceção automática de antivírus.

## Evidências anteriores e validação Windows

1. [39974894526](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39974894526), código `90c7c783`: seis asserções negativas do atualizador e duas de navegação falharam. O arquivo vazio também interrompeu o ensaio residual.
2. [39975396551](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39975396551), código `3bc36dc2`: atualizador corrigido passou; 16 asserções residuais/desinstalação e duas de navegação ainda falharam. O harness passou a continuar após a recusa do arquivo vazio, revelando as colisões em HKCU/HKLM e Registry32/Registry64.
3. [39976177095](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39976177095), código `c69eba25`: política, regressões Windows e pacote passaram após as correções principais.
4. [39977665408](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39977665408), código `386a2872`: regressões e política passaram com a otimização. A etapa de pacote foi cancelada pela publicação do próximo teste negativo; não apresentar essa execução inteira como aprovada.
5. [39978488283](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39978488283), código `0ac9981d`: presença oculta falhou, com controle positivo da lista visual passando. [39979233063](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39979233063), código `e62d106a`: política, regressões e pacote passaram após a correção.

6. [39980176037](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39980176037), código `c3f2e826`: os dois pilotos aceitaram uma raiz sintética dentro de Documentos; ambas as asserções negativas de limite pessoal falharam. Nenhum arquivo pessoal foi excluído no ensaio.

Os testes WORK9 de Registro criam chaves exclusivas `GF-W9-*`, alteram sua hierarquia/conteúdo, recusam prévias antigas, apagam apenas folhas autorizadas e conferem a preservação de irmãs. O teste adicional cria o mesmo caminho nas duas visões HKLM e comprova que limpar Registry32 preserva Registry64. HKCU pode compartilhar dados entre visões; não foi apresentado como duas instalações físicas independentes.

A correção experimental `24068d93`/`d065f9c6` posterior à .48 já passou pelos controles positivos de abertura da chave-pai, visão, exclusão e pós-conferência. Não foi atribuída uma nova correção ao que já estava corrigido. Ainda falta confirmar a chave real do Notepad++ no PC FORTE.

## Desempenho medido

Classificação sintética, sem leitura/exclusão de arquivos reais ou rede. Mesmo .NET 10.0.13/SDK 10.0.402, Linux x64, oito processadores informados pelo ambiente. Ordem ABBA, processos separados executados em sequência, cinco aquecimentos por carga e 14 amostras medidas por versão/carga; compilação em níveis desativada nos dois lados. Base de código: `c69eba25`.

| Evidências de entrada | Mediana antes → depois | Alocações antes → depois, MB decimais | Redução de alocações |
| ---: | ---: | ---: | ---: |
| 5.000 | 48,47 → 37,52 ms | 23,83 → 20,81 | 12,66% |
| 10.000 | 83,77 → 78,93 ms | 47,66 → 41,62 | 12,67% |
| 20.000 | 160,89 → 155,76 ms | 94,11 → 82,03 | 12,83% |
| 50.000 | 316,25 → 224,38 ms | 142,09 → 123,97 | 12,75% |

Mudança: reutilizar o nome já normalizado do grupo e agrupar locais uma única vez. Hashes dos relatórios, incluindo decisões/evidências, ficaram iguais nas 112 amostras medidas. A carga de 50.000 mantém o limite de 30.000 entradas e informa 20.000 não examinadas; a otimização não oculta esse limite.

[Dados brutos, CPU, memória, hashes das fontes e método reproduzível](benchmarks/WORK9-classificacao.json). O comando do harness é `dotnet run --project TestesResiduos/TestesResiduos.csproj -c Release -- --benchmark-work9`; usar exatamente o mesmo harness nos dois códigos.

Alocação não equivale à RAM residente do GF. Não houve comparação física antes/depois de inicialização, inventário, consulta de fontes, disco ou consumo ocioso nos dois PCs. Os logs da .48 fornecem uma referência histórica do PC FORTE, não uma comparação com a nova candidata. Leituras de presença anteriores à exclusão continuam deliberadamente sem cache.

## Interface, limites e pendências

As regressões cobrem 132 combinações resolução/aba, 22 cenários de tela e 12 ciclos de bandeja, além de Inicialização em DPI 100/125/150/200%. Foram inspecionadas capturas sintéticas de 720p, 1440×900 e investigação residual. Conteúdo, listas e botões mantêm a identidade escura e compacta. No Windows Server do runner ainda aparecem elementos não cliente claros em diálogos; não equivale à validação visual integral no Windows dos dois PCs. Permanecem chamadas nativas de aviso/erro a revisar na parte 2.

- U8/R7/R4 continuam abertos. Nenhum aplicativo foi instalado, desinstalado ou atualizado de verdade para homologá-los nesta missão.
- A investigação geral continua parcial por limites locais/globais, profundidade, permissões e links preservados. Sumatra sem achados específicos não comprova ausência em todo o computador. O arquivo vazio agora pode ser investigado, em vez de abortar a prévia específica.
- Arquivos de configuração/histórico continuam sendo dados do usuário, mesmo dentro de uma pasta dedicada. A lista exata e o consentimento separado permanecem necessários. Integrações Windows, componentes compartilhados e alvos desconhecidos não receberam exclusão heurística.
- A mutação externa de Registro entre a última conferência e a chamada nativa de exclusão não é uma transação atômica. Reinstalação externa, aliases de caminhos e encerramento inesperado exigem mais ensaios; não alegar segurança universal ou concluir R4 para distribuição ampla.
- Receitas são fixadas por versão/origem/hash e não cobrem todo o catálogo. Ensaios opcionais de rede/downloads oficiais não foram acionados nesta missão; não executamos instaladores desconhecidos para ampliar cobertura.
- Não há medição pós-correção em PC FRACO, aprovação AVG/SmartScreen ou assinatura Authenticode nova. Drivers e implantação de diagnóstico remoto permanecem fora desta parte.

## Próximos testes físicos mínimos

**PC FORTE:** usar apenas o artefato final identificado pelo commit fonte. Conferir uma única prévia Notepad++ com o aplicativo ausente; a chave própria remanescente deve desaparecer após a confirmação e continuar ausente após reiniciar. Reabrir a investigação para verificar que não permaneceu cartão obsoleto. Sumatra deve informar apenas o resultado dos locais consultados. Somente após essa fase, testar separadamente as versões antigas descartáveis já combinadas: atualização silenciosa, aplicativo aberto/fechado e versão final real. Não executar limpeza comparativa do IObit simultaneamente.

**PC FRACO:** primeiro somente navegação e leitura: abrir, alternar Inicialização/Windows Update e outras abas, retornar à mesma tela, minimizar/restaurar e encerrar. Conferir 1440×900/DPI habitual, botões acessíveis, ausência de cartões atrasados e consumo ocioso. Medir inventário/atualizações com o mesmo conjunto de aplicativos. Instalação/desinstalação física fica para uma bateria controlada posterior.

## Checkpoint para retomar a parte 2

Código e evidências estão na branch experimental; `main`, releases estáveis e merge do PR não foram alterados. A próxima ação é conferir a CI final e sua origem, realizar a bateria física mínima e só então organizar documentação/workflows obsoletos sem apagar histórico necessário. Preservar os testes negativos e não transformar testes simulados em checkpoints físicos concluídos.

Referências de API usadas na implementação: [exclusão pelo handle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle), [estrutura de disposição](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_disposition_info), [pastas conhecidas](https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid) e [consulta sem verificar/criar a pasta](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ne-shlobj_core-known_folder_flag).
