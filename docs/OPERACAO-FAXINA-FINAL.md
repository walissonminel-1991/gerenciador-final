# Operação Faxina Final — revisão consolidada

21/10/2026 · **v5.13.11.26 TESTE** · roadmap **26/42 (61,9%)**.

Código inicial: [`5e3ecd4`](https://github.com/walissonminel-1991/gerenciador-final/commit/5e3ecd450d6e41ae8a432caca8c590375ef9585d).
Implementação: [`79f2be4`](https://github.com/walissonminel-1991/gerenciador-final/commit/79f2be4e3aafe4496431dd4b3464376f4f47291d).
Correção após a primeira regressão Windows: [`a85a7e1`](https://github.com/walissonminel-1991/gerenciador-final/commit/a85a7e1e3575a831f1bda27018d84d56f4dfc9fe).
Código anterior integralmente aprovado: [`d96b635`](https://github.com/walissonminel-1991/gerenciador-final/commit/d96b6355f1e44df8517c02fa4b1bca5312a69ecf).
Código da .25 aprovado, incluindo o contrato Windows Update: [`190d47d`](https://github.com/walissonminel-1991/gerenciador-final/commit/190d47d9167646ab3896319527ba3ae48a7effeb).
[PR #40](https://github.com/walissonminel-1991/gerenciador-final/pull/40) mantida Draft.
A `main` permaneceu em `9ac3dc167b9e67ac50628749d5ad33c563c09376`.

Esta revisão implementou correções; não certifica ausência de vulnerabilidades,
compatibilidade AVG, eficácia de manutenção ou prontidão para distribuição.
Não houve execução de manutenção nos PCs, exclusão de resíduos, alteração de
drivers, envio de diagnóstico, deploy, release ou remoção de histórico remoto.

## Incorporação conservadora — v5.13.11.26 TESTE

Base real conferida: `26925169430969b35799626bc972474cd0269e2b` (.25),
com as três Actions aprovadas. Código desta incorporação:
[`cbea81a`](https://github.com/walissonminel-1991/gerenciador-final/commit/cbea81acc2ad640f661afe75be209d97faa569fd).
A conferência visual encontrou o novo motivo abaixo do resumo técnico extenso.
[`8578fa3`](https://github.com/walissonminel-1991/gerenciador-final/commit/8578fa3a8698c60b723e528feaa073f4fc066502)
coloca o motivo antes da cobertura detalhada e acrescenta a regressão de ordem.
Este é o código final da .26; o número não mudou durante a revisão deste lote.
As árvores enviadas pelo conector foram comparadas pelos objetos Git com as
árvores locais revisadas (`bfa166cddde964e221bff3a4bbf029424fd07a4c` e
`0a53b85b0bc2df9564508155f2ce977f214c0cd5`).
O terminal não possuía credencial de push; a gravação pelo conector exigiu o HEAD
esperado e `force=false`. Nenhum histórico foi reescrito. Revisões independentes
de procedência e preservação foram confrontadas antes da integração.

### Fronteiras e resultado implementado

`HistoricalResidualAudit` e seus cinco itens continuam consultivos e restritos;
`ApplicationIdentityCatalog` conserva identidade/editor e não concede ações.
`ResidualPathIndex`, guard de arquivos, budgets e `ResidualPreview` permanecem
fronteiras de proteção. Nenhum catálogo novo, parser externo, esquema, caminho
interpretável, serviço, dependência ou conexão foi introduzido.

- `ResidualInspection.cs`: enum e pistas compiladas pequenas a partir do nome e
  tipo de metadado já observado. Explica favoritos/histórico, credenciais,
  cookies, sessão/perfil, cache, backup, playlist, saves e fontes MSI. Fallback
  desconhecido preserva. Nomes parecidos não recebem categoria por substring.
- `ResidualExplorerDialog.cs`: apresenta a explicação no detalhe existente,
  explicita que finalidade/vínculo não foram confirmados e mantém o conteúdo
  fechado. Nenhum botão novo ou autorização de limpeza.
- `BroadResidualDiscovery.cs`: cache, dados de sessão, backups, playlists e saves
  deixam de ser nomes de produtos independentes. Permanecem subordinados ao
  pai observado, sem comprovar propriedade exclusiva ou abandono. Não atravessa
  novos diretórios nem aumenta profundidade ou limites.
- Recusa `InvalidDataException` do guard passa a produzir parcialidade controlada
  na sessão/fluxo de UI. A recusa não vira caminho aceito nem autorização.
- `TestesResiduos/ResidualInspectionTests.cs`, `ResidualMissionTests.cs` e
  `TestesLayout/ResidualExplorerTests.cs`: fixtures e consequências verificadas.
  Projeto/manifesto identificam **5.13.11.26**. Nenhum worker foi alterado.

`CanRemove` e `CanExecute` continuam falsos. Todas as entradas da inspeção
continuam preservadas. Um nome de cache não comprova descarte; CacheStorage
pode guardar estado offline de sites. As pistas não medem espaço recuperável,
não leem conteúdo nem provam que um arquivo pertence a um navegador específico.

### Matriz de procedência e decisões

| Origem revalidada / arquivo | Licença / uso | Decisão e efeito no GF |
| --- | --- | --- |
| [Dism-Multi-language `875bce3`](https://github.com/Chuyu-Team/Dism-Multi-language/tree/875bce3511995000ceed38e3b7aba5917e4868cc): LICENSE, README, Data.xml (blob `a230f0c15057ff9aadce2c781ff5863c1437dedf`) | MIT, ©2016 Chuyu-Team. Referência factual; **nenhum código, texto expressivo ou regra copiado**. Não presume licença do motor/binários. | Incorporar a necessidade de explicar e preservar backups/fontes de reparação, por implementação própria. `MobileSync/Backup`, PatchCache/MSOCache e Office servem como contraexemplos de descarte. Sem importar caminhos ou comandos. |
| Dism Data.xml: CleanCollection4/SystemOptimization/DefaultFilterList | Mesma revisão; XML ativo conferido: **73/163/32** itens, sem comentários. | Contagens não medem segurança. Rejeitar limpeza de Windows.old/WinSxS/Installer/Prefetch, movimentações, Registro, serviços e chamadas internas. |
| [WinScript `2afba40`](https://github.com/flick9000/winscript/tree/2afba4028f2ec16c07c28f7151d3ea9d43d89884): LICENSE, scripts.ts (blob `50ed16dac2095e84316eed61dd7a1cb295922210`) | GPLv3. Somente referência de comportamento/contraexemplo; **nenhuma cópia, tradução ou adaptação de implementação GPL**. | A remoção de places.sqlite motivou teste negativo de preservação. A solução GF foi escrita com documentação oficial, sem importar as 126 ações, presets ou organização do catálogo. MIT do GF preservada. |
| WinScript: appinstall.ts e .github/workflows/publish.yml | GPLv3; análise documental | Referência futura de identidades e SignPath. Não adicionar WinGet/Chocolatey, workflow, secrets, conta ou solicitação de assinatura. Não fornecem novo motor de inventário nem reputação transferível. |
| [Mozilla: conteúdo dos perfis](https://support.mozilla.org/en-US/kb/profiles-where-firefox-stores-user-data) | Fatos técnicos, consulta em 21/10/2026; nenhum texto copiado | Confirma places.sqlite, bookmarkbackups, cookies.sqlite, key4.db/logins.json, sessionstore.jsonlz4 e perfis separados/MSIX. Pistas e fixtures próprias; nenhuma nova varredura de perfis. |
| [Chromium `06ac4fc`](https://github.com/chromium/chromium/tree/06ac4fc411aefd2825ffe09017052da8732437b0), tag 140.0.7339.138 | Fontes oficiais BSD; apenas identificadores/fatos, sem código copiado | chrome_constants.cc, bookmark_constants.cc, history_constants.cc, password_manager_constants.cc, session_constants.cc, gpu_disk_cache_type.cc, storage_partition_impl.cc e cache_storage_manager.cc confirmam nomes de perfil/dados/cache. Pistas locais próprias; não garantem conteúdo nem exclusividade. |
| [Microsoft: Installer Cache](https://learn.microsoft.com/en-us/troubleshoot/windows-client/application-management/missing-windows-installer-cache) e [WinSxS](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/clean-up-the-winsxs-folder) | Documentação oficial; fatos, sem copiar implementação | Preservar fontes de instalação/reparo; rejeitar limpeza manual de componentes. Não duplicar DISM/SFC ou mecanismos próprios existentes. |

Nenhum material expressivo MIT foi reutilizado; não há novo componente externo
para distribuir nem aviso de código transplantado a adicionar. A tabela registra
procedência factual, não substitui licença caso haja cópia em um lote futuro.
Não consultamos revisões novas dos dois projetos: os commits são os fixados pela
missão. Debloat, mudanças de segurança/serviços, drivers, script remoto e presets
ficaram fora. Inventário, preservação compartilhada, prévia, DISM/SFC e guards já
eram atendidos pelo GF e foram mantidos.

### Validação da incorporação

Código final `8578fa3`: **três Actions aprovadas**, sem falhas intermediárias
de CI neste lote. A primeira revisão `cbea81a` também passou nas três Actions;
a conferência visual posterior motivou a correção de apresentação acima,
sem excluir testes nem esconder falhas.

| Validação nativa Windows final | Resultado e comparação com a base .25 |
| --- | --- |
| [Regressões Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39642731857) | Resíduos **351/351** (base **337/337**), MSIX 30/30, instaladores 95/95, comparação 18/18, catálogo 14/14, multifontes 64/64, drivers 53 verificações e encerramento 30/30. Privacidade, RAM, fixtures de limpeza/restauração e bandeja aprovados. |
| Layout | Mantidas 132 combinações resolução/aba e DPI de 100% a 200%. Novos detalhes/recusa de guard aprovados; 11 capturas do runner verificadas por ZIP, com inspeção visual da preservação em 1280×720 e 1440×900. Resoluções simuladas, não os computadores do usuário. |
| [Build Windows x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39642731837) | Aplicativo sem erros/avisos, publicação self-contained, ativação de manifesto e origem aprovadas. SHA256SUMS de **273 arquivos** conferidos no runner e novamente no ZIP baixado, sem executar o aplicativo. |
| [Políticas/privacidade](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39642736539) | Aprovada. Mantidos três workflows e suas permissões/SHAs. Jobs externos opcionais e atestação exclusiva da main ficaram ignorados; não contam como executados. |

Artefato escolhido: [GerenciadorFinal-v5.13.11.26-165069f3-TESTE-win-x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39642731837/artifacts/12366321007),
ID `12366321007`, **58.872.989 bytes**, retenção indicada até **28/10/2026**.
Fonte `8578fa3a8698c60b723e528feaa073f4fc066502`;
merge sintético testado `165069f332001a01e2da8af640f91606c0b67b9a`;
base `9ac3dc167b9e67ac50628749d5ad33c563c09376`.
BUILD-INFO confere esses valores. Hashes SHA-256 verificados:

- ZIP: `8a30abca5cefcee7d5168c5741ac71975abc294c0517ea99d133c71bf91d66e2`
- EXE: `293df2f1110bbddc19b7ceb1eb981968f4361bb3ca1e55ecde661e77327ce4bf`
- DLL do GF: `cc68083c0f8493c7044002a3baebfb974e99846179c6373babdaae40069579af`

A assinatura Authenticode permanece ausente. A consolidação documental posterior
não muda o código de produção; o pacote escolhido continua identificado pela
fonte acima, não pelo HEAD documental. PR permanece Draft, sem merge/release.


Local Linux, SDK 10.0.402 / runtime 10.0.13: aplicativo e layout compilados com
zero erros/avisos; subconjunto `--missao` passou **151 verificações**, contra 137
na base. Políticas **14/14** (111 fontes e links/esquema/versão), protótipos Node
**10/10**, sem rede/deploy. Isso não substitui execução nativa Windows.

Fixtures: dois perfis alternativos/MSIX sintéticos, base places.sqlite com
FileShare.None, dados pessoais ao lado de caches, credenciais/cookies/sessões,
playlists/backups/saves/MSI, conteúdo byte a byte intacto, nomes parecidos
preservados, link externo recusado, cancelamento/limites e limpeza das fixtures.
Classificação separa os fabricantes, protege aplicativo instalado e mantém
Unknown/BasisDisposition quando inventário é parcial. UI verifica explicação,
incerteza, texto nativo completo, botões e capturas em 1280×720, 1440×900 e
2560×1440, além da matriz existente.

A recusa injetada valida a fronteira da UI e preserva resultados anteriores;
**não reproduz a corrida nativa entre pré-checagem e pin**. A estrutura MSIX de
fixture não exercita PackageManager. Não houve testes presenciais ou operações
reais de limpeza/reparo/instalação. Nenhuma falha de CI foi ocultada.

### Custo e limites

Benchmark de classificação existente, Linux/.NET 10.0.13, oito processadores
lógicos expostos, base `2692516` e árvore da .26. Ordem alternada nas rodadas:
candidata/base e base/candidata. Medianas de seis amostras por revisão nos três
primeiros cenários e três no saturado; a última rodada não registrou o cenário
saturado completo e esse par foi excluído desse cenário em ambas as revisões.
A medição inicial com saída incompleta também foi excluída. Resultados lógicos
iguais em todos os registros completos; no saturado, 15.000 grupos e **20.000
evidências não avaliadas**, sem mudar o orçamento de 30.000 entradas.

| Apps / evidências | Mediana base / .26 (ms) | Alocação mediana base / .26 (bytes) |
| --- | --- | --- |
| 500 / 5.000 | 60,46 / 66,51 | 24.616.968 / 24.616.968 |
| 1.000 / 10.000 | 146,47 / 117,29 | 49.224.952 / 49.224.932 |
| 1.000 / 20.000 | 281,74 / 185,98 | 97.293.136 / 94.894.016 |
| 1.000 / 50.000 | 124,12 / 177,85 | 140.939.840 / 140.939.832 |

Tempos variam nos dois sentidos e as alocações incluem trabalho concorrente/JIT;
**não demonstram ganho ou regressão causal de desempenho**. O benchmark não mede
o custo visual de selecionar uma explicação. As pistas são calculadas só para
metadados já consultados e detalhe selecionado, sem varredura nova no arranque.
CPU/RAM/abertura reais continuam pendentes.

Fora do lote: parser histórico embutido ainda aceita campos extras/duplicados e
precisa de revisão própria antes de expandir seu contrato. Não foi acrescentado
catálogo nem nova origem de regras; a falha de um catálogo adicional não se
aplica. Não foi ampliada a autoridade das regras existentes para acomodar fontes
externas. Também continuam pendentes **MSIX 0x80040154 no PC fraco, AVG, validação
de drivers e medições reais**. Roadmap **26/42**; R4 suspenso.

### Roteiro consultivo da .26 nos PCs

1. Conferir versão, SHA fonte, BUILD-INFO e SHA256SUMS do artefato TESTE abaixo.
2. No PC fraco, repetir consultas de inventário e diagnóstico na mesma sessão;
   registrar estado parcial/fase/apartment/HRESULT sem reinstalar Store.
3. Na investigação existente, inspecionar apenas locais já listados; ao selecionar
   arquivos/pastas conhecidos, conferir explicação de preservação e incerteza.
   Não é obrigatório que esses dados apareçam: raízes/profundidade não cresceram.
4. Conferir cache ao lado de dados importantes, parcialidade e cancelamento;
   não apagar dados, executar reparos, instalar programas nem alterar drivers.
5. Repetir no PC principal, conferir legibilidade em 1440×900/2560×1440 e anotar
   DPI, CPU/RAM e alertas AVG observados, sem desativar proteções.

As seções seguintes conservam as evidências da revisão anterior (.25).

## Alcance e método

| Medida | Evidência e limite |
| --- | --- |
| Histórico alcançável inicial | 1.072 commits no HEAD; 1.086 considerando todas as referências locais obtidas. Indexação desde a raiz e exame dirigido dos marcos e implementações relevantes; não equivale à revisão manual de cada linha de cada commit. |
| Primeiro commit disponível | `a49e401`, 17/10/2026, publicação v5.12.3. Versões anteriores mencionadas nas conversas não estão disponíveis neste histórico. |
| Inventário inicial | 192 arquivos e 1.900.756 bytes nos objetos da árvore Git; 73 arquivos C# de produção na raiz, 13 projetos incluindo o aplicativo e 50 Markdown. |
| Inventário do código revisado | 192 arquivos e 1.944.735 bytes em `190d47d`; 71 arquivos C# de produção na raiz. Três arquivos retirados e três componentes/testes adicionados. O relatório documental é adicional. |
| Verificação de código e referências | 111 arquivos-fonte nas políticas estáticas; 67 referências XML concretas de projetos/propriedades sem falta; links relativos de Markdown verificados. A varredura textual não substitui análise de fluxo ou fuzzing. |
| Segredos históricos | 1.510 blobs distintos percorridos; 1.508 UTF-8, cerca de 38 MB de texto. Nenhum padrão óbvio de token GitHub, chave privada ou credencial AWS encontrado. Não é prova universal de ausência; dados pessoais em metadados de autoria não foram republicados. |
| Dependências | Sem `PackageReference` explícito nos 13 projetos. Há SDK, frameworks e projeção Windows implícitos. NuGet Audit habilitado inclusive para transitivas e indisponibilidade do serviço; consulta atual não indicou pacotes vulneráveis. Isto não audita o Windows nem todos os componentes do runtime. |
| Revisões independentes | Inventário/drivers, execução/instaladores, resíduos/privacidade, histórico/CI e limpeza temporária; revisão cruzada dos handles e integração antes do envio. |

Os números de arquivos e bytes acima usam objetos Git, sem `bin`, `obj`, caches
ou diretórios de teste. Não representam espaço liberado no GitHub. O relatório
principal concentra esta missão para evitar novos diários duplicados.

## Evolução histórica e decisões preservadas

| Área | Marcos examinados | Decisão atual |
| --- | --- | --- |
| Primeira versão acessível | `a49e401`, `278a554` | Win32, manutenção Windows e log local já existiam; a ordem incorreta de campos da restauração e a limpeza por pathname vieram dessa base. |
| Catálogo de atualizações | `d5694a8`, `88e4ef3`, `efab9fb` | RuckZuck/HTTPS e receitas locais verificadas substituíram dependência de comandos WinGet. Visual C++ sem SHA fixado continua sem execução automática. |
| Assinatura e instaladores | `3feff14`, `de1c100`, `fc0cff2`, `2b9e77f`, `6fa4aca`, `c66d3f9` | Preservadas travas de arquivo/ancestrais e retenção por sessão. Não restaurado loader que tentava interpretar um EXE como DER. |
| Inventário ampliado | `002a0d1`, `94f6c7d`, `0edfe97`, `52fbe82` | Registro, MSIX e jogos independem de receita. Inventário parcial e filtro informativo não concedem execução. |
| Desinstalação e resíduos | `bb68434`, `0784e02`, `ae32eaa`, `0f3701e` | A exclusão de resíduos foi deliberadamente suspensa; o código antigo mais destrutivo não foi restaurado. |
| Cobertura e inspeção | `3532cf3`, `cb674fc`, `e3b614a`, `d5cf179`, `f58a870`, `625a93f`, `efc53c2` | Mantidos limites, cache progressivo, motivos de parcialidade e distinção entre leitura pontual e fonte indisponível. |
| RAM e processos | `0fb97c6`, `8ec9f89`, `8f1ffb2`, `987b244`, `7d6aa13` | O monitor antigo foi substituído por RAM global e acesso às tarefas do Windows. Não foi reativada enumeração contínua de processos. |
| Drivers | `109a072`, `9ef7d87`, `0f7d575` | Consulta WMI, vínculos INF e histórico local; a antiga grade foi substituída por cartões. Driver Store completo e gestão de pacotes continuam pendentes. |
| Interface | Histórico de `ResponsiveLayout`, `StartupWindowSizing`, `WindowChrome` e testes de layout | Preservados tema escuro, bandeja, DPI e navegação. Sem reescrita visual durante esta auditoria. |
| Privacidade | `4c4b4a8`, `988f735`, `737eff4`, `e9118ac` | Coleta opcional local, revogação e prévia; compartilhamento por Issues/ingestão pública não foram restaurados. |
| GitHub e cadeia de suprimentos | `5f7cb2c`, `5e3ecd4` | OIDC só no contexto confiável da main; três workflows atuais, sem credenciais persistentes em checkout. |

Mudanças de outras branches que não são ancestrais do HEAD, como tentativas
antigas de Driver Store/DISM, foram identificadas como alternativas históricas,
não como funcionalidades perdidas que devam ser recolocadas automaticamente.

## Auditoria da limpeza anterior

Confirmadas as **50 exclusões**: 38 workflows versionados, dois fluxos antigos
e dez guias LEIA-PRIMEIRO; 1.632.650 bytes retirados da árvore ativa.
Referências sobreviventes aos workflows removidos são descrições históricas;
não foi encontrada dependência ativa ou link local quebrado causado pela faxina.

A limpeza é da **branch da PR**. A `main` ainda contém 42 workflows e não foi
alterada nesta missão. Não foram excluídos runs, artefatos, tags, branches ou
releases. A listagem remota contabilizava 1.383 runs ao ser consultada; o total
cresce durante o trabalho. A listagem global de artefatos não estava disponível
pelo acesso utilizado. Os artefatos recentes conferidos têm retenção de sete dias.

Os três fluxos cobrem publicação/integridade, regressões Windows e políticas de
segurança/privacidade. Os cinco SHAs usados por checkout, setup-dotnet, upload/download de artefatos e attest foram conferidos com as respectivas tags nos repositórios oficiais de Actions. A revisão recuperou caminhos manuais para fontes reais,
consulta WMI e downloads verificados dentro do workflow atual. Jobs opcionais
ignorados em um PR não contam como testes realizados.

A lista explícita de workflows continua exigida, agora incluindo `.yaml`.
Um teste com diretório sintético verifica recusa de fluxo inesperado e aceitação
após revisão da lista. É possível acrescentar testes legítimos sem acumular um
workflow por versão. Guardrails de strings continuam complementares, não são
prova de inexistência de efeitos destrutivos.

## Achados comprovados e correções

Foram tratados **14 problemas da base de produção**, sete de classificação média e sete
baixa. A severidade considera o cenário aplicável, não um possível impacto máximo
sem prova. Não foi comprovada vulnerabilidade crítica ou alta nesta execução.

| ID / gravidade | Cenário e consequência | Correção / teste relevante |
| --- | --- | --- |
| F01 · baixa | `LocalMsixInventory.ReadOnce`: falha tardia de enumeração descartava aplicativos já observados. Retry vazio também podia substituir evidência parcial. | Enumeração testável preserva itens/contadores. Retry apenas antes de observar qualquer pacote. Testes de `MoveNext` com falha, itens individuais e retry parcial. |
| F02 · baixa | `RunMaintenanceAuditAsync` descartava a cobertura do inventário, consultava jogos novamente e emitia `parcial=False` após falha MSIX. Falha de propriedade WMI também podia desaparecer da cobertura. | Uma fotografia é reutilizada; `InventoryIncomplete` e `Incomplete` incluem a cobertura pertinente. Falhas PnP não justificam ausência histórica. Três regressões de propagação e consulta sintética completa. |
| F03 · baixa | Driver com `DeviceName` vazio aparecia desconhecido mesmo com nome PnP disponível para o mesmo ID. | Fallback exclusivamente pelo ID correspondente; dados de versão permanecem brutos. Testes de mesmo ID, ID alheio e nome já preenchido. |
| F04 · média | Limpeza temporária verificava atributos e depois voltava a excluir pelo caminho; troca de diretório poderia mudar o alvo, especialmente em operação elevada. Exploração administrativa não foi reproduzida em WindowsTemp. | `SafeTemporaryCleanup` mantém cadeia/folha abertas e usa `FileDispositionInfo` sobre o mesmo handle. Recusa reparse, hardlink, escritor ativo e arquivo recente. Fixtures verificam preservação externa, renomeação bloqueada, limites e cancelamento. |
| F05 · média | `DiagnosticTrace.Start` usava nome previsível com truncamento; link/hardlink pré-criado podia modificar outro arquivo com privilégios do processo. Append não revalidava identidade. | Remove só a entrada antiga, usa `CreateNew`, retém identidade/ancestrais e compara Volume/Index no handle efetivamente aberto antes do append. Testes de links, leitor comum, concorrência, troca recusada, reinício e fallback. |
| F06 · média | Leitura/expiração de consentimento e pendência podia seguir redirecionamento de pasta; expiração podia excluir pendência em outro diretório. | Caminho local validado, leitura protegida por handle e ancestrais presos na exclusão. Links de folha não importam consentimento; exclusão remove só a entrada local. Revogação continua válida na sessão quando o disco falha. |
| F07 · média | MSI GameInput podia ser substituído entre extrair e prender o arquivo; outro MSI Microsoft assinado podia ganhar recibo sem ser o conteúdo do ZIP autenticado. | SHA do MSI preso deve corresponder ao conteúdo interno do NUPKG já autenticado. Testes de conteúdo intacto, substituição e cancelamento, sem executar MSI. |
| F08 · média | Desinstalador registrado como `MsiExec.exe` era lançado com nome relativo; pesquisa em pasta/PATH podia escolher homônimo. Parser também cortava pasta contendo `.exe_`. | Tokens Windows conhecidos vinculados a System32; outros relativos recusados; absolutos registrados preservados. Parser respeita fim do executável, recusa controles e preserva conversão MSI `/I` → `/X`. Testes não lançam desinstaladores. |
| F09 · média funcional | `STATEMGRSTATUS` tinha campos em ordem/alinhamento incorretos; podia ler sequência como status e finalizar restauração com sequência errada. | Cabeçalho Windows SDK exige `Pack=1`, status offset 0, sequência offset 4, tamanho 12. Fixtures de bytes e callbacks verificam início/fim e falhas, sem criar ponto real. |
| F10 · baixa | `ResidualCoverage.Combine` tratava duas observações independentes iguais como uma única observação. | Deduplicação por identidade da observação; resumos aninhados continuam sem dupla soma. Duas regressões específicas. |
| F11 · baixa | Falha na abertura/enumeração da raiz inteira do Registro era apresentada como leitura pontual. | Só o catch externo usa fonte indisponível; falhas de fabricante continuam pontuais. Duas ACLs em HKCU sintético, restauradas em `finally`. Não atribuído retroativamente ao PC fraco. |
| F12 · baixa | Scripts de ZIP/setup/hash e Inno ainda rotulavam pacotes como v5.12.3. | Scripts derivam versão do projeto; Inno do PE publicado. `--print-version` dos três scripts é validado no Windows sem publicar/assinar. Compilação completa do instalador assinado permanece pendente de certificado. |
| F13 · baixa | Consulta WMI de hardware liberava a linha depois de um `break` no limite de 16 resultados; a última linha não chegava à liberação explícita. | Liberação em `finally`, inclusive no limite e em exceções. Verificação de fluxo e compilação/regressões; não foi medida redução de recursos COM nos PCs. |
| F14 · média funcional | Desde `ed6a73a`, a consulta Windows Update gerava GUID@revisão, mas UI e worker ainda esperavam GUID sozinho. A operação supervisionada era bloqueada; teste do helper isolado não detectava a integração incompatível. | Codec canônico compartilhado preserva revisão entre consulta, seleção, serialização e worker. Teste de percurso completo e recusa de GUID sem revisão, vazio, duplicata, revisão inválida e seleção acima de 30; sem instalar atualizações. |

### Medidas preventivas e organização

- Certificado passa a vir do DER do **assinante efetivamente validado** pelo
  WinVerifyTrust, com liberação do estado nativo no `finally`. Eliminado
  `SYSLIB0057`; mantidos SHA, revogação e editor exato. Testes incluem assinatura
  ausente, host oficial do SDK com assinatura incorporada e editor divergente.
- Protótipo de homologação exige HTTPS, recusa cookies e versão com quebra de
  linha. Receptor público continua desativado; não houve deploy.
- `BUILD-INFO.txt` identifica SHA fonte, base e merge sintético testado. O SHA
  no nome de artefato de PR pode ser o merge, não o HEAD fonte.
- Retirados `ProcessMonitor.cs`, `ProcessMonitorUi.cs`, `FlickerFreeProcessGrid.cs`,
  classe sem uso `ProcessHistorySampler`, método privado sem chamadas
  `SafeDescendant`, `CatalogJson` e opção JSON órfã. `ProcessSamplingPolicy`
  permanece porque governa a RAM atual.
- Corrigidas três desreferências possivelmente nulas nos testes de layout,
  fazendo ausência de texto acessível falhar com a mensagem do teste.
- Documentação vigente esclarece consultas HTTPS automáticas, ausência da CLI
  WinGet e distinção entre resumo opcional e log técnico da candidata de teste.

### Regressão introduzida encontrada nesta missão

A primeira bateria Windows de `79f2be4` falhou no teste negativo de raiz
redirecionada. `InvalidDataException` não deriva de `IOException`; a recusa de
segurança escapava dos filtros de erro e interrompia a suíte. A mesma fronteira
existia nos novos usos de validação em log e privacidade.

`a85a7e1` trata essa recusa explicitamente, preservando falha controlada/fallback
sem aceitar o caminho. Os testes negativos existentes permanecem; foram
acrescentados cancelamento durante a consulta e liberação de handles nesse caso.
Não houve `continue-on-error`, exclusão de teste ou redução da proteção.

Duas execuções seguintes expuseram expectativas erradas da fixture positiva
Authenticode: PowerShell do Windows retornou `TRUST_E_NOSIGNATURE` para a
modalidade de assinatura incorporada; o host `dotnet.exe` validou, mas seu
Common Name é `.NET`, não `Microsoft Corporation`. A fixture passou a usar o
host oficial e exigir `.NET`, mantendo a recusa obrigatória de editor divergente.
As receitas de produção e sua política de revogação não foram alteradas.

A execução de `b4b1a91` aprovou 95/95 no atualizador e depois encontrou a
restauração ineficaz da DACL de teste: reaplicar um objeto de segurança sem
seções marcadas como alteradas podia não persistir a restauração. A correção em
`d96b635` reaplica o descritor binário da DACL e testa leitura após restaurar,
tanto na nova fixture de Registro quanto na fixture de arquivo já existente.
Nenhuma ACL do Windows ou dos computadores do usuário foi modificada.


## PC fraco: evidência e incerteza

Foram lidos os diagnósticos reais `.23` e `.24`, sem publicar seus arquivos.
Ambos informam Windows 10.0.26200, processo/sistema x64 e runtime 10.0.13.

| Observação | Interpretação permitida |
| --- | --- |
| .23: 123 pacotes observados, 19 apps MSIX, 74 entradas, status ok, sem retry | Aquela sessão consultou a fonte com sucesso. |
| .24: zero pacotes observados, `COMException(0x80040154)`, retry verdadeiro, 55 entradas | Fonte indisponível naquela consulta; não comprova desinstalação dos 19 apps. |
| .24: diagnóstico completo posterior com 55 e `parcial=False` | Erro de propagação de cobertura, corrigido em código; não comprova recuperação MSIX. |
| 108 associações, 50 INFs e três sem INF válido | Ausência de metadado ou associação incompleta; não autoriza remoção de pacote. |
| 12 grupos/41 locais, falhas pontuais e limites | Evidência consultiva parcial; não autoriza exclusão nem atravessar links. |

A causa de `0x80040154` **não foi reproduzida nem eliminada** neste ambiente.
Novos campos distinguem `activation`, `query`, `enumeration`, `package` e
`limit`, além de apartment e HRESULT desembrulhado de wrappers conhecidos.
PackageManager é documentado como Agile e `ThreadingModel.Both`; não há base
para forçar STA, registrar COM, reinstalar Store ou atribuir a falha ao AVG.

Versões estranhas de drivers continuam sendo o texto informado pelo WMI.
Não foi encontrada conversão que justificasse reescrevê-las. O tooltip explica
fonte e incerteza; mesmo fornecedor, presença ou assinatura informada não
certificam compatibilidade ou assinatura de um pacote a instalar.

## Validação e evidências

A documentação consolidada posterior não altera o executável validado abaixo.

**Código `190d47d`: as três Actions aprovadas.** Aplicação e build x64 com zero erros/avisos; manifesto e hashes conferidos. Jobs externos opcionais e atestação da main não foram executados neste PR.

| Validação Windows final | Resultado |
| --- | --- |
| [Regressões Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39620933920) | Aprovada: comparação 18/18, catálogo 14/14, multifontes 64/64, instaladores 95/95, drivers 53 verificações, resíduos **337/337**, MSIX **30/30**, encerramento 30/30, privacidade e RAM. |
| Limpeza temporária e restauração | 19 verificações de limpeza em fixtures; ABI e fluxo de restauração sintéticos aprovados; nenhuma manutenção real do Windows. |
| Layout e bandeja | 132 combinações resolução/aba, DPI 100/125/150/200%, 12 ciclos com HWND estável. Inclui percurso GUID/revisão entre seleção e worker e recusas de identidades inválidas, sem instalar. Tempo do ciclo no runner não mede o PC fraco. |
| MSIX do runner | Consulta real: status ok, apartment MTA, 32 pacotes observados e dois apps reconhecidos. Não reproduz o ambiente do usuário. |
| [Compilação Windows x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39620933932) | Publicação self-contained, ativação Win32, identificação e SHA256SUMS aprovados. |
| [Políticas e privacidade](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39620933962) | 14/14 políticas Python; 10/10 testes Node dos protótipos isolados. |

Artefato aprovado de teste:
`GerenciadorFinal-v5.13.11.25-594d9db5-TESTE-win-x64`, ID `12354259117`,
58.870.682 bytes, [disponível na build](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39620933932),
expiração indicada em 28/10/2026. SHA-256 do ZIP informado pelo Actions:
`9465b54e88fc36a83d3c35056a5198bad50cc91816391fdec576b8a71bb7385a`.
O sufixo `594d9db5` é o merge sintético testado; a fonte é `190d47d`.
O pacote continua **sem assinatura Authenticode**, sem aprovação AVG e sem release estável.

| Verificação executada localmente | Resultado / limite |
| --- | --- |
| Compilação cruzada aplicativo e projetos alterados, SDK 10.0.402 | Zero erros/avisos; não executa WinForms ou APIs Windows. |
| Comparação de versões | 18/18. |
| Catálogo/cache sintético | 14/14. |
| Fontes múltiplas sintéticas | 64/64. |
| Atualizador sintético Linux | 89/89; seis casos Authenticode nativo explicitamente não executados ali. |
| MSIX sintético Linux | 30/30; ativação nativa Linux indisponível e tratada, não aprova Windows. |
| Drivers sintéticos Linux | 53 verificações. |
| Resíduos `--missao` Linux | Aprovado; subconjunto, não substitui regressão Windows completa. |
| ABI/fluxo de restauração sintéticos | Nove verificações; não chama a API de restauração real. |
| Política estática | 14/14; 111 arquivos-fonte percorridos. |
| Protótipos Node | 10/10, sem rede/deploy. |
| Consulta RuckZuck HTTPS real neste ambiente | 631 entradas. |
| Consulta de fontes oficiais HTTPS real neste ambiente | 18 resultados; não significa 18 atualizações nos PCs. |
| NuGet Audit | Sem pacote vulnerável reportado nas fontes atuais; sem certificação do runtime. |

Baseline Windows `5e3ecd4`: 327/327 resíduos, 24/24 MSIX, 18/18 comparação,
14/14 catálogo, 64/64 multifontes, 30/30 encerramento; logs e três runs foram
consultados, não inferidos do título da PR. Baseline tinha `SYSLIB0057` no app e
três `CS8602` no projeto de layout. A matriz anterior validava 132 combinações
resolução/aba e 12 ciclos de bandeja no runner.

A primeira execução nova
[39617041483](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39617041483)
falhou no cenário negativo descrito acima. Testes posteriores ignorados nessa
execução não contam como aprovados. CI não instala apps de terceiros, não altera
drivers e não executa reparos reais; exclusões e ACLs de regressão são restritas
a fixtures aleatórias do runner.

### Comparação sintética de desempenho

Executado o benchmark existente, baseline `5e3ecd4` seguido pela candidata,
Linux/.NET 10.0.13, oito processadores lógicos expostos, três amostras por cenário.
Os limites e resultados lógicos foram idênticos, inclusive 20.000 evidências não
avaliadas no caso saturado. A ordem não foi alternada e houve grande variação de
tempo; os números **não demonstram ganho de desempenho** nem validam o PC fraco.

| Apps / evidências | Mediana baseline / candidata (ms) | Alocações medianas baseline / candidata (bytes) |
| --- | --- | --- |
| 500 / 5.000 | 246,93 / 78,00 | 24.641.544 / 24.641.544 |
| 1.000 / 10.000 | 223,29 / 157,00 | 49.224.648 / 49.224.608 |
| 1.000 / 20.000 | 191,42 / 265,98 | 94.238.032 / 97.437.864 |
| 1.000 / 50.000 | 334,24 / 213,95 | 140.938.040 / 140.938.040 |

A diferença de alocação no caso de 20.000 pede repetição isolada antes de
atribuir causa ou declarar regressão; métricas de processo incluem trabalho
concorrente. Não houve alteração dos limites para obter resultado melhor.

## Arquitetura atual e regras de evolução

| Domínio / componentes principais | Classificação atual |
| --- | --- |
| Registro, `LocalMsixInventory`, `InventoryReconciler`, `ApplicationIdentityCatalog`, `ActionableInventory`, `GameLibraryAudit` | Necessários; identidade/cobertura separadas de autorização. MSIX real ainda exige investigação. |
| `Matching`, `RuckZuckCatalog`, `OfficialReleaseSources`, receitas, guardas e coordenação de instalação | Necessários; fontes externas continuam sem autoridade de execução. Receitas sem hash revisado não são liberadas. |
| `Residual*`, `BroadResidualDiscovery`, auditorias históricas e catálogos de desinstalação | Necessários, consultivos. Não são uma implementação segura de remoção real. |
| `DriverInventory`, vínculos, histórico e apresentação | Necessários; modelo WMI parcial. Driver Store e histórico Windows completo não implementados. |
| `MemoryRam*`, `SafeMemoryOptimization`, `ProcessSamplingPolicy` | Necessários; buffers limitados e limpeza somente dos caches internos do GF. |
| `NetworkDiagnostics`, `SecurityCenterHealth`, diagnóstico e hardware | Necessários; resultados inconclusivos não autorizam reparos automáticos. |
| `SafeTemporaryCleanup`, `WindowsSystemTools`, `RestorePointService`, energia e worker | Necessários, exigem validação real supervisionada. Limpeza temporária separada da investigação de resíduos. |
| `AnonymousTelemetry`, payload, consentimento, log técnico e métricas | Necessários na candidata; log técnico e resumo opcional têm finalidades/retenções distintas. |
| Controles escuros, responsividade, sizing, chrome e acessibilidade | Necessários; preservar testes de navegação/DPI, sem redesenho amplo nesta missão. |
| `Program.cs`, `AdvancedFeatures`, `IntegratedSystemUi` | Necessários, mas ainda concentram responsabilidades. Próximas mudanças devem extrair fronteiras testáveis por função; não fazer reescrita geral. |
| Scripts de assinatura/instalação e Inno | Legado com uso declarado e dependência de certificado; não equivalem à futura SignPath. |
| Receptores de telemetria em `prototipos/` | Protótipo testável, excluído da execução do aplicativo; não é infraestrutura implantada. |
| Monitor de processos e helpers retirados | Redundantes comprovados por ausência de referências e substituição histórica. |

Regras verificáveis para próximos lotes:

1. Fontes e diagnósticos retornam dados e cobertura; só uma decisão explícita
   pode escolher ação. Identidade de catálogo não concede execução/exclusão.
2. Negar a ação quando inventário, identidade, assinatura, escopo ou consentimento
   forem indeterminados. Preservar resultados parciais sem fabricar completude.
3. Manter aplicativo `asInvoker`; worker recebe tarefa de allowlist e autentica
   o peer do pipe. Novos comandos não podem usar shell/PATH implicitamente.
4. Operação por arquivo exige identidade no handle efetivo, ancestrais estáveis
   e política de links. Verificação prévia por pathname não autoriza exclusão.
5. Em mudança de guard compartilhado, testar seus tipos de exceção em **todos os
   chamadores**, inclusive fallback/consentimento; o primeiro CI deste lote
   demonstrou por que essa regra é necessária.
6. Cada novo recurso tem dono e liberação em `finally`/`Dispose`; retenção por
   sessão deve ter limite declarado. Não compartilhar objetos COM entre threads
   sem respeitar a API; não mudar apartment por tentativa sem evidência.
7. Usar cancelamento/prazos e limites de entrada, profundidade e memória;
   indicar quando são cooperativos. Não prometer interrupção de chamada nativa.
8. Compartilhar fotografias imutáveis entre classificação e UI; não reaproveitar
   ausência de uma fonte indisponível como confirmação de remoção.
9. Esquemas de diagnóstico e contratos entre UI/worker usam codec comum, allowlist, limites e rejeição de campos extras;
   mudanças incompatíveis pedem versão/migração e teste de revogação/falha.
10. Manter testes negativos comportamentais e cenários perigosos sintéticos;
    aprovação no runner não habilita funcionalidades administrativas novas.
11. CI deve manter SHAs de actions, credenciais mínimas e separação de OIDC;
    aceitar novo workflow só com finalidade/cobertura e política revisadas.
12. Reversão ocorre por commit de correção/revert revisável; nunca apagar
    histórico, ocultar teste falho ou apresentar hipótese como correção provada.

## Pendências e limitações

| Pendência | Por que permanece / próximo limite de decisão |
| --- | --- |
| MSIX no PC fraco | Necessária nova coleta de fase/apartment na mesma máquina e sessão; não restaurar pacotes ou alterar permissões para mascarar leitura. |
| AVG e SmartScreen | Nenhuma certificação ou causa comprovada. Investigar o artefato por hash, origem e assinatura quando disponível; sem evasão, exclusões ou desativação. |
| Desempenho real | Não houve medição comparável nova de CPU/RAM/abertura nos PCs. A remoção de código morto não comprova menor consumo em runtime. |
| Criação de pastas locais | Validação de caminho local precede criação, mas um ancestral junction com sufixo inexistente pode gerar pasta vazia antes do pin recusar. Escrita de conteúdo é recusada. Criação segura por componentes precisa de projeto/testes próprios. |
| Leituras longas | Limites são cooperativos; COM/WMI ou I/O nativo bloqueado podem exceder prazos. `GetSubKeyNames` materializa arrays antes do orçamento em ramos excepcionalmente grandes. |
| Leitura consultiva de resíduos | Nem toda enumeração histórica prende todos os caminhos durante a leitura. Não usar esta descoberta para reativar exclusão. |
| Atualizações/desinstalação reais | Precisam de ambiente supervisionado; teste de assinatura/download não valida instalação, dados preservados ou versão final universal. VC++ sem hash continua bloqueado. |
| Drivers | Validar nomes/versões/dispositivos nos dois PCs antes de ampliar Driver Store ou gestão. Não há instalação, remoção ou rollback implementados nesta missão. |
| Governança da main | Consulta mostrou `protected=false` e nenhum ruleset. CODEOWNERS não impõe aprovação sozinho. Acesso administrativo detalhado retornou 403; nada foi alterado. Configuração do mantenedor permanece decisão separada. |
| Windows Update supervisionado | O contrato GUID/revisão foi corrigido por testes de integração sem instalação. EULA, confirmação e reconsulta da revisão permanecem exigidas; instalação real continua pendente de teste supervisionado. |
| Envio e manutenção por IA | Continuam planejados, sem cliente de envio/autenticação implantado ou agente acionado pelas Actions. Os workflows atuais executam verificações determinísticas; a presença de planos/protótipos não implementa essa função. |
| Artefatos e CI externo | Nenhuma exclusão remota autorizada; limpeza da árvore não libera automaticamente armazenamento. Jobs de rede/download opcionais só aprovam quando realmente executados. |
| Instalador assinado/SignPath | Sem certificado/aprovação disponíveis; não foi compilado ou publicado instalador assinado. |

## Roteiro consultivo da .25

1. Obter a build identificada por versão **e commit** na PR. Conferir
   `BUILD-INFO.txt` e `SHA256SUMS.txt`; manter runtime e arquivos adjacentes.
2. No PC fraco, abrir uma vez e consultar **Atualizar lista**. Registrar contagens,
   estado parcial, `msix_status`, `fase`, `apartment`, `repeticao` e HRESULT.
   Repetir a consulta na mesma sessão antes de encerrar. Não remover apps Store.
3. Abrir **Diagnóstico completo**: se a fotografia usada falhar em MSIX/ARP/jogos,
   o relatório precisa continuar parcial. Nova consulta bem-sucedida pode ter
   cobertura diferente; comparar os timestamps e a fonte de cada resultado.
4. Conferir DRIVERS: nomes antes vazios só devem ser preenchidos quando existe
   o mesmo ID PnP. Comparar amostra com o Gerenciador de Dispositivos; preservar
   as três associações sem INF e versões incomuns para análise.
5. Investigar resíduos sem excluir. Conferir manutenção de `InventoryPartial`,
   `ReadFailure`, `DepthLimit` e `LinkSkipped`; menos avisos não é objetivo por si.
6. Testar recusa/simulação/coleta local/revogação, leitura do log durante a sessão,
   fechamento normal, bandeja e tamanho da janela; não enviar logs automaticamente.
7. Repetir as consultas no PC principal, anotando resolução/DPI e contagens.
   Medir abertura, CPU e RAM em condições comparáveis. Não executar limpeza,
   reparos, desinstalações ou alterações de drivers para concluir esta bateria.

## Referências primárias

- [PackageManager: Agile e ThreadingModel.Both](https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.packagemanager?view=winrt-26100)
- [FindPackagesForUser](https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.packagemanager.findpackagesforuser)
- [STATEMGRSTATUS](https://learn.microsoft.com/en-us/windows/win32/api/srrestoreptapi/ns-srrestoreptapi-statemgrstatus) e [cabeçalho Microsoft com pack(1)](https://github.com/microsoft/win32metadata/blob/2f767671802301cf043c717d66bf004ed7863487/generation/WinSDK/RecompiledIdlHeaders/um/SrRestorePtApi.h)
- [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle)
- [WTHelperGetProvSignerFromChain](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-wthelpergetprovsignerfromchain)
- [Win32_PnPSignedDriver](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/whqlprov/win32-pnpsigneddriver) e [Win32_PnPEntity](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-pnpentity)
