# Operação Faxina Final — revisão consolidada

**Índice histórico.** Para o estado atual, leia [WORK9 parte 2](MISSAO-WORK9-PARTE2.md), [roadmap](ROADMAP.md) e [roteiro vigente](TESTE-CONSOLIDADO.md). Os estados de versões abaixo valem somente para a etapa indicada.

## MISSÃO WORK 4 — v5.13.11.31 TESTE

21/10/2026 · Roadmap **26/42 (61,9%)** · PR #40 permanece Draft.
Código final desta etapa: `de6ee62d2da87cebe8d82933bc4f8bf5c0099634`.
CI do código final **APROVADA**: [regressões Windows](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39668933022),
[build x64](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39668933034) e
[política/privacidade](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39668933046).
Última mudança de produto: `51726d1`; suíte integrada final: `de6ee62`.
A atualização documental posterior não altera o executável escolhido abaixo.

### Resultado e limites

Implementadas quatro frentes funcionais: retenção MSIX em memória após falha,
fusão de atualizações vinculada ao snapshot correto e cache monotônico,
cobertura de resíduos por origem com enumeração protegida, e matriz consultiva
INF publicado ↔ Driver Store por SetupAPI. Diagnóstico opt-in agora contém
falhas de gravação e limita **todas** as tentativas, inclusive liga/desliga, a 160
por processo. O modal de resíduos é contido no monitor do proprietário após DPI.
Não há nova exclusão, instalação/remoção de driver, envio, dependência ou privilégio.

A leitura privada do Markdown, log .30 e imagem .30 foi realizada. Somente as
contagens sanitizadas abaixo entram no repositório; nenhum log bruto, caminho
pessoal, imagem do desktop ou ID PnP foi anexado. A captura .30 confirma
`Otimizar` em uma linha; esse botão não foi refeito.

A primeira validação Windows da nova API reprovou zero vínculos mapeados. A
instrumentação sem caminhos isolou a recusa: `SetupGetInfPublishedNameW` devolveu
o nome simples do INF no runner. A fronteira nativa agora aceita somente esse
nome exato, ancorado em Windows/INF; outro relativo, INF diferente, ADS, caminho
fora do Store e reparse continuam recusados. O teste de integração foi mantido:
na revisão `bdc277b`, 66 associações WMI, 45 vínculos confirmados, resultado
parcial pelos INFs ausentes. CI dessa revisão: [regressões 39667750464](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39667750464).

A revisão de consumidores do snapshot também encontrou `CurrentUserVersion` do
Windows Terminal verificando só `status=ok`. O novo helper exige **zero falhas**,
entrada atual, família conhecida e identidade auditada. Cinco testes adicionais
impedem confirmação por retenção, fonte parcial ou publisher de exibição falso.
Nenhuma instalação ocorreu; a correção foi feita antes da entrega .31.

**Não comprovado:** causa do HRESULT MSIX no PC FRACO; causa das nove associações
e três INFs adicionais do PC FORTE; causa de alertas AVG; ganho de desempenho real;
exclusividade de resíduos; compatibilidade e assinatura criptográfica de drivers.
A nova correlação SetupAPI é evidência de localização, nunca de segurança para
manutenção. D2 ainda não inclui histórico de instalação do Windows nem enumera
pacotes sem associação WMI. Nenhum checkpoint aberto foi marcado concluído.

### Baseline confirmado

- PR #40 aberto, Draft, base `main` em `9ac3dc1`; HEAD inicial `900051d`,
  implementação anterior .30 em `e0db292`. As três Actions de `900051d` estavam
  aprovadas: 39660291360, 39660291409 e 39660291496.
- PC FORTE .30: 174 entradas, 27 MSIX, 140 com rota de desinstalação, 34 sem rota;
  33 flags `InventoryOnly`. Logo uma entrada sem rota não possui essa flag;
  flag e possibilidade de ação são conceitos diferentes. Nove sem editor e 45
  sem versão não receberam metadados inventados.
- Partição de atualizações: **3 + 31 + 28 + 106 + 1 + 2 + 3 = 174**,
  respectivamente candidato, somente inventário, sem versão superior, sem
  catálogo, versão principal divergente, registro antigo e ambíguo. Os quatro
  incertos são os três ambíguos mais a divergência principal; não somá-los de novo.
  Oito fontes responderam, catálogo RuckZuck com 631 entradas. Uma atualização de
  software do Windows foi somente consultada.
- Drivers: 177 associações/177 IDs, 63 INFs, 20 compartilhados; três sem INF válido,
  dois sem presença determinada, uma assinatura WMI indeterminada. As nove
  associações a mais entre .28/.29 são observações adicionais, sem prova de
  instalação. Os 103 registros históricos sem correspondência não são removíveis.
- Resíduos: **213 + 611 + 43 = 867** grupos gerais classificados. Quarenta e quatro
  grupos/161 locais na união de auditorias não são o mesmo universo de 867 grupos.
  `origens=12` contabiliza raízes/visões visitadas na descoberta; `fontes=13` é a
  união de identificadores das auditorias, incluindo integrações e histórico.
  `3925` é o contador da descoberta; `4041` agrega o orçamento e outras auditorias,
  inclusive App Paths, que já debitava orçamento fora daquele contador. Não é
  evidência de duplicidade nem 116 candidatos ignorados. A .30 não permite
  reconstruir a distribuição exata desse delta: as novas métricas por origem
  permitirão essa conferência na .31.
- Parcialidade `EntryLimit + ReadFailure + DepthLimit + LinkSkipped`, com falhas
  pontuais em AppData e ProgramData. Velocidade não remove limites: duas camadas,
  2.500 entradas por raiz e 128 filhos por pasta. Diretórios no limite da segunda
  camada registram profundidade conservadoramente, mesmo sem investigar o próximo
  nível. ACL e reparse também preservam. Não aumentar orçamento para mascará-los.
- Abertura instrumentada 325 ms, inventário inicial 813 ms; atualizações 6,43 s
  inicialmente e 0,39 s na repetição. Cache de dez minutos e pooling explicam uma
  hipótese consistente, não uma atribuição causal de toda a latência. RAM de
  processo observada ~173,5 MB decimais; a métrica inicial não mede todas as
  consultas posteriores. Encerramento normal não comprova aprovação AVG.

### Mapa de dependências e barreiras

| Área | Entrada → componentes → saída | Barreira preservada |
| --- | --- | --- |
| Inventário | Registro + WinRT/MSIX + manifestos de jogos → `AppServices.ScanAllInstalledApps`, `LocalMsixSnapshot`, `InventoryReconciler` → UI e proteção de resíduos | Falha de fonte mantém parcial; entradas retidas não recebem receita |
| Atualizações | Snapshot de instalações → `Matching`, `RuckZuckCatalog`, `OfficialReleaseSources`, `MultiSourceMatcher` → candidatos | Identidade por referência; editor/versão/canal; catálogo não autoriza instalação |
| Instalação de apps | Seleção individual → `VerifiedUpdaters`, `InstallerFileGuard`, preflight → instalador oficial → releitura | Hash e assinatura, confirmação da mesma instalação; não executado nesta missão |
| Resíduos | Inventário + raízes permitidas → `BroadResidualDiscovery`, `ResidualScanBudget`, classificadores/merger → `ResidualResultIndex`, explorer/inspection | Guard de ancestrais, tetos/cancelamento, parcial preserva, `CanRemove=false` |
| Drivers | WMI PnP e associação assinada → `DriverInventory`, `DriverPackageLinks`, `DriverStoreAudit` → tabela e metadados locais | SetupAPI somente leitura, round trip exato; `CanMaintain=false`; nenhuma escrita |
| Histórico | Snapshot WMI completo → `DriverHistory` → hashes locais e diferenças | Parcial/vazio não substitui base; diferença não prova instalação/remoção |
| Diagnóstico | Opt-in manual → `TestDiagnosticSession`/allowlist → log local existente | Máximo acumulado de 160 tentativas; falha desativa; sem IDs, caminhos ou envio |
| Manutenção e entrega | UI/worker já existentes → tarefas em allowlist; três workflows → pacote x64 e hashes | Workers não ampliados; PR sem escrita/credenciais; main, atestação e release preservadas |

### Matriz P0–P3

Achado de implementação reproduzível é distinto de causa comprovada nos PCs.
Nenhum P0 novo foi identificado nesta revisão; isso não equivale a ausência
universal de vulnerabilidades. R4 e G1–G5 continuam contidos.

| Prioridade / natureza | Arquivo e método | Antes → depois / efeito | Evidência e risco residual |
| --- | --- | --- | --- |
| P1, falha latente comprovada | `OfficialReleaseSources.cs`, `MultiSourceMatcher.Merge` | Resultado de outra linha podia ser reaplicado se o chamador entregasse lista errada → recusa por identidade do snapshot; revalida somente inventário | Fixture antiga aceitava o vínculo errado; agora exige recusa. Geração da UI já protegia o fluxo normal; não atribuir aos três candidatos reais. Risco: recusa conservadora de resultado incoerente |
| P1, lacuna de proteção comprovada | `LocalMsixInventory.ReadCurrentUser`, `LocalMsixSnapshot.Read` | Falha após leitura válida eliminava a fotografia anterior da lista → retém apenas nesta sessão, sinaliza presença não confirmada e bloqueia receitas | Seis casos novos: falha, leitura parcial recente, base preservada, recuperação, processo sem base e vazio completo. Não corrige a causa do COM no PC FRACO |
| P1, regressão contida antes da entrega | `WindowsTerminalUpdater.CurrentUserVersion`, `LocalMsixInventory.ConfirmedMicrosoftVersion` | Fonte parcial com status ok e fotografia retida podiam ser usadas na confirmação → exige fonte completa, atual e identidade auditada | Cinco testes MSIX adicionais; nenhum instalador executado. Retenção continua apenas protetora |
| P1, robustez/privacidade comprovada | `TestDiagnosticDetail`, `TestDiagnosticSession.Write/SetProfile/Record` | Trocas de perfil geravam linhas fora do teto; sink podia lançar exceção → todas as tentativas limitadas; falha contém coleta | Churn de 500 trocas, teto 160, falhas/recusas e campos indevidos. Menos linhas úteis após muitas trocas é efeito intencional |
| P2, medição comprovada | `BroadResidualDiscovery.InspectDirectory/InspectRegistryHive` | Item além do teto debitava orçamento antes de ser examinado → contagem só de itens examinados | Fixture 150 filhos resulta em raiz + 128; orçamento e contador coincidem |
| P2, barreira defensiva | `BroadResidualDiscovery.InspectDirectory`, `InstallerFileGuard.PinDirectory` | Checagem prévia sem manter ancestrais presos durante enumeração → lease de raiz/filho até concluir leitura | Regressões de links, ACL, caminhos e guard existentes + nova fixture. Pode aumentar parcialidade se a trava for negada; não contorna ACL. Não comprova reprodução de toda corrida nativa |
| P2, diagnóstico de cobertura | `ResidualScanBudget`, `ResidualCoverage.Combine`, `ResidualResultIndex` | Somente flags globais → entradas/motivos por origem, fotografia copiada e fusão sem dupla soma | Testes de fonte distinta, subtotal, imutabilidade e cobertura agregada. Processos já consultados agora também debitam orçamento |
| P2, cobertura de drivers | `DriverStoreAudit.Resolve/CheckMapping/Capture` | WMI/INF não tinha vínculo verificável com Store → API Windows + conferência de volta ao INF exato | Fixtures adversariais de caminho/INF, limite/cancelamento/ACL e testes nativos no runner. Buffers limitados; falha fica indisponível. Sem história Windows, validação de catálogo/assinatura ou operação |
| P2, confiabilidade de cache | `RuckZuckCatalog.GetCatalogAsync`, `OfficialReleaseSources.GetVersionAsync` | TTL dependia de relógio civil → relógio monotônico; status cache/HTTPS explícito | Relógios controlados sem sleeps de TTL; vencimento aos dez minutos, concorrência e cancelamento. Rede real dos PCs segue pendente |
| P2, posicionamento | `ResidualExplorerDialog.OnShown/FitBounds` | Tamanho inicial baseado no monitor primário → limites físicos do monitor do owner após DPI | 24 combinações adicionais de geometria, inclusive coordenadas negativas, além da matriz UI. DPI físico 125% do PC FORTE ainda pendente |
| P3, organização | README, ROADMAP, TESTES, PROGRESSO, índice | Referências atuais ainda apontavam a .26/.29/.30 → ponto de entrada .31 e roteiro consolidado | Links/políticas em CI; histórico anterior preservado abaixo e no Git. Sem novos relatórios por versão |

### Roadmap: antes e depois, sem inflação

| Checkpoint | Antes → depois | Evidência / o que falta |
| --- | --- | --- |
| I7 | Aberto → aberto | Snapshot MSIX corrigido e fixtures; falta conferir inventário em ambos os PCs |
| R4 | Bloqueado → bloqueado | Nenhum botão de apagar; exclusividade, consentimento durável, revalidação e recuperação ainda não demonstrados |
| R6 | Aberto → aberto | Mais rastreabilidade e proteção; os casos históricos reais ainda não têm atribuição exclusiva comprovada |
| R7 | Aberto → aberto | Preservação sintética aprovada; falta desinstalação individual supervisionada nos dois PCs |
| U6 | Aberto → aberto | Partição real de 174 explicada, identidade/cache reforçados; falta referência WinGet equivalente no mesmo PC/horário |
| U8 | Aberto → aberto | Nenhuma instalação no PC realizada nesta missão; falta versão final confirmada e recuperação |
| P5 | Aberto → aberto | Métricas .30 disponíveis, CI não substitui três sessões comparáveis nos dois PCs |
| P6 | Aberto → aberto | AVG não reproduzido no runner; não há correção causal comprovada |
| V5 | Aberto → aberto | Matriz/geometry CI; ainda falta legibilidade e fluidez em 1440×900 e 2560×1440/125% reais |
| D2 | Parcial → parcial mais completo | Novo elo WMI ↔ INF publicado ↔ Store; histórico confiável do Windows e pacotes não associados permanecem pendentes |
| D4 | Aberto → aberto | Interpretação precisa ser confrontada localmente com Windows em ambos os PCs |
| G1 | Bloqueado → bloqueado, desenho abaixo | Sem provedor de fonte oficial/compatibilidade instalado |
| G2 | Bloqueado → bloqueado, desenho abaixo | `IsSigned` WMI e localização INF não são validação criptográfica |
| G3 | Bloqueado → bloqueado | Nenhum instalador de drivers ou API de escrita introduzido |
| G4 | Bloqueado → bloqueado | Sem rollback demonstrado em VM e recuperação de falha |
| G5 | Bloqueado → bloqueado | Depende de G1–G4 e consentimento específico; nenhum teste destrutivo em PC real |

### Preparação G1/G2 e decisões rejeitadas

O elo implementado é `DeviceId → INF WMI → INF publicado → Store`.
A expansão deverá consultar Hardware IDs/Compatible IDs, SO/build, arquitetura,
classe e pacote correspondente, com provedor oficial identificado por política,
sem inferir URL pelo nome. A avaliação de um candidato deverá exigir evidências
separadas de procedência, compatibilidade, SHA-256 de origem confiável e assinatura
criptográfica/catálogo aplicável. Todos os estados desconhecidos recusam a
operação. Nenhum desses dados autoriza escrita só por ter versão superior.

G3/G4 só poderão ser desenhados após esse contrato, backup/export oficial e ensaio
em VM/sistema descartável. A reversão depende de suporte real do dispositivo e
Windows; não prometer rollback universal. Rede, vídeo, chipset, storage, pacote
compartilhado e driver inbox devem ser cenários separados. Testar falha/energia e
recuperação fora dos PCs pessoais antes de propor teste supervisionado.

**Não implementado deliberadamente:** continuação automática da descoberta,
aumento de 128/2.500/30.000, mais profundidade ou busca de perfis privados.
Uma retomada por offset em enumeração mutável pode pular/repetir entradas e fingir
completude. A paginação em memória de resultados e a inspeção voluntária já
existentes continuam disponíveis, limitadas e canceláveis; uma continuação de
leitura exige cursor/snapshot consistente, teto acumulado, invalidação por mudança
e testes adversariais próprios. Não remover `PARCIAL` após inspecionar um local.

### Commits e regressões intermediárias

| Commit | Resultado |
| --- | --- |
| [a9338fa](https://github.com/walissonminel-1991/gerenciador-final/commit/a9338fa6dfa7cb3fb9d2fd0f1dea8267c045b253) | Retenção MSIX e versão .31 |
| [7328132](https://github.com/walissonminel-1991/gerenciador-final/commit/73281321380d63366729e4d6b969603144bc23f6) | Identidade de snapshot e cache monotônico |
| [490a47d](https://github.com/walissonminel-1991/gerenciador-final/commit/490a47d32b5f4188383f706f7469e749bc7378e1) | Cobertura, limites, guard e modal |
| [f729775](https://github.com/walissonminel-1991/gerenciador-final/commit/f729775c163e6d853a3b8234d9dde30350c35d2b) | Driver Store consultivo e diagnóstico limitado |
| [a59efe1](https://github.com/walissonminel-1991/gerenciador-final/commit/a59efe1deb0b26f767b72d7c39096c239d9a4689) | Processos no orçamento e normalização dos blobs |
| [8bde7ef](https://github.com/walissonminel-1991/gerenciador-final/commit/8bde7efb8621615ff9e0a4148878ea0dd8bc482c) / [245b43b](https://github.com/walissonminel-1991/gerenciador-final/commit/245b43b7697a462e735531aab7dfd2a161fd82a4) | Localização da falha nativa sem caminhos/IDs no diagnóstico |
| [bdc277b](https://github.com/walissonminel-1991/gerenciador-final/commit/bdc277bfd03cb535866b167ebf39949fc24583bb) | Nome publicado nativo exato; primeiro lote Windows integralmente aprovado |
| [51726d1](https://github.com/walissonminel-1991/gerenciador-final/commit/51726d128b1665382a9585b5c156c82523d458c1) | Consumidor operacional recusa MSIX parcial/retido e identidade não auditada |
| [de6ee62](https://github.com/walissonminel-1991/gerenciador-final/commit/de6ee62d2da87cebe8d82933bc4f8bf5c0099634) | Suíte de instaladores passa a compilar inventário/snapshot reais, removendo stub obsoleto |

As regressões Windows [39666245130](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39666245130),
[39666868542](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39666868542) e
[39667390621](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39667390621)
reprovaram a integração SetupAPI: primeiro zero vínculos, depois recusa isolada de
caminho. São tentativas da mesma investigação, não três causas diferentes nos PCs.
A assertiva de pelo menos um vínculo nativo válido foi mantida e aprovada após o
fix. A rodada [39668166740](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39668166740)
reprovou a compilação do stub antigo da suíte de instaladores depois da nova
confirmação MSIX. O stub foi removido e a suíte usa os componentes reais;
compilação local sem avisos e 89/89 testes portáveis (seis exigem Authenticode
Windows). Nenhuma dessas builds reprovadas foi entregue nesta missão.

Onze capturas sintéticas foram produzidas no lote aprovado `bdc277b`; revisão
visual direta de `Residuos-1280x720.png` e `Residuos-Preservacao-1440x900.png`
confirmou botões de navegação/fechamento, detalhes, rolagens e preservação visíveis.
Não é teste físico de DPI 125% no PC FORTE. O runner usa decoração de janela do
Windows Server; a aparência da barra de título não reproduz o desktop do usuário.

### Candidata aprovada e pacote conferido

| Verificação do código final `de6ee62` | Resultado observado |
| --- | --- |
| Compilação principal com analisadores | Sem avisos/erros |
| Comparação / catálogo / multifontes | 18/18 · 17/17 · 69/69 |
| Instaladores verificados, sem instalar | 95/95 |
| Resíduos e preservação | **360/360**, antes 351/351 |
| MSIX e confirmação de identidade | **41/41**, antes 30/30 |
| Drivers / diagnóstico opt-in | 95 assertivas OK contadas na etapa; WMI nativo 61 associações, 42 vínculos Store confirmados; matriz ainda parcial por INF ausente/inválido |
| UI e DPI sintético | 132 combinações resolução/aba, 24 novos casos de contenção do modal; suites de RAM, bandeja, foco, cancelamento e fechamento aprovadas |
| Encerramento de instaladores | 30/30; nenhum processo encerrado no ensaio |
| Política / receptor isolado | 14/14 políticas, 10/10 Node; privacidade/expiração/falhas de disco e demais etapas Windows aprovadas |
| Jobs opcionais / atestação main | Fontes/downloads reais opcionais não executados neste lote; atestação da main não executada |

[ZIP Windows x64 TESTE aprovado](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/39668933034/artifacts/12379295504),
**58.885.631 bytes**, `GerenciadorFinal-v5.13.11.31-8656bb1e-TESTE-win-x64`.
Retenção GitHub: até **28/10/2026, 17:17:50 (Fortaleza)**, sete dias.
Runtime incluído; não requer SDK no computador do usuário.

ZIP baixado para inspeção, sem executar: BUILD-INFO confirmou versão **5.13.11.31**,
fonte **de6ee62d2da87cebe8d82933bc4f8bf5c0099634**, merge sintético
**8656bb1edc3103e4df33dd2fe210456f13e6b219**, base **9ac3dc1**, execução
**39668933034**. Os **273 arquivos** do manifesto foram conferidos, conjunto exato
sem omissões/duplicações; total 274 entradas incluindo SHA256SUMS. Presença de
EXE, DLL e runtime verificada. Atestação/Authenticode continuam ausentes;
assinatura informada no BUILD-INFO: AUSENTE; AVG: NÃO VALIDADO.

| Arquivo | SHA-256 |
| --- | --- |
| ZIP | `ed4b61056e5690c388674d082b5c7f190659adda1fb1b6fa3caa4ee7b23941fd` |
| EXE | `26a57b007db4a842b7657559fa93ccb8434c75afec956a53a6e4d3dbc0cb8dc5` |
| DLL | `08d7b69e9cb856b651c2af91dddff01ed95786ca503bae82800c9ee26a2320e7` |

### Validação e fila seguinte

Validações locais: build principal/layout sem avisos, comparação 18/18,
catálogo 17/17, multifontes 69/69, MSIX 41/41 e testes de drivers/privacidade.
As novas fixtures de resíduos passaram localmente; a suíte integral aborta no
Linux em uma fixture preexistente de bibliotecas Steam com caminhos Windows.
Isso não é aprovação da suíte: o runner Windows é a referência obrigatória.
Um erro de compilação inicial da fixture nova (`CanRemove` é do achado,
não do relatório) foi corrigido antes de enviar à CI. A expectativa insegura de
snapshot trocado foi substituída por uma assertiva de recusa, sem retirar proteção.
Os blobs de fontes enviados pela API foram normalizados em commit explícito
conforme `.gitattributes`; sem force-push ou reescrita de histórico.

- **Sem PC:** validar a continuação consultiva consistente, projetar parser
  próprio de histórico Windows com limites/redação antes de habilitar leitura,
  ampliar fixtures de identidade/canais e revisar catálogos somente com prova.
- **PC FORTE:** executar primeiro [bateria consultiva atual](TESTES.md); resolver
  amostras ambíguas reais antes de operações individuais de aplicativos.
- **PC FRACO:** repetir inventário MSIX na mesma sessão, recuperação e UI/CPU/RAM;
  nenhum reparo/reinstalação de Windows para esconder 0x80040154.
- **Ambiente descartável:** desenho e testes futuros de aquisição, integridade,
  instalação/reversão de driver, falha de boot/rede e recuperação. Fora desta build.

Fontes primárias da implementação nativa:
[SetupGetInfDriverStoreLocationW](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupgetinfdriverstorelocationw)
e [SetupGetInfPublishedNameW](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupgetinfpublishednamew).
Implementação própria, sem cópia de projetos terceiros ou nova dependência.

**Decisão de segurança: exclusão de resíduos e manutenção de drivers continuam
BLOQUEADAS.** CI verde somente habilita distribuição experimental para a bateria
consultiva; não comprova segurança universal, eficácia, assinatura ou aprovação AVG.

## Histórico preservado — revisão e incorporação até v5.13.11.26

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
