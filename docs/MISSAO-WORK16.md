# WORK16 — expansão estrutural do ecossistema

Base: PR #51, `f179dbabe78d0d42c4112a283040c5c781f109f0`. Auditoria anterior à implementação: [WORK16-AUDITORIA.md](WORK16-AUDITORIA.md). Nenhuma PR anterior foi integrada à main. Candidata de código .61; CI/pacote ainda pendentes nesta revisão.

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

`PackageIdentityMatcher` combina ProductCode, UpgradeCode, PFN, identidade no gerenciador, fabricante, versão instalada, assinatura comprovada e contexto local. Nome é apenas uma dimensão. Evidências contraditórias prevalecem sobre semelhança. UpgradeCode sozinho não identifica instalação. A mesma identidade associada a duas entradas ARP não é vinculada automaticamente.

Níveis: `Unmatched` (sem associação), `Suggestion` (pista), `Corroborated` (nome/fabricante/versão/local), `Strong` (identificador estável e evidências independentes), `Ambiguous` (mais de uma instalação) e `Blocked` (contradição). Nenhum nível é um token de execução.

As fases operacionais são: descoberta dos provedores → fatos de identidade → associação única → elegibilidade por mecanismo → download/validação de payload → consentimento explícito → execução pelo contrato existente → confirmação de versão ou ausência no inventário. `IApplicationDiscoveryProvider` não recebe comando, shell, delegado de exclusão ou executor. As permissões efêmeras anteriores e `PreparedUninstaller` continuam separadas. Metadados consultivos não podem gerar uma permissão por cópia de DTO.

Provedores declaram capacidades/prioridade. O coordenador isola erros, cancelamento e prazo por fonte e oferece cache em memória de dois minutos, forçável. Leitura operacional/presença não usa cache como confirmação. O COM roda em filho próprio sem janela, saída JSON limitada e cancelável; o GF só pode encerrar esse leitor, nunca o servidor WinGet ou outro aplicativo. Ativação elevada é recusada, sem reduzir o nível de confiança do COM. Ausência/versão incompatível/política corporativa deixam apenas essa fonte indisponível. O WinGet nativo pode manter logs/cache próprios das APIs; não há ação de instalação, reparo, refresh de fonte ou alteração de configuração solicitada pelo GF.

Scoop/Chocolatey são inicialmente readers; a operação continua na ferramenta original. URLs de orientação são fixas. O botão Gerenciar não interpola IDs ou caminhos em comandos. A lista/dica e diagnóstico distinguem descoberta, plano silencioso, plano supervisionado, gerenciamento externo e bloqueios. Pacotes Chocolatey são apresentados como registros de pacote, separados do inventário ARP: somar ambos não mede novos programas únicos.

O SHA-1 lido em RELEASES Squirrel é verificação de consistência do formato legado, **não** uma raiz de autenticidade; a autorização depende também das assinaturas Windows válidas e da identidade local. Não há download nem execução de feed Squirrel. Updater genérico, Velopack, identidade divergente, assinatura ausente e escopo de máquina conservam as rotas anteriores.

## Medição e validação

`scripts/measure_work16.py` compila o **mesmo probe** contra a base imutável e a candidata em cópias descartáveis. Mede separadamente seleção de manifestos, consultas SQLite por identidade, leitura de pacotes e seleção de desinstalação Squirrel. A assinatura do último conjunto é explicitamente simulada. Os denominadores não podem ser somados como se fossem aplicativos reais.

A matriz `Work16Coverage.json` será publicada pela CI; números não são antecipados aqui. A suite de ecossistemas é portátil; as regressões de lançamento, Registro, pacote, UI e assinatura continuam Windows. Catálogo público e binários oficiais inspecionados são conjuntos próprios. Nenhum resultado sintético fecha I7/U8/R7/R4; roadmap preservado em 27/42.

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
