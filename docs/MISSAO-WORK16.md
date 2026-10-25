# WORK16 — expansão estrutural do ecossistema

Base: PR #51, `f179dbabe78d0d42c4112a283040c5c781f109f0`. Auditoria anterior à implementação: [WORK16-AUDITORIA.md](WORK16-AUDITORIA.md). Nenhuma PR anterior foi integrada à main. Candidata .61 aprovada na [CI 40184303904](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40184303904), fonte `cdc6de0d58d69807841fd38beacbcd8ad4461ea4` e build PR `f549eb1fbc167f282dba441fb93cff9f87877dab`. [Portátil x64 TESTE selecionado](https://github.com/walissonminel-1991/gerenciador-final/actions/runs/40184303904/artifacts/12596105758).

## Implementação funcional

| Origem/mecanismo | Descoberta e identidade | Operações do GF |
|---|---|---|
| WinGet COM oficial | Interop Microsoft 1.29.380; catálogo local instalado e tracking já existente, sem parsing CLI | Somente consulta; nunca instala/repara WinGet, aceita termos de fontes ou chama Install/Upgrade/Uninstall |
| Índice Microsoft autenticado | Novas consultas por PackageFamilyName MSIX e PackageIdentifier observado no tracking e corroborado com a instalação | Consultivas; não emitem receita executável |
| Scoop usuário/global | Metadados legados e prefixados; junction `current` limitada à versão do mesmo pacote; arquitetura e bucket oficial já clonado | Inventário e versão do bucket local recente; gerenciar na ferramenta original, sem executar scripts |
| Chocolatey | ID/versão coerentes entre nuspec e nupkg locais | Registro de **pacote**, que pode ser metapacote ou resto de instalação; não comprova presença do produto nem autoriza upgrade que instalaria um ausente |
| MSI/WiX, NSIS, Inno | Contratos e verificações anteriores preservados; manifestos com modo exclusivamente interativo ganham plano supervisionado | Payload autenticado, hash, assinaturas, produto, escopo, arquitetura, instalação única e versão posterior continuam obrigatórios |
| ARM64 | Identidade PE e Template Summary MSI `Arm64`; não confunde com x64/x86 | Exige ARM64 no Windows local e manifesto da mesma arquitetura; sem homologação física ARM |
| Squirrel.Windows | Updater .NET com tipo `Squirrel.Update.Program`, registro exato per-user, binário/versão, RELEASES e nuspec locais coerentes | Novo `--uninstall --silent` somente com assinatura válida igual entre updater e aplicativo e fabricante correspondente; exclusividade revalidada antes da execução |
| MSIX/Store genérico | PFN permite encontrar versão mesmo com título localizado | Gerenciamento pelo Windows; sem remoção genérica, all-users, frameworks ou dependências |
| Burn/bootstrapper e portáteis próprios | Identificados somente quando há metadados suficientes nas fontes existentes | Sem nova autorização genérica: cadeia, upgrade identity e comportamento do bootstrapper não foram comprovados |

## Fronteiras e confiança

`PackageIdentityMatcher` combina ProductCode, UpgradeCode, PFN, identidade no gerenciador, fabricante, versão instalada, assinatura comprovada e contexto local. Nome é apenas uma dimensão. Evidências contraditórias prevalecem sobre semelhança. UpgradeCode sozinho não identifica instalação. A mesma identidade associada a duas entradas ARP não é vinculada automaticamente. Ambiguidade também impede nova emissão e invalida uma permissão de atualização já emitida; a indicação chega à matriz de capacidades. Não é possível contornar isso copiando receita ou repetindo a consulta.

Níveis: `Unmatched` (sem associação), `Suggestion` (pista), `Corroborated` (nome/fabricante/versão/local), `Strong` (identificador estável e evidências independentes), `Ambiguous` (mais de uma instalação) e `Blocked` (contradição). Nenhum nível é um token de execução.

As fases operacionais são: descoberta dos provedores → fatos de identidade → associação única → elegibilidade por mecanismo → download/validação de payload → consentimento explícito → execução pelo contrato existente → confirmação de versão ou ausência no inventário. `IApplicationDiscoveryProvider` não recebe comando, shell, delegado de exclusão ou executor. As permissões efêmeras anteriores e `PreparedUninstaller` continuam separadas. Metadados consultivos não podem gerar uma permissão por cópia de DTO.

Provedores declaram capacidades/prioridade. O coordenador isola erros, cancelamento e prazo por fonte e oferece cache em memória de dois minutos, forçável. Leitura operacional/presença não usa cache como confirmação. O COM roda em filho próprio sem janela, saída JSON limitada e cancelável; o GF só pode encerrar esse leitor, nunca o servidor WinGet ou outro aplicativo. Ativação elevada é recusada, sem reduzir o nível de confiança do COM. Ausência/versão incompatível/política corporativa deixam apenas essa fonte indisponível. O WinGet nativo pode manter logs/cache próprios das APIs; não há ação de instalação, reparo, refresh de fonte ou alteração de configuração solicitada pelo GF.

Scoop/Chocolatey são inicialmente readers; a operação continua na ferramenta original. URLs de orientação são fixas. O botão Gerenciar não interpola IDs ou caminhos em comandos. A lista/dica e diagnóstico distinguem descoberta, plano silencioso, plano supervisionado, gerenciamento externo e bloqueios. Pacotes Chocolatey são apresentados como registros de pacote, separados do inventário ARP: somar ambos não mede novos programas únicos.

O SHA-1 lido em RELEASES Squirrel é verificação de consistência do formato legado, **não** uma raiz de autenticidade; a autorização depende também das assinaturas Windows válidas e da identidade local. Não há download nem execução de feed Squirrel. Updater genérico, Velopack, identidade divergente, assinatura ausente e escopo de máquina conservam as rotas anteriores.

## Medição e validação

`scripts/measure_work16.py` compila o **mesmo probe** contra a base imutável e a candidata em cópias descartáveis. Mede separadamente seleção de manifestos, consultas SQLite por identidade, leitura de pacotes e seleção de desinstalação Squirrel. A assinatura do último conjunto é explicitamente simulada. Os denominadores não podem ser somados como se fossem aplicativos reais.

A [matriz reproduzida contra #51](benchmarks/WORK16-comparacao.json) foi obtida na CI Windows, em subamostras distintas. Não somar os denominadores como programas físicos:

| Indicador de componente | Antes | Depois | Denominador / limite |
|---|---:|---:|---|
| Registros Scoop/Chocolatey inventariados | 0/2 | 2/2 | dois registros locais sintéticos; pacote Chocolatey não comprova produto |
| Identidade/versão consultável por PFN | 0/1 | 1/1 | mesmo índice SQLite descartável, título independente |
| Consulta por PackageIdentifier observado | 0/1 | 1/1 | consulta consultiva no índice; não autoriza instalação |
| Contratos de atualização silenciosa selecionados | 3/6 | 3/6 | mesmos seis manifestos; validação final do payload ainda necessária |
| Contratos de atualização supervisionada selecionados | 0/6 | 3/6 | os outros três manifestos, com InstallModes interativo explícito |
| Desinstalação Squirrel silenciosa selecionada | 0/1 | 1/1 | mesma instalação sintética e assinante **simulado** |
| Desinstalação Squirrel interativa | 1/1 | 0/1 | migração de modo; não é um segundo programa novo |
| Gerenciamento externo de pacotes | indisponível | 2 entradas integradas | dois registros acima, fluxo de inventário/capacidades validado no produto |
| Controles negativos de manifestos recusados | 6/6 | 6/6 | arquitetura, escopo, motor, comandos, dependência e migração |
| Inferências Squirrel indevidas recusadas | 6/6 | 6/6 | não equivale a bloquear a rota interativa registrada |
| Payloads finais autorizados nessa amostra | 0 | 0 | nenhum executado ou homologado |

Os totais de aplicativos inventariados, identidades, atualizações identificadas, operações elegíveis, ambiguidades e bloqueios **nos PCs** não foram medidos nesta missão. O GF agora os registra em contadores separados por fotografia; comparação física deve usar o mesmo computador e denominador. A amostra de seleção de manifestos não substitui a elegibilidade completa depois de hash/assinatura/contexto. Os 27 identificadores estáticos e as 7 políticas residuais existentes não foram inflados.

Conjuntos reais separados, sem execução: catálogo Microsoft assinado e manifestos OBSProject.OBSStudio 33.0.0, VideoLAN.VLC 3.0.24, ShareX.ShareX 21.0.0 e SumatraPDF.SumatraPDF; nova consulta por PFN/PackageIdentifier Microsoft.WindowsTerminal validada no índice real. VLC teve motor NSIS e assinatura VideoLAN confirmados. ShareX teve hash/motor Inno confirmados, mas ausência de assinatura manteve a execução bloqueada. Isso não homologa uma instalação real desses produtos nem o novo contrato Squirrel.

O leitor COM real foi iniciado pelo teste de protocolo no runner: retornou `contexto_elevado_recusado`. Não foi feita ativação de registro per-user com confiança inferior, instalação/reparo de WinGet ou alteração de privilégios. Interop compilado e protocolo exercitado; **consulta nativa em usuário comum ainda precisa de homologação**. A suite de ecossistemas é portátil; as regressões de lançamento, Registro, pacote, UI e assinatura continuam Windows. Catálogo público e binários oficiais inspecionados são conjuntos próprios. Nenhum resultado sintético fecha I7/U8/R7/R4; roadmap preservado em 27/42.

## Roteiro físico mínimo

PC FORTE, sem elevação inicial e com AVG ativo:

1. Usar somente o pacote selecionado após CI e conferir seu SHA. Executar Atualizar e verificar, observar contagens de versões descobertas, contratos silenciosos e supervisionados. Uma consulta COM indisponível não deve impedir as fontes HTTPS.
2. Se Scoop/Chocolatey já existirem, comparar com seus pacotes efetivamente registrados. Não instalar gerenciadores apenas para o teste. Conferir que um pacote sem produto ARP não ganha botão Desinstalar; Gerenciar abre orientação oficial.
3. Para uma atualização já necessária, executar somente o contrato apresentado e consentido. Validar versão, caminho, edição, progresso, cancelamento antes do início e resultado posterior. Se supervisionada, manter diretório/escopo existentes; alterar opções pode impedir confirmação e não recebe sucesso presumido.
4. Só se houver remoção Squirrel já desejada e elegível, consentir e verificar ausência repetida no inventário. A limpeza residual mantém a política própria e SIM/NÃO, sem estender o escopo pelo formato do desinstalador.
5. Reavaliar a issue #48 no ciclo residual real, sem desativar antivírus, alterar ACL ou forçar reinício.

PC FRACO, mesmo pacote/hash:

1. Conferir abertura, Atualizar e verificar, cancelamento e navegação em 1440×900 e 1280×720. Uma fonte lenta não deve travar a interface.
2. Conferir origem/capacidade nas dicas e botões sem sobreposição. Comparar contagens com o inventário desse PC, sem reutilizar denominador do PC FORTE.
3. Somente uma operação já necessária e autorizada; conferir progresso, resultado e preservação de configurações. U8/R7/R4 físicos continuam pendentes até evidências de ambos os computadores.

## Bloqueios restantes

WinGet executável via COM, scripts Chocolatey/Scoop, Burn genérico, feeds de portáteis, Squirrel update e remoção MSIX genérica precisam de revisão por contexto, autorização e confirmação próprias. APIs ausentes ou bloqueadas não são reparadas automaticamente. Licenças originais do interop Microsoft são incluídas no pacote, sem distribuir o serviço App Installer. Assinatura GF e avaliação AVG permanecem pendentes.


## Encerramento e pacote selecionado

CI de produção 40184303904 aprovada: compilação/analisadores sem erros/avisos; 42 controles de ecossistemas, 368 atualizadores, 697 resíduos/desinstalação, 30/30 mutações, 159 multifontes, 17 catálogo, 47 MSIX, 30 encerramento seguro, 35 Python e 10 Node. Integração UI de dois recibos de gerenciadores e MSIX genérico aprovada; regressões de resoluções, progresso e privacidade preservadas. Fontes/drivers opcionais e atestação exclusiva da main ficaram skipped conforme as condições existentes, sem remover verificações obrigatórias.

O primeiro ensaio encontrou um alvo textual obsoleto do teste de mutação depois da extensão ARM64. O alvo foi atualizado para a nova comparação; a mutação continua retirando a proteção real e foi detectada. A revisão também corrigiu o elo PFN → inventário genérico → consulta, preservou as rotas Store conhecidas, manteve escopo Chocolatey desconhecido e propagou ambiguidade até a autorização/revalidação. O fechamento documental posterior preserva o código/pacote selecionados.

`GerenciadorFinal-v5.13.11.61-cdc6de0d-TESTE-win-x64.zip`: **59.407.693 bytes**, 285 arquivos, 284 hashes exatos, **152.888.981 bytes extraídos**. SHA-256 da API e do download independente:

`f3672cb5ad8cfa38c335a84d0c4c8b97b60311c1806dd0a9169fd5452b909f3e`

Conferência independente também validou origem, AMD64, .NET/WindowsDesktop 10.0.13 self-contained, FileVersion .61 e RT_MANIFEST id 1 com `asInvoker`. O aumento perante .60 é 220.938 bytes comprimidos e 1.018.253 bytes extraídos; as duas DLLs do interop e NOTICE original foram incluídas e abrangidas pelos hashes. GF sem assinatura, AVG pendente. Artefato expira em **01/11/2026 10:28 UTC (07:28 de Brasília)**.

A PR #52 permanece Draft sobre #51; main preservada, sem merge ou release estável. Catálogo estático permanece com 27 identidades e políticas residuais explícitas 7/27; roadmap **27/42**. U8/R7/R4 e consulta nativa WinGet em usuário comum continuam pendentes. Não houve instalação, atualização, desinstalação ou limpeza nos PCs pessoais. Famílias contempladas não equivalem a compatibilidade universal: Squirrel exige as provas locais, Scoop/Chocolatey dependem de metadados coerentes existentes, e versões encontradas por PFN continuam consultivas.
